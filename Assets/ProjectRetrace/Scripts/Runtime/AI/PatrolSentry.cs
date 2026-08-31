using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectRetrace
{
    public enum SentryState
    {
        Inactive,
        Patrolling,
        Looking,
        Chasing
    }

    /// <summary>
    /// The phase-2 antagonist: walks the player's recorded phase-1 route via the navmesh, in
    /// the same direction the player walked it, pausing to look around wherever the player
    /// stopped. Its pace and pause lengths are deliberately its own, never the recording's:
    /// replaying the player's timing would let them idle in a corner during phase 1 to buy an
    /// easy phase 2.
    ///
    /// Spotting is instant-loss. The chase that follows only sells the catch -- the outcome
    /// is decided (and player input frozen) the moment the cone turns red, and a hard time
    /// cap ends the run even if the agent's path to the player fails.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public class PatrolSentry : MonoBehaviour
    {
        private const float EyeHeight = 1.6f;

        /// <summary>Path distance along the route the sentry starts at, so it never begins on
        /// top of the player -- crumb 0 and the phase-2 spawn are the same place.</summary>
        private const float HeadStartMetres = 5f;

        private const float WaypointReachedDistance = 0.6f;
        private const float ChaseCapSeconds = 2.5f;
        private const float LookSweepDegrees = 45f;
        private const float LookTurnDegreesPerSecond = 120f;

        /// <summary>The drawn cone is a facing indicator, not a range ruler -- at full
        /// visionRange it would carpet whole rooms and read as noise.</summary>
        private const float ConeVisualMetres = 3f;

        private static readonly float[] SampleHeights = { 1.6f, 1.0f };

        public FirstPersonController player;
        public BreadcrumbTrail trail;
        public RetraceSettings settings;

        private RetraceSettings _fallbackSettings;
        private NavMeshAgent _agent;
        private readonly Dictionary<int, DwellPoint> _dwellByCrumb = new Dictionary<int, DwellPoint>();
        private int _targetIndex;
        private bool _lookedAtTarget;
        private float _lookTimer;
        private float _lookYaw;
        private float _graceUntil;
        private float _chaseDeadline;
        private MeshFilter _coneFilter;
        private Renderer _coneRenderer;
        private Material _coneMaterial;

        public SentryState State { get; private set; }
        public int TargetIndex => _targetIndex;

        public RetraceSettings EffectiveSettings
        {
            get
            {
                if (settings != null) return settings;
                if (_fallbackSettings == null) _fallbackSettings = RetraceSettings.CreateDefault();
                return _fallbackSettings;
            }
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            BuildConeVisual();
        }

        private void OnDestroy()
        {
            if (_coneMaterial != null) Destroy(_coneMaterial);
            if (_coneFilter != null && _coneFilter.sharedMesh != null) Destroy(_coneFilter.sharedMesh);
        }

        /// <summary>Called by GameDirector when the stealth phase starts.</summary>
        public void BeginPatrol()
        {
            var crumbs = trail != null ? trail.Phase1Crumbs : null;
            if (crumbs == null || crumbs.Count < 2)
            {
                Debug.LogWarning("[PatrolSentry] No recorded route to patrol -- staying inactive.", this);
                return;
            }

            gameObject.SetActive(true);

            var config = EffectiveSettings;
            _agent.speed = config.sentrySpeed;
            _agent.angularSpeed = 360f;
            _agent.acceleration = 20f;
            _agent.autoBraking = false;
            _agent.stoppingDistance = 0f;

            _dwellByCrumb.Clear();
            foreach (var dwell in trail.DwellPoints)
            {
                _dwellByCrumb[Mathf.Max(0, dwell.CrumbIndex)] = dwell;
            }

            _targetIndex = StartIndex(crumbs);
            _agent.Warp(crumbs[_targetIndex].Position);
            transform.rotation = Quaternion.LookRotation(crumbs[_targetIndex].Direction, Vector3.up);
            _lookedAtTarget = true;
            AdvanceWaypoint(crumbs);

            _graceUntil = Time.time + config.graceSeconds;
            RebuildConeMesh(config);
            SetConeAlarmed(false);
            State = SentryState.Patrolling;
        }

        /// <summary>Called by GameDirector on run end and restart.</summary>
        public void StopPatrol()
        {
            State = SentryState.Inactive;
            if (_agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
            }

            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (State == SentryState.Inactive) return;

            if (State == SentryState.Chasing)
            {
                UpdateChase();
                return;
            }

            if (State == SentryState.Looking)
            {
                UpdateLook();
            }
            else
            {
                UpdateWalk();
            }

            TrySpotPlayer();
        }

        private void UpdateWalk()
        {
            if (_agent.pathPending || _agent.remainingDistance >= WaypointReachedDistance) return;

            var crumbs = trail.Phase1Crumbs;
            if (!_lookedAtTarget && _dwellByCrumb.TryGetValue(_targetIndex, out var dwell))
            {
                _lookedAtTarget = true;
                BeginLook(dwell);
                return;
            }

            AdvanceWaypoint(crumbs);
        }

        private void BeginLook(DwellPoint dwell)
        {
            State = SentryState.Looking;
            _agent.isStopped = true;
            _agent.updateRotation = false;
            _lookTimer = 0f;
            _lookYaw = dwell.FacingYaw;
        }

        private void UpdateLook()
        {
            _lookTimer += Time.deltaTime;
            var config = EffectiveSettings;

            // One full sweep across the recorded facing over the fixed pause, so the stop
            // scans the area the player was interested in rather than freezing in place.
            var sweep = Mathf.Sin(_lookTimer / config.lookAroundSeconds * Mathf.PI * 2f) * LookSweepDegrees;
            var target = Quaternion.Euler(0f, _lookYaw + sweep, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, LookTurnDegreesPerSecond * Time.deltaTime);

            if (_lookTimer < config.lookAroundSeconds) return;

            State = SentryState.Patrolling;
            _agent.updateRotation = true;
            _agent.isStopped = false;
            AdvanceWaypoint(trail.Phase1Crumbs);
        }

        /// <summary>
        /// Always forward, wrapping to crumb 0 after the last: the return leg cuts directly
        /// across the house, which reads as the sentry heading back to the trail head rather
        /// than moonwalking the route in reverse.
        /// </summary>
        private void AdvanceWaypoint(IReadOnlyList<Breadcrumb> crumbs)
        {
            _targetIndex = (_targetIndex + 1) % crumbs.Count;
            _lookedAtTarget = false;
            _agent.SetDestination(crumbs[_targetIndex].Position);
        }

        private void TrySpotPlayer()
        {
            if (player == null || Time.time < _graceUntil) return;

            var config = EffectiveSettings;
            var eye = transform.position + Vector3.up * EyeHeight;
            var forward = Flatten(transform.forward);

            // Head and chest samples: peeking over furniture exposes the head first, and the
            // chest catches a player whose head alone is tucked behind something thin.
            for (var i = 0; i < SampleHeights.Length; i++)
            {
                var target = player.transform.position + Vector3.up * SampleHeights[i];
                var toTarget = target - eye;
                if (toTarget.magnitude > config.visionRange) continue;
                if (Vector3.Angle(forward, Flatten(toTarget)) > config.visionAngle * 0.5f) continue;
                if (!HasLineOfSight(eye, target)) continue;

                OnPlayerSeen();
                return;
            }
        }

        private bool HasLineOfSight(Vector3 eye, Vector3 target)
        {
            var toTarget = target - eye;
            var ray = new Ray(eye, toTarget.normalized);
            var hits = Physics.RaycastAll(ray, toTarget.magnitude, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (var i = 0; i < hits.Length; i++)
            {
                var root = hits[i].collider.transform.root;
                if (root == transform.root) continue;
                return root == player.transform.root;
            }

            // Nothing between the eye and the sample point at all.
            return true;
        }

        private void OnPlayerSeen()
        {
            State = SentryState.Chasing;
            _chaseDeadline = Time.time + ChaseCapSeconds;
            _agent.updateRotation = true;
            _agent.isStopped = false;
            _agent.speed = EffectiveSettings.chaseSpeed;
            SetConeAlarmed(true);

            if (GameDirector.Instance != null) GameDirector.Instance.OnPlayerSpotted();
        }

        private void UpdateChase()
        {
            if (player == null) return;

            _agent.SetDestination(player.transform.position);

            var gap = Flatten(player.transform.position - transform.position, normalize: false);
            if (gap.magnitude <= EffectiveSettings.catchDistance || Time.time >= _chaseDeadline)
            {
                if (GameDirector.Instance != null) GameDirector.Instance.OnPlayerCaught();
            }
        }

        /// <summary>Route distance from crumb 0 decides the spawn crumb, giving the player a
        /// fixed head start however densely the crumbs were dropped.</summary>
        private static int StartIndex(IReadOnlyList<Breadcrumb> crumbs)
        {
            var travelled = 0f;
            for (var i = 1; i < crumbs.Count; i++)
            {
                travelled += Vector3.Distance(crumbs[i - 1].Position, crumbs[i].Position);
                if (travelled >= HeadStartMetres) return i;
            }

            return crumbs.Count - 1;
        }

        private static Vector3 Flatten(Vector3 vector, bool normalize = true)
        {
            vector.y = 0f;
            if (!normalize) return vector;
            return vector.sqrMagnitude > 0.0001f ? vector.normalized : Vector3.forward;
        }

        /// <summary>
        /// A flat fan on the floor showing where the sentry is looking. In a primitives-only
        /// game this is the player's only readable tell, so it is always on during stealth.
        /// </summary>
        private void BuildConeVisual()
        {
            var cone = new GameObject("VisionCone");
            cone.transform.SetParent(transform, false);
            cone.transform.localPosition = new Vector3(0f, 0.07f, 0f);

            _coneFilter = cone.AddComponent<MeshFilter>();
            _coneRenderer = cone.AddComponent<MeshRenderer>();
            _coneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _coneRenderer.receiveShadows = false;
            _coneMaterial = TrailVisualizer.CreateUnlitMaterial(Color.white);
            _coneRenderer.sharedMaterial = _coneMaterial;
        }

        private void RebuildConeMesh(RetraceSettings config)
        {
            if (_coneFilter.sharedMesh != null) Destroy(_coneFilter.sharedMesh);

            var radius = Mathf.Min(ConeVisualMetres, config.visionRange);
            var halfAngle = config.visionAngle * 0.5f;
            const int segments = 12;

            var mesh = new Mesh { name = "VisionCone" };
            var vertices = new Vector3[segments + 2];
            vertices[0] = Vector3.zero;
            for (var i = 0; i <= segments; i++)
            {
                var angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
            }

            var triangles = new int[segments * 3];
            for (var i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            var normals = new Vector3[vertices.Length];
            for (var i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            _coneFilter.sharedMesh = mesh;
        }

        private void SetConeAlarmed(bool alarmed)
        {
            var color = alarmed ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.85f, 0.3f, 1f);
            if (_coneMaterial.HasProperty("_BaseColor")) _coneMaterial.SetColor("_BaseColor", color);
            if (_coneMaterial.HasProperty("_Color")) _coneMaterial.SetColor("_Color", color);
        }
    }
}

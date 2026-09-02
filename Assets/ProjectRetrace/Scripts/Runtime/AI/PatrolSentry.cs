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
        /// <summary>Standing at the route's end, fading out, about to restart.</summary>
        Waiting,
        /// <summary>Just (re)spawned at the route start: frozen and blind until fully faded in.</summary>
        Materializing,
        Chasing
    }

    /// <summary>
    /// A stealth-phase antagonist: walks a recorded route of the player's via the navmesh, in
    /// the same direction the player walked it, pausing to look around wherever the player
    /// stopped. Which route is the director's business -- round 2's sentry retraces the
    /// search, round 3 adds a second sentry retracing round 2's sneak. Its pace and pause
    /// lengths are deliberately its own, never the recording's: replaying the player's timing
    /// would let them idle in a corner to buy an easy next round.
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
        private const float WaypointReachedDistance = 0.6f;
        private const int ConeSegments = 24;

        private static readonly float[] SampleHeights = { 1.6f, 1.0f };

        public FirstPersonController player;

        [Tooltip("Body colour, so two sentries on different routes read as two characters.")]
        public Color bodyTint = Color.white;

        [Tooltip("Played once at the moment of detection.")]
        public AudioClip spottedClip;

        private NavMeshAgent _agent;
        private IReadOnlyList<Breadcrumb> _route;
        private readonly Dictionary<int, DwellPoint> _dwellByCrumb = new Dictionary<int, DwellPoint>();
        private int _targetIndex;
        private bool _lookedAtTarget;
        private float _lookTimer;
        private float _lookYaw;
        private float _graceUntil;
        private float _chaseDeadline;
        private float _restartAt;
        private MeshFilter _coneFilter;
        private Renderer _coneRenderer;
        private Material _coneMaterial;
        private Material _bodyMaterial;
        private Mesh _coneMesh;
        private Vector3[] _coneVertices;
        private Color _coneColor = new Color(1f, 0.85f, 0.3f);
        private float _alpha = 1f;

        public SentryState State { get; private set; }
        public int TargetIndex => _targetIndex;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            BuildConeVisual();
            TintBody();
        }

        /// <summary>One owned, transparency-capable material for the whole body, so the
        /// route-restart fade can drive a real alpha -- the stock primitive material is
        /// opaque and ignores it.</summary>
        private void TintBody()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            _bodyMaterial = new Material(shader) { name = "SentryBody" };
            MakeTransparent(_bodyMaterial);
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer == _coneRenderer) continue;
                renderer.sharedMaterial = _bodyMaterial;
            }

            MakeTransparent(_coneMaterial);
            ApplyAlpha();
        }

        private static void MakeTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private void ApplyAlpha()
        {
            var body = bodyTint;
            body.a = _alpha;
            if (_bodyMaterial.HasProperty("_BaseColor")) _bodyMaterial.SetColor("_BaseColor", body);
            if (_bodyMaterial.HasProperty("_Color")) _bodyMaterial.SetColor("_Color", body);

            var cone = _coneColor;
            cone.a = _alpha;
            if (_coneMaterial.HasProperty("_BaseColor")) _coneMaterial.SetColor("_BaseColor", cone);
            if (_coneMaterial.HasProperty("_Color")) _coneMaterial.SetColor("_Color", cone);
        }

        private void OnDestroy()
        {
            if (_coneMaterial != null) Destroy(_coneMaterial);
            if (_bodyMaterial != null) Destroy(_bodyMaterial);
            if (_coneMesh != null) Destroy(_coneMesh);
        }

        /// <summary>Called by GameDirector when a stealth round starts, with the recorded
        /// route this sentry is to retrace.</summary>
        public void BeginPatrol(IReadOnlyList<Breadcrumb> route, IReadOnlyList<DwellPoint> dwells)
        {
            if (route == null || route.Count < 2)
            {
                Debug.LogWarning("[PatrolSentry] No recorded route to patrol -- staying inactive.", this);
                return;
            }

            _route = route;
            gameObject.SetActive(true);

            var config = RetraceConfig.Current;
            _agent.speed = config.sentrySpeed;
            _agent.angularSpeed = 360f;
            _agent.acceleration = 20f;
            _agent.autoBraking = false;
            _agent.stoppingDistance = 0f;

            // Ghosts pass through each other: agent avoidance would shove them off their
            // recorded routes wherever the player's walks overlapped -- doorways, the
            // stairs -- which is exactly where fidelity matters most.
            _agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance;

            _dwellByCrumb.Clear();
            if (dwells != null)
            {
                foreach (var dwell in dwells)
                {
                    _dwellByCrumb[Mathf.Max(0, dwell.CrumbIndex)] = dwell;
                }
            }

            // The route can begin off the mesh -- the spawn point may sit outside the baked
            // house -- so start at the first crumb past the head start that actually lands
            // on it. Warping to an off-mesh point would strand the agent entirely.
            _targetIndex = FirstCrumbOnMesh(route, StartIndex(route, config.headStartMetres));
            if (_targetIndex < 0)
            {
                Debug.LogWarning("[PatrolSentry] No crumb of the route is on the navmesh -- staying inactive.", this);
                gameObject.SetActive(false);
                return;
            }

            _agent.Warp(route[_targetIndex].Position);
            transform.rotation = Quaternion.LookRotation(route[_targetIndex].Direction, Vector3.up);
            _lookedAtTarget = true;

            _graceUntil = Time.time + config.graceSeconds;
            _alpha = 0f;
            SetConeAlarmed(false);
            UpdateConeVisual();
            State = SentryState.Materializing;
            _agent.isStopped = true;
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

            UpdateFade();
            UpdateConeVisual();

            if (State == SentryState.Chasing)
            {
                UpdateChase();
                return;
            }

            if (State == SentryState.Waiting)
            {
                if (Time.time >= _restartAt) RestartFromBeginning();
            }
            else if (State == SentryState.Materializing)
            {
                if (_alpha >= 1f)
                {
                    State = SentryState.Patrolling;
                    _agent.isStopped = false;
                    AdvanceOrRestart();
                }
            }
            else if (State == SentryState.Looking)
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
            _agent.speed = RetraceConfig.Current.sentrySpeed;
            if (_agent.pathPending || _agent.remainingDistance >= WaypointReachedDistance) return;

            if (!_lookedAtTarget && _dwellByCrumb.TryGetValue(_targetIndex, out var dwell))
            {
                _lookedAtTarget = true;
                BeginLook(dwell);
                return;
            }

            AdvanceOrRestart();
        }

        private void BeginLook(DwellPoint dwell)
        {
            State = SentryState.Looking;
            _agent.isStopped = true;
            _agent.updateRotation = false;
            _lookTimer = 0f;
            _lookYaw = dwell.FacingYaw;
            Rummage(dwell);
        }

        /// <summary>The ghost repeats the player's use of whatever they touched here. The
        /// visible rummage is optional; checking a cupboard for a hider never is.</summary>
        private void Rummage(DwellPoint dwell)
        {
            if (dwell.Prop == null) return;

            if (RetraceConfig.Current.sentriesOpenFurniture && dwell.Prop.TryGetComponent<IOpenable>(out var openable))
            {
                openable.Open();
            }

            var spot = dwell.Prop.GetComponentInParent<HidingSpot>();
            if (spot != null) spot.OpenedBy(this);
        }

        /// <summary>Detection by touch rather than sight: the ghost opened the door you
        /// were behind.</summary>
        public void SpotPlayer()
        {
            if (State == SentryState.Chasing || State == SentryState.Inactive) return;
            OnPlayerSeen();
        }

        private void UpdateLook()
        {
            _lookTimer += Time.deltaTime;
            var config = RetraceConfig.Current;

            // One full sweep across the recorded facing over the fixed pause, so the stop
            // scans the area the player was interested in rather than freezing in place.
            var sweep = Mathf.Sin(_lookTimer / config.lookAroundSeconds * Mathf.PI * 2f) * config.lookSweepDegrees;
            var target = Quaternion.Euler(0f, _lookYaw + sweep, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, config.lookTurnDegreesPerSecond * Time.deltaTime);

            if (_lookTimer < config.lookAroundSeconds) return;

            State = SentryState.Patrolling;
            _agent.updateRotation = true;
            _agent.isStopped = false;
            AdvanceOrRestart();
        }

        /// <summary>At the route's end the sentry pauses, then teleports back to the start
        /// and walks it all over again -- no return leg, so it only ever moves along ground
        /// the player actually covered. It fades out over the pause and stops seeing: a
        /// half-vanished ghost catching you reads as a bug, not a loss.</summary>
        private void AdvanceOrRestart()
        {
            if (_targetIndex >= _route.Count - 1)
            {
                State = SentryState.Waiting;
                _agent.isStopped = true;
                _restartAt = Time.time + RetraceConfig.Current.restartDelaySeconds;
                return;
            }

            _targetIndex++;
            _lookedAtTarget = false;
            _agent.SetDestination(_route[_targetIndex].Position);
        }

        /// <summary>Same placement as the initial spawn, grace period included: the player
        /// may be standing near the route's start, and materialising mid-room should never
        /// be an instant catch.</summary>
        private void RestartFromBeginning()
        {
            var config = RetraceConfig.Current;
            _targetIndex = FirstCrumbOnMesh(_route, StartIndex(_route, config.headStartMetres));
            if (_targetIndex < 0)
            {
                State = SentryState.Waiting;
                _restartAt = Time.time + config.restartDelaySeconds;
                return;
            }

            _agent.Warp(_route[_targetIndex].Position);
            transform.rotation = Quaternion.LookRotation(_route[_targetIndex].Direction, Vector3.up);
            _lookedAtTarget = true;
            _graceUntil = Time.time + config.graceSeconds;
            _alpha = 0f;
            ApplyAlpha();
            State = SentryState.Materializing;
            _agent.isStopped = true;
        }

        /// <summary>The fade-in is meant to stay shorter than the grace period, so the
        /// sentry is never fully visible yet unfairly blind, or vice versa.</summary>
        private void UpdateFade()
        {
            var config = RetraceConfig.Current;
            var alpha = State == SentryState.Waiting
                ? Mathf.Clamp01((_restartAt - Time.time) / config.restartDelaySeconds)
                : Mathf.Min(1f, _alpha + Time.deltaTime / config.fadeInSeconds);
            if (Mathf.Approximately(alpha, _alpha)) return;

            _alpha = alpha;
            ApplyAlpha();
        }

        private void TrySpotPlayer()
        {
            // No catches from a half-vanished ghost, in either direction of the fade.
            if (State == SentryState.Waiting || State == SentryState.Materializing) return;
            if (player == null || Time.time < _graceUntil) return;

            var config = RetraceConfig.Current;
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
            var config = RetraceConfig.Current;
            _chaseDeadline = Time.time + config.chaseCapSeconds;
            _agent.updateRotation = true;
            _agent.isStopped = false;
            _agent.speed = config.chaseSpeed;
            SetConeAlarmed(true);

            // PlayClipAtPoint rather than an owned AudioSource: the whistle must outlive the
            // sentry, which gets deactivated moments later when the catch ends the attempt.
            if (spottedClip != null)
            {
                AudioSource.PlayClipAtPoint(spottedClip, transform.position + Vector3.up * EyeHeight);
            }

            if (GameDirector.Instance != null) GameDirector.Instance.OnPlayerSpotted();
        }

        private void UpdateChase()
        {
            if (player == null) return;

            _agent.SetDestination(player.transform.position);

            var gap = Flatten(player.transform.position - transform.position, normalize: false);
            if (gap.magnitude <= RetraceConfig.Current.catchDistance || Time.time >= _chaseDeadline)
            {
                if (GameDirector.Instance != null) GameDirector.Instance.OnPlayerCaught();
            }
        }

        private static int FirstCrumbOnMesh(IReadOnlyList<Breadcrumb> crumbs, int from)
        {
            for (var i = from; i < crumbs.Count; i++)
            {
                if (NavMesh.SamplePosition(crumbs[i].Position, out _, 1f, NavMesh.AllAreas)) return i;
            }

            return -1;
        }

        /// <summary>Route distance from crumb 0 decides the spawn crumb, giving the player a
        /// fixed head start however densely the crumbs were dropped. The head start is short
        /// on purpose: the sentry begins right in front of the player, already walking away
        /// -- an unmissable "that thing is following my route" beat -- while keeping the two
        /// capsules from overlapping at spawn.</summary>
        private static int StartIndex(IReadOnlyList<Breadcrumb> crumbs, float headStartMetres)
        {
            var travelled = 0f;
            for (var i = 1; i < crumbs.Count; i++)
            {
                travelled += Vector3.Distance(crumbs[i - 1].Position, crumbs[i].Position);
                if (travelled >= headStartMetres) return i;
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
        /// A flat fan on the floor showing where the sentry can see. In a primitives-only
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

            _coneMesh = new Mesh { name = "VisionCone" };
            _coneMesh.MarkDynamic();
            _coneVertices = new Vector3[ConeSegments + 2];
            _coneMesh.vertices = _coneVertices;

            var triangles = new int[ConeSegments * 3];
            for (var i = 0; i < ConeSegments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            _coneMesh.triangles = triangles;
            var normals = new Vector3[_coneVertices.Length];
            for (var i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            _coneMesh.normals = normals;
            _coneFilter.sharedMesh = _coneMesh;
        }

        /// <summary>
        /// The fan is re-cut against the walls every frame, at the sentry's true range and
        /// angle, so the floor shows its actual sightline. A fixed-shape cone lies in one of
        /// two directions: drawn short it reads safe where the sentry can see you, drawn long
        /// it pokes through walls and reads seen where you are hidden.
        /// </summary>
        private void UpdateConeVisual()
        {
            var config = RetraceConfig.Current;
            var eye = transform.position + Vector3.up * EyeHeight;
            var halfAngle = config.visionAngle * 0.5f;

            _coneVertices[0] = Vector3.zero;
            for (var i = 0; i <= ConeSegments; i++)
            {
                var angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)ConeSegments);
                var localDirection = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var worldDirection = transform.rotation * localDirection;

                var distance = config.visionRange;
                if (Physics.Raycast(eye, worldDirection, out var hit, config.visionRange, ~0, QueryTriggerInteraction.Ignore))
                {
                    distance = hit.distance;
                }

                _coneVertices[i + 1] = localDirection * distance;
            }

            _coneMesh.vertices = _coneVertices;
            _coneMesh.RecalculateBounds();
        }

        private void SetConeAlarmed(bool alarmed)
        {
            _coneColor = alarmed ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.85f, 0.3f);
            ApplyAlpha();
        }
    }
}

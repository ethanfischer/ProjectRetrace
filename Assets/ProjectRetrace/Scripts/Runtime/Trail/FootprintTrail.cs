using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// The teaching aid: footprints on the floor that show a player the game is recording
    /// their route. During the search the player's own prints appear behind them as they
    /// walk. In the stealth rounds that follow, the same prints reappear in a bright colour
    /// under the ghost that retraces them, hold for a few seconds, then fade out together, so the
    /// link between "the route I walked" and "the route the ghost walks" is on screen rather
    /// than explained. Playtesters who were told nothing did not make that link on their own.
    ///
    /// Ghost prints are limited to the first few stealth rounds (footprintRounds) on purpose:
    /// shown every round, every ghost's whole route would be a minimap of the threats, and the
    /// difficulty curve is built on the player having to remember where they went.
    ///
    /// Prints are a sparse sample of the crumbs (one per stride, alternating feet) purely for
    /// looks; the sentry still walks every crumb. Rendered with real renderers rather than
    /// gizmos so it works in a build.
    /// </summary>
    [RequireComponent(typeof(BreadcrumbTrail))]
    public class FootprintTrail : MonoBehaviour
    {
        private const float FootSpread = 0.09f;

        [Tooltip("A transparent unlit material asset, cloned so the per-print alpha fade survives build-time shader stripping. Assigned by ProjectRetrace > Setup Scene Systems or Setup Footprints.")]
        public Material materialTemplate;

        // Not serialized: the scene keeps no copy, so a colour change here is the change.
        private static readonly Color OwnColor = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color GhostColor = Color.white;
        private const float HeightOffset = 0.04f;

        private BreadcrumbTrail _trail;
        private Material _material;
        private Mesh _mesh;
        private MaterialPropertyBlock _block;
        private readonly List<Track> _tracks = new List<Track>();
        private Track _ownTrack;
        private int _ownRouteIndex = -1;

        private class Print
        {
            public Renderer Renderer;
        }

        private class Track
        {
            public RecordedRoute Route;
            public PatrolSentry Sentry;
            public Transform Root;
            public Color Color;
            public readonly List<Print> Prints = new List<Print>();
            public int PrintedCrumbs;
            public float Alpha = 1f;
            public float ShownAt;
        }

        private void Awake()
        {
            _trail = GetComponent<BreadcrumbTrail>();
            _block = new MaterialPropertyBlock();
            _mesh = BuildFootprintMesh();
            _material = CreateMaterial();
        }

        private void OnDestroy()
        {
            ClearAll();
            if (_material != null) Destroy(_material);
            if (_mesh != null) Destroy(_mesh);
        }

        private void LateUpdate()
        {
            var director = GameDirector.Instance;
            var config = RetraceConfig.Current;
            if (director == null || !config.footprintsEnabled || !director.IsLocalTurn)
            {
                ClearAll();
                return;
            }

            if (director.Phase == GamePhase.Search)
            {
                ClearGhostTracks();
                UpdateOwnTrack();
            }
            else if (director.Phase == GamePhase.Stealth && director.StealthRound <= config.footprintRounds)
            {
                ClearOwnTrack();
                UpdateGhostTracks(director);
            }
            else
            {
                ClearAll();
            }
        }

        private void UpdateOwnTrack()
        {
            var route = _trail.CurrentRoute;
            var index = _trail.Routes.Count - 1;
            if (route == null)
            {
                ClearOwnTrack();
                return;
            }

            // A restarted route starts its prints over; the old ones would show a walk
            // that no ghost will ever take.
            if (_ownTrack != null && (index != _ownRouteIndex || route.Crumbs.Count < _ownTrack.PrintedCrumbs))
            {
                ClearOwnTrack();
            }

            if (_ownTrack == null)
            {
                _ownTrack = CreateTrack(route, null, OwnColor, "Own");
                _ownRouteIndex = index;
            }

            GrowTrack(_ownTrack);
        }

        private void UpdateGhostTracks(GameDirector director)
        {
            var sentries = director.Sentries;
            var routes = director.PatrolledRoutes;
            var count = Mathf.Min(sentries.Count, routes.Count);

            if (!TracksMatch(sentries, routes, count))
            {
                ClearGhostTracks();
                for (var i = 0; i < count; i++)
                {
                    var color = director.Multiplayer ? Opaque(sentries[i].bodyTint) : GhostColor;
                    _tracks.Add(CreateTrack(routes[i], sentries[i], color, "Ghost " + (i + 1)));
                }
            }

            for (var i = 0; i < _tracks.Count; i++)
            {
                GrowTrack(_tracks[i]);
                FadeAfterHold(_tracks[i]);
            }
        }

        private bool TracksMatch(IReadOnlyList<PatrolSentry> sentries, IReadOnlyList<RecordedRoute> routes, int count)
        {
            if (_tracks.Count != count) return false;
            for (var i = 0; i < count; i++)
            {
                if (_tracks[i].Route != routes[i] || _tracks[i].Sentry != sentries[i]) return false;
            }

            return true;
        }

        /// <summary>A glimpse, not a map: the whole trail holds for footprintHoldSeconds
        /// from the start of the attempt, then fades together and stays gone. Long enough
        /// to see the ghost step onto your own route, short enough that the round is still
        /// played from memory.</summary>
        private void FadeAfterHold(Track track)
        {
            var config = RetraceConfig.Current;
            var elapsed = Time.time - track.ShownAt - config.footprintHoldSeconds;
            var alpha = elapsed <= 0f ? 1f : 1f - Mathf.Clamp01(elapsed / Mathf.Max(0.01f, config.footprintFadeSeconds));
            if (Mathf.Approximately(alpha, track.Alpha)) return;

            track.Alpha = alpha;
            foreach (var print in track.Prints)
            {
                print.Renderer.enabled = alpha > 0f;
                if (alpha > 0f) SetAlpha(print.Renderer, track.Color, track.Color.a * alpha);
            }
        }

        private void GrowTrack(Track track)
        {
            var crumbs = track.Route.Crumbs;
            var stride = StrideInCrumbs();
            for (var i = track.PrintedCrumbs; i < crumbs.Count; i++)
            {
                if (i % stride == 0)
                {
                    track.Prints.Add(CreatePrint(track, crumbs[i], (i / stride) % 2 == 0));
                }
            }

            track.PrintedCrumbs = crumbs.Count;
        }

        private static int StrideInCrumbs()
        {
            var config = RetraceConfig.Current;
            return Mathf.Max(1, Mathf.RoundToInt(config.footprintStride / Mathf.Max(0.01f, config.dotSpacing)));
        }

        private Track CreateTrack(RecordedRoute route, PatrolSentry sentry, Color color, string label)
        {
            var root = new GameObject("Footprints " + label).transform;
            root.SetParent(transform, false);
            return new Track { Route = route, Sentry = sentry, Root = root, Color = color, ShownAt = Time.time };
        }

        private Print CreatePrint(Track track, Breadcrumb crumb, bool leftFoot)
        {
            var side = Vector3.Cross(Vector3.up, crumb.Direction) * (leftFoot ? -FootSpread : FootSpread);
            var print = new GameObject("Print");
            print.transform.SetParent(track.Root, false);
            print.transform.SetPositionAndRotation(
                crumb.Position + side + Vector3.up * HeightOffset,
                Quaternion.LookRotation(crumb.Direction, Vector3.up));

            print.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = print.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            SetAlpha(renderer, track.Color, track.Color.a * track.Alpha);
            renderer.enabled = track.Alpha > 0f;

            return new Print { Renderer = renderer };
        }

        private void SetAlpha(Renderer renderer, Color color, float alpha)
        {
            color.a = alpha;
            renderer.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", color);
            _block.SetColor("_Color", color);
            renderer.SetPropertyBlock(_block);
        }

        private static Color Opaque(Color color)
        {
            color.a = 1f;
            return color;
        }

        private void ClearAll()
        {
            ClearOwnTrack();
            ClearGhostTracks();
        }

        private void ClearOwnTrack()
        {
            if (_ownTrack == null) return;
            DestroyTrack(_ownTrack);
            _ownTrack = null;
            _ownRouteIndex = -1;
        }

        private void ClearGhostTracks()
        {
            foreach (var track in _tracks) DestroyTrack(track);
            _tracks.Clear();
        }

        private static void DestroyTrack(Track track)
        {
            if (track.Root != null) Destroy(track.Root.gameObject);
        }

        private Material CreateMaterial()
        {
            if (materialTemplate != null) return new Material(materialTemplate) { name = "Footprint" };

            var material = TrailVisualizer.CreateUnlitMaterial(Color.white);
            material.name = "Footprint";
            PatrolSentry.MakeTransparent(material);
            return material;
        }

        /// <summary>A sole: a small heel oval and a larger toe oval, toe towards local +Z.
        /// Built in code so the project carries no mesh assets; one mesh serves every print.</summary>
        private static Mesh BuildFootprintMesh()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            AddOval(vertices, triangles, new Vector3(0f, 0f, -0.07f), 0.038f, 0.055f);
            AddOval(vertices, triangles, new Vector3(0f, 0f, 0.055f), 0.048f, 0.07f);

            var mesh = new Mesh { name = "Footprint" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            var normals = new Vector3[vertices.Count];
            for (var i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddOval(List<Vector3> vertices, List<int> triangles, Vector3 center, float halfWidth, float halfLength)
        {
            const int segments = 12;
            var centerIndex = vertices.Count;
            vertices.Add(center);
            for (var i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                vertices.Add(center + new Vector3(Mathf.Sin(angle) * halfWidth, 0f, Mathf.Cos(angle) * halfLength));
            }

            for (var i = 0; i < segments; i++)
            {
                var a = centerIndex + 1 + i;
                var b = centerIndex + 1 + (i + 1) % segments;
                triangles.Add(centerIndex);
                triangles.Add(a);
                triangles.Add(b);
            }
        }
    }
}

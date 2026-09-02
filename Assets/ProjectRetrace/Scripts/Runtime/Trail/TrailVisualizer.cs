using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Debug view: draws the route currently being recorded as flat floor arrows pointing
    /// the direction walked, toggled with the debug key. Only ever the current route --
    /// every older one is some sentry's patrol script by now, and showing it would hand
    /// the player a minimap of the threats. Uses real renderers rather than editor gizmos
    /// so it also works in a build.
    /// </summary>
    [RequireComponent(typeof(BreadcrumbTrail))]
    public class TrailVisualizer : MonoBehaviour
    {
        [SerializeField] private float arrowScale = 1f;
        [SerializeField] private float heightOffset = 0.06f;
        [SerializeField] private Color searchColor = new Color(0.25f, 0.75f, 1f);
        [SerializeField] private Color sneakColor = new Color(1f, 0.55f, 0.15f);

        private BreadcrumbTrail _trail;
        private Transform _root;
        private readonly List<Renderer> _arrows = new List<Renderer>();
        private Material _material;
        private Mesh _arrowMesh;
        private int _routeIndex = -1;
        private bool _lastVisible;

        private void Awake()
        {
            _trail = GetComponent<BreadcrumbTrail>();

            var rootObject = new GameObject("BreadcrumbArrows");
            _root = rootObject.transform;
            _root.SetParent(transform, false);

            _material = CreateUnlitMaterial(searchColor);
            _arrowMesh = BuildArrowMesh();

            _root.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            DestroyResource(_material);
            DestroyResource(_arrowMesh);
        }

        private void LateUpdate()
        {
            var route = _trail.CurrentRoute;
            var index = _trail.Routes.Count - 1;

            if (route != null)
            {
                // A new or restarted route replaces the arrows outright; the search route
                // keeps its own colour so round 0 still reads distinctly in playtests.
                if (index != _routeIndex || route.Crumbs.Count < _arrows.Count)
                {
                    ClearArrows();
                    _routeIndex = index;
                    SetColor(index == 0 ? searchColor : sneakColor);
                }

                for (var i = _arrows.Count; i < route.Crumbs.Count; i++)
                {
                    _arrows.Add(CreateArrow(route.Crumbs[i]));
                }
            }

            var visible = GameDirector.DebugVisible && route != null;
            if (visible != _lastVisible)
            {
                _root.gameObject.SetActive(visible);
                _lastVisible = visible;
            }
        }

        private Renderer CreateArrow(Breadcrumb crumb)
        {
            var arrow = new GameObject("Crumb");
            arrow.transform.SetParent(_root, false);
            arrow.transform.SetPositionAndRotation(
                crumb.Position + Vector3.up * heightOffset,
                Quaternion.LookRotation(crumb.Direction, Vector3.up));
            arrow.transform.localScale = Vector3.one * arrowScale;

            arrow.AddComponent<MeshFilter>().sharedMesh = _arrowMesh;
            var renderer = arrow.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private void ClearArrows()
        {
            for (var i = 0; i < _arrows.Count; i++)
            {
                if (_arrows[i] != null) Destroy(_arrows[i].gameObject);
            }

            _arrows.Clear();
        }

        private void SetColor(Color color)
        {
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", color);
            if (_material.HasProperty("_Color")) _material.SetColor("_Color", color);
        }

        /// <summary>
        /// A flat chevron-tailed arrow lying on the floor, pointing local +Z. Built in code so
        /// the project carries no mesh assets; one shared mesh serves every arrow.
        /// </summary>
        private static Mesh BuildArrowMesh()
        {
            var mesh = new Mesh { name = "CrumbArrow" };
            mesh.vertices = new[]
            {
                new Vector3(-0.06f, 0f, -0.22f),
                new Vector3(0.06f, 0f, -0.22f),
                new Vector3(0.06f, 0f, 0.05f),
                new Vector3(-0.06f, 0f, 0.05f),
                new Vector3(-0.16f, 0f, 0.05f),
                new Vector3(0.16f, 0f, 0.05f),
                new Vector3(0f, 0f, 0.3f),
            };
            mesh.triangles = new[]
            {
                0, 3, 2,
                0, 2, 1,
                4, 6, 5,
            };
            var normals = new Vector3[mesh.vertexCount];
            for (var i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Built in code so the project carries no material assets to merge-conflict over.
        /// Falls back through URP -> built-in so it renders whichever pipeline is active.
        /// </summary>
        internal static Material CreateUnlitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var material = new Material(shader) { name = "CrumbArrow" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            return material;
        }

        private static void DestroyResource(Object resource)
        {
            if (resource == null) return;
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }
    }
}

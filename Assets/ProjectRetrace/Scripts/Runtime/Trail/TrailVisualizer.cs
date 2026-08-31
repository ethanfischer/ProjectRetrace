using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Draws both trails as flat floor arrows pointing the direction the player walked:
    /// round 1 in one colour, round 2 in another. Hidden by default -- phase 2 is meant to be
    /// blind -- and toggled with the debug key; the Results phase forces it on so the player
    /// can walk the house comparing the two routes side by side. Uses real renderers rather
    /// than editor gizmos so it also works in a build.
    /// </summary>
    [RequireComponent(typeof(BreadcrumbTrail))]
    public class TrailVisualizer : MonoBehaviour
    {
        [SerializeField] private float arrowScale = 1f;
        [SerializeField] private float heightOffset = 0.06f;
        [SerializeField] private Color round1Color = new Color(0.25f, 0.75f, 1f);
        [SerializeField] private Color round2Color = new Color(1f, 0.55f, 0.15f);

        private BreadcrumbTrail _trail;
        private Transform _root;
        private readonly List<Renderer> _round1Dots = new List<Renderer>();
        private readonly List<Renderer> _round2Dots = new List<Renderer>();
        private Material _round1Material;
        private Material _round2Material;
        private Mesh _arrowMesh;
        private bool _lastVisible;

        private void Awake()
        {
            _trail = GetComponent<BreadcrumbTrail>();

            var rootObject = new GameObject("BreadcrumbArrows");
            _root = rootObject.transform;
            _root.SetParent(transform, false);

            _round1Material = CreateUnlitMaterial(round1Color);
            _round2Material = CreateUnlitMaterial(round2Color);
            _arrowMesh = BuildArrowMesh();

            _root.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            DestroyResource(_round1Material);
            DestroyResource(_round2Material);
            DestroyResource(_arrowMesh);
        }

        private void LateUpdate()
        {
            SyncArrows(_trail.Phase1Crumbs, _round1Dots, _round1Material);
            SyncArrows(_trail.Phase2Crumbs, _round2Dots, _round2Material);

            var visible = GameDirector.DebugVisible;
            if (visible != _lastVisible)
            {
                _root.gameObject.SetActive(visible);
                _lastVisible = visible;
            }
        }

        /// <summary>Adds arrows for newly dropped crumbs, and clears when a run restarts.</summary>
        private void SyncArrows(IReadOnlyList<Breadcrumb> crumbs, List<Renderer> arrows, Material material)
        {
            if (crumbs.Count < arrows.Count)
            {
                ClearArrows(arrows);
            }

            for (var i = arrows.Count; i < crumbs.Count; i++)
            {
                arrows.Add(CreateArrow(crumbs[i], material));
            }
        }

        private Renderer CreateArrow(Breadcrumb crumb, Material material)
        {
            var arrow = new GameObject("Crumb");
            arrow.transform.SetParent(_root, false);
            arrow.transform.SetPositionAndRotation(
                crumb.Position + Vector3.up * heightOffset,
                Quaternion.LookRotation(crumb.Direction, Vector3.up));
            arrow.transform.localScale = Vector3.one * arrowScale;

            arrow.AddComponent<MeshFilter>().sharedMesh = _arrowMesh;
            var renderer = arrow.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static void ClearArrows(List<Renderer> arrows)
        {
            for (var i = 0; i < arrows.Count; i++)
            {
                if (arrows[i] != null) Destroy(arrows[i].gameObject);
            }

            arrows.Clear();
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

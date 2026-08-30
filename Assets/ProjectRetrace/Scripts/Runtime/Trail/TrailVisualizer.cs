using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Draws both trails: round 1 in one colour, round 2 in another. Hidden by default --
    /// phase 2 is meant to be blind -- and toggled with the debug key; the Results phase
    /// forces it on so the player can walk the house comparing the two routes side by side.
    /// Uses real renderers rather than editor gizmos so it also works in a build, which is
    /// what you want when handing a build to someone else to playtest.
    /// </summary>
    [RequireComponent(typeof(BreadcrumbTrail))]
    public class TrailVisualizer : MonoBehaviour
    {
        [SerializeField] private float dotScale = 0.18f;
        [SerializeField] private float dotHeightOffset = 0.05f;
        [SerializeField] private Color round1Color = new Color(0.25f, 0.75f, 1f);
        [SerializeField] private Color round2Color = new Color(1f, 0.55f, 0.15f);

        private BreadcrumbTrail _trail;
        private Transform _root;
        private readonly List<Renderer> _round1Dots = new List<Renderer>();
        private readonly List<Renderer> _round2Dots = new List<Renderer>();
        private Material _round1Material;
        private Material _round2Material;
        private bool _lastVisible;

        private void Awake()
        {
            _trail = GetComponent<BreadcrumbTrail>();

            var rootObject = new GameObject("BreadcrumbDots");
            _root = rootObject.transform;
            _root.SetParent(transform, false);

            _round1Material = CreateUnlitMaterial(round1Color);
            _round2Material = CreateUnlitMaterial(round2Color);

            _root.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            DestroyMaterial(_round1Material);
            DestroyMaterial(_round2Material);
        }

        private void LateUpdate()
        {
            SyncDots(_trail.Phase1Crumbs, _round1Dots, _round1Material);
            SyncDots(_trail.Phase2Crumbs, _round2Dots, _round2Material);

            var visible = GameDirector.DebugVisible;
            if (visible != _lastVisible)
            {
                _root.gameObject.SetActive(visible);
                _lastVisible = visible;
            }
        }

        /// <summary>Adds dots for newly dropped crumbs, and clears when a run restarts.</summary>
        private void SyncDots(IReadOnlyList<Breadcrumb> crumbs, List<Renderer> dots, Material material)
        {
            if (crumbs.Count < dots.Count)
            {
                ClearDots(dots);
            }

            for (var i = dots.Count; i < crumbs.Count; i++)
            {
                dots.Add(CreateDot(crumbs[i].Position, material));
            }
        }

        private Renderer CreateDot(Vector3 position, Material material)
        {
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "Crumb";

            // The sphere primitive ships with a collider, which would otherwise sit in the
            // middle of the room and swallow the player's interaction raycasts.
            var collider = dot.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            dot.transform.SetParent(_root, false);
            dot.transform.position = position + Vector3.up * dotHeightOffset;
            dot.transform.localScale = Vector3.one * dotScale;

            var renderer = dot.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static void ClearDots(List<Renderer> dots)
        {
            for (var i = 0; i < dots.Count; i++)
            {
                if (dots[i] != null) Destroy(dots[i].gameObject);
            }

            dots.Clear();
        }

        /// <summary>
        /// Built in code so the project carries no material assets to merge-conflict over.
        /// Falls back through URP -> built-in so it renders whichever pipeline is active.
        /// </summary>
        private static Material CreateUnlitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var material = new Material(shader) { name = "CrumbDot" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            return material;
        }

        private static void DestroyMaterial(Material material)
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }
    }
}

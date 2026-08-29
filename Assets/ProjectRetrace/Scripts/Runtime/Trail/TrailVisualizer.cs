using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Draws the breadcrumbs. Hidden by default -- phase 2 is meant to be blind -- and toggled
    /// with the debug key. Uses real renderers rather than editor gizmos so it also works in a
    /// build, which is what you want when handing a build to someone else to playtest.
    ///
    /// Green = collected, red = missed. At the results screen this is the whole explanation of
    /// the score: you are looking directly at where you drifted.
    /// </summary>
    [RequireComponent(typeof(BreadcrumbTrail))]
    public class TrailVisualizer : MonoBehaviour
    {
        [SerializeField] private float dotScale = 0.18f;
        [SerializeField] private float dotHeightOffset = 0.05f;
        [SerializeField] private Color pendingColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color collectedColor = new Color(0.25f, 1f, 0.35f);
        [SerializeField] private Color missedColor = new Color(1f, 0.25f, 0.25f);

        private BreadcrumbTrail _trail;
        private Transform _root;
        private readonly List<Renderer> _dots = new List<Renderer>();
        private readonly List<bool> _dotCollected = new List<bool>();
        private Material _pendingMaterial;
        private Material _collectedMaterial;
        private Material _missedMaterial;
        private bool _lastVisible;
        private bool _lastResultsPhase;

        private void Awake()
        {
            _trail = GetComponent<BreadcrumbTrail>();

            var rootObject = new GameObject("BreadcrumbDots");
            _root = rootObject.transform;
            _root.SetParent(transform, false);

            _pendingMaterial = CreateUnlitMaterial(pendingColor);
            _collectedMaterial = CreateUnlitMaterial(collectedColor);
            _missedMaterial = CreateUnlitMaterial(missedColor);

            _root.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            DestroyMaterial(_pendingMaterial);
            DestroyMaterial(_collectedMaterial);
            DestroyMaterial(_missedMaterial);
        }

        private void LateUpdate()
        {
            SyncDots();

            var visible = GameDirector.DebugVisible;
            if (visible != _lastVisible)
            {
                _root.gameObject.SetActive(visible);
                _lastVisible = visible;
            }

            if (!visible) return;

            // In Results, uncollected dots go red rather than staying "pending" -- the run is
            // over, so an uncollected mark is a miss, not a mark you have yet to reach.
            var resultsPhase = GameDirector.Instance != null && GameDirector.Instance.Phase == GamePhase.Results;
            var phaseChanged = resultsPhase != _lastResultsPhase;
            _lastResultsPhase = resultsPhase;

            RefreshColors(phaseChanged, resultsPhase);
        }

        /// <summary>Adds dots for newly dropped crumbs, and clears everything when a run restarts.</summary>
        private void SyncDots()
        {
            var crumbs = _trail.Crumbs;

            if (crumbs.Count < _dots.Count)
            {
                ClearDots();
            }

            for (var i = _dots.Count; i < crumbs.Count; i++)
            {
                _dots.Add(CreateDot(crumbs[i].Position));
                _dotCollected.Add(false);
            }
        }

        private void RefreshColors(bool forceAll, bool resultsPhase)
        {
            var crumbs = _trail.Crumbs;
            var count = Mathf.Min(crumbs.Count, _dots.Count);

            for (var i = 0; i < count; i++)
            {
                var collected = crumbs[i].Collected;
                if (!forceAll && collected == _dotCollected[i]) continue;

                _dotCollected[i] = collected;
                _dots[i].sharedMaterial = collected
                    ? _collectedMaterial
                    : resultsPhase ? _missedMaterial : _pendingMaterial;
            }
        }

        private Renderer CreateDot(Vector3 position)
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
            renderer.sharedMaterial = _pendingMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private void ClearDots()
        {
            for (var i = 0; i < _dots.Count; i++)
            {
                if (_dots[i] != null) Destroy(_dots[i].gameObject);
            }

            _dots.Clear();
            _dotCollected.Clear();
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

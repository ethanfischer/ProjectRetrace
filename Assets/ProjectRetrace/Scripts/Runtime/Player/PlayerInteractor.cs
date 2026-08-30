using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>Raycasts from the camera and drives whatever interactable is under the reticle.</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public Transform rayOrigin;
        [SerializeField] private float reach = 2.5f;
        [SerializeField] private LayerMask interactableMask = ~0;
        [SerializeField] private Key interactKey = Key.E;

        [Tooltip("Hitting a prop's static shell latches onto its nearest moving part within this distance, so a whole dresser prompts, not just the drawer fronts.")]
        [SerializeField] private float shellLatchRadius = 0.9f;

        [SerializeField] private Color highlightTint = new Color(1f, 0.85f, 0.45f);

        private IInteractable _current;
        private bool _inputEnabled = true;
        private readonly List<Renderer> _highlighted = new List<Renderer>();
        private MaterialPropertyBlock _highlightBlock;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>Null when nothing usable is in front of the player. Read by the HUD.</summary>
        public IInteractable Current => _current;

        public string CurrentPrompt => _current != null ? _current.Prompt : null;

        public void SetInputEnabled(bool inputEnabled)
        {
            _inputEnabled = inputEnabled;
            if (!inputEnabled)
            {
                _current = null;
                SetHighlighted(null);
            }
        }

        private void Awake()
        {
            if (rayOrigin == null && Camera.main != null)
            {
                rayOrigin = Camera.main.transform;
            }

            _highlightBlock = new MaterialPropertyBlock();
            _highlightBlock.SetColor(BaseColorId, highlightTint);
            _highlightBlock.SetColor(ColorId, highlightTint);
        }

        private void Update()
        {
            if (!_inputEnabled)
            {
                return;
            }

            _current = FindTarget();
            SetHighlighted(_current as Component);

            if (_current != null && WasPressedThisFrame(interactKey))
            {
                _current.Interact(this);
            }
        }

        private static bool WasPressedThisFrame(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].wasPressedThisFrame;
        }

        private IInteractable FindTarget()
        {
            if (rayOrigin == null) return null;

            var ray = new Ray(rayOrigin.position, rayOrigin.forward);

            // The camera sits at the skin of the player's own capsule, so a steeply pitched ray
            // can clip it and die immediately -- which silently ate every glance at low
            // furniture. Cast through everything and take the first hit that isn't ourselves.
            if (!TryHitOtherThanSelf(ray, out var hit))
            {
                return null;
            }

            return Resolve(hit);
        }

        private bool TryHitOtherThanSelf(Ray ray, out RaycastHit hit)
        {
            var hits = Physics.RaycastAll(ray, reach, interactableMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (var i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider.transform.root == transform.root) continue;
                hit = hits[i];
                return true;
            }

            hit = default;
            return false;
        }

        /// <summary>GetComponentInParent so a prop can carry its collider on a child mesh; shells
        /// with no interactable of their own latch onto the prop's nearest moving part.</summary>
        private IInteractable Resolve(RaycastHit hit)
        {
            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null)
            {
                interactable = LatchOntoNearestPart(hit);
            }

            return interactable != null && interactable.CanInteract ? interactable : null;
        }

        /// <summary>
        /// A prop's shell (dresser carcass, chest walls) has no interactable of its own, but the
        /// player aiming at it clearly means the prop. Pick the nearest usable part under the
        /// same root, so the whole piece of furniture responds instead of just its moving faces.
        /// </summary>
        private IInteractable LatchOntoNearestPart(RaycastHit hit)
        {
            var parts = hit.collider.transform.root.GetComponentsInChildren<IInteractable>();
            IInteractable best = null;
            var bestSqr = shellLatchRadius * shellLatchRadius;

            for (var i = 0; i < parts.Length; i++)
            {
                if (!parts[i].CanInteract) continue;
                var partTransform = ((Component)parts[i]).transform;
                var sqr = (partTransform.position - hit.point).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = parts[i];
            }

            return best;
        }

        private void SetHighlighted(Component target)
        {
            ClearHighlight();
            if (target == null) return;

            target.GetComponentsInChildren(_highlighted);
            for (var i = 0; i < _highlighted.Count; i++)
            {
                _highlighted[i].SetPropertyBlock(_highlightBlock);
            }
        }

        private void ClearHighlight()
        {
            for (var i = 0; i < _highlighted.Count; i++)
            {
                if (_highlighted[i] != null) _highlighted[i].SetPropertyBlock(null);
            }

            _highlighted.Clear();
        }
    }
}

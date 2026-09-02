using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>Raycasts from the camera and drives whatever interactable is under the reticle.</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public Transform rayOrigin;
        [SerializeField] private LayerMask interactableMask = ~0;

        [SerializeField] private Color highlightTint = new Color(1f, 0.85f, 0.45f);

        /// <summary>Raised after the player uses something. The trail listens so a sentry
        /// later pauses exactly where the player rummaged.</summary>
        public event Action<IInteractable> Interacted;

        /// <summary>Set by the HidingSpot the player is inside. While hidden every Use press
        /// means "get out": the reticle is pressed against the inside of a door, and the
        /// door itself must not answer.</summary>
        public HidingSpot Hiding { get; set; }

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

            _current = Hiding != null ? Hiding : FindTarget();
            SetHighlighted(Hiding != null ? null : _current as Component);

            if (_current != null && InteractPressedThisFrame())
            {
                var used = _current;
                used.Interact(this);
                Interacted?.Invoke(used);
            }
        }

        private static bool InteractPressedThisFrame()
        {
            var config = RetraceConfig.Current;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[config.InteractKey].wasPressedThisFrame) return true;

            var mouse = Mouse.current;
            return config.interactWithLeftClick && mouse != null && mouse.leftButton.wasPressedThisFrame;
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
            var hits = Physics.RaycastAll(ray, RetraceConfig.Current.interactReach, interactableMask, QueryTriggerInteraction.Ignore);
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
            if (interactable == null || !interactable.CanInteract)
            {
                interactable = LatchOntoNearestPart(hit);
            }

            return interactable != null && interactable.CanInteract ? interactable : null;
        }

        /// <summary>
        /// A prop's shell (dresser carcass, chest walls) has no interactable of its own, but the
        /// player aiming at it clearly means the prop. Pick the nearest usable part under the
        /// same root within shellLatchRadius, so the whole piece of furniture responds instead
        /// of just its moving faces.
        /// </summary>
        private IInteractable LatchOntoNearestPart(RaycastHit hit)
        {
            var parts = hit.collider.transform.root.GetComponentsInChildren<IInteractable>();
            IInteractable best = null;
            var radius = RetraceConfig.Current.shellLatchRadius;
            var bestSqr = radius * radius;

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

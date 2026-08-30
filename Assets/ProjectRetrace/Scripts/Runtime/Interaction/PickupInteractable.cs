using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>An object that disappears when taken, and comes back on reset.</summary>
    public class PickupInteractable : InteractableBase
    {
        [SerializeField] private bool hideOnPickup = true;

        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private bool _taken;

        public override bool CanInteract => base.CanInteract && !_taken;

        protected virtual void Awake()
        {
            CaptureInitialState();
        }

        public override void Interact(PlayerInteractor interactor)
        {
            if (_taken) return;
            _taken = true;

            // Hide first: OnTaken may kick off the phase transition, which restores every
            // interactable. Hiding afterwards would immediately re-hide the restored keys.
            if (hideOnPickup) SetVisible(false);
            OnTaken(interactor);
        }

        protected virtual void OnTaken(PlayerInteractor interactor)
        {
        }

        /// <summary>
        /// Captured in LOCAL space on purpose. The keys are parented to a spot inside a drawer
        /// or chest, and RestoreAll runs in arbitrary registry order: a world-space restore
        /// could run while the container was still open, then get dragged out of place when the
        /// container snapped shut afterwards -- leaving the keys stranded inside the carcass.
        /// A local pose is correct no matter what state the parent is in when this restores.
        /// </summary>
        public override void CaptureInitialState()
        {
            _initialLocalPosition = transform.localPosition;
            _initialLocalRotation = transform.localRotation;
        }

        public override void RestoreInitialState()
        {
            _taken = false;
            transform.localPosition = _initialLocalPosition;
            transform.localRotation = _initialLocalRotation;
            SetVisible(true);
        }

        /// <summary>
        /// Toggles renderers and colliders rather than the GameObject itself: deactivating it
        /// would fire OnDisable and drop the object out of the registry, so it could never be
        /// restored on the phase transition.
        /// </summary>
        protected void SetVisible(bool visible)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++) renderers[i].enabled = visible;

            var colliders = GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++) colliders[i].enabled = visible;
        }
    }
}

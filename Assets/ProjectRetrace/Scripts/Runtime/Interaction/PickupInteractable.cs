using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>An object that disappears when taken, and comes back on reset.</summary>
    public class PickupInteractable : InteractableBase
    {
        [SerializeField] private bool hideOnPickup = true;

        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
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

        public override void CaptureInitialState()
        {
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
        }

        public override void RestoreInitialState()
        {
            _taken = false;
            transform.SetPositionAndRotation(_initialPosition, _initialRotation);
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

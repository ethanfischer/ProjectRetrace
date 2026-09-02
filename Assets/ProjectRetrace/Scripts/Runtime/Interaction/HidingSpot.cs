using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Lets the player climb into a piece of furniture and pull the door shut. Sits on the
    /// prop's root beside its DoorInteractable: the door must already be open to climb in,
    /// so the ray naturally hits the interior and resolves here instead of the door.
    ///
    /// Hiding is only as safe as the route that got you there. A ghost retracing a route
    /// that opened this cupboard opens it again on its pause, and finds whoever is inside
    /// -- the same "your past self betrays you" rule that drives everything else.
    /// </summary>
    public class HidingSpot : InteractableBase
    {
        [Tooltip("Where the player stands while hidden, in the prop's local space.")]
        [SerializeField] private Vector3 hideLocalPosition = Vector3.zero;

        [Tooltip("Where the player steps out to, in the prop's local space.")]
        [SerializeField] private Vector3 exitLocalPosition = new Vector3(0f, 0f, 0.9f);

        private DoorInteractable _door;
        private PlayerInteractor _occupant;
        private FirstPersonController _occupantController;

        public bool Occupied => _occupant != null;

        public override string Prompt => Occupied ? "Leave" : "Hide";

        public override bool CanInteract =>
            base.CanInteract && (Occupied || (_door != null && _door.IsOpen));

        private void Awake()
        {
            _door = GetComponentInChildren<DoorInteractable>();
        }

        public override void Interact(PlayerInteractor interactor)
        {
            if (Occupied) Leave();
            else Enter(interactor);
        }

        /// <summary>A ghost whose route opened this prop opens it again. Anyone inside is
        /// hauled out and spotted on the spot: the chase then connects at arm's length.</summary>
        public void OpenedBy(PatrolSentry ghost)
        {
            if (_door != null) _door.SetOpen(true);
            if (!Occupied) return;

            Leave();
            ghost.SpotPlayer();
        }

        private void Enter(PlayerInteractor interactor)
        {
            var controller = interactor.GetComponentInParent<FirstPersonController>();
            if (controller == null) return;

            _occupant = interactor;
            _occupantController = controller;
            controller.Teleport(transform.TransformPoint(hideLocalPosition), transform.rotation);
            controller.SetMovementEnabled(false);
            interactor.Hiding = this;
            if (_door != null) _door.SetOpen(false);
        }

        private void Leave()
        {
            if (_door != null) _door.SetOpen(true);
            _occupantController.Teleport(transform.TransformPoint(exitLocalPosition), transform.rotation);
            Release();
        }

        /// <summary>Drops the occupant without moving them: the director teleports the
        /// player to spawn right after restoring, so a stray hop out would be pointless.</summary>
        private void Release()
        {
            if (_occupantController != null) _occupantController.SetMovementEnabled(true);
            if (_occupant != null) _occupant.Hiding = null;
            _occupant = null;
            _occupantController = null;
        }

        public override void CaptureInitialState()
        {
        }

        public override void RestoreInitialState()
        {
            Release();
        }
    }
}

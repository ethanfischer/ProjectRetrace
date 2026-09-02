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
        [SerializeField] private Vector3 hideLocalPosition = new Vector3(0f, 0f, -0.08f);

        [Tooltip("Where the player steps out to, in the prop's local space.")]
        [SerializeField] private Vector3 exitLocalPosition = new Vector3(0f, 0f, 0.9f);

        private DoorInteractable _door;
        private PlayerInteractor _occupant;
        private FirstPersonController _occupantController;
        private float _savedNearClip;
        private Renderer[] _doorRenderers = System.Array.Empty<Renderer>();

        /// <summary>The default near plane is wider than the cupboard is deep, so it cut
        /// straight through the door and showed the room outside.</summary>
        private const float HiddenNearClip = 0.03f;

        public bool Occupied => _occupant != null;

        public override string Prompt => Occupied ? "Leave" : "Hide";

        public override bool CanInteract =>
            base.CanInteract && (Occupied || (_door != null && _door.IsOpen));

        private void Awake()
        {
            _door = GetComponentInChildren<DoorInteractable>();
            if (_door != null) _doorRenderers = _door.GetComponentsInChildren<Renderer>();
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
            controller.SetPeek(transform.eulerAngles.y);
            SetHiddenCamera(true);
            SetDoorVisible(false);
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
            if (_occupantController != null)
            {
                _occupantController.SetMovementEnabled(true);
                _occupantController.ClearPeek();
                SetHiddenCamera(false);
                SetDoorVisible(true);
            }

            if (_occupant != null) _occupant.Hiding = null;
            _occupant = null;
            _occupantController = null;
        }

        /// <summary>The door's collider keeps blocking sightlines; only its mesh goes, so
        /// the crack you peek through actually shows the room. From outside the door reads
        /// as missing for the duration, which nobody is positioned to see.</summary>
        private void SetDoorVisible(bool visible)
        {
            foreach (var renderer in _doorRenderers)
            {
                if (renderer != null) renderer.enabled = visible;
            }
        }

        private void SetHiddenCamera(bool hidden)
        {
            var camera = Camera.main;
            if (camera == null) return;

            if (hidden)
            {
                _savedNearClip = camera.nearClipPlane;
                camera.nearClipPlane = HiddenNearClip;
            }
            else
            {
                camera.nearClipPlane = _savedNearClip;
            }
        }

        /// <summary>Everything but a thin bright band goes dark: you are looking through
        /// the crack of a door, and the crack is where the ghost will appear.</summary>
        private void OnGUI()
        {
            if (!Occupied) return;

            GUI.depth = 10;
            HudScale.Apply();
            var width = HudScale.Width;
            var height = HudScale.Height;
            var slit = height * RetraceConfig.Current.peekSlitHeight;
            var slitTop = (height - slit) * 0.5f;

            var previous = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, width, slitTop), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, slitTop + slit, width, height - slitTop - slit), Texture2D.whiteTexture);
            GUI.color = previous;
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

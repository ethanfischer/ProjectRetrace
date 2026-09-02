using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>A drawer that slides open along a local axis.</summary>
    public class DrawerInteractable : InteractableBase, IOpenable
    {
        [SerializeField] private Vector3 slideAxis = Vector3.forward;
        [SerializeField] private float openDistance = 0.45f;
        [SerializeField] private float openSpeed = 4f;

        private Vector3 _closedLocalPosition;
        private bool _isOpen;
        private float _openAmount;

        public override string Prompt => "Open drawer";

        public bool IsOpen => _isOpen;

        public void Open() => _isOpen = true;

        /// <summary>Opening is one-way: a search leaves the house visibly rummaged, and
        /// closing things back up would only be busywork between the player and the keys.</summary>
        public override bool CanInteract => base.CanInteract && !_isOpen;

        private void Awake()
        {
            CaptureInitialState();
        }

        public override void Interact(PlayerInteractor interactor)
        {
            _isOpen = true;
        }

        private void Update()
        {
            var target = _isOpen ? 1f : 0f;
            if (Mathf.Approximately(_openAmount, target)) return;

            _openAmount = Mathf.MoveTowards(_openAmount, target, openSpeed * Time.deltaTime);
            transform.localPosition = _closedLocalPosition + slideAxis.normalized * (openDistance * _openAmount);
        }

        public override void CaptureInitialState()
        {
            _closedLocalPosition = transform.localPosition;
        }

        public override void RestoreInitialState()
        {
            _isOpen = false;
            _openAmount = 0f;
            transform.localPosition = _closedLocalPosition;
        }
    }
}

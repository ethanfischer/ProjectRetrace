using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>A drawer that slides open along a local axis.</summary>
    public class DrawerInteractable : InteractableBase
    {
        [SerializeField] private Vector3 slideAxis = Vector3.forward;
        [SerializeField] private float openDistance = 0.45f;
        [SerializeField] private float openSpeed = 4f;

        private Vector3 _closedLocalPosition;
        private bool _isOpen;
        private float _openAmount;

        public override string Prompt => _isOpen ? "Close drawer" : "Open drawer";

        private void Awake()
        {
            CaptureInitialState();
        }

        public override void Interact(PlayerInteractor interactor)
        {
            _isOpen = !_isOpen;
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

using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>A door that swings around its local up axis.</summary>
    public class DoorInteractable : InteractableBase
    {
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float openSpeed = 3f;

        private Quaternion _closedLocalRotation;
        private bool _isOpen;
        private float _openAmount;

        public override string Prompt => _isOpen ? "Close door" : "Open door";

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
            transform.localRotation = _closedLocalRotation * Quaternion.Euler(0f, openAngle * _openAmount, 0f);
        }

        public override void CaptureInitialState()
        {
            _closedLocalRotation = transform.localRotation;
        }

        public override void RestoreInitialState()
        {
            _isOpen = false;
            _openAmount = 0f;
            transform.localRotation = _closedLocalRotation;
        }
    }
}

using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>A hinged panel: room doors, cupboard doors, chest lids. Swings around hingeAxis.</summary>
    public class DoorInteractable : InteractableBase
    {
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float openSpeed = 3f;

        [Tooltip("Local axis to swing around. Up for doors, Right for a chest lid.")]
        [SerializeField] private Vector3 hingeAxis = Vector3.up;

        [Tooltip("Noun shown in the prompt: 'Open door', 'Open chest', ...")]
        [SerializeField] private string label = "door";

        private Quaternion _closedLocalRotation;
        private bool _isOpen;
        private float _openAmount;

        public override string Prompt => (_isOpen ? "Close " : "Open ") + label;

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
            transform.localRotation = _closedLocalRotation * Quaternion.AngleAxis(openAngle * _openAmount, hingeAxis);
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

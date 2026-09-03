using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// A drawer that slides open along a local axis. Unlike furniture doors it can be shut
    /// again: the art pack stacks drawers, and an open upper drawer hides the front of the
    /// one below and steals its clicks, so a one-way drawer would lock the rest of the
    /// stack out of a search.
    /// </summary>
    public class DrawerInteractable : InteractableBase, IOpenable
    {
        [SerializeField] private Vector3 slideAxis = Vector3.forward;
        [SerializeField] private float openDistance = 0.45f;
        [SerializeField] private float openSpeed = 4f;

        private Vector3 _closedLocalPosition;
        private bool _isOpen;
        private float _openAmount;

        public override string Prompt => (_isOpen ? "Close " : "Open ") + "drawer";

        public bool IsOpen => _isOpen;

        public void Open() => _isOpen = true;

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

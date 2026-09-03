using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// A hinged panel: room doors, cupboard doors, chest lids. Swings around hingeAxis, and
    /// swings back on a second use: a search is recorded by the trail whether or not the
    /// player tidies up after it, so shutting things again costs the ghosts nothing and
    /// lets a hider close the door on a cupboard they are not in.
    /// </summary>
    public class DoorInteractable : InteractableBase, IOpenable
    {
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float openSpeed = 3f;

        [Tooltip("Local axis to swing around. Up for doors, Right for a chest lid.")]
        [SerializeField] private Vector3 hingeAxis = Vector3.up;

        [Tooltip("Noun shown in the prompt: 'Open door', 'Open chest', ...")]
        [SerializeField] private string label = "door";

        [Tooltip("Displayed round number from which this door can be opened. 0 = never locked.")]
        [Min(0)]
        [SerializeField] private int unlocksAtRound;

        [Tooltip("World volume this door seals off while locked. The keys are never hidden inside it until the door unlocks.")]
        [SerializeField] private Bounds sealedArea;

        private Quaternion _closedLocalRotation;
        private bool _isOpen;
        private float _openAmount;

        public override string Prompt => Locked
            ? $"Locked (opens round {unlocksAtRound})"
            : (_isOpen ? "Close " : "Open ") + label;

        /// <summary>
        /// Gated on the round counter rather than a key item: it is the only clock the game
        /// has, and it already survives retries and couch handovers, so the lock needs no
        /// state of its own to reset.
        /// </summary>
        public bool Locked =>
            unlocksAtRound > 0
            && GameDirector.Instance != null
            && GameDirector.Instance.StealthRound + 1 < unlocksAtRound;

        public bool Seals(Vector3 worldPoint) => Locked && sealedArea.Contains(worldPoint);

        public bool IsOpen => _isOpen;

        public void Open() => SetOpen(true);

        public void SetOpen(bool open)
        {
            if (Locked) return;
            _isOpen = open;
        }

        /// <summary>True on the round this door first opens, so the HUD can call it out once.</summary>
        public bool UnlocksThisRound =>
            unlocksAtRound > 0
            && GameDirector.Instance != null
            && GameDirector.Instance.StealthRound + 1 == unlocksAtRound;

        private void Awake()
        {
            CaptureInitialState();
        }

        public override void Interact(PlayerInteractor interactor)
        {
            if (Locked) return;
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

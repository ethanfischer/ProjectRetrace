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

        [Tooltip("Local point the leaf swings about. Zero swings about the pivot; the importer sets the far edge when a mesh is hinged on the wrong side.")]
        [SerializeField] private Vector3 hingeOffset;

        [Tooltip("Noun shown in the prompt: 'Open door', 'Open chest', ...")]
        [SerializeField] private string label = "door";

        [SerializeField] private OpenableSound sound = OpenableSound.Cabinet;

        [Tooltip("Displayed round number from which this door can be opened. 0 = never locked.")]
        [Min(0)]
        [SerializeField] private int unlocksAtRound;

        [Tooltip("World volume this door seals off while locked. The keys are never hidden inside it until the door unlocks.")]
        [SerializeField] private Bounds sealedArea;

        private Quaternion _closedLocalRotation;
        private Vector3 _closedLocalPosition;
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
            ChangeOpenState(open);
        }

        private void ChangeOpenState(bool open)
        {
            if (_isOpen == open) return;
            _isOpen = open;
            SoundBank.PlayOpenable(sound, open, transform.position);
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
            ChangeOpenState(!_isOpen);
        }

        private void Update()
        {
            var target = _isOpen ? 1f : 0f;
            if (Mathf.Approximately(_openAmount, target)) return;

            _openAmount = Mathf.MoveTowards(_openAmount, target, openSpeed * Time.deltaTime);
            Swing(Quaternion.AngleAxis(openAngle * _openAmount, hingeAxis));
        }

        /// <summary>Rotates about hingeOffset rather than the pivot: a leaf that is part of
        /// a prefab instance cannot be reparented under a hinge object, so the hinge moves
        /// into the maths instead.</summary>
        private void Swing(Quaternion swing)
        {
            transform.localRotation = _closedLocalRotation * swing;
            transform.localPosition = _closedLocalPosition + _closedLocalRotation * (hingeOffset - swing * hingeOffset);
        }

        public override void CaptureInitialState()
        {
            _closedLocalRotation = transform.localRotation;
            _closedLocalPosition = transform.localPosition;
        }

        public override void RestoreInitialState()
        {
            _isOpen = false;
            _openAmount = 0f;
            Swing(Quaternion.identity);
        }
    }
}

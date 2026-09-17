using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// The swinging half of a baby gate, hinged at its own origin. FloorGate and RoomGate
    /// own the lock; this only follows it, because a lock is a rule and a leaf is a prop,
    /// and the two openings should share the prop without sharing the rule. Swings rather
    /// than fades: a gate that opens is an event the player recognises from the toast,
    /// where a vanishing one is a bug they report.
    /// </summary>
    public class GateLeaf : MonoBehaviour
    {
        [Tooltip("Signed: the sign picks which way the leaf swings. Set so it opens away from the player's side.")]
        public float openAngle = -100f;

        public float swingSeconds = 1.2f;

        /// <summary>Set by the owning gate every frame; the leaf eases toward it.</summary>
        public bool Open;

        private Quaternion _closed;
        private float _amount;
        private bool _settled;

        private void Awake()
        {
            _closed = transform.localRotation;
        }

        private void Update()
        {
            var target = Open ? 1f : 0f;
            if (!_settled)
            {
                // The first frame snaps: the search should not open on a gate swinging shut.
                _amount = target;
                _settled = true;
            }

            _amount = Mathf.MoveTowards(_amount, target, Time.deltaTime / Mathf.Max(swingSeconds, 0.01f));
            var eased = Mathf.SmoothStep(0f, 1f, _amount);
            transform.localRotation = _closed * Quaternion.AngleAxis(openAngle * eased, Vector3.up);
        }
    }
}

using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// The hand-placed barrier at the foot of the stairs. It reads the same round clock as
    /// the door locks, so it needs no state of its own to survive retries and couch
    /// handovers: while the upper floor is locked the barrier is solid and the keys stay
    /// downstairs; from the unlock round on it is gone and the keys hide only upstairs,
    /// so the round that opens the house also sends the player up into it. Upstairs is
    /// simply "above the plate", which is why the split is a height and not a volume.
    /// </summary>
    public class FloorGate : MonoBehaviour
    {
        [Tooltip("Key spots at or above this world height count as upstairs.")]
        public float floorHeight = 2.2f;

        private Collider _collider;

        public static bool Enabled => RetraceConfig.Current.lockUpstairs;

        public static bool Locked =>
            Enabled
            && GameDirector.Instance != null
            && GameDirector.Instance.StealthRound + 1 < RetraceConfig.Current.upstairsUnlockRound;

        /// <summary>True on the round the barrier first drops, so the HUD can say so once.</summary>
        public static bool UnlocksThisRound =>
            Enabled
            && GameDirector.Instance != null
            && GameDirector.Instance.StealthRound + 1 == RetraceConfig.Current.upstairsUnlockRound;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
        }

        private void Update()
        {
            if (_collider != null) _collider.enabled = Locked;
        }

        /// <summary>Whether a key may hide at this point in the current round. With no gate
        /// in the scene, or the lock switched off, every spot is fair.</summary>
        public static bool Allows(Vector3 worldPoint)
        {
            if (!Enabled) return true;
            var gate = FindFirstObjectByType<FloorGate>();
            if (gate == null) return true;
            var upstairs = worldPoint.y >= gate.floorHeight;
            return Locked ? !upstairs : upstairs;
        }
    }
}

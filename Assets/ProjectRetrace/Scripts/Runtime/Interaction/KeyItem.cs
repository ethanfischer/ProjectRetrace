using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// The keys. Picking them up ends phase 1; picking them up again ends phase 2 (in the
    /// default KeyPickup end mode). Position is owned by KeySpawner, not by this component.
    /// </summary>
    public class KeyItem : PickupInteractable
    {
        public override string Prompt => "Take keys";

        protected override void OnTaken(PlayerInteractor interactor)
        {
            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.OnKeyTaken();
            }
        }

        /// <summary>
        /// Called by KeySpawner after it moves the keys, so the restored position is the
        /// hiding spot for this run rather than wherever the prop sat in the scene.
        /// </summary>
        public void MakeAvailableAt(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            CaptureInitialState();
            RestoreInitialState();
        }
    }
}

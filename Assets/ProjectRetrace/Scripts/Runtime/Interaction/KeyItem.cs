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

        /// <summary>The keys sit inside a drawer, cupboard, or chest, and the prop's thin
        /// panels don't reliably stop the interaction ray -- so ask the container directly
        /// rather than trusting geometry to keep a closed drawer closed.</summary>
        public override bool CanInteract
        {
            get
            {
                if (!base.CanInteract) return false;
                var container = FindContainer();
                return container == null || container.IsOpen;
            }
        }

        private IOpenable FindContainer()
        {
            var inParent = GetComponentInParent<IOpenable>();
            if (inParent != null) return inParent;

            var spot = transform.parent;
            var prop = spot != null ? spot.parent : null;
            return prop != null ? prop.GetComponentInChildren<IOpenable>() : null;
        }

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

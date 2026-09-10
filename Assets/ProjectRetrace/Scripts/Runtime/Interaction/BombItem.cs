using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// The bomb: one per run, hidden in phase 1 like the keys but never in the same prop.
    /// Taking it puts it in the player's pocket; the carrier plants it inside any open
    /// drawer or cupboard, where it stays armed across every later round until it goes
    /// off. Anyone who then opens that container -- a ghost pausing at the stop that used
    /// it, or the player -- sets it off: the ghost's route leaves the run for good, the
    /// player loses a life. It spawns unarmed so finding it in phase 1 is safe.
    ///
    /// Restore keeps three facts the base pickup would forget: a carried bomb stays in
    /// the pocket, a planted bomb stays planted, a spent bomb stays gone.
    /// </summary>
    public class BombItem : PickupInteractable
    {
        public static BombItem Current { get; private set; }

        private PlayerBombCarrier _carrier;
        private IOpenable _armedIn;

        public override string Prompt => "Take bomb";

        public bool Carried => _carrier != null;
        public bool Armed => _armedIn != null;
        public bool Spent { get; private set; }

        /// <summary>The drawer or door the bomb sits behind while armed. Read by the key
        /// spawner so the keys are never hidden in the trap.</summary>
        public IOpenable ArmedIn => _armedIn;

        /// <summary>Once planted it cannot be picked back up: opening the container is
        /// the trigger, so a safe "take it back" would contradict the rule.</summary>
        public override bool CanInteract => base.CanInteract && !Spent && !Armed && !Carried;

        protected override void OnEnable()
        {
            base.OnEnable();
            Current = this;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Current == this) Current = null;
        }

        protected override void OnTaken(PlayerInteractor interactor)
        {
            var carrier = interactor.GetComponentInParent<PlayerBombCarrier>();
            if (carrier == null)
            {
                Debug.LogWarning("[BombItem] The player has no PlayerBombCarrier -- run Setup Scene Systems.", this);
                return;
            }

            _carrier = carrier;
            carrier.Hold(this);
            var bank = SoundBank.Instance;
            if (bank != null) SoundBank.PlayAt(bank.pickUp, transform.position);
        }

        /// <summary>Called by KeySpawner: the spawn spot is the bomb's home for the run.</summary>
        public void MakeAvailableAt(Transform spot)
        {
            transform.SetParent(spot, false);
            transform.SetPositionAndRotation(spot.position, spot.rotation);
            CaptureInitialState();
            RestoreInitialState();
        }

        /// <summary>Plant it: from now on the spot is the bomb's restore pose, so it rides
        /// through every RestoreAll like the keys do, and the container is the trigger.</summary>
        public void PlaceIn(KeySpotMarker spot)
        {
            if (_carrier != null) _carrier.Release(this);
            _carrier = null;
            _armedIn = spot.Openable;
            MakeAvailableAt(spot.transform);
        }

        /// <summary>True, and the bomb is gone, when the given prop is the armed container.</summary>
        public static bool TryDetonateAt(InteractableBase prop)
        {
            var bomb = Current;
            if (bomb == null || !bomb.Armed || bomb.Spent) return false;
            if (!ReferenceEquals(prop, bomb._armedIn)) return false;

            bomb.Detonate();
            return true;
        }

        private void Detonate()
        {
            Spent = true;
            _armedIn = null;
            SetVisible(false);
            var bank = SoundBank.Instance;
            if (bank == null) return;
            var clip = bank.bombExplode != null ? bank.bombExplode : bank.ghostStunned;
            SoundBank.PlayAt(clip, transform.position, bank.bombExplode != null ? 1f : 0.6f, 1.5f);
        }

        /// <summary>New run: back to the unarmed pickup the spawner is about to place.</summary>
        public void ResetForRun()
        {
            if (_carrier != null) _carrier.Release(this);
            _carrier = null;
            _armedIn = null;
            Spent = false;
        }

        /// <summary>Online matches do not carry the bomb yet, so the run plays without it.</summary>
        public void Retire()
        {
            ResetForRun();
            Spent = true;
            SetVisible(false);
        }

        public override void RestoreInitialState()
        {
            if (Carried || Spent)
            {
                SetVisible(false);
                return;
            }

            base.RestoreInitialState();
        }
    }
}

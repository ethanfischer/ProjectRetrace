using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// A small stack of cash in a drawer. Score only: taking it credits the current
    /// player's total and changes nothing else, so the house rewards a thorough search
    /// without touching the difficulty curve. A collected stack stays gone through the
    /// round's retries -- a restore that brought it back would pay the player for dying.
    /// Placement is the CashSpawner's business; this component only knows its amount.
    /// </summary>
    public class CashItem : PickupInteractable
    {
        public int Amount { get; private set; }
        public bool Collected { get; private set; }

        public override string Prompt => "Take $" + Amount;
        public override bool CanInteract => base.CanInteract && !Collected;

        protected override void OnTaken(PlayerInteractor interactor)
        {
            Collected = true;
            var bank = SoundBank.Instance;
            if (bank != null) SoundBank.PlayAt(bank.pickUp, transform.position, 1.15f);
            if (GameDirector.Instance != null) GameDirector.Instance.OnCashTaken(Amount);
        }

        /// <summary>Called by CashSpawner: this spot is the stack's home for the round.</summary>
        public void Spawn(int amount, Transform spot)
        {
            Amount = amount;
            Collected = false;
            transform.SetParent(spot, false);
            transform.SetPositionAndRotation(spot.position, spot.rotation);
            CaptureInitialState();
            RestoreInitialState();
        }

        /// <summary>Not in play this round: hidden, and restore keeps it that way.</summary>
        public void Retire()
        {
            Collected = true;
            SetVisible(false);
        }

        public override void RestoreInitialState()
        {
            if (Collected)
            {
                SetVisible(false);
                return;
            }

            base.RestoreInitialState();
        }
    }
}

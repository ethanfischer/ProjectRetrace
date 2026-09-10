using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>
    /// One pair of hands: the throwable the player is carrying, and the key that throws it.
    /// Separate from PlayerInteractor because a throw is not a use -- the interactor's
    /// event drives dwell collapsing, and a throw must never be folded into a stop.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerThrower : MonoBehaviour
    {
        [Tooltip("Where a carried item sits, under the camera so it follows the view.")]
        public Transform handAnchor;

        [Tooltip("The camera: throws go where the player is looking.")]
        public Transform rayOrigin;

        /// <summary>Raised after a throw with the release point, direction, and speed.
        /// The trail listens so the ghost throws from the same spot the same way.</summary>
        public event Action<ThrowableInteractable, Vector3, Vector3, float> Thrown;

        private bool _inputEnabled = true;

        public ThrowableInteractable Held { get; private set; }

        public string Prompt => Held != null ? "Throw" : null;

        public void SetInputEnabled(bool inputEnabled)
        {
            _inputEnabled = inputEnabled;
        }

        public bool TryHold(ThrowableInteractable throwable)
        {
            if (Held != null || throwable == null || handAnchor == null) return false;
            Held = throwable;
            throwable.AttachToHand(handAnchor, this);
            return true;
        }

        /// <summary>The house reset took the item away.</summary>
        public void Release(ThrowableInteractable throwable)
        {
            if (Held == throwable) Held = null;
        }

        public void Throw()
        {
            if (Held == null) return;

            var item = Held;
            Held = null;
            var origin = handAnchor.position;
            var direction = rayOrigin != null ? rayOrigin.forward : transform.forward;
            var speed = RetraceConfig.Current.throwSpeed;

            item.ReleaseFromHand();
            item.Projectile.Launch(Projectile.Thrower.Player, null, origin, direction * speed);

            var bank = SoundBank.Instance;
            if (bank != null) SoundBank.PlayAt(bank.throwWhoosh, origin);

            Thrown?.Invoke(item, origin, direction, speed);
        }

        private void Update()
        {
            if (!_inputEnabled || Held == null) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[RetraceConfig.Current.ThrowKey].wasPressedThisFrame) Throw();
        }
    }
}

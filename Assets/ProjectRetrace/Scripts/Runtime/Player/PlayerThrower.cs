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
            var bank = SoundBank.Instance;
            if (bank != null) SoundBank.PlayAt(bank.pickUp, handAnchor.position);
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
            var config = RetraceConfig.Current;
            var origin = handAnchor.position;
            var speed = config.throwSpeed;
            var direction = AimDirection(origin, speed);

            item.ReleaseFromHand();
            item.Projectile.Launch(Projectile.Thrower.Player, null, origin, direction * speed);

            var bank = SoundBank.Instance;
            if (bank != null) SoundBank.PlayAt(bank.throwWhoosh, origin);

            Thrown?.Invoke(item, origin, direction, speed);
        }

        /// <summary>The hand sits beside and below the eye, so a throw along the camera's
        /// forward lands beside and below the reticle. Aim from the hand at whatever the
        /// reticle is on instead, pitched by the ballistic solution for the throw speed so
        /// the mug meets that point rather than passing under it. Out of range, the low
        /// arc gives way to 45 degrees for maximum reach.</summary>
        private Vector3 AimDirection(Vector3 origin, float speed)
        {
            const float farAim = 30f;
            var eye = rayOrigin != null ? rayOrigin : transform;
            var target = eye.position + eye.forward * farAim;
            var hits = Physics.RaycastAll(new Ray(eye.position, eye.forward), farAim, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (var i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider.transform.root == transform.root) continue;
                target = hits[i].point;
                break;
            }

            var toTarget = target - origin;
            var flat = new Vector3(toTarget.x, 0f, toTarget.z);
            if (flat.sqrMagnitude < 0.0001f) return toTarget.normalized;
            var yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            return Quaternion.Euler(-BallisticPitchDegrees(flat.magnitude, toTarget.y, speed), yaw, 0f) * Vector3.forward;
        }

        /// <summary>Launch elevation that lands a projectile of the given speed on a point
        /// at horizontal distance x and height dy. The lower of the two solutions, so the
        /// throw stays flat and readable.</summary>
        public static float BallisticPitchDegrees(float x, float dy, float speed)
        {
            var g = Mathf.Abs(Physics.gravity.y);
            var v2 = speed * speed;
            var discriminant = v2 * v2 - g * (g * x * x + 2f * dy * v2);
            if (discriminant < 0f || x < 0.0001f) return 45f;
            return Mathf.Atan2(v2 - Mathf.Sqrt(discriminant), g * x) * Mathf.Rad2Deg;
        }

        private void Update()
        {
            if (!_inputEnabled || Held == null) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[RetraceConfig.Current.ThrowKey].wasPressedThisFrame) Throw();
        }
    }
}

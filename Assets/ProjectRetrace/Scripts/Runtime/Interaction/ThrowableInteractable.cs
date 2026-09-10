using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// A loose prop the player can carry and throw. Use picks it up; the throw key is the
    /// thrower's business. A throwable is only ever restored, never respawned: each attempt
    /// finds it where the round began, in the pose it was captured in.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Projectile))]
    public class ThrowableInteractable : InteractableBase
    {
        private Projectile _projectile;
        private Transform _initialParent;
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private PlayerThrower _holder;

        public Projectile Projectile => _projectile;
        public bool Held => _holder != null;
        public override string Prompt => "Pick up";

        /// <summary>Anything at rest can be picked up again, a ghost's missed throw
        /// included; only a prop still in the air is off limits.</summary>
        public override bool CanInteract => base.CanInteract && !Held && (!_projectile.Launched || _projectile.Resting);

        /// <summary>A ghost may take it unless the player holds it or it is mid-air.</summary>
        public bool AvailableToGhost => !Held && (!_projectile.Launched || _projectile.Resting);

        private void Awake()
        {
            _projectile = GetComponent<Projectile>();
            CaptureInitialState();
        }

        public override void Interact(PlayerInteractor interactor)
        {
            var thrower = interactor.GetComponentInParent<PlayerThrower>();
            if (thrower != null) thrower.TryHold(this);
        }

        public void AttachToHand(Transform anchor, PlayerThrower holder)
        {
            _holder = holder;
            _projectile.HoldAt(anchor.position, anchor.rotation);
            transform.SetParent(anchor, true);
        }

        /// <summary>Back under its own parent before it flies, so a route id looked up
        /// mid-flight still resolves and the camera stops carrying it.</summary>
        public void ReleaseFromHand()
        {
            transform.SetParent(_initialParent, true);
            _holder = null;
        }

        /// <summary>Spectator side: the stream says where it is, nothing here simulates.</summary>
        public void PuppetTo(Vector3 position, Quaternion rotation)
        {
            if (transform.parent != _initialParent) transform.SetParent(_initialParent, true);
            _holder = null;
            _projectile.HoldAt(position, rotation);
        }

        /// <summary>Local pose under the captured parent, for the same reason the keys use
        /// one: RestoreAll runs in registry order, and a world pose could land while a
        /// container it sits on is still open.</summary>
        public override void CaptureInitialState()
        {
            _initialParent = transform.parent;
            _initialLocalPosition = transform.localPosition;
            _initialLocalRotation = transform.localRotation;
        }

        public override void RestoreInitialState()
        {
            if (_holder != null) _holder.Release(this);
            _holder = null;
            _projectile.ResetState();
            transform.SetParent(_initialParent, false);
            transform.localPosition = _initialLocalPosition;
            transform.localRotation = _initialLocalRotation;
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>
    /// The pocket the bomb rides in, and the key that plants it. Its own key rather than
    /// Use because Use on an open drawer means "close it", and a player lining up a trap
    /// must not slam the drawer instead. The target is whatever open container the
    /// interactor already has under the reticle, so aiming works exactly as it does for
    /// opening things.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerBombCarrier : MonoBehaviour
    {
        public PlayerInteractor interactor;

        private bool _inputEnabled = true;
        private KeySpotMarker _target;

        public BombItem Held { get; private set; }

        public bool Carrying => Held != null;

        /// <summary>Null unless the bomb is carried and an open container is in reach.</summary>
        public string Prompt => _target != null ? "Place bomb" : null;

        public void SetInputEnabled(bool inputEnabled)
        {
            _inputEnabled = inputEnabled;
            if (!inputEnabled) _target = null;
        }

        public void Hold(BombItem bomb)
        {
            Held = bomb;
        }

        public void Release(BombItem bomb)
        {
            if (Held == bomb) Held = null;
        }

        private void Update()
        {
            if (!_inputEnabled || Held == null || interactor == null)
            {
                _target = null;
                return;
            }

            _target = TargetSpot();
            if (_target == null) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[RetraceConfig.Current.BombKey].wasPressedThisFrame) Held.PlaceIn(_target);
        }

        private KeySpotMarker TargetSpot()
        {
            if (!(interactor.Current is IOpenable openable) || !openable.IsOpen) return null;
            return KeySpotMarker.OwnedBy(openable);
        }
    }
}

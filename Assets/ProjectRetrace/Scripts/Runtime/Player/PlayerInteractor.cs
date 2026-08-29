using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>Raycasts from the camera and drives whatever interactable is under the reticle.</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public Transform rayOrigin;
        [SerializeField] private float reach = 2.5f;
        [SerializeField] private LayerMask interactableMask = ~0;
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        private IInteractable _current;
        private bool _inputEnabled = true;

        /// <summary>Null when nothing usable is in front of the player. Read by the HUD.</summary>
        public IInteractable Current => _current;

        public string CurrentPrompt => _current != null ? _current.Prompt : null;

        public void SetInputEnabled(bool inputEnabled)
        {
            _inputEnabled = inputEnabled;
            if (!inputEnabled) _current = null;
        }

        private void Awake()
        {
            if (rayOrigin == null && Camera.main != null)
            {
                rayOrigin = Camera.main.transform;
            }
        }

        private void Update()
        {
            if (!_inputEnabled)
            {
                return;
            }

            _current = FindTarget();

            if (_current != null && Input.GetKeyDown(interactKey))
            {
                _current.Interact(this);
            }
        }

        private IInteractable FindTarget()
        {
            if (rayOrigin == null) return null;

            var ray = new Ray(rayOrigin.position, rayOrigin.forward);
            if (!Physics.Raycast(ray, out var hit, reach, interactableMask, QueryTriggerInteraction.Ignore))
            {
                return null;
            }

            // GetComponentInParent so a prop can carry its collider on a child mesh.
            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            return interactable != null && interactable.CanInteract ? interactable : null;
        }
    }
}

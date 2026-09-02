using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Anything the player can use. Every interactable captures its own opening state and
    /// restores it on the phase transition, so nothing depends on a human remembering to
    /// reset objects in the scene.
    /// </summary>
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Use";

        private string _id;

        public virtual string Prompt => prompt;

        /// <summary>Stable across machines running the same build, and fixed for the
        /// component's lifetime: the keys are reparented into their hiding spot, and a
        /// route that named them must still find them afterwards.</summary>
        public string Id => _id ?? (_id = HierarchyPath.Of(transform));
        public virtual bool CanInteract => isActiveAndEnabled;

        protected virtual void OnEnable()
        {
            InteractableRegistry.Register(this);
        }

        protected virtual void OnDisable()
        {
            InteractableRegistry.Unregister(this);
        }

        public abstract void Interact(PlayerInteractor interactor);

        /// <summary>Called once at the start of the run, before the player can touch anything.</summary>
        public abstract void CaptureInitialState();

        /// <summary>Called on the phase transition. Must leave the object exactly as phase 1 found it.</summary>
        public abstract void RestoreInitialState();
    }
}

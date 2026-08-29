using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Tracks every live interactable so the phase transition can reset the whole house in
    /// one call, without the director needing scene references to individual props.
    /// </summary>
    public static class InteractableRegistry
    {
        private static readonly List<InteractableBase> Interactables = new List<InteractableBase>();

        public static IReadOnlyList<InteractableBase> All => Interactables;

        /// <summary>
        /// Statics survive play-mode entry when Domain Reload is disabled (which many people
        /// turn on for faster iteration), so the list is explicitly cleared on load.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Interactables.Clear();
        }

        public static void Register(InteractableBase interactable)
        {
            if (interactable == null || Interactables.Contains(interactable)) return;
            Interactables.Add(interactable);
        }

        public static void Unregister(InteractableBase interactable)
        {
            Interactables.Remove(interactable);
        }

        public static void CaptureAll()
        {
            for (var i = 0; i < Interactables.Count; i++)
            {
                if (Interactables[i] != null) Interactables[i].CaptureInitialState();
            }
        }

        public static void RestoreAll()
        {
            for (var i = 0; i < Interactables.Count; i++)
            {
                if (Interactables[i] != null) Interactables[i].RestoreInitialState();
            }
        }
    }
}

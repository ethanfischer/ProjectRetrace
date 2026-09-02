using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Tracks every live interactable so the phase transition can reset the whole house in
    /// one call, without the director needing scene references to individual props. Also
    /// the lookup from a prop's stable id (its hierarchy path) back to the component, which
    /// is how a route recorded on one machine names a prop on another.
    /// </summary>
    public static class InteractableRegistry
    {
        private static readonly List<InteractableBase> Interactables = new List<InteractableBase>();
        private static readonly Dictionary<string, InteractableBase> ById = new Dictionary<string, InteractableBase>();

        public static IReadOnlyList<InteractableBase> All => Interactables;

        /// <summary>
        /// Statics survive play-mode entry when Domain Reload is disabled (which many people
        /// turn on for faster iteration), so the list is explicitly cleared on load.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Interactables.Clear();
            ById.Clear();
        }

        public static void Register(InteractableBase interactable)
        {
            if (interactable == null || Interactables.Contains(interactable)) return;
            Interactables.Add(interactable);
            ById[interactable.Id] = interactable;
        }

        public static void Unregister(InteractableBase interactable)
        {
            Interactables.Remove(interactable);
            if (interactable != null && ById.TryGetValue(interactable.Id, out var registered) && registered == interactable)
            {
                ById.Remove(interactable.Id);
            }
        }

        public static InteractableBase Find(string id)
        {
            return !string.IsNullOrEmpty(id) && ById.TryGetValue(id, out var interactable) ? interactable : null;
        }

        public static string IdOf(IInteractable interactable)
        {
            return interactable is InteractableBase registered ? registered.Id : null;
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

        /// <summary>Every openable's state, for a spectator to mirror.</summary>
        public static void SnapshotOpenables(List<PropState> into)
        {
            into.Clear();
            for (var i = 0; i < Interactables.Count; i++)
            {
                if (Interactables[i] is IOpenable openable)
                {
                    into.Add(new PropState { id = Interactables[i].Id, open = openable.IsOpen });
                }
            }
        }

        public static void ApplyOpenables(IList<PropState> states)
        {
            if (states == null) return;
            for (var i = 0; i < states.Count; i++)
            {
                if (Find(states[i].id) is IOpenable openable && openable.IsOpen != states[i].open)
                {
                    openable.SetOpen(states[i].open);
                }
            }
        }
    }
}

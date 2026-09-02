using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Picks a hiding spot using the run seed, so phase 1 is not identical every playtest.
    /// The transition calls this a second time with a derived seed and the phase-1 spot
    /// excluded, so phase 2 hides the keys somewhere genuinely new. Spots come exclusively
    /// from KeySpotMarkers in the scene (inside furniture props), discovered at placement
    /// time -- no manual wiring.
    /// </summary>
    public class KeySpawner : MonoBehaviour
    {
        public KeyItem key;

        /// <summary>The spot chosen by the most recent placement, for the transition to exclude.</summary>
        public Transform LastSpot { get; private set; }

        public void PlaceKey(int seed, Transform exclude = null)
        {
            if (key == null)
            {
                Debug.LogError("[KeySpawner] No KeyItem assigned -- phase 1 can never end.", this);
                return;
            }

            var spots = ValidSpots();

            // Only honour the exclusion when another spot exists; a one-spot scene reusing
            // the phase-1 hiding place beats the keys not existing at all.
            if (exclude != null && spots.Count > 1)
            {
                spots.Remove(exclude);
            }

            if (spots.Count == 0)
            {
                Debug.LogWarning("[KeySpawner] No KeySpotMarkers in the scene -- leaving the keys where they are.", this);
                key.MakeAvailableAt(key.transform.position, key.transform.rotation);
                LastSpot = null;
                return;
            }

            var random = new System.Random(seed);
            var spot = spots[random.Next(spots.Count)];
            LastSpot = spot;

            // Parented so the keys ride along when their hiding place moves (a sliding drawer,
            // a swinging lid). Restore still uses the world pose captured here, which is the
            // closed state, so the phase transition puts them back correctly regardless.
            key.transform.SetParent(spot, false);
            key.MakeAvailableAt(spot.position, spot.rotation);
        }

        private static List<Transform> ValidSpots()
        {
            // Sorted by hierarchy path rather than instance id: instance ids differ from one
            // process to the next, and an online opponent must draw the same spot from the
            // same seed on their own machine.
            var markers = Object.FindObjectsByType<KeySpotMarker>(FindObjectsSortMode.None);
            var doors = Object.FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None);
            var spots = new List<Transform>(markers.Length);
            for (var i = 0; i < markers.Length; i++)
            {
                if (IsSealed(markers[i].transform.position, doors)) continue;
                spots.Add(markers[i].transform);
            }

            spots.Sort((a, b) => string.CompareOrdinal(HierarchyPath.Of(a), HierarchyPath.Of(b)));
            return spots;
        }

        /// <summary>Keys behind a locked door would make the round unwinnable.</summary>
        private static bool IsSealed(Vector3 point, DoorInteractable[] doors)
        {
            for (var i = 0; i < doors.Length; i++)
            {
                if (doors[i].Seals(point)) return true;
            }

            return false;
        }
    }
}

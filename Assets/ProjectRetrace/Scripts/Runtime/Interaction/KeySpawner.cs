using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Picks a hiding spot using the run seed, so phase 1 is not identical every playtest. The
    /// seed is reused on the transition, so phase 2 is the same house you just searched. Spots
    /// come exclusively from KeySpotMarkers in the scene (inside furniture props), discovered
    /// at placement time -- no manual wiring.
    /// </summary>
    public class KeySpawner : MonoBehaviour
    {
        public KeyItem key;

        public void PlaceKey(int seed)
        {
            if (key == null)
            {
                Debug.LogError("[KeySpawner] No KeyItem assigned -- phase 1 can never end.", this);
                return;
            }

            var spots = ValidSpots();
            if (spots.Count == 0)
            {
                Debug.LogWarning("[KeySpawner] No KeySpotMarkers in the scene -- leaving the keys where they are.", this);
                key.MakeAvailableAt(key.transform.position, key.transform.rotation);
                return;
            }

            var random = new System.Random(seed);
            var spot = spots[random.Next(spots.Count)];

            // Parented so the keys ride along when their hiding place moves (a sliding drawer,
            // a swinging lid). Restore still uses the world pose captured here, which is the
            // closed state, so the phase transition puts them back correctly regardless.
            key.transform.SetParent(spot, false);
            key.MakeAvailableAt(spot.position, spot.rotation);
        }

        private static List<Transform> ValidSpots()
        {
            // InstanceID sort keeps the order identical across the two PlaceKey calls in a run,
            // so the same seed lands on the same spot in phase 2.
            var markers = Object.FindObjectsByType<KeySpotMarker>(FindObjectsSortMode.InstanceID);
            var spots = new List<Transform>(markers.Length);
            for (var i = 0; i < markers.Length; i++)
            {
                spots.Add(markers[i].transform);
            }

            return spots;
        }
    }
}

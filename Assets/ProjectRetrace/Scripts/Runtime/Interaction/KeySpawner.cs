using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Picks a hiding spot from the candidates using the run seed, so phase 1 is not identical
    /// every playtest. The seed is reused on the transition, so phase 2 is the same house you
    /// just searched.
    /// </summary>
    public class KeySpawner : MonoBehaviour
    {
        public KeyItem key;
        public List<Transform> candidateSpots = new List<Transform>();

        private int _chosenIndex = -1;

        public int ChosenIndex => _chosenIndex;

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
                Debug.LogWarning("[KeySpawner] No candidate spots assigned -- leaving the keys where they are.", this);
                key.MakeAvailableAt(key.transform.position, key.transform.rotation);
                return;
            }

            var random = new System.Random(seed);
            _chosenIndex = random.Next(spots.Count);
            var spot = spots[_chosenIndex];
            key.MakeAvailableAt(spot.position, spot.rotation);
        }

        private List<Transform> ValidSpots()
        {
            var spots = new List<Transform>();
            for (var i = 0; i < candidateSpots.Count; i++)
            {
                if (candidateSpots[i] != null) spots.Add(candidateSpots[i]);
            }

            return spots;
        }
    }
}

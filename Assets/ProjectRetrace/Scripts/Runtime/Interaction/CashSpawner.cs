using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Hides a fresh batch of cash every round: a seeded share of the same key spots the
    /// keys use, never in the keys' own prop (that drawer pays out anyway) and never
    /// behind the armed bomb. A pool of clones of one template is reused round to round,
    /// so the registry never sees stacks come and go mid-run.
    /// </summary>
    public class CashSpawner : MonoBehaviour
    {
        [Tooltip("Inactive stack the pool is cloned from. Built by ProjectRetrace > Setup Scene Systems.")]
        public CashItem template;

        private readonly List<CashItem> _pool = new List<CashItem>();

        public void Place(int seed, Transform keySpot)
        {
            if (template == null)
            {
                Debug.LogWarning("[CashSpawner] No template assigned -- no cash this run.", this);
                return;
            }

            var spots = KeySpawner.ValidSpots();
            ExcludeProp(spots, keySpot);
            ExcludeArmedBomb(spots);

            var config = RetraceConfig.Current;
            var random = new System.Random(seed);
            Shuffle(spots, random);

            var count = spots.Count == 0 ? 0 : Mathf.Clamp(Mathf.RoundToInt(spots.Count * config.cashDrawerFraction), 1, spots.Count);
            var low = Mathf.Min(config.cashMin, config.cashMax);
            var high = Mathf.Max(config.cashMin, config.cashMax);

            EnsurePool(count);
            for (var i = 0; i < _pool.Count; i++)
            {
                if (i < count) _pool[i].Spawn(random.Next(low, high + 1), spots[i]);
                else _pool[i].Retire();
            }
        }

        /// <summary>Online matches and disabled runs: every stack out of play.</summary>
        public void Clear()
        {
            foreach (var item in _pool) item.Retire();
        }

        private void EnsurePool(int count)
        {
            while (_pool.Count < count)
            {
                var clone = Instantiate(template, transform);
                clone.name = "Cash " + (_pool.Count + 1);
                clone.gameObject.SetActive(true);
                _pool.Add(clone);
            }
        }

        private static void ExcludeProp(List<Transform> spots, Transform spot)
        {
            if (spot == null) return;
            var prop = PropRootOf(spot);
            spots.RemoveAll(candidate => PropRootOf(candidate) == prop);
        }

        private static void ExcludeArmedBomb(List<Transform> spots)
        {
            var bomb = BombItem.Current;
            if (bomb == null || !bomb.Armed) return;
            spots.RemoveAll(spot => ReferenceEquals(spot.GetComponent<KeySpotMarker>()?.Openable, bomb.ArmedIn));
        }

        private static Transform PropRootOf(Transform spot)
        {
            var marker = spot.GetComponent<KeySpotMarker>();
            return marker != null ? marker.PropRoot : spot.parent;
        }

        private static void Shuffle(List<Transform> list, System.Random random)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}

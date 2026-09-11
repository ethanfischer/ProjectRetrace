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
        public BombItem bomb;

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
            RestrictToForcedProp(spots);
            ExcludeArmedBomb(spots);

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

        /// <summary>Once per run, right after the keys: the same rule set, but never in the
        /// prop the keys are in -- a bomb in the next drawer down would be found with them
        /// and cost the search nothing.</summary>
        public void PlaceBomb(int seed)
        {
            if (bomb == null) return;
            bomb.ResetForRun();

            var spots = ValidSpots();
            if (LastSpot != null)
            {
                var keyProp = PropRootOf(LastSpot);
                spots.RemoveAll(spot => PropRootOf(spot) == keyProp);
            }

            if (spots.Count == 0)
            {
                Debug.LogWarning("[KeySpawner] No spot left for the bomb -- this run has none.", this);
                bomb.Retire();
                return;
            }

            var random = new System.Random(seed);
            bomb.MakeAvailableAt(spots[random.Next(spots.Count)]);
        }

        public void RemoveBomb()
        {
            if (bomb != null) bomb.Retire();
        }

        /// <summary>The keys must never hide behind the armed bomb: opening it is the
        /// trigger, and a round whose only way forward is to blow yourself up is not a round.</summary>
        private void ExcludeArmedBomb(List<Transform> spots)
        {
            if (bomb == null || !bomb.Armed || spots.Count <= 1) return;
            var kept = spots.FindAll(spot => !ReferenceEquals(spot.GetComponent<KeySpotMarker>()?.Openable, bomb.ArmedIn));
            if (kept.Count == 0) return;
            spots.Clear();
            spots.AddRange(kept);
        }

        private static Transform PropRootOf(Transform spot)
        {
            var marker = spot.GetComponent<KeySpotMarker>();
            return marker != null ? marker.PropRoot : spot.parent;
        }

        /// <summary>A playtester chasing one cupboard should not have to reroll until the
        /// keys land in it. Falls back to every spot, with a warning, when nothing matches.</summary>
        private static void RestrictToForcedProp(List<Transform> spots)
        {
            var wanted = RetraceConfig.Current.forceKeySpot;
            if (string.IsNullOrWhiteSpace(wanted)) return;

            var matching = spots.FindAll(spot => IsUnderPropNamed(spot, wanted));
            if (matching.Count == 0)
            {
                Debug.LogWarning($"[KeySpawner] forceKeySpot '{wanted}' matches no prop with a key spot -- using every spot.");
                return;
            }

            spots.Clear();
            spots.AddRange(matching);
        }

        private static bool IsUnderPropNamed(Transform spot, string wanted)
        {
            for (var ancestor = spot.parent; ancestor != null; ancestor = ancestor.parent)
            {
                if (ancestor.name.IndexOf(wanted, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }

        internal static List<Transform> ValidSpots()
        {
            // Sorted by hierarchy path rather than instance id: instance ids differ from one
            // process to the next, and an online opponent must draw the same spot from the
            // same seed on their own machine.
            var markers = Object.FindObjectsByType<KeySpotMarker>(FindObjectsSortMode.None);
            var doors = Object.FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None);
            var spots = new List<Transform>(markers.Length);
            for (var i = 0; i < markers.Length; i++)
            {
                var point = markers[i].transform.position;
                if (IsSealed(point, doors) || !FloorGate.Allows(point)) continue;
                if (IsExcluded(HierarchyPath.Of(markers[i].transform))) continue;
                spots.Add(markers[i].transform);
            }

            spots.Sort((a, b) => string.CompareOrdinal(HierarchyPath.Of(a), HierarchyPath.Of(b)));
            return spots;
        }

        /// <summary>Spots the house geometry makes unreachable -- a corner cupboard's far
        /// half that opens onto the stove, a cabinet wedged where nobody can stand. Too
        /// few and too particular for a rule (floor-in-front also condemns every wall
        /// cabinet over a counter), so they are named. Segments match by name, or by
        /// name plus sibling index when the entry carries one, so the art scene's
        /// re-import order cannot silently shift the list onto other props.</summary>
        private static readonly string[] ExcludedSpots =
        {
            "KitchenTabletop2_03/InteractiveFurniture10_05",
            "KitchenTabletop2_03/KeySpot#6",
            "InteractiveFurniture_06 (4)",
        };

        private static bool IsExcluded(string path)
        {
            var segments = path.Split('/');
            foreach (var entry in ExcludedSpots)
            {
                var wanted = entry.Split('/');
                for (var start = 0; start + wanted.Length <= segments.Length; start++)
                {
                    var match = true;
                    for (var i = 0; i < wanted.Length && match; i++) match = SegmentMatches(segments[start + i], wanted[i]);
                    if (match) return true;
                }
            }

            return false;
        }

        private static bool SegmentMatches(string segment, string wanted)
        {
            if (segment == wanted) return true;
            var hash = segment.LastIndexOf('#');
            return hash >= 0 && segment.Substring(0, hash) == wanted;
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

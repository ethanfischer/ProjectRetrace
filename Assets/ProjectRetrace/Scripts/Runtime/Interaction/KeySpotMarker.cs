using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Marks a transform as a candidate key hiding spot. KeySpawner discovers these at
    /// placement time, so duplicating a furniture prop adds its spot with no manual wiring.
    /// </summary>
    public class KeySpotMarker : MonoBehaviour
    {
        /// <summary>The prop this spot belongs to: the SearchableProp root when there is
        /// one, else the immediate parent.</summary>
        public Transform PropRoot
        {
            get
            {
                var prop = GetComponentInParent<SearchableProp>();
                return prop != null ? prop.transform : transform.parent;
            }
        }

        /// <summary>The drawer or door that stands between the player and this spot. A
        /// drawer's spot rides on the drawer itself; a door's sits on the carcass behind
        /// the leaf, so for anything not under an openable the nearest leaf on the same
        /// prop is taken. Resolved by geometry rather than wired, for the same reason the
        /// importer wires furniture by geometry: nobody has to remember to do it.</summary>
        public IOpenable Openable
        {
            get
            {
                var root = PropRoot;
                for (var t = transform.parent; t != null; t = t.parent)
                {
                    if (t.TryGetComponent<IOpenable>(out var ancestor)) return ancestor;
                    if (t == root) break;
                }

                if (root == null) return null;
                IOpenable best = null;
                var bestSqr = float.MaxValue;
                foreach (var openable in root.GetComponentsInChildren<IOpenable>())
                {
                    var sqr = (CenterOf((Component)openable) - transform.position).sqrMagnitude;
                    if (sqr >= bestSqr) continue;
                    bestSqr = sqr;
                    best = openable;
                }

                return best;
            }
        }

        /// <summary>The spot behind the given drawer or door, or null if it hides nothing.</summary>
        public static KeySpotMarker OwnedBy(IOpenable openable)
        {
            if (!(openable is Component part)) return null;
            var prop = part.GetComponentInParent<SearchableProp>();
            var search = prop != null ? prop.transform : part.transform.parent;
            if (search == null) return null;

            foreach (var spot in search.GetComponentsInChildren<KeySpotMarker>())
            {
                if (ReferenceEquals(spot.Openable, openable)) return spot;
            }

            return null;
        }

        /// <summary>A hinged leaf's pivot sits on its edge, so distance to the pivot would
        /// favour the wrong door of a pair; the visible centre is what the spot sits behind.</summary>
        private static Vector3 CenterOf(Component part)
        {
            var renderer = part.GetComponentInChildren<Renderer>();
            return renderer != null ? renderer.bounds.center : part.transform.position;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }
    }
}

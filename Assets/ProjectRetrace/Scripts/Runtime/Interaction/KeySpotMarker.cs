using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Marks a transform as a candidate key hiding spot. KeySpawner discovers these at
    /// placement time, so duplicating a furniture prop adds its spot with no manual wiring.
    /// </summary>
    public class KeySpotMarker : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }
    }
}

using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Sets a placed item down on whatever is under it. Key spots mark the inside of a
    /// part, not its floor, so an item left exactly at the marker floats mid-shelf or sits
    /// half inside the board below; a short downward cast from the item's own bounds finds
    /// the shelf, drawer bottom or carcass floor and rests the item on it. Called once at
    /// placement, before the pose is captured, so restores land in the same settled spot.
    /// </summary>
    public static class SurfaceRest
    {
        private const float MaxDrop = 1f;
        private const float Clearance = 0.004f;

        public static void Settle(Transform item)
        {
            if (!TryBounds(item, out var bounds)) return;

            var hits = Physics.RaycastAll(bounds.center, Vector3.down, MaxDrop, ~0, QueryTriggerInteraction.Ignore);
            var surface = float.NaN;
            var nearest = float.MaxValue;
            for (var i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider.transform.IsChildOf(item)) continue;
                if (hits[i].distance >= nearest) continue;
                nearest = hits[i].distance;
                surface = hits[i].point.y;
            }

            if (float.IsNaN(surface)) return;
            item.position += Vector3.up * (surface + Clearance - bounds.min.y);
        }

        private static bool TryBounds(Transform item, out Bounds bounds)
        {
            var renderers = item.GetComponentsInChildren<Renderer>(true);
            bounds = default;
            if (renderers.Length == 0) return false;

            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }
    }
}

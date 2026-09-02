using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// A place where the player used something: opened a drawer, lifted a lid, took the key.
    /// Recorded separately from crumbs because sampling is distance-based: rummaging drops
    /// no crumbs, so without these the sentry would walk straight past every spot the
    /// player actually searched.
    /// </summary>
    public class DwellPoint
    {
        public readonly Vector3 Position;

        /// <summary>The player root's yaw at the time -- actual facing, unlike a crumb's
        /// Direction, which is travel direction and is meaningless while standing still.</summary>
        public readonly float FacingYaw;

        /// <summary>Index of the last crumb dropped before this stop, so the patrol
        /// knows where along the route to pause.</summary>
        public readonly int CrumbIndex;

        /// <summary>What was used here, so the ghost can use it again -- it is how a
        /// hiding place the player once opened stops being safe.</summary>
        public readonly Transform Prop;

        public DwellPoint(Vector3 position, float facingYaw, int crumbIndex, Transform prop)
        {
            Position = position;
            FacingYaw = facingYaw;
            CrumbIndex = crumbIndex;
            Prop = prop;
        }
    }
}

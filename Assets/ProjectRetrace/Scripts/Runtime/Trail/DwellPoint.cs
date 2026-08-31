using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// A place where the player stood still during phase 1. Recorded separately from crumbs
    /// because sampling is distance-based: standing still drops no crumbs, so without these
    /// the sentry would walk straight past everywhere the player lingered.
    /// </summary>
    public class DwellPoint
    {
        public readonly Vector3 Position;

        /// <summary>The player root's yaw at the time -- actual facing, unlike a crumb's
        /// Direction, which is travel direction and is meaningless while standing still.</summary>
        public readonly float FacingYaw;

        /// <summary>Index of the last phase-1 crumb dropped before this stop, so the patrol
        /// knows where along the route to pause.</summary>
        public readonly int CrumbIndex;

        public DwellPoint(Vector3 position, float facingYaw, int crumbIndex)
        {
            Position = position;
            FacingYaw = facingYaw;
            CrumbIndex = crumbIndex;
        }
    }
}

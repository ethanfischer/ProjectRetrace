using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// One recorded walk through the house: the crumbs, the stops, and how far it ran.
    /// Every route the player completes becomes a sentry's patrol script the next round.
    /// </summary>
    public class RecordedRoute
    {
        public readonly List<Breadcrumb> Crumbs = new List<Breadcrumb>();
        public readonly List<DwellPoint> Dwells = new List<DwellPoint>();
        public float Distance;

        /// <summary>Which player walked it (1 in single player). In couch mode your own
        /// past routes hunt you and your opponent alike -- the tint tells whose ghost is
        /// whose.</summary>
        public int Owner = 1;
    }
}

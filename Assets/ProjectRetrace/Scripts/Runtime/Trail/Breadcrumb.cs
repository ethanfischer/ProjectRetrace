using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// One dropped mark. Pure data -- TrailVisualizer owns the GameObjects that draw these,
    /// so the trail keeps working with visuals switched off (which is the default).
    /// </summary>
    public class Breadcrumb
    {
        public readonly Vector3 Position;

        /// <summary>Whether this mark overlaps the other round's path (see BreadcrumbTrail).</summary>
        public bool Matched;

        public Breadcrumb(Vector3 position)
        {
            Position = position;
        }
    }
}

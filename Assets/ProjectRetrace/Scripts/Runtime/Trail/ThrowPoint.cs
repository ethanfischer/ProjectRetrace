using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// A throw the player made: where they stood, where the item left their hand, and the
    /// launch vector. Kept apart from DwellPoint on purpose -- dwells fold repeat uses within
    /// dwellRadius into one stop, and a throw made beside the drawer you just opened must
    /// still be replayed. Only the geometry is recorded: the ghost throws on its own
    /// schedule and the flight is simulated live, so a replayed throw is a dodgeable
    /// projectile rather than a scripted hit.
    /// </summary>
    public class ThrowPoint
    {
        /// <summary>The player root at the moment of release.</summary>
        public readonly Vector3 Position;

        /// <summary>Release point relative to the root, in the throw's yaw frame, so the
        /// ghost releases from the same spot beside its own body wherever it stands.</summary>
        public readonly Vector3 HandOffset;

        public readonly float Yaw;
        public readonly float Pitch;

        /// <summary>Recorded rather than read from config at replay, so retuning the throw
        /// speed mid-run never rewrites an old route.</summary>
        public readonly float Speed;

        /// <summary>Index of the last crumb dropped before the throw, so the patrol knows
        /// where along the route to stop and throw.</summary>
        public readonly int CrumbIndex;

        /// <summary>The throwable that was thrown, as a registry id; the ghost throws the
        /// same prop when it is free and a look-alike when it is not.</summary>
        public readonly string PropId;

        public ThrowPoint(Vector3 position, Vector3 handOffset, float yaw, float pitch, float speed, int crumbIndex, string propId)
        {
            Position = position;
            HandOffset = handOffset;
            Yaw = yaw;
            Pitch = pitch;
            Speed = speed;
            CrumbIndex = crumbIndex;
            PropId = propId;
        }

        /// <summary>Recording and replay share these two formulas, so what the trail writes
        /// is exactly what the ghost reconstructs.</summary>
        public static ThrowPoint Record(Vector3 rootPosition, Vector3 origin, Vector3 direction, float speed, int crumbIndex, string propId)
        {
            var dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            var yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            var pitch = -Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            var handOffset = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f)) * (origin - rootPosition);
            return new ThrowPoint(rootPosition, handOffset, yaw, pitch, speed, crumbIndex, propId);
        }

        public void Reconstruct(Vector3 rootPosition, out Vector3 origin, out Vector3 velocity)
        {
            origin = rootPosition + Quaternion.Euler(0f, Yaw, 0f) * HandOffset;
            velocity = Quaternion.Euler(Pitch, Yaw, 0f) * Vector3.forward * Speed;
        }
    }
}

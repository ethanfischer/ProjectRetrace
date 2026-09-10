using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// The physics half of a throwable: flight, the hit test, and coming to rest. Hits are
    /// a capsule-distance test rather than a collision callback because ghosts carry no
    /// colliders at all (their capsule would block their own sight rays), and one rule for
    /// both targets is easier to reason about than two. Ownership decides who a projectile
    /// can hurt: a player's throw only kills ghosts, a ghost's throw only hits the player,
    /// so ghosts can never thin their own pool.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        public enum Thrower { None, Player, Ghost }

        private const float PlayerRadius = 0.3f;
        private const float PlayerHeight = 1.8f;
        private const float RestSpeed = 0.2f;

        private static readonly List<Projectile> LiveList = new List<Projectile>();

        /// <summary>Every projectile launched this attempt, real props and ghost copies
        /// alike, so the stream and the attempt reset can find them without a scene walk.</summary>
        public static IReadOnlyList<Projectile> Live => LiveList;

        // Statics survive play-mode entry when Domain Reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => LiveList.Clear();

        private Rigidbody _rigidbody;
        private float _launchedAt;
        private float _slowSince = -1f;
        private bool _thudded;

        public Thrower ThrownBy { get; private set; }
        public PatrolSentry ThrowingGhost { get; private set; }
        public bool Launched { get; private set; }
        public bool Spent { get; private set; }
        public bool Resting { get; private set; }
        public bool IsClone { get; private set; }

        /// <summary>How the spectator stream names this object: the prop's registry id, or
        /// a made-up id for a ghost's copy.</summary>
        public string StreamId { get; private set; }

        /// <summary>In flight and fast enough to count: a mug rolling to a stop against a
        /// ghost's foot is not a kill, and neither is the frame it is still in the hand.</summary>
        public bool Armed
        {
            get
            {
                if (!Launched || Spent || Resting) return false;
                var config = RetraceConfig.Current;
                if (Time.time < _launchedAt + config.projectileArmSeconds) return false;
                return _rigidbody.linearVelocity.sqrMagnitude >= config.projectileHitMinSpeed * config.projectileHitMinSpeed;
            }
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            var throwable = GetComponent<ThrowableInteractable>();
            if (throwable != null) StreamId = throwable.Id;
        }

        public void Launch(Thrower by, PatrolSentry ghost, Vector3 origin, Vector3 velocity)
        {
            ThrownBy = by;
            ThrowingGhost = ghost;
            transform.position = origin;
            // The body learns of transform moves only at the next physics step; without
            // this a throw right after a pick-up would launch from wherever the prop was.
            _rigidbody.position = origin;
            _rigidbody.rotation = transform.rotation;
            SetCollidersEnabled(true);
            _rigidbody.isKinematic = false;
            _rigidbody.linearVelocity = velocity;
            _rigidbody.angularVelocity = Random.insideUnitSphere * 4f;
            Launched = true;
            Spent = false;
            Resting = false;
            _thudded = false;
            _launchedAt = Time.time;
            _slowSince = -1f;
            if (!LiveList.Contains(this)) LiveList.Add(this);
        }

        /// <summary>Parked in a hand, or puppeted from the stream: no physics, no collider
        /// to block anyone's sight or interaction ray.</summary>
        public void HoldAt(Vector3 position, Quaternion rotation)
        {
            StopMoving();
            SetCollidersEnabled(false);
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Inert until the attempt resets.</summary>
        public void Rest()
        {
            StopMoving();
            Resting = true;
        }

        /// <summary>The prop is back where the round found it.</summary>
        public void ResetState()
        {
            StopMoving();
            SetCollidersEnabled(true);
            Launched = false;
            Spent = false;
            Resting = false;
            ThrownBy = Thrower.None;
            ThrowingGhost = null;
            LiveList.Remove(this);
        }

        private void StopMoving()
        {
            // Velocity writes on a kinematic body are silently ignored, so zero it while it
            // is still dynamic or the old velocity resumes on the next launch.
            if (!_rigidbody.isKinematic)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            _rigidbody.isKinematic = true;
        }

        private void SetCollidersEnabled(bool enabled)
        {
            var colliders = GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++) colliders[i].enabled = enabled;
        }

        private void FixedUpdate()
        {
            if (!Launched || Resting) return;
            if (Armed) TestHit();
            TrackRest();
        }

        private void TestHit()
        {
            var director = GameDirector.Instance;
            if (director == null) return;
            var radius = PlayerRadius + RetraceConfig.Current.projectileHitRadius;

            if (ThrownBy == Thrower.Player)
            {
                var sentries = director.Sentries;
                for (var i = 0; i < sentries.Count; i++)
                {
                    var sentry = sentries[i];
                    if (sentry == null || !sentry.Alive) continue;
                    if (!HitsCapsule(transform.position, sentry.transform.position, radius, PlayerHeight)) continue;

                    Spent = true;
                    PlayHit();
                    sentry.Stun();
                    return;
                }

                return;
            }

            if (ThrownBy != Thrower.Ghost || director.player == null) return;

            // A hider is behind a door; the distance test would otherwise reach through it.
            if (director.interactor != null && director.interactor.Hiding != null) return;
            if (!HitsCapsule(transform.position, director.player.transform.position, radius, PlayerHeight)) return;

            Spent = true;
            PlayHit();
            director.OnPlayerHitByProjectile();
        }

        /// <summary>Distance from a point to the vertical capsule standing on basePosition.</summary>
        public static bool HitsCapsule(Vector3 point, Vector3 basePosition, float radius, float height)
        {
            var a = basePosition + Vector3.up * radius;
            var b = basePosition + Vector3.up * Mathf.Max(radius, height - radius);
            var ab = b - a;
            var t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            return (point - (a + ab * t)).sqrMagnitude <= radius * radius;
        }

        private void TrackRest()
        {
            if (_rigidbody.linearVelocity.sqrMagnitude > RestSpeed * RestSpeed)
            {
                _slowSince = -1f;
                return;
            }

            if (_slowSince < 0f) _slowSince = Time.time;
            else if (Time.time - _slowSince >= RetraceConfig.Current.projectileRestSeconds) Rest();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!Launched || _thudded) return;
            if (collision.relativeVelocity.magnitude < RetraceConfig.Current.projectileHitMinSpeed) return;
            _thudded = true;
            var bank = SoundBank.Instance;
            if (bank != null) SoundBank.PlayAt(bank.projectileThud, transform.position);
        }

        private void PlayHit()
        {
            var bank = SoundBank.Instance;
            if (bank != null) SoundBank.PlayAt(bank.projectileHit, transform.position);
        }

        /// <summary>A look-alike for a ghost to throw when the real prop is busy -- in the
        /// player's hand, or mid-air from another ghost. It never registers as an
        /// interactable: it would claim a bogus id, and nobody may pick it up.</summary>
        public static Projectile SpawnClone(ThrowableInteractable source, string streamId)
        {
            var clone = Instantiate(source.gameObject, source.transform.position, source.transform.rotation);
            clone.name = source.name + " (ghost copy)";
            var throwable = clone.GetComponent<ThrowableInteractable>();
            if (throwable != null)
            {
                throwable.enabled = false;
                Destroy(throwable);
            }

            var projectile = clone.GetComponent<Projectile>();
            projectile.IsClone = true;
            projectile.StreamId = streamId;
            projectile.HoldAt(source.transform.position, source.transform.rotation);
            LiveList.Add(projectile);
            return projectile;
        }

        /// <summary>Every attempt starts with only the props the house was captured with.</summary>
        public static void ClearClones()
        {
            for (var i = LiveList.Count - 1; i >= 0; i--)
            {
                var projectile = LiveList[i];
                if (projectile == null)
                {
                    LiveList.RemoveAt(i);
                    continue;
                }

                if (!projectile.IsClone) continue;
                LiveList.RemoveAt(i);
                Destroy(projectile.gameObject);
            }
        }
    }
}

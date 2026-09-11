using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Loops a walking or running track while the walker covers ground, from a fully
    /// spatialised source on the walker itself -- purely diegetic, so a sentry heard faint
    /// and to the left really is far away and to the left. Driven by measured horizontal
    /// speed rather than input for the same reason the trail is distance-based: standing
    /// still is silent by construction, and a sentry needs no input to walk. The samples
    /// are multi-second loops, so they are started and stopped rather than fired per
    /// stride; firing them per stride stacked overlapping copies that tailed off for
    /// seconds after the walker stopped.
    /// </summary>
    [DisallowMultipleComponent]
    public class FootstepEmitter : MonoBehaviour
    {
        public AudioClip clip;
        public AudioClip runClip;

        /// <summary>A frame delta longer than this is a teleport (round transitions, the
        /// sentry's route restart), not a very fast step.</summary>
        private const float TeleportThreshold = 2f;

        /// <summary>Slower than this is drift, not walking: navmesh braking, a nudge
        /// against a wall.</summary>
        private const float MovingSpeed = 0.5f;

        /// <summary>A stall shorter than this keeps the loop running: a frame hitch or a
        /// shoulder brushing a door frame would otherwise restart the track from the top.</summary>
        private const float StopGraceSeconds = 0.15f;

        private AudioSource _source;
        private Vector3 _lastPosition;
        private float _stalledFor;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 1f;
            _source.dopplerLevel = 0f;

            // Linear rolloff: the default logarithmic curve is near-silent past a few
            // metres indoors, which throws away exactly the near/far information the
            // player needs to track an unseen sentry.
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.minDistance = 1.5f;
            _source.maxDistance = 18f;
        }

        private void OnEnable()
        {
            _lastPosition = transform.position;
            _stalledFor = 0f;
        }

        private void OnDisable()
        {
            if (_source != null) _source.Stop();
        }

        private void Update()
        {
            var position = transform.position;
            var delta = position - _lastPosition;
            delta.y = 0f;
            _lastPosition = position;

            var travelled = delta.magnitude;
            var speed = travelled / Mathf.Max(Time.deltaTime, 0.0001f);
            if (travelled > TeleportThreshold)
            {
                _source.Stop();
                return;
            }

            if (speed < MovingSpeed)
            {
                _stalledFor += Time.deltaTime;
                if (_stalledFor >= StopGraceSeconds) _source.Stop();
                return;
            }

            _stalledFor = 0f;
            var config = RetraceConfig.Current;
            var wanted = runClip != null && speed > config.runFootstepSpeed ? runClip : clip;
            if (wanted == null)
            {
                _source.Stop();
                return;
            }

            _source.volume = Mathf.Clamp01(config.sfxVolume * config.footstepVolume);
            if (_source.isPlaying && _source.clip == wanted) return;

            _source.clip = wanted;
            _source.Play();
        }
    }
}

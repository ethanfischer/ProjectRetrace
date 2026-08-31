using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Plays a footstep for every stride of horizontal travel, from a fully spatialised
    /// source on the walker itself -- purely diegetic, so a sentry heard faint and to the
    /// left really is far away and to the left. Distance-triggered rather than timed for
    /// the same reason the trail is: standing still is silent by construction.
    /// </summary>
    [DisallowMultipleComponent]
    public class FootstepEmitter : MonoBehaviour
    {
        public AudioClip clip;

        [Tooltip("Metres of travel between steps.")]
        [SerializeField] private float strideMetres = 0.8f;

        [SerializeField] private float volume = 0.8f;

        [Tooltip("Random pitch spread per step, so one sample doesn't read as a metronome.")]
        [SerializeField] private float pitchJitter = 0.1f;

        /// <summary>A frame delta longer than this is a teleport (round transitions, the
        /// sentry's route restart), not a very fast step.</summary>
        private const float TeleportThreshold = 2f;

        private AudioSource _source;
        private Vector3 _lastPosition;
        private float _distanceSinceStep;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
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
            _distanceSinceStep = 0f;
        }

        private void Update()
        {
            var position = transform.position;
            var delta = position - _lastPosition;
            delta.y = 0f;
            _lastPosition = position;

            var travelled = delta.magnitude;
            if (travelled > TeleportThreshold)
            {
                _distanceSinceStep = 0f;
                return;
            }

            _distanceSinceStep += travelled;
            if (_distanceSinceStep < strideMetres || clip == null) return;

            _distanceSinceStep = 0f;
            _source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            _source.PlayOneShot(clip, volume);
        }
    }
}

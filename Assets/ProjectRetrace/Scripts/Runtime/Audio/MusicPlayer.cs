using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// One looping, non-spatial track for the whole session. Volume is re-read every
    /// frame so the Tab menu's slider is heard while it is dragged, not after a restart.
    /// There is no mixer on purpose: WebGL builds ignore AudioMixer entirely, so the
    /// music and SFX balance lives in config where every platform honours it.
    /// </summary>
    [DisallowMultipleComponent]
    public class MusicPlayer : MonoBehaviour
    {
        public AudioClip clip;

        private AudioSource _source;

        private void Awake()
        {
            // Reuse a source the scene already saved rather than stacking a second one:
            // a disabled-then-enabled component had left one behind.
            _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
        }

        private void Start()
        {
            _source.clip = clip;
        }

        /// <summary>The toggle is polled like the volume, so flipping it in the Tab menu
        /// starts or stops the track on the spot. The master level rides along here too:
        /// this is the one component that already reads audio config every frame.</summary>
        private void Update()
        {
            var config = RetraceConfig.Current;
            AudioListener.volume = Mathf.Clamp01(config.masterVolume);
            _source.volume = config.musicVolume;
            var wanted = config.musicEnabled && clip != null;
            if (wanted && !_source.isPlaying) _source.Play();
            else if (!wanted && _source.isPlaying) _source.Stop();
        }
    }
}

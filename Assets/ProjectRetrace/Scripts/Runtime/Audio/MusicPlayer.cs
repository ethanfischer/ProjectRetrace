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
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
        }

        private void Start()
        {
            if (clip == null) return;
            _source.clip = clip;
            _source.volume = RetraceConfig.Current.musicVolume;
            _source.Play();
        }

        private void Update()
        {
            _source.volume = RetraceConfig.Current.musicVolume;
        }
    }
}

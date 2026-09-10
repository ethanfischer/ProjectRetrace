using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Every one-shot the game plays, on one component so a scene is wired once and the
    /// callers stay free of clip references. World sounds get a throwaway source of their
    /// own so they outlive whatever fired them -- a caught player's sentry is deactivated
    /// moments after it whistles -- and so they can carry a pitch, which PlayClipAtPoint
    /// cannot. UI clicks come from a flat 2D source, since a menu has no position in the
    /// house.
    /// </summary>
    [DisallowMultipleComponent]
    public class SoundBank : MonoBehaviour
    {
        public AudioClip doorOpen;
        public AudioClip doorClose;
        public AudioClip cabinetOpen;
        public AudioClip cabinetClose;
        public AudioClip dresserOpen;
        public AudioClip dresserClose;
        public AudioClip ovenOpen;
        public AudioClip ovenClose;
        public AudioClip grabKeys;
        public AudioClip ghostSpawn;
        public AudioClip spotted;
        public AudioClip buttonClick;
        public AudioClip throwWhoosh;
        public AudioClip projectileThud;
        public AudioClip projectileHit;
        public AudioClip ghostStunned;

        private static SoundBank _instance;
        private AudioSource _uiSource;

        public static SoundBank Instance
        {
            get
            {
                if (_instance == null) _instance = FindFirstObjectByType<SoundBank>();
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        private void Awake()
        {
            _uiSource = gameObject.AddComponent<AudioSource>();
            _uiSource.playOnAwake = false;
            _uiSource.spatialBlend = 0f;
        }

        public static void PlayAt(AudioClip clip, Vector3 position, float pitch = 1f)
        {
            if (clip == null) return;

            var holder = new GameObject("One shot audio: " + clip.name);
            holder.transform.position = position;
            var source = holder.AddComponent<AudioSource>();
            source.clip = clip;
            source.pitch = pitch;
            source.volume = RetraceConfig.Current.sfxVolume;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;

            // Same linear indoor rolloff as the footsteps, for the same reason: the
            // default logarithmic curve is near-silent past a few metres, so a drawer
            // opening in the next room would give away nothing.
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = 18f;
            source.Play();
            Destroy(holder, clip.length / Mathf.Max(pitch, 0.1f));
        }

        public static void PlayUi(AudioClip clip)
        {
            var bank = Instance;
            if (clip == null || bank == null || bank._uiSource == null) return;
            bank._uiSource.PlayOneShot(clip, RetraceConfig.Current.sfxVolume);
        }

        public AudioClip OpenClip(OpenableSound sound) => sound switch
        {
            OpenableSound.Door => doorOpen,
            OpenableSound.Dresser => dresserOpen,
            OpenableSound.Oven => ovenOpen,
            _ => cabinetOpen,
        };

        public AudioClip CloseClip(OpenableSound sound) => sound switch
        {
            OpenableSound.Door => doorClose,
            OpenableSound.Dresser => dresserClose,
            OpenableSound.Oven => ovenClose,
            _ => cabinetClose,
        };

        /// <summary>Plays the open or shut sound for a piece of furniture at its position.
        /// Callers fire it only on an actual state change, so a restore or a repeated
        /// SetOpen from the spectator stream stays silent. Pitch jitter keeps a row of
        /// drawers from sounding like the same sample three times.</summary>
        public static void PlayOpenable(OpenableSound sound, bool opened, Vector3 position)
        {
            var bank = Instance;
            if (bank == null) return;
            var jitter = RetraceConfig.Current.furniturePitchJitter;
            PlayAt(opened ? bank.OpenClip(sound) : bank.CloseClip(sound), position, 1f + Random.Range(-jitter, jitter));
        }
    }

    /// <summary>Which pair of open/shut samples a piece of furniture uses.</summary>
    public enum OpenableSound
    {
        Cabinet,
        Door,
        Dresser,
        Oven,
    }
}

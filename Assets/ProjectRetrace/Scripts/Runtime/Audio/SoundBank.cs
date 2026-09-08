using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Every one-shot the game plays, on one component so a scene is wired once and the
    /// callers stay free of clip references. World sounds go through PlayClipAtPoint so
    /// they outlive whatever fired them -- a caught player's sentry is deactivated moments
    /// after it whistles -- while UI clicks come from a flat 2D source, since a menu has no
    /// position in the house.
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

        public static void PlayAt(AudioClip clip, Vector3 position)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, RetraceConfig.Current.sfxVolume);
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
        /// SetOpen from the spectator stream stays silent.</summary>
        public static void PlayOpenable(OpenableSound sound, bool opened, Vector3 position)
        {
            var bank = Instance;
            if (bank == null) return;
            PlayAt(opened ? bank.OpenClip(sound) : bank.CloseClip(sound), position);
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

using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>
    /// Every tuning number in the game, in one player-editable JSON file. The file lives in
    /// Application.persistentDataPath so a shipped build can write it: on first launch the
    /// defaults below are written out, and from then on the file is the truth -- there is no
    /// inspector copy of any of these values, so a tweak can never quietly live in two places.
    ///
    /// Missing entries fall back to the defaults (JsonUtility overwrites only the keys it
    /// finds), so a player can delete lines they don't care about, and an old config keeps
    /// working when a new field is added. Keybinds are stored as the InputSystem Key enum's
    /// names ("E", "F3", "Enter") so they read as words rather than numbers.
    /// </summary>
    [Serializable]
    public class RetraceConfig
    {
        public const string FileName = "retrace-config.json";

        // Player
        public float walkSpeed = 3.4f;
        public float sprintSpeed = 6.0f;
        public float jumpSpeed = 4.5f;
        public float gravity = -18f;
        public float mouseSensitivity = 2.7f;
        public float pitchLimit = 89f;
        public float interactReach = 2.5f;
        public float shellLatchRadius = 0.9f;

        // Trail. dotSpacing is metres of travel between breadcrumbs: smaller follows the
        // walked route more faithfully.
        public float dotSpacing = 0.1f;
        public float dwellRadius = 0.9f;
        public float dwellSeconds = 2f;

        // Sentry. sentrySpeed sits below walkSpeed so being followed stays escapable; the
        // chase after a spot only sells a catch that is already decided.
        public float sentrySpeed = 2.0f;
        public float chaseSpeed = 5.5f;
        public float chaseCapSeconds = 2.5f;
        public float catchDistance = 1.1f;
        public int stealthLives = 3;
        public float headStartMetres = 2f;
        public float restartDelaySeconds = 3f;
        public float fadeInSeconds = 1.5f;
        public float lookAroundSeconds = 3f;
        public float lookSweepDegrees = 45f;
        public float lookTurnDegreesPerSecond = 120f;

        // Vision
        public float visionRange = 11f;
        public float visionAngle = 80f;
        public float graceSeconds = 3f;

        // Run. The seed decides only where the keys hide, in the search and every round
        // after; turn randomisation off to replay the same hiding spots.
        public float transitionPause = 1.25f;
        public bool randomiseKeySpots = true;
        public int keySpotSeed = 12345;
        public bool debugVisibleByDefault = false;

        // Footsteps
        public float footstepStrideMetres = 1.7f;
        public float footstepVolume = 0.8f;
        public float footstepPitchJitter = 0.1f;

        // Keys
        public string interactKey = "E";
        public bool interactWithLeftClick = true;
        public string restartKey = "R";
        public string manualFinishKey = "Enter";
        public string debugToggleKey = "Backquote";
        public string menuKey = "M";
        public string configMenuKey = "Tab";

        public Key InteractKey => ParseKey(interactKey, Key.E);
        public Key RestartKey => ParseKey(restartKey, Key.R);
        public Key ManualFinishKey => ParseKey(manualFinishKey, Key.Enter);
        public Key DebugToggleKey => ParseKey(debugToggleKey, Key.Backquote);
        public Key MenuKey => ParseKey(menuKey, Key.M);
        public Key ConfigMenuKey => ParseKey(configMenuKey, Key.Tab);

        private static RetraceConfig _current;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static RetraceConfig Current
        {
            get
            {
                if (_current == null) Reload();
                return _current;
            }
        }

        /// <summary>Re-reads the file (writing the defaults first if it is missing). Consumers
        /// read Current every time they need a value, so a reload takes effect at once.</summary>
        public static void Reload()
        {
            var config = new RetraceConfig();
            var path = FilePath;
            try
            {
                if (File.Exists(path))
                {
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(path), config);
                }
                else
                {
                    Write(config, path);
                    Debug.Log("[RetraceConfig] Wrote default config to " + path);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RetraceConfig] Could not read " + path + " -- using defaults. " + e.Message);
            }

            _current = config;
        }

        /// <summary>The in-game settings menu's exit: the edited copy becomes Current and
        /// the file, so a hand edit and a menu edit can never disagree.</summary>
        public static void Save(RetraceConfig config)
        {
            try
            {
                Write(config, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RetraceConfig] Could not write " + FilePath + ". " + e.Message);
            }

            _current = config;
        }

        public RetraceConfig Clone()
        {
            return JsonUtility.FromJson<RetraceConfig>(JsonUtility.ToJson(this));
        }

        private static void Write(RetraceConfig config, string path)
        {
            File.WriteAllText(path, TidyNumbers(JsonUtility.ToJson(config, true)));
        }

        /// <summary>JsonUtility prints floats at full binary precision (3.4 becomes
        /// 3.4000000953674318), which is noise in a file people edit by hand.</summary>
        private static string TidyNumbers(string json)
        {
            return System.Text.RegularExpressions.Regex.Replace(json, @"-?\d+\.\d{5,}", match =>
                double.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture)
                    .ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
        }

        private static Key ParseKey(string name, Key fallback)
        {
            return Enum.TryParse(name, true, out Key key) ? key : fallback;
        }

        // Statics survive play-mode entry when Domain Reload is off, so a stale config would
        // otherwise carry over between editor sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _current = null;
        }
    }
}

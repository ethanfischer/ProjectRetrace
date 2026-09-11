using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

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

        // Input first: the settings players actually come here for. Everything below is
        // tuning that most players never touch.
        [ConfigTab("Input")]
        public float mouseSensitivity = 2.7f;
        public float pitchLimit = 89f;
        public string interactKey = "E";
        public bool interactWithLeftClick = true;
        public string hideKey = "H";
        public string throwKey = "F";
        public string bombKey = "C";
        public string restartKey = "R";
        public string manualFinishKey = "Enter";
        public string debugToggleKey = "Backquote";
        public string menuKey = "M";
        public string configMenuKey = "Tab";
        public string spectatorCameraKey = "C";

        // Player
        [ConfigTab("Player")]
        public float walkSpeed = 3.4f;
        public float sprintSpeed = 6.0f;
        public float gravity = -18f;
        // stepHeight is what the feet can climb anywhere; stairStepHeight applies only beside
        // a flight marked with Stairs. Keep the first below the lowest seat in the house.
        public float stepHeight = 0.05f;
        public float stairStepHeight = 0.3f;
        public float interactReach = 2.5f;
        public float shellLatchRadius = 0.9f;

        // Trail. dotSpacing is metres of travel between breadcrumbs: smaller follows the
        // walked route more faithfully.
        public float dotSpacing = 0.1f;
        public float dwellRadius = 0.9f;

        // Footprints: the player's own prints as they walk, and each ghost's route under it
        // in the stealth rounds. footprintStride is metres between prints.
        public bool footprintsEnabled = true;
        public float footprintStride = 0.65f;

        // Night: the whole house in one flat blue wash, applied at runtime over the scene's
        // authored daylight so the scene file never has to change hands with the setting.
        public bool nightMode = false;

        // Hiding. Peeking through the door crack: how far you can turn, and how tall the
        // crack is as a fraction of the screen.
        public float peekYawDegrees = 20f;
        public float peekSlitHeight = 0.08f;

        public bool showInteractionPrompt = false;

        // Round banner: shown once at the start of each phase, then gone, so the HUD is
        // not narrating a goal the player already knows.
        public float bannerHoldSeconds = 5f;
        public float bannerFadeSeconds = 1f;

        // Sentry. sentrySpeed sits below walkSpeed so being followed stays escapable; the
        // chase after a spot only sells a catch that is already decided.
        [ConfigTab("Sentries")]
        public float sentrySpeed = 2.0f;
        public float chaseSpeed = 5.5f;
        public float chaseCapSeconds = 2.5f;
        public float catchDistance = 1.1f;
        public int stealthLives = 3;
        public float headStartMetres = 2f;
        public float restartDelaySeconds = 3f;
        public float fadeInSeconds = 1.5f;
        public float lookAroundSeconds = 3f;
        public float lookSweepDegrees = 0.1f;
        public float lookTurnDegreesPerSecond = 0.1f;

        // How fast a ghost turns to face the prop it is about to use. Separate from the
        // look-around sweep, which is tuned to near zero so a stop reads as a stare.
        public float interactTurnDegreesPerSecond = 240f;

        // On, ghosts re-open whatever the player used at each stop -- drawers, lids, doors.
        // Off by default: a cupboard is only opened when someone is hiding in it, which
        // keeps the house quiet enough to read where a ghost has actually been.
        public bool sentriesOpenFurniture = false;

        // A thrown prop stuns a ghost: it fades out, stays blind this long, then fades
        // back in where it stood with the usual grace period.
        public float sentryStunSeconds = 3f;
        public float sentryStunFadeSeconds = 0.3f;

        // Vision
        public float visionRange = 7f;
        public float visionAngle = 30f;
        public float graceSeconds = 3f;

        // Throwing. A hit ghost is out for the attempt; a ghost replays your throw with
        // the recorded vector after a short wind-up, and a projectile counts only while it
        // is genuinely flying (armed, and above the minimum speed).
        [ConfigTab("Throwing")]
        public float throwSpeed = 11f;
        public float throwWindupSeconds = 0.6f;
        public float projectileHitRadius = 0.35f;
        public float projectileHitMinSpeed = 3f;
        public float projectileArmSeconds = 0.1f;
        public float projectileRestSeconds = 1f;

        // Run. The seed decides only where the keys hide, in the search and every round
        // after; turn randomisation off to replay the same hiding spots.
        [ConfigTab("Run")]
        public float transitionPause = 1.25f;
        public bool randomiseKeySpots = true;
        public int keySpotSeed = 12345;

        /// <summary>Displayed round from which the stairs open; until then the keys stay
        /// downstairs, from then on they hide only upstairs.</summary>
        public int upstairsUnlockRound = 4;

        /// <summary>Off, the stair barrier never appears and the keys may hide on either
        /// floor from round one.</summary>
        public bool lockUpstairs = true;

        /// <summary>Off, no bomb spawns and the run plays as before. The bomb removes a
        /// ghost for the rest of the run, which flattens the difficulty curve; on by
        /// default anyway because setting the trap turned out to be the fun part.</summary>
        public bool bombEnabled = true;

        /// <summary>Cash in drawers, re-hidden each round: score only. cashDrawerFraction is
        /// the share of key spots that hold a stack. cashValues is the draw bag: each stack's
        /// worth is one entry picked at random, so repeats set the odds -- the default is a
        /// 60/30/10 split of 1, 5 and 10.</summary>
        public bool cashEnabled = true;
        public float cashDrawerFraction = 0.25f;
        public string cashValues = "1,1,1,1,1,1,5,5,5,10";

        /// <summary>What a spare life costs when the last one goes. 0 disables the offer.</summary>
        public int extraLifePrice = 100;

        /// <summary>Testing aid: part of a prop's name (say "InteractiveFurniture_06 (1)")
        /// restricts the hide to key spots inside matching props. Empty for normal play.</summary>
        public string forceKeySpot = "";
        public bool debugVisibleByDefault = false;

        // Footsteps
        [ConfigTab("Audio")]
        /// <summary>Scales everything through AudioListener.volume: one number the player
        /// reaches for first, rather than three to balance. Range makes the menu draw it
        /// as a slider.</summary>
        [Range(0f, 1f)]
        public float masterVolume = 1f;

        /// <summary>Music and SFX get their own sliders under master so two people can
        /// settle the balance by ear on their own machines: the track never stops, so a
        /// cabinet creak has to read over it, and where that line sits is taste.</summary>
        [Range(0f, 1f)]
        public float sfxVolume = 0.7f;
        [Range(0f, 1f)]
        public float musicVolume = 0.6f;

        /// <summary>A scale under sfxVolume, not a level of its own: footsteps run on a
        /// looping source rather than through SoundBank, so they would slip past the SFX
        /// slider otherwise.</summary>
        public float footstepVolume = 0.5f;

        /// <summary>Same idea for the spotted whistle: at full SFX level it was the loudest
        /// thing in the game.</summary>
        public float spottedVolume = 0.25f;

        /// <summary>Horizontal speed above which a stride uses the running sample. Sits
        /// between a walk and a sprint, and between a patrol and a chase, so both the
        /// player and the ghosts switch samples with their gait.</summary>
        public float runFootstepSpeed = 4.5f;
        public float furniturePitchJitter = 0.1f;

        /// <summary>A setting rather than a disabled component, so no single scene has to
        /// remember whether the music is on.</summary>
        public bool musicEnabled = true;

        // Online. The relay is the tiny Node process in relay/, deployed on Render; the
        // default is the public one so a shipped build works untouched. Empty means "the
        // machine this page came from, port 8787" (or localhost in the editor), for local
        // testing. Spectators draw the turn owner's stream this far behind real time so
        // there is always a next snapshot to interpolate towards.
        public string relayUrl = "wss://retrace-relay.onrender.com";
        public float snapshotHz = 12f;
        public float spectatorDelaySeconds = 0.15f;

        public Key InteractKey => ParseKey(interactKey, Key.E);
        public Key HideKey => ParseKey(hideKey, Key.H);
        public Key ThrowKey => ParseKey(throwKey, Key.F);
        public Key BombKey => ParseKey(bombKey, Key.C);
        public Key RestartKey => ParseKey(restartKey, Key.R);
        public Key ManualFinishKey => ParseKey(manualFinishKey, Key.Enter);
        public Key DebugToggleKey => ParseKey(debugToggleKey, Key.Backquote);
        public Key MenuKey => ParseKey(menuKey, Key.M);
        public Key ConfigMenuKey => ParseKey(configMenuKey, Key.Tab);
        public Key SpectatorCameraKey => ParseKey(spectatorCameraKey, Key.C);

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

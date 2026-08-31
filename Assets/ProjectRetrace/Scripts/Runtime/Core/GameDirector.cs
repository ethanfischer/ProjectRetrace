using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>
    /// Owns the run: Search -> Transition -> Stealth round 1 -> Transition -> Stealth
    /// round 2 -> Results. Round 1 pits the player against a sentry retracing their search
    /// route; round 2 adds a second sentry retracing the round-1 sneak route they just took.
    ///
    /// The Transition step is the one that has to be exactly right. Every interactable returns
    /// to its opening state and the player returns to the identical spawn transform, so the
    /// sentries' patrol routes stay truthful to the house the player walked. The keys are the
    /// one deliberate difference: they move to a NEW hiding spot each round, because stealth
    /// is about finding them somewhere else while your own past routes hunt you.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameDirector : MonoBehaviour
    {
        public static GameDirector Instance { get; private set; }

        /// <summary>F3 debug view. Static so the visualiser and HUD share one switch.</summary>
        public static bool DebugVisible;

        [Header("Scene references")]
        public FirstPersonController player;
        public PlayerInteractor interactor;
        public BreadcrumbTrail trail;
        public KeySpawner keySpawner;
        public Transform spawnPoint;
        public PatrolSentry sentry;
        public PatrolSentry sentry2;

        [Header("Config")]
        public RetraceSettings settings;

        [Tooltip("Seconds of held black between the two phases.")]
        [SerializeField] private float transitionPause = 1.25f;

        [Tooltip("Leave on so each playtest hides the keys somewhere new.")]
        [SerializeField] private bool randomiseSeed = true;

        [SerializeField] private int fixedSeed = 12345;
        [SerializeField] private Key restartKey = Key.R;

        [Tooltip("Start each run with the breadcrumb debug view visible. Turn off for shipping builds; F3 still toggles it either way.")]
        [SerializeField] private bool debugVisibleByDefault = true;

        /// <summary>Stealth rounds per run: round 2 against your search route's sentry, round 3
        /// against it plus a second sentry retracing your round-2 sneak.</summary>
        private const int StealthRounds = 2;

        private RetraceSettings _fallbackSettings;
        private int _seed;
        private Transform _excludedSpot;

        public GamePhase Phase { get; private set; } = GamePhase.Search;
        public bool Won { get; private set; }
        public int Seed => _seed;
        public int LivesRemaining { get; private set; }

        /// <summary>1-based stealth round (1 = game round 2, 2 = game round 3); 0 during Search.</summary>
        public int StealthRound { get; private set; }

        public RetraceSettings EffectiveSettings
        {
            get
            {
                if (settings != null) return settings;
                if (_fallbackSettings == null) _fallbackSettings = RetraceSettings.CreateDefault();
                return _fallbackSettings;
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            StartRun();
        }

        public void StartRun()
        {
            StopAllCoroutines();

            DebugVisible = debugVisibleByDefault;
            Won = false;
            StealthRound = 0;
            LivesRemaining = EffectiveSettings.stealthLives;

            _seed = randomiseSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : fixedSeed;

            StopSentries();

            // Restore before capturing: on a restart the house is mid-run, and capturing that
            // state would bake open drawers in as the new "initial" state.
            InteractableRegistry.RestoreAll();
            InteractableRegistry.CaptureAll();

            if (keySpawner != null) keySpawner.PlaceKey(_seed);

            MovePlayerToSpawn();
            SetPlayerInputEnabled(true);

            if (trail != null)
            {
                trail.settings = EffectiveSettings;
                trail.BeginPhase1();
            }

            SetPhase(GamePhase.Search);
        }

        /// <summary>Called by KeyItem. Ends phase 1, or wins the current stealth round.</summary>
        public void OnKeyTaken()
        {
            if (Phase == GamePhase.Search)
            {
                StartCoroutine(TransitionToStealthRound(1));
                return;
            }

            if (Phase != GamePhase.Stealth) return;

            if (StealthRound < StealthRounds)
            {
                StartCoroutine(TransitionToStealthRound(StealthRound + 1));
            }
            else
            {
                FinishRun(won: true);
            }
        }

        private IEnumerator TransitionToStealthRound(int round)
        {
            SetPhase(GamePhase.Transition);
            SetPlayerInputEnabled(false);
            StopSentries();

            // The outgoing hiding spot is remembered here, not read back later: after the
            // next placement runs, LastSpot becomes the new spot, and a retry that excluded
            // *that* would silently move the keys between attempts.
            _excludedSpot = keySpawner != null ? keySpawner.LastSpot : null;
            StealthRound = round;
            LivesRemaining = EffectiveSettings.stealthLives;

            yield return new WaitForSeconds(transitionPause);

            BeginStealthAttempt();
        }

        /// <summary>Called after a catch that still leaves lives. Same beat as a round
        /// transition: house resets, player returns to spawn, the sentries restart.</summary>
        private IEnumerator RetryStealth()
        {
            SetPhase(GamePhase.Transition);
            StopSentries();

            yield return new WaitForSeconds(transitionPause);

            BeginStealthAttempt();
        }

        private void BeginStealthAttempt()
        {
            // Restore first, then move the keys: RestoreAll snaps them back to the spot they
            // were captured under, so the new placement must come after it. The round seed is
            // the same on every attempt, so a retry re-hides the keys in the same spot --
            // what the player learned before getting caught stays true.
            InteractableRegistry.RestoreAll();
            if (keySpawner != null)
            {
                keySpawner.PlaceKey(RoundSeed(StealthRound), _excludedSpot);
            }

            MovePlayerToSpawn();

            if (trail != null)
            {
                // Round 1 re-records the sneak route each attempt, so the second sentry ends
                // up retracing the attempt that actually succeeded. By round 2 that route is
                // a sentry's script and nothing downstream needs a recording.
                if (StealthRound == 1) trail.BeginPhase2();
                else trail.Stop();
            }

            if (sentry != null && trail != null)
            {
                sentry.settings = EffectiveSettings;
                sentry.BeginPatrol(trail.Phase1Crumbs, trail.Phase1Dwells);
            }

            if (sentry2 != null && trail != null && StealthRound >= 2)
            {
                sentry2.settings = EffectiveSettings;
                sentry2.BeginPatrol(trail.Phase2Crumbs, trail.Phase2Dwells);
            }

            SetPlayerInputEnabled(true);
            SetPhase(GamePhase.Stealth);
        }

        /// <summary>Called by PatrolSentry at the moment of detection. The run is already lost;
        /// input freezes here so the short chase plays out against a helpless player.</summary>
        public void OnPlayerSpotted()
        {
            if (Phase != GamePhase.Stealth) return;
            SetPlayerInputEnabled(false);
        }

        /// <summary>Called by PatrolSentry when the chase connects (or times out).</summary>
        public void OnPlayerCaught()
        {
            if (Phase != GamePhase.Stealth) return;

            LivesRemaining--;
            if (LivesRemaining <= 0)
            {
                FinishRun(won: false);
                return;
            }

            StartCoroutine(RetryStealth());
        }

        /// <summary>Derived rather than random so a fixed seed reproduces every hiding spot.</summary>
        private int RoundSeed(int round)
        {
            return unchecked(_seed * 486187739 + round);
        }

        private void StopSentries()
        {
            if (sentry != null) sentry.StopPatrol();
            if (sentry2 != null) sentry2.StopPatrol();
        }

        public void FinishRun(bool won)
        {
            if (Phase == GamePhase.Results) return;

            Won = won;
            if (trail != null) trail.Stop();
            StopSentries();

            // Input stays enabled either way, so the end of a run is a banner over the
            // world rather than a hard cut.
            SetPlayerInputEnabled(true);
            SetPhase(GamePhase.Results);
        }

        private static bool WasPressedThisFrame(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].wasPressedThisFrame;
        }

        private void Update()
        {
            var config = EffectiveSettings;

            if (WasPressedThisFrame(config.debugToggleKey))
            {
                DebugVisible = !DebugVisible;
            }

            if (Phase == GamePhase.Results)
            {
                if (WasPressedThisFrame(restartKey)) StartRun();
                return;
            }

            if (Phase != GamePhase.Stealth) return;

            // The escape hatch when a playtest goes sideways: skip straight to the win.
            if (WasPressedThisFrame(config.manualFinishKey))
            {
                FinishRun(won: true);
            }
        }

        private void MovePlayerToSpawn()
        {
            if (player == null || spawnPoint == null) return;
            player.Teleport(spawnPoint.position, spawnPoint.rotation);
        }

        private void SetPlayerInputEnabled(bool inputEnabled)
        {
            if (player != null) player.SetInputEnabled(inputEnabled);
            if (interactor != null) interactor.SetInputEnabled(inputEnabled);
        }

        private void SetPhase(GamePhase phase)
        {
            Phase = phase;
        }
    }
}

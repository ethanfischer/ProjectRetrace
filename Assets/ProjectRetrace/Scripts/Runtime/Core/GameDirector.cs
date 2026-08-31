using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>
    /// Owns the run: Search -> Transition -> Stealth -> Results.
    ///
    /// The Transition step is the one that has to be exactly right. Every interactable returns
    /// to its phase-1 opening state and the player returns to the identical spawn transform,
    /// so the sentry's patrol route stays truthful to the house the player searched. The keys
    /// are the one deliberate difference: they move to a NEW hiding spot, because phase 2 is
    /// about finding them somewhere else while the sentry retraces the player's route.
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

        private RetraceSettings _fallbackSettings;
        private int _seed;

        public GamePhase Phase { get; private set; } = GamePhase.Search;
        public bool Won { get; private set; }
        public int Seed => _seed;

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

            _seed = randomiseSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : fixedSeed;

            if (sentry != null) sentry.StopPatrol();

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

        /// <summary>Called by KeyItem. Ends phase 1, or wins the stealth phase.</summary>
        public void OnKeyTaken()
        {
            if (Phase == GamePhase.Search)
            {
                StartCoroutine(TransitionToStealth());
                return;
            }

            if (Phase == GamePhase.Stealth)
            {
                FinishRun(won: true);
            }
        }

        private IEnumerator TransitionToStealth()
        {
            SetPhase(GamePhase.Transition);
            SetPlayerInputEnabled(false);

            yield return new WaitForSeconds(transitionPause);

            // Restore first, then move the keys: RestoreAll snaps them back to the phase-1
            // spot they were captured under, so the new placement must come after it.
            InteractableRegistry.RestoreAll();
            if (keySpawner != null)
            {
                keySpawner.PlaceKey(StealthSeed(), keySpawner.LastSpot);
            }

            MovePlayerToSpawn();

            if (trail != null) trail.BeginPhase2();

            if (sentry != null)
            {
                sentry.settings = EffectiveSettings;
                sentry.BeginPatrol();
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
            FinishRun(won: false);
        }

        /// <summary>Derived rather than random so a fixed seed reproduces both hiding spots.</summary>
        private int StealthSeed()
        {
            return unchecked(_seed * 486187739 + 1);
        }

        public void FinishRun(bool won)
        {
            if (Phase == GamePhase.Results) return;

            Won = won;
            if (trail != null) trail.Stop();
            if (sentry != null) sentry.StopPatrol();

            // Input stays enabled either way: Results is a walkable view where the player
            // wanders the house comparing the patrol route with their own sneak route.
            SetPlayerInputEnabled(true);
            SetPhase(GamePhase.Results);
            DebugVisible = true;
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

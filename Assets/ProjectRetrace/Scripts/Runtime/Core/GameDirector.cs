using System;
using System.Collections;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Owns the run: Search -> Transition -> Retrace -> Results.
    ///
    /// The Transition step is the one that has to be exactly right. Every interactable returns
    /// to its phase-1 opening state, the keys go back to the same hiding spot, and the player
    /// returns to the identical spawn transform -- otherwise phase 2 is not the same house and
    /// the score means nothing.
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

        [Header("Config")]
        public RetraceSettings settings;

        [Tooltip("Seconds of held black between the two phases.")]
        [SerializeField] private float transitionPause = 1.25f;

        [Tooltip("Leave on so each playtest hides the keys somewhere new.")]
        [SerializeField] private bool randomiseSeed = true;

        [SerializeField] private int fixedSeed = 12345;
        [SerializeField] private KeyCode restartKey = KeyCode.R;

        private RetraceSettings _fallbackSettings;
        private float _searchStartTime;
        private float _retraceStartTime;
        private int _seed;

        public GamePhase Phase { get; private set; } = GamePhase.Search;
        public ScoreResult LastResult { get; private set; }
        public float Phase1Duration { get; private set; }
        public int Seed => _seed;

        public event Action<GamePhase> PhaseChanged;

        public RetraceSettings EffectiveSettings
        {
            get
            {
                if (settings != null) return settings;
                if (_fallbackSettings == null) _fallbackSettings = RetraceSettings.CreateDefault();
                return _fallbackSettings;
            }
        }

        /// <summary>Seconds remaining in TimeLimit mode; negative when the mode is not in use.</summary>
        public float RetraceTimeRemaining
        {
            get
            {
                if (Phase != GamePhase.Retrace || EffectiveSettings.endMode != RetraceEndMode.TimeLimit)
                {
                    return -1f;
                }

                var limit = Phase1Duration * EffectiveSettings.timeLimitMultiplier;
                return Mathf.Max(0f, limit - (Time.time - _retraceStartTime));
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

            _seed = randomiseSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : fixedSeed;

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
                trail.BeginPlacement();
            }

            _searchStartTime = Time.time;
            SetPhase(GamePhase.Search);
        }

        /// <summary>Called by KeyItem. Ends phase 1, or ends the run in KeyPickup end mode.</summary>
        public void OnKeyTaken()
        {
            if (Phase == GamePhase.Search)
            {
                StartCoroutine(TransitionToRetrace());
                return;
            }

            if (Phase == GamePhase.Retrace && EffectiveSettings.endMode == RetraceEndMode.KeyPickup)
            {
                FinishRun();
            }
        }

        private IEnumerator TransitionToRetrace()
        {
            Phase1Duration = Time.time - _searchStartTime;
            SetPhase(GamePhase.Transition);
            SetPlayerInputEnabled(false);

            yield return new WaitForSeconds(transitionPause);

            // Put the house back exactly as phase 1 found it. RestoreAll also returns the keys
            // to this run's hiding spot, because KeySpawner captured that spot as their state.
            InteractableRegistry.RestoreAll();
            MovePlayerToSpawn();

            if (trail != null) trail.BeginCollection();

            SetPlayerInputEnabled(true);
            _retraceStartTime = Time.time;
            SetPhase(GamePhase.Retrace);
        }

        public void FinishRun()
        {
            if (Phase == GamePhase.Results) return;

            if (trail != null)
            {
                trail.Stop();
                LastResult = trail.BuildScore();
            }

            SetPlayerInputEnabled(false);
            SetPhase(GamePhase.Results);

            // The results screen is where you want to see the trail, so open the debug view.
            DebugVisible = true;
        }

        private void Update()
        {
            var config = EffectiveSettings;

            if (Input.GetKeyDown(config.debugToggleKey))
            {
                DebugVisible = !DebugVisible;
            }

            if (Phase == GamePhase.Results)
            {
                if (Input.GetKeyDown(restartKey)) StartRun();
                return;
            }

            if (Phase != GamePhase.Retrace) return;

            // Manual finish stays live in every mode: it is the escape hatch when a playtest
            // goes sideways and you just want to see the score.
            if (Input.GetKeyDown(config.manualFinishKey))
            {
                FinishRun();
                return;
            }

            if (config.endMode == RetraceEndMode.TimeLimit && RetraceTimeRemaining <= 0f)
            {
                FinishRun();
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
            PhaseChanged?.Invoke(phase);
        }
    }
}

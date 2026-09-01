using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>
    /// Owns the run: Search, then stealth rounds forever -- every round survived turns the
    /// route just walked into one more sentry's patrol script, so round N is played against
    /// N sentries, each retracing a past walk. Solo there is no winning, only how far you
    /// get. In couch mode the rounds alternate between two players on one keyboard, every
    /// route haunting both of them, and the first player caught out hands the other the
    /// win; the rematch swaps who gets the threat-free search round.
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

        [Tooltip("Couch mode: rounds alternate between two players on one keyboard, every route haunting both. First player caught out loses. Toggleable on the results screen.")]
        [SerializeField] private bool twoPlayerMode;

        private readonly System.Collections.Generic.List<PatrolSentry> _sentries =
            new System.Collections.Generic.List<PatrolSentry>();

        private RetraceSettings _fallbackSettings;
        private int _seed;
        private Transform _excludedSpot;
        private int _startingPlayer = 1;
        private bool _ranBefore;

        public GamePhase Phase { get; private set; } = GamePhase.Search;
        public int Seed => _seed;
        public int LivesRemaining { get; private set; }

        public bool TwoPlayerMode => twoPlayerMode;

        /// <summary>Whose round it is (always 1 in single player).</summary>
        public int CurrentPlayer { get; private set; } = 1;

        /// <summary>Couch mode: set when the run ends -- the player who was NOT caught out.
        /// 0 in single player or while a run is live.</summary>
        public int Winner { get; private set; }

        /// <summary>Couch mode: the transition is holding for the incoming player to take
        /// the keyboard and press Space.</summary>
        public bool AwaitingHandover { get; private set; }

        /// <summary>1-based stealth round (1 = game round 2, and so on); 0 during Search.
        /// Also the number of sentries on patrol that round.</summary>
        public int StealthRound { get; private set; }

        /// <summary>The sentry pool, scene pair first, runtime clones after. Grows as rounds
        /// accumulate and is never trimmed -- StopPatrol just deactivates.</summary>
        public System.Collections.Generic.IReadOnlyList<PatrolSentry> Sentries => _sentries;

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
            StealthRound = 0;
            LivesRemaining = EffectiveSettings.stealthLives;
            Winner = 0;
            AwaitingHandover = false;

            // The rematch swaps who searches first: the searcher gets a threat-free round,
            // so fairness across a couch match is "play twice, swap the free round".
            if (twoPlayerMode && _ranBefore) _startingPlayer = Other(_startingPlayer);
            _ranBefore = true;
            CurrentPlayer = twoPlayerMode ? _startingPlayer : 1;

            _seed = randomiseSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : fixedSeed;

            EnsureSentries(0);
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
                trail.BeginFirstRoute(CurrentPlayer);
            }

            SetPhase(GamePhase.Search);
        }

        /// <summary>Called by KeyItem. Ends the search, or survives the current stealth
        /// round -- there is always a next one.</summary>
        public void OnKeyTaken()
        {
            if (Phase == GamePhase.Search)
            {
                StartCoroutine(TransitionToStealthRound(1));
                return;
            }

            if (Phase != GamePhase.Stealth) return;

            StartCoroutine(TransitionToStealthRound(StealthRound + 1));
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
            CurrentPlayer = RoundOwner(round);

            yield return new WaitForSeconds(transitionPause);

            // Couch mode: hold the frozen world until the incoming player takes the
            // keyboard -- rounds always change hands, so every round transition is a
            // handover.
            if (twoPlayerMode)
            {
                AwaitingHandover = true;
                while (!WasPressedThisFrame(Key.Space)) yield return null;
                AwaitingHandover = false;
            }

            BeginStealthAttempt(retry: false);
        }

        /// <summary>The searcher plays the even stealth rounds, their opponent the odd ones
        /// -- so the ghost pool always contains the round owner's own past walks too.</summary>
        private int RoundOwner(int round)
        {
            if (!twoPlayerMode) return 1;
            return round % 2 == 1 ? Other(_startingPlayer) : _startingPlayer;
        }

        private static int Other(int playerNumber)
        {
            return playerNumber == 1 ? 2 : 1;
        }

        /// <summary>Called after a catch that still leaves lives. Same beat as a round
        /// transition: house resets, player returns to spawn, the sentries restart.</summary>
        private IEnumerator RetryStealth()
        {
            SetPhase(GamePhase.Transition);
            StopSentries();

            yield return new WaitForSeconds(transitionPause);

            BeginStealthAttempt(retry: true);
        }

        private void BeginStealthAttempt(bool retry)
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
                // Every stealth round records the route being walked -- it is the sentry
                // added next round. A retry re-records from scratch, so only the attempt
                // that actually survives ever becomes a patrol.
                if (retry) trail.RestartRoute();
                else trail.BeginNextRoute(CurrentPlayer);

                var patrols = trail.CompletedRouteCount;
                EnsureSentries(patrols);
                for (var i = 0; i < patrols && i < _sentries.Count; i++)
                {
                    var route = trail.Routes[i];
                    if (twoPlayerMode) _sentries[i].bodyTint = GhostTint(route.Owner, i);
                    _sentries[i].settings = EffectiveSettings;
                    _sentries[i].BeginPatrol(route.Crumbs, route.Dwells);
                }
            }

            SetPlayerInputEnabled(true);
            SetPhase(GamePhase.Stealth);
        }

        /// <summary>The scene carries two sentries; every round past that clones the first
        /// one, hue-rotated so each ghost of a past round reads as its own character.</summary>
        private void EnsureSentries(int count)
        {
            if (_sentries.Count == 0)
            {
                if (sentry != null) _sentries.Add(sentry);
                if (sentry2 != null) _sentries.Add(sentry2);
            }

            while (_sentries.Count < count && sentry != null)
            {
                var clone = Instantiate(sentry.gameObject).GetComponent<PatrolSentry>();
                clone.gameObject.name = "Sentry " + (_sentries.Count + 1);
                clone.bodyTint = Color.HSVToRGB(_sentries.Count * 0.37f % 1f, 0.5f, 0.95f);
                _sentries.Add(clone);
            }
        }

        /// <summary>Couch mode: cool hues for player 1's ghosts, warm for player 2's, so a
        /// glance says whose past self is rounding the corner.</summary>
        private static Color GhostTint(int owner, int index)
        {
            var baseHue = owner == 1 ? 0.55f : 0.03f;
            return Color.HSVToRGB((baseHue + index * 0.04f) % 1f, 0.55f, 0.95f);
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
                FinishRun();
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
            for (var i = 0; i < _sentries.Count; i++)
            {
                if (_sentries[i] != null) _sentries[i].StopPatrol();
            }
        }

        public void FinishRun()
        {
            if (Phase == GamePhase.Results) return;

            if (twoPlayerMode) Winner = Other(CurrentPlayer);
            if (trail != null) trail.Stop();
            StopSentries();

            // Input stays enabled, so the end of a run is a banner over the world rather
            // than a hard cut.
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
                else if (WasPressedThisFrame(config.togglePlayersKey)) twoPlayerMode = !twoPlayerMode;
                return;
            }

            if (Phase != GamePhase.Stealth) return;

            // The escape hatch when a playtest goes sideways: skip straight to the win.
            // The playtest escape hatch now skips ahead: with no win state left, "finish"
            // means surviving the round without hunting the keys down.
            if (WasPressedThisFrame(config.manualFinishKey))
            {
                OnKeyTaken();
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

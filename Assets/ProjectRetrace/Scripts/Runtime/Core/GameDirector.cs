using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>
    /// Owns the run: Search, then stealth rounds forever -- every round survived turns the
    /// route just walked into one more sentry's patrol script, so round N is played against
    /// N sentries, each retracing a past walk. Solo there is no winning, only how far you
    /// get. In local multiplayer (2-4 players, one keyboard) the rounds rotate through the
    /// players, every route haunting all of them, and the first player caught out ends the
    /// match as its loser; the rematch rotates who gets the threat-free search round.
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
        [Tooltip("Inactive mold for the ghost pool -- never patrols itself. Every sentry on the field is a runtime clone of this, so the pool scales to any round count.")]
        [UnityEngine.Serialization.FormerlySerializedAs("sentry")]
        public PatrolSentry sentryTemplate;

        [Tooltip("Players in the match (1 = single player). Couch 2P alternates rounds on one keyboard; only your opponent's routes haunt you. First to run out of tries loses. Set from the start menu.")]
        [Range(1, 2)]
        [SerializeField] private int playerCount = 1;

        private readonly System.Collections.Generic.List<PatrolSentry> _sentries =
            new System.Collections.Generic.List<PatrolSentry>();

        private int _seed;
        private Transform _excludedSpot;
        private int _startingPlayer = 1;
        private bool _ranBefore;

        public GamePhase Phase { get; private set; } = GamePhase.Search;
        public int Seed => _seed;
        public int LivesRemaining { get; private set; }

        public int PlayerCount => playerCount;

        public bool Multiplayer => playerCount > 1;

        /// <summary>Whose round it is (always 1 in single player).</summary>
        public int CurrentPlayer { get; private set; } = 1;

        /// <summary>Multiplayer: whoever was still standing when the other ran out of
        /// tries. 0 in single player or while the match is live.</summary>
        public int Winner { get; private set; }

        /// <summary>Couch mode: the transition is holding for the incoming player to take
        /// the keyboard and press Space.</summary>
        public bool AwaitingHandover { get; private set; }

        /// <summary>1-based stealth round (1 = game round 2, and so on); 0 during Search.
        /// In single player also the number of sentries on patrol that round.</summary>
        public int StealthRound { get; private set; }

        /// <summary>Ghosts the current player faces: every completed route in single player,
        /// only the opponent's in couch mode -- your own past never haunts you, so the
        /// two players trade traps rather than each drowning in their own.</summary>
        public int GhostCount
        {
            get
            {
                if (trail == null) return 0;
                var count = 0;
                for (var i = 0; i < trail.CompletedRouteCount; i++)
                {
                    if (Haunts(trail.Routes[i])) count++;
                }

                return count;
            }
        }

        private bool Haunts(RecordedRoute route) => !Multiplayer || route.Owner != CurrentPlayer;

        /// <summary>The ghost pool: runtime clones of the template, one per patrolled route.
        /// Grows as rounds accumulate and is never trimmed -- StopPatrol just deactivates.</summary>
        public System.Collections.Generic.IReadOnlyList<PatrolSentry> Sentries => _sentries;

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
            EnterMenu();
        }

        /// <summary>The idle state between games: no run live, input frozen, cursor free
        /// for the menu. StartMenu drives the way out.</summary>
        public void EnterMenu()
        {
            StopAllCoroutines();
            AwaitingHandover = false;
            StopSentries();
            if (trail != null) trail.Stop();
            SetPlayerInputEnabled(false);
            FirstPersonController.LockCursor(false);
            SetPhase(GamePhase.Menu);
        }

        /// <summary>Menu entry point (1 = single player). A fresh mode choice also resets
        /// the rotation, so player 1 always searches first in a new match.</summary>
        public void StartGame(int players)
        {
            playerCount = Mathf.Clamp(players, 1, 2);
            _startingPlayer = 1;
            _ranBefore = false;
            StartRun();
        }

        public void StartRun()
        {
            StopAllCoroutines();

            // Each run re-reads the file, so edits made while the game is open land on
            // the next restart without relaunching.
            RetraceConfig.Reload();
            var config = RetraceConfig.Current;

            DebugVisible = config.debugVisibleByDefault;
            StealthRound = 0;
            LivesRemaining = config.stealthLives;
            Winner = 0;
            AwaitingHandover = false;

            // The rematch rotates who searches first: the searcher gets a threat-free
            // round, so fairness across a match is "everyone gets the free round once".
            if (Multiplayer && _ranBefore) _startingPlayer = _startingPlayer % playerCount + 1;
            _ranBefore = true;
            CurrentPlayer = Multiplayer ? _startingPlayer : 1;

            _seed = config.randomiseKeySpots ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : config.keySpotSeed;

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

            // The survived round's route is complete the moment the round ends -- without
            // this, the handover screen counts it as still-in-progress and reports one
            // ghost fewer than the incoming player is about to face.
            if (trail != null) trail.Stop();

            // The outgoing hiding spot is remembered here, not read back later: after the
            // next placement runs, LastSpot becomes the new spot, and a retry that excluded
            // *that* would silently move the keys between attempts.
            _excludedSpot = keySpawner != null ? keySpawner.LastSpot : null;
            StealthRound = round;
            LivesRemaining = RetraceConfig.Current.stealthLives;
            CurrentPlayer = Multiplayer ? Opponent(CurrentPlayer) : 1;

            yield return new WaitForSeconds(RetraceConfig.Current.transitionPause);

            // Local multiplayer: hold the frozen world until the incoming player takes
            // the keyboard -- rounds always change hands, so every round transition is a
            // handover.
            if (Multiplayer)
            {
                AwaitingHandover = true;
                while (!WasPressedThisFrame(Key.Space)) yield return null;
                AwaitingHandover = false;
            }

            BeginStealthAttempt(retry: false);
        }

        private static int Opponent(int player) => player == 1 ? 2 : 1;

        /// <summary>Called after a catch that still leaves lives. Same beat as a round
        /// transition: house resets, player returns to spawn, the sentries restart.</summary>
        private IEnumerator RetryStealth()
        {
            SetPhase(GamePhase.Transition);
            StopSentries();

            yield return new WaitForSeconds(RetraceConfig.Current.transitionPause);

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

                EnsureSentries(GhostCount);
                var next = 0;
                for (var i = 0; i < trail.CompletedRouteCount && next < _sentries.Count; i++)
                {
                    var route = trail.Routes[i];
                    if (!Haunts(route)) continue;

                    if (Multiplayer) _sentries[next].bodyTint = GhostTint(route.Owner, next);
                    _sentries[next].BeginPatrol(route.Crumbs, route.Dwells);
                    next++;
                }
            }

            SetPlayerInputEnabled(true);
            SetPhase(GamePhase.Stealth);
        }

        /// <summary>Grows the ghost pool to at least the given size by cloning the template
        /// -- the first ghost keeps the template's look, later ones are hue-rotated so each
        /// past round reads as its own character.</summary>
        private void EnsureSentries(int count)
        {
            while (_sentries.Count < count && sentryTemplate != null)
            {
                var clone = Instantiate(sentryTemplate.gameObject).GetComponent<PatrolSentry>();
                clone.gameObject.name = "Sentry " + (_sentries.Count + 1);
                if (_sentries.Count > 0)
                {
                    clone.bodyTint = Color.HSVToRGB(_sentries.Count * 0.37f % 1f, 0.5f, 0.95f);
                }

                _sentries.Add(clone);
            }
        }

        /// <summary>Each player's ghosts share a hue family -- blue, orange, green, purple
        /// -- so a glance says whose past self is rounding the corner.</summary>
        private static Color GhostTint(int owner, int index)
        {
            var hues = new[] { 0.55f, 0.03f, 0.33f, 0.80f };
            var baseHue = hues[Mathf.Clamp(owner - 1, 0, hues.Length - 1)];
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
            if (LivesRemaining > 0)
            {
                StartCoroutine(RetryStealth());
                return;
            }

            if (Multiplayer) Winner = Opponent(CurrentPlayer);
            FinishRun();
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
            var config = RetraceConfig.Current;

            if (WasPressedThisFrame(config.DebugToggleKey))
            {
                DebugVisible = !DebugVisible;
            }

            if (Phase == GamePhase.Menu || ConfigMenu.IsOpen) return;

            if (Phase == GamePhase.Results)
            {
                if (WasPressedThisFrame(config.RestartKey)) StartRun();
                else if (WasPressedThisFrame(config.MenuKey)) EnterMenu();
                return;
            }

            if (Phase != GamePhase.Stealth) return;

            // The escape hatch when a playtest goes sideways: skip straight to the win.
            // The playtest escape hatch now skips ahead: with no win state left, "finish"
            // means surviving the round without hunting the keys down.
            if (WasPressedThisFrame(config.ManualFinishKey))
            {
                OnKeyTaken();
            }
        }

        /// <summary>The settings menu freezes the world rather than hiding behind it: a
        /// sentry walking on under a paused player would be a free catch.</summary>
        public void SetConfigMenuOpen(bool open)
        {
            Time.timeScale = open ? 0f : 1f;
            var phaseTakesInput = Phase == GamePhase.Search || Phase == GamePhase.Stealth || Phase == GamePhase.Results;
            SetPlayerInputEnabled(!open && phaseTakesInput);
            if (open || !phaseTakesInput) FirstPersonController.LockCursor(false);
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

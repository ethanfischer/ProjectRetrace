using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Reticle and interaction prompt (always on), plus the live tuning readout (debug key
    /// only). IMGUI on purpose: no prefab wiring, no TextMeshPro import, nothing to
    /// merge-conflict over. Swap for real uGUI once the loop is proven fun.
    /// </summary>
    public class DebugHud : MonoBehaviour
    {
        public GameDirector director;
        public PlayerInteractor interactor;
        public BreadcrumbTrail trail;

        private KeyItem _key;

        private GUIStyle _label;
        private GUIStyle _centered;
        private GUIStyle _handover;

        private void Reset()
        {
            director = GetComponent<GameDirector>();
            trail = GetComponent<BreadcrumbTrail>();
        }

        private void OnGUI()
        {
            if (director != null && director.Phase == GamePhase.Menu) return;
            if (ConfigMenu.IsOpen) return;

            HudScale.Apply();
            EnsureStyles();

            if (GameDirector.DebugVisible)
            {
                DrawKeyLocator();
            }

            if (director == null || (director.Phase != GamePhase.Results && director.Phase != GamePhase.Spectate))
            {
                DrawReticle();
                DrawPrompt();
            }

            DrawPhaseBanner();

            if (GameDirector.DebugVisible)
            {
                DrawStats();
            }
        }

        private KeyItem Key
        {
            get
            {
                if (_key == null) _key = FindFirstObjectByType<KeyItem>();
                return _key;
            }
        }

        /// <summary>Debug aid: pins down "I could not find the key" bugs by showing exactly
        /// where the key thinks it is, through walls, plus whether it is still takeable.</summary>
        private void DrawKeyLocator()
        {
            var key = Key;
            var camera = Camera.main;
            if (key == null || camera == null) return;

            var screen = camera.WorldToScreenPoint(key.transform.position);
            if (screen.z <= 0f) return;

            // WorldToScreenPoint is in device pixels; the GUI matrix works in reference units.
            var rect = new Rect(screen.x / HudScale.Factor - 60f,
                HudScale.Height - screen.y / HudScale.Factor - 14f, 120f, 28f);
            GUI.color = key.CanInteract ? new Color(1f, 0.9f, 0.3f) : new Color(1f, 0.4f, 0.4f);
            GUI.Label(rect, string.Format("v KEYS {0:0.0}m", screen.z), _centered);
            GUI.color = Color.white;
        }

        private string KeyStatusLine()
        {
            var key = Key;
            if (key == null) return "Keys: NOT FOUND IN SCENE";

            var pos = key.transform.position;
            var parent = key.transform.parent != null ? key.transform.parent.name : "no parent";
            return string.Format("Keys: ({0:0.0}, {1:0.0}, {2:0.0}) in \"{3}\"{4}",
                pos.x, pos.y, pos.z, parent, key.CanInteract ? "" : "  [TAKEN/DISABLED]");
        }

        private void EnsureStyles()
        {
            if (_label != null) return;

            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, wordWrap = true };
            _label.normal.textColor = Color.white;

            _centered = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            _handover = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
        }

        private void DrawReticle()
        {
            var centre = new Rect(HudScale.Width * 0.5f - 3f, HudScale.Height * 0.5f - 3f, 6f, 6f);
            var hasTarget = interactor != null && interactor.Current != null;
            GUI.color = hasTarget ? new Color(1f, 0.9f, 0.3f) : new Color(1f, 1f, 1f, 0.5f);
            GUI.DrawTexture(centre, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawPrompt()
        {
            if (interactor == null) return;

            var prompt = interactor.CurrentPrompt;
            if (string.IsNullOrEmpty(prompt)) return;

            var rect = new Rect(0f, HudScale.Height * 0.5f + 24f, HudScale.Width, 28f);
            GUI.Label(rect, "[E] " + prompt, _centered);
        }

        private void DrawPhaseBanner()
        {
            if (director == null) return;

            if (director.AwaitingHandover)
            {
                DrawHandover();
                return;
            }

            if (director.AwaitingOpponent)
            {
                DrawWaitingForOpponent();
                return;
            }

            string banner;
            var maxLives = RetraceConfig.Current.stealthLives;
            var who = director.Multiplayer ? $"P{director.CurrentPlayer} -- " : string.Empty;
            switch (director.Phase)
            {
                case GamePhase.Search:
                    banner = who + "Find your keys";
                    break;
                case GamePhase.Transition:
                    banner = director.LivesRemaining < maxLives
                        ? $"Caught! {director.LivesRemaining} {(director.LivesRemaining == 1 ? "try" : "tries")} left..."
                        : string.Empty;
                    break;
                case GamePhase.Stealth:
                    banner = $"{who}Round {director.StealthRound + 1}: find your keys without getting caught [{director.LivesRemaining}/{maxLives} tries]";
                    if (AnyDoorUnlocksThisRound()) banner += " -- 2nd floor unlocked!";
                    break;
                case GamePhase.Spectate:
                    banner = $"Spectating Player {director.CurrentPlayer} -- round {director.StealthRound + 1} [{director.LivesRemaining}/{maxLives} tries]";
                    if (director.spectator != null)
                    {
                        banner += $"  [{RetraceConfig.Current.SpectatorCameraKey}] {ViewName(director.spectator.NextView)}";
                    }

                    break;
                default:
                    return;
            }

            GUI.Label(new Rect(0f, 24f, HudScale.Width, 30f), banner, _centered);
            DrawConnection();
        }

        /// <summary>Online only: a quiet line so a stalled stream reads as "they dropped",
        /// not "the game froze".</summary>
        private void DrawConnection()
        {
            var online = director.online;
            if (online == null || online.State == NetState.Idle || online.State == NetState.Lobby) return;

            string line;
            if (online.State == NetState.Disconnected || online.State == NetState.Error) line = "Connection lost -- [M] menu, then Resume";
            else if (!online.PeerPresent) line = "Opponent disconnected...";
            else if (online.RematchRequested && online.IsHost) line = "Opponent wants a rematch -- [R]";
            else line = $"Online -- room {online.Room} -- {online.RttMs:0} ms";
            GUI.Label(new Rect(0f, 52f, HudScale.Width, 24f), line, _handover);
        }

        private void DrawWaitingForOpponent()
        {
            var box = new Rect(HudScale.Width * 0.5f - 220f, HudScale.Height * 0.5f - 50f, 440f, 100f);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x, box.y + 16f, box.width, 30f), $"Player {director.CurrentPlayer}'s round", _centered);
            var ghosts = director.GhostCount;
            GUI.Label(new Rect(box.x, box.y + 54f, box.width, 24f),
                $"{ghosts} ghost{(ghosts == 1 ? "" : "s")} on patrol -- waiting for them to start...", _handover);
            DrawConnection();
        }

        private static string ViewName(SpectatorRig.View view)
        {
            switch (view)
            {
                case SpectatorRig.View.FirstPerson: return "their eyes";
                case SpectatorRig.View.Chase: return "chase cam";
                default: return "free camera";
            }
        }

        private static bool AnyDoorUnlocksThisRound()
        {
            foreach (var interactable in InteractableRegistry.All)
            {
                if (interactable is DoorInteractable door && door.UnlocksThisRound) return true;
            }

            return false;
        }

        /// <summary>Couch handover: the world stays frozen until whoever plays next takes
        /// the keyboard and says so.</summary>
        private void DrawHandover()
        {
            var box = new Rect(HudScale.Width * 0.5f - 220f, HudScale.Height * 0.5f - 50f, 440f, 100f);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x, box.y + 16f, box.width, 30f),
                $"Player {director.CurrentPlayer}, you're up", _centered);
            var ghosts = director.GhostCount;
            GUI.Label(new Rect(box.x, box.y + 54f, box.width, 24f),
                $"{ghosts} ghost{(ghosts == 1 ? "" : "s")} on patrol -- press Space when ready",
                _handover);
            DrawConnection();
        }

        private void DrawStats()
        {
            if (trail == null) return;

            var settings = RetraceConfig.Current;

            // Layout-sized rather than a fixed rect: the readout grows a line every time
            // something new is worth showing, and a fixed box silently clips the newest one.
            GUILayout.BeginArea(new Rect(12f, 12f, 420f, HudScale.Height - 24f));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>DEBUG</b>  (" + settings.DebugToggleKey + " to hide, " + settings.ConfigMenuKey + " for settings)", _label);
            GUILayout.Label("Phase: " + (director != null ? director.Phase.ToString() : "-")
                + (director != null && director.StealthRound > 0 ? "  (round " + (director.StealthRound + 1) + ")" : ""), _label);
            var current = trail.CurrentRoute;
            GUILayout.Label(string.Format("Routes recorded: {0}", trail.CompletedRouteCount)
                + (current != null
                    ? string.Format("   now: {0} crumbs, {1} stops, {2:0.0}m",
                        current.Crumbs.Count, current.Dwells.Count, current.Distance)
                    : ""), _label);
            GUILayout.Label(SentryStatusLine(), _label);
            GUILayout.Label(string.Format("spacing {0:0.00}m", settings.dotSpacing), _label);
            GUILayout.Label(KeyStatusLine(), _label);
            GUILayout.Label("config: " + RetraceConfig.FilePath, _label);
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private string SentryStatusLine()
        {
            if (director == null) return "Sentries: -";

            var active = 0;
            var nearest = float.MaxValue;
            SentryState nearestState = SentryState.Inactive;
            foreach (var target in director.Sentries)
            {
                if (target == null || target.State == SentryState.Inactive) continue;
                active++;
                if (target.player == null) continue;
                var distance = Vector3.Distance(target.transform.position, target.player.transform.position);
                if (distance < nearest)
                {
                    nearest = distance;
                    nearestState = target.State;
                }
            }

            if (active == 0) return "Sentries: none active";
            return string.Format("Sentries: {0} active, nearest {1:0.0}m ({2})", active, nearest, nearestState);
        }
    }
}

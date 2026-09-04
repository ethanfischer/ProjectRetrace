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

        private GamePhase _lastPhase = GamePhase.Menu;
        private string _toast = string.Empty;
        private float _toastStartedAt;

        private GUIStyle _label;
        private GUIStyle _centered;
        private GUIStyle _handover;

        private void Reset()
        {
            director = GetComponent<GameDirector>();
            trail = GetComponent<BreadcrumbTrail>();
        }

        private void Update()
        {
            if (director == null || director.Phase == _lastPhase) return;
            _lastPhase = director.Phase;
            ShowToast(PhaseToast(director.Phase));
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
            var hasTarget = interactor != null && (interactor.Current != null || interactor.HideTarget != null);
            GUI.color = hasTarget ? new Color(1f, 0.9f, 0.3f) : new Color(1f, 1f, 1f, 0.5f);
            GUI.DrawTexture(centre, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawPrompt()
        {
            if (interactor == null) return;

            var config = RetraceConfig.Current;
            var text = string.Empty;
            if (!string.IsNullOrEmpty(interactor.CurrentPrompt) && config.showInteractionPrompt) text = "[" + config.interactKey + "] " + interactor.CurrentPrompt;
            if (!string.IsNullOrEmpty(interactor.HidePrompt))
            {
                if (text.Length > 0) text += "     ";
                text += "[" + config.hideKey + "] " + interactor.HidePrompt;
            }

            if (text.Length == 0) return;

            var rect = new Rect(0f, HudScale.Height * 0.5f + 24f, HudScale.Width, 28f);
            GUI.Label(rect, text, _centered);
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

            if (director.Phase == GamePhase.Spectate) DrawSpectateBanner();
            DrawToast();
            if (director.Phase != GamePhase.Menu && director.Phase != GamePhase.Results) DrawConnection();
        }

        /// <summary>Spectating is the one banner that stays up: the camera hint and the
        /// live tries count are what the watcher keeps needing.</summary>
        private void DrawSpectateBanner()
        {
            var banner = $"Spectating Player {director.CurrentPlayer} -- round {director.StealthRound + 1} [{director.LivesRemaining}/{RetraceConfig.Current.stealthLives} tries]";
            if (director.spectator != null)
            {
                banner += $"  [{RetraceConfig.Current.SpectatorCameraKey}] {ViewName(director.spectator.NextView)}";
            }

            GUI.Label(new Rect(0f, 24f, HudScale.Width, 30f), banner, _centered);
        }

        private string PhaseToast(GamePhase phase)
        {
            var maxLives = RetraceConfig.Current.stealthLives;
            var who = director.Multiplayer ? $"P{director.CurrentPlayer} -- " : string.Empty;
            switch (phase)
            {
                case GamePhase.Search:
                    return who + "Find your keys";
                case GamePhase.Stealth:
                    // A retry only needs the stakes; the goal was spelled out on the first attempt.
                    if (director.LivesRemaining < maxLives)
                    {
                        if (director.LivesRemaining == 1)
                        {
                            return "Last try";
                        }
                        return $"{director.LivesRemaining} tries left";
                    }

                    var toast = $"{who}Round {director.StealthRound + 1}";
                    if (AnyDoorUnlocksThisRound()) toast += " -- 2nd floor unlocked!";
                    return toast;
                default:
                    return string.Empty;
            }
        }

        private void ShowToast(string text)
        {
            _toast = text;
            _toastStartedAt = Time.time;
        }

        private void DrawToast()
        {
            if (string.IsNullOrEmpty(_toast)) return;

            var config = RetraceConfig.Current;
            var elapsed = Time.time - _toastStartedAt;
            var alpha = 1f - Mathf.Clamp01((elapsed - config.bannerHoldSeconds) / Mathf.Max(config.bannerFadeSeconds, 0.01f));
            if (alpha <= 0f)
            {
                _toast = string.Empty;
                return;
            }

            // Above the reticle, clear of the interaction prompt that sits just below it.
            DrawOutlinedLabel(new Rect(0f, HudScale.Height * 0.5f - 90f, HudScale.Width, 36f), _toast, _centered, alpha);
        }

        /// <summary>IMGUI has no text outline, so the outline is the same label stamped in
        /// black around the eight neighbouring pixels. White text alone vanishes against
        /// the pale walls and floors of the house.</summary>
        private static void DrawOutlinedLabel(Rect rect, string text, GUIStyle style, float alpha)
        {
            var textColor = style.normal.textColor;
            style.normal.textColor = Color.black;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    GUI.Label(new Rect(rect.x + dx, rect.y + dy, rect.width, rect.height), text, style);
                }
            }

            style.normal.textColor = textColor;
            GUI.Label(rect, text, style);
            GUI.color = Color.white;
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

            var streamed = director.Phase == GamePhase.Spectate && director.spectator != null
                ? $", {director.spectator.StreamedSentries} in stream"
                : string.Empty;
            if (active == 0) return "Sentries: none active" + streamed;
            return string.Format("Sentries: {0} active, nearest {1:0.0}m ({2}){3}", active, nearest, nearestState, streamed);
        }
    }
}

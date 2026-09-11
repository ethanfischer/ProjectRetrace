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
        public PlayerThrower thrower;
        public PlayerBombCarrier bombCarrier;
        public BreadcrumbTrail trail;

        private KeyItem _key;

        private GamePhase _lastPhase = GamePhase.Menu;
        private string _toast = string.Empty;
        private float _toastStartedAt;

        private GUIStyle _label;
        private GUIStyle _centered;
        private GUIStyle _handover;
        private GUIStyle _cash;

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
                DrawBombLocator();
            }

            if (director == null || (director.Phase != GamePhase.Results && director.Phase != GamePhase.Spectate))
            {
                DrawReticle();
                DrawPrompt();
                DrawBombIcon();
            }

            if (director != null && director.Phase != GamePhase.Results) DrawCashTotal();

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
            if (key == null) return;
            DrawLocator(key.transform.position, "KEYS", key.CanInteract);
        }

        /// <summary>Same aid for the bomb. Once armed it is marked in red at the container
        /// it waits in; while carried or spent there is nothing in the world to point at.</summary>
        private void DrawBombLocator()
        {
            var bomb = BombItem.Current;
            if (bomb == null || bomb.Carried || bomb.Spent) return;
            DrawLocator(bomb.transform.position, bomb.Armed ? "BOMB (armed)" : "BOMB", !bomb.Armed);
        }

        private void DrawLocator(Vector3 worldPosition, string label, bool available)
        {
            var camera = Camera.main;
            if (camera == null) return;

            var screen = camera.WorldToScreenPoint(worldPosition);
            if (screen.z <= 0f) return;

            // WorldToScreenPoint is in device pixels; the GUI matrix works in reference units.
            var rect = new Rect(screen.x / HudScale.Factor - 80f,
                HudScale.Height - screen.y / HudScale.Factor - 14f, 160f, 28f);
            GUI.color = available ? new Color(1f, 0.9f, 0.3f) : new Color(1f, 0.4f, 0.4f);
            GUI.Label(rect, string.Format("v {0} {1:0.0}m", label, screen.z), _centered);
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

        /// <summary>The pocket, shown rather than written: a carried bomb is a standing
        /// state, not a prompt, and a line of text under the reticle read as one.</summary>
        private void DrawBombIcon()
        {
            if (bombCarrier == null || !bombCarrier.Carrying) return;
            var icon = BombIcon.Texture;
            const float size = 64f;
            var rect = new Rect(HudScale.Width - size - 24f, HudScale.Height - size - 24f, size, size);
            GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit, true);
        }

        /// <summary>Bottom-left, opposite the bomb: a running total is a standing state
        /// like the pocket, not a prompt. Couch mode shows both players so the score is a
        /// contest at a glance.</summary>
        private void DrawCashTotal()
        {
            if (!RetraceConfig.Current.cashEnabled) return;
            var text = director.Multiplayer
                ? $"P1 ${director.CashOf(1)}    P2 ${director.CashOf(2)}"
                : $"${director.CashOf(1)}";
            HudText.OutlinedLabel(new Rect(24f, HudScale.Height - 56f, 400f, 32f), text, _cash);
        }

        private static string BombStatusLine()
        {
            var bomb = BombItem.Current;
            if (bomb == null) return "Bomb: NOT FOUND IN SCENE";
            if (bomb.Spent) return "Bomb: spent";
            if (bomb.Carried) return "Bomb: in pocket";

            var pos = bomb.transform.position;
            var spot = bomb.GetComponentInParent<KeySpotMarker>();
            var propName = spot != null && spot.PropRoot != null ? spot.PropRoot.name : "no prop";
            return string.Format("Bomb: ({0:0.0}, {1:0.0}, {2:0.0}) in \"{3}\"{4}",
                pos.x, pos.y, pos.z, propName, bomb.Armed ? "  [ARMED]" : "");
        }

        private void EnsureStyles()
        {
            // A domain reload mid-play keeps the field but hands back a hollow GUIStyle;
            // font size 0 is the tell.
            if (_label != null && _label.fontSize != 0) return;

            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, wordWrap = true };
            _label.normal.textColor = Color.white;

            _centered = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            _handover = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            _cash = new GUIStyle(_label) { fontSize = 24, fontStyle = FontStyle.Bold };
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

            if (thrower != null && !string.IsNullOrEmpty(thrower.Prompt))
            {
                if (text.Length > 0) text += "     ";
                text += "[" + config.throwKey + "] " + thrower.Prompt;
            }

            if (bombCarrier != null && !string.IsNullOrEmpty(bombCarrier.Prompt))
            {
                if (text.Length > 0) text += "     ";
                text += "[" + config.bombKey + "] " + bombCarrier.Prompt;
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
            var banner = $"Spectating Player {director.CurrentPlayer} \nRound {director.StealthRound + 1} \n[{director.LivesRemaining}/{RetraceConfig.Current.stealthLives} tries]";
            if (director.spectator != null)
            {
                banner += $"  [{RetraceConfig.Current.SpectatorCameraKey}] {ViewName(director.spectator.NextView)}";
            }

            GUI.Label(new Rect(0f, 24f, HudScale.Width, 30f), banner, _centered);
        }

        private string PhaseToast(GamePhase phase)
        {
            var maxLives = RetraceConfig.Current.stealthLives;
            var who = director.Multiplayer ? $"Player {director.CurrentPlayer}: " : string.Empty;
            switch (phase)
            {
                case GamePhase.Search:
                    return who + "Find the keys";
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
                    if (FloorGate.UnlocksThisRound || AnyDoorUnlocksThisRound()) toast += "\nLook upstairs";
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

            // Above the reticle, clear of the interaction prompt that sits just below it;
            // tall enough for the two-line round toast, which grows downward from the same top.
            HudText.OutlinedLabel(new Rect(0f, HudScale.Height * 0.5f - 90f, HudScale.Width, 72f), _toast, _centered, alpha);
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
                $"{ghosts} ghost{(ghosts == 1 ? "" : "s")} on patrol. Press Space when ready",
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
            GUILayout.Label(BombStatusLine(), _label);
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

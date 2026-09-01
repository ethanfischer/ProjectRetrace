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

        private void Reset()
        {
            director = GetComponent<GameDirector>();
            trail = GetComponent<BreadcrumbTrail>();
        }

        private void OnGUI()
        {
            HudScale.Apply();
            EnsureStyles();

            if (GameDirector.DebugVisible)
            {
                DrawKeyLocator();
            }

            if (director == null || director.Phase != GamePhase.Results)
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

            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
            _label.normal.textColor = Color.white;

            _centered = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
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

            string banner;
            var maxLives = director.EffectiveSettings.stealthLives;
            switch (director.Phase)
            {
                case GamePhase.Search:
                    banner = "Find your keys";
                    break;
                case GamePhase.Transition:
                    banner = director.LivesRemaining < maxLives
                        ? $"Caught! {director.LivesRemaining} {(director.LivesRemaining == 1 ? "try" : "tries")} left..."
                        : string.Empty;
                    break;
                case GamePhase.Stealth:
                    banner = $"Round {director.StealthRound + 1}: find your keys without getting caught [{director.LivesRemaining}/{maxLives} tries]";
                    break;
                default:
                    return;
            }

            GUI.Label(new Rect(0f, 24f, HudScale.Width, 30f), banner, _centered);
        }

        private void DrawStats()
        {
            if (trail == null) return;

            var settings = trail.EffectiveSettings;

            var box = new Rect(12f, 12f, 300f, 210f);
            GUI.Box(box, GUIContent.none);

            GUILayout.BeginArea(new Rect(box.x + 10f, box.y + 8f, box.width - 20f, box.height - 16f));
            GUILayout.Label("<b>DEBUG</b>  (" + settings.debugToggleKey + " to hide)", _label);
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

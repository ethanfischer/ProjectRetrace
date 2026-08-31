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
        public PatrolSentry sentry;

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
            switch (director.Phase)
            {
                case GamePhase.Search:
                    banner = "Find your keys";
                    break;
                case GamePhase.Transition:
                    banner = "Someone's coming to retrace your steps...";
                    break;
                case GamePhase.Stealth:
                    banner = "Steal the keys back. Don't get seen.";
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

            var box = new Rect(12f, 12f, 300f, 190f);
            GUI.Box(box, GUIContent.none);

            GUILayout.BeginArea(new Rect(box.x + 10f, box.y + 8f, box.width - 20f, box.height - 16f));
            GUILayout.Label("<b>DEBUG</b>  (" + settings.debugToggleKey + " to hide)", _label);
            GUILayout.Label("Phase: " + (director != null ? director.Phase.ToString() : "-"), _label);
            GUILayout.Label(string.Format("Route: {0} crumbs, {1} stops, {2:0.0}m",
                trail.Phase1Crumbs.Count, trail.DwellPoints.Count, trail.Phase1Distance), _label);
            GUILayout.Label(string.Format("Sneak: {0} crumbs, {1:0.0}m",
                trail.Phase2Crumbs.Count, trail.Phase2Distance), _label);
            GUILayout.Label(SentryStatusLine(), _label);
            GUILayout.Label(string.Format("spacing {0:0.00}m", settings.dotSpacing), _label);
            GUILayout.Label(KeyStatusLine(), _label);
            GUILayout.EndArea();
        }

        private string SentryStatusLine()
        {
            if (sentry == null) return "Sentry: NOT WIRED";
            if (sentry.State == SentryState.Inactive) return "Sentry: inactive";

            var distance = sentry.player != null
                ? Vector3.Distance(sentry.transform.position, sentry.player.transform.position)
                : -1f;
            return string.Format("Sentry: {0} -> crumb {1}, {2:0.0}m away",
                sentry.State, sentry.TargetIndex, distance);
        }
    }
}

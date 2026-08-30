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

        private GUIStyle _label;
        private GUIStyle _centered;

        private void Reset()
        {
            director = GetComponent<GameDirector>();
            trail = GetComponent<BreadcrumbTrail>();
        }

        private void OnGUI()
        {
            EnsureStyles();

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

        private void EnsureStyles()
        {
            if (_label != null) return;

            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
            _label.normal.textColor = Color.white;

            _centered = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
        }

        private void DrawReticle()
        {
            var centre = new Rect(Screen.width * 0.5f - 3f, Screen.height * 0.5f - 3f, 6f, 6f);
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

            var rect = new Rect(0f, Screen.height * 0.5f + 24f, Screen.width, 28f);
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
                    banner = "Now retrace your steps...";
                    break;
                case GamePhase.Retrace:
                    banner = "Retrace your route";
                    break;
                default:
                    return;
            }

            var remaining = director.RetraceTimeRemaining;
            if (remaining >= 0f)
            {
                banner += string.Format("   {0:0}s", remaining);
            }

            GUI.Label(new Rect(0f, 24f, Screen.width, 30f), banner, _centered);
        }

        private void DrawStats()
        {
            if (trail == null) return;

            var score = trail.BuildScore();
            var settings = trail.EffectiveSettings;

            var box = new Rect(12f, 12f, 300f, 210f);
            GUI.Box(box, GUIContent.none);

            GUILayout.BeginArea(new Rect(box.x + 10f, box.y + 8f, box.width - 20f, box.height - 16f));
            GUILayout.Label("<b>DEBUG</b>  (" + settings.debugToggleKey + " to hide)", _label);
            GUILayout.Label("Phase: " + (director != null ? director.Phase.ToString() : "-"), _label);
            GUILayout.Label(string.Format("Overlap: {0} / {1} of round 1", score.Matched1, score.Total1), _label);
            GUILayout.Label(string.Format("On-path: {0} / {1} of round 2", score.Matched2, score.Total2), _label);
            GUILayout.Label(string.Format("Coverage: {0:P0}  Precision: {1:P0}", score.Coverage, score.Precision), _label);
            GUILayout.Label(string.Format("Distance: {0:0.0}m then {1:0.0}m", score.Phase1Distance, score.Phase2Distance), _label);
            GUILayout.Label(string.Format("Live score: {0}%", score.Percent), _label);
            GUILayout.Label(string.Format("spacing {0:0.00}m / radius {1:0.00}m", settings.dotSpacing, settings.collectRadius), _label);
            GUILayout.EndArea();
        }
    }
}

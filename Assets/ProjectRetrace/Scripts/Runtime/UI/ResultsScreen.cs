using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// End-of-run breakdown. Shows the two terms separately, because "you hit the marks but
    /// wandered" and "you moved efficiently but took a different route" are different mistakes
    /// and the player should be able to tell which one they made.
    /// </summary>
    public class ResultsScreen : MonoBehaviour
    {
        public GameDirector director;

        private GUIStyle _title;
        private GUIStyle _line;
        private GUIStyle _grade;

        private void Reset()
        {
            director = GetComponent<GameDirector>();
        }

        private void OnGUI()
        {
            if (director == null || director.Phase != GamePhase.Results) return;

            EnsureStyles();

            var result = director.LastResult;
            var panel = new Rect(Screen.width * 0.5f - 210f, Screen.height * 0.5f - 150f, 420f, 300f);
            GUI.Box(panel, GUIContent.none);

            GUILayout.BeginArea(new Rect(panel.x + 24f, panel.y + 20f, panel.width - 48f, panel.height - 40f));

            GUILayout.Label("RETRACE COMPLETE", _title);
            GUILayout.Space(6f);
            GUILayout.Label(string.Format("{0}   {1}%", result.Grade, result.Percent), _grade);
            GUILayout.Space(10f);

            GUILayout.Label(string.Format("Marks hit          {0} / {1}", result.Collected, result.Total), _line);
            GUILayout.Label(string.Format("Coverage           {0:P0}", result.Coverage), _line);
            GUILayout.Label(string.Format("Efficiency         {0:P0}", result.Efficiency), _line);
            GUILayout.Space(6f);
            GUILayout.Label(string.Format("Walked             {0:0.0}m  vs  {1:0.0}m", result.Phase2Distance, result.Phase1Distance), _line);
            GUILayout.Space(10f);
            GUILayout.Label(Verdict(result), _line);
            GUILayout.Space(10f);
            GUILayout.Label("[R] run again      green = hit, red = missed", _line);

            GUILayout.EndArea();
        }

        /// <summary>Names which of the two failure modes dominated, so the score is actionable.</summary>
        private static string Verdict(ScoreResult result)
        {
            if (result.Total == 0) return "No route was recorded.";
            if (result.Coverage >= 0.85f && result.Efficiency >= 0.85f) return "Near-perfect retrace.";
            if (result.Coverage < result.Efficiency) return "You missed part of your route.";
            if (result.Efficiency < result.Coverage) return "You hit your marks, but wandered getting there.";
            return "Close, but drifting.";
        }

        private void EnsureStyles()
        {
            if (_title != null) return;

            _title = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = Color.white;

            _grade = new GUIStyle(_title) { fontSize = 34 };
            _grade.normal.textColor = new Color(1f, 0.9f, 0.4f);

            _line = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _line.normal.textColor = Color.white;
        }
    }
}

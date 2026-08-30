using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// End-of-run breakdown, drawn as a compact top banner because Results is a walkable
    /// phase: the player wanders the house comparing the two trails. Shows the two terms
    /// separately, because "you missed part of your route" and "you dropped marks off the
    /// route" are different mistakes and the player should be able to tell which one they made.
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

            HudScale.Apply();
            EnsureStyles();

            var result = director.LastResult;
            var panel = new Rect(HudScale.Width * 0.5f - 260f, 12f, 520f, 128f);
            GUI.Box(panel, GUIContent.none);

            GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 10f, panel.width - 40f, panel.height - 20f));

            GUILayout.BeginHorizontal();
            GUILayout.Label("RETRACE COMPLETE", _title);
            GUILayout.FlexibleSpace();
            GUILayout.Label(string.Format("{0}  {1}%", result.Grade, result.Percent), _grade);
            GUILayout.EndHorizontal();

            GUILayout.Label(string.Format(
                "Coverage {0:P0}  ({1}/{2} of round 1 revisited)      Precision {3:P0}  ({4}/{5} of round 2 on the old path)",
                result.Coverage, result.Matched1, result.Total1,
                result.Precision, result.Matched2, result.Total2), _line);
            GUILayout.Label(string.Format("Walked {0:0.0}m vs {1:0.0}m.  {2}", result.Phase2Distance, result.Phase1Distance, Verdict(result)), _line);
            GUILayout.Label("Walk around and compare:  blue = round 1, orange = round 2.      [R] run again", _line);

            GUILayout.EndArea();
        }

        /// <summary>Names which of the two failure modes dominated, so the score is actionable.</summary>
        private static string Verdict(ScoreResult result)
        {
            if (result.Total1 == 0) return "No route was recorded.";
            if (result.Coverage >= 0.85f && result.Precision >= 0.85f) return "Near-perfect retrace.";
            if (result.Coverage < result.Precision) return "You missed part of your route.";
            if (result.Precision < result.Coverage) return "You strayed off your route.";
            return "Close, but drifting.";
        }

        private void EnsureStyles()
        {
            if (_title != null) return;

            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleLeft };
            _title.normal.textColor = Color.white;

            _grade = new GUIStyle(_title) { fontSize = 26, alignment = TextAnchor.MiddleRight };
            _grade.normal.textColor = new Color(1f, 0.9f, 0.4f);

            _line = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _line.normal.textColor = Color.white;
        }
    }
}

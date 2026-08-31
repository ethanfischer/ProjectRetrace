using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// End-of-run banner, compact because Results is a walkable phase: input stays on, so
    /// the outcome reads as a banner over the world rather than a hard cut to a menu.
    /// </summary>
    public class ResultsScreen : MonoBehaviour
    {
        public GameDirector director;

        private GUIStyle _title;
        private GUIStyle _line;

        private void Reset()
        {
            director = GetComponent<GameDirector>();
        }

        private void OnGUI()
        {
            if (director == null || director.Phase != GamePhase.Results) return;

            HudScale.Apply();
            EnsureStyles();

            var panel = new Rect(HudScale.Width * 0.5f - 260f, 12f, 520f, 72f);
            GUI.Box(panel, GUIContent.none);

            GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 10f, panel.width - 40f, panel.height - 20f));

            _title.normal.textColor = director.Won
                ? new Color(0.5f, 1f, 0.55f)
                : new Color(1f, 0.35f, 0.3f);
            GUILayout.Label(director.Won ? "YOU GOT AWAY WITH IT" : "CAUGHT", _title);
            GUILayout.Label("[R] run again", _line);

            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_title != null) return;

            _title = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };

            _line = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            _line.normal.textColor = Color.white;
        }
    }
}

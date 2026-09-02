using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// End-of-run banner: how far the run got before the catch that ended it. Compact
    /// because Results is a walkable phase -- input stays on, so the outcome reads as a
    /// banner over the world rather than a hard cut to a menu.
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

            var panel = new Rect(HudScale.Width * 0.5f - 260f, 12f, 520f, 92f);
            GUI.Box(panel, GUIContent.none);

            GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 10f, panel.width - 40f, panel.height - 20f));

            if (director.Multiplayer && director.Winner != 0)
            {
                _title.normal.textColor = new Color(0.5f, 1f, 0.55f);
                var you = director.Online ? (director.Winner == director.LocalPlayer ? "YOU WIN" : "YOU LOSE") : $"PLAYER {director.Winner} WINS";
                GUILayout.Label(you, _title);
                var rematch = director.Online && !director.online.IsHost ? "[R] ask for a rematch" : "[R] rematch (searcher rotates)";
                GUILayout.Label($"Player {director.Winner} was last standing after round {director.StealthRound + 1}. {rematch}  [M] menu", _line);
            }
            else
            {
                _title.normal.textColor = new Color(1f, 0.35f, 0.3f);
                GUILayout.Label("CAUGHT", _title);
                GUILayout.Label($"You made it to round {director.StealthRound + 1}. [R] run again  [M] menu", _line);
            }

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

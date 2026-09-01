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

            if (director.Multiplayer && director.Loser != 0)
            {
                // Two players have a clear winner to crown; with more, the loser is the
                // one story worth telling.
                _title.normal.textColor = new Color(0.5f, 1f, 0.55f);
                GUILayout.Label(director.PlayerCount == 2
                    ? $"PLAYER {(director.Loser == 1 ? 2 : 1)} WINS"
                    : $"PLAYER {director.Loser} IS OUT", _title);
                GUILayout.Label($"Player {director.Loser} was caught in round {director.StealthRound + 1}. [R] rematch (searcher rotates)  [M] menu", _line);
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

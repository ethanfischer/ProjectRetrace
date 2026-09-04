using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// End-of-run panel: how far the run got, then the same two choices the keys offer,
    /// as buttons. Laid out like the start menu so the two screens read as one system.
    /// </summary>
    public class ResultsScreen : MonoBehaviour
    {
        public GameDirector director;

        private GUIStyle _title;
        private GUIStyle _line;
        private GUIStyle _button;

        private void Reset()
        {
            director = GetComponent<GameDirector>();
        }

        private void Update()
        {
            if (director == null || director.Phase != GamePhase.Results || ConfigMenu.IsOpen) return;
            if (Cursor.lockState != CursorLockMode.None) FirstPersonController.LockCursor(false);
        }

        private void OnGUI()
        {
            if (director == null || director.Phase != GamePhase.Results || ConfigMenu.IsOpen) return;

            HudScale.Apply();
            EnsureStyles();

            var panel = new Rect(HudScale.Width * 0.5f - 220f, HudScale.Height * 0.5f - 120f, 440f, 240f);
            GUI.Box(panel, GUIContent.none);

            HudText.OutlinedLabel(new Rect(panel.x, panel.y + 20f, panel.width, 40f), Headline(), _title);
            var detail = Detail();
            if (detail.Length > 0) HudText.OutlinedLabel(new Rect(panel.x + 20f, panel.y + 62f, panel.width - 40f, 24f), detail, _line);

            if (MenuButton(panel, 0, NewRunLabel())) director.StartRun();
            if (MenuButton(panel, 1, "Menu")) director.LeaveToMenu();
        }

        private string Headline()
        {
            if (!director.Multiplayer || director.Winner == 0) return $"You made it to round {director.StealthRound + 1}";
            if (director.Online) return director.Winner == director.LocalPlayer ? "You win" : "You lose";
            return $"Player {director.Winner} wins";
        }

        private string Detail()
        {
            if (!director.Multiplayer || director.Winner == 0) return string.Empty;
            return $"Player {director.Winner} was last standing after round {director.StealthRound + 1}";
        }

        private string NewRunLabel()
        {
            if (director.Online && !director.online.IsHost) return "Ask for a rematch";
            return director.Multiplayer ? "Rematch" : "New run";
        }

        private bool MenuButton(Rect panel, int row, string label)
        {
            return HudText.OutlinedButton(new Rect(panel.x + 70f, panel.y + 100f + row * 52f, 300f, 42f), label, _button);
        }

        private void EnsureStyles()
        {
            // A domain reload mid-play keeps the field but hands back a hollow GUIStyle;
            // font size 0 is the tell.
            if (_title != null && _title.fontSize != 0) return;

            _title = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = Color.white;

            _line = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _line.normal.textColor = new Color(1f, 1f, 1f, 0.75f);

            _button = new GUIStyle(GUI.skin.button) { fontSize = 16 };
        }
    }
}

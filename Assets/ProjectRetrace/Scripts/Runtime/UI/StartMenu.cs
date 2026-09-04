using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>
    /// The start menu: Singleplayer, Multiplayer (couch, two players), or Online. IMGUI
    /// like the rest of the UI, drawn over the live scene -- the empty house makes its own
    /// title screen. Buttons for the mouse, 1/2/3 for the keyboard.
    /// </summary>
    public class StartMenu : MonoBehaviour
    {
        public GameDirector director;
        public OnlineSession online;

        private GUIStyle _title;
        private GUIStyle _subtitle;
        private GUIStyle _button;

        private bool _multiplayerExpanded;

        private void Reset()
        {
            director = GetComponent<GameDirector>();
        }

        private void Update()
        {
            if (director == null || director.Phase != GamePhase.Menu || ConfigMenu.IsOpen || LobbyOpen) return;

            // Something else may lock the cursor after EnterMenu runs (the player
            // controller locks it in its own Start); the menu needs it free every frame.
            if (Cursor.lockState != CursorLockMode.None) FirstPersonController.LockCursor(false);

            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) director.StartGame(1);
            else if (keyboard.digit2Key.wasPressedThisFrame) director.StartGame(2);
            else if (keyboard.digit3Key.wasPressedThisFrame && online != null) online.OpenLobby();
        }

        private bool LobbyOpen => online != null && online.State != NetState.Idle;

        private void OnGUI()
        {
            if (director == null || director.Phase != GamePhase.Menu || ConfigMenu.IsOpen || LobbyOpen) return;

            HudScale.Apply();
            EnsureStyles();

            var panel = new Rect(HudScale.Width * 0.5f - 220f, HudScale.Height * 0.5f - 168f, 440f, 336f);
            GUI.Box(panel, GUIContent.none);

            HudText.OutlinedLabel(new Rect(panel.x, panel.y + 20f, panel.width, 40f), "PROJECT RETRACE", _title);

            if (_multiplayerExpanded) DrawMultiplayerMenu(panel);
            else DrawMainMenu(panel);
        }

        private void DrawMainMenu(Rect panel)
        {
            if (MenuButton(panel, 0, "Singleplayer")) director.StartGame(1);
            if (MenuButton(panel, 1, "Multiplayer")) _multiplayerExpanded = true;
            if (MenuButton(panel, 2, "Settings")) ConfigMenu.Toggle();
        }

        private void DrawMultiplayerMenu(Rect panel)
        {
            if (MenuButton(panel, 0, "Local")) director.StartGame(2);
            if (online != null && MenuButton(panel, 1, "Online")) online.OpenLobby();
            if (MenuButton(panel, 2, "← Back")) _multiplayerExpanded = false;
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

            _subtitle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            _subtitle.normal.textColor = new Color(1f, 1f, 1f, 0.75f);

            _button = new GUIStyle(GUI.skin.button) { fontSize = 16 };
        }
    }
}

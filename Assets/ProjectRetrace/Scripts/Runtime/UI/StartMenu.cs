using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>
    /// The start menu: Singleplayer or Multiplayer (local, two players). IMGUI like the
    /// rest of the UI, drawn over the live scene -- the empty house makes its own title
    /// screen. Buttons for the mouse, 1/2 for the keyboard.
    ///
    /// The director supports 3-4 players and ONLINE.md sketches online play, but neither
    /// is surfaced here yet: one hidden mode that works beats a menu tree of stubs.
    /// </summary>
    public class StartMenu : MonoBehaviour
    {
        public GameDirector director;

        private GUIStyle _title;
        private GUIStyle _subtitle;
        private GUIStyle _button;

        private void Reset()
        {
            director = GetComponent<GameDirector>();
        }

        private void Update()
        {
            if (director == null || director.Phase != GamePhase.Menu || ConfigMenu.IsOpen) return;

            // Something else may lock the cursor after EnterMenu runs (the player
            // controller locks it in its own Start); the menu needs it free every frame.
            if (Cursor.lockState != CursorLockMode.None) FirstPersonController.LockCursor(false);

            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) director.StartGame(1);
            else if (keyboard.digit2Key.wasPressedThisFrame) director.StartGame(2);
        }

        private void OnGUI()
        {
            if (director == null || director.Phase != GamePhase.Menu || ConfigMenu.IsOpen) return;

            HudScale.Apply();
            EnsureStyles();

            var panel = new Rect(HudScale.Width * 0.5f - 220f, HudScale.Height * 0.5f - 142f, 440f, 284f);
            GUI.Box(panel, GUIContent.none);

            GUI.Label(new Rect(panel.x, panel.y + 20f, panel.width, 40f), "PROJECT RETRACE", _title);
            GUI.Label(new Rect(panel.x, panel.y + 60f, panel.width, 24f),
                "Find your keys. Don't get caught by your past selves.", _subtitle);

            if (GUI.Button(new Rect(panel.x + 70f, panel.y + 100f, 300f, 42f), "[1]  Singleplayer", _button))
            {
                director.StartGame(1);
            }

            if (GUI.Button(new Rect(panel.x + 70f, panel.y + 152f, 300f, 42f), "[2]  Multiplayer", _button))
            {
                director.StartGame(2);
            }

            var configKey = RetraceConfig.Current.ConfigMenuKey;
            if (GUI.Button(new Rect(panel.x + 70f, panel.y + 204f, 300f, 42f), "[" + configKey + "]  Settings", _button))
            {
                ConfigMenu.Toggle();
            }
        }

        private void EnsureStyles()
        {
            if (_title != null) return;

            _title = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = Color.white;

            _subtitle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            _subtitle.normal.textColor = new Color(1f, 1f, 1f, 0.75f);

            _button = new GUIStyle(GUI.skin.button) { fontSize = 16 };
        }
    }
}

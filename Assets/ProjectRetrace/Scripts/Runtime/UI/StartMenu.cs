using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>
    /// The start menu: pick single player or couch 2P. IMGUI like the rest of the UI, drawn
    /// over the live scene (the empty house makes its own title screen). Buttons for the
    /// mouse, 1/2 for the keyboard.
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
            if (director == null || director.Phase != GamePhase.Menu) return;

            // Something else may lock the cursor after EnterMenu runs (the player
            // controller locks it in its own Start); the menu needs it free every frame.
            if (Cursor.lockState != CursorLockMode.None) FirstPersonController.LockCursor(false);

            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) director.StartGame(twoPlayers: false);
            else if (keyboard.digit2Key.wasPressedThisFrame) director.StartGame(twoPlayers: true);
        }

        private void OnGUI()
        {
            if (director == null || director.Phase != GamePhase.Menu) return;

            HudScale.Apply();
            EnsureStyles();

            var panel = new Rect(HudScale.Width * 0.5f - 220f, HudScale.Height * 0.5f - 130f, 440f, 260f);
            GUI.Box(panel, GUIContent.none);

            GUI.Label(new Rect(panel.x, panel.y + 22f, panel.width, 40f), "PROJECT RETRACE", _title);
            GUI.Label(new Rect(panel.x, panel.y + 64f, panel.width, 24f),
                "Steal your keys back from your own past selves", _subtitle);

            if (GUI.Button(new Rect(panel.x + 70f, panel.y + 110f, 300f, 44f), "[1]  Single player", _button))
            {
                director.StartGame(twoPlayers: false);
            }

            if (GUI.Button(new Rect(panel.x + 70f, panel.y + 166f, 300f, 44f), "[2]  Couch 2P", _button))
            {
                director.StartGame(twoPlayers: true);
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

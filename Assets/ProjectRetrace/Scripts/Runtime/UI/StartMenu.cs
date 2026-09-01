using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>
    /// The start menu: Singleplayer, or Multiplayer -> Local / Online -> player count.
    /// IMGUI like the rest of the UI, drawn over the live scene (the empty house makes its
    /// own title screen). Buttons for the mouse, number keys for the keyboard, Escape to
    /// go back. Online is navigable but not built yet -- picking a count says so.
    /// </summary>
    public class StartMenu : MonoBehaviour
    {
        private enum Screen
        {
            Root,
            MultiplayerType,
            LocalCount,
            OnlineCount
        }

        public GameDirector director;

        private Screen _screen = Screen.Root;
        private string _notice;
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
            if (keyboard.digit1Key.wasPressedThisFrame) Choose(1);
            else if (keyboard.digit2Key.wasPressedThisFrame) Choose(2);
            else if (keyboard.digit3Key.wasPressedThisFrame) Choose(3);
            else if (keyboard.digit4Key.wasPressedThisFrame) Choose(4);
            else if (keyboard.escapeKey.wasPressedThisFrame) Back();
        }

        private void Choose(int option)
        {
            _notice = null;
            switch (_screen)
            {
                case Screen.Root:
                    if (option == 1) director.StartGame(1);
                    else if (option == 2) _screen = Screen.MultiplayerType;
                    break;
                case Screen.MultiplayerType:
                    if (option == 1) _screen = Screen.LocalCount;
                    else if (option == 2) _screen = Screen.OnlineCount;
                    break;
                case Screen.LocalCount:
                    if (option >= 2 && option <= 4) director.StartGame(option);
                    break;
                case Screen.OnlineCount:
                    if (option >= 2 && option <= 4) _notice = "Online play isn't built yet -- see ONLINE.md for the plan.";
                    break;
            }
        }

        private void Back()
        {
            _notice = null;
            switch (_screen)
            {
                case Screen.MultiplayerType:
                    _screen = Screen.Root;
                    break;
                case Screen.LocalCount:
                case Screen.OnlineCount:
                    _screen = Screen.MultiplayerType;
                    break;
            }
        }

        private const float OptionStride = 52f;
        private const float OptionsTop = 100f;

        private void OnGUI()
        {
            if (director == null || director.Phase != GamePhase.Menu) return;

            HudScale.Apply();
            EnsureStyles();

            // The panel grows with its contents -- a fixed height is how the 4-player
            // button ended up wearing the Back button as a hat.
            var options = _screen == Screen.Root ? 2 : _screen == Screen.MultiplayerType ? 2 : 3;
            var showBack = _screen != Screen.Root;
            var height = OptionsTop + options * OptionStride
                + (showBack ? 44f : 8f) + (_notice != null ? 30f : 0f) + 12f;
            var panel = new Rect(HudScale.Width * 0.5f - 220f, HudScale.Height * 0.5f - height * 0.5f, 440f, height);
            GUI.Box(panel, GUIContent.none);

            GUI.Label(new Rect(panel.x, panel.y + 20f, panel.width, 40f), "PROJECT RETRACE", _title);
            GUI.Label(new Rect(panel.x, panel.y + 60f, panel.width, 24f),
                "Find your keys. Don't get caught by your past selves.", _subtitle);

            switch (_screen)
            {
                case Screen.Root:
                    Option(panel, 0, "[1]  Singleplayer", () => Choose(1));
                    Option(panel, 1, "[2]  Multiplayer", () => Choose(2));
                    break;
                case Screen.MultiplayerType:
                    Option(panel, 0, "[1]  Local (one keyboard)", () => Choose(1));
                    Option(panel, 1, "[2]  Online", () => Choose(2));
                    break;
                case Screen.LocalCount:
                case Screen.OnlineCount:
                    Option(panel, 0, "[2]  2 players", () => Choose(2));
                    Option(panel, 1, "[3]  3 players", () => Choose(3));
                    Option(panel, 2, "[4]  4 players", () => Choose(4));
                    break;
            }

            var cursor = panel.y + OptionsTop + options * OptionStride;
            if (showBack)
            {
                if (GUI.Button(new Rect(panel.x + 70f, cursor + 6f, 300f, 30f), "[Esc]  Back", _button))
                {
                    Back();
                }

                cursor += 44f;
            }

            if (_notice != null)
            {
                GUI.Label(new Rect(panel.x, cursor + 4f, panel.width, 24f), _notice, _subtitle);
            }
        }

        private void Option(Rect panel, int slot, string label, System.Action pick)
        {
            if (GUI.Button(new Rect(panel.x + 70f, panel.y + OptionsTop + slot * OptionStride, 300f, 42f), label, _button))
            {
                pick();
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

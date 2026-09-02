using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>
    /// The online lobby, drawn over the start menu: create a room and read the code out,
    /// or type a friend's. Four characters is the whole invitation -- the relay does the
    /// pairing and the handshake does the version check.
    /// </summary>
    public class OnlineLobby : MonoBehaviour
    {
        public GameDirector director;
        public OnlineSession session;

        private string _code = string.Empty;
        private GUIStyle _title;
        private GUIStyle _line;
        private GUIStyle _button;

        public bool IsOpen => session != null && session.State != NetState.Idle && director != null && director.Phase == GamePhase.Menu;

        private void Update()
        {
            if (!IsOpen || ConfigMenu.IsOpen) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) session.Leave();
        }

        private void OnGUI()
        {
            if (!IsOpen || ConfigMenu.IsOpen) return;

            HudScale.Apply();
            EnsureStyles();

            var panel = new Rect(HudScale.Width * 0.5f - 220f, HudScale.Height * 0.5f - 150f, 440f, 300f);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x, panel.y + 16f, panel.width, 32f), "ONLINE", _title);

            var y = panel.y + 60f;
            switch (session.State)
            {
                case NetState.Lobby:
                case NetState.Error:
                case NetState.Disconnected:
                    DrawEntry(panel, ref y);
                    break;
                case NetState.Connecting:
                    GUI.Label(new Rect(panel.x, y, panel.width, 24f), "Connecting to " + OnlineSession.RelayUrl + "...", _line);
                    y += 40f;
                    break;
                case NetState.InRoom:
                    GUI.Label(new Rect(panel.x, y, panel.width, 24f), "Room code", _line);
                    GUI.Label(new Rect(panel.x, y + 24f, panel.width, 40f), session.Room, _title);
                    GUI.Label(new Rect(panel.x, y + 70f, panel.width, 24f),
                        session.PeerPresent ? "Checking builds match..." : "Waiting for a friend to join...", _line);
                    y += 110f;
                    break;
                case NetState.Ready:
                    GUI.Label(new Rect(panel.x, y, panel.width, 24f), $"Room {session.Room} -- you are Player {session.Seat}", _line);
                    y += 34f;
                    if (session.IsHost)
                    {
                        if (GUI.Button(new Rect(panel.x + 70f, y, 300f, 42f), "Start match", _button)) director.StartOnlineRun();
                    }
                    else
                    {
                        GUI.Label(new Rect(panel.x, y + 8f, panel.width, 24f), "Waiting for the host to start...", _line);
                    }

                    y += 56f;
                    break;
            }

            if (session.State == NetState.Error || session.State == NetState.Disconnected)
            {
                GUI.Label(new Rect(panel.x + 10f, panel.y + panel.height - 74f, panel.width - 20f, 40f),
                    (session.State == NetState.Error ? "Error: " : "Disconnected: ") + session.LastError, _line);
            }

            if (GUI.Button(new Rect(panel.x + 70f, panel.y + panel.height - 50f, 300f, 36f), "[Esc]  Back", _button))
            {
                session.Leave();
            }
        }

        private void DrawEntry(Rect panel, ref float y)
        {
            if (GUI.Button(new Rect(panel.x + 70f, y, 300f, 42f), "Create room", _button)) session.CreateRoom();
            y += 52f;

            GUI.SetNextControlName("code");
            _code = GUI.TextField(new Rect(panel.x + 70f, y, 140f, 42f), _code, 4, _button).ToUpperInvariant();
            if (GUI.Button(new Rect(panel.x + 220f, y, 150f, 42f), "Join", _button) && _code.Length == 4) session.JoinRoom(_code);
            y += 52f;

            if (session.CanResume && GUI.Button(new Rect(panel.x + 70f, y, 300f, 42f), "Resume last match", _button)) session.Resume();
            y += 52f;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = Color.white;
            _line = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _line.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
            _button = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            _button.alignment = TextAnchor.MiddleCenter;
        }
    }
}

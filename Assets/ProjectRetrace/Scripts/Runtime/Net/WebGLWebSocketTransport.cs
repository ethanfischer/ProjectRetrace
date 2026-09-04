#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;

namespace ProjectRetrace
{
    /// <summary>
    /// Browser transport: the page's own WebSocket, reached through RetraceWebSocket.jslib.
    /// The browser calls back into a named GameObject via SendMessage, so the session
    /// forwards those calls here; delivery is already on the main thread.
    /// </summary>
    public class WebGLWebSocketTransport : INetTransport
    {
        [DllImport("__Internal")] private static extern void RetraceWs_Connect(string url, string gameObjectName);
        [DllImport("__Internal")] private static extern void RetraceWs_Send(string json);
        [DllImport("__Internal")] private static extern void RetraceWs_Close();
        [DllImport("__Internal")] private static extern int RetraceWs_State();

        private readonly string _receiverName;

        public WebGLWebSocketTransport(string receiverName)
        {
            _receiverName = receiverName;
        }

        public bool IsOpen => RetraceWs_State() == 1;

        public event Action Opened;
        public event Action<string> Closed;
        public event Action<string> Message;

        public void Connect(string url) => RetraceWs_Connect(url, _receiverName);
        public void Send(string json) => RetraceWs_Send(json);
        public void Close() => RetraceWs_Close();
        public void Poll() { }

        public void HandleOpen() => Opened?.Invoke();
        public void HandleMessage(string json) => Message?.Invoke(json);
        public void HandleClose(string reason) => Closed?.Invoke(reason);
    }
}
#endif

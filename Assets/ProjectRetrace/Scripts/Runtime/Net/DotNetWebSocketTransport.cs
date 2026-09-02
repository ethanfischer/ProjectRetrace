#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectRetrace
{
    /// <summary>
    /// Editor and standalone transport over System.Net.WebSockets. The socket's callbacks
    /// land on thread-pool threads, so they are queued and replayed from Poll.
    /// </summary>
    public class DotNetWebSocketTransport : INetTransport
    {
        private const int ReceiveBufferBytes = 64 * 1024;

        private ClientWebSocket _socket;
        private CancellationTokenSource _cancel;
        private readonly ConcurrentQueue<Action> _pending = new ConcurrentQueue<Action>();
        private Task _sendChain = Task.CompletedTask;
        private readonly object _sendLock = new object();

        public bool IsOpen => _socket != null && _socket.State == WebSocketState.Open;

        public event Action Opened;
        public event Action<string> Closed;
        public event Action<string> Message;

        public void Connect(string url)
        {
            Close();
            _socket = new ClientWebSocket();
            _cancel = new CancellationTokenSource();
            var socket = _socket;
            var token = _cancel.Token;
            Task.Run(async () =>
            {
                try
                {
                    await socket.ConnectAsync(new Uri(url), token);
                    _pending.Enqueue(() => Opened?.Invoke());
                    await ReceiveLoop(socket, token);
                }
                catch (Exception e)
                {
                    if (!token.IsCancellationRequested) _pending.Enqueue(() => Closed?.Invoke(e.Message));
                }
            });
        }

        private async Task ReceiveLoop(ClientWebSocket socket, CancellationToken token)
        {
            var buffer = new byte[ReceiveBufferBytes];
            var text = new StringBuilder();
            while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _pending.Enqueue(() => Closed?.Invoke(result.CloseStatusDescription ?? "closed"));
                    return;
                }

                text.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (!result.EndOfMessage) continue;

                var message = text.ToString();
                text.Clear();
                _pending.Enqueue(() => Message?.Invoke(message));
            }
        }

        public void Send(string json)
        {
            var socket = _socket;
            if (socket == null || socket.State != WebSocketState.Open) return;

            var bytes = Encoding.UTF8.GetBytes(json);
            // Sends are chained: ClientWebSocket allows only one in flight, and a burst of
            // snapshots would otherwise throw.
            lock (_sendLock)
            {
                _sendChain = _sendChain.ContinueWith(_ =>
                    socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None)).Unwrap();
            }
        }

        public void Close()
        {
            if (_socket == null) return;
            _cancel?.Cancel();
            try { _socket.Dispose(); } catch { }
            _socket = null;
        }

        public void Poll()
        {
            while (_pending.TryDequeue(out var action)) action();
        }
    }
}
#endif

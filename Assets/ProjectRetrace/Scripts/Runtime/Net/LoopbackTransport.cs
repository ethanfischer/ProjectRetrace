using System;
using System.Collections.Generic;

namespace ProjectRetrace
{
    /// <summary>
    /// A transport with nobody on the other end, for driving the session from a test or an
    /// editor eval: Inject plays a message as if the relay sent it, Sent records what the
    /// game tried to say.
    /// </summary>
    public class LoopbackTransport : INetTransport
    {
        public readonly List<string> Sent = new List<string>();
        private readonly Queue<string> _incoming = new Queue<string>();
        private bool _open;

        public bool IsOpen => _open;

        public event Action Opened;
        public event Action<string> Closed;
        public event Action<string> Message;

        public void Connect(string url)
        {
            _open = true;
            Opened?.Invoke();
        }

        public void Send(string json) => Sent.Add(json);

        public void Close()
        {
            if (!_open) return;
            _open = false;
            Closed?.Invoke("closed");
        }

        public void Inject(string json) => _incoming.Enqueue(json);

        public void Inject(object message) => Inject(UnityEngine.JsonUtility.ToJson(message));

        public void Poll()
        {
            while (_incoming.Count > 0) Message?.Invoke(_incoming.Dequeue());
        }
    }
}

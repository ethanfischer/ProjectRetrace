using System;

namespace ProjectRetrace
{
    /// <summary>
    /// The thinnest possible socket: text frames in, text frames out. Everything is
    /// delivered from Poll on the main thread, so consumers never think about threads --
    /// the browser build has none, and the editor build's receive loop must not touch
    /// Unity objects anyway.
    /// </summary>
    public interface INetTransport
    {
        bool IsOpen { get; }

        event Action Opened;
        event Action<string> Closed;
        event Action<string> Message;

        void Connect(string url);
        void Send(string json);
        void Close();
        void Poll();
    }
}

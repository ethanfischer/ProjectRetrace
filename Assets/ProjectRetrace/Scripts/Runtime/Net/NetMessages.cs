using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Every message on the wire, as flat JsonUtility-friendly classes. Parsing is two-pass:
    /// read the header for "type", then the concrete class -- JsonUtility has no
    /// polymorphism. "epoch" is the run number so a message from before a rematch is ignored;
    /// "durable" asks the relay to cache the message for a reconnecting peer.
    /// </summary>
    [Serializable]
    public class MsgHeader
    {
        public string type;
        public int epoch;
    }

    [Serializable]
    public class NetMessage
    {
        public string type;
        public int epoch;
        public bool durable;

        protected NetMessage(string type)
        {
            this.type = type;
        }
    }

    // ---- client <-> relay ----

    [Serializable] public class CreateMsg : NetMessage { public CreateMsg() : base("create") { } }

    [Serializable] public class JoinMsg : NetMessage { public string room; public JoinMsg() : base("join") { } }

    [Serializable] public class ResumeMsg : NetMessage { public string room; public int seat; public string token; public ResumeMsg() : base("resume") { } }

    [Serializable] public class JoinedMsg : NetMessage { public string room; public int seat; public string token; public bool peerPresent; public JoinedMsg() : base("joined") { } }

    [Serializable] public class PeerMsg : NetMessage { public bool present; public PeerMsg() : base("peer") { } }

    [Serializable] public class ErrorMsg : NetMessage { public string reason; public ErrorMsg() : base("error") { } }

    [Serializable] public class SyncedMsg : NetMessage { public SyncedMsg() : base("synced") { } }

    [Serializable] public class PingMsg : NetMessage { public float t; public PingMsg() : base("ping") { } }

    [Serializable] public class PongMsg : NetMessage { public float t; public PongMsg() : base("pong") { } }

    // ---- peer <-> peer ----

    [Serializable]
    public class HelloMsg : NetMessage
    {
        public int seat;
        public string house;
        public int spots;
        public int protocol;
        public HelloMsg() : base("hello") { }
    }

    [Serializable]
    public class MatchStartMsg : NetMessage
    {
        public int seed;
        public int startingPlayer;
        public MatchStartMsg() : base("match-start") { durable = true; }
    }

    [Serializable]
    public class RoundStartMsg : NetMessage
    {
        public int round;
        public int attempt;
        public int lives;
        public int owner;
        public List<PropState> props = new List<PropState>();
        public RoundStartMsg() : base("round-start") { }
    }

    [Serializable]
    public class RouteCompleteMsg : NetMessage
    {
        public int owner;
        public int round;
        public RouteData route;
        public RouteCompleteMsg() : base("route-complete") { durable = true; }
    }

    [Serializable]
    public class RoundResultMsg : NetMessage
    {
        public const string Key = "key";
        public const string Caught = "caught";

        public string kind;
        public int round;
        public int by;
        public int lives;
        public int winner;
        public RoundResultMsg() : base("round-result") { durable = true; }
    }

    [Serializable]
    public class SnapshotMsg : NetMessage
    {
        public float t;
        public PlayerSnap player = new PlayerSnap();
        public List<SentrySnap> sentries = new List<SentrySnap>();
        public List<PropState> props = new List<PropState>();
        public SnapshotMsg() : base("snapshot") { }
    }

    [Serializable] public class RematchMsg : NetMessage { public RematchMsg() : base("rematch") { } }

    // ---- payload pieces ----

    [Serializable]
    public class PropState
    {
        public string id;
        public bool open;
    }

    [Serializable]
    public class PlayerSnap
    {
        public Vector3 p;
        public float yaw;
        public float pitch;
        public bool hiding;
    }

    [Serializable]
    public class SentrySnap
    {
        public int i;
        public Vector3 p;
        public float yaw;
        public int state;
        public float alpha;
    }
}

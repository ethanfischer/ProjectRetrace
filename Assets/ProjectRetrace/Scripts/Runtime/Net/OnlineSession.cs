using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    public enum NetState
    {
        /// <summary>Not online; the lobby is closed.</summary>
        Idle,
        /// <summary>Lobby open, nothing connected yet.</summary>
        Lobby,
        Connecting,
        /// <summary>Seated in a room, waiting for the other seat.</summary>
        InRoom,
        /// <summary>Both seated and the builds match; the host can start.</summary>
        Ready,
        InMatch,
        Disconnected,
        Error
    }

    /// <summary>
    /// One online match from this client's side: the socket, the seat, the handshake, and
    /// the translation between wire messages and director calls. The director never sees
    /// the socket and the socket never sees the game -- everything crosses here.
    ///
    /// A match is two seats: the creator is player 1, the joiner player 2. Rounds
    /// alternate exactly as on the couch; the only difference is that the player whose
    /// round it isn't watches a stream instead of the shared screen.
    /// </summary>
    public class OnlineSession : MonoBehaviour
    {
        private const float PingIntervalSeconds = 5f;
        private const string SavedRoomKey = "ProjectRetrace.Room";
        private const string SavedSeatKey = "ProjectRetrace.Seat";
        private const string SavedTokenKey = "ProjectRetrace.Token";

        public GameDirector director;
        public SpectatorRig spectator;

        private INetTransport _transport;
        private string _pendingRequest;
        private bool _replaying;
        private MatchStartMsg _replayMatch;
        private readonly List<RouteCompleteMsg> _replayRoutes = new List<RouteCompleteMsg>();
        private readonly List<RoundResultMsg> _replayResults = new List<RoundResultMsg>();
        private bool _helloSent;
        private bool _helloMatched;
        private float _nextPingAt;
        private float _nextSnapshotAt;
        private readonly List<PropState> _lastProps = new List<PropState>();
        private readonly List<PropState> _propScratch = new List<PropState>();

        public NetState State { get; private set; }
        public string Room { get; private set; } = string.Empty;
        public int Seat { get; private set; }
        public string Token { get; private set; } = string.Empty;
        public bool IsHost => Seat == 1;
        public bool PeerPresent { get; private set; }
        public float RttMs { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public int Epoch { get; private set; }
        public bool RematchRequested { get; private set; }
        public bool InMatch => State == NetState.InMatch;
        public bool CanResume => PlayerPrefs.HasKey(SavedRoomKey);

        // ---- lobby ----

        public void OpenLobby()
        {
            if (State == NetState.Idle) State = NetState.Lobby;
        }

        public void CreateRoom() => Connect("create");

        public void JoinRoom(string code)
        {
            Room = (code ?? string.Empty).Trim().ToUpperInvariant();
            Connect("join");
        }

        /// <summary>Back into the last room from this browser: the seat token is all the
        /// relay needs, and the durable log it replays is all the game needs.</summary>
        public void Resume()
        {
            Room = PlayerPrefs.GetString(SavedRoomKey, string.Empty);
            Seat = PlayerPrefs.GetInt(SavedSeatKey, 0);
            Token = PlayerPrefs.GetString(SavedTokenKey, string.Empty);
            Connect("resume");
        }

        public void Leave()
        {
            _transport?.Close();
            _transport = null;
            State = NetState.Idle;
            PeerPresent = false;
            _helloSent = _helloMatched = false;
            RematchRequested = false;
        }

        public void UseTransport(INetTransport transport)
        {
            _transport?.Close();
            _transport = transport;
            _transport.Opened += OnOpened;
            _transport.Closed += OnClosed;
            _transport.Message += OnMessage;
        }

        private void Connect(string request)
        {
            if (_transport == null) UseTransport(CreateTransport());
            _pendingRequest = request;
            _helloSent = _helloMatched = false;
            LastError = string.Empty;
            State = NetState.Connecting;
            _transport.Connect(RetraceConfig.Current.relayUrl);
        }

        private INetTransport CreateTransport()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WebGLWebSocketTransport(gameObject.name);
#else
            return new DotNetWebSocketTransport();
#endif
        }

        // ---- jslib receivers (WebGL only; the names are part of the plugin contract) ----

        public void OnWsOpen(string _)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            (_transport as WebGLWebSocketTransport)?.HandleOpen();
#endif
        }

        public void OnWsMessage(string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            (_transport as WebGLWebSocketTransport)?.HandleMessage(json);
#endif
        }

        public void OnWsClose(string reason)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            (_transport as WebGLWebSocketTransport)?.HandleClose(reason);
#endif
        }

        // ---- transport events ----

        private void OnOpened()
        {
            switch (_pendingRequest)
            {
                case "create": Send(new CreateMsg()); break;
                case "join": Send(new JoinMsg { room = Room }); break;
                case "resume": Send(new ResumeMsg { room = Room, seat = Seat, token = Token }); break;
            }

            _pendingRequest = null;
            _nextPingAt = Time.unscaledTime + PingIntervalSeconds;
        }

        private void OnClosed(string reason)
        {
            if (State == NetState.Idle) return;
            PeerPresent = false;
            State = State == NetState.Connecting ? NetState.Error : NetState.Disconnected;
            LastError = reason;
        }

        private void OnMessage(string json)
        {
            var header = JsonUtility.FromJson<MsgHeader>(json);
            if (header == null || string.IsNullOrEmpty(header.type)) return;

            switch (header.type)
            {
                case "joined": OnJoined(JsonUtility.FromJson<JoinedMsg>(json)); return;
                case "synced": OnSynced(); return;
                case "peer": OnPeer(JsonUtility.FromJson<PeerMsg>(json)); return;
                case "error": OnError(JsonUtility.FromJson<ErrorMsg>(json)); return;
                case "pong": RttMs = (Time.unscaledTime - JsonUtility.FromJson<PongMsg>(json).t) * 1000f; return;
                case "hello": OnHello(JsonUtility.FromJson<HelloMsg>(json)); return;
                case "rematch": RematchRequested = true; return;
            }

            if (_replaying)
            {
                BufferReplay(header.type, json);
                return;
            }

            if (header.type == "match-start")
            {
                OnMatchStart(JsonUtility.FromJson<MatchStartMsg>(json));
                return;
            }

            // Anything left is a live game message from the current match only.
            if (State != NetState.InMatch || header.epoch != Epoch) return;
            switch (header.type)
            {
                case "round-start":
                    director.OnRemoteRoundStart(JsonUtility.FromJson<RoundStartMsg>(json));
                    break;
                case "route-complete":
                    director.OnRemoteRouteComplete(JsonUtility.FromJson<RouteCompleteMsg>(json).route.ToRoute());
                    break;
                case "round-result":
                    director.OnRemoteRoundResult(JsonUtility.FromJson<RoundResultMsg>(json));
                    break;
                case "snapshot":
                    if (spectator != null) spectator.OnSnapshot(JsonUtility.FromJson<SnapshotMsg>(json));
                    break;
            }
        }

        private void OnJoined(JoinedMsg message)
        {
            Room = message.room;
            Seat = message.seat;
            Token = message.token;
            PeerPresent = message.peerPresent;
            PlayerPrefs.SetString(SavedRoomKey, Room);
            PlayerPrefs.SetInt(SavedSeatKey, Seat);
            PlayerPrefs.SetString(SavedTokenKey, Token);
            PlayerPrefs.Save();

            // The relay replays the durable log right after seating; it ends with "synced".
            _replaying = true;
            _replayMatch = null;
            _replayRoutes.Clear();
            _replayResults.Clear();
            State = NetState.InRoom;
        }

        private void BufferReplay(string type, string json)
        {
            switch (type)
            {
                case "match-start":
                    _replayMatch = JsonUtility.FromJson<MatchStartMsg>(json);
                    _replayRoutes.Clear();
                    _replayResults.Clear();
                    break;
                case "route-complete": _replayRoutes.Add(JsonUtility.FromJson<RouteCompleteMsg>(json)); break;
                case "round-result": _replayResults.Add(JsonUtility.FromJson<RoundResultMsg>(json)); break;
            }
        }

        private void OnSynced()
        {
            _replaying = false;
            if (_replayMatch != null)
            {
                Epoch = _replayMatch.epoch;
                State = NetState.InMatch;
                var routes = new List<RecordedRoute>();
                foreach (var r in _replayRoutes) routes.Add(r.route.ToRoute());
                director.RestoreFromLog(_replayMatch, routes, _replayResults);
            }

            if (PeerPresent) SendHello();
        }

        private void OnPeer(PeerMsg message)
        {
            PeerPresent = message.present;
            if (message.present)
            {
                _helloSent = false;
                SendHello();
                if (State == NetState.InMatch) director.OnPeerReturned();
            }
            else if (State == NetState.Ready)
            {
                _helloMatched = false;
                State = NetState.InRoom;
            }
        }

        private void OnError(ErrorMsg message)
        {
            LastError = message.reason;
            State = NetState.Error;
        }

        private void SendHello()
        {
            if (_helloSent) return;
            _helloSent = true;
            Send(new HelloMsg { seat = Seat, house = HouseIdentity.Current, spots = HouseIdentity.KeySpotCount, protocol = HouseIdentity.Protocol });
        }

        /// <summary>Two builds that disagree about the house cannot play: the hiding spots
        /// and prop ids would silently point at different furniture.</summary>
        private void OnHello(HelloMsg message)
        {
            var mine = HouseIdentity.Current;
            if (message.protocol != HouseIdentity.Protocol || message.house != mine || message.spots != HouseIdentity.KeySpotCount)
            {
                LastError = $"Builds differ: theirs {message.house} ({message.spots} spots), yours {mine} ({HouseIdentity.KeySpotCount})";
                State = NetState.Error;
                return;
            }

            _helloMatched = true;
            if (State == NetState.InRoom) State = NetState.Ready;
        }

        private void OnMatchStart(MatchStartMsg message)
        {
            Epoch = message.epoch;
            RematchRequested = false;
            State = NetState.InMatch;
            director.StartRunWith(message.seed, message.startingPlayer);
        }

        // ---- outbound ----

        /// <summary>Host only. Bumping the epoch tells the relay to forget the previous
        /// match's log and tells the guest to drop anything still in flight from it.</summary>
        public void StartMatch(int seed, int startingPlayer)
        {
            Epoch++;
            RematchRequested = false;
            State = NetState.InMatch;
            Send(new MatchStartMsg { seed = seed, startingPlayer = startingPlayer });
        }

        public void RequestRematch() => Send(new RematchMsg());

        public void SendRoundStart(int round, int attempt, int lives, int owner)
        {
            var message = new RoundStartMsg { round = round, attempt = attempt, lives = lives, owner = owner };
            InteractableRegistry.SnapshotOpenables(message.props);
            _lastProps.Clear();
            _lastProps.AddRange(message.props);
            Send(message);
        }

        public void SendRouteComplete(RecordedRoute route, int round)
        {
            if (route == null) return;
            Send(new RouteCompleteMsg { owner = route.Owner, round = round, route = RouteData.From(route) });
        }

        public void SendRoundResult(string kind, int round, int by, int lives, int winner)
        {
            Send(new RoundResultMsg { kind = kind, round = round, by = by, lives = lives, winner = winner });
        }

        public void Send(NetMessage message)
        {
            if (_transport == null) return;
            message.epoch = Epoch;
            _transport.Send(JsonUtility.ToJson(message));
        }

        private void Update()
        {
            if (_transport == null) return;
            _transport.Poll();
            if (State == NetState.Idle || State == NetState.Lobby) return;

            if (_transport.IsOpen && Time.unscaledTime >= _nextPingAt)
            {
                _nextPingAt = Time.unscaledTime + PingIntervalSeconds;
                Send(new PingMsg { t = Time.unscaledTime });
            }

            if (State == NetState.InMatch && director != null && director.IsStreamingTurn && Time.unscaledTime >= _nextSnapshotAt)
            {
                _nextSnapshotAt = Time.unscaledTime + 1f / Mathf.Max(1f, RetraceConfig.Current.snapshotHz);
                Send(BuildSnapshot());
            }
        }

        /// <summary>The turn owner's world, as much of it as a renderer needs. Props are
        /// diffed against the last frame so a house full of drawers costs nothing at rest.</summary>
        private SnapshotMsg BuildSnapshot()
        {
            var snapshot = new SnapshotMsg { t = Time.unscaledTime };
            var player = director.player;
            if (player != null)
            {
                snapshot.player.p = player.transform.position;
                snapshot.player.yaw = player.transform.eulerAngles.y;
                snapshot.player.pitch = player.cameraPivot != null ? NormalisePitch(player.cameraPivot.localEulerAngles.x) : 0f;
                snapshot.player.hiding = director.interactor != null && director.interactor.Hiding != null;
            }

            var sentries = director.Sentries;
            for (var i = 0; i < sentries.Count; i++)
            {
                var sentry = sentries[i];
                if (sentry == null || sentry.State == SentryState.Inactive) continue;
                snapshot.sentries.Add(new SentrySnap
                {
                    i = i,
                    p = sentry.transform.position,
                    yaw = sentry.transform.eulerAngles.y,
                    state = (int)sentry.State,
                    alpha = sentry.Alpha
                });
            }

            InteractableRegistry.SnapshotOpenables(_propScratch);
            for (var i = 0; i < _propScratch.Count; i++)
            {
                if (!SameAsLast(_propScratch[i])) snapshot.props.Add(_propScratch[i]);
            }

            _lastProps.Clear();
            _lastProps.AddRange(_propScratch);
            return snapshot;
        }

        private bool SameAsLast(PropState state)
        {
            for (var i = 0; i < _lastProps.Count; i++)
            {
                if (_lastProps[i].id == state.id) return _lastProps[i].open == state.open;
            }

            return false;
        }

        private static float NormalisePitch(float degrees) => degrees > 180f ? degrees - 360f : degrees;
    }
}

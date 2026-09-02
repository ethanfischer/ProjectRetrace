# Online play

Couch 2P proved the structure: rounds alternate, only one player ever acts, and the
"opponent" is always a recording. Online keeps that shape, which is why it never needs
real multiplayer netcode. Two layers, both built:

## Layer 1: route exchange

Between rounds the clients exchange the run seed (every hiding spot derives from it --
`GameDirector.RoundSeed(seed, round)`), and each completed route as `RouteData` (crumbs,
dwells, owner -- a few tens of KB of plain data). The receiving client replays it with the
exact same `PatrolSentry` code that runs locally; nothing in the game cares where a route
came from. Rounds hand over on a `round-result` and the incoming player still gets the
"you're up -- press Space" gate, so finishing a round never means the opponent is hunted
the instant they look back at the screen.

## Layer 2: live spectating

Watching your opponent's round is one-way streaming, not netcode:

- **The turn owner is authoritative for everything.** Their client runs the round exactly
  as offline and broadcasts `snapshot`s at `snapshotHz` (12 by default): player position +
  look, each sentry's position/yaw/state/fade alpha, and a diff of every openable's state.
- **The spectator simulates nothing.** `SpectatorRig` drives the player rig as a camera
  mount and the ghost pool in `PatrolSentry.BeginPuppet` mode (agent off, eyes off) from a
  `SnapshotBuffer` read `spectatorDelaySeconds` behind the sender's clock. Do NOT "run the
  round locally and only stream the player": NavMeshAgent movement is not bit-deterministic
  across machines and the spectator would eventually show a catch that didn't happen.
- **Camera**: chase cam behind a conjured avatar first (watching your own ghost stalk your
  friend is the point); free-fly on `spectatorCameraKey` (C).

## Transport

`relay/` is a tiny Node WebSocket room server (see [relay/README.md](relay/README.md)).
It seats two sockets per 4-letter room code, forwards everything verbatim, and keeps the
messages flagged `durable` (`match-start`, `route-complete`, `round-result`) so a peer that
drops can `resume` with its seat token and rebuild the match from the log
(`GameDirector.RestoreFromLog`). A turn owner who drops restarts their round; the recording
lived only in the memory that went away.

Unity side: `OnlineSession` owns the socket, the seat, the handshake and the translation
between wire messages and director calls. `INetTransport` has three backends --
`WebGLWebSocketTransport` over `Plugins/WebGL/RetraceWebSocket.jslib` (browser),
`DotNetWebSocketTransport` (editor/standalone) and `LoopbackTransport` (tests and evals).
An empty `relayUrl` in `retrace-config.json` means the page's own host on port 8787 (or
localhost in the editor); set it explicitly to point elsewhere, and an https page needs `wss://`.

## Protocol

JSON, one `type` field, parsed header-first (`MsgHeader`) then as the concrete class in
`NetMessages.cs`. Every message carries `epoch` (the run number, bumped on rematch so
stale messages are dropped) and `durable`.

| From | Message | Purpose |
|---|---|---|
| client → relay | `create`, `join {room}`, `resume {room, seat, token}` | seating |
| relay → client | `joined`, `peer {present}`, `error {reason}`, replayed log, `synced` | seating |
| both | `hello {seat, house, spots, protocol}` | refuse mismatched builds |
| host | `match-start {seed, startingPlayer}` | both clients start the same run |
| owner | `round-start {round, attempt, lives, owner, props}` | spectator rebuilds the house |
| owner | `route-complete {owner, round, route}` | the route joins the opponent's ghost pool |
| owner | `round-result {kind, round, by, lives, winner}` | hand over, retry, or finish |
| owner | `snapshot {t, player, sentries, props}` | the spectator's whole world |
| guest | `rematch` | asks the host to press R |

## Same build, same house

The house is generated in the editor and baked into the scene, so two clients cannot
rebuild it from a seed. `hello` compares `HouseIdentity` (the `TestHouse (seed N)` root
name plus `Application.version`) and the key-spot count, and refuses to pair mismatches.
Everything else that has to agree across machines does so by construction: props are
named by `HierarchyPath` (name plus sibling index at every level), `KeySpawner` orders
spots by that path rather than by instance id, and every seed derives from the run seed.

## Testing without a second machine

The editor talks to a local relay directly. For a second peer, either serve a WebGL build
(`npx serve <build folder>`) in a browser, or script one: any process that speaks the
table above works. `LoopbackTransport` drives `OnlineSession` with no socket at all --
see `OnlineContractTests`.

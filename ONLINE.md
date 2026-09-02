# Online play — design notes (not built yet)

Couch 2P already proves the structure: rounds alternate, only one player ever acts, and
the "opponent" is always a recording. Online keeps that shape, which is why it never
needs real multiplayer netcode. Two layers, buildable independently:

## Layer 1: route exchange (async play)

The minimum viable online mode is play-by-mail. Between rounds, clients exchange:

- the run seed (every hiding spot derives from it — see `GameDirector.RoundSeed`),
- the house seed (the generated house must match),
- each completed `RecordedRoute` (crumbs, dwells, owner).

A route is a few hundred positions plus a handful of dwell points — a few KB of plain
data. The receiving client replays it with the exact same `PatrolSentry` code that runs
locally today; nothing in the game cares where a route came from. Turns can be taken
whenever (Wordle-with-ghosts), and the transition screen becomes "waiting for Player 2…".

## Layer 2: live spectating

Watching your opponent's round live is one-way streaming, not netcode:

- **The turn owner is authoritative for everything.** Their client runs the round exactly
  as today and broadcasts snapshots at ~10–15Hz: player transform + look yaw, each
  sentry's transform/state/fade alpha, plus discrete events (drawer opened, key taken,
  spotted, caught).
- **The spectator simulates nothing.** Pure renderer, interpolating streamed transforms.
  Do NOT "run the round locally and only stream the player": NavMeshAgent movement is not
  bit-deterministic across machines, the spectator's sentries would drift, and the stream
  would eventually show a catch that didn't happen. Dumb replication cannot lie.
- **Spectator camera**: chase cam reconstructed from the streamed transform + look yaw
  first (watching your own ghost stalk your friend is the point); free-fly second.
- No input sync, no prediction, no authority conflicts. Latency only delays the show.

Spectating leaks no hidden information: seeing the route being laid against you is
exactly the couch experience, where you'd have watched the shared screen anyway.

## Transport

The build target is WebGL, so it's WebSockets either way:

- **Unity Netcode for GameObjects + WebSocket transport + Unity Relay** — rooms, joining,
  and reconnects handled for you; a heavyweight dependency for streaming ~15 transforms
  in one direction.
- **A hand-rolled WebSocket relay** (tiny Node/Go process, flat byte layout or JSON) —
  closer to this project's build-it-in-code ethos, but lobby/reconnect handling is yours.

Decide when the work starts; nothing in the game code favours either.

## What to keep true in the meantime

These are the contracts online play will lean on — cheap to honour now, painful to
retrofit:

1. `RecordedRoute` / `Breadcrumb` / `DwellPoint` stay plain data (positions, yaw,
   indices), trivially serialisable. No component references, no scene handles.
2. Everything random derives from the run seed (`RoundSeed`) or the house seed. A remote
   client must reproduce every hiding spot and the entire house from two ints.
3. Sentry behaviour stays a pure function of (route, dwells, settings). Anything a sentry
   needs beyond that would have to be streamed.

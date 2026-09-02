# Retrace relay

The WebSocket room server for online play. One Node process, one file, no database.

```bash
cd relay
npm install
npm start          # ws://localhost:8787, or PORT=9000 npm start
```

Point the game at it with `relayUrl` in `retrace-config.json` (default `ws://localhost:8787`).

## What it does

- `create` opens a room and seats you as player 1; `join {room}` seats player 2.
- Every other message is forwarded verbatim to the other seat. Messages flagged
  `durable` are kept per room and replayed, in order, to anyone who `resume`s with the
  seat's token, followed by `synced`. A higher `epoch` on a durable message (a rematch)
  clears the log first.
- Rooms with both seats empty for 30 minutes are dropped. The log lives in memory only:
  restarting the relay forgets every match in progress.

## Deploying

The game's WebGL build is served over https (itch.io), and browsers refuse `ws://` from an
https page, so a public relay must be reachable over `wss://`. Any host that terminates TLS
in front of a Node process works (Fly.io, Railway, Render), or run Caddy in front of it:

```
relay.example.com {
    reverse_proxy localhost:8787
}
```

Then set `relayUrl` to `wss://relay.example.com`.

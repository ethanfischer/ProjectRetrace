# ProjectRetrace

A first-person stealth game made in Unity. First you search a house for your keys. Then
the house fills up, one round at a time, with ghosts of every walk you've taken — and you
keep hunting for the keys until they catch you.

**Round 1 — Search.** Hunt the house. While moving, the game silently records the route
you take, including everywhere you stop to look around. Every round records.

**Every round after — Hunt.** The house resets, the keys hide somewhere **new**, and one
more sentry joins the patrol: each walks one of your past routes, in your direction,
pausing to look around wherever you did. Round N has N ghosts. There is no winning — only
how deep you get before your tries run out. The door to the stairs stays locked until
round 4, so the early rounds play out on the ground floor and the house doubles once
you've earned it.

The twist: you know every patrol perfectly, because they're all your own routes. Every
route you walk is also the trap you set for your future self.

**Multiplayer** (from the start menu): two players, one keyboard, rounds alternating.
Only your opponent's routes haunt you: every route you walk is a trap for *them*, and
every one they walk is a trap for you, so the ghost pool you face grows by one every
other round. Run out of tries and the other player wins. The rematch rotates who gets
the threat-free search round.

The sentries project their vision cones on the floor — the cone is the true sightline,
cut off by walls, so trust it. Their footsteps are 3D: faint means far. **`** (backquote) toggles a
debug view of the route you're currently recording.

See [SETUP.md](SETUP.md) to get running.

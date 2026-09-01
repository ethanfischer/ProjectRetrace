# ProjectRetrace

A first-person stealth game made in Unity. First you search a house for your keys. Then
the house fills up, one round at a time, with ghosts of every walk you've taken — and you
keep hunting for the keys until they catch you.

**Round 1 — Search.** Hunt the house. While moving, the game silently records the route
you take, including everywhere you stop to look around. Every round records.

**Every round after — Hunt.** The house resets, the keys hide somewhere **new**, and one
more sentry joins the patrol: each walks one of your past routes, in your direction,
pausing to look around wherever you did. Round N has N ghosts. There is no winning — only
how deep you get before your tries run out.

The twist: you know every patrol perfectly, because they're all your own routes. Every
route you walk is also the trap you set for your future self.

**Local multiplayer** (2–4 players, from the start menu): one keyboard, rounds rotating
through the players — everyone's routes haunt whoever's up, each player's ghosts in their
own colour. Run out of tries and you're eliminated, but your ghosts keep fighting; last
one standing wins. The rematch rotates who gets the threat-free search round.

The sentries project their vision cones on the floor — the cone is the true sightline,
cut off by walls, so trust it. Their footsteps are 3D: faint means far. **F3** toggles a
debug view of the route you're currently recording.

See [SETUP.md](SETUP.md) to get running.

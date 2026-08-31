# ProjectRetrace

A first-person stealth game made in Unity. First you search a house for your keys. Then
someone else walks in and retraces your every step — and you have to steal the keys back
without being seen.

**Round 1 — Search.** Hunt the house. While moving, the game silently records the route
you take, including everywhere you stop to look around. It keeps recording in round 2.

**Round 2 — Steal.** The house resets to exactly how it started, but the keys are hidden
somewhere **new** — and a sentry now patrols your round-1 route, pausing to look around
wherever you did. Find the keys and grab them without entering its vision.

**Round 3 — Steal again.** Your round-2 sneak left a trail too. The keys move once more,
and a second sentry joins the first — one walking your search route, one walking the very
path you just snuck. Three catches in a round and the run is over.

The twist: you know the patrol perfectly, because it's your own route. Search the whole
house in phase 1 and the sentry is spread thin over a long loop; beeline to the keys and
it camps a short, tight circuit. How you search *is* the difficulty you inherit.

The sentry projects its vision cone on the floor — the cone is its true sightline, cut
off by walls, so trust it. **F3** toggles a debug view of the recorded trails.

See [SETUP.md](SETUP.md) to get running.

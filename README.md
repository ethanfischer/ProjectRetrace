# ProjectRetrace

A first-person game made in Unity. First you search a house for your keys. Then a second
phase where you start over and try to retrace your steps as closely as possible.

**Phase 1 — Search.** Hunt the house. Every 1.5 metres of travel, the game silently drops a
breadcrumb behind you.

**Phase 2 — Retrace.** The house resets to exactly how it started, the keys go back to the
same hiding spot, and you walk it again — blind. You collect those breadcrumbs by passing
near them.

**Score.** `coverage × efficiency`: the fraction of marks you hit, times how much extra
ground you covered getting there. Shortcut the route and coverage drops; wander the whole
house hoovering up marks and efficiency drops. You need both.

Press **F3** to see the trail — green marks were hit, red were missed.

See [SETUP.md](SETUP.md) to get running.

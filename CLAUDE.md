## Project

Unity 6 (`6000.3.17f1`) URP first-person stealth game. Three rounds: search a house for keys
while the game silently records your route; steal them back — re-hidden somewhere new —
while a sentry NPC retraces your search route; then steal them once more against two
sentries, the second retracing the sneak route you just took. Catches on sight, three lives
per stealth round, no score. See [README.md](README.md) for the pitch and
[SETUP.md](SETUP.md) for scene setup, controls, and tuning guidance.

## Commands

The Unity editor is the primary tool. The `.sln`/`.csproj` files at the root are Unity-generated
and gitignored. Never hand-edit them; edit the `.asmdef` files instead.

### Unity CLI (preferred when the editor is open)

The official Unity CLI (`unity`, ~/.unity/bin) talks to the *running* editor via the
`com.unity.pipeline` package (already in the manifest). Prefer it over batchmode whenever the
editor is open — batchmode can't run then, and the CLI can do far more:

```bash
unity status --no-banner
```

lists connected editors (state `ready` means good to go). The workhorses:

- `unity cmd eval --no-banner '<C# code>'` — run arbitrary C# in the editor (Roslyn). Works in
  play mode too; `return` a string for output. This is the debugging tool of choice: it found
  the interaction-ray bug by reading live component state in play mode.
- `unity cmd editor_play` / `editor_pause` / `editor_stop` — drive play mode.
- `unity cmd recompile` then poll `unity cmd recompile_status` until `{"status":"completed"}` —
  compile without focusing the editor.
- `unity cmd console` — read editor console output; `clear_console` resets it.
- `unity cmd capture_game_view` / `capture_scene_view` — screenshot to PNG.
- `unity list --no-banner` — full catalog of available commands.

Verify play-mode behavior yourself with eval (teleport the player, read state, assert) rather
than asking a human to playtest. The console warning about "-automated" mode is harmless.

### Batchmode (editor closed)

Compile-check headlessly (swap the version if Hub has a different install):

```bash
/Applications/Unity/Hub/Editor/6000.3.17f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -nographics -projectPath "$PWD" -logFile -
```

Run tests headlessly:

```bash
/Applications/Unity/Hub/Editor/6000.3.17f1/Unity.app/Contents/MacOS/Unity -batchmode -runTests -testPlatform EditMode -projectPath "$PWD" -testResults ./TestResults.xml -logFile -
```

`com.unity.test-framework` is installed but no test assembly exists yet. Adding one means a new
`.asmdef` under `Assets/ProjectRetrace/Scripts/Tests/` referencing `ProjectRetrace.Runtime` plus
the nunit/test-framework references. `-testFilter` narrows a run to a single test.

Batchmode fails if the editor already has the project open — use the Unity CLI instead in that
case.

## Architecture

Two assemblies, both under `Assets/ProjectRetrace/Scripts/`:
`ProjectRetrace.Runtime` (namespace `ProjectRetrace`) and `ProjectRetrace.Editor`
(namespace `ProjectRetrace.EditorTools`, editor-only, references Runtime).

`GameDirector` owns the run as a state machine over `GamePhase` (Search, Transition, Stealth,
Results), exposing a static `Instance`. The Transition step is the load-bearing one: it calls
`InteractableRegistry.RestoreAll()`, then re-places the keys with a seed *derived* from the run
seed (excluding the phase-1 spot — placement must come after RestoreAll, which snaps the keys
back to their captured spot), teleports the player to the identical spawn transform, and starts
the sentry. Same house, new hiding spot: that pairing is the game. `StartRun` restores *before*
capturing on purpose, so a mid-run restart doesn't bake open drawers in as the new initial
state. Getting spotted routes through `OnPlayerSpotted` (freezes input — the attempt is decided)
and `OnPlayerCaught`; both are sentry-driven. A catch spends one of `stealthLives` (3): with
lives left it re-runs the stealth setup with the *same* derived seed — same phase-2 hiding spot,
so knowledge survives a retry — and only the last life ends the run.

`BreadcrumbTrail` samples by distance travelled, not by time, and only on the XZ plane, so the
patrol route captures geometry, not pacing, and jump-spam can't distort it. Standing still drops
no crumbs, so the trail also records `DwellPoint`s (position + facing yaw) wherever the player
lingers — one per stop no matter how long, which is deliberate anti-exploit design (see below).

`PatrolSentry` (`Runtime/AI/`) is the whole NPC on one component: NavMeshAgent patrol over a
recorded route in the player's direction (at the end it fades out, teleports back to the
start, and fades in — frozen and blind during both fades), a fixed-length look-around at each
dwell point, cone + line-of-sight detection (head and chest samples, the
RaycastAll-skip-own-root idiom from `PlayerInteractor` — no tags or layers), and a time-capped
chase that only sells the catch. It deliberately never replays the player's *timing*: pace and
pause lengths are its own, or players would camp in phase 1 to pad the patrol and soften
phase 2. `NavMeshRuntimeBaker` bakes from live colliders in `Awake` (agent radius 0.3 to fit
the generated 1.1m doorways) — no baked asset to go stale when the test house regenerates.

`InteractableRegistry` is a static list that self-populates from `InteractableBase.OnEnable`, so
the director resets the whole house without holding scene references to individual props. It
clears itself via `[RuntimeInitializeOnLoadMethod]` because statics survive play-mode entry when
Domain Reload is off.

`RetraceSettings` is a `ScriptableObject`. Every consumer reads it through an
`EffectiveSettings` property that falls back to `RetraceSettings.CreateDefault()`, so a missing
asset never blocks a playtest. Follow that pattern rather than dereferencing `settings` directly.

`SceneSetupMenu` (menu: ProjectRetrace > Setup Scene Systems) builds and wires the entire rig
into the open scene, including the sentry (inactive until the stealth phase) and the navmesh
baker. When you add a system that needs scene wiring, wire it there too, or it
silently won't exist in anyone else's scene. Related editor menus: ProjectRetrace > Furniture
builds searchable props (hiding spots auto-register via `KeySpotMarker`), and
ProjectRetrace > Generate Test House builds a seeded two-story house with furniture scattered.

## Conventions

- Input is the Input System package (`UnityEngine.InputSystem`), polled directly via
  `Keyboard.current` / `Mouse.current`. There is no `.inputactions` asset and no legacy
  `Input.*`. Always null-check `.current`. Keybinds are `UnityEngine.InputSystem.Key` fields
  on settings.
- UI is IMGUI (`OnGUI`) on purpose: no prefabs to wire, nothing to merge-conflict over.
- Comments in this codebase justify *why* a non-obvious choice was made (distance-based
  spacing, XZ-only accumulation, restore-before-capture). Match that bar. Explain decisions,
  not mechanics.
- `.meta` files are part of the source. Commit them with every add, move, or rename, or
  everyone else's references break.

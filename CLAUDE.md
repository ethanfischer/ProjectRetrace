## Project

Unity 6 (`6000.3.17f1`) URP first-person stealth game, endless: search a house for keys
while the game silently records your route, then keep hunting them down — re-hidden each
round — without being caught by an ever-growing pool of sentry NPCs, each retracing one of
your own past routes.
Round N has N ghosts; the run ends when a round's lives run out. Couch 2P mode alternates
rounds between two players on one keyboard, each haunted only by the other's routes (round
4 is P2 against P1's two ghosts, round 5 is P1 against P2's two), first to run out of
tries loses. See [README.md](README.md) for the pitch and [SETUP.md](SETUP.md) for scene setup,
controls, and tuning guidance.

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

The EditMode test assembly is `ProjectRetrace.Tests` under `Assets/ProjectRetrace/Scripts/Tests/`.
`-testFilter` narrows a run to a single test; with the editor open use `unity cmd run_tests`.

Batchmode fails if the editor already has the project open — use the Unity CLI instead in that
case.

## Architecture

Three assemblies, all under `Assets/ProjectRetrace/Scripts/`:
`ProjectRetrace.Runtime` (namespace `ProjectRetrace`), `ProjectRetrace.Editor`
(namespace `ProjectRetrace.EditorTools`, editor-only, references Runtime) and
`ProjectRetrace.Tests` (EditMode NUnit tests).

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
patrol route captures geometry, not pacing, and jump-spam can't distort it. The trail also
records a `DwellPoint` (position + facing yaw) wherever the player *uses* something, fed by
`PlayerInteractor.Interacted`; standing still records nothing, and repeat uses within
`dwellRadius` collapse into one stop, which is deliberate anti-exploit design (see below).

`PatrolSentry` (`Runtime/AI/`) is the whole NPC on one component: NavMeshAgent patrol over a
recorded route in the player's direction (at the end it fades out, teleports back to the
start, and fades in — frozen and blind during both fades), a fixed-length look-around at each
dwell point, cone + line-of-sight detection (head and chest samples, the
RaycastAll-skip-own-root idiom from `PlayerInteractor` — no tags or layers), and a time-capped
chase that only sells the catch. It deliberately never replays the player's *timing*: pace and
pause lengths are its own, or players would camp in phase 1 to pad the patrol and soften
phase 2. `NavMeshRuntimeBaker` bakes from live colliders in `Awake` (agent radius 0.3 to fit
the generated 1.1m doorways) — no baked asset to go stale when the test house regenerates.
Every `DoorInteractable` is excluded from that bake: ghosts never operate doors, so they
walk through them rather than being stranded by one that restored closed.

`HidingSpot` sits on a cupboard's root beside its `DoorInteractable` and answers the hide
key (`hideKey`, H), never Use: with the door open, H climbs in and shuts it; while hidden
Use is dead and H is "Leave". Keeping the two on separate keys means E always operates
the door wherever the player aims at the cupboard. Hiding is only as safe as the route
that got you there: a `DwellPoint` carries the
`Prop` that was used, and a ghost pausing at one calls `HidingSpot.OpenedBy`, which opens
the door and hauls out (and spots) anyone inside. Ghosts never hide themselves. With
`sentriesOpenFurniture` on, a ghost also re-opens whatever `IOpenable` the player used at
each stop (the default); off, furniture only opens when a hider is found.
ProjectRetrace > Furniture > Add Hiding Spots To Cupboards retrofits an older scene.

`DoorInteractable` can be round-locked (`unlocksAtRound`, a displayed round number, read
against `GameDirector.Instance.StealthRound`) and carries a `sealedArea`; `KeySpawner`
skips any hiding spot inside a locked door's sealed volume. The generated house uses this
to keep the upper floor shut until round 4.

Online (`Runtime/Net/`, design in [ONLINE.md](ONLINE.md)) is couch mode split across two
machines. `OnlineSession` owns the socket and translates wire messages into director
calls; the director's only new question per beat is `IsLocalTurn`. The turn owner runs
the round unchanged and streams snapshots; the other client sits in `GamePhase.Spectate`
where `SpectatorRig` puppets the player rig and the ghost pool from the stream and
simulates nothing. Routes cross the wire as `RouteData`, props are named by
`HierarchyPath` ids, and `hello` refuses a peer whose `HouseIdentity` differs. The relay
is `relay/server.js`, dumb by design. Test against the editor with `LoopbackTransport` or
any scripted peer; `OnlineContractTests` covers the contracts.

`LevelImportMenu` (ProjectRetrace > Level) is how the art team's scene becomes the playable
house. `HomeInterior_FirstFloor.unity` stays pure art; Import deletes every `TestHouse*`
root (plus the dev grid and the origin point light), moves her scene's roots under a new
`TestHouse (HomeInterior_FirstFloor)` root, and runs Prepare on it, so re-importing after
her next PR is one click. Prepare is idempotent: it adds MeshColliders to the pack's raw
FBX instances (its prefabs have them, its model instances don't), flips the FBX importers
to Read/Write (the runtime bake reads mesh data, and the editor hides that it would fail in
a build), swaps the static cabinets the art scene uses for their interactive twins from the pack
(a table in `LevelImportMenu`; a taller twin lifts whatever stood on the original), and
wires every `InteractiveFurniture_*` prefab by geometry alone: a part whose
pivot sits on its edge is a door (hinge Up, swing sign from which side), one pivoted in
the middle is a drawer, tall deep props get a `HidingSpot`, and each part gets a `KeySpot`.
Hand-placed additions (a ceiling, extra props) go under the `TestHouse (Additions)` root,
which re-imports never touch and the navmesh still bakes; anything found inside the
imported copy that the art scene lacks is moved there rather than deleted, and the spawn
point is only placed on the very first import.
`NavMeshRuntimeBaker` sizes its bake volume from the house's own colliders, so the imported
level (built 20 m west of the origin) is fully covered.

`InteractableRegistry` is a static list that self-populates from `InteractableBase.OnEnable`, so
the director resets the whole house without holding scene references to individual props. It
clears itself via `[RuntimeInitializeOnLoadMethod]` because statics survive play-mode entry when
Domain Reload is off.

`RetraceConfig` is the single home for every tuning value: a plain `[Serializable]` class
written to and read from `retrace-config.json` in `Application.persistentDataPath`, so
players can edit it. Consumers read `RetraceConfig.Current` at the point of use; there are
no inspector copies of tuning numbers. When you add a tunable, add a field with its default
there rather than a `[SerializeField]` or a `const` -- missing keys in an existing file fall
back to the field default, so adding fields never breaks old configs. `GameDirector.StartRun`
reloads the file. `ConfigMenu` (Tab) edits it in-game by reflecting over the config's
public fields, so new tunables appear there without UI work. Keybind defaults avoid F-keys,
which a browser build cannot intercept.

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

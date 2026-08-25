# The Haunt

A 2D life sim game (Stardew Valley-like) built with Godot 4.7 (.NET build) and C#.
Premise: a small New England town, hidden from every map, where buying property binds
you to the town under a malevolent force — cozy town sim (farming, mining, fishing)
layered under a supernatural endgame. `docs/design.md` holds the canon and the roadmap;
check with the user before inventing lore (names and specialties are deliberately undecided).

The architecture contracts are `docs/foundation-spec.md` (clock/save/state base),
`docs/phase2-spec.md` (items/inventory, farming, stamina, economy), and
`docs/phase3-spec.md` (story flags, maps/travel, dialogue, NPCs, scripted intro). Read
them before touching Core/Systems code — public signatures and semantics are specified there.

## Toolchain

- Godot 4.7.2 .NET build: `godot-mono` (installed via Homebrew, app at /Applications/Godot_mono.app)
- .NET SDK 9 (project targets net8.0)

## Commands

- Build (fast correctness check — run after every C# change): `dotnet build`
- Full test suite (headless, 63 tests, exit code 0/1): `godot-mono --headless res://scenes/tests/TestRunner.tscn`
- Re-import after adding/changing assets or scenes: `godot-mono --headless --import`
- Run the game: `godot-mono --path .`
- Screenshot for visual verification (opens a window briefly, saves PNG, quits): `godot-mono --path . -- --screenshot /path/out.png`
  (dev flags: `--start-map <id>` boots into a map; `--screenshot-frames <n>` delays the capture, e.g. past a beat's staging timer)

## Structure

- `src/Core/` — PURE C# (no `using Godot`, test-enforced): GameTime/calendar, ClockModel, GameData + save DTOs, migrations, item/crop defs (code registries), InventoryData, FarmActions, OvernightSim; story: StoryKeys/IntroRules/MapIds, DialogueDef(s)/DialogueSession/DialogueSelector, NpcDef(s)/NpcSchedules
- `src/Systems/` — the four autoloads, in registration order: GameState, Clock, SaveService, WorldSim (the single gameplay-mutation bus — all model writes flow through it, incl. story flags, travel requests, and the dialogue session; UI subscribes to its events)
- `src/World/` — MapRoot base (owns NPC views), MapRegistry, TestMap/TownMap/TownHallMap (programmatic), IInteractable, Bed, Sign, ShippingBin, MapExit, Door, NpcView, PlaceholderSprites
- `src/Player/` — PlayerController (movement, tool targeting, hotbar input), InteractionProbe (focus consults CanInteract; guards freed nodes)
- `src/Story/` — StoryDirector (plain Node child of Main, NOT an autoload; runs the scripted beats)
- `src/UI/` — Hud, InteractionPrompt, HotbarUi, StaminaBar, DialogueUi, PauseMenu, ScreenFade (each builds its own controls in _Ready)
- `src/Tests/` — headless [SimTest] suite + TestRunner; scenes/tests/TestRunner.tscn
- `src/Main.cs` + `scenes/Main.tscn` — composition root: boot, map loading, sleep + travel flows
- `assets/`, `data/` — art/audio and .tres game data (empty so far; placeholder art is procedural)

## Standing architecture rules (from the design review — violations are bugs)

- Save state lives in the central `GameData` model; scenes are views rebuilt from it.
  Never store durable state in nodes (the only IPersistentSystem is the player).
- C# events: every `+=` in a node has a matching `-=` in `_ExitTree` (C# events don't
  auto-disconnect on free; Godot signals do).
- `MinuteTicked` is display-only (HUD). Gameplay/sim code subscribes to `TenMinuteTicked`.
- Entities (crops, NPCs) never subscribe to time events — systems do, iterating model data.
- Gate behavior on `GameState.ClockRuns` / `PlayerHasControl`, never by comparing the Phase enum.
- `GetTree().Paused` is used exclusively for the Paused phase (GameState.TransitionTo owns it).
- Time-dependent saved state stores day-indexes (`LastWateredDay`), never booleans.
- Days advance only via `Clock.AdvanceToDayStart()` (the clock clamps at 1:59 AM); the sleep
  flow in Main owns fade → advance → autosave. OvernightSim mutates the model on DayEnded
  (payload day); WorldSim repaints maps and fires UI events on DayStarted.
- Unknown item/crop ids from save files are ALWAYS preserved, never destroyed (renders as
  '?' placeholder). An id rename ships as a save migration.
- Migrations are frozen JSON literals, only-if-absent, and never call live code
  (ItemDefs/StarterKit/NewGame). A drift guard failing (Save_MigratedKitMatchesNewGame,
  Save_MigratedStoryMatchesNewGame) means: make a conscious decision — never edit a
  frozen migration.
- Story flags are monotone day-stamped entries in GameData.StoryFlags (no unset; absence
  = false; unknown keys preserved). All flag writes go through WorldSim.SetStoryFlag.
- Story beats start ONLY via StoryDirector's CallDeferred check — never synchronously
  from StateChanged (nested TransitionTo) or TenMinuteTicked (Accumulate keeps ticking
  after a mid-loop phase change).
- Any Main-booting test that plants and sleeps must pre-stamp the intro completion flags
  (crew_arrival_done, meeting_done) or drive the dialogue — otherwise the crew beat
  fires on the morning after and WaitUntil(Playing) hangs.

## Conventions

- C#: file-scoped namespaces, nullable enabled, one class per file, class name matches file
  name (Godot requires this for node scripts). Namespaces mirror folders (`TheHaunt.Core` etc).
- Facing encoding: 0=down, 1=left, 2=right, 3=up. Tiles are 16px; tile (x,y) center = (x*16+8, y*16+8).
- Physics layers: 1 = world/blocking, 2 = interactable areas.
- Scene files: keep hand-authored .tscn minimal (node skeleton + scripts); scripts build their
  visual children in code until real art lands. Real maps will be editor-authored later.
- Code-built Controls: use `SetAnchorsAndOffsetsPreset(...)` to lay out, never `SetAnchorsPreset(...)`
  — the latter keeps the control's current rect (zero for a fresh Control) by compensating offsets,
  which silently produces invisible zero-size UI.
- Pixel-art settings: nearest filtering, 640x360 viewport in a 1280x720 window. Revisit with art direction.
- Do not commit `.godot/` (generated) or `export_presets.cfg`.

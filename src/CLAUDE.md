# src/ — cross-cutting code rules

The contracts live WITH the code: each directory's CLAUDE.md carries its layer's
rules, the tests pin the semantics, and story content is `src/Content` (its CLAUDE.md
is the writing contract). The dated phase specs that once planned these layers were
deleted on 2026-09-01 (Kevin's D12 — a stale plan reads as authority; git history
keeps them). The art contract is `docs/designs/` — see `docs/designs/CLAUDE.md`.

`src/Main.cs` + `scenes/Main.tscn` are the composition root: boot, map loading, and the
sleep + travel flows. Each subdirectory carries its own CLAUDE.md with its local rules.

## Standing architecture rules (from the design review — violations are bugs)

- Save state lives in the central `GameData` model; scenes are views rebuilt from it.
  Never store durable state in nodes (the only IPersistentSystem is the player).
- C# events: every `+=` in a node has a matching `-=` in `_ExitTree` (C# events don't
  auto-disconnect on free; Godot signals do).
- `MinuteTicked` is display-only (HUD). Gameplay/sim code subscribes to `TenMinuteTicked`
  (or `HourTicked` for hour-granular sim — WorldSim's garage customer roll is the one
  subscriber; entities still never subscribe to time).
- Entities (crops, NPCs) never subscribe to time events — systems do, iterating model data.
- Gate behavior on `GameState.ClockRuns` / `PlayerHasControl` (or `CanStartDialogue`:
  Playing or Cutscene), never by comparing the Phase enum.
- `GetTree().Paused` is used exclusively for the Paused phase (GameState.TransitionTo
  owns it).
- Days advance only via `Clock.AdvanceToDayStart()` (the clock clamps at 1:59 AM); the
  sleep flow in Main owns fade → advance → autosave, and awaits the overnight report
  while the phase is still Sleeping. Between advance and autosave it applies the one
  scripted wake relocation (`IntroRules.WakesAtTownHall` — src/Story/CLAUDE.md).
  Main's travel flow auto-parks the scooter at the door — riding never goes indoors.
- `Engine.IsEditorHint()` and `[Tool]` appear nowhere in src (test-enforced —
  SourceRulesTests): Tiled is the map editor, and no game code runs in the Godot editor.

## Conventions

- C#: file-scoped namespaces, nullable enabled, one class per file, class name matches
  file name (Godot requires this for node scripts). Namespaces mirror folders
  (`TheHaunt.Core` etc).
- Facing encoding: 0=down, 1=left, 2=right, 3=up. Tiles are 16px; tile (x,y) center =
  (x*16+8, y*16+8). Physics layers: 1 = world/blocking, 2 = interactable areas.
- Characters are 16x32 with feet on the bottom row (one tile of floor, one tile of
  overhang). Right is a horizontal flip of left; the sheet holds down/left/up only.
  EXCEPTION: the two scooter sheets and the four tool work sheets are authored
  facing right and flip for left (`RiderFlipH`/`ParkedFlipH`/`WorkFlipH`).
  Furniture and crops follow the same rule: a 16x32 piece stands on its cell and
  overhangs the one above it.

# src/ — cross-cutting code rules

Read the architecture contracts before touching Core/Systems code — public signatures
and semantics are specified there: `docs/foundation-spec.md` (clock/save/state base),
`docs/phase2-spec.md` (items/inventory, farming, stamina, economy), `docs/phase3-spec.md`
(story flags, maps/travel, dialogue, NPCs, scripted intro), and `docs/phase3b-spec.md`
(storage/chest + save v4, general store + buy flow, overnight report, Menu phase, help
panel). The art contract is `docs/designs/` — see `docs/designs/CLAUDE.md`.

`src/Main.cs` + `scenes/Main.tscn` are the composition root: boot, map loading, and the
sleep + travel flows. Each subdirectory carries its own CLAUDE.md with its local rules.

## Standing architecture rules (from the design review — violations are bugs)

- Save state lives in the central `GameData` model; scenes are views rebuilt from it.
  Never store durable state in nodes (the only IPersistentSystem is the player).
- C# events: every `+=` in a node has a matching `-=` in `_ExitTree` (C# events don't
  auto-disconnect on free; Godot signals do).
- `MinuteTicked` is display-only (HUD). Gameplay/sim code subscribes to `TenMinuteTicked`.
- Entities (crops, NPCs) never subscribe to time events — systems do, iterating model data.
- Gate behavior on `GameState.ClockRuns` / `PlayerHasControl` (or `CanStartDialogue`:
  Playing or Cutscene), never by comparing the Phase enum.
- `GetTree().Paused` is used exclusively for the Paused phase (GameState.TransitionTo
  owns it).
- Days advance only via `Clock.AdvanceToDayStart()` (the clock clamps at 1:59 AM); the
  sleep flow in Main owns fade → advance → autosave, and awaits the overnight report
  while the phase is still Sleeping. Main's travel flow auto-parks the scooter at the
  door — riding never goes indoors.
- `Engine.IsEditorHint()` and `[Tool]` may appear ONLY in `src/EditorTools/` and
  `addons/` (test-enforced — see src/EditorTools/CLAUDE.md; never sprinkle editor
  guards through the game layers).

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

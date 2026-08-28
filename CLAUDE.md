# The Haunt

A 2D life sim game (Stardew Valley-like) built with Godot 4.7 (.NET build) and C#.
Premise: a small New England town, hidden from every map, where buying property binds
you to the town under a malevolent force — cozy town sim (farming, mining, fishing)
layered under a supernatural endgame. `docs/design.md` holds the canon and the roadmap,
and `docs/story/README.md` is the expanding lore doc (locations, cast, the wrap-around
roads); check with the user before inventing lore beyond what those two state (most
names and specialties are deliberately undecided).

Context is split into directory-scoped CLAUDE.md files: this file keeps only what is
project-wide, and each directory below documents its own contracts. The architecture
specs (docs/*-spec.md) and the cross-cutting code rules are in `src/CLAUDE.md`; the
art contract (six binding handoffs) is in `docs/designs/CLAUDE.md`.

## Toolchain

- Godot 4.7.2 .NET build: `godot-mono` (installed via Homebrew, app at /Applications/Godot_mono.app)
- .NET SDK 9 (project targets net8.0)

## Commands

- Build (fast correctness check — run after every C# change): `dotnet build`
- Full test suite (headless, exit code 0/1): `godot-mono --headless res://scenes/tests/TestRunner.tscn`
- Re-import after adding/changing assets or scenes: `godot-mono --headless --import`
- Run the game: `godot-mono --path .`
- Screenshot for visual verification (opens a window briefly, saves PNG, quits): `godot-mono --path . -- --screenshot /path/out.png`
  (dev flags: `--start-map <id>` boots into a map; `--spawn <marker>` lands on a named spawn
  instead of the map default, e.g. to frame a corner; `--screenshot-frames <n>` delays the
  capture, e.g. past a beat's staging timer; `--add-minutes <n>` advances the clock in-memory,
  e.g. into shop hours or dusk; `--open-ui <chest|shop|help>` pops a UI after boot;
  `--ride` mounts the scooter after boot, for capturing the riding sprite;
  `--work-tool <itemId>` selects that tool and holds use_tool from boot, for capturing
  the work loop — boot physics catch-up outruns the frame count, so expect mid-loop)
- Edit a map graphically: `godot-mono --path . --editor` — full workflow in
  `src/EditorTools/CLAUDE.md` (Save in the Haunt Mapper dock; Ctrl+S saves the SCENE,
  not the map)

## Structure

Each directory's CLAUDE.md carries its contracts — read it before working there.

- `src/Core/` — PURE C# model layer (no `using Godot`, test-enforced): time, save
  data + migrations, item/crop/npc/dialogue defs, rules, overnight sim
- `src/Systems/` — the four autoloads: GameState, Clock, SaveService, WorldSim (the
  single gameplay-mutation bus)
- `src/World/` — maps and views (all programmatic), the art layer, signage, recipes
- `src/Player/` — PlayerController (the one IPersistentSystem) + InteractionProbe
- `src/Story/` — StoryDirector, the scripted intro beats
- `src/UI/` — the code-built HUD and menu layer
- `src/Tests/` — headless [SimTest] suite + TestRunner (scenes/tests/TestRunner.tscn)
- `src/EditorTools/` + `addons/haunt_mapper/` — the in-editor map editor (stage + plugin)
- `src/Main.cs` + `scenes/Main.tscn` — composition root: boot, map loading, sleep +
  travel flows (rules in src/CLAUDE.md)
- `assets/` — shipped handoff art (never redraw; import rules in assets/CLAUDE.md);
  `assets/audio` and `assets/fonts` are still empty
- `data/maps/` — map recipes: CONTENT, not save state; one JSON per map id
- `docs/designs/` — the six binding art handoff bundles
- `tools/` — asset-derivation one-shots (`regen_scooter_rider.py`,
  `gen_item_icons.py` — the inventory icon atlas)

## Project-wide conventions

- Pixel-art settings: nearest filtering, 480x270 viewport in a 1280x720 window (30x17
  tiles; `MapRoot.ViewportWidth/Height` mirrors it for camera limits). Every imported
  PNG must keep Filter: Nearest and Mipmaps: off — one wrong filter is the difference
  between pixel art and mush.
- Scene files: keep hand-authored .tscn minimal (node skeleton + scripts); scripts
  build their visual children in code until real art lands. Maps are NOT becoming
  .tscn — see `data/maps/CLAUDE.md`.
- Do not commit `.godot/` (generated) or `export_presets.cfg`.

# The Haunt

A 2D life sim game (Stardew Valley-like) built with Godot 4.7 (.NET build) and C#.
Premise: a small New England town, hidden from every map, where buying property binds
you to the town under a malevolent force — cozy town sim (farming, mining, fishing)
layered under a supernatural endgame. Story content — characters, places, arcs, the
words — lives in `src/Content` (its CLAUDE.md is the writing contract; every file
marks CANON / PROPOSED / PLANNED / DEFERRED). `docs/design.md` is VISION and open
questions only and never describes what the game does. Never invent names or lore
beyond a file's CANON block without asking Kevin — the town, the malevolence, the
motel and more are deliberately unnamed.

Context is split into directory-scoped CLAUDE.md files: this file keeps only what is
project-wide, and each directory below documents its own contracts. The cross-cutting
code rules are in `src/CLAUDE.md`; the art contract (six binding handoffs) is in
`docs/designs/CLAUDE.md`.

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
  e.g. into shop hours or dusk; `--open-ui <chest|shop|help|mail|letter|quests|garage|skills>` pops a UI after boot;
  `--garage-job <serviceId>` stamps the garage deed and puts a car on a lift (repeat
  for a second car), for capturing the shop floor; `--ride` mounts the scooter after
  boot, for capturing the riding sprite;
  `--work-tool <itemId>` selects that tool and holds use_tool from boot, for capturing
  the work loop — boot physics catch-up outruns the frame count, so expect mid-loop;
  `--tiled-file <path>` builds a map from that .tmx instead of its shipped
  `data/maps/<id>.tmx`, for checking a painted copy without touching the shipped file)
- Content dump (the generated "read the world" review doc — characters, schedules,
  sampled talk tables, places + copy, quests, letters, flags, every dialogue line):
  `godot-mono --headless --path . -- --dump-content /tmp/content.md`
  (output is GENERATED: read it, throw it away, never commit it)
- World dump (every exterior map's geography as JSON, for the intro flyover's Blender
  generator — schema on `src/World/WorldDump.cs`): `godot-mono --headless --path . --
  --dump-world /tmp/world.json` (GENERATED: never commit it)
- Seed a Tiled map's file: `godot-mono --headless --path . -- --seed-tiled <mapId>`
  (any map id — every map is a Tiled map: the exteriors, the farm and the thirteen
  interiors) — always rewrites all five derived palettes (`data/maps/tiled/` surfaces,
  obstacles, floors, walls, dressing: each `.tsx` + `.png`); writes
  `<mapId>.tmx` and `thehaunt.tiled-project` only where missing, never over them.
- Edit a map graphically: in Tiled 1.11+, via the project file
  `data/maps/tiled/thehaunt.tiled-project` (workflow and contract in data/maps/CLAUDE.md)

## Structure

Each directory's CLAUDE.md carries its contracts — read it before working there.

- `src/Core/` — PURE C# model layer (no `using Godot`, test-enforced): time, save
  data + migrations, item/crop catalogs, engine rules, overnight sim
- `src/Content/` — PURE C# story layer (depends on Core only, test-enforced): one
  file per character/place/arc — schedules, dialogue, selectors, quests, letters
- `src/Systems/` — the four autoloads: GameState, Clock, SaveService, WorldSim (the
  single gameplay-mutation bus)
- `src/World/` — maps and views (all programmatic), the art layer, signage, recipes
- `src/Player/` — PlayerController (the one IPersistentSystem) + InteractionProbe
- `src/Story/` — StoryDirector (runs the intro beats defined in `src/Content/Story`)
- `src/UI/` — the code-built HUD and menu layer
- `src/Tests/` — headless [SimTest] suite + TestRunner (scenes/tests/TestRunner.tscn)
- `src/Main.cs` + `scenes/Main.tscn` — composition root: boot, map loading, sleep +
  travel flows (rules in src/CLAUDE.md)
- `assets/` — shipped handoff art (never redraw; import rules in assets/CLAUDE.md);
  `assets/audio` and `assets/fonts` are still empty
- `data/maps/` — map files: CONTENT, not save state; one Tiled `.tmx` per map id
  (every map: the exteriors, the farm and the interiors; Tiled support files under
  `data/maps/tiled/`)
- `docs/designs/` — the six binding art handoff bundles
- `tools/` — asset-derivation one-shots (`regen_scooter_rider.py`,
  `gen_item_icons.py` — the inventory icon atlas, `run_gen_cast.mjs` — local
  runner for the cast handoff's gen_cast.js wardrobe generator); `tools/flyover/` —
  the Blender generator for the intro flyover (phased plan + decisions in its CLAUDE.md)

## Project-wide conventions

- Pixel-art settings: nearest filtering, 480x270 viewport in a 1280x720 window (30x17
  tiles; `MapRoot.ViewportWidth/Height` mirrors it for camera limits). Every imported
  PNG must keep Filter: Nearest and Mipmaps: off — one wrong filter is the difference
  between pixel art and mush.
- Scene files: keep hand-authored .tscn minimal (node skeleton + scripts); scripts
  build their visual children in code until real art lands. Maps are NOT becoming
  .tscn — see `data/maps/CLAUDE.md`.
- Do not commit `.godot/` (generated) or `export_presets.cfg`.

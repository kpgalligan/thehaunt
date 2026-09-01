# src/EditorTools — the map editor's stage and pure geometry

MapStage is the [Tool] node that renders a live map in the Godot editor (scene:
`scenes/editor/MapStage.tscn`); PlacementFootprint + PlacementHit are the pure geometry
the mapper needs. The `addons/haunt_mapper/` plugin drives all three; this directory is
the part the headless test suite can reach.

## Rules

- PlacementFootprint + PlacementHit live OUTSIDE `#if TOOLS` on purpose, so the
  headless suite can reach them (MapEditorTests). They are pure: no node, no editor
  API. MapStage.cs also compiles in game builds — its `_Ready` guard frees it on sight
  in a game process, and that is the only editor guard in `src/` (SourceRulesTests
  enforces it).
- `Engine.IsEditorHint()` and `[Tool]` may appear ONLY in `src/EditorTools/` and
  `addons/`. They are verified unnecessary in `src/World/`: the stage hand-instantiates
  the four autoloads in project.godot's order, so map code runs in the editor
  completely unmodified. If the editor misbehaves, fix the stage — never sprinkle
  guards through the World layer.
- MapStage owns the working recipe (the single mutable copy of a map's placements) and
  the preview built from it. Rebuild never writes to disk; only SaveRecipe does — the
  editor is the only writer of `data/maps/*.json` (recipes are content, never written
  at runtime).
- The unsaved working recipe is parked in node METADATA (`haunt_recipe_<map>`), never a
  C# field: a `dotnet build` with the editor open swaps managed objects out from under
  the still-living nodes, nulling fields; metadata is engine-side and survives.
  StageMapId's setter deliberately rebuilds even on a same-value set — the reload's
  property restore is the stage's only notice that the preview's C# state is gone.
- SaveRecipe refuses to overwrite an unreadable recipe file — the tool's one data-loss
  path, pinned by Editor_SaveRefusesToOverwriteAnUnreadableRecipe. "No recipe yet"
  means an empty recipe; "recipe present but unparseable" is never written over.
- Footprints derive from the ART tables the map's own builder uses (a recipe stores no
  footprint — a `w` field beside a 48px sprite is two truths waiting to diverge); only
  kinds that own their span instead of art (Exit, ShopCounter) read w/h from the
  record. Hits are RANKED: own anchor cell > tighter footprint > later (topmost) record.

## Workflow

Edit a map graphically: `godot-mono --path . --editor`, then open
`scenes/editor/MapStage.tscn` and select the MapStage node. The Haunt Mapper dock
(right) picks the map and the time of day; drag placements in the viewport, then press
Save in the dock — Ctrl+S saves the SCENE, not the map. `--screenshot` stays the
in-game cross-check.

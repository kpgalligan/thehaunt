# addons/haunt_mapper — the Haunt Mapper EditorPlugin

The placement editor: drag records inside a MapStage's working recipe in the Godot
viewport and it rebuilds the live preview; Save in the dock writes
`data/maps/<map>.json`. Four files — HauntMapper (the EditorPlugin), MapperDock,
MapperOverlay, OverlayLayers — ALL inside `#if TOOLS`, enabled from project.godot's
`[editor_plugins]` (plugin.cfg).

## Rules

- The code conventions in `src/CLAUDE.md` apply here too: file-scoped namespaces, one
  class per file, `+=`/`-=` event pairing, and `SetAnchorsAndOffsetsPreset(...)` (never
  `SetAnchorsPreset`) for code-built Controls — MapperDock builds its Controls in code.
- `Engine.IsEditorHint()` and `[Tool]` may appear ONLY here and in `src/EditorTools/` —
  test-enforced (SourceRulesTests). Map code in src/World runs in the editor completely
  unmodified because the stage hand-instantiates the four autoloads in project.godot's
  order; if the editor misbehaves, fix the stage — never guard the World layer.
- Ownership, one line each: MapStage owns the working recipe and the preview — the only
  thing that writes disk. MapperDock owns the widgets, holds no engine object, decides
  nothing. HauntMapper owns canvas input, the selection, and the undo actions.
- RELOAD SAFETY shapes everything: every `dotnet build` with the editor open swaps
  managed objects out from under still-living nodes (fields null, statics reset). So:
  nothing built in a constructor; the stage re-validated on every use; NO lambdas on
  Godot signals (the reload's closure restore throws and the connection silently dies —
  method groups survive); no widget cached in a plain C# field (accessors re-find nodes
  by NAME, engine-side); dock state is READ by a 0.25 s poll, never pushed via C#
  events (they are not serialised across a reload).
- Selection is an INDEX into Recipe.Placements plus a recipe stamp: a drag mutates
  records in place so the index survives; an undo re-parses the recipe, moves the
  stamp, and the selection is correctly dropped rather than pointing at whatever
  inherited the slot.
- MapperOverlay draws into the EDITOR's viewport control, never into the map (the map
  subtree sits at ProcessMode.Disabled between scrubs, and a gizmo node would serialise
  into the next scene save). It renders what the game never shows: grid, footprints and
  anchors, reserved tiles, transparent blockers, NPC slots (OverlayLayers flags;
  Default = Grid | Placements | Reserved).
- Pure dependencies (PlacementFootprint, PlacementHit) live in `src/EditorTools/`
  outside `#if TOOLS` so the headless suite covers them — new pure geometry goes there,
  not here.

## Workflow

`godot-mono --path . --editor`, open `scenes/editor/MapStage.tscn`, select the MapStage
node. The dock (right) picks the map and time of day; drag in the viewport; press Save
in the dock — Ctrl+S saves the SCENE, not the map. `--screenshot` stays the in-game
cross-check. Recipes are content read at map build time, never written at runtime; the
editor is the only writer.

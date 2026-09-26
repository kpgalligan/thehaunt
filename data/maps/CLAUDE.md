# data/maps — map recipes

One file per map id, read by that map's build function: a JSON recipe
(`data/maps/<mapId>.json` — the farm's `test_farm.json`) or a Tiled map
(`data/maps/<mapId>.tmx` — every `ExteriorMap`: the town and the road strip,
`west_entry`/`billies`/`fork`/`east_fork`/`east_entry`/`drive_in`; see "Tiled maps"
below). The interiors still hold their placements as C# literals. The reader/writer code lives in
`src/World/` (MapRecipe, MapPlacement, PlacementKinds/PlacementFields, MapRecipeFile,
MapRecipeException, MapRecipeSeeds; TiledMap, TiledMapFile, TiledSurfaces,
TiledObstacles, TiledSeeds);
the graphical editor for JSON recipes is the Haunt Mapper (`scenes/editor/MapStage.tscn`
+ `addons/haunt_mapper/`); Tiled maps are edited in Tiled.

- Map recipes are CONTENT, not save state — the same bucket as ItemDefs and CropDefs,
  never GameData. Read at map build time, never written at runtime; the editor is the
  only writer. No SaveMigrations versioning: a recipe change is a content change.
- A recipe stores tile coordinates and NAMES, never atlas coordinates and never pixel
  positions, because that is exactly what keeps `ForAct` wrapping every painted cell
  and `Prop.Anchor` owning every anchor. Unknown records round-trip verbatim, like
  unknown item ids — unknown kinds AND unknown fields both survive load and save
  untouched.
- A map with no recipe falls back to its C# literals, so every map stays constructible
  with no file present. A recipe that exists but cannot be read throws
  `MapRecipeException`, which always names the file. Maps are NOT becoming .tscn —
  `MapRegistry`'s "becomes PackedScene.Instantiate" comment is superseded.
- Canonical text format, for legible diffs (being mergeable is most of why this is JSON
  and not a scene): one placement per line, sorted by y then x then kind, fields in a
  fixed order, "\n" endings on every platform. Serialising the same recipe twice is
  byte-identical, and so is a load/save cycle. Values are strings, numbers and bools
  only — an object or array value cannot be held to one line per placement.
- Scatter/prop placements are DECORATIVE-ONLY on maps with field obstacles: the
  farm's clearable trees, stumps and boulders are save state (ObstacleGen seeds
  them; the axe and pick clear them), so its recipe keeps only the fallen log. A
  drawn obstacle that ignored the axe beside an identical one that falls would be
  the map lying about its own rules.
- Terrain painting stays generative. A JSON recipe has no terrain representation at
  all; a Tiled map carries the SURFACE grid (what each cell is, by name), never painted
  tiles. What moves into data is what a person would otherwise drag: props, scatter,
  spawn markers, doors, exits, signs, furniture and the interactables.
- Field keys are `PlacementFields` constants, never literals: a mistyped key is not an
  error, it is an unknown field that round-trips perfectly and is silently ignored by
  the builder — the worst possible failure. A sign's `text` field is the EDITOR'S
  scratch channel only: a promoted sign resolves its words from the owning place's
  file (src/Content/Places — `Farm.SignTextFor`, by placement id), which wins over
  the field; a freshly dragged board carries its text here until it is promoted.
- Seeding: `MapRecipeSeeds` exports a map's first recipe from its C# placement literals
  (fidelity by construction — never transcribe coordinates by hand). Once a map is
  seeded, its file is the map; the seed lives on as the missing-file fallback, and
  `MapSeedTests` is the drift guard — once someone drags a placement in the editor,
  file and seed part company on purpose and the guard says so.

## Tiled maps (every exterior)

Every `ExteriorMap` is a Tiled map: `data/maps/<mapId>.tmx`, read by our own C# loader
(`TiledMap`/`TiledMapFile`, src/World) — no Tiled importer addon. `ExteriorMap` owns
the one build; each map's C# literals (`BuildDefaultSurfaces`, `BuildDefaultRecipe`)
stay as the missing-file fallback and the seed. `town.tmx` is Kevin's hand edit; the
six road-strip files are still exactly their seeds.

- TMX, not TMJ: Tiled writes a TMX CSV layer one map row per line, so a repainted cell
  is a one-line diff. In Map Properties keep Tile Layer Format = CSV; the reader
  refuses anything else (and infinite maps, other tile sizes, embedded tilesets,
  extra layers, groups, image layers) with a `MapRecipeException` naming the file.
- Tilesets: one or two external tilesets — `tiled/surfaces.tsx` (required) and
  `tiled/obstacles.tsx` (optional), each at most once. Tile layers: one or two
  map-sized CSV layers — `surface` (required) and `obstacles` (optional), each at most
  once, in any order. A gid belongs to the tileset with the greatest firstgid at or
  below it, so a file saved before a surface was appended still reads.
- The `surface` tile layer is the map's `ExteriorMap.Surface` grid BY NAME: each
  palette tile stands for one surface (Grass, Dirt, Road...) and the game still paints
  every cell itself (BuildGround + ForAct). The swatch you paint with is never the
  tile the game draws.
- Kerb cuts (where a driveway breaks the road's kerb) are derived AND placed: every
  verge cell beside the gutter (row 13 above the road, row 16 below) that is Dirt,
  Gravel or Cobble cuts the kerb by itself; Asphalt and Concrete do NOT — a paved lot
  is kerbed, and its driveway is a `kerb_cut` placement (row 14 = north kerb, row 15 =
  south; its rectangle's width is the columns). The two sources merge into maximal
  runs. A map with no road (the drive-in) refuses a kerb_cut.
- A lot's stall stripes (the west entry) and a field's ramp rows (the drive-in) are
  drawn over the bounding box of the map's Asphalt — repaint the Asphalt and the
  markings follow it. Water and DeepWater are
  impassable (placeholder art; depth is visual only).
- The optional `obstacles` tile layer is the map's `ExteriorMap.Obstacle` grid BY
  NAME, painted from the `tiled/obstacles.tsx` palette (Fence, Bush); 0 = empty. Both
  block. Code picks each fence's piece from its fence neighbours
  (`FarmTiles.FenceFor`) — paint "fence here", never a particular piece. No layer
  means no obstacles, and the layer needs no obstacles.tsx while it is all empty.
- Adding the layer to an existing map: rerun `--seed-tiled <mapId>` (it writes the
  obstacles palette), then in Tiled use Map > Add External Tileset… and pick
  `tiled/obstacles.tsx`, and Layer > New > Tile Layer, named exactly `obstacles`,
  above `surface`. Paint, then save.
- Checking a painted copy without touching the shipped file: `--tiled-file <path>`
  builds the map its `map` property names from that .tmx instead (e.g.
  `godot-mono --path . -- --start-map town --tiled-file /tmp/town.tmx --screenshot
  /tmp/town.png`).
- The `placements` object layer: one rectangle object per placement — Class
  (`type`) = kind, Name = id, cell = (x/16, y/16), custom properties = the record's
  fields (string/int/bool only; reserved keys refused). `exit`/`shop_counter`/`kerb_cut`
  take their `w`/`h` from the rectangle's size; every other kind is a 16x16 box whose
  size is ignored. No nudges. Enable Snap to Grid: an off-grid object is an error.
- Kinds an exterior builds: `prop` (the id must be in that map's prop catalog — an
  unknown one throws, listing the known ids), `spawn`, `sign`, `door`, `exit`,
  `kerb_cut`. Any other known kind throws; an unknown kind rides through. A sign's
  `board` (bool, default true) is false where the art already draws the board (the
  motel's pole sign, the drive-in marquee): the node reads, it draws nothing.
  A door or exit's id is its target map; the node is `Door_<id>` / `Exit_<id>`.
- `tiled/` holds Tiled's support files and carries a `.gdignore` (Godot must not
  import them). Open `tiled/thehaunt.tiled-project` in Tiled 1.11+ to get one object
  class per placement kind, with its fields' defaults.
- The palettes (`tiled/surfaces.tsx` + `.png`, `tiled/obstacles.tsx` + `.png`) are
  DERIVED from `TiledSurfaces.Names` / `TiledObstacles.Names` (= the `Surface` and
  `Obstacle` enums, both APPEND-ONLY — gids index them) and never hand-edited:
  `godot-mono --headless --path . -- --seed-tiled <mapId>` (any exterior) rewrites all
  four on every run. The same run writes `thehaunt.tiled-project` only if it is
  missing and the `.tmx` only if it is missing — an existing `.tmx` is never
  overwritten. The project carries one class per placement kind, so after a kind or a
  class member is added (e.g. `kerb_cut`, the sign's `board`), delete the project file
  and re-run a seed to pick them up.
- Drift guards compare SEMANTICALLY (not bytes — Tiled owns the bytes):
  `Town_ShippedTmxPlacementsMatchTheCodeSeed` is size + placements only, since Kevin
  reshaped the town's woods in Tiled (2026-09-26) and its ground left its seed behind
  on purpose; `Exteriors_ShippedTmxMatchTheirCodeSeeds` holds the six road-strip files
  to their seeds in full (size, every surface, every obstacle, placements). When a
  Tiled edit makes one fail, DECIDE, as with MapSeedTests.

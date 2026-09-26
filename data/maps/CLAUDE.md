# data/maps — map files

Every map is a Tiled map, one `.tmx` per map id, read by that map's build function:
`data/maps/<mapId>.tmx` — every `ExteriorMap` (the town and the road strip,
`west_entry`/`billies`/`fork`/`east_fork`/`east_entry`/`drive_in`), the farm
(`test_farm.tmx`) and every `InteriorMap` (the thirteen interiors); see "Tiled maps",
"The farm" and "Interiors" below. The reader/writer code lives in `src/World/`
(MapRecipe, MapPlacement, PlacementKinds/PlacementFields, MapRecipeException; TiledMap,
TiledFormat, TiledPalette, TiledMapFile, TiledSurfaces, TiledObstacles, TiledSeeds).
Maps are edited in Tiled, through `tiled/thehaunt.tiled-project`.

- Map files are CONTENT, not save state — the same bucket as ItemDefs and CropDefs,
  never GameData. Read at map build time, never written at runtime; Tiled (and the
  `--seed-tiled` exporter, for a missing file) is the only writer. No SaveMigrations
  versioning: a map change is a content change.
- A map file stores tile coordinates and NAMES, never atlas coordinates and never pixel
  positions, because that is exactly what keeps `ForAct` wrapping every painted cell
  and `Prop.Anchor` owning every anchor. Unknown placement kinds ride through the
  build untouched.
- A map with no file falls back to its C# code seed, so every map stays constructible
  with no file present. A file that exists but cannot be read throws
  `MapRecipeException`, which always names the file. Maps are NOT becoming .tscn —
  `MapRegistry`'s "becomes PackedScene.Instantiate" comment is superseded.
- Scatter/prop placements are DECORATIVE-ONLY on maps with field obstacles: the
  farm's clearable trees, stumps and boulders are save state (ObstacleGen seeds
  them; the axe and pick clear them), so its file keeps only the fallen log. A
  drawn obstacle that ignored the axe beside an identical one that falls would be
  the map lying about its own rules.
- Terrain painting stays generative. A Tiled map carries its grids (what each cell
  is, by name — surfaces, or an interior's floors and walls), never painted tiles;
  its placements are what a person would otherwise drag: props, scatter, spawn
  markers, doors, exits, signs, furniture and the interactables.
- Field keys are `PlacementFields` constants, never literals: a mistyped key is not an
  error, it is an unknown field that round-trips perfectly and is silently ignored by
  the builder — the worst possible failure. A sign's `text` field is the EDITOR'S
  scratch channel only: a promoted sign resolves its words from the owning place's
  file (src/Content/Places — `Farm.SignTextFor`, by placement id), which wins over
  the field; a freshly placed board carries its text here until it is promoted.

## Tiled maps (every map)

Every map is a Tiled map: `data/maps/<mapId>.tmx`, read by our own C# loader
(`TiledMap`/`TiledMapFile`, src/World) — no Tiled importer addon. `ExteriorMap` owns
the exteriors' one build and `InteriorMap` the interiors' (see "Interiors"); the farm
(`TestMap`) keeps its own (see "The farm"). Each map's C# literals stay as the
missing-file fallback and the seed. `town.tmx` is Kevin's hand edit; the six
road-strip files, `test_farm.tmx` and the thirteen interior files are still exactly
their seeds.

- TMX, not TMJ: Tiled writes a TMX CSV layer one map row per line, so a repainted cell
  is a one-line diff. In Map Properties keep Tile Layer Format = CSV; the reader
  refuses anything else (and infinite maps, other tile sizes, embedded tilesets,
  extra layers, groups, image layers) with a `MapRecipeException` naming the file.
- Two FORMATS (`TiledFormat`), and a file is one or the other — its tilesets say
  which, and mixing them is an error. EXTERIOR (every exterior and the farm): external
  tilesets `tiled/surfaces.tsx` (required) and `tiled/obstacles.tsx` (optional); tile
  layers `surface` (required) and `obstacles` (optional). INTERIOR (every interior):
  tilesets `tiled/floors.tsx` (required), `tiled/walls.tsx` and `tiled/dressing.tsx`
  (optional); tile layers `floor` (required), `walls` and `dressing` (optional). Each
  tileset and each layer at most once, layers map-sized CSV in any order. The first
  layer has no empty cell; the optional ones read 0 as empty. A gid belongs to the
  tileset with the greatest firstgid at or below it, so a file saved before a palette
  entry was appended still reads, and it must land in its layer's own palette. A map
  refuses a file of the other format (`RequireFormat`), naming the file.
- The `surface` tile layer is the map's `ExteriorMap.Surface` grid BY NAME: each
  palette tile stands for one surface (Grass, Dirt, Road..., and the farm's Pasture
  and Path) and the game still paints
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
  NAME, painted from the `tiled/obstacles.tsx` palette (Fence, Bush, and the farm's
  Gate); 0 = empty. Fence and Bush block. Code picks each fence's piece from its fence neighbours
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
- The five palettes (`TiledPalette.All`: `tiled/surfaces`, `obstacles`, `floors`,
  `walls`, `dressing` — ten files, each a `.tsx` + `.png`) are DERIVED from their
  names (= the `ExteriorMap.Surface`/`Obstacle` and `InteriorMap.Floor`/`Wall`/`Dressing`
  enums, all APPEND-ONLY — gids index them) and never hand-edited:
  `godot-mono --headless --path . -- --seed-tiled <mapId>` (any map) rewrites all
  ten on every run. The same run writes `thehaunt.tiled-project` only if it is
  missing and the `.tmx` only if it is missing — an existing `.tmx` is never
  overwritten. The project carries one class per placement kind, so after a kind or a
  class member is added (e.g. `kerb_cut`, the sign's `board`, the chest's `art`, the
  garage's `lift`), delete the project file and re-run a seed to pick them up.
- Drift guards compare SEMANTICALLY (not bytes — Tiled owns the bytes):
  `Town_ShippedTmxPlacementsMatchTheCodeSeed` is size + placements only, since Kevin
  reshaped the town's woods in Tiled (2026-09-26) and its ground left its seed behind
  on purpose; `Exteriors_ShippedTmxMatchTheirCodeSeeds` holds the six road-strip files
  and `test_farm.tmx` to their seeds in full (size, every surface, every obstacle,
  placements), and `Interiors_ShippedTmxMatchTheirCodeSeeds` holds all thirteen
  interior files in full (size, every floor, wall and dressing cell, placements).
  When a Tiled edit makes one fail, DECIDE: a code change to the seed means the file
  follows (delete it and re-seed); a deliberate Tiled edit means the map has left its
  seed behind, and the guard should say so for it. Never quietly re-seed over a
  hand-edited map.

## The farm (`test_farm.tmx`)

The id stays `test_farm` (its rename is a separate save migration). `TestMap` reads the
file through its own load (`LoadMap`), not the `ExteriorMap` template.

- Surfaces: `Pasture`, `Path`, `Dirt` (the wagon road — unsealed, the farm never
  paves) and `Woods`. Obstacles: `Fence` and `Gate` (the gate is open and walkable).
  The fences paint on the farm's Ground layer, from its own sheet: rails block through
  the sheet's collision, the gate cell is standable and reserved from the hoe.
- The pen is the bounding box of the farm's fences — one enclosure. ObstacleGen keeps
  it plus a one-cell ring clear of field obstacles.
- The palette is one list for every map, so each side refuses the other's entries:
  the farm refuses Grass, Gravel, Cobble, Asphalt, Concrete, Road, Water, DeepWater and
  Bush; an exterior refuses Pasture, Path and Gate. Both throw `MapRecipeException`
  naming the file and the cell.
- Kinds the farm builds: `prop` (tree ids), `scatter`, `spawn`, `door`, `sign`,
  `shipping_bin`, `mailbox`, and exactly one `exit`, to `fork`. A sign named
  `BlockadeSign` is required (the storm blockade toggles it).
- The drift guard (`Exteriors_ShippedTmxMatchTheirCodeSeeds`) holds the farm in full.
- Not in the file, and never: the facades, their footprint blockers and roof
  reservations, the storm debris, the road corridor, and the field obstacles, soil,
  crops and watered state (save state).

## Interiors

Every `InteriorMap` is an interior-format Tiled map, one file per map id — each motel
room is its own file (`motel_room_1`..`4`), because the rooms already differ (3 is
Pell's, 4 the one Walt gave up on) and story treats them separately. The room's size
is the file's. `InteriorMap._Ready` is the one build.

- Layers, by name: `floor` (`InteriorMap.Floor`: Plank, PlankStagger, PlankWorn,
  Stone, Dirt, Board, Check, Hay, RugA, RugB, Stain, Dark) paints Ground; `walls`
  (`InteriorMap.Wall`: the wall courses, windows, cornices, ceiling, rafters, loft,
  fixtures, shelves and storage, and `Blocker` — collision for a sprite, drawing
  nothing) paints Obstacles, the visual AND the collision; `dressing`
  (`InteriorMap.Dressing`: Cobweb, the sheet's one alpha tile) hangs over both.
- DERIVED, never in the file: each floor's variant (`(x+y) % n` — PlankStagger is the
  farmhouse's plank-plank-worn step, PlankWorn Billie's worn-heavy one); a Counter's
  or Hearth's L/C/R piece from its same-name neighbours (paint "counter here", never a
  piece); door_open on every door cell, and the threshold on the floor cell just
  north of it. Paint a hearth's fire (HearthFire) yourself, under its centre.
- PAINTED, per room: the ring's north row is a cornice whose material is picked for
  contrast with THAT room's floor (a log cornice over plank, or plank over dirt, is
  the same brown twice and the back wall dissolves). The sides carry the building's
  material.
- Kinds an interior builds: `spawn`; `door` (id = target map, `spawn` its target
  spawn; the node is `Door_<id>`); `furniture` (id = a `Furniture` piece in
  snake_case — `tall_shelf`, `chair_back`; an unknown one throws listing them; blocks
  its base row unless `blocks` is false — a till on a counter, a lamp on a desk);
  `bed` (id = its piece); `chest` (id = its storage id; `art` = a furniture piece, or
  empty for the procedural placeholder); `shop_counter` (id = a shop catalog; its
  rectangle is the strip); and the garage's `lift` (id "0"/"1", its bay's west cell;
  exactly one of each, or the garage refuses the file before building anything). Any
  other known kind throws; an unknown kind rides through.
- Never in the file: the Surround (sized from the grid), the garage's cars and their
  labels (GarageJobs), and the barn's sweep (BarnRules repaints the file's Stain
  floors and Cobweb dressing to dirt and nothing once the barn is repaired — the file
  draws it derelict).

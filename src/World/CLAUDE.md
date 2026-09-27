# src/World — maps, views, and the art layer

Everything in this directory is a VIEW. Save state lives in the central `GameData`
model; these scenes are rebuilt from it and hold nothing durable. All model writes go
through the WorldSim autoload — this layer paints, syncs, and diffs. Violations of the
rules below are bugs.

What lives here:
- `MapRoot` — base for every map: owns the NPC views (`SyncNpcs`), the parked-scooter
  sync (`SyncScooter` diffs the view against `GameData.Scooter`), `IsStandable`,
  `IsInterior`, arrival geometry (`GetArrival`) and the shared scatter hash.
  `MapRegistry` is map id -> root-node factory; unknown ids throw
  and Main falls back to the farm, leaving the unknown map's MapState untouched
  (preservation rule).
- Exteriors: `TestMap` (the farm) and the `ExteriorMap` base carrying `TownMap` and
  the road strip `WestEntryMap`/`BilliesMap`/`ForkMap`/`EastForkMap`/`EastEntryMap`,
  plus `DriveInMap`. All programmatic, all Tiled. `ExteriorMap` owns the surface grid,
  the shared town-sheet painters and THE build (`_Ready`, a template no subclass
  overrides): load `data/maps/<MapId>.tmx` (else the code seed), resolve every
  placement up front, then ground → dressing → road kerbs → Obstacles → props →
  Spawns → signs → exits → doors (Y-sort ties fall back to that order). A subclass
  supplies its code seed (`BuildDefaultSurfaces`, `BuildDefaultRecipe` — off-tree
  safe, `CodeSeed` wraps both for `TiledSeeds`), its prop catalog (`PropCatalog`:
  placement id → `ExteriorProp`, the node builder plus its footprint as offsets from
  the placed cell, blocked minus door cells) and hooks: `HasRoad`, `SignTextFor` (the
  place's copy), `ConfigureDoor` (locks and lines), `ExitGate` (a gated private
  drive), `BuildDressing` (ground decals), `OnBuilt` (a map that keeps hold of its
  own nodes). Views derived from the build (`WestEntryMap.MotorCourt`/`LotStalls`,
  `DriveInMap.FieldRamps`) are instance state, null before the build or when the
  file places nothing for them. `TestMap` is on Tiled too (`data/maps/farm.tmx`)
  but keeps its OWN build (`LoadMap`, then its farm-sheet layers, facades, soil and
  crops): surfaces Pasture/Path/Dirt/Woods, its pen from the file's Fence/Gate cells
  (`Pen`/`PenGate`, painted on Ground by `PaintFences`), its code seed
  (`BuildDefaultSurfaces` + `DefaultRecipe`, `CodeSeed` for `TiledSeeds`). The
  facades, storm debris, road corridor and roof reservations stay code.
- Interiors: the `InteriorMap` base with `TownHallMap`/`FarmHouseMap`/`GeneralStoreMap`/
  `BarnMap`/`MotelMap`/`GasStationMap`/`BilliesBarMap`/`SalonMap`/`MotelRoomMap` (one
  class, four registered room ids, one file each), and `GarageInteriorMap` (Kevin's
  2026-08-30 garage-operation commission). All Tiled. `InteriorMap` owns THE interior
  build (`_Ready`, a template no subclass overrides): load `data/maps/<MapId>.tmx`
  (an interior-format file: floor, walls and dressing grids by name, plus placements;
  the room's size is the file's) else the code seed, resolve every placement up front
  (a subclass validates its own kinds in `ResolveTaken`, before anything exists), then
  Surround → Ground (floor variants, thresholds) → Obstacles (walls, door_open,
  furniture blockers) → Dressing → furniture props → Spawns → bed → chests → shop
  counters → doors → `OnBuilt` (Y-sort ties fall back to that order). A subclass
  supplies its code seed (`BuildDefaultLayout`, starting with `ResetLayout`, +
  `BuildDefaultRecipe` — off-tree safe, `CodeSeed` wraps both for `TiledSeeds`) and
  hooks: `TakesKind` (a room-only kind), `ResolveTaken`, `OnBuilt` (builds only; the
  live painters `PaintFloor`/`PaintDressing`/`Block` and `CellsOf` serve it). The
  barn's repair states repaint the file's Stain floors and Cobweb dressing in
  `OnBuilt`/ApplyState (the `_built` guard: WorldSim may call ApplyState before
  `_Ready`). The garage takes the `lift` kind (exactly lifts "0".."MaxCars-1"): two
  permanently-Block()ed lift bays whose cars are the map's model-derived staging —
  ApplyState diffs one `GuestCar` per occupied lift from `GameData.GarageJobs`, keyed
  by (ArrivalDay, ArrivalHour, ServiceId), with a floating progress label per bay;
  painted per service so the bay tells you the job. The permanent bay blockers are
  load-bearing: an hourly arrival must never spawn a collider on top of the player.
- Interactables: `IInteractable`, `Bed`, `Sign`, `ShippingBin`, `Chest`, `ShopCounter`,
  `Mailbox`, `Scooter` (parked view of `GameData.Scooter`), `MapExit`, `Door` (flag-lockable:
  `RequiredFlag` + `LockedMessage` — a locked handle answers with a line; the
  garage's shop door is deed-locked on garage.deed, the motel-room pattern),
  `GarageSaleSign` (the west entry's FOR SALE board: opens WorldSim's garage-sale
  session until garage.deed lands, then answers SOLD — checked live per interact
  like Door.RequiredFlag, so the purchase needs no repaint), and `LiftStation`
  (one per garage bay: no prompt over an empty lift, "Work" = one
  WorldSim.WorkOnGarageJob press, a finished or out-of-stamina press answers with
  a line — the locked-Door rule: never silence).
- `NpcView` (ambles around its schedule anchor when the placement grants an Ambit),
  `GuestCar` (one per occupied motel room, synced by `WestEntryMap.ApplyState`;
  reused for the garage's lift cars), `CarLift` (the drawn two-post lift under
  them, blockerless by design);
  placeholders: `PlaceholderSprites`, `PlaceholderBuilding`/`RoadBarrier`/`PitCover`/
  `DriveInScreen`/`DriveInSpeaker` (code-built stand-ins for buildings with no art yet,
  chained-off roads, the pit, and the dead drive-in).
- Signage (motel handoff, all authored in code): `PixelFont` (the one 3x5 typeface),
  `MotelFacade`/`MotelSign` (the blinking V), `WallBandSign`/`BracketSign`/
  `NeonWordSign`/`PoleSign`, `RoadsideTiles`/`RoadsideTerrain` (generated
  lot/concrete/road source beside the town atlas; ExteriorMap's Asphalt/Concrete/Road
  surfaces need `RoadsideTerrain.Get()`. Source 2 is the generated `LandscapeTiles`
  sheet — placeholder water, deep water and bush, every tile a blocker — and source 3
  a private copy of the farm atlas, for fences), `StreetLight` (the cobra heads).
- Art layer: `TileSetTools` (walkable derivation + blockers), the five named-coordinate
  tables `TerrainTiles`/`FarmTiles`/`InteriorTiles`/`RoadsideTiles`/`LandscapeTiles`
  with the TileSets `TownTerrain`/`FarmTerrain`/`InteriorTerrain`/`CropTiles`, `Prop` + the sheets
  `TownProps`/`FarmBuildings`/`Furniture`, `StoreFacade`/`BarnFacade`/`LampPost`,
  `CharacterSprites` + `CharacterSprite`, `DayNight` + `DayNightTint` + `GlowLight`.
- Placements (code side): `MapRecipe` (in-memory placements; `ToJson` is the canonical
  text the drift guards compare) + `MapPlacement` + `PlacementKinds`/`PlacementFields`
  + `MapRecipeException`. Tiled (every map): `TiledMap` (strict TMX subset, canonical
  writer, format-generic) + `TiledFormat` (which tile layers a file has: exterior =
  surface + obstacles, interior = floor + walls + dressing; `RequireFormat` refuses
  the other's) + `TiledPalette` (the five palettes — surfaces, obstacles, floors,
  walls, dressing — each a tileset whose tiles stand for names) + `TiledMapFile` (+
  the `--tiled-file` dev file) + `TiledSurfaces` (the surface palette, the farm's
  Pasture/Path included) + `TiledObstacles` (the obstacle palette: Fence, Bush, the
  farm's Gate) + `TiledSeeds` (`--seed-tiled`); `ExteriorMap.LoadSurfaces` reads the
  surface layer by name, `LoadObstacles` the optional obstacles layer (both refuse the
  farm's entries), and
  `PaintObstacles` paints it onto the Obstacles layer. The map FILES and their contract
  live in `data/maps/` — see `data/maps/CLAUDE.md`.
- `WorldDump` (+ `WorldDumpEntry`, `ISurfaceGrid`): the `--dump-world` exporter —
  builds each exterior headless and reads it back;
  schema on the class. The `internal` read-only views on the map classes exist for it.

## Travel

- Travel keeps the player's lane: `MapExit` passes the body's offset inside the mouth it
  entered, and `MapRoot.GetArrival` re-applies it along the destination mouth's LONG
  axis (the nearest exit zone to the spawn marker), clamped inside the mouth — a smaller
  mouth pins to its edge. Zero offset (doors, scripted travel, tests) lands exactly on
  the marker. A road mouth must therefore be longer across the road than deep (e.g. the
  farm's south exit is 2x1), or the carry cannot tell its axes apart.

## NPC staging

- `NpcPlacement.Ambit` is the view-side amble radius in tiles (0 = a fixture). The
  amble is VOLATILE view state in NpcView — gated on `ClockRuns`, bounded by
  IsStandable plus a physics probe (other bodies), never model state — and
  `SyncNpcs`/`SetAnchor` only teleports when the scheduled anchor CHANGES, so the
  ten-minute resync never yanks a wanderer home. Rules with names on them: Gloria and
  the seated bar patrons stay Ambit 0 (Kevin); every flag-BOUNDED row (ForbidsFlag —
  the intro beats' tableaux) stays 0.

## Motel occupancy

- Occupancy grows flags, never ints: `MotelRules.OccupiedRooms` (like LitRoom) is the
  derivation, and the west entry parks one `GuestCar` per occupied room in the stall
  under that room's door — diffed in ApplyState, never baked into the build. Lot cars
  park NOSE-IN facing the motel (Kevin, 2026-09-24: `NoseIn`, the rear view), centred
  between the stall's two stripes (`WestEntryMap.RoomStallPx`, derived from `LotStalls`);
  the garage lifts keep the side elevation.

## The scooter

- Exactly ONE scooter exists: `GameData.Scooter` is either parked (map + tile + facing)
  or mounted — never both. All writes go through WorldSim (MountScooter /
  DismountScooter / ParkScooterAt); the parked `Scooter` node is a view MapRoot syncs,
  like NPCs. OvernightSim parks it home (farmhouse frontage) on DayEnded — never
  stolen, never lost. Riding is 2x walk speed, never indoors (Main's travel flow
  auto-parks at the door, and load repair re-parks impossible interior states home via
  `MapIds.IsInterior` — a table with a drift guard against each map's IsInterior);
  mounting has NO ceremony (texture swap + speed, no fade). Do not add a recall button,
  a minimap pin, or an auto-return beyond the overnight one (handoff design intent:
  losing track of it is the player's problem).

## Art rules (from the handoffs — see docs/designs/CLAUDE.md and each README)

- Ground is drawn flat; anything vertical is drawn front-face-only in elevation, base
  anchored, never with side walls. A `Prop`'s Position is its footprint's bottom-centre.
- Draw order is Y-sort, enabled on World/MapHost in Main.tscn and on every MapRoot so
  the player passes behind a roof overhang and in front of its base row. A new drawn
  node must anchor on its base or it will sort wrong.
- Sprite-drawn facades and props get their collision from `TerrainTiles.Blocker` on the
  Obstacles layer — a transparent atlas cell. Terrain "walkable" custom data is DERIVED
  from the TileSet's own collision in `TownTerrain`, never hand-listed.
- Time of day is one CanvasModulate (`DayNightTint`, driven by `DayNight`'s keys off
  TenMinuteTicked + DayStarted); interiors take a fixed warm key instead. Lit things
  punch back through it as additive `GlowLight`s, flipping on `DayNight.SignsLit`
  (dusk 720 to dawn 150, hard cut). THREE light sources, never mixed (motel handoff):
  neon aqua/red on signs, incandescent amber strictly indoors (lit windows, bulb
  rails), cold mercury vapour on the street (`StreetLight` cobra heads — the west
  entry's east head is dead and stays dead). `LampPost` firelight is retired inside
  the town line; it stays shippable for rustic frames beyond it.
- The town is PAVED (motel handoff §Road): road rows are `Surface.Road` tiles (a full
  value-step darker than lot `Surface.Asphalt`), and kerbs/centre line/cracks come
  from `ExteriorMap.BuildRoadDressing` with a kerb cut wherever a driveway or path
  crosses. Roads turn unsealed (Dirt) past the town line — the fork's farm branch and
  every drive stay dirt, and the farm itself never paves.
- Signage: every business exterior wears exactly one of the four mounts (pole /
  wall band / hanging bracket / window+neon), lettered in `PixelFont`'s 3x5 alphabet —
  no second typeface, ever. City hall gets NO exterior sign (confirmed in the handoff).
  The motel pole sign's nameplate is BLANK and the drive-in marquee carries no name —
  both wait on Kevin. The vacancy sign's `V` is the ONLY animated sign in the game
  (4.0s cycle, 0.55s off, hard cut, never randomised); a second flickering sign must
  replace it, not join it.
- Act II/III are a variant set of the SAME tiles at the SAME coordinates, swapped by a
  story flag: every painted cell goes through `TerrainTiles.ForAct` (FarmTiles,
  InteriorTiles and RoadsideTiles carry the same ForAct seam). No map is rebuilt.
- Keep `PlaceholderSprites` and the procedural fallbacks working (`Door.DrawPlaceholder`,
  `Bed`/`Chest`/`ShippingBin`'s zero-size `ArtSource`) — they let a new map ship before
  its art exists.
- Every sheet gets its `walkable` data and its blocker from `TileSetTools`. Only the
  town atlas has a spare transparent cell; the farm borrows it (`FarmTerrain` merges a
  private copy of the town atlas, which is also where the shared woods edge comes from),
  and the exterior set borrows the farm atlas for fences (`RoadsideTerrain` source 3),
  and the interiors add a one-tile transparent source of their own.
- Water is GROUND (`Surface.Water`/`DeepWater`, blocking through its tiles); fences
  and bushes are the Obstacles layer (`ExteriorMap.Obstacle`, painted by
  `PaintObstacles` before the prop blockers). Fence pieces are DERIVED from their
  fence neighbours by `FarmTiles.FenceFor`, never stored. The generated water and bush
  tiles are placeholders whose `LandscapeTiles.ForAct` is the identity in every act.
  The farm fence tiles are opaque, so a town fence cell shows farm pasture around its
  rails.
- Interiors are Tiled files a room's `InteriorMap` subclass builds from: a floor grid
  (`InteriorMap.Floor`, painted on Ground, variants indexed `(x+y) % n`), a walls grid
  (`InteriorMap.Wall`, painted on Obstacles — visual AND collision; counter and hearth
  pieces picked from their same-name neighbours; the north row's cornice painted per
  room for contrast with its floor) and a dressing grid (`InteriorMap.Dressing` — the
  cobweb is the sheet's only alpha tile and goes on the Dressing layer). The build
  derives door_open on every door cell and the threshold just inside it; furniture is
  a `Prop` plus a blocker on its base row. All three enums are APPEND-ONLY (palette
  gids index them), and every painted cell goes through `InteriorTiles.ForAct`.
- Soil is an autotile, so `TestMap.RefreshTile` repaints a five-cell plus, not one
  cell: tilling changes this cell's tile AND its four neighbours' edges.
- The Crops layer is Y-sorted because crop cells are 16x32 and overhang the row above.
- Field obstacles are VIEWS of `MapState.Objects` (never recipe content — a drawn tree
  that ignored the axe beside one that falls would be the map lying): `TestMap.SyncObstacles`
  diffs them on every ApplyState/RefreshObstacle — stump/rock cells paint the farm
  sheet's own solid tiles into the Obstacles layer (the RoadBlock precedent, so
  IsTillable/IsStandable refuse them for free), a tree is a Y-sorted canopy `Prop`
  plus a trunk Blocker at the record's cell. `TestMap.ObstacleCandidates` is the
  farm's "certain areas" answer: open pasture minus reservations, the pen, recipe
  scatter, and a one-tile ring around every spawn. The bare-tree pick hashes the
  trunk cell — allowed HERE because a save record never moves (the identity-from-
  coordinate ban protects draggable placements).
- The barn's three drawn states are two monotone flags through `BarnRules`, never an
  int in one flag — a flag's value in this model is the day it was stamped, not a
  level. Nothing advances them yet; that seam is deliberately empty.
- A drawn doorway sits one tile ABOVE the facade's bottom row (the bottom row is its
  stone plinth), but the Door node stays on the bottom row: the player walks up to the
  ground in front of the building, not onto its foundation.

## Character sheets (cast-sprites + scooter handoffs)

- Every character — Jane (`assets/sprites/character.png`) and the cast atlases under
  `assets/sprites/cast/` (west/billies/east/town) — is a 96x96 block of 16x32 cells:
  6 columns (cols 0-1 idle, cols 2-5 walk) by 3 rows (down/left/up), feet on the
  bottom row. `NpcDef` names a sheet + block; `BodyColor` and the whole tunic-recolor
  channel are gone. `docs/designs/design_handoff_cast_sprites/art/gen_cast.js` is the
  wardrobe source of truth — changing clothes is a spec edit + re-run, never a repaint.
  Dread accents (plum/bile-green) appear on NO garment in Act I (test-guarded), Sam is
  never gendered, Bud's cap carries no insignia.
- CAUTION: both scooter sheets (`scooter_rider.png` 96x96, `scooter_parked.png` 48x32,
  three views) are authored facing RIGHT and flip for LEFT (`RiderFlipH`/`ParkedFlipH`)
  — mirrored from character.png's left-facing convention.
- Tool work sheets (tools handoff, `assets/sprites/tools/`): 64x192 per tool — 4
  frame columns by 6 rows (tier x facing: down, side; row = tier*2 + side), 16x32
  cells. The swing is baked into Jane's frames (no overlay layer): working is a
  sheet + row selection like riding, but the FRAME is pushed by PlayerController
  (`CharacterSprite.SetWorking`), never advanced in _Process — the impact timing is
  gameplay. Side rows swing on the figure's RIGHT and flip for LEFT (WorkFlipH;
  scooter convention); up is not authored and reuses down; tiers all render basic
  until tier state exists. The scythe has no work sheet (instant path).
- `scooter_rider.png` is DERIVED art: character.png composited onto the deck by
  `tools/regen_scooter_rider.py` from the scooter handoff's recipe tables — rerun it
  whenever character.png changes. The tool mirrors the profile row so Jane faces the
  direction of travel, and composites her in two parts with a slight knee-bend (both
  lifts measured from the sheet, so a repaint lands correctly without touching the tool).

## TileSet builders and the editor

- The four TileSet builders (TownTerrain/FarmTerrain/InteriorTerrain/RoadsideTerrain)
  assemble their `.tres` at runtime, and they must (a) build on a PRIVATE copy
  (`CacheMode.Ignore`) and (b) be IDEMPOTENT. Both are load-bearing, and both were
  learned the hard way. `GD.Load` returns the process-cached resource — in the editor,
  the object the editor owns and writes back to disk — so mutating it baked the derived
  walkable data and a synthesized source into the shipped art, which is precisely the
  hand-listed collision the art rules forbid. And a `dotnet build` with the editor open
  reloads the assembly, clearing the C# static but NOT Godot's resource cache, so
  `Build` re-enters an already-built set. `TileSetReloadTests` guards both.
- `Engine.IsEditorHint()` and `[Tool]` appear nowhere in src (test-enforced): Tiled
  is the map editor, and no map code runs in the Godot editor.
- The mailbox (Mailbox, recipe kind "mailbox", farm cell (6,8) — (9,8) beside it is
  the scooter's home and stays clear) opens WorldSim's mailbox session; its
  raised-flag signal is a pure GameData derivation (MailRules.HasUnread) re-derived
  by TestMap.ApplyState on every repaint, so a read stamp lowers it live through the
  same path that repaints the road and the barn. Its cell is reserved like a sign's.

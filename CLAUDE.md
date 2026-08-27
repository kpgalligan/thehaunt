# The Haunt

A 2D life sim game (Stardew Valley-like) built with Godot 4.7 (.NET build) and C#.
Premise: a small New England town, hidden from every map, where buying property binds
you to the town under a malevolent force — cozy town sim (farming, mining, fishing)
layered under a supernatural endgame. `docs/design.md` holds the canon and the roadmap,
and `docs/story/README.md` is the expanding lore doc (locations, cast, the wrap-around
roads); check with the user before inventing lore beyond what those two state (most
names and specialties are deliberately undecided).

The architecture contracts are `docs/foundation-spec.md` (clock/save/state base),
`docs/phase2-spec.md` (items/inventory, farming, stamina, economy), and
`docs/phase3-spec.md` (story flags, maps/travel, dialogue, NPCs, scripted intro), and
`docs/phase3b-spec.md` (storage/chest + save v4, general store + buy flow, overnight
report, Menu phase, help panel). Read them before touching Core/Systems code — public signatures and semantics are specified there.

The art contract is `docs/designs/` — three handoffs, all binding.
`design_handoff_town_art/` is the base: palette, projection, tile grammar, lighting keys
and the act-by-act dread escalation, with `reference/The Haunt - Art Direction.dc.html`
as the full bible. `design_handoff_farm_interiors/` is incremental on top of it (farm
soil autotile, crops at 16x32, the barn's three states, the 64-tile interior atlas and 34
furniture pieces) — where the two conflict, handoff 01 wins. Each `README.md` is its
integration brief; the `reference/` room and scene renders are design targets, not
assets. The `art/` PNGs of those two are the shipped assets, copied into
`assets/sprites/` — never redraw, scale or filter them.
`design_handoff_motel_signage/` (2026-08-27) is different in kind: its `art/` PNGs are
MOCKUPS, not atlases — the motel facade, pole sign, and the four sign mounts are
authored IN CODE from its pixel-exact spec tables (MotelFacade, MotelSign, the
*Sign nodes, PixelFont, RoadsideTerrain). It also spends the palette's two reserved
neon slots (aqua `#5fb9b0`, red `#e05a3f`) and fixes the one 3x5 pixel typeface every
sign uses.
`design_handoff_scooter/` (2026-08-27) ships PRODUCTION sheets again: `scooter_rider.png`
(96x96, drop-in grid twin of character.png) and `scooter_parked.png` (48x32, three views)
are shipped assets in `assets/sprites/`; the `*_zoom`/`_onroad` PNGs are doc-only. Its
behaviour spec is proposal-grade and Kevin amended it: the player has the scooter from
the START (acquisition seam deliberately unbuilt), and it is parked outside the
farmhouse every morning regardless of where it was left (overriding the handoff's
"stays where left forever"). CAUTION: both scooter sheets are authored facing RIGHT
and flip for LEFT — mirrored from character.png's left-facing convention.

## Toolchain

- Godot 4.7.2 .NET build: `godot-mono` (installed via Homebrew, app at /Applications/Godot_mono.app)
- .NET SDK 9 (project targets net8.0)

## Commands

- Build (fast correctness check — run after every C# change): `dotnet build`
- Full test suite (headless, 117 tests, exit code 0/1): `godot-mono --headless res://scenes/tests/TestRunner.tscn`
- Re-import after adding/changing assets or scenes: `godot-mono --headless --import`
- Run the game: `godot-mono --path .`
- Screenshot for visual verification (opens a window briefly, saves PNG, quits): `godot-mono --path . -- --screenshot /path/out.png`
  (dev flags: `--start-map <id>` boots into a map; `--spawn <marker>` lands on a named spawn
  instead of the map default, e.g. to frame a corner; `--screenshot-frames <n>` delays the
  capture, e.g. past a beat's staging timer; `--add-minutes <n>` advances the clock in-memory,
  e.g. into shop hours or dusk; `--open-ui <chest|shop|help>` pops a UI after boot;
  `--ride` mounts the scooter after boot, for capturing the riding sprite)
- Edit a map graphically: `godot-mono --path . --editor`, then open `scenes/editor/MapStage.tscn`
  and select the MapStage node. The Haunt Mapper dock (right) picks the map and the time of day;
  drag placements in the viewport, then press Save in the dock — Ctrl+S saves the SCENE, not the
  map. `--screenshot` stays the in-game cross-check.

## Structure

- `src/Core/` — PURE C# (no `using Godot`, test-enforced): GameTime/calendar, ClockModel, GameData + save DTOs, migrations, item/crop defs (code registries), InventoryData, FarmActions, OvernightSim + ShippedLine; storage: StackOps/StorageData/StorageIds; scooter: ScooterData/ScooterRules; shop: ShopCatalog (+ ShopEntry/BuyResult)/ShopHours; story: StoryKeys/IntroRules/BarnRules/MotelRules/MapIds/RoadWrap, DialogueDef(s)/DialogueSession/DialogueSelector, NpcDef(s)/NpcSchedules
- `src/Systems/` — the four autoloads, in registration order: GameState, Clock, SaveService, WorldSim (the single gameplay-mutation bus — all model writes flow through it, incl. story flags, travel requests, the dialogue session, chest/shop Menu sessions, transfers, purchases, and scooter mount/park; UI subscribes to its events)
- `src/World/` — MapRoot base (owns NPC views + IsStandable + IsInterior + the shared
  scatter hash), MapRegistry, the exteriors TestMap (farm) and the ExteriorMap base
  (surface grid + shared town-sheet painters) carrying TownMap and the road strip
  WestEntryMap/BilliesMap/ForkMap/EastForkMap/EastEntryMap plus DriveInMap, and the
  InteriorMap base with TownHallMap/FarmHouseMap/GeneralStoreMap/BarnMap/MotelMap/
  GasStationMap/BilliesBarMap/SalonMap/MotelRoomMap (one class, four registered room ids)
  on it (all programmatic), IInteractable,
  Bed, Sign, ShippingBin, Chest, ShopCounter, Scooter (parked view of GameData.Scooter;
  MapRoot.SyncScooter diffs it), MapExit, Door (flag-lockable: RequiredFlag +
  LockedMessage — a locked handle answers with a line), NpcView, PlaceholderSprites,
  PlaceholderBuilding/RoadBarrier/PitCover/DriveInScreen/DriveInSpeaker (code-built
  stand-ins for buildings with no art yet, chained-off roads, the pit, and the dead
  drive-in);
  signage (motel handoff, all authored in code): PixelFont (the one 3x5 typeface) +
  MotelFacade/MotelSign (the blinking V) + WallBandSign/BracketSign/NeonWordSign/PoleSign,
  and RoadsideTiles/RoadsideTerrain (generated lot/concrete/road source beside the town
  atlas; ExteriorMap's Asphalt/Concrete/Road surfaces need RoadsideTerrain.Get()) +
  StreetLight (the cobra heads);
  art layer: TileSetTools (walkable derivation + blockers) and the three named-coordinate
  tables TerrainTiles/FarmTiles/InteriorTiles with their TileSets TownTerrain/FarmTerrain/
  InteriorTerrain/CropTiles, Prop + the sheets TownProps/FarmBuildings/Furniture,
  StoreFacade/BarnFacade/LampPost, CharacterSprites + CharacterSprite,
  DayNight + DayNightTint + GlowLight;
  recipes: MapRecipe + MapPlacement + PlacementKinds/PlacementFields + MapRecipeFile +
  MapRecipeException, and MapRecipeSeeds (the one-shot exporter that seeds a map's first recipe
  from its C# literals)
- `src/Player/` — PlayerController (movement, tool targeting, hotbar input), InteractionProbe (focus consults CanInteract; guards freed nodes)
- `src/Story/` — StoryDirector (plain Node child of Main, NOT an autoload; runs the scripted beats)
- `src/UI/` — Hud, InteractionPrompt, HotbarUi, StaminaBar, HelpPanel, DialogueUi, ChestUi, ShopUi, OvernightReportUi, PauseMenu, ScreenFade (each builds its own controls in _Ready)
- `src/Tests/` — headless [SimTest] suite + TestRunner; scenes/tests/TestRunner.tscn
- `src/EditorTools/` — MapStage (the [Tool] node that renders a live map in the Godot editor)
  and the pure geometry the mapper needs, PlacementFootprint + PlacementHit (outside `#if TOOLS`
  on purpose, so the headless suite can reach them)
- `addons/haunt_mapper/` — the EditorPlugin: HauntMapper, MapperDock, MapperOverlay, OverlayLayers.
  All of it inside `#if TOOLS`; enabled from project.godot's `[editor_plugins]`
- `src/Main.cs` + `scenes/Main.tscn` — composition root: boot, map loading, sleep + travel flows
- `assets/sprites/` — `character.png`, `lights.png`; `town/` (terrain + its TileSet, both
  facades, props); `farm/` (farm terrain + crops TileSets, farm buildings, barn);
  `interior/` (interior TileSet + furniture). `assets/audio` and `assets/fonts` are still
  empty. Tool-use animations, seasonal variants and animals are still undrawn.
- `data/maps/` — map recipes, one JSON per map id, canonical one-placement-per-line. Only
  `test_farm.json` exists so far; every other map still holds its placements as C# literals.

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
- Chest/shop UIs run in the Menu phase, owned by WorldSim's Open*/Close* sessions
  (OpenStorageId/OpenShopId) — UIs never call TransitionTo themselves; Menu freezes clock
  and player but never tree pause. Modal UIs replicate DialogueUi's _openedFrame guard.
- Storage containers live in GameData.Storages (id -> StorageData); unknown storage keys
  and item ids are preserved verbatim; transfers/purchases go through WorldSim
  (TransferToStorage/TransferToInventory/BuyItem — checks strictly before mutations).
- The overnight report is awaited INSIDE Main.RunSleepFlow while the phase is still
  Sleeping (report before Playing, then a 0.3 s mash-grace); OvernightCompleted itself
  fires mid-advance while the screen is black — latch, never display, in the handler.
- Art rules (from the two handoffs — see their `README.md`s):
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
    story flag: every painted cell goes through `TerrainTiles.ForAct`. No map is rebuilt.
  - Keep `PlaceholderSprites` and the procedural fallbacks working (`Door.DrawPlaceholder`,
    `Bed`/`Chest`/`ShippingBin`'s zero-size `ArtSource`) — they let a new map ship before
    its art exists.
  - Every sheet gets its `walkable` data and its blocker from `TileSetTools`. Only the town
    atlas has a spare transparent cell; the farm borrows it (`FarmTerrain` merges a private
    copy of the town atlas, which is also where the shared woods edge comes from) and the
    interiors add a one-tile transparent source of their own.
  - Interiors are `InteriorMap` subclasses: a room is a layout function — size, wall set,
    floor variants indexed `(x+y) % n`, a door column, and a `Decorate()`. Walls and
    fixtures paint on Obstacles (visual AND collision); furniture is a `Prop` plus a
    blocker; the cobweb is the sheet's only alpha tile and goes on the Dressing layer.
  - Soil is an autotile, so `TestMap.RefreshTile` repaints a five-cell plus, not one cell:
    tilling changes this cell's tile AND its four neighbours' edges.
  - The Crops layer is Y-sorted because crop cells are 16x32 and overhang the row above.
  - The barn's three drawn states are two monotone flags through `BarnRules`, never an int
    in one flag — a flag's value in this model is the day it was stamped, not a level.
    Nothing advances them yet; that seam is deliberately empty.
  - Exactly ONE scooter exists: `GameData.Scooter` is either parked (map + tile + facing)
    or mounted — never both. All writes go through WorldSim (MountScooter /
    DismountScooter / ParkScooterAt); the parked `Scooter` node is a view MapRoot syncs,
    like NPCs. OvernightSim parks it home (farmhouse frontage) on DayEnded — never
    stolen, never lost. Riding is 2x walk speed, never indoors (Main's travel flow
    auto-parks at the door, and load repair re-parks impossible interior states home
    via `MapIds.IsInterior` — a table with a drift guard against each map's
    IsInterior); mounting has NO ceremony (texture swap + speed, no fade).
    Do not add a recall button, a minimap pin, or an auto-return beyond the overnight
    one (handoff design intent: losing track of it is the player's problem).
- Map recipes (`data/maps/*.json`) are CONTENT, not save state — the same bucket as ItemDefs and
  CropDefs, never GameData. Read at map build time, never written at runtime; the editor is the only
  writer. A recipe stores tile coordinates and NAMES, never atlas coordinates and never pixel
  positions, because that is exactly what keeps `ForAct` wrapping every painted cell and `Prop.Anchor`
  owning every anchor. Unknown records round-trip verbatim, like unknown item ids. A map with no
  recipe falls back to its C# literals, so every map stays constructible with no file present.
- `Engine.IsEditorHint()` and `[Tool]` may appear ONLY in `src/EditorTools/` and `addons/`. They are
  verified unnecessary in `src/World/`: the stage hand-instantiates the four autoloads in
  project.godot's order, so map code runs in the editor completely unmodified. If the editor
  misbehaves, fix the stage — never sprinkle guards through the World layer.
- The four TileSet builders (TownTerrain/FarmTerrain/InteriorTerrain/RoadsideTerrain)
  assemble their `.tres` at runtime, and they must (a) build on a PRIVATE
  copy (`CacheMode.Ignore`) and (b) be IDEMPOTENT. Both are load-bearing, and both were learned the
  hard way. `GD.Load` returns the process-cached resource — in the editor, the object the editor owns
  and writes back to disk — so mutating it baked the derived walkable data and a synthesized source
  into the shipped art, which is precisely the hand-listed collision the art rules forbid. And a
  `dotnet build` with the editor open reloads the assembly, clearing the C# static but NOT Godot's
  resource cache, so `Build` re-enters an already-built set. `TileSetReloadTests` guards both.

## Conventions

- C#: file-scoped namespaces, nullable enabled, one class per file, class name matches file
  name (Godot requires this for node scripts). Namespaces mirror folders (`TheHaunt.Core` etc).
- Facing encoding: 0=down, 1=left, 2=right, 3=up. Tiles are 16px; tile (x,y) center = (x*16+8, y*16+8).
- Physics layers: 1 = world/blocking, 2 = interactable areas.
- Scene files: keep hand-authored .tscn minimal (node skeleton + scripts); scripts build their
  visual children in code until real art lands. Maps are NOT becoming .tscn — see the map recipe
  rule above; `MapRegistry`'s and phase3-spec's "becomes PackedScene.Instantiate" comments are
  superseded.
- Code-built Controls: use `SetAnchorsAndOffsetsPreset(...)` to lay out, never `SetAnchorsPreset(...)`
  — the latter keeps the control's current rect (zero for a fresh Control) by compensating offsets,
  which silently produces invisible zero-size UI.
- Pixel-art settings: nearest filtering, 480x270 viewport in a 1280x720 window (30x17 tiles;
  `MapRoot.ViewportWidth/Height` mirrors it for camera limits). Every imported PNG must keep
  Filter: Nearest and Mipmaps: off — one wrong filter is the difference between pixel art and mush.
- UI is built in code at viewport pixel sizes, so it does not rescale with the viewport:
  `gui/theme/default_theme_scale` carries the built-in theme, and explicit font sizes and
  widget constants are tuned by hand. Re-check every UI screenshot if the viewport changes.
- Characters are 16x32 with feet on the bottom row (one tile of floor, one tile of overhang).
  Right is a horizontal flip of left; the sheet holds down/left/up only. EXCEPTION: the two
  scooter sheets are authored facing right and flip for left (`RiderFlipH`/`ParkedFlipH`). Furniture and crops
  follow the same rule: a 16x32 piece stands on its cell and overhangs the one above it.
- A drawn doorway sits one tile ABOVE the facade's bottom row (the bottom row is its stone
  plinth), but the Door node stays on the bottom row: the player walks up to the ground in
  front of the building, not onto its foundation.
- Do not commit `.godot/` (generated) or `export_presets.cfg`.

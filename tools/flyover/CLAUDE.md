# tools/flyover — the intro flyover (Blender)

A 3D model of the whole town, built in Blender by a generator, rendered as the intro
flyover video. Blender 5.2 is driven through the official Blender Lab MCP server
(`mcp__blender__*`) while iterating, and headless (`blender --background --python
tools/flyover/build.py`) for the reproducible build.

## Contracts

- The game is the source of truth for layout. `godot-mono --headless --path . --
  --dump-world <path>` exports every exterior map (surfaces per tile, building
  footprints, props, lights, sign text, exits); the generator reads that file. Never
  transcribe coordinates by hand. The dump is GENERATED: never commit it.
- The exporter and its schema (version 1, doc comment) are `src/World/WorldDump.cs`;
  `WorldDumpTests` pins full coverage, road-mouth pairing and determinism. Ground
  paint the maps solve in code comes through read-only views (kerb cuts, worn cobble,
  the motel's `stall_stripes`, the drive-in's `ramp_rows`).
- Map placement in the stitched world is derived from matching road exits, not
  hand-set (`world.py`; the road wrap is skipped). Each map's frame woods ring (one
  tile, the farm's two) is a 2D camera artifact at the E/W road seams only: east/west
  ring tiles facing another map become Grass. North/south bands and corners stay Woods
  (the farm leaves through its south treeline), as do ring tiles facing open world.
- The build is idempotent: it wipes and regenerates its own collection. The `.blend`
  and rendered frames are build output (git-ignored); only the generator and the final
  video ship.
- Scale: 1 tile = 2.5 m (one constant). Positions come from tiles; building heights
  are believable real-world heights, not the 2D drawn heights.
- Canon only: no PLANNED places (clinic, homes, Stumble Inn, power station, pond), no
  town name, sign lettering only from the canon list in the places' files. No Act I
  dread accents (plum, bile-green, bone). Exceptions Kevin approved are listed under
  Decisions; anything else new asks Kevin first.

## Running the build

- Headless (from the repo root; the dump first):
  `godot-mono --headless --path . -- --dump-world /tmp/world.json`, then
  `/Applications/Blender.app/Contents/MacOS/Blender --background --python
  tools/flyover/build.py -- --world /tmp/world.json [--out <blend>] [--only-map <id>]
  [--render-dir <dir> --px-per-tile 6] [--views] [--diorama]`. `--views` adds the EEVEE
  stills from the named cameras (+ an EEVEE top-down; `--view-names a,b`, `--view-res
  3840x2160`, `--samples 32`, `--no-layout` skips the Workbench stills); `--diorama` builds the Phase 2
  flat per-tile diorama instead of terrain + roads (the layout/fidelity reference).
  Out defaults to `tools/flyover/out/town.blend`
  (git-ignored); an existing out file is reopened and rebuilt in place.
- Live session (MCP execute code): `import sys; sys.path.insert(0,
  "<repo>/tools/flyover"); import build; build.build("<world.json>")` — it reloads the
  package modules (not `build` itself: `importlib.reload(build)` after editing it),
  so edits apply without restarting Blender.
  `build.render_stills(world, dir)` writes the top-down checks (Workbench, flat,
  Standard view transform — exact palette colours) and restores render settings.
- `python3 tools/flyover/world.py <world.json>` runs the stitch + asserts and prints the
  offset table without Blender; `routes.py` likewise. `terrain.py` / `forest.py` need numpy: run
  them with Blender's bundled `Contents/Resources/<ver>/python/bin/python3.*`.
- Modules: `routes` (road rows, kerb cuts, road-out curves, dirt tracks traced from
  kerb cuts, corners filleted; pure Python) -> `terrain` (relief, ruts/ramps/pit,
  adaptive lattice + far ring, `z_at` sampler; numpy) + `treeline` (the clearings'
  irregular edge E, + open: distance to each clearing + scaled bay/tongue noise, used
  ground forced open, kept woods forced wooded; the lone field trees; numpy;
  `treeline.py <dump> out.png` previews it) + `surfaces` (the smooth surface
  fields: per-kind signed distances from the tiles + track bands, blurred, natural
  noise, the treeline deciding woods vs open, argmax partition packed as layer fields
  in three float images; numpy) ->
  `ground` (smooth-shaded meshes), `road` (swept paved road), `markings` (lot paint
  decals), `materials` (one ground look painting the fields, EEVEE), `guides`,
  `templight` (TEMPORARY sun + sky; Phase 8 replaces it), `cameras` (Cam_TopDown +
  named views), `forest` (the planting table: density field, species, colours, LOD;
  numpy; `plant()` asserts the keep-outs on exact geometry), `trees` (the kit: a body
  + a leaf-card prototype per variant, metaball crowns, three materials), `scatter`
  (one point cloud, two Geometry Nodes objects: bodies, and shadowless leaf cards).
- Look conventions (Phase 4b, reuse for stylized-real buildings): real sizes, smooth
  shading, palette colours with noise shifts between palette NEIGHBOURS plus a bump
  height, detail down to centimetres; nothing tile-stepped. Surfaces never meet on a
  tile edge: ask `Terrain.surfaces` (visible / field / own / open_field), not tiles.
  Alpha only where it cannot cast shadow (transparent shadows cost ~18x).
- Layout: everything lives under the `Flyover` collection (Flyover_Ground,
  Flyover_Buildings, Flyover_Props, Flyover_Lights, Flyover_Cameras, Flyover_Guides,
  Flyover_TempLight, Flyover_ForestKit + Flyover_ForestCards (excluded from the view
  layer), Flyover_Forest; the `--diorama` build alone makes Placeholders_*); every datablock the build makes carries the `flyover` ID
  property, and a rebuild removes exactly those. Ground meshes: one per map plus
  Ground_Terrain (outside the maps, forest floor), face material_index =
  `config.SURFACES` order (the tile's surface, for the Workbench layout stills; every
  slot wraps the one ground look, which paints the smooth fields, FO_Surfaces_0..2);
  Road_Paved, Marking_* sit on them. Props / signs / lights carry `map_id` / `dump_id` /
  `kind` custom props and are grounded via `Terrain.z_at`. `placeholders.py` is the
  diorama's layout markers only: no marker ships in the film.
- Routes as data (Phase 4 forest keep-outs, Phase 9 camera): Flyover_Guides holds POLY
  curves Guide_Road (whole centreline), Guide_RoadOut_W/E, Guide_Track_<name>
  (FarmRoad, MansionDrive, ForkSouthStub, DriveInDrive) with a `clear_m` keep-out
  half width, the empty Guide_MansionDriveEnd, and Guide_MansionClearing (cube empty,
  props width_m / depth_m / heading_deg / ground_z: the levelled 34 x 40 m shelf the
  mansion stands on, ringed by tall hemlock / pine).
  Named views: Cam_WestRoad, Cam_Fork, Cam_Plaza, Cam_DriveIn, Cam_EastOut,
  Cam_Overview, Cam_MansionDrive, Cam_FarmTreeline, Cam_Billies, Cam_EastEntry,
  Cam_MansionGlimpse (up the drive, short of the clearing) (positions derived from
  routes/dump).
- Buildings (Phases 5-6): `archkit` (parametric parts as polygon soups: walls with
  openings, roofs incl. `spire`, `overhead_door`, `boarded`, `wall_band`, `tower_walls`,
  `pointed_hood`; prototypes as linked children) painted by `archmats` (keys
  "kind:colour"; wall kinds incl. block / stucco / plywood / boards_old, roofs incl.
  shingle / roll_roofing / corrugated / slate_ruin, ivy); `buildings_hero` (the five with
  art; `Walls`, `realised_info` = hull + tris) and `buildings_town` (the nine placeholder
  buildings, config.TOWN_DESIGNS / TOWN_FACING; the dump's wall colour is the main wall
  colour; door / band / neon / bracket x from the dump; `DESIGN_NOTES` = the lit calls;
  bodies carry `mount_<kind>` world xyz for Phase 7). `mansion` builds the ruin at
  `routes.mansion_site` (back of the clearing, facing down the drive; the clearing is
  config.MANSION_CLEARING_M, deep enough for a forecourt). Every hull joins
  forest.clear_crowns. Light families: windows (flyover_window_glow), neon
  (flyover_neon_glow), and the SIGN LAMP (flyover_sign_glow, LF_SignLamp_*: a LIT wall
  band's letters, cream by day / lantern lit, + the trough lamp under it); each x object
  `glow`. Dark bands are paint.
- Treeline (config "Phase 4c"): only the plain frame of a clearing moves. Forced open
  (a smooth max, so tongue tips round off): non-grass tiles (the road rows to the road's
  tree clearance), footprints + 6 m swept to the doors and the road, props, posts, a
  corridor from each free-standing sign to its nearest route, the screen's sightline.
  Kept wooded exactly as tiled: Woods inside a map's frame ring, a ring's N/S band where
  woods lie behind it or it meets another map's woods (the farm's south treeline),
  TREELINE_KEEP_RING maps (the drive-in), plus a noisy pad of wild ground behind them.
  Bays and tongues saturate (tanh; tongues within 80% of the room to the used ground),
  so no straight clip line; the farm, whose field reaches its frame, scallops outward.
  TREELINE_SEED 20 was picked from a scan for the fewest straight runs.
- Forest (config "Phase 4"): trunks never stand on (or within FOREST_OPEN_MARGIN_M of)
  open ground in the smooth fields, within a guide's
  `clear_m`, within FOREST_BUILDING_MARGIN_M (3 m) of a footprint, 1 m of a prop, 1.5 m
  of a post, or in the clearing (the treeline's field trees stand on the grass, checked
  against its margins instead); crowns (radius up to ~8 m) DO overhang those margins,
  so Phase 5-6 buildings that back onto a treeline (the motel) meet canopy at the
  roof. Deterministic: variant picks walk the families SORTED (a set's order follows the
  string hash seed, which varied the table and the crown drops run to run). The mansion clearing's own `overgrowth` group (young trees + hemlock / maple
  scrub, config.OVERGROWTH_*) stands clear of the house (checked) so from the drive only
  its upper storey and roofs show. Colours live on the instances (FOREST_CROWN / FOREST_BARK palette weights);
  the Workbench layout stills hide the forest. Tiers: full detail within 380 m of the
  bounds / road-outs, the `_lo` kit to 1.2 km, canopy clumps beyond. Autumn colours are
  palette blends (config.TINTS); red only as the rare muted copper/rust tree.

- Props + signs (Phase 7): `propkit` (dump -> metres, `Placer` = realise + ground + stats /
  crown hulls; loft, rock, mound, catenary `chain`, posts), `streetlights` (cobra heads;
  `drive()` = a light's energy driven by a scene property, a plain `v * k` driver that
  evaluates headless), `signs` (pole / bracket / board / for_sale), `props` (every other
  dump prop; an unknown kind raises). Tall props (street lights, pole signs, the screen)
  join forest.clear_crowns; every prop's real XY extent is a forest keep-out
  (`forest.plant(..., extra)`, checked). Lettering only where the game DRAWS it: MOTEL,
  NO VACANCY, FIREWORKS, DRIVE-IN / CLO ED, BAR (+ Phase 6's bands and OPEN); every
  Sign.cs board and the FOR SALE plank are blank. ROADSIDE rule: a board / FOR SALE sign
  just south of a road-facing (TOWN_FACING N) building is mirrored across it to the road
  side. Boards face the road (config.SIGN_FACING overrides); pole signs are double-faced,
  square to their route. Light families now: window (flyover_window_glow), neon
  (flyover_neon_glow: signs' tubes, bulb rail, BAR bulb, their point lights), sign lamp
  (flyover_sign_glow), street (flyover_street_glow: LF_Street lenses + 5 spots), pit
  (flyover_pit_glow: LF_Pit void + Light_Pit_Under / _Leak); lights in Flyover_Lights,
  each `energy = W x property`. The motel's V is its own object (`..._Neon_V_*`, glow 1)
  for Phase 10; NO is glow 0.

## Decisions (Kevin, 2026-09-23)

- South-of-road buildings (gas station, garage, salon, Abe's shack) face the ROAD in
  3D, not the 2D camera.
- Autumn, dusk: mercury-vapour street light, neon and warm windows on, kept separate.
- The film is a "real world" view of the game's town, for video only: the tile layout
  places things, but shapes are naturalistic — smooth curved edges on roads, paths,
  lots and surface boundaries, never tile stair-steps. Output is an HD or 4K video with
  an animated camera; there is no game-resolution/pixel pass. The final render finish
  is still chosen at Phase 8.
- Look: STYLIZED-REAL — real-world proportions, smooth shapes, soft full foliage (not
  faceted low-poly), lighting and atmosphere doing the work, colours palette-led so it
  stays the game's town. EEVEE.
- The mansion: a GLIMPSE of its rooftop through the trees beyond the East Fork's chained
  drive — gothic, in ruins, overgrown (its canon description). Roofline only; the
  building itself stays out of frame.
- The pit: a slight red glow leaks from under its plank cover.
- The road curves out of frame at both ends, into forest (art bible Act I rule).
- Game integration (playing the video in the intro) is a later, separate change.
- (Review of 4b) The town clearing gets an IRREGULAR treeline — bays of field, trees
  reaching toward the road — outside the ground the game uses; buildings, lots, paths
  and props stay clear. The mansion band stays a quiet darker hint. Painterly crowns
  are right: not photoreal.
- Buildings: the game only has front art (and flat placeholders), so the film DESIGNS
  reasonable full buildings — roofs, sides, backs, signs, trim — consistent with the
  fronts where they exist and the building grammar everywhere. Canon lettering only.
- Run the remaining phases through; Kevin reviews at the end (not per phase). Where
  a phase says "Kevin picks/reviews", make the call, record it here, and list it for
  the end review.

## For Kevin's end review (design calls made without asking)

- Town hall: hipped slate roof (the art's flat roof band read as a hip), cupola centred
  on it; 4 bays both storeys aligned front/back, 3 bays on the sides; the double door
  centred on the facade (the art centres it; the collision tile is half a tile west);
  doors shown closed; granite steps with cheek walls; a plain back door + stoop; no
  chimney (none drawn).
- General store: side-gable cedar shake, gable-end attic windows, a back door; drawn
  OPEN (as scene_dusk): lit windows, door ajar on a lit hall. The bracket sign is a
  blank BLADE sign (square to the road) with the two drawn cream rules.
- Motel: the office and strip are real depths (10 / 9 m), not the 17.5 m footprint
  (the 2D footprint blocks the drawn facade's height); lawn behind to the treeline. The
  strip's aqua posts carry a canopy over the concrete walk (the googie stripe is its
  fascia). Room 3 lit (dump RoomGlow). ICE lettered on a plate over the alcove.
  Plumbing vents / ventilators / downspouts / a meter added on the roof and back.
- Farmhouse: 1.5 storeys under a steep hip; the entry porch sits inside the footprint
  (the front wall set back 1.8 m) so nothing crosses the mailbox tile; two gable
  dormers on the back slope; fieldstone chimney through the east hip.
- Barn: gable-FRONT gambrel (the drawn gambrel silhouette), hayloft door in the gable
  above the big doors (drawn just under the eave); derelict = separate grey-brown
  boards with missing / broken ones, roof holes to the rafters, left leaf sagging open
  on one hinge, right leaf gone, loft door hanging, loose planks, mossy base.
- The farm's per-save sample tree 1 tile west of the farmhouse is dropped (its crown
  would pass through the roof).
- Motel aqua #5fb9b0 (motel handoff) is config.ART_COLOURS, used on the motel only.
- (6) OPEN neon: NeonWordSign.cs's #e05a3f lit / #6d4038 dead tube (ART_COLOURS neon-red /
  neon-dead), hung in the window on a dark backing board. At NEON_EMIT 8 under Standard
  it clips to near-white: Phase 8 exposure.
- (6) Lit bands (dump lit: GAS, POLICE, SALON) are a 4th family, the sign lamp
  (flyover_sign_glow, default 0 like neon); GARAGE, HARDWARE, SNACKS stay dark paint.
- (6) Gas station: 1950s flat-roof block box, cream enamel frieze carrying GAS, office
  plate glass + glass door lit, stock room / restroom doors dark, oil tank + flue behind.
  NO pumps, NO canopy (Kevin): forecourt empty. OPEN QUESTION: pumps / canopy?
- (6) Garage: block shop, stepped parapet with dark GARAGE, big bay on the kerb cut +
  a second bay, office window + man door, shed roof behind the parapet, stove chimney.
  All dark (for sale).
- (6) Fireworks stand: board-and-batten on block piers, corrugated shed roof, serving
  hatch SHUT (flap down, padlocked: seasonal, autumn), side door. No light.
- (6) Billie's: low roadhouse, worn clapboard, low shingle side-gable, five small high
  windows lit, solid dark door under a little hood, window AC unit, kitchen lean-to
  (lit window), block chimney. BAR bracket = a wall plate only (mount_bracket).
- (6) Police: painted-render civic box, stone water table / sills / heads, parapet,
  centred double door up granite steps under a concrete hood, lit POLICE band above,
  3 of 4 front windows lit, barred cell windows behind, guyed radio mast (12 m top).
- (6) Hardware: older storefront, square false front + bracketed cornice hiding a low
  gable, recessed centred entry, two display windows on bulkheads, transoms, dark
  HARDWARE band; all dark; double loading door behind.
- (6) Salon: small side-gable clapboard cottage-shop, picture window with OPEN, glass
  door, lit SALON band. Lit, but Sam's hours end at 5 PM (dump onMinutes): if the film's
  dusk is later, Phase 8 sets its windows / neon glow 0.
- (6) Abe's shack: separate grey boards (tinted by the wall colour) on block piers, sagging
  rusty corrugated shed roof with a tar-paper patch and rocks, plank door, one window at a
  faint lamp (glow 0.35), leaning stovepipe, woodpile.
- (6) Concession: boarded 1950s snack bar facing SOUTH (field + screen: its gravel apron
  is south, the cars were south, the drive comes in behind), flat roof run forward as a
  canopy on two posts, dark SNACKS on the fascia, serving windows / doors boarded, and a
  PROJECTION BOOTH on the roof with two ports at the screen (invented, plausible).
- (6) Mansion: dark stone 3-storey block, steep mossy slate side-gable, front cross-gable,
  octagonal corner turret with spire, gable dormer, gable-end chimney stacks (the west
  one broken) + a central stack; roof fallen in at the west end across the ridge (bare /
  snapped rafters), a hole low on the front slope, slates lost on the spire; ivy; all
  windows dark and empty. Placed at the BACK of a clearing enlarged to 34 x 40 m, with a
  planted overgrown forecourt (young trees + scrub) so the drive glimpse shows only the
  upper storey + roofs. Ring-tree heights left as they were (not needed).
- (6) Facing: south-of-road buildings face the road, so their 2D-front board / FOR SALE
  signs (south of the footprint) now stand BEHIND them: Phase 7 moves them roadside.
- (7) Verge street lights reach over the road, square to it (the game's pool lands on the
  road); only the plaza's two use the dump's E / W facing (toward each other). 8.6 m poles.
- (7) Pole signs (motel, FIREWORKS, marquee) are double-faced and square to their route
  (the road; the marquee its drive), not facing the 2D camera. Real sizes: motel cabinet
  3.7 x 3.1 m on a 4.2 m pylon; the bulb rail + BAR bulb ride the neon family (sign circuit).
- (7) The fork's FingerPost (a plain Sign in the game) is a finger post with three BLANK
  arms (N farm, E town, W road); all boards blank-faced, faint illegible smudges only.
- (7) Marquee: letter-board, each glyph on a cream tile; the missing S = a paler patch.
- (7) Pell's sedan: late-50s four-door (fins, hooded lamps, whitewalls), 5.1 m, parked NOSE
  WEST as the dump says, side-on inside room 3's stall (centred between the stripes). The
  alternative: backed in, nose to the road.
- (7) Pit: E-W heavy planks on a timber sill, one plank gone mid-cover + narrow gaps; the
  pit chain gets 5 posts (12.5 m run). Glow tuned subtle (PIT_EMIT 0.45, 60 W under, 18 W
  leak), default 0 like neon; Phase 8 sets it.
- (7) Screen 32 m x 11 m on 4.5 m legs; the art's three legs are three doubled timber
  trestle bents behind the face; the top-right panels gone show the girts. Faces north,
  so in the temp daylight it reads blue-cold (sky-lit): Phase 8.
- (7) Plaza benches face the well (north), the drive-in's the screen; notice board and
  planters face the road; mums in the art's three colours (barn-red, lantern, cream).
- (7) Storm slide: a mud fan spilling off the wooded east side across the dump's 4 debris
  tiles, boulders at the rock tiles, two snapped trunks (butt upslope) at the log tiles.
- (7) The farm's scooter is not in the dump: not built.

## Phases

Each phase ends in a standalone, render-verified state. Tick them off here as they land.

- [x] 1. World export: `--dump-world` in C#, read from the map model; a test that every
  exterior map exports and every exit has a matching reverse exit.
- [x] 2. Scaffold + flat diorama: package, constants, palette, idempotent rebuild,
  exit-derived stitching; one coloured quad per tile. Verify a top-down ortho render
  of each map against an in-game screenshot.
- [x] 3. Ground + roads: surface materials; the paved road (kerbs, gutters, dashed
  centre line, cracks, kerb cuts); dirt roads and stubs; lots with stall stripes;
  gentle relief outside town; the road curving out of frame at both ends.
- [x] 4. Forest: 4-6 low-poly New England species (maple, birch, white pine, hemlock)
  in autumn palette, Geometry Nodes instancing; deep band behind the mansion drive;
  a fixed-seed sample of the farm's per-save trees, stumps and rocks.
- [x] 4b. Naturalise (Kevin review of 3-4): smooth curved edges on every road, track,
  path, lot and surface boundary (no tile stair-steps); the tree kit rebuilt
  stylized-real (full soft crowns, holds up at 4K); brighter, more varied autumn
  golds/ambers/ochres (still no saturated red mass); the mansion band stays dark but
  its edge feathers into the forest.
- [x] 4c. Irregular treeline around the town clearing (see Decisions).
- [x] 5. Hero buildings (real art exists): town hall, general store, motel, farmhouse,
  barn (derelict). Five-band grammar; side-by-side renders against the handoff PNGs.
- [x] 6. Placeholder buildings (flat colours only in-game — Kevin reviews): gas station,
  garage, fireworks stand, Billie's, police, hardware, salon, Abe's shack, concession
  stand. Plus the mansion roofline. No pumps/canopy unless Kevin says so.
- [x] 7. Props + signage: cobra-head street lights (incl. the dead one), pole/bracket/
  wall-band signs, chains + boards, pit cover (plank gaps for the glow), well,
  benches, planters, notice board, fences, mailbox, shipping bin, Pell's sedan,
  FOR SALE board, drive-in screen/speakers/marquee. Lettering in the 3x5 PixelFont.
- [ ] 8. Look-dev: dusk sky + sun, the three light families, the pit glow; test stills in
  both render styles; Kevin picks.
- [ ] 9. Camera + animatic: storyboard with Kevin; path from the west road over the fork
  (look up toward the farm), the plaza, the mansion glimpse, the drive-in, out east.
- [ ] 10. Life (optional): NO VACANCY's V blinking, neon flicker, wind, chimney smoke,
  falling leaves.
- [ ] 11. Final render + encode: PNG frames -> ffmpeg -> Theora `.ogv` (Godot 4's native
  format) + an `.mp4` preview; size check; into `assets/video/`.
- [ ] 12. (Later, separate change) Play it in the intro, skippable, via StoryDirector.

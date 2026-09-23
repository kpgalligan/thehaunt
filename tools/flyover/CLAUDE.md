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
  stills from the named cameras (+ an EEVEE top-down); `--diorama` builds the Phase 2
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
  kerb cuts; pure Python) -> `terrain` (relief, sub-cell surfaces, ruts/ramps/pit,
  adaptive lattice + far ring, `z_at` sampler; numpy) -> `ground` (meshes), `road`
  (swept paved road), `tracks` (forest continuations as draped ribbons), `markings`
  (lot paint decals), `materials` (procedural EEVEE surfaces), `guides`, `templight`
  (TEMPORARY sun + sky; Phase 8 replaces it), `cameras` (Cam_TopDown + named views),
  `forest` (the planting table: density field, species, colours; numpy; `plant()`
  asserts the keep-outs on exact geometry), `trees` (the kit: one low-poly prototype
  per variant + the two instancer-attribute materials), `scatter` (one point cloud +
  one Geometry Nodes modifier: raycast onto the ground meshes, instance the kit).
- Layout: everything lives under the `Flyover` collection (Flyover_Ground,
  Placeholders_Buildings, Placeholders_Props, Flyover_Cameras, Flyover_Guides,
  Flyover_TempLight, Flyover_ForestKit (excluded from the view layer),
  Flyover_Forest); every datablock the build makes carries the `flyover` ID
  property, and a rebuild removes exactly those. Ground meshes: one per map plus
  Ground_Terrain (outside the maps, forest floor), face material_index =
  `config.SURFACES` order; Road_Paved, Track_*, Marking_* sit on them. Placeholders
  carry `map_id` / `dump_id` / `kind` custom props and are grounded via `Terrain.z_at`.
- Routes as data (Phase 4 forest keep-outs, Phase 9 camera): Flyover_Guides holds POLY
  curves Guide_Road (whole centreline), Guide_RoadOut_W/E, Guide_Track_<name>
  (FarmRoad, MansionDrive, ForkSouthStub, DriveInDrive) with a `clear_m` keep-out
  half width, the empty Guide_MansionDriveEnd, and Guide_MansionClearing (cube empty,
  props width_m / depth_m / heading_deg / ground_z: the levelled 30 x 25 m shelf Phase
  6 puts the roofline on, ringed by tall hemlock / pine).
  Named views: Cam_WestRoad, Cam_Fork, Cam_Plaza, Cam_DriveIn, Cam_EastOut,
  Cam_Overview, Cam_MansionDrive, Cam_FarmTreeline (positions derived from
  routes/dump); `--views` also writes Cam_Overview_480 (the game's native resolution).
- Forest (config "Phase 4"): trunks never stand on open map surfaces, within a guide's
  `clear_m`, within FOREST_BUILDING_MARGIN_M (3 m) of a footprint, 1 m of a prop, 1.5 m
  of a post, or in the clearing; crowns (radius up to ~5 m) DO overhang those margins,
  so Phase 5-6 buildings that back onto a treeline (the motel) meet canopy at the
  roof. Colours live on the instances (FOREST_CROWN / FOREST_BARK palette weights);
  the Workbench layout stills hide the forest. Tiers: full detail within 380 m of the
  bounds / road-outs, larger trees to 1.2 km, canopy clumps beyond.

## Decisions (Kevin, 2026-09-23)

- South-of-road buildings (gas station, garage, salon, Abe's shack) face the ROAD in
  3D, not the 2D camera.
- Autumn, dusk: mercury-vapour street light, neon and warm windows on, kept separate.
- The film is a "real world" view of the game's town, for video only: the tile layout
  places things, but shapes are naturalistic — smooth curved edges on roads, paths,
  lots and surface boundaries, never tile stair-steps. Output is an HD or 4K video with
  an animated camera; there is no game-resolution/pixel pass. The final render finish
  is still chosen at Phase 8.
- The mansion: a GLIMPSE of its rooftop through the trees beyond the East Fork's chained
  drive — gothic, in ruins, overgrown (its canon description). Roofline only; the
  building itself stays out of frame.
- The pit: a slight red glow leaks from under its plank cover.
- The road curves out of frame at both ends, into forest (art bible Act I rule).
- Game integration (playing the video in the intro) is a later, separate change.

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
- [ ] 5. Hero buildings (real art exists): town hall, general store, motel, farmhouse,
  barn (derelict). Five-band grammar; side-by-side renders against the handoff PNGs.
- [ ] 6. Placeholder buildings (flat colours only in-game — Kevin reviews): gas station,
  garage, fireworks stand, Billie's, police, hardware, salon, Abe's shack, concession
  stand. Plus the mansion roofline. No pumps/canopy unless Kevin says so.
- [ ] 7. Props + signage: cobra-head street lights (incl. the dead one), pole/bracket/
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

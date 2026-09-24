"""Flyover constants: scale, palette, surface colours, collection names.

Coordinates (every module follows this):
  Blender +X = east, +Y = north, Z up, metres. The dump's tiles run x east, y south.
  A tile (x, y) of a map at global tile offset (ox, oy) has its NORTH-WEST CORNER at
      X = (ox + x) * TILE_M,   Y = -(oy + y) * TILE_M
  and covers X..X+TILE_M, Y-TILE_M..Y. Its centre is corner + (TILE_M/2, -TILE_M/2).
  Map pixels (the dump's "px" pairs) scale the same way: px / TILE_PX tiles, so
  pixel (0, 0) is the map's north-west corner. Ground sits at Z = 0.
"""

TILE_M = 2.5          # 1 tile = 2.5 m, the one scale constant
TILE_PX = 16          # the dump's tilePx (validated on load)
DUMP_VERSION = 1

# The 30-colour palette (docs/designs/design_handoff_town_art/README.md). The Act I
# dread accents (plum, bile-green, bone) are deliberately absent: never use them here.
PALETTE = {
    "ink-900": "#171310", "ink-700": "#2b241d", "ink-500": "#453a2e",
    "cream": "#ede3cb", "stone-pale": "#b8b5a5",
    "green-dark": "#2f5228", "green-mid": "#457539", "green-base": "#4a7c3a",
    "green-light": "#5f9445", "green-pale": "#86ad5c",
    "earth-dark": "#4a3526", "wood-warm": "#6b4a2f", "earth-mid": "#7a5b3c",
    "earth-base": "#8a6a45", "earth-light": "#a5855c",
    "stone-dark": "#3e4241", "stone-shade": "#575a58", "stone-base": "#7a7a7a",
    "stone-light": "#9a9a8a", "barn-red": "#a4432f",
    "sky-day": "#8fb8cf", "water-mid": "#47788c", "water-deep": "#2e5566",
    "skin-base": "#e8c8a0", "skin-shade": "#c49a72",
    "lantern": "#f2b95c", "hair-stock": "#5a4a3a",
}

# Surface kind -> palette colour. The ORDER is the material-slot order on every
# ground mesh, so a face's material_index is SURFACES.index(kind) everywhere.
SURFACE_COLOURS = {
    "Grass": "green-base",
    "Woods": "green-dark",
    "Pasture": "green-light",
    "Dirt": "earth-mid",
    "Path": "earth-light",
    "Gravel": "stone-light",
    "Cobble": "stone-base",
    "Road": "stone-shade",
    "Asphalt": "stone-dark",
    "Concrete": "stone-pale",
}
SURFACES = list(SURFACE_COLOURS)

# Stitching: a frame-ring tile whose edge abuts another map becomes this surface.
SEAM_SURFACE = "Grass"
RING_SURFACE = "Woods"

# The flat diorama's ground plane (--diorama only; the terrain replaces it).
FOREST_FLOOR = "green-dark"
GROUND_MARGIN_M = 400.0     # how far the plane runs past the stitched world's bounds
GROUND_PLANE_Z = -0.05      # just under the tiles, no z-fighting

# ---------------------------------------------------------------------------
# Phase 3: terrain, roads, tracks, markings
# ---------------------------------------------------------------------------

# Everything outside the maps is forest floor (Phase 4 plants it): this surface.
WILD_SURFACE = "Woods"
# Prop kinds the ground itself models (no placeholder box).
GROUND_PROP_KINDS = ("kerb_cut", "worn_cobble", "stall_stripes", "ramp_rows")

SUB = 4                     # sub-cells per tile edge on detailed ground (0.625 m)
NEAR_PAD_TILES = 24         # detailed lattice runs this far past the world bounds (even)
FAR_MARGIN_M = 720.0        # terrain runs at least this far past the world bounds
FAR_CELL_M = 5.0            # far terrain cell (2 tiles)
OUTER_M = 3600.0            # ... then cells grow out to this far past the bounds
OUTER_GROWTH = 1.18
OUTER_MAX_CELL_M = 160.0

# Relief: flat town floor in a valley, forested hills rising away from it, highest to
# the north. From any view the ground climbs to a ridge: never a flat horizon.
FLAT_PAD_M = 6.0            # flat collar around every map
RISE_M = 320.0              # distance over which the hills come up to full height
WALL_EASE_M = 70.0          # the valley wall eases off the flat collar over this
HILL_BASE_M = 36.0          # full hill height south/east/west of the valley
HILL_NORTH_M = 92.0         # extra height to the north (the wilderness)
NORTH_RAMP_M = (-60.0, 460.0)   # north weighting ramps over this Y range past the world's north edge
RIM_SLOPE = 0.05            # keeps rising past RISE_M so the terrain's edge is always a ridge
HILL_WAVES_M = ((520.0, 0.5), (230.0, 0.45), (90.0, 0.22), (34.0, 0.06))   # value-noise fBm (wavelength, amp)
HILL_NOISE_MIX = 0.55       # how much of the hill height the fBm shapes (the rest is the rise)
RIDGE_M = 190.0             # peaks and saddles on the far hills: the skyline is never flat
RIDGE_FROM_M = (200.0, 950.0)   # ... growing in over this distance from the maps
RIDGE_WAVES_M = ((950.0, 0.55), (430.0, 0.33), (170.0, 0.12))
VALLEY_MIN = 0.3            # hill scale right at a road-out (the valley it runs in)
VALLEY_M = (15.0, 210.0)    # valley widens back to full hills over this distance
TRACK_VALLEY_MIN = 0.4      # ... and along a forest track (the mansion drive's hollow)
TRACK_VALLEY_M = (10.0, 150.0)
TERRAIN_SEED = 7

# The paved road (profile offsets from the centreline, metres; z relative to verge).
ROAD_HALF_M = 2.5           # the two road rows
KERB_W_M = 0.2
KERB_H_M = 0.15
GUTTER_W_M = 0.3
CROWN_M = 0.04
CUT_LIP_M = 0.02            # a dropped kerb's lip over the gutter
CUT_TAPER_M = 0.5           # transition kerb length at each end of a cut
SKIRT_URBAN_M = 0.5         # vertical skirt under the kerb (hidden by the verge)
SHOULDER_W_M = 1.0          # rural gravel shoulder
SKIRT_RURAL_M = (2.2, 1.4)  # rural embankment skirt: (out, down)
URBAN_FADE_M = (15.0, 60.0) # kerbs fade to a rural edge over this distance out of town
CORRIDOR_FLAT_M = 6.0       # terrain is levelled to the road this far from its centre
CORRIDOR_BLEND_M = 18.0     # ... and blends back to the hills over this distance
CORRIDOR_CLEAR_M = 8.0      # Phase 4: keep trees this far from the road centreline
DASH_M = (2.5, 2.5)         # centre line dash on / off (one tile each, as in the game)

# The road out of town at both ends: (start s, length, turn degrees, + = left).
ROAD_OUT_LENGTH_M = 660.0
ROAD_OUT_STEP_M = 2.5
ROAD_OUT_BENDS = ((40.0, 190.0, 48.0), (330.0, 200.0, -26.0))
ROAD_OUT_MAX_GRADE = 0.06
ROAD_OUT_SMOOTH_M = 90.0    # moving-average window on the road-out's height
ROAD_OUT_SINK_M = 30.0      # the far end sinks into the ground over this length

# Dirt tracks: 2-tile corridors traced from kerb cuts. Named by the chain they pass
# (dump prop ids); a track that dead-ends at a map's frame continues into the forest.
TRACK_MIN_TILES = 4
TRACK_NAMES = {"MansionChain": "MansionDrive", "SouthChain": "ForkSouthStub",
               "TheaterChain": "DriveInDrive"}
TRACK_INTO_MAP = {"test_farm": "FarmRoad"}      # a track reaching this map is named so
TRACK_EXTEND_M = {"MansionDrive": 95.0, "ForkSouthStub": 22.0}
TRACK_WANDER_DEG = 14.0
MANSION_CLEARING_M = (34.0, 40.0)   # (across, along the drive): the roofline site (Phase 6: deep
                                    # enough for the house + an overgrown forecourt)
MANSION_CLEARING_ENTER_M = 4.0      # the drive's end sits this far inside the clearing
CLEARING_BLEND_M = 14.0             # the clearing's level shelf blends back over this
TRACK_CLEAR_M = 3.5         # Phase 4: keep trees this far from a track centreline
RUT_DEPTH_M = 0.055
DIRT_SINK_M = 0.045
PATH_SINK_M = 0.02
PIT_SINK_M = 0.12
WOODS_SHADE_M = (900.0, 500.0)  # the forest floor darkens to canopy shade from here, over
VERGE_M = (6.0, 10.0)       # the road-outs' grassed verge fades out over this distance
LITTER_M = 6.0              # fallen leaves reach this far out of the woods onto open ground

# Phase 4b: the surfaces as smooth fields (surfaces.py). The tiles place things; the
# shapes are natural: no tile stair-steps anywhere.
SURF_PX_M = 0.3125          # the field raster (half a ground sub-cell)
SURF_CAP_M = 6.0            # signed distances are exact up to (and clamped at) this
# Every surface kind as a field, bottom -> top (the layer order the shader paints in):
# (surface, blur sigma m, baked edge noise m, shader edge softness m). A point shows the
# kind whose field is highest (a partition: no gaps, no slivers); the blur rounds
# corners and leaves straight edges where they are; each kind's own noise makes its
# edges wander (a boundary moves by the mean of its two sides), calmed next to the
# built kinds (config.SURF_BUILT) so a lot or walk keeps a clean straight edge.
SURF_KINDS = (("Woods", 1.6, 2.2, 0.3), ("Grass", 1.4, 1.6, 0.3), ("Pasture", 1.4, 1.6, 0.3),
              ("Dirt", 0.7, 0.7, 0.1), ("Path", 0.6, 0.55, 0.08), ("Gravel", 0.6, 0.45, 0.06),
              ("Asphalt", 0.7, 0.0, 0.012), ("Concrete", 0.45, 0.0, 0.012),
              ("Cobble", 0.45, 0.0, 0.012), ("Road", 0.0, 0.0, 0.012))
SURF_BUILT = ("Asphalt", "Concrete", "Cobble", "Road")
SURF_BUILT_CALM_M = (0.4, 3.5)  # natural edge noise fades in over this distance from them
SURF_NOISE_WAVES_M = ((13.0, 0.5), (5.0, 0.32), (1.9, 0.18))
SURF_DETAIL_M = (1.4, 0.12)     # shader-only edge wobble (wavelength, amplitude); < open margin
TRACK_FILLET_M = 3.5        # centreline radius at a traced corner (within ~half a tile)
TRACK_HALF_M = 2.5          # the band's half width (the two tiles)
TRACK_RUT_M = (0.75, 0.33)  # rut offset from the centreline, width
TRACK_FADE_M = 12.0         # a forest continuation narrows and grasses over at its end
TRACK_ROAD_OVERLAP_M = 3.0  # the band starts this far back under the road (no gap at the kerb)
FOREST_OPEN_MARGIN_M = 0.35 # trunks stand where the open field is below -this (exact check)
SURF_MAX_SLOPE = 2.2        # bound on the fields' slope (m/m: the edge noise steepens them)

# Drive-in ramps (rows from the dump's ramp_rows): crest height, run-up, drop.
RAMP_H_M = 0.55
RAMP_RISE_M = 7.5           # north of the crest (cars face the screen, south)
RAMP_DROP_M = 1.8
MARK_LIFT_M = 0.02          # paint decals sit this far above the ground
RAMP_DASH_M = (1.4, 3.4)    # faint ramp-line dash length, pitch

# ---------------------------------------------------------------------------
# Phase 4: the forest (forest.py plants, trees.py builds the kit, scatter.py instances)
# ---------------------------------------------------------------------------

COL_FOREST = "Flyover_Forest"
COL_FOREST_KIT = "Flyover_ForestKit"   # the prototypes (excluded from the view layer)
COL_FOREST_CARDS = "Flyover_ForestCards"   # their leaf cards (excluded; a shadowless cloud)
FOREST_SEED = 11
# The kit, in instance-index order: (family, variants). trees.py builds one prototype
# per variant ("FK_<nn>_<family>_<A..>", origin at the trunk base, metres at scale 1).
# A `<family>_lo` is the same tree at a coarser tessellation (tier B, saplings).
FOREST_KIT = (("maple", 3), ("birch", 2), ("oak", 2), ("pine", 2), ("hemlock", 2),
              ("bare", 2), ("maple_lo", 2), ("birch_lo", 1), ("oak_lo", 1), ("pine_lo", 1),
              ("hemlock_lo", 1), ("clump", 3), ("stump", 1), ("boulder", 2), ("tuft", 2),
              ("leaves", 1))
TREE_FAMILIES = ("maple", "birch", "oak", "pine", "hemlock", "bare")


def _rgb(c):
    h = PALETTE.get(c, c)
    return [int(h[i:i + 2], 16) for i in (1, 3, 5)]


def _mix_hex(a, b, t):
    return "#" + "".join(f"{round(x + (y - x) * t):02x}" for x, y in zip(_rgb(a), _rgb(b)))


def _brighten(a, k):
    return "#" + "".join(f"{min(255, round(x * k)):02x}" for x in _rgb(a))


# Autumn tints DERIVED from the palette (blends of palette neighbours, one brightened
# lantern): New England peak foliage led by the palette's golds and ambers. Leaves only;
# nothing else uses them.
TINTS = {
    "gold-bright": _brighten("lantern", 1.05),                  # lantern, sunlit
    "lemon": _brighten(_mix_hex("lantern", "green-pale", 0.14), 1.08),   # birch yellow
    "amber": _mix_hex("lantern", "barn-red", 0.22),             # orange-leaning amber
    "deep-amber": _mix_hex("lantern", "barn-red", 0.38),
    "ochre": _mix_hex("lantern", "earth-base", 0.45),
    "yellow-green": _mix_hex("green-pale", "lantern", 0.5),     # still turning
    "copper": _mix_hex("barn-red", "earth-light", 0.4),         # muted, a few trees only
    "rust": _mix_hex("barn-red", "earth-mid", 0.45),
}

# Palette colours per family, (name, weight), BRIGHT -> DARK (a spatially coherent pick
# walks this list, so neighbouring trees share a hue). Peak foliage: golds, ambers and
# ochres, some yellow-green and still-green trees, conifers dark green, birch bright
# yellow. NO saturated red mass: the art bible keeps restored barn red the only one in
# the game, so red appears only as the rare muted copper / rust tree (FOREST_RUST_P).
FOREST_CROWN = {
    "maple":   (("gold-bright", 0.2), ("lantern", 0.22), ("amber", 0.2), ("deep-amber", 0.08),
                ("ochre", 0.1), ("yellow-green", 0.12), ("green-base", 0.08)),
    "birch":   (("gold-bright", 0.42), ("lemon", 0.38), ("lantern", 0.14), ("yellow-green", 0.06)),
    "oak":     (("amber", 0.12), ("ochre", 0.26), ("deep-amber", 0.18), ("earth-light", 0.06),
                ("yellow-green", 0.15), ("green-base", 0.08), ("green-mid", 0.14)),
    "pine":    (("green-base", 0.12), ("green-mid", 0.44), ("green-dark", 0.44)),
    "hemlock": (("green-mid", 0.15), ("green-dark", 0.85)),
    "bare":    (("earth-dark", 1.0),),
    "stump":   (("earth-light", 1.0),),          # the cut face
    "boulder": (("stone-base", 0.45), ("stone-light", 0.25), ("stone-shade", 0.3)),
    "tuft":    (("earth-light", 0.3), ("green-base", 0.15), ("green-mid", 0.4), ("green-dark", 0.15)),
    "leaves":  (("gold-bright", 0.2), ("amber", 0.3), ("ochre", 0.25), ("earth-light", 0.25)),
}
FOREST_BARK = {
    "maple": (("wood-warm", 0.5), ("earth-dark", 0.3), ("stone-shade", 0.2)),
    "birch": (("cream", 0.6), ("stone-pale", 0.4)),
    "oak": (("earth-dark", 0.5), ("stone-shade", 0.3), ("wood-warm", 0.2)),
    "pine": (("earth-mid", 0.4), ("wood-warm", 0.4), ("earth-dark", 0.2)),
    "hemlock": (("earth-dark", 0.6), ("wood-warm", 0.4)), "bare": (("stone-shade", 0.5), ("earth-dark", 0.5)),
    "stump": (("wood-warm", 1.0),), "boulder": (("stone-base", 1.0),), "tuft": (("green-mid", 1.0),),
    "clump": (("earth-dark", 1.0),), "leaves": (("earth-dark", 1.0),),
}
# crown2: the crown's second tone (patches within it, its inner and lower leaves); a
# boulder's moss, a tuft's tips, the other half of the fallen leaves.
FOREST_SHADE = {
    "gold-bright": "lantern", "lantern": "ochre", "lemon": "yellow-green", "amber": "deep-amber",
    "deep-amber": "ochre", "ochre": "earth-light", "yellow-green": "green-base",
    "copper": "rust", "rust": "earth-mid",
    "earth-light": "ochre", "earth-base": "earth-mid",
    "earth-mid": "wood-warm", "wood-warm": "earth-dark", "earth-dark": "ink-500",
    "green-pale": "green-base", "green-base": "green-mid", "green-mid": "green-dark",
    "green-dark": "green-dark", "stone-light": "green-mid",
    "stone-base": "green-dark", "stone-shade": "green-dark", "stone-dark": "green-dark",
}
FOREST_RUST_P = 0.012       # the few muted copper / rust maples and oaks
RUST_CROWN = ("copper", "rust")             # (crown, crown2)
DARK_BAND_CROWN = (("green-dark", 1.0),)    # conifers deep in the mansion band ...
DARK_BAND_SHADE = "green-dark"              # ... all the way down

# Tiers: (cell m, keep, scale). Full-detail trees within TIER_A of the world bounds or
# the road-outs, bigger sparser trees to TIER_B (both "full" canopy), then canopy
# clumps out to the terrain's edge. Tiers cross-fade over +-blend metres.
FOREST_TIER_A = (6.0, 0.82, 1.0)
FOREST_TIER_B = (9.0, 0.86, 1.08)
FOREST_TIER_C = (24.0, 0.9, 1.0)
FOREST_TIER_A_M, FOREST_TIER_A_BLEND = 380.0, 80.0
FOREST_TIER_B_M, FOREST_TIER_B_BLEND = 1200.0, 150.0
FOREST_FIELD_CELL_M = 20.0  # the coarse grid tier distance and elevation are sampled on
FOREST_EDGE_INSET_M = 40.0  # clumps stop this far inside the terrain's edge

# The open raster and the treeline.
FOREST_CELL_M = 1.25        # open raster cell (two ground sub-cells)
FOREST_EDGE_CAP_M = 30.0    # edge distance is exact up to here
FOREST_BUILDING_MARGIN_M = 3.0
FOREST_PROP_MARGIN_M = 1.0
FOREST_POST_RADIUS_M = 1.5  # street lights and sign posts
FOREST_SETBACK_M = (0.2, 1.8)       # trunks stand back min + noise * range from open ground
FOREST_EDGE_GROW_M = 14.0   # trees reach full size this far into the woods ...
FOREST_EDGE_MIN = 0.62      # ... from this fraction at the treeline
UNDER_BAND_M = 7.0          # understorey band depth past the setback
UNDER_CELL_M = 2.0
UNDER_KEEP = 0.55
UNDER_SCALE = (0.25, 0.45)

# Species mix (stands via low-frequency noise).
CONIFER_BASE = 0.26
CONIFER_UPHILL = (40.0, 160.0, 0.16)    # more conifers from this elevation to that, +p
CONIFER_PATCH = 0.65        # +-p/2 from the stand noise (other dark stands: the band is one of many)
# Kit heights are real (maple ~19 m, oak ~18, birch ~14, white pine ~24, hemlock ~20),
# so these spread them over believable ranges.
SPECIES_SCALE = {"maple": (0.8, 1.15), "birch": (0.8, 1.2), "oak": (0.82, 1.25),
                 "pine": (0.85, 1.2), "hemlock": (0.8, 1.15), "bare": (0.8, 1.15),
                 "clump": (0.85, 1.2), "stump": (0.85, 1.15), "boulder": (0.55, 1.3),
                 "tuft": (0.7, 1.3), "leaves": (0.8, 1.4)}
FOREST_SINK_M = {"tree": 0.3, "clump": 2.5, "stump": 0.08, "boulder": 0.15, "tuft": 0.03,
                 "leaves": 0.0}

# The mansion: a deep, dark band along the drive past its chain, tall trees ringing the
# clearing (Guide_MansionClearing) so later only the roofline shows through.
BAND_M = (25.0, 150.0)      # full band within, feathering out by (edge wobbled by noise)
BAND_WOBBLE_M = 30.0        # ... at two scales (BAND_WOBBLE_WL_M), so it has no outline
BAND_WOBBLE_WL_M = (70.0, 23.0)
BAND_CONIFER = 0.75
BAND_HEMLOCK = 0.85
BAND_BARE = 0.22            # dead snags among the band's hardwoods
BAND_SCALE = 1.2
RING_M = 14.0               # the tall ring's depth around the clearing
RING_SETBACK_M = 3.5        # ring trunks stand back so their crowns meet the clearing's edge
RING_SCALE = (1.15, 1.35)
RING_PINE = 0.35

# The farm's per-save sample (dump "sample": true), placed exactly.
FARM_TREE_SCALE = (0.72, 0.82)
FARM_BARE_SCALE = 0.9
FOREST_PROP_KINDS = ("tree", "stump", "rock")   # sample props the forest places (no marker)

# Ground dressing.
BOULDER_CELL_M = 14.0
BOULDER_KEEP = 0.22
TUFT_ROAD_V_M = (4.2, 3.2)  # road-out verge tufts: min offset from the centreline + range
TUFT_ROAD_STEP_M = 1.2
TUFT_ROAD_KEEP = 0.55
TUFT_TOWN_V_M = (2.95, 1.4) # in-town verge tufts, past the kerb
TUFT_TOWN_KEEP = 0.14
TUFT_EDGE_CELL_M = 0.8      # the open side of a treeline
TUFT_EDGE_M = 3.5
TUFT_EDGE_KEEP = 0.5
LEAF_EDGE_CELL_M = 0.9      # fallen-leaf scatters on open ground near a treeline (town only)
LEAF_EDGE_M = 7.0
LEAF_EDGE_KEEP = 0.55
LEAF_ROAD_CLEAR_M = 4.6     # past the kerb / rural shoulder
TUFT_FIELD_MAP = "drive_in" # "weeds thickest at the edges" of the drive-in's field
TUFT_FIELD_CELL_M = 1.0
TUFT_FIELD_M = 2.5
TUFT_FIELD_KEEP = 0.55

# Phase 4c: the clearings' irregular treeline (treeline.py). Only the plain frame of
# each clearing moves; everything the game uses stays open, the tree lines the game
# draws stay wooded.
TREELINE_SEED = 20
TREELINE_CELL_M = 1.25      # the field grid (divides the tile)
TREELINE_DIST_CAP_M = (45.0, 70.0)  # clearing distance exact to (inside, outside)
TREELINE_MIN_TILES = 30     # smaller open regions are not clearings
# The edge noise, (wavelength m, outward amp m, inward amp m) at scale 1 (the town
# strip): broad bays tens of metres deep, narrower tongues of trees, then scallops and a
# few-metre wobble (the surface fields add their own metre-scale wobble on top).
TREELINE_WAVES_M = ((130.0, 50.0, 20.0), (70.0, 26.0, 24.0), (36.0, 12.0, 30.0), (16.0, 4.0, 14.0),
                    (6.5, 1.6, 1.8))
TREELINE_SCALE_POW = 0.3    # a clearing's scale = (area / the largest's) ** this ...
TREELINE_SCALE_CLAMP = (0.35, 1.0)
TREELINE_BAY_MAX_M = 40.0  # bays saturate (tanh) toward this depth past the frame (* scale)
TREELINE_TONGUE_MAX_M = 22.0   # ... and tongues toward this depth into the clearing,
TREELINE_TONGUE_ROOM = 0.8     # at most this share of the room before the used ground,
TREELINE_TONGUE_WL_M = 26.0    # varying along the edge at this wavelength (fingers)
TREELINE_SEP_M = 4.0        # two clearings' bays stay 2x this apart (woods between)
TREELINE_SMOOTH_M = 6.0     # the used ground's smooth max (a tongue's tip rounds off)
# Used ground: forced open to this margin (m) past it.
TREELINE_USED_M = {"default": 4.0, "Pasture": 1.5, "Road": 9.5}   # per tile surface
TREELINE_BUILDING_M = 6.0   # around a footprint
TREELINE_DOOR_M = 12.0      # swept out from the door side (and toward the road) ...
TREELINE_SWEEP_M = 4.0      # ... with this margin (also the drive-in's screen sightline)
TREELINE_PROP_M = 3.0
TREELINE_POST_M = 4.0       # street lights and sign posts
TREELINE_SIGN_M = 4.0       # a sign's face: a corridor to its nearest route, this wide
TREELINE_SIGN_REACH_M = 40.0
TREELINE_KEEP_RING = ("drive_in",)  # maps whose whole frame ring stays wooded
TREELINE_KEEP_PAD_M = 14.0  # the wild ground behind kept woods stays wooded this deep (noisy)
# Lone trees and small clumps out in the open grass.
ISLE_CELL_M = 30.0
ISLE_KEEP = 0.75
ISLE_CLUMP_P = 0.35
ISLE_CLUMP_N = (2, 5)       # members of a clump (numpy integers: high exclusive)
ISLE_CLUMP_R_M = 4.5
ISLE_OPEN_M = 9.0           # at least this far out of the woods
ISLE_USED_M = 3.0           # and this far past every used margin
ISLE_ROAD_M = 20.0          # and this far from the road's centreline
ISLE_SCALE = (0.8, 1.05)    # field-grown: full size

# ---------------------------------------------------------------------------
# Phase 5: the hero buildings (buildings_hero.py designs them from archkit.py parts,
# painted by archmats.py)
# ---------------------------------------------------------------------------

COL_HERO = "Flyover_Buildings"
HERO_ART = ("town_hall", "general_store", "motel", "farmhouse", "barn")   # dump "art" values
# Colours the art handoffs add beyond the 30: the motel's googie aqua (motel handoff,
# MotelFacade.NeonAqua) - the motel's stripe, posts and aqua doors only.
ART_COLOURS = {"aqua": "#5fb9b0"}
# The three light families never mix; each is ONE scene custom property (read through a
# View Layer attribute) times a per-object `glow` (0 dark / 1 lit):
#   amber windows ("amber only indoors")  -> WINDOW_GLOW_PROP, materials LF_Window_*
#   neon (signs, the googie tube, vending) -> NEON_GLOW_PROP,   materials LF_Neon_*
#   mercury-vapour street light            -> STREET_GLOW_PROP, LF_Street_* + spot lights (Phase 7)
#   (the sign lamp and the pit glow are families of their own: SIGN_GLOW_PROP, PIT_GLOW_PROP)
WINDOW_GLOW_PROP = "flyover_window_glow"
WINDOW_GLOW = 1.0           # the family's level at full dusk (Phase 8: x its flyover_dusk gate)
WINDOW_EMIT = 0.75          # emission strength at glow 1 (Phase 8: amber, not blown, under AgX)
NEON_GLOW_PROP = "flyover_neon_glow"
NEON_GLOW = 1.0             # lit at dusk (Phase 8; gated by flyover_dusk)
NEON_EMIT = 8.0
# A LIT wall band (WallBandSign: cream letters by day, lantern after dusk, lit from
# below) is its own family, the sign lamp: its letters + the trough lamp under the band.
SIGN_GLOW_PROP = "flyover_sign_glow"
SIGN_GLOW = 1.0             # lit at dusk (Phase 8; gated by flyover_dusk)
SIGN_EMIT = 3.0

# ---------------------------------------------------------------------------
# Phase 6: the placeholder buildings, designed (buildings_town.py) + the mansion
# roofline (mansion.py)
# ---------------------------------------------------------------------------

# dump building id -> design (placeholders with no art: flat wall colour in-game)
TOWN_DESIGNS = {"GasStation": "gas_station", "Garage": "garage", "FireworksStand": "fireworks_stand",
                "Bar": "billies", "PoliceStation": "police", "HardwareStore": "hardware", "Salon": "salon",
                "Shack": "shack", "Concession": "concession"}
# Which side each faces in 3D (Kevin: south-of-road buildings face the ROAD, north);
# the rest face south (the road, or the drive-in's field and screen).
TOWN_FACING = {"GasStation": "N", "Garage": "N", "Salon": "N", "Shack": "N"}
# The window mount's neon word (NeonWordSign.cs): lit tube colour (the dump's glow
# colour for it) and the dead tube by day.
ART_COLOURS["neon-red"] = "#e05a3f"
ART_COLOURS["neon-dead"] = "#6d4038"
# The mansion (a glimpse of its roofline, Guide_MansionClearing): main block across x
# along, eave, ridge; the turret's top.
MANSION_BLOCK_M = (19.0, 13.0)
MANSION_EAVE_M = 11.0
MANSION_MASS_M = (-0.6, -3.7, 2.9, 0.7)    # the mass past the block: (west, front, east, back) m
MANSION_BACK_M = 4.0        # the block's back wall from the clearing's far edge
# The overgrowth (forest.py): young trees filling the clearing round the house, so from
# the drive only the upper storey and the roofs show over them.
OVERGROWTH_CELL_M = 2.7
OVERGROWTH_KEEP = 0.8
OVERGROWTH_SCALE = (0.38, 0.62)
OVERGROWTH_SCRUB_SCALE = (0.16, 0.3)
OVERGROWTH_WALL_M = 2.2     # trunks this far from the mass (crowns then clear it: clear_crowns)
OVERGROWTH_DRIVE_M = 6.0    # the drive's end keeps this much open
MANSION_SEED = 13

# ---------------------------------------------------------------------------
# Phase 7: props (props.py), signs (signs.py), street lights (streetlights.py)
# ---------------------------------------------------------------------------

COL_PROP_ART = "Flyover_Props"      # every designed prop / sign / street light
COL_LIGHTS = "Flyover_Lights"       # the Blender lights the light families drive
# The mercury-vapour street light: the 5th family. Lens emission (LF_Street_*) and one
# spot light per LIT head, both = object / light `glow` x the scene's STREET_GLOW_PROP
# (the spot's energy by a driver). StreetLight.cs's cold blue-green, deliberately
# outside the warm palette (motel handoff), as art colours.
STREET_GLOW_PROP = "flyover_street_glow"
STREET_GLOW = 1.0           # lit at dusk (Phase 8; gated by flyover_dusk)
STREET_EMIT = 6.0
STREET_LIGHT_W = 1500.0     # spot energy at glow 1 (Phase 8: a readable pool on the road)
STREET_SPOT_DEG = (118.0, 0.55)     # spot size, blend: a directed cone, not a radial
STREET_CONE_SPREAD = 0.5    # the visible cone's radius at the ground / the lens height (Phase 8)
STREET_CONE_EMIT = 0.15     # its emission at the core, just under the lens
ART_COLOURS["mercury"] = "#afe6e1"      # StreetLight.Mercury (175, 230, 225)
ART_COLOURS["mercury-cone"] = "#bee6e1"  # ConeTop (190, 235, 230): the lens
STREET_POLE_M = 8.6         # base to the arm's root (aluminium, tapered)
STREET_ARM_M = 2.6          # the mast arm's reach
# The pit (Kevin: "a slight red glow coming from the pit"): the 6th family, its own
# property. A hidden point light under the planks + the void's faint emission.
PIT_GLOW_PROP = "flyover_pit_glow"
PIT_GLOW = 1.0              # the faint glow (Phase 8; gated by flyover_dusk)
PIT_EMIT = 0.45
PIT_LIGHT_W = 25.0
ART_COLOURS["pit-red"] = "#b8402c"     # barn-red toward the neon red: an ember, not a beacon
# Signs that hang lights of their own on the NEON family (the sign circuit: the motel's
# red spill and bulb rail, the BAR bulb): point lights, energy = W x flyover_neon_glow.
NEON_SPILL_W = 25.0
BULB_W = 8.0
# Which way a free-standing board faces. Default: toward the paved road (north of it
# faces S, south of it N). Overrides: the storm blockade is read from the farm side.
SIGN_FACING = {"BlockadeSign": "N"}
# Pole signs are double-faced cabinets square to the route they serve (read from a
# moving car both ways); the drive-in's marquee is square to its drive.
POLE_SIGN_PX_M = {"MotelSign": 0.05, "FireworksPole": 0.07, "Marquee": 0.08}   # metres per sign pixel
POLE_SIGN_BOTTOM_M = {"MotelSign": 4.2, "FireworksPole": 3.0, "Marquee": 2.6}   # cabinet underside
PROP_SEED = 17

# ---------------------------------------------------------------------------
# Phase 8: look-dev (look.py: sun, sky, haze, grade, finishes) + output.py (render
# presets). The film's canonical lighting state is the game's at FILM_MINUTE.
# ---------------------------------------------------------------------------

FILM_MINUTE = 720           # 18:00, DayNight.cs's dusk key ("lanterns light here")
SHOP_HOURS = (180, 660)     # Core/ShopHours.cs 9-5: the general store's open / closed facade
# The time of the light: one scene property, 0 = 16:00 (the last day key, late
# afternoon) .. 1 = 18:00 (dusk). It drives the sun, sky, haze and every family's gate.
DUSK_PROP = "flyover_dusk"
# Each family's on-ramp over flyover_dusk (emission and lights = object glow x family
# level x gate): the game's LightLevel ramps windows / street lights in from 16:00 to
# 18:00; DayNight.SignsLit hard-cuts neon, sign bulbs and lit bands at 18:00 (a cut
# over the last 2.5% so a flight through it reads as the signs flicking on).
DUSK_GATE = {WINDOW_GLOW_PROP: (0.0, 1.0), STREET_GLOW_PROP: (0.0, 1.0), PIT_GLOW_PROP: (0.0, 1.0),
             NEON_GLOW_PROP: (0.97, 0.995), SIGN_GLOW_PROP: (0.97, 0.995)}
FINISH = "clear_dusk"       # the default finish (look.FINISHES; build.py --finish)
COL_LOOK = "Flyover_Look"   # the sun (the world and the compositor tree are datablocks)

# Trees: a crown may overhang a roof but never pass through a building (buildings_hero
# ray-casts each building into a height field; forest.clear_crowns drops the trees
# whose kit geometry would reach into it, with this clearance).
HULL_CELL_M = 0.5
CROWN_CLEAR_M = 0.4

# Named perspective stills (Phase 3 checks; Phase 9 builds the real path).
VIEW_LENS_MM = 35.0
CLIP_END_M = 9000.0

# Phase 9: the flight (flight.py holds the storyboard: beats, camera / aim keys, dusk).
FLIGHT_FPS = 30
FLIGHT_CAMERA = "Cam_Flight"
FLIGHT_LENS_MM = 35.0
FLIGHT_NEAR_M = 0.5             # the lens's radius for every check (clip_start is 0.3)
FLIGHT_CLEAR_M = {"tree": 3.0, "solid": 1.5, "ground": 2.5}   # clearance past the radius
FLIGHT_PATH_SMOOTH_M = 6.0     # the path through the camera keys is blurred by this (m)
FLIGHT_SPEED_SMOOTH_S = 1.2    # the keys' segment speeds are blurred by this (s): speed only eases
FLIGHT_AIM_SMOOTH_S = 0.8      # the view's turns between targets are blurred by this (s)
FLIGHT_FLOOR_SMOOTH_S = 1.6     # a lift over the clearance floor is spread over this
FLIGHT_BANK = (0.45, 4.0)       # bank = gain x the turn's lean atan(a_lat / g), capped (deg)
FLIGHT_MAX_TURN_DEG_S = 40.0    # asserted: the view never swings faster

# Collections added in Phase 3.
COL_GUIDES = "Flyover_Guides"

# The Phase 2 layout markers (placeholders.py): the --diorama build only.
BUILDING_HEIGHT_M = 3.0
ART_BUILDING_COLOUR = "cream"   # art buildings carry no wall colour in the dump
PROP_COLOURS = {                # palette names, or None = take the dump's colour
    "car": None, "tree": "green-pale", "stump": "wood-warm", "rock": "stone-base",
    "kerb_cut": "stone-pale", "worn_cobble": "stone-light", "debris": "earth-dark",
    "chain": "ink-500", "pit_cover": "wood-warm", "fence": "earth-light",
    "screen": "cream", "speaker": "ink-700", "well": "stone-light",
    "bench": "wood-warm", "planter": "earth-base", "notice_board": "earth-base",
    "mailbox": "stone-dark", "shipping_bin": "wood-warm", "scatter": "earth-dark",
}
PROP_DEFAULT_COLOUR = "earth-base"
PROP_HEIGHTS_M = {              # marker heights; flat ground details are slabs
    "kerb_cut": 0.04, "worn_cobble": 0.04, "pit_cover": 0.15, "car": 1.4,
    "tree": 6.0, "screen": 8.0, "fence": 1.1, "chain": 0.8, "speaker": 1.2,
}
PROP_DEFAULT_HEIGHT_M = 0.8
STREET_LIGHT_HEIGHT_M = 7.0
LIGHT_LIT_COLOUR = "lantern"
LIGHT_DEAD_COLOUR = "ink-700"
SIGN_COLOUR = "cream"
SIGN_HEIGHT_M = 1.8

# Collections. Everything the build makes lives under ROOT; a rebuild deletes ROOT's
# tree and every datablock tagged with TAG_PROP, and nothing else.
ROOT = "Flyover"
COL_GROUND = "Flyover_Ground"
COL_BUILDINGS = "Placeholders_Buildings"
COL_PROPS = "Placeholders_Props"
COL_CAMERAS = "Flyover_Cameras"
TAG_PROP = "flyover"

CAMERA_TOPDOWN = "Cam_TopDown"
CAMERA_HEIGHT_M = 500.0
FRAME_PADDING = 1.04    # ortho framing slack


def hex_rgba(hex_colour, alpha=1.0):
    """'#rrggbb' -> linear-light RGBA (Blender material colours are linear)."""
    h = hex_colour.lstrip("#")
    srgb = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    lin = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb]
    return (*lin, alpha)


def colour(name_or_hex):
    """A palette name, an art colour (ART_COLOURS), a foliage tint (TINTS) or a raw
    '#rrggbb' from the dump -> hex."""
    if name_or_hex.startswith("#"):
        return name_or_hex
    return PALETTE.get(name_or_hex) or ART_COLOURS.get(name_or_hex) or TINTS[name_or_hex]

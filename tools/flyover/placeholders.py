"""Placeholder markers for everything that stands on the ground: buildings as boxes of
their blocked FOOTPRINT (not the art buildings buildings_hero.py designs), props / lights / signs as small boxes and posts. All share
two unit meshes (scene.unit_box / unit_cylinder) scaled per object, with the colour
linked per object. Phases 5-7 replace them; each object carries its dump identity
(map_id, dump_id, kind, ...) as custom properties so a replacement can find its slot.

Grounding: given a ground sampler (terrain.Terrain), a box sits at the LOWEST ground
height under its footprint and grows by the footprint's height range, so it never
floats or sinks where the ground slopes (ramps, ruts, the pit); a post is sunk 5 cm.
Kinds the ground models itself (config.GROUND_PROP_KINDS) get no marker, nor (when the
forest is built) the farm's per-save sample trees, stumps and rocks: forest.py plants them.
"""

import config
import scene

GLOW_SIZE_M = 0.45
GLOW_Z_M = 2.2
POLE_DIAMETER_M = 0.25
SIGN_W_M, SIGN_D_M, SIGN_H_M = 1.2, 0.2, 0.7
BUILDING_SIGN_Z_M = config.BUILDING_HEIGHT_M - 0.9   # wallband / bracket / neon


def _corner(placed, x, y):
    t = config.TILE_M
    return (placed.ox + x) * t, -(placed.oy + y) * t


def _px(placed, px):
    s = config.TILE_M / config.TILE_PX
    return placed.ox * config.TILE_M + px[0] * s, -(placed.oy * config.TILE_M + px[1] * s)


_GROUND = None   # set by build(): the terrain sampler, or None for the flat diorama
_FOREST = False  # set by build(): the forest places the sample trees / stumps / rocks
_HERO = set()    # set by build(): ids of the buildings buildings_hero / buildings_town design (no box,
                 # no glow marker, no marker for the wall band / neon they wear)


def _box(name, col, mat, x, y, w_m, d_m, h_m, z=0.0, **props):
    """A box with its NW-bottom corner at (x, y, z above the ground), w_m east, d_m
    south, h_m up."""
    ob = scene.new_object(name, scene.unit_box(), col, **props)
    lo = hi = 0.0
    if _GROUND is not None:
        lo, hi = _GROUND.z_range(x, y - d_m, x + w_m, y)
    ob.location = (x, y, z + lo)
    ob.scale = (w_m, d_m, h_m + (hi - lo if z == 0.0 else 0.0))
    scene.set_object_material(ob, mat)
    return ob


def _box_centred(name, col, mat, cx, cy, w_m, d_m, h_m, z=0.0, **props):
    return _box(name, col, mat, cx - w_m / 2, cy + d_m / 2, w_m, d_m, h_m, z, **props)


def _post(name, col, mat, cx, cy, h_m, diameter=POLE_DIAMETER_M, **props):
    ob = scene.new_object(name, scene.unit_cylinder(), col, **props)
    ob.location = (cx, cy, 0.0 if _GROUND is None else _GROUND.z_at(cx, cy) - 0.05)
    ob.scale = (diameter, diameter, h_m)
    scene.set_object_material(ob, mat)
    return ob


def build_buildings(placed, col, mats):
    t = config.TILE_M
    for b in placed.data["buildings"]:
        if b["id"] in _HERO:
            continue        # buildings_hero.py / buildings_town.py designs it
        colour = b["wall"] or config.ART_BUILDING_COLOUR
        mat = mats.colour("Wall", colour)
        x, y = _corner(placed, b["x"], b["y"])
        _box(f"Bldg_{placed.id}_{b['id']}", col, mat, x, y, b["w"] * t, b["h"] * t,
             config.BUILDING_HEIGHT_M, map_id=placed.id, dump_id=b["id"], art=b["art"],
             place=b["place"], label=b["label"], state=b["state"])


def _prop_colour(p):
    name = config.PROP_COLOURS.get(p["kind"], config.PROP_DEFAULT_COLOUR)
    return p.get("paint") if name is None else name


def build_props(placed, col, mats):
    t = config.TILE_M
    for i, p in enumerate(placed.data["props"]):
        kind = p["kind"]
        if _GROUND is not None and kind in config.GROUND_PROP_KINDS:
            continue
        if _FOREST and p.get("sample") and kind in config.FOREST_PROP_KINDS:
            continue    # forest.py plants the per-save sample
        name = f"Prop_{placed.id}_{kind}_{p['id'] or i}"
        mat = mats.colour("Prop", _prop_colour(p) or config.PROP_DEFAULT_COLOUR)
        h = config.PROP_HEIGHTS_M.get(kind, config.PROP_DEFAULT_HEIGHT_M)
        x, y = _corner(placed, p["x"], p["y"])
        w, d = p["w"] * t, p["h"] * t
        ident = dict(map_id=placed.id, dump_id=p["id"], kind=kind,
                     conditional=p.get("conditional"), sample=p.get("sample"))
        if kind == "fence":        # a perimeter, not a solid block
            rail = 0.15
            for side, (sx, sy, sw, sd) in {
                    "N": (x, y, w, rail), "S": (x, y - d + rail, w, rail),
                    "W": (x, y, rail, d), "E": (x + w - rail, y, rail, d)}.items():
                _box(f"{name}_{side}", col, mat, sx, sy, sw, sd, h, **ident)
        elif kind == "tree":       # trunk-ish post so the sample reads as a tree
            _post(name, col, mat, x + t / 2, y - t / 2, h, diameter=1.6, **ident)
        else:
            _box(name, col, mat, x, y, w, d, h, **ident)


def build_lights(placed, col, mats):
    for i, lt in enumerate(placed.data["lights"]):
        cx, cy = _px(placed, lt["px"])
        name = f"Light_{placed.id}_{lt['kind']}_{lt.get('id') or i}"
        ident = dict(map_id=placed.id, dump_id=lt.get("id"), kind=lt["kind"])
        if lt["kind"] == "street":
            colour = config.LIGHT_LIT_COLOUR if lt["lit"] else config.LIGHT_DEAD_COLOUR
            _post(name, col, mats.colour("Light", colour), cx, cy, config.STREET_LIGHT_HEIGHT_M,
                  facing=lt["facing"], lit=lt["lit"], **ident)
        elif _HERO and lt.get("parent") in _HERO:
            continue        # the building's own lit windows (LF_Window_Glass) carry it
        else:
            c = lt.get("color") or "#ffffff"
            colour = config.LIGHT_LIT_COLOUR if c.lower() == "#ffffff" else c
            _box_centred(name, col, mats.colour("Light", colour), cx, cy, GLOW_SIZE_M,
                         GLOW_SIZE_M, GLOW_SIZE_M, z=GLOW_Z_M, parent_building=lt.get("parent"),
                         **ident)


def build_signs(placed, col, mats):
    mat = mats.colour("Sign", config.SIGN_COLOUR)
    for i, s in enumerate(placed.data["signs"]):
        cx, cy = _px(placed, s["px"])
        if s["kind"] in ("wallband", "neon") and s.get("building") in _HERO:
            continue        # the designed building wears it
        on_building = s["kind"] in ("wallband", "bracket", "neon")
        z = BUILDING_SIGN_Z_M if on_building else config.SIGN_HEIGHT_M - SIGN_H_M
        _box_centred(f"Sign_{placed.id}_{s['kind']}_{s.get('id') or i}", col, mat, cx, cy,
                     SIGN_W_M, SIGN_D_M, SIGN_H_M, z=z, map_id=placed.id, dump_id=s.get("id"),
                     kind=s["kind"], text=s.get("text"), building=s.get("building"),
                     conditional=s.get("conditional"))


def build(world, col_buildings, col_props, mats, only=None, ground=None, forest=False, hero=False):
    """hero: the art buildings (buildings_hero.py) and the placeholder buildings
    (buildings_town.py, config.TOWN_DESIGNS) are designed: skip their boxes, the glow
    markers of their windows / signs, and their wall-band + neon markers. Only the
    Phase 7 signs (pole, bracket, board, FOR SALE) keep markers."""
    global _GROUND, _FOREST, _HERO
    _GROUND, _FOREST = ground, forest
    _HERO = {b["id"] for m in world.maps.values() for b in m.data["buildings"]
             if b.get("art") in config.HERO_ART or b["id"] in config.TOWN_DESIGNS} if hero else set()
    for placed in world.maps.values():
        if only is not None and placed.id != only:
            continue
        build_buildings(placed, col_buildings, mats)
        build_props(placed, col_props, mats)
        build_lights(placed, col_props, mats)
        build_signs(placed, col_props, mats)

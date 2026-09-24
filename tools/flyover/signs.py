"""Every free-standing and bracket sign the dump carries, designed for the film (the
wall bands and neon OPEN are the buildings' own, buildings_*.py):

  pole      MotelSign (MotelSign.cs + the motel handoff: the googie cabinet, MOTEL panel,
            the BLANK nameplate with its one ruled line, the NO VACANCY neon panel, bulb
            rail, atomic starburst, pylon, concrete foot) and the plainer PoleSign
            cabinets: FIREWORKS (cream face, barn-red letters) and the drive-in's
            letter-board marquee (DRIVE-IN / CLO ED: the S tile is missing). Double-faced,
            square to the route they serve (the road; the marquee its drive).
  bracket   BAR on Billie's (BracketSign.cs): iron arm off the modelled wall plate
            (the body's `mount_bracket`), the plaque square to the facade, one bulb.
  board     Sign.cs: a wood board on a post. The game draws NO lettering on a board
            (its words show only when read), so every board is blank-faced: weathered
            paint, a few illegible smudges. The fork's FingerPost is the same Sign in
            the game; here a finger post with three BLANK arms (N farm, E town, W road).
  for_sale  GarageSaleSign.cs: the realtor's pale plank with a rust-red notice stripe
            on a wood post, no letters.

Lettering (3x5 PixelFont, only what the game DRAWS): MOTEL, NO VACANCY (NO dark forever,
the V its own object for Phase 10's blink), FIREWORKS, DRIVE-IN / CLO ED, BAR.

Facing: a board faces the paved road (config.SIGN_FACING overrides: the blockade is read
from the farm). A south-of-road building now faces the road (Phase 6), so the board /
FOR SALE sign the 2D put in front of its south face would stand behind it: such a sign
is mirrored across the building to the road side, at the same distance from the
building's front (ROADSIDE rule).
"""

import math

import archkit as ak
import config
import pixelfont
import propkit as pk
from archkit import T, Rz

TM = config.TILE_M
ROT_PI = Rz(math.pi)


def _double(front, M_back=ROT_PI):
    """A face decoration authored on the front (y < 0) plus its twin on the back."""
    return ak.Part().add(front).add(front, M_back)


# ---------------------------------------------------------------------------
# The motel's pole sign (MotelSign.cs geometry: 74 x 88 px, cabinet y 4..66)
# ---------------------------------------------------------------------------

MOTEL_W, MOTEL_CAB = 74, (4, 66)


def motel_sign(s, bottom):
    """Returns the Assembly: body (pylon, foot, cabinet, panels, lettering) + prototype
    objects for the light-family parts: NO (glow 0), V (glow 1: Phase 10 blinks it),
    ACANCY (glow 1), the bulb rail and the starburst (neon family)."""
    a = ak.Assembly("MotelSign")
    b = a.body
    W = MOTEL_W * s
    H = (MOTEL_CAB[1] - MOTEL_CAB[0]) * s
    D = 0.46
    z0, z1 = bottom, bottom + H

    def X(px):
        return (px - MOTEL_W / 2) * s

    def Z(py):
        return z1 - (py - MOTEL_CAB[0]) * s

    yf = -D / 2
    # the pylon (ink outline around a stone-shade steel body) and the foot
    b.box(-0.25, -0.2, -0.3, 0.25, 0.2, z0, "metal:ink-700", skip=("bottom",))
    b.box(-0.2, -0.205, 0.2, 0.2, 0.205, z0 - 0.05, "metal:stone-shade", skip=("bottom", "top"))
    b.box(-0.55, -0.35, -0.3, 0.55, 0.35, 0.22, "paint:ink-900", skip=("bottom",))
    # the cabinet: ink-700 casing, cream field on both faces
    b.box(-W / 2, yf, z0, W / 2, -yf, z1, "metal:ink-700")
    face = ak.Part()
    face.box(X(2), yf - 0.01, Z(64), X(72), yf, Z(6), "enamel:cream", skip=("back",))
    face.box(X(4), yf - 0.03, Z(24), X(70), yf - 0.01, Z(8), "paint:barn-red", skip=("back",))      # MOTEL panel
    pixelfont.text(face, "MOTEL", 0.0, Z(11), 2 * s, 0.02, "paint:cream", y=yf - 0.03)
    face.box(X(4), yf - 0.03, Z(40), X(70), yf - 0.01, Z(26), "paint:ink-900", skip=("back",))     # the nameplate:
    face.box(X(8), yf - 0.035, Z(34), X(66), yf - 0.03, Z(32), "paint:ink-500", skip=("back",))    # blank, one rule
    face.box(X(4), yf - 0.03, Z(62), X(70), yf - 0.01, Z(42), "paint:ink-900", skip=("back",))     # vacancy panel
    b.add(_double(face))
    # the neon: NO (circuit A, dark), V (C, blinks), ACANCY (B, steady)
    tube = "neon:neon-dead/neon-red"
    yt = yf - 0.03
    no = pixelfont.text(ak.Part(), "NO", 0.0, Z(45), s, 0.03, tube, y=yt)
    vx = X(MOTEL_W / 2 - pixelfont.measure("VACANCY", 1) / 2)
    v = pixelfont.text(ak.Part(), "V", vx + 1.5 * s, Z(53), s, 0.03, tube, y=yt)
    acancy = pixelfont.text(ak.Part(), "ACANCY", vx + 4 * s + pixelfont.measure("ACANCY", s) / 2, Z(53), s, 0.03,
                            tube, y=yt)
    for name, part, glow in (("Neon_NO", no, 0.0), ("Neon_V", v, 1.0), ("Neon_ACANCY", acancy, 1.0)):
        a.protos[name] = _double(part)
        a.place(name, T(), glow=glow, circuit={"Neon_NO": "A", "Neon_V": "C", "Neon_ACANCY": "B"}[name])
    # the bulb rail (amber bulbs on the sign circuit) and the atomic starburst (aqua neon)
    rail = ak.Part()
    for px in range(4, 69, 6):
        rail.cylinder((X(px + 1), yf - 0.01, Z(61)), (X(px + 1), yf - 0.07, Z(61)), s * 0.9, "neon:stone-pale/lantern",
                      n=8)
    a.protos["BulbRail"] = _double(rail)
    a.place("BulbRail", T(), glow=1.0)
    star = ak.Part()
    cx, cz = X(63), z1 + 0.28
    star.cylinder((cx, 0, cz - 0.3), (cx, 0, cz + 0.3), 0.035, "neon:aqua", n=8)
    star.cylinder((cx - 0.3, 0, cz), (cx + 0.3, 0, cz), 0.035, "neon:aqua", n=8)
    star.cylinder((cx, -0.3, cz), (cx, 0.3, cz), 0.035, "neon:aqua", n=8)
    b.cylinder((cx, 0, z1), (cx, 0, cz - 0.3), 0.02, "metal:ink-700", n=6)
    a.protos["Starburst"] = star
    a.place("Starburst", T(), glow=1.0)
    a.meta = dict(spill=[(X(37), yf - 0.6, Z(52)), (X(37), -yf + 0.6, Z(52))])
    return a


# ---------------------------------------------------------------------------
# PoleSign cabinets: FIREWORKS, the drive-in marquee
# ---------------------------------------------------------------------------

def pole_cabinet(lines, face, letters, s, bottom, letterboard=False, missing=()):
    """PoleSign.cs: an ink-700 outline, the face colour, the lines centred in the 3x5
    font (letters colour), a narrow stone-shade pylon, a stone-dark foot. letterboard:
    each glyph a tile on a grooved board; `missing` = (line, index) tiles gone (only a
    paler patch where each hung)."""
    a = ak.Assembly("PoleSign")
    b = a.body
    tw = max(pixelfont.measure(t, 1) for t in lines)
    cw = max(tw + 12, 34)
    line_h = pixelfont.GLYPH_H + 4
    ch = len(lines) * line_h + 6
    W, H = (cw + 4) * s, (ch + 4) * s
    D = 0.36 if not letterboard else 0.42
    z0, z1 = bottom, bottom + H
    yf = -D / 2
    b.box(-0.22, -0.16, -0.3, 0.22, 0.16, z0, "metal:ink-700", skip=("bottom",))
    b.box(-0.17, -0.165, 0.2, 0.17, 0.165, z0 - 0.05, "metal:stone-shade", skip=("bottom", "top"))
    b.box(-0.5, -0.3, -0.3, 0.5, 0.3, 0.18, "stone_trim:stone-dark", skip=("bottom",))
    b.box(-W / 2, yf, z0, W / 2, -yf, z1, "metal:ink-700")
    fp = ak.Part()
    field = f"letterboard:{face}" if letterboard else f"enamel:{face}"
    fp.box(-W / 2 + 2 * s, yf - 0.012, z0 + 2 * s, W / 2 - 2 * s, yf, z1 - 2 * s, field, skip=("back",))
    for i, t in enumerate(lines):
        ztop = z1 - (2 + 5 + i * line_h) * s + 2 * s
        if not letterboard:
            pixelfont.text(fp, t, 0.0, ztop, s, 0.025, f"paint:{letters}", y=yf - 0.012)
            continue
        x0 = -pixelfont.measure(t, s) / 2
        for k, ch_ in enumerate(t):
            cx = x0 + k * 4 * s + 1.5 * s
            if (i, k) in missing:           # the tile is gone: a paler, cleaner patch where it hung
                fp.box(cx - 2 * s, yf - 0.013, ztop - 5.6 * s, cx + 2 * s, yf - 0.012, ztop + 0.6 * s,
                       "paint:stone-light", skip=("back",))
                continue
            if ch_ == " ":
                continue
            fp.box(cx - 2 * s, yf - 0.02, ztop - 5.6 * s, cx + 2 * s, yf - 0.012, ztop + 0.6 * s, "paint:cream",
                   skip=("back",))                                   # the letter's tile, hung in the grooves
            pixelfont.text(fp, ch_, cx, ztop, s, 0.012, f"paint:{letters}", y=yf - 0.02)
    b.add(_double(fp))
    return a


# ---------------------------------------------------------------------------
# BracketSign: BAR
# ---------------------------------------------------------------------------

def bracket_sign(text, s=0.05):
    """Local: the wall plate at the origin, the arm out along -Y (away from the wall),
    the plaque hanging in the x = 0 plane (square to the facade), both faces lettered.
    Returns (Assembly, bulb position)."""
    a = ak.Assembly("Bracket")
    b = a.body
    pw = (pixelfont.measure(text, 1) + 8) * s
    L = pw + 6 * s + 0.25
    ph = 11 * s
    b.box(-0.03, -L, -0.035, 0.03, 0.0, 0.035, "metal:ink-900")                 # the arm
    b.beam((0, -0.05, -0.32), (0, -0.55, -0.02), 0.03, 0.03, "metal:ink-900")     # its knee brace
    y0, y1 = -L + 0.1, -L + 0.1 + pw
    for yy in (y0 + 2 * s, y1 - 2 * s):
        b.cylinder((0, yy, -0.035), (0, yy, -0.035 - 3 * s), 0.008, "metal:ink-900", n=6)     # hangers
    zt = -0.035 - 3 * s
    b.box(-0.03, y0, zt - ph, 0.03, y1, zt, "paint:ink-700")
    face = ak.Part()
    face.box(-0.035, y0 + s, zt - ph + s, -0.03, y1 - s, zt - s, "metal:ink-900", skip=("back",))
    part = pixelfont.text(ak.Part(), text, 0.0, 0.0, s, 0.012, "paint:cream")    # on the x-z plane, facing -y
    # turn the lettering onto the plaque's -X face (reading along the arm, wall on the right)
    face.add(part, T(-0.035, (y0 + y1) / 2, zt - 3 * s) @ Rz(-math.pi / 2))
    b.add(face)
    b.add(face, Rz(math.pi) @ T(0, -(y0 + y1), 0))        # the twin on the +X face
    bulb = (0.0, (y0 + y1) / 2, 0.12)
    b.cylinder((0, bulb[1], 0.035), (0, bulb[1], 0.07), 0.025, "metal:ink-900", n=8)
    a.protos["Bulb"] = ak.Part().cylinder((0, bulb[1], 0.07), (0, bulb[1], 0.16), 0.035, "neon:stone-pale/lantern",
                                           n=10, r1=0.02)
    a.place("Bulb", T(), glow=1.0)
    return a, bulb


# ---------------------------------------------------------------------------
# Boards (Sign.cs), the finger post, FOR SALE
# ---------------------------------------------------------------------------

def board(rng, w=0.95, h=0.55, bottom=0.95, colour="earth-base", stripe=None):
    """A board on one post, front toward -Y: three planks with hairline gaps (blank:
    signboard paint), a batten behind, the post up the back. `stripe` = a painted
    notice line (the FOR SALE plank's rust red)."""
    p = ak.Part()
    face = f"signboard:{colour}"
    n = 3
    ph = h / n
    for k in range(n):
        z0 = bottom + k * ph + 0.004
        tilt = rng.uniform(-0.004, 0.004)
        p.box(-w / 2, -0.03 + tilt, z0, w / 2, 0.0 + tilt, z0 + ph - 0.008, face)
    p.box(-0.05, 0.0, bottom + 0.03, 0.05, 0.03, bottom + h - 0.03, "plank:earth-mid")          # batten
    pk.post(p, 0.0, 0.08, bottom + h - 0.05, 0.1, "timber:earth-base", cap=None, sink=0.5)
    if stripe:
        p.box(-w / 2 + 0.07, -0.034, bottom + h * 0.56, w / 2 - 0.07, -0.03, bottom + h * 0.72, f"paint:{stripe}",
              skip=("back",))
    return p


def finger_post(rng):
    """The fork's finger post: a tall post, three blank pointed arms (N the farm, E town,
    W the west road: where they point is canon, the words only show when read)."""
    p = ak.Part()
    pk.post(p, 0.0, 0.0, 2.45, 0.12, "timber:earth-base", sink=0.6)
    p.cylinder((0, 0, 2.45), (0, 0, 2.58), 0.1, "timber:earth-mid", n=8, r1=0.02)
    arms = {}
    for k, (heading, z) in enumerate((("N", 2.18), ("E", 1.92), ("W", 1.66))):
        arm = ak.Part()
        L, h, t = 0.95, 0.19, 0.03
        tip = 0.14
        outline = [(0.06, -h / 2), (L - tip, -h / 2), (L, 0.0), (L - tip, h / 2), (0.06, h / 2)]
        arm.prism([(x, zz) for x, zz in outline], -t / 2, t / 2, "signboard:earth-base")
        arms[heading] = (arm, z)
    for heading, (arm, z) in arms.items():
        r = {"E": 0.0, "N": math.pi / 2, "W": math.pi}[heading] + rng.uniform(-0.04, 0.04)
        p.add(arm, T(0, 0, z) @ Rz(r))
    return p


# ---------------------------------------------------------------------------
# From the dump
# ---------------------------------------------------------------------------

def _route_dir(routes, x, y):
    """The nearest route's direction at (x, y): 'EW' (the road) or 'NS' (a track)."""
    best, d_road = "EW", abs(y - routes.road_y)
    for t in routes.tracks.values():
        for (ax, ay), (bx, by) in zip(t.pts[:-1], t.pts[1:]):
            vx, vy = bx - ax, by - ay
            L2 = vx * vx + vy * vy or 1.0
            u = max(0.0, min(1.0, ((x - ax) * vx + (y - ay) * vy) / L2))
            d = math.hypot(x - ax - u * vx, y - ay - u * vy)
            if d < d_road:
                d_road, best = d, ("NS" if abs(vy) > abs(vx) else "EW")
    return best


def _roadside(world, placed, s, x, y):
    """ROADSIDE rule: a board just south of a road-facing (config.TOWN_FACING 'N')
    building moves to the road side, mirrored across the building. Returns (x, y,
    moved-from building id or None)."""
    for b in placed.data["buildings"]:
        if config.TOWN_FACING.get(b["id"]) != "N":
            continue
        x0, y0, x1, y1 = pk.rect_m(placed, b)
        if x0 <= x <= x1 and 0.0 < y0 - y <= 2 * TM:
            return x, y1 + (y0 - y), b["id"]
    return x, y, None


def build(world, routes, placer, lights_col):
    """Every pole / bracket / board / for_sale sign. Returns the count."""
    import random

    import bpy

    import scene
    import streetlights
    rng = random.Random(config.PROP_SEED + 1)
    n = 0
    for placed in world.maps.values():
        for i, sg in enumerate(placed.data["signs"]):
            kind = sg["kind"]
            if kind in ("wallband", "neon"):
                continue                 # the designed buildings wear them
            name = f"Sign_{placed.id}_{sg.get('id') or i}"
            ident = dict(map_id=placed.id, dump_id=sg.get("id"), kind=f"sign_{kind}", text=sg.get("text"),
                         read_text=sg.get("readText"), conditional=sg.get("conditional"))
            x, y = pk.px_m(placed, sg["px"])
            if kind == "pole":
                y += TM / 2            # the foot's anchor is the tile's bottom edge
                sid = sg["id"]
                s, bottom = config.POLE_SIGN_PX_M[sid], config.POLE_SIGN_BOTTOM_M[sid]
                if sid == "MotelSign":
                    assert sg["lines"] == ["MOTEL", "", "NO VACANCY"] and sg["neonDark"] == "NO" \
                        and sg["blinks"] == "V" and sg["nameplateBlank"], sg
                    asm = motel_sign(s, bottom)
                else:
                    lines = sg["lines"]
                    board_ = sid == "Marquee"
                    missing = ()
                    if board_:          # "CLO ED": the S tile is gone
                        missing = tuple((li, k) for li, t in enumerate(lines) for k, c in enumerate(t)
                                        if c == " " and 0 < k < len(t) - 1)
                    face = _palette(sg["face"])
                    letters = _palette(sg["letters"])
                    asm = pole_cabinet(lines, face, letters, s, bottom, board_, missing)
                facing = "W" if _route_dir(routes, x, y) == "EW" else "N"
                z = placer.z_max(x, y, 0.5)
                body = placer.put(asm, name, x, y, facing, z=z, hull=True, lines=sg.get("lines"), **ident)
                if sid == "MotelSign":
                    body["neon_dark"], body["blinks"] = sg["neonDark"], sg["blinks"]
                    for k, p in enumerate(asm.meta["spill"]):
                        _point(bpy, scene, lights_col, f"Light_Neon_{sid}_{k}", body, p, config.colour("neon-red"),
                               config.NEON_GLOW_PROP, config.NEON_SPILL_W, streetlights, radius=0.6)
            elif kind == "bracket":
                bld = bpy.data.objects.get(f"Bldg_{placed.id}_{sg['building']}")
                if bld is None or "mount_bracket" not in bld:
                    raise AssertionError(f"{name}: no mount_bracket on {sg['building']}")
                mx, my, mz = bld["mount_bracket"]
                facing = bld.get("facing", "S")
                asm, bulb = bracket_sign(sg["text"])
                body = placer.put(asm, name, mx, my, facing, z=mz, **ident)
                _point(bpy, scene, lights_col, f"Light_Bulb_{sg['building']}", body, bulb, config.colour("lantern"),
                       config.NEON_GLOW_PROP, config.BULB_W, streetlights, radius=0.05)
            elif kind in ("board", "for_sale"):
                x, y = pk.tile_m(placed, sg["x"], sg["y"])
                x, y, moved = _roadside(world, placed, sg, x, y)
                facing = config.SIGN_FACING.get(sg.get("id")) or pk.toward_road(y, routes.road_y)
                if sg.get("id") == "FingerPost":
                    part = finger_post(rng)
                    facing = "S"
                elif kind == "for_sale":
                    part = board(rng, 0.9, 0.5, 0.9, "cream", stripe="barn-red")
                else:
                    part = board(rng)
                placer.put(part, name, x, y, facing, moved_from=moved, **ident)
            else:
                raise AssertionError(f"{name}: no design for sign kind {kind!r}")
            n += 1
    return n


def _palette(hex_):
    """The dump's '#rrggbb' back to its palette name (PoleSign colours are palette)."""
    for k, v in config.PALETTE.items():
        if v.lower() == hex_.lower():
            return k
    return hex_


def _point(bpy, scene, col, name, body, local, colour, prop, watts, streetlights, radius=0.1):
    """A point light at a body-local point, energy = watts x the scene property."""
    L = scene.tag(bpy.data.lights.new(name, "POINT"))
    L.color = config.hex_rgba(colour)[:3]
    L.shadow_soft_size = radius
    L.energy = 0.0
    streetlights.drive(L, "energy", prop, watts)
    ob = scene.new_object(name, L, col, family=prop, glow=1.0, parent_sign=body.name)
    from mathutils import Vector
    ob.location = body.matrix_world @ Vector(local)
    return ob

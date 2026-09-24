"""The nine buildings the game only has as flat wall-colour placeholders, DESIGNED for the
film: the gas station, the garage, the fireworks stand (west entry), Billie's (billies),
the police station, the hardware store, the salon (east entry), Abe's shack (east fork)
and the drive-in's concession stand. They replace the Phase 2 boxes.

Nothing is drawn for them but a wall colour, a footprint, a door and their canon
lettering, so each is designed as a believable small-town New England building of its
kind: the town is older than the 1950s motel-era storefronts it wears (clapboard and
masonry grammar under them; "the town electrified before it had taste"). Every one
takes the dump's wall colour as its main wall colour; real heights; footprint, door and
sign positions from the dump (config.TOWN_FACING: the south-of-road buildings turn to
face the road in 3D, Kevin's decision; the rest face south). The same archkit parts and
archmats keys as buildings_hero, the same local frame and grounding.

Lettering: ONLY the dump's wall-band words (GAS, GARAGE, POLICE, HARDWARE, SALON,
SNACKS) and the neon OPEN, in the 3x5 pixel font. A LIT band (dump "lit": GAS, POLICE,
SALON) wears its letters and the trough lamp under it on the sign-lamp family
(LF_SignLamp_*, config.SIGN_GLOW_PROP); dark bands (GARAGE, HARDWARE, SNACKS) are plain
paint. OPEN is LF_Neon (NeonWordSign.cs colours, config.ART_COLOURS neon-red /
neon-dead). Windows: glow 0 / 1 per window on LF_Window_Glass (calls in DESIGN_NOTES).

Phase 7 hangs the pole / bracket / board / FOR SALE signs; the bar's bracket mount point
is modelled (a wall plate) and every body carries its sign mounts as custom props
(`mount_<kind>` = world xyz).
"""

import math
import random

import numpy as np

import archkit as ak
import buildings_hero as bh
import config
import pixelfont
from archkit import T, Rz

TM = config.TILE_M
Walls, profile_tops, rear, downspout = bh.Walls, bh.profile_tops, bh.rear, bh.downspout


def _wallband(a, cx, y_face, z0, text, lit, px=0.11, depth=0.16, pad=0.28):
    """A wall band (archkit.wall_band) on a front face at y_face, bottom at z0, centred cx."""
    band, letters, lamp, (w, h) = ak.wall_band(text, px, lit, depth=depth, pad=pad)
    M = T(cx, y_face, z0)
    a.body.add(band, M)
    if lit:
        a.protos[f"Band_{text}"] = letters
        a.place(f"Band_{text}", M, glow=1.0)
        a.protos[f"BandLamp_{text}"] = lamp
        a.place(f"BandLamp_{text}", M, glow=1.0)
    else:
        a.body.add(letters, M)
    return w, h


def _neon_open(a, Mwall, u, z_top, px=0.065):
    """The window mount's OPEN (NeonWordSign.cs): tube letters hung just inside the glass."""
    a.protos.setdefault("Neon_OPEN", pixelfont.text_part("OPEN", px, 0.022, "neon:neon-dead/neon-red"))
    a.place("Neon_OPEN", Mwall @ T(u, 0.2 - 0.08, z_top), glow=1.0)
    w, h = pixelfont.measure("OPEN", px) + 0.2, pixelfont.GLYPH_H * px + 0.2      # its dark backing board
    a.body.add(ak.Part().box(-w / 2, 0.0, -h + 0.1, w / 2, 0.012, 0.1, "paint:ink-900", skip=("back",)),
               Mwall @ T(u, 0.2 - 0.08, z_top))


def _apron(b, x0, x1, y_wall, depth, z_top=0.12, mat="stone_trim:stone-pale"):
    """A concrete apron / walk poured along a front wall (out toward -Y)."""
    b.box(x0, y_wall - depth, -0.3, x1, y_wall, z_top, mat, skip=("bottom",))


# ---------------------------------------------------------------------------
# Gas station: a 1950s "oblong box" in painted block, a cream enamel frieze all round,
# flat roof; the office's plate glass (OPEN in the window, lit), a glass door, a stock
# room; restroom doors on the side, the heating-oil tank and flue behind. No pumps, no
# canopy (Kevin): the forecourt stays empty.
# ---------------------------------------------------------------------------

def gas_station(W, D, zb, ctx):
    a = ak.Assembly("GasStation")
    b = a.body
    wall = f"block:{ctx['wall']}"
    x0, y0, x1, y1 = 0.4, 0.5, W - 0.4, D - 0.4
    rect = (x0, y0, x1, y1)
    zp, zw = 0.3, 4.0
    ak.band(b, rect, zb, zp, 0.05, "stone_trim:stone-pale", bottom=False)
    _apron(b, x0 - 0.05, x1 + 0.05, y0, 1.1)
    xd, xn = ctx["door_x"], ctx["neon_x"]
    plate = a.proto("Plate", lambda: ak.window(3.2, 1.9, 0.2, (2, 1), sash="fixed", frame="metal:stone-pale",
                                               sill="metal:stone-pale"))
    plate_n = a.proto("Plate_Neon", lambda: ak.window(3.2, 1.9, 0.2, (1, 1), sash="fixed", frame="metal:stone-pale",
                                                      sill="metal:stone-pale"))
    door = a.proto("Door", lambda: ak.door(0.95, 2.15, 0.2, paint="metal:stone-pale", frame="metal:stone-pale",
                                           glass_upper=True, panels=False, transom=0.45))
    high = a.proto("Win_High", lambda: ak.window(1.2, 0.6, 0.2, (2, 1), sash="fixed", frame="metal:stone-pale"))
    rdoor = a.proto("Door_Rest", lambda: ak.door(0.85, 2.05, 0.2, paint="paint:stone-light", panels=False,
                                                 frame="metal:stone-pale"))
    bdoor = a.proto("Door_Back", lambda: ak.door(0.9, 2.05, 0.2, paint="paint:stone-shade", panels=False))
    bwin = a.proto("Win_Back", lambda: ak.window(1.0, 1.0, 0.2, (2, 2), frame="trim_dark", sill="stone_trim:stone-pale"))
    walls = Walls(a, rect, zp, zw, wall, 0.2)
    # the office: the plate window with the OPEN neon (the dump's neon x), the door, a
    # second plate on the far side of the door; the stock room's high window beyond
    walls.add("S", xn, 3.2, 0.95, 1.9, plate_n, glow=1.0)
    neon_at = xn
    walls.add("S", xd, 0.95, zp, 2.6, door, glow=1.0)
    other = xd - (xn - xd) if xn > xd else xd + (xd - xn)
    walls.add("S", other, 3.2, 0.95, 1.9, plate, glow=1.0)
    far = (x0 + other - 1.6) / 2 if xn > xd else (x1 + other + 1.6) / 2
    walls.add("S", far, 1.2, 2.3, 0.6, high, glow=0.0)
    ym = (y0 + y1) / 2
    side = "W" if xn > xd else "E"          # the restrooms on the stock-room end
    walls.add(side, ym - 1.0, 0.85, zp, 2.05, rdoor, glow=0.0)
    walls.add(side, ym + 1.2, 0.85, zp, 2.05, rdoor, glow=0.0)
    walls.add("E" if side == "W" else "W", ym, 1.0, 1.5, 1.0, bwin, glow=1.0)
    walls.add("N", x0 + 3.0, 0.9, zp, 2.05, bdoor, glow=0.0)
    walls.add("N", x1 - 3.5, 1.0, 1.5, 1.0, bwin, glow=0.0)
    walls.build()
    # the enamel frieze all round (the GAS band mounts on it), a dark coping over it
    ak.band(b, rect, zw - 0.95, zw + 0.25, 0.12, "enamel:cream")
    ak.band(b, rect, zw - 1.0, zw - 0.95, 0.15, "metal:stone-dark")
    ak.flat_roof(b, *rect, zw + 0.4, 0.15, 0.14, "gravel_roof", "metal:stone-dark", parapet=0.06)
    _wallband(a, ctx["band_x"], y0 - 0.12, zw - 0.86, ctx["band_text"], ctx["band_lit"], px=0.1, depth=0.1)
    _neon_open(a, walls.frames["S"], walls.u("S", neon_at), 0.95 + 1.9 * 0.62, px=0.075)
    # steps to the door, the downspouts from the back scuppers, the oil tank and flue
    for x in (x0 + 0.3, x1 - 0.3):
        b.box(x - 0.1, y1, zw + 0.1, x + 0.1, y1 + 0.25, zw + 0.25, "metal:stone-shade")
        b.cylinder((x, y1 + 0.14, 0.02), (x, y1 + 0.14, zw + 0.12), 0.05, "metal:stone-shade", n=8)
    tx = x1 - 1.6
    b.cylinder((tx - 0.9, y1 + 0.75, 0.55), (tx + 0.9, y1 + 0.75, 0.55), 0.55, "paint:stone-light", n=16)
    for lx in (tx - 0.6, tx + 0.6):
        for ly in (y1 + 0.45, y1 + 1.05):
            b.cylinder((lx, ly, 0.0), (lx, ly, 0.2), 0.03, "metal:stone-dark", n=6)
    b.cylinder((tx, y1 + 0.75, 1.08), (tx, y1 + 0.75, 1.3), 0.04, "metal:stone-dark", n=6)
    fx = x0 + 5.0
    b.cylinder((fx, y1 + 0.2, 2.2), (fx, y1 + 0.2, zw + 1.3), 0.1, "metal:stone-shade", n=10)
    b.cylinder((fx, y1 + 0.2, zw + 1.3), (fx, y1 + 0.2, zw + 1.45), 0.18, "metal:stone-dark", n=10)
    b.box(x0 + 1.6, y1 - 3.0, zw + 0.4, x0 + 2.4, y1 - 2.2, zw + 0.8, "metal:stone-light")      # roof vent
    a.meta = dict(eave=zw + 0.4, ridge=zw + 0.5)
    return a


# ---------------------------------------------------------------------------
# Garage: a 1940s block repair shop, stepped parapet front carrying the (dark) GARAGE
# band, two sectional service-bay doors (the big one on the kerb cut), an office window
# and a man door; a shed roof falling to the back behind the parapet; steel factory
# windows on the sides; the shop stove's block chimney. For sale: everything dark.
# ---------------------------------------------------------------------------

def garage(W, D, zb, ctx):
    a = ak.Assembly("Garage")
    b = a.body
    wall = f"block:{ctx['wall']}"
    x0, y0, x1, y1 = 0.4, 0.5, W - 0.4, D - 0.4
    rect = (x0, y0, x1, y1)
    zp, zf, zbk = 0.15, 4.5, 3.9            # floor, roof at the front / back wall
    ak.band(b, rect, zb, zp, 0.05, "stone_trim:stone-base", bottom=False)
    _apron(b, x0, x1, y0, 1.2, 0.1, "stone_trim:stone-base")
    kc = ctx["cut_x"]
    bay_big = a.proto("Bay_Big", lambda: ak.overhead_door(3.4, 3.3, 0.2, "paint:stone-light"))
    bay = a.proto("Bay", lambda: ak.overhead_door(3.0, 3.1, 0.2, "paint:stone-light"))
    mdoor = a.proto("Door", lambda: ak.door(0.95, 2.1, 0.2, paint="paint:stone-shade", glass_upper=True,
                                            frame="trim_dark"))
    owin = a.proto("Win_Office", lambda: ak.window(1.2, 1.3, 0.2, (3, 2), sash="fixed", frame="metal:stone-dark",
                                                   sill="stone_trim:stone-pale"))
    steel = a.proto("Win_Steel", lambda: ak.window(1.8, 1.3, 0.2, (4, 3), sash="fixed", frame="metal:stone-dark",
                                                   sill="stone_trim:stone-pale"))
    bdoor = a.proto("Door_Back", lambda: ak.door(0.9, 2.05, 0.2, paint="paint:stone-shade", panels=False))
    walls = Walls(a, rect, zp, zf, wall, 0.2)
    # the big bay on the kerb cut, the second bay toward the near end, office + man door
    # on the other side (the order flips with the cut's side of centre)
    sgn = 1 if kc < (x0 + x1) / 2 else -1
    walls.add("S", kc, 3.4, zp, 3.3, bay_big, glow=0.0)
    walls.add("S", kc - sgn * 4.4, 3.0, zp, 3.1, bay, glow=0.0)
    walls.add("S", kc + sgn * 3.6, 1.2, 1.2, 1.3, owin, glow=0.0)
    walls.add("S", kc + sgn * 5.3, 0.95, zp, 2.1, mdoor, glow=0.0)
    for s in "EW":
        for yy in (y0 + 1.9, y1 - 1.9):
            walls.add(s, yy, 1.8, 1.6, 1.3, steel, glow=0.0)
    walls.add("N", x0 + 2.2, 0.9, zp, 2.05, bdoor, glow=0.0)
    walls.add("N", (x0 + x1) / 2 + 2.0, 1.8, 1.6, 1.3, steel, glow=0.0)
    L = y1 - y0
    walls.top("E", [(0.0, zf), (L, zbk)])
    walls.top("W", [(0.0, zbk), (L, zf)])
    walls.tops["N"] = [(0.0, zbk), (x1 - x0, zbk)]
    walls.build()
    # the parapet: the front wall up to 5.3, stepped to 5.9 over the middle, capped
    ym = (x0 + x1) / 2
    zpar, zstep, sw = 5.3, 5.9, 3.4
    b.box(x0, y0, zf, x1, y0 + 0.25, zpar, wall, skip=("bottom",))
    b.box(ym - sw, y0, zpar, ym + sw, y0 + 0.25, zstep, wall, skip=("bottom",))
    for xa, xb, z in ((x0 - 0.05, ym - sw, zpar), (ym + sw, x1 + 0.05, zpar), (ym - sw - 0.05, ym + sw + 0.05, zstep)):
        b.box(xa, y0 - 0.06, z, xb, y0 + 0.31, z + 0.1, "stone_trim:stone-pale")
    for s in ((x0, x0 + 0.35), (x1 - 0.35, x1)):      # pilasters at the corners
        b.box(s[0], y0 - 0.1, zb, s[1], y0 + 0.01, zpar, wall, skip=("back",))
    # the shed roof: roll roofing on a slab from the parapet to the back, a gutter behind
    prof = [(y0 + 0.2, zf - 0.02), (y1 + 0.35, zbk - 0.02 - 0.35 * (zf - zbk) / L)]
    rb = ak.Part()
    ak.sweep_roof(rb, prof, x0 - 0.1, x1 + 0.1, 0.15, "roll_roofing:ink-500", "paint:stone-shade", "metal:stone-dark")
    b.add(rb)
    ze = prof[1][1]
    ak.gutter(b, (x1 + 0.1, y1 + 0.35), (x0 - 0.1, y1 + 0.35), ze + 0.1, 0.0, "metal:stone-shade")
    for x in (x0 + 0.4, x1 - 0.4):
        downspout(b, x, y1, y1 + 0.42, ze + 0.1, "metal:stone-shade", side=1)
    _wallband(a, ctx["band_x"], y0 - 0.02, 4.55, ctx["band_text"], ctx["band_lit"], px=0.1, depth=0.12)
    # the shop stove's chimney up the back, a ventilator on the roof
    cx = x1 - 2.4
    ak.chimney(b, cx, y1 + 0.3, 0.6, 0.5, 0.0, zbk + 1.4, "block:stone-shade", "stone_trim:stone-base", pots=0)
    b.cylinder((x0 + 5.0, (y0 + y1) / 2, 4.1), (x0 + 5.0, (y0 + y1) / 2, 4.9), 0.22, "metal:stone-light", n=12)
    b.cylinder((x0 + 5.0, (y0 + y1) / 2, 4.9), (x0 + 5.0, (y0 + y1) / 2, 5.15), 0.36, "metal:stone-light", n=12,
               r1=0.05)
    a.meta = dict(eave=zf, ridge=zstep + 0.1)
    return a


# ---------------------------------------------------------------------------
# Fireworks stand: a roadside board stand on block piers, board-and-batten in the wall
# colour, a corrugated shed roof falling back, the serving hatch across the front shut
# (its top-hinged flap down and padlocked over a counter ledge: a seasonal stand in
# autumn), a side door. Its FIREWORKS pole sign is Phase 7.
# ---------------------------------------------------------------------------

def fireworks_stand(W, D, zb, ctx):
    a = ak.Assembly("FireworksStand")
    b = a.body
    wall = f"boards:{ctx['wall']}"
    x0, y0, x1, y1 = 0.7, 0.9, W - 0.7, D - 0.7
    rect = (x0, y0, x1, y1)
    zp, zf, zbk = 0.45, 2.9, 2.35
    # block piers under a sill beam, the floor deck edge
    for px_ in np.linspace(x0 + 0.2, x1 - 0.2, 4):
        for py in (y0 + 0.2, y1 - 0.2):
            b.box(px_ - 0.2, py - 0.1, zb, px_ + 0.2, py + 0.1, zp - 0.12, "block:stone-base", skip=("bottom",))
    ak.band(b, rect, zp - 0.14, zp, 0.03, "paint:earth-dark", bottom=True)
    sdoor = a.proto("Door", lambda: ak.door(0.85, 2.0, 0.12, paint=f"paint:{ctx['wall']}", frame="trim_dark"))
    walls = Walls(a, rect, zp, zf, wall, 0.12)
    walls.add("E", (y0 + y1) / 2, 0.85, zp, 2.0, sdoor, glow=0.0)
    L = y1 - y0
    walls.top("E", [(0.0, zf), (L, zbk)])
    walls.top("W", [(0.0, zbk), (L, zf)])
    walls.tops["N"] = [(0.0, zbk), (x1 - x0, zbk)]
    walls.build()
    ak.corner_boards(b, rect, zp, zbk, 0.12, 0.025, "paint:earth-dark")
    # the hatch: a framed flap over the front, hinge strap on top, hasp + padlock, the
    # counter ledge on brackets under it, two props leaned at the corner
    hx0, hx1, hz0, hz1 = x0 + 0.6, x1 - 0.6, 1.25, 2.45
    b.box(hx0 - 0.1, y0 - 0.05, hz0 - 0.1, hx1 + 0.1, y0, hz1 + 0.1, "paint:earth-dark", skip=("back",))
    b.box(hx0, y0 - 0.09, hz0, hx1, y0 - 0.05, hz1, "plywood_fresh:earth-light", skip=("back",))
    b.box(hx0, y0 - 0.1, hz1 - 0.08, hx1, y0 - 0.09, hz1 - 0.02, "metal:stone-dark", skip=("back",))
    for x in (hx0 + 0.4, hx1 - 0.4):
        b.box(x - 0.03, y0 - 0.1, hz1 - 0.35, x + 0.03, y0 - 0.09, hz1 - 0.02, "metal:stone-dark", skip=("back",))
    mx = (hx0 + hx1) / 2
    b.box(mx - 0.04, y0 - 0.12, hz0 + 0.05, mx + 0.04, y0 - 0.09, hz0 + 0.22, "metal:stone-dark", skip=("back",))
    b.box(mx - 0.04, y0 - 0.16, hz0 - 0.04, mx + 0.04, y0 - 0.12, hz0 + 0.06, "metal:stone-light")
    b.box(hx0 - 0.15, y0 - 0.45, hz0 - 0.2, hx1 + 0.15, y0, hz0 - 0.14, "paint:earth-dark")
    for x in (hx0 + 0.2, hx1 - 0.2, mx):
        b.beam((x, y0 - 0.02, hz0 - 0.55), (x, y0 - 0.4, hz0 - 0.2), 0.05, 0.05, "paint:earth-dark")
    for k in range(2):
        xx = x1 - 0.15 + k * 0.12
        b.beam((xx, y0 - 0.55 - k * 0.08, 0.02), (xx - 0.05, y0 - 0.05, 1.9 - k * 0.1), 0.05, 0.05, "boards_weathered")
    # the roof: corrugated, rafters showing under the front overhang
    prof = [(y0 - 0.55, zf + 0.05 + 0.55 * (zf - zbk) / L), (y1 + 0.35, zbk + 0.05 - 0.35 * (zf - zbk) / L)]
    rf = ak.Part()
    ak.sweep_roof(rf, prof, x0 - 0.3, x1 + 0.3, 0.05, "corrugated:stone-base", "boards_weathered", "metal:stone-shade")
    b.add(rf)
    for x in np.linspace(x0, x1, 6):
        b.beam((x, y0 + 0.2, ak.profile_z(prof, y0 + 0.2) - 0.08), (x, y0 - 0.5, ak.profile_z(prof, y0 - 0.5) - 0.08),
               0.05, 0.12, "boards_weathered")
    a.meta = dict(eave=zbk, ridge=zf + 0.2)
    return a


# ---------------------------------------------------------------------------
# Billie's: a low roadhouse dive bar. Worn clapboard in the wall colour on a block
# foundation, a low side-gable asphalt roof, small high windows (lit: the bar is open
# all hours), a solid door under a little shed hood, a kitchen lean-to behind, the
# block chimney at the east gable, an exhaust fan. The BAR bracket sign's mount plate on
# the front wall at the east corner (the sign itself is Phase 7).
# ---------------------------------------------------------------------------

def billies(W, D, zb, ctx):
    a = ak.Assembly("Billies")
    b = a.body
    wall = f"clapboard:{ctx['wall']}"
    x0, y0, x1, y1 = 0.5, 0.6, W - 0.5, D - 3.0
    rect = (x0, y0, x1, y1)
    zp, zw = 0.45, 3.3
    pitch, ov, thick = 22.0, 0.4, 0.18
    ak.band(b, rect, zb, zp, 0.04, "block:stone-shade", bottom=False)
    prof = ak.gable_profile(y0, y1, zw, pitch, ov)
    win = a.proto("Win", lambda: ak.window(0.95, 0.75, 0.16, (2, 1), sash="fixed", frame="trim_dark",
                                           casing="paint:earth-dark", casing_w=0.09, sill="paint:earth-dark"))
    door = a.proto("Door", lambda: ak.door(0.95, 2.1, 0.16, paint="paint:ink-700", casing="paint:earth-dark"))
    vent = a.proto("Vent", lambda: ak.louvre_vent(0.6, 0.5, "paint:earth-dark", "paint:earth-dark", 5))
    walls = Walls(a, rect, zp, zw, wall, 0.16)
    xd = ctx["door_x"]
    for x in (x0 + 2.6, xd - 2.8, xd + 3.2, x1 - 4.4, x1 - 2.2):
        walls.add("S", x, 0.95, 1.75, 0.75, win, glow=1.0)
    walls.add("S", xd, 0.95, zp, 2.1, door, glow=0.0)
    ym = (y0 + y1) / 2
    walls.add("E", ym, 0.95, 1.75, 0.75, win, glow=1.0)
    walls.add("W", ym + 1.2, 0.95, 1.75, 0.75, win, glow=0.0)
    walls.add("W", ym, 0.6, zw + 0.7, 0.5, vent, hole=False)
    walls.add("N", x1 - 3.0, 0.95, 1.75, 0.75, win, glow=0.0)
    walls.top("E", profile_tops(prof, y0, y1))
    walls.top("W", profile_tops(prof, y0, y1, reverse=True))
    walls.build()
    ak.corner_boards(b, rect, zp, zw, 0.12, 0.03, "paint:earth-dark")
    for s in "SN":
        ak.band_run(b, *walls.ends[s], zw - 0.22, zw, 0.03, "paint:earth-dark")
    ak.sweep_roof(b, prof, x0 - 0.3, x1 + 0.3, thick, "shingle:stone-dark", "paint:earth-dark", "paint:earth-dark")
    zr = prof[1][1] + thick
    b.beam((x0 - 0.32, ym, zr + 0.02), (x1 + 0.32, ym, zr + 0.02), 0.28, 0.06, "metal:stone-dark")
    ze = prof[0][1]
    ak.gutter(b, (x0 - 0.3, y0 - ov), (x1 + 0.3, y0 - ov), ze + 0.1, 0.0, "metal:stone-shade")
    downspout(b, x0 + 0.3, y0, y0 - ov - 0.07, ze + 0.1, "metal:stone-shade")
    # the door hood (a little shed roof on two knee braces) and a block stoop
    hp = [(-0.95, zp + 2.55), (0.0, zp + 2.85)]
    hood = ak.Part()
    ak.sweep_roof(hood, [(y0 + y, z) for y, z in hp], xd - 0.9, xd + 0.9, 0.08, "shingle:stone-dark", "paint:earth-dark",
                  "paint:earth-dark")
    b.add(hood)
    for x in (xd - 0.75, xd + 0.75):
        b.beam((x, y0 - 0.02, zp + 2.1), (x, y0 - 0.8, zp + 2.58), 0.07, 0.07, "paint:earth-dark")
    b.box(xd - 0.85, y0 - 0.9, -0.3, xd + 0.85, y0, zp - 0.02, "block:stone-base", skip=("bottom",))
    ax = xd + 3.2                              # a window air conditioner under a front window
    b.box(ax - 0.33, y0 - 0.45, 1.25, ax + 0.33, y0 + 0.05, 1.7, "metal:stone-light")
    b.box(ax - 0.3, y0 - 0.46, 1.3, ax + 0.3, y0 - 0.45, 1.65, "metal:stone-shade", skip=("back",))
    # the BAR bracket's wall plate (Phase 7 hangs the sign): the front wall's east end
    bx, bz = min(ctx["bracket_x"], x1 - 0.35), zw - 0.55
    b.box(bx - 0.09, y0 - 0.03, bz - 0.25, bx + 0.09, y0, bz + 0.12, "metal:ink-900", skip=("back",))
    a.meta = dict(eave=zw, ridge=zr, mounts={"bracket": (bx, y0 - 0.03, bz)})
    # the kitchen lean-to behind (west half), its back door, a shed roof of roll roofing
    lx0, lx1, ly1 = x0 + 0.6, x0 + 9.0, D - 0.5
    lrect = (lx0, y1, lx1, ly1)
    lz = 2.6
    ak.band(b, (lx0, y1, lx1, ly1), zb, zp, 0.04, "block:stone-shade", bottom=False)
    lw = Walls(a, lrect, zp, lz, wall, 0.16)
    bdoor = a.proto("Door_Back", lambda: ak.door(0.9, 2.0, 0.16, paint="paint:earth-dark", casing="paint:earth-dark"))
    lw.add("N", lx0 + 2.0, 0.9, zp, 2.0, bdoor, glow=0.0)
    lw.add("N", lx1 - 2.2, 0.95, 1.45, 0.75, win, glow=1.0)
    lw.build(sides="ENW")
    ld = ly1 - y1
    lp = [(y1 - 0.2, zw - 0.42), (ly1 + 0.3, lz - 0.05)]
    lr = ak.Part()
    ak.sweep_roof(lr, lp, lx0 - 0.25, lx1 + 0.25, 0.12, "roll_roofing:ink-500", "paint:earth-dark", "paint:earth-dark")
    b.add(lr)
    # its end walls up under the slope
    for x, flip in ((lx0, False), (lx1, True)):
        pts = [(x, y1, lz), (x, ly1, lz), (x, ly1, lp[1][1] - 0.05), (x, y1, lp[0][1] - 0.05)]
        b.poly(pts[::-1] if flip else pts, wall)
    b.box(lx0 + 1.4, ly1, -0.3, lx0 + 2.6, ly1 + 0.9, zp - 0.02, "block:stone-base", skip=("bottom",))
    # the chimney (block) up the east gable, an exhaust fan hood on the lean-to
    ak.chimney(b, x1 + 0.35, ym + 0.8, 0.6, 0.6, 0.0, zr + 0.9, "block:stone-shade", "stone_trim:stone-base", pots=0)
    b.box(lx1 - 3.0, ly1 - 1.2, lz + 0.15, lx1 - 2.3, ly1 - 0.5, lz + 0.75, "metal:stone-light")
    a.meta["ridge"] = zr
    return a


# ---------------------------------------------------------------------------
# Police station: a small civic building in painted cement render (the wall colour), a
# stone water table, sills and heads, a parapet with stone coping over a flat roof; the
# centred entrance up granite steps under a cantilevered concrete hood, the LIT POLICE
# band above it; lit office windows; barred cell windows behind; a radio mast.
# ---------------------------------------------------------------------------

def police(W, D, zb, ctx):
    a = ak.Assembly("Police")
    b = a.body
    wall = f"stucco:{ctx['wall']}"
    x0, y0, x1, y1 = 0.6, 0.9, W - 0.6, D - 0.5
    rect = (x0, y0, x1, y1)
    zp, zw = 0.75, 4.6
    ak.band(b, rect, zb, zp, 0.1, "ashlar:stone-base", bottom=False)
    ak.band(b, rect, zp - 0.02, zp + 0.12, 0.13, "stone_trim:stone-pale")
    cx = ctx["band_x"]
    win = a.proto("Win", lambda: ak.window(1.2, 1.9, 0.26, (2, 2), frame="trim_dark", sill="stone_trim:stone-pale",
                                           head="stone_trim:stone-pale"))
    door = a.proto("Door", lambda: ak.door(1.7, 2.3, 0.26, leaves=2, paint="paint:ink-700", glass_upper=True,
                                           transom=0.5, frame="trim_dark"))
    cell = a.proto("Win_Cell", lambda: _barred(0.9, 0.55, 0.26))
    bdoor = a.proto("Door_Back", lambda: ak.door(0.95, 2.1, 0.26, paint="paint:stone-shade", panels=False))
    walls = Walls(a, rect, zp, zw, wall, 0.26)
    lit = {0: 1.0, 1: 1.0, 2: 1.0, 3: 0.0}
    for k, dx in enumerate((-7.0, -3.9, 3.9, 7.0)):
        walls.add("S", cx + dx, 1.2, 1.5, 1.9, win, glow=lit[k])
    walls.add("S", cx, 1.7, zp, 2.8, door, glow=1.0)
    ym = (y0 + y1) / 2
    for s, g in (("E", (1.0, 0.0)), ("W", (0.0, 1.0))):
        walls.add(s, ym - 1.6, 1.2, 1.5, 1.9, win, glow=g[0])
        walls.add(s, ym + 1.8, 1.2, 1.5, 1.9, win, glow=g[1])
    for dx in (-5.5, -2.5, 2.5):
        walls.add("N", cx + dx, 0.9, 2.4, 0.55, cell, glow=0.0)
    walls.add("N", cx + 6.0, 0.95, zp, 2.1, bdoor, glow=0.0)
    walls.build()
    # parapet, coping, cornice band; the flat roof behind it
    zpar = zw + 0.75
    ak.band(b, rect, zw - 0.3, zw - 0.12, 0.08, "stone_trim:stone-pale")
    b.add(_parapet(rect, zw, zpar, wall, "stone_trim:stone-pale"))
    b.quad((x0 + 0.2, y0 + 0.2, zw + 0.05), (x1 - 0.2, y0 + 0.2, zw + 0.05), (x1 - 0.2, y1 - 0.2, zw + 0.05),
           (x0 + 0.2, y1 - 0.2, zw + 0.05), "gravel_roof")
    # the entrance: granite steps with cheek walls, a cantilevered hood, a stone surround
    ak.steps(b, cx - 1.6, cx + 1.6, y0, zp, 3, 0.3, "stone_trim:stone-light", cheek="ashlar:stone-base")
    hz = zp + 2.8 + 0.1
    b.box(cx - 2.1, y0 - 1.5, hz, cx + 2.1, y0, hz + 0.22, "stone_trim:stone-pale")
    b.box(cx - 2.1, y0 - 1.5, hz + 0.22, cx + 2.1, y0 - 1.42, hz + 0.3, "stone_trim:stone-pale")
    for sx in (-1, 1):
        xa, xb = sorted((cx + sx * 1.05, cx + sx * 1.35))
        b.box(xa, y0 - 0.1, zp, xb, y0, hz, "stone_trim:stone-pale", skip=("back",))
    # the lit POLICE band on the parapet over the hood
    _wallband(a, cx, y0 - 0.02, 4.3, ctx["band_text"], ctx["band_lit"], px=0.1, depth=0.12)
    # a radio mast off the back corner of the roof (guyed), a roof hatch, a vent stack
    mx, my = x1 - 1.6, y1 - 1.6
    b.cylinder((mx, my, zw), (mx, my, zw + 7.5), 0.05, "metal:stone-shade", n=8)
    for k in range(4):
        z = zw + 1.8 + k * 1.6
        b.beam((mx - 0.4, my, z), (mx + 0.4, my, z), 0.03, 0.03, "metal:stone-shade")
    for gx, gy in ((mx - 3.0, my), (mx, my - 3.5), (mx + 0.4, my + 0.9)):
        b.cylinder((mx, my, zw + 5.5), (gx, gy, zw + 0.1), 0.008, "metal:stone-dark", n=4, caps=False)
    b.box(x0 + 3.0, y1 - 3.0, zw + 0.05, x0 + 3.9, y1 - 2.1, zw + 0.5, "metal:stone-light")
    b.cylinder((x0 + 7.0, y1 - 2.0, zw), (x0 + 7.0, y1 - 2.0, zw + 1.0), 0.06, "metal:stone-dark", n=8)
    a.meta = dict(eave=zw, ridge=zpar + 0.1)
    return a


def _barred(w, h, depth):
    p = ak.window(w, h, depth, (1, 1), sash="fixed", frame="trim_dark", sill="stone_trim:stone-pale")
    for x in np.linspace(-w / 2 + 0.12, w / 2 - 0.12, 5):
        p.cylinder((x, 0.03, 0.0), (x, 0.03, h), 0.014, "metal:ink-900", n=6)
    return p


def _parapet(rect, z0, z1, mat, coping):
    """A parapet wall around a flat roof: the outside face flush with the walls, a thin
    inner face, a projecting coping."""
    x0, y0, x1, y1 = rect
    p = ak.Part()
    t = 0.22
    p.box(x0, y0, z0, x1, y0 + t, z1, mat, skip=("bottom",))
    p.box(x0, y1 - t, z0, x1, y1, z1, mat, skip=("bottom",))
    p.box(x0, y0 + t, z0, x0 + t, y1 - t, z1, mat, skip=("bottom",))
    p.box(x1 - t, y0 + t, z0, x1, y1 - t, z1, mat, skip=("bottom",))
    c, o = t + 0.07, 0.07
    for bx in ((x0 - o, y0 - o, x1 + o, y0 - o + c), (x0 - o, y1 + o - c, x1 + o, y1 + o),
               (x0 - o, y0 - o + c, x0 - o + c, y1 + o - c), (x1 + o - c, y0 - o + c, x1 + o, y1 + o - c)):
        p.box(bx[0], bx[1], z1, bx[2], bx[3], z1 + 0.12, coping)
    return p


# ---------------------------------------------------------------------------
# Hardware store: an older Main Street store with a false front (square parapet +
# cornice on brackets) hiding a low gable roof; a recessed centred entry between two
# display windows on paneled bulkheads, a transom band, the DARK HARDWARE band on the
# frieze. Closed until further notice: every window dark. A double loading door behind.
# ---------------------------------------------------------------------------

def hardware(W, D, zb, ctx):
    a = ak.Assembly("Hardware")
    b = a.body
    wall = f"clapboard:{ctx['wall']}"
    x0, y0, x1, y1 = 0.4, 0.0, W - 0.4, D - 0.4
    rect = (x0, y0, x1, y1)
    zp, zw, zff = 0.3, 4.3, 6.4
    pitch = 11.0
    ak.band(b, rect, zb, zp, 0.05, "rubble:stone-shade", bottom=False)
    cx = (x0 + x1) / 2
    disp = a.proto("Display", lambda: ak.window(3.6, 2.2, 0.16, (2, 1), sash="fixed", frame="trim_dark"))
    tran = a.proto("Transom", lambda: ak.window(3.6, 0.55, 0.16, (6, 1), sash="fixed", frame="trim_dark"))
    side = a.proto("Win", lambda: ak.window(1.0, 1.5, 0.16, (2, 2), frame="trim_dark", casing="trim_dark",
                                            casing_w=0.1, sill="trim:cream"))
    load = a.proto("Door_Load", lambda: ak.door(1.8, 2.4, 0.16, leaves=2, paint="paint:earth-dark", casing="trim_dark"))
    door = a.proto("Door", lambda: ak.door(1.0, 2.25, 0.16, paint="paint:earth-dark", glass_upper=True,
                                           transom=0.45, casing="trim_dark"))
    ent_w, ent_d, ent_h = 2.4, 1.3, 3.05
    walls = Walls(a, rect, zp, zw, wall, 0.16)
    for sx in (-1, 1):
        xc = cx + sx * (ent_w / 2 + 0.35 + 1.8)
        walls.add("S", xc, 3.6, 0.95, 2.2, disp, glow=0.0)
        walls.add("S", xc, 3.6, 3.3, 0.55, tran, glow=0.0)
    walls.add("S", cx, ent_w, zp, ent_h - zp)
    ym = (y0 + y1) / 2
    for s in "EW":
        walls.add(s, ym, 1.0, 1.4, 1.5, side, glow=0.0)
    walls.add("N", x0 + 3.0, 1.8, zp, 2.4, load, glow=0.0)
    walls.add("N", x1 - 3.0, 1.0, 1.4, 1.5, side, glow=0.0)
    prof_x = ak.gable_profile(x0, x1, zw, pitch, 0.3)
    walls.tops["S"] = [(0.0, zff), (x1 - x0, zff)]               # the false front
    walls.top("N", profile_tops(prof_x, x0, x1, reverse=True))
    walls.build()
    # the recessed entry: its side linings, ceiling, floor and back wall with the door
    rec = ak.Part()
    ak.reveals([(-ent_w / 2, ent_w / 2, zp, ent_h)], ent_d, wall, rec, sill=False)
    rec.quad((-ent_w / 2, 0, zp + 0.01), (ent_w / 2, 0, zp + 0.01), (ent_w / 2, ent_d, zp + 0.01),
             (-ent_w / 2, ent_d, zp + 0.01), "stone_trim:stone-light")
    back = ak.panel([(-ent_w / 2, zp), (ent_w / 2, zp), (ent_w / 2, ent_h), (-ent_w / 2, ent_h)],
                    [(-0.5, 0.5, zp, zp + 2.7)], wall)
    rec.add(back, T(0, ent_d, 0))
    rec.add(ak.reveals([(-0.5, 0.5, zp, zp + 2.7)], 0.16, wall), T(0, ent_d, 0))
    b.add(rec, T(cx, 0, 0))
    a.place(door, T(cx, ent_d, zp), glow=0.0)
    # bulkheads under the displays, pilasters, the frieze and the cornice on brackets
    for sx in (-1, 1):
        xc = cx + sx * (ent_w / 2 + 0.35 + 1.8)
        b.box(xc - 1.85, -0.06, zp, xc + 1.85, 0.0, 0.95, "trim:earth-dark", skip=("back",))
        for k in range(3):
            px_ = xc - 1.8 + 0.15 + k * 1.2
            b.box(px_, -0.08, zp + 0.12, px_ + 1.0, -0.06, 0.83, "trim:earth-dark", skip=("back",))
    for xp in (x0, cx - ent_w / 2 - 0.35, cx + ent_w / 2 + 0.05, x1 - 0.3):
        b.box(xp, -0.12, zp, xp + 0.3, 0.0, 3.95, "trim:cream", skip=("back",))
    b.box(x0 - 0.05, -0.14, 3.95, x1 + 0.05, 0.0, 4.1, "trim:cream", skip=("back",))
    b.box(x0 - 0.1, -0.35, zff - 0.35, x1 + 0.1, 0.0, zff - 0.15, "trim:cream")
    b.box(x0 - 0.15, -0.45, zff - 0.15, x1 + 0.15, 0.05, zff, "trim:cream")
    for xk in np.linspace(x0 + 0.2, x1 - 0.2, 7):
        b.box(xk - 0.08, -0.33, zff - 0.8, xk + 0.08, 0.0, zff - 0.35, "trim:cream", skip=("back",))
    # the false front's back face and its side returns (the roof hides behind it)
    b.box(x0, 0.0, zw, x0 + 0.16, 0.9, zff, wall, skip=("bottom",))
    b.box(x1 - 0.16, 0.0, zw, x1, 0.9, zff, wall, skip=("bottom",))
    b.quad((x0, 0.16, zw), (x0, 0.16, zff), (x1, 0.16, zff), (x1, 0.16, zw), "paint:stone-shade")
    _wallband(a, cx, -0.02, 4.55, ctx["band_text"], ctx["band_lit"], px=0.1, depth=0.12)
    # the low gable, ridge front to back (profile over -x swept along y, as the barn)
    sprof = [(-x, z) for x, z in reversed(prof_x)]
    rf = ak.Part()
    ak.sweep_roof(rf, sprof, 0.16, y1 + 0.3, 0.15, "roll_roofing:ink-500", "trim:cream", "trim:cream")
    b.add(rf, Rz(math.pi / 2))
    ak.corner_boards(b, rect, zp, zw, 0.14, 0.03, "trim:cream")
    ak.chimney(b, x1 - 2.5, y1 - 1.5, 0.55, 0.55, zw - 0.5, ak.profile_z(prof_x, x1 - 2.5) + 1.2, "block:stone-shade",
               "stone_trim:stone-base", pots=0)
    b.box(x0 + 2.0, y1, -0.3, x0 + 4.0, y1 + 1.0, zp - 0.02, "stone_trim:stone-base", skip=("bottom",))   # dock
    a.meta = dict(eave=zw, ridge=zff)
    return a


# ---------------------------------------------------------------------------
# Salon: a small side-gable shop in clapboard (the wall colour): the picture window with
# OPEN in it, the glass door under a small flat hood, the lit SALON band between them and
# the eave; gable-end windows, a back door, a small chimney. Lit (Sam's open).
# ---------------------------------------------------------------------------

def salon(W, D, zb, ctx):
    a = ak.Assembly("Salon")
    b = a.body
    wall = f"clapboard:{ctx['wall']}"
    x0, y0, x1, y1 = 0.6, 0.6, W - 0.6, D - 0.6
    rect = (x0, y0, x1, y1)
    zp, zw = 0.45, 3.8
    pitch, ov, thick = 36.0, 0.35, 0.16
    ak.band(b, rect, zb, zp, 0.05, "block:stone-base", bottom=False)
    prof = ak.gable_profile(y0, y1, zw, pitch, ov)
    xd, xn = ctx["door_x"], ctx["neon_x"]
    pic = a.proto("Picture", lambda: ak.window(2.7, 1.65, 0.16, (1, 1), sash="fixed", frame="trim_dark",
                                               casing="trim:cream", casing_w=0.12, sill="trim:cream"))
    win = a.proto("Win", lambda: ak.window(1.0, 1.4, 0.16, (2, 2), frame="trim_dark", casing="trim:cream",
                                           casing_w=0.1, sill="trim:cream"))
    door = a.proto("Door", lambda: ak.door(0.95, 2.1, 0.16, paint="paint:stone-shade", glass_upper=True,
                                           casing="trim:cream"))
    walls = Walls(a, rect, zp, zw, wall, 0.16)
    walls.add("S", xn, 2.7, 0.9, 1.65, pic, glow=1.0)
    walls.add("S", xd, 0.95, zp, 2.1, door, glow=1.0)
    other = xd - (xn - xd) * 0.9
    walls.add("S", other, 1.0, 1.15, 1.4, win, glow=1.0)
    ym = (y0 + y1) / 2
    walls.add("E", ym, 1.0, 1.15, 1.4, win, glow=1.0)
    walls.add("W", ym, 1.0, 1.15, 1.4, win, glow=0.0)
    for s in "EW":
        walls.add(s, ym, 0.6, zw + 0.55, 0.75, win, glow=0.0)
    bdoor = a.proto("Door_Back", lambda: ak.door(0.9, 2.05, 0.16, paint="paint:stone-shade", casing="trim:cream"))
    walls.add("N", x0 + 2.2, 0.9, zp, 2.05, bdoor, glow=0.0)
    walls.add("N", x1 - 2.8, 1.0, 1.15, 1.4, win, glow=1.0)
    walls.top("E", profile_tops(prof, y0, y1))
    walls.top("W", profile_tops(prof, y0, y1, reverse=True))
    walls.build()
    ak.corner_boards(b, rect, zp, zw, 0.13, 0.03, "trim:cream")
    for s in "SN":
        ak.band_run(b, *walls.ends[s], zw - 0.22, zw, 0.03, "trim:cream")
    ak.sweep_roof(b, prof, x0 - 0.3, x1 + 0.3, thick, "shingle:stone-shade", "trim:cream", "trim:cream")
    zr = prof[1][1] + thick
    b.beam((x0 - 0.32, ym, zr + 0.02), (x1 + 0.32, ym, zr + 0.02), 0.26, 0.06, "metal:stone-dark")
    ze = prof[0][1]
    ak.gutter(b, (x0 - 0.3, y0 - ov), (x1 + 0.3, y0 - ov), ze + 0.1, 0.0, "metal:stone-shade")
    downspout(b, x1 - 0.3, y0, y0 - ov - 0.07, ze + 0.1, "metal:stone-shade")
    # the flat hood over the door, on two iron brackets; a concrete step
    b.box(xd - 0.75, y0 - 0.55, -0.3, xd + 0.75, y0, zp - 0.02, "stone_trim:stone-pale", skip=("bottom",))
    _wallband(a, ctx["band_x"], y0 - 0.02, zp + 2.33, ctx["band_text"], ctx["band_lit"], px=0.075, depth=0.1, pad=0.15)
    _neon_open(a, walls.frames["S"], walls.u("S", xn), 0.9 + 1.65 * 0.66, px=0.06)
    ak.chimney(b, x1 - 2.0, ym + 1.0, 0.5, 0.5, zw - 0.5, zr + 0.6, "block:stone-shade", "stone_trim:stone-base", pots=0)
    a.meta = dict(eave=zw, ridge=zr)
    return a


# ---------------------------------------------------------------------------
# Abe's shack: twenty years and never a fence post. Separate old boards gone grey over
# the wall colour, on block piers; a sagging shed roof of rusted corrugated steel patched
# with tar paper; a plank door, one small window (a faint lamp), a leaning stovepipe, a
# woodpile against the side.
# ---------------------------------------------------------------------------

def shack(W, D, zb, ctx):
    a = ak.Assembly("Shack")
    b = a.body
    rng = random.Random(ctx.get("seed", 17))
    x0, y0, x1, y1 = 1.4, 0.9, W - 1.2, D - 0.9
    rect = (x0, y0, x1, y1)
    zp, zf, zbk = 0.4, 2.55, 2.05
    for px_ in (x0 + 0.15, (x0 + x1) / 2, x1 - 0.15):
        for py in (y0 + 0.15, y1 - 0.15):
            h = zp - 0.1 - rng.uniform(0.0, 0.06)
            b.box(px_ - 0.2, py - 0.1, zb, px_ + 0.2, py + 0.1, h, "block:stone-shade", skip=("bottom",))
    b.box(x0, y0, zp - 0.12, x1, y1, zp, "boards_weathered", skip=("bottom",))
    L, Wl = y1 - y0, x1 - x0
    xd = (x0 + x1) / 2 - 0.9
    door_o = (xd - x0 - 0.42, xd - x0 + 0.42, zp, zp + 1.95)
    xw = x1 - 1.2
    win_o = (xw - x0 - 0.3, xw - x0 + 0.3, zp + 1.0, zp + 1.6)
    wall_key = f"boards_old:{ctx['wall']}"
    sides = {
        "S": (bh.walls_ends(rect, "S"), Wl, lambda u: zf, [door_o, win_o], []),
        "E": (bh.walls_ends(rect, "E"), L, lambda u: zf + (zbk - zf) * u / L, [], []),
        "N": (bh.walls_ends(rect, "N"), Wl, lambda u: zbk, [], [(1.2, 1.5, 0.9, 1.6)]),
        "W": (bh.walls_ends(rect, "W"), L, lambda u: zbk + (zf - zbk) * u / L, [], []),
    }
    for s, ((p0, p1), Ls, top, opens, gaps) in sides.items():
        M = ak.wall_frame(p0, p1)
        wpart = bh.board_wall(ak.Part(), rng, Ls, zp, top, opens, gaps, missing=0.02, broken=0.03, battens=False,
                              mat=wall_key)
        backing = ak.Part()
        outline = [(0.0, zp), (Ls, zp), (Ls, top(Ls) - 0.02), (0.0, top(0.0) - 0.02)]
        ak.panel(outline, opens, "interior_dark", y=0.05, part=backing)
        wpart.add(backing)
        for o in opens:
            wpart.add(ak.reveals([o], 0.05, "boards_weathered", sill=False))
        b.add(wpart, M)
    for cx_, cy_ in ((x0, y0), (x1, y0), (x1, y1), (x0, y1)):
        top = zf if cy_ == y0 else zbk
        b.box(cx_ - 0.06, cy_ - 0.06, zp, cx_ + 0.06, cy_ + 0.06, top, "boards_weathered")
    # the door (a plank leaf, slightly ajar on its hinge? no: shut, a hasp), the window
    leaf = bh.barn_leaf(0.84, 1.93, rng, braces=True)
    b.add(leaf, T(xd - 0.42, y0 - 0.06, zp + 0.01))
    swin = a.proto("Win", lambda: ak.window(0.6, 0.6, 0.06, (2, 2), sash="fixed", frame="boards_weathered",
                                            casing="boards_weathered", casing_w=0.07))
    a.on_wall(swin, ak.wall_frame(*bh.walls_ends(rect, "S")), xw - x0, zp + 1.0, glow=0.35)
    b.box(xd - 0.5, y0 - 0.5, -0.2, xd + 0.5, y0 - 0.06, 0.2, "boards_weathered")   # a plank step on a block
    # the roof: sagging corrugated sheets, a tar-paper patch, rocks holding it down
    prof = [(y0 - 0.45, zf + 0.06 + 0.45 * (zf - zbk) / L), (y1 + 0.3, zbk + 0.06 - 0.3 * (zf - zbk) / L)]
    rf = ak.Part()
    ak.sweep_roof(rf, prof, x0 - 0.3, x1 + 0.3, 0.04, "corrugated:stone-shade", "boards_weathered", "metal:stone-dark")
    xm = (x0 + x1) / 2

    def sag(p):
        f = 1.0 - ((p[:, 0] - xm) / ((x1 - x0) / 2 + 0.3)) ** 2
        p[:, 2] -= 0.09 * np.clip(f, 0, 1)
        return p
    b.add(rf.warp(sag))
    patch = ak.Part()
    ak.sweep_roof(patch, [(y0 + 0.4, ak.profile_z(prof, y0 + 0.4) + 0.05), (y0 + 1.9, ak.profile_z(prof, y0 + 1.9) + 0.05)],
                  x0 + 0.6, x0 + 2.0, 0.01, "roll_roofing:ink-700", "roll_roofing:ink-700", "roll_roofing:ink-700")
    b.add(patch.warp(sag))
    for k in range(3):
        rx, ry = x0 + 0.8 + k * 1.3 + rng.uniform(-0.2, 0.2), y0 + rng.uniform(0.3, 2.4)
        b.cylinder((rx, ry, ak.profile_z(prof, ry) + 0.02), (rx, ry, ak.profile_z(prof, ry) + 0.2), 0.14,
                   "rubble:stone-base", n=7)
    # the stovepipe through the roof, leaning, with a rain cap; guy wire
    sx, sy = x1 - 0.8, y1 - 0.9
    zr = ak.profile_z(prof, sy)
    b.cylinder((sx, sy, zr - 0.3), (sx + 0.12, sy + 0.05, zr + 1.3), 0.075, "metal:stone-dark", n=10)
    b.cylinder((sx + 0.12, sy + 0.05, zr + 1.35), (sx + 0.12, sy + 0.05, zr + 1.45), 0.16, "metal:stone-dark", n=10,
               r1=0.02)
    b.cylinder((sx + 0.11, sy + 0.05, zr + 1.25), (x1 + 0.1, y1 + 0.1, zbk - 0.2), 0.006, "metal:ink-700", n=4, caps=False)
    # the woodpile against the west end (splits in a lean stack, two posts)
    wx = x0 - 0.45
    for r_ in range(4):
        for c in range(9 - r_):
            yy = y0 + 0.35 + c * 0.26 + (r_ % 2) * 0.13
            zz = 0.12 + r_ * 0.22
            ln = rng.uniform(0.38, 0.46)
            b.cylinder((wx - ln / 2, yy, zz), (wx + ln / 2, yy, zz + rng.uniform(-0.02, 0.02)), 0.11,
                       "boards_weathered" if (r_ + c) % 3 else "paint:earth-light", n=6)
    a.meta = dict(eave=zbk, ridge=zf + 0.2)
    return a


# ---------------------------------------------------------------------------
# Drive-in concession stand: a 1950s snack bar in painted block, facing SOUTH across the
# ramps toward the screen (the cars' side: its gravel apron is south, the drive comes in
# behind it). A flat roof carried forward as a canopy over the serving front, the dark
# SNACKS band on the canopy fascia; the serving windows and doors boarded with plywood
# long ago; the projection booth on the roof, its two ports aimed at the screen.
# ---------------------------------------------------------------------------

def concession(W, D, zb, ctx):
    a = ak.Assembly("Concession")
    b = a.body
    rng = random.Random(ctx.get("seed", 23))
    wall = f"block:{ctx['wall']}"
    cd = 1.9                                    # the canopy depth, inside the footprint
    x0, y0, x1, y1 = 0.5, cd, W - 0.5, D - 0.4
    rect = (x0, y0, x1, y1)
    zp, zw = 0.2, 3.3
    ak.band(b, rect, zb, zp, 0.05, "stone_trim:stone-base", bottom=False)
    _apron(b, x0 - 0.3, x1 + 0.3, y0, cd + 0.2, 0.1, "stone_trim:stone-base")
    walls = Walls(a, rect, zp, zw, wall, 0.2)
    cx = (x0 + x1) / 2
    serve = [cx - 3.4, cx, cx + 3.4]
    for x in serve:
        walls.add("S", x, 2.4, 1.05, 1.25)
    walls.add("S", x0 + 0.95, 0.9, zp, 2.1)
    walls.add("S", x1 - 0.95, 0.9, zp, 2.1)
    ym = (y0 + y1) / 2
    walls.add("E", ym, 0.9, zp, 2.1)
    walls.add("W", ym, 0.9, zp, 2.1)
    walls.add("N", cx + 2.0, 0.9, zp, 2.1)
    walls.add("N", cx - 2.5, 1.2, 1.4, 0.8)
    walls.build()
    # the openings' dark behind the boards (a lining), then the boards
    for s, M in walls.frames.items():
        for u0, u1, z0, z1 in walls.open[s]:
            b.add(ak.Part().quad((u0, 0.15, z0), (u1, 0.15, z0), (u1, 0.15, z1), (u0, 0.15, z1), "interior_dark"), M)
            w, h = u1 - u0, z1 - z0
            planks = h < 1.5 and rng.random() < 0.5
            board = ak.boarded(w, h, rng, "plywood:earth-light", planks=planks)
            b.add(board, M @ T((u0 + u1) / 2, 0.0, z0))
    # the serving counter ledge under each window
    for x in serve:
        b.box(x - 1.3, y0 - 0.4, 0.98, x + 1.3, y0, 1.05, "stone_trim:stone-pale")
    # the flat roof + canopy slab, the fascia with the SNACKS band, two steel posts
    zr = zw + 0.1
    b.box(x0 - 0.3, 0.1, zr, x1 + 0.3, y1 + 0.3, zr + 0.3, "paint:stone-light")
    b.quad((x0 - 0.3, 0.1, zr + 0.3), (x1 + 0.3, 0.1, zr + 0.3), (x1 + 0.3, y1 + 0.3, zr + 0.3), (x0 - 0.3, y1 + 0.3, zr + 0.3),
           "gravel_roof")
    b.box(x0 - 0.35, 0.02, zr - 0.35, x1 + 0.35, 0.1, zr + 0.45, "paint:stone-light")
    for x in (x0 + 0.1, x1 - 0.1):
        b.cylinder((x, 0.35, 0.0), (x, 0.35, zr), 0.06, "metal:stone-dark", n=10)
    _wallband(a, ctx["band_x"], 0.02, zr - 0.3, ctx["band_text"], ctx["band_lit"], px=0.09, depth=0.1)
    # the projection booth: a block box on the roof toward the front, two small ports
    bx0, bx1, by0, by1 = cx - 1.9, cx + 1.9, y0 + 0.4, y0 + 3.4
    bz0, bz1 = zr + 0.3, zr + 2.7
    booth = Walls(a, (bx0, by0, bx1, by1), bz0, bz1, wall, 0.2)
    for x in (cx - 0.9, cx + 0.9):
        booth.add("S", x, 0.45, bz0 + 1.2, 0.32)
    booth.add("N", cx + 0.9, 0.8, bz0, 1.95)
    booth.build()
    for x in (cx - 0.9, cx + 0.9):
        b.quad((x - 0.225, by0 + 0.15, bz0 + 1.2), (x + 0.225, by0 + 0.15, bz0 + 1.2), (x + 0.225, by0 + 0.15, bz0 + 1.52),
               (x - 0.225, by0 + 0.15, bz0 + 1.52), "interior_dark")
    b.add(rear(ak.boarded(0.8, 1.95, rng, "plywood:earth-light").moved(T(cx + 0.9, 0, bz0)), cx + 0.9, by1))
    ak.flat_roof(b, bx0, by0, bx1, by1, bz1 + 0.15, 0.25, 0.12, "gravel_roof", "paint:stone-light")
    b.cylinder((bx1 - 0.6, by1 - 0.6, bz1 + 0.15), (bx1 - 0.6, by1 - 0.6, bz1 + 0.9), 0.1, "metal:stone-dark", n=8)
    # the back: a vent hood, a stovepipe, a downspout
    b.box(cx - 3.8, y1, 2.2, cx - 3.0, y1 + 0.35, 2.7, "metal:stone-light")
    b.cylinder((x1 - 0.5, y1 + 0.12, 0.05), (x1 - 0.5, y1 + 0.12, zr + 0.1), 0.05, "metal:stone-shade", n=8)
    a.meta = dict(eave=zr + 0.3, ridge=bz1 + 0.3)
    return a


# ---------------------------------------------------------------------------
# From the dump
# ---------------------------------------------------------------------------

DESIGNS = {"gas_station": gas_station, "garage": garage, "fireworks_stand": fireworks_stand, "billies": billies,
           "police": police, "hardware": hardware, "salon": salon, "shack": shack, "concession": concession}

# The per-building calls (Kevin's end review lists them): lit windows at dusk.
DESIGN_NOTES = {
    "gas_station": "office plate glass + door lit (open 7 AM-1 AM), stock room / restrooms dark",
    "garage": "all dark (for sale)", "fireworks_stand": "shut, hatch flap down (seasonal), no window",
    "billies": "front + east windows and the kitchen lit (open all hours)",
    "police": "front office windows lit (3 of 4) + door transom, cells dark",
    "hardware": "all dark (closed until further notice)",
    "salon": "lit (Sam's hours end 5 PM: Phase 8 may darken it for dusk)",
    "shack": "one small window, a faint lamp (glow 0.35)", "concession": "all dark (boarded)",
}


def local_x(bld, facing, tile_x):
    """A dump x (tiles, may be fractional: a door tile's centre, a sign's px / 16) ->
    local x along the building's front."""
    if facing == "N":
        return (bld["x"] + bld["w"] - tile_x) * TM
    return (tile_x - bld["x"]) * TM


def context(placed, bld, facing):
    ctx = {"wall": bld["wall"]}
    px = config.TILE_PX
    if bld.get("door"):
        ctx["door_x"] = local_x(bld, facing, bld["door"]["x"] + 0.5)
    for s in placed.data["signs"]:
        if s.get("building") != bld["id"]:
            continue
        x = local_x(bld, facing, s["px"][0] / px)
        if s["kind"] == "wallband":
            assert s["text"] == bld["label"], (s, bld["label"])
            ctx.update(band_x=x, band_text=s["text"], band_lit=bool(s.get("lit")))
        elif s["kind"] == "neon":
            assert s["text"] == "OPEN", s
            ctx["neon_x"] = x
        elif s["kind"] == "bracket":
            ctx["bracket_x"] = x
    # a kerb cut in front (the garage's big bay sits on it)
    for p in placed.data["props"]:
        if p["kind"] == "kerb_cut" and p["x"] < bld["x"] + bld["w"] and p["x"] + p["w"] > bld["x"]:
            if (facing == "N" and p.get("side") == "S" and p["y"] < bld["y"]) or \
                    (facing == "S" and p.get("side") == "N" and p["y"] > bld["y"]):
                ctx["cut_x"] = local_x(bld, facing, p["x"] + p["w"] / 2)
    if "door_x" not in ctx:
        ctx["door_x"] = bld["w"] * TM / 2
    if "cut_x" not in ctx:
        ctx["cut_x"] = ctx["door_x"]
    return ctx


def town_buildings(world, only=None):
    for placed in world.maps.values():
        if only is not None and placed.id != only:
            continue
        for bld in placed.data["buildings"]:
            if bld["id"] in config.TOWN_DESIGNS and bld.get("art") is None:
                yield placed, bld


def build(world, terrain, col, mats, only=None):
    """Design, realise and ground the nine buildings. Returns [dict(name, tris, hull, ...)]
    like buildings_hero.build."""
    import bpy

    import archmats
    sc = bpy.context.scene
    sc[config.SIGN_GLOW_PROP] = config.SIGN_GLOW
    resolve = archmats.Resolver(mats)
    out = []
    for placed, bld in town_buildings(world, only):
        design = config.TOWN_DESIGNS[bld["id"]]
        facing = config.TOWN_FACING.get(bld["id"], "S")
        x0, y0, x1, y1 = bh.footprint(placed, bld)
        Mxy, W, D = ak.place(x0, y0, x1, y1, facing)
        lo, hi = terrain.z_range(x0, y0, x1, y1, n=7) if terrain is not None else (0.0, 0.0)
        zb = -(hi - lo) - 0.3
        asm = DESIGNS[design](W, D, zb, context(placed, bld, facing))
        name = f"Bldg_{placed.id}_{bld['id']}"
        Mw = T(0, 0, hi) @ Mxy
        body, kids = ak.realise(asm, col, Mw, resolve, name, map_id=placed.id, dump_id=bld["id"], design=design,
                                place=bld["place"], label=bld["label"], state=bld["state"], facing=facing,
                                kind="building", lit=DESIGN_NOTES[design])
        for k, p in asm.meta.get("mounts", {}).items():
            body[f"mount_{k}"] = [float(v) for v in ak.apply(Mw, [p])[0]]
        out.append(bh.realised_info(name, body, kids, hi))
    return out

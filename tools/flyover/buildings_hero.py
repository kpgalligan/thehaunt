"""The five buildings with real front art, designed in full from archkit parts: the town
hall, the general store, the motel (office + four-room strip), the farmhouse and the
barn (derelict: the dump's new-game state). They replace the Phase 2 boxes.

The art (docs/designs: town art, motel signage, farm interiors) draws FRONTS only; the
sides, backs and roofs here are designed as a real New England building of that type
and era would have them, in the five-band grammar (stone foundation / wall of cream
clapboard or stone-light masonry / eave / roof of slate or cedar shake / ridge, and a
chimney only where a hearth is drawn). Real proportions (1 tile = 2.5 m for position,
believable heights); horizontal positions of doors / windows / signs come from the art
(the pixel art is ~3x wider than tall at 2.5 m a tile, so SIZES are real, not drawn).

Every building: footprint, facing and door from the dump; the building's local frame
(archkit.place) has its origin at the footprint's front-left corner, on the HIGHEST
ground under it; the foundation runs down past the lowest. Lit windows (the art's lit
panes / the dump's glow lights) carry glow = 1 (LF_Window_Glass; the scene property
config.WINDOW_GLOW_PROP lights them all at once); the motel's googie tube, OFFICE
letters and vending fronts are LF_Neon_* (config.NEON_GLOW_PROP). Canon lettering only
(the dump's motel "lettering": OFFICE, 1-4, ICE); the store's bracket sign is blank.

build() returns the per-building height fields (hulls) that forest.clear_crowns keeps
the tree crowns out of, and triangle counts.
"""

import math
import random

import numpy as np

import archkit as ak
import config
import pixelfont
from archkit import T, Rz

TM = config.TILE_M


# ---------------------------------------------------------------------------
# Walls around a rectangular body, openings given in building coordinates
# ---------------------------------------------------------------------------

class Walls:
    """The four walls of a body x0..x1, y0..y1 from z0 to z1 (or to a per-side top
    outline), with openings and the prototypes that fill them. Sides S E N W walk the
    footprint counter-clockwise; an opening is placed by its centre in building
    coordinates (x on S / N, y on E / W)."""

    def __init__(self, asm, rect, z0, z1, mat, depth, reveal=None):
        self.asm, self.rect, self.z0, self.z1 = asm, rect, z0, z1
        self.mat, self.depth, self.reveal = mat, depth, reveal
        x0, y0, x1, y1 = rect
        self.ends = {"S": ((x0, y0), (x1, y0)), "E": ((x1, y0), (x1, y1)),
                     "N": ((x1, y1), (x0, y1)), "W": ((x0, y1), (x0, y0))}
        self.open = {k: [] for k in "SENW"}
        self.items = {k: [] for k in "SENW"}
        self.tops = {}
        self.frames = {}

    def u(self, side, c):
        x0, y0, x1, y1 = self.rect
        return {"S": c - x0, "E": c - y0, "N": x1 - c, "W": y1 - c}[side]

    def length(self, side):
        (a, b) = self.ends[side]
        return math.dist(a, b)

    def add(self, side, c, w, z0, h, proto=None, hole=True, **props):
        u = self.u(side, c)
        if hole:
            self.open[side].append((u - w / 2, u + w / 2, z0, z0 + h))
        if proto:
            self.items[side].append((proto, u, z0, props))

    def top(self, side, pts):
        """A gable / gambrel end: the top outline [(u, z)] from u = 0 to the length."""
        self.tops[side] = sorted(pts)

    def build(self, sides="SENW"):
        for s in sides:
            L = self.length(s)
            top = self.tops.get(s) or [(0.0, self.z1), (L, self.z1)]
            outline = [(0.0, self.z0), (L, self.z0)] + [p for p in reversed(top)]
            M = ak.wall(self.asm, *self.ends[s], outline, self.mat, self.open[s], self.depth, self.reveal)
            self.frames[s] = M
            for proto, u, z, props in self.items[s]:
                self.asm.on_wall(proto, M, u, z, **props)
        return self


def profile_tops(profile, a0, a1, reverse=False):
    """Top outline of an end wall from an underside profile [(a, z)] across a0..a1."""
    pts = [(a0, ak.profile_z(profile, a0)), (a1, ak.profile_z(profile, a1))]
    pts += [(a, z) for a, z in profile if a0 < a < a1]
    return sorted(((a1 - a) if reverse else (a - a0), z) for a, z in pts)


def rear(part, cx, y_wall):
    """A Part authored as if on the front (outside -Y, x = building x) moved to the
    back wall at y_wall, mirrored about x = cx."""
    return part.moved(T(cx, y_wall) @ Rz(math.pi) @ T(-cx, 0))


def downspout(part, x, y_wall, y_gutter, z_gutter, mat, z_foot=0.05, side=-1):
    """A downspout on the wall face at (x, y_wall + side * 0.08), with an offset jog up
    to the gutter at y_gutter."""
    yw = y_wall + side * 0.09
    zj = z_gutter - 0.45
    part.cylinder((x, yw, z_foot), (x, yw, zj), 0.045, mat, n=8)
    part.cylinder((x, yw, zj), (x, y_gutter, z_gutter - 0.08), 0.045, mat, n=8)
    part.cylinder((x, yw, z_foot), (x, yw + side * 0.25, z_foot - 0.02), 0.045, mat, n=8)   # splash shoe


# ---------------------------------------------------------------------------
# Town hall: masonry, two storeys, hipped slate roof, cupola (the tallest in town)
# ---------------------------------------------------------------------------

def town_hall(W, D, zb, ctx):
    a = ak.Assembly("TownHall")
    b = a.body
    x0, y0, x1, y1 = 0.6, 0.0, W - 0.6, D - 0.6
    rect = (x0, y0, x1, y1)
    cx = W / 2
    zp, zw = 1.0, 9.4                 # plinth top (floor), wall top (eave)
    ak.band(b, rect, zb, zp, 0.1, "ashlar:stone-base", bottom=False)
    ak.band(b, rect, zp - 0.02, zp + 0.14, 0.14, "stone_trim:stone-pale", bottom=True)    # water table
    ak.band(b, rect, 5.2, 5.36, 0.06, "stone_trim:stone-pale")                             # storey belt
    ak.band(b, rect, zw - 0.62, zw - 0.42, 0.09, "stone_trim:stone-pale")                  # cornice
    ak.band(b, rect, zw - 0.42, zw - 0.17, 0.22, "stone_trim:stone-pale")

    win_lo = a.proto("Win_Lower", lambda: ak.window(1.1, 2.3, 0.34, (2, 1), frame="trim_dark",
                                                    sill="stone_trim:stone-pale", head="stone_trim:stone-pale"))
    win_up = a.proto("Win_Upper", lambda: ak.window(1.1, 2.2, 0.34, (2, 1), frame="trim_dark",
                                                    sill="stone_trim:stone-pale", head="stone_trim:stone-pale"))
    door_w, door_h, side, transom = 2.0, 2.8, 0.45, 0.6
    door = a.proto("Door_Main", lambda: ak.door(door_w, door_h, 0.34, leaves=2, paint="paint:wood-warm",
                                                transom=transom, sidelights=side))
    door_back = a.proto("Door_Back", lambda: ak.door(1.1, 2.4, 0.34, paint="paint:wood-warm", transom=0.45))
    walls = Walls(a, rect, zp, zw, "ashlar:stone-light", 0.34)
    bays = [cx - 6.6, cx - 3.4, cx + 3.4, cx + 6.6]
    lit_lo = {bays[1], bays[2]}       # the art: the inner lower panes lit, the outer dark
    for x in bays:
        walls.add("S", x, 1.1, 2.0, 2.3, win_lo, glow=1.0 if x in lit_lo else 0.0)
        walls.add("S", x, 1.1, 6.0, 2.2, win_up, glow=1.0)
    walls.add("S", cx, door_w + 2 * side, zp, door_h + transom, door, glow=1.0)
    for k, x in enumerate(bays):
        walls.add("N", x, 1.1, 6.0, 2.2, win_up, glow=1.0 if k in (0, 3) else 0.0)
        walls.add("N", x, 1.1, 2.0, 2.3, win_lo, glow=0.0)
    walls.add("N", cx, 1.1, zp, 2.85, door_back, glow=0.0)
    for k in range(3):
        y = y0 + (y1 - y0) * (k + 0.5) / 3
        walls.add("E", y, 1.1, 2.0, 2.3, win_lo, glow=0.0)
        walls.add("E", y, 1.1, 6.0, 2.2, win_up, glow=1.0 if k == 0 else 0.0)
        walls.add("W", y, 1.1, 2.0, 2.3, win_lo, glow=1.0 if k == 1 else 0.0)
        walls.add("W", y, 1.1, 6.0, 2.2, win_up, glow=1.0 if k < 2 else 0.0)
    walls.build()

    # the main door's stone surround (pilasters, entablature, cornice hood)
    hw = door_w / 2 + side
    top = zp + door_h + transom
    for sx in (-1, 1):
        xa, xb = sorted((cx + sx * hw, cx + sx * (hw + 0.34)))
        b.box(xa, -0.12, zp, xb, 0.0, top + 0.05, "stone_trim:stone-pale", skip=("back",))
        b.box(xa - 0.04, -0.16, zp, xb + 0.04, 0.0, zp + 0.3, "stone_trim:stone-pale", skip=("back",))
    b.box(cx - hw - 0.4, -0.16, top + 0.05, cx + hw + 0.4, 0.0, top + 0.5, "stone_trim:stone-pale", skip=("back",))
    b.box(cx - hw - 0.55, -0.3, top + 0.5, cx + hw + 0.55, 0.0, top + 0.64, "stone_trim:stone-pale", skip=("back",))
    # the front steps (granite, cheek walls) and the back stoop
    ak.steps(b, cx - 1.8, cx + 1.8, 0.0, zp, 5, 0.32, "stone_trim:stone-light", cheek="ashlar:stone-base")
    back = ak.Part()
    ak.steps(back, cx - 0.9, cx + 0.9, 0.0, zp, 4, 0.3, "stone_trim:stone-light")
    back.box(cx - 0.95, -0.12, zp + 2.9, cx + 0.95, 0.0, zp + 3.12, "stone_trim:stone-pale", skip=("back",))
    b.add(rear(back, cx, y1))

    # hipped slate roof (stone cornice below, lead-dark edge, hip caps)
    zr = ak.hip_roof(b, x0, y0, x1, y1, zw, 30.0, 0.55, 0.22, "slate", "trim:stone-pale", "metal:stone-dark",
                     cap="metal:stone-shade")
    # the cupola over the ridge: clapboard base, a glazed lantern (lit: the Cupola glow),
    # a pyramidal cap and a finial
    cy = (y0 + y1) / 2
    s0, s1 = 1.5, 1.2
    base_top = zr + 1.25
    b.box(cx - s0, cy - s0, zr - 0.6, cx + s0, cy + s0, base_top, "clapboard:cream", skip=("bottom",))
    ak.corner_boards(b, (cx - s0, cy - s0, cx + s0, cy + s0), zr - 0.3, base_top, 0.14, 0.03, "trim:cream")
    ak.band(b, (cx - s0, cy - s0, cx + s0, cy + s0), base_top, base_top + 0.16, 0.08, "trim:cream")
    lz0, lz1 = base_top + 0.16, base_top + 1.85
    lan = Walls(a, (cx - s1, cy - s1, cx + s1, cy + s1), lz0, lz1, "clapboard:cream", 0.12)
    cwin = a.proto("Win_Cupola", lambda: ak.window(0.9, 1.25, 0.12, (2, 2), sash="fixed", frame="trim_dark",
                                                   casing="trim:cream", casing_w=0.08))
    for s in "SENW":
        c = cx if s in "SN" else cy
        lan.add(s, c, 0.9, lz0 + 0.25, 1.25, cwin, glow=1.0)
    lan.build()
    ak.corner_boards(b, (cx - s1, cy - s1, cx + s1, cy + s1), lz0, lz1, 0.12, 0.03, "trim:cream")
    ak.band(b, (cx - s1, cy - s1, cx + s1, cy + s1), lz1, lz1 + 0.18, 0.12, "trim:cream")
    zc = ak.hip_roof(b, cx - s1, cy - s1, cx + s1, cy + s1, lz1 + 0.18, 42.0, 0.28, 0.1, "metal:stone-dark",
                     "trim:cream", "trim:cream")
    b.cylinder((cx, cy, zc), (cx, cy, zc + 1.3), 0.035, "metal:stone-dark", n=8)
    b.cylinder((cx, cy, zc + 0.55), (cx, cy, zc + 0.75), 0.12, "metal:stone-dark", n=10)
    a.meta = dict(eave=zw, ridge=zr + 0.22, top=zc + 1.3)
    return a


# ---------------------------------------------------------------------------
# General store: clapboard, one storey, cedar-shake side gable, three windows, blade
# sign on an iron bracket (no lettering). Open or closed by ShopHours at the film's
# minute (StoreFacade.cs swaps building_store.png's variants): open = lit windows, the
# door open on a lit interior; closed (the film's 18:00) = the outer two windows behind
# shut louvred shutters, the middle one dark glass, the panelled door shut, all dark.
# ---------------------------------------------------------------------------

def shutters(w, h, paint="paint:wood-warm"):
    """A pair of closed louvred shutters filling a w x h opening, in the reveal just
    behind the wall face (window coordinates: centred x, bottom z 0, outside -Y)."""
    p = ak.Part()
    hw, y0, st = w / 2, 0.012, 0.07
    for x0, x1 in ((-hw, -0.004), (0.004, hw)):
        p.box(x0, y0 + 0.03, 0.0, x1, y0 + 0.05, h, "paint:earth-dark", skip=("back",))   # the dark behind the louvres
        p.box(x0, y0, 0.0, x0 + st, y0 + 0.03, h, paint, skip=("back",))                  # stiles
        p.box(x1 - st, y0, 0.0, x1, y0 + 0.03, h, paint, skip=("back",))
        for za, zb in ((0.0, st * 1.4), (h / 2 - st / 2, h / 2 + st / 2), (h - st, h)):      # rails
            p.box(x0 + st, y0, za, x1 - st, y0 + 0.03, zb, paint, skip=("back",))
        for z0, z1 in ((st * 1.4, h / 2 - st / 2), (h / 2 + st / 2, h - st)):             # the louvres
            n = max(2, int((z1 - z0) / 0.075))
            for k in range(n):
                za = z0 + (z1 - z0) * k / n
                p.box(x0 + st, y0 + 0.004, za + 0.008, x1 - st, y0 + 0.026, za + (z1 - z0) / n, paint,
                      skip=("back",))
        p.cylinder((x1 - 0.12 if x0 < 0 else x0 + 0.12, y0, h / 2), (x1 - 0.12 if x0 < 0 else x0 + 0.12, y0 - 0.04,
                   h / 2), 0.015, "metal:ink-900", n=6)                                       # the turn-button
    return p


def general_store(W, D, zb, ctx):
    a = ak.Assembly("GeneralStore")
    b = a.body
    x0, y0, x1, y1 = 0.5, 0.0, W - 0.5, D - 0.5
    rect = (x0, y0, x1, y1)
    zp, zw = 0.6, 4.3
    pitch, ov, rake, thick = 34.0, 0.45, 0.35, 0.2
    ak.band(b, rect, zb, zp, 0.06, "rubble:stone-shade", bottom=False)
    prof = ak.gable_profile(y0, y1, zw, pitch, ov)
    win = a.proto("Win", lambda: ak.window(1.25, 1.65, 0.16, (2, 2), frame="trim_dark", casing="trim_dark",
                                           casing_w=0.12, sill="trim:cream"))
    attic = a.proto("Win_Attic", lambda: ak.window(0.7, 0.95, 0.16, (2, 2), sash="fixed", frame="trim_dark",
                                                   casing="trim_dark", casing_w=0.1, sill="trim:cream"))
    is_open = ctx["open"]
    if is_open:
        door = a.proto("Door", lambda: ak.door(1.0, 2.25, 0.16, paint="paint:wood-warm", glass_upper=True,
                                               transom=0.4, casing="trim_dark", open_deg=72.0,
                                               interior=(1.8, "paint:earth-base", "deck:wood-warm")))
    else:
        door = a.proto("Door_Shut", lambda: ak.door(1.0, 2.25, 0.16, paint="paint:wood-warm", transom=0.4,
                                                    casing="trim_dark"))

    def _shuttered():
        p = ak.window(1.25, 1.65, 0.16, (2, 2), frame="trim_dark", casing="trim_dark", casing_w=0.12,
                      sill="trim:cream")
        p.add(shutters(1.25, 1.65), T())
        return p
    shut = a.proto("Win_Shuttered", _shuttered) if not is_open else win
    door_back = a.proto("Door_Back", lambda: ak.door(1.0, 2.2, 0.16, paint="paint:wood-warm", casing="trim_dark"))
    walls = Walls(a, rect, zp, zw, "clapboard:cream", 0.16)
    xd = ctx["door_x"]
    lit = 1.0 if is_open else 0.0
    for k, xa in enumerate(ctx["windows"]):
        walls.add("S", xa, 1.25, 1.45, 1.65, shut if k != 1 else win, glow=lit)
    walls.add("S", xd, 1.0, zp, 2.65, door, glow=lit)
    for x in (x0 + 3.2, x1 - 3.2):
        walls.add("N", x, 1.25, 1.45, 1.65, win, glow=0.0)
    walls.add("N", x0 + 6.4, 1.0, zp, 2.2, door_back, glow=0.0)
    ym = (y0 + y1) / 2
    for s in "EW":
        walls.add(s, ym, 1.25, 1.45, 1.65, win, glow=lit if s == "W" else 0.0)
        walls.add(s, ym, 0.7, zw + 0.9, 0.95, attic, glow=0.0)
    walls.top("E", profile_tops(prof, y0, y1))
    walls.top("W", profile_tops(prof, y0, y1, reverse=True))
    walls.build()
    ak.corner_boards(b, rect, zp, zw, 0.14, 0.03, "trim:cream")
    for s in "SN":      # frieze board under the eaves
        p0, p1 = walls.ends[s]
        ak.band_run(b, p0, p1, zw - 0.28, zw, 0.035, "trim:cream")
    # the cedar-shake roof, cream rake / fascia boards, a ridge board
    ak.sweep_roof(b, prof, x0 - rake, x1 + rake, thick, "shake", "trim:cream", "trim:cream")
    zr = prof[1][1] + thick
    b.beam((x0 - rake - 0.02, ym, zr + 0.02), (x1 + rake + 0.02, ym, zr + 0.02), 0.26, 0.07, "paint:earth-dark")
    # gutters front and back, downspouts at the corners
    ze = prof[0][1]
    for s, yg, side in (("S", y0 - ov, -1), ("N", y1 + ov, 1)):
        p0, p1 = ((x0 - rake, yg), (x1 + rake, yg)) if s == "S" else ((x1 + rake, yg), (x0 - rake, yg))
        ak.gutter(b, p0, p1, ze + 0.12, 0.0, "metal:stone-shade")
        yw = y0 if s == "S" else y1
        for x in (x0 + 0.35, x1 - 0.35):
            downspout(b, x, yw, yg + side * 0.07, ze + 0.12, "metal:stone-shade", side=side)
    # steps to the front door and the back door
    ak.steps(b, xd - 0.95, xd + 0.95, 0.0, zp, 2, 0.32, "stone_trim:stone-light")
    back = ak.Part()
    ak.steps(back, x0 + 5.8, x0 + 7.0, 0.0, zp, 2, 0.3, "stone_trim:stone-light")
    b.add(rear(back, x0 + 6.4, y1))
    # the blade sign: an iron bracket off the front wall near the east corner, the board
    # hanging square to the road (blank: two cream rules, as drawn)
    sx, sz = x1 - 0.32, zw - 0.55
    b.box(sx - 0.05, -0.02, sz - 0.35, sx + 0.05, 0.0, sz + 0.12, "metal:ink-900", skip=("back",))
    b.beam((sx, -0.02, sz), (sx, -1.25, sz), 0.035, 0.05, "metal:ink-900")
    b.beam((sx, -0.02, sz - 0.3), (sx, -0.75, sz - 0.02), 0.025, 0.025, "metal:ink-900")
    bw, bh = 0.95, 0.62
    by0, by1 = -1.18, -1.18 + bw
    bz1 = sz - 0.14
    for yy in (by0 + 0.08, by1 - 0.08):
        b.cylinder((sx, yy, bz1), (sx, yy, sz - 0.02), 0.008, "metal:ink-900", n=5)
    b.box(sx - 0.03, by0, bz1 - bh, sx + 0.03, by1, bz1, "paint:wood-warm")
    for zz in (bz1 - bh * 0.36, bz1 - bh * 0.62):
        for f in (-1, 1):
            b.box(min(sx + f * 0.03, sx + f * 0.034), by0 + 0.14, zz - 0.022, max(sx + f * 0.03, sx + f * 0.034),
                  by1 - 0.14, zz + 0.022, "paint:cream")
    a.meta = dict(eave=zw, ridge=zr)
    return a


# ---------------------------------------------------------------------------
# Motel: a 1958 googie motor court. Office (taller, the upswept eave overhanging west,
# the aqua stripe, the OFFICE box, plate glass, a red door, the soda machine) and the
# four-room strip (flat gravel roof, a canopy over the walk on aqua posts with the googie
# stripe, doors red / aqua / red / aqua with plaques 1-4, the ICE alcove at the east end)
# ---------------------------------------------------------------------------

MOTEL_DEPTH = {"office": 10.0, "strip": 9.0}     # real depths; the 2D footprint is deeper


def _unit_x(strip_x0, strip_w, px):
    """A pixel position on the drawn strip (0..288 = the strip's width) -> local x."""
    return strip_x0 + px / 288.0 * strip_w


def motel(W, D, zb, ctx):
    a = ak.Assembly("Motel")
    b = a.body
    ox0, ox1 = ctx["office"]
    sx0, sx1 = ctx["strip"]
    aqua = "enamel:aqua"
    # ---- office ----
    od = MOTEL_DEPTH["office"]
    ozw = 4.4
    orect = (ox0 + 0.3, 0.0, ox1, od)
    ak.band(b, orect, zb, 0.26, 0.02, "metal:stone-dark", bottom=False)                  # kick plate
    glass_w, glass_c = 5.0, ctx["office_glass_x"]
    plate = a.proto("Office_Glass", lambda: ak.window(glass_w, 2.1, 0.12, (2, 1), sash="fixed",
                                                      frame="metal:stone-pale", sill="trim:cream"))
    odoor = a.proto("Office_Door", lambda: ak.door(0.95, 2.15, 0.12, paint=f"paint:{ctx['office_door']}",
                                                   panels=False, frame="trim_dark"))
    oside = a.proto("Office_Side", lambda: ak.window(1.6, 1.2, 0.12, (2, 1), sash="fixed", frame="metal:stone-pale",
                                                     sill="trim:cream"))
    oback = a.proto("Office_BackDoor", lambda: ak.door(0.9, 2.1, 0.12, paint="paint:stone-light", panels=False))
    ow = Walls(a, orect, 0.26, ozw, "enamel:cream", 0.12)
    ow.add("S", glass_c, glass_w, 0.8, 2.1, plate, glow=1.0)
    ow.add("S", ctx["office_door_x"], 0.95, 0.26, 2.15, odoor, glow=0.0)
    ow.add("E", od * 0.55, 1.6, 1.3, 1.2, oside, glow=1.0)
    ow.add("W", od * 0.6, 1.6, 1.3, 1.2, oside, glow=0.0)
    ow.add("N", (orect[0] + orect[2]) / 2 + 2.5, 0.9, 0.26, 2.1, oback, glow=0.0)
    ow.add("N", (orect[0] + orect[2]) / 2 - 2.0, 1.6, 1.3, 1.2, oside, glow=1.0)
    ow.build()
    ak.flat_roof(b, *orect, ozw + 0.5, 0.5, 0.06, "gravel_roof", "paint:stone-shade", parapet=0.08)
    # the upswept eave: a thin slab over the front, cantilevered west past the wall and
    # sweeping up toward its tip; dark edge over the aqua googie stripe, a neon tube under
    ez, et = ozw - 0.3, 0.16
    eave = ak.Part()
    ak.sweep_roof(eave, [(-1.0, ez), (0.25, ez)], ox0 - 1.35, ox1 + 0.25, et, "metal:stone-shade", "enamel:cream",
                  "trim_dark")
    eave.box(ox0 - 1.35, -1.04, ez - 0.2, ox1 + 0.25, -1.0, ez, aqua)                   # front stripe
    eave.box(ox0 - 1.39, -1.04, ez - 0.2, ox0 - 1.35, 0.25, ez, aqua)                    # west stripe
    eave.box(ox0 - 1.39, -1.06, ez - 0.28, ox0 - 1.0, -1.0, ez - 0.2, "trim_dark")      # the tip's drop
    tip0 = orect[0]

    def upsweep(p):
        dx = np.clip(tip0 - p[:, 0], 0.0, None)
        p[:, 2] += dx * 0.26 + dx * dx * 0.05
        return p
    eave.warp(upsweep)
    b.add(eave)
    tube = ak.Part().cylinder((ox0 - 1.3, -1.02, ez - 0.22), (ox1 + 0.2, -1.02, ez - 0.22), 0.018, "neon:aqua", n=6)
    a.protos["Neon_Office"] = tube.warp(upsweep)
    a.place("Neon_Office", np.eye(4), glow=1.0)
    # OFFICE box sign on the front wall above the glass (letters LF_Neon: aqua by day,
    # the art's amber at night)
    bx, bw, bz0, bz1 = ctx["office_sign_x"], 4.3, 3.05, 3.95
    b.box(bx - bw / 2, -0.28, bz0, bx + bw / 2, 0.0, bz1, "paint:ink-900", skip=("back",))
    letters = pixelfont.text_part(ctx["office_text"], 0.11, 0.03, "neon:aqua/lantern")
    a.protos["Office_Letters"] = letters
    a.place("Office_Letters", T(bx, -0.28, (bz0 + bz1) / 2 + 0.275), glow=1.0)
    # the soda machine under the eave, west of the office's front corner
    sm_x = orect[0] - 0.8
    b.box(sm_x - 0.42, -0.8, 0.0, sm_x + 0.42, -0.1, 1.75, "paint:barn-red")
    b.box(sm_x - 0.3, -0.85, 1.05, sm_x + 0.3, -0.8, 1.55, "paint:cream", skip=("back",))
    b.box(sm_x - 0.12, -0.85, 0.55, sm_x + 0.12, -0.8, 0.8, "paint:ink-900", skip=("back",))
    b.box(sm_x - 0.44, -0.82, 1.75, sm_x + 0.44, -0.08, 1.8, "metal:stone-shade")
    # office roof: a flue and a vent
    b.cylinder((ox1 - 2.0, od - 2.0, ozw + 0.4), (ox1 - 2.0, od - 2.0, ozw + 1.5), 0.12, "metal:stone-shade", n=10)
    b.box(ox0 + 2.0, od - 3.0, ozw + 0.45, ox0 + 3.0, od - 2.2, ozw + 0.95, "metal:stone-light")

    # ---- the room strip ----
    sd = MOTEL_DEPTH["strip"]
    szw = 3.3
    srect = (sx0, 0.0, sx1, sd)
    sw_ = sx1 - sx0
    ak.band(b, srect, zb, 0.26, 0.02, "metal:stone-dark", bottom=False)
    rwin = a.proto("Room_Window", lambda: ak.window(2.4, 1.35, 0.12, (2, 2), sash="fixed",
                                                    frame="metal:stone-pale", sill="trim:cream"))
    bath = a.proto("Bath_Window", lambda: ak.window(0.7, 0.5, 0.12, (1, 1), sash="fixed", frame="metal:stone-pale"))
    sw = Walls(a, srect, 0.26, szw, "enamel:cream", 0.12)
    alc_c, alc_w, alc_h, alc_d = _unit_x(sx0, sw_, 272.0), 2.5, 2.55, 1.2
    for r in ctx["rooms"]:
        i = r["room"] - 1
        dx = _unit_x(sx0, sw_, 13 + 68 * i)
        wx = _unit_x(sx0, sw_, 44 + 68 * i)
        name = f"Room_Door_{r['paint'].lstrip('#')}"
        a.proto(name, lambda r=r: ak.door(0.95, 2.15, 0.12, paint=f"paint:{r['paint']}", panels=False,
                                          frame="trim_dark"))
        sw.add("S", dx, 0.95, 0.26, 2.15, name, glow=0.0)
        sw.add("S", wx, 2.4, 0.95, 1.35, rwin, glow=1.0 if r["room"] == ctx["lit_room"] else 0.0)
        # the number plaque over the door (ink-900, a cream 3x5 digit)
        b.box(dx - 0.19, -0.04, 2.5, dx + 0.19, 0.0, 2.8, "paint:ink-900", skip=("back",))
        pixelfont.text(b, str(r["room"]), dx, 2.73, 0.045, 0.012, "paint:cream", y=-0.04)
        # the back: a small high bath window per room
        sw.add("N", sx1 - (wx - sx0), 0.7, 1.9, 0.5, bath, glow=0.0)
    sw.add("S", alc_c, alc_w, 0.26, alc_h - 0.26)
    sw.build()
    # the ICE alcove: a recess lined in stone-shade with the ice machine (aqua) and a
    # soda machine (red), fronts lit on the neon circuit; ICE on a plate over the recess
    ax0, ax1 = alc_c - alc_w / 2, alc_c + alc_w / 2
    zf = 0.26
    rec = "paint:stone-shade"
    b.quad((ax0, alc_d, zf), (ax1, alc_d, zf), (ax1, alc_d, alc_h), (ax0, alc_d, alc_h), rec)          # back
    b.quad((ax0, 0, zf), (ax0, alc_d, zf), (ax0, alc_d, alc_h), (ax0, 0, alc_h), rec)                  # west side
    b.quad((ax1, 0, zf), (ax1, 0, alc_h), (ax1, alc_d, alc_h), (ax1, alc_d, zf), rec)                  # east side
    b.quad((ax0, 0, alc_h), (ax0, alc_d, alc_h), (ax1, alc_d, alc_h), (ax1, 0, alc_h), rec)            # ceiling
    b.quad((ax0, 0, zf), (ax1, 0, zf), (ax1, alc_d, zf), (ax0, alc_d, zf), "stone_trim:stone-base")   # floor
    for mx0, colour in ((ax0 + 0.2, "aqua"), (ax0 + 1.3, "barn-red")):
        b.box(mx0, alc_d - 0.75, zf, mx0 + 1.0, alc_d, zf + 1.85, f"paint:{colour}")
        a.protos.setdefault(f"Vend_{colour}", ak.Part().box(0.0, -0.02, 0.0, 0.8, 0.0, 0.75, f"neon:{colour}",
                                                             skip=("back",)))
        a.place(f"Vend_{colour}", T(mx0 + 0.1, alc_d - 0.75, zf + 0.95), glow=1.0)
    b.box(ax0 - 0.1, -0.05, alc_h, ax1 + 0.1, 0.0, alc_h + 0.42, "paint:ink-900", skip=("back",))
    pixelfont.text(b, ctx["ice_text"], alc_c, alc_h + 0.33, 0.05, 0.015, "paint:cream", y=-0.05)
    # the strip's flat gravel roof and the canopy over the walk
    ak.flat_roof(b, *srect, szw + 0.45, 0.45, 0.05, "gravel_roof", "paint:stone-shade", parapet=0.06)
    cz, cd = szw - 0.12, 2.2
    can = ak.Part()
    ak.sweep_roof(can, [(-cd, cz), (0.0, cz)], sx0, sx1, 0.16, "metal:stone-shade", "enamel:cream", aqua)
    can.box(sx0, -cd - 0.02, cz + 0.16, sx1, -cd + 0.06, cz + 0.2, "trim_dark")          # the shadow line
    b.add(can)
    a.protos["Neon_Canopy"] = ak.Part().cylinder((sx0 + 0.1, -cd + 0.12, cz - 0.03), (sx1 - 0.1, -cd + 0.12, cz - 0.03),
                                                 0.018, "neon:aqua", n=6)
    a.place("Neon_Canopy", np.eye(4), glow=1.0)
    posts = [_unit_x(sx0, sw_, 2 + 68 * i) for i in range(4)] + [sx1 - 0.3]
    for px in posts:
        b.cylinder((px, -cd + 0.2, 0.0), (px, -cd + 0.2, cz), 0.055, "paint:aqua", n=10)
        b.cylinder((px, -cd + 0.2, 0.0), (px, -cd + 0.2, 0.08), 0.1, "metal:stone-dark", n=10)
    # the back: a downspout off each roof scupper, an electric meter, a wall vent per room
    for i in range(5):
        dx = sx0 + 0.6 + i * (sw_ - 1.2) / 4
        b.box(dx - 0.12, sd, szw + 0.05, dx + 0.12, sd + 0.25, szw + 0.2, "metal:stone-shade")
        b.cylinder((dx, sd + 0.12, 0.05), (dx, sd + 0.12, szw + 0.05), 0.05, "metal:stone-shade", n=8)
    b.box(sx0 + 3.0, sd, 1.3, sx0 + 3.35, sd + 0.15, 1.75, "metal:stone-light")
    b.cylinder((sx0 + 3.175, sd + 0.05, 1.75), (sx0 + 3.175, sd + 0.05, szw + 0.2), 0.025, "metal:stone-dark", n=6)
    for r in ctx["rooms"]:
        vx = sx1 - (_unit_x(sx0, sw_, 30 + 68 * (r["room"] - 1)) - sx0)
        b.box(vx - 0.2, sd, 2.55, vx + 0.2, sd + 0.04, 2.8, "metal:stone-light", skip=("front",))
    # rooftop: a plumbing vent per room, two ventilators
    for i in range(4):
        vx = _unit_x(sx0, sw_, 30 + 68 * i)
        b.cylinder((vx, sd - 2.2, szw + 0.4), (vx, sd - 2.2, szw + 1.0), 0.05, "metal:stone-dark", n=8)
    for vx in (_unit_x(sx0, sw_, 70), _unit_x(sx0, sw_, 206)):
        b.box(vx - 0.4, sd - 4.5, szw + 0.4, vx + 0.4, sd - 3.7, szw + 0.85, "metal:stone-light")
    a.meta = dict(eave=szw, ridge=ozw + 0.6)
    return a


# ---------------------------------------------------------------------------
# Farmhouse: cream clapboard, 1.5 storeys under a steep brown-shingle hip, stone plinth,
# a stone chimney (the hearth), a small entry porch on two posts, the door open on a lit
# hall, two lit windows; the half storey lit by gable dormers on the back slope
# ---------------------------------------------------------------------------

def farmhouse(W, D, zb, ctx):
    a = ak.Assembly("Farmhouse")
    b = a.body
    pd = 1.8                                       # the porch, inside the footprint
    x0, y0, x1, y1 = 0.9, pd, W - 0.9, D - 0.6
    rect = (x0, y0, x1, y1)
    zp, zw = 0.7, 4.6
    pitch, ov, thick = 42.0, 0.5, 0.2
    ak.band(b, rect, zb, zp, 0.07, "rubble:stone-shade", bottom=False)
    win = a.proto("Win", lambda: ak.window(1.05, 1.6, 0.16, (2, 1), frame="trim_dark", casing="trim_dark",
                                           casing_w=0.11, sill="stone_trim:stone-light"))
    xd = ctx["door_x"]
    door = a.proto("Door", lambda: ak.door(0.95, 2.2, 0.16, paint="paint:wood-warm", transom=0.4, casing="trim_dark",
                                           open_deg=65.0, interior=(1.8, "paint:earth-light", "deck:wood-warm")))
    kdoor = a.proto("Door_Kitchen", lambda: ak.door(0.9, 2.1, 0.16, paint="paint:wood-warm", glass_upper=True,
                                                    casing="trim_dark"))
    walls = Walls(a, rect, zp, zw, "clapboard:cream", 0.16)
    for xw in ctx["windows"]:
        walls.add("S", xw, 1.05, 1.55, 1.6, win, glow=1.0)
    walls.add("S", xd, 0.95, zp, 2.6, door, glow=1.0)
    for k, y in enumerate((y0 + 2.0, y1 - 2.0)):
        walls.add("E", y, 1.05, 1.55, 1.6, win, glow=1.0 if k == 0 else 0.0)
        walls.add("W", y, 1.05, 1.55, 1.6, win, glow=0.0 if k == 0 else 1.0)
    walls.add("N", x0 + 3.2, 0.9, zp, 2.1, kdoor, glow=1.0)
    walls.add("N", x0 + 5.8, 1.05, 1.55, 1.6, win, glow=1.0)
    walls.add("N", x1 - 3.0, 1.05, 1.55, 1.6, win, glow=0.0)
    walls.build()
    ak.corner_boards(b, rect, zp, zw, 0.14, 0.03, "trim:cream")
    for s in "SENW":
        ak.band_run(b, *walls.ends[s], zw - 0.26, zw, 0.035, "trim:cream")
    zr = ak.hip_roof(b, x0, y0, x1, y1, zw, pitch, ov, thick, "shake", "trim:cream", "trim:cream",
                     cap="paint:earth-dark")
    tp = math.tan(math.radians(pitch))
    # the chimney (fieldstone), through the east hip, above the ridge
    ym = (y0 + y1) / 2
    cxh = ctx["chimney_x"]
    ak.chimney(b, cxh, ym, 0.85, 0.85, zr - 1.0, zr + thick + 0.95, "rubble:stone-shade", "stone_trim:stone-base",
               pots=1, pot_mat="metal:stone-dark")
    # two gable dormers on the back slope light the half storey
    dwin = a.proto("Win_Dormer", lambda: ak.window(0.75, 0.9, 0.12, (2, 1), frame="trim_dark", casing="trim:cream",
                                                   casing_w=0.08))
    for k, xc in enumerate(ctx["dormers"]):
        part, Mf, zfb = gable_dormer(zw + thick, pitch, 0.9, 1.55, 1.25, 45.0, 0.2, 0.12,
                                     "clapboard:cream", "shake", "trim:cream")
        M = T(xc, y1) @ Rz(math.pi)
        b.add(part, M)
        a.on_wall(dwin, M @ Mf, 0.775, zfb + 0.18, glow=1.0 if k == 0 else 0.0)
    # gutters all round the eaves, downspouts at the front and back corners
    ze = zw - ov * tp + 0.1
    X0, Y0, X1, Y1 = x0 - ov, y0 - ov, x1 + ov, y1 + ov
    ak.gutter(b, (X0, Y0), (X1, Y0), ze, 0.0, "metal:stone-shade")
    ak.gutter(b, (X1, Y1), (X0, Y1), ze, 0.0, "metal:stone-shade")
    for x, yw, yg, side in ((x0 + 0.3, y0, Y0 - 0.07, -1), (x1 - 0.3, y0, Y0 - 0.07, -1),
                            (x0 + 0.3, y1, Y1 + 0.07, 1), (x1 - 0.3, y1, Y1 + 0.07, 1)):
        downspout(b, x, yw, yg, ze, "metal:stone-shade", side=side)
    # the entry porch: deck, two posts, a header, a shed roof off the wall, steps
    px0, px1 = xd - 2.1, xd + 2.1
    b.box(px0, 0.0, zb, px1, pd, zp - 0.04, "rubble:stone-shade", skip=("bottom",))
    b.box(px0 - 0.03, -0.03, zp - 0.05, px1 + 0.03, pd, zp, "deck:earth-base")
    zph = 3.25
    for xp in (px0 + 0.15, px1 - 0.15):
        b.box(xp - 0.08, 0.07, zp, xp + 0.08, 0.23, zph, "trim:cream", skip=("bottom",))
        b.box(xp - 0.12, 0.03, zp, xp + 0.12, 0.27, zp + 0.18, "trim:cream", skip=("bottom",))
    b.box(px0, 0.05, zph, px1, 0.25, zph + 0.22, "trim:cream")
    ak.sweep_roof(b, [(-0.3, zph + 0.18), (pd, zph + 0.18 + (pd + 0.3) * math.tan(math.radians(18.0)))],
                  px0 - 0.2, px1 + 0.2, 0.14, "shake", "trim:cream", "trim:cream")
    ak.steps(b, xd - 0.75, xd + 0.75, 0.0, zp, 3, 0.28, "stone_trim:stone-light")
    a.meta = dict(eave=zw, ridge=zr + thick)
    return a


def gable_dormer(z_top_wall, pitch, y_front, w, h, dpitch, ov, thick, wall_mat, roof_mat, trim_mat):
    """A gable dormer on a main roof slope rising toward +Y from the wall line (y 0),
    whose top surface is at z_top_wall there; built centred on x = 0. Returns (Part,
    front-wall frame (u 0..w), z of the front wall's foot)."""
    tp = math.tan(math.radians(pitch))
    zb = z_top_wall + y_front * tp
    zt = zb + h
    zr = zt + (w / 2) * math.tan(math.radians(dpitch))
    yc = (zt - z_top_wall) / tp
    yr = (zr - z_top_wall) / tp
    p = ak.Part()
    outline = [(0.0, zb), (w, zb), (w, zt), (w / 2, zr), (0.0, zt)]
    Mf = T(-w / 2, y_front, 0.0)
    ow = (w / 2 - 0.375, w / 2 + 0.375, zb + 0.18, zb + 1.08)
    p.add(ak.panel(outline, [ow], wall_mat), Mf)
    p.add(ak.reveals([ow], 0.12, wall_mat), Mf)
    xl, xr = -w / 2, w / 2
    p.poly([(xl, y_front, zb), (xl, y_front, zt), (xl, yc, zt)], wall_mat)
    p.poly([(xr, y_front, zb), (xr, yc, zt), (xr, y_front, zt)], wall_mat)
    roof = ak.Part()
    prof = ak.gable_profile(xl, xr, zt, dpitch, ov)
    prof = [(-y, z) for y, z in reversed(prof)]      # swept along y below: profile over -x
    ak.sweep_roof(roof, prof, y_front - ov, yr + (thick + 0.05) / tp, thick, roof_mat, trim_mat, trim_mat)
    p.add(roof, Rz(math.pi / 2))
    return p, Mf, zb


# ---------------------------------------------------------------------------
# Barn (derelict): unpainted board-and-batten gone grey, a gambrel roof of rotten cedar
# shakes with holes to the rafters, missing and broken boards, a hayloft door hanging,
# the big door sagging open on a dark interior, a mossy fieldstone base
# ---------------------------------------------------------------------------

BOARD_W = 0.26
BOARD_T = 0.028


def board_wall(part, rng, L, z0, top, openings, holes, missing=0.06, broken=0.05, battens=True,
               mat="boards_weathered"):
    """A wall of separate vertical boards in wall-local coords (u 0..L, outside -Y):
    top(u) -> z; openings / holes (u0, u1, z0, z1) cut the boards; `missing` boards are
    gone, `broken` ones end short. Battens cover the joints (some lost)."""
    u = 0.0
    k = 0
    while u < L - 1e-3:
        bw = min(BOARD_W * rng.uniform(0.85, 1.15), L - u)
        ua, ub = u, u + bw
        u = ub
        k += 1
        if rng.random() < missing:
            continue
        zt_a, zt_b = top(ua), top(ub)
        segs = [(z0, max(zt_a, zt_b))]
        for o in list(openings) + list(holes):
            if o[0] < ub and o[1] > ua:
                new = []
                for s0, s1 in segs:
                    if o[2] > s0:
                        new.append((s0, min(s1, o[2])))
                    if o[3] < s1:
                        new.append((max(s0, o[3]), s1))
                segs = [s for s in new if s[1] - s[0] > 0.05]
        if rng.random() < broken and segs:
            s0, s1 = segs[0]
            segs[0] = (s0, s0 + (s1 - s0) * rng.uniform(0.3, 0.8))
        for s0, s1 in segs:
            if s1 >= max(zt_a, zt_b) - 1e-6:        # reaches the top: follow the rake
                za, zb_ = max(zt_a, s0 + 0.02), max(zt_b, s0 + 0.02)
            else:
                za = zb_ = s1
            part.prism([(ua + 0.004, s0), (ub - 0.004, s0), (ub - 0.004, zb_), (ua + 0.004, za)], -BOARD_T, 0.0,
                       mat)
        if battens and rng.random() > 0.25 and k > 1:
            bs = [(z0 + 0.1, min(zt_a, top(ua + 0.03)) - 0.05)]
            for o in list(openings) + list(holes):
                if o[0] < ua + 0.04 and o[1] > ua - 0.04:
                    bs = [s for s0, s1 in bs for s in ((s0, min(s1, o[2])), (max(s0, o[3]), s1))]
            for s0, s1 in bs:
                if s1 - s0 > 0.1:
                    part.box(ua - 0.035, -BOARD_T - 0.02, s0, ua + 0.035, -BOARD_T, s1, "boards_weathered",
                             skip=("back",))
    return part


def barn(W, D, zb, ctx):
    a = ak.Assembly("Barn")
    b = a.body
    rng = random.Random(ctx.get("seed", 5))
    x0, y0, x1, y1 = 0.4, 0.3, W - 0.4, D - 0.3
    rect = (x0, y0, x1, y1)
    zp, zw = 0.55, 5.2
    ak.band(b, rect, zb, zp, 0.1, "rubble_moss:stone-base", bottom=False)
    # the gambrel, ridge north-south: the profile across x, swept along y
    prof = ak.gambrel_profile(x0, x1, zw, 58.0, 22.0, 0.3, 0.35)
    thick = 0.14
    # holes per profile segment (0 west lower, 1 west upper, 2 east upper, 3 east lower):
    # [(y0, y1, s0, s1)], s metres up the segment from its first (western) point
    holes = {0: [(y0 + 1.2, y0 + 2.6, 0.9, 2.5)], 1: [(y1 - 4.2, y1 - 2.4, 1.0, 2.8)],
             3: [(y0 + 3.6, y0 + 5.0, 0.6, 2.2), (y1 - 2.2, y1 - 1.3, 1.4, 2.4)]}
    roof = ak.Part()
    sprof = [(-x, z) for x, z in reversed(prof)]      # over -x (increasing): east first
    # map the hole segments to the reversed profile: index k of prof -> len-2-k of sprof
    shole = {len(prof) - 2 - k: [(h0, h1, _seg_len(prof, k) - s1, _seg_len(prof, k) - s0)
                                 for h0, h1, s0, s1 in v] for k, v in holes.items()}
    ak.sweep_roof(roof, sprof, y0 - 0.4, y1 + 0.35, thick, "shake_rot", "boards_weathered", "boards_weathered",
                  holes=shole)
    b.add(roof, Rz(math.pi / 2))
    # rafters under the holed slopes (seen through the holes), 0.6 m apart
    for k in holes:
        (xa, za), (xb, zb_) = prof[k], prof[k + 1]
        y = y0 - 0.2
        while y < y1 + 0.2:
            b.beam((xa, y, za - 0.12), (xb, y, zb_ - 0.12), 0.07, 0.18, "boards_weathered")
            y += 0.6
    # purlins at the knees
    for xk, zk in (prof[1], prof[3]):
        b.beam((xk, y0, zk - 0.15), (xk, y1, zk - 0.15), 0.15, 0.15, "boards_weathered")
    # the walls: separate weathered boards, the dark interior behind
    tops_s = profile_tops(prof, x0, x1)
    tops_n = profile_tops(prof, x0, x1, reverse=True)

    def top_fn(pts):
        us = [p[0] for p in pts]
        zs = [p[1] for p in pts]
        return lambda u: float(np.interp(u, us, zs))
    xd = ctx["door_x"]
    dw, dh = 3.6, 3.7
    door_o = (xd - x0 - dw / 2, xd - x0 + dw / 2, zp, zp + dh)
    loft_o = (xd - x0 - 0.8, xd - x0 + 0.8, 6.3, 8.1)
    win_s = [(u - 0.4, u + 0.4, 2.9, 3.7) for u in (2.2, x1 - x0 - 2.2)]
    Wl = x1 - x0
    Dl = y1 - y0
    sides = {
        "S": (walls_ends(rect, "S"), Wl, top_fn(tops_s), [door_o, loft_o] + win_s,
              [(0.3, 1.4, 5.6, 7.2), (Wl - 1.6, Wl - 0.5, 1.0, 2.6)]),
        "E": (walls_ends(rect, "E"), Dl, lambda u: zw, [(2.5, 3.3, 2.9, 3.7), (6.0, 6.8, 2.9, 3.7), (8.6, 9.6, zp, zp + 2.1)],
              [(4.2, 5.0, 0.8, 2.4), (9.8, 11.0, 3.6, 5.2)]),
        "N": (walls_ends(rect, "N"), Wl, top_fn(tops_n), [(Wl / 2 - 0.45, Wl / 2 + 0.45, 6.6, 7.5)],
              [(3.0, 4.1, 1.1, 3.2)]),
        "W": (walls_ends(rect, "W"), Dl, lambda u: zw, [(2.5, 3.3, 2.9, 3.7), (6.0, 6.8, 2.9, 3.7), (9.2, 10.0, 2.9, 3.7)],
              [(7.2, 8.4, 2.0, 4.4)]),
    }
    for s, ((p0, p1), L, top, opens, gaps) in sides.items():
        M = ak.wall_frame(p0, p1)
        wall = board_wall(ak.Part(), rng, L, zp, top, opens, gaps, missing=0.05, broken=0.06)
        # the dark interior just behind (not behind the big door: see deep inside)
        back = ak.Part()
        outline = [(0.0, zp), (L, zp)] + [(u, top(u) - 0.05) for u in np.linspace(L, 0, 9)]
        ak.panel([(u, z) for u, z in outline], [door_o] if s == "S" else [], "interior_dark", y=0.4, part=back)
        wall.add(back)
        for o in opens:              # jambs: rough framing lining each opening
            wall.add(ak.reveals([o], 0.4, "boards_weathered", sill=False))
        b.add(wall, M)
    b.quad((x0 + 0.03, y0 + 0.03, zp + 0.012), (x1 - 0.03, y0 + 0.03, zp + 0.012), (x1 - 0.03, y1 - 0.03, zp + 0.012),
           (x0 + 0.03, y1 - 0.03, zp + 0.012), "interior_dark")                                   # the floor
    # corner posts and the eave plates (weathered timber, proud of the boards)
    for cx_, cy_ in ((x0, y0), (x1, y0), (x1, y1), (x0, y1)):
        b.box(cx_ - 0.1, cy_ - 0.1, zp, cx_ + 0.1, cy_ + 0.1, zw, "boards_weathered")
    # the big door: the left leaf sagging open on one hinge, the right one gone; a
    # track beam above; the hayloft door hanging from one hinge
    leaf = barn_leaf(dw / 2, dh - 0.05, rng)
    hx = xd - dw / 2
    M_leaf = T(hx, y0 - BOARD_T - 0.06, zp) @ Rz(math.radians(-38.0)) @ T(0, 0, dh) @ ak.Ry(math.radians(4.0)) \
        @ T(0, 0, -dh)
    b.add(leaf, M_leaf)
    b.box(xd - dw / 2 - 0.3, y0 - 0.2, zp + dh + 0.05, xd + dw / 2 + 0.3, y0 - BOARD_T, zp + dh + 0.25,
          "boards_weathered")
    loft = barn_leaf(1.6, 1.75, rng, braces=False)
    M_loft = T(xd - 0.8, y0 - BOARD_T - 0.05, 6.32) @ Rz(math.radians(-24.0)) @ T(0, 0, 1.75) \
        @ ak.Ry(math.radians(9.0)) @ T(0, 0, -1.75)
    b.add(loft, M_loft)
    # the hay-track beam out of the gable peak, a ramp of planks to the door
    zpk = ak.profile_z(prof, xd)
    b.beam((xd, y0 + 0.3, zpk - 0.55), (xd, y0 - 1.1, zpk - 0.55), 0.16, 0.2, "boards_weathered")
    for k in range(5):
        xx = xd - 1.2 + k * 0.5 + rng.uniform(-0.05, 0.05)
        b.beam((xx, y0 - 1.5, 0.02), (xx + rng.uniform(-0.06, 0.06), y0 - 0.05, zp - 0.02), 0.44, 0.04,
               "boards_weathered")
    # loose boards dropped along the foundation (as drawn)
    for xx, yy, ang in ((x0 + 0.6, y0 - 0.55, 3.0), (x1 - 3.4, y0 - 0.5, -4.0), (x1 - 2.6, y0 - 0.9, 11.0)):
        a_ = math.radians(ang)
        L = rng.uniform(2.2, 2.9)
        b.beam((xx, yy, 0.03), (xx + L * math.cos(a_), yy + L * math.sin(a_), 0.03 + rng.uniform(0, 0.1)), 0.24,
               0.03, "boards_weathered")
    # the window frames (glass all but gone: a shard or two), on the front and sides
    frame = a.proto("Win_Broken", lambda: broken_window(0.8, 0.8, rng))
    for s, us in (("S", [2.2, Wl - 2.2]), ("E", [2.9, 6.4]), ("W", [2.9, 6.4, 9.6])):
        (p0, p1) = walls_ends(rect, s)
        M = ak.wall_frame(p0, p1)
        for u in us:
            a.on_wall(frame, M, u, 2.9, glow=0.0)
    a.meta = dict(eave=zw, ridge=prof[2][1] + thick)
    return a


def _seg_len(prof, k):
    (xa, za), (xb, zb) = prof[k], prof[k + 1]
    return math.hypot(xb - xa, zb - za)


def walls_ends(rect, s):
    x0, y0, x1, y1 = rect
    return {"S": ((x0, y0), (x1, y0)), "E": ((x1, y0), (x1, y1)),
            "N": ((x1, y1), (x0, y1)), "W": ((x0, y1), (x0, y0))}[s]


def barn_leaf(w, h, rng, braces=True):
    """A board door leaf (hinge at x 0, outside -Y): vertical boards on rails, a Z brace."""
    p = ak.Part()
    u = 0.0
    while u < w - 1e-3:
        bw = min(0.2 * rng.uniform(0.9, 1.1), w - u)
        if rng.random() > 0.1:
            p.box(u + 0.004, 0.0, 0.0, u + bw - 0.004, 0.03, h * rng.uniform(0.93, 1.0), "boards_weathered")
        u += bw
    for z in (0.25, h / 2, h - 0.3):
        p.box(0.04, -0.04, z - 0.1, w - 0.04, 0.0, z + 0.1, "boards_weathered", skip=("back",))
    if braces:
        p.beam((0.15, -0.03, 0.35), (w - 0.15, -0.03, h / 2 - 0.1), 0.16, 0.035, "boards_weathered", up=(0, -1, 0))
        p.beam((0.15, -0.03, h / 2 + 0.1), (w - 0.15, -0.03, h - 0.4), 0.16, 0.035, "boards_weathered", up=(0, -1, 0))
    return p


def broken_window(w, h, rng):
    p = ak.window(w, h, 0.4, (2, 2), sash="fixed", frame="boards_weathered", glass="interior_dark")
    # a couple of glass shards left in the corners
    p.poly([(-w / 2 + 0.07, 0.36, 0.08), (-w / 2 + 0.3, 0.36, 0.08), (-w / 2 + 0.07, 0.36, 0.33)], "glass")
    p.poly([(w / 2 - 0.07, 0.36, h - 0.08), (w / 2 - 0.07, 0.36, h - 0.28), (w / 2 - 0.22, 0.36, h - 0.08)], "glass")
    return p


# ---------------------------------------------------------------------------
# From the dump
# ---------------------------------------------------------------------------

DESIGNS = {"town_hall": town_hall, "general_store": general_store, "motel": motel, "farmhouse": farmhouse,
           "barn": barn}


def _px_x(bld, px):
    """A drawn-art x pixel (0 = the facade art's left edge, 16 px a tile) -> local x."""
    return px / config.TILE_PX * TM


def context(placed, bld):
    """Per-design inputs derived from the dump (doors, parts, rooms, lettering) and the
    art's drawn positions."""
    art = bld["art"]
    ctx = {}
    dx = (bld["door"]["x"] - bld["x"] + 0.5) * TM if bld.get("door") else None
    if art == "general_store":
        ctx["open"] = config.SHOP_HOURS[0] <= config.FILM_MINUTE < config.SHOP_HOURS[1]   # StoreFacade.cs
        ctx["door_x"] = dx                                  # building_store.png: windows at px 19, 39, 93
        ctx["windows"] = [_px_x(bld, 19), _px_x(bld, 39), _px_x(bld, 93)]
    elif art == "farmhouse":
        ctx["door_x"] = dx                                  # farm_buildings.png: windows px 21, 75; chimney px 75
        ctx["windows"] = [_px_x(bld, 21), _px_x(bld, 75)]
        ctx["chimney_x"] = _px_x(bld, 75)
        ctx["dormers"] = [_px_x(bld, 34), _px_x(bld, 62)]
    elif art == "barn":
        ctx["door_x"] = bld["w"] * TM / 2                   # barn.png: the doors centred on the gable
        ctx["seed"] = 5
    elif art == "motel":
        parts = {p["part"]: p for p in bld["parts"]}
        o, s = parts["office"], parts["strip"]
        ctx["office"] = ((o["x"] - bld["x"]) * TM, (o["x"] - bld["x"] + o["w"]) * TM)
        ctx["strip"] = ((s["x"] - bld["x"]) * TM, (s["x"] - bld["x"] + s["w"]) * TM)
        ox0 = ctx["office"][0]
        # MotelFacade.cs: office wall at texture x 14..94 (= the office part), glass
        # centre 44, sign box centre 46, door centre 82
        ctx["office_glass_x"] = ox0 + (44 - 14) / 80 * (ctx["office"][1] - ox0)
        ctx["office_sign_x"] = ox0 + (46 - 14) / 80 * (ctx["office"][1] - ox0)
        ctx["office_door_x"] = ox0 + (82 - 14) / 80 * (ctx["office"][1] - ox0)
        ctx["office_door"] = o.get("doorColor", "barn-red")
        ctx["office_text"] = o["text"]
        ctx["ice_text"] = next(t for t in bld["lettering"] if t == "ICE")
        ctx["rooms"] = [dict(room=r["room"], paint=r["doorColor"]) for r in bld["rooms"]]
        for r in ctx["rooms"]:
            assert str(r["room"]) in bld["lettering"], r
        # the lit room: the dump's RoomGlow light, nearest room window
        glow = next((lt for lt in placed.data["lights"] if lt.get("id") == "RoomGlow"), None)
        sx0, sx1 = ctx["strip"]
        wins = {r["room"]: _unit_x(sx0, sx1 - sx0, 44 + 68 * (r["room"] - 1)) for r in ctx["rooms"]}
        if glow is not None:
            gx = glow["px"][0] / config.TILE_PX * TM - bld["x"] * TM
            ctx["lit_room"] = min(wins, key=lambda k: abs(wins[k] - gx))
        else:
            ctx["lit_room"] = 0
    return ctx


def footprint(placed, bld):
    x0 = (placed.ox + bld["x"]) * TM
    y1 = -(placed.oy + bld["y"]) * TM
    return x0, y1 - bld["h"] * TM, x0 + bld["w"] * TM, y1


def hero_buildings(world, only=None):
    for placed in world.maps.values():
        if only is not None and placed.id != only:
            continue
        for bld in placed.data["buildings"]:
            if bld.get("art") in config.HERO_ART:
                yield placed, bld


def height_field(objs, rect, cell):
    """Ray-cast the objects' evaluated meshes straight down on a grid over rect: the
    building's top surface height per cell (-inf where nothing). Returns (x0, y0, cell, H)."""
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    verts, polys = [], []
    for ob in objs:
        me = ob.data
        mw = ob.matrix_world
        base = len(verts)
        verts += [mw @ v.co for v in me.vertices]
        polys += [[base + i for i in p.vertices] for p in me.polygons]
    tree = BVHTree.FromPolygons(verts, polys)
    x0, y0, x1, y1 = rect
    nx, ny = int(math.ceil((x1 - x0) / cell)), int(math.ceil((y1 - y0) / cell))
    H = np.full((ny, nx), -np.inf)
    ztop = max(v.z for v in verts) + 1.0
    for j in range(ny):
        for i in range(nx):
            hit = tree.ray_cast(Vector((x0 + (i + 0.5) * cell, y0 + (j + 0.5) * cell, ztop)), Vector((0, 0, -1)))
            if hit[0] is not None:
                H[j, i] = hit[0].z
    return x0, y0, cell, H


def realised_info(name, body, kids, z_ground):
    """After realise: the building's crown-clearance height field (hull), triangle count,
    height above its ground (also stamped on the body as `tris` / `height_m`)."""
    import bpy
    bpy.context.view_layer.update()
    objs = [body] + kids
    pts = np.array([ob.matrix_world @ v.co for ob in objs for v in ob.data.vertices])
    pad = 1.0
    rect = (pts[:, 0].min() - pad, pts[:, 1].min() - pad, pts[:, 0].max() + pad, pts[:, 1].max() + pad)
    hull = height_field(objs, rect, config.HULL_CELL_M)
    tris = sum(len(p.vertices) - 2 for ob in objs for p in ob.data.polygons)
    body["tris"] = tris
    body["height_m"] = float(pts[:, 2].max() - z_ground)
    return dict(name=name, tris=tris, objects=len(objs), hull=hull, height=float(pts[:, 2].max() - z_ground),
                extent=rect)


def build(world, terrain, col, mats, only=None):
    """Design, realise and ground the hero buildings. Returns [dict(name, tris, hull,
    ...)] (hull = height field for the forest's crown clearance)."""
    import bpy

    import archmats
    sc = bpy.context.scene
    sc[config.WINDOW_GLOW_PROP] = config.WINDOW_GLOW
    sc[config.NEON_GLOW_PROP] = config.NEON_GLOW
    resolve = archmats.Resolver(mats)
    out = []
    for placed, bld in hero_buildings(world, only):
        x0, y0, x1, y1 = footprint(placed, bld)
        facing = bld.get("facing", "S")
        Mxy, W, D = ak.place(x0, y0, x1, y1, facing)
        lo, hi = terrain.z_range(x0, y0, x1, y1, n=7) if terrain is not None else (0.0, 0.0)
        zb = -(hi - lo) - 0.3
        asm = DESIGNS[bld["art"]](W, D, zb, context(placed, bld))
        name = f"Bldg_{placed.id}_{bld['id']}"
        body, kids = ak.realise(asm, col, T(0, 0, hi) @ Mxy, resolve, name, map_id=placed.id, dump_id=bld["id"],
                                art=bld["art"], place=bld["place"], state=bld["state"], kind="building")
        out.append(realised_info(name, body, kids, hi))
    return out

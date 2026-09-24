"""The mansion's ROOFLINE (Kevin: a glimpse of its rooftop through the trees beyond the
East Fork's chained drive; canon: north of the fork, in ruins, gothic, overgrown). It
stands on Guide_MansionClearing's level shelf (routes.mansion_clearing, ground_z),
ringed by the tall hemlock / pine forest.py plants, facing back down the drive.

Designed as a real ruined gothic-revival house so its top reads true from anywhere: a
dark stone main block (three storeys) under a steep side-gable of mossy slate, a steep
front cross-gable, an octagonal corner turret with a candle-snuffer spire, a gable
dormer, clustered chimneys (one broken off). The ruin: the main roof has fallen in at
the west end, across the ridge (a broken ridge, rafters bare, some snapped), with a
second hole low on the front slope and lost slates on the spire; ivy climbs the walls
and the turret. Its windows are DARK (empty frames, shards). The upper storey and roofs
are built fully; the lower walls are plain (the trees hide them). Canon only: no name,
no sign, no features beyond "gothic, in ruins, overgrown".
"""

import math
import random

import numpy as np

import archkit as ak
import buildings_hero as bh
import config
from archkit import T, Rz

WALL = "ashlar:stone-dark"
TRIM = "stone_trim:stone-base"
ROOF = "slate_ruin:stone-dark"
UNDER = "boards_weathered"


def _dark_window(w, h, rng):
    """An empty gothic window: dark frame and mullion, the room black behind, a shard or
    two of glass left."""
    p = ak.window(w, h, 0.45, (2, 3), sash="fixed", frame="trim_dark", glass="interior_dark")
    if rng.random() < 0.6:
        p.poly([(-w / 2 + 0.07, 0.42, 0.08), (-w / 2 + 0.3, 0.42, 0.08), (-w / 2 + 0.07, 0.42, 0.4)], "glass")
    if rng.random() < 0.5:
        p.poly([(w / 2 - 0.07, 0.42, h - 0.1), (w / 2 - 0.07, 0.42, h - 0.35), (w / 2 - 0.25, 0.42, h - 0.1)], "glass")
    return p


def _ivy(part, rng, u0, u1, z0, zmax, y=-0.06, step=0.45):
    """A climbing ivy mat on a wall plane (wall-local u, outside -Y): a jagged top
    profile, ragged sides, two leaf layers for depth."""
    us = np.arange(u0, u1 + 1e-6, step)
    if len(us) < 2:
        return
    mid = (u0 + u1) / 2
    for layer, dy in ((0, 0.0), (1, -0.07)):
        tops, walk = [], 0.0
        for u in us:
            f = 1.0 - abs(u - mid) / ((u1 - u0) / 2 + 1e-6)
            walk = max(-0.45, min(0.12, walk + rng.uniform(-0.16, 0.16)))
            shoulder = min(1.0, f * 3.0) ** 0.7          # a broad mat, rounded shoulders
            tendril = rng.uniform(0.05, 0.25) if rng.random() < 0.18 else 0.0     # a runner climbing ahead
            h = (zmax - z0) * (0.35 + 0.65 * shoulder + walk + tendril) * (0.88 if layer else 1.0)
            tops.append(z0 + max(0.3, h))
        for k in range(len(us) - 1):
            ua, ub = us[k], us[k + 1]
            za, zb = tops[k], tops[k + 1]
            zlo = z0 + (rng.uniform(0.0, 0.5) if layer else 0.0)
            part.poly([(ua, y + dy, zlo), (ub, y + dy, zlo), (ub, y + dy, zb), ((ua + ub) / 2, y + dy - 0.03,
                       max(za, zb) + rng.uniform(0.05, 0.35)), (ua, y + dy, za)], "ivy")


def mansion(rng):
    a = ak.Assembly("Mansion")
    b = a.body
    W, D = config.MANSION_BLOCK_M
    x0, y0, x1, y1 = 0.0, 0.0, W, D
    rect = (x0, y0, x1, y1)
    zp, zw = 0.8, config.MANSION_EAVE_M
    pitch, ov, thick = 55.0, 0.55, 0.22
    tp = math.tan(math.radians(pitch))
    ak.band(b, rect, -1.5, zp, 0.12, "rubble_moss:stone-base", bottom=False)
    ak.band(b, rect, 7.3, 7.5, 0.1, TRIM)                                   # the storey string course
    ak.band(b, rect, zw - 0.45, zw - 0.15, 0.2, TRIM)                       # the eave cornice
    win_up = a.proto("Win_Upper", lambda: _dark_window(1.0, 2.3, rng))
    win_lo = a.proto("Win_Lower", lambda: _dark_window(1.1, 2.4, rng))
    # the front cross-gable bay (west of centre), projecting 1.2 m
    gx0, gx1, gy0 = 4.2, 9.8, -1.2
    gcx = (gx0 + gx1) / 2
    walls = Walls(a, rect, zp, zw)
    bays_front = [1.8, 3.2, 11.6, 14.3]
    for x in bays_front:
        walls.add("S", x, 1.0, 7.7, 2.3, win_up, glow=0.0)
        for z in (1.4, 4.4):
            walls.add("S", x, 1.1, z, 2.4, win_lo, glow=0.0)
    for x in (2.2, 5.5, 9.5, 13.5, 16.8):
        walls.add("N", x, 1.0, 7.7, 2.3, win_up, glow=0.0)
        for z in (1.4, 4.4):
            walls.add("N", x, 1.1, z, 2.4, win_lo, glow=0.0)
    for s in "EW":
        for y in (3.4, 10.0):
            walls.add(s, y, 1.0, 7.7, 2.3, win_up, glow=0.0)
            for z in (1.4, 4.4):
                walls.add(s, y, 1.1, z, 2.4, win_lo, glow=0.0)
    prof = ak.gable_profile(y0, y1, zw, pitch, ov)
    walls.top("E", bh.profile_tops(prof, y0, y1))
    walls.top("W", bh.profile_tops(prof, y0, y1, reverse=True))
    for s in "EW":            # attic lancets in the gable ends
        walls.add(s, D / 2, 0.9, zw + 1.6, 2.4, win_up, glow=0.0)
    walls.build()
    # the cross-gable bay: three walls, a steep gable end with a tall window pair
    cprof_pitch = 60.0
    gw = Walls(a, (gx0, gy0, gx1, 0.25), zp, zw)
    cz_r = zw + (gcx - gx0) * math.tan(math.radians(cprof_pitch))
    gw.tops["S"] = [(0.0, zw), (gcx - gx0, cz_r), (gx1 - gx0, zw)]
    gw.add("S", gcx - 0.8, 1.0, 7.7, 2.3, win_up, glow=0.0)
    gw.add("S", gcx + 0.8, 1.0, 7.7, 2.3, win_up, glow=0.0)
    gw.add("S", gcx, 0.9, zw + 1.2, 2.4, win_up, glow=0.0)
    gw.add("S", gcx - 1.3, 1.1, 4.2, 2.4, win_lo, glow=0.0)
    gw.add("S", gcx + 1.3, 1.1, 4.2, 2.4, win_lo, glow=0.0)
    gw.add("S", gcx, 1.8, zp, 2.9)                           # the entrance: doors long gone
    gw.build(sides="SEW")
    ak.band(b, (gx0, gy0, gx1, 0.0), -1.5, zp, 0.12, "rubble_moss:stone-base", bottom=False)
    ak.band(b, (gx0, gy0, gx1, 0.0), 7.3, 7.5, 0.1, TRIM)
    b.quad((gcx - 0.9, gy0 + 0.45, zp), (gcx + 0.9, gy0 + 0.45, zp), (gcx + 0.9, gy0 + 0.45, zp + 2.9),
           (gcx - 0.9, gy0 + 0.45, zp + 2.9), "interior_dark")
    # gothic hood moulds over the upper windows (front, bay, gable ends)
    for x in bays_front:
        ak.pointed_hood(b, x, 7.7 + 2.3, 1.0, 0.55, TRIM, y=0.0)
    for x in (gcx - 0.8, gcx + 0.8):
        ak.pointed_hood(b, x, 7.7 + 2.3, 1.0, 0.55, TRIM, y=gy0)
    ak.pointed_hood(b, gcx, zw + 1.2 + 2.4, 0.9, 0.5, TRIM, y=gy0)
    # the main roof, fallen in at the west end across the ridge, a hole low in front
    L0 = math.hypot(prof[1][0] - prof[0][0], prof[1][1] - prof[0][1])
    holes = {0: [(1.9, 5.3, L0 - 3.2, L0), (14.6, 16.4, 1.2, 3.0)],
             1: [(1.4, 6.2, 0.0, 5.2)]}
    ak.sweep_roof(b, prof, x0 - 0.4, x1 + 0.4, thick, ROOF, UNDER, "metal:stone-dark", holes=holes)
    zr = prof[1][1]
    ym = (y0 + y1) / 2
    # the ridge cap either side of the break, its broken end sagging
    b.beam((x0 - 0.42, ym, zr + thick + 0.03), (1.9, ym, zr + thick + 0.03), 0.26, 0.08, "metal:stone-dark")
    b.beam((5.3, ym, zr + thick + 0.03), (x1 + 0.42, ym, zr + thick + 0.03), 0.26, 0.08, "metal:stone-dark")
    # bare rafters in the holes (0.55 m apart, some snapped short), the ridge beam broken
    (ya, za), (yr, zr_) = prof[0], prof[1]
    (_yb, zb_) = prof[2]
    x = 1.2
    while x < 6.4:
        f = rng.uniform(0.35, 1.0) if rng.random() < 0.4 else 1.0
        b.beam((x, yr, zr_ - 0.1), (x, yr + (y1 + ov - yr) * 0.55 * f, zr_ - 0.1 - (zr_ - zb_) * 0.55 * f),
               0.07, 0.18, UNDER)
        if x > 1.8 and x < 5.4:
            g = rng.uniform(0.3, 1.0) if rng.random() < 0.35 else 1.0
            b.beam((x, yr, zr_ - 0.1), (x, yr - (yr - ya) * 0.45 * g, zr_ - 0.1 - (zr_ - za) * 0.45 * g),
                   0.07, 0.18, UNDER)
        x += 0.55
    b.beam((x0 - 0.2, ym, zr_ - 0.05), (2.6, ym, zr_ - 0.05), 0.18, 0.28, UNDER)
    b.beam((4.4, ym, zr_ - 0.05), (x1 + 0.2, ym, zr_ - 0.05), 0.18, 0.28, UNDER)
    b.beam((2.6, ym, zr_ - 0.05), (3.6, ym + 0.2, zr_ - 1.3), 0.18, 0.28, UNDER)      # the snapped end
    for xx in (14.9, 15.5, 16.1):
        s0 = rng.uniform(0.9, 1.3)
        b.beam((xx, ya + (yr - ya) * 0.08, za + (zr_ - za) * 0.08 - 0.1),
               (xx, ya + (yr - ya) * 0.35 * s0, za + (zr_ - za) * 0.35 * s0 - 0.1), 0.07, 0.18, UNDER)
    # the floor under the fallen roof: dark (the house is gutted)
    b.quad((x0 + 0.3, y0 + 0.3, zw - 0.6), (6.8, y0 + 0.3, zw - 0.6), (6.8, y1 - 0.3, zw - 0.6), (x0 + 0.3, y1 - 0.3, zw - 0.6),
           "interior_dark")
    # the cross-gable roof (ridge along y into the main slope)
    cprof = ak.gable_profile(gx0, gx1, zw, cprof_pitch, 0.45)
    sprof = [(-xx, z) for xx, z in reversed(cprof)]
    crf = ak.Part()
    y_in = (cz_r + 0.4 - zw) / tp + 0.3
    ak.sweep_roof(crf, sprof, gy0 - 0.45, y_in, 0.2, ROOF, UNDER, "metal:stone-dark")
    b.add(crf, Rz(math.pi / 2))
    b.beam((gcx, gy0 - 0.47, cz_r + 0.24), (gcx, y_in - 0.4, cz_r + 0.24), 0.24, 0.08, "metal:stone-dark")
    b.cylinder((gcx, gy0 - 0.4, cz_r + 0.2), (gcx, gy0 - 0.4, cz_r + 1.6), 0.04, "metal:ink-700", n=6)   # finial
    # a gable dormer on the front slope east of the bay
    part, Mf, zfb = bh.gable_dormer(zw + thick, pitch, 1.0, 1.7, 2.0, 60.0, 0.25, 0.14, WALL, ROOF, TRIM)
    b.add(part, T(12.8, 0.0))
    a.on_wall(a.proto("Win_Dormer", lambda: _dark_window(0.9, 1.6, rng)), T(12.8, 0.0) @ Mf, 0.85, zfb + 0.2, glow=0.0)
    # the octagonal turret at the front-east corner, its bell-cast spire (slates lost on
    # one face), a finial
    tcx, tcy, tr, n = x1 - 0.3, -0.5, 2.5, 8
    tz1 = zw + 3.6
    ak.tower_walls(b, tcx, tcy, tr, n, -1.5, tz1, WALL, rot=math.pi / 8)
    ak.tower_walls(b, tcx, tcy, tr + 0.14, n, 7.3, 7.5, TRIM, rot=math.pi / 8)
    ak.tower_walls(b, tcx, tcy, tr + 0.18, n, tz1 - 0.4, tz1, TRIM, rot=math.pi / 8)
    for k in range(n):         # a dark lancet in each outward face, two storeys
        ang = math.pi / 8 + 2 * math.pi * (k + 0.5) / n
        if not (math.cos(ang) > 0.2 or math.sin(ang) < -0.2):
            continue
        apo = tr * math.cos(math.pi / n)
        M = T(tcx + apo * math.cos(ang), tcy + apo * math.sin(ang)) @ Rz(ang + math.pi / 2)
        for z in (7.7, zw + 0.7):
            a.place(win_up, M @ T(0, -0.45, z), glow=0.0)
    zapex = ak.spire(b, tcx, tcy, tr + 0.15, n, tz1, 8.6, ROOF, UNDER, "metal:stone-dark", overhang=0.5,
                     thick=0.16, rot=math.pi / 8, holes=(2,))
    b.cylinder((tcx, tcy, zapex - 0.3), (tcx, tcy, zapex + 1.6), 0.05, "metal:ink-700", n=6)
    b.cylinder((tcx, tcy, zapex + 0.4), (tcx, tcy, zapex + 0.65), 0.14, "metal:ink-700", n=8)
    # clustered chimneys at the gable ends (stone, three flues each); the west one broken
    for cx_, broken in ((0.9, True), (x1 - 0.9, False)):
        top = zr + thick + (1.1 if broken else 2.6)
        if not broken:
            ak.chimney(b, cx_, ym, 1.1, 2.2, zw - 1.0, top, WALL, TRIM, pots=3, pot_mat="rubble:stone-shade")
        else:
            b.box(cx_ - 0.55, ym - 1.1, zw - 1.0, cx_ + 0.55, ym + 1.1, top, WALL, skip=("bottom",))
            for k in range(5):
                dx, dy = rng.uniform(-0.45, 0.25), rng.uniform(-0.9, 0.7)
                h = rng.uniform(0.2, 0.9)
                b.box(cx_ + dx, ym + dy, top - 0.05, cx_ + dx + 0.3, ym + dy + 0.4, top + h, WALL, skip=("bottom",))
    # a central stack through the ridge east of the break
    ak.chimney(b, 11.0, ym, 0.9, 1.4, zr - 2.0, zr + thick + 2.0, WALL, TRIM, pots=2, pot_mat="rubble:stone-shade")
    # ivy: up the front, the bay, the east end and round the turret (dense low, thinning up)
    iv = ak.Part()
    for (p0, p1), spans in (
            (((x0, y0), (gx0, y0)), [(0.0, 4.0, 10.5)]),
            (((gx0, gy0), (gx1, gy0)), [(0.2, 2.6, 13.5), (3.8, 5.6, 8.0)]),
            (((gx1, y0), (x1 - 2.0, y0)), [(0.5, 3.6, 9.0)]),
            (((x1, 2.0), (x1, y1)), [(0.3, 5.5, 11.5), (7.5, 10.5, 6.0)]),
            (((x0, y1), (x0, y0)), [(1.0, 9.0, 12.0)]),
            (((x1, y1), (x0, y1)), [(0.5, 5.0, 9.0), (10.0, 16.5, 11.0)])):
        M = ak.wall_frame(p0, p1)
        for u0, u1, zmax in spans:
            pp = ak.Part()
            _ivy(pp, rng, u0, u1, -0.5, zmax, y=-0.14)
            iv.add(pp, M)
    for k in (4, 5, 6):          # the turret's south / west faces
        ang = math.pi / 8 + 2 * math.pi * k / n
        a0 = (tcx + tr * math.cos(ang), tcy + tr * math.sin(ang))
        ang1 = ang + 2 * math.pi / n
        a1 = (tcx + tr * math.cos(ang1), tcy + tr * math.sin(ang1))
        pp = ak.Part()
        _ivy(pp, rng, 0.0, math.dist(a0, a1), -0.5, rng.uniform(9.0, 14.5), y=-0.1, step=0.35)
        iv.add(pp, ak.wall_frame(a0, a1))
    b.add(iv)
    a.meta = dict(eave=zw, ridge=zr + thick, top=zapex + 1.6)
    return a


class Walls(bh.Walls):
    """bh.Walls in the mansion's stone, 0.45 m deep reveals."""

    def __init__(self, asm, rect, z0, z1):
        super().__init__(asm, rect, z0, z1, WALL, 0.45)


def build(world, terrain, col, mats):
    """Realise the mansion at routes.mansion_site (the back of the clearing's shelf,
    facing back down the drive). Returns [info] like buildings_hero.build (its hull joins
    the forest's crown check), or []."""
    import archmats
    site = terrain.routes.mansion_site()
    if site is None:
        return []
    ox, oy, rot, rect = site
    asm = mansion(random.Random(config.MANSION_SEED))
    M = T(ox, oy) @ Rz(rot)
    x0, y0, x1, y1 = rect
    corners = ak.apply(M, [(x0, y0, 0), (x1, y0, 0), (x1, y1, 0), (x0, y1, 0), ((x0 + x1) / 2, (y0 + y1) / 2, 0)])
    zs = [terrain.z_at(float(p[0]), float(p[1])) for p in corners]
    hi = max(zs)
    resolve = archmats.Resolver(mats)
    name = "Mansion_Roofline"
    body, kids = ak.realise(asm, col, T(0, 0, hi) @ M, resolve, name, kind="building", design="mansion",
                            ground_z=float(hi), ground_span=float(max(zs) - min(zs)))
    return [bh.realised_info(name, body, kids, hi)]

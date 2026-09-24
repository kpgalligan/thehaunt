"""Every ground prop the dump carries, designed for the film (stylized-real, real sizes,
palette-led; sizes are real-world, positions and facings from the dump):

  chain          RoadBarrier.cs: timber posts (hair-stock) and a sagging steel chain
                 across the rect (a long run gets intermediate posts). The drive-in's
                 theater chain is UP (it is down only summer 9-6; the film is autumn).
  pit_cover      PitCover.cs: heavy planks with gaps (one plank missing) over a timber
                 sill on the dished dirt; under them the dark void (LF_Pit) and a hidden
                 red point light, plus a faint leak light above the widest gap
                 (flyover_pit_glow, Kevin: "a slight red glow coming from the pit").
  well           the plaza's fieldstone well: stone curb, dark water, two posts, a
                 windlass + bucket, a barn-red shingled gable (the art's well).
  bench          park benches (the art): wood slats on dark iron ends; the plaza's face
                 the well, the drive-in's the screen.
  planter        wooden boxes of autumn mums (variant 0 barn-red, 1 lantern, 2 cream).
  notice_board   two posts, a shingled cap, a framed board of pinned notices (drawn
                 rules, no letters).
  mailbox        the farm's rural mailbox on a post, flag down (Mailbox.cs idle).
  shipping_bin   the farm's lidded crate, shut (ShippingBin.cs: empty at a first visit).
  fence          the pen: post-and-two-rail round the rect's edge tiles, the gate OPEN.
  scatter log    the farm's fallen log.
  debris         the storm slide over the farm road (TestMap.StormDebris): a mud fan
                 off the wooded side, boulders at the rock tiles, snapped trunks at the
                 log tiles ("the storm brought half the hillside down").
  car            Pell's sedan (GuestCar.cs): a late-1950s four-door in the dump's paint,
                 nose west as the dump says.
  screen         DriveInScreen.cs: a weathered cream face on three timber trestle
                 bents, water stains, the top-right panel gone (frame showing); faces
                 north over the field.
  speaker        DriveInSpeaker.cs: waist-high pipe posts, window speakers hung either
                 side (a couple gone), cables perished.
"""

import math
import random

import numpy as np

import archkit as ak
import config
import propkit as pk
from archkit import T, Rz, Rx

TM = config.TILE_M
SKIP_KINDS = set(config.GROUND_PROP_KINDS)


# ---------------------------------------------------------------------------
# Chains and the pit
# ---------------------------------------------------------------------------

CHAIN_Z = 0.78
CHAIN_POST_H = 0.98
CHAIN_SPAN_MAX = 4.4


def chain_run(placer, x0, x1, y, rng):
    """Posts along x0..x1 at y (each grounded) and the chain between them, in world
    coordinates, as one Part (origin world 0)."""
    L = x1 - x0
    n = max(1, int(math.ceil(L / CHAIN_SPAN_MAX)))
    xs = [x0 + L * k / n for k in range(n + 1)]
    part = ak.Part()
    tops = []
    for x in xs:
        z = placer.z(x, y)
        lean = rng.uniform(-0.02, 0.02)
        post = ak.Part()
        pk.post(post, 0.0, 0.0, CHAIN_POST_H, 0.14, "timber:hair-stock", cap="plank:earth-mid", sink=0.6)
        post.cylinder((0, -0.075, CHAIN_Z), (0, -0.1, CHAIN_Z), 0.018, "metal:stone-dark", n=6)     # the eye bolt
        part.add(post, T(x, y, z) @ Rz(rng.uniform(-0.1, 0.1)) @ ak.Ry(lean))
        tops.append((x, y - 0.1, z + CHAIN_Z))
    for a, b in zip(tops[:-1], tops[1:]):
        span = math.dist(a, b)
        pk.chain(part, a, b, 0.05 + 0.045 * span, "metal:stone-shade")
    return part


def pit(rng, W, D):
    """The pit cover over a W x D rect (local: x 0..W, y 0..D, origin at the rect's
    south-west corner, z 0 = the dish's floor at its centre). Returns (Assembly, under
    light point, leak light point)."""
    a = ak.Assembly("Pit")
    b = a.body
    ins = 0.3
    x0, y0, x1, y1 = ins, ins, W - ins, D - ins
    zt = 0.26                                    # the sill's top (the dish rises 6 cm to it)
    # the sill: four heavy timbers round the hole, half sunk in the dirt
    for bx in ((x0 - 0.28, y0 - 0.28, x1 + 0.28, y0), (x0 - 0.28, y1, x1 + 0.28, y1 + 0.28),
               (x0 - 0.28, y0, x0, y1), (x1, y0, x1 + 0.28, y1)):
        b.box(bx[0], bx[1], -0.3, bx[2], bx[3], zt, "timber:earth-dark", skip=("bottom",))
    # the void: a shallow dark box inside the sill (LF_Pit, glowing faintly from below)
    void = ak.Part()
    zv = 0.1
    void.quad((x0, y0, zv), (x1, y0, zv), (x1, y1, zv), (x0, y1, zv), "pitglow:pit-red")
    void.quad((x0, y0, zv), (x0, y1, zv), (x0, y1, zt), (x0, y0, zt), "pitglow:pit-red")
    void.quad((x1, y0, zv), (x1, y0, zt), (x1, y1, zt), (x1, y1, zv), "pitglow:pit-red")
    void.quad((x0, y0, zv), (x0, y0, zt), (x1, y0, zt), (x1, y0, zv), "pitglow:pit-red")
    void.quad((x0, y1, zv), (x1, y1, zv), (x1, y1, zt), (x0, y1, zt), "pitglow:pit-red")
    a.protos["Void"] = void
    a.place("Void", T(), glow=1.0)
    # a bearer across the middle under the plank butts
    xm = W / 2 + rng.uniform(-0.6, 0.6)
    b.box(xm - 0.12, y0 - 0.2, zt - 0.2, xm + 0.12, y1 + 0.2, zt, "timber:earth-dark")
    # planks east-west, gaps between, one gone in the middle (PitCover.cs row 2)
    y = y0 - 0.22
    rows = []
    while y < y1 + 0.2:
        w = rng.uniform(0.27, 0.34)
        rows.append((y, min(w, y1 + 0.24 - y)))
        y += w + rng.uniform(0.018, 0.06)
    gone = len(rows) // 2
    wide = rows[gone]
    for k, (py, w) in enumerate(rows):
        if k == gone:
            continue
        cut = xm + rng.uniform(-0.08, 0.08)
        for (px0, px1) in ((x0 - 0.2 - rng.uniform(0, 0.15), cut - 0.01), (cut + 0.01, x1 + 0.2 + rng.uniform(0, 0.15))):
            th = rng.uniform(0.065, 0.08)
            dz = rng.uniform(-0.01, 0.015)
            key = "plank:earth-mid" if (k + (px0 > xm)) % 2 else "plank:wood-warm"
            pl = ak.Part().box(px0, py, zt + dz, px1, py + w, zt + dz + th, key)
            tilt = rng.uniform(-0.012, 0.012)
            b.add(pl, T(0, py + w / 2, zt) @ Rx(tilt) @ T(0, -(py + w / 2), -zt))
    under = ((x0 + x1) / 2, (y0 + y1) / 2, 0.18)
    leak = ((x0 + x1) / 2 + rng.uniform(-1, 1), wide[0] + wide[1] / 2, zt + 0.45)
    return a, under, leak


# ---------------------------------------------------------------------------
# The plaza: well, benches, planters, notice board
# ---------------------------------------------------------------------------

def well(rng):
    p = ak.Part()
    R, r, h = 0.95, 0.7, 0.72
    n = 20
    for k in range(n):                                   # the stone curb (outer + inner + top)
        t0, t1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
        o0, o1 = (R * math.cos(t0), R * math.sin(t0)), (R * math.cos(t1), R * math.sin(t1))
        i0, i1 = (r * math.cos(t0), r * math.sin(t0)), (r * math.cos(t1), r * math.sin(t1))
        p.quad((*o0, -0.3), (*o1, -0.3), (*o1, h), (*o0, h), "rubble_moss:stone-base", smooth=True)
        p.quad((*i1, -0.1), (*i0, -0.1), (*i0, h), (*i1, h), "rubble:stone-shade", smooth=True)
        p.quad((*o0, h), (*o1, h), (*(np.array(o1) * 0.99 + np.array(i1) * 0.01), h + 0.1),
               (*(np.array(o0) * 0.99 + np.array(i0) * 0.01), h + 0.1), "stone_trim:stone-light")
        p.quad((*o0, h + 0.1), (*o1, h + 0.1), (*i1, h + 0.1), (*i0, h + 0.1), "stone_trim:stone-light")
        p.quad((*i0, h + 0.1), (*i1, h + 0.1), (*i1, h), (*i0, h), "stone_trim:stone-light")
    p.poly([(r * math.cos(2 * math.pi * k / n), r * math.sin(2 * math.pi * k / n), 0.05) for k in range(n)],
           "water:water-deep")
    # posts on the curb, the windlass, rope and bucket, the roof
    zp = 2.15
    for sx in (-1, 1):
        p.box(sx * 0.83 - 0.07, -0.07, h, sx * 0.83 + 0.07, 0.07, zp, "timber:earth-dark", skip=("bottom",))
    zw = 1.4
    p.cylinder((-0.76, 0, zw), (0.76, 0, zw), 0.07, "timber:earth-mid", n=10)
    p.cylinder((0.9, 0, zw), (0.98, 0, zw), 0.025, "metal:ink-700", n=6)
    p.beam((0.98, 0, zw), (0.98, 0, zw - 0.28), 0.03, 0.03, "metal:ink-700")
    p.cylinder((0.98, 0, zw - 0.28), (1.1, 0, zw - 0.28), 0.02, "timber:earth-mid", n=6)
    for k in range(5):                                    # rope wound on the drum
        x = -0.25 + k * 0.05
        p.cylinder((x, 0, zw), (x + 0.045, 0, zw), 0.085, "timber:earth-light", n=10)
    p.cylinder((-0.05, -0.08, zw), (-0.05, -0.08, 0.95), 0.012, "timber:earth-light", n=5)
    p.cylinder((-0.05, -0.08, 0.95), (-0.05, -0.08, 0.66), 0.14, "timber:wood-warm", n=10, r1=0.12)   # the bucket
    p.cylinder((-0.05, -0.08, 0.95), (-0.05, -0.08, 0.93), 0.145, "metal:stone-dark", n=10)
    # the gable roof, ridge along x
    prof = ak.gable_profile(-0.62, 0.62, zp, 42.0, 0.3)
    ak.sweep_roof(p, prof, -1.25, 1.25, 0.07, "shingle:barn-red", "timber:earth-mid", "timber:earth-dark")
    p.box(-1.0, -0.05, zp - 0.04, 1.0, 0.05, zp + 0.08, "timber:earth-dark")
    p.box(-1.25, -0.05, prof[1][1] + 0.05, 1.25, 0.05, prof[1][1] + 0.12, "timber:earth-dark")
    return p


def bench(variant):
    """A park bench (the art's): three seat slats and two back slats on dark iron ends,
    1.8 m long, front toward -Y."""
    p = ak.Part()
    L = 1.8
    wood = "plank:wood-warm" if variant != "b" else "plank:earth-mid"
    for k, y in enumerate((-0.22, -0.08, 0.06)):
        p.box(-L / 2, y, 0.44, L / 2, y + 0.12, 0.475, wood)
    for z in (0.62, 0.78):
        p.box(-L / 2, 0.19, z, L / 2, 0.215, z + 0.1, wood)
    for x in (-L / 2 + 0.18, L / 2 - 0.18):
        iron = "metal:ink-700"
        p.beam((x, -0.25, 0.0), (x, -0.2, 0.44), 0.05, 0.05, iron)          # front leg
        p.beam((x, 0.2, 0.0), (x, 0.2, 0.9), 0.05, 0.05, iron)              # back leg up the back
        p.beam((x, -0.26, 0.43), (x, 0.2, 0.43), 0.05, 0.035, iron)         # seat rail
        p.beam((x, -0.27, 0.65), (x, 0.16, 0.67), 0.045, 0.04, iron)        # arm
        p.beam((x, -0.25, 0.44), (x, -0.27, 0.66), 0.04, 0.04, iron)
        p.box(x - 0.05, -0.3, -0.05, x + 0.05, 0.25, 0.02, iron)            # the foot
    return p


def planter(variant, rng):
    """A wooden planter box of autumn mums: foliage mounds + flower heads."""
    p = ak.Part()
    w, d, h = 0.95, 0.5, 0.42
    for k in range(3):
        z = k * h / 3
        p.box(-w / 2, -d / 2, z, w / 2, d / 2, z + h / 3 - 0.01, "plank:earth-mid")
    for sx in (-1, 1):
        for sy in (-1, 1):
            qx, qy = sx * (w / 2 - 0.015), sy * (d / 2 - 0.015)
            p.box(qx - 0.035, qy - 0.035, 0.0, qx + 0.035, qy + 0.035, h + 0.02, "plank:earth-dark")
    p.box(-w / 2 - 0.03, -d / 2 - 0.03, h, w / 2 + 0.03, d / 2 + 0.03, h + 0.03, "plank:earth-dark")
    p.quad((-w / 2 + 0.04, -d / 2 + 0.04, h - 0.04), (w / 2 - 0.04, -d / 2 + 0.04, h - 0.04),
           (w / 2 - 0.04, d / 2 - 0.04, h - 0.04), (-w / 2 + 0.04, d / 2 - 0.04, h - 0.04), "mud:earth-dark")
    bloom = {0: "mums:barn-red", 1: "mums:lantern", 2: "mums:cream"}.get(variant, "mums:lantern")
    for k in range(3):                       # three plants, domes of bloom over the soil
        cx = -0.28 + k * 0.28 + rng.uniform(-0.03, 0.03)
        dome = pk.rock(rng, 0.19, squash=0.8, mat=bloom, sub=2, rough=0.18)
        p.add(dome, T(cx, rng.uniform(-0.03, 0.03), h - 0.07) @ Rz(rng.uniform(0, 6.3)))
    return p


def notice_board(rng):
    p = ak.Part()
    W, H, zb = 1.7, 1.05, 0.85
    for x in (-W / 2 - 0.08, W / 2 + 0.08):
        pk.post(p, x, 0.01, 2.05, 0.13, "timber:earth-dark", sink=0.6)
    p.box(-W / 2, -0.02, zb, W / 2, 0.04, zb + H, "plank:wood-warm")          # the board (behind)
    p.box(-W / 2 + 0.07, -0.03, zb + 0.07, W / 2 - 0.07, -0.02, zb + H - 0.07, "paint:earth-base", skip=("back",))
    for (x0, z0, x1, z1) in ((-W / 2, zb - 0.06, W / 2, zb), (-W / 2, zb + H, W / 2, zb + H + 0.06),
                             (-W / 2 - 0.02, zb, -W / 2 + 0.06, zb + H), (W / 2 - 0.06, zb, W / 2 + 0.02, zb + H)):
        p.box(x0, -0.06, z0, x1, 0.04, z1, "plank:earth-dark")                 # the frame
    # the notices (the art's drawn rules, never letters) with barn-red pins
    for (cx, cz, w, h) in ((-0.48, zb + 0.72, 0.42, 0.28), (0.05, zb + 0.78, 0.3, 0.22), (-0.35, zb + 0.28, 0.36, 0.22),
                           (0.42, zb + 0.34, 0.5, 0.3), (0.52, zb + 0.8, 0.22, 0.28)):
        tilt = rng.uniform(-0.05, 0.05)
        sheet = ak.Part().box(-w / 2, -0.034, -h / 2, w / 2, -0.03, h / 2, "paper:cream", skip=("back",))
        sheet.cylinder((0, -0.034, h / 2 - 0.03), (0, -0.045, h / 2 - 0.03), 0.012, "paint:barn-red", n=6)
        p.add(sheet, T(cx, 0, cz) @ ak.Ry(tilt))
    prof = ak.gable_profile(-0.18, 0.12, 2.05, 30.0, 0.12)
    ak.sweep_roof(p, prof, -W / 2 - 0.2, W / 2 + 0.2, 0.05, "shingle:stone-dark", "plank:earth-dark", "plank:earth-dark")
    return p


# ---------------------------------------------------------------------------
# The farm: mailbox, shipping bin, the pen, the log, the storm slide
# ---------------------------------------------------------------------------

def mailbox():
    p = ak.Part()
    pk.post(p, 0.0, 0.12, 1.0, 0.1, "timber:wood-warm", sink=0.6)
    p.box(-0.12, -0.2, 1.0, 0.12, 0.3, 1.04, "plank:wood-warm")
    # the tunnel box: D section, door to the front (-Y)
    ring = [(0.09 * math.cos(a), 1.04 + 0.1 + 0.1 * math.sin(a)) for a in np.linspace(0, math.pi, 7)]
    ring = [(0.09, 1.04)] + ring + [(-0.09, 1.04)]
    ring = [(y, z) for y, z in ring]
    sec = [(x, ring) for x in (-0.24, 0.24)]
    box = pk.loft(ak.Part(), sec, lambda i, j: "metal:stone-base", cap0="metal:stone-light", cap1="metal:stone-base")
    p.add(box, Rz(math.pi / 2))          # the tunnel runs along y, the door at -y
    p.box(-0.04, -0.25, 1.1, 0.04, -0.245, 1.14, "metal:ink-700")                                # the latch
    p.box(0.1, 0.02, 1.08, 0.105, 0.3, 1.11, "paint:barn-red")                                  # the flag, down
    p.box(0.1, 0.24, 1.08, 0.105, 0.3, 1.2, "paint:barn-red")
    return p


def shipping_bin(rng):
    p = ak.Part()
    W, D, H = 1.55, 0.85, 0.78
    for k in range(4):
        z = 0.08 + k * (H - 0.08) / 4
        for y0, y1 in ((-D / 2, -D / 2 + 0.03), (D / 2 - 0.03, D / 2)):
            p.box(-W / 2, y0, z, W / 2, y1, z + (H - 0.08) / 4 - 0.012, "plank:earth-mid")
        for x0, x1 in ((-W / 2, -W / 2 + 0.03), (W / 2 - 0.03, W / 2)):
            p.box(x0, -D / 2, z, x1, D / 2, z + (H - 0.08) / 4 - 0.012, "plank:earth-mid")
    for sx in (-1, 1):
        for sy in (-1, 1):
            qx, qy = sx * (W / 2 - 0.02), sy * (D / 2 - 0.02)
            p.box(qx - 0.045, qy - 0.045, 0.0, qx + 0.045, qy + 0.045, H, "timber:earth-dark")
    p.quad((-W / 2, -D / 2, 0.1), (W / 2, -D / 2, 0.1), (W / 2, D / 2, 0.1), (-W / 2, D / 2, 0.1), "interior_dark")
    # the lid, shut: planks on two battens, a strap hinge at the back
    lid = ak.Part()
    for k in range(5):
        x0 = -W / 2 - 0.04 + k * (W + 0.08) / 5
        lid.box(x0 + 0.005, -D / 2 - 0.05, 0.0, x0 + (W + 0.08) / 5 - 0.005, D / 2 + 0.03, 0.035, "plank:wood-warm")
    for x in (-W / 2 + 0.2, W / 2 - 0.2):
        lid.box(x - 0.03, D / 2 - 0.05, -0.012, x + 0.03, D / 2 + 0.035, 0.04, "metal:ink-700")
    p.add(lid, T(0, 0, H + 0.004) @ Rx(rng.uniform(-0.01, 0.01)))
    return p


def fence(placer, placed, p, rng):
    """The pen's post-and-two-rail fence along the centres of its perimeter tiles, the
    gate cell's leaf swung open. World-coordinate Part (origin world 0)."""
    x0, y0, x1, y1 = pk.rect_m(placed, p)
    X0, X1, Y0, Y1 = x0 + TM / 2, x1 - TM / 2, y0 + TM / 2, y1 - TM / 2
    gx, _gy = pk.tile_m(placed, p["gate"]["x"], p["gate"]["y"])
    gate_side = "S" if p["gate"]["y"] == p["y"] + p["h"] - 1 else "N"
    yg = Y0 if gate_side == "S" else Y1
    ga, gb = gx - TM / 2 + 0.1, gx + TM / 2 - 0.1                 # the gate posts
    part = ak.Part()

    def z(x, y):
        return placer.z(x, y)

    def post_at(x, y, big=False):
        w = 0.17 if big else 0.12
        pp = ak.Part()
        pk.post(pp, 0.0, 0.0, 1.25 if big else 1.15, w, "timber:earth-dark", sink=0.6)
        part.add(pp, T(x, y, z(x, y)) @ Rz(rng.uniform(-0.15, 0.15)) @ ak.Ry(rng.uniform(-0.025, 0.025)))

    def rails(a, b):
        for h in (0.45, 0.95):
            za, zb = z(*a) + h + rng.uniform(-0.03, 0.03), z(*b) + h + rng.uniform(-0.03, 0.03)
            part.beam((a[0], a[1], za), (b[0], b[1], zb), 0.07, 0.11, "plank:wood-warm")

    sides = [((X0, Y1), (X1, Y1)), ((X1, Y1), (X1, Y0)), ((X1, Y0), (X0, Y0)), ((X0, Y0), (X0, Y1))]
    for (ax, ay), (bx, by) in sides:
        L = math.hypot(bx - ax, by - ay)
        n = int(round(L / TM))
        pts = [(ax + (bx - ax) * k / n, ay + (by - ay) * k / n) for k in range(n + 1)]
        on_gate_side = abs(ay - yg) < 1e-6 and abs(by - yg) < 1e-6
        if on_gate_side:           # the gate posts replace the posts round the gate cell
            pts = [q for q in pts if not (ga - 0.6 < q[0] < gb + 0.6)] + [(ga, yg), (gb, yg)]
            pts.sort(key=lambda q: q[0] * (1 if bx > ax else -1))
        for q in pts[:-1]:
            post_at(*q, big=on_gate_side and q[0] in (ga, gb))
        for a, b in zip(pts[:-1], pts[1:]):
            if on_gate_side and {round(a[0], 4), round(b[0], 4)} == {round(ga, 4), round(gb, 4)}:
                continue
            rails(a, b)
    # the gate leaf: a five-bar farm gate hinged on the west post, swung open outward
    leaf = ak.Part()
    Lg = gb - ga - 0.18
    for k, zz in enumerate((0.25, 0.45, 0.65, 0.85, 1.05)):
        leaf.box(0.0, -0.03, zz, Lg, 0.03, zz + 0.09, "plank:earth-light")
    leaf.box(0.0, -0.04, 0.2, 0.1, 0.04, 1.16, "plank:earth-mid")
    leaf.box(Lg - 0.1, -0.04, 0.2, Lg, 0.04, 1.16, "plank:earth-mid")
    leaf.beam((0.05, -0.045, 0.3), (Lg - 0.05, -0.045, 1.1), 0.09, 0.03, "plank:earth-mid", up=(0, -1, 0))
    for zz in (0.35, 0.95):
        leaf.box(-0.05, -0.02, zz, 0.2, 0.02, zz + 0.04, "metal:ink-700")
    swing = math.radians(-105.0 if gate_side == "S" else 105.0)
    part.add(leaf, T(ga + 0.09, yg, z(ga, yg)) @ Rz(swing))
    return part


def fallen_log(rng, L=2.4, r=0.24):
    p = ak.Part()
    p.cylinder((-L / 2, 0, r * 0.85), (L / 2, 0, r * 0.8), r, "bark:earth-dark", n=12, caps=False, r1=r * 0.88)
    ring = [(r * math.cos(a), r * 0.85 + r * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 12, endpoint=False)]
    p.poly([(-L / 2, -y, z) for y, z in ring], "plank:earth-light")
    p.poly([(L / 2, y * 0.88, z * 0.94) for y, z in ring], "plank:earth-light")
    for k in range(3):
        x = rng.uniform(-L / 3, L / 3)
        a_ = rng.uniform(0.3, 2.8)
        p.cylinder((x, 0, r), (x + rng.uniform(-0.2, 0.2), r * 2.6 * math.cos(a_), r + r * 2.2 * math.sin(a_)),
                   0.05, "bark:earth-dark", n=6, r1=0.02)
    return p


def trunk(rng, L, r, broken_top=True):
    """A snapped trunk lying along +x from a torn butt (root plate stub) to a splintered
    break, a few dead limbs."""
    p = ak.Part()
    p.cylinder((0, 0, r * 0.8), (L, 0, r * 0.7), r, "bark:earth-dark", n=12, caps=False, r1=r * 0.7)
    ring = [(r * math.cos(a), r * 0.8 + r * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 12, endpoint=False)]
    p.poly([(0.0, -y, z) for y, z in ring], "mud:earth-dark")                   # the torn butt, mud-caked
    for k in range(11):                                                         # splinters at the break
        a_ = 2 * math.pi * k / 11 + rng.uniform(-0.2, 0.2)
        rr = r * 0.7 * rng.uniform(0.25, 0.85)
        y, zz = rr * math.cos(a_), r * 0.7 + rr * math.sin(a_)
        p.cylinder((L - 0.03, y, zz), (L + rng.uniform(0.06, 0.3), y * 1.1, zz + rng.uniform(-0.03, 0.03)),
                   rng.uniform(0.04, 0.07), "plank:earth-light", n=4, r1=0.01)
    p.poly([(L, y * 0.7, 0.7 * r + (z - 0.8 * r) * 0.7) for y, z in ring], "plank:earth-light")
    for k in range(4):                                                          # dead limbs, splayed low
        x = rng.uniform(L * 0.35, L * 0.9)
        side = rng.choice((-1, 1))
        ln = rng.uniform(0.9, 2.0)
        up = rng.uniform(0.1, 0.45)
        tip = (x + ln * 0.6, side * ln * 0.75, r * 0.8 + ln * up)
        p.cylinder((x, side * r * 0.6, r * 0.9), tip, 0.06, "bark:earth-dark", n=6, r1=0.015)
        p.cylinder((tip[0] - ln * 0.3, tip[1] * 0.6, (r + tip[2]) / 2),
                   (tip[0] - ln * 0.1, tip[1] * 0.6 + side * 0.5, tip[2] * 0.7), 0.025, "bark:earth-dark", n=5, r1=0.006)
    return p


def debris(placer, placed, pieces, rng):
    """The storm slide over the farm road, as one world-coordinate Part: a mud fan
    spilling off the wooded (east) side across the dump's debris tiles, boulders at the
    rock tiles, snapped trunks across the log tiles."""
    rects = [pk.rect_m(placed, p) for p in pieces]
    x0, y0 = min(r[0] for r in rects), min(r[1] for r in rects)
    x1, y1 = max(r[2] for r in rects), max(r[3] for r in rects)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    zc = placer.z(cx, cy)
    part = ak.Part()
    fan = ak.Part()
    rx0, ry0, rx1, ry1 = x0 - 1.4, y0 - 1.2, x1 + 2.2, y1 + 1.2
    ground = np.vectorize(placer.z)
    pk.mound(fan, rng, rx0, ry0, rx1, ry1, 0.85, "mud:earth-dark", cell=0.25, lumps=18,
             fn=lambda X, Y: 0.4 * np.clip((X - x0) / (x1 - x0 + 2), 0, 1), base=lambda X, Y: ground(X, Y) - zc)
    part.add(fan, T(0, 0, zc - 0.03))
    for p, (rx0_, ry0_, rx1_, ry1_) in zip(pieces, rects):
        px, py = (rx0_ + rx1_) / 2, (ry0_ + ry1_) / 2
        if p["piece"] == "rock":
            big = pk.rock(rng, rng.uniform(0.75, 0.95), squash=0.66, mat="boulder:stone-base", sub=2, rough=0.4)
            part.add(big, T(px, py, zc + 0.15) @ Rz(rng.uniform(0, 6.28)))
            for _ in range(3):
                small = pk.rock(rng, rng.uniform(0.18, 0.4), squash=0.7, mat="boulder:stone-light", sub=1, rough=0.4)
                part.add(small, T(px + rng.uniform(-1.3, 1.3), py + rng.uniform(-1.3, 1.3), zc + 0.2) @ Rz(rng.uniform(0, 6)))
        elif p["piece"] == "log":
            L, r = rng.uniform(5.0, 6.5), rng.uniform(0.26, 0.34)
            ang = math.radians(rng.uniform(150, 200))           # butt up the slope (east), lying across the road
            tr = trunk(rng, L, r)
            bx, by = px + math.cos(ang + math.pi) * L * 0.45, py + math.sin(ang + math.pi) * L * 0.45
            part.add(tr, T(bx, by, zc + 0.12) @ Rz(ang) @ ak.Ry(math.radians(-3)))
        else:
            raise AssertionError(f"debris piece {p['piece']!r}")
    return part


# ---------------------------------------------------------------------------
# Pell's sedan (GuestCar.cs): a late-1950s four-door, nose +X (turned west on placing)
# ---------------------------------------------------------------------------

def sedan(paint):
    p = ak.Part()
    body = f"carpaint:{paint}"
    L, HW = 5.3, 0.97
    xf, xr = 1.55, -1.45                     # the axles (wheelbase 3.0 m)
    rw = 0.36

    def zb(x):                               # the underside, notched over the wheels (arches)
        z = 0.3
        for xa in (xf, xr):
            d = abs(x - xa)
            if d < 0.47:
                z = max(z, rw + math.sqrt(0.47 ** 2 - d ** 2) * 0.95)
        return z

    def interp(x, xs_, zs_):
        return float(np.interp(x, xs_, zs_))

    def zf(x):                               # the fender line: fins at the tail, hooded lamps at the nose
        return interp(x, [-2.65, -2.52, -1.9, -1.2, 0.0, 1.2, 2.3, 2.65], [0.99, 1.06, 1.0, 0.975, 0.965, 0.955, 0.94, 0.87])

    def zh(x):                               # the hood / rear deck between the fenders (none under the cab)
        if -1.45 <= x <= 1.05:
            return zf(x)
        return interp(x, [-2.65, -2.5, -1.45, 1.05, 1.6, 2.3, 2.65], [0.84, 0.92, zf(-1.45), zf(1.05), 0.905, 0.88, 0.8])

    def zt(x):
        return zh(x)

    def hw(x):
        return interp(x, [-2.65, -2.4, 2.3, 2.65], [0.9, HW, HW, 0.9])

    def ring(x):
        """The lower body's section: rounded sills, a fender crown each side, the hood /
        deck dipping between them (20 points; the bottom is the last quad)."""
        z0, f, h, w = zb(x), zf(x), zh(x), hw(x)
        rb = min(0.08, (f - z0) * 0.25)
        rf = min(0.15, (f - z0) * 0.45)
        pts = []
        for k in range(4):
            a_ = math.radians(-90 + 30 * k)
            pts.append((w - rb + rb * math.cos(a_), z0 + rb + rb * math.sin(a_)))
        for k in range(4):
            a_ = math.radians(30 * k)
            pts.append((w - rf + rf * math.cos(a_), f - rf + rf * math.sin(a_)))
        pts += [(w - rf - 0.14, f - 0.4 * (f - h)), (w * 0.45, h)]
        pts += [(-w * 0.45, h), (-(w - rf - 0.14), f - 0.4 * (f - h))]
        for k in range(4):
            a_ = math.radians(90 + 30 * k)
            pts.append((-(w - rf) + rf * math.cos(a_), f - rf + rf * math.sin(a_)))
        for k in range(4):
            a_ = math.radians(180 + 30 * k)
            pts.append((-(w - rb) + rb * math.cos(a_), z0 + rb + rb * math.sin(a_)))
        return pts
    xs = sorted(set(np.round(np.concatenate([np.linspace(-2.65, 2.65, 27), [xf - 0.47, xf, xf + 0.47,
                                                                             xr - 0.47, xr, xr + 0.47, -1.45, 1.05],
                                              np.linspace(xf - 0.44, xf + 0.44, 7),
                                              np.linspace(xr - 0.44, xr + 0.44, 7)]), 4)))
    sec = [(x, ring(x)) for x in xs]
    pk.loft(p, sec, lambda i, j: "interior_dark" if j == 19 else body, cap0=body, cap1=body)
    # wheel houses (so the arches read dark, not see-through)
    for xa in (xf, xr):
        p.box(xa - 0.47, -0.62, 0.3, xa + 0.47, 0.62, 0.86, "interior_dark")
    # the greenhouse: windshield, roof, B pillar, rear window
    cab = [(1.03, 0.99, 0.02), (0.4, 1.41, 0.0), (0.28, 1.43, 0.0), (-0.08, 1.43, 0.0), (-0.2, 1.43, 0.0),
           (-0.66, 1.42, 0.0), (-0.78, 1.4, 0.0), (-1.43, 1.0, 0.02)]
    csec = []
    for x, top, _ in cab:
        base = zt(x) - 0.02
        hw_ = hw(x) - 0.08
        csec.append((x, pk.rounded_rect(hw_, base, max(top, base + 0.05), min(0.14, (top - base) * 0.45) if top - base > 0.1 else 0.012,
                                        0.005, n=3, taper=0.2 * min(1.0, (top - base) / 0.45))))

    def cmat(i, j):
        side = j in (3, 11)
        top = 4 <= j <= 10
        if i == 0:
            return "glass" if (top or side) else body          # the windshield
        if i == 6:
            return "glass" if top else body                    # the rear window
        if i in (2, 4):
            return "glass" if (side or j in (4, 10)) else body   # the door glass
        return body                                            # header, B pillar, C pillar
    pk.loft(p, csec, cmat, smooth=True)
    # chrome: bumpers wrapping the ends, the grille, the side spear, trim
    ch = "chrome:stone-light"
    for sgn in (1, -1):
        xa, xb = sorted((sgn * 2.6, sgn * 2.76))
        p.box(xa, -HW + 0.05, 0.32, xb, HW - 0.05, 0.48, ch)
        for sy in (-1, 1):
            p.beam((sgn * 2.68, sy * (HW - 0.08), 0.4), (sgn * 2.3, sy * (HW + 0.03), 0.4), 0.1, 0.14, ch)
    p.box(2.6, -0.62, 0.5, 2.67, 0.62, 0.72, "interior_dark")                    # the grille
    for z in (0.54, 0.6, 0.66):
        p.box(2.66, -0.6, z, 2.69, 0.6, z + 0.022, ch)
    for sy in (-1, 1):
        p.cylinder((2.55, sy * 0.7, 0.72), (2.68, sy * 0.7, 0.72), 0.095, ch, n=12)
        p.cylinder((2.68, sy * 0.7, 0.72), (2.7, sy * 0.7, 0.72), 0.075, "paint:cream", n=12)    # headlamps
        p.box(-2.67, sy * 0.72 - 0.1, 0.72, -2.62, sy * 0.72 + 0.1, 0.9, "paint:#7a3028")        # taillights
        for sx0, sx1 in ((xr + 0.5, xf - 0.5), (-2.45, xr - 0.5)):                             # side spear
            p.box(sx0, sy * (HW + 0.004) - 0.01, 0.72, sx1, sy * (HW + 0.004) + 0.01, 0.735, ch)
        p.box(0.1, sy * (HW + 0.01) - 0.01, 0.9, 0.22, sy * (HW + 0.01) + 0.01, 0.92, ch)         # door handles
        p.box(-1.05, sy * (HW + 0.01) - 0.01, 0.9, -0.93, sy * (HW + 0.01) + 0.01, 0.92, ch)
        p.box(-0.05, sy * HW - 0.004, 0.36, -0.04, sy * HW + 0.004, 0.94, "interior_dark")        # door shuts
        p.box(1.05, sy * HW - 0.004, 0.4, 1.06, sy * HW + 0.004, 0.94, "interior_dark")
        p.box(-1.15, sy * HW - 0.004, 0.36, -1.14, sy * HW + 0.004, 0.94, "interior_dark")
    p.box(-2.69, -0.2, 0.52, -2.66, 0.2, 0.68, "paint:stone-pale")                 # the plate (blank)
    # wheels: black tyres with whitewalls, chrome hubcaps
    for xa in (xf, xr):
        for sy in (-1, 1):
            y0, y1 = sy * 0.62, sy * 0.84
            p.cylinder((xa, y0, rw), (xa, y1, rw), rw, "rubber", n=18)
            p.cylinder((xa, y1, rw), (xa, y1 + sy * 0.004, rw), rw * 0.8, "paint:cream", n=18)
            p.cylinder((xa, y1 + sy * 0.004, rw), (xa, y1 + sy * 0.006, rw), rw * 0.62, "rubber", n=18)
            p.cylinder((xa, y1, rw), (xa, y1 + sy * 0.035, rw), rw * 0.56, ch, n=14, r1=rw * 0.34)
    return p


# ---------------------------------------------------------------------------
# The drive-in: screen, speakers
# ---------------------------------------------------------------------------

def screen(rng, W):
    """DriveInScreen.cs as a structure (local: the face in the plane y 0, facing -Y,
    x 0..W; the bents behind at +y). Face ~11 m tall on 4.5 m of legs; the art's
    top-right panels gone."""
    a = ak.Assembly("Screen")
    b = a.body
    zb, H = 4.5, 11.0
    zt = zb + H
    t = 0.12
    # the missing panels (DriveInScreen.cs: 23 x 9 px + 12 x 4 px under it, of 224 x 56)
    sx = W / 224.0
    sz = H / 56.0
    holes = [(W - 26 * sx, W - 3 * sx, zt - 12 * sz, zt - 3 * sz), (W - 26 * sx, W - 14 * sx, zt - 16 * sz, zt - 12 * sz)]
    outline = [(0.0, zb), (W, zb), (W, zt), (0.0, zt)]
    face = ak.panel(outline, holes, "screen:cream", y=0.0)
    back = ak.panel(outline, holes, "plywood:stone-light", y=t)
    back.polys = [(q[::-1], m, sm) for q, m, sm in back.polys]      # the back faces +Y
    b.add(face)
    b.add(back)
    for u0, u1, z0, z1 in holes:
        b.add(ak.reveals([(u0, u1, z0, z1)], t, "plywood:stone-light"))
    # the casing: an ink-700 frame round the face (the art's 3 px border)
    c = 0.35
    for (x0, z0, x1, z1) in ((-c, zb - c, W + c, zb), (-c, zt, W + c, zt + c), (-c, zb, 0.0, zt), (W, zb, W + c, zt)):
        b.box(x0, -0.08, z0, x1, t + 0.05, z1, "paint:ink-700")
    # framing behind: girts every 1.22 m, studs every 2.44 m (seen through the hole)
    for z in np.arange(zb + 0.6, zt - 0.2, 1.22):
        b.box(0.0, t, z, W, t + 0.2, z + 0.1, "timber:ink-500")
    for x in np.arange(0.0, W + 0.01, 2.44):
        b.box(x - 0.05, t, zb, x + 0.05, t + 0.14, zt, "timber:ink-500")
    # three trestle bents (the art's three legs): front post, raked back post, braces
    for fx in (W / 6, W / 2, W - W / 6):
        for dx in (-0.35, 0.35):
            x = fx + dx
            b.box(x - 0.14, t + 0.2, -0.6, x + 0.14, t + 0.48, zt - 0.3, "timber:ink-500", skip=("bottom",))
            b.beam((x, t + 0.34, zt - 0.6), (x, 5.2, -0.4), 0.26, 0.26, "timber:ink-500")
            for zz in (1.2, 5.5, 9.5):
                back_y = t + 0.34 + (5.2 - t - 0.34) * (zt - 0.6 - zz) / (zt - 0.2)
                if back_y > t + 0.6:
                    b.beam((x, t + 0.34, zz), (x, back_y, zz), 0.18, 0.18, "timber:ink-500")
            b.beam((x, t + 0.34, 1.2), (x, 3.3, 5.5), 0.14, 0.14, "timber:ink-500")
            b.beam((x, t + 0.34, 9.5), (x, 1.8, 5.5), 0.14, 0.14, "timber:ink-500")
        b.box(fx - 0.5, t + 0.2, 0.9, fx + 0.5, t + 0.48, 1.1, "timber:ink-500")
    for zz in (2.6, 8.0):                    # longitudinal ties between the bents
        b.box(W / 6, t + 0.48, zz, W - W / 6, t + 0.62, zz + 0.2, "timber:ink-500")
    for fx in (W / 6, W / 2, W - W / 6):     # concrete footings
        for dy in (t + 0.34, 5.2):
            b.box(fx - 0.7, dy - 0.4, -0.5, fx + 0.7, dy + 0.4, 0.15, "stone_trim:stone-base", skip=("bottom",))
    return a


def speaker_post(rng, both=True):
    p = ak.Part()
    p.cylinder((0, 0, -0.3), (0, 0, 0.12), 0.14, "stone_trim:stone-pale", n=10)
    p.cylinder((0, 0, 0.1), (0, 0, 1.05), 0.03, "metal:stone-shade", n=8)
    p.box(-0.07, -0.05, 1.02, 0.07, 0.05, 1.13, "metal:stone-dark")               # the junction head
    for k, sx in enumerate((-1, 1)):
        if not both and k == 1:
            p.cylinder((sx * 0.07, 0, 1.06), (sx * 0.2, 0, 0.98), 0.006, "rubber", n=4)   # a perished cable stub
            continue
        sp = ak.Part()
        sp.box(-0.13, -0.05, -0.1, 0.13, 0.05, 0.1, "metal:stone-dark")          # the box
        sp.box(-0.09, -0.056, -0.07, 0.09, -0.05, 0.07, "metal:stone-base", skip=("back",))   # grille
        for z in (-0.04, 0.0, 0.04):
            sp.box(-0.085, -0.058, z - 0.006, 0.085, -0.056, z + 0.006, "metal:stone-light", skip=("back",))
        sp.box(-0.02, 0.05, 0.06, 0.02, 0.08, 0.12, "metal:ink-700")              # the window hook
        p.add(sp, T(sx * 0.22, 0, 0.98) @ Rz(sx * math.pi / 2 + rng.uniform(-0.2, 0.2)) @ ak.S(1.15))
        p.box(sx * 0.07, -0.015, 1.05, sx * 0.13, 0.015, 1.08, "metal:ink-700")
    return p


# ---------------------------------------------------------------------------
# From the dump
# ---------------------------------------------------------------------------

SEDAN_L = 5.1      # a late-50s full-size four-door, bumpers ~5.3 m: it fits its stall side-on


def _stall(placed, x, y):
    """The car's x centred in the lot stall it stands in (between the dump's stall
    stripes), or unchanged."""
    s = config.TILE_M / config.TILE_PX
    for p in placed.data["props"]:
        if p["kind"] != "stall_stripes":
            continue
        x0, y0, x1, y1 = pk.rect_m(placed, p)
        if not (y0 <= y <= y1):
            continue
        edges = sorted(x0 + (sx + sw / 2) * s for sx, _sy, sw, _sh in p["stripesPx"])
        for a, b in zip(edges[:-1], edges[1:]):
            if a <= x <= b:
                return (a + b) / 2, [round(a, 3), round(b, 3)]
    return x, None


def _facing_to(x, y, tx, ty):
    """N or S, whichever looks toward (tx, ty)."""
    return "N" if ty > y else "S"


def build(world, routes, placer, lights_col):
    """Every dump prop (bar the ground's own and the forest's farm sample). Returns the
    count; raises on a kind with no design (nothing may fall back to a marker)."""
    import bpy

    import scene
    import signs
    import streetlights
    rng = random.Random(config.PROP_SEED)
    sc = bpy.context.scene
    sc[config.PIT_GLOW_PROP] = config.PIT_GLOW
    n = 0
    for placed in world.maps.values():
        props = placed.data["props"]
        debris_pieces = [p for p in props if p["kind"] == "debris"]
        centres = {k: [pk.centre_m(placed, p) for p in props if p["kind"] == k] for k in ("well", "screen")}
        for i, p in enumerate(props):
            kind = p["kind"]
            if kind in SKIP_KINDS or (p.get("sample") and kind in config.FOREST_PROP_KINDS):
                continue
            name = f"Prop_{placed.id}_{kind}_{p['id'] or i}"
            ident = dict(map_id=placed.id, dump_id=p["id"], kind=kind, conditional=p.get("conditional"))
            cx, cy = pk.centre_m(placed, p)
            x0, y0, x1, y1 = pk.rect_m(placed, p)
            if kind == "chain":
                part = chain_run(placer, x0 + 0.25, x1 - 0.25, cy, rng)
                placer.put(part, name, 0.0, 0.0, z=0.0, board=p.get("board"), **ident)
            elif kind == "pit_cover":
                zc = placer.z(cx, cy)
                asm, under, leak = pit(rng, x1 - x0, y1 - y0)
                body = placer.put(asm, name, x0, y0, z=zc, **ident)
                for lname, pt, w, soft, shadow in (("Under", under, config.PIT_LIGHT_W, 0.8, True),
                                                   ("Leak", leak, config.PIT_LIGHT_W * 0.3, 1.2, False)):
                    ob = signs._point(bpy, scene, lights_col, f"Light_Pit_{lname}", body, pt,
                                      config.colour("pit-red"), config.PIT_GLOW_PROP, w, streetlights, radius=soft)
                    ob.data.use_shadow = shadow
            elif kind == "well":
                placer.put(well(rng), name, cx, cy, z=placer.z_max(cx, cy, 0.9), variant=p.get("variant"), **ident)
            elif kind == "bench":
                tgt = min(centres["well"] + centres["screen"], key=lambda c: math.dist(c, (cx, cy)), default=None)
                facing = _facing_to(cx, cy, *tgt) if tgt else pk.toward_road(cy, routes.road_y)
                placer.put(bench(p.get("variant")), name, cx, cy, facing, z=placer.z_max(cx, cy, 0.8),
                           variant=p.get("variant"), **ident)
            elif kind == "planter":
                placer.put(planter(p.get("variant"), rng), name, cx, cy, pk.toward_road(cy, routes.road_y),
                           z=placer.z_max(cx, cy, 0.4), variant=p.get("variant"), **ident)
            elif kind == "notice_board":
                placer.put(notice_board(rng), name, cx, cy, pk.toward_road(cy, routes.road_y), **ident)
            elif kind == "mailbox":
                placer.put(mailbox(), name, cx, cy, "S", **ident)
            elif kind == "shipping_bin":
                placer.put(shipping_bin(rng), name, cx, cy, "S", z=placer.z_max(cx, cy, 0.7), **ident)
            elif kind == "fence":
                placer.put(fence(placer, placed, p, rng), name, 0.0, 0.0, z=0.0, gate_open=p.get("gateOpen"), **ident)
            elif kind == "scatter":
                if p["id"] != "log":
                    raise AssertionError(f"{name}: no design for scatter {p['id']!r}")
                placer.put(fallen_log(rng), name, cx, cy, rot=rng.uniform(-0.3, 0.3), z=placer.z(cx, cy) - 0.05, **ident)
            elif kind == "debris":
                if p is not debris_pieces[0]:
                    continue                 # one slide for all the pieces
                part = debris(placer, placed, debris_pieces, rng)
                placer.put(part, f"Prop_{placed.id}_debris_slide", 0.0, 0.0, z=0.0,
                           pieces=len(debris_pieces), **ident)
            elif kind == "car":
                rot = {"W": math.pi, "E": 0.0}[p["facing"]]
                cx, stall = _stall(placed, cx, cy)
                car = sedan(p["paint"]).moved(ak.S(SEDAN_L / 5.3, 1.0, 1.0))
                placer.put(car, name, cx, cy, rot=rot, z=placer.z(cx, cy), paint=p["paint"],
                           facing=p["facing"], stall=stall, **ident)
            elif kind == "screen":
                W = (x1 - x0) * 0.92
                asm = screen(rng, W)
                # faces north over the field: local x runs west (turned 180), the face on the
                # footprint's north edge, the bents behind it (south)
                placer.put(asm, name, cx + W / 2, y1 - 0.3, "N", z=placer.z_max(cx, y1 - 2, 3.0), hull=True, **ident)
            elif kind == "speaker":
                both = rng.random() > 0.25
                placer.put(speaker_post(rng, both), name, cx, cy, "S", **ident)
            else:
                raise AssertionError(f"{name}: no design for prop kind {kind!r}")
            n += 1
    return n

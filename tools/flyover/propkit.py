"""The prop kit: geometry beyond archkit's building parts (lofted bodies, rocks, mounds,
catenary chains, tapered poles) and the placement helpers props.py / signs.py /
streetlights.py share. Geometry is archkit Parts (polygon soups, material keys), so
the same archmats keys paint everything.

Local frame (archkit's): +X along the prop's front, the front faces -Y, Z up from the
ground; `put()` turns it by a facing (config/archkit FACING_ROT: S 0, N pi, E, W) and
grounds it with Terrain.z_at. Positions and facings always come from the dump.
"""

import math

import numpy as np

import archkit as ak
import config
from archkit import T, Rz

TM = config.TILE_M


# ---------------------------------------------------------------------------
# Dump -> metres
# ---------------------------------------------------------------------------

def rect_m(placed, p):
    """A dump rect (tiles, y south) -> (x0, y0, x1, y1) metres, y0 < y1."""
    x0 = (placed.ox + p["x"]) * TM
    y1 = -(placed.oy + p["y"]) * TM
    return x0, y1 - p["h"] * TM, x0 + p["w"] * TM, y1


def centre_m(placed, p):
    x0, y0, x1, y1 = rect_m(placed, p)
    return (x0 + x1) / 2, (y0 + y1) / 2


def tile_m(placed, x, y):
    """A tile's centre."""
    return (placed.ox + x + 0.5) * TM, -(placed.oy + y + 0.5) * TM


def px_m(placed, px):
    s = TM / config.TILE_PX
    return placed.ox * TM + px[0] * s, -(placed.oy * TM + px[1] * s)


def road_side(y, road_y):
    """'N' if the point is north of the road's centreline."""
    return "N" if y > road_y else "S"


def toward_road(y, road_y):
    """The facing (S / N) that looks at the paved road from y."""
    return "S" if y > road_y else "N"


# ---------------------------------------------------------------------------
# Realising
# ---------------------------------------------------------------------------

class Placer:
    """Realises Assemblies / Parts into the props collection, grounded on the terrain,
    and keeps the stats (+ crown-clearance hulls of the tall ones)."""

    def __init__(self, terrain, col, resolve):
        self.terrain, self.col, self.resolve = terrain, col, resolve
        self.out = []

    def z(self, x, y):
        return self.terrain.z_at(x, y) if self.terrain is not None else 0.0

    def z_max(self, x, y, r):
        """Highest ground within r of (x, y) (a base's footing reaches down to the rest)."""
        return max(self.z(x + dx * r, y + dy * r) for dx in (-1, 0, 1) for dy in (-1, 0, 1))

    def put(self, asm, name, x, y, facing="S", z=None, rot=None, hull=False, **props):
        """The assembly at (x, y), its front turned to `facing` (or `rot` radians), on
        the ground (z None: the terrain at x, y). Returns the body object."""
        if isinstance(asm, ak.Part):
            part, asm = asm, ak.Assembly(name)
            asm.body = part
        z = self.z(x, y) if z is None else z
        r = ak.FACING_ROT[facing] if rot is None else rot
        M = T(x, y, z) @ Rz(r)
        body, kids = ak.realise(asm, self.col, M, self.resolve, name, **props)
        self.record(name, body, kids, z, hull)
        return body

    def record(self, name, body, kids, z, hull=False):
        tris = sum(len(p.vertices) - 2 for ob in [body] + kids for p in ob.data.polygons)
        info = dict(name=name, tris=tris, objects=1 + len(kids))
        if hull:
            import buildings_hero as bh
            info.update(bh.realised_info(name, body, kids, z))
        body["tris"] = tris
        self.out.append(info)
        return info


# ---------------------------------------------------------------------------
# Geometry
# ---------------------------------------------------------------------------

def loft(part, sections, mat_fn, cap0=None, cap1=None, smooth=True):
    """Rings of equal point count along a body: sections = [(x, [(y, z), ...])], each
    ring CCW seen from +X. Quads between neighbouring rings, material mat_fn(i_section,
    j_point) -> key; optional end caps (keys)."""
    for i in range(len(sections) - 1):
        (xa, ra), (xb, rb) = sections[i], sections[i + 1]
        n = len(ra)
        for j in range(n):
            k = (j + 1) % n
            part.quad((xa, *ra[j]), (xa, *ra[k]), (xb, *rb[k]), (xb, *rb[j]), mat_fn(i, j), smooth)
    if cap0:
        x, r = sections[0]
        part.poly([(x, y, z) for y, z in reversed(r)], cap0)
    if cap1:
        x, r = sections[-1]
        part.poly([(x, y, z) for y, z in r], cap1)
    return part


def rounded_rect(hw, z0, z1, r_top, r_bot, n=3, taper=0.0):
    """A ring (y, z) around a section: half width hw at the bottom (narrowing by `taper`
    at the top), rounded corners r_top / r_bot, CCW seen from +X (y right = +Y... the
    ring runs bottom -> +y side -> top -> -y side)."""
    ht = hw - taper
    pts = []
    for cy, cz, r, a0 in ((hw - r_bot, z0 + r_bot, r_bot, -90), (ht - r_top, z1 - r_top, r_top, 0),
                          (-(ht - r_top), z1 - r_top, r_top, 90), (-(hw - r_bot), z0 + r_bot, r_bot, 180)):
        for k in range(n + 1):
            a = math.radians(a0 + 90 * k / n)
            pts.append((cy + r * math.cos(a), cz + r * math.sin(a)))
    return pts


def taper_pole(part, p0, p1, r0, r1, mat, n=12):
    part.cylinder(p0, p1, r0, mat, n=n, r1=r1)
    return part


def arc_tube(part, pts, r, mat, n=8):
    """A tube along a polyline (the cobra head's mast arm, a pipe): one cylinder per
    segment, joints overlapping."""
    for a, b in zip(pts[:-1], pts[1:]):
        part.cylinder(a, b, r, mat, n=n, caps=False)
    return part


def rock(rng, r, squash=0.7, mat="rubble:stone-base", sub=2, rough=0.22):
    """A boulder: a subdivided icosahedron, displaced by smooth random lumps, flattened
    (squash) and sat on z 0 (its lowest 15% sunk)."""
    t = (1 + 5 ** 0.5) / 2
    V = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
         (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    F = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2),
         (10, 7, 6), (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11),
         (6, 2, 10), (8, 6, 7), (9, 8, 1)]
    V = [np.array(v, float) / np.linalg.norm(v) for v in V]
    for _ in range(sub):
        cache, F2 = {}, []

        def mid(a, b):
            key = (min(a, b), max(a, b))
            if key not in cache:
                m = V[a] + V[b]
                V.append(m / np.linalg.norm(m))
                cache[key] = len(V) - 1
            return cache[key]
        for a, b, c in F:
            ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
            F2 += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        F = F2
    P = np.array(V)
    nr = np.random.default_rng(rng.randrange(1 << 30))       # rng: a random.Random (deterministic)
    lumps = [(nr.normal(size=3), nr.uniform(0.6, 1.4)) for _ in range(6)]
    d = np.ones(len(P))
    for c, w in lumps:
        c = c / np.linalg.norm(c)
        d += rough * w * (P @ c) ** 3
    P = P * d[:, None] * r
    P[:, 2] *= squash
    P[:, 2] -= P[:, 2].min() + 0.15 * r * squash
    part = ak.Part()
    for a, b, c in F:
        part.poly([P[a], P[b], P[c]], mat, smooth=True)
    return part


def mound(part, rng, x0, y0, x1, y1, height, mat, cell=0.25, lumps=12, fn=None, base=None):
    """A lumpy heap on a grid over the rect: height x a smooth falloff to 0 at the edge
    (an irregular outline from the noise), displaced by random lumps; `fn(x, y)` adds a
    height term fading out to the edge (a fan's slope), `base(x, y)` the ground under it
    everywhere. Faces smooth; the edge sinks 8 cm under."""
    nx, ny = int(math.ceil((x1 - x0) / cell)), int(math.ceil((y1 - y0) / cell))
    X, Y = np.meshgrid(np.linspace(x0, x1, nx + 1), np.linspace(y0, y1, ny + 1))
    u = (X - x0) / (x1 - x0) * 2 - 1
    v = (Y - y0) / (y1 - y0) * 2 - 1
    ang = np.arctan2(v, u)
    edge = (1.0 + 0.16 * np.sin(3 * ang + rng.uniform(0, 6)) + 0.1 * np.sin(5 * ang + rng.uniform(0, 6))
            + 0.05 * np.sin(9 * ang + rng.uniform(0, 6)))
    rr = np.hypot(u, v) / edge
    H = height * np.clip(1 - rr ** 2, 0, None) ** 1.6
    for _ in range(lumps):
        cx, cy = rng.uniform(x0, x1), rng.uniform(y0, y1)
        s = rng.uniform(0.3, 1.1)
        H += rng.uniform(-0.12, 0.25) * height * np.exp(-((X - cx) ** 2 + (Y - cy) ** 2) / s ** 2) * (rr < 1)
    fall = np.clip(1 - rr ** 2, 0, None) ** 0.5
    if fn is not None:
        H += fn(X, Y) * fall
    inside = rr < 1.0
    H = np.where(inside, H, -0.08) + (base(X, Y) if base is not None else 0.0)
    for j in range(ny):
        for i in range(nx):
            q = [(X[j, i], Y[j, i], H[j, i]), (X[j, i + 1], Y[j, i + 1], H[j, i + 1]),
                 (X[j + 1, i + 1], Y[j + 1, i + 1], H[j + 1, i + 1]), (X[j + 1, i], Y[j + 1, i], H[j + 1, i])]
            if inside[j:j + 2, i:i + 2].any():
                part.quad(*q, mat, smooth=True)
    return part


def catenary(a, b, sag, n):
    """n+1 points on a hanging chain from a to b (3D), sagging `sag` at mid span."""
    a, b = np.asarray(a, float), np.asarray(b, float)
    out = []
    for k in range(n + 1):
        t = k / n
        p = a + (b - a) * t
        p[2] -= sag * 4 * t * (1 - t)
        out.append(p)
    return out


def chain(part, a, b, sag, mat, link=0.075, wire=0.009):
    """A steel chain hanging from a to b: alternate links turned 90 degrees, each an
    oval ring of a 4-sided wire, laid along the catenary."""
    L = float(np.linalg.norm(np.asarray(b, float) - np.asarray(a, float)))
    n = max(2, int(L / (link * 0.72)))
    pts = catenary(a, b, sag, n)
    ring = []
    for k in range(8):                              # an oval: long along x
        t = 2 * math.pi * k / 8
        ring.append((math.cos(t) * link / 2, math.sin(t) * link * 0.3))
    for k in range(n):
        p, q = pts[k], pts[k + 1]
        d = q - p
        dl = np.linalg.norm(d)
        if dl < 1e-6:
            continue
        ex = d / dl
        side = np.cross(ex, (0, 0, 1.0))
        side = side / (np.linalg.norm(side) or 1.0)
        up = np.cross(side, ex)
        ey = up if k % 2 else side
        ez = np.cross(ex, ey)
        c = (p + q) / 2
        loop = [c + ex * x + ey * y for x, y in ring]
        for i in range(8):
            p0, p1 = loop[i], loop[(i + 1) % 8]
            part.quad(p0 - ez * wire, p1 - ez * wire, p1 + ez * wire, p0 + ez * wire, mat)
            m0, m1 = (p0 - c), (p1 - c)
            m0, m1 = m0 / np.linalg.norm(m0), m1 / np.linalg.norm(m1)
            part.quad(p0 + m0 * wire, p1 + m1 * wire, p1 - m1 * wire, p0 - m0 * wire, mat)
    return part


def post(part, x, y, h, w, mat, cap=None, sink=0.5, bevel=True):
    """A square timber post (w x w) from below ground to h, a cap board or a chamfered
    top."""
    part.box(x - w / 2, y - w / 2, -sink, x + w / 2, y + w / 2, h, mat, skip=("bottom",))
    if cap:
        part.box(x - w / 2 - 0.02, y - w / 2 - 0.02, h, x + w / 2 + 0.02, y + w / 2 + 0.02, h + 0.035, cap)
    return part

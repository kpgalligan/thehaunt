"""The architecture kit: parametric building parts as plain geometry, reused by every
building the flyover designs (buildings_hero.py now, the placeholder buildings next).

Everything is authored in a building's LOCAL frame: +X along the front (east for a
south-facing building), +Y into the building (the front wall is the plane y = 0 and its
outside is -Y), Z up from the ground (z = 0 at the highest ground under the building).
`place()` turns a dump footprint + facing into that frame, so a north-facing building is
the same code turned 180 degrees.

Parts are polygon soups (`Part`): numpy corner arrays + a material KEY (archmats.py
resolves keys like "clapboard:cream" or "glass") + a smooth flag. Every face gets a
metric UV ("UVm") from its own plane: u runs horizontally along the face, v up it (up
the slope on a roof), so siding, courses and shingles line up with eaves and sills.

Walls are `wall()` panels: a convex outline in the wall's own (u, z) plane (a gable or
gambrel end is one pentagon) minus rectangular openings, each opening lined with a
reveal. A wall frame maps wall-local (x along, y INTO the building, z up) to building
space; windows and doors are authored as if on a south wall at y = 0 and placed through
that frame. Repeated parts (windows, doors, vents) are PROTOTYPES: one mesh each, placed
as linked-duplicate child objects (`Assembly.place`) carrying custom props (`glow`).

Roofs are slabs swept along the ridge from a cross-section profile (`sweep_roof`: shed,
gable, gambrel, dormer; holes for a derelict roof) or built as hips (`hip_roof`, a
pyramid when square), plus `flat_roof`. Trim is `band` (plinths, cornices, kick plates,
friezes), `corner_boards`, `gutter`; then `chimney`, `steps`, `cylinder` posts.
"""

import math

import numpy as np

EPS = 1e-6


# ---------------------------------------------------------------------------
# Matrices
# ---------------------------------------------------------------------------

def T(x=0.0, y=0.0, z=0.0):
    m = np.eye(4)
    m[:3, 3] = (x, y, z)
    return m


def Rz(a):
    c, s = math.cos(a), math.sin(a)
    m = np.eye(4)
    m[0, 0], m[0, 1], m[1, 0], m[1, 1] = c, -s, s, c
    return m


def Rx(a):
    c, s = math.cos(a), math.sin(a)
    m = np.eye(4)
    m[1, 1], m[1, 2], m[2, 1], m[2, 2] = c, -s, s, c
    return m


def Ry(a):
    c, s = math.cos(a), math.sin(a)
    m = np.eye(4)
    m[0, 0], m[0, 2], m[2, 0], m[2, 2] = c, s, -s, c
    return m


def S(x, y=None, z=None):
    m = np.eye(4)
    m[0, 0], m[1, 1], m[2, 2] = x, x if y is None else y, x if z is None else z
    return m


def apply(M, pts):
    pts = np.asarray(pts, float)
    return pts @ M[:3, :3].T + M[:3, 3]


def wall_frame(p0, p1):
    """Wall-local (x along p0->p1, y inward, z up) -> building space. Outlines walk the
    footprint COUNTER-clockwise seen from above, so the outside is on the right."""
    return T(p0[0], p0[1]) @ Rz(math.atan2(p1[1] - p0[1], p1[0] - p0[0]))


# ---------------------------------------------------------------------------
# The polygon soup
# ---------------------------------------------------------------------------

class Part:
    """Polygons: (corners (k, 3), material key, smooth)."""

    def __init__(self):
        self.polys = []

    def poly(self, pts, mat, smooth=False):
        pts = np.asarray(pts, float)
        if len(pts) >= 3 and _area(pts) > 1e-9:
            self.polys.append((pts, mat, smooth))
        return self

    def quad(self, a, b, c, d, mat, smooth=False):
        return self.poly([a, b, c, d], mat, smooth)

    def add(self, other, M=None):
        flip = M is not None and np.linalg.det(M[:3, :3]) < 0
        for pts, mat, sm in other.polys:
            q = apply(M, pts) if M is not None else pts.copy()
            self.polys.append((q[::-1] if flip else q, mat, sm))
        return self

    def moved(self, M):
        return Part().add(self, M)

    def warp(self, fn):
        """Move every corner through fn((k, 3) array) -> (k, 3) (an upswept eave)."""
        self.polys = [(fn(p.copy()), m, s) for p, m, s in self.polys]
        return self

    def recolour(self, mapping):
        """Swap material keys (a prototype reused in another paint)."""
        self.polys = [(p, mapping.get(m, m), s) for p, m, s in self.polys]
        return self

    def tris(self):
        return sum(len(p) - 2 for p, _m, _s in self.polys)

    def bounds(self):
        allp = np.concatenate([p for p, _m, _s in self.polys])
        return allp.min(0), allp.max(0)

    # -- primitives ------------------------------------------------------------
    def cuboid(self, o, ex, ey, ez, mat, skip=(), smooth=False):
        """A parallelepiped: corner o, edge vectors ex, ey, ez. skip: any of
        'bottom', 'top', 'front' (-ey), 'back', 'left' (-ex), 'right'."""
        o, ex, ey, ez = (np.asarray(v, float) for v in (o, ex, ey, ez))

        def c(a, b, d):
            return o + a * ex + b * ey + d * ez
        faces = {
            "bottom": (c(0, 0, 0), c(0, 1, 0), c(1, 1, 0), c(1, 0, 0)),
            "top": (c(0, 0, 1), c(1, 0, 1), c(1, 1, 1), c(0, 1, 1)),
            "front": (c(0, 0, 0), c(1, 0, 0), c(1, 0, 1), c(0, 0, 1)),
            "back": (c(0, 1, 0), c(0, 1, 1), c(1, 1, 1), c(1, 1, 0)),
            "left": (c(0, 0, 0), c(0, 0, 1), c(0, 1, 1), c(0, 1, 0)),
            "right": (c(1, 0, 0), c(1, 1, 0), c(1, 1, 1), c(1, 0, 1)),
        }
        flip = np.dot(np.cross(ex, ey), ez) < 0
        for name, pts in faces.items():
            if name not in skip:
                self.poly(pts[::-1] if flip else pts, mat, smooth)
        return self

    def box(self, x0, y0, z0, x1, y1, z1, mat, skip=()):
        return self.cuboid((x0, y0, z0), (x1 - x0, 0, 0), (0, y1 - y0, 0), (0, 0, z1 - z0), mat, skip)

    def beam(self, a, b, w, h, mat, up=(0, 0, 1), skip=()):
        """A rectangular member from a to b (centre line), w across, h along `up`."""
        a, b, up = (np.asarray(v, float) for v in (a, b, up))
        d = b - a
        side = np.cross(d, up)
        n = np.linalg.norm(side)
        side = side / n if n > EPS else np.array([1.0, 0, 0])
        upp = np.cross(side, d / np.linalg.norm(d))
        o = a - side * w / 2 - upp * h / 2
        return self.cuboid(o, side * w, d, upp * h, mat, skip)

    def cylinder(self, p0, p1, r, mat, n=10, caps=True, smooth=True, r1=None):
        p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
        a = p1 - p0
        a /= np.linalg.norm(a)
        u = np.cross(a, (0, 0, 1) if abs(a[2]) < 0.9 else (1, 0, 0))
        u /= np.linalg.norm(u)
        v = np.cross(a, u)
        r1 = r if r1 is None else r1
        ang = [2 * math.pi * i / n for i in range(n)]
        b = [p0 + r * (math.cos(t) * u + math.sin(t) * v) for t in ang]
        t_ = [p1 + r1 * (math.cos(t) * u + math.sin(t) * v) for t in ang]
        for i in range(n):
            j = (i + 1) % n
            self.quad(b[i], b[j], t_[j], t_[i], mat, smooth)
        if caps:
            self.poly(t_, mat)
            self.poly(b[::-1], mat)
        return self

    def prism(self, outline_xz, y0, y1, mat, skip_ends=False, end_mat=None):
        """A polygon in the (x, z) plane (CCW with x right, z up) extruded from y0 to
        y1 (y0 < y1): its y0 face looks -Y."""
        pts = [np.array((x, 0.0, z)) for x, z in outline_xz]
        n = len(pts)
        if not skip_ends:
            self.poly([p + (0, y0, 0) for p in pts], end_mat or mat)
            self.poly([p + (0, y1, 0) for p in pts[::-1]], end_mat or mat)
        for i in range(n):
            a, b = pts[i], pts[(i + 1) % n]
            self.quad(a + (0, y0, 0), a + (0, y1, 0), b + (0, y1, 0), b + (0, y0, 0), mat)
        return self


def _area(pts):
    s = np.zeros(3)
    for i in range(1, len(pts) - 1):
        s += np.cross(pts[i] - pts[0], pts[i + 1] - pts[0])
    return np.linalg.norm(s) / 2


# ---------------------------------------------------------------------------
# Walls with openings
# ---------------------------------------------------------------------------

def _clip(poly, x0, z0, x1, z1):
    """Sutherland-Hodgman: a convex (u, z) polygon clipped to a rectangle."""
    def cut(pts, inside, inter):
        out = []
        for i in range(len(pts)):
            a, b = pts[i - 1], pts[i]
            ia, ib = inside(a), inside(b)
            if ib:
                if not ia:
                    out.append(inter(a, b))
                out.append(b)
            elif ia:
                out.append(inter(a, b))
        return out

    def lerp_u(a, b, u):
        t = (u - a[0]) / (b[0] - a[0])
        return (u, a[1] + (b[1] - a[1]) * t)

    def lerp_z(a, b, z):
        t = (z - a[1]) / (b[1] - a[1])
        return (a[0] + (b[0] - a[0]) * t, z)
    pts = list(poly)
    for inside, inter in ((lambda p: p[0] >= x0, lambda a, b: lerp_u(a, b, x0)),
                          (lambda p: p[0] <= x1, lambda a, b: lerp_u(a, b, x1)),
                          (lambda p: p[1] >= z0, lambda a, b: lerp_z(a, b, z0)),
                          (lambda p: p[1] <= z1, lambda a, b: lerp_z(a, b, z1))):
        if not pts:
            break
        pts = cut(pts, inside, inter)
    return pts


def panel(outline, openings, mat, y=0.0, part=None, extra_u=(), extra_z=()):
    """A flat panel in the plane y (wall-local), facing -Y: the convex outline (u, z)
    CCW, minus rectangular openings (u0, u1, z0, z1), gridded along the openings' edges.
    extra_u / extra_z add grid lines (a derelict wall's missing boards)."""
    part = part or Part()
    us = sorted({round(p[0], 6) for p in outline} | {round(v, 6) for o in openings for v in o[:2]}
                | {round(v, 6) for v in extra_u})
    zs = sorted({round(p[1], 6) for p in outline} | {round(v, 6) for o in openings for v in o[2:]}
                | {round(v, 6) for v in extra_z})
    for i in range(len(us) - 1):
        for j in range(len(zs) - 1):
            cu, cz = (us[i] + us[i + 1]) / 2, (zs[j] + zs[j + 1]) / 2
            if any(o[0] < cu < o[1] and o[2] < cz < o[3] for o in openings):
                continue
            pts = _clip(outline, us[i], zs[j], us[i + 1], zs[j + 1])
            if len(pts) >= 3:
                part.poly([(u, y, z) for u, z in pts], mat)
    return part


def reveals(openings, depth, mat, part=None, sill=True):
    """The four linings of each opening, from the wall plane (y 0) to y depth."""
    part = part or Part()
    d = depth
    for u0, u1, z0, z1 in openings:
        part.quad((u0, 0, z0), (u0, d, z0), (u0, d, z1), (u0, 0, z1), mat)          # +X
        part.quad((u1, 0, z0), (u1, 0, z1), (u1, d, z1), (u1, d, z0), mat)          # -X
        part.quad((u0, 0, z1), (u0, d, z1), (u1, d, z1), (u1, 0, z1), mat)          # head
        if sill:
            part.quad((u0, 0, z0), (u1, 0, z0), (u1, d, z0), (u0, d, z0), mat)      # sill
    return part


def wall(asm, p0, p1, outline, mat, openings=(), depth=0.2, reveal_mat=None, **kw):
    """A wall panel on the plan edge p0 -> p1 (outside on the right) into the
    assembly's body. outline: (u, z) CCW, u in metres from p0. Returns its frame."""
    M = wall_frame(p0, p1)
    p = panel(outline, openings, mat, **kw)
    reveals(openings, depth, reveal_mat or mat, p)
    asm.body.add(p, M)
    return M


def rect_outline(length, z0, z1):
    return [(0.0, z0), (length, z0), (length, z1), (0.0, z1)]


# ---------------------------------------------------------------------------
# Roofs
# ---------------------------------------------------------------------------

def profile_z(profile, y):
    """Height of a cross-section profile [(y, z), ...] (y increasing) at y."""
    ys = [p[0] for p in profile]
    zs = [p[1] for p in profile]
    return float(np.interp(y, ys, zs))


def sweep_roof(part, profile, x0, x1, thick, top, under, edge, holes=None, rake_mat=None):
    """A roof slab along X from x0 to x1: `profile` is the UNDERSIDE cross-section
    [(y, z), ...] (y increasing: front eave -> ridge(s) -> back eave); the top is `thick`
    higher (vertically). Per segment k, holes[k] = [(x0, x1, s0, s1)] (s = metres up the
    segment from its first point) leave the slab open, lined with `edge`. The ends
    (rakes) and the eave edges get `rake_mat` / `edge`."""
    rake_mat = rake_mat or edge
    t = thick
    for k in range(len(profile) - 1):
        (ya, za), (yb, zb) = profile[k], profile[k + 1]
        L = math.hypot(yb - ya, zb - za)
        hs = (holes or {}).get(k, [])

        def P(x, s, dz=0.0):
            f = s / L
            return np.array((x, ya + (yb - ya) * f, za + (zb - za) * f + dz))
        xs = sorted({x0, x1} | {h[0] for h in hs} | {h[1] for h in hs})
        ss = sorted({0.0, L} | {h[2] for h in hs} | {h[3] for h in hs})
        for i in range(len(xs) - 1):
            for j in range(len(ss) - 1):
                cx, cs = (xs[i] + xs[i + 1]) / 2, (ss[j] + ss[j + 1]) / 2
                if any(h[0] < cx < h[1] and h[2] < cs < h[3] for h in hs):
                    continue
                a, b = (xs[i], ss[j]), (xs[i + 1], ss[j + 1])
                part.quad(P(a[0], a[1], t), P(b[0], a[1], t), P(b[0], b[1], t), P(a[0], b[1], t), top)
                part.quad(P(a[0], a[1]), P(a[0], b[1]), P(b[0], b[1]), P(b[0], a[1]), under)
        for hx0, hx1, s0, s1 in hs:        # the hole's cut edges show the slab
            part.quad(P(hx0, s0), P(hx0, s0, t), P(hx0, s1, t), P(hx0, s1), edge)
            part.quad(P(hx1, s0), P(hx1, s1), P(hx1, s1, t), P(hx1, s0, t), edge)
            part.quad(P(hx0, s0), P(hx1, s0), P(hx1, s0, t), P(hx0, s0, t), edge)
            part.quad(P(hx0, s1), P(hx0, s1, t), P(hx1, s1, t), P(hx1, s1), edge)
    # the rakes (slab ends) and the eave edges
    lo = [(y, z) for y, z in profile]
    hi = [(y, z + t) for y, z in profile]
    ring = lo + hi[::-1]
    part.poly([(x0, y, z) for y, z in ring[::-1]], rake_mat)
    part.poly([(x1, y, z) for y, z in ring], rake_mat)
    (ya, za), (yb, zb) = profile[0], profile[-1]
    part.quad((x0, ya, za), (x1, ya, za), (x1, ya, za + t), (x0, ya, za + t), edge)
    part.quad((x0, yb, zb), (x0, yb, zb + t), (x1, yb, zb + t), (x1, yb, zb), edge)
    return part


def gable_profile(y0, y1, z_eave, pitch_deg, overhang):
    """Underside of a symmetric gable over walls y0..y1 whose tops are at z_eave."""
    tp = math.tan(math.radians(pitch_deg))
    ym = (y0 + y1) / 2
    zr = z_eave + (ym - y0) * tp
    return [(y0 - overhang, z_eave - overhang * tp), (ym, zr), (y1 + overhang, z_eave - overhang * tp)]


def gambrel_profile(y0, y1, z_eave, lower_deg, upper_deg, knee_frac, overhang):
    """Underside of a gambrel: steep lower slopes to the knees (knee_frac of the half
    span in from each wall), shallow upper slopes to the ridge."""
    half = (y1 - y0) / 2
    tl, tu = math.tan(math.radians(lower_deg)), math.tan(math.radians(upper_deg))
    run_l = half * knee_frac
    zk = z_eave + run_l * tl
    zr = zk + (half - run_l) * tu
    return [(y0 - overhang, z_eave - overhang * tl), (y0 + run_l, zk), ((y0 + y1) / 2, zr),
            (y1 - run_l, zk), (y1 + overhang, z_eave - overhang * tl)]


def hip_roof(part, x0, y0, x1, y1, z_eave, pitch_deg, overhang, thick, top, under, edge,
             cap=None, cap_w=0.22):
    """A hipped slab over walls x0..x1, y0..y1 (tops at z_eave); a pyramid if square.
    Returns the ridge height of the underside."""
    tp = math.tan(math.radians(pitch_deg))
    o = overhang
    X0, Y0, X1, Y1 = x0 - o, y0 - o, x1 + o, y1 + o
    ze = z_eave - o * tp
    W, D = X1 - X0, Y1 - Y0
    h = min(W, D) / 2
    zr = ze + h * tp
    if W >= D:
        ra, rb = (X0 + h, (Y0 + Y1) / 2), (X1 - h, (Y0 + Y1) / 2)
    else:
        ra, rb = ((X0 + X1) / 2, Y0 + h), ((X0 + X1) / 2, Y1 - h)
    corners = [(X0, Y0), (X1, Y0), (X1, Y1), (X0, Y1)]
    # planes: each eave edge with its ridge end(s)
    if W >= D:
        planes = [(0, 1, rb, ra), (1, 2, rb, None), (2, 3, ra, rb), (3, 0, ra, None)]
    else:
        planes = [(0, 1, ra, None), (1, 2, rb, ra), (2, 3, rb, None), (3, 0, ra, rb)]
    for dz, mat, flip in ((thick, top, False), (0.0, under, True)):
        for i, j, r1, r2 in planes:
            pts = [(*corners[i], ze + dz), (*corners[j], ze + dz), (*r1, zr + dz)]
            if r2 is not None and math.dist(r1, r2) > EPS:
                pts.append((*r2, zr + dz))
            part.poly(pts[::-1] if flip else pts, mat)
    for i in range(4):     # the eave edge all round
        a, b = corners[i], corners[(i + 1) % 4]
        part.quad((*a, ze), (*b, ze), (*b, ze + thick), (*a, ze + thick), edge)
    if cap:
        zc = thick + 0.02
        for c in corners:
            r = ra if math.dist(c, ra) < math.dist(c, rb) else rb
            part.beam((*c, ze + zc), (*r, zr + zc), cap_w, 0.08, cap)
        if math.dist(ra, rb) > EPS:
            part.beam((*ra, zr + zc), (*rb, zr + zc), cap_w, 0.1, cap)
    return zr


def flat_roof(part, x0, y0, x1, y1, z_top, fascia_h, lip, top, fascia, parapet=0.0, coping=0.2):
    """A flat slab: the deck (gravel etc.) at z_top, a fascia band fascia_h deep all
    round overhanging the walls by lip (its underside the soffit), and a low coping
    ring `parapet` high."""
    X0, Y0, X1, Y1 = x0 - lip, y0 - lip, x1 + lip, y1 + lip
    part.box(X0, Y0, z_top - fascia_h, X1, Y1, z_top, fascia, skip=("top",))
    part.quad((X0, Y0, z_top), (X1, Y0, z_top), (X1, Y1, z_top), (X0, Y1, z_top), top)
    if parapet > 0:
        c = coping
        for bx in ((X0, Y0, X1, Y0 + c), (X0, Y1 - c, X1, Y1), (X0, Y0 + c, X0 + c, Y1 - c),
                   (X1 - c, Y0 + c, X1, Y1 - c)):
            part.box(bx[0], bx[1], z_top, bx[2], bx[3], z_top + parapet, fascia, skip=("bottom",))
    return part


# ---------------------------------------------------------------------------
# Trim
# ---------------------------------------------------------------------------

def band(part, rect, z0, z1, out, mat, top=True, bottom=True):
    """A belt around the rectangle (x0, y0, x1, y1), projecting `out` past it, from z0
    to z1: plinths, cornices, kick plates, frieze boards."""
    x0, y0, x1, y1 = rect
    X0, Y0, X1, Y1 = x0 - out, y0 - out, x1 + out, y1 + out
    skip = () if (top and bottom) else (("top",) if not top else ("bottom",))
    if not top and not bottom:
        skip = ("top", "bottom")
    part.box(X0, Y0, z0, X1, Y1, z1, mat, skip=skip)
    return part


def band_run(part, p0, p1, z0, z1, out, mat, ext0=0.0, ext1=0.0):
    """A belt along one plan edge (outside on the right), extended past its ends."""
    M = wall_frame(p0, p1)
    L = math.dist(p0, p1)
    part.add(Part().box(-ext0, -out, z0, L + ext1, 0.0, z1, mat, skip=("back",)), M)
    return part


def corner_boards(part, rect, z0, z1, w, t, mat):
    """Corner boards: at each corner of the rectangle a board w wide on both faces,
    standing t proud of the wall."""
    x0, y0, x1, y1 = rect
    for cx, cy, sx, sy in ((x0, y0, 1, 1), (x1, y0, -1, 1), (x1, y1, -1, -1), (x0, y1, 1, -1)):
        xa, xb = sorted((cx - sx * t, cx + sx * w))
        ya, yb = sorted((cy - sy * t, cy))
        part.box(xa, ya, z0, xb, yb, z1, mat, skip=("bottom",))
        xa, xb = sorted((cx - sx * t, cx))
        ya, yb = sorted((cy - sy * t, cy + sy * w))
        part.box(xa, ya, z0, xb, yb, z1, mat, skip=("bottom",))
    return part


def gutter(part, p0, p1, z, out, mat, down=None, r=0.06):
    """A box gutter along an eave edge (plan p0 -> p1, outside on the right) hung at
    height z, `out` past the edge; downspouts at the plan points in `down`
    [(x, y, z_bottom)] (their tops meet the gutter)."""
    M = wall_frame(p0, p1)
    L = math.dist(p0, p1)
    g = Part()
    g.box(0.0, -out - 2 * r, z - r * 1.4, L, -out, z, mat, skip=("top",))
    g.quad((0, -out - 2 * r, z), (L, -out - 2 * r, z), (L, -out - 2 * r + 0.02, z), (0, -out - 2 * r + 0.02, z), mat)
    part.add(g, M)
    for x, y, zb in (down or ()):
        part.cylinder((x, y, zb), (x, y, z - r), 0.045, mat, n=8)
    return part


def chimney(part, x, y, w, d, z0, z1, mat, cap_mat, pots=1, pot_mat=None):
    part.box(x - w / 2, y - d / 2, z0, x + w / 2, y + d / 2, z1, mat, skip=("bottom",))
    part.box(x - w / 2 - 0.06, y - d / 2 - 0.06, z1 - 0.28, x + w / 2 + 0.06, y + d / 2 + 0.06, z1 - 0.14,
             cap_mat)
    part.box(x - w / 2 - 0.03, y - d / 2 - 0.03, z1, x + w / 2 + 0.03, y + d / 2 + 0.03, z1 + 0.07, cap_mat)
    for k in range(pots):
        px = x + (k - (pots - 1) / 2) * (w / max(pots, 1))
        part.cylinder((px, y, z1 + 0.07), (px, y, z1 + 0.45), 0.11, pot_mat or cap_mat, n=10, r1=0.09)
    return part


def steps(part, x0, x1, y_wall, z_top, n, tread, mat, cheek=None, cheek_w=0.3):
    """n steps down from z_top at the wall line (y_wall) out toward -Y, each `tread`
    deep. Cheek walls (stone) either side if `cheek`."""
    rise = z_top / n
    for k in range(n):
        z1 = z_top - k * rise
        y_out = y_wall - (k + 1) * tread
        part.box(x0, y_out, -0.2, x1, y_wall - k * tread, z1, mat, skip=("bottom",))
    if cheek:
        yo = y_wall - n * tread
        for cx0, cx1 in ((x0 - cheek_w, x0), (x1, x1 + cheek_w)):
            part.poly([(cx0, y_wall, -0.2), (cx0, yo, -0.2), (cx0, yo, rise * 1.2), (cx0, y_wall, z_top + rise)][::-1],
                      cheek)
            part.poly([(cx1, y_wall, -0.2), (cx1, yo, -0.2), (cx1, yo, rise * 1.2), (cx1, y_wall, z_top + rise)],
                      cheek)
            part.quad((cx0, yo, -0.2), (cx1, yo, -0.2), (cx1, yo, rise * 1.2), (cx0, yo, rise * 1.2), cheek)
            part.quad((cx0, yo, rise * 1.2), (cx1, yo, rise * 1.2), (cx1, y_wall, z_top + rise),
                      (cx0, y_wall, z_top + rise), cheek)
    return part


# ---------------------------------------------------------------------------
# Windows, doors and other wall prototypes (south-wall local: opening centred on x = 0,
# its bottom at z = 0, the wall plane y = 0, outside -Y, the reveal to y = +depth)
# ---------------------------------------------------------------------------

def window(w, h, depth, panes=(2, 2), sash="double", frame="trim_dark", glass="glass",
           casing=None, casing_w=0.11, sill=None, head=None, bar=0.035):
    """A window unit for an opening w x h: a frame in the reveal, one or two sashes with
    muntins (panes = columns, rows per sash), the glass, and on the wall face a casing
    (painted trim) or stone surround (`head` lintel + `sill`)."""
    p = Part()
    fy = depth - 0.07                     # the frame's outer face, set back in the reveal
    fw = 0.055                            # frame member width
    hw = w / 2
    # frame (jambs, head, sill board)
    p.box(-hw, fy, 0, -hw + fw, depth, h, frame, skip=("back",))
    p.box(hw - fw, fy, 0, hw, depth, h, frame, skip=("back",))
    p.box(-hw, fy, h - fw, hw, depth, h, frame, skip=("back",))
    p.box(-hw, fy - 0.02, 0, hw, depth, fw, frame, skip=("back",))
    gw, gh = w - 2 * fw, h - 2 * fw
    gx0, gz0 = -hw + fw, fw
    sashes = [(gz0, gz0 + gh)] if sash != "double" else [(gz0, gz0 + gh / 2 + bar / 2), (gz0 + gh / 2 - bar / 2, gz0 + gh)]
    gy = depth - 0.02
    for k, (sz0, sz1) in enumerate(sashes):
        sy0 = fy + 0.005 + (0.03 if (sash == "double" and k == 0) else 0.0)
        sb = 0.045
        # sash stiles and rails
        p.box(gx0, sy0, sz0, gx0 + sb, sy0 + 0.04, sz1, frame, skip=("back",))
        p.box(gx0 + gw - sb, sy0, sz0, gx0 + gw, sy0 + 0.04, sz1, frame, skip=("back",))
        p.box(gx0, sy0, sz0, gx0 + gw, sy0 + 0.04, sz0 + sb * (1.4 if k == 0 else 1.0), frame, skip=("back",))
        p.box(gx0, sy0, sz1 - sb, gx0 + gw, sy0 + 0.04, sz1, frame, skip=("back",))
        cols, rows = panes
        for c in range(1, cols):
            x = gx0 + gw * c / cols
            p.box(x - bar / 2, sy0 + 0.005, sz0, x + bar / 2, sy0 + 0.035, sz1, frame, skip=("back",))
        for r in range(1, rows):
            z = sz0 + (sz1 - sz0) * r / rows
            p.box(gx0, sy0 + 0.005, z - bar / 2, gx0 + gw, sy0 + 0.035, z + bar / 2, frame, skip=("back",))
    p.quad((gx0, gy, gz0), (gx0 + gw, gy, gz0), (gx0 + gw, gy, gz0 + gh), (gx0, gy, gz0 + gh), glass)
    if casing:
        cw, ct = casing_w, 0.025
        p.box(-hw - cw, -ct, -0.02, -hw, 0.0, h + cw, casing, skip=("back",))
        p.box(hw, -ct, -0.02, hw + cw, 0.0, h + cw, casing, skip=("back",))
        p.box(-hw - cw - 0.03, -ct - 0.02, h, hw + cw + 0.03, 0.0, h + cw + 0.05, casing, skip=("back",))
        p.box(-hw - cw - 0.02, -ct - 0.03, h + cw + 0.05, hw + cw + 0.05, 0.0, h + cw + 0.08, casing,
              skip=("back",))
    if sill:
        p.box(-hw - 0.08, -0.07, -0.07, hw + 0.08, fy, 0.0, sill, skip=("back",))
    if head:
        p.box(-hw - 0.12, -0.04, h, hw + 0.12, 0.0, h + 0.28, head, skip=("back",))
    return p


def door(w, h, depth, leaves=1, paint="paint:wood-warm", frame="trim_dark", casing=None, casing_w=0.12,
         glass_upper=False, glass="glass", transom=0.0, sidelights=0.0, open_deg=0.0, interior=None,
         knob="metal:earth-light", panels=True, sag_deg=0.0, missing=()):
    """A door unit for an opening (w + 2 sidelights) x (h + transom): frame, 1 or 2
    leaves with raised panels (or glazed upper half), knobs, a lit transom / sidelights,
    a threshold. open_deg swings the leaves inward (hinged at the jambs); `interior`
    = (depth, wall material, floor material) lines a lit vestibule behind an open door
    (its back wall is glass: the interior glow). sag_deg tips a leaf about its hinge
    (a derelict barn), `missing` drops leaves by index."""
    p = Part()
    W = w + 2 * sidelights
    H = h + transom
    fy, fw = depth - 0.1, 0.07
    hw = W / 2
    p.box(-hw, fy, 0, -hw + fw, depth, H, frame, skip=("back",))
    p.box(hw - fw, fy, 0, hw, depth, H, frame, skip=("back",))
    p.box(-hw, fy, H - fw, hw, depth, H, frame, skip=("back",))
    p.box(-hw, fy - 0.03, -0.02, hw, depth, 0.03, "stone_trim:stone-light", skip=("back",))   # threshold
    if sidelights > 0:
        for sx in (-1, 1):
            x0, x1 = (-hw + fw, -w / 2) if sx < 0 else (w / 2, hw - fw)
            p.box(x0 if sx > 0 else x1 - 0.05, fy, 0, x0 + 0.05 if sx > 0 else x1, depth, h, frame,
                  skip=("back",))
            p.box(x0, fy, 0, x1, depth, 0.5, frame, skip=("back",))
            p.quad((x0, fy + 0.04, 0.5), (x1, fy + 0.04, 0.5), (x1, fy + 0.04, h - 0.05), (x0, fy + 0.04, h - 0.05),
                   glass)
            for z in np.linspace(0.5, h, 4)[1:-1]:
                p.box(x0, fy + 0.01, z - 0.017, x1, fy + 0.035, z + 0.017, frame, skip=("back",))
    if transom > 0:
        p.box(-hw, fy, h - 0.03, hw, depth, h + 0.05, frame, skip=("back",))
        p.quad((-hw + fw, fy + 0.04, h + 0.05), (hw - fw, fy + 0.04, h + 0.05),
               (hw - fw, fy + 0.04, H - fw), (-hw + fw, fy + 0.04, H - fw), glass)
        n = max(2, int(round(W / 0.35)))
        for k in range(1, n):
            x = -hw + fw + (W - 2 * fw) * k / n
            p.box(x - 0.015, fy + 0.01, h + 0.05, x + 0.015, fy + 0.035, H - fw, frame, skip=("back",))
    # leaves
    lw = w / leaves
    lt = 0.05
    for k in range(leaves):
        if k in missing:
            continue
        leaf = Part()
        leaf.box(0, 0, 0.01, lw - 0.01, lt, h - 0.01, paint)
        if glass_upper:
            leaf.quad((0.12, -0.002, h * 0.55), (lw - 0.13, -0.002, h * 0.55), (lw - 0.13, -0.002, h - 0.13),
                      (0.12, -0.002, h - 0.13), glass)
        if panels:
            rows = ((0.12, h * 0.42), (h * 0.47, h - 0.12)) if not glass_upper else ((0.12, h * 0.48),)
            cols = 2 if lw > 0.75 else 1
            cw = (lw - 0.12 - 0.08 * (cols - 1)) / cols
            for z0, z1 in rows:
                for c in range(cols):
                    x0 = 0.06 + c * (cw + 0.08)
                    leaf.box(x0, -0.012, z0, x0 + cw, 0.0, z1, paint, skip=("back",))
        kx = lw - 0.09
        leaf.cylinder((kx, 0.0, h * 0.47), (kx, -0.07, h * 0.47), 0.028, knob, n=8)
        # hinge at the jamb: leaf 0 hinges on the left jamb, leaf 1 (of 2) on the right
        if leaves == 2 and k == 1:
            M_leaf = T(w / 2, fy + 0.02, 0) @ Rz(math.radians(-open_deg)) @ S(-1, 1, 1)
        else:
            M_leaf = T(-w / 2, fy + 0.02, 0) @ Rz(math.radians(open_deg))
        if sag_deg and k == 0:
            M_leaf = M_leaf @ T(0, 0, h) @ Ry(math.radians(sag_deg)) @ T(0, 0, -h)
        p.add(leaf, M_leaf)
    if interior:
        d, wmat, fmat = interior
        x0, x1 = -W / 2 + 0.02, W / 2 - 0.02
        y0, y1 = depth, depth + d
        p.quad((x0, y0, 0.0), (x1, y0, 0.0), (x1, y1, 0.0), (x0, y1, 0.0), fmat)       # floor, faces up
        p.quad((x0, y0, 0), (x0, y0, H + 0.3), (x0, y1, H + 0.3), (x0, y1, 0), wmat)  # faces +X
        p.quad((x1, y0, 0), (x1, y1, 0), (x1, y1, H + 0.3), (x1, y0, H + 0.3), wmat)  # faces -X
        p.quad((x0, y0, H + 0.3), (x1, y0, H + 0.3), (x1, y1, H + 0.3), (x0, y1, H + 0.3), wmat)
        # a lamp-lit wall seen past the jamb (the lit room reads from any angle)
        p.quad((x0 + 0.01, y1 - d * 0.6, 0.9), (x0 + 0.01, y1 - d * 0.6, H), (x0 + 0.01, y1, H), (x0 + 0.01, y1, 0.9), glass)
        p.quad((x1 - 0.01, y1 - d * 0.6, 0.9), (x1 - 0.01, y1, 0.9), (x1 - 0.01, y1, H), (x1 - 0.01, y1 - d * 0.6, H), glass)
        p.quad((x0, y1, 0), (x1, y1, 0), (x1, y1, H + 0.3), (x0, y1, H + 0.3), glass)  # the lit room
    if casing:
        cw, ct = casing_w, 0.03
        p.box(-hw - cw, -ct, 0, -hw, 0, H + cw, casing, skip=("back",))
        p.box(hw, -ct, 0, hw + cw, 0, H + cw, casing, skip=("back",))
        p.box(-hw - cw - 0.04, -ct - 0.03, H, hw + cw + 0.04, 0, H + cw + 0.07, casing, skip=("back",))
    return p


def overhead_door(w, h, depth, paint, frame="trim_dark", sections=4, lites=True, glass="interior_dark"):
    """A sectional overhead (garage / service bay) door filling a w x h opening: raised
    horizontal sections with a groove between, a row of small lites in the second
    section from the top, a steel jamb angle, a bottom seal."""
    p = Part()
    hw = w / 2
    fy = depth - 0.12
    p.box(-hw, fy, 0, -hw + 0.08, depth, h, frame, skip=("back",))
    p.box(hw - 0.08, fy, 0, hw, depth, h, frame, skip=("back",))
    p.box(-hw, fy, h - 0.1, hw, depth, h, frame, skip=("back",))
    sh = (h - 0.1) / sections
    x0, x1 = -hw + 0.08, hw - 0.08
    for k in range(sections):
        z0 = k * sh
        p.box(x0, fy + 0.02, z0 + 0.012, x1, fy + 0.06, z0 + sh - 0.012, paint, skip=("back",))
        if lites and k == sections - 2:
            n = max(2, int(round((x1 - x0) / 0.62)))
            lw = (x1 - x0) / n
            for i in range(n):
                a = x0 + i * lw + 0.09
                p.quad((a, fy + 0.018, z0 + sh * 0.28), (a + lw - 0.18, fy + 0.018, z0 + sh * 0.28),
                       (a + lw - 0.18, fy + 0.018, z0 + sh * 0.78), (a, fy + 0.018, z0 + sh * 0.78), glass)
        else:     # two raised panels per section run
            n = max(2, int(round((x1 - x0) / 1.2)))
            pw = (x1 - x0) / n
            for i in range(n):
                a = x0 + i * pw + 0.1
                p.box(a, fy + 0.005, z0 + sh * 0.2, a + pw - 0.2, fy + 0.02, z0 + sh * 0.8, paint, skip=("back",))
    p.box(x0, fy - 0.01, 0.0, x1, fy + 0.03, 0.05, "metal:ink-700", skip=("back",))
    p.box(-0.12, fy - 0.03, sh * 0.4, 0.12, fy + 0.02, sh * 0.48, "metal:stone-dark", skip=("back",))   # the handle
    return p


def boarded(w, h, rng, mat="plywood:earth-light", planks=None, nails="metal:stone-dark"):
    """Boards over an opening w x h (centred on x 0, bottom z 0), nailed to the wall face
    (outside -Y): one plywood sheet cut a little oversize, or `planks` = n rough planks
    across it; a batten or two tacked diagonally."""
    p = Part()
    o = 0.08
    if planks:
        z = -o
        while z < h + o - 1e-3:
            ph = min(h + 2 * o - (z + o), rng.uniform(0.18, 0.26))
            tilt = rng.uniform(-0.03, 0.03)
            p.beam((-w / 2 - o - rng.uniform(0, 0.1), -0.03, z + ph / 2), (w / 2 + o + rng.uniform(0, 0.1), -0.03,
                   z + ph / 2 + tilt), ph - 0.015, 0.025, mat, up=(0, -1, 0))
            z += ph
    else:
        p.box(-w / 2 - o, -0.02, -o, w / 2 + o, 0.0, h + o, mat, skip=("back",))
    p.beam((-w / 2 + 0.05, -0.05, 0.1), (w / 2 - 0.05, -0.05, h - 0.1), 0.14, 0.025, mat, up=(0, -1, 0))
    return p


def wall_band(text, px, lit, depth=0.16, pad=0.28):
    """A wall band sign (WallBandSign.cs): an ink-900 band, flush 3x5 letters (cream by
    day; a LIT band's letters go lantern after dusk on the sign-lamp family), a steel
    trough lamp along its foot shining up at a lit band. Centred on x 0, the band's
    face at y = -depth (on a wall at y 0), its bottom at z 0. Returns (band Part,
    letters Part, lamp Part or None, (width, height))."""
    import pixelfont
    tw = pixelfont.measure(text, px)
    w, h = tw + 2 * pad, pixelfont.GLYPH_H * px + 2 * pad * 0.8
    band = Part().box(-w / 2, -depth, 0.0, w / 2, 0.0, h, "paint:ink-900", skip=("back",))
    band.box(-w / 2 - 0.03, -depth - 0.02, h - 0.05, w / 2 + 0.03, 0.0, h + 0.02, "metal:stone-dark", skip=("back",))
    key = "signlamp:cream/lantern" if lit else "paint:cream"
    letters = pixelfont.text(Part(), text, 0.0, h - pad * 0.8, px, 0.018, key, y=-depth)
    lamp = None
    if lit:
        lamp = Part()
        lw = w * 0.86
        for x in np.linspace(-lw / 2 + 0.2, lw / 2 - 0.2, 3):
            band.beam((x, -depth, -0.02), (x, -depth - 0.34, -0.12), 0.03, 0.03, "metal:ink-900")
        lamp.box(-lw / 2, -depth - 0.44, -0.2, lw / 2, -depth - 0.26, -0.1, "metal:ink-700", skip=("top",))
        lamp.quad((-lw / 2, -depth - 0.44, -0.1), (lw / 2, -depth - 0.44, -0.1), (lw / 2, -depth - 0.26, -0.1),
                  (-lw / 2, -depth - 0.26, -0.1), "signlamp:stone-pale/lantern")
    return band, letters, lamp, (w, h)


def tower_walls(part, cx, cy, r, n, z0, z1, mat, rot=0.0):
    """An n-sided tower (turret) shell around (cx, cy), circumradius r, z0..z1, faces
    out. Returns the corner list [(x, y)]."""
    pts = [(cx + r * math.cos(rot + 2 * math.pi * k / n), cy + r * math.sin(rot + 2 * math.pi * k / n))
           for k in range(n)]
    for k in range(n):
        a, b = pts[k], pts[(k + 1) % n]
        part.quad((*a, z0), (*b, z0), (*b, z1), (*a, z1), mat)
    return pts


def spire(part, cx, cy, r, n, z_eave, height, top, under, edge, overhang=0.35, thick=0.12, rot=0.0,
          flare=0.0, holes=()):
    """A polygonal cone roof (a turret's candle-snuffer) over an n-gon of circumradius r:
    eave at z_eave, apex `height` above it; `flare` kicks the lowest 30% outward (a bell
    cast). holes = face indices left open (a ruin's lost slates: the dark under-lath)."""
    R = r + overhang
    apex = (cx, cy, z_eave + height)
    ring = [(cx + R * math.cos(rot + 2 * math.pi * k / n), cy + R * math.sin(rot + 2 * math.pi * k / n))
            for k in range(n)]
    ze = z_eave - overhang * height / r * (1.0 - flare)
    for k in range(n):
        a, b = ring[k], ring[(k + 1) % n]
        if k in holes:
            m = [(a[0] + (apex[0] - a[0]) * 0.45, a[1] + (apex[1] - a[1]) * 0.45),
                 (b[0] + (apex[0] - b[0]) * 0.45, b[1] + (apex[1] - b[1]) * 0.45)]
            zm = ze + (apex[2] - ze) * 0.45
            part.poly([(*m[0], zm + thick), (*m[1], zm + thick), (apex[0], apex[1], apex[2] + thick)], top)
            part.poly([(*a, ze + thick), (*b, ze + thick), (*m[1], ze + (zm - ze) * 0.2 + thick),
                       (*m[0], ze + (zm - ze) * 0.2 + thick)], top)
            part.poly([(*m[0], zm), (*m[1], zm), (*b, ze), (*a, ze)], "interior_dark")
            continue
        part.poly([(*a, ze + thick), (*b, ze + thick), (apex[0], apex[1], apex[2] + thick)], top)
        part.poly([(apex[0], apex[1], apex[2]), (*b, ze), (*a, ze)], under)
        part.quad((*a, ze), (*b, ze), (*b, ze + thick), (*a, ze + thick), edge)
    return apex[2] + thick


def pointed_hood(part, cx, z_spring, w, rise, mat, y=0.0, t=0.1, proud=0.08, stops=0.25):
    """A gothic hood mould over an opening: two straight-ish segments meeting in a point
    over the opening (w wide, springing at z_spring, `rise` to the point), with short
    label stops dropping at the ends. On the wall plane y (outside -Y)."""
    hw = w / 2 + t
    a, apex, b = (cx - hw, y - proud / 2, z_spring), (cx, y - proud / 2, z_spring + rise), (cx + hw, y - proud / 2, z_spring)
    part.beam(a, apex, t, proud, mat, up=(0, -1, 0))
    part.beam(apex, b, t, proud, mat, up=(0, -1, 0))
    for x in (cx - hw, cx + hw):
        part.box(x - t / 2, y - proud, z_spring - stops, x + t / 2, y, z_spring + 0.02, mat, skip=("back",))
    return part


def louvre_vent(w, h, mat, frame, slats=6):
    """A louvred attic vent mounted on the wall face (no opening)."""
    p = Part()
    p.box(-w / 2 - 0.07, -0.04, -0.07, w / 2 + 0.07, 0.0, h + 0.07, frame, skip=("back",))
    for k in range(slats):
        z = (k + 0.5) * h / slats
        p.beam((-w / 2, -0.07, z), (w / 2, -0.07, z), 0.06, 0.012, mat, up=(0, -0.6, 0.8))
    p.quad((-w / 2, -0.041, 0), (w / 2, -0.041, 0), (w / 2, -0.041, h), (-w / 2, -0.041, h), "interior_dark")
    return p


# ---------------------------------------------------------------------------
# Placement and assembly
# ---------------------------------------------------------------------------

FACING_ROT = {"S": 0.0, "N": math.pi, "E": math.pi / 2, "W": -math.pi / 2}


def place(x0, y0, x1, y1, facing="S"):
    """A world footprint rect (metres, y0 < y1) + facing -> (M_local_to_world_xy,
    width, depth): local x runs along the front, y back from it."""
    W, D = x1 - x0, y1 - y0
    if facing == "S":
        return T(x0, y0) @ Rz(0.0), W, D
    if facing == "N":
        return T(x1, y1) @ Rz(math.pi), W, D
    if facing == "E":
        return T(x1, y0) @ Rz(math.pi / 2), D, W
    return T(x0, y1) @ Rz(-math.pi / 2), D, W


class Assembly:
    """One building: the joined body Part plus prototype instances (child objects)."""

    def __init__(self, name):
        self.name = name
        self.body = Part()
        self.protos = {}          # proto name -> Part
        self.inst = []            # (proto name, M (building local), props)

    def proto(self, name, build):
        if name not in self.protos:
            self.protos[name] = build()
        return name

    def place(self, proto, M, **props):
        self.inst.append((proto, M, props))

    def on_wall(self, proto, Mwall, u, z, **props):
        """Place a wall prototype at (u along the wall, z) in the wall frame Mwall."""
        self.place(proto, Mwall @ T(u, 0.0, z), **props)

    def tris(self):
        n = self.body.tris()
        for name, _M, _p in self.inst:
            n += self.protos[name].tris()
        return n


# ---------------------------------------------------------------------------
# Into Blender (bpy imported lazily: the geometry above runs without it)
# ---------------------------------------------------------------------------

MERGE_M = 2e-5          # coincident corners are welded, so smooth parts shade smooth
SHARP_DEG = 38.0        # ... and every edge sharper than this stays a hard edge


def face_uv(pts):
    """Metric UV of a face from its own plane: u horizontal along it, v up it."""
    n = np.zeros(3)
    for i in range(len(pts)):
        a, b = pts[i], pts[(i + 1) % len(pts)]
        n += np.cross(a, b)
    n /= (np.linalg.norm(n) or 1.0)
    h = math.hypot(n[0], n[1])
    t = np.array((-n[1] / h, n[0] / h, 0.0)) if h > 1e-4 else np.array((1.0, 0.0, 0.0))
    b = np.cross(n, t)
    return np.stack([pts @ t, pts @ b], 1)


def mesh(name, part, resolve):
    """A tagged mesh from a Part; resolve(key) -> bpy material."""
    import bmesh
    import bpy

    import scene
    keys = []
    for _p, m, _s in part.polys:
        if m not in keys:
            keys.append(m)
    verts, faces, uvs, mi, smooth = [], [], [], [], []
    k = 0
    for pts, m, sm in part.polys:
        verts.append(pts)
        faces.append(list(range(k, k + len(pts))))
        uvs.append(face_uv(pts))
        mi.append(keys.index(m))
        smooth.append(sm)
        k += len(pts)
    me = scene.tag(bpy.data.meshes.new(name))
    me.from_pydata(np.concatenate(verts).tolist(), [], faces)
    for key in keys:
        me.materials.append(resolve(key))
    me.polygons.foreach_set("material_index", np.asarray(mi, np.int32))
    me.polygons.foreach_set("use_smooth", np.asarray(smooth, bool))
    uv = me.uv_layers.new(name="UVm")
    uv.data.foreach_set("uv", np.concatenate(uvs).astype(np.float32).ravel())
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=MERGE_M)
    bm.to_mesh(me)
    bm.free()
    me.set_sharp_from_angle(angle=math.radians(SHARP_DEG))
    me.validate(verbose=False)
    me.update()
    return me


def _matrix(M):
    from mathutils import Matrix
    return Matrix([list(r) for r in M])


def realise(asm, col, M_world, resolve, prefix, **props):
    """The body object (named `prefix`) at M_world (4x4, building local -> world) with
    every prototype instance as a linked-duplicate child. Returns (body, children)."""
    import scene
    body = scene.new_object(prefix, mesh(f"{prefix}_body", asm.body, resolve), col, **props)
    body.matrix_world = _matrix(M_world)
    meshes = {name: mesh(f"Arch_{prefix}_{name}", p, resolve) for name, p in asm.protos.items()}
    kids = []
    for i, (name, M, p) in enumerate(asm.inst):
        ob = scene.new_object(f"{prefix}_{name}_{i:02d}", meshes[name], col, proto=name, **p)
        ob.parent = body
        ob.matrix_parent_inverse.identity()
        ob.matrix_basis = _matrix(M)
        kids.append(ob)
    return body, kids

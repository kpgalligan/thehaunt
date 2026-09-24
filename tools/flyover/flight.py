"""Phase 9: the flight -- the intro flyover's animated camera, derived from the guides.

The STORYBOARD below is the one place the flight is tuned: BEATS (name, start, hero:
the beat's still), CAM_KEYS (time, anchor, left offset m, height m above the ground),
AIM_KEYS (time, anchor, height m above the ground), DUSK_KEYS (time, flyover_dusk).
Anchors name the guides and the dump, never world coordinates:

  ("out_w", s) / ("out_e", s)   Guide_RoadOut_W / _E at arc s (m) from the town edge
  ("road", ref, dx)             the in-town road centreline (Guide_Road) at ref's x + dx
  ("at", ref, dx, dy)           a dump object's centre (building / prop / sign / light id)
  ("track", name, s)            Guide_Track_<name> at arc s from the road
  ("mansion", dx, dy)           the ruin's site (routes.mansion_site; aim z is over its roof)

A camera key's offset is to the LEFT of eastbound travel (north in town). Heights are
above Terrain.z_at. Space and time are separate (plan()): the path is a spline through
the camera keys, blurred so it never hairpins; the timing is the keys' segment speeds,
blurred so speed only eases, settling to rest after the last key (then HOLD_S still).
The view turns between aim targets by ANGLE, never through a point near the lens. The
heights are then lifted, smoothly, over a clearance FLOOR (Clearance: every tree's
crown by its kit's radial profile, every building / tall prop hull, every prop / light
mesh box, the terrain), and the whole flight is checked frame by frame against the
same obstacles, plus the view's turn rate (config.FLIGHT_MAX_TURN_DEG_S): asserted.
Roll: a gentle bank into the travel's turns.

plan(world, terrain, clearance) is pure numpy; bake() writes Blender: Cam_Flight (one
LINEAR key per frame), Guide_CamPath / Guide_CamAim (POLY curves of the per-frame
camera / aim points), beat timeline markers, the scene frame range, and the
flyover_dusk keys (look.py's time of the light: the neon's hard cut lands on the motel).
"""

import math

import numpy as np

import config

# ---------------------------------------------------------------------------
# STORYBOARD (seconds at config.FLIGHT_FPS)
# ---------------------------------------------------------------------------

# (marker, start, hero): the timeline marker at `start`; `hero` is the beat's still.
BEATS = (
    ("1 Road in", 0.0, 3.0),
    ("2 West Entry", 6.3, 7.8),
    ("3 Billie's", 16.0, 18.8),
    ("4 The Fork", 22.5, 27.0),
    ("5 Town", 30.5, 34.8),
    ("6 East Fork", 38.0, 46.4),
    ("7 East Entry", 48.5, 52.0),
    ("8 The valley", 58.0, 69.0),
)
HOLD_S = 3.0                # the camera rests this long after it settles (title / fade room)

# (t, anchor, left m, height m)
CAM_KEYS = (
    # 1 low over the road deep in the forest, travelling east round the road-out's bend
    (0.0, ("out_w", 150.0), 0.0, 12.0),
    (3.0, ("out_w", 80.0), 0.0, 10.0),
    (5.6, ("out_w", 22.0), -1.0, 8.5),
    # 2 the motel's pole sign at eye level on the left, the lot, the gas station across
    (8.4, ("road", "MotelSign", 3.0), -6.0, 7.0),
    (12.2, ("road", "GuestCar3", 2.0), -3.0, 8.5),
    (15.3, ("road", "Garage", 4.5), -1.0, 9.5),
    # 3 Billie's lit windows; slowing, a dip toward the pit's cover ahead on the right
    (18.2, ("road", "Bar", 0.0), -2.0, 8.0),
    (20.8, ("road", "Pit", -6.0), -4.5, 6.5),
    # 4 the fork: rising a touch to look up the farm road through the treeline
    (24.4, ("road", "FingerPost", -35.0), 0.0, 11.0),
    (27.2, ("road", "FingerPost", -3.0), -4.0, 19.0),
    (30.0, ("road", "FingerPost", 24.0), -3.0, 18.0),
    # 5 south of the plaza: the town hall, the well and its lamps
    (33.2, ("at", "Well", -40.0, -22.0), 0.0, 17.0),
    (36.4, ("at", "Well", -10.0, -24.0), 0.0, 20.0),
    # 6 rising over the trees: the drive-in off to the right, then a left turn up the
    # chained drive: the ruin's roofline over the canopy
    (40.4, ("road", "TownHall", 55.0), -26.0, 38.0),
    (43.3, ("at", "MansionChain", -10.0, -14.0), 0.0, 38.0),
    (45.8, ("at", "MansionChain", -5.0, 20.0), 0.0, 38.0),
    (47.4, ("at", "MansionChain", 6.0, 29.0), 0.0, 40.0),
    (49.0, ("at", "MansionChain", 25.0, 20.0), 0.0, 40.0),
    # 7 turning back to the road: police, hardware, salon; out along the road-out
    (50.8, ("at", "MansionChain", 46.0, -8.0), 0.0, 33.0),
    (52.6, ("road", "PoliceStation", -6.0), -4.0, 16.0),
    (54.8, ("road", "HardwareStore", 10.0), -3.0, 13.0),
    (57.5, ("out_e", 45.0), 4.0, 40.0),
    # 8 the reveal: up over the forest north of the road-out, turning back to the valley
    (61.0, ("out_e", 105.0), 22.0, 45.0),
    (65.0, ("out_e", 150.0), 48.0, 68.0),
)

# (t, anchor, height m)
AIM_KEYS = (
    (0.0, ("out_w", 50.0), 3.0),
    (3.0, ("road", "MotelSign", -5.0), 4.0),
    (5.6, ("at", "MotelSign", 0.0, 0.0), 5.5),
    (7.6, ("at", "MotelSign", 0.0, 0.0), 5.5),
    (9.8, ("at", "GuestCar3", 0.0, 0.0), 1.0),
    (12.2, ("at", "GasStation", 0.0, 0.0), 3.0),
    (13.6, ("at", "Garage", 0.0, 0.0), 3.0),
    (15.8, ("at", "Bar", 0.0, -3.0), 3.0),
    (19.0, ("at", "Pit", 10.0, 6.0), 0.5),
    (20.0, ("at", "Pit", 10.0, 6.0), 0.5),
    (22.0, ("road", "FingerPost", 5.0), 3.0),
    (26.4, ("track", "FarmRoad", 62.0), 4.0),
    (28.8, ("at", "Barn", 0.0, 0.0), 6.0),
    (32.4, ("at", "TownHall", 0.0, 0.0), 7.0),
    (35.8, ("at", "TownHall", 0.0, 0.0), 9.0),
    (39.4, ("at", "Screen", 0.0, 0.0), 5.0),
    (40.7, ("at", "Screen", 0.0, 0.0), 5.0),
    (44.7, ("mansion", 0.0, 0.0), -3.0),
    (47.2, ("mansion", 0.0, 0.0), -3.0),
    (50.4, ("road", "HardwareStore", 30.0), 4.0),
    (52.9, ("road", "HardwareStore", 45.0), 3.0),
    (55.0, ("out_e", 60.0), 4.0),
    (57.6, ("out_e", 190.0), 20.0),
    (61.4, ("at", "MansionChain", 210.0, 300.0), 40.0),
    (65.6, ("at", "TownHall", -40.0, 0.0), 0.0),
)

# (t, flyover_dusk): 17:43 light in the forest (0.86 of 16:00 -> 18:00); the neon cuts
# on as the motel's sign fills the left of frame (DUSK_GATE's 0.97-0.995 ramp); 18:00 on.
NEON_ON_S = 7.4
DUSK_KEYS = ((0.0, 0.86), (NEON_ON_S - 0.3, 0.968), (NEON_ON_S + 0.5, 1.0))


# ---------------------------------------------------------------------------
# Anchors
# ---------------------------------------------------------------------------

def _arc(pts):
    p = np.asarray(pts, float)
    return np.concatenate([[0.0], np.cumsum(np.hypot(*np.diff(p[:, :2], axis=0).T))])


def _along(pts, s):
    """(x, y, tx, ty) on a polyline at arc s (tangent in increasing s)."""
    p = np.asarray(pts, float)
    a = _arc(p)
    if not 0.0 <= s <= a[-1]:
        raise ValueError(f"arc {s} outside the guide (0..{a[-1]:.0f} m)")
    k = int(min(np.searchsorted(a, s, side="right") - 1, len(p) - 2))
    u = (s - a[k]) / max(a[k + 1] - a[k], 1e-9)
    d = p[k + 1, :2] - p[k, :2]
    d = d / max(np.hypot(*d), 1e-9)
    x, y = p[k, :2] + (p[k + 1, :2] - p[k, :2]) * u
    return float(x), float(y), float(d[0]), float(d[1])


def _ref(world, ref):
    """A dump object's centre (x, y) metres, by id (buildings, props, signs, lights)."""
    import propkit
    for m in world.maps.values():
        for key in ("buildings", "props"):
            for p in m.data[key]:
                if p.get("id") == ref:
                    return propkit.centre_m(m, p)
        for key in ("signs", "lights"):
            for p in m.data[key]:
                if p.get("id") == ref:
                    return propkit.px_m(m, p["px"]) if p.get("px") else propkit.tile_m(m, p["x"], p["y"])
    # the well and friends carry no id: a kind name picks the first of that kind
    for m in world.maps.values():
        for p in m.data["props"]:
            if p["kind"] == ref.lower():
                return propkit.centre_m(m, p)
    raise KeyError(f"flight anchor: no dump object '{ref}'")


def _mansion_top(world):
    for b in getattr(world, "buildings", None) or ():
        if b["name"].startswith("Mansion"):
            return b["height"]
    return 18.0


def anchor(world, terrain, spec, left=0.0):
    """(x, y) of an anchor, offset `left` metres to the left of eastbound travel, and
    the extra height it asks for (the mansion's roof)."""
    r = terrain.routes
    kind = spec[0]
    lx, ly, extra = 0.0, 1.0, 0.0            # the left normal (north in town)
    if kind in ("out_w", "out_e"):
        x, y, tx, ty = _along(r.out_w if kind == "out_w" else r.out_e, spec[1])
        if kind == "out_w":                  # the guide runs west: eastbound is backwards
            tx, ty = -tx, -ty
        lx, ly = -ty, tx
    elif kind == "road":
        x, y = _ref(world, spec[1])[0] + spec[2], r.road_y
        if not r.road_x[0] <= x <= r.road_x[1]:
            raise ValueError(f"flight anchor {spec}: x {x:.1f} is off the in-town road")
    elif kind == "at":
        x, y = _ref(world, spec[1])
        x, y = x + spec[2], y + spec[3]
    elif kind == "track":
        x, y, _tx, _ty = _along(r.tracks[spec[1]].pts, spec[2])
    elif kind == "mansion":
        x, y, _rot, _rect = r.mansion_site()
        x, y = x + spec[1], y + spec[2]
        extra = _mansion_top(world)
    else:
        raise ValueError(f"unknown flight anchor {spec}")
    return x + lx * left, y + ly * left, extra


# ---------------------------------------------------------------------------
# Splines
# ---------------------------------------------------------------------------

def spline(tk, yk, t, end_rest=True):
    """Cubic spline through (tk, yk) evaluated at t: natural start, zero slope at the
    last knot when end_rest (the camera settles), constant past it. yk: (n,) or (n, d)."""
    tk = np.asarray(tk, float)
    yk = np.asarray(yk, float)
    one = yk.ndim == 1
    Y = yk[:, None] if one else yk
    n = len(tk)
    h = np.diff(tk)
    if (h <= 0).any():
        raise ValueError("spline knots must increase")
    A = np.zeros((n, n))
    B = np.zeros((n, Y.shape[1]))
    A[0, 0] = 1.0                                    # natural: M0 = 0
    for i in range(1, n - 1):
        A[i, i - 1], A[i, i], A[i, i + 1] = h[i - 1], 2 * (h[i - 1] + h[i]), h[i]
        B[i] = 6 * ((Y[i + 1] - Y[i]) / h[i] - (Y[i] - Y[i - 1]) / h[i - 1])
    if end_rest:                                     # y'(tn) = 0
        A[-1, -2], A[-1, -1] = h[-1], 2 * h[-1]
        B[-1] = 6 * (0.0 - (Y[-1] - Y[-2]) / h[-1])
    else:
        A[-1, -1] = 1.0
    M = np.linalg.solve(A, B)
    t = np.asarray(t, float)
    tc = np.clip(t, tk[0], tk[-1])
    k = np.clip(np.searchsorted(tk, tc, side="right") - 1, 0, n - 2)
    a, b = tk[k + 1] - tc, tc - tk[k]
    hk = h[k][:, None]
    a, b = a[:, None], b[:, None]
    out = (M[k] * a ** 3 + M[k + 1] * b ** 3) / (6 * hk) \
        + (Y[k] / hk - M[k] * hk / 6) * a + (Y[k + 1] / hk - M[k + 1] * hk / 6) * b
    return out[:, 0] if one else out


def _blur(v, sigma):
    if sigma <= 0:
        return v.copy()
    r = int(math.ceil(3 * sigma))
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma) ** 2)
    k /= k.sum()
    return np.convolve(np.pad(v, r, mode="edge"), k, mode="valid")


def _dilate(v, r):
    p = np.pad(v, r, mode="edge")
    return np.max(np.stack([p[i:i + len(v)] for i in range(2 * r + 1)]), axis=0)


# ---------------------------------------------------------------------------
# Clearance
# ---------------------------------------------------------------------------

class Clearance:
    """The obstacles the lens must stay clear of, as a height FLOOR over any XY (the
    lowest the camera may pass there: over everything within its padded radius) and
    a point test. Trees: each instance's kit geometry (bodies + leaf cards) binned by
    radial distance (min / max height per 0.25 m ring, any turn), scaled; hulls:
    (x0, y0, cell, H) top-surface height fields (buildings, tall props); boxes: world
    AABBs (x0, y0, z0, x1, y1, z1) of every other mesh; the terrain."""

    STEP = 0.25
    CELL = 16.0

    def __init__(self, terrain, table=None, kit=None, hulls=(), boxes=(), near=None, clear=None):
        self.terrain = terrain
        near = config.FLIGHT_NEAR_M if near is None else near
        c = dict(config.FLIGHT_CLEAR_M, **(clear or {}))
        self.r_tree, self.r_solid, self.r_ground = near + c["tree"], near + c["solid"], near + c["ground"]
        self.hulls = list(hulls)
        self.boxes = np.asarray(boxes, float).reshape(-1, 6)
        self.trees = None
        if table is not None and kit is not None:
            self._index_trees(table, kit)

    def _index_trees(self, tb, kit):
        keep = ~np.isin(tb.group, ["tufts", "leaves"])
        self.tx, self.ty = tb.x[keep].astype(float), tb.y[keep].astype(float)
        self.tk = tb.kind[keep].astype(int)
        self.tsw = np.maximum(tb.scale[keep, 0], tb.scale[keep, 1]).astype(float)
        self.tsz = tb.scale[keep, 2].astype(float)
        self.tsink = tb.sink[keep].astype(float)
        self.tz = np.full(len(self.tx), np.nan)      # ground under each trunk, on demand
        nk = max(kit) + 1
        prof, reach = [], 0.0
        for k in range(nk):
            v = kit.get(k, np.zeros((1, 3)))
            rho = np.hypot(v[:, 0], v[:, 1])
            nb = int(np.ceil(rho.max() / self.STEP)) + 1
            b = np.minimum((rho / self.STEP).astype(int), nb - 1)
            lo, hi = np.full(nb, np.inf), np.full(nb, -np.inf)
            np.minimum.at(lo, b, v[:, 2])
            np.maximum.at(hi, b, v[:, 2])
            prof.append((lo, hi))
            reach = max(reach, float(rho.max()))
        nb = max(len(lo) for lo, _hi in prof)
        self.LO = np.full((nk, nb + 1), np.inf)          # per kit: min / max height per ring
        self.HI = np.full((nk, nb + 1), -np.inf)         # (padded: past the reach is empty)
        for k, (lo, hi) in enumerate(prof):
            self.LO[k, :len(lo)], self.HI[k, :len(hi)] = lo, hi
        self.reach = reach * float(self.tsw.max()) + self.r_tree
        self.span = int(np.ceil(2 * self.r_tree / self.tsw.min() / self.STEP)) + 2
        cell = np.floor(np.stack([self.tx, self.ty], 1) / self.CELL).astype(int)
        order = np.lexsort((cell[:, 1], cell[:, 0]))
        keys, first = np.unique(cell[order], axis=0, return_index=True)
        bounds = list(first) + [len(order)]
        self.grid = {tuple(k): order[bounds[i]:bounds[i + 1]] for i, k in enumerate(keys)}
        self.trees = True

    def _near_trees(self, x, y):
        n = int(math.ceil(self.reach / self.CELL))
        cx, cy = int(math.floor(x / self.CELL)), int(math.floor(y / self.CELL))
        parts = [self.grid[(i, j)] for i in range(cx - n, cx + n + 1) for j in range(cy - n, cy + n + 1)
                 if (i, j) in self.grid]
        return np.concatenate(parts) if parts else np.zeros(0, int)

    def _tree_spans(self, x, y):
        """[(z_lo, z_hi, what)] of the tree geometry within r_tree of (x, y)."""
        if not self.trees:
            return []
        rr = self.r_tree
        i = self._near_trees(x, y)
        if not len(i):
            return []
        d = np.hypot(self.tx[i] - x, self.ty[i] - y)
        sw = self.tsw[i]
        b0 = (np.maximum(d - rr, 0.0) / sw / self.STEP).astype(int)
        b1 = ((d + rr) / sw / self.STEP).astype(int)
        nb = self.LO.shape[1] - 1
        near = b0 < nb
        i, b0, b1 = i[near], b0[near], np.minimum(b1[near], nb - 1)
        if not len(i):
            return []
        J = b0[:, None] + np.arange(self.span)[None, :]
        ok = J <= b1[:, None]
        J = np.minimum(J, nb)
        k = self.tk[i][:, None]
        zl = np.where(ok, self.LO[k, J], np.inf).min(axis=1)
        zh = np.where(ok, self.HI[k, J], -np.inf).max(axis=1)
        hit = np.isfinite(zl)
        need = i[hit][np.isnan(self.tz[i[hit]])]
        if len(need):
            self.tz[need] = self.terrain.z_many(self.tx[need], self.ty[need]) - self.tsink[need]
        out = []
        for j, a, b in zip(i[hit], zl[hit], zh[hit]):
            sz = self.tsz[j]
            out.append((self.tz[j] + a * sz, self.tz[j] + b * sz, f"tree #{j} at ({self.tx[j]:.1f}, {self.ty[j]:.1f})"))
        return out

    def _hull_tops(self, x, y):
        rr, out = self.r_solid, []
        for n, (x0, y0, cell, H) in enumerate(self.hulls):
            ny, nx = H.shape
            i0, i1 = int((x - rr - x0) / cell), int((x + rr - x0) / cell)
            j0, j1 = int((y - rr - y0) / cell), int((y + rr - y0) / cell)
            if i1 < 0 or j1 < 0 or i0 >= nx or j0 >= ny:
                continue
            sub = H[max(j0, 0):min(j1, ny - 1) + 1, max(i0, 0):min(i1, nx - 1) + 1]
            if sub.size and np.isfinite(sub).any():
                out.append((float(sub[np.isfinite(sub)].max()), f"hull #{n}"))
        return out

    def _box_hits(self, x, y):
        b, rr = self.boxes, self.r_solid
        if not len(b):
            return b
        m = (x > b[:, 0] - rr) & (x < b[:, 3] + rr) & (y > b[:, 1] - rr) & (y < b[:, 4] + rr)
        return b[m]

    def ground(self, X, Y):
        """Highest terrain within the lens's ground radius of each (X, Y) (arrays)."""
        X, Y, r = np.asarray(X, float), np.asarray(Y, float), self.r_ground
        offs = ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1))
        z = self.terrain.z_many(np.concatenate([X + dx * r for dx, _ in offs]),
                                np.concatenate([Y + dy * r for _, dy in offs]))
        return z.reshape(len(offs), -1).max(axis=0)

    def floors(self, X, Y):
        """The lowest camera height at each (X, Y) that clears everything around it."""
        g = self.ground(X, Y) + self.r_ground
        return np.array([max(gi, self._floor_above(float(x), float(y))) for gi, x, y in zip(g, X, Y)])

    def _floor_above(self, x, y):
        f = -np.inf
        for _zl, zh, _w in self._tree_spans(x, y):
            f = max(f, zh + self.r_tree)
        for top, _w in self._hull_tops(x, y):
            f = max(f, top + self.r_solid)
        hits = self._box_hits(x, y)
        if len(hits):
            f = max(f, float(hits[:, 5].max()) + self.r_solid)
        return f

    def violations(self, x, y, z, g):
        """What the padded lens at (x, y, z) would touch, g = ground(x, y): [text]."""
        bad = []
        if z < g + self.r_ground:
            bad.append(f"terrain {z - g:.2f} m below the camera")
        for zl, zh, w in self._tree_spans(x, y):
            if zl - self.r_tree < z < zh + self.r_tree:
                bad.append(f"{w} crown {zl:.1f}..{zh:.1f} m")
        for top, w in self._hull_tops(x, y):
            if z < top + self.r_solid:
                bad.append(f"{w} top {top:.1f} m")
        for b in self._box_hits(x, y):
            if b[2] - self.r_solid < z < b[5] + self.r_solid:
                bad.append(f"box z {b[2]:.1f}..{b[5]:.1f} at ({b[0]:.1f}, {b[1]:.1f})")
        return bad


# ---------------------------------------------------------------------------
# The plan (pure numpy)
# ---------------------------------------------------------------------------

class Flight:
    """Per frame: t, pos (n, 3), aim (n, 3), R (n, 3, 3) camera-to-world (columns: right,
    up, back), bank (deg); frames are 1-based (frame f = t (f - 1) / fps)."""

    def __init__(self, **kw):
        self.__dict__.update(kw)

    @property
    def n(self):
        return len(self.t)

    def frame(self, t):
        return int(round(t * self.fps)) + 1


def _look(pos, aim, bank_deg):
    f = aim - pos
    f /= np.linalg.norm(f, axis=1)[:, None]
    up_w = np.array([0.0, 0.0, 1.0])
    right = np.cross(f, up_w)
    right /= np.linalg.norm(right, axis=1)[:, None]
    up = np.cross(right, f)
    b = np.radians(bank_deg)[:, None]
    r2 = right * np.cos(b) + up * np.sin(b)      # b > 0: the left side dips (a left turn)
    u2 = up * np.cos(b) - right * np.sin(b)
    return np.stack([r2, u2, -f], axis=2)        # columns


def _path(P):
    """The camera's path in space: a chord-length cubic spline through the key points,
    sampled every ~0.25 m, blurred by config.FLIGHT_PATH_SMOOTH_M (no hairpins). Returns (points (m, 3), arc length (m,), arc at each key)."""
    P = np.asarray(P, float)
    c = np.concatenate([[0.0], np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))])
    u = np.linspace(0.0, c[-1], int(c[-1] / 0.25) + 2)
    pts = spline(c, P, u, end_rest=False)
    sig = config.FLIGHT_PATH_SMOOTH_M / (u[1] - u[0])   # round the tight corners off
    pts = np.stack([_blur(pts[:, i], sig) for i in range(3)], axis=1)
    s = np.concatenate([[0.0], np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))])
    return pts, s, np.interp(c, u, s)


def _wrap(a):
    return (a + np.pi) % (2 * np.pi) - np.pi


def plan(world, terrain, clearance=None, fps=None):
    """The flight, frame by frame. Space and time are separate: the path is a smooth
    curve through the camera keys; the timing is the keys' segment speeds, blurred
    (config.FLIGHT_SPEED_SMOOTH_S) so speed only ever eases, integrated along the arc and
    settling to rest after the last key (then HOLD_S of stillness). The view turns
    between aim targets by ANGLE (each target's yaw / pitch from where the camera is
    now: a held target stays locked as the camera passes), linear in key time, then
    blurred (config.FLIGHT_AIM_SMOOTH_S): never through a point near the lens."""
    fps = fps or config.FLIGHT_FPS
    ck = [(t, *anchor(world, terrain, a, left), h) for t, a, left, h in CAM_KEYS]
    ak = [(t, *anchor(world, terrain, a), h) for t, a, h in AIM_KEYS]
    tk = np.array([k[0] for k in ck])
    sig_v = config.FLIGHT_SPEED_SMOOTH_S
    t_rest = tk[-1] + 2.5 * sig_v
    n = int(round((t_rest + HOLD_S) * fps)) + 1
    t = np.arange(n) / fps
    # space
    cxyz = np.array([(x, y, terrain.z_at(x, y) + h) for _t, x, y, _e, h in ck])
    pts, arc, sk = _path(cxyz)
    # time: blurred segment speeds, integrated, scaled to land exactly on the last key
    vseg = np.diff(sk) / np.diff(tk)
    seg = np.clip(np.searchsorted(tk, t, side="right") - 1, 0, len(vseg) - 1)
    vraw = np.where(t < tk[-1], vseg[seg], 0.0)
    v = _blur(vraw, sig_v * fps)
    v[t >= t_rest] = 0.0
    s = np.concatenate([[0.0], np.cumsum((v[1:] + v[:-1]) / 2) / fps])
    s *= arc[-1] / s[-1]
    pos = np.stack([np.interp(s, arc, pts[:, i]) for i in range(3)], axis=1)
    lift = np.zeros(n)
    floor = None
    if clearance is not None:
        # lift the heights, smoothly, over the clearance floor (independent of z)
        floor = clearance.floors(pos[:, 0], pos[:, 1])
        w = max(1, int(config.FLIGHT_FLOOR_SMOOTH_S * fps / 2))
        for _ in range(8):
            deficit = np.maximum(0.0, floor - (pos[:, 2] + lift))
            if deficit.max() <= 1e-4:
                break
            lift += _blur(_dilate(deficit + 0.05, w), w / 2.5)
        pos[:, 2] += lift
    # the view: angles to each target from the camera, blended by key time
    T = np.array([(x, y, terrain.z_at(x, y) + e + h) for _t, x, y, e, h in ak])
    ta = np.array([k[0] for k in ak])
    d = T[None, :, :] - pos[:, None, :]
    yaw_k = np.arctan2(d[..., 1], d[..., 0])
    pitch_k = np.arctan2(d[..., 2], np.hypot(d[..., 0], d[..., 1]))
    dist_k = np.linalg.norm(d, axis=2)
    tau = np.interp(t, ta, np.arange(len(ta), dtype=float))
    k0 = np.minimum(np.floor(tau).astype(int), len(ta) - 2)
    u = tau - k0
    i = np.arange(n)
    yaw = yaw_k[i, k0] + _wrap(yaw_k[i, k0 + 1] - yaw_k[i, k0]) * u
    pitch = pitch_k[i, k0] * (1 - u) + pitch_k[i, k0 + 1] * u
    dist = dist_k[i, k0] * (1 - u) + dist_k[i, k0 + 1] * u
    sa = config.FLIGHT_AIM_SMOOTH_S * fps
    yaw = _blur(np.unwrap(yaw), sa)
    pitch = _blur(pitch, sa)
    dist = _blur(dist, sa)
    fwd = np.stack([np.cos(pitch) * np.cos(yaw), np.cos(pitch) * np.sin(yaw), np.sin(pitch)], axis=1)
    aim = pos + fwd * dist[:, None]
    # bank into the travel's turns: lateral acceleration from the horizontal heading rate
    vel = np.gradient(pos[:, :2], 1.0 / fps, axis=0)
    speed = np.hypot(vel[:, 0], vel[:, 1])
    head = np.unwrap(np.arctan2(vel[:, 1], vel[:, 0]))
    omega = np.gradient(head, 1.0 / fps) * (speed > 0.5)
    gain, cap = config.FLIGHT_BANK
    bank = gain * np.degrees(np.arctan(speed * omega / 9.81))
    bank = _blur(np.clip(bank * np.cos(yaw - head), -cap, cap), fps * 0.5)
    R = _look(pos, aim, bank)
    return Flight(fps=fps, t=t, pos=pos, aim=aim, R=R, bank=bank, lift=lift, floor=floor, speed=speed,
                  cam_keys=ck, aim_keys=ak, key_arc=sk, s=s, last_key=float(tk[-1]), hold_from=float(t_rest))


def stats(fl):
    """Smoothness numbers: speeds (m/s), acceleration (m/s^2), the view's turn rate and
    its change (deg/s, deg/s^2), height above ground range."""
    dt = 1.0 / fl.fps
    v = np.gradient(fl.pos, dt, axis=0)
    a = np.gradient(v, dt, axis=0)
    f = -fl.R[:, :, 2]
    cosang = np.clip(np.sum(f[1:] * f[:-1], axis=1), -1.0, 1.0)
    turn = np.degrees(np.arccos(cosang)) / dt
    turn_acc = np.abs(np.diff(_blur(turn, 2))) / dt
    moving = fl.t[:-1] < fl.last_key - 1.0
    return dict(speed_max=float(np.linalg.norm(v, axis=1).max()),
                speed_min_moving=float(np.linalg.norm(v, axis=1)[:-1][moving].min()),
                accel_max=float(np.linalg.norm(a, axis=1).max()),
                turn_max=float(turn.max()), turn_acc_max=float(turn_acc.max()),
                lift_max=float(fl.lift.max()))


def check(fl, clearance):
    """Every frame's padded lens against the obstacles, and the turn rate; raises."""
    bad = []
    g = clearance.ground(fl.pos[:, 0], fl.pos[:, 1])
    for i, ((x, y, z), gi) in enumerate(zip(fl.pos, g)):
        for w in clearance.violations(float(x), float(y), float(z), float(gi)):
            bad.append(f"frame {i + 1}: {w}")
    st = stats(fl)
    if st["turn_max"] > config.FLIGHT_MAX_TURN_DEG_S:
        bad.append(f"the view turns {st['turn_max']:.1f} deg/s (max {config.FLIGHT_MAX_TURN_DEG_S})")
    if st["speed_min_moving"] < 0.5:
        bad.append(f"the camera stalls mid-flight ({st['speed_min_moving']:.2f} m/s)")
    if bad:
        raise AssertionError(f"flight clearance: {len(bad)} problems: " + "; ".join(bad[:12]))
    return st


# ---------------------------------------------------------------------------
# Blender
# ---------------------------------------------------------------------------

def scene_boxes(cols):
    """World AABBs of every rendered mesh object under the collections."""
    import bpy
    bpy.context.view_layer.update()
    out = []
    seen = set()
    for col in cols:
        if col is None:
            continue
        for ob in col.all_objects:
            if ob.type != "MESH" or ob.hide_render or ob.name in seen:
                continue
            seen.add(ob.name)
            mw = ob.matrix_world
            c = np.array([tuple(mw @ __import__("mathutils").Vector(v)) for v in ob.bound_box])
            out.append((*c.min(0), *c.max(0)))
    return np.array(out, float).reshape(-1, 6)


def _curve(name, pts, col, **props):
    import bpy
    import scene
    cu = scene.tag(bpy.data.curves.new(name, "CURVE"))
    cu.dimensions = "3D"
    sp = cu.splines.new("POLY")
    sp.points.add(len(pts) - 1)
    co = np.concatenate([np.asarray(pts, float), np.ones((len(pts), 1))], axis=1)
    sp.points.foreach_set("co", co.ravel())
    ob = scene.new_object(name, cu, col, **props)
    ob.hide_render = True
    return ob


def _fcurves(idb, name, keys):
    """A tagged action on idb with one LINEAR fcurve per (data_path, index, frames,
    values). Returns the action."""
    import bpy
    from bpy_extras import anim_utils
    import scene
    ad = idb.animation_data or idb.animation_data_create()
    if ad.action is not None and not scene.is_ours(ad.action):
        raise RuntimeError(f"{idb.name} already carries a foreign action '{ad.action.name}'")
    act = scene.tag(bpy.data.actions.new(name))
    slot = act.slots.new(id_type=idb.id_type, name=idb.name)
    ad.action = act
    ad.action_slot = slot
    bag = anim_utils.action_ensure_channelbag_for_slot(act, slot)
    lin = bpy.types.Keyframe.bl_rna.properties["interpolation"].enum_items["LINEAR"].value
    for path, index, frames, values in keys:
        fc = bag.fcurves.new(path, index=index)
        kp = fc.keyframe_points
        kp.add(len(frames))
        co = np.empty(2 * len(frames))
        co[0::2], co[1::2] = frames, values
        kp.foreach_set("co", co)
        kp.foreach_set("interpolation", [lin] * len(frames))
        fc.update()
    return act


def dusk_fcurve(sc=None):
    """The scene's flyover_dusk fcurve (the flight's time of the light), or None."""
    import bpy
    sc = sc or bpy.context.scene
    ad = sc.animation_data
    if ad is None or ad.action is None:
        return None
    from bpy_extras import anim_utils
    bag = anim_utils.action_get_channelbag_for_slot(ad.action, ad.action_slot)
    return None if bag is None else bag.fcurves.find(f'["{config.DUSK_PROP}"]')


def bake(fl, cams_col, guides_col):
    import bpy
    from mathutils import Matrix
    import scene
    sc = bpy.context.scene
    cam = scene.tag(bpy.data.cameras.new(config.FLIGHT_CAMERA))
    cam.lens = config.FLIGHT_LENS_MM
    cam.sensor_width = 36.0
    cam.sensor_fit = "AUTO"
    cam.clip_start = config.FLIGHT_NEAR_M * 0.6
    cam.clip_end = config.CLIP_END_M
    ob = scene.new_object(config.FLIGHT_CAMERA, cam, cams_col, kind="flight")
    ob.rotation_mode = "QUATERNION"
    frames = np.arange(1, fl.n + 1, dtype=float)
    q = np.empty((fl.n, 4))
    prev = None
    for i, R in enumerate(fl.R):
        qi = Matrix(R.tolist()).to_quaternion()
        if prev is not None and qi.dot(prev) < 0:
            qi.negate()
        q[i] = tuple(qi)
        prev = qi
    keys = [("location", i, frames, fl.pos[:, i]) for i in range(3)]
    keys += [("rotation_quaternion", i, frames, q[:, i]) for i in range(4)]
    _fcurves(ob, "Cam_Flight_Action", keys)
    ob["beats"] = str([(b[0], fl.frame(b[1]), fl.frame(b[2])) for b in BEATS])
    _curve("Guide_CamPath", fl.pos, guides_col, kind="cam_path", fps=fl.fps, frames=fl.n)
    _curve("Guide_CamAim", fl.aim, guides_col, kind="cam_aim")
    # the time of the light
    dk = np.array(DUSK_KEYS, float)
    _fcurves(sc, "Flyover_SceneAction",
             [(f'["{config.DUSK_PROP}"]', 0, [fl.frame(t) for t in dk[:, 0]], dk[:, 1])])
    # beat markers (a rebuild replaces its own)
    for name in list(sc.get("flyover_markers", [])):
        m = sc.timeline_markers.get(name)
        if m is not None:
            sc.timeline_markers.remove(m)
    for name, start, _hero in BEATS:
        sc.timeline_markers.new(name, frame=fl.frame(start))
    sc["flyover_markers"] = [b[0] for b in BEATS]
    sc.frame_start, sc.frame_end = 1, fl.n
    sc.render.fps = fl.fps
    sc.camera = ob
    sc.frame_set(1)
    return ob


def hero_frames(fl):
    return [(name, fl.frame(hero)) for name, _start, hero in BEATS]


def build(world, terrain, cams_col, guides_col, kit_vertices, solid_cols):
    """Plan, check and bake the flight. kit_vertices: trees.kit_vertices(); solid_cols:
    the collections whose meshes are obstacles (buildings, props, lights)."""
    import time
    t0 = time.time()
    hulls = [b["hull"] for b in world.buildings] + [p["hull"] for p in world.props if "hull" in p]
    cl = Clearance(terrain, world.forest.table, kit_vertices, hulls, scene_boxes(solid_cols))
    fl = plan(world, terrain, cl)
    st = check(fl, cl)
    ob = bake(fl, cams_col, guides_col)
    print(f"flyover: flight {fl.n} frames ({fl.n / fl.fps:.1f} s at {fl.fps} fps), "
          f"{len(cl.boxes)} boxes + {len(hulls)} hulls + {len(cl.tx)} trees clear; "
          + ", ".join(f"{k} {v:.1f}" for k, v in st.items()) + f" in {time.time() - t0:.1f}s")
    fl.stats = st
    fl.clearance = cl
    return fl, ob

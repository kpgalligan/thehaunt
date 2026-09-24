"""Phase 9: the flights -- the flyover's animated cameras, derived from the guides.

The STORYBOARDS live in `flights/` (the registry: flights/__init__.py documents a
sequence module); this module is the machinery every sequence shares. Anchors name the
guides and the dump, never world coordinates:

  ("out_w", s) / ("out_e", s)   Guide_RoadOut_W / _E at arc s (m) from the town edge
  ("road", ref, dx)             the in-town road centreline (Guide_Road) at ref's x + dx
  ("at", ref, dx, dy)           a dump object's centre (building / prop / sign / light id)
  ("track", name, s)            Guide_Track_<name> at arc s from the road
  ("mansion", dx, dy)           the ruin's site (routes.mansion_site; aim z is over its roof)
  ("framed", ref, side, frac)   the spot on the `side` (N/S/E/W) of a BUILT building,
                                square to it, where it spans `frac` of the frame's width
                                at the sequence's lens (its real extents: the hull)

A camera key's offset is to the LEFT of eastbound travel (north in town). Heights: see
flights/__init__.py (m above Terrain.z_at, or `canopy`). Space and time are separate
(plan()): the path is a spline through the camera keys, blurred so it never hairpins; the
timing is the keys' segment speeds, blurred so speed only eases, settling to rest after
the last key (then HOLD_S still); before the first key's time the camera holds still.
The view turns between aim targets by ANGLE, never through a point near the lens. The
heights are then lifted, smoothly, over a clearance FLOOR (Clearance: every tree's crown
by its kit's radial profile, every building / tall prop hull, every prop / light mesh
box, the terrain), and the whole flight is checked frame by frame against the same
obstacles, plus the view's turn rate: asserted. Roll: a bank into the travel's turns.

Scenes: the PRIMARY flight lives in the host scene (the build's own); every other flight
in its own scene (a Scene.copy() of the host: the same collections, render settings and
look, its own camera, frame range, markers and flyover_dusk action; look.sync_scene).
Drivers read the ACTIVE scene (look.py's CONTEXT_PROP variables) and the materials the
rendered scene's properties, so each scene's dusk keys light its own film.

Live iteration (no world rebuild): the build caches what the plan needs (terrain, the
forest table, kit profiles, hulls, solid boxes) next to the .blend
(<blend>.flight_cache.pkl); `rebake(name)` re-reads the sequence file and re-plans,
checks and bakes that one camera in seconds; `switch(name)` puts the viewport on it.
"""

import math
import os
import pickle
import sys
import time
import types

import numpy as np

import config

# ---------------------------------------------------------------------------
# The registry
# ---------------------------------------------------------------------------

_DEFAULTS = ("LENS_MM", "CLEAR_M", "PATH_SMOOTH_M", "SPEED_SMOOTH_S", "AIM_SMOOTH_S", "FLOOR_SMOOTH_S",
             "BANK", "MAX_TURN_DEG_S")


def _flights():
    import importlib
    import flights
    return importlib.reload(flights)


def names():
    return tuple(_flights().NAMES)


def primary():
    return _flights().PRIMARY


class Seq:
    """A sequence module + its defaults (config.FLIGHT_*), by attribute."""

    def __init__(self, mod):
        self.mod = mod
        self.name = mod.NAME
        d = {k: getattr(config, "FLIGHT_" + k) for k in _DEFAULTS}
        d.update(BANK_KEYS=None, PIN_ENDS=False, MIN_SPEED_MS=0.5, START_EASE_S=2.0, EXACT_SOLIDS=False, HOLD_S=3.0,
                 DUSK_KEYS=((0.0, 1.0),), SCENE=None)
        for k, v in d.items():
            setattr(self, k, getattr(mod, k, v))
        for k in ("BEATS", "CAM_KEYS", "AIM_KEYS", "CAMERA"):
            setattr(self, k, getattr(mod, k))
        self.clear = dict(config.FLIGHT_CLEAR_M, **self.CLEAR_M)

    @property
    def is_primary(self):
        return self.name == primary()

    @property
    def scene_name(self):
        return self.SCENE or self.name


def seq(name):
    return Seq(_flights().load(name))


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


def _hull_cells(world, ref):
    """A built building's real footprint (roof + eaves included): the (x, y) corners of
    every covered cell of its hull."""
    for b in getattr(world, "buildings", None) or ():
        if b["name"].endswith("_" + ref) and "hull" in b:
            x0, y0, cell, H = b["hull"]
            j, i = np.nonzero(np.isfinite(np.asarray(H)))
            xs, ys = x0 + i * cell, y0 + j * cell
            return np.concatenate([np.stack([xs + dx * cell, ys + dy * cell], 1) for dx in (0, 1) for dy in (0, 1)])
    raise KeyError(f"flight anchor: no built building '{ref}' (framed anchors need the buildings)")


def _framed(world, ref, side, frac, lens):
    """The spot square to a building's `side`, on its centre line, from which its real
    footprint spans `frac` of the frame's width (every corner's own depth: a porch or
    eave nearer the lens counts as it would on screen)."""
    P = _hull_cells(world, ref)
    ang = {"S": -math.pi / 2, "N": math.pi / 2, "W": math.pi, "E": 0.0}[side]
    ox, oy = math.cos(ang), math.sin(ang)                  # outward, from the building
    rx, ry = -oy, ox                                       # across the view
    c = P.mean(axis=0)
    u = (P - c) @ np.array([rx, ry])                       # across
    v = (P - c) @ np.array([ox, oy])                       # toward the lens
    uc = (u.min() + u.max()) / 2
    tan = 18.0 / lens                                      # half the 36 mm sensor's width
    lo, hi = v.max() + 0.5, v.max() + 500.0
    for _ in range(60):                                    # the span shrinks with distance
        d = (lo + hi) / 2
        s = (u - uc) / (d - v)
        lo, hi = (d, hi) if (s.max() - s.min()) / (2 * tan) > frac else (lo, d)
    d = (lo + hi) / 2
    return float(c[0] + ox * d + rx * uc), float(c[1] + oy * d + ry * uc)


def anchor(world, terrain, spec, left=0.0, lens=None):
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
    elif kind == "framed":
        x, y = _framed(world, spec[1], spec[2], spec[3], lens or config.FLIGHT_LENS_MM)
    else:
        raise ValueError(f"unknown flight anchor {spec}")
    return x + lx * left, y + ly * left, extra


def key_z(terrain, clearance, x, y, h):
    """A key's height spec -> world z (see flights/__init__.py)."""
    if isinstance(h, tuple):
        if h[0] != "canopy":
            raise ValueError(f"unknown height {h}")
        if clearance is None:
            raise ValueError("a canopy height needs the clearance (the forest)")
        return max(terrain.z_at(x, y), clearance.canopy(x, y, h[2])) + h[1]
    return terrain.z_at(x, y) + h


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


def _blur(v, sigma, pin=False):
    """Gaussian blur; pin keeps the two end values exactly (odd reflection)."""
    if sigma <= 0:
        return v.copy()
    r = int(math.ceil(3 * sigma))
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma) ** 2)
    k /= k.sum()
    if pin:
        r = min(r, len(v) - 1)
        k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma) ** 2)
        k /= k.sum()
        return np.convolve(np.pad(v, r, mode="reflect", reflect_type="odd"), k, mode="valid")
    return np.convolve(np.pad(v, r, mode="edge"), k, mode="valid")


def _dilate(v, r):
    p = np.pad(v, r, mode="edge")
    return np.max(np.stack([p[i:i + len(v)] for i in range(2 * r + 1)]), axis=0)


def _smoothstep(u):
    u = np.clip(u, 0.0, 1.0)
    return u * u * (3.0 - 2.0 * u)


# ---------------------------------------------------------------------------
# Clearance
# ---------------------------------------------------------------------------

class Clearance:
    """The obstacles the lens must stay clear of, as a height FLOOR over any XY (the
    lowest the camera may pass there: over everything within its padded radius) and
    a point test. Trees: each instance's kit geometry (bodies + leaf cards) binned by
    radial distance (min / max height per 0.25 m ring, any turn), scaled; hulls:
    (x0, y0, cell, H) top-surface height fields (buildings, tall props); boxes: world
    AABBs (x0, y0, z0, x1, y1, z1) of every other mesh; the terrain. `exact` (optional,
    Blender: ExactSolids) replaces the hulls and boxes by the meshes' real surfaces, so
    the space under an overhang (a street light's arm) is open."""

    STEP = 0.25
    CELL = 16.0

    def __init__(self, terrain, table=None, kit=None, hulls=(), boxes=(), near=None, clear=None, exact=None):
        self.terrain = terrain
        near = config.FLIGHT_NEAR_M if near is None else near
        c = dict(config.FLIGHT_CLEAR_M, **(clear or {}))
        self.r_tree, self.r_solid, self.r_ground = near + c["tree"], near + c["solid"], near + c["ground"]
        self.hulls = list(hulls)
        self.boxes = np.asarray(boxes, float).reshape(-1, 6)
        self.exact = exact
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
        self.top = np.where(np.isfinite(self.HI), self.HI, -np.inf).max(axis=1)   # per kit
        self.reach = reach * float(self.tsw.max()) + self.r_tree
        self.span = int(np.ceil(2 * self.r_tree / self.tsw.min() / self.STEP)) + 2
        cell = np.floor(np.stack([self.tx, self.ty], 1) / self.CELL).astype(int)
        order = np.lexsort((cell[:, 1], cell[:, 0]))
        keys, first = np.unique(cell[order], axis=0, return_index=True)
        bounds = list(first) + [len(order)]
        self.grid = {tuple(k): order[bounds[i]:bounds[i + 1]] for i, k in enumerate(keys)}
        self.trees = True

    def _near_trees(self, x, y, reach=None):
        n = int(math.ceil((reach or self.reach) / self.CELL))
        cx, cy = int(math.floor(x / self.CELL)), int(math.floor(y / self.CELL))
        parts = [self.grid[(i, j)] for i in range(cx - n, cx + n + 1) for j in range(cy - n, cy + n + 1)
                 if (i, j) in self.grid]
        return np.concatenate(parts) if parts else np.zeros(0, int)

    def _ground_under(self, i):
        need = i[np.isnan(self.tz[i])]
        if len(need):
            self.tz[need] = self.terrain.z_many(self.tx[need], self.ty[need]) - self.tsink[need]

    def canopy(self, x, y, r):
        """The highest crown top (world z) of the trees standing within r of (x, y), or
        -inf where none does."""
        if not self.trees:
            return -np.inf
        i = self._near_trees(x, y, r + 1.0)
        i = i[np.hypot(self.tx[i] - x, self.ty[i] - y) < r] if len(i) else i
        if not len(i):
            return -np.inf
        self._ground_under(i)
        return float((self.tz[i] + self.top[self.tk[i]] * self.tsz[i]).max())

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
        self._ground_under(i[hit])
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

    def _box_idx(self, x, y):
        b, rr = self.boxes, self.r_solid
        if not len(b):
            return np.zeros(0, int)
        return np.nonzero((x > b[:, 0] - rr) & (x < b[:, 3] + rr) & (y > b[:, 1] - rr) & (y < b[:, 4] + rr))[0]

    def _box_hits(self, x, y):
        return self.boxes[self._box_idx(x, y)]

    def _exact_hits(self, x, y, z):
        """[(top z, what)] of the solids whose real surface is within r_solid of the lens."""
        idx = self._box_idx(x, y)
        b = self.boxes[idx]
        idx = idx[(b[:, 2] - self.r_solid < z) & (z < b[:, 5] + self.r_solid)] if len(idx) else idx
        return self.exact.hits(x, y, z, self.r_solid, idx)

    def ground(self, X, Y):
        """Highest terrain within the lens's ground radius of each (X, Y) (arrays)."""
        X, Y, r = np.asarray(X, float), np.asarray(Y, float), self.r_ground
        offs = ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1))
        z = self.terrain.z_many(np.concatenate([X + dx * r for dx, _ in offs]),
                                np.concatenate([Y + dy * r for _, dy in offs]))
        return z.reshape(len(offs), -1).max(axis=0)

    def floors(self, X, Y, Z=None):
        """The lowest camera height at each (X, Y) that clears everything around it. With
        `exact`, a solid counts only where the planned Z actually touches it."""
        g = self.ground(X, Y) + self.r_ground
        if Z is None or self.exact is None:
            return np.array([max(gi, self._floor_above(float(x), float(y))) for gi, x, y in zip(g, X, Y)])
        return np.array([max(gi, self._floor_above(float(x), float(y), float(z))) for gi, x, y, z in zip(g, X, Y, Z)])

    def _floor_above(self, x, y, z=None):
        f = -np.inf
        for _zl, zh, _w in self._tree_spans(x, y):
            f = max(f, zh + self.r_tree)
        if self.exact is not None and z is not None:
            for top, _w in self._exact_hits(x, y, z):
                f = max(f, top + self.r_solid)
            return f
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
        if self.exact is not None:
            for _top, w in self._exact_hits(x, y, z):
                bad.append(w)
            return bad
        for top, w in self._hull_tops(x, y):
            if z < top + self.r_solid:
                bad.append(f"{w} top {top:.1f} m")
        for b in self._box_hits(x, y):
            if b[2] - self.r_solid < z < b[5] + self.r_solid:
                bad.append(f"box z {b[2]:.1f}..{b[5]:.1f} at ({b[0]:.1f}, {b[1]:.1f})")
        return bad


class ExactSolids:
    """The solids' real surfaces (Blender): a world-space BVH per mesh object, built on
    first use. hits() = the objects within r of a point (candidate indices into the
    boxes, named by `names`)."""

    def __init__(self, names, boxes):
        self.names, self.boxes, self._bvh = list(names), np.asarray(boxes, float), {}

    def _tree(self, name):
        if name not in self._bvh:
            import bpy
            from mathutils.bvhtree import BVHTree
            ob = bpy.data.objects[name]
            if ob.get("kind") == "light_cone":      # a street light's glow in the air: not a solid
                self._bvh[name] = None
                return None
            dg = bpy.context.evaluated_depsgraph_get()
            ev = ob.evaluated_get(dg)
            me = ev.to_mesh()
            mw = ob.matrix_world
            verts = [mw @ v.co for v in me.vertices]
            polys = [tuple(p.vertices) for p in me.polygons]
            ev.to_mesh_clear()
            self._bvh[name] = BVHTree.FromPolygons(verts, polys) if polys else None
        return self._bvh[name]

    def hits(self, x, y, z, r, idx):
        from mathutils import Vector
        out = []
        for i in idx:
            name = self.names[i]
            tree = self._tree(name)
            if tree is None:
                continue
            loc, _n, _f, d = tree.find_nearest(Vector((x, y, z)), r)
            if loc is not None:
                out.append((float(self.boxes[i, 5]), f"{name} {d:.2f} m"))
        return out


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


def _path(P, smooth_m, pin=False):
    """The camera's path in space: a chord-length cubic spline through the key points,
    sampled every ~0.25 m, blurred by smooth_m (no hairpins; pin: the ends stay exactly
    on the first / last key). Returns (points (m, 3), arc length (m,), arc at each key)."""
    P = np.asarray(P, float)
    c = np.concatenate([[0.0], np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))])
    u = np.linspace(0.0, c[-1], int(c[-1] / 0.25) + 2)
    pts = spline(c, P, u, end_rest=False)
    sig = smooth_m / (u[1] - u[0])   # round the tight corners off
    pts = np.stack([_blur(pts[:, i], sig, pin) for i in range(3)], axis=1)
    s = np.concatenate([[0.0], np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))])
    return pts, s, np.interp(c, u, s)


def _wrap(a):
    return (a + np.pi) % (2 * np.pi) - np.pi


def plan(world, terrain, clearance=None, fps=None, sq=None):
    """The flight of sequence `sq` (a Seq; default the primary), frame by frame. Space
    and time are separate: the path is a smooth curve through the camera keys; the
    timing is the keys' segment speeds, blurred (SPEED_SMOOTH_S) so speed only ever
    eases, integrated along the arc and settling to rest after the last key (then HOLD_S
    of stillness); before the first key's time the camera holds still, then eases off
    (START_EASE_S). The view turns between aim targets by ANGLE (each target's yaw /
    pitch from where the camera is now: a held target stays locked as the camera passes),
    linear in key time, then blurred (AIM_SMOOTH_S): never through a point near the lens."""
    sq = sq or seq(primary())
    fps = fps or config.FLIGHT_FPS
    lens = sq.LENS_MM
    ck = [(t, *anchor(world, terrain, a, left, lens), h) for t, a, left, h in sq.CAM_KEYS]
    ak = [(t, *anchor(world, terrain, a, 0.0, lens), h) for t, a, h in sq.AIM_KEYS]
    tk = np.array([k[0] for k in ck])
    t_go = float(tk[0])                           # the camera holds still until the first key
    sig_v = sq.SPEED_SMOOTH_S
    t_rest = tk[-1] + 2.5 * sig_v
    n = int(round((t_rest + sq.HOLD_S) * fps)) + 1
    t = np.arange(n) / fps
    # space
    cxyz = np.array([(x, y, key_z(terrain, clearance, x, y, h)) for _t, x, y, _e, h in ck])
    pts, arc, sk = _path(cxyz, sq.PATH_SMOOTH_M, sq.PIN_ENDS)
    # time: blurred segment speeds, integrated, scaled to land exactly on the last key
    vseg = np.diff(sk) / np.diff(tk)
    seg = np.clip(np.searchsorted(tk, t, side="right") - 1, 0, len(vseg) - 1)
    vraw = np.where(t < tk[-1], vseg[seg], 0.0)
    if t_go > 0:
        vraw[t < t_go] = 0.0
    v = _blur(vraw, sig_v * fps)
    v[t >= t_rest] = 0.0
    if t_go > 0:
        v *= _smoothstep((t - t_go) / sq.START_EASE_S)
    s = np.concatenate([[0.0], np.cumsum((v[1:] + v[:-1]) / 2) / fps])
    s *= arc[-1] / s[-1]
    pos = np.stack([np.interp(s, arc, pts[:, i]) for i in range(3)], axis=1)
    lift = np.zeros(n)
    floor = None
    if clearance is not None:
        # lift the heights, smoothly, over the clearance floor (independent of z; with
        # exact solids, a solid counts where the planned lens touches it)
        floor = clearance.floors(pos[:, 0], pos[:, 1], pos[:, 2])
        w = max(1, int(sq.FLOOR_SMOOTH_S * fps / 2))
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
    sa = sq.AIM_SMOOTH_S * fps
    yaw_raw = np.unwrap(yaw)
    yaw = _blur(yaw_raw, sa)
    if t_go > 0:        # the held opening: the view is exactly still, then eases into the blur
        wv = _smoothstep((t - t_go) / (2.5 * sq.AIM_SMOOTH_S))
        g = int(round(t_go * fps))
        yaw = yaw_raw[g] * (1 - wv) + yaw * wv
        pitch = pitch[g] * (1 - wv) + _blur(pitch, sa) * wv
        dist = dist[g] * (1 - wv) + _blur(dist, sa) * wv
    else:
        pitch = _blur(pitch, sa)
        dist = _blur(dist, sa)
    fwd = np.stack([np.cos(pitch) * np.cos(yaw), np.cos(pitch) * np.sin(yaw), np.sin(pitch)], axis=1)
    aim = pos + fwd * dist[:, None]
    # bank into the travel's turns: lateral acceleration from the horizontal heading rate
    vel = np.gradient(pos[:, :2], 1.0 / fps, axis=0)
    speed = np.hypot(vel[:, 0], vel[:, 1])
    head = np.unwrap(np.arctan2(vel[:, 1], vel[:, 0]))
    omega = np.gradient(head, 1.0 / fps) * (speed > 0.5)
    gain, cap = sq.BANK
    if sq.BANK_KEYS:                # (t, gain, cap): a deliberate banking turn
        bk = np.array(sq.BANK_KEYS, float)
        gain, cap = np.interp(t, bk[:, 0], bk[:, 1]), np.interp(t, bk[:, 0], bk[:, 2])
    bank = gain * np.degrees(np.arctan(speed * omega / 9.81))
    bank = _blur(np.clip(bank * np.cos(yaw - head), -cap, cap), fps * 0.5)
    R = _look(pos, aim, bank)
    return Flight(name=sq.name, fps=fps, t=t, pos=pos, aim=aim, R=R, bank=bank, lift=lift, floor=floor,
                  speed=speed, cam_keys=ck, aim_keys=ak, key_arc=sk, s=s, last_key=float(tk[-1]),
                  first_key=t_go, hold_from=float(t_rest), beats=tuple(sq.BEATS))


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
    if fl.first_key > 0:
        moving &= fl.t[:-1] > fl.first_key + 1.0
    return dict(speed_max=float(np.linalg.norm(v, axis=1).max()),
                speed_min_moving=float(np.linalg.norm(v, axis=1)[:-1][moving].min()),
                accel_max=float(np.linalg.norm(a, axis=1).max()),
                turn_max=float(turn.max()), turn_acc_max=float(turn_acc.max()),
                lift_max=float(fl.lift.max()))


def violations(fl, clearance, sq):
    """Every frame's padded lens against the obstacles, the turn rate, the stall test:
    ([text], stats)."""
    bad = []
    g = clearance.ground(fl.pos[:, 0], fl.pos[:, 1])
    for i, ((x, y, z), gi) in enumerate(zip(fl.pos, g)):
        for w in clearance.violations(float(x), float(y), float(z), float(gi)):
            bad.append(f"frame {i + 1}: {w}")
    st = stats(fl)
    if st["turn_max"] > sq.MAX_TURN_DEG_S:
        bad.append(f"the view turns {st['turn_max']:.1f} deg/s (max {sq.MAX_TURN_DEG_S})")
    if st["speed_min_moving"] < sq.MIN_SPEED_MS:
        bad.append(f"the camera stalls mid-flight ({st['speed_min_moving']:.2f} m/s)")
    return bad, st


def check(fl, clearance, sq=None):
    """violations(), raising on any."""
    bad, st = violations(fl, clearance, sq or seq(primary()))
    if bad:
        raise AssertionError(f"flight {fl.name} clearance: {len(bad)} problems: " + "; ".join(bad[:12]))
    return st


def table(fl, sq):
    """The storyboard as flown, one line per camera key: time, beat, height above the
    ground, speed, lift over the floor: the numbers to tune by."""
    beats = sorted(fl.beats, key=lambda b: b[1])
    lines = [f"flight {fl.name}: {fl.n} frames, {fl.n / fl.fps:.1f} s at {fl.fps} fps"]
    zg = _terrain_z(fl)
    for (tkey, *_rest) in fl.cam_keys:
        f = min(fl.n - 1, int(round(tkey * fl.fps)))
        beat = [b[0] for b in beats if b[1] <= tkey + 1e-6]
        v3 = np.linalg.norm(fl.pos[min(f + 1, fl.n - 1)] - fl.pos[max(f - 1, 0)]) * fl.fps / 2
        lines.append(f"  {tkey:6.1f} s  {beat[-1] if beat else '':<24} {fl.pos[f, 2] - zg[f]:6.1f} m up  "
                     f"{v3:5.1f} m/s  lift {fl.lift[f]:4.1f}")
    return "\n".join(lines)


def _terrain_z(fl):
    return fl.ground_z if getattr(fl, "ground_z", None) is not None else np.zeros(fl.n)


# ---------------------------------------------------------------------------
# Blender: scenes
# ---------------------------------------------------------------------------

HOST_PROP = "flyover_host"
FLIGHT_PROP = "flyover_flight"


def host_scene():
    """The build's own scene (untagged, holds the primary flight)."""
    import bpy
    import scene
    for sc in bpy.data.scenes:
        if sc.get(HOST_PROP):
            return sc
    for sc in (bpy.context.scene, *bpy.data.scenes):
        if sc is not None and not scene.is_ours(sc):
            return sc
    raise RuntimeError("no host scene")


def scene_of(name, create=False):
    """The scene a flight lives in (the host for the primary)."""
    import bpy
    import look
    import scene as scene_mod
    sq = seq(name)
    host = host_scene()
    if sq.is_primary:
        return host
    sc = bpy.data.scenes.get(sq.scene_name)
    if sc is not None and not scene_mod.is_ours(sc):
        raise RuntimeError(f"scene '{sq.scene_name}' exists but was not made by the build")
    if sc is None:
        if not create:
            raise KeyError(f"flight '{name}' has no scene: rebuild")
        sc = scene_mod.tag(host.copy())
        sc.name = sq.scene_name
        for k in (HOST_PROP, "flyover_markers", "flyover_build_key", CACHE_PROP):
            if k in sc:
                del sc[k]
        sc.timeline_markers.clear()
        sc.animation_data_clear()
    look.sync_scene(host, sc)
    return sc


def set_window_scene(sc):
    """Make sc the active scene (every window; headless too: the file's window exists)."""
    import bpy
    for win in bpy.context.window_manager.windows:
        if win.scene != sc:
            win.scene = sc
    return sc


def _remove_named(names):
    import bpy
    import scene
    for n in names:
        ob = bpy.data.objects.get(n)
        if ob is not None and scene.is_ours(ob):
            data = ob.data
            ad = ob.animation_data
            act = ad.action if ad else None
            bpy.data.objects.remove(ob, do_unlink=True)
            if data is not None and data.users == 0:
                (bpy.data.cameras if isinstance(data, bpy.types.Camera) else bpy.data.curves).remove(data)
            if act is not None and act.users == 0:
                bpy.data.actions.remove(act)


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


def _fcurves(idb, name, keys, interp="LINEAR"):
    """A tagged action on idb with one fcurve per (data_path, index, frames, values),
    `interp` keys (LINEAR; life.py's V is CONSTANT). Returns the action."""
    import bpy
    from bpy_extras import anim_utils
    import scene
    ad = idb.animation_data or idb.animation_data_create()
    if ad.action is not None and not scene.is_ours(ad.action):
        raise RuntimeError(f"{idb.name} already carries a foreign action '{ad.action.name}'")
    old = ad.action
    act = scene.tag(bpy.data.actions.new(name))
    slot = act.slots.new(id_type=idb.id_type, name=idb.name)
    ad.action = act
    ad.action_slot = slot
    if old is not None and old.users == 0:
        bpy.data.actions.remove(old)
    bag = anim_utils.action_ensure_channelbag_for_slot(act, slot)
    ip = bpy.types.Keyframe.bl_rna.properties["interpolation"].enum_items[interp].value
    for path, index, frames, values in keys:
        fc = bag.fcurves.new(path, index=index)
        kp = fc.keyframe_points
        kp.add(len(frames))
        co = np.empty(2 * len(frames))
        co[0::2], co[1::2] = frames, values
        kp.foreach_set("co", co)
        kp.foreach_set("interpolation", [ip] * len(frames))
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


def _names(sq):
    return sq.CAMERA, f"Guide_CamPath_{sq.name}", f"Guide_CamAim_{sq.name}"


def bake(fl, sq, sc, cams_col, guides_col):
    """The flight into scene sc: its camera (one LINEAR key per frame, quaternions), the
    path / aim guide curves, beat markers, frame range, its dusk keys; sc's camera. A
    rebake replaces the flight's own objects."""
    import bpy
    from mathutils import Matrix
    import scene
    cam_name, path_name, aim_name = _names(sq)
    _remove_named((cam_name, path_name, aim_name))
    cam = scene.tag(bpy.data.cameras.new(cam_name))
    cam.lens = sq.LENS_MM
    cam.sensor_width = 36.0
    cam.sensor_fit = "AUTO"
    cam.clip_start = config.FLIGHT_NEAR_M * 0.6
    cam.clip_end = config.CLIP_END_M
    ob = scene.new_object(cam_name, cam, cams_col, kind="flight", flight=sq.name)
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
    _fcurves(ob, f"{cam_name}_Action", keys)
    ob["beats"] = str([(b[0], fl.frame(b[1]), fl.frame(b[2])) for b in sq.BEATS])
    _curve(path_name, fl.pos, guides_col, kind="cam_path", fps=fl.fps, frames=fl.n, flight=sq.name)
    _curve(aim_name, fl.aim, guides_col, kind="cam_aim", flight=sq.name)
    # the time of the light
    dk = np.array(sq.DUSK_KEYS, float).reshape(-1, 2)
    _fcurves(sc, f"Flyover_Dusk_{sq.name}",
             [(f'["{config.DUSK_PROP}"]', 0, [fl.frame(t) for t in dk[:, 0]], dk[:, 1])])
    # beat markers (a rebake replaces its own)
    for name in list(sc.get("flyover_markers", [])):
        m = sc.timeline_markers.get(name)
        if m is not None:
            sc.timeline_markers.remove(m)
    for name, start, _hero in sq.BEATS:
        sc.timeline_markers.new(name, frame=fl.frame(start))
    sc["flyover_markers"] = [b[0] for b in sq.BEATS]
    sc[FLIGHT_PROP] = sq.name
    sc.frame_start, sc.frame_end = 1, fl.n
    sc.render.fps = fl.fps
    sc.camera = ob
    sc.frame_set(1)
    return ob


def hero_frames(fl):
    return [(name, fl.frame(hero)) for name, _start, hero in fl.beats]


# ---------------------------------------------------------------------------
# The build + the live loop
# ---------------------------------------------------------------------------

CACHE_PROP = "flyover_cache_id"
_MEM = "flyover_flight_cache"       # bpy.app.driver_namespace key: survives module reloads


def scene_boxes(cols):
    """World AABBs of every rendered mesh object under the collections + their names."""
    import bpy
    from mathutils import Vector
    bpy.context.view_layer.update()
    out, names = [], []
    seen = set()
    for col in cols:
        if col is None:
            continue
        for ob in col.all_objects:
            if ob.type != "MESH" or ob.hide_render or ob.name in seen:
                continue
            seen.add(ob.name)
            mw = ob.matrix_world
            c = np.array([tuple(mw @ Vector(v)) for v in ob.bound_box])
            out.append((*c.min(0), *c.max(0)))
            names.append(ob.name)
    return np.array(out, float).reshape(-1, 6), names


def _site(world, terrain, kit_vertices, boxes, box_names):
    """What planning needs, without Blender or the world build: picklable."""
    import copy

    import world as world_mod
    lite = world_mod.World(maps=world.maps, bounds=world.bounds)
    lite.buildings = [dict(name=b["name"], height=b["height"], hull=b["hull"]) for b in world.buildings]
    lite.prop_hulls = [p["hull"] for p in world.props if "hull" in p]
    terr = copy.copy(terrain)           # its parts hold the world: point them at the lite one
    terr.world = lite
    for part in ("treeline", "surfaces"):
        if hasattr(terr, part):
            sub = copy.copy(getattr(terr, part))
            sub.world = lite
            setattr(terr, part, sub)
    tb = world.forest.table
    table = types.SimpleNamespace(**{k: np.asarray(getattr(tb, k)) for k in ("x", "y", "kind", "scale", "sink",
                                                                           "group")})
    return dict(world=lite, terrain=terr, table=table, kit=kit_vertices, boxes=boxes, box_names=box_names,
                id=f"{time.time():.3f}")


def clearance_for(site, sq, exact=True):
    hulls = [b["hull"] for b in site["world"].buildings] + list(site["world"].prop_hulls)
    ex = ExactSolids(site["box_names"], site["boxes"]) if (sq.EXACT_SOLIDS and exact) else None
    return Clearance(site["terrain"], site["table"], site["kit"], hulls, site["boxes"], clear=sq.clear, exact=ex)


def plan_checked(site, name, strict=True, fps=None):
    """Plan + check one flight: (Flight, Seq, stats, [problems])."""
    sq = seq(name)
    cl = clearance_for(site, sq)
    fl = plan(site["world"], site["terrain"], cl, fps, sq)
    fl.ground_z = site["terrain"].z_many(fl.pos[:, 0], fl.pos[:, 1])
    bad, st = violations(fl, cl, sq)
    if bad and strict:
        raise AssertionError(f"flight {name} clearance: {len(bad)} problems: " + "; ".join(bad[:12]))
    fl.stats, fl.clearance, fl.problems = st, cl, bad
    return fl, sq, st, bad


def build(world, terrain, cams_col, guides_col, kit_vertices, solid_cols):
    """Plan and check every flight; bake the primary into the host scene (the others are
    baked into their own scenes by finish_scenes(), once the world is complete). Returns
    {name: Flight}."""
    import bpy
    t0 = time.time()
    boxes, box_names = scene_boxes(solid_cols)
    site = _site(world, terrain, kit_vertices, boxes, box_names)
    bpy.app.driver_namespace[_MEM] = site
    out = {}
    for name in names():
        t1 = time.time()
        fl, sq, st, _bad = plan_checked(site, name)
        out[name] = fl
        cl = fl.clearance
        print(f"flyover: flight {name} {fl.n} frames ({fl.n / fl.fps:.1f} s at {fl.fps} fps), "
              f"{len(cl.boxes)} boxes + {len(cl.hulls)} hulls + {len(cl.tx)} trees clear"
              f"{' (exact solids)' if cl.exact else ''}; "
              + ", ".join(f"{k} {v:.1f}" for k, v in st.items()) + f" in {time.time() - t1:.1f}s")
    host = host_scene()
    host[HOST_PROP] = True
    if host.name != primary() and primary() not in bpy.data.scenes:
        host.name = primary()           # the scene selector reads as the flights
    host[CACHE_PROP] = site["id"]
    sq = seq(primary())
    bake(out[primary()], sq, host, cams_col, guides_col)
    print(f"flyover: flights planned + checked in {time.time() - t0:.1f}s")
    return out


def finish_scenes(flights, cams_col, guides_col):
    """Every non-primary flight into its own scene (made from the host now the world is
    complete). The host stays active."""
    host = host_scene()
    for name, fl in flights.items():
        sq = seq(name)
        if sq.is_primary:
            continue
        sc = scene_of(name, create=True)
        bake(fl, sq, sc, cams_col, guides_col)
        print(f"flyover: flight {name} baked into scene '{sc.name}' (frames 1..{fl.n})")
    set_window_scene(host)


def cache_path(blend=None):
    import bpy
    blend = blend or bpy.data.filepath
    return os.path.splitext(blend)[0] + ".flight_cache.pkl" if blend else None


def save_cache(blend=None):
    """Write the planning site next to the .blend (build.main / film / a live build)."""
    import bpy
    site = bpy.app.driver_namespace.get(_MEM)
    path = cache_path(blend)
    if site is None or path is None:
        return None
    with open(path + ".tmp", "wb") as fh:
        pickle.dump(site, fh, protocol=pickle.HIGHEST_PROTOCOL)
    os.replace(path + ".tmp", path)
    return path


def _load_site():
    import bpy
    want = host_scene().get(CACHE_PROP)
    site = bpy.app.driver_namespace.get(_MEM)
    if site is not None and site["id"] == want:
        return site
    path = cache_path()
    if path and os.path.exists(path):
        with open(path, "rb") as fh:
            site = pickle.load(fh)
        if site["id"] == want:
            bpy.app.driver_namespace[_MEM] = site
            return site
    raise RuntimeError(f"no flight cache for this .blend (build id {want}; {path}): run a full build")


def rebake(name=None, strict=False):
    """The live loop: re-read flights/<name>.py and re-plan, check and bake that one
    camera (+ its markers, frame range, dusk keys) into its scene, in seconds, without
    rebuilding the world. name defaults to the active scene's flight. Prints the flown
    storyboard table and any clearance problems (strict raises instead). Life (wind,
    leaves, smoke) keeps the full build's flights: rebuild to refresh it."""
    import bpy
    t0 = time.time()
    name = name or bpy.context.scene.get(FLIGHT_PROP) or primary()
    site = _load_site()
    fl, sq, st, bad = plan_checked(site, name, strict=strict)
    sc = scene_of(name, create=True)
    cams = bpy.data.collections[config.COL_CAMERAS]
    guides = bpy.data.collections[config.COL_GUIDES]
    frame = sc.frame_current
    bake(fl, sq, sc, cams, guides)
    sc.frame_set(min(frame, fl.n))
    lines = [table(fl, sq), "  " + ", ".join(f"{k} {v:.1f}" for k, v in st.items())]
    if bad:
        lines.append(f"  CLEARANCE: {len(bad)} problems:")
        lines += [f"    {b}" for b in bad[:60]]
    else:
        lines.append("  clearance: every frame clear")
    lines.append(f"flyover: rebaked {name} in {time.time() - t0:.1f}s")
    report = "\n".join(lines)
    print(report)
    txt = bpy.data.texts.get(REPORT_TEXT)
    if txt is None:
        import scene
        txt = scene.tag(bpy.data.texts.new(REPORT_TEXT))
    txt.clear()
    txt.write(report + "\n")
    return fl, bad


REPORT_TEXT = "flyover_rebake_report"


def viewport_playblast(context, path=None):
    """The live preview: the 3D view the operator runs in, through the flight camera
    (bpy.ops.render.opengl: the viewport's own Solid shading and hidden cards), every
    frame at 960 wide to out/playblast_<flight>_live.mp4. Blocks the UI while it runs."""
    import render
    sc = context.scene
    name = sc.get(FLIGHT_PROP) or primary()
    path = path or os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", f"playblast_{name}_live.mp4")
    r = sc.render
    keep = (r.resolution_x, r.resolution_y, r.resolution_percentage)
    restore_video = render._video(sc, path, sc.render.fps)
    restore_stamp = render._stamp(sc, name)
    try:
        r.resolution_x, r.resolution_y = render.PLAYBLAST_RES
        r.resolution_percentage = 100
        context.space_data.region_3d.view_perspective = "CAMERA"
        import bpy
        bpy.ops.render.opengl(animation=True)
    finally:
        restore_stamp()
        restore_video()
        r.resolution_x, r.resolution_y, r.resolution_percentage = keep
    return path


LEAF_CARDS = ("Forest_Cards", "Forest_WindCards")


def switch(name, frame=1, solid=True):
    """Put the viewport on a flight: its scene active, its camera, frame, the 3D views
    looking through the camera in Solid shading, the leaf cards (opaque squares in Solid)
    and the street-light cones hidden in the viewport (renders keep them), playback
    dropping frames to hold real time."""
    import bpy
    sc = scene_of(name, create=False)
    set_window_scene(sc)
    sc.frame_set(frame)
    sc.sync_mode = "FRAME_DROP"
    vl = sc.view_layers[0]
    for ob in bpy.data.objects:
        if ob.name in LEAF_CARDS or ob.get("kind") == "light_cone":
            try:
                ob.hide_set(solid, view_layer=vl)
            except RuntimeError:
                pass
    for win in bpy.context.window_manager.windows:
        for area in win.screen.areas:
            if area.type != "VIEW_3D":
                continue
            sp = area.spaces.active
            sp.region_3d.view_perspective = "CAMERA"
            if solid:
                sp.shading.type = "SOLID"
                sp.shading.color_type = "MATERIAL"
            sp.lock_camera = False
    return sc

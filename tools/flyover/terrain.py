"""The ground as numbers (numpy, no bpy): relief, surfaces and ground features, on an
adaptive lattice. ground.py turns it into meshes; placeholders and markings ask it
for heights (Terrain.z_at).

Relief (Relief.height): 0 inside every map; outside, hills rise with the distance
from the nearest map (flat collar config.FLAT_PAD_M, full height after RISE_M, still
climbing past it so the terrain's outer edge is always a ridge), taller to the north,
shaped by a deterministic value-noise fBm, and pressed down into a valley along each
road-out (and, gently, along each forest track); the mansion clearing (routes.mansion_clearing) is levelled to a shelf. Along the road-outs the ground is levelled to the road (config.CORRIDOR_*):
under the carriageway it sits below the road, the verge meets the kerb top in town and
drops to a rural shoulder away from it.

Road height (Terrain.road_z): the paved road's reference level (the verge / kerb top)
along the whole centreline: 0 in town; outside, the relief sampled under the road-out,
smoothed and grade-limited, so the road rides the valley floor.

Lattice: the NEAR rect (world bounds + config.NEAR_PAD_TILES, even tiles) at
config.SUB x SUB vertices per tile. What SHOWS on the ground is surfaces.Surfaces (smooth
fields painted by one shader); a face's material_index is only its tile's surface (the
Workbench layout check). A tile is FINE (SUB x SUB quads) where anything varies in or
next to it (surfaces, ruts, ramps, the pit, the road-out corridor), COARSE (one quad)
elsewhere; T-junctions are closed by pinning edge vertices to the coarse edge's line.
Every ground face is smooth-shaded.
The FAR ring (config.FAR_CELL_M quads) runs out to config.FAR_MARGIN_M past the
bounds; the near rect's outer vertices are pinned to its 5 m spacing.

Features (heights added to the relief, per vertex, from the surface fields): dirt is
a slightly sunken yard with a soft lip, and along a track's filleted centreline two
ruts (the W profile; the shader draws them from the same `td` field); Path is a
shallow dip; pit_cover rects dish down; ramp_rows become drive-in ramps (gentle run-up
north of each crest, short drop south). The one mesh attribute is `verge` (1 within
config.VERGE_M of a road-out: the forest floor grasses over along the road).
"""

import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import config  # noqa: E402

T = config.TILE_M
S = config.SUB
STEP = T / S
SURF = {k: i for i, k in enumerate(config.SURFACES)}


# ---------------------------------------------------------------------------
# Small numeric helpers
# ---------------------------------------------------------------------------

def smoothstep(e0, e1, x):
    t = np.clip((np.asarray(x, dtype=float) - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def _hash(ix, iy, seed):
    h = (ix.astype(np.uint64) * np.uint64(374761393) + iy.astype(np.uint64) * np.uint64(668265263)
         + np.uint64(seed * 1442695041 % 2**32)) & np.uint64(0xFFFFFFFF)
    h = ((h ^ (h >> np.uint64(13))) * np.uint64(1274126177)) & np.uint64(0xFFFFFFFF)
    h = h ^ (h >> np.uint64(16))
    return h.astype(np.float64) / 4294967296.0


def vnoise(x, y, seed):
    """Smooth value noise in [0, 1] (unit lattice)."""
    x, y = np.asarray(x, float), np.asarray(y, float)
    ix, iy = np.floor(x), np.floor(y)
    fx, fy = x - ix, y - iy
    ux, uy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    ix, iy = ix.astype(np.int64), iy.astype(np.int64)
    a, b = _hash(ix, iy, seed), _hash(ix + 1, iy, seed)
    c, d = _hash(ix, iy + 1, seed), _hash(ix + 1, iy + 1, seed)
    return (a * (1 - ux) + b * ux) * (1 - uy) + (c * (1 - ux) + d * ux) * uy


def fbm(x, y, waves, seed):
    """Sum of value-noise octaves, normalised to [-1, 1]."""
    tot, amp_sum = 0.0, 0.0
    for k, (wl, amp) in enumerate(waves):
        tot = tot + amp * (vnoise(x / wl + 17.3 * k, y / wl - 9.1 * k, seed + k) * 2 - 1)
        amp_sum += amp
    return tot / amp_sum


def dist_rects(X, Y, rects):
    d = np.full(np.shape(X), np.inf)
    for x0, y0, x1, y1 in rects:
        dx = np.maximum(np.maximum(x0 - X, 0), X - x1)
        dy = np.maximum(np.maximum(y0 - Y, 0), Y - y1)
        d = np.minimum(d, np.hypot(dx, dy))
    return d


def inside_depth(X, Y, rect):
    """Signed distance INTO a rect (positive inside, negative outside)."""
    x0, y0, x1, y1 = rect
    return np.minimum(np.minimum(X - x0, x1 - X), np.minimum(Y - y0, y1 - Y))


def nearest_on(X, Y, pts, vals=None):
    """Distance from each point to a polyline, plus `vals` (n, k) interpolated at the
    nearest point. Returns (d, v)."""
    pts = np.asarray(pts, float)
    X, Y = np.asarray(X, float), np.asarray(Y, float)
    best = np.full(X.shape, np.inf)
    out = None if vals is None else np.zeros(X.shape + (np.shape(vals)[1],))
    for i in range(len(pts) - 1):
        ax, ay = pts[i]
        bx, by = pts[i + 1]
        abx, aby = bx - ax, by - ay
        L2 = abx * abx + aby * aby
        if L2 == 0:
            continue
        t = np.clip(((X - ax) * abx + (Y - ay) * aby) / L2, 0, 1)
        d = np.hypot(X - (ax + t * abx), Y - (ay + t * aby))
        m = d < best
        best = np.where(m, d, best)
        if vals is not None and m.any():
            vi = vals[i][None, :] * (1 - t[m])[:, None] + vals[i + 1][None, :] * t[m][:, None]
            out[m] = vi
    return best, out


def densify(pts, step):
    out = [tuple(pts[0])]
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        n = max(1, int(math.ceil(math.hypot(x1 - x0, y1 - y0) / step)))
        out += [(x0 + (x1 - x0) * k / n, y0 + (y1 - y0) * k / n) for k in range(1, n + 1)]
    return out


def arc_lengths(pts):
    p = np.asarray(pts, float)
    return np.concatenate([[0.0], np.cumsum(np.hypot(*np.diff(p, axis=0).T))])


def _offsets(radius):
    r = int(radius)
    return [(dj, di, math.hypot(dj, di)) for dj in range(-r, r + 1) for di in range(-r, r + 1)
            if 0 < math.hypot(dj, di) <= radius]


def _shift(a, dj, di, fill):
    """a shifted so out[j, i] = a[j + dj, i + di] (fill outside)."""
    out = np.full_like(a, fill)
    H, W = a.shape
    js, je = max(0, -dj), min(H, H - dj)
    is_, ie = max(0, -di), min(W, W - di)
    out[js:je, is_:ie] = a[js + dj:je + dj, is_ + di:ie + di]
    return out


def chamfer(open_mask, cap_cells):
    """Per cell: approximate Euclidean distance (cells) to the nearest True cell of
    open_mask (8-neighbour chamfer relaxation), capped at cap_cells."""
    d = np.where(open_mask, 0.0, np.inf).astype(np.float32)
    H, W = d.shape
    steps = ((1, 0, 1.0), (0, 1, 1.0), (1, 1, 1.41421), (1, -1, 1.41421))
    for _ in range(int(math.ceil(cap_cells))):
        for dj, di, c in steps:
            for sj, si in ((dj, di), (-dj, -di)):
                dst = d[max(sj, 0):H + min(sj, 0), max(si, 0):W + min(si, 0)]
                src = d[max(-sj, 0):H + min(-sj, 0), max(-si, 0):W + min(-si, 0)]
                np.minimum(dst, src + c, out=dst)
    return np.minimum(d, cap_cells)


# ---------------------------------------------------------------------------
# Relief
# ---------------------------------------------------------------------------

class Relief:
    def __init__(self, world, routes):
        self.rects = [(m.ox * T, -(m.oy + m.height) * T, (m.ox + m.width) * T, -m.oy * T)
                      for m in world.maps.values()]
        self.north_y = -world.bounds[1] * T
        # road-outs, coarsened for distance queries
        self.outs = [np.asarray(densify(o[::2] + ([o[-1]] if len(o) % 2 == 0 else []), 5.0))
                     for o in (routes.out_w, routes.out_e)]
        self.road = [None, None]        # per out: (n, 2) values (zr, urban) set by Terrain
        self.track_ext = [np.asarray(densify(t.pts[t.extension_from:], 5.0))
                          for t in routes.tracks.values() if t.extension_from >= 0]
        self.clearing = None
        c = routes.mansion_clearing()
        if c is not None:
            self.clearing_z = float(self.hills(np.array([c[0]]), np.array([c[1]]))[0])
            self.clearing = c

    def hills(self, X, Y):
        """The relief without the road corridor."""
        X, Y = np.asarray(X, float), np.asarray(Y, float)
        d = dist_rects(X, Y, self.rects)
        pad = config.FLAT_PAD_M
        # the valley walls: a short ease off the flat collar, then an ease-out climb
        t = np.clip((d - pad) / config.RISE_M, 0, 1)
        rise = smoothstep(pad, pad + config.WALL_EASE_M, d) * (1 - (1 - t) ** 2)
        amp = config.HILL_BASE_M + config.HILL_NORTH_M * smoothstep(
            *config.NORTH_RAMP_M, Y - self.north_y)
        n = fbm(X, Y, config.HILL_WAVES_M, config.TERRAIN_SEED)
        rim = config.RIM_SLOPE * np.maximum(d - pad - config.RISE_M, 0) * (0.5 + 0.5 * amp / (
            config.HILL_BASE_M + config.HILL_NORTH_M))
        dv = np.minimum(*[nearest_on(X, Y, o)[0] for o in self.outs])
        valley = config.VALLEY_MIN + (1 - config.VALLEY_MIN) * smoothstep(*config.VALLEY_M, dv)
        for tr in self.track_ext:       # a forest track climbs a gentle hollow
            dt = nearest_on(X, Y, tr)[0]
            valley = valley * (config.TRACK_VALLEY_MIN + (1 - config.TRACK_VALLEY_MIN)
                               * smoothstep(*config.TRACK_VALLEY_M, dt))
        shape = 1 - config.HILL_NOISE_MIX + config.HILL_NOISE_MIX * (0.5 + 0.5 * n)
        ridge = config.RIDGE_M * smoothstep(*config.RIDGE_FROM_M, d) * (
            0.5 + 0.5 * fbm(X, Y, config.RIDGE_WAVES_M, config.TERRAIN_SEED + 11)) ** 1.4
        h = (amp * rise * shape + rim + ridge) * valley
        return self._level_clearing(X, Y, h)

    def _level_clearing(self, X, Y, h):
        """The mansion clearing is a level shelf (its centre's height), blending back to
        the hills over config.CLEARING_BLEND_M."""
        if self.clearing is None:
            return h
        cx, cy, th, w, d = self.clearing
        u = (X - cx) * np.cos(th) + (Y - cy) * np.sin(th)
        v = -(X - cx) * np.sin(th) + (Y - cy) * np.cos(th)
        depth = np.minimum(d / 2 - np.abs(u), w / 2 - np.abs(v))
        f = smoothstep(-config.CLEARING_BLEND_M, 2.0, depth)
        return h * (1 - f) + self.clearing_z * f

    def height(self, X, Y):
        """The relief with the road-out corridors levelled to the road."""
        X, Y = np.asarray(X, float), np.asarray(Y, float)
        h = self.hills(X, Y)
        best = np.full(X.shape, np.inf)
        zr = np.zeros(X.shape)
        urban = np.zeros(X.shape)
        for o, vals in zip(self.outs, self.road):
            d, v = nearest_on(X, Y, o, vals)
            m = d < best
            best = np.where(m, d, best)
            zr = np.where(m, v[..., 0], zr)
            urban = np.where(m, v[..., 1], urban)
        under = best < config.ROAD_HALF_M - config.KERB_W_M
        depth = np.where(under, 0.35, 0.2 * (1 - urban))
        t = smoothstep(config.CORRIDOR_FLAT_M, config.CORRIDOR_FLAT_M + config.CORRIDOR_BLEND_M, best)
        # in town the verge band IS the kerb top: keep the flat band tight there
        t = np.where(~under & (urban > 0.5) & (best < config.ROAD_HALF_M + 0.4), 0.0, t)
        return (zr - depth) * (1 - t) + h * t


# ---------------------------------------------------------------------------
# Terrain
# ---------------------------------------------------------------------------

def _even_floor(v):
    return v - (v % 2)


class Terrain:
    def __init__(self, world, routes):
        self.world, self.routes = world, routes
        gx0, gy0, gx1, gy1 = world.bounds
        P = config.NEAR_PAD_TILES
        self.nx0, self.ny0 = _even_floor(gx0 - P), _even_floor(gy0 - P)
        nx1, ny1 = -_even_floor(-(gx1 + P)), -_even_floor(-(gy1 + P))
        self.NTx, self.NTy = nx1 - self.nx0, ny1 - self.ny0
        self.NI, self.NJ = self.NTx * S + 1, self.NTy * S + 1
        self.map_ids = list(world.maps)
        self.relief = Relief(world, routes)
        self._road_profile()
        self._tiles()
        self._raster()
        import surfaces     # (these import this module's helpers)
        import treeline
        self.treeline = treeline.Treeline(world, routes, self.tile_surf, self.tile_map,
                                          self.nx0, self.ny0)
        self.surfaces = surfaces.Surfaces(world, routes, self.tile_surf, self.nx0, self.ny0,
                                          self.treeline)
        self._features()
        self._fine()
        self._heights()
        self._far()

    # -- coordinates -------------------------------------------------------
    def lattice_xy(self):
        X = (self.nx0 + np.arange(self.NI) / S) * T
        Y = -(self.ny0 + np.arange(self.NJ) / S) * T
        return np.meshgrid(X, Y)

    def near_rect_m(self):
        return (self.nx0 * T, -(self.ny0 + self.NTy) * T, (self.nx0 + self.NTx) * T, -self.ny0 * T)

    # -- the road's height ---------------------------------------------------
    def _road_profile(self):
        """Per road-out: arc length s, reference height zr (corridor target), urban
        factor, and the drawn road height (zr sunk under the ground at the far end)."""
        self.outs = []
        for i, pts in enumerate((self.routes.out_w, self.routes.out_e)):
            p = np.asarray(pts, float)
            s = arc_lengths(p)
            raw = self.relief.hills(p[:, 0], p[:, 1])
            win = max(1, int(round(config.ROAD_OUT_SMOOTH_M / config.ROAD_OUT_STEP_M)))
            pad = np.concatenate([np.full(win, raw[0]), raw, np.full(win, raw[-1])])
            sm = np.convolve(pad, np.ones(2 * win + 1) / (2 * win + 1), mode="same")[win:-win]
            sm = sm * smoothstep(0.0, 25.0, s)       # leave town level
            g = config.ROAD_OUT_MAX_GRADE * config.ROAD_OUT_STEP_M
            zr = sm.copy()
            zr[0] = 0.0
            for k in range(1, len(zr)):
                zr[k] = np.clip(zr[k], zr[k - 1] - g, zr[k - 1] + g)
            urban = 1.0 - smoothstep(*config.URBAN_FADE_M, s)
            sink = 1.6 * smoothstep(s[-1] - config.ROAD_OUT_SINK_M, s[-1], s)
            self.outs.append(dict(pts=p, s=s, zr=zr, urban=urban, z=zr - sink))
            # the relief's corridor reads (zr, urban) on its own coarsened copy
            d, v = nearest_on(self.relief.outs[i][:, 0], self.relief.outs[i][:, 1], p,
                              np.stack([zr, urban], 1))
            self.relief.road[i] = v

    # -- tiles and sub-cells ---------------------------------------------------
    def _tiles(self):
        self.tile_surf = np.full((self.NTy, self.NTx), SURF[config.WILD_SURFACE], np.int16)
        self.tile_map = np.full((self.NTy, self.NTx), -1, np.int16)
        self.tile_road = np.zeros((self.NTy, self.NTx), bool)
        for k, m in enumerate(self.world.maps.values()):
            ty, tx = m.oy - self.ny0, m.ox - self.nx0
            if tx < 0 or ty < 0 or tx + m.width > self.NTx or ty + m.height > self.NTy:
                raise AssertionError(f"{m.id} lies outside the near lattice")
            idx = np.array([[SURF[s] for s in row] for row in m.surfaces], np.int16)
            self.tile_surf[ty:ty + m.height, tx:tx + m.width] = idx
            self.tile_map[ty:ty + m.height, tx:tx + m.width] = k
            self.tile_road[ty:ty + m.height, tx:tx + m.width] = idx == SURF["Road"]

    def _sub_centres(self):
        X = (self.nx0 + (np.arange(self.NTx * S) + 0.5) / S) * T
        Y = -(self.ny0 + (np.arange(self.NTy * S) + 0.5) / S) * T
        return np.meshgrid(X, Y)

    def _raster(self):
        self.surf = np.repeat(np.repeat(self.tile_surf, S, 0), S, 1)
        self.surf_face = self.surf      # material_index: the tile's surface

    # -- features --------------------------------------------------------------
    def _features(self):
        X, Y = self.lattice_xy()
        sf = self.surfaces
        own_d = sf.own("Dirt", X, Y)
        td, fade = sf.field("td", X, Y), sf.field("fade", X, Y)
        lip = np.clip(own_d / 0.6, 0, 1)
        off, w = config.TRACK_RUT_M
        rut = np.exp(-((td - off) / w) ** 2) * (1 - fade) * (td < config.TRACK_HALF_M)
        F = -(config.DIRT_SINK_M + config.RUT_DEPTH_M * rut) * lip
        F -= config.PATH_SINK_M * np.clip(sf.own("Path", X, Y) / 0.6, 0, 1)
        # the pit, ramps
        self.ramp_crests = []     # (x0, x1, y) metres
        for m in self.world.maps.values():
            for p in m.data["props"]:
                rect = ((m.ox + p["x"]) * T, -(m.oy + p["y"] + p["h"]) * T,
                        (m.ox + p["x"] + p["w"]) * T, -(m.oy + p["y"]) * T)
                if p["kind"] == "pit_cover":
                    F -= config.PIT_SINK_M * smoothstep(-0.3, 0.9, inside_depth(X, Y, rect))
                elif p["kind"] == "ramp_rows":
                    inset = smoothstep(0.0, 1.25, inside_depth(X, Y, rect))
                    for ypx in p["rowsPx"]:
                        yc = rect[3] - ypx * T / config.TILE_PX
                        ds = yc - Y         # > 0 south of the crest
                        up = np.clip(1 + ds / config.RAMP_RISE_M, 0, 1) * (ds <= 0)
                        dn = np.clip(1 - ds / config.RAMP_DROP_M, 0, 1) * (ds > 0)
                        F += config.RAMP_H_M * (up + dn) * inset
                        self.ramp_crests.append((rect[0], rect[2], yc))
        self.F = F

    def _fine(self):
        ts = self.tile_surf
        diff = np.zeros_like(ts, bool)
        for dj in (-1, 0, 1):
            for di in (-1, 0, 1):
                if dj or di:
                    diff |= _shift(ts, dj, di, -1) != ts
        blocks = self.surf.reshape(self.NTy, S, self.NTx, S)
        nonuni = blocks.min(axis=(1, 3)) != blocks.max(axis=(1, 3))
        feat = np.abs(self.F) > 1e-5
        fblk = (feat[:-1, :-1] | feat[1:, :-1] | feat[:-1, 1:] | feat[1:, 1:])
        fblk = fblk.reshape(self.NTy, S, self.NTx, S).any(axis=(1, 3))
        grow = nonuni.copy()
        for dj in (-1, 0, 1):
            for di in (-1, 0, 1):
                grow |= _shift(nonuni, dj, di, False)
        # road-out corridor near town (kerb-to-verge accuracy)
        Xt = (self.nx0 + np.arange(self.NTx) + 0.5) * T
        Yt = -(self.ny0 + np.arange(self.NTy) + 0.5) * T
        XT, YT = np.meshgrid(Xt, Yt)
        dcor = np.minimum(*[nearest_on(XT, YT, o["pts"][:40])[0] for o in self.outs])
        corr = (dcor < config.CORRIDOR_FLAT_M + 2.0) & (self.tile_map < 0)
        self.fine = (diff & (self.tile_map >= 0)) | grow | fblk | corr
        # tiles at the lattice frame stay coarse (they meet the far ring)
        self.fine[0, :] = self.fine[-1, :] = False
        self.fine[:, 0] = self.fine[:, -1] = False

    def _heights(self):
        NJ, NI = self.NJ, self.NI
        # coarse natural relief at tile corners, bilinear up to the lattice
        Xk = (self.nx0 + np.arange(self.NTx + 1)) * T
        Yk = -(self.ny0 + np.arange(self.NTy + 1)) * T
        XK, YK = np.meshgrid(Xk, Yk)
        Hc = self.relief.height(XK, YK)
        f = (np.arange(NI) % S) / S
        i0 = np.minimum(np.arange(NI) // S, self.NTx - 1)
        f = np.where(np.arange(NI) == NI - 1, 1.0, f)
        A = Hc[:, i0] * (1 - f) + Hc[:, i0 + 1] * f
        g = (np.arange(NJ) % S) / S
        j0 = np.minimum(np.arange(NJ) // S, self.NTy - 1)
        g = np.where(np.arange(NJ) == NJ - 1, 1.0, g)
        Hn = A[j0, :] * (1 - g)[:, None] + A[j0 + 1, :] * g[:, None]
        # exact relief on the vertices of fine wild tiles
        wild_fine = self.fine & (self.tile_map < 0)
        vm = np.zeros((NJ, NI), bool)
        for a in range(S + 1):
            for b in range(S + 1):
                vm[a:a + self.NTy * S:S, b:b + self.NTx * S:S] |= wild_fine
        X, Y = self.lattice_xy()
        Hn[vm] = self.relief.height(X[vm], Y[vm])
        # in-map vertices (any adjacent tile in a map) are the flat town floor
        inmap = np.zeros((NJ, NI), bool)
        in_tile = np.repeat(np.repeat(self.tile_map >= 0, S, 0), S, 1)
        inmap[:-1, :-1] |= in_tile
        inmap[1:, :-1] |= in_tile
        inmap[:-1, 1:] |= in_tile
        inmap[1:, 1:] |= in_tile
        H = np.where(inmap, 0.0, Hn) + self.F
        # under the in-town carriageway (where a wild tile meets the road's end) the
        # ground sits below the road like it does under the road-outs
        r = self.routes
        under = ((np.abs(Y - r.road_y) < config.ROAD_HALF_M - config.KERB_W_M - 1e-6)
                 & (X >= r.road_x[0] - 1e-6) & (X <= r.road_x[1] + 1e-6))
        H = np.where(under, -0.35, H)
        # pin the frame to the far ring's spacing
        k = int(round(config.FAR_CELL_M / STEP))
        for line in (H[0, :], H[-1, :]):
            idx = np.arange(len(line))
            line[:] = np.interp(idx, idx[::k], line[::k])
        for line in (H[:, 0], H[:, -1]):
            idx = np.arange(len(line))
            line[:] = np.interp(idx, idx[::k], line[::k])
        # close T-junctions between fine and coarse tiles
        fine = self.fine
        horiz = _shift(fine, -1, 0, False) != fine      # edge at the top of tile row ty
        horiz[0, :] = False
        for ty, tx in zip(*np.nonzero(horiz)):
            j, i = ty * S, tx * S
            H[j, i + 1:i + S] = np.interp(np.arange(1, S), [0, S], [H[j, i], H[j, i + S]])
        vert = _shift(fine, 0, -1, False) != fine        # edge at the left of tile col tx
        vert[:, 0] = False
        for ty, tx in zip(*np.nonzero(vert)):
            j, i = ty * S, tx * S
            H[j + 1:j + S, i] = np.interp(np.arange(1, S), [0, S], [H[j, i], H[j + S, i]])
        self.H = H
        self.inmap = inmap

    def _far(self):
        c = config.FAR_CELL_M
        x0, y0, x1, y1 = self.near_rect_m()
        gx0, gy0, gx1, gy1 = self.world.bounds
        need = lambda span: int(math.ceil(max(0.0, span) / c))  # noqa: E731
        fx0 = x0 - c * need(x0 - (gx0 * T - config.FAR_MARGIN_M))
        fx1 = x1 + c * need(gx1 * T + config.FAR_MARGIN_M - x1)
        fy0 = y0 - c * need(y0 - (-gy1 * T - config.FAR_MARGIN_M))
        fy1 = y1 + c * need(-gy0 * T + config.FAR_MARGIN_M - y1)
        # past the margin the cells grow geometrically out to OUTER_M (a rectilinear
        # grid, so no T-junctions): the horizon is always more hills, never an edge
        grow = []
        step, dist = c, 0.0
        while dist < config.OUTER_M - config.FAR_MARGIN_M:
            step = min(step * config.OUTER_GROWTH, config.OUTER_MAX_CELL_M)
            dist += step
            grow.append(dist)
        grow = np.array(grow)
        self.far_x = np.concatenate([fx0 - grow[::-1], np.arange(fx0, fx1 + c / 2, c), fx1 + grow])
        self.far_y = np.concatenate([fy1 + grow[::-1], np.arange(fy1, fy0 - c / 2, -c), fy0 - grow])
        FX, FY = np.meshgrid(self.far_x, self.far_y)
        self.far_H = self.relief.height(FX, FY)
        # match the near frame exactly (same function, but make it bit-identical)
        xs = np.round((self.far_x - x0) / STEP).astype(int)
        ys = np.round((y1 - self.far_y) / STEP).astype(int)
        inx = (xs >= 0) & (xs < self.NI)
        iny = (ys >= 0) & (ys < self.NJ)
        for jj in np.nonzero(iny)[0]:
            for ii in np.nonzero(inx)[0]:
                j, i = ys[jj], xs[ii]
                if j in (0, self.NJ - 1) or i in (0, self.NI - 1):
                    self.far_H[jj, ii] = self.H[j, i]
        self.far_rect = (self.far_x[0], self.far_y[-1], self.far_x[-1], self.far_y[0])

    # -- queries ---------------------------------------------------------------
    def z_at(self, x, y):
        """Ground height at a point (bilinear on the lattice; the far ring outside)."""
        fx = (x / T - self.nx0) * S
        fy = (-y / T - self.ny0) * S
        if 0 <= fx <= self.NI - 1 and 0 <= fy <= self.NJ - 1:
            i0, j0 = min(int(fx), self.NI - 2), min(int(fy), self.NJ - 2)
            u, v = fx - i0, fy - j0
            H = self.H
            z = (H[j0, i0] * (1 - u) * (1 - v) + H[j0, i0 + 1] * u * (1 - v)
                 + H[j0 + 1, i0] * (1 - u) * v + H[j0 + 1, i0 + 1] * u * v)
            ty, tx = int(fy // S), int(fx // S)
            if 0 <= ty < self.NTy and 0 <= tx < self.NTx and self.tile_road[ty, tx]:
                z = -config.KERB_H_M + config.CROWN_M * 0.5
            return float(z)
        return float(self.relief.height(np.array([x]), np.array([y]))[0])

    def z_many(self, X, Y):
        """z_at over arrays: the lattice point by point, the far ring in one vectorised
        relief pass (it is ~10 ms per call one point at a time)."""
        X, Y = np.asarray(X, float).ravel(), np.asarray(Y, float).ravel()
        fx, fy = (X / T - self.nx0) * S, (-Y / T - self.ny0) * S
        inside = (fx >= 0) & (fx <= self.NI - 1) & (fy >= 0) & (fy <= self.NJ - 1)
        out = np.empty(len(X))
        for i in np.nonzero(inside)[0]:
            out[i] = self.z_at(X[i], Y[i])
        if (~inside).any():
            out[~inside] = self.relief.height(X[~inside], Y[~inside])
        return out

    def z_range(self, x0, y0, x1, y1, n=3):
        zs = [self.z_at(x0 + (x1 - x0) * a / (n - 1), y0 + (y1 - y0) * b / (n - 1))
              for a in range(n) for b in range(n)]
        return min(zs), max(zs)

    def road_samples(self):
        """The paved road's centreline samples, west -> east: list of dicts with x, y,
        s (arc length from the west end), zr (reference / kerb-top level), z (drawn
        reference, sunk at the far ends), urban, cut_n, cut_s."""
        r = self.routes
        out = []
        w = self.outs[0]
        for k in range(len(w["pts"]) - 1, 0, -1):     # west end first, towards town
            out.append(dict(x=w["pts"][k][0], y=w["pts"][k][1], zr=w["zr"][k], z=w["z"][k],
                            urban=w["urban"][k], cut_n=0.0, cut_s=0.0))
        xs = set(np.arange(r.road_x[0], r.road_x[1] + 1e-6, T).round(4))
        for side, x0, x1 in r.cuts:
            xs |= {round(x0 + config.CUT_TAPER_M, 4), round(x1 - config.CUT_TAPER_M, 4)}
        for x in sorted(xs):
            cn = cs = 0.0
            for side, x0, x1 in r.cuts:
                f = float(np.clip(min(x - x0, x1 - x) / config.CUT_TAPER_M, 0, 1))
                if side == "N":
                    cn = max(cn, f)
                else:
                    cs = max(cs, f)
            out.append(dict(x=x, y=r.road_y, zr=0.0, z=0.0, urban=1.0, cut_n=cn, cut_s=cs))
        e = self.outs[1]
        for k in range(1, len(e["pts"])):
            out.append(dict(x=e["pts"][k][0], y=e["pts"][k][1], zr=e["zr"][k], z=e["z"][k],
                            urban=e["urban"][k], cut_n=0.0, cut_s=0.0))
        s = arc_lengths([(p["x"], p["y"]) for p in out])
        for p, sv in zip(out, s):
            p["s"] = float(sv)
        return out

    def track_polyline(self, name, step=2.5):
        """(x, y, z) along a dirt track's centreline, on the ground."""
        t = self.routes.tracks[name]
        return [(x, y, self.z_at(x, y)) for x, y in densify(t.pts, step)]

    # -- mesh arrays -------------------------------------------------------------
    def _quads(self, tiles_mask):
        """Vertex-index quads + material indices for the selected near tiles."""
        NI = self.NI
        quads, mats = [], []
        ty, tx = np.nonzero(tiles_mask & ~self.fine)
        if len(ty):
            j, i = ty * S, tx * S
            q = np.stack([j * NI + i, (j + S) * NI + i, (j + S) * NI + i + S, j * NI + i + S], 1)
            quads.append(q)
            mats.append(self.tile_surf[ty, tx])
        ty, tx = np.nonzero(tiles_mask & self.fine)
        if len(ty):
            a, b = np.meshgrid(np.arange(S), np.arange(S), indexing="ij")
            j = (ty[:, None] * S + a.ravel()[None, :]).ravel()
            i = (tx[:, None] * S + b.ravel()[None, :]).ravel()
            q = np.stack([j * NI + i, (j + 1) * NI + i, (j + 1) * NI + i + 1, j * NI + i + 1], 1)
            quads.append(q)
            mats.append(self.surf_face[j, i])
        if not quads:
            return np.zeros((0, 4), np.int64), np.zeros(0, np.int16)
        return np.concatenate(quads), np.concatenate(mats)

    def _compact(self, quads):
        used, inv = np.unique(quads.ravel(), return_inverse=True)
        j, i = used // self.NI, used % self.NI
        X = (self.nx0 + i / S) * T
        Y = -(self.ny0 + j / S) * T
        verts = np.stack([X, Y, self.H[j, i]], 1)
        return verts, inv.reshape(-1, 4), {}

    def map_mesh(self, k):
        """(verts world metres, quads, material idx, attrs) for map k (road rows left
        to the road mesh)."""
        q, mats = self._quads((self.tile_map == k) & ~self.tile_road)
        verts, quads, attrs = self._compact(q)
        attrs["verge"] = self._verge(verts)     # the frame ring's woods where a road-out leaves
        return verts, quads, mats, attrs

    def terrain_mesh(self):
        """Everything outside the maps: near wild tiles + the far ring."""
        q, mats = self._quads(self.tile_map < 0)
        verts, quads, attrs = self._compact(q)
        # far ring: 5 m cells outside the near rect
        nx, ny = len(self.far_x), len(self.far_y)
        FX, FY = np.meshgrid(self.far_x, self.far_y)
        fverts = np.stack([FX.ravel(), FY.ravel(), self.far_H.ravel()], 1)
        x0, y0, x1, y1 = self.near_rect_m()
        cx = (self.far_x[:-1] + self.far_x[1:]) / 2
        cy = (self.far_y[:-1] + self.far_y[1:]) / 2
        CX, CY = np.meshgrid(cx, cy)
        keep = ~((CX > x0) & (CX < x1) & (CY > y0) & (CY < y1))
        jj, ii = np.nonzero(keep)
        fq = np.stack([jj * nx + ii, (jj + 1) * nx + ii, (jj + 1) * nx + ii + 1, jj * nx + ii + 1], 1)
        base = len(verts)
        verts = np.concatenate([verts, fverts])
        quads = np.concatenate([quads, fq + base])
        mats = np.concatenate([mats, np.full(len(fq), SURF[config.WILD_SURFACE], np.int16)])
        attrs["verge"] = self._verge(verts)
        return verts, quads, mats, attrs

    def _verge(self, verts):
        """Grassed verges along the road-outs: 1 near the road, 0 past config.VERGE_M."""
        v = np.zeros(len(verts))
        far = config.VERGE_M[1] + 2.0
        for o in self.relief.outs:
            m = ((verts[:, 0] > o[:, 0].min() - far) & (verts[:, 0] < o[:, 0].max() + far)
                 & (verts[:, 1] > o[:, 1].min() - far) & (verts[:, 1] < o[:, 1].max() + far))
            d = nearest_on(verts[m, 0], verts[m, 1], o)[0]
            v[m] = np.maximum(v[m], 1 - smoothstep(*config.VERGE_M, d))
        return v


if __name__ == "__main__":
    import time
    import routes as routes_mod
    import world as world_mod
    t0 = time.time()
    w = world_mod.build_world(sys.argv[1])
    t = Terrain(w, routes_mod.build_routes(w))
    print(f"terrain in {time.time() - t0:.1f}s; near {t.NTx}x{t.NTy} tiles, fine {t.fine.sum()}, "
          f"H {t.H.min():.2f}..{t.H.max():.2f}, far H {t.far_H.min():.1f}..{t.far_H.max():.1f}")
    v, q, m, a = t.terrain_mesh()
    print("terrain mesh", len(v), "verts", len(q), "quads")
    for k, mid in enumerate(t.map_ids):
        v, q, m, a = t.map_mesh(k)
        print(f"  {mid}: {len(q)} quads, z {v[:, 2].min():.3f}..{v[:, 2].max():.3f}")
    for o, n in zip(t.outs, "WE"):
        print(f"road-out {n}: zr end {o['zr'][-1]:.1f}, max grade "
              f"{np.max(np.abs(np.diff(o['zr']))) / config.ROAD_OUT_STEP_M:.3f}")
    print("mansion end", w and t.routes.mansion_end(), "z", t.z_at(*t.routes.mansion_end()))

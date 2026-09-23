"""The forest as numbers (numpy, no bpy): every tree, sapling, far clump, stump, boulder
and grass tuft the flyover plants, with its kit variant, size, turn and palette colours.
scatter.py turns the table into ONE point cloud that Geometry Nodes instances onto the
ground; trees.py builds the kit (config.FOREST_KIT order = the instance index).

The density field (everything derived from the dump via world / routes / terrain):
  open ground   the OPEN raster (config.FOREST_CELL_M over the near lattice, every guide
                and the clearing): map sub-cells that are not Woods, each guide's
                `clear_m` corridor (the road and road-outs 8 m, tracks 3.5 m; stamped
                conservatively, so no trunk stands inside one), building footprints +
                config.FOREST_BUILDING_MARGIN_M, prop / light / sign keep-outs, and the
                mansion clearing. `edge` = chamfer distance to open ground (capped).
  tiers         full-detail trees within FOREST_TIER_A_M of the world bounds or the
                road-outs, bigger sparser trees to FOREST_TIER_B_M, then cheap canopy
                clumps to the terrain's edge; the tiers cross-fade stochastically.
  the treeline  trunks stand back a noisy setback from open ground, trees are smaller
                near it and an understorey of saplings fills the band: irregular, not a
                hedge.
  species       stands, not salt-and-pepper: low-frequency noise picks conifer vs
                hardwood (more conifers uphill) and the hardwood mix; colours come from
                per-family palette weights (config.FOREST_CROWN) with a coherent pick.
  the mansion   a deep, dark band of tall hemlock (and dead snags) along the drive past
                its chain; TALL hemlock / pine ring the clearing at its end.
  exact         the farm's fixed-seed per-save sample (dump "sample": true): trees as
                the leafy / bare variants, stumps, rocks as boulders, at their tiles.
  dressing      grass tufts on the road verges, the drive-in's field edges and the open
                side of every treeline; a few boulders near the forest edge.

`python3 forest.py <world.json>` (Blender's bundled python, for numpy) prints the counts.
"""

import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import config  # noqa: E402
from terrain import chamfer, densify, nearest_on, smoothstep, vnoise  # noqa: E402

T = config.TILE_M
WOODS = config.SURFACES.index("Woods")
GREENS = [config.SURFACES.index(k) for k in ("Grass", "Pasture")]

# the kit: family -> (first instance index, variant count)
KIT, FAMILY = [], {}
for _fam, _n in config.FOREST_KIT:
    FAMILY[_fam] = (len(KIT), _n)
    KIT += [(_fam, k) for k in range(_n)]


def kit_names():
    return [f"FK_{i:02d}_{fam}_{'ABCDEFGH'[k]}" for i, (fam, k) in enumerate(KIT)]


def _rgba(names):
    lut = {n: config.hex_rgba(config.colour(n)) for n in set(names)}
    return np.array([lut[n] for n in names], np.float32).reshape(-1, 4)


# ---------------------------------------------------------------------------
# The open raster
# ---------------------------------------------------------------------------

class OpenRaster:
    """Open ground (no trunk may stand) and the distance into the woods from it."""

    def __init__(self, terrain, guides, clearing):
        c = self.c = config.FOREST_CELL_M
        self.terrain = terrain
        x0, y0, x1, y1 = terrain.near_rect_m()
        pts = np.concatenate([np.asarray(g, float) for g, _ in guides])
        pad = config.FOREST_EDGE_CAP_M + 10.0
        kx0 = int(math.ceil(max(0.0, x0 - (pts[:, 0].min() - pad)) / c))
        kx1 = int(math.ceil(max(0.0, pts[:, 0].max() + pad - x1) / c))
        ky0 = int(math.ceil(max(0.0, pts[:, 1].max() + pad - y1) / c))   # north
        ky1 = int(math.ceil(max(0.0, y0 - (pts[:, 1].min() - pad)) / c))
        self.x0, self.y1 = x0 - kx0 * c, y1 + ky0 * c
        self.W = kx0 + int(round((x1 - x0) / c)) + kx1
        self.H = ky0 + int(round((y1 - y0) / c)) + ky1
        # map surfaces: a cell (two sub-cells square) is open if any sub-cell is
        sub = (np.repeat(np.repeat(terrain.tile_map >= 0, config.SUB, 0), config.SUB, 1)
               & (terrain.surf_face != WOODS))
        k = int(round(c / (T / config.SUB)))
        agg = sub.reshape(sub.shape[0] // k, k, sub.shape[1] // k, k).any(axis=(1, 3))
        self.surf_open = np.zeros((self.H, self.W), bool)
        self.surf_open[ky0:ky0 + agg.shape[0], kx0:kx0 + agg.shape[1]] = agg
        self.keep = np.zeros((self.H, self.W), bool)   # guides, buildings, props, clearing
        for g, clear in guides:
            self._stamp(np.asarray(densify(g, 1.0)), clear + 0.9)
        self._keepouts(terrain.world)
        if clearing is not None:
            self._clearing(clearing)
        self.open = self.surf_open | self.keep
        self.edge = chamfer(self.open, config.FOREST_EDGE_CAP_M / c) * c
        # distance from open ground to the woods (the tufts' "open side of a treeline")
        self.to_woods = chamfer(~self.surf_open, 4.0 / c) * c

    # -- rasterising -----------------------------------------------------------
    def _centres(self, i, j):
        return self.x0 + (i + 0.5) * self.c, self.y1 - (j + 0.5) * self.c

    def _stamp(self, pts, radius):
        c = self.c
        i0 = np.floor((pts[:, 0] - self.x0) / c).astype(int)
        j0 = np.floor((self.y1 - pts[:, 1]) / c).astype(int)
        r = int(math.ceil(radius / c)) + 1
        for dj in range(-r, r + 1):
            for di in range(-r, r + 1):
                i, j = i0 + di, j0 + dj
                cx, cy = self._centres(i, j)
                ok = ((np.hypot(cx - pts[:, 0], cy - pts[:, 1]) <= radius)
                      & (i >= 0) & (i < self.W) & (j >= 0) & (j < self.H))
                self.keep[j[ok], i[ok]] = True

    def _rect(self, x0, y0, x1, y1, margin):
        """Cells whose centre lies in the rect grown by margin + half a cell diagonal."""
        m = margin + self.c * 0.7072
        i0 = max(0, int(math.floor((x0 - m - self.x0) / self.c)))
        i1 = min(self.W, int(math.ceil((x1 + m - self.x0) / self.c)))
        j0 = max(0, int(math.floor((self.y1 - (y1 + m)) / self.c)))
        j1 = min(self.H, int(math.ceil((self.y1 - (y0 - m)) / self.c)))
        if i0 >= i1 or j0 >= j1:
            return
        I, J = np.meshgrid(np.arange(i0, i1), np.arange(j0, j1))
        cx, cy = self._centres(I, J)
        inside = (cx >= x0 - m) & (cx <= x1 + m) & (cy >= y0 - m) & (cy <= y1 + m)
        self.keep[J[inside], I[inside]] = True

    def _keepouts(self, world):
        s = T / config.TILE_PX
        for m in world.maps.values():
            ox, oy = m.ox * T, -m.oy * T
            for b in m.data["buildings"]:
                self._rect(ox + b["x"] * T, oy - (b["y"] + b["h"]) * T, ox + (b["x"] + b["w"]) * T,
                           oy - b["y"] * T, config.FOREST_BUILDING_MARGIN_M)
            for p in m.data["props"]:
                if p["kind"] in config.GROUND_PROP_KINDS or p.get("sample"):
                    continue
                self._rect(ox + p["x"] * T, oy - (p["y"] + p["h"]) * T, ox + (p["x"] + p["w"]) * T,
                           oy - p["y"] * T, config.FOREST_PROP_MARGIN_M)
            for item in m.data["lights"] + m.data["signs"]:
                if item["kind"] == "glow":
                    continue
                cx, cy = ox + item["px"][0] * s, oy - item["px"][1] * s
                r = config.FOREST_POST_RADIUS_M
                self._rect(cx, cy, cx, cy, r)

    def _clearing(self, clearing):
        cx, cy, th, w, d = clearing
        R = math.hypot(w, d) / 2 + 2.0
        i0 = max(0, int((cx - R - self.x0) / self.c))
        j0 = max(0, int((self.y1 - (cy + R)) / self.c))
        I, J = np.meshgrid(np.arange(i0, min(self.W, i0 + int(2 * R / self.c) + 2)),
                           np.arange(j0, min(self.H, j0 + int(2 * R / self.c) + 2)))
        u, v = clearing_local(clearing, *self._centres(I, J))
        m = self.c * 0.7072
        inside = (np.abs(u) <= d / 2 + m) & (np.abs(v) <= w / 2 + m)
        self.keep[J[inside], I[inside]] = True

    # -- lookups -----------------------------------------------------------------
    def cells(self, x, y):
        i = np.floor((x - self.x0) / self.c).astype(int)
        j = np.floor((self.y1 - y) / self.c).astype(int)
        inside = (i >= 0) & (i < self.W) & (j >= 0) & (j < self.H)
        return np.clip(j, 0, self.H - 1), np.clip(i, 0, self.W - 1), inside

    def edge_at(self, x, y):
        """(distance into the woods from open ground, is-open) per point."""
        j, i, inside = self.cells(x, y)
        e = np.where(inside, self.edge[j, i], config.FOREST_EDGE_CAP_M)
        return e, inside & self.open[j, i]


def clearing_local(clearing, X, Y):
    """(u along the drive's heading, v across) from the clearing's centre."""
    cx, cy, th, _w, _d = clearing
    dx, dy = X - cx, Y - cy
    return dx * math.cos(th) + dy * math.sin(th), -dx * math.sin(th) + dy * math.cos(th)


def clearing_depth(clearing, X, Y):
    """Signed distance OUT of the clearing rect (negative inside)."""
    u, v = clearing_local(clearing, X, Y)
    _cx, _cy, _th, w, d = clearing
    ou, ov = np.abs(u) - d / 2, np.abs(v) - w / 2
    out = np.hypot(np.maximum(ou, 0), np.maximum(ov, 0))
    return np.where((ou <= 0) & (ov <= 0), np.maximum(ou, ov), out)


# ---------------------------------------------------------------------------
# Coarse fields: tier distance and elevation
# ---------------------------------------------------------------------------

class Coarse:
    def __init__(self, terrain):
        c = self.c = config.FOREST_FIELD_CELL_M
        fx0, fy0, fx1, fy1 = terrain.far_rect
        self.x0, self.y0 = fx0, fy0
        xs, ys = np.arange(fx0, fx1 + c, c), np.arange(fy0, fy1 + c, c)
        X, Y = np.meshgrid(xs, ys)
        gx0, gy0, gx1, gy1 = terrain.world.bounds
        rect = (gx0 * T, -gy1 * T, gx1 * T, -gy0 * T)
        dx = np.maximum(np.maximum(rect[0] - X, 0), X - rect[2])
        dy = np.maximum(np.maximum(rect[1] - Y, 0), Y - rect[3])
        d = np.hypot(dx, dy)
        for o in terrain.outs:
            d = np.minimum(d, nearest_on(X, Y, densify(list(map(tuple, o["pts"][::4])), 10.0))[0])
        self.dist = d
        self.elev = terrain.relief.hills(X, Y)

    def at(self, field, x, y):
        fx = np.clip((x - self.x0) / self.c, 0, field.shape[1] - 1.001)
        fy = np.clip((y - self.y0) / self.c, 0, field.shape[0] - 1.001)
        i, j = fx.astype(int), fy.astype(int)
        u, v = fx - i, fy - j
        return (field[j, i] * (1 - u) * (1 - v) + field[j, i + 1] * u * (1 - v)
                + field[j + 1, i] * (1 - u) * v + field[j + 1, i + 1] * u * v)


# ---------------------------------------------------------------------------
# The table
# ---------------------------------------------------------------------------

class Planting:
    """Column arrays, one row per instance: x, y, kind (kit index), scale (n, 3), rot,
    sink, crown / crown2 / bark (linear RGBA), plus `group` (a label for the counts)."""

    COLS = ("x", "y", "kind", "scale", "rot", "sink", "crown", "crown2", "bark", "group")

    def __init__(self):
        self.parts = []

    def add(self, **cols):
        n = len(cols["x"])
        if n:
            cols["group"] = np.full(n, cols["group"], object)
            self.parts.append(cols)

    def finish(self):
        for k in self.COLS:
            setattr(self, k, np.concatenate([p[k] for p in self.parts]))
        return self

    def counts(self):
        out = {}
        for g in self.group:
            out[g] = out.get(g, 0) + 1
        return out

    def kind_counts(self):
        names = kit_names()
        u, n = np.unique(self.kind, return_counts=True)
        return {names[i]: int(c) for i, c in zip(u, n)}


class Forest:
    def __init__(self, world, terrain):
        self.world, self.terrain, self.routes = world, terrain, terrain.routes
        self.rng = np.random.default_rng(config.FOREST_SEED)
        self.seed = config.FOREST_SEED
        r = self.routes
        self.guides = [(r.centreline(), config.CORRIDOR_CLEAR_M)]
        self.guides += [(t.pts, config.TRACK_CLEAR_M) for t in r.tracks.values()]
        self.clearing = r.mansion_clearing()
        self.raster = OpenRaster(terrain, self.guides, self.clearing)
        self.coarse = Coarse(terrain)
        drive = r.tracks.get("MansionDrive")
        self.drive = (np.asarray(densify(drive.pts[max(0, drive.extension_from - 4):], 4.0))
                      if drive is not None else None)
        self.table = Planting()
        self._trees()
        self._understorey()
        self._farm_sample()
        self._boulders()
        self._tufts()
        self.table.finish()

    # -- helpers ---------------------------------------------------------------
    def _jitter(self, rect, cell):
        x0, y0, x1, y1 = rect
        xs, ys = np.arange(x0, x1, cell), np.arange(y0, y1, cell)
        X, Y = np.meshgrid(xs, ys)
        X = X.ravel() + self.rng.random(X.size) * cell
        Y = Y.ravel() + self.rng.random(Y.size) * cell
        return X, Y

    def _noise(self, x, y, wl, k):
        return vnoise(x / wl, y / wl, self.seed + k)

    def _pick(self, lists, u):
        """Per point: a palette name from its (name, weight) list at quantile u."""
        out = np.empty(len(u), object)
        for key in set(map(id, lists)):
            sel = np.array([id(li) == key for li in lists])
            li = next(li for li in lists if id(li) == key)
            w = np.cumsum([wt for _n, wt in li])
            idx = np.searchsorted(w / w[-1], np.clip(u[sel], 0, 0.9999), side="right")
            out[sel] = np.array([n for n, _w in li], object)[idx]
        return out

    def _colours(self, fam, x, y, dark=None):
        """crown, crown2, bark RGBA arrays for families `fam` (array of str)."""
        n = len(fam)
        coh = self._noise(x, y, 55.0, 5)
        u = np.clip(0.55 * coh + 0.45 * self.rng.random(n) - 0.05 + 0.1 * self.rng.random(n), 0, 1)
        lists = [config.FOREST_CROWN.get(f, config.FOREST_CROWN["maple"]) for f in fam]
        if dark is not None:
            lists = [config.DARK_BAND_CROWN if dk and f in ("pine", "hemlock") else li
                     for li, f, dk in zip(lists, fam, dark)]
        crown = self._pick(lists, u)
        crown2 = np.array([config.FOREST_SHADE.get(c, c) for c in crown], object)
        rust = (fam == "maple") & (self.rng.random(n) < config.FOREST_RUST_P)
        crown[rust], crown2[rust] = config.RUST_CROWN
        if dark is not None:
            deep = dark & np.isin(fam, ("pine", "hemlock"))
            crown2[deep] = config.DARK_BAND_SHADE
        bark = self._pick([config.FOREST_BARK[f] for f in fam], self.rng.random(n))
        return _rgba(crown), _rgba(crown2), _rgba(bark)

    def _kinds(self, fam):
        out = np.zeros(len(fam), np.int32)
        for f in set(fam):
            sel = fam == f
            start, nv = FAMILY[f]
            out[sel] = start + self.rng.integers(0, nv, sel.sum())
        return out

    def _scale(self, fam, mult):
        n = len(fam)
        lo = np.array([config.SPECIES_SCALE[f][0] for f in fam])
        hi = np.array([config.SPECIES_SCALE[f][1] for f in fam])
        s = (lo + (hi - lo) * self.rng.random(n)) * mult
        wide = s * (1 + 0.08 * (self.rng.random(n) * 2 - 1))
        tall = s * (0.92 + 0.2 * self.rng.random(n))
        return np.stack([wide, s * (1 + 0.08 * (self.rng.random(n) * 2 - 1)), tall], 1).astype(np.float32)

    def _add(self, group, x, y, fam, mult, sink_key, dark=None):
        crown, crown2, bark = self._colours(fam, x, y, dark)
        scale = self._scale(fam, mult)
        sink = config.FOREST_SINK_M[sink_key] * (scale[:, 2] if sink_key in ("tree", "boulder")
                                                 else np.ones(len(x)))
        self.table.add(group=group, x=x, y=y, kind=self._kinds(fam), scale=scale,
                       rot=(self.rng.random(len(x)) * 2 * math.pi).astype(np.float32),
                       sink=sink.astype(np.float32), crown=crown, crown2=crown2, bark=bark)

    # -- species -----------------------------------------------------------------
    def _species(self, x, y, band, ring):
        n = len(x)
        elev = self.coarse.at(self.coarse.elev, x, y)
        e0, e1, up = config.CONIFER_UPHILL
        pc = (config.CONIFER_BASE + up * smoothstep(e0, e1, elev)
              + (self._noise(x, y, 150.0, 1) - 0.5) * config.CONIFER_PATCH)
        pc = pc + (config.BAND_CONIFER - pc) * band
        conifer = self.rng.random(n) < pc
        ph = 0.5 + (self._noise(x, y, 95.0, 2) - 0.5) * 0.9
        ph = ph + (config.BAND_HEMLOCK - ph) * band
        hem = self.rng.random(n) < ph
        n2, n3 = self._noise(x, y, 110.0, 3), self._noise(x, y, 70.0, 4)
        w = np.stack([0.42 * (0.5 + n2), 0.18 * (0.3 + 1.4 * n3), 0.26 * (1.5 - n2),
                      0.05 + (config.BAND_BARE - 0.05) * band], 1)
        cw = np.cumsum(w, 1)
        hard = np.array(["maple", "birch", "oak", "bare"], object)[
            (self.rng.random(n)[:, None] * cw[:, -1:] > cw).sum(1)]
        fam = np.where(conifer, np.where(hem, "hemlock", "pine"), hard).astype(object)
        if ring is not None and ring.any():
            fam[ring] = np.where(self.rng.random(ring.sum()) < config.RING_PINE, "pine", "hemlock")
        return fam

    def _band(self, x, y):
        if self.drive is None:
            return np.zeros(len(x))
        d = np.full(len(x), np.inf)
        r = config.BAND_M[1] + config.BAND_WOBBLE_M + 5
        near = ((x > self.drive[:, 0].min() - r) & (x < self.drive[:, 0].max() + r)
                & (y > self.drive[:, 1].min() - r) & (y < self.drive[:, 1].max() + r))
        if near.any():
            d[near] = nearest_on(x[near], y[near], self.drive)[0]
        d = d + (self._noise(x, y, 60.0, 9) - 0.5) * 2 * config.BAND_WOBBLE_M
        return 1 - smoothstep(*config.BAND_M, d)

    # -- plantings ---------------------------------------------------------------
    def _tier_weights(self, x, y):
        D = self.coarse.at(self.coarse.dist, x, y)
        wa = 1 - smoothstep(config.FOREST_TIER_A_M - config.FOREST_TIER_A_BLEND,
                            config.FOREST_TIER_A_M + config.FOREST_TIER_A_BLEND, D)
        wb = (1 - wa) * (1 - smoothstep(config.FOREST_TIER_B_M - config.FOREST_TIER_B_BLEND,
                                        config.FOREST_TIER_B_M + config.FOREST_TIER_B_BLEND, D))
        return wa, wb, 1 - wa - wb

    def _setback(self, x, y):
        lo, rng = config.FOREST_SETBACK_M
        return lo + rng * self._noise(x, y, 7.0, 6)

    def _trees(self):
        gx0, gy0, gx1, gy1 = self.world.bounds
        fx0, fy0, fx1, fy1 = self.terrain.far_rect
        ins = config.FOREST_EDGE_INSET_M
        full = (fx0 + ins, fy0 + ins, fx1 - ins, fy1 - ins)
        outs = np.concatenate([o["pts"] for o in self.terrain.outs])
        bx0, by0 = min(gx0 * T, outs[:, 0].min()), min(-gy1 * T, outs[:, 1].min())
        bx1, by1 = max(gx1 * T, outs[:, 0].max()), max(-gy0 * T, outs[:, 1].max())
        for tier, (cell, keep, mult), reach, idx in (
                ("A", config.FOREST_TIER_A, config.FOREST_TIER_A_M + config.FOREST_TIER_A_BLEND, 0),
                ("B", config.FOREST_TIER_B, config.FOREST_TIER_B_M + config.FOREST_TIER_B_BLEND, 1),
                ("C", config.FOREST_TIER_C, None, 2)):
            rect = full if reach is None else (max(full[0], bx0 - reach), max(full[1], by0 - reach),
                                                min(full[2], bx1 + reach), min(full[3], by1 + reach))
            x, y = self._jitter(rect, cell)
            inside = (x > full[0]) & (x < full[2]) & (y > full[1]) & (y < full[3])
            x, y = x[inside], y[inside]
            w = self._tier_weights(x, y)[idx]
            band = self._band(x, y)
            p = keep * w * (0.82 + 0.18 * self._noise(x, y, 45.0, 7)) * (1 + 0.15 * band)
            e, is_open = self.raster.edge_at(x, y)
            ring = np.zeros(len(x), bool)
            if self.clearing is not None:
                cd = clearing_depth(self.clearing, x, y)
                ring = (cd > config.RING_SETBACK_M) & (cd < config.RING_M) & (tier == "A")
                p = np.where(ring, 1.0, np.where(cd < config.RING_SETBACK_M, 0.0, p))
            ok = (self.rng.random(len(x)) < p) & ~is_open & ((e >= self._setback(x, y)) | ring)
            x, y, e, band, ring = x[ok], y[ok], e[ok], band[ok], ring[ok]
            f_edge = config.FOREST_EDGE_MIN + (1 - config.FOREST_EDGE_MIN) * smoothstep(
                0, config.FOREST_EDGE_GROW_M, e)
            f_edge = np.maximum(f_edge, 0.9 * band)
            m = mult * f_edge * (1 + (config.BAND_SCALE - 1) * band)
            if tier == "C":
                self._add("clump", x, y, np.full(len(x), "clump", object), m, "clump")
                self._clump_colours(x, y)
                continue
            fam = self._species(x, y, band, ring)
            lo, hi = config.RING_SCALE
            m = np.where(ring, lo + (hi - lo) * self.rng.random(len(x)), m)
            self._add(f"trees_{tier}", x, y, fam, m, "tree", dark=band > 0.5)

    def _clump_colours(self, x, y):
        """A far clump stands for a patch of forest: two tones from the local stand."""
        part = self.table.parts[-1]
        for col in ("crown", "crown2"):
            fam = self._species(x, y, np.zeros(len(x)), None)
            fam = np.where(fam == "bare", "oak", fam).astype(object)
            part[col] = self._colours(fam, x + (col == "crown2") * 37.0, y)[0]

    def _understorey(self):
        r = self.raster
        rect = (r.x0, r.y1 - r.H * r.c, r.x0 + r.W * r.c, r.y1)
        x, y = self._jitter(rect, config.UNDER_CELL_M)
        e, is_open = r.edge_at(x, y)
        sb = self._setback(x, y)
        into = e - sb * 0.5
        p = config.UNDER_KEEP * (1 - smoothstep(config.UNDER_BAND_M * 0.5, config.UNDER_BAND_M, into))
        p *= self._tier_weights(x, y)[0]
        if self.clearing is not None:     # the clearing's edge is the tall ring, not saplings
            p *= clearing_depth(self.clearing, x, y) > config.RING_M
        ok = ~is_open & (into >= 0) & (self.rng.random(len(x)) < p)
        x, y = x[ok], y[ok]
        fam = np.array(["maple", "hemlock", "birch", "oak"], object)[
            np.searchsorted([0.45, 0.7, 0.85, 1.0], self.rng.random(len(x)), side="right").clip(0, 3)]
        lo, hi = config.UNDER_SCALE
        self._add("understorey", x, y, fam, lo + (hi - lo) * self.rng.random(len(x)), "tree")

    def _farm_sample(self):
        for m in self.world.maps.values():
            props = [p for p in m.data["props"] if p.get("sample")]
            if not props:
                continue
            for kind in ("tree", "stump", "rock"):
                ps = [p for p in props if p["kind"] == kind]
                if not ps:
                    continue
                x = np.array([(m.ox + p["x"] + p["w"] / 2) * T for p in ps])
                y = np.array([-(m.oy + p["y"] + p["h"] / 2) * T for p in ps])
                if kind == "tree":
                    bare = np.array([p.get("variant") == "bare" for p in ps])
                    h = (np.floor(x * 7.1 + y * 3.7) % 3).astype(int)
                    fam = np.where(bare, "bare", np.array(["maple", "oak", "maple"], object)[h])
                    lo, hi = config.FARM_TREE_SCALE
                    mult = np.where(bare, config.FARM_BARE_SCALE, lo + (hi - lo) * self.rng.random(len(x)))
                    self._add("farm_sample", x, y, fam.astype(object), mult, "tree")
                elif kind == "stump":
                    self._add("farm_sample", x, y, np.full(len(x), "stump", object), 1.0, "stump")
                else:
                    self._add("farm_sample", x, y, np.full(len(x), "boulder", object), 1.0, "boulder")

    def _boulders(self):
        r = self.raster
        rect = (r.x0, r.y1 - r.H * r.c, r.x0 + r.W * r.c, r.y1)
        x, y = self._jitter(rect, config.BOULDER_CELL_M)
        e, is_open = r.edge_at(x, y)
        p = config.BOULDER_KEEP * (1 - smoothstep(4.0, 25.0, e)) * self._tier_weights(x, y)[0]
        ok = ~is_open & (e >= 1.0) & (self.rng.random(len(x)) < p)
        self._add("boulders", x[ok], y[ok], np.full(ok.sum(), "boulder", object), 1.0, "boulder")

    # -- tufts -------------------------------------------------------------------
    def _surface_at(self, x, y):
        t = self.terrain
        fi = np.floor((x / T - t.nx0) * config.SUB).astype(int)
        fj = np.floor((-y / T - t.ny0) * config.SUB).astype(int)
        inside = (fi >= 0) & (fi < t.surf_face.shape[1]) & (fj >= 0) & (fj < t.surf_face.shape[0])
        s = np.full(len(x), WOODS)
        s[inside] = t.surf_face[fj[inside], fi[inside]]
        return s

    def _tuft_ok(self, x, y):
        """On grass / pasture / forest floor, clear of buildings, props and the roads' own
        surfaces (the stamped keep-out is the clear_m corridor, so it is not used)."""
        s = self._surface_at(x, y)
        green = np.isin(s, GREENS + [WOODS])
        r = self.raster
        j, i, inside = r.cells(x, y)
        return green & ~(inside & r.surf_open[j, i] & ~np.isin(s, GREENS))

    def _building_free(self, x, y):
        ok = np.ones(len(x), bool)
        for m in self.world.maps.values():
            ox, oy = m.ox * T, -m.oy * T
            for b in m.data["buildings"] + [p for p in m.data["props"]
                                            if p["kind"] not in config.GROUND_PROP_KINDS]:
                x0, x1 = ox + b["x"] * T - 0.5, ox + (b["x"] + b["w"]) * T + 0.5
                y1, y0 = oy - b["y"] * T + 0.5, oy - (b["y"] + b["h"]) * T - 0.5
                ok &= ~((x > x0) & (x < x1) & (y > y0) & (y < y1))
        return ok

    def _tufts(self):
        xs, ys = [], []
        # road-out verges (past the shoulder, inside the tree clearance)
        for o in self.terrain.outs:
            p = np.asarray(densify(list(map(tuple, o["pts"])), config.TUFT_ROAD_STEP_M))
            tan = np.gradient(p, axis=0)
            tan /= np.linalg.norm(tan, axis=1)[:, None]
            left = np.stack([-tan[:, 1], tan[:, 0]], 1)
            for side in (-1, 1):
                v0, vr = config.TUFT_ROAD_V_M
                v = side * (v0 + vr * self.rng.random(len(p)))
                q = p + left * v[:, None] + tan * (self.rng.random(len(p)) - 0.5)[:, None]
                keep = self.rng.random(len(p)) < config.TUFT_ROAD_KEEP * (
                    0.4 + 0.9 * self._noise(q[:, 0], q[:, 1], 12.0, 8))
                xs.append(q[keep, 0])
                ys.append(q[keep, 1])
        # in-town verges along the road rows
        r = self.routes
        n = int((r.road_x[1] - r.road_x[0]) / 0.9)
        for side in (-1, 1):
            x = r.road_x[0] + self.rng.random(n) * (r.road_x[1] - r.road_x[0])
            v0, vr = config.TUFT_TOWN_V_M
            y = r.road_y + side * (v0 + vr * self.rng.random(n))
            keep = self.rng.random(n) < config.TUFT_TOWN_KEEP
            xs.append(x[keep])
            ys.append(y[keep])
        # the open side of every treeline
        R = self.raster
        rect = (R.x0, R.y1 - R.H * R.c, R.x0 + R.W * R.c, R.y1)
        x, y = self._jitter(rect, config.TUFT_EDGE_CELL_M)
        j, i, inside = R.cells(x, y)
        dw = np.where(inside, R.to_woods[j, i], 99.0)
        p = config.TUFT_EDGE_KEEP * (1 - smoothstep(0.5, config.TUFT_EDGE_M, dw))
        keep = inside & R.surf_open[j, i] & (self.rng.random(len(x)) < p)
        xs.append(x[keep])
        ys.append(y[keep])
        # the drive-in's field edges
        m = self.world.maps.get(config.TUFT_FIELD_MAP)
        if m is not None:
            self._field_tufts(m, xs, ys)
        x, y = np.concatenate(xs), np.concatenate(ys)
        ok = self._tuft_ok(x, y) & self._building_free(x, y)
        x, y = x[ok], y[ok]
        self._add("tufts", x, y, np.full(len(x), "tuft", object), 1.0, "tuft")

    def _field_tufts(self, m, xs, ys):
        t = self.terrain
        S = config.SUB
        j0, i0 = (m.oy - t.ny0) * S, (m.ox - t.nx0) * S
        sub = t.surf_face[j0:j0 + m.height * S, i0:i0 + m.width * S]
        grass = sub == config.SURFACES.index("Grass")
        dist = chamfer(~grass, config.TUFT_FIELD_M / (T / S)) * (T / S)
        x0, y1 = m.ox * T, -m.oy * T
        x, y = self._jitter((x0, y1 - m.height * T, x0 + m.width * T, y1), config.TUFT_FIELD_CELL_M)
        fi = np.clip(((x - x0) / (T / S)).astype(int), 0, grass.shape[1] - 1)
        fj = np.clip(((y1 - y) / (T / S)).astype(int), 0, grass.shape[0] - 1)
        d = dist[fj, fi]
        # the field's edges: grass next to the lot / drive (not the woods ring)
        lot = chamfer(np.isin(sub, [config.SURFACES.index(k) for k in ("Asphalt", "Dirt")]),
                      config.TUFT_FIELD_M / (T / S)) * (T / S)
        near_lot = lot[fj, fi] < config.TUFT_FIELD_M
        p = config.TUFT_FIELD_KEEP * (1 - smoothstep(0.3, config.TUFT_FIELD_M, d)) * near_lot + 0.03
        keep = grass[fj, fi] & (self.rng.random(len(x)) < p)
        xs.append(x[keep])
        ys.append(y[keep])


def check(forest):
    """The keep-outs, verified on the finished table (exact geometry, not the raster):
    returns a list of violation strings (empty = clean). Trunks (every group but the
    farm sample and tufts) must stand on Woods (in a map) or wild ground, at least
    clear_m from every guide, clear of building footprints + margin and the clearing."""
    tb, bad = forest.table, []
    trunk = ~np.isin(tb.group, ["farm_sample", "tufts"])
    x, y = tb.x[trunk], tb.y[trunk]
    for g, clear in forest.guides:
        gp = np.asarray(densify(g, 2.0))
        near = ((x > gp[:, 0].min() - clear) & (x < gp[:, 0].max() + clear)
                & (y > gp[:, 1].min() - clear) & (y < gp[:, 1].max() + clear))
        d = nearest_on(x[near], y[near], np.asarray(g, float))[0]
        if (d < clear).any():
            bad.append(f"{(d < clear).sum()} trunks within {clear} m of a guide (min {d.min():.2f})")
    s = forest._surface_at(x, y)
    if (s != WOODS).any():
        bad.append(f"{(s != WOODS).sum()} trunks on open map surfaces")
    for m in forest.world.maps.values():
        ox, oy = m.ox * T, -m.oy * T
        mg = config.FOREST_BUILDING_MARGIN_M
        for b in m.data["buildings"]:
            inb = ((x > ox + b["x"] * T - mg) & (x < ox + (b["x"] + b["w"]) * T + mg)
                   & (y < oy - b["y"] * T + mg) & (y > oy - (b["y"] + b["h"]) * T - mg))
            if inb.any():
                bad.append(f"{inb.sum()} trunks within {mg} m of {m.id}/{b['id']}")
    if forest.clearing is not None:
        inside = clearing_depth(forest.clearing, x, y) < 0
        if inside.any():
            bad.append(f"{inside.sum()} trunks in the mansion clearing")
    return bad


def plant(world, terrain):
    f = Forest(world, terrain)
    bad = check(f)
    if bad:
        raise AssertionError("forest keep-outs violated: " + "; ".join(bad))
    return f


if __name__ == "__main__":
    import time
    import routes as routes_mod
    import terrain as terrain_mod
    import world as world_mod
    t0 = time.time()
    w = world_mod.build_world(sys.argv[1])
    terr = terrain_mod.Terrain(w, routes_mod.build_routes(w))
    t1 = time.time()
    f = plant(w, terr)
    tb = f.table
    print(f"terrain {t1 - t0:.1f}s, forest {time.time() - t1:.1f}s: {len(tb.x)} instances")
    print("raster", f.raster.W, "x", f.raster.H, "open", int(f.raster.open.sum()))
    print("groups", tb.counts())
    print("kinds", tb.kind_counts())
    print("clearing", f.clearing)

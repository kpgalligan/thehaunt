"""The ground's surfaces as smooth fields (numpy, no bpy): the tile layout places every
surface, the fields give it a natural shape. terrain.py samples them for the ground's
features, forest.py for the open ground, materials.py paints them (three float
images, one ground shader).

The partition: every surface kind k (config.SURF_KINDS, Woods the floor, the paved road
rows on top) gets a field s_k, a signed distance in metres (+ inside, clamped at
config.SURF_CAP_M) to its own tiles, processed; a point shows the kind whose s_k is
highest, so there are no gaps and no slivers, only a smooth boundary where two
fields cross.

  exact     the tile distance is exact (per pixel, to every tile square within three
            tiles), so a built edge is a straight line;
  tracks    the traced 2-tile dirt tracks are not tiles but a band of config.TRACK_HALF_M
            around the filleted centreline (routes.fillet), forest continuations
            included (narrowing and grassing over at the end); the band is Dirt and
            carves every other kind; a track tile the band leaves goes to its nearest
            neighbour (a bend's outer corner fills in from both sides);
  rounding  a Gaussian blur per kind rounds corners and leaves straight edges;
  natural   each natural kind's own value-noise wobble (zero for the built kinds,
            config.SURF_BUILT, and calmed next to them, so lots, walks, the plaza and
            the kerb line keep clean straight edges).

Layers: the shader paints bottom -> top in SURF_KINDS order; layer L's channel is
U_L = (max over kinds >= L of s - max over kinds < L of s) / 2, so the top layer with
U_L > 0 IS the argmax, and U_L is the distance to that layer's edge. Channels hold
U for Grass .. Cobble (Woods is the floor, the road rows are under the road mesh).

The treeline (treeline.Treeline, Phase 4c) decides woods vs open instead of the tiles:
Woods' field is -E, Grass takes every open point the tiles call Woods (bays, the plain
frame ring) and gives up every wooded one (tongues); built kinds are untouched (E is
forced open over them), and kept woods are exactly the tiles'.

Extra channels: `td` (distance to the nearest track centreline, for ruts and the
grassy crown), `fade` (a track's grass-over at its forest end), `litter` (1 on the
forest floor and under the farm's and the field's lone trees, fading over
config.LITTER_M onto open ground).

Raster: config.SURF_PX_M pixels over the terrain's near rect (row 0 = north). The
images store the same values; Blender's image rows run bottom-up (images() flips).
"""

import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import config  # noqa: E402
from terrain import densify, fbm, nearest_on, smoothstep  # noqa: E402

T = config.TILE_M
KINDS = [name for name, *_ in config.SURF_KINDS]
LAYERS = KINDS[1:-1]              # the painted channels (floor and road rows excluded)
SOFT = {name: soft for name, _s, _a, soft in config.SURF_KINDS}
SURF = {k: i for i, k in enumerate(config.SURFACES)}
# image packing: 3 RGBA float images
CHANNELS = LAYERS + ["td", "fade", "litter", "spare"]


def _blur(a, sigma_px):
    """Gaussian-ish blur: three box passes per axis (edge-padded)."""
    if sigma_px <= 0.3:
        return a
    w = int(round(math.sqrt(12.0 * sigma_px * sigma_px / 3.0 + 1.0)))
    w += 1 - w % 2
    r = w // 2
    out = a
    for axis in (0, 1):
        for _ in range(3):
            pad = [(0, 0), (0, 0)]
            pad[axis] = (r + 1, r)
            p = np.pad(out, pad, mode="edge")
            c = np.cumsum(p, axis=axis, dtype=np.float64)
            if axis == 0:
                out = ((c[w:] - c[:-w]) / w).astype(np.float32)
            else:
                out = ((c[:, w:] - c[:, :-w]) / w).astype(np.float32)
    return out


class Surfaces:
    def __init__(self, world, routes, tile_layer_src, nx0, ny0, treeline=None):
        """tile_layer_src: (NTy, NTx) config.SURFACES index per near tile, -1 = wild.
        treeline: treeline.Treeline (the clearings' edge), or None for the tiles' own."""
        P = self.P = config.SURF_PX_M
        K = self.K = int(round(T / P))
        if abs(K * P - T) > 1e-9:
            raise ValueError("SURF_PX_M must divide the tile")
        self.world, self.routes = world, routes
        NTy, NTx = tile_layer_src.shape
        self.x0, self.y1 = nx0 * T, -ny0 * T           # north-west corner
        self.W, self.H = NTx * K, NTy * K
        self.nx0, self.ny0 = nx0, ny0
        xs = self.x0 + (np.arange(self.W) + 0.5) * P
        ys = self.y1 - (np.arange(self.H) + 0.5) * P
        self.X, self.Y = np.meshgrid(xs.astype(np.float32), ys.astype(np.float32))
        track_tiles = np.zeros((NTy, NTx), bool)
        for t in routes.tracks.values():
            for gx, gy in t.tiles:
                track_tiles[gy - ny0, gx - nx0] = True
        self._table = self._dist_table()
        band, td, fade = self._tracks()
        built = np.isin(tile_layer_src, [SURF[k] for k in config.SURF_BUILT])
        calm = smoothstep(*config.SURF_BUILT_CALM_M, -self._tile_sdf(built)).astype(np.float32)
        E = None
        if treeline is not None:
            E = np.clip(treeline.at(self.X, self.Y), -config.SURF_CAP_M,
                        config.SURF_CAP_M).astype(np.float32)
        self.field_trees = treeline.isles if treeline is not None else (np.zeros(0), np.zeros(0))
        fields = []
        for ki, (name, sigma, amp, _soft) in enumerate(config.SURF_KINDS):
            mask = (tile_layer_src == SURF[name]) & ~track_tiles
            f = self._tile_sdf(mask)
            if E is not None and name == "Woods":       # the treeline decides woods / open
                f_woods, f = f, -E
            elif E is not None and name == "Grass":     # open ground the tiles call woods is grass
                f = np.minimum(np.maximum(f, f_woods), E)
                del f_woods
            f = np.maximum(f, band) if name == "Dirt" else np.minimum(f, -band)
            if name == "Road":      # the rows win inside, never outside (the road mesh is there)
                f = np.where(f > 0, f, -config.SURF_CAP_M)
            f = _blur(f, sigma / P)
            if amp:
                n = fbm(self.X, self.Y, config.SURF_NOISE_WAVES_M, config.TERRAIN_SEED + 31 + 7 * ki)
                f = f + amp * n.astype(np.float32) * calm
            fields.append(f)
        fields = np.stack(fields)
        # layer channels: (max above - max below) / 2
        below = np.maximum.accumulate(fields, axis=0)
        above = np.maximum.accumulate(fields[::-1], axis=0)[::-1]
        self.U = {}
        for li, name in enumerate(KINDS):
            if name in LAYERS:
                u = (above[li] - below[li - 1]) / 2
                self.U[name] = np.clip(u, -config.SURF_CAP_M, config.SURF_CAP_M).astype(np.float32)
        del fields, below, above
        self.td, self.fade = td, fade
        self.litter = self._litter()

    # -- exact tile-union distances --------------------------------------------
    def _dist_table(self):
        """D[dj, di] (K, K): metres from each pixel centre of a tile to the square of the
        tile at offset (di east, dj south), for |offsets| <= 3."""
        K, R = self.K, 3
        u = (np.arange(K) + 0.5) / K
        tab = {}
        for dj in range(-R, R + 1):
            for di in range(-R, R + 1):
                dx = np.maximum(np.maximum(di - u, u - (di + 1)), 0.0)       # columns (x)
                dy = np.maximum(np.maximum(dj - u, u - (dj + 1)), 0.0)       # rows (south)
                tab[(dj, di)] = (np.hypot(dy[:, None], dx[None, :]) * T).astype(np.float32)
        return tab

    def _tile_sdf(self, mask):
        NTy, NTx = mask.shape
        K = self.K
        inf = np.float32(1e9)
        d_out = np.full((NTy, NTx, K, K), inf, np.float32)
        d_in = np.full((NTy, NTx, K, K), inf, np.float32)
        for (dj, di), D in self._table.items():
            sh = np.zeros_like(mask)       # sh[j, i] = mask[j + dj, i + di] (outside: not member)
            js, je = max(0, -dj), min(NTy, NTy - dj)
            is_, ie = max(0, -di), min(NTx, NTx - di)
            sh[js:je, is_:ie] = mask[js + dj:je + dj, is_ + di:ie + di]
            comp = ~sh                     # outside the lattice is not a member either
            np.minimum(d_out, np.where(sh[:, :, None, None], D[None, None], inf), out=d_out)
            np.minimum(d_in, np.where(comp[:, :, None, None], D[None, None], inf), out=d_in)
        m = mask[:, :, None, None]
        sdf = np.where(m, d_in, -d_out)
        sdf = sdf.transpose(0, 2, 1, 3).reshape(NTy * K, NTx * K)
        return np.clip(sdf, -config.SURF_CAP_M, config.SURF_CAP_M).astype(np.float32)

    # -- tracks ------------------------------------------------------------------
    def _tracks(self):
        cap = config.SURF_CAP_M
        band = np.full((self.H, self.W), -cap, np.float32)
        td = np.full((self.H, self.W), cap, np.float32)
        fade = np.zeros((self.H, self.W), np.float32)
        self.centrelines = {}
        for name, t in self.routes.tracks.items():
            pts = [tuple(p) for p in t.pts]
            # start under the road so the band meets the kerb square
            (ax, ay), (bx, by) = pts[0], pts[1]
            L = math.hypot(bx - ax, by - ay) or 1.0
            o = config.TRACK_ROAD_OVERLAP_M
            pts = [(ax - (bx - ax) / L * o, ay - (by - ay) / L * o)] + pts
            p = np.asarray(densify(pts, 0.5), float)
            s = np.concatenate([[0.0], np.cumsum(np.hypot(*np.diff(p, axis=0).T))])
            if t.extension_from >= 0:
                f = smoothstep(s[-1] - config.TRACK_FADE_M, s[-1], s)
            else:
                f = np.zeros(len(s))
            hw = config.TRACK_HALF_M * (1 - 0.55 * f)
            self.centrelines[name] = p
            r = config.TRACK_HALF_M + cap
            i0 = max(0, int((p[:, 0].min() - r - self.x0) / self.P))
            i1 = min(self.W, int((p[:, 0].max() + r - self.x0) / self.P) + 1)
            j0 = max(0, int((self.y1 - p[:, 1].max() - r) / self.P))
            j1 = min(self.H, int((self.y1 - p[:, 1].min() + r) / self.P) + 1)
            if i0 >= i1 or j0 >= j1:
                continue
            X, Y = self.X[j0:j1, i0:i1], self.Y[j0:j1, i0:i1]
            d, v = nearest_on(X, Y, p, np.stack([hw, f, s], 1))
            b = v[..., 0] - d
            if t.extension_from < 0:     # an in-map end stops square where it meets a surface
                (cx, cy), (ex, ey) = p[-2], p[-1]
                L = math.hypot(ex - cx, ey - cy) or 1.0
                along = ((X - ex) * (ex - cx) + (Y - ey) * (ey - cy)) / L
                b = np.where(along > 0, np.minimum(b, -along), b)
            # past either end (a round cap) there are no ruts: the track stops, it
            # does not turn round
            d = np.where((v[..., 2] < 1e-3) | (v[..., 2] > s[-1] - 1e-3), cap, d)
            sub = (slice(j0, j1), slice(i0, i1))
            closer = d < td[sub]
            band[sub] = np.maximum(band[sub], b)
            fade[sub] = np.where(closer, v[..., 1], fade[sub])
            td[sub] = np.minimum(td[sub], d)
        return band, np.clip(td, 0, cap), fade

    # -- litter ------------------------------------------------------------------
    def _litter(self):
        lit = 1 - smoothstep(0.0, config.LITTER_M, self.U[LAYERS[0]])
        trees = [((m.ox + p["x"] + 0.5) * T, -(m.oy + p["y"] + 0.5) * T)
                 for m in self.world.maps.values() for p in m.data["props"] if p["kind"] == "tree"]
        trees += list(zip(*self.field_trees))       # the treeline's lone trees and clumps
        for cx, cy in trees:
            i0 = max(0, int((cx - 8 - self.x0) / self.P))
            j0 = max(0, int((self.y1 - cy - 8) / self.P))
            sub = (slice(j0, j0 + int(16 / self.P)), slice(i0, i0 + int(16 / self.P)))
            d = np.hypot(self.X[sub] - cx, self.Y[sub] - cy)
            lit[sub] = np.maximum(lit[sub], 1 - smoothstep(1.5, config.LITTER_M, d))
        return lit.astype(np.float32)

    # -- sampling (bilinear on pixel centres, like the images' linear filter) ------
    def _bilinear(self, a, x, y):
        x, y = np.asarray(x, float), np.asarray(y, float)
        fx = np.clip((x - self.x0) / self.P - 0.5, 0, self.W - 1.0001)
        fy = np.clip((self.y1 - y) / self.P - 0.5, 0, self.H - 1.0001)
        i, j = fx.astype(int), fy.astype(int)
        u, v = fx - i, fy - j
        return (a[j, i] * (1 - u) * (1 - v) + a[j, i + 1] * u * (1 - v)
                + a[j + 1, i] * (1 - u) * v + a[j + 1, i + 1] * u * v)

    def inside(self, x, y):
        x, y = np.asarray(x, float), np.asarray(y, float)
        return ((x >= self.x0) & (x <= self.x0 + self.W * self.P)
                & (y <= self.y1) & (y >= self.y1 - self.H * self.P))

    def field(self, name, x, y):
        """A channel at points; outside the raster: the floor's values."""
        a = self.U.get(name)
        if a is None:
            a = {"td": self.td, "fade": self.fade, "litter": self.litter}[name]
        out = self._bilinear(a, x, y)
        outside = {"td": config.SURF_CAP_M, "fade": 0.0, "litter": 1.0}.get(name, -config.SURF_CAP_M)
        return np.where(self.inside(x, y), out, outside)

    def own(self, name, x, y):
        """Signed distance into the part of layer `name` no higher layer covers."""
        li = LAYERS.index(name)
        u = self.field(name, x, y)
        if li + 1 < len(LAYERS):
            u = np.minimum(u, -self.field(LAYERS[li + 1], x, y))
        return u

    def open_field(self, x, y):
        """Signed distance out of the Woods floor (+ on open ground)."""
        return self.field(LAYERS[0], x, y)

    def visible(self, x, y):
        """config.SURFACES index of the surface showing at each point (Woods = floor)."""
        x = np.asarray(x, float)
        out = np.full(x.shape, SURF["Woods"], np.int16)
        for name in LAYERS:
            out = np.where(self.field(name, x, y) > 0, SURF[name], out)
        return out

    # -- images --------------------------------------------------------------------
    def images(self):
        """[(name, (H, W, 4) float32 bottom-up rows)] for the three packed images."""
        chans = dict(self.U)
        chans.update(td=self.td, fade=self.fade, litter=self.litter,
                     spare=np.zeros_like(self.td))
        out = []
        for k in range(0, len(CHANNELS), 4):
            names = CHANNELS[k:k + 4]
            a = np.stack([chans[n] for n in names], -1)[::-1]
            out.append((f"FO_Surfaces_{k // 4}", np.ascontiguousarray(a, np.float32)))
        return out

    def rect(self):
        """(x0, y0, x1, y1) metres the images cover (y0 south)."""
        return (self.x0, self.y1 - self.H * self.P, self.x0 + self.W * self.P, self.y1)

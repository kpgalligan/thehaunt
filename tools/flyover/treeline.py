"""The clearings' treeline as a field (numpy, no bpy): where the town's open ground ends
and the forest begins. The game's maps are rectangles, so the plain frame of every
clearing is ruler-straight; this field makes it a New England valley's edge instead:
bays of field cutting back into the forest, tongues of trees reaching toward the road,
scallops at every scale, and a few lone trees and small clumps out in the grass.

E (metres, + open, - wooded) on a config.TREELINE_CELL_M grid over the terrain's near
lattice, everything derived from the dump:

  clearings   connected open in-map ground (every non-Woods tile but the dirt tracks),
              each with a scale from its area (the town strip 1, the farm and the
              drive-in smaller: their bays and tongues are smaller and shorter);
  the edge    E_c = signed distance to clearing c + its scaled multi-octave noise
              (config.TREELINE_WAVES_M: bays tens of metres deep down to a few-metre
              wobble); E = max over the clearings, kept config.TREELINE_SEP_M apart
              wherever two clearings' bays would meet (the woods between stay woods);
  used        everything the game places or uses is forced open (a smooth max, so a
              tongue's tip rounds off): non-grass tiles (the road rows with the road's
              tree clearance), building footprints + a margin, swept toward their doors
              and the road, props, posts, sign faces (a corridor to the nearest route),
              the drive-in's screen sightline;
  kept woods  the in-map Woods the game draws as tree lines stay wooded, exactly as
              the tiles say (a hard min): every Woods tile inside a map's frame ring,
              the ring's N/S band where a band of woods lies behind it or where it meets
              another map's woods (the farm's south treeline), the whole ring of the maps
              in config.TREELINE_KEEP_RING (the drive-in), plus the wild ground within a
              noisy config.TREELINE_KEEP_PAD_M of those (no field behind a tree band).

Only the plain frame gets irregular. surfaces.Surfaces turns E into the Woods / Grass
partition (so trunks, understorey, litter and tufts follow it); forest.Forest plants
the lone trees and clumps (`field_trees`) on the grass, each with a litter halo.

`python3 treeline.py <world.json> [out.png]` (Blender's python, for numpy) prints the
stats and writes a preview (green open, dark woods, red used, blue kept woods).
"""

import math
import os
import sys
from collections import deque

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import config  # noqa: E402
from terrain import chamfer, densify, nearest_on, vnoise  # noqa: E402

T = config.TILE_M
SURF = {k: i for i, k in enumerate(config.SURFACES)}


def smax(a, b, k):
    """Smooth maximum (polynomial, radius k): >= max(a, b), equal once they differ by k."""
    h = np.clip(0.5 + 0.5 * (a - b) / k, 0.0, 1.0)
    return b + (a - b) * h + k * h * (1 - h) * 0.5


def smin(a, b, k):
    return -smax(-a, -b, k)


def _noise(x, y, waves, scale, seed):
    """Sum of value-noise octaves in metres ((wavelength m, out amp m, in amp m), lengths
    * scale): a positive lobe pushes the field out (a bay), a negative one in (a tongue)."""
    tot = 0.0
    for k, (wl, out, into) in enumerate(waves):
        w = wl * scale
        n = 2 * vnoise(x / w + 31.7 * k, y / w - 12.3 * k, seed + 5 * k) - 1
        tot = tot + scale * np.where(n > 0, out * n, into * n)
    return tot


class Treeline:
    def __init__(self, world, routes, tile_surf, tile_map, nx0, ny0):
        c = self.c = config.TREELINE_CELL_M
        K = self.K = int(round(T / c))
        if abs(K * c - T) > 1e-9:
            raise ValueError("TREELINE_CELL_M must divide the tile")
        self.world, self.routes = world, routes
        NTy, NTx = tile_surf.shape
        self.nx0, self.ny0 = nx0, ny0
        self.x0, self.y1 = nx0 * T, -ny0 * T
        self.W, self.H = NTx * K, NTy * K
        xs = self.x0 + (np.arange(self.W) + 0.5) * c
        ys = self.y1 - (np.arange(self.H) + 0.5) * c
        self.X, self.Y = np.meshgrid(xs, ys)
        self.seed = config.TREELINE_SEED
        up = lambda a: np.repeat(np.repeat(a, K, 0), K, 1)   # noqa: E731  tiles -> cells

        inmap = tile_map >= 0
        woods = tile_surf == SURF["Woods"]
        track = np.zeros_like(inmap)
        for t in routes.tracks.values():
            for gx, gy in t.tiles:
                track[gy - ny0, gx - nx0] = True
        self.kept_tiles = self._kept(woods & inmap)
        comps = self._components(inmap & ~woods & ~track)
        self.U = self._used(tile_surf, inmap, woods, track, up)

        # each clearing's edge: signed distance + its scaled noise
        cap_in, cap_out = config.TREELINE_DIST_CAP_M
        areas = [m.sum() * T * T for m in comps]
        a_max = max(areas)
        Es, Ds = [], []
        for k, (mask, area) in enumerate(zip(comps, areas)):
            cm = up(mask)
            d = np.where(cm, chamfer(~cm, cap_in / c) * c - c / 2,
                         -(chamfer(cm, cap_out / c) * c - c / 2))
            s = float(np.clip((area / a_max) ** config.TREELINE_SCALE_POW, *config.TREELINE_SCALE_CLAMP))
            n = _noise(self.X, self.Y, config.TREELINE_WAVES_M, s, self.seed + 101 * k)
            # where the game uses the clearing right up to its frame (the farm's field),
            # tongues cannot come in: the edge scallops outward instead
            f = self._used_edge(mask, tile_surf)
            n = n * (1 - f) + f * np.sqrt(n * n + 4.0)
            if self._keeps_ring(mask):        # a kept ring: no field behind it
                n = np.minimum(n, 0.0)
            else:                             # bays saturate softly (no flat bottoms)
                b = config.TREELINE_BAY_MAX_M * s * (0.55 + 0.9 * vnoise(
                    self.X / 55.0, self.Y / 55.0, self.seed + 3 + k))
                n = np.where(n > 0, b * np.tanh(n / b), n)
            # so do tongues, within the room between the frame and the used ground: their
            # tips end short of it, rounded, never all cut along one line by the road
            room = np.maximum(d - self.U, 1.0)
            t = np.minimum(config.TREELINE_TONGUE_MAX_M * s, config.TREELINE_TONGUE_ROOM * room)
            wl = config.TREELINE_TONGUE_WL_M * s          # (a uniform room would give a
            t = t * (0.25 + 0.75 * vnoise(self.X / wl, self.Y / wl, self.seed + 11 + k))  # line)
            n = np.where(n < 0, t * np.tanh(n / t), n)
            e = d + n
            Es.append(e)
            Ds.append(d)
        E = np.max(Es, axis=0)
        if len(Ds) > 1:     # two clearings' bays never meet
            Dsrt = np.sort(np.stack(Ds), axis=0)
            sep = ((Dsrt[-1] - Dsrt[-2]) / 2 - config.TREELINE_SEP_M
                   + _noise(self.X, self.Y, ((30.0, 5.0, 5.0),), 1.0, self.seed + 5))
            sep = np.where(Dsrt[-2] > -cap_out + 1, sep, config.SURF_CAP_M * 10)
            E = smin(E, sep, config.TREELINE_SMOOTH_M)
        del Es, Ds
        self.E_edge = E.copy()          # before the used / kept constraints (diagnostics)

        # used ground: forced open
        E = smax(E, self.U, config.TREELINE_SMOOTH_M)

        # kept woods: forced wooded, exactly as the tiles say; and a noisy pad of wild
        # ground behind them (softly: its edge is a treeline too)
        kept = up(self.kept_tiles)
        pad = config.TREELINE_KEEP_PAD_M
        dk = chamfer(kept, (pad * 1.5) / c) * c
        padn = pad * (0.6 + 0.8 * vnoise(self.X / 23.0, self.Y / 23.0, self.seed + 7))
        wild = ~up(inmap)
        E = np.where(wild, smin(E, dk - padn, config.TREELINE_SMOOTH_M), E)
        P = chamfer(~kept, 12.0 / c) * c - c / 2          # metres into the kept woods
        E = np.where(kept, np.minimum(E, -P), E)
        self.kept = kept | (wild & (dk < padn))
        # a distance again (|grad| <= 1): the noise octaves steepen it in places, and the
        # surface fields, the shader's edge softness and the forest's setbacks read metres
        gy, gx = np.gradient(E, c)
        E = E / np.maximum(1.0, np.hypot(gx, gy))
        self.E = E.astype(np.float32)
        self.isles = self._isles()

    # -- kept woods ------------------------------------------------------------
    def _kept(self, woods):
        """In-map Woods tiles that stay wooded (see the module doc)."""
        kept = np.zeros_like(woods)
        nx0, ny0 = self.nx0, self.ny0
        world = self.world
        for m in world.maps.values():
            r = m.ring
            tx, ty = m.ox - nx0, m.oy - ny0
            sub = np.array([[s == "Woods" for s in row] for row in m.surfaces])
            inner = np.zeros_like(sub)
            inner[r:m.height - r, r:m.width - r] = True
            k = sub & inner                                   # woods inside the frame
            if m.id in config.TREELINE_KEEP_RING:
                k = sub.copy()
            for band, row_in in ((range(0, r), r), (range(m.height - r, m.height), m.height - 1 - r)):
                for x in range(m.width):
                    keep = bool(sub[row_in, x]) and row_in not in band and inner[row_in, x]
                    for y in band:            # the band meets another map's woods N / S
                        for dx in (-1, 0, 1):
                            for dy in (-1, 1):
                                gx, gy = m.ox + x + dx, m.oy + y + dy
                                o = world.owner(gx, gy)
                                if o is not None and o is not m and \
                                        o.surface(gx - o.ox, gy - o.oy) == "Woods":
                                    keep = True
                    if keep:
                        for y in band:
                            k[y, x] = k[y, x] or bool(sub[y, x])
            kept[ty:ty + m.height, tx:tx + m.width] |= k
        return kept

    @staticmethod
    def _used_edge(mask, tile_surf):
        """The fraction of a clearing's edge tiles that are not plain Grass."""
        inner = mask.copy()
        inner[1:] &= mask[:-1]
        inner[:-1] &= mask[1:]
        inner[:, 1:] &= mask[:, :-1]
        inner[:, :-1] &= mask[:, 1:]
        edge = mask & ~inner
        return float((tile_surf[edge] != SURF["Grass"]).mean())

    def _keeps_ring(self, mask):
        ids = {self.world.owner(gx + self.nx0, gy + self.ny0).id for gy, gx in zip(*np.nonzero(mask))}
        return bool(ids & set(config.TREELINE_KEEP_RING))

    # -- clearings -----------------------------------------------------------------
    def _components(self, open_t):
        """Connected (4-neighbour) open tile regions, largest first; tiny ones dropped."""
        H, W = open_t.shape
        label = np.full((H, W), -1, np.int32)
        comps = []
        for j0, i0 in zip(*np.nonzero(open_t)):
            if label[j0, i0] >= 0:
                continue
            n = len(comps)
            q = deque([(j0, i0)])
            label[j0, i0] = n
            cells = []
            while q:
                j, i = q.popleft()
                cells.append((j, i))
                for dj, di in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    a, b = j + dj, i + di
                    if 0 <= a < H and 0 <= b < W and open_t[a, b] and label[a, b] < 0:
                        label[a, b] = n
                        q.append((a, b))
            comps.append(cells)
        out = []
        for cells in sorted(comps, key=len, reverse=True):
            if len(cells) >= config.TREELINE_MIN_TILES:
                m = np.zeros((H, W), bool)
                jj, ii = zip(*cells)
                m[list(jj), list(ii)] = True
                out.append(m)
        return out

    # -- used ground -------------------------------------------------------------
    def _used(self, tile_surf, inmap, woods, track, up):
        """U = max over what the game uses of (margin - distance): >= margin on it."""
        classes = {}

        def add(margin, mask):
            classes[margin] = classes.get(margin, np.zeros((self.H, self.W), bool)) | mask

        for name in config.SURFACES:
            if name in ("Grass", "Woods"):
                continue
            t = inmap & (tile_surf == SURF[name])
            if name == "Dirt":
                t = t & ~woods          # (tracks through kept woods are woods either side)
            if t.any():
                add(config.TREELINE_USED_M.get(name, config.TREELINE_USED_M["default"]), up(t))
        routes, s = self.routes, T / config.TILE_PX
        guides = [np.asarray(routes.centreline(), float)] + [np.asarray(t.pts, float)
                                                              for t in routes.tracks.values()]
        for m in self.world.maps.values():
            ox, oy = m.ox * T, -m.oy * T
            has_road = any(row and all(v == "Road" for v in row) for row in m.surfaces)
            for b in m.data["buildings"]:
                x0, x1 = ox + b["x"] * T, ox + (b["x"] + b["w"]) * T
                y0, y1 = oy - (b["y"] + b["h"]) * T, oy - b["y"] * T
                add(config.TREELINE_BUILDING_M, self._rect(x0, y0, x1, y1))
                reach = config.TREELINE_DOOR_M
                for d in m.data["doors"]:
                    if d.get("building") != b["id"]:
                        continue
                    dy = (d["y"] + 0.5) - (b["y"] + b["h"] / 2)
                    dx = (d["x"] + 0.5) - (b["x"] + b["w"] / 2)
                    if abs(dy) * b["w"] >= abs(dx) * b["h"]:
                        yy = (y0 - reach, y0) if dy > 0 else (y1, y1 + reach)
                        add(config.TREELINE_SWEEP_M, self._rect(x0, yy[0], x1, yy[1]))
                    else:
                        xx = (x1, x1 + reach) if dx > 0 else (x0 - reach, x0)
                        add(config.TREELINE_SWEEP_M, self._rect(xx[0], y0, xx[1], y1))
                if has_road:        # the front faces the road in 3D: keep that side open
                    ry = routes.road_y
                    add(config.TREELINE_SWEEP_M, self._rect(x0, min(ry, y0), x1, max(ry, y1)))
            screen = lot = None
            for p in m.data["props"]:
                rect = (ox + p["x"] * T, oy - (p["y"] + p["h"]) * T,
                        ox + (p["x"] + p["w"]) * T, oy - p["y"] * T)
                if p["kind"] == "screen":
                    screen = rect
                if p["kind"] == "ramp_rows":
                    lot = rect
                if p["kind"] in config.GROUND_PROP_KINDS or p.get("sample"):
                    continue
                add(config.TREELINE_PROP_M, self._rect(*rect))
            if screen and lot:          # the screen's sightline over the field
                add(config.TREELINE_SWEEP_M, self._rect(min(screen[0], lot[0]), min(screen[1], lot[1]),
                                                        max(screen[2], lot[2]), max(screen[3], lot[3])))
            for item in m.data["lights"] + m.data["signs"]:
                if item["kind"] == "glow":
                    continue
                px, py = ox + item["px"][0] * s, oy - item["px"][1] * s
                add(config.TREELINE_POST_M, self._rect(px, py, px, py))
            for sign in m.data["signs"]:
                if sign.get("building"):
                    continue
                # its face: a corridor to the nearest route (where it is read from)
                px, py = ox + sign["px"][0] * s, oy - sign["px"][1] * s
                best = None
                for g in guides:
                    d, v = nearest_on(np.array([px]), np.array([py]), g, g)
                    if best is None or d[0] < best[0]:
                        best = (d[0], v[0])
                if best[0] < config.TREELINE_SIGN_REACH_M:
                    r = self.c / 2
                    seg = np.asarray(densify([(px, py), tuple(best[1])], r))
                    add(config.TREELINE_SIGN_M, self._stroke(seg, r))
        U = np.full((self.H, self.W), -np.inf, np.float32)
        for margin, mask in classes.items():
            cap = margin + config.TREELINE_SMOOTH_M + 2.0
            d = chamfer(mask, cap / self.c) * self.c
            U = np.maximum(U, margin - d)
        self.used_mask = np.zeros((self.H, self.W), bool)
        for mask in classes.values():
            self.used_mask |= mask
        return U

    def _rect(self, x0, y0, x1, y1):
        """Cells overlapping the rect (at least the cell containing a point)."""
        c = self.c
        i0 = max(0, int(math.floor((x0 - self.x0) / c)))
        i1 = min(self.W, max(i0 + 1, int(math.ceil((x1 - self.x0) / c))))
        j0 = max(0, int(math.floor((self.y1 - y1) / c)))
        j1 = min(self.H, max(j0 + 1, int(math.ceil((self.y1 - y0) / c))))
        m = np.zeros((self.H, self.W), bool)
        m[j0:j1, i0:i1] = True
        return m

    def _stroke(self, pts, r):
        m = np.zeros((self.H, self.W), bool)
        for x, y in pts:
            m |= self._rect(x - r, y - r, x + r, y + r)
        return m

    # -- lone trees and clumps -------------------------------------------------------
    def _isles(self):
        """(x, y) arrays: field trees standing alone or in small clumps on open grass,
        well clear of the treeline and of everything the game uses."""
        rng = np.random.default_rng(self.seed)
        cell = config.ISLE_CELL_M
        x0, y0, x1, y1 = self.rect()
        gx, gy = np.meshgrid(np.arange(x0, x1, cell), np.arange(y0, y1, cell))
        x = gx.ravel() + rng.random(gx.size) * cell
        y = gy.ravel() + rng.random(gy.size) * cell
        keep = rng.random(len(x)) < config.ISLE_KEEP
        n = np.where(rng.random(len(x)) < config.ISLE_CLUMP_P,
                     rng.integers(*config.ISLE_CLUMP_N, len(x)), 1)
        xs, ys = [], []
        for cx, cy, k, kp in zip(x, y, n, keep):
            if not kp:
                continue
            ang = rng.random(k) * 2 * math.pi
            rad = np.where(np.arange(k) == 0, 0.0, config.ISLE_CLUMP_R_M * (0.5 + 0.5 * rng.random(k)))
            px, py = cx + rad * np.cos(ang), cy + rad * np.sin(ang)
            ok = self.ok_isle(px, py)
            xs.append(px[ok])
            ys.append(py[ok])
        if not xs:
            return np.zeros(0), np.zeros(0)
        return np.concatenate(xs), np.concatenate(ys)

    def ok_isle(self, x, y):
        """Open ground at least ISLE_OPEN_M from the treeline, clear of the used margins,
        and back from the road (they frame the town, never screen it)."""
        x, y = np.asarray(x, float), np.asarray(y, float)
        e = self.sample(self.E, x, y)
        u = self.sample(self.U, x, y)
        inside = self.inside(x, y)
        road = nearest_on(x, y, np.asarray(densify(self.routes.centreline(), 5.0)))[0]
        return (inside & (e > config.ISLE_OPEN_M) & (u < -config.ISLE_USED_M)
                & (road > config.ISLE_ROAD_M))

    # -- sampling ----------------------------------------------------------------
    def rect(self):
        return (self.x0, self.y1 - self.H * self.c, self.x0 + self.W * self.c, self.y1)

    def inside(self, x, y):
        x0, y0, x1, y1 = self.rect()
        x, y = np.asarray(x, float), np.asarray(y, float)
        return (x >= x0) & (x <= x1) & (y >= y0) & (y <= y1)

    def sample(self, a, x, y):
        """Bilinear on cell centres; outside the grid: the nearest edge value."""
        x, y = np.asarray(x, float), np.asarray(y, float)
        fx = np.clip((x - self.x0) / self.c - 0.5, 0, self.W - 1.0001)
        fy = np.clip((self.y1 - y) / self.c - 0.5, 0, self.H - 1.0001)
        i, j = fx.astype(int), fy.astype(int)
        u, v = fx - i, fy - j
        return (a[j, i] * (1 - u) * (1 - v) + a[j, i + 1] * u * (1 - v)
                + a[j + 1, i] * (1 - u) * v + a[j + 1, i + 1] * u * v)

    def at(self, x, y):
        """E (+ open, - wooded) at points; outside the grid: deep woods."""
        return np.where(self.inside(x, y), self.sample(self.E, x, y), -config.SURF_CAP_M)


def _png(path, rgb):
    import struct
    import zlib
    h, w, _ = rgb.shape
    raw = b"".join(b"\x00" + rgb[j].astype(np.uint8).tobytes() for j in range(h))

    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(raw, 6)) + chunk(b"IEND", b""))


if __name__ == "__main__":
    import time
    import routes as routes_mod
    import terrain as terrain_mod
    import world as world_mod
    w = world_mod.build_world(sys.argv[1])
    r = routes_mod.build_routes(w)
    t0 = time.time()
    terr = terrain_mod.Terrain.__new__(terrain_mod.Terrain)
    terr.world, terr.routes = w, r
    gx0, gy0, gx1, gy1 = w.bounds
    P = config.NEAR_PAD_TILES
    terr.nx0, terr.ny0 = terrain_mod._even_floor(gx0 - P), terrain_mod._even_floor(gy0 - P)
    terr.NTx = -terrain_mod._even_floor(-(gx1 + P)) - terr.nx0
    terr.NTy = -terrain_mod._even_floor(-(gy1 + P)) - terr.ny0
    terr._tiles()
    tl = Treeline(w, r, terr.tile_surf, terr.tile_map, terr.nx0, terr.ny0)
    print(f"treeline {tl.W}x{tl.H} cells in {time.time() - t0:.1f}s; open {(tl.E > 0).mean():.3f}, "
          f"kept {tl.kept.sum()}, isles {len(tl.isles[0])}")
    if len(sys.argv) > 2:
        img = np.zeros((tl.H, tl.W, 3))
        op = tl.E > 0
        img[op] = (110, 170, 80)
        img[~op] = (40, 70, 35)
        img[tl.used_mask] = (200, 80, 60)
        img[tl.kept & ~op] = (40, 60, 110)
        img[(tl.E_edge > 0) != op] *= 0.8
        for x, y in zip(*tl.isles):
            i, j = int((x - tl.x0) / tl.c), int((tl.y1 - y) / tl.c)
            img[max(0, j - 1):j + 2, max(0, i - 1):i + 2] = (255, 230, 0)
        for m in w.maps.values():
            i0, i1 = (m.ox - tl.nx0) * tl.K, (m.ox + m.width - tl.nx0) * tl.K - 1
            j0, j1 = (m.oy - tl.ny0) * tl.K, (m.oy + m.height - tl.ny0) * tl.K - 1
            img[j0:j1 + 1, [i0, i1]] = 255
            img[[j0, j1], i0:i1 + 1] = 255
        img = np.repeat(np.repeat(img, 2, 0), 2, 1)
        _png(sys.argv[2], img)
        print("wrote", sys.argv[2])

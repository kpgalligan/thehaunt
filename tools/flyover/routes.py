"""Where the roads go, derived from the dump: the paved road's rows and kerb cuts, the
road out of town at both ends (curving out of sight), and the dirt tracks traced from
the kerb cuts. Pure Python (no bpy), XY only; terrain.py adds heights.

Coordinates follow config (metres, +X east, +Y north). A polyline is a list of (x, y).

  road rows    the rows that are Road in every strip map (asserted identical)
  kerb cuts    the dump's kerb_cut props, as global metre ranges per side
  road outs    from west_entry's west edge heading west and east_entry's east edge
               heading east: straight, then smooth bends (config.ROAD_OUT_BENDS) so the
               road leaves every frame on a curve, never on a horizon
  tracks       2-tile Dirt corridors traced from a 2-wide kerb cut, through map seams
               and turns; named by the chain they pass (config.TRACK_NAMES) or the map
               they reach; a track that dead-ends at a map's frame is continued into
               the forest (config.TRACK_EXTEND_M), wandering gently
"""

import math
import os
import sys
from dataclasses import dataclass, field

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import config  # noqa: E402
import world as world_mod  # noqa: E402

T = config.TILE_M
DIRS = {"N": (0, -1), "S": (0, 1), "E": (1, 0), "W": (-1, 0)}   # tile steps (y south)


@dataclass
class Track:
    name: str
    pts: list                 # centreline (x, y) metres, from the road outwards
    extension_from: int = -1  # index in pts where the forest continuation starts (-1 none)
    chain: str = None


@dataclass
class Routes:
    road_rows: tuple          # (gy_top, gy_end) global tile rows, end exclusive
    road_y: float             # centreline Y (m)
    road_x: tuple             # (x_west, x_east) of the in-town road (m)
    cuts: list                # [(side 'N'|'S', x0, x1)] metres
    out_w: list               # road-out polyline from the west end, heading west
    out_e: list               # road-out polyline from the east end, heading east
    tracks: dict = field(default_factory=dict)   # name -> Track

    def centreline(self):
        """The whole paved road west->east: west road-out (reversed), town, east road-out."""
        return list(reversed(self.out_w)) + self.out_e

    def mansion_end(self):
        t = self.tracks.get("MansionDrive")
        return t.pts[-1] if t else None

    def mansion_clearing(self):
        """(cx, cy, heading rad, width across, depth along) of the clearing at the
        mansion drive's end (Phase 6 places the roofline in it): on the drive's line,
        the drive entering config.MANSION_CLEARING_ENTER_M into its near edge."""
        t = self.tracks.get("MansionDrive")
        if t is None:
            return None
        (ax, ay), (bx, by) = t.pts[max(0, len(t.pts) - 5)], t.pts[-1]
        th = math.atan2(by - ay, bx - ax)
        w, d = config.MANSION_CLEARING_M
        off = d / 2 - config.MANSION_CLEARING_ENTER_M
        return bx + math.cos(th) * off, by + math.sin(th) * off, th, w, d


# ---------------------------------------------------------------------------
# Surfaces on the global tile grid
# ---------------------------------------------------------------------------

def surface_at(world, gx, gy):
    m = world.owner(gx, gy)
    return None if m is None else m.surface(gx - m.ox, gy - m.oy)


def _road_rows(world):
    rows = None
    for mid in world_mod.ROAD_ORDER:
        m = world.maps[mid]
        mine = [m.oy + y for y in range(m.height) if all(s == "Road" for s in m.surfaces[y])]
        if rows is None:
            rows = mine
        elif mine != rows:
            raise AssertionError(f"{mid}: road rows {mine} != {rows}")
    if len(rows) != 2 or rows[1] != rows[0] + 1:
        raise AssertionError(f"expected two contiguous road rows, got {rows}")
    return rows[0], rows[1] + 1


def _cuts(world, road_rows):
    cuts = []
    for m in world.maps.values():
        for p in m.data["props"]:
            if p["kind"] != "kerb_cut":
                continue
            gy = m.oy + p["y"]
            if gy not in range(*road_rows):
                raise AssertionError(f"{m.id}: kerb cut at row {gy} is off the road {road_rows}")
            cuts.append((p["side"], (m.ox + p["x"]) * T, (m.ox + p["x"] + p["w"]) * T,
                         m.ox + p["x"], p["w"], m.id))
    return cuts


# ---------------------------------------------------------------------------
# Road out of town
# ---------------------------------------------------------------------------

def _heading_rate(s):
    """d(heading)/ds (rad/m): each bend is a sin-shaped curvature bump (smooth ends)."""
    k = 0.0
    for s0, length, deg in config.ROAD_OUT_BENDS:
        if s0 <= s <= s0 + length:
            k += math.radians(deg) * math.pi / (2 * length) * math.sin(math.pi * (s - s0) / length)
    return k


def road_out(x0, y0, heading):
    """Polyline from (x0, y0), initial heading (rad, CCW from +X), config.ROAD_OUT_*."""
    step, n = config.ROAD_OUT_STEP_M, int(round(config.ROAD_OUT_LENGTH_M / config.ROAD_OUT_STEP_M))
    pts, x, y, th = [(x0, y0)], x0, y0, heading
    sub = 10
    for i in range(n):
        for k in range(sub):   # integrate finely, keep every step
            s = (i + k / sub) * step
            ds = step / sub
            th_mid = th + _heading_rate(s + ds / 2) * ds / 2
            x += math.cos(th_mid) * ds
            y += math.sin(th_mid) * ds
            th += _heading_rate(s + ds / 2) * ds
        pts.append((x, y))
    return pts


# ---------------------------------------------------------------------------
# Dirt tracks
# ---------------------------------------------------------------------------

def _is_dirt(world, gx, gy):
    return surface_at(world, gx, gy) == "Dirt"


def _trace(world, gx, gy, d):
    """Trace a 2-wide dirt corridor from the pair of tiles starting at (gx, gy) (the
    pair runs perpendicular to d: (gx, gy) and the next tile east for N/S, south for
    E/W). Returns (points in tile-corner coords, tiles visited)."""
    dx, dy = DIRS[d]
    pair = (lambda x, y: [(x, y), (x + 1, y)]) if dx == 0 else (lambda x, y: [(x, y), (x, y + 1)])

    def open_(x, y):
        return all(_is_dirt(world, *c) for c in pair(x, y))

    if not open_(gx, gy):
        return [], []
    # start at the tile edge the corridor enters through (the kerb line)
    if dx == 0:
        pts = [(gx + 1, gy + (1 if dy < 0 else 0))]
    else:
        pts = [(gx + (1 if dx < 0 else 0), gy + 1)]
    tiles = list(pair(gx, gy))
    x, y = gx, gy
    for _ in range(400):
        if open_(x + dx, y + dy):
            x, y = x + dx, y + dy
            tiles += pair(x, y)
            continue
        # a turn: the corridor's last two cells along d continue sideways
        turned = False
        for nd in (("W", "E") if dx == 0 else ("N", "S")):
            ndx, ndy = DIRS[nd]
            if dx == 0:   # heading N/S, last two rows are y and y - dy
                r0 = min(y, y - dy)
                bx, by = (x - 1 if ndx < 0 else x + 2), r0
                if _is_dirt(world, bx, by) and _is_dirt(world, bx, by + 1):
                    pts.append((x + 1, r0 + 1))
                    x, y, dx, dy, d = (x if ndx < 0 else x + 1), r0, ndx, ndy, nd
                    pair = lambda a, b: [(a, b), (a, b + 1)]  # noqa: E731
                    turned = True
                    break
            else:         # heading E/W, last two columns are x and x - dx
                c0 = min(x, x - dx)
                bx, by = c0, (y - 1 if ndy < 0 else y + 2)
                if _is_dirt(world, bx, by) and _is_dirt(world, bx + 1, by):
                    pts.append((c0 + 1, y + 1))
                    x, y, dx, dy, d = c0, (y if ndy < 0 else y + 1), ndx, ndy, nd
                    pair = lambda a, b: [(a, b), (a + 1, b)]  # noqa: E731
                    turned = True
                    break
        if not turned:
            break
    # end at the far tile edge
    if dx == 0:
        pts.append((x + 1, y + (0 if dy < 0 else 1)))
    else:
        pts.append((x + (0 if dx < 0 else 1), y + 1))
    return pts, tiles, d


def _chain_on(world, tiles):
    tiles = set(tiles)
    for m in world.maps.values():
        for p in m.data["props"]:
            if p["kind"] != "chain":
                continue
            for x in range(p["w"]):
                for y in range(p["h"]):
                    if (m.ox + p["x"] + x, m.oy + p["y"] + y) in tiles:
                        return p["id"]
    return None


def _extend(start, heading, length, seed):
    """A gentle wander from start: heading drifts by a smooth sin mix (deterministic)."""
    pts, (x, y) = [start], start
    step = config.ROAD_OUT_STEP_M
    amp = math.radians(config.TRACK_WANDER_DEG)
    n = max(1, int(round(length / step)))
    for i in range(1, n + 1):
        s = i * step
        th = heading + amp * (math.sin(s / 23.0 + seed) * 0.7 + math.sin(s / 9.0 + 2 * seed) * 0.3) \
            * min(1.0, s / 15.0)
        x += math.cos(th) * step
        y += math.sin(th) * step
        pts.append((x, y))
    return pts


def _tracks(world, road_rows, cuts):
    tracks = {}
    for side, _x0, _x1, gx, w, mid in cuts:
        if w != 2:
            continue
        gy = road_rows[0] - 1 if side == "N" else road_rows[1]
        traced = _trace(world, gx, gy, side)
        if not traced[0]:
            continue
        corner_pts, tiles, end_dir = traced
        if len(tiles) // 2 < config.TRACK_MIN_TILES:
            continue
        chain = _chain_on(world, tiles)
        maps_on = {world.owner(*t).id for t in tiles}
        name = config.TRACK_NAMES.get(chain)
        if name is None:
            name = next((config.TRACK_INTO_MAP[m] for m in maps_on if m in config.TRACK_INTO_MAP),
                        f"Track_{mid}_{side}")
        pts = [(cx * T, -cy * T) for cx, cy in corner_pts]
        track = Track(name, pts, chain=chain)
        ext = config.TRACK_EXTEND_M.get(name)
        if ext:
            # the traced end stops at the map's woods frame; the track goes on
            dx, dy = DIRS[end_dir]
            heading = math.atan2(-dy, dx)
            more = _extend(pts[-1], heading, ext, seed=len(name))
            track.extension_from = len(pts) - 1
            track.pts = pts + more[1:]
        tracks[name] = track
    return tracks


# ---------------------------------------------------------------------------

def densify(pts, step):
    out = [pts[0]]
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        L = math.hypot(x1 - x0, y1 - y0)
        n = max(1, int(math.ceil(L / step)))
        out += [(x0 + (x1 - x0) * k / n, y0 + (y1 - y0) * k / n) for k in range(1, n + 1)]
    return out


def length(pts):
    return sum(math.hypot(b[0] - a[0], b[1] - a[1]) for a, b in zip(pts, pts[1:]))


def build_routes(world):
    rows = _road_rows(world)
    raw_cuts = _cuts(world, rows)
    road_y = -(rows[0] + 1) * T          # between the two rows
    west = world.maps[world_mod.ROAD_ORDER[0]]
    east = world.maps[world_mod.ROAD_ORDER[-1]]
    xw, xe = west.ox * T, (east.ox + east.width) * T
    return Routes(
        road_rows=rows, road_y=road_y, road_x=(xw, xe),
        cuts=[(side, x0, x1) for side, x0, x1, *_ in raw_cuts],
        out_w=road_out(xw, road_y, math.pi),
        out_e=road_out(xe, road_y, 0.0),
        tracks=_tracks(world, rows, raw_cuts))


if __name__ == "__main__":
    r = build_routes(world_mod.build_world(sys.argv[1]))
    print("road rows", r.road_rows, "y", r.road_y, "x", r.road_x)
    print("cuts", r.cuts)
    for name, pts in (("out_w", r.out_w), ("out_e", r.out_e)):
        print(name, len(pts), "end", tuple(round(v, 1) for v in pts[-1]), f"len {length(pts):.0f}")
    for t in r.tracks.values():
        print("track", t.name, t.chain, [tuple(round(v, 1) for v in p) for p in t.pts[:6]], "...",
              tuple(round(v, 1) for v in t.pts[-1]), f"len {length(t.pts):.0f}", "ext@", t.extension_from)

"""Load and validate a world dump, stitch its maps into one world, fix the frame ring.

Pure Python (no bpy): `python3 tools/flyover/world.py <dump.json>` prints the offset
table and runs every assertion without Blender.

Stitching is derived from exits, never hand-set: west_entry sits at (0, 0) and each
non-wrap exit A->B with a reverse exit B->A on the opposite edge places B so the two
mouths abut (A's edge + 1 = B's edge, mouths aligned along it). The road wrap
(west_entry W <-> east_entry E) is skipped so it cannot pull east_entry back west.

Frame ring: each map is framed by a woods ring (depth derived per map: the number of
outer lines that are all Woods apart from exit mouths, minimum over the four edges).
Only the E/W road seams open: a ring tile in an east or west band becomes
config.SEAM_SURFACE when that band faces another map, so no tree line crosses the road.
North/south bands always stay Woods (the farm leaves "through the SOUTH treeline"; the
drive-in sits behind the woods) and corners stay Woods, so the outer tree line is
unbroken; the exit mouths are already open in the data.
"""

import json
import os
import sys
from collections import deque
from dataclasses import dataclass, field

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import config  # noqa: E402

ORIGIN_MAP = "west_entry"
OPPOSITE = {"N": "S", "S": "N", "E": "W", "W": "E"}

# Expected layout, asserted after stitching (the derivation must reproduce it).
ROAD_ORDER = ["west_entry", "billies", "fork", "town", "east_fork", "east_entry"]
NORTH_OF = {"test_farm": "fork"}      # map -> the strip map its south mouth opens onto
SOUTH_OF = {"drive_in": "east_fork"}  # map -> the strip map its north mouth opens onto


@dataclass
class Placed:
    id: str
    data: dict
    ox: int
    oy: int
    width: int
    height: int
    ring: int = 1
    surfaces: list = field(default_factory=list)  # effective rows (seam-fixed), str per row
    seam_tiles: int = 0

    def contains(self, gx, gy):
        return self.ox <= gx < self.ox + self.width and self.oy <= gy < self.oy + self.height

    def surface(self, x, y):
        return self.surfaces[y][x]


@dataclass
class World:
    maps: dict            # id -> Placed, in dump order
    bounds: tuple         # (gx0, gy0, gx1, gy1) global tiles, exclusive max

    def owner(self, gx, gy):
        for m in self.maps.values():
            if m.contains(gx, gy):
                return m
        return None


# ---------------------------------------------------------------------------
# Load + validate
# ---------------------------------------------------------------------------

def load(path):
    with open(path, encoding="utf-8") as f:
        doc = json.load(f)
    if doc.get("version") != config.DUMP_VERSION:
        raise ValueError(f"world dump version {doc.get('version')}, expected {config.DUMP_VERSION}")
    if doc.get("tilePx") != config.TILE_PX:
        raise ValueError(f"world dump tilePx {doc.get('tilePx')}, expected {config.TILE_PX}")
    ids = [m["id"] for m in doc["maps"]]
    if len(set(ids)) != len(ids):
        raise ValueError(f"duplicate map ids in dump: {ids}")
    for m in doc["maps"]:
        _validate_map(m)
    return doc


def _validate_map(m):
    w, h, mid = m["width"], m["height"], m["id"]
    if len(m["surfaces"]) != h or any(len(r) != w for r in m["surfaces"]):
        raise ValueError(f"{mid}: surfaces grid is not {w}x{h}")
    for code, name in m["legend"].items():
        if name not in config.SURFACE_COLOURS:
            raise ValueError(f"{mid}: surface '{name}' has no colour in config.SURFACE_COLOURS")
    used = {c for row in m["surfaces"] for c in row}
    if not used <= set(m["legend"]):
        raise ValueError(f"{mid}: surface codes {used - set(m['legend'])} missing from legend")
    for e in m["exits"]:
        if e["edge"] not in OPPOSITE:
            raise ValueError(f"{mid}: exit {e['id']} has edge {e['edge']}")


# ---------------------------------------------------------------------------
# Stitching
# ---------------------------------------------------------------------------

def _reverse_exit(src_id, exit_, dst):
    want = OPPOSITE[exit_["edge"]]
    for r in dst["exits"]:
        if r["to"] == src_id and r["edge"] == want and not r["wrap"]:
            return r
    return None


def _offset_for(a, ax, ay, e, b, r):
    """Offset of map b so exit e (on a at ax, ay) and b's reverse exit r abut."""
    if (e["w"], e["h"]) != (r["w"], r["h"]):
        raise ValueError(f"{a['id']}.{e['id']} mouth {e['w']}x{e['h']} != "
                         f"{b['id']}.{r['id']} mouth {r['w']}x{r['h']}")
    edge = e["edge"]
    if edge == "E":
        return ax + a["width"], ay + e["y"] - r["y"]
    if edge == "W":
        return ax - b["width"], ay + e["y"] - r["y"]
    if edge == "S":
        return ax + e["x"] - r["x"], ay + a["height"]
    return ax + e["x"] - r["x"], ay - b["height"]  # N


def stitch(doc):
    by_id = {m["id"]: m for m in doc["maps"]}
    if ORIGIN_MAP not in by_id:
        raise ValueError(f"dump has no '{ORIGIN_MAP}' to anchor the stitch")
    offsets = {ORIGIN_MAP: (0, 0)}
    queue = deque([ORIGIN_MAP])
    while queue:
        aid = queue.popleft()
        a = by_id[aid]
        ax, ay = offsets[aid]
        for e in a["exits"]:
            if e["wrap"]:
                continue
            b = by_id.get(e["to"])
            if b is None:
                raise ValueError(f"{aid}.{e['id']} leads to '{e['to']}', not in the dump")
            r = _reverse_exit(aid, e, b)
            if r is None:
                raise ValueError(f"{aid}.{e['id']} ({e['edge']}) -> {b['id']} has no reverse exit "
                                 f"on edge {OPPOSITE[e['edge']]}")
            off = _offset_for(a, ax, ay, e, b, r)
            if b["id"] in offsets:
                if offsets[b["id"]] != off:
                    raise ValueError(f"{b['id']} placed at {offsets[b['id']]} but "
                                     f"{aid}.{e['id']} wants {off}")
                continue
            offsets[b["id"]] = off
            queue.append(b["id"])
    unplaced = set(by_id) - set(offsets)
    if unplaced:
        raise ValueError(f"maps not reachable by non-wrap exits: {sorted(unplaced)}")

    maps = {}
    for m in doc["maps"]:
        ox, oy = offsets[m["id"]]
        maps[m["id"]] = Placed(m["id"], m, ox, oy, m["width"], m["height"])
    gx0 = min(p.ox for p in maps.values())
    gy0 = min(p.oy for p in maps.values())
    gx1 = max(p.ox + p.width for p in maps.values())
    gy1 = max(p.oy + p.height for p in maps.values())
    world = World(maps, (gx0, gy0, gx1, gy1))
    _check_layout(world)
    _fix_rings(world)
    _check_seams(world)
    return world


def _check_layout(world):
    ms = list(world.maps.values())
    for i, a in enumerate(ms):
        for b in ms[i + 1:]:
            if (a.ox < b.ox + b.width and b.ox < a.ox + a.width
                    and a.oy < b.oy + b.height and b.oy < a.oy + a.height):
                raise AssertionError(f"maps overlap: {a.id} and {b.id}")
    strip = [world.maps[i] for i in ROAD_ORDER]
    order = [p.id for p in sorted(strip, key=lambda p: p.ox)]
    if order != ROAD_ORDER:
        raise AssertionError(f"road order west->east is {order}, expected {ROAD_ORDER}")
    for a, b in zip(strip, strip[1:]):
        if a.ox + a.width != b.ox:
            raise AssertionError(f"{a.id} and {b.id} do not abut")
    for mid, host in NORTH_OF.items():
        m, h = world.maps[mid], world.maps[host]
        if m.oy + m.height != h.oy or not (m.ox < h.ox + h.width and h.ox < m.ox + m.width):
            raise AssertionError(f"{mid} is not directly north of {host}")
    for mid, host in SOUTH_OF.items():
        m, h = world.maps[mid], world.maps[host]
        if h.oy + h.height != m.oy or not (m.ox < h.ox + h.width and h.ox < m.ox + m.width):
            raise AssertionError(f"{mid} is not directly south of {host}")


# ---------------------------------------------------------------------------
# Frame ring
# ---------------------------------------------------------------------------

def _raw(m, x, y):
    return m.data["legend"][m.data["surfaces"][y][x]]


def _mouth(m, edge, along):
    """True if `along` (x for N/S, y for E/W) lies in an exit mouth on that edge."""
    for e in m.data["exits"]:
        if e["edge"] != edge:
            continue
        lo, n = (e["x"], e["w"]) if edge in "NS" else (e["y"], e["h"])
        if lo <= along < lo + n:
            return True
    return False


def _edge_depth(m, edge, cap=4):
    depth = 0
    for k in range(cap):
        if edge in "NS":
            y = k if edge == "N" else m.height - 1 - k
            line = [(x, y, x) for x in range(m.width)]
        else:
            x = k if edge == "W" else m.width - 1 - k
            line = [(x, y, y) for y in range(m.height)]
        if all(_raw(m, x, y) == config.RING_SURFACE or _mouth(m, edge, a) for x, y, a in line):
            depth += 1
        else:
            break
    return depth


def _fix_rings(world):
    for m in world.maps.values():
        m.ring = min(_edge_depth(m, e) for e in "NSEW")
        if m.ring < 1:
            raise AssertionError(f"{m.id}: no woods frame ring found")
        codes = {name: code for code, name in m.data["legend"].items()}
        seam_code = codes.get(config.SEAM_SURFACE, "~")  # '~' = a seam-only surface
        rows = [list(r) for r in m.data["surfaces"]]
        for y in range(m.height):
            for x in range(m.width):
                if _raw(m, x, y) != config.RING_SURFACE:
                    continue
                if y < m.ring or y >= m.height - m.ring:
                    continue  # N/S bands keep their tree line; only the mouth is open
                beyond = []
                if x < m.ring:
                    beyond.append((m.ox - 1, m.oy + y))
                if x >= m.width - m.ring:
                    beyond.append((m.ox + m.width, m.oy + y))
                if beyond and all(world.owner(gx, gy) is not None for gx, gy in beyond):
                    rows[y][x] = seam_code
                    m.seam_tiles += 1
        legend = dict(m.data["legend"])
        legend["~"] = config.SEAM_SURFACE
        m.surfaces = [[legend[c] for c in r] for r in rows]


def _check_seams(world):
    """Across every shared edge, the open (non-ring) tiles on the two sides must match:
    the road, dirt roads and mouths continue; nothing opens onto a wall of woods."""
    ms = list(world.maps.values())
    for a in ms:
        for b in ms:
            if a is b:
                continue
            if a.ox + a.width == b.ox:        # a | b, vertical seam
                pairs = [((a.width - 1, gy - a.oy), (0, gy - b.oy))
                         for gy in range(max(a.oy, b.oy), min(a.oy + a.height, b.oy + b.height))]
            elif a.oy + a.height == b.oy:     # a over b, horizontal seam
                pairs = [((gx - a.ox, a.height - 1), (gx - b.ox, 0))
                         for gx in range(max(a.ox, b.ox), min(a.ox + a.width, b.ox + b.width))]
            else:
                continue
            for (ax, ay), (bx, by) in pairs:
                a_open = _raw(a, ax, ay) != config.RING_SURFACE
                b_open = _raw(b, bx, by) != config.RING_SURFACE
                if a_open != b_open:
                    raise AssertionError(
                        f"seam mismatch {a.id}({ax},{ay})={_raw(a, ax, ay)} vs "
                        f"{b.id}({bx},{by})={_raw(b, bx, by)}")


def offset_table(world):
    lines = [f"{'map':<12} {'ox':>4} {'oy':>4} {'w':>3} {'h':>3} {'ring':>4} {'seam':>4}"]
    for p in sorted(world.maps.values(), key=lambda p: (p.oy, p.ox)):
        lines.append(f"{p.id:<12} {p.ox:>4} {p.oy:>4} {p.width:>3} {p.height:>3} "
                     f"{p.ring:>4} {p.seam_tiles:>4}")
    gx0, gy0, gx1, gy1 = world.bounds
    lines.append(f"bounds: x {gx0}..{gx1}, y {gy0}..{gy1} tiles "
                 f"({(gx1 - gx0) * config.TILE_M:.0f} x {(gy1 - gy0) * config.TILE_M:.0f} m)")
    return "\n".join(lines)


def build_world(path):
    return stitch(load(path))


if __name__ == "__main__":
    print(offset_table(build_world(sys.argv[1])))

"""Lot paint as thin decal quads lifted config.MARK_LIFT_M off the ground: the motel
lot's stall stripes (dump prop stall_stripes, exact game pixels) and the drive-in's
faint ramp lines (dump prop ramp_rows: broken dashes along each ramp crest). One mesh
per kind, material Paint_Faded."""

import numpy as np

import config
import scene


def _quad(verts, quads, x0, y0, x1, y1, z):
    """Axis-aligned rect (x0 < x1, y0 < y1) at height z, CCW from +Z."""
    b = len(verts)
    verts += [(x0, y1, z), (x0, y0, z), (x1, y0, z), (x1, y1, z)]
    quads.append((b, b + 1, b + 2, b + 3))


def _draped(buf, terrain, x0, y0, x1, y1, lift):
    """Like _quad, each corner at the ground under it (paint over a ramp crest)."""
    verts, quads = buf
    b = len(verts)
    for x, y in ((x0, y1), (x0, y0), (x1, y0), (x1, y1)):
        verts.append((x, y, terrain.z_at(x, y) + lift))
    quads.append((b, b + 1, b + 2, b + 3))


def build(world, terrain, col, mat, only=None):
    t, px = config.TILE_M, config.TILE_M / config.TILE_PX
    lift = config.MARK_LIFT_M
    stripes, dashes = ([], []), ([], [])
    for m in world.maps.values():
        if only is not None and m.id != only:
            continue
        for p in m.data["props"]:
            lx, ly = (m.ox + p["x"]) * t, -(m.oy + p["y"]) * t     # rect's NW corner
            if p["kind"] == "stall_stripes":
                for sx, sy, sw, sh in p["stripesPx"]:
                    x0, x1 = lx + sx * px, lx + (sx + sw) * px
                    y1, y0 = ly - sy * px, ly - (sy + sh) * px
                    z = max(terrain.z_at(x0, y0), terrain.z_at(x1, y1)) + lift
                    _quad(*stripes, x0, y0, x1, y1, z)
            elif p["kind"] == "ramp_rows":
                x_end = lx + p["w"] * t
                on, pitch = config.RAMP_DASH_M
                for ypx in p["rowsPx"]:
                    yc = ly - ypx * px
                    x = lx + 0.6
                    k = 0
                    while x + on < x_end - 0.6:
                        jitter = (np.sin(k * 12.9898 + ypx) * 43758.5453) % 1.0 * 0.6
                        x0 = x + jitter
                        _draped(dashes, terrain, x0, yc - 0.08, x0 + on, yc + 0.08, lift)
                        x += pitch
                        k += 1
    out = []
    for name, (verts, quads) in (("Marking_StallStripes", stripes), ("Marking_RampLines", dashes)):
        if not quads:
            continue
        me = scene.mesh_from_arrays(name, np.array(verts), np.array(quads), None, [mat])
        out.append(scene.new_object(name, me, col, kind="marking"))
    return out

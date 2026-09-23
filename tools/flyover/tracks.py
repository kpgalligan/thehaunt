"""Dirt track continuations into the forest (routes.Track.extension_from onwards, e.g.
the mansion drive past its chain, the fork's chained south stub): draped ribbon
meshes that follow the ground, since they wander off the tile grid. In-map track
tiles are the ground mesh's own (terrain.py ruts); a ribbon starts a little inside
that corridor so the join is covered.

Profile across (2 tiles wide): edges tucked to the ground, two ruts, a crown; the
`rut` / `crown` / `edge` attributes feed Surface_Dirt like the ground's. Over its
last stretch the track narrows and grasses over (crown -> 1)."""

import math

import numpy as np

import config
import scene
import terrain as terrain_mod

ACROSS = 9             # vertices across the ribbon
LIFT_M = 0.06          # above the (coarse) ground it drapes over
EDGE_LIFT_M = 0.015
STEP_M = 1.25


def _ribbon(terrain, pts, mat_slots, name, col):
    pts = terrain_mod.densify(pts, STEP_M)
    p = np.asarray(pts, float)
    s = terrain_mod.arc_lengths(p)
    tan = np.gradient(p, axis=0)
    tan /= np.linalg.norm(tan, axis=1)[:, None]
    left = np.stack([-tan[:, 1], tan[:, 0]], 1)
    half = config.TILE_M
    fade = min(12.0, 0.4 * s[-1])
    verts, attrs = [], {"rut": [], "crown": [], "edge": []}
    for k in range(len(p)):
        f = float(terrain_mod.smoothstep(s[-1] - fade, s[-1], s[k]))
        w = half * (1 - 0.55 * f)
        for a in range(ACROSS):
            u = -1 + 2 * a / (ACROSS - 1)           # -1 .. 1 across
            v = u * w
            x, y = p[k] + left[k] * v
            d = (1 - abs(u)) * w                      # metres in from the edge
            rut = math.exp(-((abs(v) - 0.8) / 0.35) ** 2) * (1 - f)
            lift = EDGE_LIFT_M + (LIFT_M - EDGE_LIFT_M) * min(1.0, d / 0.6) * min(1.0, s[k] / STEP_M)
            verts.append((x, y, terrain.z_at(x, y) + lift - config.RUT_DEPTH_M * 0.7 * rut))
            attrs["rut"].append(rut)
            attrs["crown"].append(max(f, 1.0 if abs(v) < 0.35 else 0.0))
            attrs["edge"].append(min(1.0, d / 2.5))
    quads = []
    for k in range(len(p) - 1):
        for a in range(ACROSS - 1):
            i0, i1 = k * ACROSS + a, k * ACROSS + a + 1      # u runs right (-1) -> left (+1)
            j0, j1 = i0 + ACROSS, i1 + ACROSS
            quads.append((i1, i0, j0, j1))                   # CCW from +Z
    me = scene.mesh_from_arrays(name, np.array(verts), np.array(quads), None, mat_slots,
                                {k: np.array(v) for k, v in attrs.items()})
    return scene.new_object(name, me, col, kind="track")


def build(terrain, col, mats):
    out = []
    for name, t in terrain.routes.tracks.items():
        if t.extension_from < 0:
            continue
        start = t.pts[t.extension_from]
        prev = t.pts[t.extension_from - 1]
        dx, dy = start[0] - prev[0], start[1] - prev[1]
        L = math.hypot(dx, dy) or 1.0
        back = (start[0] - dx / L * STEP_M, start[1] - dy / L * STEP_M)
        pts = [back] + list(t.pts[t.extension_from:])
        out.append(_ribbon(terrain, pts, [mats.surface("Dirt")], f"Track_{name}", col))
    return out

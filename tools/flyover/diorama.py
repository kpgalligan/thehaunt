"""The flat diorama: one ground mesh per map, one quad per tile, plus the forest-floor
plane under the whole stitched world.

One mesh PER MAP (not one combined): each object sits at its map's north-west corner
with tile-local vertices, so a map can be hidden, framed or replaced alone, and a
single-map build (--only-map) is the same code path. Faces share vertices on a
(w+1) x (h+1) grid; every mesh carries all config.SURFACES as material slots in the
same order, so a face's material_index is the surface kind everywhere and Phase 3
swaps materials without touching geometry.
"""

import bpy

import config
import scene


def build_map_ground(placed, col, mats):
    t = config.TILE_M
    w, h = placed.width, placed.height
    verts = [(x * t, -y * t, 0.0) for y in range(h + 1) for x in range(w + 1)]
    faces, indices = [], []
    for y in range(h):
        for x in range(w):
            a = y * (w + 1) + x
            faces.append((a, a + w + 1, a + w + 2, a + 1))   # CCW seen from +Z
            indices.append(config.SURFACES.index(placed.surface(x, y)))
    me = scene.tag(bpy.data.meshes.new(f"Ground_{placed.id}"))
    me.from_pydata(verts, [], faces)
    for kind in config.SURFACES:
        me.materials.append(mats.surface(kind))
    me.polygons.foreach_set("material_index", indices)
    me.update()
    ob = scene.new_object(f"Ground_{placed.id}", me, col, map_id=placed.id,
                          tile_ox=placed.ox, tile_oy=placed.oy)
    ob.location = (placed.ox * t, -placed.oy * t, 0.0)
    return ob


def build_forest_floor(world, col, mats):
    t, m = config.TILE_M, config.GROUND_MARGIN_M
    gx0, gy0, gx1, gy1 = world.bounds
    x0, x1 = gx0 * t - m, gx1 * t + m
    y0, y1 = -gy1 * t - m, -gy0 * t + m
    z = config.GROUND_PLANE_Z
    me = scene.tag(bpy.data.meshes.new("Ground_ForestFloor"))
    me.from_pydata([(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)], [], [(0, 1, 2, 3)])
    me.materials.append(mats.get("Ground_ForestFloor", config.colour(config.FOREST_FLOOR)))
    me.update()
    return scene.new_object("Ground_ForestFloor", me, col)


def build(world, col, mats, only=None):
    for placed in world.maps.values():
        if only is None or placed.id == only:
            build_map_ground(placed, col, mats)
    build_forest_floor(world, col, mats)

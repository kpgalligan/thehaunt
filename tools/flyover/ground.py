"""The ground meshes from terrain.Terrain: one per map (Ground_<map>, origin at the
map's north-west corner, tile-local vertices, as in the diorama) and Ground_Terrain
for everything outside the maps (near wild tiles + the far ring, world origin).

Face material_index = config.SURFACES order on every ground mesh (Woods is the wild
forest floor): the tile's surface, for the Workbench layout stills; every slot wraps
the one ground look (materials.ground_look), which paints the smooth surface fields.
Point attribute `verge` (road-out verges). Smooth-shaded. The paved road's rows are
left out of the map meshes: road.py lays the road there.
"""

import numpy as np

import config
import scene


def _slots(mats):
    return [mats.surface(kind) for kind in config.SURFACES]


def build(terrain, col, mats, only=None):
    t = config.TILE_M
    objs = []
    for k, mid in enumerate(terrain.map_ids):
        if only is not None and mid != only:
            continue
        placed = terrain.world.maps[mid]
        verts, quads, mi, attrs = terrain.map_mesh(k)
        origin = np.array([placed.ox * t, -placed.oy * t, 0.0])
        me = scene.mesh_from_arrays(f"Ground_{mid}", verts - origin, quads, mi, _slots(mats), attrs)
        me.shade_smooth()
        ob = scene.new_object(f"Ground_{mid}", me, col, map_id=mid, tile_ox=placed.ox,
                              tile_oy=placed.oy)
        ob.location = tuple(origin)
        objs.append(ob)
    if only is None:
        verts, quads, mi, attrs = terrain.terrain_mesh()
        me = scene.mesh_from_arrays("Ground_Terrain", verts, quads, mi, _slots(mats), attrs)
        me.shade_smooth()
        ob = scene.new_object("Ground_Terrain", me, col)
        x0, y0, x1, y1 = terrain.far_rect
        ob["extent_m"] = [x0, y0, x1, y1]
        objs.append(ob)
    return objs

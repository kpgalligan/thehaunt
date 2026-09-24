"""Blender bookkeeping: the Flyover collection tree, tagging, the idempotent wipe, and
the shared materials / unit meshes every layer builds from.

Every datablock the build creates is tagged with the ID property config.TAG_PROP, so
a rebuild removes exactly the build's own objects, meshes, materials, cameras and
collections, and nothing else in the file.
"""

import bpy

import config

_DATA_KINDS = ("objects", "meshes", "materials", "cameras", "lights", "curves", "worlds",
               "node_groups", "images", "metaballs", "actions", "collections", "texts")


def tag(idb):
    idb[config.TAG_PROP] = True
    return idb


def is_ours(idb):
    return bool(idb.get(config.TAG_PROP))


def wipe():
    """Delete the Flyover tree and every tagged datablock. Refuses to touch a foreign
    collection that happens to be called config.ROOT."""
    root = bpy.data.collections.get(config.ROOT)
    if root is not None and not is_ours(root):
        raise RuntimeError(f"collection '{config.ROOT}' exists but was not made by the build")
    # the flights' own scenes (flight.py) go first: the host becomes the active scene
    own = [sc for sc in bpy.data.scenes if is_ours(sc)]
    if own:
        host = next((sc for sc in bpy.data.scenes if not is_ours(sc)), None)
        if host is None:
            raise RuntimeError("every scene is the build's: no host scene to keep")
        for win in bpy.context.window_manager.windows:
            if is_ours(win.scene):
                win.scene = host
        for sc in own:
            bpy.data.scenes.remove(sc)
    if root is not None:
        for ob in list(root.all_objects):
            bpy.data.objects.remove(ob, do_unlink=True)
    for kind in _DATA_KINDS:
        coll = getattr(bpy.data, kind)
        for idb in [d for d in coll if is_ours(d)]:
            coll.remove(idb)


def collection(name, parent=None):
    """A new tagged collection linked under parent (or the scene root)."""
    if name in bpy.data.collections:
        raise RuntimeError(f"collection '{name}' already exists (foreign, or wipe() not run)")
    col = tag(bpy.data.collections.new(name))
    (parent.children if parent else bpy.context.scene.collection.children).link(col)
    return col


def new_object(name, data, col, **props):
    if name in bpy.data.objects:
        raise RuntimeError(f"object name '{name}' collides with an existing object")
    ob = tag(bpy.data.objects.new(name, data))
    col.objects.link(ob)
    for k, v in props.items():
        if v is not None:
            ob[k] = v
    return ob


class Materials:
    """Flat, emission-free Principled BSDF materials, one per colour, cached by name."""

    def __init__(self):
        self._cache = {}

    def get(self, name, hex_colour):
        mat = self._cache.get(name)
        if mat is not None:
            return mat
        if name in bpy.data.materials:
            raise RuntimeError(f"material name '{name}' collides with an existing material")
        mat = tag(bpy.data.materials.new(name))
        rgba = config.hex_rgba(hex_colour)
        mat.diffuse_color = rgba            # what Workbench's MATERIAL colour shows
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Roughness"].default_value = 1.0
        mat["hex"] = hex_colour
        self._cache[name] = mat
        return mat

    def put(self, name, mat):
        """Register a material built elsewhere (materials.py) under its cache name."""
        self._cache[name] = mat
        return mat

    def get_cached(self, name):
        return self._cache[name]

    def surface(self, kind):
        return self.get(f"Surface_{kind}", config.colour(config.SURFACE_COLOURS[kind]))

    def colour(self, prefix, name_or_hex):
        key = name_or_hex.lstrip("#")
        return self.get(f"{prefix}_{key}", config.colour(name_or_hex))


def unit_box(name="FO_UnitBox"):
    """A 1x1x1 box with its origin at the bottom NORTH-WEST corner (spans x 0..1,
    y -1..0, z 0..1), so an object at a tile corner scaled (w, h, z) covers the
    footprint. One slot, linked per object."""
    me = bpy.data.meshes.get(name)
    if me is not None and is_ours(me):
        return me
    v = [(x, y, z) for z in (0, 1) for y in (-1, 0) for x in (0, 1)]
    f = [(0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)]
    me = tag(bpy.data.meshes.new(name))
    me.from_pydata(v, [], f)
    me.materials.append(None)
    me.update()
    return me


def unit_cylinder(name="FO_UnitCylinder", segments=12):
    """Radius 0.5, height 1, centred on the origin in XY, base at z 0."""
    import math
    me = bpy.data.meshes.get(name)
    if me is not None and is_ours(me):
        return me
    ring = [(0.5 * math.cos(2 * math.pi * i / segments), 0.5 * math.sin(2 * math.pi * i / segments))
            for i in range(segments)]
    v = [(x, y, 0.0) for x, y in ring] + [(x, y, 1.0) for x, y in ring]
    f = [tuple(reversed(range(segments))), tuple(range(segments, 2 * segments))]
    f += [(i, (i + 1) % segments, segments + (i + 1) % segments, segments + i) for i in range(segments)]
    me = tag(bpy.data.meshes.new(name))
    me.from_pydata(v, [], f)
    me.materials.append(None)
    me.update()
    return me


def set_object_material(ob, mat):
    slot = ob.material_slots[0]
    slot.link = "OBJECT"
    slot.material = mat


def mesh_from_arrays(name, verts, quads, mat_index=None, materials=(), attrs=None, uvs=None):
    """A tagged mesh from numpy arrays: verts (n, 3), quads (m, 4) CCW from +Z,
    mat_index (m,), point-domain float attrs {name: (n,)}, uvs {name: (m*4, 2)}."""
    import numpy as np
    me = tag(bpy.data.meshes.new(name))
    verts = np.asarray(verts, np.float32)
    quads = np.asarray(quads, np.int32)
    me.vertices.add(len(verts))
    me.vertices.foreach_set("co", verts.ravel())
    me.loops.add(quads.size)
    me.loops.foreach_set("vertex_index", quads.ravel())
    me.polygons.add(len(quads))
    me.polygons.foreach_set("loop_start", np.arange(0, quads.size, 4, dtype=np.int32))
    for m in materials:
        me.materials.append(m)
    if mat_index is not None:
        me.polygons.foreach_set("material_index", np.asarray(mat_index, np.int32))
    me.update(calc_edges=True)
    for key, vals in (attrs or {}).items():
        a = me.attributes.new(key, "FLOAT", "POINT")
        a.data.foreach_set("value", np.asarray(vals, np.float32))
    for key, vals in (uvs or {}).items():
        uv = me.uv_layers.new(name=key)
        uv.data.foreach_set("uv", np.asarray(vals, np.float32).ravel())
    me.validate(verbose=False)
    return me

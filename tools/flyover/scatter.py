"""The forest in the scene: ONE point cloud (the vertex-only mesh Forest_Points in
Flyover_Forest) holding forest.py's table as point attributes, used by two objects:
Forest_Points, whose Geometry Nodes modifier (FO_ForestScatter) drops every point onto
the ground and instances the kit's bodies on it, and Forest_Cards (FO_ForestCards: the
same, instancing the leaf-card kit), which casts no shadow. No per-tree objects.

Point attributes: `kind` (INT, the kit index), `scale` (FLOAT_VECTOR), `rot` (FLOAT, z
turn), `sink` (FLOAT, how far the base goes below the ground), `seed` (FLOAT 0..1),
`crown` / `crown2` / `bark` (FLOAT_COLOR, linear palette colours); they propagate to the
instances, where the Forest_* materials read them as INSTANCER attributes.

Phase 10 (life.py): points carrying `sway` 1 (the trees near the flight) are dropped
here; the wind twins plant them again, pre-grounded the same way, and move them.

Grounding: a Raycast straight down onto the joined ground meshes (Ground_Terrain + every
Ground_<map>, the meshes the camera sees), so a trunk sits on the rendered surface
exactly, whatever the lattice resolution; its base then sinks by `sink`. Points that
miss the ground are dropped.
"""

import bpy
import numpy as np

import config
import scene

RAY_FROM_Z = 2000.0
RAY_LENGTH = 4000.0


def _points(table, col):
    me = scene.tag(bpy.data.meshes.new("Forest_Points"))
    n = len(table.x)
    me.vertices.add(n)
    co = np.stack([table.x, table.y, np.full(n, RAY_FROM_Z)], 1).astype(np.float32)
    me.vertices.foreach_set("co", co.ravel())
    for name, kind, vals in (("kind", "INT", table.kind), ("rot", "FLOAT", table.rot),
                             ("sink", "FLOAT", table.sink), ("seed", "FLOAT", table.seed),
                             ("scale", "FLOAT_VECTOR", table.scale),
                             ("crown", "FLOAT_COLOR", table.crown),
                             ("crown2", "FLOAT_COLOR", table.crown2),
                             ("bark", "FLOAT_COLOR", table.bark)):
        a = me.attributes.new(name, kind, "POINT")
        key = {"INT": "value", "FLOAT": "value", "FLOAT_VECTOR": "vector", "FLOAT_COLOR": "color"}[kind]
        a.data.foreach_set(key, np.asarray(vals).astype(np.int32 if kind == "INT" else np.float32).ravel())
    me.update()
    return scene.new_object("Forest_Points", me, col, instances=n)


class _Tree:
    def __init__(self, ng):
        self.ng = ng
        self.x = 0

    def node(self, kind, **props):
        n = self.ng.nodes.new(kind)
        n.location = (self.x, 0)
        self.x += 220
        for k, v in props.items():
            setattr(n, k, v)
        return n

    def link(self, a, b):
        self.ng.links.new(a, b)

    def attr(self, name, data_type):
        n = self.node("GeometryNodeInputNamedAttribute", data_type=data_type)
        n.inputs["Name"].default_value = name
        return n.outputs["Attribute"]


def _ground(t, grounds, pts):
    join = t.node("GeometryNodeJoinGeometry")
    for ob in grounds:
        oi = t.node("GeometryNodeObjectInfo", transform_space="RELATIVE")
        oi.inputs["Object"].default_value = ob
        t.link(oi.outputs["Geometry"], join.inputs["Geometry"])
    ray = t.node("GeometryNodeRaycast")
    t.link(join.outputs["Geometry"], ray.inputs["Target Geometry"])
    t.link(t.node("GeometryNodeInputPosition").outputs["Position"], ray.inputs["Source Position"])
    ray.inputs["Ray Direction"].default_value = (0.0, 0.0, -1.0)
    ray.inputs["Ray Length"].default_value = RAY_LENGTH
    down = t.node("ShaderNodeCombineXYZ")
    neg = t.node("ShaderNodeMath", operation="MULTIPLY")
    t.link(t.attr("sink", "FLOAT"), neg.inputs[0])
    neg.inputs[1].default_value = -1.0
    t.link(neg.outputs["Value"], down.inputs["Z"])
    at = t.node("ShaderNodeVectorMath", operation="ADD")
    t.link(ray.outputs["Hit Position"], at.inputs[0])
    t.link(down.outputs["Vector"], at.inputs[1])
    # drop the misses first: fields re-evaluate on each node's own geometry, so the ray
    # must still start from the cloud's original height. Phase 10: the near trees
    # (`sway` 1) are life.py's wind twins, dropped here too.
    miss = t.node("FunctionNodeBooleanMath", operation="NOT")
    t.link(ray.outputs["Is Hit"], miss.inputs[0])
    cmp = t.node("FunctionNodeCompare", data_type="FLOAT", operation="GREATER_THAN")
    t.link(t.attr("sway", "FLOAT"), cmp.inputs["A"])
    cmp.inputs["B"].default_value = 0.5
    either = t.node("FunctionNodeBooleanMath", operation="OR")
    t.link(miss.outputs["Boolean"], either.inputs[0])
    t.link(cmp.outputs["Result"], either.inputs[1])
    drop = t.node("GeometryNodeDeleteGeometry", domain="POINT")
    t.link(pts, drop.inputs["Geometry"])
    t.link(either.outputs["Boolean"], drop.inputs["Selection"])
    setp = t.node("GeometryNodeSetPosition")
    t.link(drop.outputs["Geometry"], setp.inputs["Geometry"])
    t.link(at.outputs["Vector"], setp.inputs["Position"])
    return setp.outputs["Geometry"]


def node_group(grounds, kit, name="FO_ForestScatter", placed=False, post=None):
    """The scatter: ground the points (a raycast; the misses and the points life.py marks
    `sway` are dropped) and instance the kit on them. placed=True: the points already
    sit on the ground (life.py's wind twins: no raycast, nothing dropped); post(g,
    instances) -> instances runs on the instances when given (life.wind_nodes)."""
    ng = scene.tag(bpy.data.node_groups.new(name, "GeometryNodeTree"))
    ng.interface.new_socket("Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
    ng.interface.new_socket("Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
    t = _Tree(ng)
    gin = t.node("NodeGroupInput")
    pts = gin.outputs["Geometry"]
    if not placed:
        pts = _ground(t, grounds, pts)
    kitn = t.node("GeometryNodeCollectionInfo", transform_space="ORIGINAL")
    kitn.inputs["Collection"].default_value = kit
    kitn.inputs["Separate Children"].default_value = True
    kitn.inputs["Reset Children"].default_value = True
    euler = t.node("ShaderNodeCombineXYZ")
    t.link(t.attr("rot", "FLOAT"), euler.inputs["Z"])
    rot = t.node("FunctionNodeEulerToRotation")
    t.link(euler.outputs["Vector"], rot.inputs[0])
    inst = t.node("GeometryNodeInstanceOnPoints")
    t.link(pts, inst.inputs["Points"])
    t.link(kitn.outputs["Instances"], inst.inputs["Instance"])
    inst.inputs["Pick Instance"].default_value = True
    t.link(t.attr("kind", "INT"), inst.inputs["Instance Index"])
    t.link(rot.outputs[0], inst.inputs["Rotation"])
    t.link(t.attr("scale", "FLOAT_VECTOR"), inst.inputs["Scale"])
    out = inst.outputs["Instances"]
    if post is not None:
        import materials
        g = materials.G(tree=ng, keep=True)
        g.x = t.x
        out = post(g, out)
    gout = t.node("NodeGroupOutput")
    t.link(out, gout.inputs[0])
    return ng


def build(table, grounds, kits, col):
    """The point cloud + its two scatter objects. grounds: the ground mesh objects;
    kits: (body kit collection, card kit collection) from trees.build."""
    kit, cards = kits
    ob = _points(table, col)
    mod = ob.modifiers.new("FO_ForestScatter", "NODES")
    mod.node_group = node_group(grounds, kit)
    for g, n in table.counts().items():
        ob[f"count_{g}"] = int(n)
    cob = scene.new_object("Forest_Cards", ob.data, col)
    cob.visible_shadow = False
    mod = cob.modifiers.new("FO_ForestCards", "NODES")
    mod.node_group = node_group(grounds, cards, "FO_ForestCards")
    return ob

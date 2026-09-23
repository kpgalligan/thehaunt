"""Procedural EEVEE materials for the ground and the road: flat, rough, palette-true.

Every colour comes from config.PALETTE. Variation is noise-driven value shifts between
palette NEIGHBOURS (a ramp's stops are palette colours), in world space, at scales of
metres, never finer than a few centimetres, so it survives a 480x270 pixel pass. No
image textures. Mesh attributes the ground carries (terrain.py): `rut`, `crown`,
`edge`, `litter`; the road mesh carries `urban` and the `RoadUV` map (u = metres along the road,
v = metres left of the centreline).

Each material's diffuse_color is its base palette colour, so Workbench stills (the
--diorama layout check) still read flat palette colours.
"""

import bpy

import config
import scene


def rgba(name):
    return config.hex_rgba(config.colour(name))


class G:
    """A tiny node-graph builder over one material's node tree."""

    def __init__(self, mat):
        mat.use_nodes = True
        self.nt = mat.node_tree
        self.nt.nodes.clear()
        self.x = 0

    def node(self, kind, **props):
        n = self.nt.nodes.new(kind)
        n.location = (self.x, 0)
        self.x += 200
        for k, v in props.items():
            setattr(n, k, v)
        return n

    def link(self, out, inp):
        self.nt.links.new(out, inp)

    @staticmethod
    def sock(coll, ident):
        for s in coll:
            if s.identifier == ident or s.name == ident:
                return s
        raise KeyError(ident)

    def _in(self, node, ident, value):
        s = self.sock(node.inputs, ident)
        if isinstance(value, bpy.types.NodeSocket):
            self.link(value, s)
        else:
            s.default_value = value

    def pos(self):
        return self.node("ShaderNodeNewGeometry").outputs["Position"]

    def uv(self, name):
        n = self.node("ShaderNodeUVMap")
        n.uv_map = name
        return n.outputs["UV"]

    def attr(self, name):
        n = self.node("ShaderNodeAttribute", attribute_name=name)
        return n.outputs["Fac"]

    def noise(self, vec, scale, detail=2.0, rough=0.5):
        n = self.node("ShaderNodeTexNoise")
        self._in(n, "Vector", vec)
        self._in(n, "Scale", scale)
        self._in(n, "Detail", detail)
        self._in(n, "Roughness", rough)
        return n.outputs["Fac"]

    def voronoi(self, vec, scale, feature="F1", rand=1.0):
        n = self.node("ShaderNodeTexVoronoi", feature=feature)
        self._in(n, "Vector", vec)
        self._in(n, "Scale", scale)
        self._in(n, "Randomness", rand)
        return n

    def white(self, vec):
        n = self.node("ShaderNodeTexWhiteNoise", noise_dimensions="3D")
        self._in(n, "Vector", vec)
        return n.outputs["Value"]

    def ramp(self, fac, stops, interp="LINEAR"):
        """stops: [(pos, palette name or rgba)]."""
        n = self.node("ShaderNodeValToRGB")
        cr = n.color_ramp
        cr.interpolation = interp
        while len(cr.elements) > 1:
            cr.elements.remove(cr.elements[-1])
        for k, (p, c) in enumerate(stops):
            e = cr.elements[0] if k == 0 else cr.elements.new(p)
            e.position = p
            e.color = rgba(c) if isinstance(c, str) else c
        self.link(fac, n.inputs["Fac"])
        return n.outputs["Color"]

    def mix(self, fac, a, b):
        n = self.node("ShaderNodeMix", data_type="RGBA", blend_type="MIX", clamp_factor=True)
        self._in(n, "Factor_Float", fac)
        self._in(n, "A_Color", rgba(a) if isinstance(a, str) else a)
        self._in(n, "B_Color", rgba(b) if isinstance(b, str) else b)
        return self.sock(n.outputs, "Result_Color")

    def math(self, op, a, b=0.0, clamp=False):
        n = self.node("ShaderNodeMath", operation=op, use_clamp=clamp)
        ins = [s for s in n.inputs if s.enabled]
        for s, v in zip(ins, (a, b)):
            if isinstance(v, bpy.types.NodeSocket):
                self.link(v, s)
            else:
                s.default_value = v
        return n.outputs["Value"]

    def vadd(self, vec, offset):
        n = self.node("ShaderNodeVectorMath", operation="ADD")
        self.link(vec, n.inputs[0])
        n.inputs[1].default_value = offset
        return n.outputs["Vector"]

    def step(self, x, edge, soft=0.0):
        """0 below edge, 1 above (smoothed over +-soft)."""
        if soft <= 0:
            return self.math("GREATER_THAN", x, edge)
        n = self.node("ShaderNodeMapRange", interpolation_type="SMOOTHSTEP")
        self._in(n, "Value", x)
        self._in(n, "From Min", edge - soft)
        self._in(n, "From Max", edge + soft)
        return n.outputs["Result"]

    def sep(self, vec):
        n = self.node("ShaderNodeSeparateXYZ")
        self.link(vec, n.inputs["Vector"])
        return n.outputs["X"], n.outputs["Y"], n.outputs["Z"]

    def combine(self, x, y, z=0.0):
        n = self.node("ShaderNodeCombineXYZ")
        for s, v in zip(n.inputs, (x, y, z)):
            if isinstance(v, bpy.types.NodeSocket):
                self.link(v, s)
            else:
                s.default_value = v
        return n.outputs["Vector"]

    def output(self, colour, rough=1.0):
        b = self.node("ShaderNodeBsdfPrincipled")
        self.link(colour, b.inputs["Base Color"])
        b.inputs["Roughness"].default_value = rough
        spec = b.inputs.get("Specular IOR Level")
        if spec is not None:
            spec.default_value = 0.15
        o = self.node("ShaderNodeOutputMaterial")
        self.link(b.outputs["BSDF"], o.inputs["Surface"])


# ---------------------------------------------------------------------------
# Shared looks
# ---------------------------------------------------------------------------

def _mottle(g, p, stops, scale=0.09, detail=3.0):
    return g.ramp(g.noise(p, scale, detail), stops)


def _flecks(g, p, base, colour, scale, thresh, seed_shift=0.0):
    """Sparse flecks of a palette colour (leaf litter, pebbles)."""
    q = g.vadd(p, (seed_shift, -seed_shift, 0.0)) if seed_shift else p
    f = g.step(g.noise(q, scale, 1.0, 0.3), thresh, 0.02)
    return g.mix(f, base, colour)


def _litter(g, p, c, amount=1.0):
    """Fallen leaves on open ground near the woods (the ground's `litter` attribute):
    sparse palette flecks, denser toward the treeline."""
    lit = g.math("MULTIPLY", g.attr("litter"), amount)
    for colour, scale, thresh, shift in (("earth-light", 1.2, 0.66, 0.0), ("earth-base", 1.5, 0.68, 5.1),
                                         ("lantern", 1.1, 0.74, 11.3)):
        q = g.vadd(p, (shift, -shift, 0.0)) if shift else p
        f = g.step(g.noise(q, scale, 1.0, 0.3), thresh, 0.02)
        c = g.mix(g.math("MULTIPLY", f, lit), c, colour)
    return c


def _grass(g, p):
    # autumn grass: the palette's mid / dark greens, a faint dry-gold variation
    c = _mottle(g, p, [(0.32, "green-dark"), (0.58, "green-mid"), (0.86, "green-base")], 0.07)
    dry = g.step(g.noise(p, 0.03, 2.0), 0.55, 0.08)                   # dry swathes
    c = g.mix(g.math("MULTIPLY", dry, 0.32), c, "earth-light")
    c = g.mix(g.math("MULTIPLY", g.step(g.noise(p, 0.45, 2.0), 0.62, 0.05), 0.18), c, "earth-base")
    c = _flecks(g, p, c, "earth-light", 1.1, 0.82)                     # a few fallen leaves
    c = _flecks(g, p, c, "wood-warm", 1.4, 0.85, 13.7)
    return _litter(g, p, c)


def _woods(g, p, centre=(0.0, 0.0)):
    c = _mottle(g, p, [(0.3, "earth-dark"), (0.55, "wood-warm"), (0.75, "earth-mid")], 0.12)
    moss = g.step(g.noise(p, 0.05, 2.0), 0.6, 0.08)
    c = g.mix(g.math("MULTIPLY", moss, 0.7), c, "green-dark")
    c = _flecks(g, p, c, "earth-light", 1.3, 0.8)                       # leaf litter
    c = _flecks(g, p, c, "earth-base", 1.0, 0.79, 7.3)
    c = _flecks(g, p, c, "lantern", 1.6, 0.86, 3.9)
    # far from town the floor between the canopy clumps reads as shade, not dirt
    x, y, _z = g.sep(p)
    d = g.math("SQRT", g.math("ADD", g.math("POWER", g.math("SUBTRACT", x, centre[0]), 2.0),
                              g.math("POWER", g.math("SUBTRACT", y, centre[1]), 2.0)))
    far = g.step(d, config.WOODS_SHADE_M[0] + config.WOODS_SHADE_M[1] / 2, config.WOODS_SHADE_M[1] / 2)
    shade = _mottle(g, p, [(0.35, "green-dark"), (0.6, "earth-dark")], 0.02)
    c = g.mix(g.math("MULTIPLY", far, 0.85), c, shade)
    # the road-outs' verge: rough autumn grass with a ragged edge into the woods
    verge = g.step(g.math("ADD", g.attr("verge"), g.math("MULTIPLY", g.noise(p, 0.35, 2.0), 0.6)),
                   0.75, 0.08)
    grass = _mottle(g, p, [(0.3, "green-dark"), (0.55, "green-mid"), (0.8, "earth-light")], 0.12)
    grass = _flecks(g, p, grass, "earth-light", 1.2, 0.72)
    grass = _flecks(g, p, grass, "earth-base", 1.5, 0.76, 5.1)
    return g.mix(verge, c, grass)


def _pasture(g, p):
    c = _mottle(g, p, [(0.3, "green-mid"), (0.55, "green-base"), (0.78, "green-light")], 0.06)
    dry = g.step(g.noise(p, 0.04, 2.0), 0.56, 0.08)
    c = g.mix(g.math("MULTIPLY", dry, 0.35), c, "earth-light")
    c = _flecks(g, p, c, "earth-light", 1.1, 0.84)
    return _litter(g, p, c)


def _dirt(g, p):
    c = _mottle(g, p, [(0.35, "earth-mid"), (0.62, "earth-base")], 0.25)
    c = g.mix(g.math("MULTIPLY", g.attr("rut"), 0.75), c, "earth-dark")
    crown = g.math("MULTIPLY", g.attr("crown"), g.step(g.noise(p, 0.9, 2.0), 0.42, 0.1))
    c = g.mix(crown, c, "green-mid")
    edge = g.math("MULTIPLY", g.step(g.attr("edge"), 0.12, 0.08), 1.0)   # 1 inside, 0 at edge
    tuft = g.math("MULTIPLY", g.math("SUBTRACT", 1.0, edge), g.step(g.noise(p, 1.2), 0.5, 0.1))
    c = g.mix(tuft, c, "green-base")
    return _flecks(g, p, c, "wood-warm", 1.5, 0.8)


def _path(g, p):
    c = _mottle(g, p, [(0.35, "earth-light"), (0.65, "earth-base")], 0.3)
    return _flecks(g, p, c, "wood-warm", 1.5, 0.82)


def _gravel(g, p):
    c = _mottle(g, p, [(0.35, "stone-base"), (0.6, "stone-light")], 0.4, 2.0)
    c = _flecks(g, p, c, "stone-pale", 6.0, 0.72)
    c = _flecks(g, p, c, "stone-shade", 5.0, 0.74, 3.1)
    edge = g.math("SUBTRACT", 1.0, g.step(g.attr("edge"), 0.15, 0.1))
    weed = g.math("MULTIPLY", edge, g.step(g.noise(p, 0.9), 0.55, 0.05))
    return g.mix(weed, c, "green-mid")


def _asphalt(g, p, weeds=True, cracks=True):
    c = _mottle(g, p, [(0.35, "stone-dark"), (0.55, "stone-dark"), (0.7, "stone-shade")], 0.06)
    patch = g.step(g.noise(p, 0.11, 1.0), 0.66, 0.005)                  # sealed patches
    c = g.mix(g.math("MULTIPLY", patch, 0.28), c, "ink-700")
    if cracks:
        v = g.voronoi(p, 0.4, "DISTANCE_TO_EDGE")
        line = g.math("LESS_THAN", v.outputs["Distance"], 0.02)
        where = g.step(g.noise(p, 0.07, 2.0), 0.63, 0.04)
        c = g.mix(g.math("MULTIPLY", g.math("MULTIPLY", line, where), 0.75), c, "ink-700")
    if weeds:
        edge = g.math("SUBTRACT", 1.0, g.step(g.attr("edge"), 0.18, 0.12))
        crack_weeds = g.math("MULTIPLY", g.step(g.noise(p, 0.8), 0.7, 0.03), 0.35)
        w = g.math("MULTIPLY", g.math("ADD", edge, crack_weeds), g.step(g.noise(p, 1.6), 0.45, 0.05))
        c = g.mix(w, c, g.ramp(g.noise(p, 2.0), [(0.4, "green-dark"), (0.6, "green-mid")]))
    return c


def _concrete(g, p):
    c = _mottle(g, p, [(0.35, "stone-light"), (0.6, "stone-pale")], 0.2)
    x, y, _z = g.sep(p)
    jx = g.math("LESS_THAN", g.math("PINGPONG", x, 0.625), 0.03)          # slab joints
    jy = g.math("LESS_THAN", g.math("PINGPONG", y, 0.625), 0.03)
    return g.mix(g.math("MAXIMUM", jx, jy), c, "stone-light")


COBBLE_SCALE, COBBLE_RAND = 3.4, 0.55     # setts ~0.3 m, laid near-regular


def _cobble(g, p, worn_xy):
    v = g.voronoi(p, COBBLE_SCALE, rand=COBBLE_RAND)
    col = g.ramp(g.sep(v.outputs["Color"])[0], [(0.3, "stone-base"), (0.8, "stone-light")])
    e = g.voronoi(p, COBBLE_SCALE, "DISTANCE_TO_EDGE", rand=COBBLE_RAND)
    mortar = g.math("LESS_THAN", e.outputs["Distance"], 0.06)
    c = g.mix(mortar, col, "stone-shade")
    # the plaza kerb: a hard stone-dark band where the cobble meets anything else
    kerb = g.math("SUBTRACT", 1.0, g.step(g.attr("edge"), 0.07, 0.01))
    c = g.mix(kerb, c, "stone-dark")
    if worn_xy is not None:
        # the ONE wrong stone: the cell whose feature point is the worn tile's
        t = g.voronoi(g.combine(worn_xy[0], worn_xy[1], 0.0), COBBLE_SCALE, rand=COBBLE_RAND)
        dist = g.node("ShaderNodeVectorMath", operation="DISTANCE")
        g.link(v.outputs["Position"], dist.inputs[0])
        g.link(t.outputs["Position"], dist.inputs[1])
        same = g.math("LESS_THAN", dist.outputs["Value"], 0.01)
        c = g.mix(same, c, g.mix(0.28, c, "stone-pale"))
    return c


SURFACE_LOOKS = {
    "Grass": _grass, "Woods": _woods, "Pasture": _pasture, "Dirt": _dirt, "Path": _path,
    "Gravel": _gravel, "Asphalt": _asphalt, "Concrete": _concrete,
    "Road": lambda g, p: _asphalt(g, p, weeds=False),
}


def _new(name, base):
    if name in bpy.data.materials:
        raise RuntimeError(f"material name '{name}' collides with an existing material")
    mat = scene.tag(bpy.data.materials.new(name))
    mat.diffuse_color = rgba(base)
    mat["hex"] = config.colour(base)
    return mat


def surface_materials(mats, world):
    """Procedural Surface_<kind> materials, registered in the shared cache (slot order
    = config.SURFACES, as on every ground mesh)."""
    worn = None
    for m in world.maps.values():
        for p in m.data["props"]:
            if p["kind"] == "worn_cobble":
                t = config.TILE_M
                worn = ((m.ox + p["x"] + 0.5) * t, -(m.oy + p["y"] + 0.5) * t)
    gx0, gy0, gx1, gy1 = world.bounds
    centre = ((gx0 + gx1) / 2 * config.TILE_M, -(gy0 + gy1) / 2 * config.TILE_M)
    for kind in config.SURFACES:
        mat = _new(f"Surface_{kind}", config.SURFACE_COLOURS[kind])
        g = G(mat)
        p = g.pos()
        if kind == "Cobble":
            colour = _cobble(g, p, worn)
        elif kind == "Woods":
            colour = _woods(g, p, centre)
        else:
            colour = SURFACE_LOOKS[kind](g, p)
        g.output(colour)
        mats.put(mat.name, mat)
    return worn


# ---------------------------------------------------------------------------
# The road
# ---------------------------------------------------------------------------

ROAD_SLOTS = ("Road_Verge", "Road_Kerb", "Road_Gutter", "Road_Asphalt")


def road_materials(mats):
    out = []
    # embankment / skirt: grassed earth (hidden under the verge in town)
    mat = _new("Road_Verge", "earth-mid")
    g = G(mat)
    p = g.pos()
    g.output(g.mix(g.step(g.noise(p, 0.6), 0.45, 0.1), "earth-mid", "green-mid"))
    out.append(mat)
    # kerb (concrete, in town) fading to a gravel shoulder (rural)
    mat = _new("Road_Kerb", "stone-pale")
    g = G(mat)
    p = g.pos()
    conc = _mottle(g, p, [(0.4, "stone-base"), (0.65, "stone-light")], 0.4)
    grav = _mottle(g, p, [(0.35, "stone-shade"), (0.6, "stone-base")], 0.5, 2.0)
    grav = _flecks(g, p, grav, "stone-light", 6.0, 0.74)
    grav = g.mix(g.math("MULTIPLY", g.step(g.noise(p, 0.3), 0.55, 0.1), 0.6), grav, "earth-mid")
    g.output(g.mix(g.attr("urban"), grav, conc))
    out.append(mat)
    # gutter pan (concrete) fading to the asphalt edge
    mat = _new("Road_Gutter", "stone-light")
    g = G(mat)
    p = g.pos()
    conc = _mottle(g, p, [(0.4, "stone-shade"), (0.65, "stone-base")], 0.3)
    g.output(g.mix(g.attr("urban"), _asphalt(g, p, weeds=False, cracks=False), conc))
    out.append(mat)
    # carriageway: asphalt, patches, cracks, the worn dashed centre line
    mat = _new("Road_Asphalt", "stone-dark")
    g = G(mat)
    p = g.pos()
    c = _asphalt(g, p, weeds=False)
    u, v, _ = g.sep(g.uv("RoadUV"))
    on, off = config.DASH_M
    period = on + off
    in_dash = g.math("LESS_THAN", g.math("MODULO", g.math("ADD", u, 1000.0), period), on)
    centre = g.math("LESS_THAN", g.math("ABSOLUTE", v), 0.075)
    dash_id = g.math("FLOOR", g.math("DIVIDE", g.math("ADD", u, 1000.0), period))
    keep = g.math("GREATER_THAN", g.white(g.combine(dash_id, 3.0, 0.0)), 0.25)   # plows took some
    wear = g.step(g.noise(p, 1.3, 2.0), 0.36, 0.08)
    line = g.math("MULTIPLY", g.math("MULTIPLY", in_dash, centre), g.math("MULTIPLY", keep, wear))
    c = g.mix(g.math("MULTIPLY", line, 0.85), c, "stone-pale")
    # a darker wheel-polished band in each lane
    lane = g.step(g.math("ABSOLUTE", g.math("SUBTRACT", g.math("ABSOLUTE", v), 1.25)), 0.45, 0.2)
    c = g.mix(g.math("MULTIPLY", g.math("SUBTRACT", 1.0, lane), 0.2), c, "ink-700")
    g.output(c)
    out.append(mat)
    for m in out:
        mats.put(m.name, m)
    return out


def paint_material(mats):
    """Faded lot paint (stall stripes, ramp lines): stone-pale worn toward the asphalt."""
    mat = _new("Paint_Faded", "stone-pale")
    g = G(mat)
    p = g.pos()
    wear = g.step(g.noise(p, 2.2, 2.0), 0.5, 0.12)
    g.output(g.mix(g.math("MULTIPLY", wear, 0.7), "stone-pale", "stone-shade"))
    mats.put(mat.name, mat)
    return mat

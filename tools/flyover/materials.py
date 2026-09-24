"""Procedural EEVEE materials for the ground and the road: rough, palette-led.

Every colour comes from config.PALETTE. Variation is noise-driven value shifts between
palette NEIGHBOURS (a ramp's stops are palette colours), in world space, down to
centimetre grain (blades, pebbles, setts) so the ground holds up at 4K from a low
camera, with a matching bump height.

The ground: ONE look (node group FO_GroundLook) paints every ground face. It reads
surfaces.Surfaces as three packed float images over the near rect (cubic filtered;
EXTEND outside, where every field says forest floor): the layer fields U (the top
layer with U > 0 shows, soft over config.SURF_KINDS' softness, a little shader-only
wobble on natural edges), `td` / `fade` (a track's ruts and grassy crown), `litter`
(fallen leaves near the woods), plus the mesh attribute `verge` (road-out verges).
A layer's own edge distance (U, minus what the next layer covers) drives its weeds,
tufts and the plaza kerb. Surface_<kind> materials are thin wrappers around the group
(identical shader code, compiled once); each keeps its kind's palette colour as
diffuse_color, so Workbench stills (the layout checks) still read the tiles.

The road mesh carries `urban` and the `RoadUV` map (u = metres along the road, v =
metres left of the centreline).
"""

import bpy

import config
import scene


def rgba(name):
    return config.hex_rgba(config.colour(name))


class G:
    """A tiny node-graph builder over one material's (or node group's) node tree."""

    def __init__(self, mat=None, tree=None, keep=False):
        """keep: build on in an existing tree (its nodes stay)."""
        if mat is not None:
            mat.use_nodes = True
            tree = mat.node_tree
        self.nt = tree
        if not keep:
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

    def fmix(self, fac, a, b):
        n = self.node("ShaderNodeMix", data_type="FLOAT", clamp_factor=True)
        self._in(n, "Factor_Float", fac)
        self._in(n, "A_Float", a)
        self._in(n, "B_Float", b)
        return self.sock(n.outputs, "Result_Float")

    def image(self, img, vec):
        n = self.node("ShaderNodeTexImage", interpolation="Cubic", extension="EXTEND")
        n.image = img
        self.link(vec, n.inputs["Vector"])
        return n

    def channels(self, img_node):
        """(r, g, b, a) float sockets of an image node (channel-packed)."""
        sp = self.node("ShaderNodeSeparateColor", mode="RGB")
        self.link(img_node.outputs["Color"], sp.inputs["Color"])
        return sp.outputs["Red"], sp.outputs["Green"], sp.outputs["Blue"], img_node.outputs["Alpha"]

    def gauss(self, x, centre, width):
        """exp(-((x - centre) / width)^2)."""
        t = self.math("DIVIDE", self.math("SUBTRACT", x, centre), width)
        return self.math("EXPONENT", self.math("MULTIPLY", self.math("MULTIPLY", t, t), -1.0))

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

    def output(self, colour, rough=1.0, height=None, bump=(0.35, 0.03)):
        b = self.node("ShaderNodeBsdfPrincipled")
        self.link(colour, b.inputs["Base Color"])
        b.inputs["Roughness"].default_value = rough
        if height is not None:
            bn = self.node("ShaderNodeBump")
            bn.inputs["Strength"].default_value = bump[0]
            bn.inputs["Distance"].default_value = bump[1]
            self.link(height, bn.inputs["Height"])
            self.link(bn.outputs["Normal"], b.inputs["Normal"])
        spec = b.inputs.get("Specular IOR Level")
        if spec is not None:
            spec.default_value = 0.15
        o = self.node("ShaderNodeOutputMaterial")
        self.link(b.outputs["BSDF"], o.inputs["Surface"])


# ---------------------------------------------------------------------------
# Shared looks. Each look(g, p, ctx) -> (colour, height 0..1); ctx holds the ground
# look's sockets: edge (metres into this layer's own part), td, fade, litter.
# ---------------------------------------------------------------------------

def _mottle(g, p, stops, scale=0.09, detail=3.0):
    return g.ramp(g.noise(p, scale, detail), stops)


def _flecks(g, p, base, colour, scale, thresh, seed_shift=0.0, amount=1.0):
    """Sparse flecks of a palette colour (leaf litter, pebbles)."""
    q = g.vadd(p, (seed_shift, -seed_shift, 0.0)) if seed_shift else p
    f = g.step(g.noise(q, scale, 1.0, 0.3), thresh, 0.02)
    if amount != 1.0:
        f = g.math("MULTIPLY", f, amount)
    return g.mix(f, base, colour)


def _near_edge(g, ctx, width, soft):
    """1 at this layer's edge, 0 from `width` metres in."""
    return g.math("SUBTRACT", 1.0, g.step(ctx["edge"], width, soft))


LEAF_TINTS = (("earth-light", 1.2, 0.64, 0.0), ("earth-base", 1.5, 0.67, 5.1),
              ("lantern", 1.1, 0.72, 11.3), ("wood-warm", 1.7, 0.74, 17.9))


def _leaves(g, p, c, amount):
    """Fallen leaves: sparse palette flecks at leaf scale, `amount` 0..1 (socket)."""
    for colour, scale, thresh, shift in LEAF_TINTS:
        q = g.vadd(p, (shift, -shift, 0.0)) if shift else p
        f = g.step(g.noise(q, scale * 3.2, 1.0, 0.3), thresh, 0.03)
        c = g.mix(g.math("MULTIPLY", f, amount), c, colour)
    return c


def _turf(g, p, stops, dry=0.3):
    """Real turf: big mottle, metre clumps, blade grain, dry swathes, clover."""
    c = _mottle(g, p, stops, 0.07)
    clump = g.noise(p, 1.4, 3.0, 0.6)
    c = g.mix(g.math("MULTIPLY", g.step(clump, 0.6, 0.12), 0.35), c, "green-light")
    c = g.mix(g.math("MULTIPLY", g.math("SUBTRACT", 1.0, g.step(clump, 0.38, 0.12)), 0.35), c, "green-dark")
    blade = g.noise(p, 26.0, 2.0, 0.7)
    c = g.mix(g.math("MULTIPLY", g.step(blade, 0.56, 0.1), 0.22), c, "green-pale")
    c = g.mix(g.math("MULTIPLY", g.math("SUBTRACT", 1.0, g.step(blade, 0.42, 0.1)), 0.25), c, "green-dark")
    swathe = g.step(g.noise(p, 0.03, 2.0), 0.55, 0.08)
    c = g.mix(g.math("MULTIPLY", swathe, dry), c, "earth-light")
    straw = g.math("MULTIPLY", g.step(g.noise(p, 3.1, 2.0), 0.64, 0.05), 0.4)
    c = g.mix(g.math("MULTIPLY", straw, swathe), c, "earth-base")
    clover = g.step(g.noise(g.vadd(p, (3.3, 1.7, 0.0)), 0.8, 2.0), 0.66, 0.04)
    c = g.mix(g.math("MULTIPLY", clover, 0.45), c, "green-base")
    h = g.math("ADD", g.math("MULTIPLY", blade, 0.7), g.math("MULTIPLY", clump, 0.3))
    h = g.math("ADD", h, g.math("MULTIPLY", clover, 0.15))
    return c, h


def _grass(g, p, ctx):
    c, h = _turf(g, p, [(0.32, "green-dark"), (0.58, "green-mid"), (0.86, "green-base")])
    c = _flecks(g, p, c, "earth-light", 1.1, 0.84)                       # a few fallen leaves
    c = _leaves(g, p, c, g.math("MULTIPLY", ctx["litter"], 0.9))
    return c, h


def _pasture(g, p, ctx):
    c, h = _turf(g, p, [(0.3, "green-mid"), (0.55, "green-base"), (0.8, "green-light")], dry=0.36)
    c = _leaves(g, p, c, g.math("MULTIPLY", ctx["litter"], 0.8))
    return c, h


def _woods(g, p, ctx, centre=(0.0, 0.0)):
    c = _mottle(g, p, [(0.3, "earth-dark"), (0.55, "wood-warm"), (0.75, "earth-mid")], 0.12)
    moss = g.step(g.noise(p, 0.05, 2.0), 0.6, 0.08)
    c = g.mix(g.math("MULTIPLY", moss, 0.7), c, "green-dark")
    c = _leaves(g, p, c, 1.0)
    # the first metres into the woods: grass creeping in, in ragged patches
    creep = g.math("ADD", ctx["open"], g.math("MULTIPLY", g.noise(p, 0.45, 3.0), 3.0))
    creep = g.step(creep, 0.2, 0.25)
    turf, _th = _turf(g, p, [(0.3, "green-dark"), (0.6, "green-mid"), (0.85, "earth-light")], dry=0.3)
    c = g.mix(g.math("MULTIPLY", creep, 0.85), c, _leaves(g, p, turf, 0.6))
    grain = g.noise(p, 9.0, 2.0, 0.6)
    c = g.mix(g.math("MULTIPLY", g.step(grain, 0.62, 0.08), 0.3), c, "earth-dark")
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
    grass, gh = _turf(g, p, [(0.3, "green-dark"), (0.55, "green-mid"), (0.8, "earth-light")], dry=0.4)
    grass = _flecks(g, p, grass, "earth-light", 1.2, 0.72)
    grass = _flecks(g, p, grass, "earth-base", 1.5, 0.76, 5.1)
    h = g.fmix(verge, g.math("MULTIPLY", grain, 0.6), gh)
    return g.mix(verge, c, grass), h


def _dirt(g, p, ctx):
    c = _mottle(g, p, [(0.35, "earth-mid"), (0.62, "earth-base")], 0.25)
    grit = g.noise(p, 14.0, 2.0, 0.6)
    c = g.mix(g.math("MULTIPLY", g.step(grit, 0.6, 0.06), 0.3), c, "earth-light")
    c = _flecks(g, p, c, "stone-base", 7.0, 0.72)                        # pebbles
    c = _flecks(g, p, c, "wood-warm", 1.5, 0.8)
    off, w = config.TRACK_RUT_M
    rut = g.math("MULTIPLY", g.gauss(ctx["td"], off, w), g.math("SUBTRACT", 1.0, ctx["fade"]))
    c = g.mix(g.math("MULTIPLY", rut, 0.7), c, "earth-dark")
    # the crown between the ruts (and a track's grassed-over end)
    mid = g.math("MAXIMUM", g.math("SUBTRACT", 1.0, g.step(ctx["td"], 0.36, 0.12)), ctx["fade"])
    mid = g.math("MINIMUM", mid, g.math("SUBTRACT", 1.0, g.step(ctx["td"], 2.2, 0.3)))
    tuft = g.math("MULTIPLY", g.step(g.noise(p, 2.8, 3.0, 0.6), 0.44, 0.12), 0.85)
    turf = g.mix(g.step(g.noise(p, 9.0, 2.0), 0.5, 0.2), "green-mid", "green-base")
    c = g.mix(g.math("MULTIPLY", mid, tuft), c, turf)
    # weeds and worn grass along the verge
    edge = _near_edge(g, ctx, 0.4, 0.25)
    weeds = g.math("MULTIPLY", g.step(g.noise(p, 1.3), 0.45, 0.1), g.step(g.noise(p, 7.0, 2.0), 0.5, 0.06))
    c = g.mix(g.math("MULTIPLY", edge, weeds), c, "green-mid")
    h = g.math("SUBTRACT", g.math("MULTIPLY", grit, 0.5), g.math("MULTIPLY", rut, 0.8))
    return c, h


def _path(g, p, ctx):
    c = _mottle(g, p, [(0.35, "earth-light"), (0.65, "earth-base")], 0.3)
    grit = g.noise(p, 16.0, 2.0, 0.6)
    c = g.mix(g.math("MULTIPLY", g.step(grit, 0.62, 0.06), 0.25), c, "earth-mid")
    c = _flecks(g, p, c, "wood-warm", 1.5, 0.82)
    edge = _near_edge(g, ctx, 0.35, 0.2)
    weeds = g.math("MULTIPLY", g.step(g.noise(p, 1.4), 0.45, 0.1), g.step(g.noise(p, 7.5, 2.0), 0.5, 0.06))
    c = g.mix(g.math("MULTIPLY", edge, weeds), c, "green-mid")
    return c, g.math("MULTIPLY", grit, 0.4)


def _gravel(g, p, ctx):
    stones = g.voronoi(p, 11.0, rand=0.9)
    col = g.ramp(g.sep(stones.outputs["Color"])[0],
                 [(0.2, "stone-shade"), (0.45, "stone-base"), (0.75, "stone-light"), (0.95, "stone-pale")])
    c = g.mix(0.35, col, _mottle(g, p, [(0.35, "stone-base"), (0.6, "stone-light")], 0.4, 2.0))
    worn = g.step(g.noise(p, 0.18, 2.0), 0.6, 0.08)                     # dirt showing through
    c = g.mix(g.math("MULTIPLY", worn, 0.45), c, "earth-mid")
    edge = _near_edge(g, ctx, 0.6, 0.3)
    weed = g.math("MULTIPLY", edge, g.step(g.noise(p, 0.9), 0.5, 0.06))
    c = g.mix(weed, c, "green-mid")
    h = g.math("SUBTRACT", 1.0, g.math("MULTIPLY", stones.outputs["Distance"], 1.6))
    return c, h


def _asphalt(g, p, ctx=None, weeds=True, cracks=True):
    c = _mottle(g, p, [(0.35, "stone-dark"), (0.55, "stone-dark"), (0.7, "stone-shade")], 0.06)
    agg = g.noise(p, 30.0, 1.0, 0.5)                                    # aggregate grain
    c = g.mix(g.math("MULTIPLY", g.step(agg, 0.62, 0.05), 0.25), c, "stone-shade")
    patch = g.step(g.noise(p, 0.11, 1.0), 0.66, 0.005)                  # sealed patches
    c = g.mix(g.math("MULTIPLY", patch, 0.28), c, "ink-700")
    line = 0.0
    if cracks:
        v = g.voronoi(p, 0.4, "DISTANCE_TO_EDGE")
        line = g.math("LESS_THAN", v.outputs["Distance"], 0.02)
        where = g.step(g.noise(p, 0.07, 2.0), 0.63, 0.04)
        line = g.math("MULTIPLY", line, where)
        c = g.mix(g.math("MULTIPLY", line, 0.75), c, "ink-700")
    if weeds and ctx is not None:
        edge = _near_edge(g, ctx, 0.35, 0.2)
        crack_weeds = g.math("MULTIPLY", line if cracks else 0.0, 0.8)
        w = g.math("MULTIPLY", g.math("ADD", edge, crack_weeds), g.step(g.noise(p, 1.6), 0.45, 0.05))
        c = g.mix(w, c, g.ramp(g.noise(p, 2.0), [(0.4, "green-dark"), (0.6, "green-mid")]))
    h = g.math("SUBTRACT", g.math("MULTIPLY", agg, 0.3), line if cracks else 0.0)
    return c, h


def _concrete(g, p, ctx):
    c = _mottle(g, p, [(0.35, "stone-light"), (0.6, "stone-pale")], 0.2)
    stain = g.step(g.noise(p, 0.5, 2.0), 0.62, 0.1)
    c = g.mix(g.math("MULTIPLY", stain, 0.3), c, "stone-base")
    x, y, _z = g.sep(p)
    jx = g.math("LESS_THAN", g.math("PINGPONG", x, 0.625), 0.02)          # slab joints, 1.25 m
    jy = g.math("LESS_THAN", g.math("PINGPONG", y, 0.625), 0.02)
    joint = g.math("MAXIMUM", jx, jy)
    c = g.mix(joint, c, "stone-base")
    edge = _near_edge(g, ctx, 0.15, 0.08)
    c = g.mix(g.math("MULTIPLY", edge, 0.35), c, "stone-base")
    return c, g.math("SUBTRACT", g.math("MULTIPLY", g.noise(p, 20.0), 0.2), joint)


COBBLE_SCALE, COBBLE_RAND = 3.4, 0.55     # setts ~0.3 m, laid near-regular


def _cobble(g, p, ctx, worn_xy=None):
    v = g.voronoi(p, COBBLE_SCALE, rand=COBBLE_RAND)
    col = g.ramp(g.sep(v.outputs["Color"])[0], [(0.3, "stone-base"), (0.8, "stone-light")])
    e = g.voronoi(p, COBBLE_SCALE, "DISTANCE_TO_EDGE", rand=COBBLE_RAND)
    mortar = g.step(e.outputs["Distance"], 0.06, 0.02)                  # 0 in the joint
    c = g.mix(g.math("SUBTRACT", 1.0, mortar), col, "stone-shade")
    # the plaza kerb: a hard stone-dark band where the cobble meets anything else
    kerb = _near_edge(g, ctx, 0.25, 0.01)
    c = g.mix(kerb, c, "stone-dark")
    if worn_xy is not None:
        # the ONE wrong stone: the cell whose feature point is the worn tile's
        t = g.voronoi(g.combine(worn_xy[0], worn_xy[1], 0.0), COBBLE_SCALE, rand=COBBLE_RAND)
        dist = g.node("ShaderNodeVectorMath", operation="DISTANCE")
        g.link(v.outputs["Position"], dist.inputs[0])
        g.link(t.outputs["Position"], dist.inputs[1])
        same = g.math("LESS_THAN", dist.outputs["Value"], 0.01)
        c = g.mix(same, c, g.mix(0.28, c, "stone-pale"))
    dome = g.math("SQRT", g.math("MINIMUM", g.math("MULTIPLY", e.outputs["Distance"], 6.0), 1.0))
    return c, dome


LOOKS = {"Grass": _grass, "Pasture": _pasture, "Dirt": _dirt, "Path": _path, "Gravel": _gravel,
         "Asphalt": _asphalt, "Concrete": _concrete, "Cobble": _cobble}
NATURAL_EDGE = ("Grass", "Pasture", "Dirt", "Path", "Gravel")


def _new(name, base):
    if name in bpy.data.materials:
        raise RuntimeError(f"material name '{name}' collides with an existing material")
    mat = scene.tag(bpy.data.materials.new(name))
    mat.diffuse_color = rgba(base)
    mat["hex"] = config.colour(base)
    return mat


def _images(surf):
    out = []
    for name, arr in surf.images():
        if name in bpy.data.images:
            raise RuntimeError(f"image name '{name}' collides with an existing image")
        H, W, _ = arr.shape
        img = scene.tag(bpy.data.images.new(name, W, H, alpha=True, float_buffer=True))
        img.colorspace_settings.name = "Non-Color"
        img.alpha_mode = "CHANNEL_PACKED"
        img.pixels.foreach_set(arr.ravel())
        img.file_format = "OPEN_EXR"
        img.pack()
        out.append(img)
    return out


def ground_look(world, surf, worn, centre):
    """The FO_GroundLook node group: Color + Height for every ground face."""
    import surfaces
    ng = scene.tag(bpy.data.node_groups.new("FO_GroundLook", "ShaderNodeTree"))
    ng.interface.new_socket("Color", in_out="OUTPUT", socket_type="NodeSocketColor")
    ng.interface.new_socket("Height", in_out="OUTPUT", socket_type="NodeSocketFloat")
    g = G(tree=ng)
    p = g.pos()
    x0, y0, x1, y1 = surf.rect()
    x, y, _z = g.sep(p)
    uv = g.combine(g.math("DIVIDE", g.math("SUBTRACT", x, x0), x1 - x0),
                   g.math("DIVIDE", g.math("SUBTRACT", y, y0), y1 - y0), 0.0)
    ch = {}
    for img, names in zip(_images(surf), [surfaces.CHANNELS[k:k + 4] for k in range(0, 12, 4)]):
        for name, sock in zip(names, g.channels(g.image(img, uv))):
            ch[name] = sock
    wl, amp = config.SURF_DETAIL_M
    wobble = g.math("MULTIPLY", g.math("SUBTRACT", g.noise(p, 1.0 / wl, 2.0), 0.5), 2 * amp)
    ctx = dict(td=ch["td"], fade=ch["fade"], litter=ch["litter"], open=ch[surfaces.LAYERS[0]])
    c, h = _woods(g, p, ctx, centre)
    layers = surfaces.LAYERS
    for k, name in enumerate(layers):
        u = ch[name]
        own = u if k + 1 == len(layers) else g.math("MINIMUM", u, g.math("MULTIPLY", ch[layers[k + 1]], -1.0))
        lctx = dict(ctx, edge=own)
        if name == "Cobble":
            lc, lh = _cobble(g, p, lctx, worn)
        else:
            lc, lh = LOOKS[name](g, p, lctx)
        soft = surfaces.SOFT[name]
        uu = g.math("ADD", u, wobble) if name in NATURAL_EDGE else u
        m = g.step(uu, 0.0, soft)
        c = g.mix(m, c, lc)
        h = g.fmix(m, h, lh)
    out = g.node("NodeGroupOutput")
    g.link(c, out.inputs["Color"])
    g.link(h, out.inputs["Height"])
    return ng


def surface_materials(mats, world, terrain):
    """Surface_<kind> materials (slot order = config.SURFACES, as on every ground mesh),
    all wrapping the one ground look; registered in the shared cache."""
    worn = None
    for m in world.maps.values():
        for p in m.data["props"]:
            if p["kind"] == "worn_cobble":
                t = config.TILE_M
                worn = ((m.ox + p["x"] + 0.5) * t, -(m.oy + p["y"] + 0.5) * t)
    gx0, gy0, gx1, gy1 = world.bounds
    centre = ((gx0 + gx1) / 2 * config.TILE_M, -(gy0 + gy1) / 2 * config.TILE_M)
    ng = ground_look(world, terrain.surfaces, worn, centre)
    for kind in config.SURFACES:
        mat = _new(f"Surface_{kind}", config.SURFACE_COLOURS[kind])
        g = G(mat)
        grp = g.node("ShaderNodeGroup")
        grp.node_tree = ng
        g.output(grp.outputs["Color"], rough=0.92, height=grp.outputs["Height"])
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
    g.output(g.mix(g.attr("urban"), _asphalt(g, p, weeds=False, cracks=False)[0], conc))
    out.append(mat)
    # carriageway: asphalt, patches, cracks, the worn dashed centre line
    mat = _new("Road_Asphalt", "stone-dark")
    g = G(mat)
    p = g.pos()
    c = _asphalt(g, p, weeds=False)[0]
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

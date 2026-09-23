"""The forest kit: one low-poly, flat-shaded prototype mesh per variant in
config.FOREST_KIT order (FK_<nn>_<family>_<A..>), in the Flyover_ForestKit collection
(excluded from the view layer; scatter.py instances it), plus the two forest
materials.

The trees read as the GAME's trees in 3D (docs/designs: the leafy tree is a lumpy,
few-faceted crown on a plain trunk; the bare tree a trunk forking into upswept limbs):
crowns are jittered icosahedron lobes (20 faces each), conifers stacked 6-7 sided cones,
trunks 5-sided prisms. Origin at the trunk base, metres at scale 1, deterministic.

Material slot 0 = Forest_Bark, slot 1 = Forest_Crown. Colours are NOT in the kit: each
instance carries palette colours (`crown`, `crown2`, `bark`, forest.py) that the
materials read as INSTANCER attributes; the kit's face attribute `alt` (1 on a crown's
under-lobes, a boulder's moss, a tuft's tips) switches `crown` -> `crown2`.
"""

import math
import random

import bmesh
import bpy

import config
import forest
import scene

_IC = None


def _ico():
    global _IC
    if _IC is None:
        p = (1 + 5 ** 0.5) / 2
        v = [(-1, p, 0), (1, p, 0), (-1, -p, 0), (1, -p, 0), (0, -1, p), (0, 1, p),
             (0, -1, -p), (0, 1, -p), (p, 0, -1), (p, 0, 1), (-p, 0, -1), (-p, 0, 1)]
        n = math.sqrt(1 + p * p)
        v = [(a / n, b / n, c / n) for a, b, c in v]
        f = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4),
             (11, 10, 2), (10, 7, 6), (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8),
             (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1)]
        _IC = (v, f)
    return _IC


class Kit:
    """Accumulates parts: verts, faces, per-face material index and `alt`."""

    def __init__(self, seed):
        self.rng = random.Random(seed)
        self.v, self.f, self.mat, self.alt = [], [], [], []

    def _face(self, idx, mat, alt):
        self.f.append(idx)
        self.mat.append(mat)
        self.alt.append(alt)

    def lobe(self, c, r, jitter=0.2, alt=0, flat_bottom=0.0):
        """A jittered icosahedron: centre c, radii r (x, y, z). flat_bottom squashes the
        lower half (a crown's underside)."""
        v, f = _ico()
        b = len(self.v)
        rot = self.rng.random() * math.tau
        for x, y, z in v:
            k = 1 + jitter * (self.rng.random() * 2 - 1)
            x, y = x * math.cos(rot) - y * math.sin(rot), x * math.sin(rot) + y * math.cos(rot)
            zz = z * (1 - flat_bottom) if z < 0 else z
            self.v.append((c[0] + x * r[0] * k, c[1] + y * r[1] * k, c[2] + zz * r[2] * k))
        for tri in f:
            under = alt or (sum(v[i][2] for i in tri) / 3 < -0.35)
            self._face([b + i for i in tri], 1, 1 if under and alt != -1 else 0)
        return self

    def prism(self, p0, p1, r0, r1, sides=5, mat=0, cap=True, alt=0):
        """A tapered prism from p0 to p1 (any direction)."""
        ax = [p1[i] - p0[i] for i in range(3)]
        L = math.sqrt(sum(a * a for a in ax)) or 1.0
        ax = [a / L for a in ax]
        up = (0, 0, 1) if abs(ax[2]) < 0.9 else (1, 0, 0)
        u = _norm(_cross(ax, up))
        w = _cross(ax, u)
        b = len(self.v)
        ph = self.rng.random() * math.tau
        for p, r in ((p0, r0), (p1, r1)):
            for k in range(sides):
                a = ph + math.tau * k / sides
                self.v.append(tuple(p[i] + r * (math.cos(a) * u[i] + math.sin(a) * w[i]) for i in range(3)))
        for k in range(sides):
            k1 = (k + 1) % sides
            self._face([b + k, b + k1, b + sides + k1, b + sides + k], mat, alt)
        if cap:
            self._face([b + sides + k for k in range(sides)], mat, alt)
        return self

    def cone(self, z0, h, r, sides=7, off=(0.0, 0.0), tip=(0.0, 0.0), alt=0, droop=0.0):
        """A conifer tier: a jittered ring at z0 (drooping droop m at the rim) to an apex."""
        b = len(self.v)
        ph = self.rng.random() * math.tau
        for k in range(sides):
            a = ph + math.tau * k / sides
            rr = r * (1 + 0.18 * (self.rng.random() * 2 - 1))
            self.v.append((off[0] + rr * math.cos(a), off[1] + rr * math.sin(a),
                           z0 - droop * (0.6 + 0.4 * self.rng.random())))
        self.v.append((off[0] + tip[0], off[1] + tip[1], z0 + h))
        apex = b + sides
        for k in range(sides):
            self._face([b + k, b + (k + 1) % sides, apex], 1, alt)
        self._face([b + k for k in reversed(range(sides))], 1, 1)
        return self

    def blade(self, a, h, w, lean):
        """A grass blade: one tall triangle leaning outwards."""
        b = len(self.v)
        ca, sa = math.cos(a), math.sin(a)
        self.v += [(-sa * w, ca * w, 0.0), (sa * w, -ca * w, 0.0), (ca * lean, sa * lean, h)]
        self._face([b, b + 1, b + 2], 1, 0)
        return self

    def mesh(self, name):
        me = scene.tag(bpy.data.meshes.new(name))
        bm = bmesh.new()
        verts = [bm.verts.new(p) for p in self.v]
        alt = bm.faces.layers.float.new("alt")
        for idx, mat, a in zip(self.f, self.mat, self.alt):
            try:
                face = bm.faces.new([verts[i] for i in idx])
            except ValueError:     # a duplicate face (degenerate jitter): skip
                continue
            face.material_index = mat
            face[alt] = float(a)
            face.smooth = False
        bmesh.ops.recalc_face_normals(bm, faces=[f for f in bm.faces if f.calc_area() > 1e-9])
        bm.to_mesh(me)
        bm.free()
        return me


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _norm(a):
    L = math.sqrt(sum(x * x for x in a)) or 1.0
    return tuple(x / L for x in a)


# ---------------------------------------------------------------------------
# The variants (metres at scale 1; the planting scales them)
# ---------------------------------------------------------------------------

def _maple(k, v):
    """Sugar maple: a rounded, lumpy crown on a short trunk (the game's leafy tree)."""
    h, w = ((12.5, 3.6), (14.0, 3.1), (11.5, 4.2))[v]
    k.prism((0, 0, 0), (0.15, 0.1, h * 0.5), 0.3, 0.2, cap=False)
    k.lobe((0, 0, h * 0.68), (w, w, h * 0.27), flat_bottom=0.35)
    for i in range((3, 3, 4)[v]):
        a = math.tau * i / (3, 3, 4)[v] + k.rng.random() * 0.8
        rr = w * (0.55 + 0.15 * k.rng.random())
        k.lobe((math.cos(a) * rr, math.sin(a) * rr, h * (0.55 + 0.18 * k.rng.random())),
               (w * 0.6, w * 0.6, h * 0.2), flat_bottom=0.3)
    k.lobe((0.3, -0.2, h * 0.86), (w * 0.62, w * 0.62, h * 0.16))


def _birch(k, v):
    """Paper birch: pale slender trunk(s), an airy crown of small lobes high up."""
    stems = ((0.25, 0.0),) if v == 0 else ((0.9, 0.35), (-0.8, -0.3))
    for lx, ly in stems:
        h = 12.0 * (1 if v == 0 else 0.9 + 0.1 * k.rng.random())
        top = (lx * 1.4, ly * 1.4, h * 0.82)
        k.prism((0, 0, 0), top, 0.17, 0.09, cap=False)
        for i in range(3):
            a = k.rng.random() * math.tau
            c = (top[0] + math.cos(a) * 1.1, top[1] + math.sin(a) * 1.1, h * (0.62 + 0.12 * i))
            k.lobe(c, (1.7, 1.7, 1.9), jitter=0.28, flat_bottom=0.2)
        k.lobe((top[0], top[1], h * 0.95), (1.2, 1.2, 1.5), jitter=0.25)


def _oak(k, v):
    """Red / white oak: a thick short trunk, spreading limbs, a broad flat crown."""
    h, w = ((13.5, 5.0), (12.0, 4.4))[v]
    k.prism((0, 0, 0), (0, 0, h * 0.42), 0.42, 0.3, cap=False)
    for i in range(3):
        a = math.tau * i / 3 + 0.4 * v
        k.prism((0, 0, h * 0.36), (math.cos(a) * w * 0.55, math.sin(a) * w * 0.55, h * 0.6),
                0.22, 0.12, sides=4, cap=False)
    k.lobe((0, 0, h * 0.72), (w * 0.85, w * 0.85, h * 0.2), flat_bottom=0.5)
    for i in range(5 - v):
        a = math.tau * i / (5 - v) + k.rng.random() * 0.6
        rr = w * (0.6 + 0.15 * k.rng.random())
        k.lobe((math.cos(a) * rr, math.sin(a) * rr, h * (0.62 + 0.1 * k.rng.random())),
               (w * 0.48, w * 0.48, h * 0.17), flat_bottom=0.45)


def _pine(k, v):
    """Eastern white pine: tall, open, layered, asymmetric tiers (flattened cones)."""
    h = (19.0, 17.0)[v]
    k.prism((0, 0, 0), (0.2, 0, h * 0.97), 0.34, 0.1, cap=False)
    tiers = ((0.36, 3.4), (0.5, 3.0), (0.63, 2.6), (0.75, 2.0), (0.86, 1.3))[:5 - v]
    for i, (z, r) in enumerate(tiers):
        a = k.rng.random() * math.tau
        off = (math.cos(a) * 0.8, math.sin(a) * 0.8)
        k.cone(h * z, h * 0.12, r, sides=6, off=off, droop=0.35, alt=0 if i % 2 else 1)
    k.cone(h * 0.9, h * 0.1, 0.9, sides=5, off=(0.2, 0.0))


def _hemlock(k, v):
    """Eastern hemlock: dense, dark, conical to the ground, a nodding leader."""
    h, w = ((16.0, 3.6), (18.0, 2.9))[v]
    k.prism((0, 0, 0), (0, 0, h * 0.35), 0.36, 0.28, cap=False)
    n = 4
    for i in range(n):
        z = h * (0.17 + 0.18 * i)
        r = w * (0.95 - 0.2 * i)
        k.cone(z, h * 0.34, r, sides=7, droop=0.5, alt=1 if i == 0 else 0,
               tip=(0.0, 0.0) if i < n - 1 else (0.5, 0.2))


def _bare(k, v):
    """The game's bare tree: a trunk forking into upswept limbs; B is a broken snag."""
    if v == 0:
        k.prism((0, 0, 0), (0, 0, 4.2), 0.3, 0.22, cap=False)
        for i in range(4):
            a = math.tau * i / 4 + k.rng.random() * 0.5
            tip = (math.cos(a) * 2.6, math.sin(a) * 2.6, 9.0 + k.rng.random() * 1.5)
            k.prism((0, 0, 4.0), tip, 0.18, 0.05, sides=4, cap=False)
            mid = tuple(0.5 * (4.0 * (i2 == 2)) + 0.5 * tip[i2] for i2 in range(3))
            k.prism(mid, (mid[0] + math.cos(a + 0.9) * 1.6, mid[1] + math.sin(a + 0.9) * 1.6,
                          mid[2] + 2.2), 0.08, 0.03, sides=3, cap=False)
    else:
        k.prism((0, 0, 0), (0.3, 0.1, 7.5), 0.34, 0.22, cap=True)
        for z, a in ((4.5, 0.5), (6.0, 2.9)):
            k.prism((0.15, 0.05, z), (math.cos(a) * 1.8, math.sin(a) * 1.8, z + 1.6),
                    0.12, 0.05, sides=4, cap=False)


def _clump(k, v):
    """A far canopy clump: a patch of forest as a handful of lobes, two tones."""
    n = (6, 5, 7)[v]
    R = (10.0, 9.0, 11.5)[v]
    for i in range(n):
        a = math.tau * i / n + k.rng.random() * 0.7
        rr = R * (0.35 + 0.55 * k.rng.random()) if i else 0.0
        h = 11.0 + 6.0 * k.rng.random()
        if v == 2 and i % 3 == 1:     # a conifer spire among the lobes
            k.cone(h * 0.2, h * 0.95, 3.2, sides=6, off=(math.cos(a) * rr, math.sin(a) * rr),
                   alt=1)
            continue
        w = 4.2 + 2.0 * k.rng.random()
        k.lobe((math.cos(a) * rr, math.sin(a) * rr, h * 0.62), (w, w, h * 0.42),
               jitter=0.22, alt=1 if i % 2 else -1, flat_bottom=0.4)


def _stump(k, v):
    k.prism((0, 0, -0.1), (0, 0, 0.45), 0.36, 0.32, sides=7, cap=False)
    b = len(k.v)
    k.prism((0, 0, 0.44), (0, 0, 0.45), 0.32, 0.32, sides=7, mat=1, cap=True)
    for a in (0.3, 2.4, 4.4):
        k.prism((0, 0, 0.12), (math.cos(a) * 0.65, math.sin(a) * 0.65, -0.05), 0.12, 0.06,
                sides=4, cap=False)
    return b


def _boulder(k, v):
    r = ((0.75, 0.62, 0.5), (0.55, 0.7, 0.42))[v]
    k.lobe((0, 0, r[2] * 0.6), r, jitter=0.14, alt=-1, flat_bottom=0.4)
    for i in range(len(k.alt) - 20, len(k.alt)):   # moss on the upward faces
        tri = k.f[i]
        zc = sum(k.v[j][2] for j in tri) / 3
        k.alt[i] = 1 if zc > r[2] * 1.05 else 0


def _tuft(k, v):
    n = (5, 7)[v]
    for i in range(n):
        a = math.tau * i / n + k.rng.random() * 0.6
        k.blade(a, 0.35 + 0.2 * k.rng.random(), 0.05, 0.12 + 0.1 * k.rng.random())
    for i in range(len(k.alt) - n, len(k.alt)):
        k.alt[i] = 1 if k.rng.random() < 0.4 else 0


BUILDERS = {"maple": _maple, "birch": _birch, "oak": _oak, "pine": _pine, "hemlock": _hemlock,
            "bare": _bare, "clump": _clump, "stump": _stump, "boulder": _boulder, "tuft": _tuft}


# ---------------------------------------------------------------------------
# Materials
# ---------------------------------------------------------------------------

def _instancer_material(name, attr, base, alt_attr=None):
    """Principled, rough, colour = the instance's `attr` (or `alt_attr` on alt faces)."""
    if name in bpy.data.materials:
        raise RuntimeError(f"material name '{name}' collides with an existing material")
    mat = scene.tag(bpy.data.materials.new(name))
    mat.diffuse_color = config.hex_rgba(config.colour(base))
    mat["hex"] = config.colour(base)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    a = nt.nodes.new("ShaderNodeAttribute")
    a.attribute_type = "INSTANCER"
    a.attribute_name = attr
    col = a.outputs["Color"]
    if alt_attr:
        b = nt.nodes.new("ShaderNodeAttribute")
        b.attribute_type = "INSTANCER"
        b.attribute_name = alt_attr
        f = nt.nodes.new("ShaderNodeAttribute")
        f.attribute_type = "GEOMETRY"
        f.attribute_name = "alt"
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        nt.links.new(f.outputs["Fac"], mix.inputs["Factor"])
        nt.links.new(col, mix.inputs[6])
        nt.links.new(b.outputs["Color"], mix.inputs[7])
        col = mix.outputs[2]
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(col, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 1.0
    spec = bsdf.inputs.get("Specular IOR Level")
    if spec is not None:
        spec.default_value = 0.1
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def build(root):
    """The kit collection (excluded from the view layer) with one object per variant.
    Returns the collection."""
    col = scene.collection(config.COL_FOREST_KIT, root)
    bark = _instancer_material("Forest_Bark", "bark", "wood-warm")
    crown = _instancer_material("Forest_Crown", "crown", "earth-light", alt_attr="crown2")
    for i, (name, (fam, v)) in enumerate(zip(forest.kit_names(), forest.KIT)):
        k = Kit(seed=1000 + i)
        BUILDERS[fam](k, v)
        me = k.mesh(name)
        me.materials.append(bark)
        me.materials.append(crown)
        scene.new_object(name, me, col, family=fam, variant=v, kit_index=i)
    lc = _layer_collection(bpy.context.view_layer.layer_collection, col.name)
    if lc is not None:
        lc.exclude = True
    return col


def _layer_collection(lc, name):
    if lc.name == name:
        return lc
    for ch in lc.children:
        found = _layer_collection(ch, name)
        if found is not None:
            return found
    return None

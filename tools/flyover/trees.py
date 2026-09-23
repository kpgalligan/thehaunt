"""The forest kit: one prototype mesh per variant in config.FOREST_KIT order
(FK_<nn>_<family>_<A..>), in the Flyover_ForestKit collection (excluded from the view
layer; scatter.py instances it), plus the two forest materials.

STYLIZED-REAL: real-world sizes, smooth-shaded, soft full foliage. A broadleaf crown is
a metaball cloud (clusters at the limb ends, a filler shell, small lumps on the
outside) tessellated once and jittered by 3D value noise, on a real branching trunk
(flared base, limbs into the clusters, twigs); white pine is a tall trunk with
irregular whorls of branches ending in flat, airy pads; hemlock a dense stack of
drooping, scalloped tiers with a nodding leader; the bare tree a recursive fork (the
game's bare tree); boulders are lumpy metaball stones, stumps flared cylinders with a
cut face; tufts are curved grass blades and a scatter of fallen leaves. LOD: every
tree family has a `_lo` twin (config.FOREST_KIT) built from the same recipe at a
coarser tessellation, and `clump` is a whole patch of far canopy as one soft cloud.
Origin at the trunk base, metres at scale 1, deterministic (seeded per variant).

Material slot 0 = Forest_Bark, slot 1 = Forest_Crown, slot 2 = Forest_Leaves (the leaf
cards: alpha-masked leaf clusters / needle sprays on the crown surface, shaded with the
crown's normal, `cn`, so a crown gets a fringed silhouette and depth without real
leaves). The cards are a second prototype per variant (FK_<nn>_..._cards in
Flyover_ForestCards, same order): scatter.py instances them from a cloud that casts no
shadow, since transparent shadows cost ~18x the render time. Colours are NOT in the kit: each
instance carries palette colours (`crown`, `crown2`, `bark`) and a `seed` (forest.py)
that the materials read as INSTANCER attributes. The kit's point attributes: `alt` (0..1,
crown -> crown2: a boulder's moss, a tuft's tips, a leaf's tint), `shade` (0 deep in /
under a crown .. 1 on its sunlit outside: cheap self-shadow and the greener inner
leaves), `mark` (1 on birch bark: the dark lenticels).
"""

import math
import random

import bmesh
import bpy
import numpy as np

import config
import forest
import scene

MB_STIFF = 2.0
MB_RADIUS = 1.87     # element radius per metre of surface radius (stiffness 2, threshold 0.6)


def _vnoise3(P, seed, scale):
    """Smooth 3D value noise in [-1, 1] at points P (n, 3)."""
    q = np.asarray(P, float) * scale
    i = np.floor(q).astype(np.int64)
    f = q - i
    u = f * f * (3 - 2 * f)

    def h(dx, dy, dz):
        a = ((i[:, 0] + dx) * 374761393 + (i[:, 1] + dy) * 668265263 + (i[:, 2] + dz) * 2147483647
             + seed * 1442695041) & 0xFFFFFFFF
        a = ((a ^ (a >> 13)) * 1274126177) & 0xFFFFFFFF
        return (a ^ (a >> 16)) / 4294967296.0

    out = 0.0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = ((u[:, 0] if dx else 1 - u[:, 0]) * (u[:, 1] if dy else 1 - u[:, 1])
                     * (u[:, 2] if dz else 1 - u[:, 2]))
                out = out + w * h(dx, dy, dz)
    return out * 2 - 1


class Kit:
    """Accumulates parts: verts, faces, per-face material, per-vertex alt / shade / mark."""

    def __init__(self, seed, lo=False):
        self.seed = seed
        self.rng = random.Random(seed)
        self.lo = lo
        self.cards = None       # (per m^2 of foliage, (min, max) size m, stretch): leaf cards
        self.v, self.f, self.mat = [], [], []
        self.alt, self.shade, self.mark = [], [], []

    def _vert(self, p, alt=0.0, shade=1.0, mark=0.0):
        self.v.append(tuple(p))
        self.alt.append(alt)
        self.shade.append(shade)
        self.mark.append(mark)
        return len(self.v) - 1

    def r(self, a, b):
        return a + (b - a) * self.rng.random()

    # -- wood --------------------------------------------------------------------
    def tube(self, path, radii, sides=8, mark=0.0, shade=(0.55, 1.0), cap=False, mat=0):
        """A generalised cylinder along `path` (list of 3D points) with a radius per
        point; parallel-transported frames, so it bends smoothly."""
        path = [np.asarray(p, float) for p in path]
        n = len(path)
        tans = []
        for k in range(n):
            a, b = path[max(0, k - 1)], path[min(n - 1, k + 1)]
            t = b - a
            tans.append(t / (np.linalg.norm(t) or 1.0))
        ref = np.array([1.0, 0.0, 0.0]) if abs(tans[0][2]) > 0.9 else np.array([0.0, 0.0, 1.0])
        u = np.cross(tans[0], ref)
        u /= np.linalg.norm(u)
        rings = []
        ph = self.rng.random() * math.tau
        for k in range(n):
            if k:
                u = u - tans[k] * np.dot(u, tans[k])
                u /= (np.linalg.norm(u) or 1.0)
            w = np.cross(tans[k], u)
            sh = shade[0] + (shade[1] - shade[0]) * k / max(1, n - 1)
            ring = []
            for s in range(sides):
                a = ph + math.tau * s / sides
                ring.append(self._vert(path[k] + radii[k] * (math.cos(a) * u + math.sin(a) * w),
                                       shade=sh, mark=mark))
            rings.append(ring)
        for k in range(n - 1):
            for s in range(sides):
                s1 = (s + 1) % sides
                self._face([rings[k][s], rings[k][s1], rings[k + 1][s1], rings[k + 1][s]], mat)
        if cap:
            self._face(rings[-1], mat)
        return self

    def _face(self, idx, mat):
        self.f.append(idx)
        self.mat.append(mat)

    def branch_path(self, p0, direction, length, rise=0.0, droop=0.0, segs=4):
        """Points from p0 along direction (unit-ish 3D), curving up by `rise` and
        sagging by `droop` (metres) over its length."""
        d = np.asarray(direction, float)
        d /= np.linalg.norm(d)
        pts = []
        for k in range(segs + 1):
            t = k / segs
            p = np.asarray(p0, float) + d * length * t
            p[2] += rise * t * t - droop * math.sin(math.pi * t) * 0.5
            pts.append(p)
        return pts

    # -- foliage -----------------------------------------------------------------
    def cloud(self, elems, res, jitter=0.18, jscale=0.9, centre=None, alt=0.0, shade_floor=0.3,
              shade_gain=1.0):
        """A metaball cloud: elems [(centre, surface radius, (sx, sy, sz) or None[, quat])],
        tessellated at `res` metres, jittered, with `shade` from exposure."""
        mb = bpy.data.metaballs.new(f"FKMB_{self.seed}")
        mb.resolution = mb.render_resolution = res
        mb.threshold = 0.6
        for el in elems:
            c, R, size = el[:3]
            e = mb.elements.new(type="ELLIPSOID" if size else "BALL")
            e.co = c
            e.radius = R * MB_RADIUS
            e.stiffness = MB_STIFF
            if size:
                e.size_x, e.size_y, e.size_z = size
            if len(el) > 3:
                e.rotation = el[3]
        ob = bpy.data.objects.new(f"FKMB_{self.seed}", mb)
        bpy.context.scene.collection.objects.link(ob)
        try:
            dg = bpy.context.evaluated_depsgraph_get()
            dg.update()
            ev = ob.evaluated_get(dg)
            me = ev.to_mesh()
            nv = len(me.vertices)
            co = np.zeros(nv * 3)
            me.vertices.foreach_get("co", co)
            co = co.reshape(-1, 3)
            polys = [tuple(p.vertices) for p in me.polygons]
            ev.to_mesh_clear()
        finally:
            bpy.data.objects.remove(ob)
            bpy.data.metaballs.remove(mb)
        if nv == 0:
            return self
        if jitter:
            n = np.stack([_vnoise3(co, self.seed + k * 17, jscale) for k in range(3)], 1)
            co = co + n * jitter
        cen = np.asarray(centre if centre is not None else co.mean(0))
        lo, hi = co[:, 2].min(), co[:, 2].max()
        rad = np.linalg.norm((co - cen)[:, :2], axis=1)
        rmax = rad.max() or 1.0
        hz = (co[:, 2] - lo) / max(hi - lo, 1e-6)
        sh = np.clip(shade_floor + (1 - shade_floor) * (0.55 * hz + 0.45 * rad / rmax), 0, 1) * shade_gain
        base = len(self.v)
        al = alt if np.ndim(alt) else np.full(nv, float(alt))
        for p, s, a in zip(co, sh, al):
            self._vert(p, alt=float(a), shade=float(s))
        for poly in polys:
            self._face([base + i for i in poly], 1)
        return self

    # -- mesh ----------------------------------------------------------------------
    def mesh(self, name):
        """(body mesh, card mesh or None). The cards are a separate prototype: they are
        instanced by a point cloud that casts no shadow (the crown body casts it)."""
        me = scene.tag(bpy.data.meshes.new(name))
        bm = bmesh.new()
        verts = [bm.verts.new(p) for p in self.v]
        bm.verts.index_update()
        for idx, mat in zip(self.f, self.mat):
            try:
                face = bm.faces.new([verts[i] for i in idx])
            except ValueError:     # a duplicate / degenerate face: skip
                continue
            face.material_index = mat
            face.smooth = True
        bmesh.ops.recalc_face_normals(bm, faces=[f for f in bm.faces if f.calc_area() > 1e-9])
        bm.normal_update()
        cards = self._cards(bm) if self.cards else None
        bm.to_mesh(me)
        bm.free()
        for key, vals in (("alt", self.alt), ("shade", self.shade), ("mark", self.mark)):
            a = me.attributes.new(key, "FLOAT", "POINT")
            a.data.foreach_set("value", np.asarray(vals, np.float32))
        cme = None
        if cards is not None:
            cv, cf, attrs = cards
            cme = scene.tag(bpy.data.meshes.new(name + "_cards"))
            cme.from_pydata(cv, [], cf)
            cme.polygons.foreach_set("material_index", np.full(len(cf), 2, np.int32))
            cme.shade_smooth()
            uv = cme.uv_layers.new(name="UVMap")
            uv.data.foreach_set("uv", np.tile(np.array([0, 0, 1, 0, 1, 1, 0, 1], np.float32), len(cf)))
            for key, vals in attrs.items():
                if key == "cn":
                    a = cme.attributes.new(key, "FLOAT_VECTOR", "POINT")
                    a.data.foreach_set("vector", np.asarray(vals, np.float32).ravel())
                else:
                    a = cme.attributes.new(key, "FLOAT", "POINT")
                    a.data.foreach_set("value", np.asarray(vals, np.float32))
        return me, cme

    def _cards(self, bm):
        """Leaf-cluster cards (alpha-masked) scattered over the foliage faces, facing out,
        pushed a little past the surface: a fringed silhouette and depth. Each card's
        `cn` is the crown's normal there, so it shades like the crown, not a flat card;
        `needle` switches the mask to needle sprays (conifers)."""
        density, (s0, s1), stretch, needle = self.cards
        rng = self.rng
        cv, cf = [], []
        attrs = {"cn": [], "shade": [], "alt": [], "needle": []}
        for face in [f for f in bm.faces if f.material_index == 1]:
            want = face.calc_area() * density
            n_cards = int(want) + (1 if rng.random() < want - int(want) else 0)
            for _ in range(n_cards):
                ws = [rng.random() for _ in face.verts]
                tot = sum(ws)
                p = sum(np.array(v.co) * (w / tot) for v, w in zip(face.verts, ws))
                nrm = np.array(face.normal)
                sh = sum(self.shade[v.index] for v in face.verts) / len(face.verts)
                d = nrm + np.array([rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6), rng.uniform(0.0, 0.5)])
                d = d / (np.linalg.norm(d) or 1.0)
                ref = np.array([0.0, 0.0, 1.0]) if abs(d[2]) < 0.9 else np.array([1.0, 0.0, 0.0])
                t1 = np.cross(d, ref)
                t1 /= np.linalg.norm(t1)
                t2 = np.cross(d, t1)
                if needle:      # needle sprays hang along the tier, not any which way
                    a = math.atan2(nrm[1], nrm[0]) + rng.uniform(-0.5, 0.5)
                    t1 = np.array([math.cos(a), math.sin(a), rng.uniform(-0.4, 0.1)])
                    t1 /= np.linalg.norm(t1)
                    t2 = np.cross(d, t1)
                    t2 /= (np.linalg.norm(t2) or 1.0)
                else:
                    a = rng.uniform(0, math.tau)
                    t1, t2 = t1 * math.cos(a) + t2 * math.sin(a), -t1 * math.sin(a) + t2 * math.cos(a)
                s = rng.uniform(s0, s1)
                c = p + nrm * s * rng.uniform(0.1, 0.4)
                base = len(cv)
                for q in (c + (-t1 * stretch - t2) * s / 2, c + (t1 * stretch - t2) * s / 2,
                          c + (t1 * stretch + t2) * s / 2, c + (-t1 * stretch + t2) * s / 2):
                    cv.append(tuple(float(x) for x in q))
                    attrs["cn"].append(tuple(float(x) for x in nrm))
                    attrs["shade"].append(min(1.0, sh * 1.05 + 0.1))
                    attrs["alt"].append(0.0)
                    attrs["needle"].append(1.0 if needle else 0.0)
                cf.append((base, base + 1, base + 2, base + 3))
        return (cv, cf, attrs) if cf else None


# ---------------------------------------------------------------------------
# Broadleaf crowns
# ---------------------------------------------------------------------------

def _fib(n):
    """n roughly even unit vectors (Fibonacci sphere)."""
    out = []
    g = math.pi * (3 - math.sqrt(5))
    for i in range(n):
        z = 1 - 2 * (i + 0.5) / n
        r = math.sqrt(max(0.0, 1 - z * z))
        out.append((math.cos(g * i) * r, math.sin(g * i) * r, z))
    return out


def _broadleaf(k, h, rw, base, flat=0.0, limbs=5, trunk_r=0.35, lumps=True, airy=0.0,
               lean=(0.0, 0.0), stems=((0.0, 0.0),)):
    """A broadleaf tree: crown ellipsoid from base*h to h, radius rw; `flat` squashes
    the top (oak), `airy` opens gaps (birch)."""
    rh = h * (1 - base) / 2
    zc = h - rh
    re = rh * 1.12 - rw * 0.2          # the element shell's vertical reach
    sides = 5 if k.lo else 9
    elems = []
    for sx, sy in stems:
        top = np.array([sx + lean[0], sy + lean[1], zc - rh * 0.35])
        fork = np.array([sx + lean[0] * 0.5, sy + lean[1] * 0.5, base * h + 0.6])
        path = [np.array([sx, sy, 0.0]), np.array([sx, sy, 0.7]), fork * 0.5 + np.array([sx, sy, 0]) * 0.5,
                fork, top]
        k.tube(path, [trunk_r * 1.55, trunk_r, trunk_r * 0.88, trunk_r * 0.75, trunk_r * 0.45],
               sides=sides, mark=1.0 if airy else 0.0, shade=(0.35, 0.9))
        # limbs out to the clusters
        for i in range(limbs):
            a = math.tau * i / limbs + k.r(-0.4, 0.4)
            el = k.r(-0.1, 0.7)
            end = np.array([sx + math.cos(a) * rw * k.r(0.5, 0.75), sy + math.sin(a) * rw * k.r(0.5, 0.75),
                            zc + rh * (el - 0.35) * (1 - 0.5 * flat)])
            start = fork + np.array([0, 0, k.r(0.0, h * 0.12)])
            mid = (start + end) / 2 + np.array([0, 0, rh * 0.15])
            k.tube([start, mid, end], [trunk_r * 0.5, trunk_r * 0.32, trunk_r * 0.12],
                   sides=3 if k.lo else 6, mark=1.0 if airy else 0.0, shade=(0.3, 0.5))
            if not k.lo:     # twigs into the underside, seen from below
                for _ in range(2):
                    t = k.r(0.4, 0.8)
                    p = mid * t + end * (1 - t)
                    d = np.array([math.cos(a + k.r(-1, 1)), math.sin(a + k.r(-1, 1)), k.r(-0.3, 0.4)])
                    k.tube([p, p + d * rw * 0.3], [trunk_r * 0.12, 0.02], sides=3, shade=(0.3, 0.4))
            R = rw * k.r(0.36, 0.46) * (1 - 0.3 * airy)
            elems.append((tuple(end + np.array([0, 0, R * 0.3])), R, None))
            for _ in range(2 if airy else 3):
                off = np.array([k.r(-1, 1), k.r(-1, 1), k.r(-0.3, 0.8)])
                off /= np.linalg.norm(off)
                elems.append((tuple(end + off * R * 0.8), R * k.r(0.55, 0.75), None))
    # the filler shell: a full dome (fewer, looser for an airy crown), and clusters inside
    n_shell = int((12 if k.lo else 30) * (1 - 0.5 * airy))
    for d in _fib(n_shell):
        if d[2] < -0.72 or k.rng.random() < airy * 0.5:
            continue
        zz = d[2] * (1 - flat * max(0.0, d[2]))
        f = k.r(0.72, 0.84)
        p = (d[0] * rw * f + lean[0], d[1] * rw * f + lean[1], zc + zz * re * f)
        elems.append((p, rw * k.r(0.28, 0.38) * (1 - 0.35 * airy), None))
    for _ in range(0 if airy else 6):
        d = np.array([k.r(-1, 1), k.r(-1, 1), k.r(-0.3, 0.9)])
        d *= k.r(0.2, 0.55) / np.linalg.norm(d)
        elems.append(((d[0] * rw + lean[0], d[1] * rw + lean[1], zc + d[2] * rh), rw * k.r(0.3, 0.42), None))
    # small lumps on the outside: a leafy, broken silhouette
    if lumps and not k.lo:
        for d in _fib(40):
            if d[2] < -0.6 or k.rng.random() < 0.35 + 0.3 * airy:
                continue
            zz = d[2] * (1 - flat * max(0.0, d[2]))
            f = k.r(0.95, 1.08)
            p = (d[0] * rw * f + lean[0], d[1] * rw * f + lean[1], zc + zz * re * f)
            elems.append((p, rw * k.r(0.13, 0.2), None))
    res = max(0.36, rw * 0.095) if not k.lo else max(0.9, rw * 0.2)
    k.cards = (1.8, (1.1, 1.9), 1.0, False) if not k.lo else (0.3, (1.8, 2.8), 1.0, False)
    # the cloud is the crown's shadowed body; the leaf cards on it carry the light
    k.cloud(elems, res, jitter=0.0 if k.lo else rw * 0.035, jscale=1.1, centre=(lean[0], lean[1], zc),
            shade_gain=0.85)


def _maple(k, v):
    """Sugar maple: a full, rounded ovoid crown on a short straight trunk."""
    h, rw = ((19.0, 5.6), (20.0, 5.0), (18.0, 6.3))[v]
    _broadleaf(k, h, rw, base=0.3, limbs=(5, 5, 6)[v], trunk_r=0.36,
               lean=((0.3, 0.1), (-0.2, 0.3), (0.1, -0.35))[v])


def _oak(k, v):
    """Red / white oak: a thick trunk, spreading limbs, a broad, flatter-topped crown."""
    h, rw = ((19.0, 7.2), (17.5, 6.4))[v]
    _broadleaf(k, h, rw, base=0.34, flat=0.35, limbs=6, trunk_r=0.48,
               lean=((0.2, -0.2), (-0.3, 0.1))[v])


def _birch(k, v):
    """Paper birch: slender pale stem(s), an airy crown of separate clusters."""
    h, rw = ((14.5, 3.3), (13.5, 3.8))[v]
    stems = ((0.0, 0.0),) if v == 0 else ((0.45, 0.2), (-0.4, -0.15))
    _broadleaf(k, h, rw, base=0.4, limbs=4, trunk_r=0.17, airy=0.4, stems=stems,
               lean=((0.4, 0.0), (0.2, 0.2))[v])


# ---------------------------------------------------------------------------
# Conifers
# ---------------------------------------------------------------------------

def _quat_tilt(k, amt):
    """A small random tilt (quaternion w, x, y, z)."""
    ax = np.array([k.r(-1, 1), k.r(-1, 1), 0.0])
    ax /= (np.linalg.norm(ax) or 1.0)
    a = k.r(-amt, amt) / 2
    return (math.cos(a), ax[0] * math.sin(a), ax[1] * math.sin(a), 0.0)


def _pine(k, v):
    """Eastern white pine: tall, open and irregular: whorls of near-horizontal branches,
    some missing, ending in soft, flat, tilted tufts of needles; a ragged top."""
    h = (24.5, 22.5)[v]
    sides = 5 if k.lo else 9
    k.tube([(0, 0, 0), (0, 0, 0.8), (0.15, 0.05, h * 0.5), (0.25, -0.1, h * 0.97)],
           [0.55, 0.38, 0.28, 0.06], sides=sides, shade=(0.35, 0.9))
    elems = []
    z0 = h * 0.36
    z = z0
    while z < h * 0.9:
        t = (z - z0) / (h - z0)
        maxl = 6.2 * (1 - t) ** 0.75 + 0.9
        n = k.rng.choice((3, 4, 4, 5))
        a0 = k.rng.random() * math.tau
        for i in range(n):
            if k.rng.random() < 0.3:            # a missing branch: the pine's gaps
                continue
            a = a0 + math.tau * i / n + k.r(-0.35, 0.35)
            L = maxl * k.r(0.55, 1.1)
            d = (math.cos(a), math.sin(a), k.r(-0.05, 0.2))
            pts = k.branch_path((0.2 * t, 0, z), d, L, rise=k.r(0.3, 1.1), droop=k.r(0.2, 0.7))
            if not k.lo:
                k.tube(pts, [0.16 * (1 - t) + 0.05, 0.08, 0.05, 0.035, 0.02], sides=4, shade=(0.3, 0.5))
            nt = 1 if L < 2.5 else k.rng.choice((2, 2, 3))
            for j in range(nt):
                frac = 1.0 - j * k.r(0.25, 0.35)
                p = pts[int(round(frac * (len(pts) - 1)))]
                s = k.r(0.75, 1.15) * (0.6 + 0.4 * min(1.0, L / 5))
                off = np.array([k.r(-0.3, 0.3), k.r(-0.3, 0.3), k.r(0.05, 0.35)])
                elems.append((tuple(p + off), s, (k.r(1.1, 1.8), k.r(0.8, 1.3), k.r(0.42, 0.62)),
                              _quat_tilt(k, 0.5)))
        z += k.r(1.4, 2.8)
    for i in range(4):     # the ragged top
        elems.append(((0.25 + k.r(-0.7, 0.7), -0.1 + k.r(-0.7, 0.7), h * k.r(0.88, 0.99)),
                      k.r(0.45, 0.7), (1.3, 1.0, 0.7), _quat_tilt(k, 0.8)))
    k.cards = (1.4, (0.8, 1.3), 1.5, True) if not k.lo else (0.3, (1.3, 2.0), 1.4, True)
    k.cloud(elems, 0.9 if k.lo else 0.44, jitter=0.0 if k.lo else 0.12, jscale=1.6,
            centre=(0, 0, h * 0.6), shade_floor=0.35, shade_gain=0.8)


def _skirt(k, z, r, th, droop, segs):
    """One hemlock tier: a drooping, scalloped lampshade with a closed underside that
    tucks back to the trunk."""
    ph = k.rng.random() * math.tau
    lobes = k.rng.choice((3, 4, 5))
    wav = [1 + 0.14 * math.sin(lobes * (ph + math.tau * s / segs)) + k.r(-0.12, 0.12)
           for s in range(segs)]
    off = (k.r(-0.25, 0.25), k.r(-0.25, 0.25))
    tilt = (k.r(-0.08, 0.08), k.r(-0.08, 0.08))
    prof = ((0.18, th * 0.95, 0.25), (0.5, th * 0.45, 0.6), (0.86, 0.0, 0.95), (1.0, -droop, 1.0),
            (0.9, -droop - 0.22, 0.55), (0.45, -th * 0.2, 0.2), (0.12, th * 0.1, 0.1))
    rings = []
    for fr, dz, sh in prof:
        ring = []
        for s in range(segs):
            a = ph + math.tau * s / segs
            rr = r * fr * (wav[s] if fr > 0.6 else 1.0)
            dd = dz * (wav[s] if dz < 0 else 1.0)
            px, py = rr * math.cos(a), rr * math.sin(a)
            o = fr * fr
            ring.append(k._vert((px + off[0] * o, py + off[1] * o, z + dd + px * tilt[0] + py * tilt[1]),
                                shade=sh))
        rings.append(ring)
    for a in range(len(rings) - 1):
        for s in range(segs):
            s1 = (s + 1) % segs
            k._face([rings[a][s], rings[a + 1][s], rings[a + 1][s1], rings[a][s1]], 1)


def _hemlock(k, v):
    """Eastern hemlock: dense and dark, drooping tiers to near the ground, a nodding
    leader."""
    h, R = ((21.0, 4.4), (19.0, 3.8))[v]
    k.tube([(0, 0, 0), (0, 0, 0.6), (0, 0, h * 0.9)], [0.5, 0.38, 0.1],
           sides=5 if k.lo else 8, shade=(0.3, 0.6))
    n = 8 if k.lo else 13
    segs = 9 if k.lo else 18
    k.cards = (1.3, (0.9, 1.4), 1.6, True) if not k.lo else (0.25, (1.4, 2.2), 1.5, True)
    z0 = h * 0.1
    for i in range(n):
        t = i / (n - 1)
        z = z0 + (h * 0.9 - z0) * t ** 0.92 + k.r(-0.3, 0.3)
        r = R * (1 - t) ** 0.85 * k.r(0.8, 1.12) + 0.35
        th = (h - z0) / n * 1.9
        _skirt(k, z, r, th, droop=0.25 + 0.65 * (1 - t) * k.r(0.8, 1.2), segs=segs)
    # the nodding leader
    top = [(0, 0, h * 0.88), (0.05, 0.0, h * 0.95), (0.35, 0.1, h), (0.6, 0.15, h * 0.985)]
    k.tube(top, [0.12, 0.08, 0.05, 0.02], sides=4, mat=1, shade=(0.8, 1.0))


# ---------------------------------------------------------------------------
# Bare, far clumps, ground dressing
# ---------------------------------------------------------------------------

def _limb(k, p, d, L, r, depth):
    d = np.asarray(d, float)
    d /= np.linalg.norm(d)
    pts = k.branch_path(p, d, L, rise=L * 0.12, segs=3)
    k.tube(pts, [r, r * 0.85, r * 0.7, r * 0.55], sides=max(3, 3 + depth), shade=(0.5, 0.8))
    if depth == 0:
        return
    end = pts[-1]
    for i in range(k.rng.choice((2, 2, 3))):
        a = k.r(0.35, 0.7) * (1 if i % 2 else -1)
        # rotate d about a random horizontal-ish axis
        axis = np.cross(d, (0, 0, 1)) if abs(d[2]) < 0.99 else np.array([1.0, 0, 0])
        axis = axis / np.linalg.norm(axis)
        spin = k.r(0, math.tau)
        axis = axis * math.cos(spin) + np.cross(d, axis) * math.sin(spin)
        nd = d * math.cos(a) + np.cross(axis, d) * math.sin(a)
        nd[2] += 0.25
        _limb(k, end, nd, L * k.r(0.55, 0.72), r * 0.55, depth - 1)


def _bare(k, v):
    """The game's bare tree: a trunk forking into upswept limbs; B is a broken snag."""
    if v == 0:
        k.tube([(0, 0, 0), (0, 0, 0.6), (0.1, 0.05, 5.0)], [0.55, 0.36, 0.3], sides=8, shade=(0.4, 0.7))
        for i in range(3):
            a = math.tau * i / 3 + k.r(-0.3, 0.3)
            _limb(k, (0.1, 0.05, 4.8), (math.cos(a) * 0.55, math.sin(a) * 0.55, 1.0), 4.2, 0.2, 2)
    else:
        k.tube([(0, 0, 0), (0, 0, 0.6), (0.3, 0.1, 5.0), (0.35, 0.1, 8.5)], [0.6, 0.42, 0.32, 0.24],
               sides=8, cap=True, shade=(0.4, 0.7))
        for z, a in ((4.2, 0.5), (6.3, 2.9), (7.4, 4.6)):
            _limb(k, (0.2, 0.08, z), (math.cos(a), math.sin(a), 0.5), k.r(1.3, 2.4), 0.12, 0)


def _clump(k, v):
    """A far canopy clump: a patch of forest as one soft cloud of crowns (C adds conifer
    spires)."""
    R = (11.0, 10.0, 12.0)[v]
    n = (8, 7, 9)[v]
    elems = []
    for i in range(n):
        a = math.tau * i / n + k.r(-0.4, 0.4)
        rr = R * k.r(0.25, 0.8) if i else 0.0
        top = k.r(13.0, 19.0)
        x, y = math.cos(a) * rr, math.sin(a) * rr
        if v == 2 and i % 3 == 1:        # a conifer spire
            elems.append(((x, y, top * 0.55), 2.6, (1.0, 1.0, 2.6)))
            continue
        w = k.r(3.6, 5.2)
        elems.append(((x, y, top - w * 0.7), w, (1.0, 1.0, 0.8)))
        elems.append(((x, y, top - w * 1.5), w * 0.9, None))
    k.cloud(elems, 1.3, jitter=0.4, jscale=0.35, centre=(0, 0, 10.0), shade_floor=0.35)


def _stump(k, v):
    k.tube([(0, 0, -0.1), (0, 0, 0.12), (0, 0, 0.45)], [0.62, 0.42, 0.38], sides=10, shade=(0.4, 0.7))
    ring = [k._vert((0.38 * math.cos(math.tau * s / 10), 0.38 * math.sin(math.tau * s / 10), 0.45))
            for s in range(10)]
    k._face(ring, 1)
    for a in (0.3, 2.4, 4.4):
        k.tube([(0, 0, 0.15), (math.cos(a) * 0.5, math.sin(a) * 0.5, 0.05),
                (math.cos(a) * 0.95, math.sin(a) * 0.95, -0.08)], [0.16, 0.1, 0.05], sides=5,
               shade=(0.4, 0.5))


def _boulder(k, v):
    """A lumpy granite boulder; moss (`alt`) on its upward faces."""
    r = ((0.8, 0.65, 0.55), (0.6, 0.75, 0.45))[v]
    elems = [((0, 0, r[2] * 0.45), 1.0, r)]
    for _ in range(3):
        elems.append(((k.r(-0.3, 0.3), k.r(-0.3, 0.3), r[2] * k.r(0.3, 0.7)), k.r(0.35, 0.5),
                      (r[0] * 0.9, r[1] * 0.9, r[2] * 0.8)))
    before = len(k.v)
    k.cloud(elems, 0.09, jitter=0.07, jscale=2.2, shade_floor=0.5)
    zs = [p[2] for p in k.v[before:]]
    top = max(zs) if zs else 1.0
    for i in range(before, len(k.v)):
        k.alt[i] = float(np.clip((k.v[i][2] / top - 0.62) / 0.25, 0, 1))


def _tuft(k, v):
    """A clump of autumn grass: curved, tapering blades; some tips take crown2."""
    n = (22, 32)[v]
    for i in range(n):
        a = k.r(0, math.tau)
        hgt = k.r(0.25, 0.55) * (1.0 if v else 0.8)
        lean = k.r(0.08, 0.28)
        w = k.r(0.012, 0.02)
        base = np.array([math.cos(a) * k.r(0, 0.08), math.sin(a) * k.r(0, 0.08), -0.02])
        side = np.array([-math.sin(a + 1.2), math.cos(a + 1.2), 0.0])
        tip_alt = 1.0 if k.rng.random() < 0.4 else 0.0
        prev = None
        for s in range(4):
            t = s / 3
            c = base + np.array([math.cos(a) * lean * t * t, math.sin(a) * lean * t * t, hgt * t])
            ww = w * (1 - 0.85 * t)
            pair = (k._vert(c - side * ww, alt=tip_alt * t, shade=0.35 + 0.65 * t),
                    k._vert(c + side * ww, alt=tip_alt * t, shade=0.35 + 0.65 * t))
            if prev:
                k._face([prev[0], prev[1], pair[1], pair[0]], 1)
            prev = pair


def _leaves(k, v):
    """Fallen leaves lying on the grass (a treeline's spill): small curled ovals, their
    tint between crown and crown2 (`alt`)."""
    for i in range(14):
        cx, cy = k.r(-0.6, 0.6), k.r(-0.6, 0.6)
        a = k.r(0, math.tau)
        s = k.r(0.035, 0.055)
        al = k.rng.random()
        ca, sa = math.cos(a), math.sin(a)
        curl = k.r(0.1, 0.5)
        ring = []
        for j in range(8):
            t = math.tau * j / 8
            px, py = math.cos(t) * (1.0 + 0.25 * math.cos(t)), math.sin(t) * 0.6
            pz = curl * px * px * 0.5 + 0.1
            ring.append(k._vert((cx + (px * ca - py * sa) * s, cy + (px * sa + py * ca) * s, 0.01 + pz * s),
                                alt=al, shade=0.85))
        k._face(ring, 1)


BUILDERS = {"maple": _maple, "birch": _birch, "oak": _oak, "pine": _pine, "hemlock": _hemlock,
            "bare": _bare, "clump": _clump, "stump": _stump, "boulder": _boulder, "tuft": _tuft,
            "leaves": _leaves}


# ---------------------------------------------------------------------------
# Materials
# ---------------------------------------------------------------------------

def _attr(nt, name, kind="INSTANCER"):
    a = nt.nodes.new("ShaderNodeAttribute")
    a.attribute_type = kind
    a.attribute_name = name
    return a


def _instance_coords(g):
    """Object-space coordinates offset by the instance's seed (no two trees alike)."""
    tc = g.node("ShaderNodeTexCoord")
    seed = _attr(g.nt, "seed").outputs["Fac"]
    off = g.combine(g.math("MULTIPLY", seed, 173.0), g.math("MULTIPLY", seed, 91.0), 0.0)
    n = g.node("ShaderNodeVectorMath", operation="ADD")
    g.link(tc.outputs["Object"], n.inputs[0])
    g.link(off, n.inputs[1])
    return n.outputs["Vector"]


def _foliage_material(name, cards=False):
    """Forest_Crown (the crown surfaces, cones, blades, stones) and Forest_Leaves (the
    alpha-masked leaf-cluster cards): the same colour, from the instance's palette pair."""
    import materials
    mat = _new_mat(name, "earth-light")
    g = materials.G(mat)
    p = _instance_coords(g)
    crown = _attr(g.nt, "crown").outputs["Color"]
    crown2 = _attr(g.nt, "crown2").outputs["Color"]
    alt = _attr(g.nt, "alt", "GEOMETRY").outputs["Fac"]
    shade = _attr(g.nt, "shade", "GEOMETRY").outputs["Fac"]
    # patches of the second tone within the crown, plus the kit's alt
    patch = g.step(g.noise(p, 0.28, 2.0, 0.55), 0.52, 0.12)
    f = g.math("MAXIMUM", g.math("MULTIPLY", patch, 0.65), alt)
    c = g.mix(f, crown, crown2)
    # inner and lower leaves: toward the second tone, darker (cheap self-shadow)
    inner = g.math("SUBTRACT", 1.0, shade)
    c = g.mix(g.math("MULTIPLY", inner, 0.35), c, crown2)
    clump = g.noise(p, 1.3, 4.0, 0.62)
    cavity = g.math("SUBTRACT", 1.0, g.step(clump, 0.46, 0.14))       # gaps between clumps
    dk = g.math("ADD", g.math("MULTIPLY", inner, 0.45), g.math("MULTIPLY", cavity, 0.0 if cards else 0.25))
    dark = g.node("ShaderNodeMix", data_type="RGBA", blend_type="MULTIPLY", clamp_factor=True)
    g._in(dark, "Factor_Float", g.math("MINIMUM", dk, 0.6))
    g.link(c, g.sock(dark.inputs, "A_Color"))
    deep = g.node("ShaderNodeMix", data_type="RGBA", blend_type="MULTIPLY", clamp_factor=True)
    g._in(deep, "Factor_Float", 1.0)                     # a hue-keeping shadow: crown2 x crown2
    g.link(crown2, g.sock(deep.inputs, "A_Color"))
    g.link(crown2, g.sock(deep.inputs, "B_Color"))
    g.link(g.sock(deep.outputs, "Result_Color"), g.sock(dark.inputs, "B_Color"))
    c = g.sock(dark.outputs, "Result_Color")
    b = g.node("ShaderNodeBsdfPrincipled")
    b.inputs["Roughness"].default_value = 0.75
    for key, val in (("Specular IOR Level", 0.08), ("Sheen Weight", 0.0)):
        if b.inputs.get(key) is not None:
            b.inputs[key].default_value = val
    if cards:
        # a cluster of leaves on the card: voronoi cells inside a ragged disc
        uv = g.uv("UVMap")
        cells = g.voronoi(uv, 2.3, "F1", rand=1.0)
        leaf = g.math("SUBTRACT", 1.0, g.step(cells.outputs["Distance"], 0.46, 0.04))
        # conifers: needle sprays (stretched cells: long thin strands)
        su = g.node("ShaderNodeVectorMath", operation="MULTIPLY")
        g.link(uv, su.inputs[0])
        su.inputs[1].default_value = (1.2, 9.0, 1.0)
        strands = g.voronoi(su.outputs["Vector"], 1.0, "F1", rand=0.8)
        nd = g.math("SUBTRACT", 1.0, g.step(strands.outputs["Distance"], 0.36, 0.05))
        leaf = g.fmix(_attr(g.nt, "needle", "GEOMETRY").outputs["Fac"], leaf, nd)
        u, v, _ = g.sep(uv)
        r = g.math("SQRT", g.math("ADD", g.math("POWER", g.math("SUBTRACT", u, 0.5), 2.0),
                                  g.math("POWER", g.math("SUBTRACT", v, 0.5), 2.0)))
        disc = g.math("SUBTRACT", 1.0, g.step(r, 0.47, 0.03))
        g.link(g.math("MULTIPLY", leaf, disc), b.inputs["Alpha"])
        tint = g.white(cells.outputs["Position"])                       # leaf-to-leaf value
        c = g.mix(g.math("MULTIPLY", g.step(tint, 0.5, 0.2), 0.3), c, crown2)
        # shade like the crown underneath, not like a flat card
        cn = _attr(g.nt, "cn", "GEOMETRY").outputs["Vector"]
        vt = g.node("ShaderNodeVectorTransform", vector_type="NORMAL", convert_from="OBJECT",
                    convert_to="WORLD")
        g.link(cn, vt.inputs["Vector"])
        g.link(vt.outputs["Vector"], b.inputs["Normal"])
        mat.surface_render_method = "DITHERED"
        if hasattr(mat, "use_transparent_shadow"):
            mat.use_transparent_shadow = False     # the cards cast no shadow at all (scatter.py)
    else:
        bump = g.node("ShaderNodeBump")
        bump.inputs["Strength"].default_value = 0.5
        bump.inputs["Distance"].default_value = 0.15
        g.link(clump, bump.inputs["Height"])
        g.link(bump.outputs["Normal"], b.inputs["Normal"])
    g.link(c, b.inputs["Base Color"])
    tr = g.node("ShaderNodeBsdfTranslucent")
    g.link(c, tr.inputs["Color"])
    mix = g.node("ShaderNodeMixShader")
    mix.inputs["Fac"].default_value = 0.15 if cards else 0.08
    g.link(b.outputs["BSDF"], mix.inputs[1])
    g.link(tr.outputs["BSDF"], mix.inputs[2])
    if cards:   # the translucent lobe must respect the cutout too
        tp = g.node("ShaderNodeBsdfTransparent")
        m2 = g.node("ShaderNodeMixShader")
        g.link(g.math("MULTIPLY", leaf, disc), m2.inputs["Fac"])
        g.link(tp.outputs["BSDF"], m2.inputs[1])
        g.link(mix.outputs["Shader"], m2.inputs[2])
        mix = m2
    o = g.node("ShaderNodeOutputMaterial")
    g.link(mix.outputs["Shader"], o.inputs["Surface"])
    return mat


def _bark_material():
    import materials
    mat = _new_mat("Forest_Bark", "wood-warm")
    g = materials.G(mat)
    p = _instance_coords(g)
    bark = _attr(g.nt, "bark").outputs["Color"]
    shade = _attr(g.nt, "shade", "GEOMETRY").outputs["Fac"]
    mark = _attr(g.nt, "mark", "GEOMETRY").outputs["Fac"]
    # vertical furrows (stretched noise)
    sc = g.node("ShaderNodeVectorMath", operation="MULTIPLY")
    g.link(p, sc.inputs[0])
    sc.inputs[1].default_value = (7.0, 7.0, 0.9)
    furrow = g.noise(sc.outputs["Vector"], 1.0, 3.0, 0.6)
    ink = materials.rgba("ink-700")
    c = g.mix(g.math("MULTIPLY", g.math("SUBTRACT", 1.0, g.step(furrow, 0.45, 0.12)), 0.45), bark, ink)
    # birch: dark horizontal lenticels and patches
    sl = g.node("ShaderNodeVectorMath", operation="MULTIPLY")
    g.link(p, sl.inputs[0])
    sl.inputs[1].default_value = (2.5, 2.5, 14.0)
    lent = g.step(g.noise(sl.outputs["Vector"], 1.0, 2.0, 0.5), 0.64, 0.03)
    c = g.mix(g.math("MULTIPLY", lent, g.math("MULTIPLY", mark, 0.85)), c, ink)
    # the base is darker (damp, shaded)
    c = g.mix(g.math("MULTIPLY", g.math("SUBTRACT", 1.0, shade), 0.5), c, materials.rgba("ink-500"))
    b = g.node("ShaderNodeBsdfPrincipled")
    g.link(c, b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.9
    spec = b.inputs.get("Specular IOR Level")
    if spec is not None:
        spec.default_value = 0.15
    bump = g.node("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.8
    bump.inputs["Distance"].default_value = 0.03
    g.link(furrow, bump.inputs["Height"])
    g.link(bump.outputs["Normal"], b.inputs["Normal"])
    o = g.node("ShaderNodeOutputMaterial")
    g.link(b.outputs["BSDF"], o.inputs["Surface"])
    return mat


def _new_mat(name, base):
    if name in bpy.data.materials:
        raise RuntimeError(f"material name '{name}' collides with an existing material")
    mat = scene.tag(bpy.data.materials.new(name))
    mat.diffuse_color = config.hex_rgba(config.colour(base))
    mat["hex"] = config.colour(base)
    return mat


def build(root):
    """The kit collection (excluded from the view layer) with one object per variant.
    Returns the collection."""
    col = scene.collection(config.COL_FOREST_KIT, root)
    ccol = scene.collection(config.COL_FOREST_CARDS, root)
    bark = _bark_material()
    crown = _foliage_material("Forest_Crown")
    leaves = _foliage_material("Forest_Leaves", cards=True)
    tris = {}
    for i, (name, (fam, v)) in enumerate(zip(forest.kit_names(), forest.KIT)):
        base, lo = (fam[:-3], True) if fam.endswith("_lo") else (fam, False)
        k = Kit(seed=1000 + i, lo=lo)
        BUILDERS[base](k, v)
        me, cme = k.mesh(name)
        if cme is None:     # a placeholder, so the card kit's indices match the body kit's
            cme = scene.tag(bpy.data.meshes.new(name + "_cards"))
        for m in (me, cme):
            for mat in (bark, crown, leaves):
                m.materials.append(mat)
        ob = scene.new_object(name, me, col, family=fam, variant=v, kit_index=i)
        scene.new_object(name + "_cards", cme, ccol, family=fam, variant=v, kit_index=i)
        tris[name] = (sum(len(p.vertices) - 2 for p in me.polygons), 2 * len(cme.polygons))
        ob["tris"] = tris[name][0]
        ob["card_tris"] = tris[name][1]
    col["tris"] = str(tris)
    for c in (col, ccol):
        lc = _layer_collection(bpy.context.view_layer.layer_collection, c.name)
        if lc is not None:
            lc.exclude = True
    return col, ccol


def _layer_collection(lc, name):
    if lc.name == name:
        return lc
    for ch in lc.children:
        found = _layer_collection(ch, name)
        if found is not None:
            return found
    return None

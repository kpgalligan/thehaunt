"""Phase 10: life -- the few things that move besides the camera. Everything is a pure
function of the scene frame (t = (frame - 1) / fps), so any frame renders the same in
isolation and nothing is simulated or baked.

- The V (MotelSign.cs, BINDING): the motel's V is the ONE animated sign in the game. Its
  object `glow` (the neon family's per-object level) is keyed CONSTANT: off for
  MOTEL_V_OFF_S (0.55 s) at the start of every MOTEL_V_CYCLE_S (4.0 s) cycle, a hard cut,
  never randomised; the phase puts one off-blink in frame with the sign close
  (MOTEL_V_FIRST_OFF_S). Edges sit on quarter frames (between the 180-degree shutters),
  so every rendered frame is fully on or fully off. The panel's two spill lights drop to
  MOTEL_V_SPILL_OFF with it (MotelSign's PanelGlow: "the V's share of the glow cuts with
  the V"). The V is the only animated SIGN; nothing in the film flickers.
- The pit breathes (Kevin, 2026-09-24; film-only, not a sign, not a flicker): the void's
  object `glow` keyed every frame on a slow smooth breath (config.PIT_BREATH_*: 5 s,
  inhale 2 s / exhale 3 s, cosine-eased, 0.77-1.10 of the reviewed level, min / max 0.70),
  and the two pit lights driven by it, so emission and light rise and fall together.
- Wind: trees near the flight (within WIND_NEAR_M of the lens at some frame) move from
  the two static forest objects (`sway` point attribute: dropped there) to two wind
  twins (Forest_Wind / Forest_WindCards on Forest_WindPoints, pre-grounded exactly as
  the scatter grounds them, so nothing is ray-cast per frame), whose Geometry Nodes
  tilt each instance about its base: a gust field travelling downwind + a slow
  per-tree sway. EEVEE shader
  displacement was the first choice (per-vertex, whole forest) but does nothing in this
  Blender (5.2: no vertex moves, in EEVEE or Cycles), so the tilt is rigid per tree and
  the far forest stands still (sub-pixel there anyway).
- Falling leaves (Life_Leaves): palette leaves dropping from the broadleaf crowns the
  camera passes close to (LEAF_WINDOWS), each on a looping parametric path (fall, drift
  downwind, flutter, tumble; faded in and out at the loop's ends). Leaves that would
  come within LEAF_CLEAR_M of the lens, or fall into a building, are dropped
  (checked on every frame in numpy with the same formula the nodes use).
- Chimney smoke (Life_Smoke_<id>): only from a lit hearth canon puts in a building at
  18:00 (SMOKE_DESIGNS; the building's `mount_flue`): soft alpha puffs rising and
  drifting with the same wind, growing and thinning; kept SMOKE_CLEAR_M off the lens.
"""

import math

import numpy as np

import config

TAU = 2.0 * math.pi


# ---------------------------------------------------------------------------
# The V
# ---------------------------------------------------------------------------

def v_first_off_frame(fps):
    """The first in-frame off edge: the nearest frame to MOTEL_V_FIRST_OFF_S, + 1/4."""
    return round(1 + config.MOTEL_V_FIRST_OFF_S * fps) + 0.25


def v_keys(n_frames, fps):
    """CONSTANT keys (frames, values) for the V's glow over 1..n_frames."""
    cyc, off = config.MOTEL_V_CYCLE_S * fps, config.MOTEL_V_OFF_S * fps
    s0 = v_first_off_frame(fps)
    s0 -= math.ceil((s0 - 1) / cyc) * cyc          # the earliest cycle reaching frame 1
    frames, vals = [], []
    s = s0
    while s <= n_frames:
        for f, v in ((s, 0.0), (s + off, 1.0)):
            if f > 1:
                frames.append(f)
                vals.append(v)
        s += cyc
    first = 0.0 if s0 <= 1 < s0 + off else 1.0
    return [1.0] + frames, [first] + vals


def v_on(frame, fps):
    """The game's truth table (MotelSign.Resolve) at a frame: True = the V lit."""
    t = (frame - v_first_off_frame(fps)) / fps
    return (t % config.MOTEL_V_CYCLE_S) >= config.MOTEL_V_OFF_S


def blink(n_frames, fps):
    """Key the V's glow; tie the panel's spill lights to it. Returns (V object, keys)."""
    import bpy

    import flight
    vs = [ob for ob in bpy.data.objects if ob.get("circuit") == "C" and ob.get(config.TAG_PROP)]
    if len(vs) != 1:
        raise AssertionError(f"expected the motel's one V (circuit C), found {[o.name for o in vs]}")
    v = vs[0]
    frames, vals = v_keys(n_frames, fps)
    flight._fcurves(v, "Life_V_Action", [('["glow"]', 0, frames, vals)], interp="CONSTANT")
    spill = [ob for ob in bpy.data.objects if ob.name.startswith("Light_Neon_MotelSign_")]
    if not spill:
        raise AssertionError("the motel sign's spill lights are missing")
    for ob in spill:
        d = ob.data.animation_data.drivers.find("energy").driver
        var = d.variables.new()
        var.name = "b"
        var.type = "SINGLE_PROP"
        var.targets[0].id_type = "OBJECT"
        var.targets[0].id = v
        var.targets[0].data_path = '["glow"]'
        lo = config.MOTEL_V_SPILL_OFF
        d.expression = f"({d.expression}) * ({lo!r} + {1.0 - lo!r} * b)"
    return v, (frames, vals)


# ---------------------------------------------------------------------------
# The pit's breath
# ---------------------------------------------------------------------------

def breath(t):
    """The pit's glow level at t seconds (numpy-friendly): a trough at
    PIT_BREATH_TROUGH_S (+ k x PIT_BREATH_S), rising for the inhale's share of the
    cycle, falling for the rest; each half a half-cosine, so the level and its slope are
    continuous everywhere (no kink at the top or bottom: a breath, not a pulse)."""
    P = config.PIT_BREATH_S
    rise = P * config.PIT_BREATH_IN
    u = np.mod(np.asarray(t, dtype=float) - config.PIT_BREATH_TROUGH_S, P)
    s = np.where(u < rise, 0.5 - 0.5 * np.cos(np.pi * u / rise),
                 0.5 + 0.5 * np.cos(np.pi * (u - rise) / (P - rise)))
    lo, hi = config.PIT_BREATH_RANGE
    return lo + (hi - lo) * s


def breathe(n_frames, fps):
    """Key the pit void's glow on every frame 1..n_frames (LINEAR between: motion blur
    samples the curve) and scale the pit's two lights by it. Returns the void object."""
    import bpy

    import flight
    voids = [ob for ob in bpy.data.objects if ob.get(config.TAG_PROP)
             and any(sl.material and sl.material.name.startswith("LF_Pit") for sl in ob.material_slots)]
    if len(voids) != 1:
        raise AssertionError(f"expected the pit's one void (LF_Pit), found {[o.name for o in voids]}")
    void = voids[0]
    frames = np.arange(1, n_frames + 1, dtype=float)
    flight._fcurves(void, "Life_Pit_Action", [('["glow"]', 0, frames, breath((frames - 1) / fps))])
    lights = [bpy.data.objects.get(f"Light_Pit_{n}") for n in ("Under", "Leak")]
    if None in lights:
        raise AssertionError("the pit's lights (Light_Pit_Under / _Leak) are missing")
    for ob in lights:
        d = ob.data.animation_data.drivers.find("energy").driver
        var = d.variables.new()
        var.name = "b"
        var.type = "SINGLE_PROP"
        var.targets[0].id_type = "OBJECT"
        var.targets[0].id = void
        var.targets[0].data_path = '["glow"]'
        d.expression = f"({d.expression}) * b"
    return void


# ---------------------------------------------------------------------------
# Shared: node-graph bits (Geometry Nodes through materials.G)
# ---------------------------------------------------------------------------

def _seconds(g, fps):
    st = g.node("GeometryNodeInputSceneTime")
    return g.math("DIVIDE", g.math("SUBTRACT", st.outputs["Frame"], 1.0), float(fps))


def _named(g, name, data_type):
    n = g.node("GeometryNodeInputNamedAttribute", data_type=data_type)
    n.inputs["Name"].default_value = name
    return n.outputs["Attribute"]


def _vmath(g, op, a, b=None, scale=None):
    n = g.node("ShaderNodeVectorMath", operation=op)
    import bpy
    for s, v in zip(n.inputs, (a, b)):
        if v is None:
            continue
        if isinstance(v, bpy.types.NodeSocket):
            g.link(v, s)
        else:
            s.default_value = v
    if scale is not None:
        g._in(n, "Scale", scale)
    return n.outputs["Value" if op in ("DOT_PRODUCT", "LENGTH", "DISTANCE") else "Vector"]


def _wind_xy():
    a = math.radians(config.WIND_TOWARD_DEG)
    return math.cos(a), math.sin(a)


# ---------------------------------------------------------------------------
# Wind
# ---------------------------------------------------------------------------

def _families(kind):
    import forest
    fam = np.array([f for f, _k in forest.KIT], object)
    return fam[np.asarray(kind, int)]


def sway_mask(table, ground, pos, kit_hi):
    """1 for every tree (config.TREE_FAMILIES, either tier) whose trunk mid-height comes
    within WIND_NEAR_M of the lens at some frame (every 3rd). pos: (n, 3) the lens."""
    fam = np.array([f[:-3] if f.endswith("_lo") else f for f in _families(table.kind)], object)
    tree = np.isin(fam, config.TREE_FAMILIES)
    P = np.asarray(pos, float)[::3]
    r = config.WIND_NEAR_M
    lo, hi = P[:, :2].min(0) - r, P[:, :2].max(0) + r
    cand = np.where(tree & (table.x > lo[0]) & (table.x < hi[0]) & (table.y > lo[1]) & (table.y < hi[1]))[0]
    near = np.zeros(len(table.x), np.float32)
    for c in np.array_split(cand, max(1, len(cand) // 2000)):
        d2 = (table.x[c, None] - P[None, :, 0]) ** 2 + (table.y[c, None] - P[None, :, 1]) ** 2
        ok = d2.min(axis=1) < r * r
        c, d2 = c[ok], d2[ok]
        if not len(c):
            continue
        hk = np.array([kit_hi[int(k)] for k in table.kind[c]])
        zg = np.array([ground.hit(x, y) or 0.0 for x, y in zip(table.x[c], table.y[c])])
        zm = zg + 0.5 * hk * table.scale[c, 2]
        d = np.sqrt(d2 + (zm[:, None] - P[None, :, 2]) ** 2).min(axis=1)
        near[c] = (d < r).astype(np.float32)
    return near


def wind_nodes(g, inst, fps):
    """Rotate every instance of `inst` about its base: a gust field travelling downwind
    (3D noise at (p - U t) / L) plus a slow per-tree sway, a slight mean lean, and a
    smaller cross-wind rock. Returns the rotated instances socket."""
    ux, uy = _wind_xy()
    sec = _seconds(g, fps)
    pos = g.node("GeometryNodeInputPosition").outputs["Position"]
    seed = _named(g, "seed", "FLOAT")
    U = config.WIND_GUST_SPEED_MS
    drift = g.combine(g.math("MULTIPLY", sec, U * ux), g.math("MULTIPLY", sec, U * uy), 0.0)
    q = _vmath(g, "SUBTRACT", pos, drift)
    gust = g.math("SUBTRACT", g.math("MULTIPLY", g.noise(q, 1.0 / config.WIND_GUST_M, 1.0, 0.5), 2.0), 1.0)
    T = config.WIND_PERIOD_S
    osc = g.math("SINE", g.math("ADD", g.math("MULTIPLY", sec, TAU / T), g.math("MULTIPLY", seed, TAU)))
    rock = g.math("SINE", g.math("ADD", g.math("MULTIPLY", sec, TAU / (T * 0.83)), g.math("MULTIPLY", seed, 13.0)))
    A = config.WIND_TILT_RAD
    lean, wg, wo, wr = config.WIND_MIX
    down = g.math("MULTIPLY", g.math("ADD", g.math("ADD", g.math("MULTIPLY", gust, wg), g.math("MULTIPLY", osc, wo)),
                                     lean), A)
    side = g.math("MULTIPLY", rock, A * wr)
    # a tilt `down` toward the wind's heading + `side` across it, as rotations about
    # world X / Y: tipping the top toward (ux, uy) is +down about (-uy, ux)
    ax = g.math("SUBTRACT", g.math("MULTIPLY", down, -uy), g.math("MULTIPLY", side, ux))
    ay = g.math("ADD", g.math("MULTIPLY", down, ux), g.math("MULTIPLY", side, -uy))
    e = g.node("FunctionNodeEulerToRotation")
    g.link(g.combine(ax, ay, 0.0), e.inputs[0])
    ri = g.node("GeometryNodeRotateInstances")
    g.link(inst, ri.inputs["Instances"])
    g.link(e.outputs[0], ri.inputs["Rotation"])
    g.link(pos, ri.inputs["Pivot Point"])
    ri.inputs["Local Space"].default_value = False
    return ri.outputs["Instances"]


class Ground:
    """The rendered ground as FO_ForestScatter sees it: the ground meshes (no modifiers)
    in world space, hit straight down from scatter.RAY_FROM_Z; the first surface from
    above wins (the joined geometry's nearest hit)."""

    def __init__(self, grounds):
        import bmesh
        from mathutils.bvhtree import BVHTree
        self.trees = []
        for ob in grounds:
            bm = bmesh.new()
            bm.from_mesh(ob.data)
            bm.transform(ob.matrix_world)
            self.trees.append(BVHTree.FromBMesh(bm))
            bm.free()

    def hit(self, x, y):
        """World z of the ground under (x, y), or None."""
        from mathutils import Vector

        import scatter
        o, down = Vector((float(x), float(y), scatter.RAY_FROM_Z)), Vector((0.0, 0.0, -1.0))
        zs = [h[0].z for h in (b.ray_cast(o, down, scatter.RAY_LENGTH) for b in self.trees) if h[0] is not None]
        return max(zs) if zs else None

    def bases(self, table, rows):
        """(n, 3) trunk bases for the rows (`sink` below the hit); NaN where it misses."""
        out = np.full((len(rows), 3), np.nan)
        for n, j in enumerate(rows):
            z = self.hit(table.x[j], table.y[j])
            if z is not None:
                out[n] = (table.x[j], table.y[j], z - float(table.sink[j]))
        return out


def wind(table, fl, kit, kit_hi, grounds, fcol):
    """Mark the near trees (`sway` on Forest_Points: the static scatter drops them) and
    plant them again, pre-grounded, as the wind twins (Forest_WindPoints, instanced by
    Forest_Wind / Forest_WindCards: no raycast per frame). Returns the swaying count."""
    import bpy

    import scatter
    import scene
    pts = bpy.data.objects["Forest_Points"]
    ground = Ground(grounds)
    sway = sway_mask(table, ground, fl.pos, kit_hi)     # the film's (primary) flight only:
    # adding another flight's near trees would move trees the film shows (its frames reproduce)
    rows = np.where(sway > 0.5)[0]
    base = ground.bases(table, rows)
    hit = ~np.isnan(base[:, 0])
    sway[rows[~hit]] = 0.0                 # a miss is dropped by the scatter anyway
    rows, base = rows[hit], base[hit]
    a = pts.data.attributes.new("sway", "FLOAT", "POINT")
    a.data.foreach_set("value", sway)
    pts.data.update()
    me = scene.tag(bpy.data.meshes.new("Forest_WindPoints"))
    me.vertices.add(len(rows))
    me.vertices.foreach_set("co", base.astype(np.float32).ravel())
    for name, kind in (("kind", "INT"), ("rot", "FLOAT"), ("seed", "FLOAT"), ("scale", "FLOAT_VECTOR"),
                       ("crown", "FLOAT_COLOR"), ("crown2", "FLOAT_COLOR"), ("bark", "FLOAT_COLOR")):
        at = me.attributes.new(name, kind, "POINT")
        key = {"INT": "value", "FLOAT": "value", "FLOAT_VECTOR": "vector", "FLOAT_COLOR": "color"}[kind]
        vals = np.asarray(getattr(table, name))[rows]
        at.data.foreach_set(key, vals.astype(np.int32 if kind == "INT" else np.float32).ravel())
    me.update()
    body, cards = kit
    post = lambda g, inst: wind_nodes(g, inst, fl.fps)   # noqa: E731
    ob = scene.new_object("Forest_Wind", me, fcol, kind="wind", instances=len(rows))
    ob.modifiers.new("FO_ForestWind", "NODES").node_group = scatter.node_group(
        grounds, body, "FO_ForestWind", placed=True, post=post)
    cob = scene.new_object("Forest_WindCards", me, fcol, kind="wind")
    cob.visible_shadow = False
    cob.modifiers.new("FO_ForestWindCards", "NODES").node_group = scatter.node_group(
        grounds, cards, "FO_ForestWindCards", placed=True, post=post)
    return len(rows)


# ---------------------------------------------------------------------------
# Falling leaves
# ---------------------------------------------------------------------------

class Leaves:
    """Column arrays, one row per leaf: o (n, 3) origin, drop (m), per (s, the loop),
    ph (s), drift (n, 3) m/s, flut (n, 3) (amplitude m, rad/s, phase), spin (n, 3) rad/s,
    size (m), col (n, 4) linear RGBA."""

    def positions(self, sec):
        """(n, 3) positions and (n,) scale factors at time sec (the node formula)."""
        u = np.mod((sec + self.ph) / self.per, 1.0)
        age = u * self.per
        a, w, p = self.flut[:, 0], self.flut[:, 1], self.flut[:, 2]
        off = np.stack([a * np.sin(w * sec + p), 0.6 * a * np.cos(0.77 * w * sec + p), -u * self.drop], 1)
        f = config.LEAF_FADE
        fade = np.clip(u / f, 0.0, 1.0) * np.clip((1.0 - u) / f, 0.0, 1.0)
        return self.o + self.drift * age[:, None] + off, fade

    def keep(self, m):
        for k in ("o", "drop", "per", "ph", "drift", "flut", "spin", "size", "col"):
            setattr(self, k, getattr(self, k)[m])
        return self


def _in_hull(hulls, P, pad=0.3):
    """True where a point lies inside a building / tall prop hull height field."""
    inside = np.zeros(len(P), bool)
    for x0, y0, cell, H in hulls:
        H = np.asarray(H)
        i = np.floor((P[:, 0] - x0) / cell).astype(int)
        j = np.floor((P[:, 1] - y0) / cell).astype(int)
        ok = (i >= 0) & (j >= 0) & (j < H.shape[0]) & (i < H.shape[1])
        if not ok.any():
            continue
        h = np.full(len(P), -np.inf)
        h[ok] = H[j[ok], i[ok]]
        inside |= P[:, 2] < h + pad
    return inside


def plant_leaves(table, terrain, fl, kit_hi, kit_rho, hulls, others=()):
    """The leaves: dropped from broadleaf crowns the lens passes within LEAF_TREE_M of
    (in front of it) during LEAF_WINDOWS, off the crown's edge facing the flight; lens /
    building checks on every frame."""
    rng = np.random.default_rng(config.LIFE_SEED)
    fam = _families(table.kind)
    broad = np.isin(fam, config.LEAF_FAMILIES)
    fps = fl.fps
    chosen = []
    for t0, t1 in config.LEAF_WINDOWS:
        fr = np.arange(int(t0 * fps), min(int(t1 * fps), fl.n - 1) + 1, 3)
        P = fl.pos[fr]
        fwd = -fl.R[fr][:, :, 2]
        best = {}
        r = config.LEAF_TREE_M
        box = (P[:, :2].min(0) - r, P[:, :2].max(0) + r)
        idx = np.where(broad & (table.x > box[0][0]) & (table.x < box[1][0]) & (table.y > box[0][1])
                       & (table.y < box[1][1]))[0]
        for k in range(len(fr)):
            d = np.stack([table.x[idx] - P[k, 0], table.y[idx] - P[k, 1]], 1)
            dist = np.hypot(d[:, 0], d[:, 1])
            ahead = (d[:, 0] * fwd[k, 0] + d[:, 1] * fwd[k, 1]) > 0.5 * dist * np.hypot(fwd[k, 0], fwd[k, 1])
            for j, dj in zip(idx[(dist < r) & ahead].tolist(), dist[(dist < r) & ahead].tolist()):
                if dj < best.get(j, (np.inf,))[0]:
                    best[j] = (dj, P[k, 0], P[k, 1])
        # the crowns the lens passes nearest (the leaves read there; far ones are specks),
        # each shedding from its side facing the flight (in the open air, not under it)
        for j in sorted(best, key=lambda j: (best[j][0], j))[:config.LEAF_TREES_PER_WINDOW]:
            face = math.atan2(best[j][2] - table.y[j], best[j][1] - table.x[j])
            chosen.append((j, face))
    chosen = sorted(dict(chosen).items())
    n_per = config.LEAF_PER_TREE
    rows = []
    for j, face in chosen:
        k = int(table.kind[j])
        H = kit_hi[k] * float(table.scale[j, 2])
        R = kit_rho[k] * float(max(table.scale[j, 0], table.scale[j, 1]))
        zg = terrain.z_at(float(table.x[j]), float(table.y[j]))
        c0, c1 = table.crown[j], table.crown2[j]
        for _ in range(n_per):
            a = face + rng.normal(0.0, 0.6)
            rr = R * rng.uniform(0.75, 1.05)
            z0 = zg + H * rng.uniform(0.45, 0.85)
            rows.append((table.x[j] + rr * math.cos(a), table.y[j] + rr * math.sin(a), z0, z0 - zg - 0.05,
                         rng.uniform(*config.LEAF_FALL_MS), rng.uniform(0, 1), rng.uniform(0.35, 1.0),
                         rng.uniform(0.25, 0.7), rng.uniform(TAU / 2.8, TAU / 1.3), rng.uniform(0, TAU),
                         rng.uniform(-1, 1, 3) * config.LEAF_SPIN, rng.uniform(*config.LEAF_SIZE_M),
                         c0 if rng.uniform() < 0.55 else c1))
    L = Leaves()
    n = len(rows)
    L.o = np.array([r[:3] for r in rows], float).reshape(n, 3)
    L.drop = np.array([r[3] for r in rows], float)
    v = np.array([r[4] for r in rows], float)
    L.per = L.drop / v
    L.ph = np.array([r[5] for r in rows], float) * L.per
    ux, uy = _wind_xy()
    g = np.array([r[6] for r in rows], float)
    U = config.LEAF_DRIFT_MS
    L.drift = np.stack([g * U * ux, g * U * uy, np.zeros(n)], 1)
    L.flut = np.array([(r[7], r[8], r[9]) for r in rows], float).reshape(n, 3)
    L.spin = np.array([r[10] for r in rows], float).reshape(n, 3)
    L.size = np.array([r[11] for r in rows], float)
    L.col = np.array([r[12] for r in rows], float).reshape(n, 4)
    planted = n
    # the checks, every frame: never near the lens, never into a building
    bad = np.zeros(n, bool)
    for f in range(1, fl.n + 1):
        X, fade = L.positions((f - 1) / fps)
        vis = fade > 0.0
        d = np.linalg.norm(X - fl.pos[f - 1], axis=1)
        bad |= vis & (d < config.LEAF_CLEAR_M)
        if f % 3 == 1:
            bad |= vis & _in_hull(hulls, X)
    for o in others:                        # the other flights' lenses too (whole length)
        for f in range(1, o.n + 1):
            X, fade = L.positions((f - 1) / fps)
            bad |= (fade > 0.0) & (np.linalg.norm(X - o.pos[f - 1], axis=1) < config.LEAF_CLEAR_M)
    L.keep(~bad)
    L.stats = dict(trees=len(chosen), planted=planted, dropped=int(bad.sum()), kept=len(L.drop))
    return L


def _leaf_mesh():
    """One leaf ~1 unit long: a curled pointed oval (instanced at its size)."""
    import bpy

    import scene
    ring = []
    for j in range(10):
        t = TAU * j / 10
        px, py = 0.5 * math.cos(t), 0.3 * math.sin(t) * (1.0 - 0.35 * math.cos(t))
        ring.append((px, py, 0.18 * px * px - 0.06 * abs(py)))
    me = scene.tag(bpy.data.meshes.new("Life_LeafMesh"))
    me.from_pydata(ring + [(0.0, 0.0, 0.02)], [], [(i, (i + 1) % 10, 10) for i in range(10)])
    me.update()
    for p in me.polygons:
        p.use_smooth = True
    return me


def _leaf_material():
    import bpy

    import materials
    import scene
    mat = scene.tag(bpy.data.materials.new("Life_Leaf"))
    mat.diffuse_color = config.hex_rgba(config.colour("lantern"))
    g = materials.G(mat)
    a = g.node("ShaderNodeAttribute", attribute_type="INSTANCER", attribute_name="col")
    b = g.node("ShaderNodeBsdfPrincipled")
    g.link(a.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.7
    tr = g.node("ShaderNodeBsdfTranslucent")
    g.link(a.outputs["Color"], tr.inputs["Color"])
    mix = g.node("ShaderNodeMixShader")
    mix.inputs["Fac"].default_value = 0.45     # thin leaves: lit through from behind
    g.link(b.outputs["BSDF"], mix.inputs[1])
    g.link(tr.outputs["BSDF"], mix.inputs[2])
    o = g.node("ShaderNodeOutputMaterial")
    g.link(mix.outputs["Shader"], o.inputs["Surface"])
    mat.use_backface_culling = False
    return mat


def _points(name, n, attrs):
    import bpy

    import scene
    me = scene.tag(bpy.data.meshes.new(name))
    me.vertices.add(n)
    for key, (kind, vals) in attrs.items():
        a = me.attributes.new(key, kind, "POINT")
        field = {"FLOAT": "value", "FLOAT_VECTOR": "vector", "FLOAT_COLOR": "color"}[kind]
        a.data.foreach_set(field, np.asarray(vals, np.float32).ravel())
    me.update()
    return me


def leaves_nodes(ng, proto, fps):
    """Position = o + drift * age + flutter - u * drop; tumble; scale = size x fade."""
    import materials
    g = materials.G(tree=ng)
    gin = g.node("NodeGroupInput")
    sec = _seconds(g, fps)
    per, ph, drop = _named(g, "per", "FLOAT"), _named(g, "ph", "FLOAT"), _named(g, "drop", "FLOAT")
    u = g.math("FRACT", g.math("DIVIDE", g.math("ADD", sec, ph), per))
    age = g.math("MULTIPLY", u, per)
    fx, fw, fp = g.sep(_named(g, "flut", "FLOAT_VECTOR"))
    ox = g.math("MULTIPLY", fx, g.math("SINE", g.math("ADD", g.math("MULTIPLY", fw, sec), fp)))
    oy = g.math("MULTIPLY", g.math("MULTIPLY", fx, 0.6),
                g.math("COSINE", g.math("ADD", g.math("MULTIPLY", g.math("MULTIPLY", fw, 0.77), sec), fp)))
    oz = g.math("MULTIPLY", g.math("MULTIPLY", u, drop), -1.0)
    p = _vmath(g, "ADD", _named(g, "o", "FLOAT_VECTOR"), _vmath(g, "SCALE", _named(g, "drift", "FLOAT_VECTOR"),
                                                                scale=age))
    p = _vmath(g, "ADD", p, g.combine(ox, oy, oz))
    setp = g.node("GeometryNodeSetPosition")
    g.link(gin.outputs[0], setp.inputs["Geometry"])
    g.link(p, setp.inputs["Position"])
    f = config.LEAF_FADE
    fade = g.math("MULTIPLY", g.math("MINIMUM", g.math("DIVIDE", u, f), 1.0),
                  g.math("MINIMUM", g.math("DIVIDE", g.math("SUBTRACT", 1.0, u), f), 1.0))
    sx, sy, sz = g.sep(_named(g, "spin", "FLOAT_VECTOR"))
    rx = g.math("ADD", g.math("MULTIPLY", sx, sec), fp)
    ry = g.math("ADD", g.math("MULTIPLY", sy, sec), g.math("MULTIPLY", fp, 2.0))
    rz = g.math("ADD", g.math("MULTIPLY", sz, sec), g.math("MULTIPLY", fp, 3.0))
    e = g.node("FunctionNodeEulerToRotation")
    g.link(g.combine(rx, ry, rz), e.inputs[0])
    oi = g.node("GeometryNodeObjectInfo", transform_space="ORIGINAL")
    oi.inputs["Object"].default_value = proto
    inst = g.node("GeometryNodeInstanceOnPoints")
    g.link(setp.outputs["Geometry"], inst.inputs["Points"])
    g.link(oi.outputs["Geometry"], inst.inputs["Instance"])
    g.link(e.outputs[0], inst.inputs["Rotation"])
    sc = g.math("MULTIPLY", _named(g, "size", "FLOAT"), fade)
    g.link(g.combine(sc, sc, sc), inst.inputs["Scale"])
    gout = g.node("NodeGroupOutput")
    g.link(inst.outputs["Instances"], gout.inputs[0])


def _group(name):
    import bpy

    import scene
    ng = scene.tag(bpy.data.node_groups.new(name, "GeometryNodeTree"))
    ng.interface.new_socket("Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
    ng.interface.new_socket("Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
    return ng


def leaves(L, fps, col):
    """Life_Leaves: the point cloud + its nodes; the leaf prototype hidden in the kit."""
    import scene
    n = len(L.drop)
    me = _points("Life_Leaves", n, {"o": ("FLOAT_VECTOR", L.o), "drop": ("FLOAT", L.drop),
                                    "per": ("FLOAT", L.per), "ph": ("FLOAT", L.ph),
                                    "drift": ("FLOAT_VECTOR", L.drift), "flut": ("FLOAT_VECTOR", L.flut),
                                    "spin": ("FLOAT_VECTOR", L.spin), "size": ("FLOAT", L.size),
                                    "col": ("FLOAT_COLOR", L.col)})
    leaf = _leaf_mesh()
    leaf.materials.append(_leaf_material())
    proto = scene.new_object("Life_LeafProto", leaf, col, kind="proto")
    proto.hide_render = True
    proto.hide_viewport = True
    ob = scene.new_object("Life_Leaves", me, col, kind="leaves", leaves=n, **L.stats)
    ob.visible_shadow = False
    ng = _group("FO_Leaves")
    leaves_nodes(ng, proto, fps)
    ob.modifiers.new("FO_Leaves", "NODES").node_group = ng
    return ob


# ---------------------------------------------------------------------------
# Chimney smoke
# ---------------------------------------------------------------------------

class Plume:
    """One chimney's puffs: at (flue), per puff ph (s), rise (m/s), side (phase), plus
    the shared life / drift. The node formula, in numpy for the lens check."""

    def __init__(self, flue, rng):
        n = config.SMOKE_PUFFS
        Ls = config.SMOKE_LIFE_S
        self.flue = np.asarray(flue, float)
        self.ph = (np.arange(n) + rng.uniform(0, 0.8, n)) / n * Ls
        self.rise = rng.uniform(*config.SMOKE_RISE_MS, n)
        self.side = rng.uniform(0, TAU, n)
        self.grow = rng.uniform(0.8, 1.2, n)

    def at(self, sec):
        """(n, 3) centres, (n,) radii, (n,) opacity."""
        Ls = config.SMOKE_LIFE_S
        age = np.mod(sec + self.ph, Ls)
        ux, uy = _wind_xy()
        U = config.SMOKE_DRIFT_MS
        w = 0.25 * age * np.sin(1.3 * sec + self.side)
        c = self.flue + np.stack([U * ux * age - uy * w, U * uy * age + ux * w,
                                  self.rise * age * (1.0 - 0.3 * age / Ls)], 1)
        r0, gr = config.SMOKE_RADIUS
        r = (r0 + gr * age) * self.grow
        op = np.clip(age / 0.6, 0, 1) * (1.0 - age / Ls) ** config.SMOKE_FADE_POW
        return c, r, op


def smoke_nodes(ng, proto, flue, fps):
    import materials
    g = materials.G(tree=ng)
    gin = g.node("NodeGroupInput")
    sec = _seconds(g, fps)
    Ls = config.SMOKE_LIFE_S
    ph, rise, side, grow = (_named(g, k, "FLOAT") for k in ("ph", "rise", "side", "grow"))
    age = g.math("FLOORED_MODULO", g.math("ADD", sec, ph), Ls)
    ux, uy = _wind_xy()
    U = config.SMOKE_DRIFT_MS
    w = g.math("MULTIPLY", g.math("MULTIPLY", age, 0.25),
               g.math("SINE", g.math("ADD", g.math("MULTIPLY", sec, 1.3), side)))
    x = g.math("ADD", g.math("MULTIPLY", age, U * ux), g.math("MULTIPLY", w, -uy))
    y = g.math("ADD", g.math("MULTIPLY", age, U * uy), g.math("MULTIPLY", w, ux))
    z = g.math("MULTIPLY", g.math("MULTIPLY", rise, age),
               g.math("SUBTRACT", 1.0, g.math("MULTIPLY", age, 0.3 / Ls)))
    setp = g.node("GeometryNodeSetPosition")
    g.link(gin.outputs[0], setp.inputs["Geometry"])
    g.link(_vmath(g, "ADD", g.combine(x, y, z), tuple(flue)), setp.inputs["Position"])
    r0, gr = config.SMOKE_RADIUS
    r = g.math("MULTIPLY", g.math("ADD", g.math("MULTIPLY", age, gr), r0), grow)
    op = g.math("MULTIPLY", g.math("MINIMUM", g.math("DIVIDE", age, 0.6), 1.0),
                g.math("POWER", g.math("SUBTRACT", 1.0, g.math("DIVIDE", age, Ls)), config.SMOKE_FADE_POW))
    sa = g.node("GeometryNodeStoreNamedAttribute", data_type="FLOAT", domain="POINT")
    sa.inputs["Name"].default_value = "op"
    g.link(setp.outputs["Geometry"], sa.inputs["Geometry"])
    g.link(op, sa.inputs["Value"])
    oi = g.node("GeometryNodeObjectInfo", transform_space="ORIGINAL")
    oi.inputs["Object"].default_value = proto
    e = g.node("FunctionNodeEulerToRotation")
    g.link(g.combine(g.math("MULTIPLY", side, 1.0), g.math("MULTIPLY", side, 2.0), g.math("MULTIPLY", age, 0.2)),
           e.inputs[0])
    inst = g.node("GeometryNodeInstanceOnPoints")
    g.link(sa.outputs["Geometry"], inst.inputs["Points"])
    g.link(oi.outputs["Geometry"], inst.inputs["Instance"])
    g.link(e.outputs[0], inst.inputs["Rotation"])
    g.link(g.combine(r, r, g.math("MULTIPLY", r, 0.85)), inst.inputs["Scale"])
    gout = g.node("NodeGroupOutput")
    g.link(inst.outputs["Instances"], gout.inputs[0])


def _smoke_material():
    """Soft puffs: alpha = the instance's opacity x a rim falloff (facing) x a broken-up
    noise, BLENDED (smooth alpha, no shadow); a pale cool grey lit by the scene."""
    import bpy

    import materials
    import scene
    mat = scene.tag(bpy.data.materials.new("Life_Smoke"))
    mat.diffuse_color = config.hex_rgba(config.colour(config.SMOKE_COLOUR))
    g = materials.G(mat)
    op = g.node("ShaderNodeAttribute", attribute_type="INSTANCER", attribute_name="op").outputs["Fac"]
    lw = g.node("ShaderNodeLayerWeight")
    lw.inputs["Blend"].default_value = 0.5
    core = g.math("POWER", g.math("SUBTRACT", 1.0, lw.outputs["Facing"]), 2.2)
    tc = g.node("ShaderNodeTexCoord").outputs["Object"]
    brk = g.math("ADD", g.math("MULTIPLY", g.noise(tc, 1.6, 3.0, 0.6), 1.2), 0.2)
    a = g.math("MINIMUM", g.math("MULTIPLY", g.math("MULTIPLY", core, brk), g.math("MULTIPLY", op, config.SMOKE_ALPHA)),
               1.0)
    b = g.node("ShaderNodeBsdfPrincipled")
    b.inputs["Base Color"].default_value = materials.rgba(config.SMOKE_COLOUR)
    b.inputs["Roughness"].default_value = 1.0
    s = b.inputs.get("Specular IOR Level")
    if s is not None:
        s.default_value = 0.0
    g.link(a, b.inputs["Alpha"])
    o = g.node("ShaderNodeOutputMaterial")
    g.link(b.outputs["BSDF"], o.inputs["Surface"])
    mat.surface_render_method = "BLENDED"
    mat.use_backface_culling = True
    if hasattr(mat, "use_transparent_shadow"):
        mat.use_transparent_shadow = False
    return mat


def _puff_mesh():
    import bmesh
    import bpy

    import scene
    me = scene.tag(bpy.data.meshes.new("Life_PuffMesh"))
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    return me


def smoke(world, fl, col, others=()):
    """A plume from every SMOKE_DESIGNS building's flue. Returns [(name, stats)]."""
    import bpy

    import scene
    rng = np.random.default_rng(config.LIFE_SEED + 1)
    out = []
    proto = None
    for ob in sorted((o for o in bpy.data.objects if o.get("kind") == "building"
                      and o.get("design") in config.SMOKE_DESIGNS), key=lambda o: o.name):
        if "mount_flue" not in ob:
            raise AssertionError(f"{ob.name}: a smoking design without a mount_flue")
        if proto is None:
            me = _puff_mesh()
            me.materials.append(_smoke_material())
            proto = scene.new_object("Life_PuffProto", me, col, kind="proto")
            proto.hide_render = True
            proto.hide_viewport = True
        flue = tuple(ob["mount_flue"])
        pl = Plume(flue, rng)
        # the lens never enters a puff (radius + SMOKE_CLEAR_M), every frame
        near = np.inf
        for o in (fl, *others):
            for f in range(1, o.n + 1):
                c, r, op = pl.at((f - 1) / o.fps)
                d = np.linalg.norm(c - o.pos[f - 1], axis=1) - r
                near = min(near, float(d[op > 0.02].min()))
        if near < config.SMOKE_CLEAR_M:
            raise AssertionError(f"{ob.name}: the smoke comes within {near:.1f} m of the lens")
        n = len(pl.ph)
        me = _points(f"Life_Smoke_{ob['dump_id']}", n, {"ph": ("FLOAT", pl.ph), "rise": ("FLOAT", pl.rise),
                                                        "side": ("FLOAT", pl.side), "grow": ("FLOAT", pl.grow)})
        name = f"Life_Smoke_{ob['dump_id']}"
        so = scene.new_object(name, me, col, kind="smoke", building=ob.name, lens_clear_m=round(near, 2))
        so.visible_shadow = False
        ng = _group(f"FO_Smoke_{ob['dump_id']}")
        smoke_nodes(ng, proto, flue, fl.fps)
        so.modifiers.new("FO_Smoke", "NODES").node_group = ng
        out.append((name, dict(puffs=n, lens_clear_m=round(near, 1))))
    return out


# ---------------------------------------------------------------------------
# The phase
# ---------------------------------------------------------------------------

def build(world, terrain, fl, kit, kit_vertices, grounds, root, fcol, others=()):
    """Everything above, after the flights are planned (fl the primary: the leaves'
    windows and the swaying trees: the film's frames reproduce; `others` the other
    flights: every leaf and puff keeps clear of their lenses too, and the V is keyed over
    the longest). Returns a stats dict."""
    import time

    import scene
    t0 = time.time()
    secs = {}
    col = scene.collection(config.COL_LIFE, root)
    n_life = max([fl.n] + [o.n for o in others]) + 60 * fl.fps
    _v, (frames, _vals) = blink(n_life, fl.fps)
    breathe(n_life, fl.fps)
    kit_hi = {k: float(v[:, 2].max()) for k, v in kit_vertices.items()}
    kit_rho = {k: float(np.hypot(v[:, 0], v[:, 1]).max()) for k, v in kit_vertices.items()}
    t1 = time.time()
    swaying = wind(world.forest.table, fl, kit, kit_hi, grounds, fcol)
    secs["wind"], t1 = round(time.time() - t1, 1), time.time()
    hulls = [b["hull"] for b in world.buildings] + [p["hull"] for p in world.props if "hull" in p]
    L = plant_leaves(world.forest.table, terrain, fl, kit_hi, kit_rho, hulls, others)
    leaves(L, fl.fps, col)
    secs["leaves"], t1 = round(time.time() - t1, 1), time.time()
    plumes = smoke(world, fl, col, others)
    secs["smoke"] = round(time.time() - t1, 1)
    secs["all"] = round(time.time() - t0, 1)
    st = dict(v_keys=len(frames), swaying=swaying, leaves=L.stats, smoke=plumes, secs=secs)
    print(f"flyover: life {st}")
    return st

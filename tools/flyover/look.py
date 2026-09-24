"""The look (Phase 8): the dusk sun, the sky, the valley haze, the grade and the finishes.

One control is the TIME of the light: the scene property `flyover_dusk` (config.DUSK_PROP),
0 = 16:00 (the game's last day key: late afternoon) .. 1 = the finish's dusk (the game's
18:00 key, "lanterns light here"). Every keyed value below is a driver
`lerp(afternoon, dusk, flyover_dusk)` (a simple expression: evaluates headless), and every
light family is gated by it too (config.DUSK_GATE: windows / street / pit ramp in with the
game's LightLevel, neon and lit bands cut on at 18:00). Phase 9 animates the one property.

- Sun: a SUN light in Flyover_Look, low in the WSW (the road curves out west), warm; its
  shadow filter radius softens the shadow-map texels that stair-stepped the Phase 6-7
  tree shadows (cheap; jittered soft shadows cost 3x at a low sun).
- Sky: Flyover_Sky, a painted gradient (not the physical sky: palette-led), horizon ->
  sky-day #8fb8cf -> a deeper zenith, a warm glow round the sun's azimuth, a sun disc.
  It also lights the scene (EEVEE's world probe): the cool fill in the shadows.
- Haze + grade: the compositor tree Flyover_Grade. Height fog from the Depth + Position
  passes (analytic exponential fog between the camera's and the point's height: the
  valley fills, the ridges separate in layers, the far forest recedes), a distance haze,
  bloom (neon / lamps glow, keeping their hue), a lift / gamma / gain toward the palette,
  a soft vignette. Cheap (2D), noise-free, deterministic; no volumetrics.
- View transform AgX (neon keeps its hue instead of clipping to white under Standard).

FINISHES are the switchable looks (build.py --finish, `apply_finish(name)` live); each
overrides the DUSK key (+ haze and grade) over the shared AFTERNOON key.
"""

import math

import bpy

import config
import scene

SUN_NAME = "Sun"
WORLD_NAME = "Flyover_Sky"
GRADE_NAME = "Flyover_Grade"
FINISH_PROP = "flyover_finish"

# The shared late-afternoon key (flyover_dusk 0 = 16:00): autumn, sun ~22 deg up in the SW.
AFTERNOON = dict(
    sun_el=22.0, sun_az=232.0, sun_w=4.2, sun_rgb="#fff0d8",
    zenith="#5d8db3", sky="#8fb8cf", horizon="#d3dcd6", glow="#fff1d6", glow_w=0.5, sky_w=1.0,
    fog_rgb="#b8c9cf", haze_km=0.10, valley=0.0012, valley_h=45.0,
)

# Each finish is the DUSK key (flyover_dusk 1) + its haze and grade. Colours are
# display hexes (palette-led: sky-day, lantern, water-deep, the dusk tint #c4703f).
FINISHES = {
    # The default: the sun just above the western ridges, the valley in blue shade with
    # its lights on, the ridges tops catching the last warm light, a light valley haze.
    "clear_dusk": dict(
        sun_el=5.0, sun_az=252.0, sun_w=3.2, sun_rgb="#ffdcb0",
        zenith="#4b7392", sky="#88abc0", horizon="#dcc3a4", glow="#ffb46a", glow_w=1.2, sky_w=0.72,
        fog_rgb="#95a4ae", haze_km=0.10, valley=0.0012, valley_h=30.0, fog_max=0.85,
        exposure=0.0, look="AgX - Base Contrast",
        lift="#f0f8f8", gamma="#fffdf8", gain="#fff8ef", bloom=0.55, vignette=0.22,
    ),
    # Heavier valley mist: the town sits in it, ridges stack as flat grey-blue layers.
    "misty_dusk": dict(
        sun_el=4.0, sun_az=252.0, sun_w=2.6, sun_rgb="#ffdab0",
        zenith="#557796", sky="#8ca8b8", horizon="#d6c3a8", glow="#ffc58a", glow_w=1.0, sky_w=0.72,
        fog_rgb="#a3aaac", haze_km=0.18, valley=0.0045, valley_h=24.0, fog_max=0.92,
        exposure=0.05, look="AgX - Base Contrast",
        lift="#f2f8f8", gamma="#fffdf8", gain="#fff8ee", bloom=0.7, vignette=0.2,
    ),
    # Later (the game's 20:00 key reached early): the sun gone below the ridge, the sky
    # a deep blue with a last amber band in the west, the lights doing all the work.
    "blue_hour": dict(
        sun_el=-2.0, sun_az=258.0, sun_w=0.0, sun_rgb="#ffc890",
        zenith="#243a55", sky="#3d5f7e", horizon="#a58b72", glow="#d99a5f", glow_w=0.6, sky_w=0.8,
        fog_rgb="#43586a", haze_km=0.16, valley=0.003, valley_h=30.0, fog_max=0.9,
        exposure=0.7, look="AgX - Medium High Contrast",
        lift="#eef8f8", gamma="#fbfdff", gain="#fff6ec", bloom=0.8, vignette=0.25,
    ),
}


def lin(hexc):
    return config.hex_rgba(hexc)[:3]


# ---------------------------------------------------------------------------
# Drivers: keyed values over flyover_dusk
# ---------------------------------------------------------------------------

def _driver(idb, path, expr, index=-1, extra=()):
    """idb.path[index] = a simple-expression driver over flyover_dusk (`d`) + extras."""
    idb.driver_remove(path, index) if index >= 0 else idb.driver_remove(path)
    fc = idb.driver_add(path, index) if index >= 0 else idb.driver_add(path)
    d = fc.driver
    d.type = "SCRIPTED"
    for name, data_path in (("d", f'["{config.DUSK_PROP}"]'),) + tuple(extra):
        scene_var(d, name, data_path)
    d.expression = expr
    return fc


def scene_var(driver, name, data_path):
    """A driver variable reading the ACTIVE scene's `data_path` (a Context Property: the
    scene being rendered / shown), so every flight's scene drives the one shared world
    with its own flyover_dusk and camera (flight.py)."""
    v = driver.variables.new()
    v.name = name
    v.type = "CONTEXT_PROP"
    v.targets[0].context_property = "ACTIVE_SCENE"
    v.targets[0].data_path = data_path
    return v


def _key(idb, path, a, b, index=-1, fmt="{}"):
    """idb.path[index] = lerp(a, b, flyover_dusk), through `fmt` (e.g. radians(...))."""
    a, b = float(a), float(b)
    _driver(idb, path, fmt.format(f"lerp({a!r}, {b!r}, d)"), index)


def _key_rgb(sock_owner, path, a_hex, b_hex):
    a, b = lin(a_hex), lin(b_hex)
    for i in range(3):
        _key(sock_owner, path, a[i], b[i], i)


# ---------------------------------------------------------------------------
# Sun
# ---------------------------------------------------------------------------

def _sun(col):
    L = scene.tag(bpy.data.lights.new(SUN_NAME, "SUN"))
    L.angle = math.radians(1.6)             # a low sun through haze: soft-edged shadows
    L.use_shadow = True
    # The Phase 6-7 stair-stepped tree shadows were the sun's shadow-map texels (the same
    # at a 1 GB pool and at 4K). A wide filter hides them at no cost; jittered soft
    # shadows also do, but cost ~3x the frame at a low sun (17 s vs 6 s at 1080p).
    L.use_shadow_jitter = False
    L.shadow_filter_radius = 7.0
    L.shadow_maximum_resolution = 0.001
    ob = scene.new_object(SUN_NAME, L, col, kind="sun")
    return ob


def _key_sun(ob, A, D):
    L = ob.data
    # a sun object points down its local -Z: rot x = 90 - elevation, rot z = 180 - azimuth
    _key(ob, "rotation_euler", 90.0 - A["sun_el"], 90.0 - D["sun_el"], 0, "radians({})")
    _key(ob, "rotation_euler", 180.0 - A["sun_az"], 180.0 - D["sun_az"], 2, "radians({})")
    ob.rotation_euler[1] = 0.0
    _key(L, "energy", A["sun_w"], D["sun_w"])
    _key_rgb(L, "color", A["sun_rgb"], D["sun_rgb"])


# ---------------------------------------------------------------------------
# Sky (world)
# ---------------------------------------------------------------------------

class _W:
    def __init__(self, nt):
        self.nt, self.x = nt, 0

    def n(self, kind, **props):
        node = self.nt.nodes.new(kind)
        node.location = (self.x, 0)
        self.x += 180
        for k, v in props.items():
            setattr(node, k, v)
        return node

    def link(self, a, b):
        self.nt.links.new(a, b)

    def math(self, op, a, b=0.0, clamp=False):
        m = self.n("ShaderNodeMath", operation=op, use_clamp=clamp)
        for s, v in zip(m.inputs, (a, b)):
            if isinstance(v, bpy.types.NodeSocket):
                self.link(v, s)
            else:
                s.default_value = v
        return m.outputs[0]

    def mix(self, fac, a, b, blend="MIX"):
        m = self.n("ShaderNodeMix", data_type="RGBA", blend_type=blend, clamp_result=False)
        for s, v in ((m.inputs[0], fac), (m.inputs[6], a), (m.inputs[7], b)):
            if isinstance(v, bpy.types.NodeSocket):
                self.link(v, s)
            else:
                s.default_value = v
        return m.outputs[2]

    def smooth(self, v, lo, hi):
        m = self.n("ShaderNodeMapRange", interpolation_type="SMOOTHSTEP", clamp=True)
        self.link(v, m.inputs["Value"])
        m.inputs["From Min"].default_value = lo
        m.inputs["From Max"].default_value = hi
        return m.outputs["Result"]

    def value(self, v=0.0):
        m = self.n("ShaderNodeValue")
        m.outputs[0].default_value = v
        return m

    def rgb(self, label):
        kind = "CompositorNodeRGB" if self.nt.bl_idname == "CompositorNodeTree" else "ShaderNodeRGB"
        m = self.n(kind, label=label, name=label)
        return m


def _sky(A, D):
    w = scene.tag(bpy.data.worlds.new(WORLD_NAME))
    w.use_nodes = True
    w.sun_threshold = 0.0                 # the sun light is the sun: never extract one from the sky
    nt = w.node_tree
    nt.nodes.clear()
    g = _W(nt)
    dir_ = g.n("ShaderNodeTexCoord").outputs["Generated"]
    nrm = g.n("ShaderNodeVectorMath", operation="NORMALIZE")
    g.link(dir_, nrm.inputs[0])
    xyz = g.n("ShaderNodeSeparateXYZ")
    g.link(nrm.outputs["Vector"], xyz.inputs[0])
    h = xyz.outputs["Z"]
    # the sun's direction (keyed like the sun light)
    sd = g.n("ShaderNodeCombineXYZ", name="SunDir", label="SunDir")
    dot = g.n("ShaderNodeVectorMath", operation="DOT_PRODUCT")
    g.link(nrm.outputs["Vector"], dot.inputs[0])
    g.link(sd.outputs[0], dot.inputs[1])
    s = g.math("MAXIMUM", dot.outputs["Value"], 0.0)
    cols = {k: g.rgb(k) for k in ("horizon", "sky", "zenith", "glow")}
    # elevation gradient: horizon -> sky (by ~15 deg) -> zenith
    c = g.mix(g.smooth(h, -0.02, 0.26), cols["horizon"].outputs[0], cols["sky"].outputs[0])
    c = g.mix(g.smooth(h, 0.18, 0.85), c, cols["zenith"].outputs[0])
    # the sun's glow: broad and low (hugging the horizon), a tighter halo, the disc
    low = g.math("SUBTRACT", 1.0, g.smooth(h, 0.0, 0.45))
    broad = g.math("MULTIPLY", g.math("POWER", s, 3.0), g.math("ADD", g.math("MULTIPLY", low, 0.8), 0.2))
    halo = g.math("MULTIPLY", g.math("POWER", s, 40.0), 1.2)
    disc = g.math("MULTIPLY", g.smooth(s, 0.99985, 0.99995), 40.0)
    gw = g.value(1.0)
    gw.name = gw.label = "GlowW"
    glow = g.math("MULTIPLY", g.math("ADD", g.math("ADD", broad, halo), disc), gw.outputs[0])
    c = g.mix(glow, c, cols["glow"].outputs[0], blend="ADD")
    # below the horizon (seen only past the far terrain's rim): a darker haze
    below = g.smooth(h, -0.02, -0.25)
    c = g.mix(g.math("MULTIPLY", below, 0.6), c, g.mix(0.6, cols["horizon"].outputs[0], (0.02, 0.025, 0.03, 1.0)))
    bg = g.n("ShaderNodeBackground", name="Background")
    g.link(c, bg.inputs["Color"])
    out = g.n("ShaderNodeOutputWorld")
    g.link(bg.outputs[0], out.inputs["Surface"])
    return w


def _sun_dir_expr(axis):
    el = "radians(lerp({a_el!r}, {d_el!r}, d))"
    az = "radians(lerp({a_az!r}, {d_az!r}, d))"
    if axis == 0:
        return f"cos({el}) * sin({az})"
    if axis == 1:
        return f"cos({el}) * cos({az})"
    return f"sin({el})"


def _key_sky(w, A, D):
    nt = w.node_tree
    for k in ("horizon", "sky", "zenith", "glow"):
        _key_rgb(nt.nodes[k].outputs[0], "default_value", A[k], D[k])
    _key(nt.nodes["GlowW"].outputs[0], "default_value", A["glow_w"], D["glow_w"])
    _key(nt.nodes["Background"].inputs["Strength"], "default_value", A["sky_w"], D["sky_w"])
    sd = nt.nodes["SunDir"]
    vals = dict(a_el=float(A["sun_el"]), d_el=float(D["sun_el"]), a_az=float(A["sun_az"]), d_az=float(D["sun_az"]))
    for i in range(3):
        _driver(sd.inputs[i], "default_value", _sun_dir_expr(i).format(**vals))


# ---------------------------------------------------------------------------
# Haze + grade (compositor)
# ---------------------------------------------------------------------------

def _grade():
    ng = scene.tag(bpy.data.node_groups.new(GRADE_NAME, "CompositorNodeTree"))
    ng.interface.new_socket("Image", in_out="OUTPUT", socket_type="NodeSocketColor")
    g = _W(ng)
    rl = g.n("CompositorNodeRLayers")
    img, depth, pos = rl.outputs["Image"], rl.outputs["Depth"], rl.outputs["Position"]
    zp = g.n("ShaderNodeSeparateXYZ")
    g.link(pos, zp.inputs[0])
    vals = {k: g.value() for k in ("CamZ", "Haze", "Valley", "ValleyH", "FogMax")}
    for k, n in vals.items():
        n.name = n.label = k
    H = vals["ValleyH"].outputs[0]
    # optical depth of an exponential layer rho(z) = valley * exp(-z / H) between the
    # camera (zc) and the point (zp): d * valley * exp(-a) * (1 - exp(-x)) / x, x = b - a
    a = g.math("DIVIDE", vals["CamZ"].outputs[0], H)
    b = g.math("DIVIDE", zp.outputs["Z"], H)
    x = g.math("SUBTRACT", b, a)
    small = g.math("LESS_THAN", g.math("ABSOLUTE", x), 1e-3)
    xs = g.math("ADD", x, g.math("MULTIPLY", small, 1e-3))
    frac = g.math("DIVIDE", g.math("SUBTRACT", 1.0, g.math("EXPONENT", g.math("MULTIPLY", xs, -1.0))), xs)
    layer = g.math("MULTIPLY", g.math("EXPONENT", g.math("MULTIPLY", a, -1.0)), frac)
    tau = g.math("MULTIPLY", depth, g.math("ADD", g.math("MULTIPLY", layer, vals["Valley"].outputs[0]),
                                            g.math("DIVIDE", vals["Haze"].outputs[0], 1000.0)))
    # the sky is haze already; the terrain mask grows 2 px so the antialiased ridge edge
    # (a sky / ground blend whose depth reads sky) is hazed too, not a dark fringe
    grow = g.n("CompositorNodeDilateErode")
    g.link(g.math("LESS_THAN", depth, config.CLIP_END_M * 0.9), grow.inputs["Mask"])
    grow.inputs["Size"].default_value = 2
    sky = grow.outputs[0]
    fog = g.math("MULTIPLY", g.math("MULTIPLY", g.math("SUBTRACT", 1.0, g.math("EXPONENT", g.math("MULTIPLY", tau, -1.0))),
                                    sky), vals["FogMax"].outputs[0])
    fog_rgb = g.rgb("FogRGB")
    c = g.mix(fog, img, fog_rgb.outputs[0])
    # bloom: the neon / lamps / windows glow, hue kept
    gl = g.n("CompositorNodeGlare", name="Bloom", label="Bloom")
    g.link(c, gl.inputs["Image"])
    gl.inputs["Type"].default_value = "Bloom"
    gl.inputs["Quality"].default_value = "High"
    gl.inputs["Threshold"].default_value = 1.0
    gl.inputs["Smoothness"].default_value = 0.4
    gl.inputs["Size"].default_value = 0.6
    gl.inputs["Saturation"].default_value = 1.0
    cb = g.n("CompositorNodeColorBalance", name="Grade", label="Grade")
    g.link(gl.outputs["Image"], cb.inputs["Image"])
    # vignette: darken toward the corners (normalised image coordinates: resolution-free)
    ic = g.n("CompositorNodeImageCoordinates")
    g.link(img, ic.inputs["Image"])
    r = g.n("ShaderNodeVectorMath", operation="LENGTH")
    g.link(ic.outputs["Normalized"], r.inputs[0])
    vig = g.n("ShaderNodeMapRange", name="Vignette", label="Vignette", interpolation_type="SMOOTHSTEP",
              clamp=True)
    g.link(r.outputs["Value"], vig.inputs["Value"])
    vig.inputs["From Min"].default_value = 0.55
    vig.inputs["From Max"].default_value = 1.45
    vig.inputs["To Min"].default_value = 1.0
    vg = g.mix(1.0, cb.outputs["Image"], vig.outputs["Result"], blend="MULTIPLY")
    out = g.n("NodeGroupOutput")
    g.link(vg, out.inputs[0])
    return ng


def _key_grade(ng, A, D):
    n = ng.nodes
    # the scene camera's height (its location: the film's cameras are unparented). NOT
    # matrix_world[2][3]: a nested index never resolves in a driver path and read 0.
    _driver(n["CamZ"].outputs[0], "default_value", "z", extra=(("z", "camera.location[2]"),))
    _key(n["Haze"].outputs[0], "default_value", A["haze_km"], D["haze_km"])
    _key(n["Valley"].outputs[0], "default_value", A["valley"], D["valley"])
    _key(n["ValleyH"].outputs[0], "default_value", A["valley_h"], D["valley_h"])
    n["FogMax"].outputs[0].default_value = D["fog_max"]
    _key_rgb(n["FogRGB"].outputs[0], "default_value", A["fog_rgb"], D["fog_rgb"])
    n["Bloom"].inputs["Strength"].default_value = D["bloom"]
    cb = n["Grade"]
    for k in ("lift", "gamma", "gain"):
        sock = next(s for s in cb.inputs if s.name == k.title() and s.type == "RGBA")
        sock.default_value = (*_display(D[k]), 1.0)
    n["Vignette"].inputs["To Max"].default_value = 1.0 - D["vignette"]


def _display(hexc):
    """Lift / gamma / gain take display-ish multipliers: the hex's plain 0..1 values."""
    h = hexc.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


# ---------------------------------------------------------------------------
# Build + finishes
# ---------------------------------------------------------------------------

def build(col, finish=None):
    """The sun (in col), the sky world, the grade tree; view transform; the finish
    (default config.FINISH). flyover_dusk = 1, the canonical 18:00 (keyframes on it, e.g.
    Phase 9's, still win at evaluation)."""
    sc = bpy.context.scene
    sc[config.DUSK_PROP] = 1.0
    sun = _sun(col)
    w = _sky(AFTERNOON, AFTERNOON)
    sc.world = w
    sc.render.engine = "BLENDER_EEVEE"      # the passes the haze reads (the grade's Render Layers)
    vl = bpy.context.view_layer
    vl.use_pass_z = True
    vl.use_pass_position = True
    ng = _grade()
    sc.compositing_node_group = ng
    sc.render.use_compositing = True
    apply_finish(finish or config.FINISH)
    return sun


def apply_finish(name):
    """Switch the look (live-safe): re-keys the sun, sky and haze between AFTERNOON and
    the finish's dusk key, sets its grade and view transform."""
    if name not in FINISHES:
        raise ValueError(f"unknown finish '{name}': {sorted(FINISHES)}")
    sc = bpy.context.scene
    D = dict(FINISHES[name])
    A = dict(AFTERNOON)
    sc[FINISH_PROP] = name
    _key_sun(bpy.data.objects[SUN_NAME], A, D)
    _key_sky(bpy.data.worlds[WORLD_NAME], A, D)
    _key_grade(bpy.data.node_groups[GRADE_NAME], A, D)
    vs = sc.view_settings
    vs.view_transform = "AgX"
    vs.look = D["look"]
    vs.exposure = D["exposure"]
    vs.gamma = 1.0
    for other in bpy.data.scenes:       # the flights' own scenes follow the host's look
        if other is not sc and scene.is_ours(other):
            sync_scene(sc, other)
    return name


SYNC_PROPS = ("flyover_window_glow", "flyover_neon_glow", "flyover_sign_glow", "flyover_street_glow",
              "flyover_pit_glow", FINISH_PROP, "flyover_output")


def sync_scene(host, sc):
    """A flight's scene (a copy of the host) takes the host's look: world, colour
    management, the light families' levels, the passes the grade reads, and its OWN copy
    of the grade (the compositor's Render Layers node names the scene it renders, so a
    shared tree would composite the host). Its flyover_dusk stays its own."""
    sc.world = host.world
    for attr in ("view_transform", "look", "exposure", "gamma"):
        setattr(sc.view_settings, attr, getattr(host.view_settings, attr))
    sc.display_settings.display_device = host.display_settings.display_device
    for k in SYNC_PROPS:
        if k in host:
            sc[k] = host[k]
    for a, b in zip(host.view_layers, sc.view_layers):
        b.use_pass_z, b.use_pass_position = a.use_pass_z, a.use_pass_position
    src = host.compositing_node_group
    if src is None:
        return sc
    name = f"{src.name}_{sc.name}"
    old = bpy.data.node_groups.get(name)
    if old is not None and scene.is_ours(old):
        bpy.data.node_groups.remove(old)
    ng = scene.tag(src.copy())
    ng.name = name
    for node in ng.nodes:
        if node.bl_idname == "CompositorNodeRLayers":
            node.scene = sc
    sc.compositing_node_group = ng
    sc.render.use_compositing = host.render.use_compositing
    return sc


def set_dusk(value):
    """The time of the light (0 = 16:00 .. 1 = the finish's dusk)."""
    sc = bpy.context.scene
    sc[config.DUSK_PROP] = float(value)
    bpy.context.view_layer.update()


def revalidate_drivers():
    """Blender flags a driver that failed to evaluate once as invalid and skips it for the
    rest of the session (only a file load clears the flag). The grade's CamZ reads
    `camera.location[2]` and is made before the build has a scene camera, so a fresh
    build's session kept a stale fog height (its renders differed from the saved .blend's
    by ~38 dB PSNR). Called once the build is complete: clear the flag on every driver,
    re-evaluate, and assert they all evaluate. Returns the number that had been flagged."""
    ids = [*bpy.data.scenes, *bpy.data.objects, *bpy.data.lights, *bpy.data.cameras, *bpy.data.node_groups,
           *(m.node_tree for m in bpy.data.materials if m.node_tree),
           *(w.node_tree for w in bpy.data.worlds if w.node_tree)]
    drivers = [fc.driver for idb in ids if idb.animation_data for fc in idb.animation_data.drivers]
    flagged = [d for d in drivers if not d.is_valid]
    for d in flagged:
        d.is_valid = True
        d.expression = d.expression         # tags the depsgraph relations for a rebuild
    sc = bpy.context.scene
    sc.frame_set(sc.frame_current)
    bad = [d.expression for d in drivers if not d.is_valid]
    if bad:
        raise AssertionError(f"drivers still fail to evaluate: {bad}")
    return len(flagged)

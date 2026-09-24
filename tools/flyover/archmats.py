"""Building materials for archkit keys (EEVEE, procedural, palette-led).

A key is "kind" or "kind:colour" (a palette name, config.ART_COLOURS name or '#rrggbb'),
e.g. "clapboard:cream", "paint:barn-red", "ashlar:stone-light". `Resolver(mats)` turns
keys into materials, built once per build and cached in the shared scene.Materials
(names "Arch_<Kind>_<colour>"; light families "LF_*").

Patterns run on the face's metric UV ("UVm": u along the face, v up it / up the
slope), so clapboard laps, ashlar courses and shingle courses follow sills and eaves;
weathering runs on Object coordinates (z = height above the building's ground): grime
and splash near the ground, moss on the derelict barn. Colours shift between palette
NEIGHBOURS (a darker / lighter blend of the same colour), never off-palette hues.

Light families (config): LF_Window_Glass (amber interior glow = object `glow` x the
scene's `flyover_window_glow`), LF_Neon_* (object `glow` x `flyover_neon_glow`),
LF_SignLamp_* (lit wall bands: object `glow` x `flyover_sign_glow`).
"""

import bpy

import config
import materials
import scene
from materials import G, rgba


def _hex(c):
    return config.colour(c)


def _shade(c, k=0.22, toward="ink-700"):
    return config._mix_hex(_hex(c), _hex(toward), k)


def _mix(a, b, k):
    return config._mix_hex(_hex(a), _hex(b), k)


def _light(c, k=0.18, toward="cream"):
    return config._mix_hex(_hex(c), _hex(toward), k)


def _new(name, base):
    if name in bpy.data.materials:
        raise RuntimeError(f"material name '{name}' collides with an existing material")
    mat = scene.tag(bpy.data.materials.new(name))
    mat.diffuse_color = config.hex_rgba(_hex(base))
    mat["hex"] = _hex(base)
    return mat


def _uv(g):
    return g.uv("UVm")


def _obj(g):
    return g.node("ShaderNodeTexCoord").outputs["Object"]


def _vmul(g, v, s):
    n = g.node("ShaderNodeVectorMath", operation="MULTIPLY")
    g.link(v, n.inputs[0])
    n.inputs[1].default_value = s
    return n.outputs["Vector"]


def _bsdf(g, colour, rough=0.8, height=None, bump=(0.4, 0.01), metallic=0.0, spec=0.25,
          emit=None, emit_strength=None, coat=0.0):
    b = g.node("ShaderNodeBsdfPrincipled")
    g._in(b, "Base Color", rgba(colour) if isinstance(colour, str) else colour)
    g._in(b, "Roughness", rough)
    b.inputs["Metallic"].default_value = metallic
    s = b.inputs.get("Specular IOR Level")
    if s is not None:
        s.default_value = spec
    if coat and b.inputs.get("Coat Weight") is not None:
        b.inputs["Coat Weight"].default_value = coat
    if height is not None:
        bn = g.node("ShaderNodeBump")
        bn.inputs["Strength"].default_value = bump[0]
        bn.inputs["Distance"].default_value = bump[1]
        g.link(height, bn.inputs["Height"])
        g.link(bn.outputs["Normal"], b.inputs["Normal"])
    if emit is not None:
        g._in(b, "Emission Color", emit)
        g._in(b, "Emission Strength", emit_strength)
    o = g.node("ShaderNodeOutputMaterial")
    g.link(b.outputs["BSDF"], o.inputs["Surface"])
    return b


def _grime(g, c, colour, height=0.9, amount=0.55, toward="earth-dark"):
    """Splash and grime rising from the ground, ragged (object z = height above it)."""
    ob = _obj(g)
    _x, _y, z = g.sep(ob)
    n = g.noise(ob, 1.6, 3.0, 0.6)
    zz = g.math("ADD", z, g.math("MULTIPLY", g.math("SUBTRACT", n, 0.5), height * 0.6))
    f = g.math("MULTIPLY", g.math("SUBTRACT", 1.0, g.step(zz, height * 0.5, height * 0.5)), amount)
    return g.mix(f, c, _shade(colour, 0.5, toward))


def _mottle(g, p, a, b, scale, detail=3.0):
    return g.mix(g.step(g.noise(p, scale, detail), 0.5, 0.2), a, b)


def _streaks(g, c, colour, amount=0.18):
    """Faint vertical weather streaks (rain run-off)."""
    ob = _obj(g)
    s = g.noise(_vmul(g, ob, (5.0, 5.0, 0.35)), 1.0, 2.0, 0.5)
    return g.mix(g.math("MULTIPLY", g.step(s, 0.6, 0.08), amount), c, _shade(colour, 0.35, "stone-shade"))


# ---------------------------------------------------------------------------
# Wall finishes
# ---------------------------------------------------------------------------

def clapboard(mat, colour):
    """Lapped clapboard, 11.5 cm exposure: each butt proud with a shadow line under it,
    boards subtly varied, paint chalked and grimed near the ground."""
    g = G(mat)
    uv = _uv(g)
    u, v, _ = g.sep(uv)
    E = 0.115
    f = g.math("FRACT", g.math("DIVIDE", v, E))
    board = g.math("FLOOR", g.math("DIVIDE", v, E))
    c = _mottle(g, _obj(g), rgba(colour), rgba(_shade(colour, 0.08)), 0.6)
    rnd = g.white(g.combine(board, g.math("FLOOR", g.math("DIVIDE", u, 4.3)), 0.0))
    c = g.mix(g.math("MULTIPLY", rnd, 0.35), c, rgba(_shade(colour, 0.12, "stone-pale")))
    lap = g.step(f, 0.86, 0.05)                                  # the shadow under the next butt
    c = g.mix(g.math("MULTIPLY", lap, 0.55), c, rgba(_shade(colour, 0.35, "stone-shade")))
    grain = g.noise(_vmul(g, uv, (1.2, 40.0, 1.0)), 3.0, 2.0, 0.5)
    c = g.mix(g.math("MULTIPLY", g.step(grain, 0.62, 0.06), 0.12), c, rgba(_shade(colour, 0.2)))
    c = _streaks(g, c, colour, 0.12)
    c = _grime(g, c, colour, 0.8, 0.45)
    h = g.math("SUBTRACT", 1.0, f)                               # thick butt -> thin top
    h = g.math("ADD", h, g.math("MULTIPLY", grain, 0.05))
    _bsdf(g, c, 0.62, h, (0.55, 0.012))


def boards(mat, colour, weathered=False, tint=False):
    """Board-and-batten: 25 cm boards, a batten every joint; weathered = unpainted
    grey-brown barn wood (per-board tone, grain, knots, moss at the foot)."""
    g = G(mat)
    uv = _uv(g)
    u, v, _ = g.sep(uv)
    Wb = 0.25
    k = g.math("FLOOR", g.math("DIVIDE", u, Wb))
    fu = g.math("FRACT", g.math("DIVIDE", u, Wb))
    rnd = g.white(g.combine(k, 7.0, 0.0))
    if weathered and tint:            # old boards that still carry a stain / paint colour
        c = g.ramp(rnd, [(0.0, _shade(colour, 0.35)), (0.45, colour), (0.8, _light(colour, 0.12, "earth-light")),
                         (1.0, "stone-shade")])
    elif weathered:
        c = g.ramp(rnd, [(0.0, "stone-shade"), (0.3, "earth-dark"), (0.6, "wood-warm"), (0.85, "earth-mid"),
                         (1.0, "ink-500")])
    else:
        c = g.mix(g.math("MULTIPLY", rnd, 0.4), rgba(colour), rgba(_shade(colour, 0.15)))
    grain = g.noise(_vmul(g, uv, (40.0, 0.9, 1.0)), 2.0, 3.0, 0.6)
    tone = "ink-500" if weathered else _shade(colour, 0.3)
    c = g.mix(g.math("MULTIPLY", g.step(grain, 0.58, 0.08), 0.45 if weathered else 0.15), c, rgba(tone))
    silver = g.noise(_vmul(g, uv, (6.0, 0.4, 1.0)), 1.0, 2.0)
    if weathered:
        c = g.mix(g.math("MULTIPLY", g.step(silver, 0.58, 0.1), 0.08 if tint else 0.2), c, rgba("stone-light"))
        knot = g.step(g.noise(_vmul(g, uv, (4.0, 2.0, 1.0)), 6.0, 1.0), 0.78, 0.02)
        c = g.mix(knot, c, rgba("ink-700"))
    batten = g.math("MAXIMUM", g.step(fu, 0.93, 0.01), g.math("SUBTRACT", 1.0, g.step(fu, 0.07, 0.01)))
    gap = g.math("MAXIMUM", g.step(fu, 0.985, 0.004), g.math("SUBTRACT", 1.0, g.step(fu, 0.015, 0.004)))
    c = g.mix(g.math("MULTIPLY", gap, 0.8), c, rgba("ink-900"))
    if weathered:
        ob = _obj(g)
        _x, _y, z = g.sep(ob)
        moss = g.math("MULTIPLY", g.math("SUBTRACT", 1.0, g.step(z, 0.5, 0.35)),
                      g.step(g.noise(ob, 2.2, 3.0), 0.45, 0.1))
        c = g.mix(g.math("MULTIPLY", moss, 0.45), c, g.mix(g.noise(ob, 9.0), rgba("green-dark"), rgba("green-mid")))
        c = _streaks(g, c, "wood-warm", 0.3)
    c = _grime(g, c, "earth-dark" if weathered else colour, 0.7, 0.4)
    h = g.math("ADD", g.math("MULTIPLY", batten, 0.8), g.math("MULTIPLY", grain, 0.25))
    h = g.math("SUBTRACT", h, gap)
    _bsdf(g, c, 0.9 if weathered else 0.65, h, (0.6, 0.012))


def ashlar(mat, colour):
    """Dressed stone in courses (the town hall's stone-light masonry): per-stone tone
    between palette neighbours, pale speckle, recessed mortar, rain-darkened streaks."""
    g = G(mat)
    uv = _uv(g)
    b = g.node("ShaderNodeTexBrick", offset=0.5, offset_frequency=2, squash=1.0, squash_frequency=2)
    g.link(uv, b.inputs["Vector"])
    b.inputs["Scale"].default_value = 1.0
    b.inputs["Mortar Size"].default_value = 0.012
    b.inputs["Mortar Smooth"].default_value = 0.3
    b.inputs["Bias"].default_value = 0.0
    b.inputs["Brick Width"].default_value = 0.66
    b.inputs["Row Height"].default_value = 0.33
    b.inputs["Color1"].default_value = (0, 0, 0, 1)
    b.inputs["Color2"].default_value = (1, 1, 1, 1)
    b.inputs["Mortar"].default_value = (0.5, 0.5, 0.5, 1)
    tone = g.sep(b.outputs["Color"])[0]
    c = g.ramp(tone, [(0.0, _shade(colour, 0.12, "stone-base")), (0.5, colour), (1.0, _light(colour, 0.25, "stone-pale"))])
    ob = _obj(g)
    speck = g.step(g.noise(ob, 9.0, 2.0), 0.64, 0.03)
    c = g.mix(g.math("MULTIPLY", speck, 0.5), c, rgba("stone-pale"))
    pit = g.noise(ob, 25.0, 2.0, 0.6)
    c = g.mix(g.math("MULTIPLY", g.step(pit, 0.66, 0.05), 0.35), c, rgba("stone-base"))
    mortar = b.outputs["Fac"]
    c = g.mix(mortar, c, rgba(_shade("stone-base", 0.1, "stone-shade")))
    c = _streaks(g, c, colour, 0.22)
    c = _grime(g, c, colour, 1.2, 0.5, "stone-dark")
    h = g.math("SUBTRACT", g.math("MULTIPLY", pit, 0.25), g.math("MULTIPLY", mortar, 1.0))
    _bsdf(g, c, 0.85, h, (0.6, 0.015))


def rubble(mat, colour, moss=False):
    """Fieldstone / rubble: irregular stones in dark mortar (foundations, chimneys)."""
    g = G(mat)
    uv = _uv(g)
    v = g.voronoi(uv, 3.2, rand=0.85)
    e = g.voronoi(uv, 3.2, "DISTANCE_TO_EDGE", rand=0.85)
    tone = g.sep(v.outputs["Color"])[0]
    c = g.ramp(tone, [(0.1, "stone-dark"), (0.35, "stone-shade"), (0.65, colour), (0.95, "stone-light")])
    ob = _obj(g)
    c = g.mix(g.math("MULTIPLY", g.step(g.noise(ob, 12.0, 2.0), 0.6, 0.05), 0.3), c, rgba("stone-pale"))
    joint = g.math("SUBTRACT", 1.0, g.step(e.outputs["Distance"], 0.05, 0.02))
    c = g.mix(joint, c, rgba("ink-500"))
    if moss:
        m = g.math("MULTIPLY", g.step(g.noise(ob, 1.4, 3.0), 0.5, 0.1), g.math("ADD", joint, 0.35))
        c = g.mix(g.math("MINIMUM", m, 0.8), c, g.mix(g.noise(ob, 8.0), rgba("green-dark"), rgba("green-mid")))
    c = _grime(g, c, colour, 0.5, 0.35, "earth-dark")
    dome = g.math("SQRT", g.math("MINIMUM", g.math("MULTIPLY", e.outputs["Distance"], 5.0), 1.0))
    h = g.math("ADD", dome, g.math("MULTIPLY", g.noise(ob, 18.0), 0.2))
    _bsdf(g, c, 0.9, h, (0.8, 0.03))


def enamel(mat, colour):
    """Porcelain-enamel steel panels (the motel): cream, 1.2 x 0.6 m panels with dark
    seams, a satin sheen, chalky speckle, grime at the kick."""
    g = G(mat)
    uv = _uv(g)
    u, v, _ = g.sep(uv)
    fu = g.math("PINGPONG", u, 0.61)
    fv = g.math("PINGPONG", v, 0.305)
    seam = g.math("MAXIMUM", g.math("LESS_THAN", fu, 0.004), g.math("LESS_THAN", fv, 0.004))
    c = rgba(colour)
    ob = _obj(g)
    c = g.mix(g.math("MULTIPLY", g.step(g.noise(ob, 30.0, 1.0), 0.66, 0.02), 0.6), c, rgba("stone-pale"))
    panel = g.white(g.combine(g.math("FLOOR", g.math("DIVIDE", u, 1.22)), g.math("FLOOR", g.math("DIVIDE", v, 0.61)), 0.0))
    c = g.mix(g.math("MULTIPLY", panel, 0.3), c, rgba(_shade(colour, 0.1, "stone-pale")))
    c = g.mix(g.math("MULTIPLY", seam, 0.7), c, rgba(_shade(colour, 0.5, "stone-shade")))
    c = _streaks(g, c, colour, 0.15)
    c = _grime(g, c, colour, 0.6, 0.4, "stone-dark")
    h = g.math("MULTIPLY", seam, -1.0)
    _bsdf(g, c, 0.32, h, (0.3, 0.004), spec=0.5)


def paint(mat, colour, rough=0.55):
    """Painted wood / metal: the colour, a darker neighbour in the brush grain and wear."""
    g = G(mat)
    uv = _uv(g)
    ob = _obj(g)
    c = _mottle(g, ob, rgba(colour), rgba(_shade(colour, 0.1)), 1.3)
    grain = g.noise(_vmul(g, uv, (1.0, 30.0, 1.0)), 2.0, 2.0)
    c = g.mix(g.math("MULTIPLY", g.step(grain, 0.6, 0.06), 0.15), c, rgba(_shade(colour, 0.25)))
    wear = g.step(g.noise(ob, 4.0, 3.0), 0.7, 0.04)
    c = g.mix(g.math("MULTIPLY", wear, 0.25), c, rgba(_light(colour, 0.25, "stone-pale")))
    _bsdf(g, c, rough, g.math("MULTIPLY", grain, 0.3), (0.2, 0.004))


def trim(mat, colour):
    paint(mat, colour, 0.5)


def stone_trim(mat, colour):
    """Dressed stone: sills, lintels, cornices, steps."""
    g = G(mat)
    ob = _obj(g)
    c = _mottle(g, ob, rgba(colour), rgba(_shade(colour, 0.12, "stone-base")), 2.0)
    pit = g.noise(ob, 30.0, 2.0, 0.6)
    c = g.mix(g.math("MULTIPLY", g.step(pit, 0.64, 0.05), 0.3), c, rgba("stone-base"))
    c = _grime(g, c, colour, 0.4, 0.4, "stone-dark")
    _bsdf(g, c, 0.8, pit, (0.3, 0.006))


def metal(mat, colour):
    g = G(mat)
    ob = _obj(g)
    c = _mottle(g, ob, rgba(colour), rgba(_shade(colour, 0.2)), 2.5)
    c = _streaks(g, c, colour, 0.25)
    _bsdf(g, c, 0.45, None, metallic=0.55, spec=0.5)


def deck(mat, colour):
    """Porch deck boards (14 cm, along u)."""
    g = G(mat)
    uv = _uv(g)
    u, v, _ = g.sep(uv)
    k = g.math("FLOOR", g.math("DIVIDE", v, 0.14))
    c = g.mix(g.math("MULTIPLY", g.white(g.combine(k, 3.0, 0.0)), 0.5), rgba(colour), rgba(_shade(colour, 0.25)))
    joint = g.math("LESS_THAN", g.math("FRACT", g.math("DIVIDE", v, 0.14)), 0.05)
    c = g.mix(g.math("MULTIPLY", joint, 0.8), c, rgba("ink-700"))
    grain = g.noise(_vmul(g, uv, (1.0, 25.0, 1.0)), 2.0, 2.0)
    c = g.mix(g.math("MULTIPLY", g.step(grain, 0.6, 0.06), 0.2), c, rgba(_shade(colour, 0.3)))
    _bsdf(g, c, 0.8, g.math("SUBTRACT", g.math("MULTIPLY", grain, 0.2), joint), (0.4, 0.006))


# ---------------------------------------------------------------------------
# Roofs
# ---------------------------------------------------------------------------

def _courses(g, uv, width, row, colours, gap_colour, offset=0.5):
    b = g.node("ShaderNodeTexBrick", offset=offset, offset_frequency=2, squash=1.0, squash_frequency=2)
    g.link(uv, b.inputs["Vector"])
    b.inputs["Scale"].default_value = 1.0
    b.inputs["Mortar Size"].default_value = 0.006
    b.inputs["Mortar Smooth"].default_value = 0.0
    b.inputs["Brick Width"].default_value = width
    b.inputs["Row Height"].default_value = row
    b.inputs["Color1"].default_value = (0, 0, 0, 1)
    b.inputs["Color2"].default_value = (1, 1, 1, 1)
    tone = g.sep(b.outputs["Color"])[0]
    c = g.ramp(tone, colours)
    c = g.mix(b.outputs["Fac"], c, rgba(gap_colour))
    _u, v, _ = g.sep(uv)
    f = g.math("FRACT", g.math("DIVIDE", v, row))
    return c, f, b.outputs["Fac"], tone


def slate(mat, colour="stone-shade"):
    """Slate: 30 x 16 cm exposure courses, dark blue-grey neighbours, butt shadow lines,
    faint lichen."""
    g = G(mat)
    uv = _uv(g)
    c, f, gap, tone = _courses(g, uv, 0.3, 0.16, [(0.0, "stone-dark"), (0.5, colour), (1.0, "stone-base")],
                               "ink-700")
    c = g.mix(g.math("MULTIPLY", g.math("SUBTRACT", 1.0, g.step(f, 0.12, 0.05)), 0.5), c, rgba("ink-700"))
    ob = _obj(g)
    lichen = g.step(g.noise(ob, 3.5, 2.0), 0.7, 0.03)
    c = g.mix(g.math("MULTIPLY", lichen, 0.35), c, rgba("stone-light"))
    h = g.math("SUBTRACT", g.math("SUBTRACT", 1.0, f), gap)
    _bsdf(g, c, 0.55, h, (0.5, 0.01), spec=0.4)


def shake(mat, colour="wood-warm", rot=False):
    """Cedar shakes: 20 cm rows of split shingles, weathered brown neighbours, grain and
    butt shadows; rot = the derelict barn's (black rot, moss, bleached patches)."""
    g = G(mat)
    uv = _uv(g)
    stops = ([(0.0, "earth-dark"), (0.4, "wood-warm"), (0.75, "earth-mid"), (1.0, "earth-base")] if not rot else
             [(0.0, "ink-700"), (0.35, "earth-dark"), (0.7, "wood-warm"), (1.0, "stone-shade")])
    c, f, gap, tone = _courses(g, uv, 0.2, 0.2, stops, "ink-900")
    grain = g.noise(_vmul(g, uv, (30.0, 1.2, 1.0)), 2.0, 3.0, 0.6)
    c = g.mix(g.math("MULTIPLY", g.step(grain, 0.56, 0.08), 0.4), c, rgba("earth-dark"))
    c = g.mix(g.math("MULTIPLY", g.math("SUBTRACT", 1.0, g.step(f, 0.14, 0.05)), 0.55), c, rgba("ink-900"))
    ob = _obj(g)
    if rot:
        speck = g.step(g.noise(ob, 14.0, 2.0), 0.62, 0.02)
        c = g.mix(g.math("MULTIPLY", speck, 0.8), c, rgba("ink-900"))
        moss = g.step(g.noise(ob, 0.9, 3.0, 0.6), 0.6, 0.1)
        mtone = g.mix(g.noise(ob, 6.0), rgba("green-dark"), rgba("green-mid"))
        c = g.mix(g.math("MULTIPLY", moss, 0.55), c, mtone)
        bleach = g.step(g.noise(ob, 0.5, 2.0), 0.62, 0.1)
        c = g.mix(g.math("MULTIPLY", bleach, 0.4), c, rgba("stone-base"))
    else:
        c = g.mix(g.math("MULTIPLY", g.step(g.noise(ob, 0.7, 2.0), 0.6, 0.1), 0.3), c, rgba("stone-shade"))
    h = g.math("ADD", g.math("SUBTRACT", 1.0, f), g.math("MULTIPLY", grain, 0.3))
    h = g.math("SUBTRACT", h, gap)
    _bsdf(g, c, 0.85, h, (0.7, 0.015))


def gravel_roof(mat, colour="stone-shade"):
    g = G(mat)
    p = _obj(g)
    v = g.voronoi(p, 40.0, rand=0.9)
    c = g.ramp(g.sep(v.outputs["Color"])[0], [(0.1, "stone-dark"), (0.5, colour), (0.9, "stone-base")])
    c = g.mix(g.math("MULTIPLY", g.step(g.noise(p, 0.8, 2.0), 0.55, 0.1), 0.35), c, rgba("stone-dark"))
    h = g.math("SUBTRACT", 1.0, g.math("MULTIPLY", v.outputs["Distance"], 1.5))
    _bsdf(g, c, 0.95, h, (0.5, 0.01))


def block(mat, colour):
    """Painted concrete block (40 x 20 cm, running bond): the paint over the block's
    open texture, struck joints, chalking, rust-free streaks, grime at the foot."""
    g = G(mat)
    uv = _uv(g)
    b = g.node("ShaderNodeTexBrick", offset=0.5, offset_frequency=2, squash=1.0, squash_frequency=2)
    g.link(uv, b.inputs["Vector"])
    b.inputs["Scale"].default_value = 1.0
    b.inputs["Mortar Size"].default_value = 0.008
    b.inputs["Mortar Smooth"].default_value = 0.4
    b.inputs["Brick Width"].default_value = 0.4
    b.inputs["Row Height"].default_value = 0.2
    b.inputs["Color1"].default_value = (0, 0, 0, 1)
    b.inputs["Color2"].default_value = (1, 1, 1, 1)
    tone = g.sep(b.outputs["Color"])[0]
    c = g.mix(g.math("MULTIPLY", tone, 0.35), rgba(colour), rgba(_shade(colour, 0.1)))
    ob = _obj(g)
    pore = g.noise(ob, 45.0, 2.0, 0.6)
    c = g.mix(g.math("MULTIPLY", g.step(pore, 0.64, 0.04), 0.3), c, rgba(_shade(colour, 0.3)))
    c = g.mix(g.math("MULTIPLY", g.step(g.noise(ob, 1.1, 3.0), 0.62, 0.08), 0.25), c,
              rgba(_light(colour, 0.2, "stone-pale")))                    # chalked, sun-faded
    mortar = b.outputs["Fac"]
    c = g.mix(g.math("MULTIPLY", mortar, 0.45), c, rgba(_shade(colour, 0.3, "stone-shade")))
    c = _streaks(g, c, colour, 0.25)
    c = _grime(g, c, colour, 0.9, 0.5, "stone-dark")
    h = g.math("SUBTRACT", g.math("MULTIPLY", pore, 0.2), mortar)
    _bsdf(g, c, 0.8, h, (0.45, 0.01))


def stucco(mat, colour):
    """Painted cement render (smooth-troweled): soft patches, hairline cracks, streaks."""
    g = G(mat)
    ob = _obj(g)
    c = _mottle(g, ob, rgba(colour), rgba(_shade(colour, 0.1)), 0.9)
    c = g.mix(g.math("MULTIPLY", g.step(g.noise(ob, 0.6, 3.0), 0.6, 0.1), 0.3), c,
              rgba(_light(colour, 0.2, "stone-pale")))
    crack = g.math("SUBTRACT", 1.0, g.step(g.voronoi(ob, 1.4, "DISTANCE_TO_EDGE").outputs["Distance"], 0.012, 0.004))
    crack = g.math("MULTIPLY", crack, g.step(g.noise(ob, 0.8, 2.0), 0.6, 0.05))
    c = g.mix(g.math("MULTIPLY", crack, 0.6), c, rgba(_shade(colour, 0.5, "stone-dark")))
    c = _streaks(g, c, colour, 0.3)
    c = _grime(g, c, colour, 1.0, 0.5, "stone-dark")
    h = g.math("SUBTRACT", g.math("MULTIPLY", g.noise(ob, 30.0, 3.0), 0.2), crack)
    _bsdf(g, c, 0.85, h, (0.35, 0.008))


def plywood(mat, colour, weathered=True):
    """Plywood sheets (1.22 x 2.44 m) nailed up: faint face grain, sheet seams, nail
    lines, paint (the colour) peeling to grey veneer, water stain rising from the foot."""
    g = G(mat)
    uv = _uv(g)
    u, v, _ = g.sep(uv)
    fu = g.math("FRACT", g.math("DIVIDE", u, 1.22))
    fv = g.math("FRACT", g.math("DIVIDE", v, 2.44))
    seam = g.math("MAXIMUM", g.math("MAXIMUM", g.step(fu, 0.994, 0.002), g.math("SUBTRACT", 1.0, g.step(fu, 0.006, 0.002))),
                  g.math("MAXIMUM", g.step(fv, 0.997, 0.001), g.math("SUBTRACT", 1.0, g.step(fv, 0.003, 0.001))))
    sheet = g.white(g.combine(g.math("FLOOR", g.math("DIVIDE", u, 1.22)), g.math("FLOOR", g.math("DIVIDE", v, 2.44)), 0.0))
    c = g.mix(g.math("MULTIPLY", sheet, 0.4), rgba(colour), rgba(_shade(colour, 0.18)))
    grain = g.noise(_vmul(g, uv, (0.8, 18.0, 1.0)), 2.0, 3.0, 0.6)
    c = g.mix(g.math("MULTIPLY", g.step(grain, 0.58, 0.07), 0.25), c, rgba(_shade(colour, 0.3, "earth-dark")))
    ob = _obj(g)
    if weathered:
        peel = g.step(g.noise(ob, 1.8, 4.0, 0.65), 0.62, 0.02)
        c = g.mix(g.math("MULTIPLY", peel, 0.85), c, g.mix(grain, rgba("stone-light"), rgba("stone-shade")))
        c = _streaks(g, c, colour, 0.35)
    c = g.mix(g.math("MULTIPLY", seam, 0.8), c, rgba("ink-700"))
    c = _grime(g, c, colour, 0.8, 0.55, "earth-dark")
    h = g.math("SUBTRACT", g.math("MULTIPLY", grain, 0.15), seam)
    _bsdf(g, c, 0.85, h, (0.35, 0.006))


def corrugated(mat, colour, rusty=True):
    """Corrugated steel roofing (7.6 cm pitch along u): galvanised grey, rust blooming
    along the laps and low edge in palette browns (earth-mid / wood-warm)."""
    g = G(mat)
    uv = _uv(g)
    u, v, _ = g.sep(uv)
    wave = g.math("SINE", g.math("MULTIPLY", u, 2 * 3.14159 / 0.076))
    ob = _obj(g)
    c = _mottle(g, ob, rgba(colour), rgba(_shade(colour, 0.15)), 1.5)
    sheet = g.white(g.combine(g.math("FLOOR", g.math("DIVIDE", u, 0.8)), 3.0, 0.0))
    c = g.mix(g.math("MULTIPLY", sheet, 0.35), c, rgba(_light(colour, 0.15, "stone-light")))
    if rusty:
        r = g.noise(ob, 1.6, 4.0, 0.6)
        lap = g.math("SUBTRACT", 1.0, g.step(g.math("FRACT", g.math("DIVIDE", v, 2.4)), 0.12, 0.1))
        rust = g.math("MINIMUM", g.math("ADD", g.step(r, 0.58, 0.06), g.math("MULTIPLY", lap, 0.5)), 1.0)
        rc = g.mix(g.noise(ob, 12.0), rgba("earth-mid"), rgba("wood-warm"))
        c = g.mix(g.math("MULTIPLY", rust, 0.8), c, rc)
    c = _streaks(g, c, colour, 0.3)
    _bsdf(g, c, 0.55, wave, (0.9, 0.02), metallic=0.35, spec=0.4)


def shingle(mat, colour="stone-dark"):
    """Asphalt three-tab shingles (33 x 14 cm exposure): granule speckle, tab slots,
    butt shadows, a few curled / lost tabs, algae streaks on the worn ones."""
    g = G(mat)
    uv = _uv(g)
    c, f, gap, tone = _courses(g, uv, 0.33, 0.14, [(0.0, _shade(colour, 0.25)), (0.5, colour),
                                                   (1.0, _light(colour, 0.12, "stone-shade"))], "ink-900")
    c = g.mix(g.math("MULTIPLY", g.math("SUBTRACT", 1.0, g.step(f, 0.1, 0.04)), 0.6), c, rgba("ink-900"))
    ob = _obj(g)
    gran = g.noise(ob, 70.0, 1.0)
    c = g.mix(g.math("MULTIPLY", g.step(gran, 0.6, 0.05), 0.35), c, rgba("stone-base"))
    lost = g.step(g.white(g.combine(g.math("FLOOR", g.math("DIVIDE", g.sep(uv)[0], 0.33)),
                                    g.math("FLOOR", g.math("DIVIDE", g.sep(uv)[1], 0.14)), 1.0)), 0.985)
    c = g.mix(g.math("MULTIPLY", lost, 0.9), c, rgba("ink-700"))
    streak = g.step(g.noise(_vmul(g, uv, (3.0, 0.3, 1.0)), 1.0, 2.0), 0.62, 0.06)
    c = g.mix(g.math("MULTIPLY", streak, 0.35), c, rgba("ink-700"))
    h = g.math("SUBTRACT", g.math("SUBTRACT", 1.0, f), g.math("ADD", gap, lost))
    _bsdf(g, c, 0.9, h, (0.5, 0.01))


def roll_roofing(mat, colour="ink-500"):
    """Tar paper / roll roofing: 0.9 m strips down the slope with lapped seams, patches,
    tears showing the boards, moss."""
    g = G(mat)
    uv = _uv(g)
    u, v, _ = g.sep(uv)
    f = g.math("FRACT", g.math("DIVIDE", v, 0.86))
    ob = _obj(g)
    c = _mottle(g, ob, rgba(colour), rgba(_shade(colour, 0.3)), 1.2)
    c = g.mix(g.math("MULTIPLY", g.step(g.noise(ob, 0.9, 2.0), 0.6, 0.05), 0.5), c, rgba("stone-shade"))
    lap = g.math("SUBTRACT", 1.0, g.step(f, 0.05, 0.02))
    c = g.mix(g.math("MULTIPLY", lap, 0.7), c, rgba("ink-900"))
    tear = g.step(g.noise(ob, 2.4, 3.0, 0.7), 0.7, 0.02)
    c = g.mix(tear, c, g.mix(g.noise(ob, 20.0), rgba("wood-warm"), rgba("earth-dark")))
    moss = g.step(g.noise(ob, 1.7, 3.0), 0.62, 0.08)
    c = g.mix(g.math("MULTIPLY", moss, 0.5), c, g.mix(g.noise(ob, 7.0), rgba("green-dark"), rgba("green-mid")))
    h = g.math("ADD", g.math("MULTIPLY", g.noise(ob, 6.0, 3.0), 0.3), g.math("SUBTRACT", lap, tear))
    _bsdf(g, c, 0.9, h, (0.5, 0.01))


def slate_ruin(mat, colour="stone-dark"):
    """The mansion's slate: darker, heavy moss and lichen in the courses, a slate or two
    slipped (lighter lost patches showing the dark lath behind)."""
    g = G(mat)
    uv = _uv(g)
    c, f, gap, tone = _courses(g, uv, 0.3, 0.16, [(0.0, _mix("ink-900", colour, 0.5)), (0.45, colour),
                                                  (0.8, _mix(colour, "stone-shade", 0.35)),
                                                  (1.0, _mix(colour, "water-deep", 0.3))], "ink-900")
    c = g.mix(g.math("MULTIPLY", g.math("SUBTRACT", 1.0, g.step(f, 0.12, 0.05)), 0.6), c, rgba("ink-900"))
    ob = _obj(g)
    lost = g.step(g.white(g.combine(g.math("FLOOR", g.math("DIVIDE", g.sep(uv)[0], 0.3)),
                                    g.math("FLOOR", g.math("DIVIDE", g.sep(uv)[1], 0.16)), 2.0)), 0.95)
    c = g.mix(lost, c, rgba("ink-900"))
    lichen = g.step(g.noise(ob, 4.0, 2.0), 0.7, 0.03)
    c = g.mix(g.math("MULTIPLY", lichen, 0.25), c, rgba("stone-light"))
    moss = g.math("MULTIPLY", g.step(g.noise(ob, 0.35, 3.0, 0.6), 0.56, 0.1), g.math("ADD", gap, 0.4))
    c = g.mix(g.math("MINIMUM", g.math("MULTIPLY", moss, 0.7), 0.6), c,
              g.mix(g.noise(ob, 5.0), rgba("green-dark"), rgba(_mix("green-dark", "green-mid", 0.4))))
    h = g.math("SUBTRACT", g.math("SUBTRACT", 1.0, f), g.math("ADD", gap, lost))
    _bsdf(g, c, 0.7, h, (0.6, 0.012))


def ivy(mat, colour="green-dark"):
    """Ivy / creeper mats on the mansion (leaves: palette greens, turning in patches to the
    muted autumn rust / copper tints)."""
    g = G(mat)
    ob = _obj(g)
    v = g.voronoi(_vmul(g, ob, (1.0, 1.0, 1.0)), 14.0, rand=1.0)
    leaf = g.sep(v.outputs["Color"])[0]
    c = g.ramp(leaf, [(0.0, "ink-700"), (0.35, colour), (0.75, _mix(colour, "green-mid", 0.5)), (1.0, "green-mid")])
    turn = g.step(g.noise(ob, 0.3, 2.0), 0.64, 0.08)
    c = g.mix(g.math("MULTIPLY", turn, 0.55), c, g.ramp(leaf, [(0.2, _mix(config.TINTS["rust"], "ink-700", 0.3)),
                                                                (0.8, config.TINTS["rust"])]))
    e = v.outputs["Distance"]
    c = g.mix(g.math("MULTIPLY", g.step(e, 0.42, 0.05), 0.7), c, rgba("ink-900"))
    _bsdf(g, c, 0.75, g.math("SUBTRACT", 1.0, e), (0.8, 0.04))


def interior_dark(mat, colour="ink-900"):
    g = G(mat)
    ob = _obj(g)
    c = g.mix(g.math("MULTIPLY", g.noise(ob, 1.0, 3.0), 0.35), rgba(colour), rgba("earth-dark"))
    _bsdf(g, c, 1.0, spec=0.05)


# ---------------------------------------------------------------------------
# Light families
# ---------------------------------------------------------------------------

def _prop(g, name):
    n = g.node("ShaderNodeAttribute", attribute_type="VIEW_LAYER", attribute_name=name)
    return n.outputs["Fac"]


def _obj_attr(g, name):
    n = g.node("ShaderNodeAttribute", attribute_type="OBJECT", attribute_name=name)
    return n.outputs["Fac"]


def window_glass(mat, colour="water-deep"):
    """LF_Window_Glass: dark reflective glass; lit (object `glow` x the scene's
    flyover_window_glow) it shows a warm amber interior - a lamp-lit gradient with soft
    shapes (curtains, furniture) in the palette's ambers."""
    g = G(mat)
    uv = _uv(g)
    ob = g.node("ShaderNodeTexCoord").outputs["Object"]
    lit = g.math("MULTIPLY", _obj_attr(g, "glow"), _prop(g, config.WINDOW_GLOW_PROP))
    _u, v, _ = g.sep(uv)
    blob = g.noise(uv, 1.3, 2.0, 0.5)
    warm = g.ramp(g.math("ADD", g.math("MULTIPLY", blob, 0.6), g.math("MULTIPLY", g.math("FRACT", g.math("DIVIDE", v, 2.0)), 0.3)),
                  [(0.2, config.TINTS["deep-amber"]), (0.55, "lantern"), (0.85, config._mix_hex(_hex("lantern"), _hex("cream"), 0.45))])
    shape = g.step(g.noise(_vmul(g, ob, (2.5, 2.5, 0.7)), 1.0, 2.0), 0.62, 0.05)   # curtain folds / furniture
    warm = g.mix(g.math("MULTIPLY", shape, 0.55), warm, rgba(config.TINTS["deep-amber"]))
    refl = g.mix(g.step(g.noise(ob, 0.35, 2.0), 0.5, 0.2), rgba("water-deep"), rgba("stone-dark"))
    base = g.mix(g.math("MINIMUM", g.math("MULTIPLY", lit, 1.5), 1.0), refl, warm)
    rough = g.fmix(g.math("MINIMUM", lit, 1.0), 0.06, 0.35)
    _bsdf(g, base, rough, spec=0.6, emit=warm, emit_strength=g.math("MULTIPLY", lit, config.WINDOW_EMIT))


def neon(mat, colour, emit_colour=None):
    """LF_Neon_*: a sign / tube colour by day; lit (object `glow` x the scene's
    flyover_neon_glow) it glows in its own colour (or `emit_colour`)."""
    g = G(mat)
    lit = g.math("MULTIPLY", _obj_attr(g, "glow"), _prop(g, config.NEON_GLOW_PROP))
    e = rgba(emit_colour or colour)
    _bsdf(g, rgba(colour), 0.35, spec=0.5, emit=e, emit_strength=g.math("MULTIPLY", lit, config.NEON_EMIT))


def sign_lamp(mat, colour, emit_colour=None):
    """LF_SignLamp_*: a lit wall band (WallBandSign: letters cream by day, lantern after
    dusk, lit from below). Its letters and the lamp lenses of the trough under the band
    glow with object `glow` x the scene's flyover_sign_glow, a family of its own (not
    window amber, not neon)."""
    g = G(mat)
    lit = g.math("MULTIPLY", _obj_attr(g, "glow"), _prop(g, config.SIGN_GLOW_PROP))
    e = rgba(emit_colour or colour)
    base = g.mix(g.math("MINIMUM", lit, 1.0), rgba(colour), e)
    _bsdf(g, base, 0.5, spec=0.3, emit=e, emit_strength=g.math("MULTIPLY", lit, config.SIGN_EMIT))


# ---------------------------------------------------------------------------
# Key -> material
# ---------------------------------------------------------------------------

KINDS = {
    "clapboard": (clapboard, "cream"), "boards": (boards, "wood-warm"),
    "boards_weathered": (lambda m, c: boards(m, c, True), "wood-warm"),
    "boards_old": (lambda m, c: boards(m, c, True, True), "wood-warm"),
    "ashlar": (ashlar, "stone-light"), "rubble": (rubble, "stone-base"),
    "rubble_moss": (lambda m, c: rubble(m, c, True), "stone-base"),
    "enamel": (enamel, "cream"), "paint": (paint, "cream"), "trim": (trim, "cream"),
    "trim_dark": (trim, "ink-700"), "stone_trim": (stone_trim, "stone-pale"), "metal": (metal, "stone-shade"),
    "deck": (deck, "earth-base"), "slate": (slate, "stone-shade"), "shake": (shake, "wood-warm"),
    "shake_rot": (lambda m, c: shake(m, c, True), "wood-warm"), "gravel_roof": (gravel_roof, "stone-shade"),
    "interior_dark": (interior_dark, "ink-900"), "glass": (window_glass, "water-deep"),
    "neon": (neon, "aqua"), "signlamp": (sign_lamp, "cream"),
    "block": (block, "stone-light"), "stucco": (stucco, "stone-light"), "plywood": (plywood, "earth-base"),
    "plywood_fresh": (lambda m, c: plywood(m, c, False), "earth-base"),
    "corrugated": (corrugated, "stone-base"), "corrugated_clean": (lambda m, c: corrugated(m, c, False), "stone-base"),
    "shingle": (shingle, "stone-dark"), "roll_roofing": (roll_roofing, "ink-500"),
    "slate_ruin": (slate_ruin, "stone-dark"), "ivy": (ivy, "green-dark"),
}
LIGHT_NAMES = {"glass": "LF_Window_Glass"}


class Resolver:
    """key -> material, built on first use and kept in the shared cache."""

    def __init__(self, mats):
        self.mats = mats

    def __call__(self, key):
        kind, _, colour = key.partition(":")
        fn, default = KINDS[kind]
        colour = colour or default
        extra = None
        if kind in ("neon", "signlamp") and "/" in colour:     # "neon:aqua/lantern": body / glow colours
            colour, extra = colour.split("/")
        if kind in LIGHT_NAMES:
            name = LIGHT_NAMES[kind]
        elif kind == "neon":
            name = f"LF_Neon_{colour.lstrip('#')}" + (f"_{extra.lstrip('#')}" if extra else "")
        elif kind == "signlamp":
            name = f"LF_SignLamp_{colour.lstrip('#')}" + (f"_{extra.lstrip('#')}" if extra else "")
        else:
            name = f"Arch_{kind.title().replace('_', '')}_{colour.lstrip('#')}"
        try:
            return self.mats.get_cached(name)
        except KeyError:
            pass
        mat = _new(name, colour)
        if kind == "neon":
            neon(mat, colour, extra)
        elif kind == "signlamp":
            sign_lamp(mat, colour, extra)
        else:
            fn(mat, colour)
        return self.mats.put(name, mat)

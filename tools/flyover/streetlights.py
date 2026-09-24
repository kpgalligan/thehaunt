"""The street lights: aluminium cobra heads on tapered poles (StreetLight.cs + the motel
handoff: "nothing cast iron", "the town electrified before it had taste"), at the
dump's street lights.

Each: a concrete footing, a ribbed collar (ink), the tapered aluminium pole (stone-shade
with its highlight), a davit mast arm rising in a curve to the luminaire, the cobra
head's pale shell (stone-pale), dark underside and the prismatic refractor lens
(LF_Street_*: object `glow` 1 lit / 0 dead x the scene's flyover_street_glow), and under
a lit head the game's pale cone (Phase 8: LF_StreetCone, `light_cone`).

The arm: a light in the verge (by the kerb) reaches over the road, square to it, as a
cobra head does (the game's cone and pool land ON the road); the plaza's two reach the
way the dump faces them (E / W, toward each other across the plaza). The dead head
(dump lit false) is dark forever: no spot, glow 0.

Every LIT head carries a real spot light (Flyover_Lights, `Light_Street_*`) at its lens,
pointing down, energy = config.STREET_LIGHT_W x flyover_street_glow (a driver), colour
the mercury vapour; Phase 8 tunes it.
"""

import math

import archkit as ak
import config
import propkit as pk
from archkit import T, Rz

VERGE_M = 4.0           # a light within this of the road's edge reaches over it


def cobra_head(lit):
    """The whole light in local coordinates: base at the origin, the arm reaching +X."""
    p = ak.Part()
    H, A = config.STREET_POLE_M, config.STREET_ARM_M
    # footing, collar, pole (tapered 0.11 -> 0.065 m radius)
    p.cylinder((0, 0, -0.4), (0, 0, 0.12), 0.26, "stone_trim:stone-pale", n=14)
    p.cylinder((0, 0, 0.12), (0, 0, 0.55), 0.15, "metal:ink-900", n=12, r1=0.13)
    for z in (0.2, 0.32, 0.44):
        p.cylinder((0, 0, z), (0, 0, z + 0.035), 0.155, "metal:ink-700", n=12)
    p.cylinder((0, 0, 0.55), (0, 0, H), 0.11, "metal:stone-shade", n=14, r1=0.065)
    p.box(-0.06, -0.115, 0.9, 0.06, -0.1, 1.2, "metal:stone-light")            # the hand-hole cover
    # the davit arm: up and over in a smooth curve, then out level to the head
    pts = []
    for k in range(9):
        t = k / 8
        a = t * math.pi / 2
        pts.append((0.9 * (1 - math.cos(a)), 0.0, H - 0.3 + 0.9 * math.sin(a) * 0.75))
    zt = pts[-1][2]
    pts += [(0.9 + (A - 0.9 - 0.35) * t, 0.0, zt - 0.08 * t) for t in (0.5, 1.0)]
    pk.arc_tube(p, pts, 0.045, "metal:stone-shade", n=10)
    p.cylinder((0, 0, H - 0.4), (0, 0, H + 0.02), 0.07, "metal:stone-shade", n=12)
    p.cylinder((0, 0, H + 0.02), (0, 0, H + 0.1), 0.04, "metal:stone-dark", n=10)
    # the head: a cobra's hood, long and low, lens underneath
    hx0 = A - 0.45
    zh = zt - 0.08
    sec = []
    for x, hw, top, bot in ((hx0 - 0.05, 0.07, 0.07, -0.05), (hx0 + 0.1, 0.17, 0.11, -0.1),
                            (hx0 + 0.45, 0.22, 0.13, -0.13), (hx0 + 0.75, 0.21, 0.11, -0.12),
                            (hx0 + 0.92, 0.12, 0.06, -0.07)):
        ring = pk.rounded_rect(hw, zh + bot, zh + top, 0.08, 0.02, n=3, taper=0.05)
        sec.append((x, ring))
    shell = "metal:stone-pale"
    pk.loft(p, sec, lambda i, j: shell, cap0=shell, cap1=shell)
    # the underside rim + refractor bowl
    p.box(hx0 + 0.08, -0.17, zh - 0.12, hx0 + 0.82, 0.17, zh - 0.1, "metal:ink-900")
    lens = "streetlens:cream" if lit else "streetlens:stone-dark"
    bowl = [(x, pk.rounded_rect(hw, zh - 0.21, zh - 0.1, 0.02, 0.06, n=3))
            for x, hw in ((hx0 + 0.13, 0.1), (hx0 + 0.3, 0.14), (hx0 + 0.6, 0.14), (hx0 + 0.77, 0.1))]
    lp = ak.Part()
    pk.loft(lp, bowl, lambda i, j: lens, cap0=lens, cap1=lens)
    return p, lp, (hx0 + 0.45, 0.0, zh - 0.22)


def light_cone(at, n=28):
    """The game's cone (StreetLight.cs `_cone`, the handoff's pale wedge to the pool): an
    open cone from just under the lens to the ground, LF_StreetCone (additive mercury
    glow, brightest under the head and down its axis; casts no shadow)."""
    p = ak.Part()
    x, y, z = at
    r0, r1 = 0.14, config.STREET_CONE_SPREAD * z
    ring = [(math.cos(2 * math.pi * k / n), math.sin(2 * math.pi * k / n)) for k in range(n)]
    for k in range(n):
        (ca, sa), (cb, sb) = ring[k], ring[(k + 1) % n]
        p.quad((x + r1 * ca, y + r1 * sa, 0.03), (x + r1 * cb, y + r1 * sb, 0.03),
               (x + r0 * cb, y + r0 * sb, z - 0.02), (x + r0 * ca, y + r0 * sa, z - 0.02), "streetcone:mercury-cone",
               smooth=True)
    return p


def build(world, routes, placer, lights_col):
    """Every dump street light. Returns the number built."""
    import bpy

    import scene
    sc = bpy.context.scene
    sc[config.STREET_GLOW_PROP] = config.STREET_GLOW
    road_edge = config.ROAD_HALF_M
    n = 0
    for placed in world.maps.values():
        for i, lt in enumerate(placed.data["lights"]):
            if lt["kind"] != "street":
                continue
            x, y = pk.px_m(placed, lt["px"])
            y += config.TILE_M / 2               # the base anchor is the tile's bottom edge
            lit = bool(lt["lit"])
            near = abs(y - routes.road_y) - road_edge < VERGE_M
            if near:        # reach over the road, square to it
                rot = -math.pi / 2 if y > routes.road_y else math.pi / 2
            else:           # the dump's facing (the arm's side, E or W)
                rot = 0.0 if lt["facing"] == "E" else math.pi
            body_p, lens_p, lens_at = cobra_head(lit)
            asm = ak.Assembly("Street")
            asm.body = body_p
            asm.protos["Lens"] = lens_p
            asm.place("Lens", T(), glow=1.0 if lit else 0.0)
            if lit:
                asm.protos["Cone"] = light_cone(lens_at)
                asm.place("Cone", T(), glow=1.0)
            name = f"StreetLight_{placed.id}_{lt.get('id') or i}"
            z = placer.z(x, y)
            body = placer.put(asm, name, x, y, rot=rot, z=z, map_id=placed.id, dump_id=lt.get("id"),
                              kind="street_light", lit=lit, facing=lt["facing"], hull=True)
            n += 1
            if not lit:
                continue
            for ob in body.children:
                if ob.get("proto") == "Cone":
                    ob.visible_shadow = False           # a glow in the air: never an occluder
                    ob["kind"] = "light_cone"
            L = scene.tag(bpy.data.lights.new(f"Light_Street_{placed.id}_{i}", "SPOT"))
            L.color = config.hex_rgba(config.colour("mercury"))[:3]
            L.spot_size = math.radians(config.STREET_SPOT_DEG[0])
            L.spot_blend = config.STREET_SPOT_DEG[1]
            L.shadow_soft_size = 0.25
            L.energy = 0.0
            drive(L, "energy", config.STREET_GLOW_PROP, config.STREET_LIGHT_W)
            ob = scene.new_object(L.name, L, lights_col, kind="street_light", family=config.STREET_GLOW_PROP,
                                  dump_id=lt.get("id"), glow=1.0)
            M = T(x, y, z) @ Rz(rot) @ T(*lens_at)
            ob.matrix_world = ak._matrix(M)          # spot lights point down -Z already
            body["light"] = ob.name
    return n


def drive(idb, path, prop, scale, index=-1):
    """idb.path = scale x the scene's custom property `prop` x the family's gate over the
    time of the light (config.DUSK_GATE over flyover_dusk; Phase 8), as a driver: Phase 9
    / 10 animate the properties. `v * k * clamp(...)` stays a simple expression (runs
    without Python), so it evaluates headless too."""
    fc = idb.driver_add(path) if index < 0 else idb.driver_add(path, index)
    d = fc.driver
    d.type = "SCRIPTED"
    import look
    for name, p in (("v", prop), ("d", config.DUSK_PROP)):
        look.scene_var(d, name, f'["{p}"]')        # the ACTIVE scene's (each flight's own)
    a, b = config.DUSK_GATE[prop]
    d.expression = f"v * {float(scale)!r} * clamp((d - {a!r}) / {b - a!r}, 0.0, 1.0)"
    return fc

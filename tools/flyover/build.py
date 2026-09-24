"""Build the flyover town from a world dump.

Headless:
    Blender --background --python tools/flyover/build.py -- --world <dump.json>
        [--out tools/flyover/out/town.blend] [--only-map <id>]
        [--render-dir <dir>] [--px-per-tile 6] [--diorama] [--views]
        [--finish clear_dusk|misty_dusk|blue_hour] [--dusk 0..1]
  --diorama builds the Phase 2 flat per-tile diorama instead of terrain + roads (the
  top-down layout/fidelity reference). --views also renders the EEVEE perspective
  stills (named cameras) + an EEVEE top-down into --render-dir
  ([--view-names a,b] [--preset preview_1080|final_2160] [--view-res WxH] [--samples n]
  [--no-layout]).
  Opens --out if it exists (the rebuild replaces only the Flyover tree inside it),
  otherwise starts from an empty file; saves back to --out.

Inside a live Blender session (e.g. MCP execute code):
    import sys; sys.path.insert(0, "<repo>/tools/flyover")
    import build; build.build("<dump.json>")
  build() reloads the package modules first, so edits are picked up without a restart.
"""

import argparse
import importlib
import os
import sys

PKG_DIR = os.path.dirname(os.path.abspath(__file__))
if PKG_DIR not in sys.path:
    sys.path.insert(0, PKG_DIR)

DEFAULT_OUT = os.path.join(PKG_DIR, "out", "town.blend")
_MODULES = ("config", "world", "routes", "treeline", "terrain", "surfaces", "forest", "scene", "materials",
            "diorama", "ground", "road", "markings", "guides", "look", "output", "placeholders", "trees",
            "scatter", "cameras", "flight", "render", "archkit", "pixelfont", "archmats", "buildings_hero",
            "buildings_town", "mansion", "propkit", "streetlights", "signs", "props")


def _modules():
    mods = {}
    for name in _MODULES:   # dependency order: config first
        mod = sys.modules.get(name)
        mods[name] = importlib.reload(mod) if mod else importlib.import_module(name)
    return mods


def build(world_path, only_map=None, diorama=False, finish=None):
    """Wipe and regenerate the Flyover tree in the current file. Returns the World
    (with .terrain attached unless diorama). finish: a look.FINISHES name (default
    config.FINISH)."""
    import time

    import bpy
    t0 = time.time()
    m = _modules()
    config, scene = m["config"], m["scene"]
    world = m["world"].build_world(world_path)
    if only_map is not None and only_map not in world.maps:
        raise ValueError(f"--only-map '{only_map}' is not in the dump: {list(world.maps)}")
    print(m["world"].offset_table(world))

    scene.wipe()
    root = scene.collection(config.ROOT)
    ground = scene.collection(config.COL_GROUND, root)
    cams = scene.collection(config.COL_CAMERAS, root)
    mats = scene.Materials()
    world.terrain = None
    if diorama:
        for kind in config.SURFACES:   # every surface material exists, used or not
            mats.surface(kind)
        m["diorama"].build(world, ground, mats, only_map)
        buildings = scene.collection(config.COL_BUILDINGS, root)
        props = scene.collection(config.COL_PROPS, root)
        m["placeholders"].build(world, buildings, props, mats, only_map)
    else:
        routes = m["routes"].build_routes(world)
        terr = m["terrain"].Terrain(world, routes)
        world.terrain = terr
        guides = scene.collection(config.COL_GUIDES, root)
        light = scene.collection(config.COL_LOOK, root)
        m["materials"].surface_materials(mats, world, terr)
        m["materials"].road_materials(mats)
        paint = m["materials"].paint_material(mats)
        m["ground"].build(terr, ground, mats, only_map)
        if only_map is None:
            m["road"].build(terr, ground, mats)
        m["markings"].build(world, terr, ground, paint, only_map)
        m["guides"].build(terr, guides)
        m["look"].build(light, finish)
        t1 = time.time()
        hero = scene.collection(config.COL_HERO, root)
        world.buildings = m["buildings_hero"].build(world, terr, hero, mats, only_map)
        world.buildings += m["buildings_town"].build(world, terr, hero, mats, only_map)
        if only_map in (None, "east_fork"):
            world.buildings += m["mansion"].build(world, terr, hero, mats)
        for b in world.buildings:
            print(f"flyover: {b['name']} {b['tris']} tris, {b['objects']} objects, {b['height']:.1f} m tall")
        print(f"flyover: buildings in {time.time() - t1:.1f}s")
        t1 = time.time()
        world.props, extents = build_props(m, world, terr, root, mats, only_map)
        tall = [p for p in world.props if "hull" in p]
        print(f"flyover: props {len(world.props)} placed, {sum(p['objects'] for p in world.props)} objects, "
              f"{sum(p['tris'] for p in world.props)} tris ({len(tall)} tall with hulls) in {time.time() - t1:.1f}s")
        if only_map is None:
            t1 = time.time()
            world.forest = m["forest"].plant(world, terr, extents)
            kit = m["trees"].build(root)
            profiles = m["forest"].crown_profiles(m["trees"].kit_vertices(*kit))
            dropped, sample = m["forest"].clear_crowns(world.forest, [b["hull"] for b in world.buildings + tall],
                                                       profiles)
            print(f"flyover: {dropped} trees dropped to keep crowns out of the buildings "
                  f"(farm sample trees among them: {sample})")
            grounds = [ob for ob in ground.objects if ob.name.startswith("Ground_")]
            fcol = scene.collection(config.COL_FOREST, root)
            m["scatter"].build(world.forest.table, grounds, kit, fcol)
            print(f"flyover: forest {len(world.forest.table.x)} instances "
                  f"{world.forest.table.counts()} in {time.time() - t1:.1f}s")
            solids = [bpy.data.collections.get(n) for n in (config.COL_HERO, config.COL_PROP_ART, config.COL_LIGHTS)]
            world.flight, _cam = m["flight"].build(world, terr, cams, guides, m["trees"].kit_vertices(*kit), solids)
    cam = m["cameras"].build_topdown(world, cams)
    if not diorama:
        m["cameras"].build_views(world, world.terrain, cams)
    if only_map is not None:
        m["cameras"].frame_map(world, only_map, cam)
    m["output"].apply(m["output"].DEFAULT)
    print(f"flyover: built in {time.time() - t0:.1f}s")
    return world


def build_props(m, world, terr, root, mats, only_map=None):
    """Phase 7: street lights, signs, props (no Phase 2 markers ship). Returns (stats,
    [(x0, y0, x1, y1)] world XY extents for the forest's keep-outs)."""
    import bpy
    import numpy as np
    config, scene = m["config"], m["scene"]
    col = scene.collection(config.COL_PROP_ART, root)
    lights = scene.collection(config.COL_LIGHTS, root)
    placer = m["propkit"].Placer(terr, col, m["archmats"].Resolver(mats))
    routes = terr.routes
    sub = world
    if only_map is not None:
        sub = type(world)(maps={only_map: world.maps[only_map]}, bounds=world.bounds)
    m["streetlights"].build(sub, routes, placer, lights)
    m["signs"].build(sub, routes, placer, lights)
    m["props"].build(sub, routes, placer, lights)
    bpy.context.view_layer.update()
    extents = []
    for ob in col.objects:
        if ob.parent is not None:
            continue
        pts = [o.matrix_world @ v.co for o in [ob] + list(ob.children) for v in o.data.vertices]
        a = np.array([(p.x, p.y) for p in pts])
        extents.append((float(a[:, 0].min()), float(a[:, 1].min()), float(a[:, 0].max()), float(a[:, 1].max())))
    return placer.out, extents


VIEWS = ("Cam_WestRoad", "Cam_Fork", "Cam_Plaza", "Cam_DriveIn", "Cam_EastOut", "Cam_Overview",
         "Cam_MansionDrive", "Cam_FarmTreeline", "Cam_Billies", "Cam_EastEntry", "Cam_MansionGlimpse")


def render_stills(world, out_dir, px_per_tile=6, only_map=None):
    return _modules()["render"].stills(world, out_dir, px_per_tile, only_map)


def render_views(world, out_dir, names=VIEWS, preset="preview_1080", samples=None, res=None, topdown=True,
                 suffix=""):
    import render
    return render.eevee_stills(world, out_dir, names, preset=preset, res=res, samples=samples, topdown=topdown,
                               suffix=suffix)


def _parse(argv):
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    p = argparse.ArgumentParser(prog="build.py")
    p.add_argument("--world", required=True, help="world dump JSON (godot --dump-world)")
    p.add_argument("--out", default=DEFAULT_OUT, help="the .blend to (re)build")
    p.add_argument("--only-map", default=None, help="generate a single map")
    p.add_argument("--render-dir", default=None, help="also render top-down stills here")
    p.add_argument("--px-per-tile", type=int, default=6)
    p.add_argument("--diorama", action="store_true", help="the flat Phase 2 diorama instead")
    p.add_argument("--views", action="store_true", help="also render the EEVEE view stills")
    p.add_argument("--view-names", default=None, help="comma-separated cameras (default: all)")
    p.add_argument("--finish", default=None, help="the look: clear_dusk (default) / misty_dusk / blue_hour")
    p.add_argument("--dusk", type=float, default=None, help="the time of the light, 0 = 16:00 .. 1 = dusk")
    p.add_argument("--preset", default="preview_1080", help="output preset for --views (output.PRESETS)")
    p.add_argument("--view-res", default=None, help="override the preset's size, e.g. 3840x2160")
    p.add_argument("--samples", type=int, default=None, help="override the preset's EEVEE samples")
    p.add_argument("--no-layout", action="store_true", help="skip the Workbench layout stills")
    return p.parse_args(argv)


def main():
    import bpy
    args = _parse(sys.argv)
    out = os.path.abspath(args.out)
    if os.path.exists(out):
        bpy.ops.wm.open_mainfile(filepath=out)
    else:
        bpy.ops.wm.read_homefile(use_empty=True)
    world = build(os.path.abspath(args.world), args.only_map, args.diorama, args.finish)
    if args.dusk is not None and not args.diorama:
        sys.modules["look"].set_dusk(args.dusk)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=out)
    print(f"flyover: saved {out}")
    if args.render_dir:
        if not args.no_layout:
            for path in render_stills(world, os.path.abspath(args.render_dir), args.px_per_tile,
                                      args.only_map):
                print(f"flyover: rendered {path}")
        if args.views and not args.diorama:
            names = args.view_names.split(",") if args.view_names else VIEWS
            res = tuple(int(v) for v in args.view_res.split("x")) if args.view_res else None
            h = (res or sys.modules["output"].PRESETS[args.preset]["res"])[1]
            suffix = "" if h == 1080 else f"_{h}p"
            for path, secs in render_views(world, os.path.abspath(args.render_dir), names, args.preset,
                                           args.samples, res, topdown=args.view_names is None,
                                           suffix=suffix):
                print(f"flyover: rendered {path} in {secs:.1f}s")


if __name__ == "__main__":
    main()

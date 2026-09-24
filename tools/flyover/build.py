"""Build the flyover town from a world dump.

Headless:
    Blender --background --python tools/flyover/build.py -- --world <dump.json>
        [--out tools/flyover/out/town.blend] [--only-map <id>]
        [--render-dir <dir>] [--px-per-tile 6] [--diorama] [--views]
  --diorama builds the Phase 2 flat per-tile diorama instead of terrain + roads (the
  top-down layout/fidelity reference). --views also renders the EEVEE perspective
  stills (named cameras) + an EEVEE top-down into --render-dir
  ([--view-names a,b] [--view-res 3840x2160] [--samples 32] [--no-layout]).
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
            "diorama", "ground", "road", "markings", "guides", "templight", "placeholders", "trees",
            "scatter", "cameras", "render")


def _modules():
    mods = {}
    for name in _MODULES:   # dependency order: config first
        mod = sys.modules.get(name)
        mods[name] = importlib.reload(mod) if mod else importlib.import_module(name)
    return mods


def build(world_path, only_map=None, diorama=False):
    """Wipe and regenerate the Flyover tree in the current file. Returns the World
    (with .terrain attached unless diorama)."""
    import time
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
    buildings = scene.collection(config.COL_BUILDINGS, root)
    props = scene.collection(config.COL_PROPS, root)
    cams = scene.collection(config.COL_CAMERAS, root)
    mats = scene.Materials()
    world.terrain = None
    if diorama:
        for kind in config.SURFACES:   # every surface material exists, used or not
            mats.surface(kind)
        m["diorama"].build(world, ground, mats, only_map)
        m["placeholders"].build(world, buildings, props, mats, only_map)
    else:
        routes = m["routes"].build_routes(world)
        terr = m["terrain"].Terrain(world, routes)
        world.terrain = terr
        guides = scene.collection(config.COL_GUIDES, root)
        light = scene.collection(config.COL_TEMP_LIGHT, root)
        m["materials"].surface_materials(mats, world, terr)
        m["materials"].road_materials(mats)
        paint = m["materials"].paint_material(mats)
        m["ground"].build(terr, ground, mats, only_map)
        if only_map is None:
            m["road"].build(terr, ground, mats)
        m["markings"].build(world, terr, ground, paint, only_map)
        m["guides"].build(terr, guides)
        m["templight"].build(light)
        m["placeholders"].build(world, buildings, props, mats, only_map, ground=terr,
                                forest=only_map is None)
        if only_map is None:
            t1 = time.time()
            world.forest = m["forest"].plant(world, terr)
            kit = m["trees"].build(root)
            grounds = [ob for ob in ground.objects if ob.name.startswith("Ground_")]
            fcol = scene.collection(config.COL_FOREST, root)
            m["scatter"].build(world.forest.table, grounds, kit, fcol)
            print(f"flyover: forest {len(world.forest.table.x)} instances "
                  f"{world.forest.table.counts()} in {time.time() - t1:.1f}s")
    cam = m["cameras"].build_topdown(world, cams)
    if not diorama:
        m["cameras"].build_views(world, world.terrain, cams)
    if only_map is not None:
        m["cameras"].frame_map(world, only_map, cam)
    print(f"flyover: built in {time.time() - t0:.1f}s")
    return world


VIEWS = ("Cam_WestRoad", "Cam_Fork", "Cam_Plaza", "Cam_DriveIn", "Cam_EastOut", "Cam_Overview",
         "Cam_MansionDrive", "Cam_FarmTreeline")


def render_stills(world, out_dir, px_per_tile=6, only_map=None):
    return _modules()["render"].stills(world, out_dir, px_per_tile, only_map)


def render_views(world, out_dir, names=VIEWS, samples=32, res=(1920, 1080), topdown=True, suffix=""):
    import render
    return render.eevee_stills(world, out_dir, names, res=res, samples=samples, topdown=topdown,
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
    p.add_argument("--view-res", default="1920x1080", help="EEVEE still size, e.g. 3840x2160")
    p.add_argument("--samples", type=int, default=32, help="EEVEE samples per still")
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
    world = build(os.path.abspath(args.world), args.only_map, args.diorama)
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
            w, h = (int(v) for v in args.view_res.split("x"))
            suffix = "" if (w, h) == (1920, 1080) else f"_{h}p"
            for path, secs in render_views(world, os.path.abspath(args.render_dir), names,
                                           args.samples, (w, h), topdown=args.view_names is None,
                                           suffix=suffix):
                print(f"flyover: rendered {path} in {secs:.1f}s")


if __name__ == "__main__":
    main()

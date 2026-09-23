"""Routes as data other phases read: curve objects (POLY splines, points on the ground)
and empties in the Flyover_Guides collection. Hidden from renders.

  Guide_Road             the whole paved road centreline, west end -> east end (z = road)
  Guide_RoadOut_W / _E   just the road-outs, from the town edge outwards
  Guide_Track_<name>     each dirt track centreline from the road (FarmRoad,
                         MansionDrive, ForkSouthStub, DriveInDrive)
  Guide_MansionDriveEnd  empty at the mansion drive's end (Phase 6 roofline target)
  Guide_MansionClearing  empty (cube display) over the clearing past it: local X along
                         the drive; custom props width_m (across) / depth_m (along) /
                         heading_deg / ground_z (the levelled shelf). Phase 6 puts the
                         ruined mansion's roofline here; the forest rings it with tall trees.

Custom props: `clear_m` = keep-out half width for the forest (Phase 4), `length_m`.
The same data, without Blender: routes.build_routes(world) + terrain.Terrain(...)
(road_samples(), track_polyline(name), routes.mansion_end()).
"""

import bpy

import config
import scene
import terrain as terrain_mod


def _curve(name, pts, col, **props):
    cu = scene.tag(bpy.data.curves.new(name, "CURVE"))
    cu.dimensions = "3D"
    sp = cu.splines.new("POLY")
    sp.points.add(len(pts) - 1)
    for p, (x, y, z) in zip(sp.points, pts):
        p.co = (x, y, z, 1.0)
    ob = scene.new_object(name, cu, col, **props)
    ob.hide_render = True
    return ob


def build(terrain, col):
    road = terrain.road_samples()
    pts = [(p["x"], p["y"], p["z"]) for p in road]
    s = terrain_mod.arc_lengths([(p[0], p[1]) for p in pts])
    _curve("Guide_Road", pts, col, clear_m=config.CORRIDOR_CLEAR_M, length_m=float(s[-1]))
    for side, o in zip("WE", terrain.outs):
        opts = [(x, y, z) for (x, y), z in zip(o["pts"], o["z"])]
        _curve(f"Guide_RoadOut_{side}", opts, col, clear_m=config.CORRIDOR_CLEAR_M,
               length_m=float(o["s"][-1]))
    for name, t in terrain.routes.tracks.items():
        tp = terrain.track_polyline(name)
        _curve(f"Guide_Track_{name}", tp, col, clear_m=config.TRACK_CLEAR_M,
               length_m=float(terrain_mod.arc_lengths([(p[0], p[1]) for p in tp])[-1]),
               chain=t.chain, extension_from_m=float(
                   terrain_mod.arc_lengths(t.pts[:t.extension_from + 1])[-1])
               if t.extension_from >= 0 else None)
    end = terrain.routes.mansion_end()
    if end is not None:
        ob = scene.new_object("Guide_MansionDriveEnd", None, col, kind="mansion_target")
        ob.location = (end[0], end[1], terrain.z_at(*end))
        ob.empty_display_type = "SPHERE"
        ob.empty_display_size = 3.0
    c = terrain.routes.mansion_clearing()
    if c is not None:
        import math
        cx, cy, th, w, d = c
        ob = scene.new_object("Guide_MansionClearing", None, col, kind="mansion_clearing",
                              width_m=w, depth_m=d, heading_deg=math.degrees(th),
                              ground_z=terrain.z_at(cx, cy))
        ob.location = (cx, cy, terrain.z_at(cx, cy))
        ob.rotation_euler = (0.0, 0.0, th)
        ob.empty_display_type = "CUBE"      # unit cube scaled to the clearing's half extents
        ob.scale = (d / 2, w / 2, 4.0)

"""Top-down orthographic framing: Cam_TopDown over the whole stitched world, and a
helper to frame any tile rect (a single map) with it. North is up in frame."""

import bpy

import config
import scene


def rect_m(world, map_id=None):
    """(x0, y0, x1, y1) metres of the stitched world, or of one map."""
    t = config.TILE_M
    if map_id is None:
        gx0, gy0, gx1, gy1 = world.bounds
    else:
        p = world.maps[map_id]
        gx0, gy0, gx1, gy1 = p.ox, p.oy, p.ox + p.width, p.oy + p.height
    return gx0 * t, -gy1 * t, gx1 * t, -gy0 * t


def build_topdown(world, col):
    cam = scene.tag(bpy.data.cameras.new(config.CAMERA_TOPDOWN))
    cam.type = "ORTHO"
    cam.clip_start = 1.0
    cam.clip_end = config.CAMERA_HEIGHT_M * 2
    cam.sensor_fit = "AUTO"
    ob = scene.new_object(config.CAMERA_TOPDOWN, cam, col)
    ob.rotation_euler = (0.0, 0.0, 0.0)   # looking straight down -Z, +Y (north) up
    frame(ob, rect_m(world))
    if bpy.context.scene.camera is None:
        bpy.context.scene.camera = ob
    return ob


def frame(cam_ob, rect, aspect=None):
    """Point the ortho camera at rect (metres). aspect = render width / height; defaults
    to the current scene's. Returns the (w, h) metres framed."""
    x0, y0, x1, y1 = rect
    w, h = (x1 - x0) * config.FRAME_PADDING, (y1 - y0) * config.FRAME_PADDING
    if aspect is None:
        r = bpy.context.scene.render
        aspect = (r.resolution_x * r.pixel_aspect_x) / (r.resolution_y * r.pixel_aspect_y)
    cam_ob.location = ((x0 + x1) / 2, (y0 + y1) / 2, config.CAMERA_HEIGHT_M)
    # AUTO sensor fit: ortho_scale spans the larger render dimension.
    cam_ob.data.ortho_scale = max(w, h * aspect) if aspect >= 1 else max(h, w / aspect)
    return w, h


def frame_map(world, map_id, cam_ob=None, aspect=None):
    cam_ob = cam_ob or bpy.data.objects[config.CAMERA_TOPDOWN]
    return frame(cam_ob, rect_m(world, map_id), aspect)


# ---------------------------------------------------------------------------
# Named perspective views (Phase 3 checks; reusable framing for later phases).
# Every position is derived from the dump via routes / terrain, never hand-set.
# ---------------------------------------------------------------------------

def _look(name, col, pos, target, lens=config.VIEW_LENS_MM):
    from mathutils import Vector
    cam = scene.tag(bpy.data.cameras.new(name))
    cam.lens = lens
    cam.clip_start = 0.3
    cam.clip_end = config.CLIP_END_M
    ob = scene.new_object(name, cam, col, kind="view")
    ob.location = pos
    d = Vector(target) - Vector(pos)
    ob.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    ob["target"] = list(target)
    return ob


def _at_arc(pts, s_want):
    """(x, y, index) on a polyline at arc length s_want."""
    import math
    s = 0.0
    for k, (a, b) in enumerate(zip(pts, pts[1:])):
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        if s + L >= s_want and L > 0:
            t = (s_want - s) / L
            return a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, k
        s += L
    return pts[-1][0], pts[-1][1], len(pts) - 1


def _surface_bbox(placed, kind):
    t = config.TILE_M
    cells = [(x, y) for y, row in enumerate(placed.surfaces) for x, s in enumerate(row) if s == kind]
    xs, ys = [c[0] for c in cells], [c[1] for c in cells]
    return ((placed.ox + min(xs)) * t, -(placed.oy + max(ys) + 1) * t,
            (placed.ox + max(xs) + 1) * t, -(placed.oy + min(ys)) * t)


def _prop_rect(world, kind):
    t = config.TILE_M
    for m in world.maps.values():
        for p in m.data["props"]:
            if p["kind"] == kind:
                return ((m.ox + p["x"]) * t, -(m.oy + p["y"] + p["h"]) * t,
                        (m.ox + p["x"] + p["w"]) * t, -(m.oy + p["y"]) * t)
    return None


def build_views(world, terrain, col):
    r = terrain.routes
    z = terrain.z_at
    views = []
    # West road: standing on the road-out, looking east into West Entry.
    wx, wy, _ = _at_arc(r.out_w, 55.0)
    tx = r.road_x[0] + 75.0
    views.append(_look("Cam_WestRoad", col, (wx, wy + 1.2, z(wx, wy) + 2.4),
                       (tx, r.road_y + 3.0, 2.0)))
    # Fork: above the road, south of the fork, looking north up the farm road.
    farm = r.tracks.get("FarmRoad")
    if farm is not None:
        fx, fy = farm.pts[0]
        ax, ay, _ = _at_arc(farm.pts, 70.0)
        views.append(_look("Cam_Fork", col, (fx, fy - 38.0, 24.0), (ax, ay, 0.0)))
    # Plaza: low oblique over the town's cobble, from the south-west.
    town = next((m for m in world.maps.values()
                 if any("Cobble" in row for row in m.surfaces)), None)
    if town is not None:
        x0, y0, x1, y1 = _surface_bbox(town, "Cobble")
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        views.append(_look("Cam_Plaza", col, (cx - 14.0, cy - 17.0, 9.5), (cx, cy + 1.0, 0.0)))
    # Drive-in: from above its north-east corner, over the ramps toward the screen.
    field = _prop_rect(world, "ramp_rows")
    if field is not None:
        x0, y0, x1, y1 = field
        # over the field's grass north of the lot, above the (real-height) treeline
        views.append(_look("Cam_DriveIn", col, (x1 - 3.0, y1 + 12.0, 22.0),
                           ((x0 + x1) / 2 - 4.0, (y0 + y1) / 2 - 4.0, 0.0)))
    # East out: on the road near the east edge, looking east into the curve.
    ex, ey, _ = _at_arc(r.out_e, 200.0)
    views.append(_look("Cam_EastOut", col, (r.road_x[1] - 30.0, r.road_y - 1.2, 3.2),
                       (ex, ey, z(ex, ey) + 1.5)))
    # Mansion drive: standing on the east_fork road, looking north up the chained drive
    # into the dark band, toward the clearing (the roofline's future place).
    drive = r.tracks.get("MansionDrive")
    if drive is not None:
        mx, my = drive.pts[0]
        ex, ey = drive.pts[-1]
        views.append(_look("Cam_MansionDrive", col, (mx - 1.0, r.road_y - 1.5, 1.8),
                           (ex, ey, z(ex, ey) + 7.0)))
    # Farm treeline: from the fork, low, looking north along the farm road through the
    # fork's north tree line and the farm's south treeline.
    if farm is not None:
        fx, fy = farm.pts[0]
        ax, ay, _ = _at_arc(farm.pts, 60.0)
        views.append(_look("Cam_FarmTreeline", col, (fx + 1.2, r.road_y - 3.0, 2.6),
                           (ax, ay, 3.0)))
    # Overview: high oblique over the whole town from the south (terrain + edges check).
    gx0, gy0, gx1, gy1 = world.bounds
    t = config.TILE_M
    cx, cy = (gx0 + gx1) / 2 * t, -(gy0 + gy1) / 2 * t
    # Pitched to hold the northern ridges against the sky: the valley, not a map.
    views.append(_look("Cam_Overview", col, (cx, cy - 520.0, 200.0), (cx, cy + 260.0, 10.0), lens=28.0))
    return views

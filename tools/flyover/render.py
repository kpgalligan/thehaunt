"""Verification stills: top-down ortho renders of the world and of each map, in
Workbench with FLAT lighting and the Standard view transform, so every tile shows
its exact palette colour (directly comparable with the 2D game); the forest is hidden
for them (it would cover the ring tiles). Render settings are restored afterwards, so
this is safe inside a live session."""

import os

import bpy

import cameras
import config


class _Saved:
    """Snapshot + restore of the render settings a still touches."""

    def __init__(self, sc):
        self.sc = sc
        r, sh = sc.render, sc.display.shading
        self.vals = dict(engine=r.engine, rx=r.resolution_x, ry=r.resolution_y,
                         pct=r.resolution_percentage, fp=r.filepath, cam=sc.camera,
                         vt=sc.view_settings.view_transform, look=sc.view_settings.look,
                         exp=sc.view_settings.exposure, comp=r.use_compositing,
                         light=sh.light, ctype=sh.color_type)
        cam = bpy.data.objects[config.CAMERA_TOPDOWN]
        self.cam_loc, self.cam_scale = tuple(cam.location), cam.data.ortho_scale

    def restore(self):
        sc, v = self.sc, self.vals
        r, sh = sc.render, sc.display.shading
        r.engine, r.resolution_x, r.resolution_y = v["engine"], v["rx"], v["ry"]
        r.resolution_percentage, r.filepath, sc.camera = v["pct"], v["fp"], v["cam"]
        sc.view_settings.view_transform, sc.view_settings.look = v["vt"], v["look"]
        sc.view_settings.exposure, r.use_compositing = v["exp"], v["comp"]
        sh.light, sh.color_type = v["light"], v["ctype"]
        cam = bpy.data.objects[config.CAMERA_TOPDOWN]
        cam.location, cam.data.ortho_scale = self.cam_loc, self.cam_scale


def still(world, path, map_id=None, px_per_tile=6):
    sc = bpy.context.scene
    saved = _Saved(sc)
    forest = bpy.data.collections.get(config.COL_FOREST)
    hidden = forest.hide_render if forest is not None else None
    cones = [ob for ob in bpy.data.objects if ob.get("kind") == "light_cone" and not ob.hide_render]
    try:
        for ob in cones:            # a glow in the air, not a solid
            ob.hide_render = True
        if forest is not None:      # the layout check shows the ground, not the canopy
            forest.hide_render = True
        x0, y0, x1, y1 = cameras.rect_m(world, map_id)
        tiles_w = (x1 - x0) / config.TILE_M * config.FRAME_PADDING
        tiles_h = (y1 - y0) / config.TILE_M * config.FRAME_PADDING
        r = sc.render
        r.engine = "BLENDER_WORKBENCH"
        r.resolution_x = int(round(tiles_w * px_per_tile))
        r.resolution_y = int(round(tiles_h * px_per_tile))
        r.resolution_percentage = 100
        sc.display.shading.light = "FLAT"
        sc.display.shading.color_type = "MATERIAL"
        sc.view_settings.view_transform = "Standard"
        sc.view_settings.look = "None"
        sc.view_settings.exposure = 0.0
        r.use_compositing = False          # no haze / grade: exact palette colours
        cam = bpy.data.objects[config.CAMERA_TOPDOWN]
        sc.camera = cam
        cameras.frame(cam, (x0, y0, x1, y1), r.resolution_x / r.resolution_y)
        r.filepath = path
        bpy.ops.render.render(write_still=True)
    finally:
        saved.restore()
        for ob in cones:
            ob.hide_render = False
        if forest is not None:
            forest.hide_render = hidden
    return path


def stills(world, out_dir, px_per_tile=6, only=None):
    os.makedirs(out_dir, exist_ok=True)
    paths = []
    if only is None:
        paths.append(still(world, os.path.join(out_dir, "world.png"), None, max(2, px_per_tile // 2)))
    for mid in world.maps:
        if only is None or mid == only:
            paths.append(still(world, os.path.join(out_dir, f"map_{mid}.png"), mid, px_per_tile))
    return paths


# ---------------------------------------------------------------------------
# EEVEE perspective stills from the named views (cameras.build_views) + an EEVEE
# top-down, in the film's look (look.py) at an output preset (output.py). Settings
# restored afterwards.
# ---------------------------------------------------------------------------

def eevee_stills(world, out_dir, names, preset="preview_1080", res=None, samples=None, topdown=True, suffix=""):
    """Render each named camera; returns [(path, seconds)]."""
    import time

    import output
    sc = bpy.context.scene
    r = sc.render
    saved = dict(rx=r.resolution_x, ry=r.resolution_y, fp=r.filepath, cam=sc.camera,
                 preset=sc.get("flyover_output", output.DEFAULT))
    top = bpy.data.objects[config.CAMERA_TOPDOWN]
    top_loc, top_scale = tuple(top.location), top.data.ortho_scale
    os.makedirs(out_dir, exist_ok=True)
    timings = []
    try:
        p = output.apply(preset, sc, res, samples)
        res = res or p["res"]
        jobs = [(n, bpy.data.objects[n]) for n in names]
        if topdown:
            jobs.append(("TopDown_eevee", top))
        for name, cam in jobs:
            if cam is top:
                x0, y0, x1, y1 = cameras.rect_m(world)
                r.resolution_x, r.resolution_y = 2400, int(2400 * (y1 - y0) / (x1 - x0) * 1.0) + 40
                cameras.frame(cam, (x0, y0, x1, y1), r.resolution_x / r.resolution_y)
            else:
                r.resolution_x, r.resolution_y = res
            sc.camera = cam
            path = os.path.join(out_dir, f"{name}{suffix}.png")
            r.filepath = path
            t0 = time.time()
            bpy.ops.render.render(write_still=True)
            timings.append((path, time.time() - t0))
    finally:
        output.apply(saved["preset"], sc)
        r.resolution_x, r.resolution_y, r.filepath, sc.camera = saved["rx"], saved["ry"], saved["fp"], saved["cam"]
        top.location, top.data.ortho_scale = top_loc, top_scale
    return timings

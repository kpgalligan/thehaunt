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
                         light=sh.light, ctype=sh.color_type)
        cam = bpy.data.objects[config.CAMERA_TOPDOWN]
        self.cam_loc, self.cam_scale = tuple(cam.location), cam.data.ortho_scale

    def restore(self):
        sc, v = self.sc, self.vals
        r, sh = sc.render, sc.display.shading
        r.engine, r.resolution_x, r.resolution_y = v["engine"], v["rx"], v["ry"]
        r.resolution_percentage, r.filepath, sc.camera = v["pct"], v["fp"], v["cam"]
        sc.view_settings.view_transform, sc.view_settings.look = v["vt"], v["look"]
        sh.light, sh.color_type = v["light"], v["ctype"]
        cam = bpy.data.objects[config.CAMERA_TOPDOWN]
        cam.location, cam.data.ortho_scale = self.cam_loc, self.cam_scale


def still(world, path, map_id=None, px_per_tile=6):
    sc = bpy.context.scene
    saved = _Saved(sc)
    forest = bpy.data.collections.get(config.COL_FOREST)
    hidden = forest.hide_render if forest is not None else None
    try:
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
        cam = bpy.data.objects[config.CAMERA_TOPDOWN]
        sc.camera = cam
        cameras.frame(cam, (x0, y0, x1, y1), r.resolution_x / r.resolution_y)
        r.filepath = path
        bpy.ops.render.render(write_still=True)
    finally:
        saved.restore()
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
# top-down, under the temporary light (templight). Settings restored afterwards.
# ---------------------------------------------------------------------------

def eevee_stills(world, out_dir, names, res=(1280, 720), samples=16, topdown=True, suffix=""):
    import time
    sc = bpy.context.scene
    r = sc.render
    saved = dict(engine=r.engine, rx=r.resolution_x, ry=r.resolution_y,
                 pct=r.resolution_percentage, fp=r.filepath, cam=sc.camera,
                 vt=sc.view_settings.view_transform, look=sc.view_settings.look,
                 samples=sc.eevee.taa_render_samples)
    top = bpy.data.objects[config.CAMERA_TOPDOWN]
    top_loc, top_scale = tuple(top.location), top.data.ortho_scale
    os.makedirs(out_dir, exist_ok=True)
    timings = []
    try:
        r.engine = "BLENDER_EEVEE"
        r.resolution_x, r.resolution_y = res
        r.resolution_percentage = 100
        sc.eevee.taa_render_samples = samples
        sc.view_settings.view_transform = "Standard"
        sc.view_settings.look = "None"
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
        r.engine, r.resolution_x, r.resolution_y = saved["engine"], saved["rx"], saved["ry"]
        r.resolution_percentage, r.filepath, sc.camera = saved["pct"], saved["fp"], saved["cam"]
        sc.view_settings.view_transform, sc.view_settings.look = saved["vt"], saved["look"]
        sc.eevee.taa_render_samples = saved["samples"]
        top.location, top.data.ortho_scale = top_loc, top_scale
    return timings

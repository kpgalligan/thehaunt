"""Render output presets (Phase 8): EEVEE quality, resolution, motion blur, colour
management and file format, by name. `apply(name)` sets the scene; build() leaves the
file on PREVIEW so a plain render (F12) is the film's look.

- preview_1080: 1920x1080, 16 samples, motion blur (1 step), PNG 8-bit. Iteration and
  the animatic.
- final_2160: 3840x2160, 48 samples, motion blur (2 steps), PNG 16-bit RGB, display-
  referred (AgX + the look applied): the frames ffmpeg encodes (Phase 11). 16-bit keeps
  the dusk sky's gradients from banding before the encode dithers them.

Measured (M-series Mac, clear_dusk, warm shaders): preview_1080 3-8 s/frame (WestRoad 6.0,
Plaza 3.7, Overview 5.4); final_2160 21-29 s (WestRoad 23, Plaza 28.5, Overview 21).
Samples x pixels drive it; the first render of a session adds ~2-3 s of shader compile.

The view transform / look / exposure belong to the finish (look.py), not here.
Shadows: a 1 GB virtual shadow pool (headroom for 4K over the forest; it was NOT the
stair-step fix: that is the sun's filter radius, look.py) + shadow rays / steps for the
soft penumbra of the lamps and the sun.
"""

import bpy

PRESETS = {
    "preview_1080": dict(res=(1920, 1080), samples=16, mb_steps=1, depth="8", pool="1024",
                         shadow_rays=1, shadow_steps=6, clamp=10.0),
    "final_2160": dict(res=(3840, 2160), samples=48, mb_steps=2, depth="16", pool="1024",
                       shadow_rays=2, shadow_steps=8, clamp=10.0),
}
DEFAULT = "preview_1080"
SHUTTER = 0.5               # 180-degree shutter: the flight's natural blur


def apply(name=DEFAULT, sc=None, res=None, samples=None):
    """Set the scene's render settings to the preset (res / samples override it)."""
    if name not in PRESETS:
        raise ValueError(f"unknown output preset '{name}': {sorted(PRESETS)}")
    p = PRESETS[name]
    sc = sc or bpy.context.scene
    r, e = sc.render, sc.eevee
    r.engine = "BLENDER_EEVEE"
    r.resolution_x, r.resolution_y = res or p["res"]
    r.resolution_percentage = 100
    r.fps = 30
    r.film_transparent = False
    r.use_motion_blur = True
    r.motion_blur_shutter = SHUTTER
    e.motion_blur_steps = p["mb_steps"]
    e.taa_render_samples = samples or p["samples"]
    e.use_shadows = True
    e.shadow_pool_size = p["pool"]
    e.shadow_resolution_scale = 1.0
    e.shadow_ray_count = p["shadow_rays"]
    e.shadow_step_count = p["shadow_steps"]
    e.use_volumetric_shadows = False
    e.clamp_surface_indirect = p["clamp"]
    im = r.image_settings
    im.file_format = "PNG"
    im.color_mode = "RGB"
    im.color_depth = p["depth"]
    im.compression = 15
    sc["flyover_output"] = name
    return p

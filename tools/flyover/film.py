"""Phase 11: render the film's frames (the flight camera, through an output preset) to
numbered PNGs, resumably. encode.py turns the frames into the .ogv / .mp4.

    Blender --background --python tools/flyover/film.py -- --world <dump.json>
        --preset preview_1080|final_2160 --frames-dir <dir> [--flight town_pass|farm_to_pit]
        [--start N --end M] [--blend tools/flyover/out/town.blend] [--rebuild]
        [--res WxH] [--samples n] [--depth 8|16] [--force-mixed]

- The scene: `--blend` (default out/town.blend) is REUSED when it carries the current
  build key (sha256 of the dump + every package .py, scene["flyover_build_key"]);
  otherwise the town is built fresh from the dump (an empty file, ~25 s), saved there
  with the key and reopened, so every run renders the saved file. `--rebuild` forces the
  build. A restart therefore skips the build.
- Flights: `--flight` (default town_pass, the film) renders that flight's camera in its
  own scene (flight.py); frames dirs are per flight (film.json records it). `--playblast
  <mp4> [--step n] [--res WxH]` renders the fast Workbench preview instead
  (render.playblast: timing / framing, not the look).
- Resumable: frame N is <dir>/f_NNNN.png. It is rendered to f_NNNN.partial.png and
  renamed when complete, so a killed render never leaves a frame that counts as done;
  existing frames are skipped, stale partials deleted on start.
- The log, <dir>/film.json (rewritten after every frame): preset, resolution, samples,
  build key, per-frame seconds, frames done, avg s/frame. A frames dir started with
  another preset / resolution / build key is refused (no mixed films) unless
  `--force-mixed`.
- Deterministic: every motion is a function of the scene frame (flight keys per frame,
  life.py's CONSTANT V keys and Scene Time node graphs), and each frame is rendered
  alone after frame_set, so a frame renders the same in any order or run.
"""

import argparse
import glob
import hashlib
import json
import os
import sys
import time

PKG_DIR = os.path.dirname(os.path.abspath(__file__))
if PKG_DIR not in sys.path:
    sys.path.insert(0, PKG_DIR)

DEFAULT_BLEND = os.path.join(PKG_DIR, "out", "town.blend")
LOG = "film.json"
KEY_PROP = "flyover_build_key"


def build_key(world_path):
    """sha256 over the dump + every package module (flights/ too): a .blend with this key
    is current."""
    h = hashlib.sha256()
    with open(world_path, "rb") as f:
        h.update(f.read())
    paths = glob.glob(os.path.join(PKG_DIR, "*.py")) + glob.glob(os.path.join(PKG_DIR, "flights", "*.py"))
    for path in sorted(paths):
        if os.path.basename(path) in ("film.py", "encode.py"):
            continue    # the renderer / encoder do not shape the scene
        h.update(os.path.relpath(path, PKG_DIR).encode())
        with open(path, "rb") as f:
            h.update(f.read())
    return h.hexdigest()[:16]


def ensure_scene(world_path, blend, rebuild=False):
    """Open the current .blend or build (and save) it. Returns 'reused' / 'built'."""
    import bpy
    import flight
    key = build_key(world_path)
    if not rebuild and os.path.exists(blend):
        bpy.ops.wm.open_mainfile(filepath=blend)
        if flight.host_scene().get(KEY_PROP) == key:
            print(f"film: reusing {blend} (build key {key})", flush=True)
            return "reused"
        print(f"film: {blend} is stale (key {flight.host_scene().get(KEY_PROP)} != {key}): rebuilding",
              flush=True)
    bpy.ops.wm.read_homefile(use_empty=True)
    import build
    build.build(os.path.abspath(world_path), cache=False)
    import flight
    flight.host_scene()[KEY_PROP] = key
    os.makedirs(os.path.dirname(blend), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    flight.save_cache(blend)
    bpy.ops.wm.open_mainfile(filepath=blend)    # render what a resumed run renders: the file
    print(f"film: built and saved {blend} (build key {key})", flush=True)
    return "built"


def frame_path(frames_dir, f, partial=False):
    return os.path.join(frames_dir, f"f_{f:04d}{'.partial' if partial else ''}.png")


def _write_json(path, data):
    tmp = path + ".tmp"
    with open(tmp, "w") as fh:
        json.dump(data, fh, indent=1)
    os.replace(tmp, path)


def _load_log(frames_dir, want, force):
    path = os.path.join(frames_dir, LOG)
    if not os.path.exists(path):
        return dict(want, times={})
    with open(path) as fh:
        log = json.load(fh)
    diff = {k: (log.get(k), v) for k, v in want.items() if log.get(k) != v}
    if diff and not force:
        raise SystemExit(f"film: {frames_dir} was started with other settings {diff}; "
                         f"use a new --frames-dir or --force-mixed")
    log.update(want)
    log.setdefault("times", {})
    return log


def _fmt(secs):
    secs = int(round(secs))
    return f"{secs // 3600}h{secs // 60 % 60:02d}m{secs % 60:02d}s"


def render_frames(frames_dir, preset, start=None, end=None, res=None, samples=None, force_mixed=False,
                  depth=None, flight_name=None):
    """Render every missing frame of start..end of a flight (default the primary) in its
    own scene; returns the log dict."""
    import bpy

    import flight
    import output
    flight_name = flight_name or flight.primary()
    sq = flight.seq(flight_name)
    sc = flight.set_window_scene(flight.scene_of(flight_name))
    r = sc.render
    p = output.apply(preset, sc, res, samples)
    if depth:                         # PNG bit depth override (8 halves final_2160's disk; dithered)
        r.image_settings.color_depth = str(depth)
    sc.camera = bpy.data.objects[sq.CAMERA]
    fc = flight.dusk_fcurve(sc)
    if fc is None:
        raise AssertionError("the flight's flyover_dusk keys are missing: is the flight built?")
    fc.mute = False                   # the film's own time of the light (eevee_stills mutes it)
    r.use_file_extension = True
    start = sc.frame_start if start is None else start
    end = sc.frame_end if end is None else end
    os.makedirs(frames_dir, exist_ok=True)
    for stale in glob.glob(os.path.join(frames_dir, "f_*.partial.png")):
        os.remove(stale)
    want = dict(flight=flight_name, preset=preset, res=list(res or p["res"]), samples=samples or p["samples"],
                mb_steps=p["mb_steps"], depth=r.image_settings.color_depth,
                build_key=flight.host_scene().get(KEY_PROP), fps=r.fps,
                frame_start=sc.frame_start, frame_end=sc.frame_end)
    log = _load_log(frames_dir, want, force_mixed)
    todo = [f for f in range(start, end + 1) if not os.path.exists(frame_path(frames_dir, f))]
    print(f"film: {preset} {want['res'][0]}x{want['res'][1]} {want['samples']} samples, frames "
          f"{start}..{end}: {end - start + 1 - len(todo)} done, {len(todo)} to render", flush=True)
    session = []
    for i, f in enumerate(todo):
        sc.frame_set(f)
        part = frame_path(frames_dir, f, partial=True)
        r.filepath = part
        t0 = time.time()
        bpy.ops.render.render(write_still=True)
        if not os.path.exists(part):
            raise RuntimeError(f"film: frame {f} did not write {part}")
        os.replace(part, frame_path(frames_dir, f))
        secs = time.time() - t0
        session.append(secs)
        log["times"][str(f)] = round(secs, 2)
        done = sorted(int(k) for k in log["times"] if os.path.exists(frame_path(frames_dir, int(k))))
        log["frames_done"] = len(done)
        log["avg_s_per_frame"] = round(sum(log["times"][str(k)] for k in done) / max(1, len(done)), 2)
        _write_json(os.path.join(frames_dir, LOG), log)
        warm = session[1:] or session       # the first render of a session compiles shaders
        eta = sum(warm) / len(warm) * (len(todo) - i - 1)
        print(f"film: frame {f} {secs:.1f}s [{i + 1}/{len(todo)}] eta {_fmt(eta)}", flush=True)
    return log


def _parse(argv):
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    ap = argparse.ArgumentParser(prog="film.py")
    ap.add_argument("--world", required=True, help="world dump JSON (godot --dump-world)")
    ap.add_argument("--preset", default="preview_1080", help="output.PRESETS name")
    ap.add_argument("--flight", default="town_pass", help="which flight (flights.NAMES); frames dirs are per flight")
    ap.add_argument("--frames-dir", default=None, help="where f_####.png + film.json go")
    ap.add_argument("--playblast", default=None, metavar="MP4",
                    help="instead: the fast Workbench preview of the flight to this .mp4 (render.playblast)")
    ap.add_argument("--step", type=int, default=1, help="--playblast: every Nth frame (at fps / N)")
    ap.add_argument("--start", type=int, default=None)
    ap.add_argument("--end", type=int, default=None)
    ap.add_argument("--blend", default=DEFAULT_BLEND, help="the built town, reused if current")
    ap.add_argument("--rebuild", action="store_true", help="build even if --blend is current")
    ap.add_argument("--res", default=None, help="override the preset's size, e.g. 1920x1080 (checks)")
    ap.add_argument("--samples", type=int, default=None, help="override the preset's samples (checks)")
    ap.add_argument("--depth", type=int, choices=(8, 16), default=None, help="override the PNG bit depth")
    ap.add_argument("--force-mixed", action="store_true", help="resume a dir started with other settings")
    return ap.parse_args(argv)


def main():
    args = _parse(sys.argv)
    ensure_scene(os.path.abspath(args.world), os.path.abspath(args.blend), args.rebuild)
    res = tuple(int(v) for v in args.res.split("x")) if args.res else None
    t0 = time.time()
    if args.playblast:
        import render
        secs = render.playblast(args.flight, os.path.abspath(args.playblast), res or render.PLAYBLAST_RES,
                                args.step)
        print(f"film: playblast {args.flight} -> {args.playblast} in {_fmt(secs)}", flush=True)
        return
    if not args.frames_dir:
        raise SystemExit("film: --frames-dir is required (or --playblast)")
    log = render_frames(os.path.abspath(args.frames_dir), args.preset, args.start, args.end, res,
                        args.samples, args.force_mixed, args.depth, args.flight)
    print(f"film: session {_fmt(time.time() - t0)}; {log.get('frames_done', 0)} frames done, "
          f"avg {log.get('avg_s_per_frame', 0)} s/frame", flush=True)


if __name__ == "__main__":
    main()

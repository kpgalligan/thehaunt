"""Phase 11: encode a film.py frames dir (f_####.png, 30 fps) with ffmpeg. Plain python3.

    python3 tools/flyover/encode.py <frames-dir> <out-dir>/<name> [--ogv-q 8] [--crf 18]
        [--prores] [--only ogv|mp4] [--start N] [--allow-short]

- <name>.ogv: Theora, no audio, 30 fps, yuv420p, BT.601 limited range (the Theora spec's
  colour space; tagged bt470bg), for Godot 4's VideoStreamPlayer (VideoStreamTheora).
  `-q:v` (0-10) is libtheora's constant quality; 8 keeps the dusk sky clean at 1080p.
- <name>.mp4: H.264 (libx264, yuv420p, CRF 18, preset slow, +faststart), BT.709 tagged:
  the review / share copy.
- --prores: <name>.mov, ProRes 422 HQ 10-bit (prores_ks profile 3): the master for the
  4K final (16-bit frames keep their depth to 10 bits).
- The frames must be contiguous from --start (default: the first) to the log's
  frame_end (film.json): ffmpeg's image2 stops silently at a gap, so a gap is an
  error; --allow-short encodes a partial run (test clips).
Each output is written to <out>.partial.<ext> and renamed, then ffprobe'd and fully
decoded to null (the check that it plays).
"""

import argparse
import glob
import json
import os
import re
import subprocess
import sys

FPS = 30


def frames(frames_dir):
    got = sorted(int(m.group(1)) for p in glob.glob(os.path.join(frames_dir, "f_*.png"))
                 if (m := re.fullmatch(r"f_(\d{4})\.png", os.path.basename(p))))
    if not got:
        raise SystemExit(f"encode: no f_####.png in {frames_dir}")
    return got


def check_run(frames_dir, start, allow_short):
    got = frames(frames_dir)
    start = got[0] if start is None else start
    end = None
    log = os.path.join(frames_dir, "film.json")
    if os.path.exists(log):
        with open(log) as fh:
            end = json.load(fh).get("frame_end")
    have = set(got)
    n = 0
    while start + n in have:
        n += 1
    last = start + n - 1
    if n == 0:
        raise SystemExit(f"encode: frame {start} is missing")
    missing = [f for f in range(start, (end or got[-1]) + 1) if f not in have]
    if missing and not allow_short:
        raise SystemExit(f"encode: {len(missing)} frames missing in {start}..{end or got[-1]} "
                         f"(first {missing[:5]}); render them or pass --allow-short")
    return start, last


def _run(cmd):
    print("encode: " + " ".join(cmd), flush=True)
    subprocess.run(cmd, check=True)


def encode(frames_dir, out_base, start, count, kind, ogv_q, crf):
    src = ["-framerate", str(FPS), "-start_number", str(start), "-i", os.path.join(frames_dir, "f_%04d.png"),
           "-frames:v", str(count), "-an"]
    if kind == "ogv":
        ext = "ogv"
        codec = ["-vf", "scale=out_color_matrix=bt601:out_range=tv:flags=accurate_rnd+full_chroma_int,"
                        "format=yuv420p",
                 "-c:v", "libtheora", "-q:v", str(ogv_q), "-g", str(FPS * 2),
                 "-colorspace", "bt470bg", "-color_primaries", "bt709", "-color_trc", "bt709",
                 "-color_range", "tv"]
    elif kind == "mp4":
        ext = "mp4"
        codec = ["-vf", "scale=out_color_matrix=bt709:out_range=tv:flags=accurate_rnd+full_chroma_int,"
                        "format=yuv420p",
                 "-c:v", "libx264", "-preset", "slow", "-crf", str(crf), "-pix_fmt", "yuv420p",
                 "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709",
                 "-color_range", "tv", "-movflags", "+faststart"]
    elif kind == "prores":
        ext = "mov"
        codec = ["-vf", "scale=out_color_matrix=bt709:out_range=tv:flags=accurate_rnd+full_chroma_int,"
                        "format=yuv422p10le",
                 "-c:v", "prores_ks", "-profile:v", "3", "-vendor", "apl0",
                 "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709",
                 "-color_range", "tv"]
    else:
        raise ValueError(kind)
    out = f"{out_base}.{ext}"
    part = f"{out_base}.partial.{ext}"
    _run(["ffmpeg", "-hide_banner", "-loglevel", "warning", "-stats", "-y", *src, *codec, part])
    os.replace(part, out)
    verify(out, count)
    return out


def verify(path, count):
    probe = json.loads(subprocess.run(
        ["ffprobe", "-v", "error", "-count_packets", "-select_streams", "v:0", "-show_entries",
         "stream=codec_name,width,height,pix_fmt,r_frame_rate,nb_read_packets,color_space:format=duration,size,"
         "bit_rate", "-of", "json", path], check=True, capture_output=True, text=True).stdout)
    s, fmt = probe["streams"][0], probe["format"]
    n = int(s["nb_read_packets"])      # Theora codes a repeated frame as an empty packet (no decoded frame)
    subprocess.run(["ffmpeg", "-v", "error", "-xerror", "-i", path, "-f", "null", "-"], check=True)
    mb = int(fmt["size"]) / 1e6
    print(f"encode: {path}: {s['codec_name']} {s['width']}x{s['height']} {s['pix_fmt']} "
          f"{s.get('color_space', '?')} {s['r_frame_rate']} {n} frames {float(fmt['duration']):.2f}s "
          f"{mb:.1f} MB ({int(fmt['bit_rate']) / 1e6:.1f} Mb/s); decodes clean", flush=True)
    if n != count:
        raise SystemExit(f"encode: {path} has {n} frames, expected {count}")


def main():
    ap = argparse.ArgumentParser(prog="encode.py")
    ap.add_argument("frames_dir")
    ap.add_argument("out_base", help="output path without extension, e.g. tools/flyover/out/flyover_1080")
    ap.add_argument("--ogv-q", type=int, default=8, help="libtheora quality 0-10")
    ap.add_argument("--crf", type=int, default=18, help="libx264 CRF")
    ap.add_argument("--prores", action="store_true", help="also a ProRes 422 HQ .mov master")
    ap.add_argument("--only", choices=("ogv", "mp4"), default=None)
    ap.add_argument("--start", type=int, default=None)
    ap.add_argument("--allow-short", action="store_true", help="encode a partial run (test clips)")
    a = ap.parse_args()
    start, last = check_run(a.frames_dir, a.start, a.allow_short)
    count = last - start + 1
    print(f"encode: frames {start}..{last} ({count}, {count / FPS:.2f} s)", flush=True)
    os.makedirs(os.path.dirname(os.path.abspath(a.out_base)), exist_ok=True)
    kinds = [a.only] if a.only else ["ogv", "mp4"]
    if a.prores:
        kinds.append("prores")
    for kind in kinds:
        encode(a.frames_dir, a.out_base, start, count, kind, a.ogv_q, a.crf)


if __name__ == "__main__":
    sys.exit(main())

"""The paved road: one swept mesh along the whole centreline (Terrain.road_samples),
west road-out -> the six strip maps -> east road-out.

Cross-section (left side mirrored right; v = metres left of travel, z relative to
the sample's reference level zr, which is the verge / kerb top):

  skirt  under the kerb (town, hidden by the verge) / an embankment (rural)
  o0     kerb top outer, on the road rows' edge  | rural: gravel shoulder outer
  o1     kerb top inner                          | rural: mid shoulder
  o2     kerb foot (gutter, KERB_H_M down)       | rural: asphalt edge
  o3     gutter / asphalt joint                  |
  c      crown (CROWN_M above the gutter)

`urban` (1 in town, fading out over config.URBAN_FADE_M) morphs kerb -> shoulder;
materials read it as a point attribute. At a kerb cut the kerb top slopes down to a
CUT_LIP_M lip over the gutter (the driveway meets it flush at the verge), tapered over
CUT_TAPER_M. UV map RoadUV: u = arc length (m), v = lateral offset (m).
"""

import math

import numpy as np

import config
import materials
import scene

BANDS = (0, 1, 1, 2, 3)   # material slot per band, outside-in: skirt, kerb top, kerb face, gutter, asphalt


def _lerp(a, b, t):
    return a + (b - a) * t


def _side(u, cut):
    """Outside-in profile points (v, dz) for one side at urban factor u, cut 0..1."""
    half, kw, kh, gw = config.ROAD_HALF_M, config.KERB_W_M, config.KERB_H_M, config.GUTTER_W_M
    sh = config.SHOULDER_W_M
    # town
    top_in = _lerp(0.0, -kh + config.CUT_LIP_M, cut)
    urban = [(half, -config.SKIRT_URBAN_M), (half, 0.0), (half - kw, top_in),
             (half - kw, -kh), (half - kw - gw, -kh + 0.01)]
    # rural
    so, sd = config.SKIRT_RURAL_M
    rural = [(half + sh + so, -sd), (half + sh, -0.1), (half + sh * 0.5, -0.07),
             (half, -0.03), (half - 0.25, -0.02)]
    return [(_lerp(r[0], t[0], u), _lerp(r[1], t[1], u)) for r, t in zip(rural, urban)]


def _crown(u):
    return _lerp(config.CROWN_M * 0.8, -config.KERB_H_M + 0.01 + config.CROWN_M, u)


def build(terrain, col, mats):
    samples = terrain.road_samples()
    n = len(samples)
    pts = np.array([(p["x"], p["y"]) for p in samples])
    tan = np.gradient(pts, axis=0)
    tan /= np.linalg.norm(tan, axis=1)[:, None]
    left = np.stack([-tan[:, 1], tan[:, 0]], 1)
    verts, uvv, urb = [], [], []
    for k, p in enumerate(samples):
        L = _side(p["urban"], p["cut_n"])
        R = _side(p["urban"], p["cut_s"])
        prof = [(v, dz) for v, dz in L] + [(0.0, _crown(p["urban"]))] + [(-v, dz) for v, dz in reversed(R)]
        for v, dz in prof:
            x, y = pts[k] + left[k] * v
            verts.append((x, y, p["z"] + dz))
            uvv.append(v)
            urb.append(p["urban"])
    m = len(prof)
    verts = np.array(verts)
    quads, mat_idx, loop_uv = [], [], []
    s = np.array([p["s"] for p in samples])
    uvv = np.array(uvv)
    band_mat = list(BANDS) + list(reversed(BANDS))
    for k in range(n - 1):
        for b in range(m - 1):
            a0, a1 = k * m + b, k * m + b + 1          # this sample, left -> right
            b0, b1 = a0 + m, a1 + m                    # next sample
            # CCW from +Z (travel +u, left +v): back-left, back-right, front-right, front-left
            quads.append((a0, a1, b1, b0))
            mat_idx.append(band_mat[b])
            for vi, si in ((a0, k), (a1, k), (b1, k + 1), (b0, k + 1)):
                loop_uv.append((s[si], uvv[vi]))
    slots = [mats.get_cached(name) for name in materials.ROAD_SLOTS]
    me = scene.mesh_from_arrays("Road_Paved", verts, np.array(quads), np.array(mat_idx), slots,
                                attrs={"urban": np.array(urb)}, uvs={"RoadUV": np.array(loop_uv)})
    ob = scene.new_object("Road_Paved", me, col, kind="road")
    ob["length_m"] = float(s[-1])
    return ob


def road_width_ok():
    """Sanity: the town profile spans exactly the two road rows."""
    L = _side(1.0, 0.0)
    return math.isclose(L[1][0], config.ROAD_HALF_M)

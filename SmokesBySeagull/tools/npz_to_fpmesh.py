"""Baked npz (Blender metres, Z up) -> .fpmesh (FoodPack format) for the game.
World mesh: lies at rest, centred over the origin in X/Z, bottom on y=0.
Held mesh (cigarette, cigars, pipes): mouth end at the origin; cigarette/cigar axis along Unity +Z (lit end at +Z);
pipe stem along +Z towards the bowl, bowl up (+Y).
python3 npz_to_fpmesh.py <bakedir> <outdir>"""
import sys, os, struct, math
import numpy as np

B, OUT = sys.argv[1], sys.argv[2]


def rx(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]], np.float32)


def ry(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]], np.float32)


def rz(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]], np.float32)


def write(path, v, n, uv, tris):
    # Blender (x, y, z) -> Unity (-x, z, -y); mirroring flips winding back with (a, c, b)
    U = np.stack([-v[:, 0], v[:, 2], -v[:, 1]], 1)
    N = np.stack([-n[:, 0], n[:, 2], -n[:, 1]], 1)
    t = tris[:, [0, 2, 1]].reshape(-1)
    with open(path, 'wb') as f:
        f.write(struct.pack('<ii', len(U), len(t)))
        f.write(U.astype('<f4').tobytes())
        f.write(N.astype('<f4').tobytes())
        f.write(uv.astype('<f4').tobytes())
        f.write(t.astype('<i4').tobytes())
    lo, hi = U.min(0), U.max(0)
    print(os.path.basename(path), len(U), 'verts', len(t) // 3, 'tris, unity size', np.round(hi - lo, 4), 'min', np.round(lo, 4))


def load(name):
    d = np.load(os.path.join(B, name + '.npz'))
    return d['verts'].astype(np.float32), d['normals'].astype(np.float32), d['uvs'].astype(np.float32), d['tris']


def rest(name, R, out=None):
    v, n, uv, t = load(name)
    v, n = v @ R.T, n @ R.T
    lo, hi = v.min(0), v.max(0)
    v = v - np.array([(lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, lo[2]], np.float32)
    write(os.path.join(OUT, (out or name) + '.fpmesh'), v, n, uv, t)


def held_axis(name, mouth_at_zero, out):
    # Blender axis is +Z; Unity +Z is Blender -Y. Rotate so that the lit end ends at Blender -Y, mouth at origin.
    v, n, uv, t = load(name)
    if not mouth_at_zero:  # mouth at +Z (cigar cap): flip
        R = rx(math.pi)
        v, n = v @ R.T, n @ R.T
        v[:, 2] -= v[:, 2].min()
    else:
        v[:, 2] -= v[:, 2].min()
    R = rx(math.pi / 2)  # Blender +Z -> -Y = Unity +Z
    v, n = v @ R.T, n @ R.T
    write(os.path.join(OUT, out + '.fpmesh'), v, n, uv, t)


def held_pipe(name, out):
    v, n, uv, t = load(name)
    # mouthpiece = far -X end; its centre at the origin; stem towards +X (Blender) -> towards bowl
    x0 = v[:, 0].min()
    tip = v[v[:, 0] < x0 + 0.002]
    c = tip.mean(0)
    v = v - np.array([x0, c[1], c[2]], np.float32)
    # Blender +X (towards the bowl) -> Unity +Z: rotate about Blender Z by -90deg (x -> -y)
    R = rz(-math.pi / 2)
    v, n = v @ R.T, n @ R.T
    write(os.path.join(OUT, out + '.fpmesh'), v, n, uv, t)


os.makedirs(OUT, exist_ok=True)
flat = rz(math.pi) @ rx(-math.pi / 2)  # front face (-Y) up, its top towards Unity +Z (reads upright looking forward)
along = ry(math.pi / 2)        # cylinder axis Z -> along X
for b in ['chapgirl', 'milston', 'marlbrrro', 'parliamint', 'bent', 'caramel']:
    rest('pack_' + b, flat)
rest('pouch_captainblock', flat)
for _pk in ('pack_wide', 'pack_slim'):
    if os.path.exists(os.path.join(B, _pk + '.npz')):
        rest(_pk, flat)
rest('lighter_zappo', flat)
rest('cutter_zikar', np.eye(3, dtype=np.float32))
for nm in ['cigarette', 'cigar_cohibra', 'cigar_montecrusto', 'cigar_cut_cohibra', 'cigar_cut_montecrusto', 'tube_cohibra', 'tube_montecrusto']:
    rest(nm, along)
for p in ['billiard', 'bent', 'churchwarden', 'corncob']:
    rest('pipe_' + p, np.eye(3, dtype=np.float32))
    held_pipe('pipe_' + p, 'pipe_%s_held' % p)
held_axis('cigarette', True, 'cigarette_held')
held_axis('cigar_cohibra', False, 'cigar_cohibra_held')
held_axis('cigar_montecrusto', False, 'cigar_montecrusto_held')
held_axis('cigar_cut_cohibra', False, 'cigar_cut_cohibra_held')
held_axis('cigar_cut_montecrusto', False, 'cigar_cut_montecrusto_held')

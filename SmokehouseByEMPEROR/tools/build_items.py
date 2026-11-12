"""Final smoked item meshes (+ butcher's twine baked in) and textures with a twine patch.
Reads game_models/*.obj (prefab-root space, x mirrored, as UnityPy writes) and tex/*_{hot,cold}.png.
Writes mod/SmokehouseByEMPEROR/meshes/<Id>.fpmesh, textures/<Id>.png and bake/<Id>.npz (Blender space, for icons).
python3 build_items.py"""
import os, struct, math
import numpy as np
from PIL import Image
R = '/mnt/project-files/smokehouse/'
G, T, MOD, BAKE = R + 'game_models/', R + 'tex/', R + 'mod/SmokehouseByEMPEROR/', R + 'bake/'
# id, cooked obj, raw obj (None: fish or no raw mesh), texture base, is fish
SPECIES = [
    ('Deer', 'GEAR_CookedMeatDeer__RawMeatMesh', 'GEAR_RawMeatDeer__RawMeatMesh', 'GEAR_MeatVenison', False),
    ('Rabbit', 'GEAR_CookedMeatRabbit__RawMeatMesh', 'GEAR_RawMeatRabbit__RawMeatMesh', 'GEAR_MeatRabbit', False),
    ('Wolf', 'GEAR_CookedMeatWolf__RawMeatMesh', 'GEAR_RawMeatWolf__RawMeatMesh', 'GEAR_MeatWolf', False),
    ('Bear', 'GEAR_CookedMeatBear__CookedMeatMesh', 'GEAR_RawMeatBear__RawMeatMesh', 'GEAR_MeatBear', False),
    ('Moose', 'GEAR_CookedMeatMoose__CookedMeatMesh', 'GEAR_RawMeatMoose__RawMeatMesh', 'GEAR_MeatMoose', False),
    ('Cougar', 'GEAR_CookedMeatCougar__CookedMeatMesh', None, 'GEAR_MeatCougar', False),
    ('Ptarmigan', 'GEAR_CookedMeatPtarmigan__OBJ_CookedMeatPtarmigan', 'GEAR_RawMeatPtarmigan__OBJ_RawMeatPtarmigan', 'GEAR_MeatPtarmigan', False),
    ('Coho', 'GEAR_CookedCohoSalmon__OBJ_CookedLakeWhiteFish', None, 'GEAR_CookedCohoSalmon', True),
    ('WhiteFish', 'GEAR_CookedLakeWhiteFish__OBJ_CookedLakeWhiteFish', None, 'GEAR_CookedLakeWhiteFish', True),
    ('Trout', 'GEAR_CookedRainbowTrout__OBJ_CookedLakeWhiteFish', None, 'GEAR_CookedRainbowTrout', True),
    ('Bass', 'GEAR_CookedSmallMouthBass__OBJ_CookedLakeWhiteFish', None, 'GEAR_CookedSmallMouthBass', True),
    ('Burbot', 'GEAR_CookedBurbot__GEAR_CookedLakeBurbot', None, 'GEAR_CookedLakeBurbot', True),
    ('Goldeye', 'GEAR_CookedGoldeye__GEAR_CookedLakeGoldeye', None, 'GEAR_CookedLakeGoldeye', True),
    ('Rockfish', 'GEAR_CookedRockfish__GEAR_CookedCopperFish', None, 'GEAR_CookedCopperFish', True),
    ('IrishLord', 'GEAR_CookedRedIrishLord__OBJ_CookedRedIrishLord', None, 'GEAR_CookedRedIrishLord', True),
]

def read_obj(name):
    v, vt, vn, f = [], [], [], []
    for l in open(G + name + '.obj'):
        p = l.split()
        if not p: continue
        if p[0] == 'v': v.append([float(x) for x in p[1:4]])
        elif p[0] == 'vt': vt.append([float(x) for x in p[1:3]])
        elif p[0] == 'vn': vn.append([float(x) for x in p[1:4]])
        elif p[0] == 'f': f.append([int(q.split('/')[0]) - 1 for q in p[1:4]])
    v, vn, vt, f = np.array(v), np.array(vn), np.array(vt), np.array(f)
    assert len(v) == len(vt) == len(vn), name
    v[:, 0] *= -1; vn[:, 0] *= -1          # back to Unity space
    f = f[:, ::-1]                          # and its winding
    return v, vn, vt, f

def hull(P):
    P = sorted(set(map(tuple, np.round(P, 5))))
    def cr(o, a, b): return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo = []
    for p in P:
        while len(lo) >= 2 and cr(lo[-2], lo[-1], p) <= 0: lo.pop()
        lo.append(p)
    up = []
    for p in reversed(P):
        while len(up) >= 2 and cr(up[-2], up[-1], p) <= 0: up.pop()
        up.append(p)
    return lo[:-1] + up[:-1]

def section(V, F, axis, pos):
    pts = []
    for a, b in ((0, 1), (1, 2), (2, 0)):
        A, B = V[F[:, a]], V[F[:, b]]
        da, db = A[:, axis] - pos, B[:, axis] - pos
        m = (da * db) < 0
        t = da[m] / (da[m] - db[m])
        pts.append(A[m] + (B[m] - A[m]) * t[:, None])
    return np.concatenate(pts) if pts else np.zeros((0, 3))

class Extra:
    def __init__(self): self.v, self.n, self.f, self.t = [], [], [], []; self.count = 0
    def add(self, v, n, f, along):
        self.v.append(v); self.n.append(n); self.f.append(f + self.count); self.t.append(along); self.count += len(v)

def tube(ex, pts, closed, r, sides=6):
    P = np.array(pts, float); n = len(P)
    if closed: tan = np.roll(P, -1, 0) - np.roll(P, 1, 0)
    else:
        tan = np.zeros_like(P); tan[1:-1] = P[2:] - P[:-2]; tan[0] = P[1] - P[0]; tan[-1] = P[-1] - P[-2]
    tan /= np.maximum(np.linalg.norm(tan, axis=1, keepdims=True), 1e-9)
    ref = np.array([0, 1, 0.]) if abs(tan[0][1]) < 0.9 else np.array([1, 0, 0.])
    nrm = np.cross(tan[0], ref); nrm /= np.linalg.norm(nrm)
    V, N, along = [], [], []
    for i in range(n):
        nrm = nrm - tan[i] * np.dot(nrm, tan[i]); nrm /= max(np.linalg.norm(nrm), 1e-9)
        b = np.cross(tan[i], nrm)
        for k in range(sides):
            a = 2 * math.pi * k / sides; d = math.cos(a) * nrm + math.sin(a) * b
            V.append(P[i] + d * r); N.append(d); along.append(i / max(n - 1, 1))
    F = []
    segs = n if closed else n - 1
    for i in range(segs):
        j = (i + 1) % n
        for k in range(sides):
            k2 = (k + 1) % sides
            F.append([i * sides + k, j * sides + k, j * sides + k2]); F.append([i * sides + k, j * sides + k2, i * sides + k2])
    ex.add(np.array(V), np.array(N), np.array(F), np.array(along))

def sphere(ex, c, r, seg=8, rings=5):
    V, N = [], []
    for i in range(rings + 1):
        th = math.pi * i / rings
        for k in range(seg):
            ph = 2 * math.pi * k / seg; d = np.array([math.sin(th) * math.cos(ph), math.cos(th), math.sin(th) * math.sin(ph)])
            V.append(np.array(c) + d * r); N.append(d)
    F = []
    for i in range(rings):
        for k in range(seg):
            k2 = (k + 1) % seg; a, b, c2, d2 = i * seg + k, i * seg + k2, (i + 1) * seg + k, (i + 1) * seg + k2
            F.append([a, c2, d2]); F.append([a, d2, b])
    ex.add(np.array(V), np.array(N), np.array(F), np.full(len(V), 0.5))

# Unity space: y up, the piece lies in x/z. All sizes below are for a piece 0.30 long (as in the approved previews).
def twine_meat(V, F, ex):
    mn, mx = V.min(0), V.max(0)
    ax = 0 if (mx[0] - mn[0]) > (mx[2] - mn[2]) else 2; ot = 2 - ax
    L = mx[ax] - mn[ax]
    def put(a, o, y, apos=None):
        q = [0, 0, 0]; q[ax] = a; q[ot] = o; q[1] = y; return q
    for t in (0.2, 0.4, 0.6, 0.8):
        pos = mn[ax] + L * t; pts = section(V, F, ax, pos)
        if len(pts) < 3: continue
        h = hull([(p[ot], p[1]) for p in pts]); c = np.mean(h, 0)
        P = []
        for a, b in h:
            d = np.array([a, b]) - c; d = d / max(np.linalg.norm(d), 1e-6) * 0.0015
            P.append(put(pos, a + d[0], b + d[1]))
        tube(ex, P, True, 0.0016)
    mid = (mn[ot] + mx[ot]) / 2
    pts = section(V, F, ot, mid)
    if len(pts) >= 3:
        h = hull([(p[ax], p[1]) for p in pts]); c = np.mean(h, 0); P = []
        for a, b in h:
            d = np.array([a, b]) - c; d = d / max(np.linalg.norm(d), 1e-6) * 0.0025
            P.append(put(a + d[0], mid, b + d[1]))
        tube(ex, P, True, 0.0016)
        k = int(np.argmax([p[ax] for p in P])); pe, ze = P[k][ax], P[k][1]; Q = []
        for i in range(21):
            a = math.pi * 2 * i / 20
            Q.append(put(pe + 0.034 * (1 - math.cos(a)) / 2, mid + 0.011 * math.sin(a), ze + 0.014 * (1 - math.cos(a)) / 2))
        tube(ex, Q, False, 0.0016)
        sphere(ex, put(pe + 0.001, mid, ze), 0.0035)

def twine_fish(V, F, ex):
    mn, mx = V.min(0), V.max(0)
    ax = 0 if (mx[0] - mn[0]) > (mx[2] - mn[2]) else 2; ot = 2 - ax; L = mx[ax] - mn[ax]
    def slab(t0, t1):
        m = (V[:, ax] >= mn[ax] + L * t0) & (V[:, ax] <= mn[ax] + L * t1); return V[m]
    a, b = slab(0, 0.12), slab(0.88, 1.0)
    wa = np.ptp(a[:, ot]) if len(a) else 9; wb = np.ptp(b[:, ot]) if len(b) else 9
    end = 0 if wa < wb else 1; sgn = -1 if end == 0 else 1
    pos = mn[ax] + L * (0.09 if end == 0 else 0.91)
    pts = section(V, F, ax, pos)
    if len(pts) < 3: return
    def put(a, o, y):
        q = [0, 0, 0]; q[ax] = a; q[ot] = o; q[1] = y; return q
    h = hull([(p[ot], p[1]) for p in pts]); c = np.mean(h, 0); P = []
    for u, w in h:
        d = np.array([u, w]) - c; d = d / max(np.linalg.norm(d), 1e-6) * 0.0016
        P.append(put(pos, u + d[0], w + d[1]))
    tube(ex, P, True, 0.0016)
    tube(ex, [put(p[ax] + 0.004 * sgn, p[ot], p[1]) for p in P], True, 0.0016)
    mid = float(c[0]); ze = max(p[1] for p in P); Q = []
    for i in range(21):
        a = math.pi * 2 * i / 20
        Q.append(put(pos + sgn * (0.002 + 0.05 * (1 - math.cos(a)) / 2), mid + 0.013 * math.sin(a), ze + 0.006 * (1 - math.cos(a)) / 2))
    tube(ex, Q, False, 0.0016)
    sphere(ex, put(pos + sgn * 0.002, mid, ze + 0.001), 0.0035)

def outward(V, N, F):
    fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    return float(np.sum(fn * (N[F[:, 0]] + N[F[:, 1]] + N[F[:, 2]])))

def free_patch(UV, F, cells=64, size=4):
    occ = np.zeros((cells, cells), bool)
    w = np.random.RandomState(1).dirichlet([1, 1, 1], 400)
    for tri in F:
        p = w @ UV[tri]
        ij = np.clip((p % 1.0 * cells).astype(int), 0, cells - 1); occ[ij[:, 1], ij[:, 0]] = True
        for k in range(3):
            for s in np.linspace(0, 1, 40):
                q = (UV[tri[k]] * (1 - s) + UV[tri[(k + 1) % 3]] * s) % 1.0
                occ[min(int(q[1] * cells), cells - 1), min(int(q[0] * cells), cells - 1)] = True
    # grow by one cell: mip levels bleed
    g = occ.copy()
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1): g |= np.roll(np.roll(occ, dy, 0), dx, 1)
    best = None
    for y in range(1, cells - size - 1):
        for x in range(1, cells - size - 1):
            if not g[y:y + size, x:x + size].any():
                # the freest neighbourhood wins
                score = g[max(0, y - 4):y + size + 4, max(0, x - 4):x + size + 4].sum()
                if best is None or score < best[0]: best = (score, x, y)
    if best is None: raise RuntimeError('no free texture patch')
    return best[1] / cells, best[2] / cells, size / cells      # u0, v0, extent (uv, v from the bottom)

def write_fpmesh(path, V, N, UV, F):
    t = F.reshape(-1)
    with open(path, 'wb') as f:
        f.write(struct.pack('<ii', len(V), len(t)))
        f.write(V.astype('<f4').tobytes()); f.write(N.astype('<f4').tobytes()); f.write(UV.astype('<f4').tobytes()); f.write(t.astype('<i4').tobytes())

def build(sid, cooked, raw, tex, fish, mode):
    use_raw = mode == 'cold' and raw is not None
    V, N, UV, F = read_obj(raw if use_raw else cooked)
    if outward(V, N, F) < 0: F = F[:, ::-1]
    if mode == 'cold' and not fish:
        mn, mx = V.min(0), V.max(0); c = np.array([(mn[0] + mx[0]) / 2, mn[1], (mn[2] + mx[2]) / 2])
        V = (V - c) * 0.9 + c
        if use_raw:
            # where the cooked piece lies in its prefab: the same middle and floor
            Vc = read_obj(cooked)[0]; cm, cx = Vc.min(0), Vc.max(0); mn, mx = V.min(0), V.max(0)
            V = V + np.array([(cm[0] + cx[0]) / 2 - (mn[0] + mx[0]) / 2, cm[1] - mn[1], (cm[2] + cx[2]) / 2 - (mn[2] + mx[2]) / 2])
    size = (V.max(0) - V.min(0)).max(); s = 0.30 / size
    ex = Extra()
    (twine_fish if fish else twine_meat)(V * s, F, ex)
    u0, v0, e = free_patch(UV, F)
    EV, EN, EF, EA = np.concatenate(ex.v) / s, np.concatenate(ex.n), np.concatenate(ex.f), np.concatenate(ex.t)
    if outward(EV, EN, EF) < 0: EF = EF[:, ::-1]
    # fibres run along the twine: u across the patch by the vertex's place along it
    EUV = np.stack([u0 + e * (0.25 + 0.5 * ((EA * 7) % 1.0)), np.full(len(EV), v0 + e * 0.5)], 1)
    V2, N2, UV2, F2 = np.concatenate([V, EV]), np.concatenate([N, EN]), np.concatenate([UV, EUV]), np.concatenate([F, EF + len(V)])
    name = f'{mode.capitalize()}{sid}'
    write_fpmesh(MOD + f'meshes/{name}.fpmesh', V2, N2, UV2, F2)
    im = Image.open(T + f'{tex}_{mode}.png').convert('RGB'); W, H = im.size; px = np.array(im).astype(np.float32)
    x0, x1 = int(u0 * W), int((u0 + e) * W); y1, y0 = H - int(v0 * H), H - int((v0 + e) * H)
    rs = np.random.RandomState(7); h, w = y1 - y0, x1 - x0
    base = np.array([195, 175, 139], np.float32) * (0.92 if mode == 'cold' else 0.86)   # a little smoked
    stripes = (np.sin(np.arange(w) * 1.9)[None, :] * 0.06 + rs.normal(0, 0.035, (h, w)))
    px[y0:y1, x0:x1] = np.clip(base[None, None, :] * (1 + stripes[:, :, None]), 0, 255)
    Image.fromarray(px.astype(np.uint8)).save(MOD + f'textures/{name}.png', optimize=True)
    # Blender space for the icon render: (x, y, z) = (-ux, -uz, uy)
    np.savez(BAKE + name + '.npz', verts=np.stack([-V2[:, 0], -V2[:, 2], V2[:, 1]], 1), normals=np.stack([-N2[:, 0], -N2[:, 2], N2[:, 1]], 1), uvs=UV2, tris=F2[:, ::-1])
    sz = V2.max(0) - V2.min(0)
    print(f'{name}: {len(V2)} verts {len(F2)} tris, size {np.round(sz, 3)}, min {np.round(V2.min(0), 3)}, patch uv {u0:.3f},{v0:.3f}')

for sid, cooked, raw, tex, fish in SPECIES:
    for mode in ('hot', 'cold'):
        build(sid, cooked, raw, tex, fish, mode)

"""TeaCoffeeByEMPEROR models, all made here (no game input).
Built Z-up right-handed in metres (Blender space), base on z=0, front facing -Y. Written to .fpmesh in Unity space like the other mods:
(x,y,z) -> (-x, z, -y), triangle winding swapped. Also cache/<name>.npz for the preview/icon renderer, and layout.json:
for each model, where each of its parts sits on its 1024x1024 texture (pixel rectangles, y down) for tools/labels.py.
python3 models.py <out_dir>"""
import sys, os, struct, math, json
import numpy as np

OUT = sys.argv[1] if len(sys.argv) > 1 else 'out'
for d in ('meshes', 'cache'):
    os.makedirs(os.path.join(OUT, d), exist_ok=True)
TEX = 1024.0
LAYOUT = {}


class M:
    def __init__(s):
        s.v, s.n, s.uv, s.t = [], [], [], []

    def add(s, v, n, uv, t):
        base = sum(len(a) for a in s.v)
        s.v.append(np.asarray(v, np.float32)); s.n.append(np.asarray(n, np.float32)); s.uv.append(np.asarray(uv, np.float32))
        s.t.append(np.asarray(t, np.int64).reshape(-1, 3) + base)
        return s

    def merge(s, o):
        V, N, U, T = o.arrays()
        return s.add(V, N, U, T)

    def arrays(s):
        return np.concatenate(s.v), np.concatenate(s.n), np.concatenate(s.uv), np.concatenate(s.t)


def px(x, y):
    """pixel (y down) -> uv"""
    return (x / TEX, 1 - y / TEX)


def grid_tris(rows, cols, wrap=False):
    t = []
    cc = cols if wrap else cols - 1
    for i in range(rows - 1):
        for j in range(cc):
            a = i * cols + j; b = i * cols + (j + 1) % cols; c = (i + 1) * cols + j; d = (i + 1) * cols + (j + 1) % cols
            t += [(a, b, c), (b, d, c)]
    return np.array(t)


def normals_of(P, T):
    N = np.zeros_like(P)
    a, b, c = P[T[:, 0]], P[T[:, 1]], P[T[:, 2]]
    fn = np.cross(b - a, c - a)
    for k in range(3): np.add.at(N, T[:, k], fn)
    return N / np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-12)


def welded(V, N, tol=2e-5):
    """one normal for all the copies of a vertex that sit in the same place (the faces of a box each have their own copies):
    without it a push along the normal opens the edges (seen in game 11-14 as see-through seams)."""
    key = np.round(np.asarray(V) / tol).astype(np.int64)
    _, inv = np.unique(key, axis=0, return_inverse=True)
    inv = inv.reshape(-1)
    S = np.zeros((inv.max() + 1, 3)); np.add.at(S, inv, N)
    S /= np.maximum(np.linalg.norm(S, axis=1, keepdims=True), 1e-12)
    return S[inv]


def surface(P, UV, rows, cols, flip=False):
    """rows x cols grid; a seam column is duplicated by the caller (u 0 and 1), so no wrap here. Normal = cross(d/dcol, d/drow) unless flip."""
    P = np.asarray(P, np.float64); T = grid_tris(rows, cols)
    if flip: T = T[:, [0, 2, 1]]
    N = normals_of(P, T)
    return M().add(P, N, UV, T)


def lathe(prof, n, u0, u1, vrows, flip=False):
    """prof: [(r, z)] from bottom to top; vrows: uv v per profile row. Columns go around CCW seen from above with a seam copy.
    Outward normals for a profile going up (flip for inside walls)."""
    P, UV = [], []
    for i, (r, z) in enumerate(prof):
        for j in range(n + 1):
            a = 2 * math.pi * j / n
            P.append((r * math.cos(a + math.pi / 2), r * math.sin(a + math.pi / 2), z))  # u=0.5 faces -Y (front)
            UV.append((u0 + (u1 - u0) * j / n, vrows[i]))
    return surface(P, UV, len(prof), n + 1, flip=not flip)


def disc(r, z, n, cx, cy, rp, up=True, dome=0.0):
    """flat (or slightly domed) disc; uv = a circle on the texture, centre (cx, cy) radius rp in pixels, seen from above with front (-Y) down."""
    rings = 6
    P, UV = [], []
    for k in range(rings + 1):
        f = k / rings
        for j in range(n + 1):
            a = 2 * math.pi * j / n
            x, y = r * f * math.cos(a), r * f * math.sin(a)
            P.append((x, y, z + dome * (1 - f * f)))
            UV.append(px(cx + rp * f * math.cos(a), cy - rp * f * math.sin(a) * (1 if up else -1)))
    m = surface(P, UV, rings + 1, n + 1, flip=up)
    return m


def face(c, du, dv, rect, sub=(1, 1), bulge=0.0, normal=None):
    """quad centre c, edge vectors du (left->right as seen) and dv (bottom->top); rect = pixel (x0, y0, x1, y1) of the picture as seen."""
    c, du, dv = (np.asarray(a, np.float64) for a in (c, du, dv))
    nrm = np.cross(du, dv); nrm /= np.linalg.norm(nrm)
    su, sv = sub
    P, UV = [], []
    x0, y0, x1, y1 = rect
    for i in range(sv + 1):
        fv = i / sv
        for j in range(su + 1):
            fu = j / su
            b = bulge * math.sin(math.pi * fu) * math.sin(math.pi * fv)
            P.append(c + du * (fu - .5) + dv * (fv - .5) + nrm * b)
            UV.append(px(x0 + (x1 - x0) * fu, y1 - (y1 - y0) * fv))
    T = grid_tris(sv + 1, su + 1)
    P = np.array(P)
    N = normals_of(P, T) if bulge else np.repeat(nrm[None], len(P), 0)
    return M().add(P, N, UV, T)


def box_layout(w, d, h, pad=6):
    """unwrap: row 1 left|front|right|back, row 2 top under front, bottom under back. Same pixels per metre everywhere."""
    s = min((TEX - 5 * pad) / (2 * w + 2 * d), (TEX - 3 * pad) / (h + d))
    W, D, H = w * s, d * s, h * s
    x = pad; y = pad
    L = {}
    L['left'] = (x, y, x + D, y + H); x += D + pad
    L['front'] = (x, y, x + W, y + H); fx = x; x += W + pad
    L['right'] = (x, y, x + D, y + H); x += D + pad
    L['back'] = (x, y, x + W, y + H); bx = x
    L['top'] = (fx, y + H + pad, fx + W, y + H + pad + D)
    L['bottom'] = (bx, y + H + pad, bx + W, y + H + pad + D)
    L['scale'] = s
    return {k: (tuple(round(t) for t in v) if isinstance(v, tuple) else v) for k, v in L.items()}


def box(name, w, d, h, sub=(1, 1, 1), bulge=0.0, round_top=0.0):
    L = box_layout(w, d, h)
    m = M()
    su, sd, sh = sub
    m.merge(face((0, -d / 2, h / 2), (w, 0, 0), (0, 0, h), L['front'], (su, sh), bulge))
    m.merge(face((0, d / 2, h / 2), (-w, 0, 0), (0, 0, h), L['back'], (su, sh), bulge))
    m.merge(face((-w / 2, 0, h / 2), (0, -d, 0), (0, 0, h), L['left'], (sd, sh), bulge * .5))
    m.merge(face((w / 2, 0, h / 2), (0, d, 0), (0, 0, h), L['right'], (sd, sh), bulge * .5))
    m.merge(face((0, 0, h), (w, 0, 0), (0, d, 0), L['top'], (su, sd), round_top))
    m.merge(face((0, 0, 0), (w, 0, 0), (0, -d, 0), L['bottom'], (su, sd)))
    LAYOUT[name] = {'kind': 'box', 'size': [w, d, h], **L}
    return m


def soft_box(name, w, d, h, r, wrinkle=0.0, seed=1, n=10):
    """a vacuum brick: a box with rounded edges and creases, mapped like box() (each side's picture to its flat part)."""
    m = box(name, w, d, h, sub=(n, max(3, int(n * d / w)), max(4, int(n * h / w))))
    V, N, U, T = m.arrays()
    V = V.astype(np.float64)
    half = np.array([w / 2, d / 2, h / 2]); c = np.array([0, 0, h / 2])
    p = V - c
    inner = np.clip(p, -(half - r), half - r)
    off = p - inner
    ln = np.linalg.norm(off, axis=1, keepdims=True)
    V = c + inner + np.where(ln > 1e-9, off / np.maximum(ln, 1e-9) * r, 0)
    if wrinkle:
        rng = np.random.default_rng(seed)
        k = rng.normal(size=(6, 3)) * 40; ph = rng.uniform(0, 6.3, 6)
        dz = sum(np.sin(V @ k[i] + ph[i]) for i in range(6)) / 6
        nrm = welded(V, normals_of(V, T))
        V = V + nrm * (dz[:, None] * wrinkle)
    o = M(); o.add(V, welded(V, normals_of(V, T)), U, T)
    return o


def pouch(name, w, d, h, seal=.022, top_taper=.30, bottom=True, bulge=.25, n=14):
    """a soft bag: a box mapped like box(); its depth narrows to a flat heat seal over the top part (and the bottom when not a stand-up bag)."""
    m = box(name, w, d, h, sub=(n, 6, n * 2))
    V, N, U, T = m.arrays()
    V = V.astype(np.float64)
    z = V[:, 2] / h
    def k(t):  # 1 below the taper, -> 0 at the top
        return np.where(t < 1 - top_taper, 1.0, np.clip((1 - t) / top_taper, 0, 1) ** .7)
    f = k(z)
    if not bottom:
        f = np.minimum(f, k(1 - z))
    # the sealed band is flat: its last part has no depth
    sealz = 1 - seal / h
    f = np.where(z > sealz, 0.04, f)
    if not bottom: f = np.where(z < seal / h, 0.04, f)
    x = V[:, 0] / (w / 2)
    pill = 1 + bulge * (1 - x ** 2)
    V[:, 1] *= f * pill
    # the sides pull in a little where the bag is flat
    V[:, 0] *= 1 - .06 * (1 - f)
    o = M(); o.add(V, normals_of(V, T), U, T)
    return o


def tin(name, r=.045, h=.13, lid=.032, n=64):
    """a round tin with a slip lid; layout: body wrap y 0..600, lid side y 612..760, lid top circle (256, 890), bottom (768, 890)."""
    m = M()
    vb = lambda y: px(0, y)[1]
    # body: rolled bead at the bottom, straight wall up into the lid
    prof = [(r - .002, 0.0), (r, .0015), (r, .004), (r, h - lid + .004)]
    m.merge(lathe(prof, n, 0, 1, [vb(600), vb(596), vb(590), vb(0)]))
    rl = r + .0012
    prof = [(r - .0003, h - lid), (rl - .0005, h - lid), (rl, h - lid + .001), (rl, h - .003), (rl - .0012, h - .0002), (rl - .003, h)]
    m.merge(lathe(prof, n, 0, 1, [vb(760), vb(759), vb(756), vb(616), vb(612), vb(612)]))
    m.merge(disc(rl - .003, h, n, 256, 890, 120, True, dome=.0012))
    m.merge(disc(r - .002, 0.0, n, 768, 890, 120, False))
    LAYOUT[name] = {'kind': 'tin', 'r': r, 'h': h, 'lid': lid, 'body': (0, 0, 1024, 600), 'lidside': (0, 612, 1024, 760),
                    'top': (256, 890, 120), 'bottom': (768, 890, 120)}
    return m


def jar(name, r=.040, h=.135, n=64):
    """instant coffee: a glass jar with shoulders and a ribbed screw lid. Body wrap y 0..640 (glass with coffee and the label),
    lid side y 652..760, lid top (256, 890), bottom (768, 890)."""
    m = M()
    vb = lambda y: px(0, y)[1]
    neck = r * .80; lid_r = neck + .003; lid_h = .024
    body_top = h - lid_h - .010
    prof = [(r - .006, 0.0), (r - .001, .002), (r, .008), (r, body_top - .012), (r - .004, body_top - .003), (neck + .003, body_top + .002),
            (neck, body_top + .006), (neck, h - lid_h + .002)]
    zs = [p[1] for p in prof]
    m.merge(lathe(prof, n, 0, 1, [vb(640 - 640 * z / zs[-1]) for z in zs]))
    ribs = 48; P = []; UV = []
    for i, z in enumerate((h - lid_h, h - .002)):
        for j in range(ribs * 2 + 1):
            a = 2 * math.pi * j / (ribs * 2) + math.pi / 2
            rr = lid_r + (.0006 if j % 2 == 0 and i == 0 else 0)
            P.append((rr * math.cos(a), rr * math.sin(a), z)); UV.append((j / (ribs * 2), vb(760 if i == 0 else 652)))
    m.merge(surface(P, UV, 2, ribs * 2 + 1, flip=True))
    m.merge(lathe([(lid_r, h - .002), (lid_r - .002, h)], n, 0, 1, [vb(652), vb(652)]))
    m.merge(disc(lid_r - .002, h, n, 256, 890, 120, True, dome=.0008))
    m.merge(lathe([(neck - .0005, h - lid_h), (lid_r + .0006, h - lid_h)], n, 0, 1, [vb(758), vb(760)], flip=True))   # the lid's underside: no gap to the neck
    m.merge(disc(r - .006, 0.0, n, 768, 890, 120, False))
    LAYOUT[name] = {'kind': 'jar', 'r': r, 'h': h, 'body': (0, 0, 1024, 640), 'bodyz': [0, zs[-1]], 'label': [.022, body_top - .022],
                    'lidside': (0, 652, 1024, 760), 'top': (256, 890, 120), 'bottom': (768, 890, 120)}
    return m


def can_tab(n=64):
    """the stay-on tab of the drink can: a thin plate with rounded edges and a finger window, held by a domed rivet. Base can's metres."""
    from kit import tab_shape, TAB_RIVET
    out, hole = tab_shape(n)
    z0, z1, bev = .13075, .1319, .00028
    LIGHT, DARK = (.25, .9995), (.75, .9995)
    V, Nn, U, T = [], [], [], []

    def normals(loop):
        res = []
        for i in range(n):
            ax, ay = loop[i - 1]; bx, by = loop[(i + 1) % n]
            tx, ty = bx - ax, by - ay; L = math.hypot(tx, ty) or 1
            res.append((ty / L, -tx / L))     # loops run anticlockwise: the right of the tangent points out
        return res

    def strip(A, B, NA, NB, uvc):
        """a band between two loops of n points (x, y, z), with their normals"""
        base = len(V)
        for p, q in ((A, NA), (B, NB)):
            for i in range(n): V.append(p[i]); Nn.append(q[i]); U.append(uvc)
        for i in range(n):
            j = (i + 1) % n
            for tri in ((base + i, base + j, base + n + j), (base + i, base + n + j, base + n + i)):
                a, b, c = (np.array(V[k]) for k in tri)
                nrm = np.cross(b - a, c - a)
                T.append(tri if np.dot(nrm, np.array(Nn[tri[0]]) + np.array(Nn[tri[1]]) + np.array(Nn[tri[2]])) >= 0 else (tri[0], tri[2], tri[1]))

    no, nh = normals(out), normals(hole)
    up = [(0, 0, 1)] * n
    s2 = 1 / math.sqrt(2)
    top_o = [(x, y, z1) for x, y in out]; top_h = [(x, y, z1) for x, y in hole]
    bev_o = [(x + nx * bev, y + ny * bev, z1 - bev) for (x, y), (nx, ny) in zip(out, no)]
    bev_h = [(x - nx * bev, y - ny * bev, z1 - bev) for (x, y), (nx, ny) in zip(hole, nh)]
    strip(top_h, top_o, up, up, LIGHT)
    strip(top_o, bev_o, up, [(nx * s2, ny * s2, s2) for nx, ny in no], LIGHT)
    strip(bev_o, [(x, y, z0) for x, y, _ in bev_o], [(nx, ny, 0) for nx, ny in no], [(nx, ny, 0) for nx, ny in no], DARK)
    strip(top_h, bev_h, up, [(-nx * s2, -ny * s2, s2) for nx, ny in nh], LIGHT)
    strip(bev_h, [(x, y, z0) for x, y, _ in bev_h], [(-nx, -ny, 0) for nx, ny in nh], [(-nx, -ny, 0) for nx, ny in nh], DARK)
    # the rivet: a low dome with a flat crown
    rx, ry, rr = TAB_RIVET
    prof = [(rr, z1 - .00005, DARK), (rr * .96, z1 + .00035, LIGHT), (rr * .70, z1 + .00062, LIGHT), (rr * .25, z1 + .00070, LIGHT)]
    rings = []
    for r, z, uvc in prof:
        rings.append(([(rx + r * math.cos(2 * math.pi * i / n), ry + r * math.sin(2 * math.pi * i / n), z) for i in range(n)], uvc))
    for k, ((A, ua), (B, ub)) in enumerate(zip(rings, rings[1:])):
        sl = [.9, .6, .15][k]
        nr = [(math.cos(2 * math.pi * i / n) * sl, math.sin(2 * math.pi * i / n) * sl, math.sqrt(1 - sl * sl)) for i in range(n)]
        strip(A, B, nr, nr, ub)
    base = len(V); V.append((rx, ry, z1 + .00072)); Nn.append((0, 0, 1)); U.append(LIGHT)
    last = base - n
    for i in range(n): T.append((last + i, last + (i + 1) % n, base))
    return np.array(V, float), np.array(Nn, float), np.array(U, np.float32), np.array(T, np.int64)


def can(name, cut=0.0, wide=1.0):
    """the aluminium drink can of the food mod (necked top, sunk lid, a real pull tab), read from can_base.fpmesh; cut: metres taken out of the
    straight wall for a short can, wide: a fatter can. The wrap is the whole width of the texture, the wall between y 210 and 869, front at x 256."""
    b = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'can_base.fpmesh'), 'rb').read()
    nv, ni = struct.unpack('<ii', b[:8]); o = 8
    v = np.frombuffer(b, '<f4', nv * 3, o).reshape(-1, 3).astype(np.float64); o += nv * 12
    nn = np.frombuffer(b, '<f4', nv * 3, o).reshape(-1, 3).astype(np.float64); o += nv * 12
    uv = np.frombuffer(b, '<f4', nv * 2, o).reshape(-1, 2); o += nv * 8
    t = np.frombuffer(b, '<i4', ni, o).reshape(-1, 3)
    # Unity space back to ours
    V = np.stack([-v[:, 0], -v[:, 2], v[:, 1]], 1); N = np.stack([-nn[:, 0], -nn[:, 2], nn[:, 1]], 1); T = t[:, [0, 2, 1]]
    # owner 11-15, a real-looking end: the flat mouth patch, the thick finger ring and the old rivet go (the mouth is painted on the panel),
    # a flat stay-on tab with a finger window and a rivet comes instead; it takes the two plain metals of the texture's top row
    uv = uv.copy()
    old = (V[:, 2] > .13) & ((uv == 0).all(1) | ((np.abs(V[:, 2] - .1308) < 6e-5) & (uv[:, 1] == 0)))
    T = T[~old[T].any(1)]
    tv, tn, tuv, tt = can_tab()
    T = np.concatenate([T, tt + len(V)]); V = np.concatenate([V, tv]); N = np.concatenate([N, tn]); uv = np.concatenate([uv, tuv])
    # owner 11-15, a real underside: the flat bottom goes, a lathe of the chamfer, the standing ring and the dome pushed up into the can comes
    from kit import can_bottom_profile
    low = V[:, 2] < .0037
    ring = np.where(np.abs(V[:, 2] - .0038) < 6e-5)[0]
    ring = ring[np.argsort(uv[ring, 0], kind='stable')]
    T = T[~low[T].any(1)]
    dirs = V[ring, :2] / np.hypot(V[ring, 0], V[ring, 1])[:, None]; us = uv[ring, 0]
    prof = can_bottom_profile(); nr = len(ring)
    bv, bn, bu, bt = [], [], [], []
    for k, (r, z, vv) in enumerate(prof):
        a_, b_ = prof[max(k - 1, 0)], prof[min(k + 1, len(prof) - 1)]
        dr, dz = b_[0] - a_[0], b_[1] - a_[1]; L = math.hypot(dr, dz) or 1
        for j in range(nr):
            bv.append((dirs[j, 0] * r, dirs[j, 1] * r, z)); bn.append((-dz / L * dirs[j, 0], -dz / L * dirs[j, 1], dr / L)); bu.append((us[j], vv))
    for k in range(len(prof) - 1):
        for j in range(nr - 1):
            a0, a1, b0, b1 = k * nr + j, k * nr + j + 1, (k + 1) * nr + j, (k + 1) * nr + j + 1
            for tri in ((a0, a1, b1), (a0, b1, b0)):
                p, q, s = (np.array(bv[i]) for i in tri)
                bt.append(tri if np.dot(np.cross(q - p, s - p), np.array(bn[tri[0]]) + np.array(bn[tri[1]]) + np.array(bn[tri[2]])) >= 0 else (tri[0], tri[2], tri[1]))
    T = np.concatenate([T, np.array(bt, np.int64) + len(V)]); V = np.concatenate([V, np.array(bv, float)]); N = np.concatenate([N, np.array(bn, float)])
    uv = np.concatenate([uv, np.array(bu, np.float32)])
    lo, hi = .020, .110
    if cut:
        z = V[:, 2].copy(); k = (hi - lo - cut) / (hi - lo)
        V[:, 2] = np.where(z < lo, z, np.where(z > hi, z - cut, lo + (z - lo) * k))
    V[:, :2] *= wide
    m = M(); m.add(V, N, uv, T)
    LAYOUT[name] = {'kind': 'can', 'r': .0262 * wide, 'hm': .1198 - cut, 'body': (0, 210, 1024, 869), 'front': 256}
    return m


def cake(name, R=.095, t=.028, n=72):
    """a pressed pu-erh cake in its paper: a domed top with a small knob at the centre, a dimple under it; the paper gathered underneath.
    top circle (256, 300) r 250, bottom circle (768, 300) r 250, edge band y 620..700."""
    m = M()
    rings = 14; P = []; UV = []
    for k in range(rings + 1):
        f = k / rings
        rr = R * f
        z = t * .12 + t * .88 * (1 - f ** 2.2) ** .6 + .004 * math.exp(-(f / .12) ** 2)
        for j in range(n + 1):
            a = 2 * math.pi * j / n
            wob = 1 + .012 * math.sin(5 * a + 1) + .008 * math.sin(11 * a)
            P.append((rr * wob * math.cos(a), rr * wob * math.sin(a), z)); UV.append(px(256 + 250 * f * math.cos(a), 300 - 250 * f * math.sin(a)))
    m.merge(surface(P, UV, rings + 1, n + 1, flip=True))
    P = []; UV = []
    for k in range(rings + 1):
        f = k / rings
        rr = R * f
        z = t * .12 * f ** 4 + (t * .3 * (1 - f / .15) if f < .15 else 0)
        for j in range(n + 1):
            a = 2 * math.pi * j / n
            wob = 1 + .012 * math.sin(5 * a + 1) + .008 * math.sin(11 * a)
            fold = .0025 * max(0, math.sin(16 * a)) * f * (1 - f) * 4
            P.append((rr * wob * math.cos(a), rr * wob * math.sin(a), z + fold * (1 - f ** 6)))
            UV.append(px(768 + 250 * f * math.cos(a), 300 + 250 * f * math.sin(a)))
    m.merge(surface(P, UV, rings + 1, n + 1, flip=False))
    LAYOUT[name] = {'kind': 'cake', 'R': R, 't': t, 'top': (256, 300, 250), 'bottom': (768, 300, 250)}
    return m


def handle_tube(path, rad, uvrect, n=10):
    """a tube along a path; uv from rect (pixel) along x by path, y around."""
    path = np.asarray(path, np.float64); k = len(path)
    tang = np.gradient(path, axis=0); tang /= np.linalg.norm(tang, axis=1, keepdims=True)
    P, UV = [], []
    x0, y0, x1, y1 = uvrect
    for i in range(k):
        side = np.cross(tang[i], [0, 1, 0.0])
        if np.linalg.norm(side) < 1e-6: side = np.cross(tang[i], [1, 0, 0.0])
        side /= np.linalg.norm(side); up = np.cross(side, tang[i])
        rr = rad[i] if hasattr(rad, '__len__') else rad
        for j in range(n + 1):
            a = 2 * math.pi * j / n
            P.append(path[i] + (side * math.cos(a) + up * math.sin(a)) * rr)
            UV.append(px(x0 + (x1 - x0) * i / (k - 1), y0 + (y1 - y0) * j / n))
    return surface(P, UV, k, n + 1, flip=False)


def loop_handle(wall, reach, zt, zb, tr, uvrect=(0, 580, 1024, 640), sink=.0015):
    """owner 11-15: handles must sit tight on the wall. A loop from zt down to zb reaching out by reach; its ends widen where they
    meet the wall (as a pressed or glued handle does) and close inside the wall, so no gap and no open end shows."""
    path, rad = [], []
    for i in range(24):
        f = i / 23; a = math.pi * f
        z = zt - (zt - zb) * (1 - math.cos(a)) / 2
        s = math.sin(a)
        path.append((wall(z) - sink + (reach + sink) * s ** .8, 0.0, z))
        rad.append(tr * (1 + .25 * (1 - s) ** 6))
    # closing points inside the wall at both ends
    for end, nxt in ((0, 1), (-1, -2)):
        x, _, z = path[end]
        cap = (wall(z) - sink - .0004, 0.0, z)
        if end == 0: path.insert(0, cap); rad.insert(0, tr * .15)
        else: path.append(cap); rad.append(tr * .15)
    return handle_tube(path, rad, uvrect, 14)


def tea_glass(name, n=64):
    """a faceted tea glass in a metal glass-holder (подстаканник) with a handle.
    layout: glass outside y 0..260, holder outside y 270..560, holder inside/rim y 560..600, glass inside y 610..660, handle y 670..730,
    tea surface (256, 870) r 140, holder bottom (768, 870) r 140."""
    m = M(); vb = lambda y: px(0, y)[1]
    rg0, rg1, hg = .028, .0335, .102
    hold_h = .062
    # glass outside, from the holder up to the rim (below the holder it is hidden)
    z0 = hold_h - .012
    prof = [(rg0 + (rg1 - rg0) * z / hg, z) for z in np.linspace(z0, hg, 6)]
    m.merge(lathe(prof, 20, 0, 1, [vb(260 - 260 * (z - z0) / (hg - z0)) for _, z in prof]))  # 20 sides: a faceted glass
    # rim and inside wall down to the tea
    tea_z = hg - .016
    m.merge(lathe([(rg1 + .0002, hg - .003), (rg1 + .0002, hg), (rg1 - .0018, hg + .0004), (rg1 - .0022, hg - .001)], n, 0, 1, [vb(609), vb(610), vb(612), vb(614)]))
    rin = lambda z: rg0 + (rg1 - rg0) * z / hg - .0022
    m.merge(lathe([(rin(tea_z), tea_z), (rin(hg - .001), hg - .001)], n, 0, 1, [vb(660), vb(614)], flip=True))
    m.merge(disc(rin(tea_z), tea_z, n, 256, 870, 140, True))
    # holder: a foot, a waist and a decorated cup
    rh = lambda z: rg0 + (rg1 - rg0) * z / hg + .0016
    prof = [(rh(0) + .004, 0.0), (rh(0) + .0045, .002), (rh(0) + .002, .005), (rh(.008) - .0005, .009), (rh(.014), .016),
            (rh(.03) + .0008, .03), (rh(hold_h - .006) + .0012, hold_h - .006), (rh(hold_h) + .0022, hold_h - .001), (rh(hold_h) + .0018, hold_h + .0012)]
    zs = [p[1] for p in prof]
    m.merge(lathe(prof, n, 0, 1, [vb(560 - 290 * z / hold_h) for z in zs]))
    m.merge(lathe([(rh(hold_h) + .0018, hold_h + .0012), (rh(hold_h) - .0002, hold_h + .0004), (rh(hold_h - .004) - .0004, hold_h - .004), (rh(hold_h - .004) - .0030, hold_h - .0045)], n, 0, 1, [vb(560), vb(580), vb(598), vb(600)]))
    m.merge(disc(rh(0) + .004, 0.0, n, 768, 870, 140, False))
    # handle on the right (+X): a loop whose two ends go into the holder wall at their own heights (the holder narrows downwards)
    hr = lambda z: float(np.interp(z, zs, [p[0] for p in prof]))
    zt, zb = hold_h - .008, .019
    ys = []
    for i in range(22):
        f = i / 21
        a = math.pi * f
        z = zt + (zb - zt) * (1 - math.cos(a)) / 2
        base = hr(zt) + (hr(zb) - hr(zt)) * f - .0025
        ys.append((base + .0235 * math.sin(a) ** .85, 0.0, z))
    m.merge(handle_tube(ys, [.0032 if 2 < i < 19 else .0028 for i in range(22)], (0, 670, 1024, 730)))
    LAYOUT[name] = {'kind': 'tea_glass', 'glass': (0, 0, 1024, 260), 'holder': (0, 270, 1024, 560), 'inner': (0, 560, 1024, 660),
                    'handle': (0, 670, 1024, 730), 'liquid': (256, 870, 140), 'bottom': (768, 870, 140)}
    return m


def mug(name, n=64):
    """an enamel camp mug: outside y 0..430, rolled rim y 430..470, inside wall y 470..560, handle y 580..640,
    coffee surface (256, 860) r 140, bottom (768, 860) r 140."""
    m = M(); vb = lambda y: px(0, y)[1]
    r, h = .041, .082
    prof = [(r - .004, 0.0), (r - .001, .002), (r, .006), (r, h - .002)]
    m.merge(lathe(prof, n, 0, 1, [vb(430), vb(425), vb(415), vb(0)]))
    m.merge(lathe([(r, h - .002), (r + .0012, h - .0005), (r + .0006, h + .0012), (r - .0012, h + .0010), (r - .0022, h - .0005)], n, 0, 1,
                  [vb(430), vb(440), vb(450), vb(460), vb(470)]))
    cz = h - .013
    m.merge(lathe([(r - .0022, cz), (r - .0022, h - .0005)], n, 0, 1, [vb(560), vb(470)], flip=True))
    m.merge(disc(r - .0022, cz, n, 256, 860, 140, True))
    m.merge(disc(r - .004, 0.0, n, 768, 860, 140, False))
    m.merge(loop_handle(lambda z: r, .029, h - .012, .020, .0046, sink=.0012))
    LAYOUT[name] = {'kind': 'mug', 'outside': (0, 0, 1024, 430), 'rim': (0, 430, 1024, 470), 'inner': (0, 470, 1024, 560),
                    'handle': (0, 580, 1024, 640), 'liquid': (256, 860, 140), 'bottom': (768, 860, 140)}
    return m


def handled_cup(name, prof, h, rim_out, handle, n=64, kind='mug'):
    """a cup on a lathe profile [(r, z)] from the foot up to the rim at height h, a rolled lip of rim_out, a loop handle
    handle = (outward reach, top z, bottom z, tube radius) or None; layout like mug() (outside y 0..430, rim 430..470, inside 470..560,
    handle 580..640, liquid (256, 860) r 140, bottom (768, 860) r 140)."""
    m = M(); vb = lambda y: px(0, y)[1]
    zs = [p[1] for p in prof]
    m.merge(lathe(prof, n, 0, 1, [vb(430 - 430 * z / h) for z in zs]))
    r = prof[-1][0]
    m.merge(lathe([(r, h), (r + rim_out, h + .0006), (r - .0008, h + .0012), (r - .0024, h - .0004)], n, 0, 1, [vb(430), vb(445), vb(458), vb(470)]))
    rin = lambda z: float(np.interp(z, zs, [p[0] for p in prof])) - .0024
    cz = h - min(.012, h * .25)
    m.merge(lathe([(rin(cz), cz), (rin(h - .003), h - .003), (r - .0024, h - .0004)], n, 0, 1, [vb(560), vb(500), vb(470)], flip=True))
    m.merge(disc(rin(cz), cz, n, 256, 860, 140, True))
    m.merge(disc(prof[0][0], 0.0, n, 768, 860, 140, False))
    if handle:
        reach, zt, zb, tr = handle
        wall = lambda z: float(np.interp(z, zs, [p[0] for p in prof]))
        m.merge(loop_handle(wall, reach, zt, zb, tr))
    LAYOUT[name] = {'kind': kind, 'outside': (0, 0, 1024, 430), 'rim': (0, 430, 1024, 470), 'inner': (0, 470, 1024, 560),
                    'handle': (0, 580, 1024, 640), 'liquid': (256, 860, 140), 'bottom': (768, 860, 140)}
    return m


def espresso(name):
    """a small porcelain demitasse: a foot, a rounded belly, a small ear of a handle"""
    h = .052
    prof = [(.017, 0.0), (.0185, .002), (.0175, .004)] + [(.0175 + (.031 - .0175) * math.sin(math.pi / 2 * i / 10) ** .7, .004 + (h - .004) * i / 10) for i in range(1, 11)]
    return handled_cup(name, prof, h, .0006, (.018, h - .009, .013, .0032), kind='espresso')


def ceramic(name):
    """a thick glazed stoneware mug, slightly waisted, a broad handle"""
    h = .098
    prof = [(.036, 0.0), (.0385, .003), (.0395, .010), (.0380, .045), (.0395, .085), (.0405, h)]
    return handled_cup(name, prof, h, .0014, (.033, h - .013, .017, .0062), kind='ceramic')


def camp(name):
    """a steel camp cup: straight, a little wider at the top, a rolled lip and a thin strap handle"""
    h = .072
    prof = [(.034, 0.0), (.0355, .002), (.0360, .006), (.0400, h)]
    return handled_cup(name, prof, h, .0016, (.029, h - .007, .022, .0028), kind='camp')


def yunomi(name):
    """a tall handleless Japanese tea cup, gently tapered, a low foot"""
    h = .085
    prof = [(.024, 0.0), (.025, .003), (.0245, .006), (.0275, .020), (.0310, .060), (.0325, h)]
    return handled_cup(name, prof, h, .0010, None, kind='yunomi')


def dome(name, top, bot, n=72, lobes=0, lobe_amp=0.0, wob=.008, fold=0.0, rings=18):
    """a pressed tea shape seen from above and below: top/bot = [(r, z)] from the centre out to the same rim point.
    top circle (256, 300) r 250, bottom circle (768, 300) r 250 like cake(); the picture radius follows the surface length."""
    m = M()
    def half(prof, cx, up):
        prof = np.asarray(prof, np.float64)
        seg = np.r_[0, np.cumsum(np.linalg.norm(np.diff(prof, axis=0), axis=1))]; seg /= seg[-1]
        # resample finer
        fs = np.linspace(0, 1, rings)
        rr = np.interp(fs, seg, prof[:, 0]); zz = np.interp(fs, seg, prof[:, 1])
        P = []; UV = []
        for k in range(len(fs)):
            for j in range(n + 1):
                a = 2 * math.pi * j / n
                w = 1 + wob * math.sin(5 * a + 1) + wob * .6 * math.sin(11 * a)
                # owner 11-15: the lobes fade out towards the middle, so a ticket pressed into the top is not bent by them
                if lobes: w *= 1 - lobe_amp * abs(math.sin(lobes * a / 2)) ** .6 * min(1.0, fs[k] / .55) ** 2
                z = zz[k]
                if fold and not up: z += fold * max(0, math.sin(16 * a)) * fs[k] * (1 - fs[k]) * 4
                P.append((rr[k] * w * math.cos(a), rr[k] * w * math.sin(a), z))
                UV.append(px(cx + 250 * fs[k] * math.cos(a), 300 - 250 * fs[k] * math.sin(a) * (1 if up else -1)))
        m.merge(surface(P, UV, len(fs), n + 1, flip=up))
    half(top, 256, True); half(bot, 768, False)
    LAYOUT[name] = {'kind': 'cake', 'top': (256, 300, 250), 'bottom': (768, 300, 250)}
    return m


def arc(r0, z0, r1, z1, k=8, pw=1.0):
    """quarter-ellipse profile points from (r0, z0) (flat there) to (r1, z1) (vertical there)"""
    return [(r0 + (r1 - r0) * math.sin(math.pi / 2 * i / k) ** pw, z1 + (z0 - z1) * math.cos(math.pi / 2 * i / k)) for i in range(k + 1)]


def cyl(name, r0, r1, h, n=64, bevel=.003):
    """a tube / log / basket: wrap y 0..600, top (256, 890) r 120, bottom (768, 890) r 120."""
    m = M(); vb = lambda y: px(0, y)[1]
    prof = [(r0 - bevel, 0.0), (r0, bevel), (r1, h - bevel), (r1 - bevel, h)]
    m.merge(lathe(prof, n, 0, 1, [vb(600), vb(594), vb(6), vb(0)]))
    m.merge(disc(r1 - bevel, h, n, 256, 890, 120, True, dome=.001))
    m.merge(disc(r0 - bevel, 0.0, n, 768, 890, 120, False))
    LAYOUT[name] = {'kind': 'cyl', 'r': r1, 'h': h, 'body': (0, 0, 1024, 600), 'top': (256, 890, 120), 'bottom': (768, 890, 120)}
    return m


def flat(m, d, h):
    """lay an upright box down so that its front looks up"""
    V, N, U, T = m.arrays()
    R = lambda A: np.stack([A[:, 0], A[:, 2], -A[:, 1]], 1)
    V = R(V); V[:, 2] += d / 2; V[:, 1] -= h / 2
    o = M(); o.add(V, R(N), U, T)
    return o


def piala(name, n=64):
    """a handleless tea bowl (пиала): outside y 0..430, rim y 430..470, inside wall y 470..560, tea (256, 860) r 140, foot (768, 860) r 140."""
    m = M(); vb = lambda y: px(0, y)[1]
    R, h = .043, .048
    prof = [(.019, 0.0), (.020, .004), (.0195, .006)] + [(.0195 + (R - .0195) * math.sin(math.pi / 2 * i / 8) ** .8, .006 + (h - .006) * (1 - math.cos(math.pi / 2 * i / 8)) ** .9) for i in range(1, 9)]
    zs = [p[1] for p in prof]
    m.merge(lathe(prof, n, 0, 1, [vb(430 - 430 * z / h) for z in zs]))
    m.merge(lathe([(R, h), (R - .0008, h + .0008), (R - .002, h - .0004)], n, 0, 1, [vb(430), vb(450), vb(470)]))
    tz = h - .010
    rin = lambda z: float(np.interp(z, zs, [p[0] for p in prof])) - .002
    m.merge(lathe([(rin(tz), tz), (rin(h - .003), h - .003), (R - .002, h - .0004)], n, 0, 1, [vb(560), vb(500), vb(470)], flip=True))
    m.merge(disc(rin(tz), tz, n, 256, 860, 140, True))
    m.merge(disc(.019, 0.0, n, 768, 860, 140, False))
    LAYOUT[name] = {'kind': 'piala', 'outside': (0, 0, 1024, 430), 'rim': (0, 430, 1024, 470), 'inner': (0, 470, 1024, 560),
                    'liquid': (256, 860, 140), 'bottom': (768, 860, 140)}
    return m


def barrel(name, r=.040, h=.115, n=64):
    """a small wooden barrel (Jamaican coffee): layout like cyl()."""
    m = M(); vb = lambda y: px(0, y)[1]
    prof = [(r * .86 - .003, 0.0)] + [(r * (.86 + .14 * math.sin(math.pi * i / 10)), h * i / 10) for i in range(11)] + [(r * .86 - .003, h)]
    prof[1] = (r * .86, .001); prof[-2] = (r * .86, h - .001)
    zs = [p[1] for p in prof]
    m.merge(lathe(prof, n, 0, 1, [vb(600 - 600 * z / h) for z in zs]))
    m.merge(disc(r * .86 - .003, h - .004, n, 256, 890, 120, True))
    m.merge(lathe([(r * .86 - .003, h - .004), (r * .86 - .003, h)], n, 0, 1, [vb(2), vb(0)], flip=True))
    m.merge(disc(r * .86 - .003, 0.0, n, 768, 890, 120, False))
    LAYOUT[name] = {'kind': 'cyl', 'r': r, 'h': h, 'body': (0, 0, 1024, 600), 'top': (256, 890, 120), 'bottom': (768, 890, 120)}
    return m


def bottle(name, r=.029, h=.175, n=64):
    """a small glass bottle with a screw cap; layout like jar()."""
    m = M(); vb = lambda y: px(0, y)[1]
    cap_h = .014; neck = .0125; sh = h * .62
    prof = [(r - .005, 0.0), (r - .001, .002), (r, .007), (r, sh)] + [(neck + (r - neck) * math.cos(math.pi / 2 * i / 6) ** 1.3, sh + (h * .80 - sh) * math.sin(math.pi / 2 * i / 6)) for i in range(1, 7)] + [(neck, h - cap_h + .002)]
    zs = [p[1] for p in prof]
    m.merge(lathe(prof, n, 0, 1, [vb(640 - 640 * z / zs[-1]) for z in zs]))
    cr = neck + .0022
    m.merge(lathe([(cr, h - cap_h), (cr, h - .0015), (cr - .0015, h)], n, 0, 1, [vb(760), vb(656), vb(652)]))
    m.merge(lathe([(neck - .0005, h - cap_h), (cr, h - cap_h)], n, 0, 1, [vb(758), vb(760)], flip=True))
    m.merge(disc(cr - .0015, h, n, 256, 890, 120, True))
    m.merge(disc(r - .005, 0.0, n, 768, 890, 120, False))
    LAYOUT[name] = {'kind': 'jar', 'r': r, 'h': h, 'body': (0, 0, 1024, 640), 'bodyz': [0, zs[-1]], 'label': [.022, sh - .012],
                    'lidside': (0, 652, 1024, 760), 'top': (256, 890, 120), 'bottom': (768, 890, 120)}
    return m


def write(m, name):
    v, nn, uv, t = m.arrays()
    np.savez(os.path.join(OUT, 'cache', name + '.npz'), v=v, n=nn, uv=uv, t=t)
    Uv = np.stack([-v[:, 0], v[:, 2], -v[:, 1]], 1); Nn = np.stack([-nn[:, 0], nn[:, 2], -nn[:, 1]], 1)
    tt = t[:, [0, 2, 1]].reshape(-1)
    with open(os.path.join(OUT, 'meshes', name + '.fpmesh'), 'wb') as f:
        f.write(struct.pack('<ii', len(Uv), len(tt)))
        f.write(Uv.astype('<f4').tobytes()); f.write(Nn.astype('<f4').tobytes()); f.write(uv.astype('<f4').tobytes()); f.write(tt.astype('<i4').tobytes())
    print(f'{name}: {len(Uv)} verts, {len(t)} tris, size {np.round(v.max(0) - v.min(0), 3)}')


MODELS = {
    # tea
    'tin_tea': lambda n: tin(n, .042, .125, .030),
    'tin_tall': lambda n: tin(n, .036, .150, .034),
    'tin_coffee': lambda n: tin(n, .052, .140, .026),
    'tin_low': lambda n: tin(n, .050, .085, .022),
    'box_bags': lambda n: box(n, .135, .072, .072, sub=(1, 1, 1)),
    'box_tall': lambda n: box(n, .075, .045, .135),
    'box_sticks': lambda n: box(n, .105, .055, .145),
    'box_flat': lambda n: box(n, .095, .030, .135),
    'pack_paper': lambda n: pouch(n, .085, .052, .175, seal=.018, top_taper=.16, bottom=True, bulge=.10),
    'bag_valve': lambda n: pouch(n, .105, .062, .205, seal=.025, top_taper=.30, bottom=True, bulge=.22),
    'sachet': lambda n: pouch(n, .070, .014, .095, seal=.008, top_taper=.12, bottom=False, bulge=.30),
    'brick': lambda n: soft_box(n, .080, .055, .130, .006, wrinkle=.0007, seed=3),
    'cake': lambda n: cake(n),
    'jar': lambda n: jar(n),
    'can': lambda n: can(n),
    'cake_small': lambda n: cake(n, .062, .022),
    'tuocha': lambda n: dome(n, arc(0, .050, .047, .014), [(0, .026), (.018, .022), (.030, .008), (.038, .001), (.044, .004), (.047, .014)], wob=.004),
    'tuo_mini': lambda n: dome(n, arc(0, .022, .019, .006), [(0, .010), (.008, .008), (.013, .002), (.017, .001), (.019, .006)], n=40, wob=.004),
    'mushroom': lambda n: dome(n, arc(0, .098, .052, .058), [(0, 0.0), (.022, 0.0), (.024, .004), (.023, .040), (.034, .050), (.048, .052), (.052, .058)], wob=.006),
    'melon': lambda n: dome(n, arc(0, .100, .062, .050), [(0, 0.0), (.030, .001), (.050, .014), (.060, .034), (.062, .050)], lobes=8, lobe_amp=.07, wob=.003, n=96, rings=40),
    'ball': lambda n: dome(n, [(0, .042), (.003, .040), (.005, .036)] + arc(.006, .0355, .0185, .018)[1:], [(0, 0.0), (.008, .001), (.015, .007), (.0185, .018)], n=40, wob=.01),
    'tangerine': lambda n: dome(n, arc(0, .046, .027, .023), [(0, .003), (.006, .001), (.016, .004), (.024, .012), (.027, .023)], n=48, lobes=10, lobe_amp=.02, wob=.006),
    'tablet': lambda n: dome(n, [(0, .014), (.036, .014), (.040, .011), (.041, .007)], [(0, 0.0), (.037, 0.0), (.040, .002), (.041, .007)], n=48, wob=.002),
    'coin': lambda n: dome(n, [(0, .005), (.004, .005), (.006, .009), (.019, .009), (.021, .0045)], [(0, .004), (.004, .004), (.006, 0.0), (.019, 0.0), (.021, .0045)], n=40, wob=.004),
    'brick_tea': lambda n: flat(soft_box(n, .095, .028, .150, .005, wrinkle=.0005, seed=7), .028, .150),
    'tile': lambda n: flat(soft_box(n, .085, .016, .140, .002), .016, .140),
    'square': lambda n: flat(soft_box(n, .100, .022, .100, .004, wrinkle=.0004, seed=9), .022, .100),
    'square_mini': lambda n: flat(soft_box(n, .050, .018, .050, .003), .018, .050),
    'briquette': lambda n: flat(soft_box(n, .075, .028, .055, .003, wrinkle=.0004, seed=4), .028, .055),
    'log': lambda n: cyl(n, .034, .034, .250, bevel=.006),
    'bamboo': lambda n: cyl(n, .021, .020, .170, bevel=.002),
    'basket': lambda n: cyl(n, .046, .058, .085, bevel=.006),
    'tube': lambda n: cyl(n, .038, .038, .150, bevel=.002),
    'tube_slim': lambda n: cyl(n, .028, .028, .190, bevel=.002),
    # more packs
    'tin_square': lambda n: box(n, .078, .078, .112),
    'tin_small': lambda n: tin(n, .032, .048, .016),
    'tin_wide': lambda n: tin(n, .062, .070, .020),
    'jar_small': lambda n: jar(n, .033, .100),
    'jar_wide': lambda n: jar(n, .047, .115),
    'doypack': lambda n: pouch(n, .125, .060, .190, seal=.022, top_taper=.34, bottom=True, bulge=.20),
    'sack': lambda n: pouch(n, .100, .070, .150, seal=.020, top_taper=.22, bottom=True, bulge=.30),
    'carton': lambda n: box(n, .090, .060, .165),
    'box_small': lambda n: box(n, .065, .040, .085),
    'box_wide': lambda n: box(n, .150, .050, .100),
    'barrel': lambda n: barrel(n),
    'bottle': lambda n: bottle(n),
    'stick': lambda n: pouch(n, .028, .010, .130, seal=.008, top_taper=.08, bottom=False, bulge=.20, n=8),
    'can_short': lambda n: can(n, cut=.031),
    'can_wide': lambda n: can(n, cut=.012, wide=1.25),
    # cups
    'piala': lambda n: piala(n),
    'tea_glass': lambda n: tea_glass(n),
    'mug': lambda n: mug(n),
    'espresso': lambda n: espresso(n),
    'ceramic': lambda n: ceramic(n),
    'camp': lambda n: camp(n),
    'yunomi': lambda n: yunomi(n),
}

if __name__ == '__main__':
    for name, f in MODELS.items():
        write(f(name), name)
    with open(os.path.join(OUT, 'layout.json'), 'w') as fh:
        json.dump(LAYOUT, fh, indent=1)

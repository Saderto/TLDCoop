"""Package meshes for SnacksByEMPEROR, built from parametric grids.
Authoring space: right-handed, metres, Y up, Z toward the viewer, the item resting on y=0, triangles counter-clockwise seen from outside.
write_fpmesh() turns that into the game's space (z negated, winding reversed), the format the EMPEROR mods read:
int nv, int ni, nv*3 pos, nv*3 normals, nv*2 uv, ni idx."""
import struct, math
import numpy as np


class Mesh:
    def __init__(self):
        self.v, self.uv, self.t = [], [], []
        self.n = 0
        self.normals = None

    def grid(self, f, nu, nv, uvf, flip=False, wrap_u=False):
        """f(s,t)->xyz over s,t in [0,1]; uvf(s,t)->(u,v). Faces outward when (dP/ds x dP/dt) points out (flip reverses)."""
        base = self.n
        S = np.linspace(0, 1, nu + 1)
        T = np.linspace(0, 1, nv + 1)
        for t in T:
            for s in S:
                self.v.append(f(s, t))
                self.uv.append(uvf(s, t))
        self.n += (nu + 1) * (nv + 1)
        W = nu + 1
        for j in range(nv):
            for i in range(nu):
                a = base + j * W + i
                b, c, d = a + 1, a + W + 1, a + W
                if flip:
                    self.t += [(a, c, b), (a, d, c)]
                else:
                    self.t += [(a, b, c), (a, c, d)]
        return self

    def disc(self, center, r_of, n, normal_up, uvc, uvr, seg=48, ring_y=None):
        """A flat fan (cap). center xyz; r_of(angle)->radius; normal_up True: faces +y."""
        base = self.n
        cx, cy, cz = center
        self.v.append((cx, cy, cz)); self.uv.append(uvc)
        for k in range(seg + 1):
            a = 2 * math.pi * k / seg
            r = r_of(a)
            self.v.append((cx + r * math.cos(a), cy if ring_y is None else ring_y, cz + r * math.sin(a)))
            self.uv.append((uvc[0] + uvr[0] * math.cos(a), uvc[1] - uvr[1] * math.sin(a)))
        self.n += seg + 2
        for k in range(seg):
            a, b = base + 1 + k, base + 2 + k
            # x right, z toward viewer, angle runs x->z: seen from +y that is clockwise, so (c,b,a) is ccw from above
            self.t.append((base, b, a) if normal_up else (base, a, b))
        return self

    def merge(self, other):
        off = self.n
        self.v += other.v; self.uv += other.uv
        self.t += [(a + off, b + off, c + off) for a, b, c in other.t]
        self.n += other.n
        return self

    def arrays(self):
        V = np.array(self.v, float); UV = np.array(self.uv, float); F = np.array(self.t, int)
        return V, UV, F

    def compute_normals(self, weld=1e-5):
        V, UV, F = self.arrays()
        fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
        N = np.zeros_like(V)
        # smooth across coincident vertices (seams of grids) unless the faces differ a lot
        key = np.round(V / weld).astype(np.int64)
        _, inv = np.unique(key, axis=0, return_inverse=True)
        inv = inv.reshape(-1)
        acc = np.zeros((inv.max() + 1, 3))
        for k in range(3):
            np.add.at(N, F[:, k], fn)
        np.add.at(acc, inv, N)
        Ns = acc[inv]
        # keep the vertex's own normal where the welded one turns away (sharp edges)
        own = N / np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-12)
        sm = Ns / np.maximum(np.linalg.norm(Ns, axis=1, keepdims=True), 1e-12)
        use = (own * sm).sum(1) > 0.6
        out = np.where(use[:, None], sm, own)
        self.normals = out
        return out

    def bounds(self):
        V = np.array(self.v)
        return V.min(0), V.max(0)

    def drop_to_floor(self):
        V = np.array(self.v); m = V[:, 1].min()
        self.v = [(x, y - m, z) for x, y, z in self.v]
        return self


def write_fpmesh(path, mesh):
    V, UV, F = mesh.arrays()
    N = mesh.normals if mesh.normals is not None else mesh.compute_normals()
    Vu = V.copy(); Vu[:, 2] *= -1
    Nu = N.copy(); Nu[:, 2] *= -1
    Fu = F[:, [0, 2, 1]]
    with open(path, 'wb') as fh:
        fh.write(struct.pack('<ii', len(Vu), Fu.size))
        fh.write(Vu.astype('<f4').tobytes())
        fh.write(Nu.astype('<f4').tobytes())
        fh.write(UV.astype('<f4').tobytes())
        fh.write(Fu.astype('<i4').reshape(-1).tobytes())


def save_npz(path, mesh):
    V, UV, F = mesh.arrays()
    N = mesh.normals if mesh.normals is not None else mesh.compute_normals()
    np.savez(path, verts=V, uvs=UV, tris=F, normals=N)


# ---------------------------------------------------------------- shapes

def _noise(s, t, seed, amp):
    r = np.random.RandomState(seed)
    ph = r.rand(6) * 6.28
    fr = r.randint(3, 9, 6)
    return amp * (math.sin(fr[0] * s * 3.1 + ph[0]) * math.sin(fr[1] * t * 2.3 + ph[1]) * .5
                  + math.sin(fr[2] * (s + t) * 2.0 + ph[2]) * .3 + math.sin(fr[3] * (s - t) * 2.7 + ph[3]) * .2)


def pillow_bag(W, H, T, seal=.06, side_seal=0.0, seed=1, wrinkle=.0025, res=(28, 40), uv_front=(0, 0, .5, 1), uv_back=(.5, 0, 1, 1), squash=1.0):
    """A sealed snack bag lying on its back. W across (x), H along (z, label's up = -z, away from viewer), T puffed thickness.
    seal: share of H flattened at each end (crimped). Front faces +y with uv_front (u0,v0,u1,v1); back faces -y."""
    m = Mesh()
    def puff(s, t):
        # inner zone between the seals
        tt = (t - seal) / (1 - 2 * seal)
        if tt <= 0 or tt >= 1: p_t = 0.0
        else: p_t = math.sin(math.pi * tt) ** .45
        ss = (s - side_seal) / (1 - 2 * side_seal) if side_seal > 0 else s
        p_s = math.sin(math.pi * min(max(ss, 0), 1)) ** .55
        return p_s * p_t
    def pos(s, t, side):
        p = puff(s, t)
        # the bag's outline pulls in a little where it is fullest
        pinch = 1 - .06 * p
        x = (s - .5) * W * pinch
        z = -(t - .5) * H * (1 - .03 * p)
        crimp = 0.0
        tt = (t - seal) / (1 - 2 * seal)
        if tt <= 0 or tt >= 1:
            crimp = .0012 * math.sin(s * W / .003 * math.pi)      # crimp ridges across the seal
        y = side * (T / 2 * p * (squash if side < 0 else 1) + _noise(s, t, seed + (side > 0), wrinkle) * p) + crimp
        return (x, y, z)
    u0, v0, u1, v1 = uv_front
    m.grid(lambda s, t: pos(s, t, 1), res[0], res[1], lambda s, t: (u0 + s * (u1 - u0), v0 + t * (v1 - v0)), flip=False)
    b0, c0, b1, c1 = uv_back
    back = Mesh().grid(lambda s, t: pos(s, t, -1), res[0], res[1], lambda s, t: (b1 - s * (b1 - b0), c0 + t * (c1 - c0)), flip=True)
    m.merge(back)
    m.drop_to_floor()
    return m


def tube_can(R, H, uv_side=(0, .25, 1, 1), uv_top=(.125, .125, .12), uv_bot=(.375, .125, .12), seg=48, lid_h=.012, lid_r=1.03):
    """A standing tube (crisps can). Side label wraps once; top: plastic lid (slightly wider); bottom: metal."""
    m = Mesh()
    u0, v0, u1, v1 = uv_side
    H2 = H - lid_h
    # side: s around, t up. Seen from outside: dP/ds x dP/dt must point out
    m.grid(lambda s, t: (R * math.sin(2 * math.pi * s), t * H2, R * math.cos(2 * math.pi * s)), seg, 2,
           lambda s, t: (u0 + s * (u1 - u0), v0 + t * (v1 - v0)), flip=False)
    # lid ring
    Rl = R * lid_r
    m.grid(lambda s, t: (Rl * math.sin(2 * math.pi * s), H2 - .003 + t * (lid_h + .003), Rl * math.cos(2 * math.pi * s)), seg, 1,
           lambda s, t: (uv_top[0] + uv_top[2] * .98 * math.sin(2 * math.pi * s), uv_top[1] + uv_top[2] * .98 * math.cos(2 * math.pi * s)), flip=False)
    m.disc((0, H, 0), lambda a: Rl, seg, True, uv_top[:2], (uv_top[2], uv_top[2]), seg=seg)
    m.disc((0, 0, 0), lambda a: R, seg, False, uv_bot[:2], (uv_bot[2], uv_bot[2]), seg=seg)
    return m


def cup(R0, R1, H, uv_side=(0, .3, 1, 1), uv_lid=(.15, .15, .14), seg=48, lid=True, open_fill=None, rim=.004):
    """A standing cup, narrower at the bottom (R0) than the top (R1). lid: foil lid on top (flat). open_fill: (uv centre, uv radius, level 0..1) shows the food inside instead."""
    m = Mesh()
    u0, v0, u1, v1 = uv_side
    m.grid(lambda s, t: ((R0 + (R1 - R0) * t) * math.sin(2 * math.pi * s), t * H, (R0 + (R1 - R0) * t) * math.cos(2 * math.pi * s)), seg, 4,
           lambda s, t: (u0 + s * (u1 - u0), v0 + t * (v1 - v0)), flip=False)
    # rolled rim
    m.grid(lambda s, t: ((R1 + rim * math.sin(math.pi * t)) * math.sin(2 * math.pi * s), H + rim * .8 * math.sin(math.pi * t * .5) - rim * t * .2,
                         (R1 + rim * math.sin(math.pi * t)) * math.cos(2 * math.pi * s)), seg, 3,
           lambda s, t: (.86, .14), flip=False)   # a plain patch of the sheet (the top row of the wall print showed as dark dashes round the lid)
    # bottom: a foot ring and a recessed base, as moulded cups have
    foot, rec = .86, .0045
    plain = (.86, .14)
    m.grid(lambda s, t: ((R0 * (1 - (1 - foot) * t)) * math.sin(2 * math.pi * s), 0.0, (R0 * (1 - (1 - foot) * t)) * math.cos(2 * math.pi * s)), seg, 1, lambda s, t: plain, flip=True)
    m.grid(lambda s, t: (R0 * (foot - .03 * t) * math.sin(2 * math.pi * s), rec * t, R0 * (foot - .03 * t) * math.cos(2 * math.pi * s)), seg, 1, lambda s, t: plain, flip=True)
    m.disc((0, rec, 0), lambda a: R0 * (foot - .03), seg, False, plain, (0, 0), seg=seg)
    if lid:
        c, r = uv_lid[:2], uv_lid[2]
        m.disc((0, H + rim * .6, 0), lambda a: R1 + rim * .9, seg, True, c, (r, r), seg=seg)
        # a tab sticking out
    if open_fill is not None:
        c, r, lvl = open_fill
        # inner wall (seen from inside: flip)
        m.grid(lambda s, t: ((R0 + (R1 - R0) * t) * .985 * math.sin(2 * math.pi * s), t * H, (R0 + (R1 - R0) * t) * .985 * math.cos(2 * math.pi * s)), seg, 3,
               lambda s, t: (u0 + .01, v0 + .01), flip=True)
        y = H * lvl
        rr = (R0 + (R1 - R0) * lvl) * .985
        m.disc((0, y, 0), lambda a: rr, seg, True, c, (r, r), seg=seg)
    return m


def tray(W, D, H, top_inset=.0, uv_lid=(0, .45, .62, 1), uv_side=(0, 0, 1, .42), r=.012, seg_c=6, lip=.005):
    """A rectangular noodle tray with rounded corners, wider at the top, a printed lid on top. W along x, D along z."""
    m = Mesh()
    # outline of a rounded rectangle, param s 0..1 around, returns (x,z) and the share along for uv
    def outline(w, d, rr, n=seg_c):
        pts = []
        cs = [(w / 2 - rr, d / 2 - rr, 0), (-w / 2 + rr, d / 2 - rr, 90), (-w / 2 + rr, -d / 2 + rr, 180), (w / 2 - rr, -d / 2 + rr, 270)]
        for cx, cz, a0 in cs:
            for k in range(n + 1):
                a = math.radians(a0 + 90 * k / n)
                pts.append((cx + rr * math.cos(a), cz + rr * math.sin(a)))
        pts.append(pts[0])  # closed: without it one wedge of the lid and one strip of the wall were missing
        return pts
    bw, bd = W * .86, D * .86
    top = outline(W, D, r)
    bot = outline(bw, bd, r * .8)
    n = len(top)
    L = [0.0]
    for i in range(1, n): L.append(L[-1] + math.dist(top[i], top[i - 1]))
    L = [x / L[-1] for x in L]
    u0, v0, u1, v1 = uv_side
    base = m.n
    for k in range(n):
        for j, (pp, y) in enumerate([(bot[k], 0.0), (top[k], H)]):
            m.v.append((pp[0], y, pp[1])); m.uv.append((u1 - L[k] * (u1 - u0), v0 + j * (v1 - v0)))  # u runs backwards: the print read mirrored
    m.n += 2 * n
    for k in range(n - 1):
        a, b = base + 2 * k, base + 2 * (k + 1)
        # points run counter-clockwise seen from above (x -> -z?) ; fix orientation later by test
        m.t += [(a, b, b + 1), (a, b + 1, a + 1)]
    # lid: a flat fan from the centre over the top outline, slightly bigger (lip)
    lt = outline(W + 2 * lip, D + 2 * lip, r + lip)
    lu0, lv0, lu1, lv1 = uv_lid
    c = m.n
    m.v.append((0, H + .002, 0)); m.uv.append(((lu0 + lu1) / 2, (lv0 + lv1) / 2))
    for (x, z) in lt:
        m.v.append((x, H + .002, z))
        m.uv.append((lu0 + (x / (W + 2 * lip) + .5) * (lu1 - lu0), lv0 + (-z / (D + 2 * lip) + .5) * (lv1 - lv0)))
    m.n += 1 + len(lt)
    for k in range(len(lt) - 1):
        m.t.append((c, c + 1 + k, c + 2 + k))
    # lid edge
    e = m.n
    for (x, z) in lt:
        m.v.append((x, H - .001, z)); m.uv.append((lu0 + .01, lv0 + .01))
        m.v.append((x, H + .002, z)); m.uv.append((lu0 + .01, lv0 + .01))
    m.n += 2 * len(lt)
    for k in range(len(lt) - 1):
        a, b = e + 2 * k, e + 2 * (k + 1)
        m.t += [(a, b, b + 1), (a, b + 1, a + 1)]
    # bottom: a rim the tray stands on, a step up, and a recessed panel with two stiffening ribs
    def ring(o1, y1, o2, y2):
        b0 = m.n
        for k in range(len(o1)):
            m.v.append((o1[k][0], y1, o1[k][1])); m.uv.append((u0 + .01, v0 + .01))
            m.v.append((o2[k][0], y2, o2[k][1])); m.uv.append((u0 + .01, v0 + .01))
        m.n += 2 * len(o1)
        for k in range(len(o1) - 1):
            a_, b_ = b0 + 2 * k, b0 + 2 * (k + 1)
            m.t += [(a_, b_, b_ + 1), (a_, b_ + 1, a_ + 1)]
    rec = .004
    in1 = outline(bw * .88, bd * .86, r * .6)
    in2 = outline(bw * .84, bd * .81, r * .5)
    ring(bot, 0.0, in1, 0.0)
    ring(in1, 0.0, in2, rec)
    bc = m.n
    m.v.append((0, rec, 0)); m.uv.append((u0 + .01, v0 + .01))
    for (x, z) in in2:
        m.v.append((x, rec, z)); m.uv.append((u0 + .01, v0 + .01))
    m.n += 1 + len(in2)
    for k in range(len(in2) - 1):
        m.t.append((bc, bc + 2 + k, bc + 1 + k))
    for zc in (-bd * .18, bd * .18):
        rb = Mesh()
        box(rb, (0, rec * .5, zc), (bw * .6, rec, bd * .05), (u0 + .01, v0 + .01))
        m.merge(rb)
    orient_out(m)
    return m


def orient_out(m):
    """Turn every triangle to face away from the mesh's centre (for convex shapes)."""
    V, UV, F = m.arrays()
    c = (V.min(0) + V.max(0)) / 2
    out = []
    for a, b, cc in F:
        n = np.cross(V[b] - V[a], V[cc] - V[a])
        mid = (V[a] + V[b] + V[cc]) / 3
        d = mid - c
        if abs(n @ d) < 1e-14 and abs(n[1]) > 0: d = np.array([0, 1 if mid[1] > c[1] else -1, 0])
        out.append((a, b, cc) if n @ d >= 0 else (a, cc, b))
    m.t = out


def bowl(R0, R1, H, uv_wall=(.0, .0, .5, .5), fill=((.75, .75), .24), level=.82, seg=48):
    """A plain bowl with food in it (brewed noodles). Outside wall + rim + inner wall + the food's surface."""
    m = Mesh()
    u0, v0, u1, v1 = uv_wall
    prof = lambda t: R0 + (R1 - R0) * (math.sin(t * math.pi / 2) ** .8)
    m.grid(lambda s, t: (prof(t) * math.sin(2 * math.pi * s), t * H, prof(t) * math.cos(2 * math.pi * s)), seg, 6,
           lambda s, t: (u0 + s * (u1 - u0), v0 + t * (v1 - v0)), flip=False)
    m.grid(lambda s, t: ((R1 - .004 * t) * math.sin(2 * math.pi * s), H + .002 * math.sin(math.pi * t), (R1 - .004 * t) * math.cos(2 * math.pi * s)), seg, 2,
           lambda s, t: (u0 + s * (u1 - u0), v1 - .01), flip=False)
    m.grid(lambda s, t: ((prof(t) - .004) * math.sin(2 * math.pi * s), max(.004, t * H), (prof(t) - .004) * math.cos(2 * math.pi * s)), seg, 6,
           lambda s, t: (u0 + s * (u1 - u0), v0 + t * (v1 - v0)), flip=True)
    m.disc((0, 0, 0), lambda a: R0, seg, False, (u0 + .02, v0 + .02), (0, 0), seg=seg)
    c, r = fill
    rr = prof(level) - .004
    m.disc((0, H * level, 0), lambda a: rr, seg, True, c, (r, r), seg=seg)
    return m


def box(m, c, size, uv):
    """An axis-aligned box added to mesh m, one uv point for all of it."""
    cx, cy, cz = c; sx, sy, sz = (x / 2 for x in size)
    faces = [((1, 0, 0), (0, 1, 0), (0, 0, 1)), ((-1, 0, 0), (0, 0, 1), (0, 1, 0)), ((0, 1, 0), (0, 0, 1), (1, 0, 0)),
             ((0, -1, 0), (1, 0, 0), (0, 0, 1)), ((0, 0, 1), (1, 0, 0), (0, 1, 0)), ((0, 0, -1), (0, 1, 0), (1, 0, 0))]
    for n, a, b in faces:
        base = m.n
        for da, db in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            p = [n[i] + a[i] * da + b[i] * db for i in range(3)]
            m.v.append((cx + p[0] * sx, cy + p[1] * sy, cz + p[2] * sz)); m.uv.append(uv)
        m.n += 4
        m.t += [(base, base + 1, base + 2), (base, base + 2, base + 3)]


def vessel(kind, uv_wall=(.0, .0, .5, .5), fill=((.75, .75), .24), seg=48):
    """What brewed noodles are served in: Bowl (white, as before), Donburi (deep Japanese bowl on a foot), Pot (Korean yellow pot with two handles),
    Plate (shallow, for noodles without broth). Same texture layout for all: wall in the bottom-left quarter, food disc centred (.75,.75)."""
    R0, R1, H, level, power = {'Bowl': (.05, .078, .062, .82, .8), 'Donburi': (.036, .084, .08, .84, .62), 'Pot': (.076, .084, .062, .8, 1.0), 'Plate': (.06, .105, .028, .62, 1.5)}[kind]
    m = Mesh()
    u0, v0, u1, v1 = uv_wall
    prof = lambda t: R0 + (R1 - R0) * (math.sin(t * math.pi / 2) ** power)
    th = .003
    m.grid(lambda s, t: (prof(t) * math.sin(2 * math.pi * s), t * H, prof(t) * math.cos(2 * math.pi * s)), seg, 6,
           lambda s, t: (u0 + s * (u1 - u0), v0 + t * (v1 - v0)), flip=False)
    m.grid(lambda s, t: ((R1 - th * t) * math.sin(2 * math.pi * s), H + .002 * math.sin(math.pi * t), (R1 - th * t) * math.cos(2 * math.pi * s)), seg, 2,
           lambda s, t: (u0 + s * (u1 - u0), v1 - .01), flip=False)
    m.grid(lambda s, t: ((prof(t) - th) * math.sin(2 * math.pi * s), max(.004, t * H), (prof(t) - th) * math.cos(2 * math.pi * s)), seg, 6,
           lambda s, t: (u0 + s * (u1 - u0), v0 + t * (v1 - v0)), flip=True)
    m.disc((0, 0, 0), lambda a: R0, seg, False, (u0 + .02, v0 + .02), (0, 0), seg=seg)
    if kind == 'Donburi':   # a foot ring under it
        m.grid(lambda s, t: ((R0 * .8) * math.sin(2 * math.pi * s), -.008 + t * .009, (R0 * .8) * math.cos(2 * math.pi * s)), seg, 1,
               lambda s, t: (u0 + s * (u1 - u0), v0 + .02), flip=False)
        m.disc((0, -.008, 0), lambda a: R0 * .8, seg, False, (u0 + .02, v0 + .02), (0, 0), seg=seg)
    if kind == 'Pot':       # two flat handles at the rim
        for sx in (-1, 1):
            box(m, (sx * (R1 + .012), H - .006, 0), (.026, .004, .05), (u0 + .25 * (u1 - u0), v0 + .9 * (v1 - v0)))
    c, r = fill
    rr = prof(level) - th
    m.disc((0, H * level, 0), lambda a: rr, seg, True, c, (r, r), seg=seg)
    m.drop_to_floor()
    return m

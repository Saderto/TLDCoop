"""JellyfishByEMPEROR models and textures, all made here (no game input).
Geometry is built Z-up right-handed (Blender space) and written to .fpmesh in Unity space with the same conversion as the smokehouse's
(x,y,z) -> (-x, z, -y), triangle winding swapped. Also writes cache/<name>.npz for the preview/icon renderer.
python3 models.py <out_dir>"""
import sys, os, struct, math, json
import numpy as np
from PIL import Image

OUT = sys.argv[1] if len(sys.argv) > 1 else 'out'
for d in ('meshes', 'textures', 'cache'):
    os.makedirs(os.path.join(OUT, d), exist_ok=True)
rng = np.random.default_rng(7)


class M:
    """A mesh being built: vertices, normals, uvs, triangles (CCW seen from the normal side)."""
    def __init__(s):
        s.v, s.n, s.uv, s.t = [], [], [], []

    def add(s, v, n, uv, t):
        base = sum(len(a) for a in s.v)
        s.v.append(np.asarray(v, np.float32)); s.n.append(np.asarray(n, np.float32)); s.uv.append(np.asarray(uv, np.float32))
        s.t.append(np.asarray(t, np.int64) + base)

    def merge(s, o):
        for i in range(len(o.v)):
            s.add(o.v[i], o.n[i], o.uv[i], o.t[i] - sum(len(a) for a in o.v[:i]))
        return s

    def arrays(s):
        return np.concatenate(s.v), np.concatenate(s.n), np.concatenate(s.uv), np.concatenate(s.t)


def grid_tris(rows, cols, wrap=False):
    t = []
    cc = cols if wrap else cols - 1
    for i in range(rows - 1):
        for j in range(cc):
            a = i * cols + j; b = i * cols + (j + 1) % cols; c = (i + 1) * cols + j; d = (i + 1) * cols + (j + 1) % cols
            t += [(a, c, b), (b, c, d)]
    return np.array(t)


def normals_of(P, T):
    N = np.zeros_like(P)
    a, b, c = P[T[:, 0]], P[T[:, 1]], P[T[:, 2]]
    fn = np.cross(b - a, c - a)
    for k in range(3): np.add.at(N, T[:, k], fn)
    N /= np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-9)
    return N


def surface(P, UV, rows, cols, wrap, flip=False):
    """P: rows*cols points. Triangles so the normal points as the grid's (i down, j around) order gives; flip reverses."""
    T = grid_tris(rows, cols, wrap)
    if flip: T = T[:, [0, 2, 1]]
    m = M(); m.add(P, normals_of(P, T), UV, T); return m


def lathe(prof_r, prof_z, n=48, uvf=None, flip=False, lobes=0, lobe_depth=0.0, wave=None):
    """Surface of revolution, rows along the profile, columns around. uvf(i_frac, angle) -> (u, v)."""
    rows = len(prof_r); P = []; UV = []
    for i in range(rows):
        f = i / (rows - 1)
        for j in range(n):
            a = 2 * math.pi * j / n
            r = prof_r[i]
            if lobes: r *= 1 - lobe_depth * f ** 3 * (0.5 - 0.5 * math.cos(lobes * a))
            z = prof_z[i]
            if wave is not None: z += wave(f, a)
            P.append((r * math.cos(a), r * math.sin(a), z))
            UV.append(uvf(f, a) if uvf else (j / n, f))
    return surface(np.array(P), np.array(UV), rows, n, True, flip)


def ribbon(path, width, normal_hint, u0, u1, v0, v1, cross=True, twist=0.0):
    """A flat strip along path (k points), width per point; cross: a second strip at right angles (seen from any side)."""
    path = np.asarray(path, np.float64); k = len(path)
    width = np.broadcast_to(np.asarray(width, np.float64), (k,))
    tang = np.gradient(path, axis=0); tang /= np.maximum(np.linalg.norm(tang, axis=1, keepdims=True), 1e-9)
    out = M()
    for s in ([0.0, math.pi / 2] if cross else [0.0]):
        P = []; UV = []
        for i in range(k):
            side = np.cross(tang[i], normal_hint); side /= max(np.linalg.norm(side), 1e-9)
            up = np.cross(side, tang[i])
            ang = s + twist * i / max(1, k - 1)
            d = side * math.cos(ang) + up * math.sin(ang)
            P += [path[i] - d * width[i] / 2, path[i] + d * width[i] / 2]
            vv = v0 + (v1 - v0) * i / (k - 1)
            UV += [(u0, vv), (u1, vv)]
        out.merge(surface(np.array(P), np.array(UV), k, 2, False))
    return out


def tentacle(start, direction, length, k=14, sway=0.3, curl=0.0, seed=0, width=0.0012, taper=0.3, uv=(0.02, 0.48, 0.02, 0.48)):
    r = np.random.default_rng(seed)
    d = np.asarray(direction, float); d /= np.linalg.norm(d)
    ph1, ph2 = r.uniform(0, 6.28, 2)
    side = np.cross(d, [0, 0, 1.0]);
    if np.linalg.norm(side) < 1e-6: side = np.array([1.0, 0, 0])
    side /= np.linalg.norm(side); side2 = np.cross(d, side)
    pts = []
    for i in range(k):
        f = i / (k - 1); s = length * f
        p = np.asarray(start, float) + d * s + side * sway * length * f * math.sin(ph1 + f * 5) * 0.25 + side2 * sway * length * f * math.cos(ph2 + f * 4) * 0.25
        p = p + np.array([0, 0, 1.0]) * curl * length * f * f
        pts.append(p)
    w = width * (1 - (1 - taper) * np.linspace(0, 1, k))
    return ribbon(pts, w, np.array([0.3, 0.2, 0.9]), uv[0], uv[1], uv[2], uv[3], cross=True)


def bell_uv(f, a, rim=1.0):
    r = 0.235 * f * rim
    return (0.25 + r * math.cos(a), 0.75 + r * math.sin(a))


# ---------------------------------------------------------------- species shapes (jar scale, metres, bell centre near origin)
def dome(R, H, rows=14, power=1.0, rim_drop=0.0):
    fr = np.linspace(0, 1, rows)
    r = R * np.sin(fr * math.pi / 2) ** power
    z = H * np.cos(fr * math.pi / 2) - rim_drop * fr ** 4
    return r, z


def lionsmane():
    m = M()
    R, H = .032, .021
    r, z = dome(R, H, power=.85)
    m.merge(lathe(r, z, 64, bell_uv, lobes=8, lobe_depth=.18))
    # the inner, lower bell (subumbrella) a little smaller: depth to the see-through body
    m.merge(lathe(r * .9, z * .55 - .002, 48, lambda f, a: bell_uv(f * .6, a), lobes=8, lobe_depth=.15))
    # oral arms: four frilly curtains hanging from the centre
    for q in range(4):
        a = q * math.pi / 2 + .3
        pts = [(math.cos(a) * .004 * (1 + i * .25), math.sin(a) * .004 * (1 + i * .25), -.002 - .0045 * i + .001 * math.sin(i * 1.3 + q)) for i in range(11)]
        m.merge(ribbon(pts, np.linspace(.010, .016, 11), np.array([math.cos(a), math.sin(a), 0]), .52, .98, .52, .98, cross=False, twist=2.2))
    # tentacles: 8 clusters under the lobes' notches, many fine ones each
    s = 0
    for c in range(8):
        a0 = (c + .5) * 2 * math.pi / 8
        for t in range(7):
            a = a0 + (t - 3) * .045; rr = R * .78
            s += 1
            m.merge(tentacle((rr * math.cos(a), rr * math.sin(a), -.001), (math.cos(a) * .12, math.sin(a) * .12, -1), .055 + .02 * ((t * 37 + c * 11) % 5) / 4, k=16, sway=.45, seed=s, width=.0009))
    return m


def aurelia():
    m = M()
    R, H = .035, .009
    r, z = dome(R, H, power=.7)
    m.merge(lathe(r, z, 72, bell_uv))
    # short fringe all round the rim
    s = 0
    for j in range(72):
        a = 2 * math.pi * j / 72; s += 1
        m.merge(tentacle((R * math.cos(a), R * math.sin(a), 0), (math.cos(a) * .5, math.sin(a) * .5, -1), .0045, k=4, sway=.3, seed=s, width=.0005, taper=.5))
    for q in range(4):
        a = q * math.pi / 2 + math.pi / 4
        pts = [(math.cos(a) * .003 * (1 + i * .5), math.sin(a) * .003 * (1 + i * .5), -.001 - .003 * i) for i in range(6)]
        m.merge(ribbon(pts, np.linspace(.006, .004, 6), np.array([math.cos(a), math.sin(a), 0]), .52, .98, .52, .98, cross=False, twist=1.2))
    return m


def crystal():
    m = M()
    R, H = .033, .011
    r, z = dome(R, H, power=.75)
    m.merge(lathe(r, z, 80, bell_uv))
    m.merge(lathe(r * .96, z * .4 - .001, 64, lambda f, a: bell_uv(f * .5, a)))
    s = 0
    for j in range(48):
        a = 2 * math.pi * (j + .3) / 48; s += 1
        m.merge(tentacle((R * .98 * math.cos(a), R * .98 * math.sin(a), -.0005), (math.cos(a) * .25, math.sin(a) * .25, -1), .022 + .012 * (j % 3) / 2, k=10, sway=.35, seed=s, width=.0005))
    # the stomach tube in the middle
    m.merge(lathe(np.array([.0001, .005, .006, .006, .004]), np.array([.001, 0, -.004, -.009, -.012]), 24, lambda f, a: (.6 + .3 * a / 6.28, .55 + .1 * f)))
    return m


def seanettle():
    m = M()
    R, H = .028, .022
    r, z = dome(R, H, power=.9, rim_drop=.002)
    m.merge(lathe(r, z, 64, bell_uv, lobes=16, lobe_depth=.08))
    for q in range(4):
        a = q * math.pi / 2
        pts = []
        for i in range(18):
            f = i / 17
            pts.append((math.cos(a + f * 2.5) * (.003 + .006 * f), math.sin(a + f * 2.5) * (.003 + .006 * f), -.002 - .075 * f))
        m.merge(ribbon(pts, np.linspace(.012, .004, 18), np.array([math.cos(a), math.sin(a), 0]), .52, .98, .52, .98, cross=False, twist=3.5))
    s = 100
    for j in range(24):
        a = 2 * math.pi * (j + .5) / 24; s += 1
        m.merge(tentacle((R * .95 * math.cos(a), R * .95 * math.sin(a), -.001), (math.cos(a) * .08, math.sin(a) * .08, -1), .065 + .015 * (j % 4) / 3, k=16, sway=.4, seed=s, width=.0008))
    return m


def periphylla():
    m = M()
    fr = np.linspace(0, 1, 18)
    r = .023 * np.sin(np.clip(fr * 1.1, 0, 1) * math.pi / 2) ** .95
    r = r * (1 - .07 * np.exp(-((fr - .55) / .06) ** 2))       # the coronal groove
    z = .034 * np.cos(fr * math.pi / 2) ** .8 - .004
    m.merge(lathe(np.maximum(r, .0005), z, 60, bell_uv, lobes=16, lobe_depth=.06))
    m.merge(lathe(np.maximum(r * .7, .0004), z * .8 + .002, 40, lambda f, a: bell_uv(f * .45, a)))
    s = 200
    for j in range(12):
        a = 2 * math.pi * (j + .5) / 12; s += 1
        m.merge(tentacle((.021 * math.cos(a), .021 * math.sin(a), -.003), (math.cos(a) * .9, math.sin(a) * .9, .25), .028, k=10, sway=.15, curl=.9, seed=s, width=.0022, taper=.25))
    return m


def comb():
    m = M()
    fr = np.linspace(0, 1, 20)
    r = .015 * np.sin(fr * math.pi) ** .9
    z = .014 * np.cos(fr * math.pi)
    m.merge(lathe(np.maximum(r, .0003), z, 48, lambda f, a: (.05 + .4 * a / 6.283, .55 + .4 * f)))
    # eight comb rows: narrow strips just over the body, mapped across the rainbow strip (u scrolls in the game)
    for j in range(8):
        a = 2 * math.pi * j / 8
        pts = [((.0157 * math.sin(f * math.pi) ** .9 + .0004) * math.cos(a), (.0157 * math.sin(f * math.pi) ** .9 + .0004) * math.sin(a), .0145 * math.cos(f * math.pi)) for f in np.linspace(.12, .88, 14)]
        m.merge(ribbon(pts, .0018, np.array([math.cos(a), math.sin(a), 0]), .52, .98, .05 + .05 * j, .45, cross=False))
    # two long tentacles with side filaments
    for side in (-1, 1):
        base = np.array([side * .008, 0, -.004])
        main = tentacle(base, (side * .4, 0, -1), .06, k=18, sway=.5, seed=300 + side, width=.0007)
        m.merge(main)
        for b in range(9):
            f = (b + 1) / 10
            p = base + np.array([side * .4, 0, -1]) / np.linalg.norm([side * .4, 0, -1]) * .06 * f
            m.merge(tentacle(p, (side * .9, .3 * (b % 2 * 2 - 1), -.2), .009, k=4, sway=.2, seed=400 + b + side, width=.0004))
    return m


SPECIES = {
    'LionsMane': lionsmane, 'Aurelia': aurelia, 'Crystal': crystal,
    'SeaNettle': seanettle, 'Periphylla': periphylla, 'CombJelly': comb,
}

# colours: (name, bell rgb, accent rgb (gonads/stripes/arms), tentacle rgb, glow rgb)
COLOURS = {
    'LionsMane': [('Amber', (1.0, .55, .22), (.85, .25, .08), (1.0, .65, .35), (1.0, .55, .25)),
                  ('Crimson', (.80, .18, .16), (.50, .05, .08), (.95, .35, .30), (1.0, .30, .25)),
                  ('Gold', (1.0, .85, .45), (.90, .60, .20), (1.0, .90, .60), (1.0, .85, .40)),
                  ('Violet', (.70, .45, .95), (.45, .20, .75), (.85, .65, 1.0), (.75, .50, 1.0))],
    'Aurelia': [('Milky', (.88, .92, .98), (.97, .97, 1.0), (.90, .95, 1.0), (.75, .85, 1.0)),
                ('Pink', (.95, .85, .92), (1.0, .55, .75), (1.0, .85, .92), (1.0, .65, .85)),
                ('Lilac', (.85, .80, .98), (.70, .50, .95), (.90, .85, 1.0), (.75, .60, 1.0)),
                ('Blue', (.70, .85, 1.0), (.35, .60, 1.0), (.75, .90, 1.0), (.40, .70, 1.0))],
    'Crystal': [('Green', (.85, .95, .95), (.35, 1.0, .55), (.85, 1.0, .92), (.30, 1.0, .50)),
                ('Turquoise', (.80, .95, .98), (.20, .95, .90), (.80, 1.0, 1.0), (.20, .95, .95)),
                ('Purple', (.90, .85, .98), (.75, .35, 1.0), (.95, .85, 1.0), (.70, .35, 1.0))],
    'SeaNettle': [('Brown', (.92, .75, .55), (.45, .22, .10), (.95, .70, .45), (1.0, .70, .40)),
                  ('Honey', (1.0, .80, .40), (.85, .50, .12), (1.0, .85, .55), (1.0, .75, .30)),
                  ('Maroon', (.65, .25, .25), (.30, .05, .08), (.80, .40, .35), (1.0, .35, .30))],
    'Periphylla': [('Red', (.85, .70, .85), (.55, .02, .08), (.75, .10, .15), (.25, .55, 1.0)),
                   ('Purple', (.80, .70, .95), (.35, .05, .45), (.55, .15, .65), (.35, .55, 1.0)),
                   ('Black', (.65, .60, .70), (.08, .03, .06), (.20, .08, .10), (.20, .45, 1.0))],
    'CombJelly': [('Clear', (.90, .95, 1.0), (1.0, 1.0, 1.0), (.95, .95, 1.0), (.70, .90, 1.0)),
                  ('Rosy', (1.0, .88, .92), (1.0, .70, .80), (1.0, .85, .90), (1.0, .70, .85)),
                  ('Milky', (1.0, 1.0, 1.0), (1.0, 1.0, 1.0), (1.0, 1.0, 1.0), (.85, .90, 1.0))],
}

S = 256
yy, xx = np.mgrid[0:S, 0:S]
U = (xx + .5) / S; V = (yy + .5) / S          # V from the bottom: row 0 here is v=0, flipped on save


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)


def texture(sp, col):
    name, bell, acc, ten, glow = col
    img = np.zeros((S, S, 4))
    bell = np.array(bell); acc = np.array(acc); ten = np.array(ten); glow = np.array(glow)
    # --- bell (polar, centre .25,.75 radius .235)
    du, dv = U - .25, V - .75
    rr = np.sqrt(du * du + dv * dv) / .235; ang = np.arctan2(dv, du)
    a = .30 + .25 * rr ** 2
    c = bell[None, None, :] * np.ones((S, S, 1))
    noise = np.sin(ang * 31 + rr * 9) * .5 + .5
    if sp == 'LionsMane':
        veins = smooth(.82, 1.0, np.cos(ang * 8) * .5 + .5) * smooth(.15, .6, rr)
        c = c * (1 - .35 * veins[..., None]) + acc * .35 * veins[..., None]
        core = smooth(.45, .1, rr)
        c = c * (1 - core[..., None] * .5) + acc * core[..., None] * .5
        a = a + .25 * core + .15 * veins
    elif sp == 'Aurelia':
        # four horseshoe gonads
        g = np.zeros_like(rr)
        for q in range(4):
            ca = q * math.pi / 2 + math.pi / 4
            cx, cy = .25 + .09 * math.cos(ca), .75 + .09 * math.sin(ca)
            d = np.sqrt((U - cx) ** 2 + (V - cy) ** 2) / .235
            g = np.maximum(g, smooth(.13, .10, np.abs(d - .17)) * smooth(-.05, .2, (du * math.cos(ca) + dv * math.sin(ca))))
        canals = smooth(.97, 1.0, np.cos(ang * 16) * .5 + .5) * smooth(.35, .7, rr) * .5
        c = c * (1 - g[..., None]) + acc * g[..., None]
        a = a * .7 + .55 * g + .2 * canals + .2 * smooth(.93, 1.0, rr)
    elif sp == 'Crystal':
        canals = smooth(.93, 1.0, np.cos(ang * 50) * .5 + .5) * smooth(.15, .3, rr)
        dots = smooth(.75, 1.0, np.cos(ang * 48) * .5 + .5) * smooth(.06, 0, np.abs(rr - .96))
        c = c * (1 - (canals * .5)[..., None]) + acc * (canals * .5 + dots)[..., None]
        a = .12 + .25 * canals + .9 * dots + .1 * rr
    elif sp == 'SeaNettle':
        stripes = smooth(.55, .8, np.cos(ang * 16) * .5 + .5) * smooth(.12, .3, rr)
        c = c * (1 - (stripes * .8)[..., None]) + acc * (stripes * .8)[..., None]
        a = .40 + .25 * rr + .25 * stripes
    elif sp == 'Periphylla':
        inner = smooth(.70, .30, rr)
        c = c * (1 - inner[..., None]) + acc * inner[..., None]
        a = .25 + .65 * inner + .15 * rr
    else:  # comb jelly: almost clear, a faint body
        a = .10 + .15 * (np.sin(V * 60) * .5 + .5) * .3
        c = c * (.9 + .1 * noise[..., None])
    m = (U < .5) & (V >= .5)
    img[m, :3] = c[m]; img[m, 3] = np.clip(a, 0, .95)[m]
    if sp == 'CombJelly':
        m2 = (U < .5) & (V >= .5)
        img[m2, :3] = (bell * (.85 + .15 * noise[..., None]))[m2]; img[m2, 3] = (.14 + .1 * np.abs(np.sin(U * 40)))[m2]
    # --- oral arms (u .5-1, v .5-1): frilled edges darker and denser
    m = (U >= .5) & (V >= .5)
    x = (U - .5) / .5; y = (V - .5) / .5
    edge = smooth(.25, .0, np.minimum(x, 1 - x)) * (.6 + .4 * np.sin(y * 70) ** 2)
    oc = bell * .45 + acc * .55
    arm = oc[None, None, :] * (1 - edge[..., None] * .3) + acc * edge[..., None] * .3
    img[m, :3] = arm[m]; img[m, 3] = np.clip(.18 + .45 * edge - .12 * y, .06, .8)[m]
    # --- tentacles (u 0-.5, v 0-.5): along v, fading to the tip
    m = (U < .5) & (V < .5)
    y = V / .5
    tcol = ten[None, None, :] * (.85 + .15 * np.sin(y * 90)[..., None])
    img[m, :3] = tcol[m]; img[m, 3] = np.clip(.75 - .55 * y, .1, .9)[m]
    # --- rainbow strip (u .5-1, v 0-.5), hue along u, repeats for the scrolling comb rows
    m = (U >= .5) & (V < .5)
    h = ((U - .5) / .5 * 2) % 1.0
    import colorsys
    rgb = np.array([colorsys.hsv_to_rgb(hh, .7, 1.0) for hh in np.linspace(0, 1, 64)])
    hi = np.clip((h * 63).astype(int), 0, 63)
    img[m, :3] = rgb[hi][m]; img[m, 3] = .85
    return img



# ================================================================ v2 (11-12 owner: «качество и реализм так себе»)
# Thicker bells (outer + inner surface joined at a rim of their own), sub-scalloped lobes and a wavy margin, ruffled oral arms
# (a grid across the arm, its edges folded in waves), many hair-thin tentacles on smooth noise paths, textures 512 with noise.

def smooth_noise(n, seed, scale=1.0):
    """1D smooth noise: n samples of a sum of three sines with random phases."""
    r = np.random.default_rng(seed); x = np.linspace(0, 1, n)
    out = np.zeros(n)
    for k, a in ((.8, .6), (1.7, .3), (3.1, .08)):
        out += a * np.sin(2 * math.pi * (k * scale * x + r.uniform()))
    return out


def bell(R, H, power=.8, lobes=0, depth=0.0, sub=0, subdepth=0.0, thick=.25, wave=0.0, n=96, rows=22, rim_drop=0.0, flat=0.0):
    """Outer (exumbrella) and inner (subumbrella) surfaces meeting at the margin. f: 0 at the apex, 1 at the margin."""
    def rad(f, a):
        r = R * math.sin(f * math.pi / 2) ** power
        k = 1.0
        if lobes: k *= 1 - depth * f ** 4 * (0.5 - 0.5 * math.cos(lobes * a)) ** .6
        if sub: k *= 1 - subdepth * f ** 6 * (0.5 - 0.5 * math.cos(sub * a)) ** 2
        return r * k
    def zout(f, a):
        z = H * (math.cos(f * math.pi / 2) ** (1 + flat)) - rim_drop * f ** 3
        return z + wave * f ** 3 * math.sin(lobes * a if lobes else 6 * a) * R
    m = M()
    P = []; UV = []
    for i in range(rows):
        f = i / (rows - 1)
        for j in range(n):
            a = 2 * math.pi * j / n
            r = rad(f, a); P.append((r * math.cos(a), r * math.sin(a), zout(f, a))); UV.append(bell_uv(f, a))
    m.merge(surface(np.array(P), np.array(UV), rows, n, True))
    # inner surface: from the margin back in, higher in the middle by the jelly's thickness
    P = []; UV = []
    for i in range(rows):
        f = 1 - i / (rows - 1)
        for j in range(n):
            a = 2 * math.pi * j / n
            r = rad(f, a) * (1 - .06 * (1 - f)); z = zout(f, a) - thick * H * math.sin(min(1.0, (1 - f) * 3) * math.pi / 2) * (1 - f * .3)
            P.append((r * math.cos(a), r * math.sin(a), z)); UV.append(bell_uv(f * .55, a))
    m.merge(surface(np.array(P), np.array(UV), rows, n, True))
    return m


def frill(path, width, cols=9, amp=.35, freq=60.0, seed=0, uv=(.52, .98, .52, .98), normal_hint=(0, 0, 1.0)):
    """A ruffled arm: rows along the path, columns across; the further from the middle line, the deeper the waves."""
    path = np.asarray(path, float); k = len(path)
    width = np.broadcast_to(np.asarray(width, float), (k,))
    tang = np.gradient(path, axis=0); tang /= np.maximum(np.linalg.norm(tang, axis=1, keepdims=True), 1e-9)
    L = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(path, axis=0), axis=1))])
    r = np.random.default_rng(seed); ph = r.uniform(0, 6.28)
    P = []; UV = []
    for i in range(k):
        side = np.cross(tang[i], normal_hint); side /= max(np.linalg.norm(side), 1e-9)
        up = np.cross(side, tang[i])
        for c in range(cols):
            x = c / (cols - 1) * 2 - 1
            off = side * x * width[i] / 2 + up * amp * width[i] * abs(x) ** 1.6 * math.sin(freq * L[i] + ph + x * 2.1)
            P.append(path[i] + off)
            UV.append((uv[0] + (uv[1] - uv[0]) * c / (cols - 1), uv[2] + (uv[3] - uv[2]) * i / (k - 1)))
    return surface(np.array(P), np.array(UV), k, cols, False)


def hair(start, direction, length, seed, width=.0005, k=26, sway=.5, curl=0.0, taper=.25, uv=(0.02, 0.48, 0.02, 0.48)):
    d = np.asarray(direction, float); d /= np.linalg.norm(d)
    side = np.cross(d, [0, 0, 1.0])
    if np.linalg.norm(side) < 1e-6: side = np.array([1.0, 0, 0])
    side /= np.linalg.norm(side); side2 = np.cross(d, side)
    n1, n2 = smooth_noise(k, seed), smooth_noise(k, seed + 999)
    pts = []
    for i in range(k):
        f = i / (k - 1)
        p = np.asarray(start, float) + d * length * f + (side * n1[i] + side2 * n2[i]) * sway * length * f * .35 + np.array([0, 0, 1.0]) * curl * length * f * f
        pts.append(p)
    w = width * (1 - (1 - taper) * np.linspace(0, 1, k))
    return ribbon(pts, w, np.array([0.3, 0.2, 0.9]), uv[0], uv[1], uv[2], uv[3], cross=True)


def lionsmane():
    m = M(); R, H = .033, .019
    m.merge(bell(R, H, power=.75, lobes=8, depth=.22, sub=32, subdepth=.10, thick=.45, wave=.04))
    # the huge folded curtain of oral arms: four ruffled arms hanging and turning
    for q in range(4):
        a = q * math.pi / 2 + .4
        pts = [(math.cos(a + i * .12) * .005 * (1 + i * .18), math.sin(a + i * .12) * .005 * (1 + i * .18), -.003 - .0042 * i) for i in range(16)]
        m.merge(frill(pts, np.linspace(.009, .022, 16), cols=11, amp=.45, freq=700, seed=q, normal_hint=(math.cos(a), math.sin(a), 0)))
    # eight clusters of hair-thin tentacles from the notches between the lobes
    s = 0
    for c in range(8):
        a0 = c * 2 * math.pi / 8
        for t in range(18):
            a = a0 + (t - 8.5) * .018; rr = R * (.70 + .06 * (t % 3)); s += 1
            ln = .05 + .035 * ((t * 37 + c * 11) % 7) / 6
            m.merge(hair((rr * math.cos(a), rr * math.sin(a), -.002), (math.cos(a) * .10, math.sin(a) * .10, -1), ln, s, width=.00045, sway=.55))
    return m


def aurelia():
    m = M(); R, H = .036, .0085
    m.merge(bell(R, H, power=.6, lobes=8, depth=.035, thick=.9, wave=.015, flat=.3))
    s = 0
    for j in range(140):
        a = 2 * math.pi * j / 140; s += 1
        m.merge(hair((R * .995 * math.cos(a), R * .995 * math.sin(a), .0), (math.cos(a) * .6, math.sin(a) * .6, -1), .0045 + .002 * (j % 3) / 2, s, width=.00028, k=5, sway=.4, taper=.5))
    for q in range(4):
        a = q * math.pi / 2 + math.pi / 4
        pts = [(math.cos(a) * .003 * (1 + i * .45), math.sin(a) * .003 * (1 + i * .45), -.002 - .0028 * i) for i in range(8)]
        m.merge(frill(pts, np.linspace(.006, .004, 8), cols=7, amp=.5, freq=900, seed=10 + q, normal_hint=(math.cos(a), math.sin(a), 0)))
    return m


def crystal():
    m = M(); R, H = .034, .010
    m.merge(bell(R, H, power=.65, thick=.8, wave=.01, flat=.2, n=120))
    s = 0
    for j in range(90):
        a = 2 * math.pi * (j + .3) / 90; s += 1
        m.merge(hair((R * .985 * math.cos(a), R * .985 * math.sin(a), -.0006), (math.cos(a) * .3, math.sin(a) * .3, -1), .018 + .016 * ((j * 7) % 5) / 4, s, width=.0003, k=12, sway=.45))
    # the stomach: a short tube with a frilled mouth
    m.merge(lathe(np.array([.0001, .0045, .0055, .0055, .0065, .0045]), np.array([.0005, -.0005, -.004, -.008, -.0105, -.012]), 32, lambda f, a: (.6 + .3 * a / 6.28, .55 + .1 * f)))
    return m


def seanettle():
    m = M(); R, H = .028, .021
    m.merge(bell(R, H, power=.85, lobes=16, depth=.10, thick=.45, wave=.03, rim_drop=.001))
    for q in range(4):
        a = q * math.pi / 2
        pts = []
        for i in range(30):
            f = i / 29
            pts.append((math.cos(a + f * 3.0) * (.003 + .007 * f), math.sin(a + f * 3.0) * (.003 + .007 * f), -.003 - .08 * f))
        m.merge(frill(pts, np.linspace(.012, .005, 30), cols=9, amp=.6, freq=500, seed=20 + q, normal_hint=(math.cos(a), math.sin(a), 0)))
    s = 100
    for j in range(32):
        a = 2 * math.pi * (j + .5) / 32; s += 1
        m.merge(hair((R * .93 * math.cos(a), R * .93 * math.sin(a), -.001), (math.cos(a) * .07, math.sin(a) * .07, -1), .065 + .02 * (j % 4) / 3, s, width=.0006, sway=.45))
    return m


def periphylla():
    m = M()
    m.merge(bell(.023, .036, power=1.0, lobes=16, depth=.07, thick=.25, wave=.02, flat=-.35, rows=26))
    # the red stomach seen through the clear helmet
    fr = np.linspace(0, 1, 16)
    m.merge(lathe(np.maximum(.012 * np.sin(fr * math.pi * .9) ** .8, .0003), .028 - .03 * fr, 40, lambda f, a: bell_uv(f * .3, a)))
    s = 200
    for j in range(12):
        a = 2 * math.pi * (j + .5) / 12; s += 1
        # rooted on the inside of the margin, between two lobes, then out and up as the helmet jellyfish holds them
        m.merge(hair((.0195 * math.cos(a), .0195 * math.sin(a), .0015), (math.cos(a) * .9, math.sin(a) * .9, .05), .03, s, width=.0018, k=12, sway=.2, curl=.8, taper=.2))
    return m


def comb():
    m = M()
    fr = np.linspace(0, 1, 24)
    r = .015 * np.sin(fr * math.pi) ** .9
    z = .0145 * np.cos(fr * math.pi)
    m.merge(lathe(np.maximum(r, .0003), z, 64, lambda f, a: (.05 + .4 * a / 6.283, .55 + .4 * f)))
    # the gut inside
    m.merge(lathe(np.maximum(.004 * np.sin(fr * math.pi), .0002), .009 * np.cos(fr * math.pi), 24, lambda f, a: (.6 + .3 * a / 6.28, .55 + .1 * f)))
    for j in range(8):
        a = 2 * math.pi * j / 8
        pts = [((.0157 * math.sin(f * math.pi) ** .9 + .0004) * math.cos(a), (.0157 * math.sin(f * math.pi) ** .9 + .0004) * math.sin(a), .0148 * math.cos(f * math.pi)) for f in np.linspace(.10, .90, 20)]
        m.merge(ribbon(pts, .0016, np.array([math.cos(a), math.sin(a), 0]), .52, .98, .05 + .05 * j, .45, cross=False))
    for side in (-1, 1):
        base = np.array([side * .007, 0, -.006]); dvec = np.array([side * .35, 0, -1]); dvec = dvec / np.linalg.norm(dvec)
        m.merge(hair(base, dvec, .065, 300 + side, width=.0006, k=24, sway=.45))
        for b in range(16):
            f = (b + 1) / 17
            p = base + dvec * .065 * f
            m.merge(hair(p, (side * .9, .3 * (b % 2 * 2 - 1), -.3), .010 * (1 - f * .5), 400 + b + side * 50, width=.00028, k=6, sway=.3))
    return m


def fbm(n, seed, octaves=5):
    r = np.random.default_rng(seed); out = np.zeros((n, n)); amp = 1.0; tot = 0
    for o in range(octaves):
        g = 4 * 2 ** o
        grid = r.uniform(-1, 1, (g + 1, g + 1))
        x = np.linspace(0, g, n, endpoint=False)
        i = x.astype(int); t = x - i; t = t * t * (3 - 2 * t)
        a = grid[i][:, i]; b = grid[i][:, i + 1]; c = grid[i + 1][:, i]; d = grid[i + 1][:, i + 1]
        tx = t[None, :]; ty = t[:, None]
        out += amp * ((a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty)
        tot += amp; amp *= .5
    return out / tot


S = 512
yy, xx = np.mgrid[0:S, 0:S]
U = (xx + .5) / S; V = (yy + .5) / S


def texture(sp, col):
    import colorsys
    name, bell_c, acc, ten, glow = col
    bell_c = np.array(bell_c); acc = np.array(acc); ten = np.array(ten)
    img = np.zeros((S, S, 4))
    nz = fbm(S, sum(map(ord, sp))); nz2 = fbm(S, 77, 6)
    du, dv = U - .25, V - .75
    rr = np.sqrt(du * du + dv * dv) / .235; ang = np.arctan2(dv, du)
    # base: clearer in the middle of the bell, denser to the margin (the jelly is seen edge-on there), soft cloudy noise
    a = .16 + .30 * rr ** 2.2 + .10 * nz
    c = bell_c[None, None, :] * (1 + .12 * nz2[..., None])
    canals = smooth(.985, 1.0, np.cos(ang * 16) * .5 + .5) * smooth(.25, .5, rr)          # radial canals
    rings = (np.sin(rr * 120) * .5 + .5) ** 6 * smooth(.6, .9, rr) * .5                     # circular muscle bands near the margin
    margin = smooth(.94, 1.0, rr)
    if sp == 'LionsMane':
        veins = smooth(.80, 1.0, np.cos(ang * 8) * .5 + .5) * smooth(.15, .7, rr)
        core = smooth(.5, .05, rr) * (.7 + .3 * nz2)
        c = c * (1 - .4 * veins[..., None]) + acc * .4 * veins[..., None]
        c = c * (1 - core[..., None] * .55) + acc * core[..., None] * .55
        a = a + .30 * core + .18 * veins + .2 * rings + .25 * margin
    elif sp == 'Aurelia':
        g = np.zeros_like(rr)
        for q in range(4):
            ca = q * math.pi / 2 + math.pi / 4
            cx, cy = .25 + .085 * math.cos(ca), .75 + .085 * math.sin(ca)
            d = np.sqrt((U - cx) ** 2 + (V - cy) ** 2) / .235
            band = smooth(.11, .07, np.abs(d - .17)) * smooth(-.08, .25, (du * math.cos(ca) + dv * math.sin(ca)) / .235 + .05)
            g = np.maximum(g, band * (.75 + .25 * np.sin(d * 200) ** 2))
        branched = smooth(.96, 1.0, np.cos(ang * 32 + np.sin(rr * 30) * .6) * .5 + .5) * smooth(.45, .75, rr) * .6
        c = c * (1 - g[..., None]) + acc * g[..., None]
        a = .05 + .07 * rr ** 2 + .45 * g + .22 * branched + .3 * margin + .03 * nz
    elif sp == 'Crystal':
        canals = smooth(.94, 1.0, np.cos(ang * 50) * .5 + .5) * smooth(.12, .25, rr)
        gon = canals * smooth(.45, .55, rr) * smooth(.92, .85, rr)
        dots = smooth(.70, 1.0, np.cos(ang * 48) * .5 + .5) * smooth(.05, 0, np.abs(rr - .965))
        c = c * (1 - (canals * .45)[..., None]) + acc * (canals * .45 + gon * .5 + dots)[..., None]
        a = .06 + .22 * canals + .25 * gon + .95 * dots + .08 * rr + .25 * margin
    elif sp == 'SeaNettle':
        st = np.cos(ang * 16 + np.sin(rr * 9) * .15) * .5 + .5
        stripes = smooth(.5, .85, st) * smooth(.08, .3, rr)
        spots = smooth(.55, .8, nz2) * .3
        c = c * (1 - (stripes * .8)[..., None]) + acc * (stripes * .8)[..., None]
        c = c * (1 - spots[..., None] * .3)
        a = .38 + .22 * rr + .25 * stripes + .2 * margin
    elif sp == 'Periphylla':
        inner = smooth(.55, .15, rr) * (.8 + .2 * nz2)
        c = c * (1 - inner[..., None]) + acc * inner[..., None]
        a = .07 + .85 * inner + .08 * rr + .2 * margin + .12 * rings
    else:
        c = bell_c[None, None, :] * (.9 + .1 * nz2[..., None])
        a = .10 + .06 * nz + .25 * smooth(.85, 1.0, np.abs(np.sin((U - .05) / .4 * math.pi * 4)))
    m = (U < .5) & (V >= .5)
    img[m, :3] = np.clip(c, 0, 1)[m]; img[m, 3] = np.clip(a, 0, .95)[m]
    # oral arms (u .5-1, v .5-1): frilled lace, the edges denser, a gutter down the middle
    m = (U >= .5) & (V >= .5)
    x = (U - .5) / .5; y = (V - .5) / .5
    edge = smooth(.22, .0, np.minimum(x, 1 - x))
    lace = (np.sin(y * 160 + x * 7) * .5 + .5) ** 3 * smooth(.35, .05, np.minimum(x, 1 - x))
    gutter = smooth(.06, 0, np.abs(x - .5))
    oc = bell_c * .4 + acc * .6
    arm = oc[None, None, :] * (.85 + .25 * nz2[..., None]) * (1 - edge[..., None] * .25) + acc * edge[..., None] * .25
    img[m, :3] = np.clip(arm, 0, 1)[m]; img[m, 3] = np.clip(.14 + .38 * edge + .25 * lace + .2 * gutter + .08 * nz - .1 * y, .04, .85)[m]
    # tentacles (u 0-.5, v 0-.5): beaded with stinging cells, fading to the tip
    m = (U < .5) & (V < .5)
    y = V / .5
    beads = (np.sin(y * 260) * .5 + .5) ** 4
    tcol = ten[None, None, :] * (.85 + .2 * beads[..., None])
    img[m, :3] = np.clip(tcol, 0, 1)[m]; img[m, 3] = np.clip(.62 - .45 * y + .15 * beads, .08, .9)[m]
    # rainbow strip for the comb rows
    m = (U >= .5) & (V < .5)
    h = ((U - .5) / .5 * 2 + (V / .5) * .5) % 1.0
    rgb = np.array([colorsys.hsv_to_rgb(hh, .65, 1.0) for hh in np.linspace(0, 1, 128)])
    hi = np.clip((h * 127).astype(int), 0, 127)
    comb_teeth = (np.sin(V / .5 * 400) * .5 + .5) ** 2
    img[m, :3] = rgb[hi][m]; img[m, 3] = (.45 + .45 * comb_teeth)[m]
    return img


SPECIES = {
    'LionsMane': lionsmane, 'Aurelia': aurelia, 'Crystal': crystal,
    'SeaNettle': seanettle, 'Periphylla': periphylla, 'CombJelly': comb,
}

def save_png(img, path):
    Image.fromarray((np.clip(img[::-1], 0, 1) * 255).astype(np.uint8), 'RGBA' if img.shape[2] == 4 else 'RGB').save(path, optimize=True)


def write_fpmesh(m, name):
    v, n, uv, t = m.arrays()
    np.savez(os.path.join(OUT, 'cache', name + '.npz'), v=v, n=n, uv=uv, t=t)
    Uv = np.stack([-v[:, 0], v[:, 2], -v[:, 1]], 1); Nn = np.stack([-n[:, 0], n[:, 2], -n[:, 1]], 1)
    tt = t[:, [0, 2, 1]].reshape(-1)
    with open(os.path.join(OUT, 'meshes', name + '.fpmesh'), 'wb') as f:
        f.write(struct.pack('<ii', len(Uv), len(tt)))
        f.write(Uv.astype('<f4').tobytes()); f.write(Nn.astype('<f4').tobytes()); f.write(uv.astype('<f4').tobytes()); f.write(tt.astype('<i4').tobytes())
    return len(Uv), len(t)


# ---------------------------------------------------------------- the jar and the aquarium
def cyl(r0, r1, z0, z1, n=48, uvx=(0, 1), uvy=(0, 1), flip=False):
    P = []; UV = []
    for i, (r, z) in enumerate(((r0, z0), (r1, z1))):
        for j in range(n):
            a = 2 * math.pi * j / n
            P.append((r * math.cos(a), r * math.sin(a), z)); UV.append((uvx[0] + (uvx[1] - uvx[0]) * j / n, uvy[i]))
    return surface(np.array(P), np.array(UV), 2, n, True, flip)


def disc(r, z, n=48, up=True, uvc=(.5, .5), uvr=.5):
    P = [(0, 0, z)] + [(r * math.cos(2 * math.pi * j / n), r * math.sin(2 * math.pi * j / n), z) for j in range(n)]
    UV = [uvc] + [(uvc[0] + uvr * math.cos(2 * math.pi * j / n), uvc[1] + uvr * math.sin(2 * math.pi * j / n)) for j in range(n)]
    T = [(0, 1 + j, 1 + (j + 1) % n) for j in range(n)]
    T = np.array(T) if up else np.array(T)[:, [0, 2, 1]]
    m = M(); m.add(P, [(0, 0, 1 if up else -1)] * len(P), UV, T); return m


def jar():
    # glass: two walls (outer and inner) so the edges read denser, like real glass seen edge-on; a thick bottom
    prof = [(0, .0015), (.046, .0012), (.053, .004), (.0555, .012), (.0555, .135), (.0525, .148), (.0445, .156), (.0425, .158), (.0425, .1665)]
    glass = lathe(np.array([p[0] for p in prof]), np.array([p[1] for p in prof]), 64, lambda f, a: (a / 6.283, f))
    inner = [(p[0] - .0022 if p[0] > .01 else p[0], p[1] + (.004 if p[1] < .01 else 0)) for p in prof]
    glass.merge(lathe(np.array([p[0] for p in inner]), np.array([p[1] for p in inner]), 64, lambda f, a: (a / 6.283, f), flip=True))
    # sea water up to the shoulder; its surface has its own place on the texture (upper half)
    water = lathe(np.array([0, .0505, .0528, .0528]), np.array([.0058, .0058, .012, .128]), 48, lambda f, a: (a / 6.283, f * .5))
    water.merge(disc(.0528, .128, 48, True, (.5, .75), .24))
    # lid: a screw cap with ribs, a rolled bottom edge and a domed top, opaque, lit like the game's own things
    lid = M()
    ribs = 36
    P = []; UV = []
    for i, z in enumerate((.1615, .1775)):
        for j in range(ribs * 2):
            a = 2 * math.pi * j / (ribs * 2); r = .0458 + (.0007 if j % 2 == 0 else 0)
            P.append((r * math.cos(a), r * math.sin(a), z)); UV.append((j / (ribs * 2), .58 + .34 * i))
    lid.merge(surface(np.array(P), np.array(UV), 2, ribs * 2, True, flip=False))
    lid.merge(lathe(np.array([.0455, .0476, .0478, .0476, .0458]), np.array([.1585, .1590, .1600, .1612, .1618]), 72, lambda f, a: (a / 6.283, .54)))
    lid.merge(lathe(np.array([.0465, .0468, .045, .040, .030, .015]), np.array([.1775, .1790, .1805, .1815, .1822, .1826]), 72, lambda f, a: (.5 + .22 * (1 - f) * math.cos(a), .25 + .22 * (1 - f) * math.sin(a))))
    lid.merge(disc(.015, .1826, 72, True, (.5, .25), .22 * .015 / .0465 * 3))
    lid.merge(disc(.0458, .1615, 72, False, (.5, .25), .22))
    return glass, water, lid


def glass_tex(n=256):
    """RGBA: almost clear, two vertical highlights, the thick bottom and the shoulder denser."""
    y, x = (np.mgrid[0:n, 0:n] + .5) / n
    a = np.full((n, n), .045)
    for u0, w, k in ((.11, .018, .55), (.135, .006, .35), (.61, .010, .28), (.36, .04, .06)):
        d = np.minimum(np.abs(x - u0), 1 - np.abs(x - u0))
        a += k * np.exp(-(d / w) ** 2) * smooth(.04, .2, y) * smooth(.97, .8, y)
    a += .22 * smooth(.08, .0, y) + .16 * smooth(.86, .97, y)
    rgb = np.stack([np.full((n, n), .90), np.full((n, n), .96), np.full((n, n), .97)], -1)
    return np.concatenate([rgb, np.clip(a, 0, .8)[..., None]], -1)


def water_tex(n=256):
    """RGBA: sea water, green-grey and a little cloudy; the upper half is its surface with a brighter meniscus at the glass."""
    y, x = (np.mgrid[0:n, 0:n] + .5) / n
    img = np.zeros((n, n, 4))
    side = y < .5
    f = y / .5
    img[..., 0] = .22; img[..., 1] = .46; img[..., 2] = .43
    img[..., 3] = np.where(side, .07 + .05 * (1 - f) + .02 * np.sin(x * 6.283 * 3 + f * 2) ** 2, 0)
    r = np.sqrt((x - .5) ** 2 + (y - .75) ** 2) / .24
    top = ~side
    ripple = .04 * np.sin(r * 40) * smooth(1, .3, r)
    surf = .16 + ripple + .34 * smooth(.82, 1.0, r)
    img[top, 0] = .58; img[top, 1] = .80; img[top, 2] = .76
    img[..., 3] = np.where(top, np.clip(surf, 0, .75), img[..., 3])
    return img


def lid_tex(n=256):
    """Faded red enamel, worn to the tin at the ribs, rust in the scratches; a printed ring on the top."""
    y, x = (np.mgrid[0:n, 0:n] + .5) / n
    nz = fbm(n, 21); nz2 = fbm(n, 33, 6)
    enamel = np.array([.52, .17, .13]) * (.85 + .25 * nz[..., None]) + np.array([.08, .09, .08]) * smooth(.3, .8, nz2)[..., None]
    tin = np.array([.62, .62, .58]) * (.9 + .1 * np.sin(x * 900))[..., None]
    rust = np.array([.40, .20, .09]) * (.8 + .3 * nz2[..., None])
    img = enamel.copy()
    side = y > .5
    ribs = (np.sin(x * 6.283 * 72) * .5 + .5) ** 6
    worn = np.clip(smooth(.55, .8, nz2) * .8 + ribs * side * .55 + smooth(.9, .97, y) * .6 + smooth(.6, .54, y) * side * .7, 0, 1)
    img = img * (1 - worn[..., None]) + tin * worn[..., None]
    rusty = smooth(.62, .78, fbm(n, 47, 6)) * (.4 + .6 * worn)
    img = img * (1 - rusty[..., None]) + rust * rusty[..., None]
    r = np.sqrt((x - .5) ** 2 + (y - .25) ** 2)
    ring = (smooth(.006, 0, np.abs(r - .15)) + smooth(.004, 0, np.abs(r - .175))) * (y < .5)
    img = img * (1 - .35 * ring[..., None]) + np.array([.75, .68, .50]) * (.25 * ring[..., None])
    return np.clip(img, 0, 1)


def quad(a, b, c, d, n, uv):
    m = M(); m.add([a, b, c, d], [n] * 4, [(uv[0], uv[2]), (uv[1], uv[2]), (uv[1], uv[3]), (uv[0], uv[3])], [(0, 1, 2), (0, 2, 3)]); return m


def box(x0, x1, y0, y1, z0, z1, uv, top=True, bottom=True, sides_uv=None, top_uv=None):
    m = M(); su = sides_uv or uv; tu = top_uv or uv
    if top: m.merge(quad((x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1), (0, 0, 1), tu))
    if bottom: m.merge(quad((x0, y1, z0), (x1, y1, z0), (x1, y0, z0), (x0, y0, z0), (0, 0, -1), uv))
    m.merge(quad((x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1), (0, -1, 0), su))
    m.merge(quad((x1, y1, z0), (x0, y1, z0), (x0, y1, z1), (x1, y1, z1), (0, 1, 0), su))
    m.merge(quad((x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1), (1, 0, 0), su))
    m.merge(quad((x0, y1, z0), (x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (-1, 0, 0), su))
    return m


def flipped(m):
    o = M()
    for i in range(len(m.v)):
        base = sum(len(a) for a in m.v[:i])
        o.add(m.v[i], -m.n[i], m.uv[i], (m.t[i] - base)[:, [0, 2, 1]])
    return o


def two_sided(m):
    o = M()
    for i in range(len(m.v)):
        base = sum(len(a) for a in m.v[:i])
        t = m.t[i] - base
        o.add(m.v[i], m.n[i], m.uv[i], t)
        o.add(m.v[i] - m.n[i] * .0005, -m.n[i], m.uv[i], t[:, [0, 2, 1]])
    return o


def aquarium():
    """An old-style tank: a varnished wooden stand with a moulding, dark iron corners and rim, a thick glass with green edges, open at the top;
    sea water with its surface, sand, stones and kelp. Same size as before (.60 x .34 x .40 on a .035 stand) so the mod's numbers hold."""
    W, D, Hh, base = .60, .34, .40, .035
    wood = (0, .5, 0, .5); metal = (.5, 1, 0, .25); sand = (0, .5, .5, 1); stone = (.5, .75, .25, .5); kelp_uv = (.5, 1, .5, 1)
    frame = M()
    frame.merge(box(-W / 2 - .018, W / 2 + .018, -D / 2 - .018, D / 2 + .018, 0, .012, wood))                 # the plinth
    frame.merge(box(-W / 2 - .012, W / 2 + .012, -D / 2 - .012, D / 2 + .012, .012, base - .004, wood))        # the body of the stand
    frame.merge(box(-W / 2 - .016, W / 2 + .016, -D / 2 - .016, D / 2 + .016, base - .004, base, wood))       # the moulding under the glass
    frame.merge(box(-W / 2 + .006, W / 2 - .006, -D / 2 + .006, D / 2 - .006, base, base + .016, sand, bottom=False))   # sand
    e = .014
    for sx in (-1, 1):
        for sy in (-1, 1):
            x0 = sx * W / 2 - (e if sx > 0 else 0) + (.003 * sx); y0 = sy * D / 2 - (e if sy > 0 else 0) + (.003 * sy)
            frame.merge(box(x0, x0 + e, y0, y0 + e, base, base + Hh, metal))
    for sy in (-1, 1):
        for z0 in (base, base + Hh - e):
            y0 = sy * D / 2 - (e if sy > 0 else 0) + .003 * sy
            frame.merge(box(-W / 2, W / 2, y0, y0 + e, z0, z0 + e, metal))
    for sx in (-1, 1):
        for z0 in (base, base + Hh - e):
            x0 = sx * W / 2 - (e if sx > 0 else 0) + .003 * sx
            frame.merge(box(x0, x0 + e, -D / 2, D / 2, z0, z0 + e, metal))
    # stones half sunk in the sand
    r0 = np.random.default_rng(5)
    for k, (x, y, sz) in enumerate([(-.19, .07, .04), (-.13, .10, .024), (.21, -.06, .034), (.16, .09, .02), (-.05, -.10, .016), (.05, .11, .028)]):
        lob = 1 + .18 * smooth_noise(19, 70 + k, 2.0)[:18]
        st = lathe(np.array([.0, sz * .8, sz, sz * .85, sz * .45, 0]), np.array([0, .002, sz * .35, sz * .7, sz * .95, sz]), 18,
                   lambda f, a: (stone[0] + (stone[1] - stone[0]) * a / 6.283, stone[2] + (stone[3] - stone[2]) * f))
        for vv in st.v:
            j = (np.round(np.arctan2(vv[:, 1], vv[:, 0]) / 6.283 * 18) % 18).astype(int)
            vv[:, 0] *= lob[j] * 1.25; vv[:, 1] *= lob[j]
            vv[:, 2] = vv[:, 2] * .8 + base + .010
            vv[:, 0] += x; vv[:, 1] += y
        frame.merge(st)
    # kelp: wavy blades from the sand, seen from both sides
    for k, (x, y, h) in enumerate([(-.22, .10, .27), (-.20, .12, .21), (-.17, .11, .30), (.19, .11, .24), (.23, .08, .19), (.02, .13, .17)]):
        pts = [(x + .012 * math.sin(t * 9 + k), y + .006 * math.cos(t * 7 + k), base + .014 + t * h) for t in np.linspace(0, 1, 18)]
        wdt = np.array([.010 + .010 * math.sin(math.pi * min(1, t * 1.2)) for t in np.linspace(0, 1, 18)])
        frame.merge(two_sided(ribbon(pts, wdt, np.array([0, 1.0, 0]), kelp_uv[0], kelp_uv[1], kelp_uv[2], kelp_uv[3], cross=False, twist=1.4 + .3 * k)))
    # glass: four thick panes (an outer and an inner face), open at the top
    glass = M()
    g0, g1 = .003, .009
    for inset, flip in ((g0, False), (g1, True)):
        x0, x1, y0, y1, z0, z1 = -W / 2 + inset, W / 2 - inset, -D / 2 + inset, D / 2 - inset, base + .002, base + Hh - .004
        part = box(x0, x1, y0, y1, z0, z1, (0, 1, 0, 1), top=False, bottom=False)
        if flip: part = flipped(part)
        glass.merge(part)
    water = box(-W / 2 + .010, W / 2 - .010, -D / 2 + .010, D / 2 - .010, base + .016, base + Hh * .88, (0, 1, 0, .5), bottom=False, sides_uv=(0, 1, 0, .5), top_uv=(0, 1, .5, 1))
    return frame, glass, water


def metal_tex(n=256, gold=True):
    img = np.zeros((n, n, 3))
    y, x = np.mgrid[0:n, 0:n] / n
    base = np.array([.78, .66, .38]) if gold else np.array([.62, .64, .66])
    grain = .9 + .1 * np.sin(x * 400 + np.sin(y * 30) * 3)
    img[:] = base * grain[..., None]
    # a stamped ring on the cap top (lower half of the texture)
    r = np.sqrt((x - .5) ** 2 + (y - .25) ** 2)
    ring = smooth(.006, 0, np.abs(r - .16)) + smooth(.004, 0, np.abs(r - .19))
    img *= (1 - .25 * ring)[..., None]
    return img


def aqua_tex(n=512):
    y, x = (np.mgrid[0:n, 0:n] + .5) / n
    img = np.zeros((n, n, 3))
    nz = fbm(n, 61); nz2 = fbm(n, 62, 6); nz3 = fbm(n, 63, 6)
    # wood (u<.5, v<.5): dark varnished oak, long grain with knots of figure
    w = (x < .5) & (y < .5)
    grain = np.sin(y * 260 + nz * 14 + np.sin(x * 9) * 2) * .5 + .5
    fine = np.sin(y * 1400 + nz2 * 30) * .5 + .5
    wood = np.array([.36, .21, .11])[None, None, :] * (.7 + .25 * grain[..., None] + .1 * fine[..., None] + .15 * nz3[..., None])
    img[w] = wood[w]
    # iron (u>.5, v<.25): black-green paint, worn to the metal on the edges, a little rust
    m = (x >= .5) & (y < .25)
    paint = np.array([.10, .13, .12])[None, None, :] * (.85 + .2 * nz[..., None])
    bare = np.array([.42, .42, .40])[None, None, :] * (.9 + .1 * np.sin(x * 2000)[..., None])
    wear = smooth(.62, .8, nz2) * .8
    rust = np.array([.36, .18, .08])[None, None, :]
    iron = paint * (1 - wear[..., None]) + bare * wear[..., None]
    rr = smooth(.66, .8, nz3) * .7
    iron = iron * (1 - rr[..., None]) + rust * rr[..., None]
    img[m] = iron[m]
    # stone (.5-.75, .25-.5): grey granite with speckles
    m = (x >= .5) & (x < .75) & (y >= .25) & (y < .5)
    speck = (rng.uniform(0, 1, (n, n)) > .93) * .25
    stone = np.array([.42, .41, .39])[None, None, :] * (.7 + .4 * nz2[..., None]) + speck[..., None] * np.array([.5, .5, .5]) - (rng.uniform(0, 1, (n, n)) > .95)[..., None] * .12
    img[m] = stone[m]
    img[(x >= .75) & (y >= .25) & (y < .5)] = .3
    # sand with small pebbles and bits of shell (u<.5, v>.5)
    p = (x < .5) & (y >= .5)
    sandc = np.array([.62, .55, .42])[None, None, :] * (.85 + .2 * nz2[..., None]) + (rng.uniform(-.06, .06, (n, n)))[..., None]
    pts = rng.uniform(0, 1, (300, 2)) * [.5, .5] + [0, .5]; cols = rng.uniform(.3, .8, (300, 1)) * np.array([[1, .96, .9]])
    for (px, py), cc in zip(pts, cols):
        rad = rng.uniform(.004, .009)
        d = (x - px) ** 2 + (y - py) ** 2
        mm = d < rad ** 2
        sandc[mm] = cc * (1 - d[mm][..., None] / rad ** 2 * .45)
    img[p] = sandc[p]
    # kelp (u>.5, v>.5): olive brown blade, darker midrib, lighter towards the tip
    k = (x >= .5) & (y >= .5)
    across = np.abs((x - .75) / .25)
    along = (y - .5) / .5
    kelp = np.array([.42, .40, .14])[None, None, :] * (1 + .5 * along[..., None]) * (.8 + .25 * nz[..., None])
    kelp *= (1 - .35 * smooth(.12, 0, across))[..., None]
    kelp *= (.85 + .15 * np.sin(y * 300 + across * 8))[..., None]
    img[k] = kelp[k]
    return np.clip(img, 0, 1)


def aqua_glass_tex(n=256):
    """RGBA: plate glass is clear face on and green at its edges; two soft diagonal glints and a few faint smudges."""
    y, x = (np.mgrid[0:n, 0:n] + .5) / n
    edge = np.minimum(np.minimum(x, 1 - x), np.minimum(y, 1 - y))
    a = .035 + .50 * smooth(.022, .004, edge)
    for c, wdt, k in ((.25, .025, .07), (.31, .008, .07), (.95, .04, .04)):
        d = np.abs((x + y * .55) - c)
        a += k * np.exp(-(d / wdt) ** 2)
    a += .04 * smooth(.6, .9, fbm(n, 77, 5))
    rgb = np.stack([.70 + .2 * (1 - smooth(.022, .004, edge)), np.full((n, n), .92), .80 + .12 * (1 - smooth(.022, .004, edge))], -1)
    return np.concatenate([rgb, np.clip(a, 0, .8)[..., None]], -1)


def soft_noise(n, seed, freq):
    """Smooth isotropic noise, -1..1: white noise low-passed in the frequency domain (no grid, tiles seamlessly)."""
    w = np.random.default_rng(seed).standard_normal((n, n))
    fy = np.fft.fftfreq(n)[:, None] * n; fx = np.fft.fftfreq(n)[None, :] * n
    f = np.fft.ifft2(np.fft.fft2(w) * np.exp(-(fx * fx + fy * fy) / (2 * freq * freq))).real
    return f / np.abs(f).max()


def aqua_water_tex(n=256):
    """RGBA: sides (lower half) deeper green towards the bottom; the surface (upper half) with ripples and a bright line at the glass."""
    y, x = (np.mgrid[0:n, 0:n] + .5) / n
    img = np.zeros((n, n, 4))
    side = y < .5; f = y / .5
    img[..., 0] = .20; img[..., 1] = .44; img[..., 2] = .42
    img[..., 3] = np.where(side, .075 + .07 * (1 - f), 0)
    top = ~side
    ty = (y - .5) / .5
    edge = np.minimum(np.minimum(x, 1 - x), np.minimum(ty, 1 - ty))
    # soft irregular swell, no grid: smooth noise stretched along the long side of the tank, its slopes a little brighter
    rip = .06 * soft_noise(n, 91, 9.0) + .03 * soft_noise(n, 92, 22.0)
    img[top, 0] = .55; img[top, 1] = .78; img[top, 2] = .74
    img[..., 3] = np.where(top, np.clip(.12 + rip + .35 * smooth(.025, .0, edge), 0, .6), img[..., 3])
    return img


def dead_and_food():
    """A dead jellyfish slumped flat with its folds, dried jellyfish as crinkled amber shreds, a coil of tentacles (bait). Opaque."""
    r0 = np.random.default_rng(11)
    # dead: an irregular slumped blob, thick in the middle, thin frilly edge, a few loose tentacles
    n, rows = 72, 16
    lobe = 1 + .12 * smooth_noise(n + 1, 3, 2.0)[:n] + .05 * np.sin(np.arange(n) * 2 * math.pi / n * 8)
    P = []; UV = []
    for i in range(rows):
        f = i / (rows - 1)
        for j in range(n):
            a = 2 * math.pi * j / n
            r = .048 * f * lobe[j]
            z = .011 * (1 - f ** 2) ** .7 + .0025 * f ** 2 * math.sin(a * 11 + f * 4) + .002
            P.append((r * math.cos(a), r * math.sin(a), z)); UV.append(bell_uv(f, a))
    dead = surface(np.array(P), np.array(UV), rows, n, True)
    for j in range(9):
        a = j * .7 + .2
        dead.merge(hair((.036 * math.cos(a), .036 * math.sin(a), .0025), (math.cos(a), math.sin(a), -.03), .028 + .01 * (j % 3), 600 + j, width=.0022, k=16, sway=.9, taper=.4))
    # dried: crinkled shreds lying over each other
    dried = M()
    for k in range(7):
        L = r0.uniform(.05, .09); W = r0.uniform(.010, .018); a = r0.uniform(0, math.pi); cx, cy = r0.uniform(-.015, .015, 2)
        rows_, cols_ = 14, 5
        P = []; UV = []
        u0 = r0.uniform(.52, .7)
        for i in range(rows_):
            s_ = (i / (rows_ - 1) - .5) * L
            for c in range(cols_):
                w = (c / (cols_ - 1) - .5) * W * (1 - .35 * abs(i / (rows_ - 1) - .5) * 2)
                x = cx + s_ * math.cos(a) - w * math.sin(a); y = cy + s_ * math.sin(a) + w * math.cos(a)
                z = .0015 * k + .0018 + .0015 * math.sin(s_ * 260 + c) + .001 * math.sin(w * 500)
                P.append((x, y, z)); UV.append((u0 + .25 * c / (cols_ - 1), .52 + .46 * i / (rows_ - 1)))
        dried.merge(surface(np.array(P), np.array(UV), rows_, cols_, False))
    # bait: a coiled bundle of tentacles
    bait = M()
    for k in range(7):
        pts = [((.012 + .002 * k) * math.cos(t + k), (.012 + .002 * k) * math.sin(t + k), .002 + .0012 * t) for t in np.linspace(0, 9, 40)]
        bait.merge(ribbon(pts, .0025, np.array([0, 0, 1.0]), .02, .48, .02, .48, cross=True))
    return dead, dried, bait


def food_tex():
    nz = fbm(S, 5); nz2 = fbm(S, 9, 6)
    img = np.zeros((S, S, 3))
    du, dv = U - .25, V - .75; rr = np.sqrt(du * du + dv * dv) / .235; ang = np.arctan2(dv, du)
    # dead jelly: pale milky pink, wet, faint radial canals, denser in the middle
    canals = smooth(.97, 1.0, np.cos(ang * 16) * .5 + .5) * smooth(.2, .6, rr)
    dead = np.array([.86, .74, .70])[None, None, :] * (.9 + .12 * nz[..., None]) - canals[..., None] * .08 + smooth(.5, 0, rr)[..., None] * np.array([.05, -.04, -.04])
    m = (U < .5) & (V >= .5); img[m] = dead[m]
    # dried: amber, fibrous streaks along the shred, darker edges, glossy specks
    m = (U >= .5) & (V >= .5)
    fib = (np.sin((U - .5) * 900 + nz2 * 8) * .5 + .5) ** 2
    dried = np.array([.80, .55, .25])[None, None, :] * (.75 + .2 * fib[..., None] + .15 * nz[..., None])
    img[m] = dried[m]
    # tentacles: reddish, beaded
    m = (U < .5) & (V < .5)
    beads = (np.sin(V * 520) * .5 + .5) ** 3
    img[m] = (np.array([.82, .48, .44])[None, None, :] * (.8 + .2 * beads[..., None] + .1 * nz[..., None]))[m]
    img[(U >= .5) & (V < .5)] = .5
    return np.clip(img, 0, 1)


if __name__ == '__main__':
    info = {}
    for sp, fn in SPECIES.items():
        info[sp] = write_fpmesh(fn(), 'Jelly_' + sp)
        for col in COLOURS[sp]:
            save_png(texture(sp, col), os.path.join(OUT, 'textures', f'Jelly_{sp}_{col[0]}.png'))
    g, w, l = jar()
    info['JarGlass'] = write_fpmesh(g, 'JarGlass'); info['JarWater'] = write_fpmesh(w, 'JarWater'); info['JarLid'] = write_fpmesh(l, 'JarLid')
    save_png(lid_tex(), os.path.join(OUT, 'textures', 'JarLid.png'))
    save_png(glass_tex(), os.path.join(OUT, 'textures', 'JarGlass.png')); save_png(water_tex(), os.path.join(OUT, 'textures', 'JarWater.png'))
    f, gl, wa = aquarium()
    info['AquaFrame'] = write_fpmesh(f, 'AquaFrame'); info['AquaGlass'] = write_fpmesh(gl, 'AquaGlass'); info['AquaWater'] = write_fpmesh(wa, 'AquaWater')
    save_png(aqua_tex(), os.path.join(OUT, 'textures', 'AquaFrame.png'))
    save_png(aqua_glass_tex(), os.path.join(OUT, 'textures', 'AquaGlass.png')); save_png(aqua_water_tex(), os.path.join(OUT, 'textures', 'AquaWater.png'))
    json.dump({'colours': {k: [c[0] for c in v] for k, v in COLOURS.items()}, 'glow': {f'{k}_{c[0]}': c[4] for k, v in COLOURS.items() for c in v}, 'mesh': info},
              open(os.path.join(OUT, 'models.json'), 'w'), indent=1)
    print(json.dumps(info))

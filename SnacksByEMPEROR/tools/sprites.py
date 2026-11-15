"""Food pieces for the labels, modelled and rendered (not drawn): ../spr/<kind>_<n>.png, transparent, cropped, long side along X.
python3 sprites.py [kinds...]"""
import sys, os, math, random
import numpy as np
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__)); OUT = os.path.join(HERE, '..', 'spr'); os.makedirs(OUT, exist_ok=True)
SIZE = int(os.environ.get('SIZE', 360)); NVAR = 3


def hexc(h): h = h.lstrip('#'); return np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]) ** 2.2
def lerp(a, b, t): t = np.clip(t, 0, 1)[..., None]; return a * (1 - t) + b * t
def vnoise(p, seed, f=1.0):
    """smooth pseudo-noise of points (n,3) -> (n,) in -1..1"""
    r = np.random.RandomState(seed); o = np.zeros(len(p))
    for k in range(4):
        d = r.normal(size=3); d /= np.linalg.norm(d); ph = r.uniform(0, 6.28); fr = f * (1.7 ** k)
        d2 = r.normal(size=3); d2 /= np.linalg.norm(d2)
        o += np.sin(p @ d * fr * 6 + ph + 1.3 * np.sin(p @ d2 * fr * 4)) / (1.5 ** k)
    return o / 2.2


class M:
    def __init__(s): s.v = []; s.f = []; s.c = []; s.n = 0
    def add(s, v, f, c):
        s.v.append(np.asarray(v, float)); s.c.append(np.asarray(c, float)); s.f += [tuple(int(i) + s.n for i in q) for q in f]; s.n += len(v)
    def grid(s, P, C, wrap_u=False, wrap_v=False):
        nu, nv = P.shape[:2]; f = []
        for i in range(nu if wrap_u else nu - 1):
            for j in range(nv if wrap_v else nv - 1):
                a = i * nv + j; b = ((i + 1) % nu) * nv + j; c = ((i + 1) % nu) * nv + (j + 1) % nv; d = i * nv + (j + 1) % nv; f.append((a, b, c, d))
        s.add(P.reshape(-1, 3), f, C.reshape(-1, 3))


def tube(m, path, rad, col, sq=1.0, seg=20, lump=0.0, seed=0, closed=False, col2=None, stripe=None):
    """tube along path (n,3); rad (n,) ; sq squashes Z."""
    path = np.asarray(path, float); n = len(path); rad = np.broadcast_to(np.asarray(rad, float), (n,))
    t = np.gradient(path, axis=0); t /= np.linalg.norm(t, axis=1)[:, None] + 1e-9
    up = np.array([0, 0, 1.0]); a = np.cross(t, up); bad = np.linalg.norm(a, axis=1) < 1e-4; a[bad] = [1, 0, 0]; a /= np.linalg.norm(a, axis=1)[:, None]; b = np.cross(a, t)
    th = np.linspace(0, 2 * math.pi, seg, endpoint=False)
    P = path[:, None, :] + rad[:, None, None] * (np.cos(th)[None, :, None] * a[:, None, :] + np.sin(th)[None, :, None] * b[:, None, :] * sq)
    if lump:
        nz = vnoise(P.reshape(-1, 3) / max(rad.max(), 1e-6) * .5, seed, 1.0).reshape(n, seg)
        P += (P - path[:, None, :]) * (nz * lump)[..., None]
    C = np.broadcast_to(col, (n, seg, 3)).copy()
    if col2 is not None:
        nz = vnoise(P.reshape(-1, 3) * 3, seed + 5, 1.0).reshape(n, seg); C = lerp(C, np.broadcast_to(col2, C.shape), nz * .5 + .5)
    if stripe is not None: C = stripe(C, th, np.linspace(0, 1, n))
    m.grid(P, C, wrap_u=closed, wrap_v=True)
    if not closed:
        for e in (0, n - 1):
            m.add(np.vstack([P[e], path[e:e + 1]]), [(i, (i + 1) % seg, seg) for i in range(seg)] if e else [((i + 1) % seg, i, seg) for i in range(seg)], np.vstack([C[e], C[e].mean(0)[None]]))


def sheet(m, outline, zf, colf, n=40, thick=.02):
    """filled star-shaped outline r(theta) as a polar grid, top and bottom."""
    rr = np.linspace(0, 1, n // 2)[:, None]; th = np.linspace(0, 2 * math.pi, n * 2, endpoint=False)[None, :]
    R = outline(th); x = rr * R * np.cos(th); y = rr * R * np.sin(th); z = zf(x, y, rr * np.ones_like(th))
    for sgn in (1, -1):
        P = np.stack([x, y, z + sgn * thick * (1 - rr ** 8) * np.ones_like(th)], -1)
        m.grid(P if sgn > 0 else P[:, ::-1], colf(x, y, rr * np.ones_like(th)) if sgn > 0 else colf(x, y, rr * np.ones_like(th))[:, ::-1], wrap_v=True)


def rect(m, hx, hy, zf, colf, nx=40, ny=28, thick=.03, r=.15, edge=None):
    """rounded rectangle slab (superellipse outline)."""
    def outline(th):
        c, s = np.abs(np.cos(th)), np.abs(np.sin(th)); p = 2 / max(r, .02)
        R = ((c / hx) ** p + (s / hy) ** p) ** (-1 / p)
        return R * (edge(th) if edge else 1)
    sheet(m, outline, zf, colf, n=max(nx, ny), thick=thick)


def ball(m, c, r, col, seed=0, lump=0.0, sq=(1, 1, 1), nu=18, nv=24, colf=None):
    u = np.linspace(0.001, math.pi - .001, nu)[:, None]; v = np.linspace(0, 2 * math.pi, nv, endpoint=False)[None, :]
    D = np.stack([np.sin(u) * np.cos(v), np.sin(u) * np.sin(v), np.cos(u) * np.ones_like(v)], -1)
    rad = r * (1 + lump * vnoise(D.reshape(-1, 3) * 1.2 + seed, seed, 1.0).reshape(nu, nv))
    P = np.asarray(c) + D * rad[..., None] * np.asarray(sq)
    C = np.broadcast_to(col, P.shape).copy() if colf is None else colf(D)
    m.grid(P, C, wrap_v=True)


# ---------------------------------------------------------------- kinds
def k_chip(m, r, ridged=False):
    """A fried potato slice as on a real pack: a thin round-oval disc, pale gold, bent like a saddle with one side curled up, blistered, a few toasted specks."""
    s = r.randint(0, 999); a = r.uniform(.22, .30); b = r.uniform(.15, .22); ph = r.uniform(0, 6.28); curl = r.uniform(.08, .2); ca = r.uniform(0, 6.28)
    g, lt, e, dk = hexc('#efbf4c'), hexc('#f8dd8c'), hexc('#d48f2a'), hexc('#a5621c')
    out = lambda th: (1 + .03 * np.sin(2 * th + ph) + .018 * np.sin(3 * th + ph * 2) + .01 * np.sin(7 * th + ph * 3)) * (1 - .1 * np.sin(th + ph) ** 2)
    def zf(x, y, rr):
        u = x * np.cos(ca) + y * np.sin(ca); w = -x * np.sin(ca) + y * np.cos(ca)
        z = a * u * u - b * w * w + curl * np.clip(u - .35, 0, None) ** 2 * 1.6 + .04 * np.sin(3 * u + ph) * w
        if ridged: z = z + .04 * np.sin(x * 24)
        blister = np.clip(vnoise(np.stack([x, y, 0 * x], -1).reshape(-1, 3), s + 5, 3.2).reshape(x.shape) - .55, 0, 1) * .12
        return z + blister + .01 * vnoise(np.stack([x, y, 0 * x], -1).reshape(-1, 3), s, 2.5).reshape(x.shape)
    def colf(x, y, rr):
        P = np.stack([x, y, 0 * x], -1).reshape(-1, 3)
        nz = vnoise(P, s + 1, 1.4).reshape(x.shape)
        c = lerp(np.broadcast_to(g, x.shape + (3,)), np.broadcast_to(e, x.shape + (3,)), rr ** 3 * .95 + nz * .3)
        bl = np.clip(vnoise(P, s + 5, 3.2).reshape(x.shape) - .5, 0, 1) * 2.2
        c = lerp(c, np.broadcast_to(lt, c.shape), bl * .6)
        spot = np.clip(vnoise(P, s + 2, 5.0).reshape(x.shape) - .72, 0, 1) * 6
        return lerp(c, np.broadcast_to(dk, c.shape), np.clip(spot, 0, 1) * .5 + (rr > .96) * .25)
    sheet(m, out, zf, colf, n=64, thick=.012)
    return dict(rough=.42, bump=.35, bscale=16, tilt=r.uniform(-10, 10), elev=66, sss=.06)

def k_triangle(m, r):
    s = r.randint(0, 999); g, e = hexc('#f0a030'), hexc('#c46a14'); ph = r.uniform(0, 6)
    def out(th):
        t = (th + math.pi / 2) % (2 * math.pi / 3) - math.pi / 3
        return .62 / np.cos(t) * (1 - .0 * th) * (1 - .12 * np.exp(-((np.abs(t) - math.pi / 3) / .12) ** 2))
    zf = lambda x, y, rr: .16 * x * x - .1 * y * y + .02 * np.sin(4 * y + ph)
    def colf(x, y, rr):
        nz = vnoise(np.stack([x, y, 0 * x], -1).reshape(-1, 3), s, 2).reshape(x.shape)
        return lerp(np.broadcast_to(g, x.shape + (3,)), np.broadcast_to(e, x.shape + (3,)), rr ** 4 + nz * .3)
    sheet(m, out, zf, colf, n=48, thick=.03)
    return dict(rough=.6, bump=.5, bscale=60, speck=('#b3200c', .35, 55))

def k_cracker(m, r):
    g, e, dk = hexc('#f1c98a'), hexc('#d99a4a'), hexc('#8a5a22')
    def colf(x, y, rr):
        c = lerp(np.broadcast_to(g, x.shape + (3,)), np.broadcast_to(e, x.shape + (3,)), rr ** 3 + .25 * np.sin(x * 3 + y * 2))
        hole = np.zeros(x.shape)
        for i in (-1, 0, 1):
            for j in (-1, 0, 1): hole = np.maximum(hole, np.exp(-(((x - i * .36) ** 2 + (y - j * .36) ** 2) / .0022)))
        return lerp(c, np.broadcast_to(dk, c.shape), hole)
    def zf(x, y, rr):
        z = .02 * np.sin(x * 4) * np.sin(y * 4)
        for i in (-1, 0, 1):
            for j in (-1, 0, 1): z = z - .03 * np.exp(-(((x - i * .36) ** 2 + (y - j * .36) ** 2) / .004))
        return z
    rect(m, .75, .75, zf, colf, nx=72, ny=72, thick=.05, r=.12, edge=lambda th: 1 + .025 * np.cos(th * 20))
    return dict(rough=.7, bump=.3, bscale=40, speck=('#fff8e8', .2, 70))

def k_flat(m, r, hx, hy, c1, c2, wav=0.0, thick=.03, corner=.12, freq=5, lump=.0, rag=0.0):
    s = r.randint(0, 999); g, e = hexc(c1), hexc(c2); ph = r.uniform(0, 6)
    zf = lambda x, y, rr: wav * np.sin(x * freq + ph) + lump * vnoise(np.stack([x, y, 0 * x], -1).reshape(-1, 3), s, 2).reshape(x.shape)
    def colf(x, y, rr):
        nz = vnoise(np.stack([x * 1.0, y * 3, 0 * x], -1).reshape(-1, 3), s + 3, 1.6).reshape(x.shape)
        return lerp(np.broadcast_to(g, x.shape + (3,)), np.broadcast_to(e, x.shape + (3,)), nz * .6 + .4 + rr ** 5 * .3)
    rect(m, hx, hy, zf, colf, nx=64, ny=40, thick=thick, r=corner, edge=(lambda th: 1 + rag * np.sin(th * 7 + ph) + rag * .6 * np.sin(th * 13)) if rag else None)

def arc(n, R, a0, a1, wob=0.0, ph=0.0):
    a = np.radians(np.linspace(a0, a1, n)); return np.stack([R * np.cos(a), R * np.sin(a) - R * .0, wob * np.sin(a * 3 + ph)], -1)

def taper(n, k=.25): t = np.linspace(0, 1, n); return np.clip(np.minimum(t, 1 - t) / k, 0, 1) ** .5

def build(kind, r):
    m = M(); o = dict(rough=.6, bump=.3, bscale=30)
    if kind == 'chip': o.update(k_chip(m, r))
    elif kind == 'ridged': o.update(k_chip(m, r, True))
    elif kind == 'triangle': o.update(k_triangle(m, r))
    elif kind == 'cracker': o.update(k_cracker(m, r))
    elif kind == 'sheet': k_flat(m, r, .95, .55, '#16361f', '#2f6b3a', wav=.03, thick=.008, corner=.06, freq=6); o.update(rough=.3, bump=.6, bscale=90, speck=('#e8dcb0', .12, 80))
    elif kind == 'strip': k_flat(m, r, 1.0, .2, '#b3261e', '#7a1408', wav=.06, thick=.03, corner=.2, freq=7); o.update(rough=.25, bump=.5, bscale=50, speck=('#f2d08a', .15, 60))
    elif kind == 'wave': k_flat(m, r, .95, .34, '#f6c39a', '#e59a6a', wav=.09, thick=.035, corner=.5, freq=9); o.update(rough=.6, bump=.5, bscale=45)
    elif kind == 'jerk': k_flat(m, r, 1.0, .36, '#5a2a14', '#2e1208', wav=.03, thick=.05, corner=.4, lump=.05, rag=.07); o.update(rough=.45, bump=1.0, bscale=14, speck=('#8a4a26', .3, 9))
    elif kind == 'slab': k_flat(m, r, 1.0, .34, '#c98a45', '#8a4f1e', thick=.09, corner=.5, lump=.03); o.update(rough=.8, bump=.9, bscale=35, speck=('#f1d9a8', .3, 22))
    elif kind == 'cube':
        # a rye crouton as in a real pack: a short dark bar cut from a loaf, porous, dusted with seasoning
        k_flat(m, r, 1.0, r.uniform(.3, .35), '#9a622c', '#6b3d18', thick=.3, corner=.045, lump=.022, rag=.012); o.update(rough=.9, bump=1.0, bscale=38, speck=('#3a1f0a', .2, 26), elev=42, tilt=r.uniform(-8, 8))
    elif kind == 'rind':
        for i in range(4): ball(m, (r.uniform(-.5, .5), r.uniform(-.25, .25), 0), r.uniform(.35, .5), hexc('#e6b46e'), seed=r.randint(0, 99), lump=.35, sq=(1.2, 1, .7))
        o.update(rough=.55, bump=1.0, bscale=10, speck=('#f7e2b8', .45, 7))
    elif kind == 'pop':
        for i in range(6):
            a = i * 1.05 + r.uniform(0, .5); rad = r.uniform(.3, .46); d = r.uniform(.25, .45)
            ball(m, (d * math.cos(a) * 1.2, d * math.sin(a), r.uniform(0, .25)), rad, hexc('#fff3d2'), seed=r.randint(0, 99), lump=.3)
        ball(m, (0, 0, .05), .3, hexc('#d99a2a'), seed=3, lump=.2)
        o.update(rough=.8, bump=.8, bscale=8, speck=('#e8b85a', .25, 5))
    elif kind == 'paw':
        col = hexc('#f0a22a')
        ball(m, (0, -.2, 0), .55, col, seed=1, lump=.1, sq=(1.2, 1, .5))
        for dx, dy in ((-.62, .3), (-.22, .55), (.22, .55), (.62, .3)): ball(m, (dx, dy, 0), .24, col, seed=r.randint(0, 9), lump=.1, sq=(1, 1.2, .7))
        o.update(rough=.75, bump=.8, bscale=40, speck=('#c9541a', .3, 45))
    elif kind in ('puff', 'flip', 'curl'):
        col, col2 = {'puff': ('#ff9a1a', '#e06a08'), 'flip': ('#e6b574', '#c98a45'), 'curl': ('#f5b53a', '#d98414')}[kind]
        if kind == 'curl': p = arc(40, .55, 150 + r.uniform(-20, 20), 420, wob=.08, ph=r.uniform(0, 6)); rad = .2
        else: p = arc(34, 1.1, 38, 142, wob=.05, ph=r.uniform(0, 6)); p[:, 1] -= 1.0; rad = .24 if kind == 'puff' else .22
        tube(m, p, rad * (.55 + .45 * taper(len(p), .15)), hexc(col), lump=.22 if kind != 'flip' else .12, seed=r.randint(0, 999), col2=hexc(col2))
        o.update(rough=.85, bump=1.0, bscale=26 if kind != 'flip' else 40, speck=('#ffd27a', .3, 60) if kind == 'puff' else None)
    elif kind == 'crescent':
        p = arc(30, .8, 50, 130); p[:, 1] -= .7; tube(m, p, .2 * (.35 + .65 * taper(30, .3)), hexc('#d9772a'), sq=.7, col2=hexc('#a84a12'), seed=r.randint(0, 99))
        o.update(rough=.2, bump=.3, bscale=30)
    elif kind in ('stick', 'roll'):
        p = np.stack([np.linspace(-1, 1, 24), np.zeros(24), np.zeros(24)], -1)
        if kind == 'stick': tube(m, p, .075, hexc('#d9a04a'), lump=.08, seed=r.randint(0, 99), col2=hexc('#a8641c')); o.update(rough=.5, bump=.6, bscale=40, speck=('#ffffff', .22, 26))
        else: tube(m, p, .17, hexc('#c0300c'), lump=.12, seed=r.randint(0, 99), col2=hexc('#7a1404')); ball(m, (1, 0, 0), .1, hexc('#3a0a02'), sq=(.3, 1, 1)); ball(m, (-1, 0, 0), .1, hexc('#3a0a02'), sq=(.3, 1, 1)); o.update(rough=.7, bump=1.0, bscale=30, speck=('#ff7a2a', .35, 50))
    elif kind == 'cone':
        t = np.linspace(0, 1, 26); p = np.stack([-0.9 + 1.8 * t, .25 * np.sin(t * 2.2), 0 * t], -1)
        tube(m, p, .05 + .36 * t, hexc('#f0bc4a'), lump=.05, seed=r.randint(0, 99), col2=hexc('#cf8a1c')); ball(m, (.9, .2, 0), .3, hexc('#8a5210'), sq=(.25, 1, 1))
        o.update(rough=.6, bump=.8, bscale=45, speck=('#fff0c0', .2, 50))
    elif kind in ('ring', 'pretzel'):
        if kind == 'ring':
            a = np.linspace(0, 2 * math.pi, 48, endpoint=False); p = np.stack([.72 * np.cos(a), .72 * np.sin(a), 0 * a], -1)
            tube(m, p, .2, hexc('#d9a04a'), lump=.18, seed=r.randint(0, 99), closed=True, col2=hexc('#b3741e'), sq=.8); o.update(rough=.7, bump=1.0, bscale=30, speck=('#f6e0a8', .3, 35))
        else:
            t = np.linspace(0, 1, 90); a = t * 2 * math.pi
            x = .95 * np.sin(a) * (1 - .0 * t) ; y = -.75 * np.cos(a) + .35 * np.cos(2 * a) - .1; z = .1 * np.sin(2 * a)
            x = np.sin(a) * (.55 + .4 * np.cos(a) ** 2 * 0) + .45 * np.sin(2 * a) * .0 + .0
            x = .9 * np.sin(a) - .5 * np.sin(2 * a) * (np.abs(t - .5) > .0) * .0
            # classic knot: a loop whose ends cross and rest on the bottom arc
            x = np.where(t < .5, 1, 1) * (.9 * np.sin(a)); y = .15 - .7 * np.cos(a)
            e = np.clip((np.abs(t - .5) - .32) / .18, 0, 1)   # the two ends
            x = x * (1 - e) + e * (np.sign(.5 - t) * (-.38)); y = y * (1 - e) + e * (.62 - .0)
            x = x - e * np.sign(.5 - t) * .0
            tube(m, np.stack([x, -y, z + .12 * e], -1), .12, hexc('#8a4a16'), seed=r.randint(0, 99), col2=hexc('#5a2a08'), lump=.05); o.update(rough=.25, bump=.4, bscale=30, speck=('#ffffff', .2, 30))
    elif kind == 'seed':
        def colf(D):
            a = np.arctan2(D[..., 2], D[..., 1]); st = (np.sin(a * 5) > .72) & (np.abs(D[..., 0]) < .85)
            return lerp(np.broadcast_to(hexc('#141414'), D.shape), np.broadcast_to(hexc('#d8d2c0'), D.shape), st * 1.0)
        u = np.linspace(.001, math.pi - .001, 26)[:, None]; v = np.linspace(0, 2 * math.pi, 32, endpoint=False)[None, :]
        D = np.stack([np.cos(u) * np.ones_like(v), np.sin(u) * np.cos(v), np.sin(u) * np.sin(v)], -1)
        prof = (1 + .45 * D[..., 0]) * .5   # wide end, pointed end
        P = np.stack([D[..., 0], D[..., 1] * prof * .95, D[..., 2] * prof * .4], -1); m.grid(P, colf(D), wrap_v=True); o.update(rough=.4, bump=.2, bscale=60)
    elif kind == 'kernel': ball(m, (0, 0, 0), .7, hexc('#f0b414'), seed=r.randint(0, 99), lump=.12, sq=(.85, 1, .6), colf=lambda D: lerp(np.broadcast_to(hexc('#f2b80e'), D.shape), np.broadcast_to(hexc('#c97a08'), D.shape), np.clip(-D[..., 1], 0, 1) ** 2 + vnoise(D.reshape(-1, 3), 3).reshape(D.shape[:-1]) * .3)); o.update(rough=.5, bump=.7, bscale=22, speck=('#fff0b0', .15, 30))
    elif kind == 'nut':
        t = np.linspace(0, 1, 30); p = np.stack([-.75 + 1.5 * t, .06 * np.sin(t * 3), 0 * t], -1); rad = .36 * (1 - .28 * np.exp(-((t - .5) / .12) ** 2)) * (.3 + .7 * taper(30, .22))
        tube(m, p, rad, hexc('#dcae6c'), seg=24, seed=r.randint(0, 99), col2=hexc('#b98440')); o.update(rough=.85, bump=1.0, bscale=18, net=True)
    elif kind == 'ball': ball(m, (0, 0, 0), .62, hexc('#c58a3a'), seed=r.randint(0, 99), lump=.1); o.update(rough=.45, bump=.9, bscale=24, speck=('#8a5a1e', .3, 14))
    elif kind == 'fish':
        t = np.linspace(0, 1, 36); p = np.stack([-1 + 2 * t, .05 * np.sin(t * 5), 0 * t], -1)
        rad = np.where(t < .8, .3 * np.sin(np.clip(t / .8, 0, 1) * math.pi) ** .7 + .03, .05 + (t - .8) * 1.5)
        tube(m, p, rad, hexc('#c9c2a8'), sq=.28, seg=20, seed=r.randint(0, 99), col2=hexc('#8a7a52'), stripe=lambda C, th, tt: lerp(C, np.broadcast_to(hexc('#6a5a3a'), C.shape), (np.sin(th)[None, :] > .5) * np.ones((len(tt), 1)) * .7))
        ball(m, (-.78, .0, .09), .045, hexc('#101010')); o.update(rough=.35, bump=.8, bscale=40)
    elif kind == 'crumb':
        for i in range(14):
            a = r.uniform(0, 6.28); c = np.array([r.uniform(-.7, .7), r.uniform(-.45, .45), r.uniform(0, .15)]); L = r.uniform(.25, .5); t = np.linspace(-1, 1, 10)
            p = c + np.stack([L * t * math.cos(a), L * t * math.sin(a) + .1 * t * t, 0 * t], -1); tube(m, p, .035, hexc(r.choice(['#e9a52a', '#d98414', '#f2c04a'])), seg=8)
        for i in range(5): ball(m, (r.uniform(-.6, .6), r.uniform(-.4, .4), .08), .09, hexc(r.choice(['#c98a3a', '#5a8a2a'])), sq=(1.3, 1, .8))
        o.update(rough=.6, bump=.3, bscale=40)
    return m, o


def render(kind, n):
    r = random.Random(hash((kind, n)) % 100000 if False else sum(map(ord, kind)) * 31 + n)
    m, o = build(kind, r)
    bpy.ops.wm.read_factory_settings(use_empty=True); sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.samples = int(os.environ.get('SAMPLES', 40)); sc.cycles.use_denoising = True
    sc.render.resolution_x = sc.render.resolution_y = SIZE; sc.render.film_transparent = True; sc.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs['Color'].default_value = (.9, .88, .85, 1); w.node_tree.nodes['Background'].inputs['Strength'].default_value = .75
    for name, e, rot, ang in [('key', 3.2, (38, 0, -40), 12), ('fill', .6, (60, 0, 140), 30)]:
        l = bpy.data.objects.new(name, bpy.data.lights.new(name, 'SUN')); l.data.energy = e; l.data.angle = math.radians(ang); l.rotation_euler = [math.radians(a) for a in rot]; sc.collection.objects.link(l)
    V = np.vstack(m.v); Cc = np.vstack(m.c)
    me = bpy.data.meshes.new(kind); me.from_pydata([tuple(x) for x in V], [], m.f); me.update()
    ca = me.color_attributes.new('Col', 'FLOAT_COLOR', 'POINT'); ca.data.foreach_set('color', np.hstack([Cc, np.ones((len(Cc), 1))]).ravel())
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new(kind, me); sc.collection.objects.link(ob)
    ob.rotation_euler = (0, math.radians(o.get('tilt', 0)), 0)
    mat = bpy.data.materials.new(kind); mat.use_nodes = True; nt = mat.node_tree; bs = nt.nodes['Principled BSDF']; L = nt.links.new
    at = nt.nodes.new('ShaderNodeAttribute'); at.attribute_name = 'Col'; col = at.outputs['Color']
    tc = nt.nodes.new('ShaderNodeTexCoord')
    if o.get('speck'):
        sc_col, amt, scale = o['speck']; nz = nt.nodes.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = scale; nz.inputs['Detail'].default_value = 2; L(tc.outputs['Object'], nz.inputs['Vector'])
        rp = nt.nodes.new('ShaderNodeValToRGB'); rp.color_ramp.elements[0].position = .62 - amt * .25; rp.color_ramp.elements[1].position = .66 - amt * .2; L(nz.outputs['Fac'], rp.inputs['Fac'])
        mx = nt.nodes.new('ShaderNodeMixRGB'); mx.inputs['Color2'].default_value = tuple(hexc(sc_col)) + (1,); L(rp.outputs['Color'], mx.inputs['Fac']); L(col, mx.inputs['Color1']); col = mx.outputs['Color']
    L(col, bs.inputs['Base Color']); bs.inputs['Roughness'].default_value = o['rough']
    if 'Subsurface Weight' in bs.inputs:
        bs.inputs['Subsurface Weight'].default_value = o.get('sss', .0)
        if o.get('sss'): bs.inputs['Subsurface Radius'].default_value = (.08, .05, .02); bs.inputs['Subsurface Scale'].default_value = 1.0
    bn = nt.nodes.new('ShaderNodeTexVoronoi' if o.get('net') else 'ShaderNodeTexNoise'); bn.inputs['Scale'].default_value = o['bscale'] if not o.get('net') else 9; L(tc.outputs['Object'], bn.inputs['Vector'])
    bp = nt.nodes.new('ShaderNodeBump'); bp.inputs['Strength'].default_value = o['bump']; bp.inputs['Distance'].default_value = .03
    L(bn.outputs['Distance' if o.get('net') else 'Fac'], bp.inputs['Height']); L(bp.outputs['Normal'], bs.inputs['Normal'])
    me.materials.append(mat)
    sub = ob.modifiers.new('s', 'SUBSURF'); sub.levels = sub.render_levels = 1
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); cam.data.type = 'ORTHO'
    e = math.radians(o.get('elev', 62)); dv = Vector((0, -math.cos(e), math.sin(e))); cam.location = dv * 6; cam.rotation_euler = (-dv).to_track_quat('-Z', 'Y').to_euler()
    cam.data.ortho_scale = 2.9; sc.camera = cam
    path = os.path.join(OUT, f'{kind}_{n}.png'); sc.render.filepath = path; bpy.ops.render.render(write_still=True)
    from PIL import Image
    im = Image.open(path).convert('RGBA'); bb = im.getchannel('A').point(lambda a: 255 if a > 8 else 0).getbbox(); im.crop(bb).save(path); print('sprite', kind, n, im.crop(bb).size)


KINDS = ['chip', 'ridged', 'rind', 'crescent', 'pop', 'triangle', 'cone', 'puff', 'flip', 'curl', 'paw', 'roll', 'stick', 'strip', 'wave', 'jerk', 'cube', 'slab', 'cracker', 'sheet', 'seed', 'kernel', 'nut', 'crumb', 'ball', 'fish', 'ring', 'pretzel']
if __name__ == '__main__':
    for k in (sys.argv[1:] or KINDS):
        for n in range(NVAR): render(k, n)

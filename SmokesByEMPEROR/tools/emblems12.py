"""Batch 10/11 emblems, second pass: every brand has its own figure (the user's rule: no drawing repeats on any pack).
packvar calls install(globals()) after the B10/B11 tables; this module replaces the repeated emblems in them."""
import math

NEED = ('mm', '_P', '_L', '_C', '_pts', 'waves9', 'text_c', 'F', 'SERIF9', 'OSW7', 'darker', 'lum')


def _R(d, cx, cy, s, x0, y0, x1, y1, col):
    d.rectangle([cx + mm(x0 * s), cy + mm(y0 * s), cx + mm(x1 * s), cy + mm(y1 * s)], fill=col)


def _E(d, cx, cy, s, x, y, rx, ry, col=None, out=None, w=.5):
    d.ellipse([cx + mm((x - rx) * s), cy + mm((y - ry) * s), cx + mm((x + rx) * s), cy + mm((y + ry) * s)], fill=col, outline=out, width=max(2, mm(w * s)) if out else 0)


def _A(d, cx, cy, s, x, y, r, a0, a1, col, w=.6, ry=None):
    ry = ry or r
    d.arc([cx + mm((x - r) * s), cy + mm((y - ry) * s), cx + mm((x + r) * s), cy + mm((y + ry) * s)], a0, a1, fill=col, width=max(2, mm(w * s)))


def _S(d, cx, cy, s, x, y, r, a0, a1, col, ry=None):
    ry = ry or r
    d.pieslice([cx + mm((x - r) * s), cy + mm((y - ry) * s), cx + mm((x + r) * s), cy + mm((y + ry) * s)], a0, a1, fill=col)


def _ell(x, y, rx, ry, rot=0., n=28):
    c, sn = math.cos(math.radians(rot)), math.sin(math.radians(rot))
    return [(x + rx * math.cos(t) * c - ry * math.sin(t) * sn, y + rx * math.cos(t) * sn + ry * math.sin(t) * c) for t in [k * 2 * math.pi / n for k in range(n)]]


def _poly(n, r, rot=-90., x=0., y=0.):
    return [(x + r * math.cos(math.radians(rot + k * 360. / n)), y + r * math.sin(math.radians(rot + k * 360. / n))) for k in range(n)]


def _star(n, r1, r2, rot=-90., x=0., y=0.):
    return [(x + (r1 if k % 2 == 0 else r2) * math.cos(math.radians(rot + k * 180. / n)), y + (r1 if k % 2 == 0 else r2) * math.sin(math.radians(rot + k * 180. / n))) for k in range(2 * n)]


def _mir(pts):
    return [(-x, y) for x, y in pts]


def _rot(pts, a, ox=0., oy=0.):
    c, sn = math.cos(math.radians(a)), math.sin(math.radians(a))
    return [(ox + (x - ox) * c - (y - oy) * sn, oy + (x - ox) * sn + (y - oy) * c) for x, y in pts]


def e_num2(txt, shape):
    def f(img, d, cx, cy, s, col, bg):
        if shape == 'plain':
            text_c(d, cx, cy - mm(2.4 * s), txt, F(SERIF9, mm(27 * s)), col)
            _R(d, cx, cy, s, -9, 12.4, 9, 13.4, col)
            return
        if shape == 'square':
            _R(d, cx, cy, s, -13, -13, 13, 13, col)
            d.rectangle([cx - mm(11.4 * s), cy - mm(11.4 * s), cx + mm(11.4 * s), cy + mm(11.4 * s)], outline=bg, width=max(2, mm(.35 * s)))
        elif shape == 'diamond':
            _P(d, cx, cy, s, _poly(4, 17.4), col)
            d.line(_pts(cx, cy, s, _poly(4, 15) + [_poly(4, 15)[0]]), fill=bg, width=max(2, mm(.35 * s)))
        elif shape == 'hex':
            _P(d, cx, cy, s, _poly(6, 16, 0), col)
            d.line(_pts(cx, cy, s, _poly(6, 14, 0) + [_poly(6, 14, 0)[0]]), fill=bg, width=max(2, mm(.35 * s)))
        elif shape == 'oval':
            _E(d, cx, cy, s, 0, 0, 18.4, 12, col)
            _E(d, cx, cy, s, 0, 0, 16.6, 10.2, None, bg, .35)
        text_c(d, cx, cy - mm(1.1 * s), txt, F(SERIF9, mm((13 if shape == 'diamond' else 15.5) * s)), bg)
    return f


def e_bell(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-3, -11), (3, -11), (6, -6), (8, 4), (11.4, 8), (-11.4, 8), (-8, 4), (-6, -6)], col)
    _C(d, cx, cy, s, 0, -12, 2.1, col)
    _C(d, cx, cy, s, 0, 10.4, 2.5, col)
    _R(d, cx, cy, s, -8.6, 3.4, 8.6, 4.2, bg)


def e_cup(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 9.4, 1.6, 3.8, None, col, 1.1)
    _P(d, cx, cy, s, [(-10, -3), (8, -3), (6, 7.4), (-8, 7.4)], col)
    _E(d, cx, cy, s, -1, 10, 13, 1.7, col)
    for x in (-5, 0, 5):
        _L(d, cx, cy, s, [(x, -6), (x - 1.5, -8.6), (x + 1, -11), (x - .5, -14)], col, .55)


def e_tophat(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-8, -13), (8, -13), (7, 5), (-7, 5)], col)
    _E(d, cx, cy, s, 0, 6, 13.6, 2.8, col)
    _R(d, cx, cy, s, -7.3, .6, 7.3, 3.2, bg)


def e_sputnik(img, d, cx, cy, s, col, bg):
    for ex, ey in ((-17, -3), (-16, 5), (-12, 11), (-5, 14)):
        _L(d, cx, cy, s, [(3, -3), (ex, ey)], col, .45)
    _C(d, cx, cy, s, 4, -4, 7, col)
    _A(d, cx, cy, s, 4, -4, 5, 200, 290, bg, .4)
    for x, y in ((-12, -11), (13, 8), (10, -13)):
        _P(d, cx, cy, s, _star(4, 2.2, .7, -90, x, y), col)


def e_propeller(img, d, cx, cy, s, col, bg):
    for a in (-90, 30, 150):
        _P(d, cx, cy, s, _rot(_ell(8.4, 0, 7.4, 2.7), a), col)
    _C(d, cx, cy, s, 0, 0, 3, col)
    _C(d, cx, cy, s, 0, 0, 1.2, bg)


def e_snowflake(img, d, cx, cy, s, col, bg):
    for k in range(6):
        a = k * 60
        _L(d, cx, cy, s, _rot([(0, 0), (0, -14.4)], a), col, .8)
        for r in (-6, -10):
            _L(d, cx, cy, s, _rot([(-3.4, r - 3.2), (0, r), (3.4, r - 3.2)], a), col, .7)
    _P(d, cx, cy, s, _poly(6, 2.6), col)


def e_pawn(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 0, -8.4, 4.8, col)
    _E(d, cx, cy, s, 0, -3, 5.8, 1.5, col)
    _P(d, cx, cy, s, [(-2.6, -3), (2.6, -3), (6.4, 8), (-6.4, 8)], col)
    _R(d, cx, cy, s, -8.8, 8, 8.8, 11.6, col)


def e_bonfire(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(0, -14), (5, -4), (4.4, 3), (0, 5), (-4.4, 3), (-5, -4)], col)
    for m in (1, -1):
        _P(d, cx, cy, s, [(m * 7, -7), (m * 3.6, -1), (m * 4.6, 4), (m * 8.4, 4), (m * 10, -1)], col)
    _P(d, cx, cy, s, [(0, -5), (2.2, 0), (0, 3.4), (-2.2, 0)], bg)
    _L(d, cx, cy, s, [(-12, 12), (12, 6)], col, 2.4)
    _L(d, cx, cy, s, [(-12, 6), (12, 12)], col, 2.4)


def e_orbits(img, d, cx, cy, s, col, bg):
    for a in (0, 60, 120):
        p = _ell(0, 0, 15, 5.4, a, 48)
        _L(d, cx, cy, s, p + [p[0]], col, .55)
    _C(d, cx, cy, s, 0, 0, 2.8, col)
    _C(d, cx, cy, s, 13, -7.4, 1.5, col)


def e_guitar(img, d, cx, cy, s, col, bg):
    pts = lambda p: _rot(p, 28)
    _P(d, cx, cy, s, pts(_ell(0, 7, 7.2, 6.6)), col)
    _P(d, cx, cy, s, pts(_ell(0, -.6, 5.2, 4.8)), col)
    _P(d, cx, cy, s, pts([(-1.3, -15), (1.3, -15), (1.3, 0), (-1.3, 0)]), col)
    _P(d, cx, cy, s, pts([(-2.4, -18), (2.4, -18), (2.4, -14.6), (-2.4, -14.6)]), col)
    _P(d, cx, cy, s, pts(_ell(0, 2.4, 2, 2)), bg)
    _P(d, cx, cy, s, pts([(-3, 8.6), (3, 8.6), (3, 9.7), (-3, 9.7)]), bg)


def e_grapes(img, d, cx, cy, s, col, bg):
    for y, xs in ((-4, (-7.8, -2.6, 2.6, 7.8)), (.8, (-5.2, 0, 5.2)), (5.6, (-2.6, 2.6)), (10.4, (0,))):
        for x in xs:
            _C(d, cx, cy, s, x, y, 2.9, col)
    _L(d, cx, cy, s, [(0, -7), (0, -13)], col, .7)
    _P(d, cx, cy, s, [(.6, -11), (6, -15), (12, -12), (10, -8), (5, -8)], col)


def e_heart(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, -5.2, -4, 6.2, col)
    _C(d, cx, cy, s, 5.2, -4, 6.2, col)
    _P(d, cx, cy, s, [(-10.9, -1.2), (10.9, -1.2), (0, 12.4)], col)
    _A(d, cx, cy, s, -5.2, -4, 3.8, 180, 270, bg, .5)


def e_handfan(img, d, cx, cy, s, col, bg):
    _S(d, cx, cy, s, 0, 11, 23, 205, 335, col)
    for a in range(215, 330, 13):
        r = math.radians(a)
        _L(d, cx, cy, s, [(0, 11), (23 * math.cos(r), 11 + 23 * math.sin(r))], bg, .3)
    _A(d, cx, cy, s, 0, 11, 17, 205, 335, bg, .4)
    _S(d, cx, cy, s, 0, 11, 8, 200, 340, bg)
    for a in (212, 328):
        r = math.radians(a)
        _L(d, cx, cy, s, [(0, 11), (9 * math.cos(r), 11 + 9 * math.sin(r))], col, .5)
    _C(d, cx, cy, s, 0, 11, 1.6, col)


def e_clock(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 0, 0, 14.4, col)
    _C(d, cx, cy, s, 0, 0, 12.2, bg)
    for k in range(12):
        _L(d, cx, cy, s, _rot([(0, -11.4), (0, -9 if k % 3 == 0 else -10.2)], k * 30), col, .7 if k % 3 == 0 else .4)
    _L(d, cx, cy, s, [(0, 0), (0, -8.4)], col, .7)
    _L(d, cx, cy, s, [(0, 0), (5.4, 3.4)], col, .9)
    _C(d, cx, cy, s, 0, 0, 1.4, col)


def e_pinwheel(img, d, cx, cy, s, col, bg):
    for k in range(4):
        _P(d, cx, cy, s, _rot([(0, 0), (13, 0), (6.5, -6.5)], k * 90), col)
        _P(d, cx, cy, s, _rot([(0, 0), (6.5, -6.5), (0, -6.5)], k * 90), darker(col, .72) if lum(col) > 110 else col)
    _C(d, cx, cy, s, 0, 0, 1.5, bg)


def e_windmill(img, d, cx, cy, s, col, bg):
    for k in range(4):
        _P(d, cx, cy, s, _rot([(1, -3.4), (14, -3.4), (14, -7.4), (1, -4.6)], k * 90 + 24, 0, -4), col)
        _L(d, cx, cy, s, _rot([(0, -4), (14, -4)], k * 90 + 24, 0, -4), col, .45)
    _P(d, cx, cy, s, [(-4.6, 13), (4.6, 13), (2.8, -3), (-2.8, -3)], col)
    _P(d, cx, cy, s, [(-3.6, -3), (3.6, -3), (0, -7.4)], col)
    _C(d, cx, cy, s, 0, -4, 1.1, bg)
    _R(d, cx, cy, s, -1.2, 8.6, 1.2, 13, bg)


def e_kris(img, d, cx, cy, s, col, bg):
    L = [(-1.7 + 1.5 * math.sin(k * 1.25), 6 - k * 1.9) for k in range(11)]
    Rr = [(1.7 + 1.5 * math.sin(k * 1.25), 6 - k * 1.9) for k in range(11)]
    _P(d, cx, cy, s, L + [(.6, -16)] + Rr[::-1], col)
    _P(d, cx, cy, s, [(-7, 6), (6, 5), (7.4, 7.6), (-5, 8.6)], col)
    _P(d, cx, cy, s, [(-1.7, 8.4), (1.7, 8.2), (4.6, 12.6), (1.4, 14.6), (-1.2, 12)], col)


def e_feather(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, _ell(2, -3, 4.8, 12, 32), col)
    a = _rot([(2, -15), (2, 17)], 32, 2, -3)
    _L(d, cx, cy, s, [a[0], _rot([(2, 9)], 32, 2, -3)[0]], bg, .35)
    _L(d, cx, cy, s, [_rot([(2, 8)], 32, 2, -3)[0], a[1]], col, .6)
    for y in (-8, -3, 2):
        _L(d, cx, cy, s, _rot([(2, y + 2), (7.4, y - 2.4)], 32, 2, -3), bg, .3)
        _L(d, cx, cy, s, _rot([(2, y + 2), (-3.4, y - 2.4)], 32, 2, -3), bg, .3)


def e_briefcase(img, d, cx, cy, s, col, bg):
    _L(d, cx, cy, s, [(-4.4, -4), (-4.4, -8.6), (4.4, -8.6), (4.4, -4)], col, 1.)
    _R(d, cx, cy, s, -13, -4.4, 13, 10, col)
    _R(d, cx, cy, s, -13, .8, 13, 1.4, bg)
    _R(d, cx, cy, s, -1.8, -.6, 1.8, 3.6, bg)
    _C(d, cx, cy, s, 0, 1.6, .8, col)


def e_rocket(img, d, cx, cy, s, col, bg):
    p = lambda q: _rot(q, 35)
    _P(d, cx, cy, s, p([(0, -16), (4.2, -8), (4.2, 5), (-4.2, 5), (-4.2, -8)]), col)
    for m in (1, -1):
        _P(d, cx, cy, s, p([(m * 4.2, -1), (m * 9.4, 9), (m * 4.2, 6)]), col)
    _P(d, cx, cy, s, p(_ell(0, -5, 1.9, 1.9)), bg)
    _P(d, cx, cy, s, p([(-2.6, 6.6), (2.6, 6.6), (0, 14)]), col)


def e_keys(img, d, cx, cy, s, col, bg):
    d.rectangle([cx - mm(14.4 * s), cy - mm(8 * s), cx + mm(14.4 * s), cy + mm(8.6 * s)], outline=col, width=max(2, mm(.6 * s)))
    for k in range(1, 7):
        x = -14.4 + k * 28.8 / 7
        _R(d, cx, cy, s, x - .2, -8, x + .2, 8.6, col)
        if k != 3:
            _R(d, cx, cy, s, x - 1.3, -8, x + 1.3, 2, col)


def e_canoe(img, d, cx, cy, s, col, bg):
    for m in (1, -1):
        _L(d, cx, cy, s, [(m * -9, -13), (m * 6, 3)], col, .7)
        _P(d, cx, cy, s, _rot(_ell(m * -9.6, -13.6, 1.9, 4.2), m * -43, m * -9.6, -13.6), col)
    _P(d, cx, cy, s, [(-18, .6), (-14, 3), (14, 3), (18, .6), (13, 8), (-13, 8)], col)
    _R(d, cx, cy, s, -12, 4.6, 12, 5.1, bg)
    waves9(d, cx - mm(17 * s), cx + mm(17 * s), cy + mm(10.6 * s), col, 2, gap=2 * s)


def e_bee(img, d, cx, cy, s, col, bg):
    _E(d, cx, cy, s, -4.6, -6, 4.4, 6.4, None, col, .6)
    _E(d, cx, cy, s, 3.4, -6.4, 4.4, 6.4, None, col, .6)
    _E(d, cx, cy, s, 0, 4, 9, 5.8, col)
    _C(d, cx, cy, s, -10.4, 3.4, 3.4, col)
    for x in (-3.6, .6, 4.8):
        _R(d, cx, cy, s, x, -2.4, x + 1.5, 10.4, bg)
    _P(d, cx, cy, s, [(8.4, 2.6), (12.6, 4), (8.4, 5.4)], col)
    _L(d, cx, cy, s, [(-11.4, .8), (-14, -3)], col, .4)
    _L(d, cx, cy, s, [(-12.6, 1.6), (-16, -.6)], col, .4)


def e_orb(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 0, 4, 9.4, col)
    _R(d, cx, cy, s, -9.4, 3.4, 9.4, 4.6, bg)
    _R(d, cx, cy, s, -.6, -5.4, .6, 4, bg)
    _R(d, cx, cy, s, -1.2, -15, 1.2, -5, col)
    _R(d, cx, cy, s, -4.4, -12.4, 4.4, -10, col)


def e_fleur(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(0, -15), (3.8, -7), (2.6, 2), (-2.6, 2), (-3.8, -7)], col)
    side = [(3.4, 2), (4.4, -4), (9, -8.6), (13.4, -5), (12.6, 1), (9.4, .4), (8.6, -3), (6.6, 2)]
    _P(d, cx, cy, s, side, col)
    _P(d, cx, cy, s, _mir(side), col)
    _R(d, cx, cy, s, -8.4, 2.6, 8.4, 5, col)
    _P(d, cx, cy, s, [(-2.6, 5.6), (2.6, 5.6), (0, 14)], col)
    for m in (1, -1):
        _P(d, cx, cy, s, [(m * 3.4, 5.6), (m * 9, 10.6), (m * 6.6, 5.6)], col)


def e_batik(img, d, cx, cy, s, col, bg):
    d.line(_pts(cx, cy, s, _poly(4, 16.6) + [_poly(4, 16.6)[0]]), fill=col, width=max(2, mm(.45 * s)))
    for a in (45, 135, 225, 315):
        _P(d, cx, cy, s, _rot(_ell(6.6, 0, 5.4, 2.9), a), col)
        _P(d, cx, cy, s, _rot(_ell(6.6, 0, 2, .8), a), bg)
    _C(d, cx, cy, s, 0, 0, 1.4, col)


def e_dome(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(0, -11), (1.2, -8), (5.6, -4.6), (8.4, 0), (8, 3.6), (5, 7), (-5, 7), (-8, 3.6), (-8.4, 0), (-5.6, -4.6), (-1.2, -8)], col)
    _R(d, cx, cy, s, -5, 7.8, 5, 14, col)
    _R(d, cx, cy, s, -.5, -17, .5, -10.6, col)
    _R(d, cx, cy, s, -2.2, -15, 2.2, -14.1, col)
    for x in (-3.6, 0, 3.6):
        _L(d, cx, cy, s, [(x * .3, -8), (x * 1.5, 0), (x, 7)], bg, .3)
    _R(d, cx, cy, s, -1.2, 10, 1.2, 14, bg)


def e_gull(img, d, cx, cy, s, col, bg):
    for m in (1, -1):
        _P(d, cx, cy, s, [(0, -3), (m * 4, -9), (m * 10, -12), (m * 17, -6), (m * 10, -8.4), (m * 5, -5), (m * 1.6, 1)], col)
    _E(d, cx, cy, s, 0, -2, 2.4, 3.6, col)
    waves9(d, cx - mm(17 * s), cx + mm(17 * s), cy + mm(8 * s), col, 3, gap=2.4 * s)


def e_peacock(img, d, cx, cy, s, col, bg):
    for m in (-1, 1):
        for y in (9, 11.4, 13.6):
            _L(d, cx, cy, s, [(0, y), (m * 4.4, y - 4.4)], col, .35)
    _L(d, cx, cy, s, [(0, 6), (0, 15)], col, .6)
    _P(d, cx, cy, s, _ell(0, -3, 8.4, 11.4), col)
    _P(d, cx, cy, s, _ell(0, -2, 5.6, 7.8), bg)
    _P(d, cx, cy, s, _ell(0, -1, 3.6, 5), col)
    _C(d, cx, cy, s, 0, .4, 1.7, bg)


def e_shipwheel(img, d, cx, cy, s, col, bg):
    for k in range(8):
        _L(d, cx, cy, s, _rot([(0, 0), (0, -14.6)], k * 45), col, .9)
        q = _rot([(0, -14.6)], k * 45)[0]
        _C(d, cx, cy, s, q[0], q[1], 1.4, col)
    _C(d, cx, cy, s, 0, 0, 10, None, col, 1.7)
    _C(d, cx, cy, s, 0, 0, 3, col)
    _C(d, cx, cy, s, 0, 0, 1.1, bg)


def e_piratehat(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-16, 5), (-10, -5), (0, -11), (10, -5), (16, 5), (8.6, 2), (0, 1), (-8.6, 2)], col)
    _A(d, cx, cy, s, 0, 12, 13, 215, 325, col, 1.2)
    _C(d, cx, cy, s, 0, -5.4, 2.2, bg)
    _L(d, cx, cy, s, [(-3.4, -2.6), (3.4, -.4)], bg, .5)
    _L(d, cx, cy, s, [(3.4, -2.6), (-3.4, -.4)], bg, .5)


def e_telescope(img, d, cx, cy, s, col, bg):
    for q in ([(-15, -3.2), (-4, -3.2), (-4, 3.2), (-15, 3.2)], [(-3.2, -2.4), (6, -2.4), (6, 2.4), (-3.2, 2.4)], [(6.8, -1.6), (15, -1.6), (15, 1.6), (6.8, 1.6)]):
        _P(d, cx, cy, s, _rot(q, -28, 0, 3), col)
    for x, y, r in ((-11, -11, 2.4), (-3, -13, 1.5), (-14, -4, 1.3)):
        _P(d, cx, cy, s, _star(4, r, r * .32, -90, x, y), col)


def e_envelope(img, d, cx, cy, s, col, bg):
    _R(d, cx, cy, s, -14, -9, 14, 9, col)
    _L(d, cx, cy, s, [(-14, -9), (0, 2), (14, -9)], bg, .5)
    _L(d, cx, cy, s, [(-14, 9), (-4, -1)], bg, .35)
    _L(d, cx, cy, s, [(14, 9), (4, -1)], bg, .35)
    _C(d, cx, cy, s, 0, 2.6, 3.8, bg)
    _C(d, cx, cy, s, 0, 2.6, 2.6, col)


def e_shamrock(img, d, cx, cy, s, col, bg):
    for a in (-90, 30, 150):
        for off in (-28, 28):
            q = _rot([(0, -7.4)], a + 90 + off)[0]
            _C(d, cx, cy, s, q[0], q[1] - 2, 3.9, col)
        q = _rot([(0, -3.4)], a + 90)[0]
        _C(d, cx, cy, s, q[0], q[1] - 2, 3.4, col)
    _L(d, cx, cy, s, [(0, 0), (.6, 7), (3.6, 13)], col, .9)


def e_bugle(img, d, cx, cy, s, col, bg):
    _E(d, cx, cy, s, -2, 1, 9.4, 5.2, None, col, 1.5)
    _P(d, cx, cy, s, [(5, -6.4), (16, -11), (16, 3), (5, -2.4)], col)
    _L(d, cx, cy, s, [(-9, -3.4), (-16, -3.4)], col, 1.3)
    _R(d, cx, cy, s, -17.6, -5, -15.6, -1.8, col)
    _L(d, cx, cy, s, [(-6, 6), (-8, 12), (-3, 10), (-4, 14)], col, .4)


def e_colosseum(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-16, 10), (-16, -11), (1, -11), (3, -5), (16, -5), (16, 10)], col)
    for y0, xs in ((-8, (-12.6, -7.6, -2.6)), (-2, (-12.6, -7.6, -2.6, 2.4, 7.4, 12.4)), (4.6, (-12.6, -7.6, -2.6, 2.4, 7.4, 12.4))):
        for x in xs:
            _R(d, cx, cy, s, x - 1.5, y0 + 1.4, x + 1.5, y0 + 4.4, bg)
            _C(d, cx, cy, s, x, y0 + 1.4, 1.5, bg)
    _R(d, cx, cy, s, -17, 10, 17, 11.4, col)


def e_bicycle(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, -9.6, 5, 6.8, None, col, .8)
    _C(d, cx, cy, s, 9.6, 5, 6.8, None, col, .8)
    _L(d, cx, cy, s, [(-9.6, 5), (-3.4, -5), (6.4, -5), (.4, 5), (-9.6, 5)], col, .7)
    _L(d, cx, cy, s, [(-3.4, -5), (.4, 5)], col, .7)
    _L(d, cx, cy, s, [(9.6, 5), (5.6, -8.6), (9, -9.6)], col, .7)
    _L(d, cx, cy, s, [(-3.4, -5), (-4.4, -8)], col, .7)
    _L(d, cx, cy, s, [(-7, -8.2), (-2, -8.2)], col, 1.)


def e_horseshoe(img, d, cx, cy, s, col, bg):
    _A(d, cx, cy, s, 0, -1, 11.4, 125, 55, col, 5, ry=12.4)
    for a in (150, 185, 220, 320, 355, 30):
        r = math.radians(a)
        _C(d, cx, cy, s, 9 * math.cos(r), -1 + 10 * math.sin(r), .85, bg)


def e_triskele(img, d, cx, cy, s, col, bg):
    for k in range(3):
        a = k * 120 - 90
        r = math.radians(a)
        _A(d, cx, cy, s, 6.6 * math.cos(r), 6.6 * math.sin(r), 6.6, a + 150, a + 400, col, 1.7)
        _C(d, cx, cy, s, 8 * math.cos(r + .5), 8 * math.sin(r + .5), 2, col)


def e_bull(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-13, -3), (-4, -7), (5, -6), (9, -3), (9.6, 3), (8, 4), (8.6, 11), (6, 11), (4.6, 4.6), (-6, 4.6), (-7, 11), (-9.6, 11), (-10, 4), (-13, 2)], col)
    _P(d, cx, cy, s, [(6, -5.4), (13, -3), (16, 2.6), (13.6, 5), (8, 2)], col)
    _L(d, cx, cy, s, [(11, -3), (13, -8), (17, -9.4)], col, .9)
    _L(d, cx, cy, s, [(-13, -2), (-16.6, 1), (-15.6, 6)], col, .6)


def e_tile(img, d, cx, cy, s, col, bg):
    d.rectangle([cx - mm(14 * s), cy - mm(14 * s), cx + mm(14 * s), cy + mm(14 * s)], outline=col, width=max(2, mm(.7 * s)))
    for x, y, a in ((-14, -14, 0), (14, -14, 90), (14, 14, 180), (-14, 14, 270)):
        _S(d, cx, cy, s, x, y, 7.4, a, a + 90, col)
    for a in (0, 90):
        _P(d, cx, cy, s, _rot(_ell(0, 0, 9, 3.2), a), col)
    _C(d, cx, cy, s, 0, 0, 2.2, bg)
    _C(d, cx, cy, s, 0, 0, 1, col)


def e_amphora(img, d, cx, cy, s, col, bg):
    for m in (1, -1):
        _L(d, cx, cy, s, [(m * 3, -9.4), (m * 8.6, -8.4), (m * 8.4, -1.4)], col, .9)
    _P(d, cx, cy, s, [(-3, -10), (3, -10), (3.6, -6), (8.4, 0), (7.2, 6), (3, 11.6), (-3, 11.6), (-7.2, 6), (-8.4, 0), (-3.6, -6)], col)
    _R(d, cx, cy, s, -4.8, -12.4, 4.8, -10.2, col)
    _R(d, cx, cy, s, -4.2, 11.6, 4.2, 13.4, col)
    _L(d, cx, cy, s, [(-7.6, 2), (-5, 2), (-5, 0), (-2.4, 0), (-2.4, 2), (.2, 2), (.2, 0), (2.8, 0), (2.8, 2), (5.4, 2), (5.4, 0), (7.6, 0)], bg, .3)


def e_tulip(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-6.4, -4), (-6.8, -12), (-3, -7.4), (0, -14), (3, -7.4), (6.8, -12), (6.4, -4), (3.4, 1), (-3.4, 1)], col)
    _L(d, cx, cy, s, [(0, 1), (0, 14)], col, .9)
    for m in (1, -1):
        _P(d, cx, cy, s, [(0, 12.6), (m * 9, 2), (m * 5.4, 10)], col)


def e_fez(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-7.4, -8), (7.4, -8), (10, 8), (-10, 8)], col)
    _E(d, cx, cy, s, 0, -8, 7.4, 1.7, col)
    _E(d, cx, cy, s, 0, 8, 10, 2, col)
    _L(d, cx, cy, s, [(0, -9.4), (9, -11.4), (12.6, -4)], col, .5)
    _P(d, cx, cy, s, [(11.4, -4), (13.8, -4), (14.6, 3.4), (10.6, 3.4)], col)


def e_compass(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 0, 0, 11, None, col, .45)
    _P(d, cx, cy, s, _star(4, 7.4, 2.2, -45), col)
    _P(d, cx, cy, s, [(0, -16), (2.8, 0), (0, 16), (-2.8, 0)], col)
    _P(d, cx, cy, s, [(-13, 0), (0, -2.8), (13, 0), (0, 2.8)], col)
    _P(d, cx, cy, s, [(0, -13.4), (2.2, 0), (0, 0)], bg)
    _C(d, cx, cy, s, 0, 0, 1.1, bg)


def e_blendcircles(img, d, cx, cy, s, col, bg):
    for x, y in ((0, -5.4), (-5.8, 4.2), (5.8, 4.2)):
        _C(d, cx, cy, s, x, y, 7.8, None, col, 1.2)
    _C(d, cx, cy, s, 0, 1, 2, col)


def e_ladyhat(img, d, cx, cy, s, col, bg):
    _E(d, cx, cy, s, 0, 4, 16, 5, col)
    _S(d, cx, cy, s, 0, 3, 7.6, 180, 360, col, ry=11)
    _R(d, cx, cy, s, -7.5, .4, 7.5, 2.2, bg)
    _P(d, cx, cy, s, [(7, 1.2), (13, -3), (12.4, 3.4)], bg)
    _P(d, cx, cy, s, [(7.4, 1.2), (12.4, -2), (12, 2.6)], col)


def e_ankh(img, d, cx, cy, s, col, bg):
    _E(d, cx, cy, s, 0, -7.6, 4.8, 6.4, None, col, 1.9)
    _R(d, cx, cy, s, -1.6, -1.6, 1.6, 14, col)
    _R(d, cx, cy, s, -8.6, -1.6, 8.6, 1.8, col)


def e_atomium(img, d, cx, cy, s, col, bg):
    n = [(0, -13), (0, 13), (-11.4, -6.5), (11.4, -6.5), (-11.4, 6.5), (11.4, 6.5)]
    for p in n:
        _L(d, cx, cy, s, [(0, 0), p], col, .6)
    for a, b in ((0, 2), (0, 3), (2, 4), (3, 5), (4, 1), (5, 1)):
        _L(d, cx, cy, s, [n[a], n[b]], col, .45)
    for p in n + [(0, 0)]:
        _C(d, cx, cy, s, p[0], p[1], 2.9, col)


def e_moustache(img, d, cx, cy, s, col, bg):
    _E(d, cx, cy, s, 0, -7, 11.6, 4.8, col)
    _R(d, cx, cy, s, -.5, -13.6, .5, -11, col)
    half = [(0, 4.4), (-4, 2.4), (-9, 3.6), (-14.4, 0), (-12.6, 6.4), (-7, 9), (-2, 7.6), (0, 6.4)]
    _P(d, cx, cy, s, half, col)
    _P(d, cx, cy, s, _mir(half), col)


def e_boot(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-5, -14), (4, -14), (4, 2), (11, 5.6), (12.4, 10), (-5.6, 10)], col)
    _R(d, cx, cy, s, -5.6, 10, -1, 13, col)
    _R(d, cx, cy, s, -5.6, 10, 12.4, 11.2, col)
    _R(d, cx, cy, s, -5, -11.4, 4, -10.6, bg)
    _L(d, cx, cy, s, [(-5.6, 6.6), (-10, 6.6)], col, .7)
    _P(d, cx, cy, s, _star(6, 3, 1.3, 0, -12, 6.6), col)


def e_chest(img, d, cx, cy, s, col, bg):
    _S(d, cx, cy, s, 0, -1.6, 13, 180, 360, col, ry=9)
    _R(d, cx, cy, s, -13, -.4, 13, 10, col)
    _R(d, cx, cy, s, -13, -1.6, 13, -.6, bg)
    for x in (-8.6, 8):
        _R(d, cx, cy, s, x, -10, x + .6, 10, bg)
    _R(d, cx, cy, s, -1.9, -2.4, 1.9, 3.4, bg)
    _C(d, cx, cy, s, 0, .6, .9, col)


def e_honeycomb(img, d, cx, cy, s, col, bg):
    cells = [(0, 0)] + [(9.2 * math.cos(math.radians(a)), 9.2 * math.sin(math.radians(a))) for a in range(30, 390, 60)]
    for i, (x, y) in enumerate(cells):
        p = _poly(6, 4.8, 0, x, y)
        if i in (0, 2, 5):
            _P(d, cx, cy, s, p, col)
        else:
            d.line(_pts(cx, cy, s, p + [p[0]]), fill=col, width=max(2, mm(.6 * s)))


def e_glove(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 1.6, -4.6, 9, col)
    _R(d, cx, cy, s, -6, -4, 9, 4, col)
    _E(d, cx, cy, s, -7.6, -1, 3.8, 5.6, col)
    _A(d, cx, cy, s, -7.6, -1, 4.4, 300, 60, bg, .35, ry=6)
    _R(d, cx, cy, s, -5, 5.2, 8, 12, col)
    _R(d, cx, cy, s, -5, 7.6, 8, 8.2, bg)


def e_lipstick(img, d, cx, cy, s, col, bg):
    _R(d, cx, cy, s, -4.4, 2, 4.4, 14, col)
    _R(d, cx, cy, s, -3.4, -3.4, 3.4, 1.4, col)
    _P(d, cx, cy, s, [(-2.7, -4), (-2.7, -9.4), (2.7, -14), (2.7, -4)], col)
    _R(d, cx, cy, s, -4.4, 5, 4.4, 5.6, bg)


def e_lemon(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, _ell(0, 2, 10.4, 7.6, -18), col)
    _P(d, cx, cy, s, _rot([(9.4, .4), (14.6, 2), (9.4, 3.6)], -18, 0, 2), col)
    _P(d, cx, cy, s, _rot([(-9.4, .4), (-13.4, 2), (-9.4, 3.6)], -18, 0, 2), col)
    _P(d, cx, cy, s, [(-9, -3), (-7, -11), (0, -12.4), (-2, -6)], col)
    _A(d, cx, cy, s, 0, 2, 7.2, 200, 260, bg, .4, ry=5)


def e_butterfly(img, d, cx, cy, s, col, bg):
    up = [(-1, -1), (-6, -12), (-13.6, -11), (-14, -3), (-7, 1)]
    lo = [(-1, 1.6), (-8.4, 2.4), (-11.6, 7.4), (-7, 12), (-2, 6.4)]
    for q in (up, lo, _mir(up), _mir(lo)):
        _P(d, cx, cy, s, q, col)
    _E(d, cx, cy, s, 0, 0, 1.3, 7.4, col)
    for m in (1, -1):
        _L(d, cx, cy, s, [(0, -6.4), (m * 3.4, -12), (m * 5, -11.4)], col, .35)
        _C(d, cx, cy, s, m * 9, -6.4, 1.7, bg)
        _C(d, cx, cy, s, m * 7, 6.6, 1.2, bg)


def e_medal(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-7, -15), (-1.6, -15), (3.4, -3), (-2, -3)], col)
    _P(d, cx, cy, s, [(7, -15), (1.6, -15), (-3.4, -3), (2, -3)], darker(col, .72) if lum(col) > 110 else col)
    _C(d, cx, cy, s, 0, 5, 8.6, col)
    _C(d, cx, cy, s, 0, 5, 6.6, None, bg, .35)
    text_c(d, cx, cy + mm(4.4 * s), '1', F(SERIF9, mm(8.6 * s)), bg)


def e_scale(img, d, cx, cy, s, col, bg):
    _R(d, cx, cy, s, -.7, -11, .7, 10, col)
    _R(d, cx, cy, s, -6.4, 10, 6.4, 12.4, col)
    _C(d, cx, cy, s, 0, -11.6, 1.8, col)
    _L(d, cx, cy, s, [(-12.6, -8), (12.6, -8)], col, .9)
    for m in (1, -1):
        _L(d, cx, cy, s, [(m * 8.4, 2), (m * 12.6, -8), (m * 16.8, 2)], col, .35)
        _S(d, cx, cy, s, m * 12.6, 2, 5.2, 0, 180, col, ry=3.6)


def e_balloon(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 0, -5, 10.4, col)
    _P(d, cx, cy, s, [(-8.4, 1), (8.4, 1), (2.8, 8.6), (-2.8, 8.6)], col)
    _E(d, cx, cy, s, 0, -5, 4.6, 10.4, None, bg, .35)
    _R(d, cx, cy, s, -10.4, -5.3, 10.4, -4.7, bg)
    _L(d, cx, cy, s, [(-2.4, 8.6), (-2, 11.6)], col, .35)
    _L(d, cx, cy, s, [(2.4, 8.6), (2, 11.6)], col, .35)
    _R(d, cx, cy, s, -2.6, 11.6, 2.6, 15, col)


def e_cherry(img, d, cx, cy, s, col, bg):
    _L(d, cx, cy, s, [(-6, 3), (-2, -8), (3, -13)], col, .6)
    _L(d, cx, cy, s, [(6.4, 1), (4.6, -6), (3, -13)], col, .6)
    _P(d, cx, cy, s, [(3, -13), (10, -15), (14, -10), (8, -8.4)], col)
    _C(d, cx, cy, s, -6.4, 7, 5.4, col)
    _C(d, cx, cy, s, 6.4, 5.4, 5.4, col)
    _A(d, cx, cy, s, -6.4, 7, 3.4, 190, 260, bg, .4)
    _A(d, cx, cy, s, 6.4, 5.4, 3.4, 190, 260, bg, .4)


def e_barcode(img, d, cx, cy, s, col, bg):
    x = -14.
    for i, w in enumerate((1.2, .5, .5, 1.6, .5, 1, .5, .5, 2, .5, 1.2, .5, .5, 1.6, 1, .5, .5, 1.2, .5, 2, .5, .5, 1.2)):
        if i % 2 == 0:
            _R(d, cx, cy, s, x, -10, x + w, 8 if i not in (0, 22, 10) else 11, col)
        x += w + .35
    text_c(d, cx - mm(.6 * s), cy + mm(11 * s), '0  00000  00020', F(OSW7, mm(2.6 * s)), col)


def e_dogtags(img, d, cx, cy, s, col, bg):
    _A(d, cx, cy, s, 0, -7, 7.4, 150, 390, col, .45, ry=7)
    for a, ox in ((-16, -4.6), (14, 4.6)):
        q = [(-4.6, -2.6), (-3, -4), (3, -4), (4.6, -2.6), (4.6, 10.6), (3, 12), (-3, 12), (-4.6, 10.6)]
        _P(d, cx, cy, s, _rot([(x + ox, y) for x, y in q], a, ox, -4), col)
        h = _rot([(ox, -1.6)], a, ox, -4)[0]
        _C(d, cx, cy, s, h[0], h[1], 1, bg)
        for y in (3, 5.4, 7.8):
            _L(d, cx, cy, s, _rot([(ox - 2.8, y), (ox + 2.8, y)], a, ox, -4), bg, .3)


def e_dice(img, d, cx, cy, s, col, bg):
    for ox, oy, a, pips in ((-6.4, 3.4, 14, ((-3.2, -3.2), (0, 0), (3.2, 3.2), (-3.2, 3.2), (3.2, -3.2))), (7.4, -5, -17, ((-2.8, -2.8), (2.8, 2.8)))):
        r = 6.8 if ox < 0 else 5.8
        _P(d, cx, cy, s, _rot([(ox - r, oy - r), (ox + r, oy - r), (ox + r, oy + r), (ox - r, oy + r)], a, ox, oy), col)
        for px, py in pips:
            q = _rot([(ox + px, oy + py)], a, ox, oy)[0]
            _C(d, cx, cy, s, q[0], q[1], 1.15, bg)


def e_clapper(img, d, cx, cy, s, col, bg):
    _R(d, cx, cy, s, -13, -2, 13, 11, col)
    top = [(-13, -8.6), (13, -8.6), (13, -4.2), (-13, -4.2)]
    _P(d, cx, cy, s, _rot(top, -10, -13, -4.2), col)
    for k in range(4):
        x = -10 + k * 6.6
        _P(d, cx, cy, s, _rot([(x, -8.6), (x + 2.6, -8.6), (x + 5.2, -4.2), (x + 2.6, -4.2)], -10, -13, -4.2), bg)
    for y in (2.4, 6.4):
        _R(d, cx, cy, s, -10.6, y, 10.6, y + .4, bg)


def e_constellation(img, d, cx, cy, s, col, bg):
    p = [(-7, -10), (-1, -3), (6, -5), (9.4, 7), (2.4, 9.4)]
    _L(d, cx, cy, s, [p[0], p[1], p[2], p[3], p[4], p[1]], col, .35)
    _P(d, cx, cy, s, _star(4, 5.4, 1.5, -90, *p[0]), col)
    for q in p[1:]:
        _C(d, cx, cy, s, q[0], q[1], 1.5, col)
    for x, y in ((-13, 4), (12, -11), (-10, 11)):
        _C(d, cx, cy, s, x, y, .7, col)


def e_soundwaves(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, -13, 0, 2.8, col)
    for r in (7.4, 13, 18.6, 24.2):
        _A(d, cx, cy, s, -13, 0, r, -38, 38, col, 1.2)


def e_icecream(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-6.6, 0), (6.6, 0), (0, 15)], col)
    for a, b in (((-3.4, 0), (1.6, 11)), ((0, 0), (3.2, 7.4)), ((3.4, 0), (-1.6, 11)), ((0, 0), (-3.2, 7.4))):
        _L(d, cx, cy, s, [a, b], bg, .3)
    for x, y in ((-3.8, -3.6), (3.8, -3.6), (0, -9.6)):
        _C(d, cx, cy, s, x, y, 5.2, col)
    _R(d, cx, cy, s, -7.4, -.4, 7.4, .4, bg)


def e_lantern(img, d, cx, cy, s, col, bg):
    _L(d, cx, cy, s, [(0, -9), (0, -15)], col, .5)
    _E(d, cx, cy, s, 0, 0, 10.4, 8.6, col)
    _E(d, cx, cy, s, 0, 0, 5.4, 8.6, None, bg, .35)
    _R(d, cx, cy, s, -.2, -8.6, .2, 8.6, bg)
    _R(d, cx, cy, s, -4.8, -10.4, 4.8, -8, col)
    _R(d, cx, cy, s, -4.8, 8, 4.8, 10.4, col)
    for x in (-2.4, 0, 2.4):
        _L(d, cx, cy, s, [(x, 10.4), (x, 15)], col, .4)


def e_shell(img, d, cx, cy, s, col, bg):
    for k in range(5):
        a = math.radians(222 + k * 24)
        _C(d, cx, cy, s, 17 * math.cos(a), 10 + 17 * math.sin(a), 3.9, col)
    _S(d, cx, cy, s, 0, 10, 18.4, 210, 330, col)
    for k in range(6):
        a = math.radians(210 + k * 24)
        _L(d, cx, cy, s, [(0, 10), (21 * math.cos(a), 10 + 21 * math.sin(a))], bg, .3)
    _P(d, cx, cy, s, [(-6.4, 8.6), (6.4, 8.6), (4.6, 13.4), (-4.6, 13.4)], col)


def e_cashcoin(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 0, 0, 13.6, col)
    _C(d, cx, cy, s, 0, 0, 11.6, None, bg, .35)
    _R(d, cx, cy, s, -3.6, -3.6, 3.6, 3.6, bg)
    for x, y in ((0, -7.6), (0, 7.6), (-7.6, 0), (7.6, 0)):
        _R(d, cx, cy, s, x - 1.5, y - .3, x + 1.5, y + .3, bg)
        _R(d, cx, cy, s, x - .3, y - 1.5, x + .3, y + 1.5, bg)


def e_door(img, d, cx, cy, s, col, bg):
    _R(d, cx, cy, s, -11.4, -12, 11.4, 13.6, col)
    _P(d, cx, cy, s, [(-14.6, -12), (14.6, -12), (11.4, -15.4), (-11.4, -15.4)], col)
    _R(d, cx, cy, s, -.25, -12, .25, 13.6, bg)
    for y in (-8.4, -4.4, 6.4, 10.4):
        for x in (-8.6, -4.4, 4.4, 8.6):
            _C(d, cx, cy, s, x, y, .85, bg)
    for x in (-3.6, 3.6):
        _C(d, cx, cy, s, x, 1, 2.1, None, bg, .5)


def e_teapot(img, d, cx, cy, s, col, bg):
    _E(d, cx, cy, s, -11, 2.6, 4.4, 5.4, None, col, 1.3)
    _P(d, cx, cy, s, [(7, 1), (14.6, -5.4), (16.6, -4), (10.4, 6.6)], col)
    _E(d, cx, cy, s, -1, 3, 10.4, 7.8, col)
    _E(d, cx, cy, s, -1, -4.8, 5.4, 1.7, col)
    _C(d, cx, cy, s, -1, -7.4, 1.7, col)
    _R(d, cx, cy, s, -6, 10.4, 4, 12, col)
    _A(d, cx, cy, s, -1, 3, 8, 200, 250, bg, .4, ry=5.6)


def e_bamboo(img, d, cx, cy, s, col, bg):
    for x, top in ((-7.4, -9), (0, -14), (7.4, -5)):
        _R(d, cx, cy, s, x - 1.6, top, x + 1.6, 14, col)
        y = top + 5.4
        while y < 13:
            _R(d, cx, cy, s, x - 2.1, y, x + 2.1, y + .9, col)
            _R(d, cx, cy, s, x - 1.6, y + .9, x + 1.6, y + 1.4, bg)
            y += 6.2
    for x, y, m in ((1.6, -9, 1), (-9, -4, -1), (9, 0, 1)):
        _P(d, cx, cy, s, [(x, y), (x + m * 7, y - 4.4), (x + m * 4, y + .6)], col)
        _P(d, cx, cy, s, [(x, y), (x + m * 6.4, y + 2.6), (x + m * 2.6, y + 2)], col)


def e_taeguk(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 0, 0, 12.6, None, col, .5)
    _S(d, cx, cy, s, 0, 0, 12.6, 180, 360, col)
    _C(d, cx, cy, s, -6.3, 0, 6.3, col)
    _C(d, cx, cy, s, 6.3, 0, 6.05, bg)
    for m in (1, -1):
        for k in range(3):
            _L(d, cx, cy, s, _rot([(m * (15 + k * 1.5), -3.4), (m * (15 + k * 1.5), 3.4)], m * -35), col, .5)


def e_drum(img, d, cx, cy, s, col, bg):
    for m in (1, -1):
        _P(d, cx, cy, s, [(m * 13, -9), (m * 1.6, -3), (m * 1.6, 3), (m * 13, 9)], col)
        _E(d, cx, cy, s, m * 13, 0, 2.2, 10, col)
    for k in range(4):
        y0 = -6.6 + k * 4.4
        _L(d, cx, cy, s, [(-11, y0), (11, -y0 * .0 + (y0 + 2.2 if k % 2 == 0 else y0 - 2.2))], bg, .3)
    _R(d, cx, cy, s, -1.6, -3.6, 1.6, 3.6, col)


def e_foursq(img, d, cx, cy, s, col, bg):
    for x, y in ((-11.4, -11.4), (1, -11.4), (-11.4, 1), (1, 1)):
        _R(d, cx, cy, s, x, y, x + 10.4, y + 10.4, col)
    d.rectangle([cx + mm(3.6 * s), cy + mm(-8.8 * s), cx + mm(8.8 * s), cy + mm(-3.6 * s)], fill=bg)
    _R(d, cx, cy, s, -8.8, 3.6, -3.6, 8.8, bg)


def e_horns(img, d, cx, cy, s, col, bg):
    for m in (1, -1):
        _L(d, cx, cy, s, [(m * 2, 1), (m * 7, -3), (m * 4, -6.4), (m * 10, -10), (m * 7, -13), (m * 11.4, -16)], col, 1.7)
        _P(d, cx, cy, s, [(m * 3, 2.4), (m * 9, 2), (m * 4, 5)], col)
    _P(d, cx, cy, s, [(-3.6, 0), (3.6, 0), (2.6, 9), (0, 13.6), (-2.6, 9)], col)
    _P(d, cx, cy, s, [(-1.2, 12), (1.2, 12), (0, 16.4)], col)


def e_clove(img, d, cx, cy, s, col, bg):
    for ox, oy, a, k in ((0, 0, 20, 1.), (-10, 3, -30, .62), (10.4, 5, 55, .55)):
        q = lambda pts: _rot([(ox + x * k, oy + y * k) for x, y in pts], a, ox, oy)
        _P(d, cx, cy, s, q([(-1.5, -3), (1.5, -3), (.8, 14), (-.8, 14)]), col)
        _P(d, cx, cy, s, q(_ell(0, -8, 3.6, 3.6)), col)
        for m in (1, -1):
            _P(d, cx, cy, s, q([(m * 1.4, -3), (m * 7, -8.4), (m * 2.6, -7.4)]), col)
        _P(d, cx, cy, s, q([(-1.6, -10.6), (0, -14), (1.6, -10.6)]), col)


def e_elephant(img, d, cx, cy, s, col, bg):
    _E(d, cx, cy, s, -3, 1.6, 10.4, 7.4, col)
    _C(d, cx, cy, s, 7.6, -2.6, 5.8, col)
    _L(d, cx, cy, s, [(11.6, -1), (14.6, 4), (13.6, 9), (16, 11.4)], col, 2.2)
    _R(d, cx, cy, s, -10.6, 5, -6.4, 13, col)
    _R(d, cx, cy, s, -.6, 5, 3.6, 13, col)
    _E(d, cx, cy, s, 4.4, -2, 3, 4.6, None, bg, .4)
    _C(d, cx, cy, s, 9.6, -4, .7, bg)
    _L(d, cx, cy, s, [(-13, -1), (-15.6, 3), (-14.6, 6)], col, .5)
    _L(d, cx, cy, s, [(10, 2), (13, 5.4)], bg, .5)


def e_umbrella(img, d, cx, cy, s, col, bg):
    _S(d, cx, cy, s, 0, -1, 14.6, 180, 360, col)
    for x in (-10.95, -3.65, 3.65, 10.95):
        _C(d, cx, cy, s, x, -1, 3.65, bg)
    _R(d, cx, cy, s, -.4, -17.4, .4, -14, col)
    _L(d, cx, cy, s, [(0, -4), (0, 10.6), (-1.6, 13), (-4, 13), (-5, 11)], col, .9)
    for x, y in ((-13, 4), (-9.4, 9.4), (10, 4), (13.6, 9.4), (6.4, 10.4)):
        _L(d, cx, cy, s, [(x, y), (x - 1.2, y + 3)], col, .5)


def e_trophy(img, d, cx, cy, s, col, bg):
    for m in (1, -1):
        _E(d, cx, cy, s, m * 8.6, -7.4, 3.8, 4.6, None, col, 1.1)
    _P(d, cx, cy, s, [(-8.4, -13), (8.4, -13), (6.4, -2.6), (2.4, 3), (-2.4, 3), (-6.4, -2.6)], col)
    _R(d, cx, cy, s, -1.3, 3, 1.3, 8.4, col)
    _R(d, cx, cy, s, -6, 8.4, 6, 10.6, col)
    _R(d, cx, cy, s, -8.4, 10.6, 8.4, 13.6, col)
    _P(d, cx, cy, s, _star(5, 3.2, 1.3, -90, 0, -7), bg)


def e_cap(img, d, cx, cy, s, col, bg):
    _S(d, cx, cy, s, -2, 2, 10.6, 180, 360, col)
    _P(d, cx, cy, s, [(7.6, -.6), (18, 1.6), (17, 4), (7.6, 2.6)], col)
    for x in (-8.4, -2, 4.4):
        _L(d, cx, cy, s, [(-2, -8.6), (x, 2)], bg, .4)
    _C(d, cx, cy, s, -2, -8.9, 1.4, col)
    _L(d, cx, cy, s, [(-14, 12.4), (12, 8)], col, .7)
    _E(d, cx, cy, s, 13.6, 7.6, 1.7, 1.1, None, col, .4)


def e_mate(img, d, cx, cy, s, col, bg):
    _L(d, cx, cy, s, [(1.6, 0), (9.6, -15)], col, 1.)
    _C(d, cx, cy, s, 0, 5.4, 8.4, col)
    _P(d, cx, cy, s, [(-5.4, 0), (5.4, 0), (4.4, -4.6), (-4.4, -4.6)], col)
    _R(d, cx, cy, s, -5, -6, 5, -4.4, col)
    _R(d, cx, cy, s, -8.4, 4.6, 8.4, 5.4, bg)
    for x in (-5, -1.6, 1.8, 5.2):
        _P(d, cx, cy, s, [(x - 1.2, 6.4), (x + 1.2, 6.4), (x, 8.8)], bg)


def e_bowler(img, d, cx, cy, s, col, bg):
    _S(d, cx, cy, s, 0, 3, 9.6, 180, 360, col, ry=12.4)
    _P(d, cx, cy, s, [(-13.6, 3), (13.6, 3), (16, 4.4), (15, 6.6), (11, 7), (-11, 7), (-15, 6.6), (-16, 4.4)], col)
    _R(d, cx, cy, s, -9.5, -.2, 9.5, 1.8, bg)


def e_acacia(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-1.2, 13), (1.2, 13), (1.5, 3), (7, -3.4), (5.4, -4.6), (0, .6), (-5.4, -4.6), (-7, -3.4), (-1.5, 3)], col)
    _E(d, cx, cy, s, 0, -6.4, 15.6, 3.4, col)
    _E(d, cx, cy, s, -6.4, -8.6, 7.4, 2.7, col)
    _E(d, cx, cy, s, 6.4, -9.4, 7, 2.6, col)
    _R(d, cx, cy, s, -16, 13, 16, 13.8, col)


def e_chair(img, d, cx, cy, s, col, bg):
    _A(d, cx, cy, s, 0, -9, 23, 58, 122, col, 1.1)
    _L(d, cx, cy, s, [(-8, 3.6), (7, 3.6)], col, 1.5)
    _L(d, cx, cy, s, [(-7.6, 3.6), (-10.4, -14)], col, 1.3)
    _L(d, cx, cy, s, [(-4.4, 3.6), (-6.6, -13)], col, .5)
    _L(d, cx, cy, s, [(-10.4, -13.6), (-6.2, -13.4)], col, 1.3)
    _L(d, cx, cy, s, [(-6.4, 3.6), (-8, 12.6)], col, .9)
    _L(d, cx, cy, s, [(5.6, 3.6), (7.4, 12.6)], col, .9)
    _L(d, cx, cy, s, [(-9, -3), (7, -3), (7, 3.6)], col, .8)


def e_boomerang(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, _rot([(-15, 8), (-11, 11), (0, -4.6), (11, 11), (15, 8), (2, -12), (0, -13), (-2, -12)], 14), col)
    for m in (1, -1):
        for t in (.3, .5, .7):
            q = _rot([(m * (1 + 11 * t), -9.4 + 18 * t)], 14)[0]
            _C(d, cx, cy, s, q[0], q[1], 1., bg)


def e_sunflower(img, d, cx, cy, s, col, bg):
    for k in range(14):
        _P(d, cx, cy, s, _rot([(-2, -9), (0, -15.6), (2, -9)], k * 360 / 14, 0, -4), col)
    _C(d, cx, cy, s, 0, -4, 6.4, col)
    for x in (-3, 0, 3):
        for y in (-7, -4, -1):
            _C(d, cx, cy, s, x, y, .7, bg)
    _L(d, cx, cy, s, [(0, 6.4), (0, 15)], col, .9)
    _P(d, cx, cy, s, [(0, 13), (7.4, 8), (5, 13)], col)
    _P(d, cx, cy, s, [(0, 13.6), (-7.4, 9), (-5, 14)], col)


def e_pouch(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-6, -5), (-8.4, -12), (-3, -9), (0, -13), (3, -9), (8.4, -12), (6, -5)], col)
    _P(d, cx, cy, s, [(-5, -5), (5, -5), (10.4, 3), (9.4, 10), (4, 13.6), (-4, 13.6), (-9.4, 10), (-10.4, 3)], col)
    _R(d, cx, cy, s, -6.4, -5.6, 6.4, -4.6, bg)
    _L(d, cx, cy, s, [(4, -5), (10, -3), (12.6, 1)], col, .45)
    _C(d, cx, cy, s, 12.8, 1.8, 1.2, col)
    q = _poly(4, 4.6, -90, 0, 4.6)
    d.line(_pts(cx, cy, s, q + [q[0]]), fill=bg, width=max(2, mm(.4 * s)))


def e_ring(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 0, 4.6, 8.8, None, col, 1.8)
    _P(d, cx, cy, s, [(-5.6, -9), (-3, -12.6), (3, -12.6), (5.6, -9), (0, -3.4)], col)
    _L(d, cx, cy, s, [(-5.6, -9), (5.6, -9)], bg, .3)
    for a, b in (((-10, -13), (-12.6, -15.4)), ((10, -13), (12.6, -15.4)), ((0, -15), (0, -18))):
        _L(d, cx, cy, s, [a, b], col, .4)


def e_crab(img, d, cx, cy, s, col, bg):
    _E(d, cx, cy, s, 0, 4, 9.4, 6, col)
    for m in (1, -1):
        _L(d, cx, cy, s, [(m * 7, 1), (m * 12, -5)], col, 1.3)
        _C(d, cx, cy, s, m * 12.6, -8, 4, col)
        _P(d, cx, cy, s, [(m * 12.6, -8), (m * 9, -14), (m * 13.6, -13)], bg)
        for k, (y, ex, ey) in enumerate(((4, 15.6, 3), (6, 15, 8.4), (8, 12.6, 12.6))):
            _L(d, cx, cy, s, [(m * 7.6, y), (m * (ex - 2.6), y - 1 + k), (m * ex, ey)], col, .7)
        _L(d, cx, cy, s, [(m * 3, -1), (m * 3.6, -4.4)], col, .5)
        _C(d, cx, cy, s, m * 3.6, -5, 1.1, col)


def e_globe(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 0, 0, 13, None, col, .9)
    _E(d, cx, cy, s, 0, 0, 5.6, 13, None, col, .5)
    _R(d, cx, cy, s, -.25, -13, .25, 13, col)
    _R(d, cx, cy, s, -13, -.25, 13, .25, col)
    _A(d, cx, cy, s, 0, -13.6, 14, 38, 142, col, .5, ry=8)
    _A(d, cx, cy, s, 0, 13.6, 14, 218, 322, col, .5, ry=8)


def e_ball(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 0, 0, 13, col)
    p = _poly(5, 4.4)
    _P(d, cx, cy, s, p, bg)
    for k, (x, y) in enumerate(p):
        _L(d, cx, cy, s, [(x, y), (x * 2.2, y * 2.2)], bg, .45)
        q = _poly(5, 4., -90 + 36, 0, 0)
        a = math.radians(-90 + k * 72 + 36)
        _P(d, cx, cy, s, [(11.6 * math.cos(a) + 3.4 * math.cos(a + t), 11.6 * math.sin(a) + 3.4 * math.sin(a + t)) for t in (math.pi, math.pi * .55, -math.pi * .55)], bg)


def e_swan(img, d, cx, cy, s, col, bg):
    _E(d, cx, cy, s, -2, 3, 10, 5, col)
    _P(d, cx, cy, s, [(-12, 2), (-16, -4), (-9, -1)], col)
    _L(d, cx, cy, s, [(5, 2), (9, -3), (6, -9), (9, -13)], col, 2.2)
    _C(d, cx, cy, s, 9.4, -13, 2, col)
    _P(d, cx, cy, s, [(10.6, -14), (15, -12), (10.6, -11.6)], col)
    _A(d, cx, cy, s, -3, 3, 7, 190, 300, bg, .4, ry=3.4)
    waves9(d, cx - mm(17 * s), cx + mm(17 * s), cy + mm(10.4 * s), col, 2, gap=2.2 * s)


CHANGES = {
    # batch 10
    'canclassics': e_canoe, 'numero7': e_num2('7', 'plain'), 'bellmont': e_bell, 'matinay': e_cup, 'peterjaxon': e_tophat,
    'laikka': e_sputnik, 'polyott': e_propeller, 'severr': e_snowflake, 'lubitelskiye': e_pawn, 'vattra': e_bonfire,
    'orbitta': e_orbits, 'ligerros': e_guitar, 'rodoppi': e_grapes, 'popularrne': e_heart, 'karmen': e_handfan,
    'pettra': e_clock, 'startt': e_rocket, 'clubb': e_globe, 'filter58': e_num2('58', 'square'),
    # batch 11
    'hbee': e_bee, 'revall': e_gull, 'junno': e_peacock, 'seniorservise': e_shipwheel, 'embassey': e_envelope, 'regall': e_orb,
    'sweetaftonn': e_shamrock, 'majorr': e_bugle, 'emms': e_colosseum, 'nazionalli': e_bicycle, 'fortunna': e_horseshoe,
    'celtass': e_triskele, 'idealles': e_bull, 'sgventill': e_pinwheel, 'suavve': e_tile, 'karellia': e_amphora,
    'assoss': e_windmill, 'maltepeh': e_tulip, 'bafrah': e_fez, 'princce': e_fleur, 'northstaate': e_compass,
    'klubbi': e_num2('77', 'diamond'), 'blennd': e_blendcircles, 'johnsylver': e_piratehat, 'marylongg': e_ladyhat,
    'memfis': e_ankh, 'belgga': e_atomium, 'bastoss': e_moustache, 'caballerro': e_boot, 'oldegold': e_chest,
    'raleygh': e_feather, 'tareytonn': e_glove, 'slimms': e_lipstick, 'caprii': e_lemon, 'mistyy': e_butterfly,
    'meritt': e_medal, 'carltonn': e_scale, 'trew': e_balloon, 'dorall': e_cherry, 'basik': e_barcode, 'giration': e_dogtags,
    'ekko': e_soundwaves, 'castter': e_icecream, 'pianissimmo': e_keys, 'liquun': e_lantern, 'baishah': e_shell,
    'nanjinng': e_cashcoin, 'frontgatte': e_door, 'hatamenn': e_teapot, 'hongmey': e_bamboo, 'thiss': e_taeguk,
    'k89': e_num2('89', 'hex'), 'arirangg': e_drum, 'goldflaik': e_honeycomb, 'wyllis': e_telescope, 'fourskware': e_foursq,
    'k3': e_horns, 'sampurna': e_kris, 'bentoell': e_clove, 'wismilakk': e_batik, 'krongthipp': e_elephant,
    'fallingrainn': e_umbrella, 'fortoon': e_dice, 'championn': e_trophy, 'jockeyklub': e_cap, 'particularres': e_mate,
    'k4371': e_num2('43', 'oval'), 'holywood': e_clapper, 'derbby': e_ball, 'moravva': e_swan, 'gunstonn': e_acacia, 'lexingtonn': e_chair,
    'winfeeld': e_boomerang, 'prilukki': e_sunflower, 'russkystyle': e_dome, 'makhorrka': e_pouch, 'vegga': e_constellation,
    'juwell': e_ring, 'kabinett': e_briefcase, 'opatiya': e_crab,
}
# brands that were type-only (layout T) and now carry a figure
RELAYOUT = {'popularrne': 'C', 'emms': 'C', 'idealles': 'A', 'trew': 'C', 'basik': 'C', 'thiss': 'A', 'particularres': 'A'}


def install(g):
    globals().update({k: g[k] for k in NEED})
    for key, fn in CHANGES.items():
        tab = g['B10'] if key in g['B10'] else g['B11']
        em, lay, font, foot = tab[key]
        tab[key] = (fn, RELAYOUT.get(key, lay), font, foot)

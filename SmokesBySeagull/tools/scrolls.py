"""Baroque scroll (acanthus) ornament for engraved panels: leafy blades that taper into tight curls, with smaller counter-curls
branching off, staggered to fill a panel. scroll_mask(W, H, seed) -> PIL L mask, white = raised scroll."""
import math, random
from PIL import Image, ImageDraw, ImageFilter


def _blade(d, cx, cy, r0, a0, sign, w0, turns=1.55, b=.30, lobes=4):
    """a spiral leaf: centreline r = r0 e^(-b th), width tapering to the curl, the outer edge scalloped into lobes"""
    n = 90
    L, R = [], []
    th_end = 2 * math.pi * turns
    pts = []
    for i in range(n + 1):
        t = i / n
        th = th_end * t
        r = r0 * math.exp(-b * th)
        a = a0 + sign * th
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a), a))
    for i, (x, y, a) in enumerate(pts):
        t = i / n
        w = w0 * min(1.0, t * 7) * (1 - t) ** .8 * (1 + .45 * abs(math.sin(lobes * math.pi * t))) + 1.4
        nx, ny = math.cos(a), math.sin(a)  # radial direction
        L.append((x + nx * w * .62, y + ny * w * .62))
        R.append((x - nx * w * .38, y - ny * w * .38))
    d.polygon(L + R[::-1], fill=255)
    return pts


def scroll_mask(W, H, seed=3, unit=None):
    rnd = random.Random(seed)
    m = Image.new('L', (W, H), 0)
    d = ImageDraw.Draw(m)
    s = unit or min(W, H) * .24
    cols = max(2, round(W / (s * 1.25)))
    rows = max(3, round(H / (s * 1.05)))
    for j in range(-1, rows + 2):
        for i in range(-1, cols + 2):
            cx = (i + .5 * (j % 2)) * W / cols + rnd.uniform(-.1, .1) * s
            cy = j * H / rows + rnd.uniform(-.1, .1) * s
            sign = 1 if j % 2 else -1
            a0 = rnd.uniform(0, 2 * math.pi)
            pts = _blade(d, cx, cy, s * .55, a0, sign, s * .22)
            # a thin stem sweeping from the blade's tail to the next unit
            x0, y0, a = pts[0]
            stem = []
            for q in range(30):
                t = q / 29
                aa = a - sign * (math.pi / 2 + 1.2 * t)
                stem.append((x0 + s * .9 * t * math.cos(aa), y0 + s * .9 * t * math.sin(aa)))
            for q in range(29):
                w = max(1, int(s * .05 * (1 - .6 * q / 29)))
                d.line([stem[q], stem[q + 1]], fill=255, width=w)
            # two leaves branching from the outer arc, curling the other way
            for k, (f, sc) in enumerate(((.10, .45), (.24, .30))):
                x, y, a = pts[int(len(pts) * f)]
                ox, oy = x + math.cos(a) * s * .32 * sc / .45, y + math.sin(a) * s * .32 * sc / .45
                _blade(d, ox, oy, s * sc * .55, a + math.pi, -sign, s * sc * .26, turns=.9, lobes=3)
    return m.filter(ImageFilter.GaussianBlur(.7))

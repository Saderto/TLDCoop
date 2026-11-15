"""Drawing kit for the labels: fonts, paper/metal/foil grounds, ornaments, text on a line or arc, small vector pictures.
Everything is drawn at SS times the size and scaled down at the end (smooth edges)."""
import os, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

HERE = os.path.dirname(os.path.abspath(__file__))
FONT_DIRS = [os.path.join(HERE, 'fonts'), '/usr/share/fonts/truetype/dejavu', '/usr/share/fonts/truetype/wqy', '/usr/share/fonts/truetype/freefont',
             '/usr/share/fonts/truetype/liberation']
SS = 2
_fc = {}


def font(name, size):
    """name like 'playfair-display-900' (tools/fonts/<name>.ttf) or a system file name."""
    key = (name, int(size))
    if key not in _fc:
        path = None
        for d in FONT_DIRS:
            for ext in ('', '.ttf', '.ttc'):
                p = os.path.join(d, name + ext)
                if os.path.isfile(p): path = p; break
            if path: break
        if path is None:
            import glob
            g = glob.glob('/usr/share/fonts/**/' + name + '*', recursive=True)
            path = g[0] if g else None
        if path is None: raise FileNotFoundError(name)
        _fc[key] = ImageFont.truetype(path, int(size))
    return _fc[key]


CJK = 'wqy-zenhei'


class Canvas:
    def __init__(s, w=1024, h=1024, bg=(255, 255, 255)):
        s.W, s.H = w, h
        s.im = Image.new('RGBA', (w * SS, h * SS), tuple(bg) + (255,) if len(bg) == 3 else bg)
        s.d = ImageDraw.Draw(s.im, 'RGBA')

    # coordinates are given in final pixels
    def S(s, *v):
        return [x * SS for x in v]

    def rect(s, x0, y0, x1, y1, fill=None, outline=None, width=1, r=0):
        if r: s.d.rounded_rectangle(s.S(x0, y0, x1, y1), r * SS, fill=fill, outline=outline, width=int(width * SS))
        else: s.d.rectangle(s.S(x0, y0, x1, y1), fill=fill, outline=outline, width=int(width * SS))

    def ellipse(s, x0, y0, x1, y1, fill=None, outline=None, width=1):
        s.d.ellipse(s.S(x0, y0, x1, y1), fill=fill, outline=outline, width=int(width * SS))

    def circle(s, cx, cy, r, fill=None, outline=None, width=1):
        s.ellipse(cx - r, cy - r, cx + r, cy + r, fill, outline, width)

    def poly(s, pts, fill=None, outline=None, width=1):
        p = [(x * SS, y * SS) for x, y in pts]
        s.d.polygon(p, fill=fill)
        if outline: s.d.line(p + [p[0]], fill=outline, width=int(width * SS), joint='curve')

    def line(s, pts, fill, width=1):
        s.d.line([(x * SS, y * SS) for x, y in pts], fill=fill, width=max(1, int(width * SS)), joint='curve')

    def arc(s, cx, cy, r, a0, a1, fill, width=1):
        s.d.arc(s.S(cx - r, cy - r, cx + r, cy + r), a0, a1, fill=fill, width=int(width * SS))

    def text(s, x, y, txt, fnt, size, fill, anchor='mm', spacing=0, stroke=0, stroke_fill=None, maxw=None):
        size = float(size)
        if not txt: return size
        f = font(fnt, size * SS)
        if maxw:
            while s.textw(txt, fnt, size, spacing) > maxw and size > 6:
                size *= .95; f = font(fnt, size * SS)
        if spacing:
            # letter by letter with extra spacing
            tw = s.textw(txt, fnt, size, spacing)
            ax = x - tw / 2 if anchor[0] == 'm' else (x - tw if anchor[0] == 'r' else x)
            for ch in txt:
                s.d.text((ax * SS, y * SS), ch, font=f, fill=fill, anchor='l' + anchor[1], stroke_width=int(stroke * SS), stroke_fill=stroke_fill)
                ax += f.getlength(ch) / SS + spacing
        else:
            s.d.text((x * SS, y * SS), txt, font=f, fill=fill, anchor=anchor, stroke_width=int(stroke * SS), stroke_fill=stroke_fill)
        return size

    def textw(s, txt, fnt, size, spacing=0):
        f = font(fnt, size * SS)
        return f.getlength(txt) / SS + spacing * max(0, len(txt) - 1)

    def text_arc(s, cx, cy, r, txt, fnt, size, fill, center_deg=-90, spacing=0, inside=False):
        """letters along a circle, centred at center_deg (-90 = top); inside: read from inside (bottom arc)."""
        f = font(fnt, size * SS)
        widths = [f.getlength(ch) / SS + spacing for ch in txt]
        total = sum(widths) - spacing
        ang = math.radians(center_deg) - (total / 2) / r * (-1 if inside else 1)
        for ch, w in zip(txt, widths):
            mid = ang + (w / 2 - spacing / 2) / r * (-1 if inside else 1)
            x, y = cx + r * math.cos(mid), cy + r * math.sin(mid)
            g = Image.new('RGBA', (int(size * SS * 2), int(size * SS * 2)), (0, 0, 0, 0))
            ImageDraw.Draw(g).text((g.width / 2, g.height / 2), ch, font=f, fill=fill, anchor='mm')
            rot = -math.degrees(mid) - 90 if not inside else -math.degrees(mid) + 90
            g = g.rotate(rot, resample=Image.BICUBIC)
            s.im.alpha_composite(g, (int(x * SS - g.width / 2), int(y * SS - g.height / 2)))
            ang += w / r * (-1 if inside else 1)

    def paste(s, img, x, y):
        if img.size[0] != img.size[0]: pass
        s.im.alpha_composite(img, (int(x * SS), int(y * SS)))

    def layer(s):
        return Canvas.__new__(Canvas)._init_layer(s.W, s.H)

    def _init_layer(s, w, h):
        s.W, s.H = w, h
        s.im = Image.new('RGBA', (w * SS, h * SS), (0, 0, 0, 0)); s.d = ImageDraw.Draw(s.im, 'RGBA')
        return s

    def over(s, other, opacity=1.0):
        im = other.im
        if opacity < 1:
            a = im.getchannel('A').point(lambda v: int(v * opacity)); im = im.copy(); im.putalpha(a)
        s.im.alpha_composite(im)

    def final(s):
        return s.im.resize((s.W, s.H), Image.LANCZOS)


# ---------------------------------------------------------------- grounds
def noise(w, h, scale=8, seed=0, octaves=4):
    rng = np.random.default_rng(seed)
    out = np.zeros((h, w))
    amp = 1.0; tot = 0
    for o in range(octaves):
        s = max(1, int(scale * 2 ** o))
        g = rng.random((s + 1, s + 1))
        im = Image.fromarray((g * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
        out += np.asarray(im, float) / 255 * amp; tot += amp; amp *= .5
    return out / tot


def texturize(img, strength=.10, seed=1, scale=24, grain=.035, fibres=0.0):
    """paper or print wear over a finished picture (RGB/RGBA)."""
    a = np.asarray(img.convert('RGBA'), float)
    h, w = a.shape[:2]
    n = noise(w, h, scale, seed) - .5
    g = np.random.default_rng(seed + 7).normal(0, 1, (h, w))
    k = 1 + n * strength * 2 + g * grain
    if fibres:
        f = noise(w, h * 1, 180, seed + 3, 2) - .5
        k += f * fibres
    a[..., :3] = np.clip(a[..., :3] * k[..., None], 0, 255)
    return Image.fromarray(a.astype(np.uint8), 'RGBA')


def metal(w, h, base=(170, 170, 172), seed=2, brushed=False):
    rng = np.random.default_rng(seed)
    a = np.ones((h, w, 3)) * np.array(base, float)
    if brushed:
        line = rng.normal(0, 1, (1, w)) * 6 + noise(w, 1, 64, seed)[0][None] * 20 - 10
        a += line[..., None]
    a += (noise(w, h, 6, seed + 1) - .5)[..., None] * 30
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).convert('RGBA')


_can_top = None


def tab_shape(n=64):
    """the can's stay-on tab seen from above (model metres, +y to the mouth): its outline and the finger window, n points each, same angles.
    A rounded plate a little narrower at the nose, the window in the half away from the mouth (owner 11-15: as on a real can)."""
    def sup(cx, cy, a, b, e, taper=0.0):
        pts = []
        for i in range(n):
            f = 2 * math.pi * i / n
            c, s = math.cos(f), math.sin(f)
            x = a * abs(c) ** (2 / e) * (1 if c >= 0 else -1); y = b * abs(s) ** (2 / e) * (1 if s >= 0 else -1)
            x *= 1 - taper * (y + b) / (2 * b)
            pts.append((cx + x, cy + y))
        return pts
    return sup(0, -.0042, .0064, .0090, 3.2, .20), sup(0, -.0086, .0037, .0029, 3.0)


TAB_RIVET = (0.0, .0008, .0019)


def can_lid_plan(N=1024, span=.042):
    """the can's end seen from above (owner 11-15: like a real one): brushed panel, the scored mouth, the tab's plate under the model's finger ring,
    the rivet, pressed beads. Model space: x right, +y to the back of the can; N px over span metres, centre in the middle."""
    S = N / span; c0 = N / 2
    P = lambda x, y: (c0 + x * S, c0 - y * S)
    rng = np.random.default_rng(5)
    base = np.ones((N, N)) * 181.0
    base += (noise(N, N, 5, 3, 3) - .5) * 14 + rng.normal(0, 3.2, (N, N))
    streak = np.asarray(Image.fromarray((rng.random((N, 8)) * 255).astype(np.uint8)).resize((N, N), Image.BICUBIC), float) / 255   # brushing across
    base += (streak - .5) * 16
    yy, xx = np.mgrid[0:N, 0:N]
    base += (c0 - yy) / N * 26 + (c0 - xx) / N * 10    # light from the upper left
    im = Image.fromarray(np.clip(np.stack([base, base + 1, base + 5], -1), 0, 255).astype(np.uint8)).convert('RGBA')

    def emboss(draw_fn, depth=1.0, w=3):
        """a pressed line: dark on the lit side's far edge, light on the near one"""
        for off, col in ((+1.6, (255, 255, 255, int(120 * depth))), (-1.6, (40, 42, 48, int(150 * depth)))):
            lay = Image.new('RGBA', (N, N), (0, 0, 0, 0)); draw_fn(ImageDraw.Draw(lay), off, col, w)
            im.alpha_composite(lay.filter(ImageFilter.GaussianBlur(.8)))

    def ring(cx, cy, rx, ry=None):
        ry = ry or rx
        return lambda d, off, col, w: d.ellipse([P(cx - rx, cy + ry)[0] + off, P(cx - rx, cy + ry)[1] + off, P(cx + rx, cy - ry)[0] + off, P(cx + rx, cy - ry)[1] + off], outline=col, width=w)

    emboss(ring(0, 0, .0184), .9, 4)                     # the panel's step
    emboss(ring(0, 0, .0174), .5, 2)
    # pressed beads either side of the tab, as on a real end
    def arc(side):
        def f(d, off, col, w):
            pts = []
            for k in range(40):
                a = math.radians(-70 + 140 * k / 39)
                pts.append((P(side * (.0082 + .0036 * math.cos(a)), -.0015 + .0105 * math.sin(a))[0] + off, P(0, -.0015 + .0105 * math.sin(a))[1] + off))
            d.line(pts, fill=col, width=w, joint='curve')
        return f
    emboss(arc(1), .7, 3); emboss(arc(-1), .7, 3)
    # the mouth: a scored outline, the metal inside a touch darker
    d = ImageDraw.Draw(im, 'RGBA')
    # owner 11-15 (same as the soda cans): a drop-shaped mouth, the narrow end round the rivet, the wide end towards the rim, scored twice
    def mouth_pts(grow=0.0, n=160):
        pts = [(( .0082 + grow) * math.cos(t), .0104 + (.0066 + grow) * math.sin(t)) for t in (2 * math.pi * k / n for k in range(n))]
        pts += [((.0038 + grow) * math.cos(t), .0010 + (.0038 + grow) * math.sin(t)) for t in (2 * math.pi * k / n for k in range(n))]
        pts = sorted(set(pts))
        def cross(o, a, b): return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
        lo, hi = [], []
        for p in pts:
            while len(lo) >= 2 and cross(lo[-2], lo[-1], p) <= 0: lo.pop()
            lo.append(p)
        for p in reversed(pts):
            while len(hi) >= 2 and cross(hi[-2], hi[-1], p) <= 0: hi.pop()
            hi.append(p)
        hull = lo[:-1] + hi[:-1]
        return [P(x, y) for x, y in hull + hull[:1]]
    MOUTH, MOUTH2 = mouth_pts(), mouth_pts(.0011)
    lay = Image.new('RGBA', (N, N), (0, 0, 0, 0)); dl = ImageDraw.Draw(lay)
    dl.polygon(MOUTH[:-1], fill=(60, 62, 70, 34))
    im.alpha_composite(lay)
    for pts_ in (MOUTH, MOUTH2):
        emboss(lambda dd, off, col, w, q=pts_: dd.line([(x + off, y + off) for x, y in q], fill=col, width=w, joint='curve'), 1.0, 3)
    d = ImageDraw.Draw(im, 'RGBA')
    d.line(MOUTH, fill=(70, 72, 80, 200), width=2, joint='curve')
    # the tab itself is modelled; here only its shadow on the panel
    outline, _ = tab_shape(96)
    sh = Image.new('RGBA', (N, N), (0, 0, 0, 0)); ds = ImageDraw.Draw(sh)
    ds.polygon([(P(x, y)[0] + 9, P(x, y)[1] + 11) for x, y in outline], fill=(20, 22, 28, 150))
    im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(7)))
    return im


def can_top():
    """rows 0..211 of every can texture: the end's picture laid into the model's own fan of triangles, then the seam's rings shaded by height.
    Row 0 is the plain metal of the finger ring and rivet (their vertices all sit on it)."""
    global _can_top
    if _can_top is not None: return _can_top
    import struct
    b = open(os.path.join(HERE, 'can_base.fpmesh'), 'rb').read()
    nv, ni = struct.unpack('<ii', b[:8]); o = 8
    v = np.frombuffer(b, '<f4', nv * 3, o).reshape(-1, 3).astype(float); o += nv * 24
    uv = np.frombuffer(b, '<f4', nv * 2, o).reshape(-1, 2).astype(float); o += nv * 8
    t = np.frombuffer(b, '<i4', ni, o).reshape(-1, 3)
    X, Y, Z = -v[:, 0], -v[:, 2], v[:, 1]
    R = np.hypot(X, Y)
    N, span = 1024, .042; S = N / span
    plan = can_lid_plan(N, span).convert('RGB')
    K = 4   # the strip is baked K times larger
    W, H = 1024 * K, 212 * K
    out = Image.new('RGB', (W, H), (150, 151, 155))
    # the seam, by rows (v of the model's rings): wall up from the panel, the bead's top, its outer side, the neck down to the printed wall
    stops = [(.895, 118), (.872, 176), (.869, 232), (.863, 236), (.859, 205), (.848, 168), (.84, 150), (.81, 150), (.795, 142)]
    col = np.zeros((H,))
    for y in range(H):
        vv = 1 - (y + .5) / (1024 * K)
        g = stops[0][1] if vv >= stops[0][0] else stops[-1][1]
        for (v0, g0), (v1, g1) in zip(stops, stops[1:]):
            if v1 <= vv <= v0: g = g0 + (g1 - g0) * (v0 - vv) / (v0 - v1)
        col[y] = g
    a = np.repeat(col[:, None], W, 1) + (noise(W, H, 6, 4, 2) - .5) * 8
    out = Image.fromarray(np.clip(np.stack([a, a + 1, a + 4], -1), 0, 255).astype(np.uint8))
    for grow in (4 * K, 0):   # first with a margin, so the unused slivers between the fan's triangles hold the neighbouring metal, then exactly
      for tri in t:
          if Z[tri].min() < .1300 or R[tri].max() > .0197: continue
          if (uv[tri] == 0).all(): continue
          dst = [(uv[i, 0] * W, (1 - uv[i, 1]) * 1024 * K) for i in tri]
          src = [(N / 2 + X[i] * S, N / 2 - Y[i] * S) for i in tri]
          A = np.array([[dx, dy, 1] for dx, dy in dst]); 
          if abs(np.linalg.det(A)) < 1e-9: continue
          cx = np.linalg.solve(A, np.array([s[0] for s in src])); cy = np.linalg.solve(A, np.array([s[1] for s in src]))
          g = grow + 2
          x0 = int(max(0, min(p[0] for p in dst) - g)); x1 = int(min(W, max(p[0] for p in dst) + g + 1)); y0 = int(max(0, min(p[1] for p in dst) - g)); y1 = int(min(int(.105 * 1024 * K) if grow else H, max(p[1] for p in dst) + g + 1))
          if x1 <= x0 or y1 <= y0: continue
          # affine for the box: output (x, y) of the box -> source
          coef = (cx[0], cx[1], cx[0] * x0 + cx[1] * y0 + cx[2], cy[0], cy[1], cy[0] * x0 + cy[1] * y0 + cy[2])
          piece = plan.transform((x1 - x0, y1 - y0), Image.AFFINE, coef, Image.BICUBIC)
          mask = Image.new('L', (x1 - x0, y1 - y0), 0)
          pts = [(px - x0, py - y0) for px, py in dst]
          ImageDraw.Draw(mask).polygon(pts, fill=255, outline=255)
          if grow: ImageDraw.Draw(mask).line(pts + pts[:1], fill=255, width=grow * 2, joint='curve')
          out.paste(piece, (x0, y0), mask)
    out = out.resize((1024, 212), Image.LANCZOS)
    d0 = ImageDraw.Draw(out); d0.rectangle([0, 0, 511, 1], fill=(212, 213, 218)); d0.rectangle([512, 0, 1024, 1], fill=(128, 129, 135))   # the tab's top and its edges
    _can_top = out
    return out


def can_bottom_profile():
    """the can's underside from the wall to the centre, (r, z, v) in the base can's metres (owner 11-15: as on a real can): the chamfer,
    the ring it stands on, the wall inside it and the dome pushed up into the can. v runs from the wall's last row (.152) to the texture's edge."""
    pts = [(.0262, .0038), (.0257, .0022), (.0244, .0008), (.0229, .0001), (.0216, 0.0), (.0205, .0002), (.0197, .0012), (.0192, .0030), (.0184, .0044)]
    for r in (.0155, .0125, .0095, .0065, .0035, 0.0):
        pts.append((r, .0044 + .0060 * (1 - (r / .0184) ** 2)))
    L = [0.0]
    for (r0, z0), (r1, z1) in zip(pts, pts[1:]): L.append(L[-1] + math.hypot(r1 - r0, z1 - z0))
    return [(r, z, .152 - (.152 - .004) * l / L[-1]) for (r, z), l in zip(pts, L)]


def can_bottom():
    """rows 869..1023 of every can texture: the underside by its rings, with the dome's soft shine on one side"""
    prof = can_bottom_profile()
    H0 = 868; H = 1024 - H0
    yy, xx = np.mgrid[0:H, 0:1024]
    vv = 1 - (yy + H0 + .5) / 1024
    vs = np.array([p[2] for p in prof])[::-1]; rs = np.array([p[0] for p in prof])[::-1]
    r = np.interp(vv, vs, rs)
    g = np.interp(r, [0, .0184, .0190, .0197, .0206, .0216, .0228, .0244, .0262], [176, 150, 96, 120, 205, 232, 190, 150, 142])
    ang = xx / 1024 * 2 * math.pi
    dome = np.clip(1 - (r / .0184) ** 2, 0, 1) ** .35 * (r < .0186)
    # the shine: a soft patch off-centre on one side, a darker one across from it
    g += dome * (62 * np.exp(-((r - .0105) / .0055) ** 2) * np.clip(np.cos(ang - 2.2), 0, 1) ** 2 - 26 * np.exp(-((r - .011) / .006) ** 2) * np.clip(np.cos(ang - 2.2 + math.pi), 0, 1) ** 2)
    g += (noise(1024, H, 5, 8, 3) - .5) * 12 + np.random.default_rng(8).normal(0, 2.5, (H, 1024))
    streak = np.asarray(Image.fromarray((np.random.default_rng(9).random((1, 256)) * 255).astype(np.uint8)).resize((1024, H), Image.BICUBIC), float) / 255
    g += (streak - .5) * 10 * dome     # drawn metal: faint lines running out from the centre
    return Image.fromarray(np.clip(np.stack([g, g + 1, g + 4], -1), 0, 255).astype(np.uint8))


def can_metal(seed):
    """bare metal of the drink can: the body's neck and bottom darker (owner 11-15), both ends painted like real ones (can_top, can_bottom)"""
    im = metal(1024, 1024, (142, 143, 147), seed)
    im.paste(can_top(), (0, 0))
    im.paste(can_bottom(), (0, 868))
    return im


def wear_edges(img, rect, amount=.5, seed=3, metal_col=(185, 185, 188)):
    """scuffs along the edges of a printed tin panel: bits where the paint is gone show bare metal."""
    return img   # owner 11-15: no wear marks on the models
    x0, y0, x1, y1 = rect
    a = np.asarray(img, float).copy()
    h, w = a.shape[:2]
    n = noise(w, h, 40, seed, 3)
    yy, xx = np.mgrid[0:h, 0:w]
    dist = np.minimum.reduce([np.abs(yy - y0), np.abs(yy - y1)]) / 30.0
    m = (n > .5 + dist * .35 / max(amount, .01)) & (yy >= y0) & (yy <= y1) & (xx >= x0) & (xx <= x1)
    a[m, :3] = a[m, :3] * .3 + np.array(metal_col) * .7
    return Image.fromarray(a.astype(np.uint8), 'RGBA')


def scratches(img, n=40, seed=5, col=(230, 230, 230), alpha=60):
    return img   # owner 11-15: the light scratch lines read as white stripes, removed everywhere
    rng = random.Random(seed)
    d = ImageDraw.Draw(img, 'RGBA')
    w, h = img.size
    for _ in range(n):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        a = rng.uniform(-.4, .4) + (0 if rng.random() < .5 else math.pi / 2)
        L = rng.uniform(4, 30)
        d.line([(x, y), (x + L * math.cos(a), y + L * math.sin(a))], fill=col + (alpha,), width=1)
    return img


# ---------------------------------------------------------------- ornaments
def border(c, x0, y0, x1, y1, col, w=3, gap=6, inner=True, corners=True):
    c.rect(x0, y0, x1, y1, outline=col, width=w)
    if inner: c.rect(x0 + gap, y0 + gap, x1 - gap, y1 - gap, outline=col, width=max(1, w * .45))
    if corners:
        r = gap * 1.6
        for (cx, cy) in ((x0, y0), (x1, y0), (x0, y1), (x1, y1)):
            c.circle(cx, cy, r * .55, fill=col)


def greek_band(c, x0, y, x1, h, col, unit=None):
    unit = unit or h
    x = x0
    while x + unit <= x1 + .1:
        u = unit; t = max(1, h * .14)
        pts = [(x, y + h), (x, y), (x + u * .8, y), (x + u * .8, y + h * .7), (x + u * .3, y + h * .7), (x + u * .3, y + h * .35), (x + u * .55, y + h * .35)]
        c.line(pts, col, t)
        c.line([(x, y + h), (x + u, y + h)], col, t)
        x += unit


def dots_band(c, x0, y, x1, r, col, step=None):
    step = step or r * 3
    x = x0 + step / 2
    while x < x1:
        c.circle(x, y, r, fill=col); x += step


def zigzag(c, x0, y, x1, h, col, step=14, width=2):
    pts = []; x = x0; up = True
    while x <= x1:
        pts.append((x, y if up else y + h)); x += step / 2; up = not up
    c.line(pts, col, width)


def leaf(c, cx, cy, L, W, ang, col, vein=None):
    a = math.radians(ang)
    pts = []
    for i in range(21):
        t = i / 20
        x = L * t; yv = W * math.sin(math.pi * t) ** .85 / 2
        pts.append((x, yv))
    pts += [(x, -y) for x, y in reversed(pts)]
    rp = [(cx + x * math.cos(a) - y * math.sin(a), cy + x * math.sin(a) + y * math.cos(a)) for x, y in pts]
    c.poly(rp, fill=col)
    if vein:
        c.line([(cx, cy), (cx + L * .92 * math.cos(a), cy + L * .92 * math.sin(a))], vein, max(1, W * .06))


def flower(c, cx, cy, r, petals, col, centre, ang=0, petal_w=.55, petal_shape=1.0):
    for i in range(petals):
        a = ang + 360 * i / petals
        leaf(c, cx, cy, r, r * petal_w, a, col)
    c.circle(cx, cy, r * .22, fill=centre)


def star(c, cx, cy, r, col, n=5, inner=.45, rot=-90):
    pts = []
    for i in range(n * 2):
        rr = r if i % 2 == 0 else r * inner
        a = math.radians(rot + 180 * i / n)
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    c.poly(pts, fill=col)


def mountains(c, x0, x1, base, peaks, col, snow=None, seed=1):
    rng = random.Random(seed)
    pts = [(x0, base)]
    for (px_, py_) in peaks:
        pts.append((px_, py_))
    pts.append((x1, base))
    c.poly(pts, fill=col)
    if snow:
        for i in range(1, len(pts) - 1):
            x, y = pts[i]
            prev, nxt = pts[i - 1], pts[i + 1]
            k = .22
            l = (x + (prev[0] - x) * k, y + (prev[1] - y) * k)
            r = (x + (nxt[0] - x) * k, y + (nxt[1] - y) * k)
            mid = ((l[0] + r[0]) / 2, (l[1] + r[1]) / 2 + abs(r[0] - l[0]) * .12)
            c.poly([l, (x, y), r, ((mid[0] + r[0]) / 2, mid[1] - 3), mid, ((mid[0] + l[0]) / 2, mid[1] - 2)], fill=snow)


def steam(c, x, y, h, col, width=4, n=3, spread=16):
    for k in range(n):
        ox = (k - (n - 1) / 2) * spread
        pts = []
        for i in range(25):
            t = i / 24
            pts.append((x + ox + math.sin(t * 6.3 + k) * spread * .35, y - t * h))
        c.line(pts, col, width * (1 - .0))


def sun_rays(c, cx, cy, r0, r1, n, col, width=None, start=180, end=360):
    for i in range(n + 1):
        a = math.radians(start + (end - start) * i / n)
        if width:
            c.line([(cx + r0 * math.cos(a), cy + r0 * math.sin(a)), (cx + r1 * math.cos(a), cy + r1 * math.sin(a))], col, width)
        else:
            da = math.radians((end - start) / n / 4)
            c.poly([(cx + r0 * math.cos(a - da), cy + r0 * math.sin(a - da)), (cx + r1 * math.cos(a), cy + r1 * math.sin(a)),
                    (cx + r0 * math.cos(a + da), cy + r0 * math.sin(a + da))], fill=col)


def barcode(c, x, y, w, h, seed=1, col=(20, 20, 20), bg=(250, 250, 250)):
    rng = random.Random(seed)
    c.rect(x - 4, y - 4, x + w + 4, y + h + 14, fill=bg)
    xx = x
    while xx < x + w:
        bw = rng.choice([1, 1, 2, 3]) * w / 95
        if rng.random() < .55: c.rect(xx, y, xx + bw, y + h, fill=col)
        xx += bw
    c.text(x + w / 2, y + h + 6, ''.join(str(rng.randint(0, 9)) for _ in range(13)), 'DejaVuSansMono', 8, col)


def paisley(c, cx, cy, s, ang, col, inner=None):
    a = math.radians(ang)
    pts = []
    for i in range(40):
        t = i / 39 * 2 * math.pi
        r = s * (.5 + .5 * math.cos(t / 2) ** 2) * (1 if t < math.pi else .8)
        x = r * math.cos(t) * .6; y = r * math.sin(t)
        pts.append((x, y))
    # a tail curling up
    rp = [(cx + x * math.cos(a) - y * math.sin(a), cy + x * math.sin(a) + y * math.cos(a)) for x, y in pts]
    c.poly(rp, fill=col)
    if inner: c.circle(cx, cy, s * .22, fill=inner)


# ------------------------------------------------------------------ round packs: no stretch
def kof(L):
    """how many times denser the wrap of a round pack is down than across: art must be drawn this much wider and squeezed"""
    x0, y0, x1, y1 = L['body']
    hm = {'tin': lambda: L['h'] - L['lid'] + .004, 'jar': lambda: L['bodyz'][1], 'cyl': lambda: L['h'], 'can': lambda: L['hm']}[L['kind']]()
    return ((y1 - y0) / hm) / (1024 / (2 * math.pi * L['r']))


def fit(c, R, k, draw, bg=(0, 0, 0, 0)):
    """draw(t, W, h) paints on a canvas k times wider than the rectangle R, which is then squeezed into R"""
    x0, y0, x1, y1 = [int(round(v)) for v in R]; w, h = x1 - x0, y1 - y0; W = max(8, int(round(w * k)))
    t = Canvas(W, h, (0, 0, 0)); t.im = Image.new('RGBA', (W * SS, h * SS), tuple(bg)); t.d = ImageDraw.Draw(t.im, 'RGBA')
    draw(t, W, h)
    paste_wrap(c, t.im.resize((w * SS, h * SS), Image.LANCZOS), x0, y0)


def paste_wrap(c, im, x, y):
    """paste an image (already at SS scale) at x, y; what sticks out past the seam comes back on the other side"""
    Wc = c.im.width
    for dx in (-Wc, 0, Wc):
        px = int(round(x * SS)) + dx
        a, b = max(0, px), min(Wc, px + im.width)
        if b > a: c.im.alpha_composite(im.crop((a - px, 0, b - px, im.height)), (a, int(round(y * SS))))

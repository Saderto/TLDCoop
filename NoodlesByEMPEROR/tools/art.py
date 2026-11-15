"""Drawing helpers for the package labels (PIL, drawn at 2x and scaled down)."""
import math, random, os
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

HERE = os.path.dirname(os.path.abspath(__file__))
FONTDIR = os.path.join(HERE, '..', 'fonts')
SYS = {
    'oswald': '/usr/share/fonts/truetype/roboto/unhinted/RobotoCondensed-Bold.ttf',
    'oswald4': '/usr/share/fonts/truetype/roboto/unhinted/RobotoCondensed-Regular.ttf',
    'oswald_l': os.path.join(FONTDIR, 'oswald-latin-700-normal.woff'),
    'playfair': os.path.join(FONTDIR, 'playfair-display-cyrillic-900-normal.woff'),
    'playfair_l': os.path.join(FONTDIR, 'playfair-display-latin-900-normal.woff'),
    'bebas': os.path.join(FONTDIR, 'bebas-neue-latin-400-normal.woff'),
    'alfa': os.path.join(FONTDIR, 'alfa-slab-one-latin-400-normal.woff'),
    'pacifico': os.path.join(FONTDIR, 'pacifico-latin-400-normal.woff'),
    'abril': os.path.join(FONTDIR, 'abril-fatface-latin-400-normal.woff'),
    'cinzel': os.path.join(FONTDIR, 'cinzel-latin-900-normal.woff'),
    'vibes': os.path.join(FONTDIR, 'great-vibes-latin-400-normal.woff'),
    'lobster': '/usr/share/fonts/opentype/lobster/lobster.otf',
    'comfortaa': '/usr/share/fonts/truetype/comfortaa/Comfortaa-Bold.ttf',
    'inter_black': '/usr/share/fonts/opentype/inter/Inter-Black.otf',
    'inter_bold': '/usr/share/fonts/opentype/inter/Inter-Bold.otf',
    'inter': '/usr/share/fonts/opentype/inter/Inter-Medium.otf',
    'ptsans_b': '/usr/share/fonts/truetype/paratype/PTS75F.ttf',
    'ptsans': '/usr/share/fonts/truetype/paratype/PTS55F.ttf',
    'ptnarrow_b': '/usr/share/fonts/truetype/paratype/PTN77F.ttf',
    'ptserif_b': '/usr/share/fonts/truetype/paratype/PTF75F.ttf',
    'roboto_black': '/usr/share/fonts/truetype/roboto/unhinted/RobotoTTF/Roboto-Black.ttf',
    'roboto_cond': '/usr/share/fonts/truetype/roboto/unhinted/RobotoCondensed-Bold.ttf',
    'ubuntu_b': '/usr/share/fonts/truetype/ubuntu/Ubuntu-B.ttf',
    'ptserif_bi': '/usr/share/fonts/truetype/paratype/PTF76F.ttf',
    'oldstd': '/usr/share/fonts/truetype/fonts-oldstandard/OldStandard-Bold.ttf',
    'cjk': '/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc',
    'yanone': '/usr/share/fonts/truetype/yanone-kaffeesatz/YanoneKaffeesatz-Bold.ttf',
    'dejavu_b': '/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf',
}
_cache = {}


def font(name, size):
    path = SYS.get(name, name)
    if not os.path.exists(path):
        # find something by family name
        import subprocess
        try:
            path = subprocess.check_output(['fc-match', '-f', '%{file}', name]).decode()
        except Exception:
            path = SYS['dejavu_b']
    k = (path, int(size))
    if k not in _cache:
        _cache[k] = ImageFont.truetype(path, int(size))
    return _cache[k]


def rgb(h):
    if isinstance(h, tuple): return h
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + ((int(h[6:8], 16),) if len(h) == 8 else ())


def mix(a, b, t):
    a, b = rgb(a), rgb(b)
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def vgrad(w, h, top, bot):
    im = Image.new('RGB', (w, h))
    d = ImageDraw.Draw(im)
    for y in range(h):
        d.line([(0, y), (w, y)], fill=mix(top, bot, y / max(1, h - 1)))
    return im


def radial(w, h, inner, outer, cx=.5, cy=.5, r=.7):
    im = Image.new('RGB', (w, h))
    px = im.load()
    for y in range(h):
        for x in range(0, w):
            d = math.hypot((x / w - cx) * w / h, y / h - cy) / r
            px[x, y] = mix(inner, outer, min(1, d))
    return im


def burst(im, cx, cy, n, col, r=None, alpha=60, rot=0):
    w, h = im.size
    r = r or max(w, h) * 1.5
    lay = Image.new('RGBA', im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    for k in range(n):
        a0 = rot + 2 * math.pi * k / n
        a1 = a0 + math.pi / n
        d.polygon([(cx, cy), (cx + r * math.cos(a0), cy + r * math.sin(a0)), (cx + r * math.cos(a1), cy + r * math.sin(a1))], fill=rgb(col) + (alpha,))
    im.paste(lay, (0, 0), lay)


def paste(im, lay, xy=(0, 0)):
    im.paste(lay, xy, lay)


def text_size(txt, f):
    b = f.getbbox(txt)
    return b[2] - b[0], b[3] - b[1], b


def fit_font(txt, name, maxw, maxh, start=400):
    s = start
    while s > 8:
        f = font(name, s)
        w, h, _ = text_size(txt, f)
        if w <= maxw and h <= maxh: return f
        s = int(s * .94)
    return font(name, 8)


def text(im, xy, txt, f, fill, anchor='mm', stroke=0, stroke_fill=None, shadow=None, shadow_off=(6, 6), blur=0, rotate=0, skew=0, alpha=255):
    """Text drawn on its own layer: outline, soft shadow, rotation and slant possible."""
    if isinstance(f, tuple): f = font(*f)
    w, h, b = text_size(txt, f)
    pad = stroke + 40 + abs(int(skew * h))
    lay = Image.new('RGBA', (w + 2 * pad, h + 2 * pad), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    o = (pad - b[0], pad - b[1])
    fillc = rgb(fill) + (alpha,)
    if shadow is not None:
        sh = Image.new('RGBA', lay.size, (0, 0, 0, 0))
        ImageDraw.Draw(sh).text((o[0] + shadow_off[0], o[1] + shadow_off[1]), txt, font=f, fill=rgb(shadow) + (150,), stroke_width=stroke, stroke_fill=rgb(shadow) + (150,))
        if blur: sh = sh.filter(ImageFilter.GaussianBlur(blur))
        lay = Image.alpha_composite(lay, sh)
        d = ImageDraw.Draw(lay)
    d.text(o, txt, font=f, fill=fillc, stroke_width=stroke, stroke_fill=(rgb(stroke_fill) + (255,)) if stroke else None)
    if skew:
        lay = lay.transform(lay.size, Image.AFFINE, (1, skew, -skew * lay.size[1] / 2, 0, 1, 0), Image.BICUBIC)
    if rotate:
        lay = lay.rotate(rotate, Image.BICUBIC, expand=True)
    x, y = xy
    lw, lh = lay.size
    if anchor == 'mm': x0, y0 = x - lw / 2, y - lh / 2
    elif anchor == 'lm': x0, y0 = x - pad, y - lh / 2
    elif anchor == 'rm': x0, y0 = x - lw + pad, y - lh / 2
    elif anchor == 'lt': x0, y0 = x - pad, y - pad
    else: x0, y0 = x - lw / 2, y - lh / 2
    im.paste(lay, (int(x0), int(y0)), lay)
    return w, h


def fit_text(im, box, txt, name, fill, **kw):
    x0, y0, x1, y1 = box
    f = fit_font(txt, name, x1 - x0, y1 - y0)
    text(im, ((x0 + x1) / 2, (y0 + y1) / 2), txt, f, fill, **kw)
    return f


def lines(im, x, y, rows, f, fill, gap=1.25, anchor='lt', maxw=None):
    if isinstance(f, tuple): f = font(*f)
    d = ImageDraw.Draw(im)
    for r in rows:
        if maxw:
            # wrap
            words, cur = r.split(' '), ''
            out = []
            for wd in words:
                t = (cur + ' ' + wd).strip()
                if text_size(t, f)[0] > maxw and cur:
                    out.append(cur); cur = wd
                else: cur = t
            out.append(cur)
        else: out = [r]
        for o in out:
            d.text((x, y), o, font=f, fill=rgb(fill))
            y += f.size * gap
    return y


def rrect(im, box, r, fill, outline=None, width=0, alpha=255):
    lay = Image.new('RGBA', im.size, (0, 0, 0, 0))
    ImageDraw.Draw(lay).rounded_rectangle(box, r, fill=rgb(fill) + (alpha,) if fill else None, outline=(rgb(outline) + (255,)) if outline else None, width=width)
    im.paste(lay, (0, 0), lay)


def ellipse(im, box, fill, outline=None, width=0, alpha=255):
    lay = Image.new('RGBA', im.size, (0, 0, 0, 0))
    ImageDraw.Draw(lay).ellipse(box, fill=rgb(fill) + (alpha,) if fill else None, outline=(rgb(outline) + (255,)) if outline else None, width=width)
    im.paste(lay, (0, 0), lay)


def poly(im, pts, fill, alpha=255, outline=None, width=0):
    lay = Image.new('RGBA', im.size, (0, 0, 0, 0))
    ImageDraw.Draw(lay).polygon(pts, fill=rgb(fill) + (alpha,) if fill else None, outline=(rgb(outline) + (255,)) if outline else None, width=width)
    im.paste(lay, (0, 0), lay)


def soft_shadow(im, box, r=30, blur=30, alpha=110, off=(0, 18), ellipse_shape=True):
    lay = Image.new('RGBA', im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    b = (box[0] + off[0], box[1] + off[1], box[2] + off[0], box[3] + off[1])
    if ellipse_shape: d.ellipse(b, fill=(0, 0, 0, alpha))
    else: d.rounded_rectangle(b, r, fill=(0, 0, 0, alpha))
    lay = lay.filter(ImageFilter.GaussianBlur(blur))
    im.paste(lay, (0, 0), lay)


def barcode(im, x, y, w, h, seed=1, digits='4 607001 771234'):
    rnd = random.Random(seed)
    rrect(im, (x - 14, y - 14, x + w + 14, y + h + 60), 6, '#ffffff')
    d = ImageDraw.Draw(im)
    cx = x
    while cx < x + w:
        bw = rnd.choice([2, 2, 4, 4, 6, 8])
        if rnd.random() < .55: d.rectangle((cx, y, cx + bw - 1, y + h), fill=(20, 20, 20))
        cx += bw
    f = font('ptsans', 34)
    d.text((x + w / 2, y + h + 30), digits, font=f, fill=(20, 20, 20), anchor='mm')


def noise_tex(im, amount=6, seed=3):
    """Faint print grain so flat colours do not look like plastic."""
    rnd = random.Random(seed)
    w, h = im.size
    n = Image.effect_noise((w, h), amount * 4).convert('L')
    n = n.point(lambda v: 128 + (v - 128) * amount // 20)
    base = im.convert('RGB')
    return ImageChops.add(ImageChops.subtract(base, Image.merge('RGB', [n] * 3), 1, 128), Image.new('RGB', (w, h), (0, 0, 0)))


def sheen(im, strength=40):
    """A soft diagonal highlight, as foil packs catch light."""
    w, h = im.size
    lay = Image.new('L', (w, h), 0)
    d = ImageDraw.Draw(lay)
    for k in range(3):
        x = w * (.15 + .3 * k)
        d.polygon([(x, 0), (x + w * .12, 0), (x - w * .25, h), (x - w * .37, h)], fill=strength // (k + 1))
    lay = lay.filter(ImageFilter.GaussianBlur(w / 12))
    white = Image.new('RGB', (w, h), (255, 255, 255))
    return Image.composite(white, im.convert('RGB'), lay)


# ------------------------------------------------------------------ food pictures

def noodle_bowl(im, cx, cy, r, broth='#d98a2b', bowl='#f4efe6', bowl_rim='#c62828', toppings=('egg', 'scallion', 'meat'), steam=True, chopsticks=True, seed=5, noodle_col='#f3d58a', pattern=None):
    rnd = random.Random(seed)
    soft_shadow(im, (cx - r * 1.1, cy + r * .35, cx + r * 1.1, cy + r * 1.05), blur=r // 6, alpha=120)
    # bowl body (lower half ellipse + trapezoid)
    body = [(cx - r * 1.05, cy), (cx + r * 1.05, cy)]
    for k in range(0, 41):
        a = math.pi * k / 40
        body.append((cx + r * 1.05 * math.cos(a), cy + r * .95 * math.sin(a) * .9))
    poly(im, body, bowl)
    # bowl shading
    lay = Image.new('RGBA', im.size, (0, 0, 0, 0))
    ImageDraw.Draw(lay).polygon(body, fill=(0, 0, 0, 60))
    mask = Image.new('L', im.size, 0)
    ImageDraw.Draw(mask).ellipse((cx - r * 2.4, cy - r * 1.6, cx + r * .75, cy + r * 1.0), fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(r / 3))      # a soft shade on the far side only: a crisp crescent read as a second bottom (user 11-14)
    body_m = lay.split()[3]
    lay.putalpha(ImageChops.multiply(ImageChops.subtract(body_m, mask).point(lambda v: int(v * .6)), body_m.point(lambda v: 255 if v else 0)))
    im.paste(lay, (0, 0), lay)
    if pattern == 'band':
        # a painted stripe round the wall, following its curve and ending at the bowl's outline (a plain ellipse here read as a second bottom: user 11-14)
        mb = Image.new('L', im.size, 0); ImageDraw.Draw(mb).polygon(body, fill=255)
        ring = Image.new('L', im.size, 0); dr = ImageDraw.Draw(ring)
        dr.ellipse((cx - r * 1.5, cy - r * .9, cx + r * 1.5, cy + r * .5), fill=255); dr.ellipse((cx - r * 1.5, cy - r * .9, cx + r * 1.5, cy + r * .36), fill=0)
        col = Image.new('RGBA', im.size, rgb(bowl_rim)[:3] + (255,)); col.putalpha(ImageChops.multiply(mb, ring))
        im.paste(col, (0, 0), col)
    # rim
    ellipse(im, (cx - r * 1.08, cy - r * .32, cx + r * 1.08, cy + r * .32), bowl_rim)
    ellipse(im, (cx - r, cy - r * .27, cx + r, cy + r * .27), broth)
    # noodles: wavy strands inside the broth ellipse
    lay = Image.new('RGBA', im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    nc = rgb(noodle_col); dark = mix(noodle_col, '#7a5a20', .35)
    for k in range(34):
        y0 = cy + (rnd.random() - .5) * r * .42
        x0 = cx - r * .95 + rnd.random() * r * .3
        pts = []
        amp = r * (.025 + rnd.random() * .03)
        ph = rnd.random() * 6
        L = r * (1.2 + rnd.random() * .7)
        for i in range(60):
            t = i / 59
            pts.append((x0 + L * t, y0 + amp * math.sin(ph + t * 18) + (t - .5) * r * .1 * (rnd.random() - .5)))
        wdt = max(2, int(r * .028))
        d.line(pts, fill=dark + (255,), width=wdt + 2, joint='curve')
        d.line(pts, fill=nc + (255,), width=wdt, joint='curve')
    m = Image.new('L', im.size, 0)
    ImageDraw.Draw(m).ellipse((cx - r * .97, cy - r * .25, cx + r * .97, cy + r * .25), fill=255)
    lay.putalpha(ImageChops.multiply(lay.split()[3], m))
    im.paste(lay, (0, 0), lay)
    # toppings
    for tp in toppings:
        if tp == 'egg':
            ex, ey = cx + r * .35, cy - r * .06
            ellipse(im, (ex - r * .2, ey - r * .1, ex + r * .2, ey + r * .1), '#fbfaf3')
            ellipse(im, (ex - r * .1, ey - r * .06, ex + r * .1, ey + r * .06), '#f6a821')
        if tp == 'scallion':
            for i in range(14):
                sx = cx + (rnd.random() - .5) * r * 1.4; sy = cy + (rnd.random() - .5) * r * .32
                ellipse(im, (sx - r * .035, sy - r * .02, sx + r * .035, sy + r * .02), '#4caf50', outline='#2e7d32', width=2)
        if tp == 'meat':
            for i in range(3):
                mx, my = cx - r * .45 + i * r * .17, cy - r * .02 + (i % 2) * r * .05
                poly(im, [(mx - r * .12, my - r * .05), (mx + r * .1, my - r * .08), (mx + r * .13, my + r * .05), (mx - r * .1, my + r * .07)], '#8d4a2b', outline='#5d2f1a', width=3)
        if tp == 'shrimp':
            for i in range(2):
                sx, sy = cx - r * .3 + i * r * .5, cy - r * .03
                d2 = ImageDraw.Draw(im)
                d2.arc((sx - r * .14, sy - r * .1, sx + r * .14, sy + r * .14), 200, 520, fill=rgb('#ff8a65'), width=int(r * .07))
        if tp == 'tofu':
            for i in range(2):
                tx, ty = cx - r * .2 + i * r * .42, cy - r * .02
                poly(im, [(tx - r * .18, ty - r * .08), (tx + r * .16, ty - r * .1), (tx + r * .19, ty + r * .07), (tx - r * .15, ty + r * .09)], '#e0a050', outline='#a86a20', width=3)
        if tp == 'chili':
            for i in range(9):
                sx = cx + (rnd.random() - .5) * r * 1.5; sy = cy + (rnd.random() - .5) * r * .34
                ellipse(im, (sx - r * .03, sy - r * .03, sx + r * .03, sy + r * .03), '#d32f2f', outline='#7f0000', width=2)
        if tp == 'mushroom':
            for i in range(3):
                mx, my = cx - r * .3 + i * r * .3, cy + r * .02
                ellipse(im, (mx - r * .09, my - r * .07, mx + r * .09, my + r * .03), '#8d6e63', outline='#4e342e', width=2)
        if tp == 'corn':
            for i in range(12):
                sx = cx + (rnd.random() - .5) * r * 1.2; sy = cy + (rnd.random() - .5) * r * .3
                ellipse(im, (sx - r * .025, sy - r * .02, sx + r * .025, sy + r * .02), '#ffd54f')
    if chopsticks:
        lay = Image.new('RGBA', im.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(lay)
        for k, off in enumerate((-r * .06, r * .06)):
            d.line([(cx + r * .2 + off, cy - r * .02), (cx + r * 1.15 + off * 3, cy - r * 1.05)], fill=(150, 98, 52, 255), width=int(r * .055))
        # a lifted strand
        pts = [(cx + r * .22 + r * .12 * math.sin(i / 3), cy - r * .02 - i * r * .03) for i in range(16)]
        d.line(pts, fill=rgb(noodle_col) + (255,), width=int(r * .03))
        im.paste(lay, (0, 0), lay)
    if steam:
        lay = Image.new('RGBA', im.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(lay)
        for k in range(3):
            x = cx - r * .45 + k * r * .4
            pts = [(x + r * .08 * math.sin(i / 2.5 + k), cy - r * .35 - i * r * .05) for i in range(14)]
            d.line(pts, fill=(255, 255, 255, 150), width=int(r * .05))
        lay = lay.filter(ImageFilter.GaussianBlur(r / 30))
        im.paste(lay, (0, 0), lay)


def chips(im, cx, cy, r, col='#f2c14e', edge='#c98a1b', n=9, seed=3, spice=None, ridges=False):
    rnd = random.Random(seed)
    soft_shadow(im, (cx - r, cy + r * .2, cx + r, cy + r * .7), blur=r // 5)
    for k in range(n):
        a = rnd.random() * 6.28
        dist = r * .55 * rnd.random() ** .7
        x, y = cx + dist * math.cos(a), cy + dist * math.sin(a) * .7
        rw, rh = r * (.32 + rnd.random() * .14), r * (.22 + rnd.random() * .1)
        rot = rnd.random() * 180
        lay = Image.new('RGBA', (int(rw * 2.4), int(rw * 2.4)), (0, 0, 0, 0))
        d = ImageDraw.Draw(lay)
        c = lay.size[0] / 2
        pts = []
        for i in range(40):
            t = 2 * math.pi * i / 40
            rr = 1 + .08 * math.sin(3 * t + k) + .05 * math.sin(5 * t + 2 * k)
            pts.append((c + rw * rr * math.cos(t), c + rh * rr * math.sin(t)))
        d.polygon(pts, fill=rgb(edge) + (255,))
        inner = [(c + (p[0] - c) * .9, c + (p[1] - c) * .9 - rh * .05) for p in pts]
        d.polygon(inner, fill=rgb(col) + (255,))
        if ridges:
            for i in range(-4, 5):
                d.line([(c + i * rw * .18 - rw * .1, c - rh * .8), (c + i * rw * .18 + rw * .1, c + rh * .8)], fill=rgb(edge) + (110,), width=3)
        hl = [(c - rw * .5, c - rh * .3), (c + rw * .1, c - rh * .55), (c + rw * .3, c - rh * .45), (c - rw * .3, c - rh * .15)]
        d.polygon(hl, fill=(255, 255, 255, 70))
        if spice:
            for i in range(int(rw / 3)):
                sx, sy = c + (rnd.random() - .5) * rw * 1.4, c + (rnd.random() - .5) * rh * 1.3
                d.ellipse((sx - 3, sy - 3, sx + 3, sy + 3), fill=rgb(spice) + (230,))
        lay = lay.rotate(rot, Image.BICUBIC)
        im.paste(lay, (int(x - c), int(y - c)), lay)


def puffs(im, cx, cy, r, col='#ff9f1c', n=18, seed=4):
    rnd = random.Random(seed)
    soft_shadow(im, (cx - r, cy + r * .2, cx + r, cy + r * .7), blur=r // 5)
    for k in range(n):
        a = rnd.random() * 6.28; dist = r * .55 * rnd.random() ** .6
        x, y = cx + dist * math.cos(a), cy + dist * math.sin(a) * .7
        L = r * (.4 + rnd.random() * .25); th = r * .16
        ang = rnd.random() * 3.14
        lay = Image.new('RGBA', im.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(lay)
        pts = []
        for i in range(20):
            t = i / 19 - .5
            pts.append((x + L * t * math.cos(ang) + th * .6 * math.sin(t * 9 + k) * math.sin(ang), y + L * t * math.sin(ang) + th * .5 * math.sin(t * 7 + k)))
        d.line(pts, fill=mix(col, '#a04000', .45) + (255,), width=int(th * 1.15), joint='curve')
        d.line(pts, fill=rgb(col) + (255,), width=int(th), joint='curve')
        for i in range(6):
            p = pts[rnd.randrange(len(pts))]
            d.ellipse((p[0] - th * .15, p[1] - th * .25, p[0] + th * .1, p[1]), fill=(255, 230, 160, 150))
        im.paste(lay, (0, 0), lay)


def croutons(im, cx, cy, r, col='#8b5a2b', top='#a8743d', n=18, seed=6, spice=None, long=False):
    rnd = random.Random(seed)
    soft_shadow(im, (cx - r, cy + r * .2, cx + r, cy + r * .7), blur=r // 5)
    items = sorted([(rnd.random() * 6.28, r * .55 * rnd.random() ** .6) for _ in range(n)], key=lambda q: math.sin(q[0]) * q[1])
    for a, dist in items:
        x, y = cx + dist * math.cos(a), cy + dist * math.sin(a) * .7
        s = r * (.2 + rnd.random() * .07)
        sx = s * (2.2 if long else 1)
        ang = rnd.random() * .6 - .3
        def R(px, py): return (x + px * math.cos(ang) - py * math.sin(ang), y + px * math.sin(ang) + py * math.cos(ang))
        poly(im, [R(-sx, -s * .4), R(sx, -s * .4), R(sx, s), R(-sx, s)], col)
        poly(im, [R(-sx, -s * .4), R(sx, -s * .4), R(sx * .8, -s), R(-sx * 1.2, -s)], top)
        if spice:
            for i in range(5):
                p = R((rnd.random() - .5) * sx * 1.6, (rnd.random() - .5) * s * 1.2)
                ellipse(im, (p[0] - 3, p[1] - 3, p[0] + 3, p[1] + 3), spice)


def seeds(im, cx, cy, r, n=60, seed=7):
    rnd = random.Random(seed)
    soft_shadow(im, (cx - r, cy + r * .2, cx + r, cy + r * .7), blur=r // 5)
    for k in range(n):
        a = rnd.random() * 6.28; dist = r * .62 * rnd.random() ** .6
        x, y = cx + dist * math.cos(a), cy + dist * math.sin(a) * .65
        L, Wd = r * .14, r * .1
        ang = rnd.random() * 360
        lay = Image.new('RGBA', (int(L * 3), int(L * 3)), (0, 0, 0, 0))
        d = ImageDraw.Draw(lay)
        c = lay.size[0] / 2
        pts = [(c + L * (i / 19 - .5) * 2, c + Wd * math.sin(math.pi * i / 19) ** .7 * (1 - .4 * i / 19)) for i in range(20)]
        pts += [(c + L * (i / 19 - .5) * 2, c - Wd * math.sin(math.pi * i / 19) ** .7 * (1 - .4 * i / 19)) for i in range(19, -1, -1)]
        d.polygon(pts, fill=(28, 26, 26, 255))
        for s_ in (-.45, 0, .45):
            d.line([(c - L * .9, c + Wd * s_), (c + L * .6, c + Wd * s_ * .7)], fill=(210, 205, 195, 200), width=3)
        d.line([(c - L * .5, c - Wd * .5), (c + L * .3, c - Wd * .6)], fill=(255, 255, 255, 60), width=4)
        lay = lay.rotate(ang, Image.BICUBIC)
        im.paste(lay, (int(x - c), int(y - c)), lay)


def peanuts(im, cx, cy, r, n=24, seed=8):
    rnd = random.Random(seed)
    soft_shadow(im, (cx - r, cy + r * .2, cx + r, cy + r * .7), blur=r // 5)
    for k in range(n):
        a = rnd.random() * 6.28; dist = r * .58 * rnd.random() ** .6
        x, y = cx + dist * math.cos(a), cy + dist * math.sin(a) * .65
        s = r * .17
        ellipse(im, (x - s, y - s * .8, x + s, y + s * .8), '#c8894a', outline='#8d5524', width=3)
        ellipse(im, (x - s * .55, y - s * .55, x - s * .05, y - s * .2), '#e3b27a', alpha=160)
        d = ImageDraw.Draw(im)
        d.line([(x, y - s * .7), (x + s * .1, y + s * .7)], fill=rgb('#8d5524'), width=3)
        for i in range(4):
            px, py = x + (rnd.random() - .5) * s, y + (rnd.random() - .5) * s
            ellipse(im, (px - 3, py - 3, px + 3, py + 3), '#ffffff', alpha=220)


def squid(im, cx, cy, r, seed=9):
    rnd = random.Random(seed)
    soft_shadow(im, (cx - r, cy + r * .2, cx + r, cy + r * .7), blur=r // 5)
    for k in range(9):
        a = rnd.random() * 6.28; dist = r * .5 * rnd.random() ** .6
        x, y = cx + dist * math.cos(a), cy + dist * math.sin(a) * .65
        ang = rnd.random() * 3.14
        lay = Image.new('RGBA', im.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(lay)
        L = r * (.4 + rnd.random() * .25)
        pts = [(x + L * (i / 15 - .5) * math.cos(ang) + r * .03 * math.sin(i), y + L * (i / 15 - .5) * math.sin(ang) + r * .03 * math.cos(i * 1.3)) for i in range(16)]
        d.line(pts, fill=(196, 140, 92, 255), width=int(r * .1), joint='curve')
        d.line(pts, fill=(238, 205, 160, 255), width=int(r * .065), joint='curve')
        im.paste(lay, (0, 0), lay)
    # a small whole squid drawn on top
    sx, sy = cx + r * .1, cy - r * .1
    poly(im, [(sx - r * .5, sy), (sx + r * .1, sy - r * .14), (sx + r * .15, sy + r * .14)], '#f0d4ae', outline='#b07a4c', width=3)
    poly(im, [(sx - r * .62, sy), (sx - r * .45, sy - r * .1), (sx - r * .45, sy + r * .1)], '#e8c49a', outline='#b07a4c', width=2)
    d = ImageDraw.Draw(im)
    for i in range(6):
        d.line([(sx + r * .12, sy - r * .1 + i * r * .04), (sx + r * .45 + i * r * .02, sy - r * .14 + i * r * .06 + r * .05 * math.sin(i))], fill=rgb('#d7a978'), width=int(r * .025))


def popcorn(im, cx, cy, r, n=30, seed=10, col='#fff6dc'):
    rnd = random.Random(seed)
    soft_shadow(im, (cx - r, cy + r * .2, cx + r, cy + r * .7), blur=r // 5)
    for k in range(n):
        a = rnd.random() * 6.28; dist = r * .6 * rnd.random() ** .6
        x, y = cx + dist * math.cos(a), cy + dist * math.sin(a) * .65
        s = r * (.08 + rnd.random() * .05)
        for i in range(5):
            bx, by = x + (rnd.random() - .5) * s * 1.3, y + (rnd.random() - .5) * s
            ellipse(im, (bx - s * .6, by - s * .55, bx + s * .6, by + s * .55), mix(col, '#e0c890', rnd.random() * .5), outline=mix(col, '#b89a60', .6), width=2)
        ellipse(im, (x - s * .2, y - s * .1, x + s * .2, y + s * .2), '#e7a63d')


def corn_sticks(im, cx, cy, r, n=20, seed=11):
    rnd = random.Random(seed)
    soft_shadow(im, (cx - r, cy + r * .2, cx + r, cy + r * .7), blur=r // 5)
    for k in range(n):
        x = cx + (rnd.random() - .5) * r * 1.2; y = cy + (rnd.random() - .5) * r * .6
        ang = rnd.random() * 3.14
        L, th = r * (.55 + rnd.random() * .25), r * .14
        lay = Image.new('RGBA', im.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(lay)
        p0 = (x - L / 2 * math.cos(ang), y - L / 2 * math.sin(ang)); p1 = (x + L / 2 * math.cos(ang), y + L / 2 * math.sin(ang))
        d.line([p0, p1], fill=(214, 168, 70, 255), width=int(th))
        d.line([p0, p1], fill=(250, 226, 150, 255), width=int(th * .6))
        for i in range(10):
            t = rnd.random()
            px, py = p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t
            d.ellipse((px - 3, py - 3, px + 3, py + 3), fill=(255, 255, 255, 230))
        im.paste(lay, (0, 0), lay)


def flames(im, x0, y1, w, h, cols=('#ffeb3b', '#ff9800', '#e53935'), seed=12, n=9, alpha=255):
    rnd = random.Random(seed)
    for ci, c in enumerate(cols[::-1]):
        lay = Image.new('RGBA', im.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(lay)
        sc = 1 - ci * .28
        pts = [(x0, y1)]
        for k in range(n):
            xa = x0 + w * (k + .5) / n
            hh = h * sc * (.6 + .4 * rnd.random())
            pts += [(xa - w / n * .35, y1 - hh * .4), (xa + w / n * .1 * (rnd.random() - .5), y1 - hh), (xa + w / n * .35, y1 - hh * .45)]
        pts += [(x0 + w, y1)]
        d.polygon(pts, fill=rgb(c) + (alpha,))
        im.paste(lay, (0, 0), lay)


def star(im, cx, cy, r, fill, n=5, inner=.45, rot=-90, outline=None, width=0):
    pts = []
    for k in range(2 * n):
        rr = r if k % 2 == 0 else r * inner
        a = math.radians(rot + 180 * k / n)
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    poly(im, pts, fill, outline=outline, width=width)


def badge(im, cx, cy, r, fill, ring='#ffffff', teeth=20):
    star(im, cx, cy, r, fill, n=teeth, inner=.88)
    ellipse(im, (cx - r * .78, cy - r * .78, cx + r * .78, cy + r * .78), None, outline=ring, width=max(3, int(r * .05)))


def sunflower(im, cx, cy, r):
    for k in range(18):
        a = 2 * math.pi * k / 18
        lay = Image.new('RGBA', im.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(lay)
        px, py = cx + r * .72 * math.cos(a), cy + r * .72 * math.sin(a)
        pts = [(cx + r * .35 * math.cos(a - .18), cy + r * .35 * math.sin(a - .18)), (px, py), (cx + r * .35 * math.cos(a + .18), cy + r * .35 * math.sin(a + .18))]
        d.polygon(pts, fill=(255, 196, 0, 255), outline=(200, 120, 0, 255))
        im.paste(lay, (0, 0), lay)
    ellipse(im, (cx - r * .42, cy - r * .42, cx + r * .42, cy + r * .42), '#4e342e')
    rnd = random.Random(2)
    for k in range(80):
        a = rnd.random() * 6.28; d_ = r * .38 * rnd.random() ** .5
        x, y = cx + d_ * math.cos(a), cy + d_ * math.sin(a)
        ellipse(im, (x - 3, y - 3, x + 3, y + 3), '#2b1b16')


def anchor_icon(im, cx, cy, r, col):
    d = ImageDraw.Draw(im)
    c = rgb(col)
    w = max(4, int(r * .12))
    d.ellipse((cx - r * .15, cy - r, cx + r * .15, cy - r * .7), outline=c, width=w)
    d.line([(cx, cy - r * .7), (cx, cy + r * .8)], fill=c, width=w)
    d.line([(cx - r * .4, cy - r * .45), (cx + r * .4, cy - r * .45)], fill=c, width=w)
    d.arc((cx - r * .8, cy - r * .1, cx + r * .8, cy + r * .9), 20, 160, fill=c, width=w)
    poly(im, [(cx - r * .85, cy + r * .25), (cx - r * .65, cy + r * .35), (cx - r * .82, cy + r * .5)], col)
    poly(im, [(cx + r * .85, cy + r * .25), (cx + r * .65, cy + r * .35), (cx + r * .82, cy + r * .5)], col)


def hedgehog(im, cx, cy, r):
    rnd = random.Random(4)
    for k in range(26):
        a = math.pi * (1.05 + .9 * k / 25)
        x1, y1 = cx + r * 1.0 * math.cos(a), cy + r * .85 * math.sin(a)
        poly(im, [(cx + r * .55 * math.cos(a - .1), cy + r * .45 * math.sin(a - .1)), (x1, y1), (cx + r * .55 * math.cos(a + .1), cy + r * .45 * math.sin(a + .1))], '#5d4037')
    ellipse(im, (cx - r * .75, cy - r * .55, cx + r * .75, cy + r * .45), '#6d4c41')
    ellipse(im, (cx + r * .25, cy - r * .2, cx + r * 1.0, cy + r * .4), '#d7ccc8')
    ellipse(im, (cx + r * .9, cy + r * .02, cx + r * 1.06, cy + r * .16), '#212121')
    ellipse(im, (cx + r * .5, cy - r * .08, cx + r * .62, cy + r * .06), '#212121')
    ellipse(im, (cx + r * .53, cy - r * .06, cx + r * .57, cy - r * .02), '#ffffff')
    ellipse(im, (cx - r * .4, cy + r * .3, cx - r * .15, cy + r * .5), '#4e342e')
    ellipse(im, (cx + r * .1, cy + r * .3, cx + r * .35, cy + r * .5), '#4e342e')


def chicken(im, cx, cy, r, body='#ffffff', comb='#e53935', angry=False):
    ellipse(im, (cx - r * .8, cy - r * .3, cx + r * .6, cy + r * .8), body, outline='#333333', width=4)
    ellipse(im, (cx - r * .1, cy - r * .9, cx + r * .7, cy - r * .1), body, outline='#333333', width=4)
    for k in range(3):
        ellipse(im, (cx + r * (.05 + k * .18), cy - r * 1.12, cx + r * (.25 + k * .18), cy - r * .82), comb)
    poly(im, [(cx + r * .68, cy - r * .55), (cx + r * .95, cy - r * .45), (cx + r * .68, cy - r * .38)], '#ffb300', outline='#8d5b00', width=2)
    ellipse(im, (cx + r * .55, cy - r * .35, cx + r * .7, cy - r * .12), comb)
    ellipse(im, (cx + r * .35, cy - r * .62, cx + r * .48, cy - r * .48), '#111111')
    if angry:
        d = ImageDraw.Draw(im)
        d.line([(cx + r * .28, cy - r * .74), (cx + r * .55, cy - r * .62)], fill=(17, 17, 17), width=max(4, int(r * .07)))
    poly(im, [(cx - r * .75, cy + r * .05), (cx - r * 1.05, cy - r * .3), (cx - r * .95, cy + r * .2), (cx - r * 1.1, cy + r * .1), (cx - r * .8, cy + r * .4)], body, outline='#333333', width=3)
    ellipse(im, (cx - r * .4, cy + r * .1, cx + r * .2, cy + r * .5), mix(body, '#9e9e9e', .25))


def fox(im, cx, cy, r):
    poly(im, [(cx - r, cy - r * .2), (cx - r * .55, cy - r * 1.1), (cx - r * .2, cy - r * .5), (cx + r * .2, cy - r * .5), (cx + r * .55, cy - r * 1.1), (cx + r, cy - r * .2), (cx, cy + r * .9)], '#f57c00', outline='#4e2600', width=5)
    poly(im, [(cx - r * .7, cy - r * .1), (cx, cy + r * .9), (cx - r * .15, cy + r * .1)], '#ffffff')
    poly(im, [(cx + r * .7, cy - r * .1), (cx, cy + r * .9), (cx + r * .15, cy + r * .1)], '#ffffff')
    ellipse(im, (cx - r * .1, cy + r * .7, cx + r * .1, cy + r * .9), '#212121')
    for s in (-1, 1):
        d = ImageDraw.Draw(im)
        d.arc((cx + s * r * .38 - r * .13, cy - r * .2, cx + s * r * .38 + r * .13, cy), 200, 340, fill=(33, 33, 33), width=max(4, int(r * .06)))


def cheetah_paw(im, cx, cy, r, col):
    ellipse(im, (cx - r * .5, cy - r * .1, cx + r * .5, cy + r * .6), col)
    for k, (dx, dy) in enumerate([(-.55, -.35), (-.2, -.65), (.2, -.65), (.55, -.35)]):
        ellipse(im, (cx + r * dx - r * .17, cy + r * dy - r * .2, cx + r * dx + r * .17, cy + r * dy + r * .2), col)


def mustache_face(im, cx, cy, r):
    ellipse(im, (cx - r, cy - r, cx + r, cy + r), '#ffe0b2', outline='#4e342e', width=5)
    ellipse(im, (cx - r * .45, cy - r * .35, cx - r * .2, cy - r * .1), '#3e2723')
    ellipse(im, (cx + r * .2, cy - r * .35, cx + r * .45, cy - r * .1), '#3e2723')
    for s in (-1, 1):
        poly(im, [(cx, cy + r * .1), (cx + s * r * .3, cy), (cx + s * r * .75, cy + r * .2), (cx + s * r * .65, cy + r * .4), (cx + s * r * .3, cy + r * .3), (cx, cy + r * .35)], '#3e2723')
    poly(im, [(cx - r * .55, cy - r * .55), (cx + r * .55, cy - r * .55), (cx + r * .3, cy - r * .85), (cx - r * .3, cy - r * .85)], '#3e2723')
    ImageDraw.Draw(im).arc((cx - r * .35, cy + r * .3, cx + r * .35, cy + r * .7), 20, 160, fill=rgb('#b71c1c'), width=6)


def crimp_band(im, y0, y1, col, line='#00000040'):
    """The heat-sealed end of a bag: ridges across."""
    d = ImageDraw.Draw(im, 'RGBA')
    d.rectangle((0, y0, im.size[0], y1), fill=rgb(col))
    for y in range(int(y0), int(y1), 10):
        d.line([(0, y), (im.size[0], y)], fill=(0, 0, 0, 40), width=3)
        d.line([(0, y + 4), (im.size[0], y + 4)], fill=(255, 255, 255, 30), width=2)

"""Matchbox labels (11-14). Flat print look: 2-4 inks on paper, slight misregistration, paper grain.
label(l) -> RGB image of the top face at the kind's aspect (portrait for matchbooks); back(l), striker(l), ends(l) for the other faces."""
import io, json, os, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops
from catalog import KINDS

HERE = os.path.dirname(os.path.abspath(__file__))
FONTS = os.path.join(HERE, '..', 'fonts')
_ICONS = None
_CMAP = {}


# ---------------------------------------------------------------- text with latin/cyrillic subsets
FAM = {
    'oswald': ['oswald-latin-700-normal.woff', 'oswald-cyrillic-700-normal.woff'],
    'oswald4': ['oswald-latin-400-normal.woff', 'oswald-cyrillic-400-normal.woff'],
    'playfair': ['playfair-display-latin-900-normal.woff', 'playfair-display-cyrillic-900-normal.woff'],
    'playfair7': ['playfair-display-latin-700-normal.woff', 'playfair-display-cyrillic-700-normal.woff'],
    'bebas': ['bebas-neue-latin-400-normal.woff', 'oswald-cyrillic-700-normal.woff'],
    'slab': ['alfa-slab-one-latin-400-normal.woff', 'oswald-cyrillic-700-normal.woff'],
    'abril': ['abril-fatface-latin-400-normal.woff', 'playfair-display-cyrillic-900-normal.woff'],
    'cinzel': ['cinzel-latin-900-normal.woff', 'playfair-display-cyrillic-900-normal.woff'],
    'cinzel4': ['cinzel-latin-700-normal.woff', 'playfair-display-cyrillic-700-normal.woff'],
    'pacifico': ['pacifico-latin-400-normal.woff', 'playfair-display-cyrillic-700-normal.woff'],
    'vibes': ['great-vibes-latin-400-normal.woff', 'playfair-display-cyrillic-700-normal.woff'],
}


def _cmap(f):
    if f not in _CMAP:
        from fontTools.ttLib import TTFont
        _CMAP[f] = set(TTFont(os.path.join(FONTS, f)).getBestCmap())
    return _CMAP[f]


def runs(text, fam):
    files = FAM[fam]
    out = []
    for ch in text:
        pick = files[0]
        for f in files:
            if ord(ch) in _cmap(f) or ch == ' ':
                pick = f
                break
        if out and out[-1][0] == pick:
            out[-1][1] += ch
        else:
            out.append([pick, ch])
    return out


def text_size(text, fam, size):
    w = 0
    asc = 0
    for f, t in runs(text, fam):
        ft = ImageFont.truetype(os.path.join(FONTS, f), size)
        w += ft.getlength(t)
        asc = max(asc, ft.getmetrics()[0])
    return w, asc


def fit(text, fam, size, maxw):
    while size > 8 and text_size(text, fam, size)[0] > maxw:
        size = int(size * .94)
    return size


def put(mask, xy, text, fam, size, anchor='mm', maxw=None, fill=255, track=0):
    """draw text into an L mask; anchor 'mm' centre, 'lm' left-middle, 'rm' right-middle"""
    if maxw:
        size = fit(text, fam, size, maxw - track * len(text))
    d = ImageDraw.Draw(mask)
    w, asc = text_size(text, fam, size)
    w += track * max(0, len(text) - 1)
    x, y = xy
    if anchor[0] == 'm':
        x -= w / 2
    elif anchor[0] == 'r':
        x -= w
    for f, t in runs(text, fam):
        ft = ImageFont.truetype(os.path.join(FONTS, f), size)
        for ch in (t if track else [t]):
            d.text((x, y), ch, font=ft, fill=fill, anchor='ls' if False else 'lm')
            x += ft.getlength(ch) + track
    return size


def put_lines(mask, cx, cy, lines, fam, size, maxw, gap=1.1, fill=255):
    size = min(fit(l, fam, size, maxw) for l in lines)
    h = size * gap
    y0 = cy - h * (len(lines) - 1) / 2
    for i, l in enumerate(lines):
        put(mask, (cx, y0 + i * h), l, fam, size, fill=fill)
    return size


def wrap(text, n):
    words = text.split()
    lines, cur = [], ''
    for w in words:
        if cur and len(cur) + 1 + len(w) > n:
            lines.append(cur)
            cur = w
        else:
            cur = (cur + ' ' + w).strip()
    if cur:
        lines.append(cur)
    return lines


# ---------------------------------------------------------------- icons
def icon(name, px):
    """white figure on black, L mask px x px"""
    global _ICONS
    import cairosvg
    if _ICONS is None:
        _ICONS = json.load(open(os.path.join(HERE, 'game-icons.json')))
    body = _ICONS['icons'][name]['body'].replace('currentColor', '#ffffff')
    svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="%d" height="%d">%s</svg>' % (px, px, body)
    im = Image.open(io.BytesIO(cairosvg.svg2png(bytestring=svg.encode()))).convert('RGBA')
    return im.split()[3]


def paste_mask(mask, m, cx, cy):
    mask.paste(255, (int(cx - m.width / 2), int(cy - m.height / 2)), m)


# ---------------------------------------------------------------- print
def hexc(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


class Print:
    """a sheet printed ink by ink: paper colour, then each ink through its mask, a pixel or two off register"""

    def __init__(self, w, h, paper, seed):
        self.w, self.h = w, h
        self.rnd = random.Random(seed)
        self.img = np.zeros((h, w, 3), np.float32)
        self.img[:] = np.array(hexc(paper), np.float32)

    def mask(self):
        return Image.new('L', (self.w, self.h), 0)

    def ink(self, mask, colour, alpha=1.0, offset=True):
        if offset:
            dx, dy = self.rnd.randint(-2, 2), self.rnd.randint(-2, 2)
            mask = ImageChops.offset(mask, dx, dy)
        m = np.array(mask, np.float32)[..., None] / 255 * alpha
        c = np.array(hexc(colour), np.float32)
        self.img = self.img * (1 - m) + c * m

    def finish(self, out_w, out_h):
        h, w = self.h, self.w
        rs = np.random.RandomState(self.rnd.randint(0, 1 << 30))
        grain = rs.normal(0, 1, (h // 4 + 1, w // 4 + 1)).astype(np.float32)
        g = np.array(Image.fromarray(grain).resize((w, h), Image.BILINEAR))
        fine = rs.normal(0, 1, (h, w)).astype(np.float32)
        img = self.img * (1 + .035 * g[..., None] + .02 * fine[..., None])
        # ink wear: tiny speckles of paper showing through
        img = np.clip(img, 0, 255).astype(np.uint8)
        return Image.fromarray(img, 'RGB').resize((out_w, out_h), Image.LANCZOS)


def rect(m, box, fill=255, r=0, width=0):
    d = ImageDraw.Draw(m)
    if r:
        d.rounded_rectangle(box, r, fill=fill if not width else None, outline=fill if width else None, width=width)
    else:
        d.rectangle(box, fill=fill if not width else None, outline=fill if width else None, width=width)


def ellipse(m, box, fill=255, width=0):
    d = ImageDraw.Draw(m)
    d.ellipse(box, fill=fill if not width else None, outline=fill if width else None, width=width)


def poly(m, pts, fill=255):
    ImageDraw.Draw(m).polygon(pts, fill=fill)


def star(cx, cy, r1, r2, n=5, rot=-90):
    pts = []
    for i in range(n * 2):
        r = r1 if i % 2 == 0 else r2
        a = math.radians(rot + i * 180 / n)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


# ---------------------------------------------------------------- palettes per series (cycled per label)
PAL = {
    'soviet': [('#efe3c4', '#b3261e', '#1d1d1b'), ('#ece0c0', '#1f5c8f', '#1d1d1b'), ('#eee2c6', '#2f6b3a', '#1d1d1b'),
               ('#efe4c8', '#c9861a', '#2a1d14'), ('#ebdfc2', '#6b2d5c', '#1d1d1b')],
    'poster': [('#f1e6c9', '#d0281f', '#141414'), ('#f3e9cf', '#e0611a', '#141414'), ('#efe4c6', '#c21d3b', '#141414')],
    'coast': [('#16314f', '#f0e6cc', '#8fb6d1'), ('#123b45', '#f2e8cf', '#7fc0c0'), ('#1f2c5c', '#f1e4c8', '#a3b4dc')],
    'fish': [('#2f7d78', '#f3ead2', '#123c3a'), ('#3a6f9a', '#f3ead2', '#14304a'), ('#5b8a3c', '#f3ead2', '#22381a')],
    'sport': [('#f4efe2', '#c4232d', '#1c4f9c'), ('#f4efe2', '#1c4f9c', '#c4232d'), ('#f4efe2', '#178a5b', '#1d1d1b')],
    'cosmos': [('#151a3d', '#f2c74a', '#e85a2b'), ('#1d1238', '#f6d36b', '#5fd0e6'), ('#0f2a3a', '#f0c24a', '#e04646')],
    'brand': [('#f0e2bd', '#9e1b1b', '#1a2e5a'), ('#d8e4dc', '#1d4d3a', '#b0321f'), ('#f3d9a4', '#1b2a4a', '#c43a1b'),
              ('#e7d3d3', '#6a1430', '#1d1d1b'), ('#cfe0ec', '#173b6c', '#d9a21b')],
    'japan': [('#c8331f', '#f6eedc', '#1a1a1a'), ('#1f2f5c', '#f3e9d6', '#d9a128'), ('#141414', '#f2e6cc', '#c8331f'),
              ('#d9a128', '#1a1a1a', '#f6eedc'), ('#2f6b4a', '#f6eedc', '#c8331f')],
    'hunter': [('#b8a77a', '#e8641b', '#1e1e18'), ('#8f9a6a', '#e8641b', '#1e1e18'), ('#c4b08a', '#d23d1c', '#232014')],
    'hearth': [('#1f3b2e', '#d9b45a', '#efe3c2'), ('#4a1820', '#d9b45a', '#efe3c2'), ('#1b2a44', '#d9b45a', '#efe3c2')],
    'souvenir': [('#f4ecd8', '#2d5a7b', '#c0392b'), ('#f4ecd8', '#3d6b3a', '#c78a1a'), ('#f4ecd8', '#5b3a7a', '#d0552b')],
    'diner': [('#f5ead2', '#e04a3a', '#1f6f8b'), ('#f5ead2', '#1f6f8b', '#e8a21b'), ('#1d2b3a', '#f2c14e', '#e86a5c'),
              ('#f5ead2', '#2a9d6a', '#d6336c')],
}


def pal(l):
    p = PAL[l['layout']]
    return p[(l['n'] - 1) % len(p)]


def label_size(l, scale=2):
    L, W, H = KINDS[l['kind']][4]
    if l['kind'] == 'Book':
        return int(500 * scale), int(500 * scale * L / W)   # portrait: the cover stands 50 high, 38 wide
    return int(500 * scale * L / W), int(500 * scale)


# ---------------------------------------------------------------- layouts
def L_soviet(l, P, w, h):
    paper, c1, c2 = pal(l)
    m = P.mask(); rect(m, (0, 0, w, h)); rect(m, (40, 40, w - 40, h - 40), fill=0, r=30)
    P.ink(m, c1, offset=False)
    m = P.mask(); rect(m, (58, 58, w - 58, h - 58), r=22, width=6); rect(m, (72, 72, w - 72, h - 72), r=18, width=2)
    for x, y in [(110, 110), (w - 110, 110), (110, h - 110), (w - 110, h - 110)]:
        poly(m, star(x, y, 22, 9))
    P.ink(m, c1)
    m = P.mask(); paste_mask(m, icon(l['icon'], int(h * .52)), w / 2, h * .47); P.ink(m, c2)
    m = P.mask(); put(m, (w / 2, h * .135), l['sru'].upper(), 'oswald4', int(h * .06), maxw=w * .7, track=3); P.ink(m, c2)
    m = P.mask(); rect(m, (w * .18, h * .74, w * .82, h * .88)); P.ink(m, c1)
    m = P.mask(); put(m, (w / 2, h * .81), l['ru'].upper(), 'oswald', int(h * .11), maxw=w * .6); P.ink(m, paper)


def L_poster(l, P, w, h):
    paper, c1, c2 = pal(l)
    m = P.mask(); poly(m, [(0, 0), (w * .62, 0), (w * .38, h), (0, h)]); P.ink(m, c1, offset=False)
    m = P.mask(); paste_mask(m, icon(l['icon'], int(h * .66)), w * .72, h * .46); P.ink(m, c2)
    m = P.mask()
    put_lines(m, w * .27, h * .44, wrap(l['ru'].upper(), 11), 'oswald', int(h * .13), w * .44, gap=1.08)
    P.ink(m, paper)
    m = P.mask(); rect(m, (0, h * .84, w, h)); P.ink(m, c2, offset=False)
    m = P.mask(); put(m, (w / 2, h * .92), 'БЕРЕГИ ЛЕС ОТ ПОЖАРА!', 'oswald', int(h * .08), maxw=w * .9, track=4); P.ink(m, paper)
    m = P.mask(); rect(m, (16, 16, w - 16, h - 16), width=8); P.ink(m, c2)


def L_coast(l, P, w, h):
    bg, c1, c2 = pal(l)
    m = P.mask(); ellipse(m, (w / 2 - h * .34, h * .14, w / 2 + h * .34, h * .82)); P.ink(m, c1)
    m = P.mask(); paste_mask(m, icon(l['icon'], int(h * .5)), w / 2, h * .48); P.ink(m, bg)
    m = P.mask()
    for k in range(4):
        y0 = h * (.78 + k * .05)
        pts = [(x, y0 + 10 * math.sin(x / 38 + k)) for x in range(0, w + 8, 8)]
        ImageDraw.Draw(m).line(pts, fill=255, width=7)
    P.ink(m, c2)
    m = P.mask(); put(m, (w / 2, h * .08), l['sru'].upper(), 'playfair7', int(h * .045), maxw=w * .7, track=2); P.ink(m, c2)
    m = P.mask(); put(m, (w * .14, h * .48), '№ %d' % l['n'], 'playfair', int(h * .085), maxw=w * .16)
    put(m, (w * .86, h * .48), l['ru'].upper(), 'playfair', int(h * .07), maxw=w * .2); P.ink(m, c1)
    m = P.mask(); rect(m, (22, 22, w - 22, h - 22), width=5); P.ink(m, c1)


def L_fish(l, P, w, h):
    bg, c1, c2 = pal(l)
    m = P.mask(); ellipse(m, (w * .14, h * .1, w * .86, h * .74)); P.ink(m, c1)
    m = P.mask(); ellipse(m, (w * .14 + 14, h * .1 + 14, w * .86 - 14, h * .74 - 14), width=5); P.ink(m, c2)
    m = P.mask(); paste_mask(m, icon(l['icon'], int(h * .5)), w / 2, h * .42); P.ink(m, c2)
    m = P.mask(); rect(m, (0, h * .78, w, h)); P.ink(m, c1, offset=False)
    m = P.mask(); put(m, (w / 2, h * .865), l['ru'].upper(), 'oswald', int(h * .1), maxw=w * .8)
    P.ink(m, c2)
    m = P.mask()
    for x in range(30, w, 60):
        ellipse(m, (x - 6, h * .76 - 6, x + 6, h * .76 + 6))
    P.ink(m, c1)
    m = P.mask(); put(m, (w * .07, h * .44), '%d' % l['n'], 'oswald', int(h * .12)); put(m, (w * .93, h * .44), '%d' % l['total'], 'oswald', int(h * .12)); P.ink(m, c1)


def L_sport(l, P, w, h):
    bg, c1, c2 = pal(l)
    m = P.mask()
    for k in range(3):
        x = w * (.04 + k * .07)
        poly(m, [(x, h), (x + w * .045, h), (x + w * .045 + h * .35, 0), (x + h * .35, 0)])
    P.ink(m, c1)
    m = P.mask(); paste_mask(m, icon(l['icon'], int(h * .58)), w * .6, h * .42); P.ink(m, c2)
    m = P.mask(); put(m, (w * .6, h * .83), l['ru'].upper(), 'oswald', int(h * .1), maxw=w * .7); P.ink(m, c1)
    m = P.mask(); put(m, (w * .6, h * .08), 'ЗИМНИЕ ИГРЫ', 'oswald4', int(h * .055), maxw=w * .5, track=6)
    for i in range(5):
        poly(m, star(w * (.42 + i * .09), h * .93, 14, 6))
    P.ink(m, c2)


def L_cosmos(l, P, w, h):
    bg, c1, c2 = pal(l)
    m = P.mask()
    cx, cy = w * .5, h * .5
    for k in range(24):
        a = math.radians(k * 15)
        a2 = math.radians(k * 15 + 5)
        R = w
        poly(m, [(cx, cy), (cx + R * math.cos(a), cy + R * math.sin(a)), (cx + R * math.cos(a2), cy + R * math.sin(a2))])
    P.ink(m, c1, alpha=.12, offset=False)
    rr = random.Random(hash(l['id']) & 0xffff)
    m = P.mask()
    for _ in range(70):
        x, y, r = rr.uniform(0, w), rr.uniform(0, h), rr.choice([2, 3, 3, 4, 6])
        ellipse(m, (x - r, y - r, x + r, y + r))
    P.ink(m, '#f4f0e0', alpha=.8, offset=False)
    m = P.mask(); ellipse(m, (cx - h * .3, cy - h * .3 - h * .04, cx + h * .3, cy + h * .3 - h * .04), width=8); P.ink(m, c2)
    m = P.mask(); paste_mask(m, icon(l['icon'], int(h * .48)), cx, cy - h * .04); P.ink(m, c1)
    m = P.mask(); put(m, (w / 2, h * .9), l['ru'].upper(), 'oswald', int(h * .1), maxw=w * .8, track=4); P.ink(m, c1)
    m = P.mask(); put(m, (w / 2, h * .08), l['sru'].upper(), 'oswald4', int(h * .05), maxw=w * .8, track=6); P.ink(m, c2)


def L_brand(l, P, w, h):
    paper, c1, c2 = pal(l)
    variant = l['n'] % 2
    m = P.mask(); rect(m, (14, 14, w - 14, h - 14), width=10); rect(m, (34, 34, w - 34, h - 34), width=3); P.ink(m, c1)
    fonts = ['abril', 'slab', 'cinzel', 'playfair', 'abril', 'slab', 'cinzel', 'playfair', 'slab', 'abril']
    f = fonts[l['n'] - 1]
    if variant:
        m = P.mask(); ellipse(m, (w / 2 - h * .25, h * .1, w / 2 + h * .25, h * .6)); P.ink(m, c1)
        m = P.mask(); paste_mask(m, icon(l['icon'], int(h * .38)), w / 2, h * .35); P.ink(m, paper)
        m = P.mask()
        poly(m, [(w * .08, h * .62), (w * .92, h * .62), (w * .88, h * .7), (w * .92, h * .78), (w * .08, h * .78), (w * .12, h * .7)])
        P.ink(m, c2)
        m = P.mask(); put(m, (w / 2, h * .7), l['en'].upper(), f, int(h * .1), maxw=w * .7); P.ink(m, paper)
        m = P.mask(); put(m, (w / 2, h * .87), 'SAFETY MATCHES  ·  AV. CONTENTS 40', 'bebas', int(h * .055), maxw=w * .8, track=2); P.ink(m, c1)
    else:
        m = P.mask(); paste_mask(m, icon(l['icon'], int(h * .5)), w * .25, h * .5); P.ink(m, c2)
        m = P.mask(); put_lines(m, w * .7, h * .42, wrap(l['en'].upper(), 10), f, int(h * .14), w * .42); P.ink(m, c1)
        m = P.mask(); rect(m, (w * .45, h * .62, w * .91, h * .64)); put(m, (w * .68, h * .72), 'SAFETY MATCHES', 'bebas', int(h * .08), maxw=w * .44, track=3)
        put(m, (w * .68, h * .82), 'STRIKE ONLY ON THE BOX', 'bebas', int(h * .045), maxw=w * .44, track=2); P.ink(m, c2)


def L_japan(l, P, w, h):
    bg, c1, c2 = pal(l)    # portrait
    m = P.mask(); ellipse(m, (w * .12, h * .12, w * .88, h * .12 + w * .76)); P.ink(m, c1)
    m = P.mask(); paste_mask(m, icon(l['icon'], int(w * .5)), w / 2, h * .12 + w * .38); P.ink(m, bg)
    m = P.mask(); put(m, (w / 2, h * .76), l['en'].upper(), 'bebas', int(w * .16), maxw=w * .84, track=3); P.ink(m, c1)
    m = P.mask(); rect(m, (w * .3, h * .83, w * .7, h * .835)); put(m, (w / 2, h * .885), 'GINZA  ·  TOKYO', 'bebas', int(w * .07), maxw=w * .6, track=4); P.ink(m, c2)
    m = P.mask(); rect(m, (0, h * .955, w, h)); P.ink(m, c2, offset=False)


def L_hunter(l, P, w, h):
    bg, c1, c2 = pal(l)
    m = P.mask()
    for k in range(-3, 14):
        x = k * w * .08
        poly(m, [(x, 0), (x + w * .04, 0), (x + w * .04 - h * .16, h * .16), (x - h * .16, h * .16)])
    P.ink(m, c1, offset=False)
    m = P.mask(); rect(m, (0, h * .16, w, h * .175)); rect(m, (0, h * .825, w, h * .84)); P.ink(m, c2, offset=False)
    m = P.mask(); paste_mask(m, icon(l['icon'], int(h * .46)), w * .24, h * .5); P.ink(m, c2)
    m = P.mask(); put(m, (w * .7, h * .34), 'ОХОТНИЧЬИ', 'oswald', int(h * .12), maxw=w * .44, track=2)
    put(m, (w * .7, h * .66), l['ru'].upper(), 'oswald', int(h * .09), maxw=w * .44)
    put(m, (w * .7, h * .5), 'ГОРЯТ НА ВЕТРУ И ПОД ДОЖДЁМ', 'oswald4', int(h * .05), maxw=w * .44); P.ink(m, c2)
    m = P.mask(); put(m, (w / 2, h * .91), 'ШТОРМОВЫЕ  ·  20 ШТ.', 'oswald4', int(h * .05), maxw=w * .8, track=5); P.ink(m, c2)


def L_hearth(l, P, w, h):
    bg, c1, c2 = pal(l)   # very wide
    m = P.mask(); rect(m, (20, 20, w - 20, h - 20), width=6); rect(m, (38, 38, w - 38, h - 38), width=2)
    for cx, cy, a0 in [(38, 38, 0), (w - 38, 38, 90), (w - 38, h - 38, 180), (38, h - 38, 270)]:
        for k in range(5):
            a = math.radians(a0 + k * 22.5)
            ImageDraw.Draw(m).line([(cx, cy), (cx + 70 * math.cos(a), cy + 70 * math.sin(a))], fill=255, width=3)
    P.ink(m, c1)
    m = P.mask(); ellipse(m, (w * .07, h * .14, w * .07 + h * .72, h * .86), width=5); paste_mask(m, icon(l['icon'], int(h * .52)), w * .07 + h * .36, h * .5); P.ink(m, c1)
    m = P.mask(); put(m, (w * .67, h * .36), 'HEARTHSIDE', 'cinzel', int(h * .2), maxw=w * .4, track=8); P.ink(m, c1)
    m = P.mask(); put(m, (w * .67, h * .58), 'LONG FIREPLACE MATCHES', 'cinzel4', int(h * .075), maxw=w * .4, track=4)
    put(m, (w * .67, h * .76), l['en'], 'vibes', int(h * .15), maxw=w * .38); P.ink(m, c2)


def L_souvenir(l, P, w, h):
    paper, c1, c2 = pal(l)
    m = P.mask(); rect(m, (40, 40, w - 40, h * .66)); P.ink(m, c1, offset=False)
    m = P.mask()
    for k in range(6):
        y = h * .2 + k * h * .06
        rect(m, (40, y, w - 40, y + h * .02))
    P.ink(m, paper, alpha=.18, offset=False)
    m = P.mask(); paste_mask(m, icon(l['icon'], int(h * .5)), w / 2, h * .42); P.ink(m, paper)
    m = P.mask(); put(m, (w / 2, h * .74), 'Greetings from', 'vibes', int(h * .1), maxw=w * .6); P.ink(m, c2)
    m = P.mask(); put(m, (w / 2, h * .87), l['en'].upper(), 'bebas', int(h * .13), maxw=w * .86, track=6); P.ink(m, c1)
    m = P.mask()
    for x in range(30, w, 34):
        ellipse(m, (x - 7, 12, x + 7, 26)); ellipse(m, (x - 7, h - 26, x + 7, h - 12))
    for y in range(30, h, 34):
        ellipse(m, (12, y - 7, 26, y + 7)); ellipse(m, (w - 26, y - 7, w - 12, y + 7))
    P.ink(m, c2)


def L_diner(l, P, w, h):
    bg, c1, c2 = pal(l)    # portrait
    m = P.mask(); poly(m, star(w / 2, h * .38, w * .46, w * .3, n=12, rot=l['n'] * 7)); P.ink(m, c1)
    m = P.mask(); paste_mask(m, icon(l['icon'], int(w * .44)), w / 2, h * .38); P.ink(m, bg)
    m = P.mask(); put(m, (w / 2, h * .74), l['en'], 'pacifico', int(w * .14), maxw=w * .86); P.ink(m, c2)
    m = P.mask(); put(m, (w / 2, h * .85), 'OPEN LATE  ·  EST. 1958', 'bebas', int(w * .07), maxw=w * .7, track=3)
    put(m, (w / 2, h * .92), 'TEL. %03d-%04d' % (200 + l['n'] * 37, 1000 + l['n'] * 731 % 9000), 'bebas', int(w * .06), maxw=w * .6, track=3); P.ink(m, c1)


LAYOUTS = dict(soviet=L_soviet, poster=L_poster, coast=L_coast, fish=L_fish, sport=L_sport, cosmos=L_cosmos, brand=L_brand,
               japan=L_japan, hunter=L_hunter, hearth=L_hearth, souvenir=L_souvenir, diner=L_diner)


def label(l, out_w=None):
    w, h = label_size(l)
    P = Print(w, h, pal(l)[0], hash(l['id']) & 0xffffff)
    LAYOUTS[l['layout']](l, P, w, h)
    ow = out_w or w // 2
    return P.finish(ow, int(ow * h / w))


# ---------------------------------------------------------------- other faces
def back(l, w=600, h=324):
    paper, c1, c2 = pal(l)
    if l['layout'] in ('coast', 'fish', 'cosmos', 'japan', 'hearth') or (l['layout'] == 'diner' and paper.startswith('#1')):
        paper, ink = c1, pal(l)[0]
    else:
        ink = c2 if l['layout'] not in ('soviet', 'poster', 'hunter') else c2
    W, H = w * 2, h * 2
    P = Print(W, H, paper, hash(l['id'] + 'b') & 0xffffff)
    m = P.mask(); rect(m, (16, 16, W - 16, H - 16), width=4)
    ru = l['layout'] in ('soviet', 'poster', 'coast', 'fish', 'sport', 'cosmos', 'hunter')
    n = KINDS[l['kind']][3]
    if ru:
        lines = ['СЕРИЯ «%s»' % l['sru'].upper().rstrip('!'), 'ЭТИКЕТКА № %d ИЗ %d' % (l['n'], l['total']),
                 'СПИЧКИ ХОЗЯЙСТВЕННЫЕ  ·  %d ШТ.' % n, 'ФАБРИКА «ПОЛЯРНАЯ ЗВЕЗДА»  ·  ЦЕНА 1 КОП.']
        fam = 'oswald4'
    else:
        lines = [l['sen'].upper(), 'LABEL No. %d OF %d' % (l['n'], l['total']), 'CONTENTS %d MATCHES' % n,
                 'CLOSE COVER BEFORE STRIKING' if l['kind'] == 'Book' else 'STRIKE ONLY ON THE BOX']
        fam = 'bebas'
    put_lines(m, W / 2, H / 2, lines, fam, int(H * .13), W * .86, gap=1.25)
    P.ink(m, ink)
    img = P.finish(w, h)
    if l['kind'] == 'Book':
        # a matchbook's striking strip is on its back, along the fold
        s = striker(l, w, int(h * .18))
        img.paste(s, (0, h - s.height))
    return img


def sleeve_colour(l):
    """the paper the sleeve is wrapped in: the label's own ground"""
    paper, c1, c2 = pal(l)
    if l['layout'] in ('coast', 'fish', 'cosmos', 'japan', 'hearth') or (l['layout'] == 'diner' and paper.startswith('#1')):
        return c1
    return paper


def striker(l, w=424, h=160):
    """a long side of the sleeve: the label paper with the striking strip along the middle"""
    rs = np.random.RandomState(hash(l['id'] + 's') & 0xffff)
    paper = np.array(hexc(sleeve_colour(l)), np.float32)
    img = np.ones((h, w, 3), np.float32) * paper * (1 + .03 * rs.normal(0, 1, (h, w, 1)).astype(np.float32))
    y0, y1 = int(h * .17), int(h * .83)
    if l['kind'] == 'Book':
        y0, y1 = 0, h                  # a matchbook's edge is all striker card
    base = np.array(hexc('#5a3326' if l['kind'] != 'Storm' else '#3a2a24'), np.float32)
    n = rs.normal(0, 1, (y1 - y0, w)).astype(np.float32)
    n2 = np.array(Image.fromarray(rs.normal(0, 1, ((y1 - y0) // 3 + 1, w // 3 + 1)).astype(np.float32)).resize((w, y1 - y0)))
    img[y0:y1] = base * (1 + .18 * n[..., None] + .1 * n2[..., None])
    im = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))
    d = ImageDraw.Draw(im)
    rr = random.Random(hash(l['id']) & 0xfff)
    for _ in range(9):          # strike marks: a few lighter scratches
        x = rr.uniform(0, w); y = rr.uniform(y0 + (y1 - y0) * .2, y0 + (y1 - y0) * .8); L = rr.uniform(20, 70)
        d.line([(x, y), (x + L, y + rr.uniform(-5, 5))], fill=(128, 96, 80), width=rr.choice([1, 2]))
    if y0 > 0:
        d.line([(0, y0), (w, y0)], fill=(40, 26, 20), width=2); d.line([(0, y1), (w, y1)], fill=(40, 26, 20), width=2)
    return im


def ends(l, w=424, h=164):
    """an end of the box: the sleeve's rim around the drawer that slides out of it (0.2.1 in game: a plain end read as a solid block)"""
    rs = np.random.RandomState(7)
    sleeve = hexc(sleeve_colour(l))
    im = Image.new('RGB', (w, h), sleeve)
    d = ImageDraw.Draw(im)
    if l['kind'] == 'Book':
        # a matchbook from its open edge: the cover folded round a comb of card matches
        t = max(10, int(h * .2))
        d.rectangle((0, t, w, h - t), fill=(214, 204, 180))
        for x in range(8, w, 26):
            d.line([(x, t), (x, h - t)], fill=(150, 140, 120), width=2)
        d.line([(0, t), (w, t)], fill=(30, 24, 20), width=3); d.line([(0, h - t), (w, h - t)], fill=(30, 24, 20), width=3)
    else:
        t = max(9, int(h * .085))            # the sleeve's card, seen edge-on
        card = (217, 199, 160)
        d.rectangle((t, t, w - t, h - t), fill=(24, 18, 14))                      # the dark gap between sleeve and drawer
        g = max(3, t // 3)
        d.rectangle((t + g, t + g, w - t - g, h - t - g), fill=card)              # the drawer's end
        # the drawer sits a little inside: the sleeve throws a shadow on it from above and from the sides
        sh = Image.new('L', (w, h), 0)
        ds = ImageDraw.Draw(sh)
        ds.rectangle((t + g, t + g, w - t - g, t + g + int(h * .16)), fill=110)
        ds.rectangle((t + g, t + g, t + g + int(h * .07), h - t - g), fill=70)
        ds.rectangle((w - t - g - int(h * .07), t + g, w - t - g, h - t - g), fill=70)
        sh = sh.filter(ImageFilter.GaussianBlur(h * .05))
        m = Image.new('L', (w, h), 0); ImageDraw.Draw(m).rectangle((t + g, t + g, w - t - g, h - t - g), fill=255)
        im.paste(Image.new('RGB', (w, h), (30, 22, 16)), (0, 0), Image.composite(sh, Image.new('L', (w, h), 0), m))
        d = ImageDraw.Draw(im)
        # the drawer's own card edge and a thumb dent in the middle of its top
        d.rectangle((t + g, t + g, w - t - g, h - t - g), outline=(150, 132, 100), width=2)
        cx = w // 2; r = int(h * .17)
        d.pieslice((cx - r, t + g - r, cx + r, t + g + r), 0, 180, fill=(24, 18, 14))
        d.rectangle((0, 0, w - 1, h - 1), outline=tuple(int(c * .7) for c in sleeve), width=2)
    a = np.array(im, np.float32) * (1 + .03 * rs.normal(0, 1, (h, w, 1)).astype(np.float32))
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))


def atlas(l):
    """1024x1024 surface: top label (0,0,1024,700); back (0,700,600,1024); striker (600,700,1024,860); ends (600,860,1024,1024)"""
    A = Image.new('RGB', (1024, 1024))
    top = label(l, 1024 if l['kind'] != 'Book' else 700)
    if l['kind'] == 'Book':
        top = top.rotate(90, expand=True)     # the portrait cover lies along the box's length
    A.paste(top.resize((1024, 700), Image.LANCZOS), (0, 0))
    A.paste(back(l), (0, 700))
    A.paste(striker(l), (600, 700))
    A.paste(ends(l), (600, 860))
    return A

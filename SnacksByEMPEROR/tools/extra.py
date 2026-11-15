"""0.5 label parts: rendered food sprites, background patterns per brand, series ribbon, seasonal marks, the finishing pass."""
import os, math, random, zlib
from PIL import Image, ImageDraw, ImageFilter, ImageChops
import art as A
from art import rgb, mix

HERE = os.path.dirname(os.path.abspath(__file__)); SPR = os.path.join(HERE, '..', 'spr')
_spr = {}
WIDE = dict(ball=1.25, kernel=1.2, cube=1.3, cracker=1.5, ring=1.6, pop=1.8, paw=1.7, rind=1.9, curl=1.6, triangle=1.8, crumb=1.9, seed=2.2, pretzel=1.9, chip=2.0, ridged=2.0)


def sprite(kind, n):
    k = (kind, n % 3)
    if k not in _spr: _spr[k] = Image.open(os.path.join(SPR, f'{kind}_{n % 3}.png')).convert('RGBA')
    return _spr[k]


def piece(im, kind, cx, cy, s, rot, seed=0, shadow=True):
    sp = sprite(kind, seed + int(cx * 7 + cy * 3))
    tw = max(8, s * WIDE.get(kind, 2.0)); k = tw / sp.size[0]
    lay = sp.resize((max(2, int(sp.size[0] * k)), max(2, int(sp.size[1] * k))), Image.LANCZOS)
    if rot: lay = lay.rotate(rot, Image.BICUBIC, expand=True)
    x, y = int(cx - lay.size[0] / 2), int(cy - lay.size[1] / 2)
    if shadow:
        pad = int(s * .25) + 2; a = Image.new('L', (lay.size[0] + pad * 2, lay.size[1] + pad * 2), 0); a.paste(lay.getchannel('A'), (pad, pad))
        a = a.filter(ImageFilter.GaussianBlur(max(2, s * .07))).point(lambda t: int(t * .42))
        im.paste((0, 0, 0), (x - pad + int(s * .05), y - pad + int(s * .1)), a)
    im.paste(lay, (x, y), lay)


def pile(im, kind, cx, cy, r, n=9, seed=3, size=.42):
    """a heap of pieces within radius r, the near ones on top."""
    rnd = random.Random(seed); pts = []
    for i in range(n):
        a = rnd.uniform(0, 6.283); d = r * .62 * math.sqrt(rnd.random())
        pts.append((cy + d * math.sin(a) * .7, cx + d * math.cos(a), rnd.uniform(-70, 70), rnd.uniform(.85, 1.15), i))
    for y, x, rot, k, i in sorted(pts): piece(im, kind, x, y, r * size * k, rot, seed + i)


# the old flat heaps of the hand-made labels become heaps of rendered pieces
def _patch_art():
    A.chips = lambda im, cx, cy, r, col=None, edge=None, n=9, seed=3, spice=None, ridges=False: pile(im, 'ridged' if ridges else 'chip', cx, cy, r, min(n, 9), seed, .5)
    A.puffs = lambda im, cx, cy, r, col=None, n=18, seed=4: pile(im, 'puff', cx, cy, r, min(n, 12), seed, .42)
    A.croutons = lambda im, cx, cy, r, col=None, top=None, n=18, seed=6, spice=None, long=False: pile(im, 'slab' if long else 'cube', cx, cy, r, min(n, 12), seed, .42 if long else .5)
    A.seeds = lambda im, cx, cy, r, n=60, seed=7: pile(im, 'seed', cx, cy, r, min(n, 26), seed, .22)
    A.peanuts = lambda im, cx, cy, r, n=24, seed=8: pile(im, 'nut', cx, cy, r, min(n, 14), seed, .34)
    A.popcorn = lambda im, cx, cy, r, n=30, seed=10, col=None: pile(im, 'pop', cx, cy, r, min(n, 14), seed, .42)
    A.corn_sticks = lambda im, cx, cy, r, n=20, seed=11: pile(im, 'flip', cx, cy, r, min(n, 12), seed, .42)
_patch_art()


def dna(b): return zlib.crc32((b.get('layout') or b['id']).encode())


def bgpat(im, b, v):
    """tone-on-tone background, one of ten per brand, so no two brands of one layout look alike."""
    bg = v[4][0]; w, h = im.size; j = dna(b); kind = j % 10; lum = sum(rgb(bg)) / 3
    t1 = mix(bg, '#ffffff' if lum < 150 else '#000000', .10 if lum < 150 else .07); t2 = mix(bg, '#000000', .14)
    lay = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(lay); C = lambda c, a=255: tuple(c) + (a,)
    if kind == 0:   # rays
        cx, cy = w / 2, h * (.3 + (j >> 4) % 3 * .1); n = 18 + (j >> 6) % 3 * 6; R = h * 1.2
        for i in range(0, n, 2):
            a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 1) / n
            d.polygon([(cx, cy), (cx + R * math.cos(a0), cy + R * math.sin(a0)), (cx + R * math.cos(a1), cy + R * math.sin(a1))], fill=C(t1))
    elif kind == 1:   # slanted stripes
        st = w * (.1 + (j >> 5) % 3 * .04)
        for k in range(-12, 24):
            x = k * st * 2; d.polygon([(x, h), (x + st, h), (x + st + h * .45, 0), (x + h * .45, 0)], fill=C(t1))
    elif kind == 2:   # dots fading upward
        st = w * .075
        for iy in range(int(h / st) + 2):
            for ix in range(int(w / st) + 2):
                r = st * .34 * (iy * st / h); x = ix * st + (iy % 2) * st / 2; y = iy * st
                d.ellipse((x - r, y - r, x + r, y + r), fill=C(t1))
    elif kind == 3:   # dark wave at the bottom
        y0 = h * (.52 + (j >> 4) % 3 * .05); pts = [(0, h)] + [(x, y0 + h * .03 * math.sin(x / w * 6.283 * 1.5 + j % 7)) for x in range(0, w + 8, 8)] + [(w, h)]
        d.polygon(pts, fill=C(t2))
    elif kind == 4:   # top-to-bottom shade
        g = A.vgrad(w, h, mix(bg, '#ffffff', .16), mix(bg, '#000000', .2)); im.paste(g, (0, 0)); return
    elif kind == 5:   # glow behind the middle
        g = A.radial(w, h, mix(bg, '#ffffff', .28), mix(bg, '#000000', .12), cx=.5, cy=.42, r=.75); im.paste(g, (0, 0)); return
    elif kind == 6:   # arch from the top
        d.ellipse((-w * .25, -h * .35, w * 1.25, h * .5), fill=C(t1))
    elif kind == 7:   # zigzag bands
        zh = h * .035
        for y0 in (h * .06, h * .96 - zh):
            pts = []; n = 14
            for i in range(n + 1): pts.append((w * i / n, y0 + (zh if i % 2 else 0)))
            d.line(pts, fill=C(t2), width=max(4, int(w * .012)))
        for k in range(0, 9, 2): d.rectangle((0, h * (.1 + k * .1), w, h * (.1 + k * .1) + h * .05), fill=C(t1, 110))
    elif kind == 8:   # big circles off the corners
        for cx, cy, r in ((w * .0, h * .08, w * .5), (w * 1.0, h * .95, w * .6)): d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=C(t1))
    else:   # split on a slant
        d.polygon([(0, h * .58), (w, h * .44), (w, h), (0, h)], fill=C(t2))
    im.paste(lay, (0, 0), lay)


def motif(d, kind, x, y, s, col):
    if kind == 'flake':
        for a in (0, 60, 120):
            c, sn = math.cos(math.radians(a)), math.sin(math.radians(a)); d.line([(x - s * c, y - s * sn), (x + s * c, y + s * sn)], fill=col, width=max(2, int(s * .16)))
            for k in (-1, 1):
                bx, by = x + k * s * .6 * c, y + k * s * .6 * sn
                for t in (50, -50):
                    c2, s2 = math.cos(math.radians(a + t)), math.sin(math.radians(a + t)); d.line([(bx, by), (bx + k * s * .3 * c2, by + k * s * .3 * s2)], fill=col, width=max(2, int(s * .12)))
    elif kind == 'star':
        pts = [(x + (s if i % 2 == 0 else s * .42) * math.cos(math.radians(-90 + i * 36)), y + (s if i % 2 == 0 else s * .42) * math.sin(math.radians(-90 + i * 36))) for i in range(10)]; d.polygon(pts, fill=col)
    elif kind == 'leaf':
        d.polygon([(x, y - s), (x + s * .6, y - s * .2), (x + s * .35, y + s * .1), (x + s * .7, y + s * .5), (x + s * .15, y + s * .45), (x, y + s), (x - s * .15, y + s * .45), (x - s * .7, y + s * .5), (x - s * .35, y + s * .1), (x - s * .6, y - s * .2)], fill=col)
    elif kind == 'pumpkin':
        for dx in (-.45, 0, .45): d.ellipse((x + s * (dx - .5), y - s * .6, x + s * (dx + .5), y + s * .7), fill=col)
        d.rectangle((x - s * .1, y - s * .95, x + s * .1, y - s * .5), fill=col)
    elif kind == 'petal':
        for a in range(0, 360, 72):
            c, sn = math.cos(math.radians(a - 90)), math.sin(math.radians(a - 90)); d.ellipse((x + s * .5 * c - s * .34, y + s * .5 * sn - s * .34, x + s * .5 * c + s * .34, y + s * .5 * sn + s * .34), fill=col)
    elif kind == 'sun':
        d.ellipse((x - s * .5, y - s * .5, x + s * .5, y + s * .5), fill=col)
        for a in range(0, 360, 45):
            c, sn = math.cos(math.radians(a)), math.sin(math.radians(a)); d.line([(x + s * .65 * c, y + s * .65 * sn), (x + s * c, y + s * sn)], fill=col, width=max(2, int(s * .16)))
    elif kind == 'heart':
        d.ellipse((x - s, y - s * .8, x, y + s * .1), fill=col); d.ellipse((x, y - s * .8, x + s, y + s * .1), fill=col); d.polygon([(x - s * .93, y - s * .1), (x + s * .93, y - s * .1), (x, y + s)], fill=col)
    else:   # firework
        for a in range(0, 360, 30):
            c, sn = math.cos(math.radians(a)), math.sin(math.radians(a)); d.line([(x + s * .3 * c, y + s * .3 * sn), (x + s * c, y + s * sn)], fill=col, width=max(2, int(s * .1)))


MOTIF = dict(winter='flake', xmas='star', newyear='firework', autumn='leaf', halloween='pumpkin', spring='petal', summer='sun', valentine='heart')
SEAL = dict(winter=('#e7f5ff', '#0b4f8a'), xmas=('#ffd43b', '#8a1010'), newyear=('#ffd43b', '#10204a'), autumn=('#ffe8a3', '#8a3a06'), halloween=('#ff922b', '#1a1a1a'),
            spring=('#ffffff', '#c2255c'), summer=('#fff3bf', '#d9480f'), valentine=('#ffffff', '#c2255c'))


def decor(im, b, v, season=None, season_words=None):
    w, h = im.size; bg, fg, acc, ink = v[4]
    if season:
        col, dark = SEAL[season]; rnd = random.Random(b['id'] + v[0])
        lay = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
        for i in range(16):   # along the two sides and the top
            side = i % 3; t = rnd.random()
            x, y = (rnd.uniform(.03, .13) * w, t * h * .9 + h * .05) if side == 0 else (rnd.uniform(.87, .97) * w, t * h * .9 + h * .05) if side == 1 else (t * w, rnd.uniform(.03, .1) * h)
            motif(d, MOTIF[season], x, y, w * rnd.uniform(.022, .045), rgb(col) + (150,))
        im.paste(lay, (0, 0), lay)
        # the seal: a toothed disc, lower left of the middle
        r = w * .125; cx, cy = w * .16, w * .16
        A.badge(im, cx, cy, r, dark, ring=col, teeth=22)
        lines = season_words.split(' ')
        l1 = ' '.join(lines[:-1]) if len(lines) > 1 else lines[0]; l2 = lines[-1] if len(lines) > 1 else ''
        for i, t in enumerate([l1, l2] if l2 else [l1]):
            f = A.fit_font(t, 'oswald', r * 1.25, r * .34); A.text(im, (cx, cy + (i - .5) * r * .42 if l2 else cy), t, f, col)
    if b.get('series'):
        # a ribbon across the top right corner with the name of the line
        txt = b['series']; L_ = int(w * .9); bh = int(max(w * .072, 22))
        rib = Image.new('RGBA', (L_, bh), rgb(acc if sum(rgb(acc)) / 3 > 120 else '#ffd43b') + (255,)); dd = ImageDraw.Draw(rib)
        dd.rectangle((0, 0, L_, max(2, bh // 12)), fill=(0, 0, 0, 70)); dd.rectangle((0, bh - max(2, bh // 12), L_, bh), fill=(0, 0, 0, 70))
        f = A.fit_font(txt, 'cjk' if any(ord(c) > 0x2e7f for c in txt) else 'oswald', w * .27, bh * .62)
        dd.text((L_ / 2, bh / 2), txt, font=f, fill=(20, 20, 20, 255), anchor='mm')
        rib = rib.rotate(-38, Image.BICUBIC, expand=True)
        cx, cy = w - w * .145, w * .165   # close to the corner: further in it ran over the brand name
        im.paste(rib, (int(cx - rib.size[0] / 2), int(cy - rib.size[1] / 2)), rib)


def finish(im):
    """Printed-pack look for the game's light: the brightest tones are pressed down (a white pack in the game's inspect light burns out and takes the text with it),
    plus a faint film grain so flat fills are not dead flat."""
    im = im.convert('RGB')
    lut = []
    for x in range(256): lut.append(x if x <= 150 else int(150 + (x - 150) * .5))
    hsv = im.convert('HSV'); hch, s, val = hsv.split(); val = val.point(lut)
    out = Image.merge('HSV', (hch, s, val)).convert('RGB')
    return out   # no film grain: it multiplied the size of every file by eight

"""Cigarette varieties: one mesh per format (numpy, no Blender) with a fixed UV layout, and one texture per variety (PIL).
Texture 512 x 1024: body = image rows 0..896 (top = lit end, filter at the bottom of that), end discs in the last 128 rows
(filter end at x 0..128, tobacco end at x 128..256).
python3 cigvar.py <bakedir> <texdir> [brand ...]  -> <bakedir>/cig_<fmt>.npz, <texdir>/cig_<brand>_<variety>.jpg"""
import sys, os, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from packs import F, SERIF9, CIN7, OSW7
import catalog

TW, TH, BODY = 512, 1024, 896
V0 = 1.0 - BODY / TH       # body starts at this v (UV origin at the image bottom)

FORMATS = {
    # L, R, filter length (m)
    'regular': (0.084, 0.0039, 0.025),
    'slim': (0.098, 0.0031, 0.025),
    'superslim': (0.098, 0.0026, 0.022),
    'short': (0.070, 0.0039, 0.021),
    'long': (0.100, 0.0039, 0.025),
    'papirosa': (0.105, 0.0042, 0.065),
    'nofilter': (0.070, 0.0039, 0.0),
    # 11-14 new shapes; a 4th number = the radius at the lit end (a cone)
    'cigaronni': (0.100, 0.0039, 0.060),          # a very long hollow mouthpiece, a short tobacco part
    'kretek': (0.088, 0.0036, 0.0, 0.0047),        # hand-rolled clove cigarette, wider at the lit end
    'bidi': (0.062, 0.0023, 0.0, 0.0037),          # a leaf rolled into a small cone and tied with a thread
}


def radii(fmt):
    f = FORMATS[fmt]
    return f[1], (f[3] if len(f) > 3 else f[1])


# ---------------------------------------------------------------- mesh
def ring(r, z, seg):
    th = 2 * math.pi * np.arange(seg + 1) / seg + math.pi / 2
    return np.stack([r * np.cos(th), r * np.sin(th), np.full(seg + 1, z)], 1)


def mesh(fmt, seg=40):
    L = FORMATS[fmt][0]
    R, R1 = radii(fmt)
    V, N, U, T = [], [], [], []

    def add(p, n, uv):
        V.append(p); N.append(n); U.append(uv)
        return len(V) - 1

    # body: two rings, u around, v linear along the length (Burn relies on that)
    th = 2 * math.pi * np.arange(seg + 1) / seg + math.pi / 2
    idx = []
    slope = (R1 - R) / L
    nk = 1.0 / math.sqrt(1.0 + slope * slope)
    for z, rr in ((0.0, R), (L, R1)):
        row = []
        for i in range(seg + 1):
            c, s = math.cos(th[i]), math.sin(th[i])
            row.append(add((rr * c, rr * s, z), (c * nk, s * nk, -slope * nk), (i / seg, V0 + (1 - V0) * z / L)))
        idx.append(row)
    for i in range(seg):
        a, b, c, d = idx[0][i], idx[0][i + 1], idx[1][i + 1], idx[1][i]
        T += [(a, b, c), (a, c, d)]

    def disc(z, prof, cu, normal_sign, R=R):
        # prof: list of (r, dz) from the rim inwards; flat-ish cap, planar UV into the 128 px square at cu
        rows = []
        for r, dz in prof:
            row = []
            for i in range(seg + 1):
                c, s = math.cos(th[i]), math.sin(th[i])
                uv = (cu + 0.125 * 0.92 * (r * c) / R * normal_sign, 0.0625 + 0.0625 * 0.92 * (r * s) / R)
                row.append(add((r * c, r * s, z + dz), (0.0, 0.0, normal_sign), uv))
            rows.append(row)
        for k in range(len(rows) - 1):
            for i in range(seg):
                a, b, c, d = rows[k][i], rows[k][i + 1], rows[k + 1][i + 1], rows[k + 1][i]
                if normal_sign > 0:
                    T.extend([(a, b, c), (a, c, d)])
                else:
                    T.extend([(a, c, b), (a, d, c)])

    disc(0.0, [(R, 0.0), (R * 0.9, -0.0002), (0.0005, -0.0003)], 0.125, -1.0)
    disc(L, [(R1, 0.0), (R1 * 0.95, 0.0), (R1 * 0.93, -0.0006), (0.0005, -0.0008)], 0.375, 1.0, R1)
    return (np.array(V, np.float32), np.array(N, np.float32), np.array(U, np.float32), np.array(T, np.int32))


# ---------------------------------------------------------------- texture
def noise_mul(im, amount, seed):
    n = Image.effect_noise(im.size, amount).convert('RGB')
    return ImageChops.multiply(im, n.point(lambda v: 225 + v // 9))


def cork(w, h, seed=3):
    rnd = random.Random(seed)
    im = Image.new('RGB', (w, h), (204, 138, 70))
    d = ImageDraw.Draw(im)
    for _ in range(int(w * h / 20)):
        x, y = rnd.uniform(0, w), rnd.uniform(0, h)
        rx, ry = rnd.uniform(.6, 2.2), rnd.uniform(.4, 1.5)
        c = rnd.choice([(176, 104, 46), (186, 114, 52), (168, 96, 40), (214, 150, 84), (224, 162, 96), (150, 84, 34)])
        d.ellipse([x - rx, y - ry, x + rx, y + ry], fill=c)
    return noise_mul(im.filter(ImageFilter.GaussianBlur(.6)), 24, seed)


def foil(w, h, base):
    im = Image.new('RGB', (w, h), base)
    d = ImageDraw.Draw(im)
    # metallic: soft bands around (u) so it shines as the cigarette turns
    for x in range(w):
        k = 0.78 + 0.32 * math.sin(x / w * 2 * math.pi * 3) ** 2
        c = tuple(min(255, int(ch * k)) for ch in base)
        d.line([(x, 0), (x, h)], fill=c)
    for y in range(0, h, 3):
        d.line([(0, y), (w, y)], fill=tuple(int(ch * 0.94) for ch in base))
    return im.filter(ImageFilter.GaussianBlur(.7))


def plain(w, h, color, seed, lines=True):
    im = Image.new('RGB', (w, h), color)
    if lines:
        d = ImageDraw.Draw(im)
        dark = tuple(max(0, c - 7) for c in color)
        for x in range(0, w, 5):
            d.line([(x, 0), (x, h)], fill=dark)
    return noise_mul(im, 10, seed)


def flowers(im, color, seed, region):
    rnd = random.Random(seed)
    d = ImageDraw.Draw(im)
    x0, y0, x1, y1 = region
    for _ in range(18):
        cx, cy = rnd.uniform(x0, x1), rnd.uniform(y0, y1)
        r = rnd.uniform(9, 16)
        for k in range(5):
            a = k * 2 * math.pi / 5 + rnd.random()
            px, py = cx + r * .7 * math.cos(a), cy + r * .7 * math.sin(a)
            d.ellipse([px - r * .45, py - r * .45, px + r * .45, py + r * .45], fill=color)
        d.ellipse([cx - r * .25, cy - r * .25, cx + r * .25, cy + r * .25], fill=tuple(min(255, c + 60) for c in color))


def leaves(im, color, seed, region):
    rnd = random.Random(seed)
    d = ImageDraw.Draw(im)
    x0, y0, x1, y1 = region
    for _ in range(16):
        cx, cy = rnd.uniform(x0, x1), rnd.uniform(y0, y1)
        a, l = rnd.uniform(0, math.pi), rnd.uniform(14, 24)
        ca, sa = math.cos(a), math.sin(a)
        pts = []
        for k in range(13):
            t = k / 12 * 2 - 1
            w = (1 - t * t) * l * .32
            pts.append((t * l, w))
        pts += [(x, -y) for x, y in reversed(pts)]
        d.polygon([(cx + x * ca - y * sa, cy + x * sa + y * ca) for x, y in pts], fill=color)
        d.line([(cx - l * ca, cy - l * sa), (cx + l * ca, cy + l * sa)], fill=tuple(max(0, c - 40) for c in color), width=1)


def scrolls(im, color, seed, region):
    # thin flourishes (vensels) in rows
    d = ImageDraw.Draw(im)
    x0, y0, x1, y1 = region
    step = 46
    for row, y in enumerate(range(int(y0) + 20, int(y1) - 10, step)):
        for x in range(int(x0) + (row % 2) * 32 - 10, int(x1), 64):
            d.arc([x - 14, y - 10, x + 14, y + 10], 200, 520, fill=color, width=2)
            d.arc([x + 4, y - 4, x + 16, y + 6], 0, 300, fill=color, width=2)
            d.arc([x - 16, y - 6, x - 4, y + 4], 180, 480, fill=color, width=2)
            d.ellipse([x - 2, y - 2, x + 2, y + 2], fill=color)


def dancer(im, color, seed, region):
    # a small dancing figure with a raised tambourine, once, under the brand name side
    d = ImageDraw.Draw(im)
    x0, y0, x1, y1 = region
    cx, cy, s = (x0 + x1) / 2 + 60, y1 - 60, 2.2
    body = [(0, -20), (6, -10), (4, 2), (14, 18), (8, 20), (0, 8), (-8, 20), (-14, 18), (-4, 2), (-6, -10)]
    d.polygon([(cx + x * s, cy + y * s) for x, y in body], fill=color)
    d.ellipse([cx - 4 * s, cy - 30 * s, cx + 4 * s, cy - 22 * s], fill=color)
    d.line([(cx + 4 * s, cy - 16 * s), (cx + 14 * s, cy - 30 * s)], fill=color, width=int(2 * s))
    d.ellipse([cx + 11 * s, cy - 36 * s, cx + 19 * s, cy - 28 * s], outline=color, width=int(1.5 * s))
    d.line([(cx - 4 * s, cy - 16 * s), (cx - 14 * s, cy - 8 * s)], fill=color, width=int(2 * s))


def end_disc(kind, color):
    w = 128
    rnd = random.Random(1)
    if kind == 'tobacco':
        im = Image.new('RGB', (w, w), (74, 42, 18))
        d = ImageDraw.Draw(im)
        for _ in range(700):
            x, y = rnd.uniform(0, w), rnd.uniform(0, w)
            l, a = rnd.uniform(2, 7), rnd.uniform(0, 3.14)
            c = rnd.choice([(48, 26, 10), (110, 66, 28), (140, 90, 40), (90, 52, 20)])
            d.line([(x, y), (x + l * math.cos(a), y + l * math.sin(a))], fill=c, width=rnd.randint(1, 2))
        return im
    im = Image.new('RGB', (w, w), color)
    d = ImageDraw.Draw(im)
    for _ in range(900):
        x, y = rnd.uniform(0, w), rnd.uniform(0, w)
        k = rnd.randint(-18, 10)
        d.point((x, y), fill=tuple(max(0, min(255, c + k)) for c in color))
    d.ellipse([3, 3, w - 3, w - 3], outline=tuple(max(0, c - 22) for c in color), width=4)
    return im.filter(ImageFilter.GaussianBlur(.5))


# where the real cigarette carries its name (user 11-09: only where the real one has it; when unsure, none)
PRINT = {'Milston': 'paper', 'Marlbrrro': 'paper', 'Caramel': 'paper', 'Lucky Strife': 'paper', 'Pell Mell': 'paper',
         'Chesterfeld': 'paper', 'L&N': 'paper', 'Galloises': 'paper', 'Newpork': 'paper', 'Kooll': 'paper', 'Djarumm': 'paper',
         'Parliamint': 'filter', 'Sobrania': 'filter'}


def leaf_wrap(w, h, color, seed):
    """A dried leaf rolled round the tobacco (bidi): mottled, with the leaf's veins running slantwise and an overlapping edge."""
    rnd = random.Random(seed)
    im = Image.new('RGB', (w, h), color)
    d = ImageDraw.Draw(im)
    for _ in range(260):
        x, y = rnd.uniform(0, w), rnd.uniform(0, h)
        r = rnd.uniform(6, 26)
        k = rnd.randint(-22, 16)
        d.ellipse([x - r, y - r * 1.6, x + r, y + r * 1.6], fill=tuple(max(0, min(255, c + k)) for c in color))
    im = im.filter(ImageFilter.GaussianBlur(5))
    d = ImageDraw.Draw(im)
    dark = tuple(max(0, c - 34) for c in color)
    light = tuple(min(255, c + 18) for c in color)
    for k in range(-6, 16):          # side veins, slanted
        y0 = k * h / 10.0
        d.line([(0, y0), (w, y0 + h * .16)], fill=dark, width=2)
        d.line([(0, y0 + 3), (w, y0 + h * .16 + 3)], fill=light, width=1)
    d.line([(w * .30, 0), (w * .36, h)], fill=dark, width=4)      # the midrib
    d.line([(w * .04, 0), (w * .02, h)], fill=tuple(max(0, c - 50) for c in color), width=5)   # the leaf's edge over the roll
    return noise_mul(im.filter(ImageFilter.GaussianBlur(.6)), 26, seed)


def cloves(im, seed):
    """Oily specks showing through the paper of a clove cigarette."""
    rnd = random.Random(seed)
    lay = Image.new('RGBA', im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    for _ in range(170):
        x, y = rnd.uniform(0, im.width), rnd.uniform(0, im.height)
        r = rnd.uniform(2, 7)
        d.ellipse([x - r, y - r * rnd.uniform(.6, 1.6), x + r, y + r], fill=(150, 104, 50, rnd.randint(26, 70)))
    im.paste(lay.filter(ImageFilter.GaussianBlur(1.6)), (0, 0), lay.filter(ImageFilter.GaussianBlur(1.6)))


def texture(brand_name, v):
    L, FL = FORMATS[v['fmt']][0], FORMATS[v['fmt']][2]
    im = Image.new('RGB', (TW, TH), (0, 0, 0))
    fil = int(round(BODY * FL / L))
    seed = sum(map(ord, brand_name + v['key']))
    if v['fmt'] == 'bidi':
        paper = leaf_wrap(TW, BODY - fil, tuple(v['paper']), seed)
    else:
        paper = plain(TW, BODY - fil, v['paper'], seed)
    if v['fmt'] == 'kretek':
        cloves(paper, seed)
    pat = {'flowers': flowers, 'leaves': leaves, 'scrolls': scrolls, 'dancer': dancer}.get(v['pattern'])
    if pat:
        pat(paper, tuple(v['pattern_color']), seed, (0, 0, TW, (BODY - fil) * 0.55))
    im.paste(paper, (0, 0))
    f = v['filter']
    if fil > 0:
        if f == 'cork':
            tip = cork(TW, fil, seed)
        elif f == 'gold':
            tip = foil(TW, fil, (206, 166, 82))
        elif f == 'white':
            tip = plain(TW, fil, (236, 234, 228), seed + 1)
        elif f == 'black':
            tip = plain(TW, fil, (30, 28, 28), seed + 1, lines=False)
        elif f == 'papirosa':
            tip = plain(TW, fil, (232, 228, 214), seed + 1)
        else:
            tip = plain(TW, fil, tuple(v['filter_color'] or (200, 200, 200)), seed + 1, lines=False)
        im.paste(tip, (0, BODY - fil))
    d = ImageDraw.Draw(im)
    # rings at the tipping edge (paper side)
    y = BODY - fil
    if v['fmt'] == 'bidi':
        # the thread that ties the roll, wound a few times a centimetre from the mouth end (its colour = the first "ring")
        tc = tuple(v['rings'][0]) if v['rings'] else (190, 40, 50)
        ty = int(BODY - BODY * .011 / L)
        for k in range(5):
            yy = ty - k * 5 + (k % 2)
            d.line([(0, yy), (TW, yy + 3)], fill=tuple(max(0, c - (18 if k % 2 else 0)) for c in tc), width=4)
    else:
        for i, rc in enumerate(v['rings']):
            d.rectangle([0, y - 10 - i * 12, TW, y - 5 - i * 12], fill=tuple(rc))
    if fil > 0 and f != 'papirosa':
        d.rectangle([0, y - 2, TW, y + 1], fill=tuple(max(0, c - 30) for c in (im.getpixel((5, y + 6)))))
    # seam
    if v['fmt'] != 'bidi':
        d.line([(TW * 0.02, 0), (TW * 0.02, BODY - fil)], fill=tuple(max(0, c - 14) for c in v['paper']), width=2)
    # brand along the cigarette, facing u = 0.5: on the paper just above the rings, or on the filter, or nowhere
    where = PRINT.get(brand_name)
    if where == 'filter' and fil <= 0:
        where = 'paper'
    if where:
        span = 300 if where == 'paper' else max(80, fil - 30)
        lay = Image.new('RGBA', (span, 64), (0, 0, 0, 0))
        ld = ImageDraw.Draw(lay)
        name = brand_name.upper()
        fs = 42 if where == 'paper' else 30
        while fs > 14 and ld.textlength(name, font=F(SERIF9, fs)) > span - 10:
            fs -= 2
        ink = tuple(v['ink'])
        if where == 'filter':
            base = im.getpixel((int(TW * .5), y + fil // 2))
            if f == 'gold':
                ink = (70, 44, 16)
            elif sum(abs(a - b) for a, b in zip(ink, base)) < 160:
                ink = (40, 30, 30) if sum(base) > 380 else (226, 196, 120)
        ld.text((span // 2, 32), name, font=F(SERIF9, fs), fill=ink + (255,), anchor='mm')
        lay = lay.rotate(90, expand=True)
        top = y - 24 - 12 * len(v['rings']) - span if where == 'paper' else y + (fil - span) // 2
        # printed once at u = .5; in the game the cigarette is rolled so this side faces the player
        im.paste(lay, (int(TW * 0.5 - 32), max(0, top)), lay)
    # end discs
    acet = (30, 28, 28) if f == 'black' else (236, 232, 218)
    if f == 'none' or fil == 0:
        im.paste(end_disc('tobacco', None), (0, BODY))
    elif f == 'papirosa':
        im.paste(end_disc('paper', (214, 210, 196)), (0, BODY))
    else:
        im.paste(end_disc('filter', acet), (0, BODY))
    im.paste(end_disc('tobacco', None), (128, BODY))
    return im


def save_npz(path, v, n, uv, t):
    np.savez(path, verts=v, normals=n, uvs=uv, tris=t)


if __name__ == '__main__':
    bake, tex = sys.argv[1], sys.argv[2]
    only = set(sys.argv[3:])
    os.makedirs(bake, exist_ok=True)
    os.makedirs(tex, exist_ok=True)
    fmts = set()
    for bkey, bname, _, v in catalog.all_varieties():
        if only and bkey not in only:
            continue
        fmts.add(v['fmt'])
        texture(bname, v).save(os.path.join(tex, 'cig_%s.jpg' % catalog.tex_key(bkey, v)), quality=90)
    for f in sorted(fmts):
        save_npz(os.path.join(bake, 'cig_%s.npz' % f), *mesh(f))
        print('mesh', f)
    print('ok')

"""Pack atlases for the cigarette varieties (catalog.py), saved 1024 px JPEG. Two box shapes:
tall = the usual flip-top 55 x 88 x 22 mm (packs.py layout, 13 px/mm); wide = flat landscape box 92 x 86 x 17 mm, lid 22 mm
(Chapman / Sobranie style), same layout at 9 px/mm.
python3 packvar.py <texdir> [brand ...] -> <texdir>/pack_<brand>_<variety>.jpg"""
import sys, os, math, random
from PIL import Image, ImageDraw, ImageChops, ImageFilter
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import packs as P
from packs import F, text_c, fit_font, lady2, SCRIPT, SERIF9, SERIF7, CIN7, CIN9, OSW4, OSW7, Brand, build_atlas
import catalog

SHAPES = {'tall': dict(W=55, H=88, D=22, LID=18, S=13), 'wide': dict(W=92, H=86, D=17, LID=22, S=9),
          'slim': dict(W=46, H=98, D=17, LID=20, S=13)}


def use_shape(name):
    for k, v in SHAPES[name].items():
        setattr(P, k, v)


def mm(v):
    return P.mm(v)


def face():
    return Image.new('RGBA', (mm(P.W), mm(P.H)), (0, 0, 0, 0))


def rgba(c):
    return tuple(c) + (255,) if len(c) == 3 else tuple(c)


def darker(c, k):
    return tuple(int(x * k) for x in c[:3]) + (255,)


def ornament(d, cx, cy, w, col):
    """Scroll flourish line used above/below a name."""
    d.line([(cx - w / 2, cy), (cx - w * .12, cy)], fill=col, width=max(1, mm(.35)))
    d.line([(cx + w * .12, cy), (cx + w / 2, cy)], fill=col, width=max(1, mm(.35)))
    r = w * .05
    for sx in (-1, 1):
        d.arc([cx + sx * w * .12 - r, cy - r, cx + sx * w * .12 + r, cy + r], 0, 360, fill=col, width=max(1, mm(.3)))
    d.regular_polygon((cx, cy, r * .8), 4, rotation=45, fill=col)


def bokeh(img, box, seed, palette):
    """Soft blurred colour spots (the photographic band on top of a Chapman box)."""
    x0, y0, x1, y1 = box
    lay = Image.new('RGBA', img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    rnd = random.Random(seed)
    for _ in range(70):
        cx, cy = rnd.uniform(x0, x1), rnd.uniform(y0, y1)
        r = rnd.uniform(.04, .16) * (x1 - x0)
        c = rnd.choice(palette)
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=c[:3] + (rnd.randint(60, 170),))
    lay = lay.filter(ImageFilter.GaussianBlur(mm(2.5)))
    mask = Image.new('L', img.size, 0)
    ImageDraw.Draw(mask).rectangle(box, fill=255)
    img.paste(lay, (0, 0), ImageChops.multiply(mask, lay.getchannel('A')))


# ---------------------------------------------------------------- Chapgirl
class ChapgirlVar(Brand):
    key, name = 'chapgirl', 'Chapgirl'

    def __init__(self, v):
        p = v['pack']
        self.v = v
        self.base, self.ink, self.stripe = rgba(p['base']), rgba(p['ink']), rgba(p['stripe'])

    def cameo(self, img, d, cx, cy, rx, ry):
        gold = self.ink
        d.ellipse([cx - rx - mm(1.2), cy - ry - mm(1.2), cx + rx + mm(1.2), cy + ry + mm(1.2)], fill=gold)
        d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=(238, 222, 196, 255))
        mask = Image.new('L', img.size, 0)
        ImageDraw.Draw(mask).ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=255)
        lay = Image.new('RGBA', img.size, (0, 0, 0, 0))
        sc = ry / mm(18) * .25
        lady2(ImageDraw.Draw(lay), cx - mm(55 * sc), cy - ry * 1.08, mm(sc), (70, 16, 22, 255) if sum(self.base[:3]) > 200 else (40, 30, 30, 255),
              accent=(190, 140, 70, 255))
        img.paste(lay, (0, 0), ImageChops.multiply(mask, lay.getchannel('A')))

    def front(self):
        return self.front_wide() if P.W > P.H else self.front_tall()

    def front_tall(self):
        img = Image.new('RGBA', (mm(P.W), mm(P.H)), self.base)
        d = ImageDraw.Draw(img)
        W, H = P.W, P.H
        for i in range(0, mm(W), mm(2.4)):
            d.line([(i, 0), (i, mm(H))], fill=self.stripe, width=mm(0.8))
        gold = self.ink
        d.rectangle([mm(2.5), mm(2.5), mm(W - 2.5), mm(H - 2.5)], outline=gold, width=mm(0.9))
        text_c(d, mm(W) / 2, mm(9.5), 'SINCE 1928', F(CIN7, mm(3.4)), gold, spacing=mm(0.6))
        self.cameo(img, d, mm(W / 2), mm(38), mm(15), mm(18))
        text_c(d, mm(W) / 2, mm(66), 'Chapgirl', F(SCRIPT, mm(15)), gold, stroke=mm(0.25), sfill=darker(self.base, .55))
        lab = self.v['label'] + '  ·  20 CIGARETTES'
        text_c(d, mm(W) / 2, mm(78.5), lab, fit_font(d, lab, CIN7, mm(W - 12), mm(3)), gold)
        return img

    def front_wide(self):
        W, H = P.W, P.H
        noir = self.v['style'] == 'noir'
        img = Image.new('RGBA', (mm(W), mm(H)), self.base)
        d = ImageDraw.Draw(img)
        ink = self.ink
        split = H * .56
        # top: picture band
        if True:  # noir too: plain pinstripes + cameo, no blurred photo band (user 11-08 22:38)
            for i in range(0, mm(W), mm(2.4)):
                d.line([(i, 0), (i, mm(split))], fill=self.stripe, width=mm(0.8))
            self.cameo(img, d, mm(W / 2), mm(split * .52), mm(13), mm(15.5))
            for sx in (-1, 1):
                for k in range(3):
                    x = mm(W / 2 + sx * (24 + k * 7))
                    d.regular_polygon((x, mm(split * .52), mm(1.6 - k * .3)), 4, rotation=45, fill=ink)
        # bottom: dark band with the name in a frame
        band = darker(self.base, .45) if not noir else (14, 13, 14, 255)
        d.rectangle([0, mm(split), mm(W), mm(H)], fill=band)
        d.line([(0, mm(split)), (mm(W), mm(split))], fill=ink, width=mm(.6))
        d.rectangle([mm(10), mm(split + 4), mm(W - 10), mm(split + 20)], outline=ink, width=mm(.35))
        ornament(d, mm(W / 2), mm(split + 4), mm(26), ink)
        ornament(d, mm(W / 2), mm(split + 20), mm(26), ink)
        if noir:
            f = fit_font(d, 'CHAPGIRL', CIN9, mm(W - 30), mm(10))
            text_c(d, mm(W) / 2, mm(split + 12), 'CHAPGIRL', f, ink)
        else:
            text_c(d, mm(W) / 2, mm(split + 11.5), 'Chapgirl', F(SCRIPT, mm(12)), ink)
        lab = self.v['label']
        text_c(d, mm(W * .68), mm(split + 26.5), lab, fit_font(d, lab, OSW7, mm(W * .5), mm(3.8)), ink if noir else (240, 236, 226, 255))
        text_c(d, mm(W / 2), mm(H - 4), '20 CIGARETTES', F(CIN7, mm(2.6)), ink, spacing=mm(.6))
        d.rectangle([mm(1.5), mm(1.5), mm(W - 1.5), mm(H - 1.5)], outline=ink, width=mm(.5))
        return img

    def back(self):
        return self.back_default(1)

    def decorate_back(self, img, d):
        d.rectangle([mm(2.5), mm(2.5), mm(P.W - 2.5), mm(P.H - 2.5)], outline=self.ink, width=mm(0.9))
        text_c(d, mm(P.W) / 2, mm(P.H * 0.30), 'Chapgirl', fit_font(d, 'Chapgirl', SCRIPT, mm(P.W - 7), mm(13)), self.ink)


# ---------------------------------------------------------------- Sobrania
def emblem(d, cx, cy, r, gold):
    """Crowned oval medallion with a stylised bird (not any real coat of arms)."""
    d.ellipse([cx - r, cy - r * 1.2, cx + r, cy + r * 1.2], outline=gold, width=max(2, int(r * .08)))
    d.ellipse([cx - r * .82, cy - r * 1.0, cx + r * .82, cy + r * 1.0], outline=gold, width=max(1, int(r * .03)))
    top = cy - r * 1.2
    d.polygon([(cx - r * .5, top - r * .05), (cx - r * .55, top - r * .5), (cx - r * .25, top - r * .25), (cx, top - r * .62),
               (cx + r * .25, top - r * .25), (cx + r * .55, top - r * .5), (cx + r * .5, top - r * .05)], fill=gold)
    s = r / 50
    body = [(0, -26), (6, -18), (5, 6), (12, 22), (0, 16), (-12, 22), (-5, 6), (-6, -18)]
    d.polygon([(cx + x * s, cy + y * s) for x, y in body], fill=gold)
    for sg in (-1, 1):
        wing = [(4, -10), (22, -24), (40, -18), (34, -8), (44, -4), (32, 2), (38, 8), (20, 6), (5, 2)]
        d.polygon([(cx + sg * x * s, cy + y * s) for x, y in wing], fill=gold)
    d.ellipse([cx - 4 * s, cy - 34 * s, cx + 4 * s, cy - 24 * s], fill=gold)


PASTELS = [(236, 150, 178), (130, 180, 226), (150, 206, 150), (240, 214, 110), (186, 160, 218)]


class SobraniaVar(Brand):
    key, name = 'sobrania', 'Sobrania'

    def __init__(self, v):
        p = v['pack']
        self.v = v
        self.base, self.ink = rgba(p['base']), rgba(p['ink'])
        self.stripes = [rgba(c) for c in (p.get('stripes') or [])]

    def palette(self):
        mine = self.stripes[0][:3]
        pal = list(PASTELS)
        pal.remove(min(pal, key=lambda c: sum(abs(a - b) for a, b in zip(c, mine))))
        pal.insert(2, mine)
        return pal

    def front(self):
        return self.front_wide() if P.W > P.H else self.front_tall()

    def front_tall(self):
        W, H = P.W, P.H
        img = Image.new('RGBA', (mm(W), mm(H)), self.base)
        d = ImageDraw.Draw(img)
        gold = self.ink
        if self.stripes:
            for i, c in enumerate(self.palette()):
                d.rectangle([mm(4 + i * (W - 8) / 5), mm(56), mm(4 + (i + 1) * (W - 8) / 5), mm(72)], fill=rgba(c))
        d.rectangle([mm(2), mm(2), mm(W - 2), mm(H - 2)], outline=gold, width=mm(0.7))
        text_c(d, mm(W) / 2, mm(8.5), 'LONDON  ·  1879', F(CIN7, mm(3)), gold, spacing=mm(0.5))
        emblem(d, mm(W / 2), mm(26), mm(9), gold)
        text_c(d, mm(W) / 2, mm(47), 'SOBRANIA', fit_font(d, 'SOBRANIA', CIN9, mm(W - 8), mm(9)), gold)
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(62), lab, fit_font(d, lab, SERIF7, mm(W - 10), mm(5.5)), gold)
        d.line([(mm(14), mm(68)), (mm(W - 14), mm(68))], fill=gold, width=mm(0.4))
        text_c(d, mm(W) / 2, mm(74), '20 FILTER CIGARETTES', F(CIN7, mm(2.8)), gold)
        return img

    def front_wide(self):
        W, H = P.W, P.H
        img = Image.new('RGBA', (mm(W), mm(H)), self.base)
        d = ImageDraw.Draw(img)
        gold = self.ink
        d.rectangle([mm(2), mm(2), mm(W - 2), mm(H - 2)], outline=gold, width=mm(0.7))
        d.rectangle([mm(3.4), mm(3.4), mm(W - 3.4), mm(H - 3.4)], outline=gold, width=mm(0.25))
        if self.stripes:
            # the cocktail: a fan of coloured cigarettes with gold tips across the lower half
            for i, c in enumerate(self.palette()):
                x = mm(14 + i * (W - 28) / 5)
                w = mm((W - 28) / 5 - 2)
                d.rectangle([x, mm(H * .58), x + w, mm(H * .84)], fill=rgba(c))
                d.rectangle([x, mm(H * .84), x + w, mm(H * .90)], fill=(206, 166, 82, 255))
        emblem(d, mm(16), mm(H * .26), mm(8), gold)
        text_c(d, mm(W * .58), mm(H * .2), 'SOBRANIA', fit_font(d, 'SOBRANIA', CIN9, mm(W * .62), mm(11)), gold)
        text_c(d, mm(W * .58), mm(H * .31), 'LONDON  ·  1879', F(CIN7, mm(2.8)), gold, spacing=mm(.5))
        lab = self.v['label']
        text_c(d, mm(W / 2), mm(H * .45), lab, fit_font(d, lab, SERIF7, mm(W - 16), mm(7)), gold)
        if not self.stripes:
            d.line([(mm(20), mm(H * .55)), (mm(W - 20), mm(H * .55))], fill=gold, width=mm(.4))
            emblem(d, mm(W / 2), mm(H * .74), mm(7), gold)
        text_c(d, mm(W / 2), mm(H - 5.5), '20 FILTER CIGARETTES', F(CIN7, mm(2.6)), gold)
        return img

    def back(self):
        return self.back_default(2)

    def decorate_back(self, img, d):
        d.rectangle([mm(2), mm(2), mm(P.W - 2), mm(P.H - 2)], outline=self.ink, width=mm(0.7))
        emblem(d, mm(P.W / 2), mm(P.H * 0.22), mm(6), self.ink)
        text_c(d, mm(P.W) / 2, mm(P.H * 0.40), 'SOBRANIA', fit_font(d, 'SOBRANIA', CIN9, mm(P.W - 7), mm(6)), self.ink)


# ---------------------------------------------------------------- the old brands as varieties (designs from packs.py, colours per variety)
from packs import crest, building, windows, leaf, llama, SLAB, OSW4


class TallDesign(Brand):
    """Front drawn on the 55 x 88 mm design; on a slim box it is scaled to the width and its top/bottom rows stretched."""

    def __init__(self, v):
        self.v = v
        p = v['pack']
        self.p = p
        self.base, self.ink = rgba(p.get('base', p.get('top'))), rgba(p.get('ink', (236, 240, 244) if 'top' in p else (30, 26, 26)))
        b, k = self.base, self.ink
        if abs((.299 * b[0] + .587 * b[1] + .114 * b[2]) - (.299 * k[0] + .587 * k[1] + .114 * k[2])) < 80:
            self.ink = rgba(p['accent']) if 'accent' in p and abs(sum(p['accent']) - sum(b[:3])) > 240 else ((236, 236, 240, 255) if sum(b[:3]) < 384 else (30, 26, 26, 255))

    def front(self):
        if P.W == 55:
            return self.design(55, 88)
        W0, H0 = P.W, P.H
        P.W, P.H = 55, 88
        try:
            img = self.design(55, 88)
        finally:
            P.W, P.H = W0, H0
        w = mm(W0)
        h = int(img.height * w / img.width)
        img = img.resize((w, h), Image.LANCZOS)
        out = Image.new('RGBA', (mm(W0), mm(H0)), self.base)
        y0 = (mm(H0) - h) // 2
        out.paste(img.crop((0, 0, w, 1)).resize((w, y0 + 1)), (0, 0))
        out.paste(img.crop((0, h - 1, w, h)).resize((w, mm(H0) - y0 - h + 1)), (0, y0 + h - 1))
        out.paste(img, (0, y0))
        return out

    def back(self):
        W0, H0 = P.W, P.H
        img = self.back_default(len(self.v['key']))
        return img


class MilstonVar(TallDesign):
    key, name = 'milston', 'Milston'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        band, gold = rgba(self.p['band']), (196, 160, 82, 255)
        light = (250, 240, 230, 255)
        d.rectangle([0, 0, mm(W), mm(22)], fill=band)
        d.rectangle([0, mm(22), mm(W), mm(23.2)], fill=gold)
        text_c(d, mm(W) / 2, mm(11), self.p.get('top', 'FULL FLAVOR'), F(OSW7, mm(4.2)), light, spacing=mm(0.8))
        crest(d, mm(W / 2), mm(35), mm(7), band, self.base, 'M', F(SERIF9, mm(8)))
        f = fit_font(d, 'MILSTON', SERIF9, mm(W - 8), mm(14))
        text_c(d, mm(W) / 2, mm(56), 'MILSTON', f, band, stroke=mm(0.3), sfill=darker(band, .5))
        d.line([(mm(8), mm(64)), (mm(W - 8), mm(64))], fill=gold, width=mm(0.6))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(69), lab, fit_font(d, lab, CIN7, mm(W - 10), mm(4)), (40, 40, 40, 255), spacing=mm(0.9))
        d.rectangle([0, mm(H - 9), mm(W), mm(H)], fill=band)
        text_c(d, mm(W) / 2, mm(H - 4.5), '20 FILTER CIGARETTES', F(OSW4, mm(3.2)), light)
        return img


class MarlbrrroVar(TallDesign):
    key, name = 'marlbrrro', 'Marlbrrro'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        red, crown = rgba(self.p['roof']), rgba(self.p.get('crown', (225, 190, 110)))
        d.polygon([(0, 0), (mm(W), 0), (mm(W), mm(40)), (mm(W / 2 + 2), mm(26)), (mm(W / 2 - 2), mm(26)), (0, mm(40))], fill=red)
        d.ellipse([mm(W / 2 - 4), mm(19), mm(W / 2 + 4), mm(27)], fill=crown)
        d.ellipse([mm(W / 2 - 3), mm(20), mm(W / 2 + 3), mm(26)], fill=red)
        d.polygon([(mm(W / 2 - 2), mm(24.5)), (mm(W / 2 - 2.2), mm(21.5)), (mm(W / 2 - 1), mm(22.8)), (mm(W / 2), mm(21)), (mm(W / 2 + 1), mm(22.8)), (mm(W / 2 + 2.2), mm(21.5)), (mm(W / 2 + 2), mm(24.5))], fill=crown)
        f = fit_font(d, 'Marlbrrro', SERIF9, mm(W - 6), mm(14))
        text_c(d, mm(W) / 2, mm(55), 'Marlbrrro', f, self.ink)
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(66), lab, fit_font(d, lab, OSW4, mm(W - 10), mm(3.6)), self.ink, spacing=mm(0.5))
        text_c(d, mm(W) / 2, mm(80), '20', F(SERIF7, mm(6)), red if sum(red[:3]) < 600 else self.ink)
        return img

    def decorate_back(self, img, d):
        W, H = P.W, P.H
        d.polygon([(0, 0), (mm(W), 0), (mm(W), mm(26)), (mm(W / 2), mm(16)), (0, mm(26))], fill=rgba(self.p['roof']))
        text_c(d, mm(W) / 2, mm(H * 0.38), 'Marlbrrro', fit_font(d, 'Marlbrrro', SERIF9, mm(P.W - 7), mm(8)), self.ink)


class ParliamintVar(TallDesign):
    key, name = 'parliamint', 'Parliamint'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        top, bot = self.p['top'], self.p['bottom']
        for y in range(mm(H)):
            t = y / mm(H)
            d.line([(0, y), (mm(W), y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(top, bot)) + (255,))
        white, mint = rgba(self.p.get('panel', (236, 240, 244))), rgba(self.p.get('accent', (110, 196, 160)))
        d.rectangle([0, mm(50), mm(W), mm(H)], fill=white)
        building(d, mm(5), mm(28), mm(0.45), (236, 240, 244, 255))
        windows(d, mm(5), mm(28), mm(0.45), tuple(top) + (255,))
        d.rectangle([0, mm(49), mm(W), mm(50.5)], fill=mint)
        f = fit_font(d, 'PARLIAMINT', CIN9, mm(W - 6), mm(9))
        text_c(d, mm(W) / 2, mm(12), 'PARLIAMINT', f, (236, 240, 244, 255))
        leaf(img, mm(W * 0.24), mm(66), mm(8), 25, mint, darker(mint, .6))
        lab = self.v['label']
        text_c(d, mm(W * 0.6), mm(66), lab, fit_font(d, lab, OSW7, mm(W * .6), mm(4.6)), darker(mint, .55))
        text_c(d, mm(W) / 2, mm(80), 'RECESSED FILTER  ·  20', F(OSW4, mm(3.2)), tuple(top) + (255,))
        return img


class BentVar(TallDesign):
    key, name = 'bent', 'Bent'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        blue, light = rgba(self.p['band']), rgba(self.p['band2'])
        d.pieslice([mm(-70), mm(-10), mm(18), mm(H + 10)], -60, 60, fill=blue)
        d.pieslice([mm(-70), mm(-4), mm(12), mm(H + 4)], -60, 60, fill=self.base)
        d.pieslice([mm(-74), mm(-14), mm(24), mm(H + 14)], -50, 50, fill=None, outline=light, width=mm(1.2))
        f = fit_font(d, 'BENT', OSW7, mm(W - 18), mm(22))
        text_c(d, mm(W * 0.6), mm(38), 'BENT', f, self.ink)
        lab = self.v['label']
        text_c(d, mm(W * 0.6), mm(52), lab, fit_font(d, lab, OSW4, mm(W * .62), mm(5)), blue, spacing=mm(0.5))
        tag = self.p.get('tag', 'SMOOTH')
        d.rounded_rectangle([mm(W * 0.6 - 10), mm(60), mm(W * 0.6 + 10), mm(66)], radius=mm(1), fill=blue)
        text_c(d, mm(W * 0.6), mm(63), tag, fit_font(d, tag, OSW7, mm(18), mm(3.4)), self.base)
        text_c(d, mm(W) / 2, mm(80), '20 CIGARETTES', F(OSW4, mm(3.2)), self.ink)
        return img


class CaramelVar(TallDesign):
    key, name = 'caramel', 'Caramel'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        brown = self.ink
        dark = rgba(self.p.get('llama', (62, 36, 18)))
        hill = rgba(self.p.get('hill', (176, 136, 80)))
        text_c(d, mm(W) / 2, mm(12), 'CARAMEL', fit_font(d, 'CARAMEL', SLAB, mm(W - 6), mm(11)), brown)
        d.polygon([(0, mm(62)), (mm(10), mm(44)), (mm(18), mm(52)), (mm(30), mm(36)), (mm(44), mm(54)), (mm(50), mm(48)), (mm(W), mm(58)), (mm(W), mm(66)), (0, mm(66))], fill=hill)
        d.polygon([(mm(26), mm(41)), (mm(30), mm(36)), (mm(34), mm(41)), (mm(31), mm(40)), (mm(30), mm(42)), (mm(29), mm(40))], fill=(240, 226, 196, 255))
        for x in (5, 9, 47, 51):
            d.polygon([(mm(x), mm(52)), (mm(x - 2.2), mm(62)), (mm(x + 2.2), mm(62))], fill=darker(hill, .62))
        llama(d, mm(11), mm(30), mm(0.30), dark)
        d.rectangle([0, mm(65), mm(W), mm(66)], fill=brown)
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(71.5), lab, fit_font(d, lab, OSW7, mm(W - 10), mm(4.2)), brown)
        text_c(d, mm(W) / 2, mm(76.5), 'TURKISH & DOMESTIC BLEND', F(OSW4, mm(2.8)), brown)
        text_c(d, mm(W) / 2, mm(82.5), '20', F(SLAB, mm(4.6)), brown)
        return img

    def decorate_back(self, img, d):
        llama(d, mm(P.W / 2 - 9), mm(10), mm(0.18), rgba(self.p.get('llama', (62, 36, 18))))
        text_c(d, mm(P.W) / 2, mm(38), 'CARAMEL', fit_font(d, 'CARAMEL', SLAB, mm(P.W - 7), mm(7)), self.ink)



# ---------------------------------------------------------------- batch 3: new brands (parody designs)
from packs import BEBAS, ABRIL


def lion(d, cx, cy, r, col):
    """Small heraldic beast silhouette (rampant, stylised)."""
    s = r / 20
    body = [(-6, 14), (-8, 4), (-4, -6), (-6, -14), (-2, -18), (4, -16), (6, -10), (4, -4), (10, 2), (12, 12), (8, 18), (2, 12), (-2, 18)]
    d.polygon([(cx + x * s, cy + y * s) for x, y in body], fill=col)
    d.polygon([(cx + x * s, cy + y * s) for x, y in [(10, 2), (18, -6), (16, 0), (12, 6)]], fill=col)


class LuckyVar(TallDesign):
    key, name = 'lucky', 'Lucky Strife'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        tgt, ring = rgba(self.p['target']), rgba(self.p.get('ring', (20, 20, 20)))
        cx, cy = mm(W / 2), mm(44)
        for r, c in ((22, ring), (20.6, (246, 244, 238, 255)), (19.4, ring), (18.4, tgt)):
            d.ellipse([cx - mm(r), cy - mm(r), cx + mm(r), cy + mm(r)], fill=c)
        light = (250, 248, 240, 255)
        text_c(d, cx, cy - mm(5), 'LUCKY', fit_font(d, 'LUCKY', BEBAS, mm(30), mm(12)), light)
        text_c(d, cx, cy + mm(6), 'STRIFE', fit_font(d, 'STRIFE', BEBAS, mm(30), mm(12)), light)
        text_c(d, cx, mm(11), "IT'S FROSTED", F(CIN7, mm(3.6)), ring, spacing=mm(.5))
        lab = self.v['label']
        text_c(d, cx, mm(74), lab, fit_font(d, lab, OSW7, mm(W - 10), mm(5)), tgt)
        text_c(d, cx, mm(81), '20 CIGARETTES', F(OSW4, mm(3)), ring)
        return img

    def decorate_back(self, img, d):
        cx = mm(P.W / 2)
        d.ellipse([cx - mm(9), mm(8), cx + mm(9), mm(26)], fill=rgba(self.p['target']))
        text_c(d, mm(P.W) / 2, mm(34), 'LUCKY STRIFE', fit_font(d, 'LUCKY STRIFE', BEBAS, mm(P.W - 7), mm(7)), self.ink)


class PellVar(TallDesign):
    key, name = 'pell', 'Pell Mell'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        gold, white = rgba(self.p.get('gold', (214, 178, 96))), (248, 244, 236, 255)
        d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=gold, width=mm(.6))
        crest(d, mm(W / 2), mm(24), mm(9), gold, self.base, '', F(SERIF9, mm(6)))
        lion(d, mm(W / 2 - 3.4), mm(24), mm(5), gold)
        lion(d, mm(W / 2 + 3.4), mm(24), mm(5), gold)
        d.polygon([(mm(W / 2 - 6), mm(12)), (mm(W / 2 - 4), mm(8)), (mm(W / 2), mm(10.5)), (mm(W / 2 + 4), mm(8)), (mm(W / 2 + 6), mm(12))], fill=gold)
        d.rectangle([0, mm(42), mm(W), mm(56)], fill=white)
        text_c(d, mm(W) / 2, mm(49), 'PELL MELL', fit_font(d, 'PELL MELL', SERIF9, mm(W - 8), mm(10)), self.base)
        text_c(d, mm(W) / 2, mm(61), 'PER ASPERA AD NIX', F(CIN7, mm(2.6)), gold, spacing=mm(.4))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(70), lab, fit_font(d, lab, OSW7, mm(W - 12), mm(5)), white)
        text_c(d, mm(W) / 2, mm(80), 'FAMOUS CIGARETTES  ·  20', F(OSW4, mm(2.8)), gold)
        return img


class DunchillVar(TallDesign):
    key, name = 'dunchill', 'Dunchill'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        gold = rgba(self.p.get('gold', (206, 170, 92)))
        d.rectangle([mm(2.5), mm(2.5), mm(W - 2.5), mm(H - 2.5)], outline=gold, width=mm(.5))
        d.rectangle([mm(4), mm(4), mm(W - 4), mm(H - 4)], outline=gold, width=mm(.25))
        cx = mm(W / 2)
        d.ellipse([cx - mm(8), mm(14), cx + mm(8), mm(30)], outline=gold, width=mm(.5))
        text_c(d, cx, mm(21.5), 'd', F(ABRIL, mm(13)), gold)
        text_c(d, cx, mm(44), 'dunchill', fit_font(d, 'dunchill', SERIF7, mm(W - 10), mm(11)), gold)
        text_c(d, cx, mm(52), 'LONDON', F(CIN7, mm(3.2)), gold, spacing=mm(1.2))
        lab = self.v['label']
        d.line([(mm(10), mm(60)), (mm(W - 10), mm(60))], fill=gold, width=mm(.3))
        text_c(d, cx, mm(66), lab, fit_font(d, lab, CIN7, mm(W - 10), mm(4)), gold)
        d.line([(mm(10), mm(72)), (mm(W - 10), mm(72))], fill=gold, width=mm(.3))
        text_c(d, cx, mm(80), '20 CIGARETTES', F(CIN7, mm(2.6)), gold)
        return img


class DovidoffVar(TallDesign):
    key, name = 'dovidoff', 'Dovidoff'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        band, ink = rgba(self.p['band']), self.ink
        d.polygon([(0, mm(58)), (mm(W), mm(46)), (mm(W), mm(60)), (0, mm(72))], fill=band)
        d.line([(0, mm(56)), (mm(W), mm(44))], fill=rgba(self.p.get('line', (200, 170, 100))), width=mm(.5))
        text_c(d, mm(W) / 2, mm(30), 'Dovidoff', fit_font(d, 'Dovidoff', SCRIPT, mm(W - 6), mm(16)), ink)
        text_c(d, mm(W) / 2, mm(40), 'GENEVA', F(CIN7, mm(3)), ink, spacing=mm(1.4))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(79), lab, fit_font(d, lab, CIN7, mm(W - 10), mm(4.4)), ink)
        text_c(d, mm(W) / 2, mm(84.5), '20 CIGARETTES', F(OSW4, mm(2.4)), ink)
        return img


class RothmunzVar(TallDesign):
    key, name = 'rothmunz', 'Rothmunz'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        stripe, gold, white = rgba(self.p['stripe']), (210, 176, 96, 255), (246, 244, 240, 255)
        for y in (30, 33):
            d.rectangle([0, mm(y), mm(W), mm(y + 1.6)], fill=stripe)
        d.rectangle([0, mm(36), mm(W), mm(62)], fill=white)
        emblem(d, mm(W / 2), mm(16), mm(7), gold)
        text_c(d, mm(W) / 2, mm(44), 'ROTHMUNZ', fit_font(d, 'ROTHMUNZ', SERIF9, mm(W - 8), mm(10)), self.base)
        text_c(d, mm(W) / 2, mm(52), 'OF LONDON', F(CIN7, mm(3)), self.base, spacing=mm(.8))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(58), lab, fit_font(d, lab, OSW7, mm(W - 12), mm(3.6)), stripe)
        for y in (64, 67):
            d.rectangle([0, mm(y), mm(W), mm(y + 1.6)], fill=stripe)
        text_c(d, mm(W) / 2, mm(79), '20 KING SIZE', F(OSW4, mm(3.2)), white)
        return img


class BensonVar(TallDesign):
    key, name = 'benson', 'Benson & Hodges'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        base = self.base
        for y in range(mm(H)):
            k = .86 + .2 * math.sin(y / mm(H) * math.pi * 3) ** 2
            d.line([(0, y), (mm(W), y)], fill=tuple(min(255, int(c * k)) for c in base[:3]) + (255,))
        ink = self.ink
        d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=ink, width=mm(.4))
        cx = mm(W / 2)
        d.ellipse([cx - mm(10), mm(12), cx + mm(10), mm(34)], outline=ink, width=mm(.5))
        lion(d, cx - mm(3), mm(23), mm(7), ink)
        lion(d, cx + mm(3), mm(23), mm(7), ink)
        text_c(d, cx, mm(44), 'BENSON', fit_font(d, 'BENSON', CIN9, mm(W - 10), mm(8)), ink)
        text_c(d, cx, mm(51), '&', F(SERIF7, mm(5)), ink)
        text_c(d, cx, mm(58), 'HODGES', fit_font(d, 'HODGES', CIN9, mm(W - 10), mm(8)), ink)
        lab = self.v['label']
        text_c(d, cx, mm(69), lab, fit_font(d, lab, CIN7, mm(W - 12), mm(4)), ink)
        text_c(d, cx, mm(80), 'KING SIZE  ·  20', F(CIN7, mm(2.6)), ink)
        return img


CLASSES = {'chapgirl': ChapgirlVar, 'sobrania': SobraniaVar, 'milston': MilstonVar, 'marlbrrro': MarlbrrroVar,
           'parliamint': ParliamintVar, 'bent': BentVar, 'caramel': CaramelVar,
           'lucky': LuckyVar, 'pell': PellVar, 'dunchill': DunchillVar, 'dovidoff': DovidoffVar, 'rothmunz': RothmunzVar, 'benson': BensonVar}


def flower(d, cx, cy, r, col, centre):
    for k in range(5):
        a = k * 2 * math.pi / 5 - math.pi / 2
        px, py = cx + r * .62 * math.cos(a), cy + r * .62 * math.sin(a)
        d.ellipse([px - r * .42, py - r * .42, px + r * .42, py + r * .42], fill=col)
    d.ellipse([cx - r * .24, cy - r * .24, cx + r * .24, cy + r * .24], fill=centre)


class VagueVar(TallDesign):
    key, name = 'vague', 'Vague'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        ink = rgba(self.p.get('ink', darker(acc, .7)))
        rnd = random.Random(len(self.v['key']) * 7)
        lite = tuple(int(c + (255 - c) * .55) for c in acc[:3]) + (255,)
        for _ in range(14):
            flower(d, mm(rnd.uniform(4, W - 4)), mm(rnd.uniform(4, 30)), mm(rnd.uniform(2.2, 4)), lite, acc)
        d.rectangle([0, mm(36), mm(W), mm(37)], fill=acc)
        text_c(d, mm(W) / 2, mm(48), 'Vague', fit_font(d, 'Vague', SCRIPT, mm(W - 8), mm(16)), ink)
        text_c(d, mm(W) / 2, mm(58), 'SUPERSLIMS', F(CIN7, mm(3)), acc, spacing=mm(1))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(68), lab, fit_font(d, lab, CIN7, mm(W - 12), mm(5)), ink)
        text_c(d, mm(W) / 2, mm(H - 6), 'CIGARETTES FRANCAISES  ·  20', F(CIN7, mm(2.2)), acc)
        return img


class GlamoorVar(TallDesign):
    key, name = 'glamoor', 'Glamoor'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        base, acc = self.base, rgba(self.p['accent'])
        for y in range(mm(H)):
            k = y / mm(H)
            d.line([(0, y), (mm(W), y)], fill=tuple(int(base[i] * (1 - k * .5) + acc[i] * k * .5) for i in range(3)) + (255,))
        ink = rgba(self.p.get('ink', darker(acc, .6)))
        for y in (8, 80):
            ornament(d, mm(W / 2), mm(y), mm(W - 8), acc if y == 8 else ink)
        cx = mm(W / 2)
        d.ellipse([cx - mm(11), mm(16), cx + mm(11), mm(38)], outline=ink, width=mm(.5))
        text_c(d, cx, mm(27), 'G', F(ABRIL, mm(14)), ink)
        text_c(d, cx, mm(50), 'GLAMOOR', fit_font(d, 'GLAMOOR', CIN9, mm(W - 8), mm(8)), ink, spacing=mm(.6))
        lab = self.v['label']
        text_c(d, cx, mm(62), lab, fit_font(d, lab, SCRIPT, mm(W - 8), mm(10)), ink)
        text_c(d, cx, mm(72), 'SUPERSLIMS  ·  20', F(CIN7, mm(2.4)), ink)
        return img


class EzzeVar(TallDesign):
    key, name = 'ezze', 'Ezze'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        ink = rgba(self.p.get('ink', (40, 40, 48)))
        # a soft wave across the lower half
        pts = [(0, mm(H))] + [(x, mm(56) + mm(6) * math.sin(x / mm(W) * math.pi * 1.6)) for x in range(0, mm(W) + 1, 4)] + [(mm(W), mm(H))]
        d.polygon(pts, fill=acc)
        pts2 = [(x, mm(60) + mm(6) * math.sin(x / mm(W) * math.pi * 1.6 + .6)) for x in range(0, mm(W) + 1, 4)]
        d.line(pts2, fill=self.base, width=mm(.6))
        text_c(d, mm(W) / 2, mm(30), 'EZZE', fit_font(d, 'EZZE', OSW4, mm(W - 10), mm(16)), ink, spacing=mm(2))
        text_c(d, mm(W) / 2, mm(42), 'SUPER SLIM', F(OSW4, mm(3)), acc, spacing=mm(1))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(74), lab, fit_font(d, lab, OSW7, mm(W - 12), mm(5)), self.base)
        text_c(d, mm(W) / 2, mm(H - 6), '20 CIGARETTES', F(OSW4, mm(2.6)), self.base)
        return img


class LNVar(TallDesign):
    key, name = 'ln', 'L&N'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        d.polygon([(0, mm(50)), (mm(W), mm(38)), (mm(W), mm(H)), (0, mm(H))], fill=acc)
        text_c(d, mm(W) / 2, mm(24), 'L&N', fit_font(d, 'L&N', SERIF9, mm(W - 10), mm(20)), acc)
        text_c(d, mm(W) / 2, mm(36), 'SINCE 1873', F(CIN7, mm(2.4)), darker(acc, .7), spacing=mm(.8))
        lab = self.v['label']
        white = (246, 244, 240, 255)
        text_c(d, mm(W) / 2, mm(64), lab, fit_font(d, lab, OSW7, mm(W - 12), mm(7)), white)
        text_c(d, mm(W) / 2, mm(78), '20 CIGARETTES', F(OSW4, mm(3)), white)
        return img


def helmet(d, cx, cy, r, col):
    """Winged helmet (a round cap with two swept wings)."""
    d.chord([cx - r * .7, cy - r * .7, cx + r * .7, cy + r * .7], 180, 360, fill=col)
    d.rectangle([cx - r * .78, cy - r * .02, cx + r * .78, cy + r * .12], fill=col)
    for sx in (-1, 1):
        wing = [(.5, -.3), (1.4, -1.1), (1.25, -.75), (1.55, -.8), (1.25, -.45), (1.45, -.4), (.7, -.02)]
        d.polygon([(cx + sx * x * r, cy + y * r) for x, y in wing], fill=col)


class GalloisesVar(TallDesign):
    key, name = 'galloises', 'Galloises'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        hc = rgba(self.p['helmet'])
        helmet(d, mm(W / 2), mm(34), mm(14), hc)
        text_c(d, mm(W) / 2, mm(56), 'GALLOISES', fit_font(d, 'GALLOISES', BEBAS, mm(W - 6), mm(11)), self.ink, spacing=mm(.4))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(67), lab, fit_font(d, lab, OSW4, mm(W - 12), mm(5)), self.ink, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(80), 'LIBERTE TOUJOURS', F(OSW4, mm(2.6)), self.ink, spacing=mm(.8))
        return img


def dancer_pack(d, cx, cy, s, col):
    body = [(0, -20), (6, -10), (4, 2), (16, 22), (8, 24), (0, 10), (-10, 24), (-16, 20), (-4, 2), (-6, -10)]
    d.polygon([(cx + x * s, cy + y * s) for x, y in body], fill=col)
    d.ellipse([cx - 4 * s, cy - 31 * s, cx + 4 * s, cy - 22 * s], fill=col)
    d.line([(cx + 4 * s, cy - 15 * s), (cx + 15 * s, cy - 30 * s)], fill=col, width=max(1, int(2.4 * s)))
    d.ellipse([cx + 12 * s, cy - 37 * s, cx + 21 * s, cy - 28 * s], outline=col, width=max(1, int(1.6 * s)))
    d.line([(cx - 4 * s, cy - 15 * s), (cx - 15 * s, cy - 6 * s)], fill=col, width=max(1, int(2.4 * s)))


class GitanosVar(TallDesign):
    key, name = 'gitanos', 'Gitanos'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        sc = rgba(self.p['smoke'])
        # the dancer in a curl of smoke
        pts = [(mm(W / 2) + mm(9) * math.sin(t / 10) * (1 - t / 140), mm(46) - mm(t * .26)) for t in range(0, 140)]
        d.line(pts, fill=sc, width=mm(.7))
        dancer_pack(d, mm(W / 2), mm(36), mm(.42), sc)
        text_c(d, mm(W) / 2, mm(60), 'GITANOS', fit_font(d, 'GITANOS', SERIF9, mm(W - 8), mm(10)), self.ink)
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(70), lab, fit_font(d, lab, CIN7, mm(W - 12), mm(4.4)), self.ink, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(81), 'CIGARETTES  ·  20', F(CIN7, mm(2.4)), self.ink)
        return img


CLASSES.update({'vague': VagueVar, 'glamoor': GlamoorVar, 'ezze': EzzeVar, 'ln': LNVar, 'galloises': GalloisesVar, 'gitanos': GitanosVar})


class BelomorVar(TallDesign):
    key, name = 'belomor', 'Belomorkanall'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        mp = rgba(self.p['map'])
        # a map of the canal: coast lines, the canal, a lake and a sea
        rnd = random.Random(len(self.v['key']) * 11)
        coast = [(mm(3), mm(10))]
        for k in range(1, 12):
            coast.append((mm(3 + k * 4.4), mm(10 + 6 * math.sin(k * 1.3) + rnd.uniform(-1, 1))))
        d.line(coast, fill=mp, width=mm(.6))
        d.ellipse([mm(8), mm(26), mm(22), mm(36)], outline=mp, width=mm(.5))
        d.line([(mm(15), mm(36)), (mm(20), mm(44)), (mm(30), mm(48)), (mm(36), mm(58)), (mm(44), mm(62))], fill=mp, width=mm(.9))
        for x, y in ((mm(20), mm(44)), (mm(30), mm(48)), (mm(36), mm(58))):
            d.rectangle([x - mm(.8), y - mm(.8), x + mm(.8), y + mm(.8)], fill=mp)
        d.rectangle([0, mm(66), mm(W), mm(H)], fill=mp)
        light = rgba(self.base)
        text_c(d, mm(W) / 2, mm(72), 'BELOMORKANALL', fit_font(d, 'BELOMORKANALL', OSW7, mm(W - 6), mm(6)), light)
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(79), lab, fit_font(d, lab, OSW4, mm(W - 12), mm(4)), light, spacing=mm(.5))
        text_c(d, mm(W) / 2, mm(84.5), 'PAPIROSY  ·  20', F(OSW4, mm(2.4)), light)
        return img


class YawaVar(TallDesign):
    key, name = 'yawa', 'Yawa'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        band = rgba(self.p['band'])
        d.rectangle([0, mm(30), mm(W), mm(54)], fill=band)
        d.line([(0, mm(29)), (mm(W), mm(29))], fill=self.ink, width=mm(.4))
        d.line([(0, mm(55)), (mm(W), mm(55))], fill=self.ink, width=mm(.4))
        text_c(d, mm(W) / 2, mm(42), 'YAWA', fit_font(d, 'YAWA', SERIF9, mm(W - 10), mm(16)), (246, 236, 210, 255) if sum(band[:3]) < 400 else self.ink)
        text_c(d, mm(W) / 2, mm(16), 'SINCE 1966', F(CIN7, mm(2.8)), self.ink, spacing=mm(.8))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(66), lab, fit_font(d, lab, CIN7, mm(W - 10), mm(5)), self.ink)
        text_c(d, mm(W) / 2, mm(80), '20 CIGARETTES', F(CIN7, mm(2.6)), self.ink)
        return img


def devil(d, cx, cy, r, col, bg):
    d.ellipse([cx - r, cy - r * .9, cx + r, cy + r * .9], fill=col)
    for sx in (-1, 1):
        d.polygon([(cx + sx * r * .55, cy - r * .6), (cx + sx * r * 1.05, cy - r * 1.6), (cx + sx * r * .9, cy - r * .3)], fill=col)
        d.polygon([(cx + sx * r * .15, cy - r * .2), (cx + sx * r * .55, cy - r * .32), (cx + sx * r * .5, cy - r * .05)], fill=bg)
    d.arc([cx - r * .55, cy - r * .2, cx + r * .55, cy + r * .55], 20, 160, fill=bg, width=max(1, int(r * .12)))
    d.polygon([(cx - r * .2, cy + r * .75), (cx, cy + r * 1.3), (cx + r * .2, cy + r * .75)], fill=col)


class DevolVar(TallDesign):
    key, name = 'devol', 'Black Devol'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        devil(d, mm(W / 2), mm(30), mm(10), acc, self.base)
        text_c(d, mm(W) / 2, mm(54), 'BLACK', fit_font(d, 'BLACK', BEBAS, mm(W - 10), mm(9)), acc)
        text_c(d, mm(W) / 2, mm(63), 'DEVOL', fit_font(d, 'DEVOL', BEBAS, mm(W - 10), mm(9)), acc)
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(72), lab, fit_font(d, lab, OSW4, mm(W - 12), mm(4.4)), (230, 230, 234, 255), spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(82), '20 CIGARETTES', F(OSW4, mm(2.6)), acc)
        return img


class NatShermanVar(TallDesign):
    key, name = 'natsherman', 'Nat Shermann'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        ink = rgba(self.p.get('ink', darker(acc, .55)))
        d.rectangle([0, 0, mm(W), mm(18)], fill=acc)
        d.rectangle([0, mm(H - 14), mm(W), mm(H)], fill=acc)
        d.rectangle([mm(3), mm(21), mm(W - 3), mm(H - 17)], outline=ink, width=mm(.3))
        text_c(d, mm(W) / 2, mm(34), 'Nat', fit_font(d, 'Nat', SCRIPT, mm(W - 20), mm(12)), ink)
        text_c(d, mm(W) / 2, mm(46), 'SHERMANN', fit_font(d, 'SHERMANN', CIN9, mm(W - 10), mm(7)), ink, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(54), 'NEW YORK', F(CIN7, mm(2.6)), ink, spacing=mm(1.2))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(64), lab, fit_font(d, lab, CIN7, mm(W - 12), mm(4.4)), ink)
        text_c(d, mm(W) / 2, mm(H - 7), 'LUXURY CIGARETTES', F(CIN7, mm(2.4)), rgba(self.base))
        return img


class DjarummVar(TallDesign):
    key, name = 'djarumm', 'Djarumm'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        for k in range(6):
            y = mm(8 + k * 2.2)
            d.line([(0, y), (mm(W), y)], fill=acc, width=mm(.5))
        # a clove: a small four-petal head on a stalk
        cx, cy = mm(W / 2), mm(34)
        d.line([(cx, cy), (cx, cy + mm(12))], fill=acc, width=mm(1.2))
        for a in range(4):
            ang = a * math.pi / 2 + math.pi / 4
            px, py = cx + mm(2.4) * math.cos(ang), cy + mm(2.4) * math.sin(ang)
            d.ellipse([px - mm(1.6), py - mm(1.6), px + mm(1.6), py + mm(1.6)], fill=acc)
        text_c(d, mm(W) / 2, mm(58), 'DJARUMM', fit_font(d, 'DJARUMM', OSW7, mm(W - 8), mm(10)), acc, spacing=mm(.5))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(68), lab, fit_font(d, lab, OSW4, mm(W - 12), mm(4.6)), (236, 232, 222, 255), spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(80), 'KRETEK  ·  20', F(OSW4, mm(2.8)), acc)
        return img


class TreasurrerVar(TallDesign):
    key, name = 'treasurrer', 'Treasurrer'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        base = self.base
        for y in range(mm(H)):
            k = .82 + .3 * abs(math.sin(y / mm(H) * math.pi * 2.2))
            d.line([(0, y), (mm(W), y)], fill=tuple(min(255, int(c * k)) for c in base[:3]) + (255,))
        acc = rgba(self.p['accent'])
        d.rectangle([mm(4), mm(4), mm(W - 4), mm(H - 4)], outline=acc, width=mm(.5))
        crest(d, mm(W / 2), mm(28), mm(9), acc, base, 'T', F(SERIF9, mm(8)))
        text_c(d, mm(W) / 2, mm(50), 'TREASURRER', fit_font(d, 'TREASURRER', CIN9, mm(W - 10), mm(7)), acc, spacing=mm(.4))
        text_c(d, mm(W) / 2, mm(57), 'LONDON', F(CIN7, mm(2.6)), acc, spacing=mm(1.4))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(68), lab, fit_font(d, lab, CIN7, mm(W - 12), mm(4.4)), acc)
        text_c(d, mm(W) / 2, mm(80), 'LUXURY  ·  20', F(CIN7, mm(2.4)), acc)
        return img


class NewporkVar(TallDesign):
    key, name = 'newpork', 'Newpork'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        # the spinnaker: a swept sail shape
        sail = [(mm(4), mm(52))] + [(mm(4 + t * .47), mm(52 - 26 * math.sin(t / 100 * math.pi * .5))) for t in range(0, 101, 4)] + [(mm(W - 4), mm(30)), (mm(W - 4), mm(40))]
        sail += [(mm(W - 4 - t * .47), mm(40 + 16 * math.sin(t / 100 * math.pi * .5))) for t in range(0, 101, 4)]
        d.polygon(sail, fill=acc)
        text_c(d, mm(W) / 2, mm(66), 'Newpork', fit_font(d, 'Newpork', SERIF7, mm(W - 10), mm(11)), darker(acc, .75))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(76), lab, fit_font(d, lab, OSW4, mm(W - 12), mm(4.2)), darker(acc, .75), spacing=mm(.5))
        text_c(d, mm(W) / 2, mm(84), '20 CIGARETTES', F(OSW4, mm(2.4)), darker(acc, .75))
        return img


CLASSES.update({'belomor': BelomorVar, 'yawa': YawaVar, 'devol': DevolVar, 'natsherman': NatShermanVar, 'djarumm': DjarummVar,
                'treasurrer': TreasurrerVar, 'newpork': NewporkVar})


# ---------------------------------------------------------------- batch 6 (11-09): 20 more brands
def lum(c):
    return .299 * c[0] + .587 * c[1] + .114 * c[2]


def star(d, cx, cy, r, col, n=5, inner=.42, rot=-90):
    pts = []
    for k in range(n * 2):
        a = math.radians(rot + k * 180 / n)
        q = r if k % 2 == 0 else r * inner
        pts.append((cx + math.cos(a) * q, cy + math.sin(a) * q))
    d.polygon(pts, fill=col)


def flame(d, cx, cy, r, col, core=None):
    pts = [(cx, cy - r * 1.4), (cx + r * .35, cy - r * .7), (cx + r * .2, cy - r * .9), (cx + r * .8, cy - r * .1), (cx + r * .75, cy + r * .5),
           (cx + r * .4, cy + r * .9), (cx - r * .4, cy + r * .9), (cx - r * .75, cy + r * .5), (cx - r * .7, cy - r * .2), (cx - r * .4, cy + r * .1),
           (cx - r * .3, cy - r * .6)]
    d.polygon(pts, fill=col)
    if core:
        d.ellipse([cx - r * .35, cy + r * .05, cx + r * .35, cy + r * .8], fill=core)


class KosmossVar(TallDesign):
    key, name = 'kosmoss', 'Kosmoss'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        rnd = random.Random(len(self.v['key']) * 7)
        for _ in range(60):
            x, y, r = rnd.uniform(0, mm(W)), rnd.uniform(0, mm(46)), rnd.uniform(.15, .45)
            d.ellipse([x - mm(r), y - mm(r), x + mm(r), y + mm(r)], fill=(250, 250, 255, 255))
        # planet with a ring and a rocket climbing past it
        cx, cy = mm(W * .32), mm(30)
        d.ellipse([cx - mm(9), cy - mm(9), cx + mm(9), cy + mm(9)], fill=acc)
        d.arc([cx - mm(15), cy - mm(4), cx + mm(15), cy + mm(4)], 200, 340, fill=self.ink, width=mm(.8))
        d.arc([cx - mm(15), cy - mm(4), cx + mm(15), cy + mm(4)], 0, 180, fill=self.ink, width=mm(.8))
        d.line([(mm(W * .55), mm(44)), (mm(W * .82), mm(10))], fill=acc, width=mm(.6))
        rx, ry = mm(W * .82), mm(10)
        d.polygon([(rx, ry - mm(4)), (rx + mm(2.4), ry + mm(2)), (rx - mm(2.4), ry + mm(2))], fill=self.ink)
        d.rectangle([0, mm(50), mm(W), mm(64)], fill=acc)
        text_c(d, mm(W) / 2, mm(57), 'KOSMOSS', fit_font(d, 'KOSMOSS', BEBAS, mm(W - 8), mm(12)), self.base, spacing=mm(.6))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(71), lab, fit_font(d, lab, OSW7, mm(W - 12), mm(5)), self.ink, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(81), '20 CIGARETTES', F(OSW4, mm(2.8)), acc)
        return img


class PrimmaVar(TallDesign):
    key, name = 'primma', 'Primma'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        # a wide diagonal ribbon across the pack with the name on it
        d.polygon([(0, mm(36)), (mm(W), mm(22)), (mm(W), mm(44)), (0, mm(58))], fill=acc)
        lay = Image.new('RGBA', img.size, (0, 0, 0, 0))
        ld = ImageDraw.Draw(lay)
        text_c(ld, mm(W) / 2, mm(40), 'PRIMMA', fit_font(ld, 'PRIMMA', SLAB, mm(W - 6), mm(13)), self.base)
        lay = lay.rotate(math.degrees(math.atan2(14, W)), center=(mm(W) / 2, mm(40)), resample=Image.BICUBIC)
        img.alpha_composite(lay)
        d = ImageDraw.Draw(img)
        for k in range(3):
            y = mm(9 + k * 2.2)
            d.line([(mm(6), y), (mm(W - 6), y)], fill=acc, width=mm(.5))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(68), lab, fit_font(d, lab, OSW7, mm(W - 12), mm(5)), self.ink, spacing=mm(.5))
        text_c(d, mm(W) / 2, mm(80), 'SINCE 1951  ·  20', F(OSW4, mm(2.8)), self.ink)
        return img


class PyotrVar(TallDesign):
    key, name = 'pyotr', 'Pyotr II'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        d.rectangle([mm(2.5), mm(2.5), mm(W - 2.5), mm(H - 2.5)], outline=acc, width=mm(.5))
        # a sailing ship: hull, three masts, square sails
        cx, cy = mm(W / 2), mm(30)
        d.polygon([(cx - mm(15), cy + mm(6)), (cx + mm(15), cy + mm(6)), (cx + mm(11), cy + mm(11)), (cx - mm(12), cy + mm(11))], fill=acc)
        for mx, hh in ((-8, 16), (0, 20), (8, 15)):
            x = cx + mm(mx)
            d.line([(x, cy + mm(6)), (x, cy + mm(6 - hh))], fill=acc, width=mm(.5))
            for k in range(2):
                y0 = cy + mm(6 - hh + 2 + k * 7)
                d.polygon([(x - mm(4.5), y0), (x + mm(4.5), y0), (x + mm(4), y0 + mm(5.5)), (x - mm(4), y0 + mm(5.5))], fill=acc)
        for k in range(3):
            y = cy + mm(13 + k * 1.6)
            d.line([(mm(6), y), (mm(W - 6), y)], fill=darker(acc, .8), width=mm(.3))
        text_c(d, mm(W) / 2, mm(55), 'PYOTR', fit_font(d, 'PYOTR', CIN9, mm(W - 10), mm(10)), self.ink, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(64), 'II', F(CIN9, mm(6)), acc)
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(73), lab, fit_font(d, lab, CIN7, mm(W - 14), mm(4)), self.ink, spacing=mm(.5))
        text_c(d, mm(W) / 2, mm(82), 'THE SECOND, NOT THE GREAT', F(CIN7, mm(2.2)), acc)
        return img


class OptimmaVar(TallDesign):
    key, name = 'optimma', 'Optimma'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        # two swooshes
        for k, (off, wd) in enumerate(((0, 5), (7, 2.5))):
            pts = [(mm(-2 + t * .6), mm(48 + off - 18 * math.sin(t / 100 * math.pi))) for t in range(0, 101, 4)]
            d.line(pts, fill=acc if k == 0 else darker(acc, .7), width=mm(wd))
        text_c(d, mm(W) / 2, mm(22), 'OPTIMMA', fit_font(d, 'OPTIMMA', OSW7, mm(W - 10), mm(11)), acc, spacing=mm(.4))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(66), lab, fit_font(d, lab, OSW7, mm(W - 14), mm(6)), acc, spacing=mm(.6))
        d.rectangle([0, mm(H - 10), mm(W), mm(H)], fill=acc)
        text_c(d, mm(W) / 2, mm(H - 5), 'OPTIMAL CHOICE  ·  20', F(OSW4, mm(2.8)), self.base)
        return img


class ChesterfeldVar(TallDesign):
    key, name = 'chesterfeld', 'Chesterfeld'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        d.rectangle([mm(4), mm(14), mm(W - 4), mm(60)], fill=acc)
        d.rectangle([mm(5), mm(15), mm(W - 5), mm(59)], outline=self.base, width=mm(.4))
        crest(d, mm(W / 2), mm(28), mm(7), self.base, acc, 'C', F(SERIF9, mm(7)))
        text_c(d, mm(W) / 2, mm(45), 'Chesterfeld', fit_font(d, 'Chesterfeld', SCRIPT, mm(W - 12), mm(14)), self.base)
        text_c(d, mm(W) / 2, mm(54), 'BLEND OF COLD TOBACCOS', fit_font(d, 'BLEND OF COLD TOBACCOS', CIN7, mm(W - 14), mm(2.6)), self.base)
        lab = self.v['label']
        ink = acc if lum(self.base) < 140 else self.ink
        text_c(d, mm(W) / 2, mm(70), lab, fit_font(d, lab, OSW7, mm(W - 14), mm(5)), ink, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(81), '20 CIGARETTES', F(OSW4, mm(2.8)), ink)
        return img


class PhillipVar(TallDesign):
    key, name = 'phillip', 'Phillip Morose'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        d.rectangle([0, 0, mm(W), mm(30)], fill=acc)
        # a crest with a little rain cloud: morose
        cx, cy = mm(W / 2), mm(16)
        for dx, r in ((-4, 4), (0, 5.5), (4.5, 4)):
            d.ellipse([cx + mm(dx) - mm(r), cy - mm(r), cx + mm(dx) + mm(r), cy + mm(r) * .7], fill=self.base)
        for k in range(5):
            x = cx + mm(-6 + k * 3)
            d.line([(x, cy + mm(5)), (x - mm(1), cy + mm(9))], fill=self.base, width=mm(.5))
        text_c(d, mm(W) / 2, mm(40), 'PHILLIP', fit_font(d, 'PHILLIP', SERIF9, mm(W - 12), mm(10)), self.ink)
        text_c(d, mm(W) / 2, mm(51), 'MOROSE', fit_font(d, 'MOROSE', SERIF9, mm(W - 12), mm(10)), acc)
        d.line([(mm(10), mm(58)), (mm(W - 10), mm(58))], fill=acc, width=mm(.4))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(66), lab, fit_font(d, lab, CIN7, mm(W - 14), mm(4.4)), self.ink, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(80), 'IT WAS BETTER BEFORE', F(CIN7, mm(2.4)), acc)
        return img


class WesstVar(TallDesign):
    key, name = 'wesst', 'Wesst'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        d.rectangle([0, mm(18), mm(W), mm(52)], fill=acc)
        text_c(d, mm(W) / 2, mm(35), 'WESST', fit_font(d, 'WESST', BEBAS, mm(W - 4), mm(26)), self.base)
        d.polygon([(mm(W - 16), mm(56)), (mm(W - 6), mm(61)), (mm(W - 16), mm(66))], fill=acc)
        text_c(d, mm(W) / 2 - mm(4), mm(61), 'GO WESST', F(OSW7, mm(4)), acc, spacing=mm(.4))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(72), lab, fit_font(d, lab, OSW7, mm(W - 14), mm(5)), self.ink, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(82), '20 CIGARETTES', F(OSW4, mm(2.8)), self.ink)
        return img


class LambertVar(TallDesign):
    key, name = 'lambert', 'Lambert & Bottler'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        base = self.base
        for y in range(mm(H)):
            k = .86 + .22 * (y / mm(H))
            d.line([(0, y), (mm(W), y)], fill=tuple(min(255, int(c * k)) for c in base[:3]) + (255,))
        acc = rgba(self.p['accent'])
        d.rectangle([mm(6), mm(10), mm(W - 6), mm(48)], outline=acc, width=mm(.5))
        text_c(d, mm(W) / 2, mm(20), 'LAMBERT', fit_font(d, 'LAMBERT', CIN9, mm(W - 16), mm(7)), acc, spacing=mm(.4))
        text_c(d, mm(W) / 2, mm(29), '&', F(SCRIPT, mm(9)), acc)
        text_c(d, mm(W) / 2, mm(38), 'BOTTLER', fit_font(d, 'BOTTLER', CIN9, mm(W - 16), mm(7)), acc, spacing=mm(.4))
        # a little bottle between the lines
        bx, by = mm(W / 2), mm(58)
        d.rounded_rectangle([bx - mm(3), by - mm(2), bx + mm(3), by + mm(8)], radius=mm(1), fill=acc)
        d.rectangle([bx - mm(1), by - mm(6), bx + mm(1), by - mm(2)], fill=acc)
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(73), lab, fit_font(d, lab, CIN7, mm(W - 14), mm(4.4)), self.ink, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(82), 'KING SIZE  ·  20', F(CIN7, mm(2.4)), self.ink)
        return img


class JpsVar(TallDesign):
    key, name = 'jps', 'John Prayer Special'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=acc, width=mm(.35))
        # the interlaced monogram
        cx, cy = mm(W / 2), mm(32)
        f = F(SERIF9, mm(16))
        for dx, ch in ((-7.5, 'J'), (0, 'P'), (7.5, 'S')):
            text_c(d, cx + mm(dx), cy, ch, f, acc)
        d.line([(mm(9), mm(44)), (mm(W - 9), mm(44))], fill=acc, width=mm(.35))
        text_c(d, mm(W) / 2, mm(50), 'JOHN PRAYER', fit_font(d, 'JOHN PRAYER', CIN7, mm(W - 14), mm(4.4)), acc, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(56), 'SPECIAL', fit_font(d, 'SPECIAL', CIN7, mm(W - 14), mm(4.4)), acc, spacing=mm(1.2))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(70), lab, fit_font(d, lab, CIN7, mm(W - 16), mm(4)), acc, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(82), 'PRAY IT IS NOT YOUR LAST', F(CIN7, mm(2.2)), acc)
        return img


class MayfireVar(TallDesign):
    key, name = 'mayfire', 'Mayfire'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        flame(d, mm(W / 2), mm(28), mm(10), acc, (250, 220, 120, 255))
        text_c(d, mm(W) / 2, mm(52), 'MAYFIRE', fit_font(d, 'MAYFIRE', SERIF9, mm(W - 8), mm(12)), self.ink)
        d.line([(mm(10), mm(60)), (mm(W - 10), mm(60))], fill=acc, width=mm(.6))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(68), lab, fit_font(d, lab, OSW7, mm(W - 14), mm(5)), self.ink, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(81), 'KEEPS THE FIRE  ·  20', F(OSW4, mm(2.6)), acc)
        return img


class SilkKutVar(TallDesign):
    key, name = 'silkkut', 'Silk Kut'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        # a sheet of silk with a cut: wavy bands with a slanted gap
        for k in range(10):
            y0 = mm(14 + k * 3.4)
            pts = [(mm(t * .55), y0 + mm(1.6) * math.sin(t / 100 * math.pi * 3 + k * .5)) for t in range(0, 101, 4)]
            d.line(pts, fill=darker(self.base, .82 + .03 * (k % 3)), width=mm(2.6))
        d.polygon([(mm(10), mm(12)), (mm(16), mm(12)), (mm(W - 10), mm(50)), (mm(W - 16), mm(50))], fill=acc)
        text_c(d, mm(W) / 2, mm(60), 'SILK KUT', fit_font(d, 'SILK KUT', SERIF7, mm(W - 10), mm(10)), acc)
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(70), lab, fit_font(d, lab, CIN7, mm(W - 14), mm(4.4)), acc, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(81), 'SMOOTH AS FROST  ·  20', F(CIN7, mm(2.3)), acc)
        return img


class StuyvesandVar(TallDesign):
    key, name = 'stuyvesand', 'Peter Stuyvesand'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        for k in range(5):
            y = mm(8 + k * 3.2)
            d.rectangle([0, y, mm(W), y + mm(1.8)], fill=acc if k % 2 == 0 else self.ink)
        d.rectangle([0, mm(H - 18), mm(W), mm(H - 16.2)], fill=self.ink)
        d.rectangle([0, mm(H - 14), mm(W), mm(H - 8)], fill=acc)
        text_c(d, mm(W) / 2, mm(36), 'PETER', fit_font(d, 'PETER', SERIF7, mm(W - 16), mm(7)), self.ink)
        text_c(d, mm(W) / 2, mm(47), 'STUYVESAND', fit_font(d, 'STUYVESAND', SERIF9, mm(W - 8), mm(9)), acc)
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(59), lab, fit_font(d, lab, OSW7, mm(W - 14), mm(5)), self.ink, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(H - 11), 'THE SAND GETS EVERYWHERE', fit_font(d, 'THE SAND GETS EVERYWHERE', OSW4, mm(W - 10), mm(2.6)), self.base)
        return img


class VicerroyVar(TallDesign):
    key, name = 'vicerroy', 'Vicerroy'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        # a pointed shield with a chevron
        cx, cy = mm(W / 2), mm(28)
        d.polygon([(cx - mm(12), cy - mm(12)), (cx + mm(12), cy - mm(12)), (cx + mm(12), cy + mm(2)), (cx, cy + mm(14)), (cx - mm(12), cy + mm(2))], fill=acc)
        d.polygon([(cx - mm(10), cy + mm(1)), (cx, cy - mm(7)), (cx + mm(10), cy + mm(1)), (cx + mm(10), cy + mm(5)), (cx, cy - mm(3)), (cx - mm(10), cy + mm(5))], fill=self.base)
        text_c(d, mm(W) / 2, mm(52), 'VICERROY', fit_font(d, 'VICERROY', OSW7, mm(W - 8), mm(10)), self.ink, spacing=mm(.6))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(64), lab, fit_font(d, lab, OSW4, mm(W - 14), mm(5)), self.ink, spacing=mm(.6))
        d.rectangle([mm(6), mm(72), mm(W - 6), mm(73)], fill=acc)
        text_c(d, mm(W) / 2, mm(79), 'KING SIZE  ·  20', F(OSW4, mm(2.8)), self.ink)
        return img


class SalemmVar(TallDesign):
    key, name = 'salemm', 'Salemm'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        # a pointed witch's hat over a crescent moon
        cx, cy = mm(W / 2), mm(30)
        d.ellipse([cx + mm(2), cy - mm(18), cx + mm(14), cy - mm(6)], fill=acc)
        d.ellipse([cx + mm(5), cy - mm(19), cx + mm(17), cy - mm(7)], fill=self.base)
        d.polygon([(cx - mm(6), cy + mm(6)), (cx - mm(1), cy - mm(16)), (cx + mm(6), cy + mm(6))], fill=acc)
        d.ellipse([cx - mm(14), cy + mm(4), cx + mm(14), cy + mm(10)], fill=acc)
        text_c(d, mm(W) / 2, mm(52), 'Salemm', fit_font(d, 'Salemm', SERIF9, mm(W - 10), mm(13)), acc)
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(64), lab, fit_font(d, lab, OSW7, mm(W - 14), mm(5)), acc, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(80), 'MENTHOL  ·  20', F(OSW4, mm(2.8)), acc)
        return img


def penguin(d, cx, cy, r, dark, light, beak):
    d.ellipse([cx - r * .6, cy - r * .5, cx + r * .6, cy + r], fill=dark)
    d.ellipse([cx - r * .4, cy - r * .2, cx + r * .4, cy + r * .9], fill=light)
    d.ellipse([cx - r * .42, cy - r * 1.05, cx + r * .42, cy - r * .25], fill=dark)
    for sx in (-1, 1):
        d.ellipse([cx + sx * r * .18 - r * .08, cy - r * .75, cx + sx * r * .18 + r * .08, cy - r * .6], fill=light)
        d.polygon([(cx + sx * r * .55, cy - r * .2), (cx + sx * r * .85, cy + r * .5), (cx + sx * r * .5, cy + r * .4)], fill=dark)
        d.ellipse([cx + sx * r * .3 - r * .2, cy + r * .9, cx + sx * r * .3 + r * .2, cy + r * 1.05], fill=beak)
    d.polygon([(cx - r * .12, cy - r * .52), (cx + r * .12, cy - r * .52), (cx, cy - r * .3)], fill=beak)


class KoollVar(TallDesign):
    key, name = 'kooll', 'Kooll'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        penguin(d, mm(W / 2), mm(28), mm(11), (24, 26, 30, 255), acc, (240, 170, 40, 255))
        text_c(d, mm(W) / 2, mm(55), 'KOOLL', fit_font(d, 'KOOLL', BEBAS, mm(W - 8), mm(15)), acc, spacing=mm(.6))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(67), lab, fit_font(d, lab, OSW7, mm(W - 14), mm(5)), self.ink, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(80), 'STAY KOOLL  ·  20', F(OSW4, mm(2.8)), self.ink)
        return img


class SpirritVar(TallDesign):
    key, name = 'spirrit', 'American Spirrit'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        # a sun over mountains and a single feather
        cx, cy = mm(W / 2), mm(30)
        for k in range(16):
            a = math.radians(k * 22.5)
            d.line([(cx + math.cos(a) * mm(8), cy + math.sin(a) * mm(8)), (cx + math.cos(a) * mm(14), cy + math.sin(a) * mm(14))], fill=acc, width=mm(.7))
        d.ellipse([cx - mm(7), cy - mm(7), cx + mm(7), cy + mm(7)], fill=acc)
        d.polygon([(0, mm(46)), (mm(14), mm(34)), (mm(24), mm(42)), (mm(36), mm(30)), (mm(W), mm(44)), (mm(W), mm(48)), (0, mm(48))], fill=self.ink)
        text_c(d, mm(W) / 2, mm(55), 'AMERICAN', fit_font(d, 'AMERICAN', SERIF7, mm(W - 14), mm(5.6)), self.ink, spacing=mm(.4))
        text_c(d, mm(W) / 2, mm(63), 'SPIRRIT', fit_font(d, 'SPIRRIT', SERIF9, mm(W - 10), mm(9)), self.ink)
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(72), lab, fit_font(d, lab, OSW7, mm(W - 14), mm(4.6)), acc, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(81), 'ADDITIVE FREE, NOT HARM FREE', fit_font(d, 'ADDITIVE FREE, NOT HARM FREE', OSW4, mm(W - 8), mm(2.4)), self.ink)
        return img


class MewiusVar(TallDesign):
    key, name = 'mewius', 'Mewius'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        # an abstract wing: three stacked swept arcs; and a tiny cat ear on the M
        for k in range(3):
            off = k * 4
            pts = [(mm(10 + t * .38), mm(36 - off - 14 * math.sin(t / 100 * math.pi * .9))) for t in range(0, 101, 4)]
            d.line(pts, fill=acc if k == 0 else darker(acc, 1.0 - k * .12), width=mm(2.2 - k * .5))
        text_c(d, mm(W) / 2, mm(52), 'MEWIUS', fit_font(d, 'MEWIUS', OSW7, mm(W - 10), mm(10)), self.ink, spacing=mm(1.0))
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(63), lab, fit_font(d, lab, OSW4, mm(W - 14), mm(4.6)), acc, spacing=mm(.6))
        d.rectangle([0, mm(H - 8), mm(W), mm(H)], fill=acc)
        text_c(d, mm(W) / 2, mm(H - 4), 'MEOW  ·  20', F(OSW4, mm(2.6)), self.base)
        return img


class HoppVar(TallDesign):
    key, name = 'hopp', 'Hopp'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        # a bow with an arrow aimed up and to the right
        cx, cy = mm(W / 2), mm(30)
        d.arc([cx - mm(14), cy - mm(14), cx + mm(14), cy + mm(14)], 135, 315, fill=acc, width=mm(1.2))
        a1 = (cx + mm(14) * math.cos(math.radians(135)), cy + mm(14) * math.sin(math.radians(135)))
        a2 = (cx + mm(14) * math.cos(math.radians(315)), cy + mm(14) * math.sin(math.radians(315)))
        d.line([a1, a2], fill=self.ink, width=mm(.3))
        d.line([(cx - mm(8), cy + mm(8)), (cx + mm(14), cy - mm(14))], fill=self.ink, width=mm(.6))
        tip = (cx + mm(14), cy - mm(14))
        d.polygon([tip, (tip[0] - mm(4), tip[1] + mm(1)), (tip[0] - mm(1), tip[1] + mm(4))], fill=self.ink)
        text_c(d, mm(W) / 2, mm(56), 'HOPP', fit_font(d, 'HOPP', SERIF9, mm(W - 12), mm(16)), self.ink)
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(68), lab, fit_font(d, lab, OSW7, mm(W - 14), mm(4.6)), acc, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(80), 'HOPP DIES LAST  ·  20', F(OSW4, mm(2.6)), self.ink)
        return img


class MurattyVar(TallDesign):
    key, name = 'muratty', 'Muratty'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        d.rectangle([mm(2.5), mm(2.5), mm(W - 2.5), mm(H - 2.5)], outline=acc, width=mm(.8))
        d.rectangle([mm(4), mm(4), mm(W - 4), mm(H - 4)], outline=acc, width=mm(.3))
        emblem(d, mm(W / 2), mm(28), mm(9), acc)
        text_c(d, mm(W) / 2, mm(51), 'MURATTY', fit_font(d, 'MURATTY', CIN9, mm(W - 12), mm(8)), acc, spacing=mm(.6))
        ornament(d, mm(W) / 2, mm(58), mm(W - 18), acc)
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(66), lab, fit_font(d, lab, CIN7, mm(W - 14), mm(4.4)), self.ink, spacing=mm(.4))
        text_c(d, mm(W) / 2, mm(80), 'MADE FOR DIPLOMATS', F(CIN7, mm(2.4)), acc)
        return img


class SovereingVar(TallDesign):
    key, name = 'sovereing', 'Sovereing'

    def design(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        acc = rgba(self.p['accent'])
        # a gold coin with a profile and a milled edge
        cx, cy, r = mm(W / 2), mm(30), mm(13)
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=acc)
        for k in range(48):
            a = math.radians(k * 7.5)
            d.line([(cx + math.cos(a) * r * .9, cy + math.sin(a) * r * .9), (cx + math.cos(a) * r, cy + math.sin(a) * r)], fill=darker(acc, .7), width=max(1, mm(.2)))
        d.ellipse([cx - r * .78, cy - r * .78, cx + r * .78, cy + r * .78], outline=darker(acc, .7), width=mm(.3))
        dk = darker(acc, .62)
        d.ellipse([cx - r * .3, cy - r * .5, cx + r * .3, cy + r * .15], fill=dk)
        d.polygon([(cx - r * .3, cy - r * .05), (cx - r * .42, cy + r * .05), (cx - r * .28, cy + r * .12)], fill=dk)
        d.pieslice([cx - r * .5, cy + r * .05, cx + r * .5, cy + r * .9], 180, 360, fill=dk)
        text_c(d, mm(W) / 2, mm(53), 'SOVEREING', fit_font(d, 'SOVEREING', SERIF9, mm(W - 8), mm(9)), acc)
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(64), lab, fit_font(d, lab, OSW7, mm(W - 14), mm(5)), self.ink, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(80), 'WORTH ITS WEIGHT  ·  20', F(OSW4, mm(2.6)), acc)
        return img


CLASSES.update({'kosmoss': KosmossVar, 'primma': PrimmaVar, 'pyotr': PyotrVar, 'optimma': OptimmaVar, 'chesterfeld': ChesterfeldVar,
                'phillip': PhillipVar, 'wesst': WesstVar, 'lambert': LambertVar, 'jps': JpsVar, 'mayfire': MayfireVar, 'silkkut': SilkKutVar,
                'stuyvesand': StuyvesandVar, 'vicerroy': VicerroyVar, 'salemm': SalemmVar, 'kooll': KoollVar, 'spirrit': SpirritVar,
                'mewius': MewiusVar, 'hopp': HoppVar, 'muratty': MurattyVar, 'sovereing': SovereingVar})


# ---------------------------------------------------------------- batch 7 (11-14): Canada, USSR / Russia, the world, three new shapes
def maple(d, cx, cy, r, col):
    """A maple leaf (half outline mirrored)."""
    half = [(0, -1.0), (.12, -.72), (.3, -.8), (.26, -.42), (.56, -.56), (.5, -.3), (.9, -.28), (.72, -.06), (.96, .12), (.6, .22),
            (.66, .44), (.3, .36), (.1, .5), (.06, .56), (.06, 1.0)]
    pts = [(cx + x * r, cy + y * r) for x, y in half] + [(cx - x * r, cy + y * r) for x, y in reversed(half)]
    d.polygon(pts, fill=col)


def peaks(d, x0, x1, base, tops, col, snow=None):
    """A mountain range: tops = [(x share, height mm)]."""
    pts = [(x0, base)]
    w = x1 - x0
    for k, (xs, h) in enumerate(tops):
        if k:
            px, ph = tops[k - 1]
            pts.append((x0 + w * (px + xs) / 2, base - mm(min(ph, h) * .45)))
        pts.append((x0 + w * xs, base - mm(h)))
    pts.append((x1, base))
    d.polygon(pts, fill=col)
    if snow:
        for xs, h in tops:
            x, y = x0 + w * xs, base - mm(h)
            d.polygon([(x, y), (x + mm(h * .22), y + mm(h * .3)), (x + mm(h * .06), y + mm(h * .24)), (x - mm(h * .04), y + mm(h * .34)),
                       (x - mm(h * .2), y + mm(h * .28))], fill=snow)


def rider(d, cx, cy, s, col):
    """A galloping horseman (silhouette), s = mm per unit."""
    def P(pts):
        d.polygon([(cx + mm(x * s), cy + mm(y * s)) for x, y in pts], fill=col)
    P([(-5, -1), (4, -1.6), (5.4, .2), (4, 1.6), (-4.6, 1.4), (-5.6, .2)])                 # body
    P([(3.6, -1.4), (6.4, -5.2), (8.4, -5.4), (9.4, -4), (8, -3.4), (7.2, -3.8), (5.6, -.2)])  # neck and head
    P([(-5.2, -.6), (-8.6, -2.6), (-9.4, -1), (-5.6, .6)])                                # tail
    for a, b in (((4, 1), (7.6, 3.4)), ((7.6, 3.4), (8.6, 2.2)), ((3, 1.2), (5.2, 4.8)), ((-3.6, 1.2), (-7, 3.6)), ((-7, 3.6), (-8.2, 5)),
                 ((-4.4, 1.2), (-5, 5))):
        d.line([(cx + mm(a[0] * s), cy + mm(a[1] * s)), (cx + mm(b[0] * s), cy + mm(b[1] * s))], fill=col, width=max(2, mm(.9 * s)))
    P([(-.6, -1.4), (.2, -5.6), (2, -5.4), (2.6, -1.6)])                                   # rider
    r = mm(1.2 * s)
    d.ellipse([cx + mm(1.4 * s) - r, cy + mm(-6.8 * s) - r, cx + mm(1.4 * s) + r, cy + mm(-6.8 * s) + r], fill=col)
    P([(0, -8.2), (2.8, -8.2), (2.4, -7.2), (.4, -7.2)])                                   # the tall cap
    P([(-.4, -5.2), (-4.4, -3), (-3.8, -1.2), (.4, -3.6)])                                 # the cloak flying back


def plane(img, cx, cy, s, col, ang):
    """An airliner seen from the side, turned by ang degrees."""
    lay = Image.new('RGBA', img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)

    def P(pts):
        d.polygon([(cx + mm(x * s), cy + mm(y * s)) for x, y in pts], fill=col)
    P([(-10, -.9), (7, -1.1), (10, 0), (7, 1.1), (-10, .7), (-11, 0)])      # fuselage
    P([(-1, .4), (-6.5, 6), (-4.2, 6), (3, .6)])                           # near wing, swept
    P([(-9.6, -.8), (-12.6, -5.4), (-10.8, -5.4), (-7, -.9)])              # fin
    P([(-12.9, -5.6), (-8.6, -5.6), (-8.2, -4.9), (-12.5, -4.9)])          # T tail
    P([(-8.4, -.4), (-6, -2.2), (-3.6, -2.2), (-3.2, -.6)])                # engines at the tail
    img.alpha_composite(lay.rotate(ang, center=(cx, cy), resample=Image.BICUBIC))


def hand(d, cx, cy, s, col):
    """An open hand, palm out."""
    d.rounded_rectangle([cx - mm(5 * s), cy - mm(2 * s), cx + mm(5 * s), cy + mm(9 * s)], radius=mm(2.4 * s), fill=col)
    for k, (x, h) in enumerate(((-3.9, 8.5), (-1.3, 10.5), (1.3, 10), (3.9, 8))):
        d.rounded_rectangle([cx + mm((x - 1.1) * s), cy - mm(h * s), cx + mm((x + 1.1) * s), cy + mm(1 * s)], radius=mm(1.1 * s), fill=col)
    d.polygon([(cx - mm(4.6 * s), cy + mm(2 * s)), (cx - mm(9.2 * s), cy - mm(2.4 * s)), (cx - mm(7.6 * s), cy - mm(4.2 * s)),
               (cx - mm(3.4 * s), cy - mm(.6 * s))], fill=col)            # thumb


def joy(d, cx, cy, s, col):
    """A paired sign of joy drawn from bars and boxes (two halves side by side, joined by long bars)."""
    w = max(2, mm(.9 * s))

    def bar(x0, y0, x1, y1):
        d.line([(cx + mm(x0 * s), cy + mm(y0 * s)), (cx + mm(x1 * s), cy + mm(y1 * s))], fill=col, width=w)

    def box(x0, y0, x1, y1):
        d.rectangle([cx + mm(x0 * s), cy + mm(y0 * s), cx + mm(x1 * s), cy + mm(y1 * s)], outline=col, width=w)
    bar(-9.5, -8, 9.5, -8); bar(-9.5, 1.6, 9.5, 1.6); bar(-9.5, -3.2, 9.5, -3.2)
    for sx in (-1, 1):
        x = sx * 4.8
        bar(x, -10.4, x, -3.2)
        bar(x - 3.4, -10.4 + 1.2, x + 3.4, -10.4 + 1.2)
        box(x - 2.8, -6.8, x + 2.8, -4.6)
        bar(x - 2.2, -1.9, x - 1.2, -.2); bar(x + 2.2, -1.9, x + 1.2, -.2)
        box(x - 3, 3.6, x + 3, 8.6)
        bar(x - 3, 6.1, x + 3, 6.1)


class Batch7(TallDesign):
    """Shared bits of the batch: the kind's name and the bottom line."""

    def start(self, W, H):
        img = face(); d = ImageDraw.Draw(img)
        d.rectangle([0, 0, mm(W), mm(H)], fill=self.base)
        return img, d, rgba(self.p['accent'])

    def kind(self, d, W, y, font=OSW7, size=4.6, col=None, margin=12):
        lab = self.v['label']
        text_c(d, mm(W) / 2, mm(y), lab, fit_font(d, lab, font, mm(W - margin), mm(size)), col or self.ink, spacing=mm(.5))


class ExportEhVar(Batch7):
    key, name = 'exporteh', 'Export Eh'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=acc, width=mm(.5))
        maple(d, mm(W / 2), mm(21), mm(11), acc)
        text_c(d, mm(W) / 2, mm(44), 'EXPORT', fit_font(d, 'EXPORT', BEBAS, mm(W - 12), mm(15)), self.ink, spacing=mm(.8))
        r = mm(7.5)
        d.ellipse([mm(W / 2) - r, mm(60) - r, mm(W / 2) + r, mm(60) + r], fill=acc)
        text_c(d, mm(W) / 2, mm(59.6), 'Eh', F(ABRIL, mm(8.4)), self.base)
        self.kind(d, W, 73.5, col=self.ink)
        text_c(d, mm(W) / 2, mm(81), 'CANADIAN BLEND  ·  20', F(OSW4, mm(2.6)), acc)
        return img


class DuMorrierVar(Batch7):
    key, name = 'dumorrier', 'du Morrier'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=acc, width=mm(.7))
        d.rectangle([mm(4.6), mm(4.6), mm(W - 4.6), mm(H - 4.6)], outline=acc, width=mm(.25))
        d.regular_polygon((mm(W / 2), mm(20), mm(5.2)), 4, rotation=45, outline=acc, width=mm(.5))
        text_c(d, mm(W) / 2, mm(19.6), 'dM', F(SERIF7, mm(4.2)), acc)
        text_c(d, mm(W) / 2, mm(42), 'du Morrier', fit_font(d, 'du Morrier', SCRIPT, mm(W - 10), mm(15)), self.ink)
        ornament(d, mm(W / 2), mm(53), mm(34), acc)
        self.kind(d, W, 62, font=CIN7, size=4.4, col=self.ink, margin=16)
        text_c(d, mm(W) / 2, mm(78), 'KING SIZE  ·  20', F(CIN7, mm(2.6)), acc, spacing=mm(.6))
        return img


class PlayerzVar(Batch7):
    key, name = 'playerz', 'Playerz'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        cx, cy, r = mm(W / 2), mm(27), mm(15)
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=acc)
        d.ellipse([cx - r * .72, cy - r * .72, cx + r * .72, cy + r * .72], fill=self.base)
        for a in range(0, 360, 90):                       # the lifebuoy's rope bands
            x, y = cx + r * .86 * math.cos(math.radians(a + 45)), cy + r * .86 * math.sin(math.radians(a + 45))
            d.ellipse([x - r * .13, y - r * .13, x + r * .13, y + r * .13], fill=self.base)
        # the sailor: face, beard, cap with a band
        d.ellipse([cx - r * .3, cy - r * .34, cx + r * .3, cy + r * .3], fill=acc)
        d.pieslice([cx - r * .42, cy - r * .2, cx + r * .42, cy + r * .62], 0, 180, fill=acc)
        d.rectangle([cx - r * .4, cy - r * .5, cx + r * .4, cy - r * .3], fill=acc)
        d.ellipse([cx - r * .46, cy - r * .64, cx + r * .46, cy - r * .38], fill=acc)
        d.rectangle([cx - r * .4, cy - r * .42, cx + r * .4, cy - r * .36], fill=self.base)
        for sx in (-1, 1):                                # two ships on the horizon
            x = cx + sx * mm(21)
            d.polygon([(x - mm(3), mm(38)), (x + mm(3), mm(38)), (x + mm(2.2), mm(40)), (x - mm(2.2), mm(40))], fill=acc)
            d.polygon([(x, mm(31)), (x + mm(2.4), mm(37.4)), (x, mm(37.4))], fill=acc)
        text_c(d, mm(W) / 2, mm(54), 'PLAYERZ', fit_font(d, 'PLAYERZ', SERIF9, mm(W - 8), mm(11)), self.ink)
        d.line([(mm(8), mm(61.5)), (mm(W - 8), mm(61.5))], fill=acc, width=mm(.5))
        self.kind(d, W, 68, font=CIN9, size=5.4, col=self.ink)
        text_c(d, mm(W) / 2, mm(80), 'CIGARETTES  ·  20', F(CIN7, mm(2.6)), acc, spacing=mm(.6))
        return img


class KazbekkVar(Batch7):
    key, name = 'kazbekk', 'Kazbekk'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        far = tuple(int(a * .45 + b * .55) for a, b in zip(acc[:3], self.base[:3])) + (255,)
        peaks(d, 0, mm(W), mm(52), [(.14, 22), (.44, 36), (.74, 27), (.96, 16)], far, snow=self.base)
        d.polygon([(0, mm(52)), (mm(14), mm(46)), (mm(30), mm(48.5)), (mm(46), mm(45)), (mm(W), mm(49)), (mm(W), mm(56)), (0, mm(56))], fill=acc)
        rider(d, mm(29), mm(41.6), .8, acc)
        d.rectangle([0, mm(56), mm(W), mm(H)], fill=acc)
        light = self.base
        text_c(d, mm(W) / 2, mm(64.5), 'KAZBEKK', fit_font(d, 'KAZBEKK', ABRIL, mm(W - 8), mm(11)), light)
        self.kind(d, W, 74, size=4.2, col=light)
        text_c(d, mm(W) / 2, mm(82), 'PAPIROSY  ·  20', F(OSW4, mm(2.6)), light)
        return img


class GertsegovinaVar(Batch7):
    key, name = 'gertsegovina', 'Gertsegovina Flor'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        for k, wd in ((3, .7), (4.8, .25)):
            d.rectangle([mm(k), mm(k), mm(W - k), mm(H - k)], outline=acc, width=mm(wd))
        for x, y in ((6.5, 6.5), (W - 6.5, 6.5), (6.5, H - 6.5), (W - 6.5, H - 6.5)):
            d.regular_polygon((mm(x), mm(y), mm(1.6)), 4, rotation=45, fill=acc)
        cx, cy = mm(W / 2), mm(24)
        for a in range(0, 360, 45):                       # an eight-petal flower
            x, y = cx + mm(6.2) * math.cos(math.radians(a)), cy + mm(6.2) * math.sin(math.radians(a))
            d.ellipse([x - mm(3.3), y - mm(3.3), x + mm(3.3), y + mm(3.3)], fill=acc)
        d.ellipse([cx - mm(3.2), cy - mm(3.2), cx + mm(3.2), cy + mm(3.2)], fill=self.base)
        d.ellipse([cx - mm(1.7), cy - mm(1.7), cx + mm(1.7), cy + mm(1.7)], fill=acc)
        text_c(d, mm(W) / 2, mm(44), 'GERTSEGOVINA', fit_font(d, 'GERTSEGOVINA', CIN9, mm(W - 12), mm(6)), self.ink, spacing=mm(.3))
        text_c(d, mm(W) / 2, mm(54.5), 'FLOR', fit_font(d, 'FLOR', SERIF9, mm(W - 22), mm(13)), acc)
        ornament(d, mm(W / 2), mm(64), mm(32), acc)
        self.kind(d, W, 70.5, font=CIN7, size=4, col=self.ink, margin=18)
        text_c(d, mm(W) / 2, mm(79), 'PAPIROSY  ·  20', F(CIN7, mm(2.4)), acc, spacing=mm(.6))
        return img


class PomirVar(Batch7):
    key, name = 'pomir', 'Pomir'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        dark = darker(self.base, .62)
        peaks(d, 0, mm(W), mm(54), [(.1, 20), (.36, 34), (.62, 26), (.9, 38)], dark, snow=acc)
        d.polygon([(0, mm(54)), (mm(20), mm(47)), (mm(34), mm(50)), (mm(W), mm(44)), (mm(W), mm(58)), (0, mm(58))], fill=darker(self.base, .8))
        # the wanderer with a staff and a pack, small against the mountains
        x, y = mm(22), mm(46.5)
        d.line([(x, y - mm(6.4)), (x, y - mm(1.6))], fill=acc, width=mm(1.5))
        d.ellipse([x - mm(1.1), y - mm(8.8), x + mm(1.1), y - mm(6.6)], fill=acc)
        d.rectangle([x - mm(2.3), y - mm(6.4), x - mm(.7), y - mm(3)], fill=acc)
        d.line([(x, y - mm(1.8)), (x - mm(1.3), y + mm(1.2))], fill=acc, width=mm(.8))
        d.line([(x, y - mm(1.8)), (x + mm(1.5), y + mm(1))], fill=acc, width=mm(.8))
        d.line([(x + mm(.4), y - mm(5.2)), (x + mm(3.2), y - mm(4))], fill=acc, width=mm(.6))
        d.line([(x + mm(3.4), y - mm(9.5)), (x + mm(3), y + mm(1.4))], fill=acc, width=mm(.5))
        text_c(d, mm(W) / 2, mm(66), 'POMIR', fit_font(d, 'POMIR', SLAB, mm(W - 10), mm(12)), self.ink)
        self.kind(d, W, 75.5, size=3.8, col=self.ink)
        text_c(d, mm(W) / 2, mm(82.5), 'CLASS 4  ·  20', F(OSW4, mm(2.5)), acc)
        return img


class Tu143Var(Batch7):
    key, name = 'tu143', 'Tu-143'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.rectangle([0, mm(46), mm(W), mm(64)], fill=acc)
        d.line([(mm(4), mm(41)), (mm(31), mm(24.5))], fill=acc, width=mm(.5))      # contrails
        d.line([(mm(2), mm(44)), (mm(30), mm(26.8))], fill=acc, width=mm(.3))
        plane(img, mm(34), mm(22), .95, acc, 31)
        d = ImageDraw.Draw(img)
        d.ellipse([mm(W - 13), mm(6), mm(W - 5), mm(14)], outline=acc, width=mm(.5))
        text_c(d, mm(W) / 2, mm(54.6), 'TU-143', fit_font(d, 'TU-143', BEBAS, mm(W - 10), mm(14)), self.base, spacing=mm(.8))
        self.kind(d, W, 71, col=self.ink)
        text_c(d, mm(W) / 2, mm(81), 'FILTER  ·  20', F(OSW4, mm(2.8)), acc if lum(acc) < lum(self.base) - 40 or lum(acc) > lum(self.base) + 40 else self.ink)
        return img


class StewardesaVar(Batch7):
    key, name = 'stewardesa', 'Stewardesa'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        cx, cy = mm(W / 2), mm(25)
        for sx in (-1, 1):                                # a pair of wings, three feathers each
            for k in range(3):
                y = cy - mm(2.4) + mm(k * 2.6)
                d.polygon([(cx + sx * mm(6.5), y), (cx + sx * mm(22 - k * 4), y - mm(3.2 - k * .5)), (cx + sx * mm(20 - k * 4), y + mm(1.8)),
                           (cx + sx * mm(6.5), y + mm(2.2))], fill=acc)
        d.ellipse([cx - mm(7.5), cy - mm(7.5), cx + mm(7.5), cy + mm(7.5)], fill=acc)
        d.ellipse([cx - mm(6.3), cy - mm(6.3), cx + mm(6.3), cy + mm(6.3)], fill=self.base)
        # her head in profile under a pillbox cap
        d.ellipse([cx - mm(2.6), cy - mm(2.4), cx + mm(2.6), cy + mm(3.4)], fill=acc)
        d.polygon([(cx - mm(3.3), cy - mm(1.4)), (cx + mm(2.9), cy - mm(2.6)), (cx + mm(2.4), cy - mm(4.6)), (cx - mm(2.9), cy - mm(3.8))], fill=acc)
        d.pieslice([cx - mm(4.4), cy - mm(.6), cx + mm(.6), cy + mm(5.6)], 80, 250, fill=acc)
        text_c(d, mm(W) / 2, mm(48), 'Stewardesa', fit_font(d, 'Stewardesa', SCRIPT, mm(W - 6), mm(14)), self.ink)
        d.line([(mm(9), mm(57)), (mm(W - 9), mm(57))], fill=acc, width=mm(.5))
        self.kind(d, W, 64.5, font=CIN7, size=4.2, col=self.ink)
        text_c(d, mm(W) / 2, mm(79), 'WE WISH YOU A PLEASANT SMOKE  ·  20', fit_font(d, 'WE WISH YOU A PLEASANT SMOKE  ·  20', OSW4, mm(W - 8), mm(2.6)),
               acc if abs(lum(acc) - lum(self.base)) > 60 else self.ink)
        return img


class SoyuzVar(Batch7):
    key, name = 'soyuz', 'Soyuz Apollon'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        ink = self.ink
        rnd = random.Random(len(self.v['key']) * 5)
        for _ in range(26):
            x, y, r = rnd.uniform(mm(3), mm(W - 3)), rnd.uniform(mm(4), mm(40)), rnd.uniform(.15, .4)
            d.ellipse([x - mm(r), y - mm(r), x + mm(r), y + mm(r)], fill=ink)
        d.arc([mm(-20), mm(34), mm(W + 20), mm(110)], 180, 360, fill=acc, width=mm(1))     # the Earth's edge
        cy = mm(22)
        # left craft: a ball, a bell, two solar panels; right craft: a cylinder, a cone, an engine bell; they meet in the middle
        d.ellipse([mm(15), cy - mm(3), mm(21), cy + mm(3)], fill=acc)
        d.polygon([(mm(9), cy - mm(3.4)), (mm(15.6), cy - mm(2.4)), (mm(15.6), cy + mm(2.4)), (mm(9), cy + mm(3.4))], fill=acc)
        for sy in (-1, 1):
            d.rectangle([mm(10), cy + sy * mm(4.2) - mm(1), mm(14.6), cy + sy * mm(9) + mm(1)] if sy > 0 else [mm(10), cy - mm(10), mm(14.6), cy - mm(3.2)],
                        outline=acc, width=mm(.4))
            d.line([(mm(12.3), cy + sy * mm(3.2)), (mm(12.3), cy + sy * mm(10))], fill=acc, width=mm(.3))
        d.rectangle([mm(21), cy - mm(1.2), mm(27), cy + mm(1.2)], fill=ink)                  # the docking tunnel
        d.polygon([(mm(27), cy - mm(1.6)), (mm(31), cy - mm(3.6)), (mm(31), cy + mm(3.6)), (mm(27), cy + mm(1.6))], fill=ink)
        d.rectangle([mm(31), cy - mm(3.6), mm(42), cy + mm(3.6)], fill=ink)
        d.polygon([(mm(42), cy - mm(1.6)), (mm(47), cy - mm(3.8)), (mm(47), cy + mm(3.8)), (mm(42), cy + mm(1.6))], fill=ink)
        text_c(d, mm(W) / 2, mm(50), 'SOYUZ', fit_font(d, 'SOYUZ', BEBAS, mm(W - 16), mm(12)), acc, spacing=mm(.8))
        text_c(d, mm(W) / 2, mm(61), 'APOLLON', fit_font(d, 'APOLLON', BEBAS, mm(W - 16), mm(12)), ink, spacing=mm(.8))
        self.kind(d, W, 71.5, size=4.2, col=acc if abs(lum(acc) - lum(self.base)) > 60 else ink)
        text_c(d, mm(W) / 2, mm(81), 'JOINT BLEND  ·  20', F(OSW4, mm(2.6)), ink)
        return img


class RothHandelVar(Batch7):
    key, name = 'rothhandel', 'Roth-Handel'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=acc, width=mm(.6))
        hand(d, mm(W / 2 + 1.5), mm(26), 1.25, acc)
        text_c(d, mm(W) / 2, mm(49), 'ROTH-', fit_font(d, 'ROTH-', SLAB, mm(W - 16), mm(10)), self.ink)
        text_c(d, mm(W) / 2, mm(60), 'HANDEL', fit_font(d, 'HANDEL', SLAB, mm(W - 12), mm(10)), self.ink)
        self.kind(d, W, 71, size=4.4, col=acc if abs(lum(acc) - lum(self.base)) > 60 else self.ink)
        text_c(d, mm(W) / 2, mm(80.5), 'KRAFTIG  ·  20', F(OSW4, mm(2.7)), self.ink)
        return img


class DoubleHappyVar(Batch7):
    key, name = 'doublehappy', 'Double Happyness'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=acc, width=mm(.7))
        for x, y in ((3, 3), (W - 3, 3), (3, H - 3), (W - 3, H - 3)):                      # corner frets
            sx, sy = (1 if x < W / 2 else -1), (1 if y < H / 2 else -1)
            d.line([(mm(x + sx * 2), mm(y + sy * 6)), (mm(x + sx * 2), mm(y + sy * 2)), (mm(x + sx * 6), mm(y + sy * 2))], fill=acc, width=mm(.4))
        r = mm(16)
        d.ellipse([mm(W / 2) - r, mm(27) - r, mm(W / 2) + r, mm(27) + r], outline=acc, width=mm(.6))
        joy(d, mm(W / 2), mm(28), 1.1, acc)
        text_c(d, mm(W) / 2, mm(53), 'DOUBLE', fit_font(d, 'DOUBLE', CIN9, mm(W - 18), mm(7)), self.ink, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(61.5), 'HAPPYNESS', fit_font(d, 'HAPPYNESS', CIN9, mm(W - 12), mm(7)), self.ink, spacing=mm(.4))
        self.kind(d, W, 71, font=CIN7, size=3.8, col=acc if abs(lum(acc) - lum(self.base)) > 60 else self.ink, margin=14)
        text_c(d, mm(W) / 2, mm(80), 'TWICE THE JOY  ·  20', F(CIN7, mm(2.3)), self.ink, spacing=mm(.4))
        return img


class MoarVar(Batch7):
    key, name = 'moar', 'Moar'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        for k in range(9):                                # fine pinstripes, top to bottom
            x = mm(6 + k * 5.4)
            d.line([(x, mm(4)), (x, mm(H - 4))], fill=darker(self.base, .86), width=mm(.4))
        d.rectangle([0, mm(24), mm(W), mm(54)], fill=self.base)
        d.line([(mm(5), mm(24)), (mm(W - 5), mm(24))], fill=acc, width=mm(.5))
        d.line([(mm(5), mm(54)), (mm(W - 5), mm(54))], fill=acc, width=mm(.5))
        text_c(d, mm(W) / 2, mm(37), 'MOAR', fit_font(d, 'MOAR', ABRIL, mm(W - 10), mm(17)), self.ink)
        text_c(d, mm(W) / 2, mm(48.5), 'BECAUSE 100 IS NOT ENOUGH', fit_font(d, 'BECAUSE 100 IS NOT ENOUGH', OSW4, mm(W - 12), mm(2.6)), acc)
        d.rectangle([mm(12), mm(60), mm(W - 12), mm(69)], fill=acc)
        text_c(d, mm(W) / 2, mm(64.4), self.v['label'], fit_font(d, self.v['label'], OSW7, mm(W - 28), mm(4.2)), self.base, spacing=mm(.4))
        text_c(d, mm(W) / 2, mm(80), '20 LONG CIGARETTES', F(OSW4, mm(2.7)), self.ink)
        return img


class CigaronniVar(Batch7):
    key, name = 'cigaronni', 'Cigaronni'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.rectangle([mm(3.5), mm(3.5), mm(W - 3.5), mm(H - 3.5)], outline=acc, width=mm(.35))
        cx, cy = mm(W / 2), mm(22)
        # a crown: band, five points with beads
        d.rectangle([cx - mm(8), cy + mm(2), cx + mm(8), cy + mm(5)], fill=acc)
        d.polygon([(cx - mm(8), cy + mm(2)), (cx - mm(9.4), cy - mm(5)), (cx - mm(4.6), cy - mm(.6)), (cx, cy - mm(7)), (cx + mm(4.6), cy - mm(.6)),
                   (cx + mm(9.4), cy - mm(5)), (cx + mm(8), cy + mm(2))], fill=acc)
        for x, y in ((-9.4, -5.8), (0, -7.9), (9.4, -5.8)):
            d.ellipse([cx + mm(x - 1), cy + mm(y - 1), cx + mm(x + 1), cy + mm(y + 1)], fill=acc)
        text_c(d, mm(W) / 2, mm(41), 'CIGARONNI', fit_font(d, 'CIGARONNI', CIN9, mm(W - 10), mm(8)), self.ink, spacing=mm(.5))
        ornament(d, mm(W / 2), mm(49), mm(30), acc)
        # the cigarette itself, lying across: three fifths of it is the mouthpiece
        d.rectangle([mm(8), mm(56), mm(W - 8), mm(59.2)], outline=acc, width=mm(.3))
        d.rectangle([mm(8), mm(56), mm(8 + (W - 16) * .6), mm(59.2)], fill=acc)
        text_c(d, mm(W) / 2, mm(63), 'MORE HOLDER THAN SMOKE', fit_font(d, 'MORE HOLDER THAN SMOKE', OSW4, mm(W - 16), mm(2.3)), acc)
        self.kind(d, W, 71.5, font=CIN7, size=3.8, col=self.ink, margin=14)
        text_c(d, mm(W) / 2, mm(81), 'YEREVAN  ·  20', F(CIN7, mm(2.4)), acc, spacing=mm(.6))
        return img


class GudangVar(Batch7):
    key, name = 'gudang', 'Gudang Garram'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=acc, width=mm(.6))
        # a row of warehouses with gabled roofs, and the railway in front of them
        for k in range(4):
            x0 = mm(7.5 + k * 10.2)
            d.rectangle([x0, mm(22), x0 + mm(9.4), mm(32)], fill=acc)
            d.polygon([(x0 - mm(.6), mm(22)), (x0 + mm(4.7), mm(15.5)), (x0 + mm(10), mm(22))], fill=acc)
            d.rectangle([x0 + mm(3.2), mm(26), x0 + mm(6.2), mm(32)], fill=self.base)
        for y in (35, 37.4):
            d.line([(mm(6), mm(y)), (mm(W - 6), mm(y))], fill=acc, width=mm(.4))
        for k in range(14):
            x = mm(7 + k * 3.2)
            d.line([(x, mm(34.4)), (x - mm(.8), mm(38))], fill=acc, width=mm(.35))
        text_c(d, mm(W) / 2, mm(48), 'GUDANG', fit_font(d, 'GUDANG', SLAB, mm(W - 12), mm(10)), self.ink)
        text_c(d, mm(W) / 2, mm(59), 'GARRAM', fit_font(d, 'GARRAM', SLAB, mm(W - 12), mm(10)), self.ink)
        self.kind(d, W, 70, size=3.8, col=acc if abs(lum(acc) - lum(self.base)) > 60 else self.ink)
        text_c(d, mm(W) / 2, mm(80), 'CLOVES INSIDE  ·  20', F(OSW4, mm(2.6)), self.ink)
        return img


class PattakaVar(Batch7):
    key, name = 'pattaka', 'Pattaka Bidi'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        cx, cy = mm(W / 2), mm(27)
        ray = darker(self.base, .88)
        for a in range(0, 360, 20):                       # a sunburst behind the firecracker
            d.pieslice([cx - mm(46), cy - mm(46), cx + mm(46), cy + mm(46)], a, a + 10, fill=ray)
        d.rectangle([0, mm(50), mm(W), mm(H)], fill=self.base)
        d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=acc, width=mm(.7))
        # the firecracker, tilted, with a fuse and a spark
        lay = Image.new('RGBA', img.size, (0, 0, 0, 0))
        ld = ImageDraw.Draw(lay)
        ld.rounded_rectangle([cx - mm(4.2), cy - mm(9), cx + mm(4.2), cy + mm(11)], radius=mm(1), fill=acc)
        for y in (-6, 8):
            ld.rectangle([cx - mm(4.2), cy + mm(y), cx + mm(4.2), cy + mm(y + 1.4)], fill=self.base)
        ld.line([(cx, cy - mm(9)), (cx + mm(1.2), cy - mm(13)), (cx - mm(.6), cy - mm(16))], fill=self.ink, width=mm(.6))
        img.alpha_composite(lay.rotate(-24, center=(cx, cy), resample=Image.BICUBIC))
        d = ImageDraw.Draw(img)
        star(d, cx - mm(7.4), cy - mm(15.2), mm(4.4), self.ink, n=8, inner=.4)
        text_c(d, mm(W) / 2, mm(58), 'PATTAKA', fit_font(d, 'PATTAKA', BEBAS, mm(W - 10), mm(13)), self.ink, spacing=mm(.6))
        text_c(d, mm(W) / 2, mm(67.5), 'BIDI', F(SLAB, mm(5.6)), acc)
        self.kind(d, W, 75, size=3.6, col=self.ink)
        text_c(d, mm(W) / 2, mm(82), 'HAND ROLLED  ·  20', F(OSW4, mm(2.5)), acc)
        return img


CLASSES.update({'exporteh': ExportEhVar, 'dumorrier': DuMorrierVar, 'playerz': PlayerzVar, 'kazbekk': KazbekkVar, 'gertsegovina': GertsegovinaVar,
                'pomir': PomirVar, 'tu143': Tu143Var, 'stewardesa': StewardesaVar, 'soyuz': SoyuzVar, 'rothhandel': RothHandelVar,
                'doublehappy': DoubleHappyVar, 'moar': MoarVar, 'cigaronni': CigaronniVar, 'gudang': GudangVar, 'pattaka': PattakaVar})


# ---------------------------------------------------------------- batch 8 (11-14): Asia; the Japanese ones look Japanese
from PIL import ImageFont as _IF
ZH = os.path.join(P.FONTS, 'wqy-zenhei.ttc')
JA = os.path.join(P.FONTS, 'ipag.ttf')


def cjk(d, cx, cy, s, size, col, font=ZH):
    d.text((cx, cy), s, font=_IF.truetype(font, int(size)), fill=col, anchor='mm')


def vcjk(d, cx, cy, s, size, col, font=JA, gap=1.12):
    for i, ch in enumerate(s):
        cjk(d, cx, cy + (i - (len(s) - 1) / 2) * size * gap, ch, size, col, font)


def seal(d, cx, cy, r, ch, col, bg, font=ZH, circle=False):
    """A stamp: one generic character cut out of a filled square or disc."""
    if circle:
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=col)
    else:
        d.rounded_rectangle([cx - r, cy - r, cx + r, cy + r], radius=r * .18, fill=col)
    cjk(d, cx, cy, ch, r * 1.5, bg, font)


def _pts(cx, cy, s, pts, ang=0.):
    c, n = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    return [(cx + mm((x * c - y * n) * s), cy + mm((x * n + y * c) * s)) for x, y in pts]


def frets(d, W, H, acc, w=.7):
    d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=acc, width=mm(w))
    for x, y in ((3, 3), (W - 3, 3), (3, H - 3), (W - 3, H - 3)):
        sx, sy = (1 if x < W / 2 else -1), (1 if y < H / 2 else -1)
        d.line([(mm(x + sx * 2), mm(y + sy * 6)), (mm(x + sx * 2), mm(y + sy * 2)), (mm(x + sx * 6), mm(y + sy * 2))], fill=acc, width=mm(.4))


def roof(d, cx, cy, s, w, y, col):
    """A tiled roof with upturned eaves, w = half width, y = its lower edge."""
    d.polygon(_pts(cx, cy, s, [(-w - 1.5, y), (-w - 2.8, y - 1.7), (-w + 1.2, y - 1.0), (-w + 3.2, y - 2.6), (w - 3.2, y - 2.6), (w - 1.2, y - 1.0),
                               (w + 2.8, y - 1.7), (w + 1.5, y)]), fill=col)


def gate(d, cx, cy, s, col, bg):
    d.rectangle([cx - mm(15 * s), cy + mm(2 * s), cx + mm(15 * s), cy + mm(11 * s)], fill=col)
    for x, w, h in ((0, 2.6, 6.4), (-8.4, 1.9, 4.6), (8.4, 1.9, 4.6)):
        d.rounded_rectangle([cx + mm((x - w) * s), cy + mm((11 - h) * s), cx + mm((x + w) * s), cy + mm(14 * s)], radius=mm(w * s), fill=bg)
    d.rectangle([cx - mm(16 * s), cy + mm(11 * s), cx + mm(16 * s), cy + mm(14.4 * s)], fill=bg)
    d.rectangle([cx - mm(16.5 * s), cy + mm(11 * s), cx + mm(16.5 * s), cy + mm(12 * s)], fill=col)
    roof(d, cx, cy, s, 16, 2, col)
    d.rectangle([cx - mm(10 * s), cy - mm(4.5 * s), cx + mm(10 * s), cy - mm(.4 * s)], fill=col)
    for k in range(-3, 4):
        d.rectangle([cx + mm((k * 2.6 - .7) * s), cy - mm(3.6 * s), cx + mm((k * 2.6 + .7) * s), cy - mm(1.2 * s)], fill=bg)
    roof(d, cx, cy, s, 12, -4.5, col)


def pagoda(d, cx, cy, s, col, tiers=5, hill=True):
    if hill:
        d.chord([cx - mm(21 * s), cy + mm(11 * s), cx + mm(21 * s), cy + mm(25 * s)], 180, 360, fill=col)
    d.rectangle([cx - mm(5.4 * s), cy + mm(9 * s), cx + mm(5.4 * s), cy + mm(12.5 * s)], fill=col)
    y = 9.
    for k in range(tiers):
        w = 8.6 - k * 1.35
        roof(d, cx, cy, s, w, y, col)
        q = max(1., w - 3.6)
        d.rectangle([cx - mm(q * s), cy + mm((y - 4.4) * s), cx + mm(q * s), cy + mm((y - 2.5) * s)], fill=col)
        y -= 4.2
    d.line([(cx, cy + mm((y + 1.8) * s)), (cx, cy + mm((y - 3.4) * s))], fill=col, width=max(2, mm(.6 * s)))


def crane(d, cx, cy, s, col, ang=0.):
    q = lambda pts: _pts(cx, cy, s, pts, ang)
    d.polygon(q([(-6, 0), (-3, -2.2), (3, -2.4), (5.4, -.6), (3.4, 2.2), (-3, 2.4)]), fill=col)
    d.line(q([(4, -.6), (8, -3.4), (11, -5.4)]), fill=col, width=max(2, mm(1.0 * s)), joint='curve')
    d.polygon(q([(10.4, -6.4), (12.6, -6.2), (16.4, -5.1), (12.4, -4.4), (10.6, -4.6)]), fill=col)
    d.line(q([(-4.5, 1), (-13.5, 3.6)]), fill=col, width=max(1, mm(.5 * s)))
    d.line(q([(-4.5, 1.6), (-13, 5)]), fill=col, width=max(1, mm(.5 * s)))
    d.polygon(q([(-3.4, -1.4), (-7.6, -10.5), (-4.2, -9.2), (-5.2, -13.6), (-1.2, -10.8), (-.4, -14.6), (2.4, -10), (3.8, -1.6)]), fill=col)
    d.polygon(q([(1.5, -1.8), (6.4, -9.8), (7.2, -6.6), (10.4, -9.4), (9, -5), (5, -1.2)]), fill=col)


def panda(d, cx, cy, s, dark, face, line=None):
    def el(x, y, rx, ry, col, **kw):
        d.ellipse([cx + mm((x - rx) * s), cy + mm((y - ry) * s), cx + mm((x + rx) * s), cy + mm((y + ry) * s)], fill=col, **kw)
    lw = dict(outline=line, width=max(2, mm(.45 * s))) if line else {}
    for sx in (-1, 1):
        el(sx * 7.2, -7, 3.7, 3.7, dark, **lw)
    el(0, 0, 9.8, 8.8, face, **lw)
    for sx in (-1, 1):
        d.polygon(_pts(cx + mm(sx * 4.3 * s), cy - mm(1 * s), s, [(0, -3.4), (2.2, -1.6), (2.4, 1.4), (.6, 3.4), (-1.6, 2.4), (-2.4, -.6)], sx * 28), fill=dark)
        el(sx * 4.1, -1.4, .95, .95, face)
    el(0, 3.1, 1.5, 1.05, dark)
    d.line([(cx, cy + mm(4 * s)), (cx, cy + mm(5.3 * s))], fill=dark, width=max(2, mm(.4 * s)))
    d.arc([cx - mm(2.2 * s), cy + mm(3.4 * s), cx, cy + mm(6.6 * s)], 20, 150, fill=dark, width=max(2, mm(.4 * s)))
    d.arc([cx, cy + mm(3.4 * s), cx + mm(2.2 * s), cy + mm(6.6 * s)], 30, 160, fill=dark, width=max(2, mm(.4 * s)))


def seigaiha(img, box, r, col, bg, w):
    """Overlapping wave scales (rows of concentric arcs)."""
    x0, y0, x1, y1 = [int(v) for v in box]
    lay = Image.new('RGBA', (x1 - x0, y1 - y0), bg)
    ld = ImageDraw.Draw(lay)
    row, y = 0, 0
    while y < (y1 - y0) + r * 2:
        x = -r if row % 2 else 0
        while x < (x1 - x0) + r:
            ld.ellipse([x - r, y - r, x + r, y + r], fill=bg)
            for k in (1., .7, .4):
                ld.arc([x - r * k, y - r * k, x + r * k, y + r * k], 180, 360, fill=col, width=w)
            x += 2 * r
        y += r / 2
        row += 1
    img.paste(lay, (x0, y0))


def wolf(d, cx, cy, s, col, ang=0):
    """A sitting wolf howling: head thrown up, the mouth open, tail on the ground."""
    d.polygon(_pts(cx, cy, s, [(-5, -14.5), (-1, -11.5), (1, -11.8), (4.2, -10), (1.8, -8.4), (3.6, -4), (7, 2), (8.5, 7), (12.6, 7.2), (12.9, 9.2),
                               (9, 10), (2, 10), (.6, 5), (-1.4, 5), (-1.7, 10), (-5, 10), (-4.6, 2), (-3.6, -4), (-4.2, -7), (-8, -10.4),
                               (-7.3, -11.3), (-3.2, -9.5), (-5.9, -13.5)], ang), fill=col)


def petal(cx, cy, s, ang, L, Wd, n=12):
    pts = [(-Wd * math.sin(math.pi * t / n) ** .8, -L * t / n) for t in range(n + 1)] + [(Wd * math.sin(math.pi * t / n) ** .8, -L * t / n) for t in range(n, -1, -1)]
    return _pts(cx, cy, s, pts, ang)


def lotus(d, cx, cy, s, col, bg):
    lw = max(2, mm(.4 * s))
    for a, L, Wd in ((-78, 11, 3.0), (78, 11, 3.0), (-50, 13, 3.4), (50, 13, 3.4), (-24, 14.5, 3.6), (24, 14.5, 3.6), (0, 15.5, 3.8)):
        d.polygon(petal(cx, cy, s, a, L, Wd), fill=col, outline=bg, width=lw)
    d.arc([cx - mm(13 * s), cy - mm(2 * s), cx + mm(13 * s), cy + mm(4.4 * s)], 10, 170, fill=col, width=max(2, mm(.7 * s)))


def leaf8(d, cx, cy, s, col, bg, ang, L=20, Wd=5.4):
    d.polygon(petal(cx, cy, s, ang, L, Wd, 16), fill=col)
    d.line(_pts(cx, cy, s, [(0, 1.5), (0, -L * .94)], ang), fill=bg, width=max(2, mm(.45 * s)))
    for k in range(1, 6):
        y = -L * k / 6.4
        for sx in (-1, 1):
            d.line(_pts(cx, cy, s, [(0, y), (sx * Wd * .62 * math.sin(math.pi * (k + .6) / 6.6), y - 2.2)], ang), fill=bg, width=max(1, mm(.28 * s)))


def dove(d, cx, cy, s, col):
    q = lambda pts: _pts(cx, cy, s, pts)
    d.polygon(q([(-6, -.4), (-2, -2.4), (4, -2.6), (6.4, -1), (4.4, 2.4), (-2, 3)]), fill=col)
    d.ellipse([cx + mm(4.8 * s), cy - mm(4.8 * s), cx + mm(9 * s), cy - mm(.8 * s)], fill=col)
    d.polygon(q([(8.6, -3.4), (11.2, -2.7), (8.6, -2)]), fill=col)
    d.polygon(q([(-5, -1), (-12.5, -3.8), (-11, -.4), (-13, 2.6), (-5, 2.2)]), fill=col)
    d.polygon(q([(-3, -1.6), (-5.6, -11.4), (-2, -9), (-1, -12.8), (1.8, -8.6), (3.6, -1.8)]), fill=col)
    d.line(q([(10.8, -2.7), (14.4, -5)]), fill=col, width=max(1, mm(.4 * s)))
    for x, y in ((13, -5.4), (14.6, -3.6), (15.4, -6)):
        d.ellipse([cx + mm((x - .9) * s), cy + mm((y - .55) * s), cx + mm((x + .9) * s), cy + mm((y + .55) * s)], fill=col)


def bat8(d, cx, cy, s, col):
    half = [(0, -3.4), (1.3, -5.6), (2.1, -2.6), (6, -7), (14.5, -5.2), (12.8, -1.4), (10.4, -3.4), (9, .8), (6.8, -1.6), (5.4, 2.8), (3.2, .4), (1.6, 4.4), (0, 5.4)]
    d.polygon(_pts(cx, cy, s, half + [(-x, y) for x, y in reversed(half[:-1])]), fill=col)


def fuji(d, cx, cy, s, col, snow):
    d.polygon(_pts(cx, cy, s, [(-18, 9), (-5, -6), (-2.6, -7.2), (2.6, -7.2), (5, -6), (18, 9)]), fill=col)
    d.polygon(_pts(cx, cy, s, [(-5, -6), (-2.6, -7.2), (2.6, -7.2), (5, -6), (7.6, -3), (5, -4.2), (3.2, -1.8), (1.2, -3.8), (-.6, -1.6), (-2.6, -4), (-4.6, -2.2), (-7.6, -3)]),
              fill=snow)


def sakura(d, cx, cy, r, col, bg):
    for k in range(5):
        a = math.radians(-90 + k * 72)
        px, py = cx + math.cos(a) * r * .52, cy + math.sin(a) * r * .52
        d.ellipse([px - r * .46, py - r * .46, px + r * .46, py + r * .46], fill=col)
    for k in range(5):
        a = math.radians(-90 + k * 72)
        t = lambda da, q: (cx + math.cos(a + da) * r * q, cy + math.sin(a + da) * r * q)
        d.polygon([t(-.09, 1.02), t(0, .8), t(.09, 1.02)], fill=bg)
        d.line([t(0, .16), t(0, .46)], fill=bg, width=max(1, int(r * .05)))
    d.ellipse([cx - r * .13, cy - r * .13, cx + r * .13, cy + r * .13], fill=bg)


def sunrays(d, cx, cy, r, col, n=18, a0=180., a1=360.):
    st = (a1 - a0) / n
    for k in range(0, n, 2):
        d.pieslice([cx - r, cy - r, cx + r, cy + r], a0 + k * st, a0 + (k + 1) * st, fill=col)


def cat8(d, cx, cy, s, col):
    d.ellipse([cx - mm(5.2 * s), cy - mm(2.5 * s), cx + mm(5.2 * s), cy + mm(10 * s)], fill=col)
    d.ellipse([cx - mm(4.3 * s), cy - mm(10.2 * s), cx + mm(4.3 * s), cy - mm(2 * s)], fill=col)
    for sx in (-1, 1):
        d.polygon(_pts(cx, cy, s, [(sx * 4.1, -7.4), (sx * 3.7, -13), (sx * .7, -9.6)]), fill=col)
    d.arc([cx + mm(.5 * s), cy + mm(1 * s), cx + mm(11.5 * s), cy + mm(11 * s)], -70, 95, fill=col, width=max(2, mm(1.7 * s)))


def minars(d, cx, cy, s, col, bg):
    for x in (-10.5, -3.5, 3.5, 10.5):
        d.rectangle([cx + mm((x - 1.1) * s), cy - mm(7 * s), cx + mm((x + 1.1) * s), cy + mm(9 * s)], fill=col)
        d.ellipse([cx + mm((x - 1.7) * s), cy - mm(10.4 * s), cx + mm((x + 1.7) * s), cy - mm(6.6 * s)], fill=col)
        d.line([(cx + mm(x * s), cy - mm(10 * s)), (cx + mm(x * s), cy - mm(12.6 * s))], fill=col, width=max(1, mm(.4 * s)))
        for y in (-3.6, .2):
            d.rectangle([cx + mm((x - 1.9) * s), cy + mm(y * s), cx + mm((x + 1.9) * s), cy + mm((y + .9) * s)], fill=col)
    d.rectangle([cx - mm(10.5 * s), cy + mm(2.4 * s), cx + mm(10.5 * s), cy + mm(9 * s)], fill=col)
    for x in (-7, 0, 7):
        d.rounded_rectangle([cx + mm((x - 1.5) * s), cy + mm(4.4 * s), cx + mm((x + 1.5) * s), cy + mm(11 * s)], radius=mm(1.5 * s), fill=bg)
    d.rectangle([cx - mm(12.6 * s), cy + mm(9 * s), cx + mm(12.6 * s), cy + mm(10 * s)], fill=col)
    d.rectangle([cx - mm(12.6 * s), cy + mm(10 * s), cx + mm(12.6 * s), cy + mm(11.4 * s)], fill=bg)


def shou(d, cx, cy, s, col):
    w = max(2, mm(.9 * s))
    d.ellipse([cx - mm(11.5 * s), cy - mm(11.5 * s), cx + mm(11.5 * s), cy + mm(11.5 * s)], outline=col, width=w)
    for x0, y0, x1, y1 in ((-5, -6.5, 5, -6.5), (-7, -3, 7, -3), (-7, .5, 7, .5), (-5, 4, 5, 4), (0, -8.6, 0, 8.4), (-7, -3, -7, .5), (7, -3, 7, .5),
                           (-5, 4, -5, 7.4), (5, 4, 5, 7.4), (-5, 7.4, -2.2, 7.4), (5, 7.4, 2.2, 7.4), (-3, -8.6, 3, -8.6)):
        d.line([(cx + mm(x0 * s), cy + mm(y0 * s)), (cx + mm(x1 * s), cy + mm(y1 * s))], fill=col, width=w)


class Batch8(Batch7):
    foot = ''

    def hi(self, acc):
        return acc if abs(lum(acc) - lum(self.base)) > 60 else self.ink

    def name2(self, d, W, a, b, y, font=CIN9, size=7, gap=8.5, m1=18, m2=12, col=None):
        text_c(d, mm(W) / 2, mm(y), a, fit_font(d, a, font, mm(W - m1), mm(size)), col or self.ink, spacing=mm(.5))
        text_c(d, mm(W) / 2, mm(y + gap), b, fit_font(d, b, font, mm(W - m2), mm(size)), col or self.ink, spacing=mm(.4))

    def name1(self, d, W, a, y, font=CIN9, size=8, m=12, col=None, sp=.5):
        text_c(d, mm(W) / 2, mm(y), a, fit_font(d, a, font, mm(W - m), mm(size)), col or self.ink, spacing=mm(sp))

    def footer(self, d, W, y=80, font=CIN7, size=2.2, col=None):
        text_c(d, mm(W) / 2, mm(y), self.foot, fit_font(d, self.foot, font, mm(W - 12), mm(size)), col or self.ink, spacing=mm(.3))


class ChunghwahVar(Batch8):
    key, name, foot = 'chunghwah', 'Chunghwah', 'GATE OF HEAVENLY SMOKE  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        frets(d, W, H, acc)
        gate(d, mm(W / 2), mm(22), 1.05, acc, self.base)
        cjk(d, mm(W / 2), mm(42.5), '香  烟', mm(3.4), acc)
        self.name1(d, W, 'CHUNGHWAH', 53, size=7.4, m=13)
        ornament(d, mm(W / 2), mm(60.5), mm(30), acc)
        self.kind(d, W, 68.5, font=CIN7, size=3.8, col=self.hi(acc), margin=16)
        self.footer(d, W)
        return img


class HongtaVar(Batch8):
    key, name, foot = 'hongta', 'Hongta Hill', 'RED PAGODA TOBACCO  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=acc, width=mm(.6))
        d.ellipse([mm(W / 2 - 15), mm(8), mm(W / 2 + 15), mm(38)], outline=acc, width=mm(.4))
        pagoda(d, mm(W / 2), mm(22), .92, acc)
        d.rectangle([mm(3.4), mm(40.2), mm(W - 3.4), mm(44)], fill=self.base)
        seal(d, mm(W - 10), mm(11), mm(3.2), '山', acc, self.base)
        self.name1(d, W, 'HONGTA', 52, font=SERIF9, size=11, m=14)
        self.name1(d, W, 'HILL', 61.5, font=CIN7, size=4.6, m=30, col=self.hi(acc), sp=2.4)
        self.kind(d, W, 71, font=CIN7, size=3.6, col=self.ink, margin=16)
        self.footer(d, W, col=self.hi(acc))
        return img


class YellowCraneVar(Batch8):
    key, name, foot = 'yellowcrane', 'Yelow Crane Tower', 'A THOUSAND YEARS OF WAITING  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        frets(d, W, H, acc, .5)
        d.ellipse([mm(27), mm(8), mm(47), mm(28)], outline=acc, width=mm(.4))
        pagoda(d, mm(15), mm(27), .62, acc, tiers=4, hill=False)
        crane(d, mm(33), mm(24), .95, acc, -14)
        d.line([(mm(7), mm(40)), (mm(W - 7), mm(40))], fill=acc, width=mm(.35))
        self.name2(d, W, 'YELOW CRANE', 'TOWER', 49.5, size=6.2, gap=8, m1=13, m2=26)
        seal(d, mm(W / 2), mm(67), mm(2.8), '鹤', acc, self.base)
        self.kind(d, W, 74, font=CIN7, size=3.4, col=self.hi(acc), margin=16)
        self.footer(d, W, 81)
        return img


class PandahVar(Batch8):
    key, name, foot = 'pandah', 'Pandah', 'BLACK  ·  WHITE  ·  SMOKY  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        bam = self.hi(acc)
        for x in (8, 12.5, W - 8, W - 12.5):
            d.line([(mm(x), mm(5)), (mm(x), mm(46))], fill=bam, width=mm(1.1))
            for y in (13, 23, 34) if int(x) % 2 == 0 else (9, 19, 29, 40):
                d.line([(mm(x - 1), mm(y)), (mm(x + 1), mm(y))], fill=self.base, width=mm(.5))
        dark = (24, 24, 24, 255)
        face_c = (244, 242, 236, 255)
        panda(d, mm(W / 2), mm(27), 1.3, dark, face_c, line=dark if lum(self.base) > 120 else face_c)
        self.name1(d, W, 'PANDAH', 55, font=ABRIL, size=13, m=12)
        self.kind(d, W, 67, font=OSW7, size=4, col=self.hi(acc), margin=14)
        self.footer(d, W, font=OSW4, size=2.5)
        return img


class YuxeeVar(Batch8):
    key, name, foot = 'yuxee', 'Yuxee', 'JADE STREAM  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=acc, width=mm(.5))
        for k in range(5):
            pts = [(mm(x), mm(17 + k * 4.2 + 1.6 * math.sin(x / 4.2 + k * .9))) for x in range(4, int(W) - 3)]
            d.line(pts, fill=acc, width=mm(.4))
        d.ellipse([mm(W / 2 - 12), mm(13.5), mm(W / 2 + 12), mm(37.5)], fill=self.base, outline=acc, width=mm(2.2))
        seal(d, mm(W / 2), mm(25.5), mm(5.4), '玉', acc, self.base, circle=True)
        self.name1(d, W, 'YUXEE', 52, size=12, m=14, sp=1.2)
        ornament(d, mm(W / 2), mm(61), mm(30), acc)
        self.kind(d, W, 69, font=CIN7, size=3.8, col=self.hi(acc), margin=16)
        self.footer(d, W)
        return img


class ZhongnanVar(Batch8):
    key, name, foot = 'zhongnan', 'Zhongnan Sea', 'LAKESIDE BLEND  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        wave = tuple(int(a * .45 + b * .55) for a, b in zip(acc[:3], self.base[:3])) + (255,)
        seigaiha(img, (0, mm(57), mm(W), mm(H)), mm(5.2), wave, self.base, mm(.4))
        d = ImageDraw.Draw(img)
        d.line([(0, mm(57)), (mm(W), mm(57))], fill=acc, width=mm(.5))
        self.name1(d, W, 'ZHONGNAN', 20, font=BEBAS, size=13, m=10, sp=.8)
        self.name1(d, W, 'SEA', 31.5, font=CIN7, size=5, m=30, col=self.hi(acc), sp=3.4)
        d.rectangle([mm(11), mm(39), mm(W - 11), mm(48)], outline=acc, width=mm(.5))
        self.kind(d, W, 43.4, font=OSW7, size=4.2, col=self.ink, margin=28)
        self.footer(d, W, 52.6, font=OSW4, size=2.4, col=self.hi(acc))
        seal(d, mm(W / 2), mm(72), mm(5), '海', acc, self.base, circle=True)
        return img


class SevenWolfsVar(Batch8):
    key, name, foot = 'sevenwolfs', 'Seven Wolfs', 'RUN WITH THE PACK  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=acc, width=mm(.5))
        d.ellipse([mm(W / 2 - 15), mm(9), mm(W / 2 + 15), mm(39)], fill=acc)
        wolf(d, mm(W / 2 - 2), mm(25.6), 1.05, self.base)
        d.rectangle([mm(3.4), mm(36.4), mm(W - 3.4), mm(41)], fill=self.base)
        for k in range(7):
            star(d, mm(W / 2 + (k - 3) * 5.6), mm(42), mm(1.3), acc)
        self.name2(d, W, 'SEVEN', 'WOLFS', 51.5, font=BEBAS, size=10, gap=9.6, m1=26, m2=22)
        self.kind(d, W, 71.5, font=OSW7, size=3.8, col=self.hi(acc), margin=16)
        self.footer(d, W, font=OSW4, size=2.5)
        return img


class LotusKingVar(Batch8):
    key, name, foot = 'lotusking', 'Lotus Kingg', 'KING OF THE POND  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=acc, width=mm(.7))
        d.rectangle([mm(4.6), mm(4.6), mm(W - 4.6), mm(H - 4.6)], outline=acc, width=mm(.25))
        lotus(d, mm(W / 2), mm(33), 1.2, acc, self.base)
        self.name1(d, W, 'LOTUS', 50, font=SERIF9, size=10.5, m=18, sp=1)
        self.name1(d, W, 'KINGG', 60.5, size=6.4, m=24, col=self.hi(acc), sp=1.6)
        self.kind(d, W, 71, font=CIN7, size=3.6, col=self.ink, margin=16)
        self.footer(d, W, col=self.hi(acc))
        return img


class GoldenLeefVar(Batch8):
    key, name, foot = 'goldenleef', 'Golden Leef', 'HARVEST OF THE EMPIRE  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        frets(d, W, H, acc)
        r = mm(15.5)
        d.ellipse([mm(W / 2) - r, mm(25) - r, mm(W / 2) + r, mm(25) + r], outline=acc, width=mm(.5))
        leaf8(d, mm(W / 2 - 6), mm(35), 1.1, acc, self.base, 32)
        seal(d, mm(W / 2 + 9), mm(32.5), mm(2.6), '叶', acc, self.base)
        self.name2(d, W, 'GOLDEN', 'LEEF', 52, size=7.4, gap=9, m1=18, m2=26)
        self.kind(d, W, 71, font=CIN7, size=3.8, col=self.hi(acc), margin=16)
        self.footer(d, W)
        return img


class SevnStarsVar(Batch8):
    key, name, foot = 'sevnstars', 'Sevn Stars', 'CHARCOAL FILTER  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        faint = tuple(int(a * .22 + b * .78) for a, b in zip(acc[:3], self.base[:3])) + (255,)
        for j in range(15):
            for i in range(10):
                star(d, mm(3.5 + i * 5.6 + (2.8 if j % 2 else 0)), mm(4 + j * 5.8), mm(.9), faint)
        d.ellipse([mm(W / 2 - 14.5), mm(10), mm(W / 2 + 14.5), mm(39)], fill=self.base, outline=acc, width=mm(.5))
        for k in range(7):
            a = math.radians(-90 + k * 360 / 7)
            star(d, mm(W / 2) + math.cos(a) * mm(9.6), mm(24.5) + math.sin(a) * mm(9.6), mm(2.1), acc)
        cjk(d, mm(W / 2), mm(24.6), '星', mm(6.4), acc, JA)
        d.rectangle([mm(5), mm(43.5), mm(W - 5), mm(65)], fill=self.base)
        self.name2(d, W, 'SEVN', 'STARS', 49.5, font=SERIF9, size=8.6, gap=9.6, m1=26, m2=22)
        d.rectangle([mm(9), mm(67.5), mm(W - 9), mm(75.5)], fill=self.base)
        self.kind(d, W, 71.4, font=CIN7, size=3.6, col=self.hi(acc), margin=22)
        d.rectangle([mm(8), mm(78), mm(W - 8), mm(83)], fill=self.base)
        self.footer(d, W, 80.4)
        vcjk(d, mm(6.2), mm(26), 'たばこ', mm(2.8), self.hi(acc))
        return img


class PeeceVar(Batch8):
    key, name, foot = 'peece', 'Peece', 'A QUIET SMOKE  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        dove(d, mm(W / 2 - 1), mm(27), 1.25, acc)
        self.name1(d, W, 'Peece', 52, font=SERIF9, size=17, m=12, col=self.hi(acc), sp=0)
        d.line([(mm(12), mm(63)), (mm(W - 12), mm(63))], fill=acc, width=mm(.3))
        self.kind(d, W, 69, font=CIN7, size=3.4, col=self.ink, margin=16)
        self.footer(d, W, 80)
        vcjk(d, mm(W - 6.5), mm(20), 'たばこ', mm(2.8), self.hi(acc))
        seal(d, mm(8), mm(9), mm(2.6), '和', acc, self.base, JA)
        return img


class GoldenBattVar(Batch8):
    key, name, foot = 'goldenbatt', 'Golden Batt', 'SINCE THE LAMPS BURNED OIL  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=acc, width=mm(.7))
        d.rectangle([mm(4.6), mm(4.6), mm(W - 4.6), mm(H - 4.6)], outline=acc, width=mm(.25))
        sunrays(d, mm(W / 2), mm(36), mm(24), darker(self.base, .86), 14)
        d.rectangle([mm(5), mm(36), mm(W - 5), mm(40)], fill=self.base)
        bat8(d, mm(W / 2), mm(21), 1.15, acc)
        bat8(d, mm(13), mm(31), .42, acc)
        bat8(d, mm(W - 13), mm(31), .42, acc)
        self.name1(d, W, 'GOLDEN', 46, size=6.6, m=20, sp=1.4)
        self.name1(d, W, 'BATT', 58, font=ABRIL, size=13, m=22, col=self.hi(acc), sp=.8)
        self.kind(d, W, 70.5, font=CIN7, size=3.4, col=self.ink, margin=16)
        self.footer(d, W, col=self.hi(acc))
        seal(d, mm(W - 9.5), mm(10), mm(2.5), '金', acc, self.base, JA)
        return img


class HiLyteVar(Batch8):
    key, name, foot = 'hilyte', 'Hi-Lyte', 'DELUXE LENGTH  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.rectangle([mm(6), 0, mm(9), mm(H)], fill=acc)
        d.ellipse([mm(W - 19), mm(8), mm(W - 7), mm(20)], outline=acc, width=mm(.6))
        d.ellipse([mm(W - 16), mm(11), mm(W - 10), mm(17)], fill=acc)
        text_c(d, mm(W / 2 + 4), mm(37), 'hi-lyte', fit_font(d, 'hi-lyte', ABRIL, mm(W - 18), mm(15)), self.ink)
        d.line([(mm(13), mm(47)), (mm(W - 5), mm(47))], fill=acc, width=mm(.4))
        d.rectangle([mm(13), mm(54), mm(W - 5), mm(63)], fill=acc)
        text_c(d, mm(W / 2 + 4), mm(58.4), self.v['label'], fit_font(d, self.v['label'], OSW7, mm(W - 26), mm(4.2)), self.base, spacing=mm(.4))
        text_c(d, mm(W / 2 + 4), mm(80), self.foot, F(OSW4, mm(2.6)), self.ink)
        vcjk(d, mm(W - 8), mm(72), 'たばこ', mm(2.6), self.ink)
        return img


class WakabahVar(Batch8):
    key, name, foot = 'wakabah', 'Wakabah', 'YOUNG LEAF  ·  PLAIN FOLK  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.ellipse([mm(W / 2 - 16), mm(7), mm(W / 2 + 16), mm(39)], outline=acc, width=mm(.5))
        d.line([(mm(W / 2), mm(36)), (mm(W / 2), mm(27))], fill=acc, width=mm(.9))
        leaf8(d, mm(W / 2), mm(28), .8, acc, self.base, -38, 17, 5)
        leaf8(d, mm(W / 2), mm(28), .8, acc, self.base, 38, 17, 5)
        self.name1(d, W, 'WAKABAH', 52, font=BEBAS, size=13, m=12, sp=.8)
        d.line([(mm(10), mm(60.5)), (mm(W - 10), mm(60.5))], fill=acc, width=mm(.35))
        self.kind(d, W, 67.5, font=OSW7, size=3.8, col=self.hi(acc), margin=16)
        self.footer(d, W, font=OSW4, size=2.4)
        seal(d, mm(W - 9), mm(9.5), mm(2.6), '葉', acc, self.base, JA)
        return img


class ShinSeyVar(Batch8):
    key, name, foot = 'shinsey', 'Shin-Sey', 'NEW LIFE EVERY MORNING  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        sunrays(d, mm(W / 2), mm(26), mm(70), darker(self.base, .9), 24, 0, 360)
        d.rectangle([0, mm(44), mm(W), mm(H)], fill=self.base)
        d.ellipse([mm(W / 2 - 11), mm(15), mm(W / 2 + 11), mm(37)], fill=acc)
        cjk(d, mm(W / 2), mm(26), '新', mm(9), self.base, JA)
        d.line([(0, mm(44)), (mm(W), mm(44))], fill=acc, width=mm(.6))
        self.name1(d, W, 'SHIN-SEY', 54, font=SLAB, size=9.5, m=12, sp=.6)
        self.kind(d, W, 66, font=OSW7, size=4, col=self.hi(acc), margin=16)
        self.footer(d, W, font=OSW4, size=2.5)
        vcjk(d, mm(6.5), mm(62), 'たばこ', mm(2.6), self.hi(acc))
        return img


class CherrieVar(Batch8):
    key, name, foot = 'cherrie', 'Cherrie', 'HANAMI BLEND  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.line([(mm(2), mm(40)), (mm(18), mm(30)), (mm(30), mm(27)), (mm(47), mm(13)), (mm(W), mm(9))], fill=self.hi(acc), width=mm(.8), joint='curve')
        sakura(d, mm(W / 2 + 1), mm(24), mm(11), acc, self.base)
        sakura(d, mm(11), mm(36), mm(4.6), acc, self.base)
        sakura(d, mm(46), mm(11), mm(4.2), acc, self.base)
        sakura(d, mm(44), mm(35), mm(3), acc, self.base)
        self.name1(d, W, 'Cherrie', 53.5, font=SCRIPT, size=17, m=10, sp=0)
        self.kind(d, W, 67.5, font=CIN7, size=3.6, col=self.hi(acc), margin=16)
        self.footer(d, W)
        seal(d, mm(8.5), mm(9.5), mm(2.6), '桜', acc, self.base, JA)
        return img


class AsaheeVar(Batch8):
    key, name, foot = 'asahee', 'Asahee', 'MOUTHPIECE CIGARETTES  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        ray = tuple(int(a * .5 + b * .5) for a, b in zip(acc[:3], self.base[:3])) + (255,)
        sunrays(d, mm(W / 2), mm(40), mm(70), ray, 16)
        d.pieslice([mm(W / 2 - 13), mm(27), mm(W / 2 + 13), mm(53)], 180, 360, fill=acc)
        fuji(d, mm(W / 2), mm(33), .78, self.ink, self.base)
        d.rectangle([0, mm(40), mm(W), mm(H)], fill=self.base)
        d.line([(0, mm(40)), (mm(W), mm(40))], fill=self.ink, width=mm(.5))
        self.name1(d, W, 'ASAHEE', 52, font=ABRIL, size=12.5, m=12, sp=.6)
        self.kind(d, W, 64.5, font=CIN7, size=3.8, col=self.hi(acc), margin=16)
        self.footer(d, W)
        seal(d, mm(W / 2), mm(73), mm(2.7), '朝', acc, self.base, JA)
        return img


class RaizonVar(Batch8):
    key, name, foot = 'raizon', 'Raizon', 'NINE LIVES  ·  TWENTY SMOKES'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.ellipse([mm(W / 2 - 4), mm(6), mm(W / 2 + 20), mm(30)], outline=acc, width=mm(.4))
        cat8(d, mm(W / 2 - 2), mm(27), 1.25, acc)
        d.line([(mm(8), mm(40.4)), (mm(W - 8), mm(40.4))], fill=acc, width=mm(.5))
        self.name1(d, W, 'RAIZON', 52, font=BEBAS, size=15, m=12, sp=1)
        self.kind(d, W, 64.5, font=OSW7, size=3.8, col=self.hi(acc), margin=16)
        self.footer(d, W, font=OSW4, size=2.5)
        return img


class VinatabakVar(Batch8):
    key, name, foot = 'vinatabak', 'Vinatabak', 'SAI GON  ·  HA NOI  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=acc, width=mm(.7))
        d.rectangle([mm(4.6), mm(4.6), mm(W - 4.6), mm(H - 4.6)], outline=acc, width=mm(.25))
        for k in range(22):
            a = math.radians(115 + k * 310 / 21)
            cx, cy = mm(W / 2) + math.cos(a) * mm(13.5), mm(24) + math.sin(a) * mm(13.5)
            d.polygon(petal(cx, cy, .22, math.degrees(a) + (60 if k < 11 else 120) + 90, 14, 4.4), fill=acc)
        star(d, mm(W / 2), mm(24), mm(8.6), acc)
        self.name1(d, W, 'VINATABAK', 50, size=7, m=13)
        ornament(d, mm(W / 2), mm(57.5), mm(30), acc)
        self.kind(d, W, 65.5, font=CIN7, size=3.6, col=self.hi(acc), margin=16)
        self.footer(d, W, 79)
        return img


class DjiSamSueVar(Batch8):
    key, name, foot = 'djisamsue', 'Dji Sam Sue', 'TWO  ·  THREE  ·  FIVE  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=acc, width=mm(.7))
        d.ellipse([mm(W / 2 - 17), mm(8), mm(W / 2 + 17), mm(42)], outline=acc, width=mm(.5))
        for k in range(9):
            a = math.radians(-90 + k * 40)
            star(d, mm(W / 2) + math.cos(a) * mm(13.6), mm(25) + math.sin(a) * mm(13.6), mm(1.7), acc)
        text_c(d, mm(W / 2), mm(24.6), '235', F(ABRIL, mm(10.5)), self.ink)
        self.name1(d, W, 'DJI SAM SUE', 53, size=6.6, m=12)
        ornament(d, mm(W / 2), mm(60.5), mm(30), acc)
        self.kind(d, W, 68.5, font=CIN7, size=3.4, col=self.hi(acc), margin=16)
        self.footer(d, W)
        return img


class CharminarrVar(Batch8):
    key, name, foot = 'charminarr', 'Charminarr', 'FOUR TOWERS  ·  NO HURRY  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=acc, width=mm(.6))
        d.pieslice([mm(W / 2 - 19), mm(9), mm(W / 2 + 19), mm(47)], 180, 360, outline=acc, width=mm(.4))
        minars(d, mm(W / 2), mm(27), 1.15, acc, self.base)
        self.name1(d, W, 'CHARMINARR', 51, size=6.6, m=12)
        ornament(d, mm(W / 2), mm(58.5), mm(30), acc)
        self.kind(d, W, 66.5, font=CIN7, size=3.8, col=self.hi(acc), margin=16)
        self.footer(d, W)
        return img


class LonggLifeVar(Batch8):
    key, name, foot = 'longglife', 'Longg Life', 'IRONY NOT INCLUDED  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        frets(d, W, H, acc, .5)
        shou(d, mm(W / 2), mm(25), 1.15, acc)
        self.name2(d, W, 'LONGG', 'LIFE', 50.5, font=SERIF9, size=8.6, gap=10, m1=22, m2=30)
        self.kind(d, W, 71.5, font=CIN7, size=3.8, col=self.hi(acc), margin=16)
        self.footer(d, W)
        return img


CLASSES.update({'chunghwah': ChunghwahVar, 'hongta': HongtaVar, 'yellowcrane': YellowCraneVar, 'pandah': PandahVar, 'yuxee': YuxeeVar,
                'zhongnan': ZhongnanVar, 'sevenwolfs': SevenWolfsVar, 'lotusking': LotusKingVar, 'goldenleef': GoldenLeefVar,
                'sevnstars': SevnStarsVar, 'peece': PeeceVar, 'goldenbatt': GoldenBattVar, 'hilyte': HiLyteVar, 'wakabah': WakabahVar,
                'shinsey': ShinSeyVar, 'cherrie': CherrieVar, 'asahee': AsaheeVar, 'raizon': RaizonVar, 'vinatabak': VinatabakVar,
                'djisamsue': DjiSamSueVar, 'charminarr': CharminarrVar, 'longglife': LonggLifeVar})


# ---- batch 9: Europe, the Americas, Egypt, Russia
def swords(d, cx, cy, s, col):
    for sx in (-1, 1):
        a, b = (-8 * sx, 10), (8 * sx, -11)
        d.line(_pts(cx, cy, s, [a, b]), fill=col, width=max(2, mm(.9 * s)))
        d.polygon(_pts(cx, cy, s, [b, (b[0] - 1.6 * sx, b[1] + .6), (b[0] - .5 * sx, b[1] + 1.9)]), fill=col)
        g = (a[0] + 16 * sx * .2, a[1] - 21 * .2)
        d.line(_pts(cx, cy, s, [(g[0] - 2.4, g[1] - 1.8 * sx), (g[0] + 2.4, g[1] + 1.8 * sx)]), fill=col, width=max(2, mm(.9 * s)))
        r = mm(1.1 * s)
        px, py = cx + mm(a[0] * s), cy + mm(a[1] * s)
        d.ellipse([px - r, py - r, px + r, py + r], fill=col)


def bridge(d, x0, x1, y, h, col, bg, n=4):
    """A stone bridge of n arches; (x0, x1, y) in px, h in mm."""
    d.polygon([(x0, y - mm(h * .78)), ((x0 + x1) / 2, y - mm(h)), (x1, y - mm(h * .78)), (x1, y), (x0, y)], fill=col)
    w = (x1 - x0) / n
    for k in range(n):
        ax = x0 + w * k
        d.pieslice([ax + w * .14, y - mm(h * .62), ax + w * .86, y + mm(h * .62)], 180, 360, fill=bg)
    d.line([(x0, y - mm(h * .78) - mm(.9)), ((x0 + x1) / 2, y - mm(h) - mm(.9)), (x1, y - mm(h * .78) - mm(.9))], fill=col, width=mm(.45))


def waves9(d, x0, x1, y, col, rows=3, gap=2.2, amp=.7, lam=7., w=.35):
    for r in range(rows):
        yy = y + mm(r * gap)
        pts = [(x, yy + math.sin((x - x0) / mm(lam) * 2 * math.pi + r * 1.6) * mm(amp)) for x in range(int(x0), int(x1), 3)]
        d.line(pts, fill=col, width=mm(w))


def fir(d, cx, base, h, col):
    for k in range(3):
        top = base - mm(h) + mm(h * .24 * k)
        wd = mm(h * (.2 + .1 * k))
        d.polygon([(cx, top), (cx + wd, top + mm(h * .44)), (cx - wd, top + mm(h * .44))], fill=col)
    d.rectangle([cx - mm(h * .04), base - mm(h * .12), cx + mm(h * .04), base], fill=col)


def lighthouse(d, cx, cy, s, col, bg, ray=None):
    if ray:
        for sx in (-1, 1):
            d.polygon(_pts(cx, cy, s, [(sx * 2.2, -11.2), (sx * 20, -17), (sx * 20, -7)]), fill=ray)
    d.polygon(_pts(cx, cy, s, [(-2.4, -8), (2.4, -8), (4.6, 12), (-4.6, 12)]), fill=col)
    for y0, y1 in ((-4, -.5), (3.5, 7.5)):
        f = lambda y: 2.4 + (y + 8) * 2.2 / 20
        d.polygon(_pts(cx, cy, s, [(-f(y0), y0), (f(y0), y0), (f(y1), y1), (-f(y1), y1)]), fill=bg)
    d.rectangle([cx - mm(3.6 * s), cy - mm(8.8 * s), cx + mm(3.6 * s), cy - mm(8 * s)], fill=col)
    d.rectangle([cx - mm(2 * s), cy - mm(12.6 * s), cx + mm(2 * s), cy - mm(8.8 * s)], outline=col, width=max(2, mm(.6 * s)))
    d.polygon(_pts(cx, cy, s, [(-2.8, -12.6), (2.8, -12.6), (0, -15.6)]), fill=col)
    d.rectangle([cx - mm(8 * s), cy + mm(12 * s), cx + mm(8 * s), cy + mm(13.2 * s)], fill=col)


def pyramids(d, cx, base, s, col, shade):
    for ox, h in ((7, 13), (-5, 19)):
        x = cx + mm(ox * s)
        d.polygon([(x - mm(h * s * .8), base), (x, base - mm(h * s)), (x + mm(h * s * .8), base)], fill=col)
        d.polygon([(x, base - mm(h * s)), (x + mm(h * s * .8), base), (x + mm(h * s * .22), base)], fill=shade)


def horse9(d, cx, cy, s, col):
    def P(pts):
        d.polygon([(cx + mm(x * s), cy + mm(y * s)) for x, y in pts], fill=col)
    P([(-5, -1), (4, -1.6), (5.4, .2), (4, 1.6), (-4.6, 1.4), (-5.6, .2)])
    P([(3.6, -1.4), (6.4, -5.2), (8.4, -5.4), (9.4, -4), (8, -3.4), (7.2, -3.8), (5.6, -.2)])
    P([(-5.2, -.6), (-8.6, -2.6), (-9.4, -1), (-5.6, .6)])
    P([(4.4, -2.4), (6, -5.6), (3.4, -3.4)])
    for a, b in (((4, 1), (7.6, 3.4)), ((7.6, 3.4), (8.6, 2.2)), ((3, 1.2), (5.2, 4.8)), ((-3.6, 1.2), (-7, 3.6)), ((-7, 3.6), (-8.2, 5)),
                 ((-4.4, 1.2), (-5, 5))):
        d.line([(cx + mm(a[0] * s), cy + mm(a[1] * s)), (cx + mm(b[0] * s), cy + mm(b[1] * s))], fill=col, width=max(2, mm(.9 * s)))


def shield9(d, cx, cy, s, col, bg, w=.6):
    pts = _pts(cx, cy, s, [(-9, -11), (9, -11), (9, 1), (6.5, 7), (0, 12), (-6.5, 7), (-9, 1)])
    d.polygon(pts, fill=bg, outline=col, width=max(2, mm(w * s)))


def sabres(d, cx, cy, s, col):
    """Two crossed curved sabres, hilts down."""
    for sx in (-1, 1):
        p0, p1, p2 = (-9 * sx, 10), (5 * sx, 6), (9 * sx, -12)
        pts = [((1 - t) ** 2 * p0[0] + 2 * t * (1 - t) * p1[0] + t * t * p2[0], (1 - t) ** 2 * p0[1] + 2 * t * (1 - t) * p1[1] + t * t * p2[1])
               for t in [k / 24 for k in range(25)]]
        d.line(_pts(cx, cy, s, pts), fill=col, width=max(2, mm(1.2 * s)), joint='curve')
        g = pts[5]
        d.line(_pts(cx, cy, s, [(g[0] - 1.2 * sx, g[1] - 2.4), (g[0] + 1.2 * sx, g[1] + 2.4)]), fill=col, width=max(2, mm(1.0 * s)))
        r = mm(1.2 * s)
        px, py = cx + mm(p0[0] * s), cy + mm(p0[1] * s)
        d.ellipse([px - r, py - r, px + r, py + r], fill=col)


def flower9(d, cx, cy, r, col, mid, n=5, rot=0.):
    for k in range(n):
        a = math.radians(rot + k * 360 / n)
        px, py = cx + math.cos(a) * r * .56, cy + math.sin(a) * r * .56
        d.ellipse([px - r * .5, py - r * .5, px + r * .5, py + r * .5], fill=col)
    d.ellipse([cx - r * .3, cy - r * .3, cx + r * .3, cy + r * .3], fill=mid)


class Batch9(Batch8):
    def frame(self, d, W, H, acc, double=True):
        d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=acc, width=mm(.6))
        if double:
            d.rectangle([mm(4.5), mm(4.5), mm(W - 4.5), mm(H - 4.5)], outline=acc, width=mm(.25))


class BoyarrdVar(Batch9):
    key, name, foot = 'boyarrd', 'Boyarrd', 'TABAC BRUN  ·  POUR LES BRAVES  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.rectangle([0, mm(43), mm(W), mm(62)], fill=acc)
        d.ellipse([mm(W / 2 - 14), mm(8), mm(W / 2 + 14), mm(36)], outline=acc, width=mm(.5))
        swords(d, mm(W / 2), mm(22), .92, acc)
        self.name1(d, W, 'BOYARRD', 52, font=SLAB, size=9, m=9, col=self.base, sp=.6)
        self.kind(d, W, 69.5, font=OSW7, size=3.8, col=self.ink, margin=16)
        self.footer(d, W, font=OSW4, size=2.4, col=self.hi(acc))
        return img


class DukadosVar(Batch9):
    key, name, foot = 'dukados', 'Dukados', 'SABOR DE VERDAD  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.polygon([(0, 0), (mm(W), 0), (mm(W), mm(20)), (0, mm(40))], fill=acc)
        shield9(d, mm(W / 2), mm(21), .82, self.base, acc, .9)
        star(d, mm(W / 2), mm(20), mm(4.6), self.base)
        self.name1(d, W, 'Dukados', 54, font=SERIF9, size=11, m=10, sp=0)
        d.line([(mm(10), mm(62.5)), (mm(W - 10), mm(62.5))], fill=self.ink, width=mm(.4))
        self.kind(d, W, 68.5, font=OSW7, size=3.6, col=self.ink, margin=16)
        self.footer(d, W, font=OSW4, size=2.4)
        return img


class F7Var(Batch9):
    key, name, foot = 'f7', 'f7', 'VOLKSEIGENER RAUCH  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.rectangle([mm(5), mm(5), mm(W - 5), mm(7)], fill=acc)
        d.rectangle([mm(5), mm(H - 7), mm(W - 5), mm(H - 5)], fill=acc)
        r = mm(17)
        d.ellipse([mm(W / 2) - r, mm(31) - r, mm(W / 2) + r, mm(31) + r], fill=acc)
        text_c(d, mm(W) / 2, mm(29.5), 'f7', F(SERIF9, mm(22)), self.base)
        self.name1(d, W, 'ZIGARETTEN', 56, font=OSW7, size=4, m=24, sp=2.2)
        d.line([(mm(14), mm(61.5)), (mm(W - 14), mm(61.5))], fill=self.hi(acc), width=mm(.4))
        self.kind(d, W, 67.5, font=OSW7, size=3.4, col=self.hi(acc), margin=14)
        self.footer(d, W, 76.5, font=OSW4, size=2.4)
        return img


class DrinnaVar(Batch9):
    key, name, foot = 'drinna', 'Drinna', 'ON THE GREEN RIVER  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        self.frame(d, W, H, acc, False)
        d.ellipse([mm(W / 2 - 7), mm(7), mm(W / 2 + 7), mm(21)], outline=acc, width=mm(.4))
        bridge(d, mm(7), mm(W - 7), mm(34), 12, acc, self.base)
        waves9(d, mm(7), mm(W - 7), mm(36.5), acc, 3)
        self.name1(d, W, 'DRINNA', 52, font=SERIF9, size=11, m=13)
        ornament(d, mm(W / 2), mm(60.5), mm(30), acc)
        self.kind(d, W, 68, font=CIN7, size=3.6, col=self.hi(acc), margin=16)
        self.footer(d, W)
        return img


class KarpatziVar(Batch9):
    key, name, foot = 'karpatzi', 'Karpatzi', 'AER DE MUNTE  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        self.frame(d, W, H, acc, False)
        peaks(d, mm(3.4), mm(W - 3.4), mm(40), [(.16, 16), (.42, 28), (.7, 21), (.92, 12)], acc, snow=self.base)
        for x, h in ((9, 8), (14, 6), (41, 7), (46.5, 9)):
            fir(d, mm(x), mm(40), h, darker(acc, .7) if lum(acc) > 120 else self.base)
        d.rectangle([mm(3.4), mm(40), mm(W - 3.4), mm(41)], fill=acc)
        self.name1(d, W, 'KARPATZI', 52, font=BEBAS, size=13, m=12, sp=1)
        self.kind(d, W, 64.5, font=OSW7, size=3.8, col=self.hi(acc), margin=16)
        self.footer(d, W, font=OSW4, size=2.5)
        return img


class DelikadosVar(Batch9):
    key, name, foot = 'delikados', 'Delikados', 'HECHO CON CALMA  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        self.frame(d, W, H, acc)
        for k, wd in ((0, .7), (1.6, .25), (3.2, .25)):
            d.ellipse([mm(8 + k), mm(14 + k), mm(W - 8 - k), mm(52 - k)], outline=acc, width=mm(wd))
        text_c(d, mm(W) / 2, mm(31.5), 'Delikados', fit_font(d, 'Delikados', SCRIPT, mm(W - 26), mm(12)), self.ink)
        star(d, mm(W / 2), mm(21), mm(1.6), acc)
        star(d, mm(W / 2), mm(44), mm(1.6), acc)
        ornament(d, mm(W / 2), mm(59), mm(30), acc)
        self.kind(d, W, 66.5, font=CIN7, size=3.4, col=self.hi(acc), margin=14)
        self.footer(d, W, 78)
        return img


class FarrosVar(Batch9):
    key, name, foot = 'farros', 'Farros', 'LUZ EN LA NIEBLA  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=self.ink, width=mm(.6))
        ray = darker(self.base, .88) if lum(self.base) > 120 else tuple(min(255, int(c * 1.5) + 20) for c in self.base[:3])
        lighthouse(d, mm(W / 2), mm(25), 1.12, acc, self.base, ray)
        waves9(d, mm(6), mm(W - 6), mm(41.5), self.ink, 2, gap=1.8)
        self.name1(d, W, 'FARROS', 54, font=SLAB, size=10, m=12, sp=.8)
        self.kind(d, W, 66, font=OSW7, size=3.3, col=self.hi(acc), margin=12)
        self.footer(d, W, font=OSW4, size=2.4)
        return img


class KleopatraVar(Batch9):
    key, name, foot = 'kleopatra', 'Kleopatra', 'QUEEN OF THE NILE BLEND  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        for y in (4, 6, 8, H - 9, H - 7, H - 5):
            d.rectangle([mm(4), mm(y), mm(W - 4), mm(y + .9)], fill=acc)
        r = mm(6)
        d.ellipse([mm(W / 2) - r, mm(19) - r, mm(W / 2) + r, mm(19) + r], fill=acc)
        sunrays(d, mm(W / 2), mm(19), mm(12), acc, 22, 180, 360)
        d.ellipse([mm(W / 2) - mm(7.4), mm(19) - mm(7.4), mm(W / 2) + mm(7.4), mm(19) + mm(7.4)], fill=self.base)
        d.ellipse([mm(W / 2) - r, mm(19) - r, mm(W / 2) + r, mm(19) + r], fill=acc)
        pyramids(d, mm(W / 2), mm(42), .78, acc, darker(acc, .68))
        d.rectangle([mm(6), mm(42), mm(W - 6), mm(42.7)], fill=acc)
        self.name1(d, W, 'KLEOPATRA', 52, size=6.8, m=12)
        self.kind(d, W, 62.5, font=CIN7, size=3.6, col=self.hi(acc), margin=16)
        self.footer(d, W, 72.5)
        return img


class LarkkVar(Batch9):
    key, name, foot = 'larkk', 'Larkk', 'RICHLY REWARDING  ·  UNCOMMONLY COLD  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        d.polygon([(0, mm(30)), (mm(W / 2), mm(14)), (mm(W), mm(30)), (mm(W), mm(44)), (mm(W / 2), mm(28)), (0, mm(44))], fill=acc)
        d.polygon([(0, mm(47)), (mm(W / 2), mm(31)), (mm(W), mm(47)), (mm(W), mm(49)), (mm(W / 2), mm(33)), (0, mm(49))], fill=acc)
        self.name1(d, W, 'LARKK', 60, font=SERIF9, size=13, m=12, sp=1)
        self.kind(d, W, 72, font=OSW7, size=3.4, col=self.hi(acc), margin=16)
        self.footer(d, W, 81, font=OSW4, size=2.2)
        star(d, mm(W / 2), mm(9.5), mm(2.6), self.hi(acc))
        return img


class EeveVar(Batch9):
    key, name, foot = 'eeve', 'Eeve', 'A GARDEN IN YOUR POCKET  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        leafc = darker(acc, .6) if lum(self.base) > 120 else self.ink
        d.line([(mm(11), mm(4)), (mm(11), mm(H - 4))], fill=leafc, width=mm(.35))
        for k, y in enumerate(range(9, 84, 9)):
            sx = 1 if k % 2 else -1
            d.polygon(petal(mm(11), mm(y + 4), .28, 90 * sx + 25 * sx, 16, 5), fill=leafc)
            flower9(d, mm(11 + sx * 3.4), mm(y), mm(2.7), acc, self.base if lum(self.base) > 120 else self.ink, rot=k * 20)
        text_c(d, mm(W / 2 + 6), mm(34), 'Eeve', fit_font(d, 'Eeve', SCRIPT, mm(W - 26), mm(19)), self.ink)
        d.line([(mm(22), mm(46)), (mm(W - 8), mm(46))], fill=acc, width=mm(.3))
        lab = self.v['label']
        text_c(d, mm(W / 2 + 6), mm(52), lab, fit_font(d, lab, CIN7, mm(W - 30), mm(3.2)), self.hi(acc), spacing=mm(.5))
        text_c(d, mm(W / 2 + 6), mm(79), self.foot, fit_font(d, self.foot, CIN7, mm(W - 26), mm(2)), self.ink, spacing=mm(.3))
        return img


class KravenAVar(Batch9):
    key, name, foot = 'kravena', 'Kraven A', 'WILL NOT AFFECT YOUR THROAT, SAYS THE CAT  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        self.frame(d, W, H, acc, False)
        r = mm(13)
        d.ellipse([mm(W / 2) - r, mm(21) - r, mm(W / 2) + r, mm(21) + r], fill=acc)
        cat8(d, mm(W / 2 - 1.4), mm(22.5), .82, self.base)
        self.name1(d, W, 'KRAVEN', 45, font=SERIF9, size=8.4, m=14, sp=.8)
        text_c(d, mm(W) / 2, mm(57.5), '“A”', F(SERIF9, mm(11)), self.hi(acc))
        self.kind(d, W, 70, font=CIN7, size=3.4, col=self.ink, margin=16)
        self.footer(d, W, font=OSW4, size=2.2)
        return img


class TroikkaVar(Batch9):
    key, name, foot = 'troikka', 'Troikka', 'THREE HORSES, ONE SLEIGH  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        self.frame(d, W, H, acc)
        for k in range(9):
            star(d, mm(9 + k * 4.6), mm(10 + (k * 7) % 5), mm(.7), acc)
        for ox, oy, sc in ((-7, -3.4, .92), (0, 0, 1.0), (7, 3.4, 1.08)):
            horse9(d, mm(W / 2 + ox * .6 - 1), mm(25 + oy), sc * 1.18, darker(acc, .72 + .14 * (oy + 3.4) / 3.4) if oy < 3 else acc)
        waves9(d, mm(7), mm(W - 7), mm(37.5), acc, 1, amp=.5, lam=12, w=.4)
        self.name1(d, W, 'TROIKKA', 50, font=SERIF9, size=9, m=13)
        ornament(d, mm(W / 2), mm(58), mm(30), acc)
        self.kind(d, W, 66, font=CIN7, size=3.8, col=self.hi(acc), margin=16)
        self.footer(d, W)
        return img


class MaksimmVar(Batch9):
    key, name, foot = 'maksimm', 'Maksimm', 'TO THE MAXIMUMM  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        for k in range(3):
            y = 8 + k * 9
            d.polygon([(0, mm(y + 14)), (mm(W), mm(y)), (mm(W), mm(y + 5)), (0, mm(y + 19))], fill=acc if k != 1 else self.hi(acc))
        self.name1(d, W, 'MAKSIMM', 55, font=SLAB, size=9.4, m=9, sp=.4)
        d.rectangle([mm(8), mm(62), mm(W - 8), mm(62.8)], fill=self.ink)
        self.kind(d, W, 69, font=OSW7, size=3.8, col=self.ink, margin=16)
        self.footer(d, W, font=OSW4, size=2.5)
        return img


class EgoriyVar(Batch9):
    key, name, foot = 'egoriy', 'Saint Egoriy', 'THE DRAGON QUIT FIRST  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        self.frame(d, W, H, acc)
        shield9(d, mm(W / 2), mm(24), 1.1, acc, self.base, .7)
        d.rectangle([mm(W / 2 - 1.3), mm(14), mm(W / 2 + 1.3), mm(34.5)], fill=acc)
        d.rectangle([mm(W / 2 - 8.4), mm(19.5), mm(W / 2 + 8.4), mm(22.1)], fill=acc)
        d.line([(mm(W / 2 - 15), mm(38)), (mm(W / 2 + 14), mm(9))], fill=acc, width=mm(.7))
        d.polygon([(mm(W / 2 + 16.4), mm(6.6)), (mm(W / 2 + 12.4), mm(8.6)), (mm(W / 2 + 14.4), mm(10.6))], fill=acc)
        self.name2(d, W, 'SAINT', 'EGORIY', 47.5, size=5.4, gap=8.4, m1=34, m2=13)
        ornament(d, mm(W / 2), mm(63), mm(30), acc)
        self.kind(d, W, 70, font=CIN7, size=3.6, col=self.hi(acc), margin=16)
        self.footer(d, W, 80.5)
        return img


class DonskoyVar(Batch9):
    key, name, foot = 'donskoy', 'Donskoy Tabakk', 'FROM THE QUIET RIVER  ·  20'

    def design(self, W, H):
        img, d, acc = self.start(W, H)
        self.frame(d, W, H, acc, False)
        sunrays(d, mm(W / 2), mm(34), mm(21), darker(self.base, .86) if lum(self.base) > 90 else tuple(int(c * 1.5) + 12 for c in self.base[:3]), 16)
        sabres(d, mm(W / 2), mm(21), 1.0, acc)
        waves9(d, mm(6), mm(W - 6), mm(36), acc, 3, gap=2)
        self.name2(d, W, 'DONSKOY', 'TABAKK', 50, font=SERIF9, size=7.4, gap=8.4, m1=14, m2=24)
        self.kind(d, W, 68.5, font=OSW7, size=3.6, col=self.hi(acc), margin=16)
        self.footer(d, W, font=OSW4, size=2.4)
        return img


CLASSES.update({'boyarrd': BoyarrdVar, 'dukados': DukadosVar, 'f7': F7Var, 'drinna': DrinnaVar, 'karpatzi': KarpatziVar, 'delikados': DelikadosVar,
                'farros': FarrosVar, 'kleopatra': KleopatraVar, 'larkk': LarkkVar, 'eeve': EeveVar, 'kravena': KravenAVar, 'troikka': TroikkaVar,
                'maksimm': MaksimmVar, 'egoriy': EgoriyVar, 'donskoy': DonskoyVar})


# ---- batch 10: Canada, the USSR, the Eastern bloc. Emblems: e(img, d, cx, cy, s, col, bg)
def _P(d, cx, cy, s, pts, col, ang=0.):
    d.polygon(_pts(cx, cy, s, pts, ang), fill=col)


def _L(d, cx, cy, s, pts, col, w=.6):
    d.line(_pts(cx, cy, s, pts), fill=col, width=max(2, mm(w * s)), joint='curve')


def _C(d, cx, cy, s, x, y, r, col=None, out=None, w=.5):
    d.ellipse([cx + mm((x - r) * s), cy + mm((y - r) * s), cx + mm((x + r) * s), cy + mm((y + r) * s)], fill=col, outline=out, width=max(2, mm(w * s)) if out else 0)


def e_peaksfir(img, d, cx, cy, s, col, bg):
    peaks(d, cx - mm(19 * s), cx + mm(19 * s), cy + mm(12 * s), [(.18, 13 * s), (.48, 24 * s), (.8, 16 * s)], col, snow=bg)
    for x, h in ((-15, 7), (-10.5, 5), (12, 6), (16, 8)):
        fir(d, cx + mm(x * s), cy + mm(12 * s), h * s, darker(col, .7) if lum(col) > 110 else bg)
    d.rectangle([cx - mm(19 * s), cy + mm(12 * s), cx + mm(19 * s), cy + mm(12.8 * s)], fill=col)


def e_num(txt):
    def f(img, d, cx, cy, s, col, bg):
        _C(d, cx, cy, s, 0, 0, 15, col)
        _C(d, cx, cy, s, 0, 0, 13.2, None, bg, .35)
        text_c(d, cx, cy - mm(1.2 * s), txt, F(SERIF9, mm(17 * s)), bg)
    return f


def e_bars(img, d, cx, cy, s, col, bg):
    for k in range(3):
        y = -12 + k * 8
        _P(d, cx, cy, s, [(-16, y + 8), (16, y), (16, y + 4.4), (-16, y + 12.4)], col if k != 1 else (darker(col, .75) if lum(col) > 110 else col))


def e_sun(img, d, cx, cy, s, col, bg):
    sunrays(d, cx, cy, mm(15 * s), col, 32, 0, 360)
    _C(d, cx, cy, s, 0, 0, 9.6, bg)
    _C(d, cx, cy, s, 0, 0, 8, col)


def e_tartan(img, d, cx, cy, s, col, bg):
    d.rectangle([cx - mm(14 * s), cy - mm(14 * s), cx + mm(14 * s), cy + mm(14 * s)], fill=col)
    for o, w in ((-8, 2.4), (-3.6, .7), (4, 2.4), (8.6, .7)):
        d.rectangle([cx + mm((o - w / 2) * s), cy - mm(14 * s), cx + mm((o + w / 2) * s), cy + mm(14 * s)], fill=bg)
        d.rectangle([cx - mm(14 * s), cy + mm((o - w / 2) * s), cx + mm(14 * s), cy + mm((o + w / 2) * s)], fill=bg)
    for o in (-8, 4):
        for q in (-8, 4):
            d.rectangle([cx + mm((o - 1.2) * s), cy + mm((q - 1.2) * s), cx + mm((o + 1.2) * s), cy + mm((q + 1.2) * s)], fill=col)


def e_fish(img, d, cx, cy, s, col, bg):
    pts = [(13 * math.cos(a) - 2, 5.6 * math.sin(a)) for a in [k * math.pi / 12 for k in range(24)]]
    _P(d, cx, cy, s, pts, col)
    _P(d, cx, cy, s, [(-13, 0), (-19, -6), (-17.4, 0), (-19, 6)], col)
    _P(d, cx, cy, s, [(-4, -5), (1, -9.6), (4, -4.6)], col)
    _C(d, cx, cy, s, 6.4, -1.4, 1.1, bg)
    _L(d, cx, cy, s, [(2.4, -2.5), (1.6, 0), (2.4, 2.5)], bg, .5)
    _L(d, cx, cy, s, [(11, .6), (15, -3), (16, -13)], col, .35)
    waves9(d, cx - mm(18 * s), cx + mm(18 * s), cy + mm(10 * s), col, 2, gap=2 * s)


def e_crest(img, d, cx, cy, s, col, bg):
    shield9(d, cx, cy, s * 1.1, col, bg, .8)
    _P(d, cx, cy, s, [(-9.9, -4.2), (9.9, -4.2), (9.9, -1.8), (-9.9, -1.8)], col)
    star(d, cx, cy - mm(8 * s), mm(2.4 * s), col)
    star(d, cx, cy + mm(4.4 * s), mm(4 * s), col)


def e_x(img, d, cx, cy, s, col, bg):
    for sx in (-1, 1):
        _P(d, cx, cy, s, [(-13 * sx, -14), (-7 * sx, -14), (13 * sx, 14), (7 * sx, 14)], col)


def e_kremlin(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-3.4, 13), (-3.4, 2), (-2.4, 2), (-2.4, -3), (-1.2, -3), (0, -10), (1.2, -3), (2.4, -3), (2.4, 2), (3.4, 2), (3.4, 13)], col)
    star(d, cx, cy - mm(12 * s), mm(2.6 * s), col)
    _C(d, cx, cy, s, 0, 4.6, 1.5, bg)
    for sx in (-1, 1):
        for k in range(4):
            x = sx * (5.2 + k * 3.4)
            _P(d, cx, cy, s, [(x - 1.2, 13), (x - 1.2, 6.6), (x - .5, 6.6), (x - .5, 7.6), (x + .5, 7.6), (x + .5, 6.6), (x + 1.2, 6.6), (x + 1.2, 13)], col)
        _P(d, cx, cy, s, [(sx * 3.4, 13), (sx * 3.4, 8.6), (sx * 17, 8.6), (sx * 17, 13)], col)


def e_aster(img, d, cx, cy, s, col, bg):
    for k in range(18):
        d.polygon(petal(cx, cy, s, k * 20, 14, 1.9), fill=col)
    _C(d, cx, cy, s, 0, 0, 4.6, bg)
    _C(d, cx, cy, s, 0, 0, 3.4, col)


def e_curl(img, d, cx, cy, s, col, bg):
    """Three wisps of smoke over a glowing end."""
    for x0, amp, top, w in ((-7, 2.6, -11, 1.5), (0, 3.2, -15, 2.1), (7, 2.6, -9, 1.5)):
        pts = [(x0 + math.sin((y - 8) / 4.6) * amp * (1 - (y - top) / (8 - top) * .5), y) for y in [top + k * (8 - top) / 30 for k in range(31)]]
        _L(d, cx, cy, s, pts, col, w)
    _P(d, cx, cy, s, [(-12, 10.4), (12, 10.4), (10, 13.6), (-10, 13.6)], col)


def e_news(img, d, cx, cy, s, col, bg):
    d.rectangle([cx - mm(13 * s), cy - mm(14 * s), cx + mm(13 * s), cy + mm(13 * s)], outline=col, width=max(2, mm(.6 * s)))
    d.rectangle([cx - mm(11 * s), cy - mm(12 * s), cx + mm(11 * s), cy - mm(8 * s)], fill=col)
    d.rectangle([cx - mm(11 * s), cy - mm(6 * s), cx - mm(1 * s), cy + mm(2 * s)], fill=col)
    for k in range(5):
        y = -6 + k * 2
        d.rectangle([cx + mm(1 * s), cy + mm(y * s), cx + mm(11 * s), cy + mm((y + .7) * s)], fill=col)
    for k in range(4):
        y = 4.4 + k * 2
        d.rectangle([cx - mm(11 * s), cy + mm(y * s), cx + mm((11 if k < 3 else 3) * s), cy + mm((y + .7) * s)], fill=col)


_DOG = [(-6, 12), (-7.4, -1), (-5.4, -7), (-5, -14), (-1, -8.6), (3, -7.6), (6, -4.4), (11.4, -2), (12, .8), (8.4, 3), (4, 3.2), (2.4, 6.4), (3.4, 12)]


def e_dog(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 0, 0, 15, None, col, .5)
    _P(d, cx - mm(1.5 * s), cy + mm(.5 * s), s * .95, _DOG, col)
    _C(d, cx - mm(1.5 * s), cy + mm(.5 * s), s * .95, 3.4, -3.6, .9, bg)


def e_dogstar(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 0, 0, 15, col)
    _P(d, cx - mm(1.5 * s), cy + mm(1.5 * s), s * .86, _DOG, bg)
    _C(d, cx - mm(1.5 * s), cy + mm(1.5 * s), s * .86, 3.4, -3.6, .9, col)
    d.arc([cx - mm(19 * s), cy - mm(8 * s), cx + mm(19 * s), cy + mm(8 * s)], 150, 30, fill=col, width=max(2, mm(.4 * s)))
    for x, y, r in ((-17, -10, 1.2), (16, -12, 1.6), (18, 8, 1), (-15, 11, .9)):
        star(d, cx + mm(x * s), cy + mm(y * s), mm(r * s), col)


def e_duck(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy - mm(2 * s), s, [(15, -2.4), (11.6, -3.2), (10.4, -5), (8, -4.4), (3, -3), (1, -3.6), (-3, -14), (-7, -12), (-4.4, -3), (-9, -1.4), (-14, -3.4), (-12.4, .6),
                                  (-6, 2.6), (2, 2.6), (7.4, -.2), (10.4, -2)], col)
    for x, h in ((-15, 9), (-12, 6), (-9.4, 8), (10, 7), (13, 10), (16, 6)):
        _L(d, cx, cy, s, [(x, 13), (x + .6, 13 - h)], col, .5)
        _P(d, cx, cy, s, [(x + .1, 13 - h + 2.6), (x + 1.1, 13 - h + 2.6), (x + 1.1, 13 - h - .4), (x + .1, 13 - h - .4)], col)
    d.rectangle([cx - mm(18 * s), cy + mm(13 * s), cx + mm(18 * s), cy + mm(13.6 * s)], fill=col)


def e_plane(img, d, cx, cy, s, col, bg):
    d.arc([cx - mm(22 * s), cy - mm(6 * s), cx + mm(14 * s), cy + mm(30 * s)], 215, 320, fill=col, width=max(2, mm(.4 * s)))
    plane(img, cx + mm(2 * s), cy - mm(3 * s), s * 1.15, col, 16)
    dd = ImageDraw.Draw(img)
    for x, y, r in ((-14, -11, 1.2), (14, 9, 1), (-11, 10, .8)):
        star(dd, cx + mm(x * s), cy + mm(y * s), mm(r * s), col)


def e_northsun(img, d, cx, cy, s, col, bg):
    sunrays(d, cx, cy + mm(6 * s), mm(19 * s), col, 18)
    d.pieslice([cx - mm(9 * s), cy - mm(3 * s), cx + mm(9 * s), cy + mm(15 * s)], 180, 360, fill=bg)
    d.pieslice([cx - mm(7.4 * s), cy - mm(1.4 * s), cx + mm(7.4 * s), cy + mm(13.4 * s)], 180, 360, fill=col)
    d.rectangle([cx - mm(19 * s), cy + mm(6 * s), cx + mm(19 * s), cy + mm(6.8 * s)], fill=col)
    for x, w in ((-12, 7), (2, 9), (11, 5), (-3, 5)):
        y = 9 if w > 6 else 11.6
        _P(d, cx, cy, s, [(x, y), (x + w, y), (x + w - 1, y + 1.2), (x + 1, y + 1.2)], col)


def e_rosette(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 0, 0, 14.6, None, col, .5)
    for k in range(8):
        d.polygon(petal(cx, cy, s, k * 45, 12.4, 3.6), fill=col)
    for k in range(8):
        d.polygon(petal(cx, cy, s, k * 45 + 22.5, 7, 1.7), fill=bg)
    _C(d, cx, cy, s, 0, 0, 2.2, col)


_FLAME = [(0, -15), (4, -9), (7, -3), (8, 3), (5.6, 8.4), (0, 10.6), (-5.6, 8.4), (-8, 3), (-6.2, -2), (-3.6, -5), (-2.6, -1), (-1, -7)]


def e_flame(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, _FLAME, col)
    _P(d, cx, cy + mm(3.4 * s), s * .5, _FLAME, bg)
    for sx in (-1, 1):
        _L(d, cx, cy, s, [(-11 * sx, 9.4), (11 * sx, 14)], col, 2)


def e_planet(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 0, 0, 9, col)
    lay = Image.new('RGBA', img.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(lay)
    ld.ellipse([cx - mm(17 * s), cy - mm(4.4 * s), cx + mm(17 * s), cy + mm(4.4 * s)], outline=rgba(col), width=max(2, mm(1.1 * s)))
    ld.ellipse([cx - mm(15 * s), cy - mm(2.8 * s), cx + mm(15 * s), cy + mm(2.8 * s)], outline=rgba(bg), width=max(2, mm(.5 * s)))
    ld.pieslice([cx - mm(9 * s), cy - mm(9 * s), cx + mm(9 * s), cy + mm(9 * s)], 180, 360, fill=rgba(col))
    img.alpha_composite(lay.rotate(18, center=(cx, cy), resample=Image.BICUBIC))
    dd = ImageDraw.Draw(img)
    for x, y, r in ((-14, -11, 1.4), (15, -9, 1), (13, 11, 1.2), (-12, 12, .8)):
        star(dd, cx + mm(x * s), cy + mm(y * s), mm(r * s), col)


def e_iceberg(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-11, 3), (-7, -6), (-3.4, -3), (1.6, -14), (6, -5), (9, -1.6), (11.4, 3)], col)
    d.line(_pts(cx, cy, s, [(-13, 3), (-9, 10), (-1, 14), (8, 10.4), (13, 3)]), fill=col, width=max(2, mm(.5 * s)))
    waves9(d, cx - mm(19 * s), cx + mm(19 * s), cy + mm(3.6 * s), col, 1, amp=.5 * s, lam=6 * s, w=.45)
    star(d, cx + mm(12 * s), cy - mm(10 * s), mm(1.6 * s), col, 4, .3)


def e_boat(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-12, 6), (13, 6), (9, 10.6), (-9, 10.6)], col)
    _P(d, cx, cy, s, [(.8, -14), (.8, 4), (11, 4)], col)
    _P(d, cx, cy, s, [(-.8, -9), (-.8, 4), (-9, 4)], col)
    d.rectangle([cx - mm(.4 * s), cy - mm(15 * s), cx + mm(.4 * s), cy + mm(6 * s)], fill=col)
    waves9(d, cx - mm(18 * s), cx + mm(18 * s), cy + mm(12.6 * s), col, 2, gap=1.9 * s)


def e_gem(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-12, -4), (-6, -11), (6, -11), (12, -4), (0, 13)], col)
    for a in ([(-12, -4), (12, -4)], [(-6, -11), (-4, -4), (0, 13)], [(6, -11), (4, -4), (0, 13)], [(-4, -4), (0, -11), (4, -4)]):
        _L(d, cx, cy, s, a, bg, .4)
    for x, y in ((-15, -12), (15, -10), (13, 9)):
        star(d, cx + mm(x * s), cy + mm(y * s), mm(1.5 * s), col, 4, .3)


def e_towerpeak(img, d, cx, cy, s, col, bg):
    peaks(d, cx - mm(19 * s), cx + mm(19 * s), cy + mm(13 * s), [(.14, 9 * s), (.5, 17 * s), (.86, 11 * s)], col)
    _P(d, cx, cy, s, [(-2.6, -3), (-1.9, -13), (-2.6, -13), (-2.6, -15), (-1.4, -15), (-1.4, -14), (-.5, -14), (-.5, -15), (.5, -15), (.5, -14), (1.4, -14), (1.4, -15),
                      (2.6, -15), (2.6, -13), (1.9, -13), (2.6, -3)], col)
    d.rectangle([cx - mm(.6 * s), cy - mm(9 * s), cx + mm(.6 * s), cy - mm(6.6 * s)], fill=bg)


def e_phoenix(img, d, cx, cy, s, col, bg):
    for sx in (-1, 1):
        _P(d, cx, cy, s, [(1 * sx, 1), (5 * sx, -6), (13 * sx, -12), (11 * sx, -6), (14.6 * sx, -6), (10 * sx, -1.4), (12.4 * sx, .6), (5 * sx, 4)], col)
    d.ellipse([cx - mm(2.6 * s), cy - mm(4 * s), cx + mm(2.6 * s), cy + mm(7 * s)], fill=col)
    _C(d, cx, cy, s, 0, -6.4, 2.2, col)
    _P(d, cx, cy, s, [(1.4, -7.4), (5, -6.6), (1.6, -5.4)], col)
    _P(d, cx, cy, s, [(-.6, -8.2), (-1.6, -12), (.9, -8.4)], col)
    for a in (150, 180, 210):
        d.polygon(petal(cx, cy + mm(5 * s), s, a, 9.6, 1.9), fill=col)


def e_torch(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy - mm(7 * s), s * .56, _FLAME, col)
    _P(d, cx, cy, s, [(-3.8, -2.2), (3.8, -2.2), (3, -.2), (-3, -.2)], col)
    _P(d, cx, cy, s, [(-2, .4), (2, .4), (.8, 14), (-.8, 14)], col)
    for sx in (-1, 1):
        for k in range(6):
            a = math.radians(100 + k * 26)
            x, y = sx * (-math.cos(a) * 13), 2 - math.sin(a) * 13 + 4
            d.polygon(petal(cx + mm(x * s), cy + mm(y * s), s, sx * (k * 26 - 20), 5, 1.5), fill=col)


def e_club(img, d, cx, cy, s, col, bg):
    for x, y in ((0, -6.4), (-6, 2), (6, 2)):
        _C(d, cx, cy, s, x, y, 5.6, col)
    _P(d, cx, cy, s, [(-1.4, 0), (1.4, 0), (4, 13), (-4, 13)], col)


def e_rose(img, d, cx, cy, s, col, bg):
    _L(d, cx, cy, s, [(0, 0), (-1, 8), (1.4, 15)], col, .8)
    d.polygon(petal(cx - mm(.6 * s), cy + mm(8 * s), s, -70, 8, 2.6), fill=col)
    d.polygon(petal(cx + mm(.6 * s), cy + mm(11 * s), s, 70, 7, 2.4), fill=col)
    flower9(d, cx, cy - mm(4 * s), mm(10 * s), col, col, 6)
    flower9(d, cx, cy - mm(4 * s), mm(5.6 * s), bg, col, 5, 36)
    _C(d, cx, cy, s, 0, -4, 3.4, col)
    _C(d, cx, cy, s, 0, -4, 1.4, bg)


def e_helmet(img, d, cx, cy, s, col, bg):
    d.arc([cx - mm(12 * s), cy - mm(15 * s), cx + mm(12 * s), cy + mm(11 * s)], 195, 345, fill=col, width=max(2, mm(3 * s)))
    _P(d, cx, cy, s, [(-7, 12), (-8.4, 1), (-7.4, -5), (-4, -9.4), (0, -10.6), (4, -9.4), (7.4, -5), (8.4, 1), (7, 12), (2.4, 12), (1.2, 4), (-1.2, 4), (-2.4, 12)], col)
    _P(d, cx, cy, s, [(-5.4, -1.6), (-1.2, -.4), (-1.2, 2), (-5, 1)], bg)
    _P(d, cx, cy, s, [(5.4, -1.6), (1.2, -.4), (1.2, 2), (5, 1)], bg)


def e_mono(ch):
    def f(img, d, cx, cy, s, col, bg):
        _C(d, cx, cy, s, 0, 0, 15, None, col, .7)
        _C(d, cx, cy, s, 0, 0, 13, None, col, .25)
        text_c(d, cx, cy - mm((1.4 if len(ch) == 1 else 1.) * s), ch, F(SERIF9, mm((19 if len(ch) == 1 else 12.5) * s)), col)
    return f


def e_chevrons(img, d, cx, cy, s, col, bg):
    for k in range(3):
        x = -15 + k * 10
        _P(d, cx, cy, s, [(x, -12), (x + 5, -12), (x + 12, 0), (x + 5, 12), (x, 12), (x + 7, 0)], col)


def e_column(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-8, -14), (8, -14), (8, -12), (6, -9.6), (-6, -9.6), (-8, -12)], col)
    _P(d, cx, cy, s, [(-5, -9), (5, -9), (5.6, 9), (-5.6, 9)], col)
    for x in (-2.8, 0, 2.8):
        _L(d, cx, cy, s, [(x, -8), (x * 1.1, 8)], bg, .45)
    _P(d, cx, cy, s, [(-7, 9.6), (7, 9.6), (7, 11.4), (9, 11.4), (9, 13.6), (-9, 13.6), (-9, 11.4), (-7, 11.4)], col)


def e_lyre(img, d, cx, cy, s, col, bg):
    for sx in (-1, 1):
        p0, p1, p2, p3 = (2.4 * sx, 10), (16 * sx, 6), (11 * sx, -8), (5 * sx, -14)
        pts = [((1 - t) ** 3 * p0[0] + 3 * t * (1 - t) ** 2 * p1[0] + 3 * t * t * (1 - t) * p2[0] + t ** 3 * p3[0],
                (1 - t) ** 3 * p0[1] + 3 * t * (1 - t) ** 2 * p1[1] + 3 * t * t * (1 - t) * p2[1] + t ** 3 * p3[1]) for t in [k / 20 for k in range(21)]]
        _L(d, cx, cy, s, pts, col, 1.7)
        _C(d, cx, cy, s, 5 * sx, -14, 1.5, col)
    _P(d, cx, cy, s, [(-4.4, 9.4), (4.4, 9.4), (6.4, 13.6), (-6.4, 13.6)], col)
    d.rectangle([cx - mm(8.4 * s), cy - mm(9 * s), cx + mm(8.4 * s), cy - mm(7.6 * s)], fill=col)
    for x in (-3, -1, 1, 3):
        _L(d, cx, cy, s, [(x, -8), (x * .7, 9.6)], col, .3)


def e_stripeshield(img, d, cx, cy, s, col, bg):
    shield9(d, cx, cy, s * 1.1, col, bg, .8)
    for k in range(4):
        y = -9.4 + k * 3.2
        d.rectangle([cx - mm(8 * s), cy + mm(y * s), cx - mm(.6 * s), cy + mm((y + 1.6) * s)], fill=col)
    d.rectangle([cx + mm(4 * s), cy - mm(9.6 * s), cx + mm(5.4 * s), cy + mm(3.4 * s)], fill=col)
    d.rectangle([cx + mm(2 * s), cy - mm(6.4 * s), cx + mm(7.4 * s), cy - mm(5.2 * s)], fill=col)
    d.rectangle([cx + mm(2.8 * s), cy - mm(3.2 * s), cx + mm(6.6 * s), cy - mm(2 * s)], fill=col)
    d.pieslice([cx - mm(5 * s), cy + mm(4 * s), cx + mm(5 * s), cy + mm(14 * s)], 180, 360, fill=col)


def e_checker(img, d, cx, cy, s, col, bg):
    for i in range(-2, 3):
        for j in range(-2, 3):
            if (i + j) % 2 == 0 and abs(i) + abs(j) <= 3:
                x, y = i * 5.6, j * 5.6
                _P(d, cx, cy, s, [(x, y - 5.2), (x + 5.2, y), (x, y + 5.2), (x - 5.2, y)], col)


def e_riversun(img, d, cx, cy, s, col, bg):
    sunrays(d, cx, cy + mm(2 * s), mm(17 * s), col, 16)
    d.pieslice([cx - mm(9 * s), cy - mm(7 * s), cx + mm(9 * s), cy + mm(11 * s)], 180, 360, fill=bg)
    d.pieslice([cx - mm(7.4 * s), cy - mm(5.4 * s), cx + mm(7.4 * s), cy + mm(9.4 * s)], 180, 360, fill=col)
    waves9(d, cx - mm(19 * s), cx + mm(19 * s), cy + mm(4.6 * s), col, 4, gap=2.4 * s, amp=.8 * s, lam=9 * s, w=.5)


def e_none(img, d, cx, cy, s, col, bg):
    pass


# key: (emblem, layout, name font, bottom line). Layouts: A framed, C open with big type, D badge, T type only, B band
B10 = {
    'canclassics': (e_peaksfir, 'A', BEBAS, 'ROCKY MOUNTAIN BLEND  ·  20'), 'numero7': (e_num('7'), 'D', OSW7, 'ALMOST LUCKY  ·  20'),
    'bellmont': (e_bars, 'C', SERIF9, 'SMOOTH AS A FROZEN LAKE  ·  20'), 'matinay': (e_sun, 'A', SCRIPT, 'FOR A QUIET AFTERNOON  ·  20'),
    'macdonnald': (e_tartan, 'B', SERIF9, 'THE LASS SENDS HER REGARDS  ·  20'), 'sportsmann': (e_fish, 'A', SLAB, 'GONE FISHING  ·  20'),
    'peterjaxon': (e_crest, 'A', CIN9, 'EXTRA EVERYTHING  ·  20'), 'marktenn': (e_x, 'C', BEBAS, 'TEN OUT OF TEN  ·  20'),
    'stolitchnye': (e_kremlin, 'A', CIN9, 'CAPITAL SMOKE  ·  20'), 'astrra': (e_aster, 'A', SERIF9, 'THROUGH THORNS  ·  20'),
    'dymokk': (e_curl, 'C', SLAB, 'A LITTLE SMOKE  ·  20'), 'novostt': (e_news, 'C', SERIF9, 'ALL THE NEWS THAT FITS  ·  20'),
    'laikka': (e_dogstar, 'D', OSW7, 'FIRST IN ORBIT  ·  20'), 'drugg': (e_dog, 'A', SERIF9, 'A FRIEND WHO WAITS  ·  20'),
    'okhotnitchyi': (e_duck, 'A', CIN9, 'FOR THE LONG WAIT IN THE REEDS  ·  20'), 'polyott': (e_plane, 'A', OSW7, 'CLEARED FOR TAKE-OFF  ·  20'),
    'severr': (e_northsun, 'C', SLAB, 'PAPIROSY  ·  20'), 'lubitelskiye': (e_rosette, 'A', SERIF9, 'FOR THOSE WHO KNOW  ·  20'),
    'vattra': (e_flame, 'C', SLAB, 'KEEP THE FIRE  ·  20'), 'orbitta': (e_planet, 'A', OSW7, 'ONE MORE TURN AROUND  ·  20'),
    'arktikka': (e_iceberg, 'C', BEBAS, 'COLDER THAN IT LOOKS  ·  20'), 'ligerros': (e_boat, 'A', SERIF9, 'TABACO NEGRO DE LA ISLA  ·  20'),
    'btt': (e_none, 'T', SLAB, 'BALKAN BLEND  ·  20'), 'rodoppi': (e_riversun, 'A', CIN9, 'MOUNTAIN LEAF  ·  20'),
    'opall': (e_gem, 'A', SERIF9, 'A SMALL GEM  ·  20'), 'shipkka': (e_towerpeak, 'C', SLAB, 'HOLD THE PASS  ·  20'),
    'fenixx': (e_phoenix, 'A', CIN9, 'UP FROM THE ASHES  ·  20'), 'sportt': (e_torch, 'C', BEBAS, 'FASTER, HIGHER, SMOKIER  ·  20'),
    'popularrne': (e_none, 'T', OSW7, 'EVERYBODY HAS THEM  ·  20'), 'klubbowe': (e_club, 'A', SERIF9, 'MEMBERS ONLY  ·  20'),
    'karmen': (e_rose, 'A', SCRIPT, 'LOVE IS A REBELLIOUS BIRD  ·  20'), 'spartta': (e_helmet, 'C', SLAB, 'THIS IS SMOKING  ·  20'),
    'pettra': (e_mono('P'), 'A', SERIF9, 'PRAGUE EVENING  ·  20'), 'startt': (e_chevrons, 'C', BEBAS, 'ON YOUR MARKS  ·  20'),
    'sopianna': (e_column, 'A', CIN9, 'ROMAN ROAD BLEND  ·  20'), 'symfonia': (e_lyre, 'A', SERIF9, 'IN THREE MOVEMENTS  ·  20'),
    'kossutth': (e_stripeshield, 'A', SLAB, 'STRONG AS A SPEECH  ·  20'), 'karro': (e_checker, 'C', BEBAS, 'KRAFTIG  ·  20'),
    'clubb': (e_mono('C'), 'B', SERIF9, 'WORLD TASTE, CORNER SHOP  ·  20'), 'moravva': (e_riversun, 'C', SERIF9, 'DOWN THE SLOW RIVER  ·  20'),
    'filter58': (e_num('58'), 'D', OSW7, 'ONE BETTER THAN LAST YEAR  ·  20'),
}
B10['rodoppi'] = (lambda img, d, cx, cy, s, col, bg: (peaks(d, cx - mm(19 * s), cx + mm(19 * s), cy + mm(12 * s), [(.2, 15 * s), (.46, 22 * s), (.7, 18 * s), (.9, 10 * s)], col, snow=bg),
                                                      _C(d, cx, cy, s, 12, -10, 3.4, col)), 'A', CIN9, 'MOUNTAIN LEAF  ·  20')


class Batch10(Batch9):
    def design(self, W, H):
        img, d, acc = self.start(W, H)
        em0, lay, font, self.foot = B10.get(self.key) or B11[self.key]
        em = lambda im, dd, cx, cy, sc, c1, c2: em0(im, dd, cx, cy, sc * min(1., W / 55.), c1, c2)
        nm = self.name if font == SCRIPT else self.name.upper()
        two = ' ' in nm and len(nm) > 9
        hi = self.hi(acc)
        big = font in (BEBAS,)
        if lay == 'A':
            self.frame(d, W, H, acc, len(self.key) % 2 == 0)
            em(img, d, mm(W / 2), mm(23.5), .95, acc, self.base); d = ImageDraw.Draw(img)
            if two:
                a, b = nm.split(' ', 1)
                self.name2(d, W, a, b, 47, font=font, size=6.4, gap=8, m1=14, m2=14)
                ornament(d, mm(W / 2), mm(62), mm(30), acc)
                self.kind(d, W, 68.5, font=CIN7, size=3.6, col=hi, margin=16)
            else:
                self.name1(d, W, nm, 51 if font != SCRIPT else 49, font=font, size=14 if big else 13 if font == SCRIPT else 8.6, m=13, sp=0 if font == SCRIPT else .5)
                ornament(d, mm(W / 2), mm(59.5), mm(30), acc)
                self.kind(d, W, 67, font=CIN7, size=3.6, col=hi, margin=16)
            self.footer(d, W)
        elif lay == 'C':
            em(img, d, mm(W / 2), mm(24), 1.0, acc, self.base); d = ImageDraw.Draw(img)
            d.rectangle([mm(6), mm(42.6), mm(W - 6), mm(43.4)], fill=acc)
            if two:
                a, b = nm.split(' ', 1)
                self.name2(d, W, a, b, 50, font=font, size=9, gap=9, m1=12, m2=12)
                self.kind(d, W, 70, font=OSW7, size=3.6, col=hi, margin=16)
            else:
                self.name1(d, W, nm, 53, font=font, size=15 if big else 10, m=11, sp=1 if big else .5)
                d.rectangle([mm(14), mm(60.6), mm(W - 14), mm(61)], fill=self.ink)
                self.kind(d, W, 67, font=OSW7, size=3.7, col=hi, margin=16)
            self.footer(d, W, font=OSW4, size=2.5)
        elif lay == 'D':
            d.rectangle([mm(3), mm(3), mm(W - 3), mm(H - 3)], outline=acc, width=mm(.4))
            em(img, d, mm(W / 2), mm(25), 1.06, acc, self.base); d = ImageDraw.Draw(img)
            self.name1(d, W, nm, 51, font=font, size=6.4, m=14, sp=1.6)
            d.rectangle([mm(12), mm(57.4), mm(W - 12), mm(57.8)], fill=acc)
            self.kind(d, W, 64, font=OSW7, size=3.6, col=hi, margin=16)
            self.footer(d, W, 77, font=OSW4, size=2.5)
        elif lay == 'T':
            d.rectangle([mm(5), mm(5), mm(W - 5), mm(9)], fill=acc)
            d.rectangle([mm(5), mm(H - 9), mm(W - 5), mm(H - 5)], fill=acc)
            for k in range(3):
                d.rectangle([mm(5), mm(11 + k * 1.6), mm(W - 5), mm(11.5 + k * 1.6)], fill=acc)
            if len(nm) <= 4:
                text_c(d, mm(W) / 2, mm(33), nm, fit_font(d, nm, font, mm(W - 12), mm(24)), acc)
                self.name1(d, W, 'CIGARETTES', 52, font=OSW7, size=3.6, m=26, sp=2.2)
            else:
                text_c(d, mm(W) / 2, mm(31), nm, fit_font(d, nm, font, mm(W - 10), mm(12)), self.ink, spacing=mm(.4))
                d.rectangle([mm(10), mm(39), mm(W - 10), mm(39.8)], fill=acc)
                self.name1(d, W, 'PAPIEROSY', 46, font=OSW4, size=3.2, m=26, sp=2.6, col=hi)
            self.kind(d, W, 62, font=OSW7, size=4, col=hi, margin=16)
            self.footer(d, W, 74, font=OSW4, size=2.5)
        else:   # B: the name on a band of the accent colour
            em(img, d, mm(W / 2), mm(22), .92, acc, self.base); d = ImageDraw.Draw(img)
            d.rectangle([0, mm(42), mm(W), mm(60)], fill=acc)
            self.name1(d, W, nm, 50.6, font=font, size=8.4, m=9, col=self.base, sp=.5)
            self.kind(d, W, 67.5, font=OSW7, size=3.8, col=self.ink, margin=16)
            self.footer(d, W, font=OSW4, size=2.4, col=hi)
        return img


for _k in catalog.BATCH10:
    CLASSES[_k] = type('B10_' + _k, (Batch10,), dict(key=_k, name=catalog.BRANDS[_k][0], foot=B10[_k][3]))


# ---- batch 11 emblems
def e_crown(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-12, 8), (-14, -7), (-7.4, 0), (-3.6, -10), (0, -1), (3.6, -10), (7.4, 0), (14, -7), (12, 8)], col)
    for x, y in ((-14, -8.6), (-3.6, -11.8), (3.6, -11.8), (14, -8.6)):
        _C(d, cx, cy, s, x, y, 1.6, col)
    d.rectangle([cx - mm(12 * s), cy + mm(9.4 * s), cx + mm(12 * s), cy + mm(12.6 * s)], fill=col)
    for x in (-7, 0, 7):
        _C(d, cx, cy, s, x, 11, .9, bg)


def e_anchor(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 0, -11.4, 2.8, None, col, 1.1)
    d.rectangle([cx - mm(.9 * s), cy - mm(9 * s), cx + mm(.9 * s), cy + mm(11 * s)], fill=col)
    d.rectangle([cx - mm(6 * s), cy - mm(6.4 * s), cx + mm(6 * s), cy - mm(4.8 * s)], fill=col)
    d.arc([cx - mm(10.5 * s), cy - mm(9 * s), cx + mm(10.5 * s), cy + mm(12 * s)], 10, 170, fill=col, width=max(2, mm(1.8 * s)))
    for sx in (-1, 1):
        _P(d, cx, cy, s, [(sx * 12.6, .4), (sx * 8, 3.6), (sx * 12.4, 5.6)], col)


def e_vine(img, d, cx, cy, s, col, bg):
    pts = [(math.sin(t * 2.2 * math.pi) * 5, -14 + t * 28) for t in [k / 40 for k in range(41)]]
    _L(d, cx, cy, s, pts, col, .7)
    for k in range(7):
        x, y = pts[3 + k * 5]
        sx = 1 if k % 2 else -1
        d.polygon(petal(cx + mm(x * s), cy + mm(y * s), s, sx * 70, 8.4, 2.8), fill=col)
    for x, y in ((6, -11), (-7, 8)):
        flower9(d, cx + mm(x * s), cy + mm(y * s), mm(3 * s), col, bg, 5)


def e_crescent(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, -2, 0, 12, col)
    _C(d, cx, cy, s, 2, 0, 10, bg)
    star(d, cx + mm(7 * s), cy, mm(4.4 * s), col, rot=-18)


def e_leaf(img, d, cx, cy, s, col, bg):
    leaf8(d, cx, cy + mm(13 * s), s * 1.15, col, bg, 0, 24, 8.4)


def e_bigstar(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 0, 0, 15, None, col, .6)
    star(d, cx, cy + mm(.6 * s), mm(11.6 * s), col)


def e_eiffel(img, d, cx, cy, s, col, bg):
    for sx in (-1, 1):
        _P(d, cx, cy, s, [(sx * .5, -15), (sx * 1.2, -15), (sx * 2.2, -5), (sx * 4.4, 4), (sx * 9, 13), (sx * 5.6, 13), (sx * 2.6, 5), (sx * .5, 5)], col)
    for y, w in ((-5, 3.2), (4, 5.6)):
        d.rectangle([cx - mm(w * s), cy + mm(y * s), cx + mm(w * s), cy + mm((y + 1.3) * s)], fill=col)
    d.pieslice([cx - mm(3.4 * s), cy + mm(8 * s), cx + mm(3.4 * s), cy + mm(18 * s)], 180, 360, fill=bg)
    d.rectangle([cx - mm(.3 * s), cy - mm(18 * s), cx + mm(.3 * s), cy - mm(15 * s)], fill=col)


def e_pyr(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 9, -9, 3.6, col)
    pyramids(d, cx, cy + mm(12 * s), s * .86, col, darker(col, .68))
    d.rectangle([cx - mm(18 * s), cy + mm(12 * s), cx + mm(18 * s), cy + mm(12.7 * s)], fill=col)


def e_horse(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 0, 0, 15, None, col, .5)
    horse9(d, cx - mm(.6 * s), cy, s * 1.28, col)


def e_coin(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 0, 0, 14, col)
    _C(d, cx, cy, s, 0, 0, 12, None, bg, .35)
    for k in range(40):
        a = k * math.pi / 20
        _L(d, cx, cy, s, [(math.cos(a) * 12.8, math.sin(a) * 12.8), (math.cos(a) * 13.8, math.sin(a) * 13.8)], bg, .3)
    star(d, cx, cy, mm(6 * s), bg)


def e_house(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-13, 0), (0, -11), (13, 0), (10, 0), (10, 12), (-10, 12), (-10, 0)], col)
    _P(d, cx, cy, s, [(6, -12), (9, -12), (9, -3.4), (6, -6)], col)
    d.rectangle([cx - mm(2.2 * s), cy + mm(4.6 * s), cx + mm(2.2 * s), cy + mm(12 * s)], fill=bg)
    for sx in (-1, 1):
        d.rectangle([cx + mm((sx * 6 - 1.5) * s), cy + mm(3 * s), cx + mm((sx * 6 + 1.5) * s), cy + mm(6 * s)], fill=bg)
    _L(d, cx, cy, s, [(7.5, -13), (8.6, -15.4), (7, -17.4)], col, .6)
    d.rectangle([cx - mm(17 * s), cy + mm(12 * s), cx + mm(17 * s), cy + mm(12.7 * s)], fill=col)


def e_pagoda(img, d, cx, cy, s, col, bg):
    pagoda(d, cx, cy - mm(1 * s), s * .86, col)


def e_pagoda3(img, d, cx, cy, s, col, bg):
    pagoda(d, cx, cy + mm(1 * s), s * 1.05, col, tiers=3, hill=False)
    d.rectangle([cx - mm(17 * s), cy + mm(13 * s), cx + mm(17 * s), cy + mm(13.7 * s)], fill=col)


def e_crane(img, d, cx, cy, s, col, bg):
    _C(d, cx, cy, s, 5, -5, 9, None, col, .4)
    crane(d, cx, cy + mm(1 * s), s * 1.05, col, -10)


def e_gate(img, d, cx, cy, s, col, bg):
    gate(d, cx, cy, s * 1.0, col, bg)


def e_plum(img, d, cx, cy, s, col, bg):
    _L(d, cx, cy, s, [(-14, 13), (-6, 5), (-1, 2), (4, -6), (12, -12)], col, 1.1)
    _L(d, cx, cy, s, [(-6, 5), (-8, -5)], col, .8)
    for x, y, r in ((4, -6, 4.6), (-8, -7, 3.8), (11, -12, 3), (-1, 3, 3.4), (-13, 9, 2.6)):
        flower9(d, cx + mm(x * s), cy + mm(y * s), mm(r * s), col, bg, 5, -90)


def e_lotus(img, d, cx, cy, s, col, bg):
    lotus(d, cx, cy + mm(7 * s), s * .95, col, bg)


def e_rain(img, d, cx, cy, s, col, bg):
    for x, y, r in ((-6, -6, 6), (2, -8.4, 7.4), (9, -5, 5)):
        _C(d, cx, cy, s, x, y, r, col)
    d.rectangle([cx - mm(8 * s), cy - mm(6 * s), cx + mm(10 * s), cy - mm(.4 * s)], fill=col)
    for k, x in enumerate((-9, -4, 1, 6, 11)):
        y = 3 + (k % 2) * 3
        _L(d, cx, cy, s, [(x, y), (x - 2.4, y + 6)], col, .9)


def e_rooster(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(4, -12), (7.4, -9), (11, -8), (7.6, -6.4), (7, -2), (4, 3), (-2, 6), (-6, 5), (-9, 0), (-12, -9), (-14.4, -3), (-13, 4), (-9, 8.6),
                      (-2, 10), (4, 8), (7.6, 3), (2, -3), (2, -9)], col)
    for k in range(3):
        _C(d, cx, cy, s, 2.4 + k * 2, -13 - (k == 1) * .8, 1.5, col)
    _C(d, cx, cy, s, 6.6, -4.6, 1.5, col)
    for x in (-3.4, .6):
        _L(d, cx, cy, s, [(x, 9), (x, 13.4), (x + 2.4, 13.4)], col, .7)
    _C(d, cx, cy, s, 5, -9.6, .7, bg)


def e_palm(img, d, cx, cy, s, col, bg):
    _L(d, cx, cy, s, [(-3, 13), (-1.6, 4), (1, -5)], col, 1.5)
    for a in (-150, -110, -70, -30, 10, 190):
        d.polygon(petal(cx + mm(1 * s), cy - mm(5 * s), s, a + 90, 11, 2.3), fill=col)
    _C(d, cx, cy, s, 11, -9, 3, col)
    waves9(d, cx - mm(18 * s), cx + mm(18 * s), cy + mm(13.4 * s), col, 2, gap=1.9 * s)


def e_hammer(img, d, cx, cy, s, col, bg):
    _P(d, cx, cy, s, [(-1.3, -4), (1.3, -4), (1.3, 13), (-1.3, 13)], col, 35)
    _P(d, cx, cy, s, [(-7, -11), (7, -11), (7, -5), (-7, -5)], col, 35)
    d.arc([cx - mm(15 * s), cy - mm(15 * s), cx + mm(15 * s), cy + mm(15 * s)], 60, 300, fill=col, width=max(2, mm(.6 * s)))
    for k in range(8):
        a = math.radians(60 + k * 240 / 7)
        _C(d, cx, cy, s, math.cos(a) * 15, math.sin(a) * 15, 1.5, col)


def e_peaks(img, d, cx, cy, s, col, bg):
    peaks(d, cx - mm(19 * s), cx + mm(19 * s), cy + mm(12 * s), [(.2, 15 * s), (.46, 22 * s), (.7, 18 * s), (.9, 10 * s)], col, snow=bg)
    _C(d, cx, cy, s, 12, -10, 3.4, col)


B11 = {
    'hbee': (e_crown, 'A', SERIF9, 'FROHEN HERZENS  ·  20'), 'ernte24': (e_num('24'), 'D', OSW7, 'ONE DAY AFTER THE HARVEST  ·  20'),
    'revall': (e_boat, 'C', SLAB, 'KRAFTIG UND EHRLICH  ·  20'), 'junno': (e_rosette, 'C', SERIF9, 'THICK AND ROUND, AND WHY NOT  ·  20'),
    'woodbyne': (e_vine, 'A', SERIF9, 'WILD ON THE HEDGE  ·  20'), 'seniorservise': (e_boat, 'A', CIN9, 'THE PERFECTION OF PATIENCE  ·  20'),
    'kapstan': (e_anchor, 'A', SLAB, 'NAVY CUT  ·  20'), 'embassey': (e_crest, 'A', CIN9, 'DIPLOMATIC POUCH  ·  20'),
    'regall': (e_crown, 'C', SERIF9, 'FIT FOR A SMALL KING  ·  20'), 'sweetaftonn': (e_riversun, 'A', SERIF9, 'FLOW GENTLY  ·  20'),
    'majorr': (e_chevrons, 'C', BEBAS, 'PROMOTED IN THE FIELD  ·  20'), 'emms': (e_none, 'T', SLAB, 'MONOPOLIO DI FUMO  ·  20'),
    'nazionalli': (e_boat, 'C', SERIF9, 'DI TUTTI  ·  20'), 'fortunna': (e_sun, 'C', BEBAS, 'LA SUERTE ES TUYA  ·  20'),
    'celtass': (e_helmet, 'A', SLAB, 'DUROS COMO EL NORTE  ·  20'), 'idealles': (e_none, 'T', OSW7, 'CALADA A CALADA  ·  20'),
    'sgventill': (e_mono('SG'), 'B', SERIF9, 'AR FRESCO  ·  20'), 'suavve': (e_boat, 'A', SERIF9, 'SUAVE COMO O MAR  ·  20'),
    'karellia': (e_column, 'A', CIN9, 'SINCE THE OLD GODS  ·  20'), 'assoss': (e_mono('A'), 'C', SLAB, 'FROM THE WHITE VILLAGE  ·  20'),
    'samsunn': (e_crescent, 'A', SERIF9, 'BLACK SEA LEAF  ·  20'), 'maltepeh': (e_towerpeak, 'A', CIN9, "SOLDIER'S OWN  ·  20"),
    'bafrah': (e_leaf, 'C', SLAB, 'LEAF AND NOTHING ELSE  ·  20'), 'princce': (e_crown, 'D', OSW7, 'I AM ALSO A PRINCE  ·  20'),
    'northstaate': (e_bigstar, 'C', BEBAS, 'TRUE NORTH  ·  20'), 'klubbi': (e_num('77'), 'D', OSW7, 'SAUNA AFTERWARDS  ·  20'),
    'blennd': (e_bars, 'A', OSW7, 'LAGOM  ·  20'), 'johnsylver': (e_anchor, 'C', SERIF9, 'ONE LEG, TWO LUNGS  ·  20'),
    'parisienn': (e_eiffel, 'A', SCRIPT, 'FROM THE OTHER PARIS  ·  20'), 'marylongg': (e_rose, 'A', SCRIPT, 'TAKE YOUR TIME, MARY  ·  20'),
    'memfis': (e_pyr, 'A', CIN9, 'DOWN THE DANUBE, UP THE NILE  ·  20'), 'belgga': (e_crest, 'C', SLAB, 'TROIS COULEURS  ·  20'),
    'bastoss': (e_checker, 'A', SLAB, 'TABAC NOIR  ·  20'), 'caballerro': (e_horse, 'A', SERIF9, 'RIDE SLOWLY  ·  20'),
    'oldegold': (e_coin, 'A', SERIF9, 'A TREASURE OF SORTS  ·  20'), 'raleygh': (e_mono('R'), 'A', CIN9, 'SAVE THE COUPONS  ·  20'),
    'tareytonn': (e_bars, 'C', SERIF9, 'WOULD RATHER WALK THAN SWITCH  ·  20'), 'slimms': (e_rose, 'A', SCRIPT, 'A LONG WAY THROUGH THE SNOW  ·  20'),
    'caprii': (e_sun, 'C', SERIF9, 'ISLAND THIN  ·  20'), 'mistyy': (e_curl, 'A', SCRIPT, 'LIGHT AS FOG  ·  20'),
    'meritt': (e_chevrons, 'A', OSW7, 'EARNED, NOT GIVEN  ·  20'), 'carltonn': (e_crest, 'A', CIN9, 'LOWEST OF THE LOW  ·  20'),
    'trew': (e_none, 'T', OSW7, 'MOSTLY AIR  ·  20'), 'dorall': (e_gem, 'C', SERIF9, 'TASTE ME, SAID THE PACK  ·  20'),
    'basik': (e_none, 'T', BEBAS, 'IT SMOKES  ·  20'), 'giration': (e_bigstar, 'C', OSW7, 'FIELD RATION ACCESSORY  ·  20'),
    'ekko': (e_peaks, 'C', BEBAS, 'ECHO  ·  ECHO  ·  ECHO  ·  20'), 'kabin': (e_house, 'A', SLAB, 'A ROOF AND A STOVE  ·  20'),
    'castter': (e_rosette, 'A', SCRIPT, 'SOFT AS VANILLA  ·  20'), 'pianissimmo': (e_lyre, 'A', SCRIPT, 'VERY, VERY QUIETLY  ·  20'),
    'liquun': (e_pagoda, 'A', CIN9, 'BY THE WEST LAKE  ·  20'), 'baishah': (e_crane, 'A', SERIF9, 'WHITE SAND, WHITE CRANE  ·  20'),
    'nanjinng': (e_gate, 'A', CIN9, 'SOUTHERN CAPITAL  ·  20'), 'frontgatte': (e_gate, 'C', SLAB, 'IN FRONT OF THE BIG GATE  ·  20'),
    'hatamenn': (e_pagoda3, 'A', SERIF9, 'OLD CITY GATE  ·  20'), 'hongmey': (e_plum, 'A', SERIF9, 'RED PLUM IN THE SNOW  ·  20'),
    'thiss': (e_none, 'T', BEBAS, 'NOT THAT  ·  20'), 'k89': (e_num('89'), 'D', OSW7, 'ONE AFTER EIGHTY-EIGHT  ·  20'),
    'arirangg': (e_peaks, 'A', SERIF9, 'OVER THE PASS  ·  20'), 'goldflaik': (e_coin, 'C', SERIF9, 'HONEY DEW BLEND  ·  20'),
    'wyllis': (e_anchor, 'A', CIN9, 'FOR GENTLEMEN AT SEA  ·  20'), 'fourskware': (e_checker, 'C', SLAB, 'LIVE LIFE SQUARELY  ·  20'),
    'k3': (e_peaks, 'C', BEBAS, 'ONE HIGHER  ·  20'), 'sampurna': (e_mono('A'), 'C', SERIF9, 'NINE STARS OF LUCK  ·  20'),
    'bentoell': (e_gem, 'A', SLAB, 'SWEET CLOVE  ·  20'), 'wismilakk': (e_crown, 'A', CIN9, 'HAND ROLLED, SLOWLY  ·  20'),
    'krongthipp': (e_lotus, 'A', SERIF9, 'CITY OF ANGELS BLEND  ·  20'), 'fallingrainn': (e_rain, 'C', SERIF9, 'WET SEASON LEAF  ·  20'),
    'fortoon': (e_bigstar, 'A', SLAB, 'LUCK IN A SOFT PACK  ·  20'), 'championn': (e_torch, 'A', BEBAS, 'SECOND TO NONE  ·  20'),
    'jockeyklub': (e_horse, 'C', SERIF9, 'ALWAYS AT THE RAIL  ·  20'), 'particularres': (e_none, 'T', SLAB, 'PICADURA FUERTE  ·  20'),
    'k4371': (e_num('43'), 'D', OSW7, 'ALMOST SEVENTY  ·  20'), 'holywood': (e_bigstar, 'C', BEBAS, 'AO SUCESSO  ·  20'),
    'derbby': (e_chevrons, 'A', SLAB, 'SEMPRE NA FRENTE  ·  20'), 'roosterr': (e_rooster, 'C', SLAB, 'UP BEFORE THE SUN  ·  20'),
    'gunstonn': (e_bars, 'C', BEBAS, 'MEN OF THE BUSH  ·  20'), 'lexingtonn': (e_crest, 'C', SERIF9, 'AFTER THE CHORES  ·  20'),
    'winfeeld': (e_sun, 'A', SLAB, 'NO WORRIES  ·  20'), 'longbeech': (e_palm, 'A', SERIF9, 'SAND IN EVERYTHING  ·  20'),
    'prilukki': (e_leaf, 'A', SERIF9, 'FROM THE BLACK EARTH  ·  20'), 'russkystyle': (e_crown, 'A', CIN9, 'WITH A BROAD GESTURE  ·  20'),
    'makhorrka': (e_leaf, 'C', OSW7, 'COARSE CUT  ·  20'), 'vegga': (e_bigstar, 'A', OSW7, 'BRIGHTEST IN THE LYRE  ·  20'),
    'juwell': (e_gem, 'C', SERIF9, 'EIN KLEINER SCHATZ  ·  20'), 'kabinett': (e_mono('K'), 'A', SERIF9, 'FOR THE COMRADE DIRECTOR  ·  20'),
    'opatiya': (e_boat, 'C', SERIF9, 'ADRIATIC EVENING  ·  20'), 'munkass': (e_hammer, 'C', SLAB, 'END OF THE SHIFT  ·  20'),
}
for _k in catalog.BATCH11:
    CLASSES[_k] = type('B11_' + _k, (Batch10,), dict(key=_k, name=catalog.BRANDS[_k][0], foot=B11[_k][3]))


import emblems12
emblems12.install(globals())

if __name__ == '__main__':
    out = sys.argv[1]
    only = set(sys.argv[2:])
    os.makedirs(out, exist_ok=True)
    for i, (bkey, bname, cls, v) in enumerate(catalog.all_varieties()):
        if only and bkey not in only:
            continue
        use_shape(v['shape'])
        b = CLASSES[cls](v)
        im = build_atlas(b, i).resize((1024, 1024), Image.LANCZOS).convert('RGB')
        im.save(os.path.join(out, 'pack_%s.jpg' % catalog.tex_key(bkey, v)), quality=90)
        print('ok', catalog.tex_key(bkey, v), v['shape'])

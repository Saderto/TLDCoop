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

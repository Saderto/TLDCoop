"""Cigarette pack textures (SmokesBySeagull). Draws a UV atlas per brand.

Atlas layout (mm, scale S px/mm), pack 55 x 88 x 22 mm, lid 18 mm:
  row 0 (y 0..22):   top face above front (x 22..77)
  row 1 (y 22..110): left side 0..22 | front 22..77 | right side 77..99 | back 99..154
  row 2 (y 110..132): bottom face under front (x 22..77)
"""
import math, random, sys, os
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

HERE = os.path.dirname(os.path.abspath(__file__))
FONTS = os.path.join(HERE, '..', 'fonts')
S = 13  # px per mm
W, H, D, LID = 55, 88, 22, 18
ATLAS = (2048, 2048)


def F(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), int(size))

SCRIPT = 'great-vibes-latin-400-normal.woff'
SERIF9 = 'playfair-display-latin-900-normal.woff'
SERIF7 = 'playfair-display-latin-700-normal.woff'
CIN7 = 'cinzel-latin-700-normal.woff'
CIN9 = 'cinzel-latin-900-normal.woff'
OSW7 = 'oswald-latin-700-normal.woff'
OSW4 = 'oswald-latin-400-normal.woff'
BEBAS = 'bebas-neue-latin-400-normal.woff'
SLAB = 'alfa-slab-one-latin-400-normal.woff'
ABRIL = 'abril-fatface-latin-400-normal.woff'


def mm(v):
    return int(round(v * S))


def text_c(d, cx, cy, s, font, fill, stroke=0, sfill=None, spacing=0):
    fit = getattr(font, '_fit', None)
    if fit and (spacing or stroke):
        # 11-09: fit_font measured the plain text; letter spacing and the outline made names run over the frame lines
        fname, max_w = fit
        def width(f, sp):
            return sum(d.textlength(ch, font=f) for ch in s) + sp * (len(s) - 1) + 2 * stroke if sp else d.textlength(s, font=f) + 2 * stroke
        if width(font, spacing) > max_w and spacing:
            spacing = max(0, (max_w - width(font, 0)) / max(1, len(s) - 1))
        size = font.size
        while width(font, spacing) > max_w and size > 6:
            size -= 1
            font = F(fname, size)
    if spacing:
        # letter-spaced text
        widths = [d.textlength(ch, font=font) for ch in s]
        total = sum(widths) + spacing * (len(s) - 1)
        x = cx - total / 2
        for ch, w in zip(s, widths):
            d.text((x, cy), ch, font=font, fill=fill, anchor='lm', stroke_width=stroke, stroke_fill=sfill)
            x += w + spacing
        return
    d.text((cx, cy), s, font=font, fill=fill, anchor='mm', stroke_width=stroke, stroke_fill=sfill)


def fit_font(d, s, fname, max_w, start):
    size = start
    while size > 6:
        f = F(fname, size)
        if d.textlength(s, font=f) <= max_w:
            f._fit = (fname, max_w)
            return f
        size -= 2
    f = F(fname, 6)
    f._fit = (fname, max_w)
    return f


def face(w, h, bg):
    return Image.new('RGBA', (mm(w), mm(h)), bg)


# ---------------------------------------------------------------- shapes
def poly_scaled(d, pts, ox, oy, sc, fill):
    d.polygon([(ox + x * sc, oy + y * sc) for x, y in pts], fill=fill)


def llama(d, ox, oy, sc, fill):
    # side view, facing left; unit box ~ 100 x 110
    d.ellipse([ox + 30 * sc, oy + 50 * sc, ox + 95 * sc, oy + 82 * sc], fill=fill)  # body
    d.polygon([(ox + 30 * sc, oy + 66 * sc), (ox + 26 * sc, oy + 14 * sc), (ox + 40 * sc, oy + 12 * sc), (ox + 46 * sc, oy + 60 * sc)], fill=fill)  # neck
    d.ellipse([ox + 14 * sc, oy + 8 * sc, ox + 42 * sc, oy + 24 * sc], fill=fill)  # head
    d.polygon([(ox + 30 * sc, oy + 11 * sc), (ox + 33 * sc, oy - 2 * sc), (ox + 36 * sc, oy + 10 * sc)], fill=fill)  # ears
    d.polygon([(ox + 36 * sc, oy + 11 * sc), (ox + 40 * sc, oy - 1 * sc), (ox + 42 * sc, oy + 12 * sc)], fill=fill)
    for x0 in (36, 48, 76, 86):
        d.rounded_rectangle([ox + x0 * sc, oy + 70 * sc, ox + (x0 + 6) * sc, oy + 108 * sc], radius=2 * sc, fill=fill)
    d.ellipse([ox + 90 * sc, oy + 54 * sc, ox + 98 * sc, oy + 66 * sc], fill=fill)  # short fluffy tail



def catmull(pts, n=14, closed=True):
    out=[]
    P=pts+pts[:3] if closed else [pts[0]]+pts+[pts[-1]]
    rng=len(pts) if closed else len(pts)-1
    for i in range(rng):
        p0,p1,p2,p3=P[i],P[i+1],P[i+2],P[i+3]
        for k in range(n):
            t=k/n;t2=t*t;t3=t2*t
            x=0.5*((2*p1[0])+(-p0[0]+p2[0])*t+(2*p0[0]-5*p1[0]+4*p2[0]-p3[0])*t2+(-p0[0]+3*p1[0]-3*p2[0]+p3[0])*t3)
            y=0.5*((2*p1[1])+(-p0[1]+p2[1])*t+(2*p0[1]-5*p1[1]+4*p2[1]-p3[1])*t2+(-p0[1]+3*p1[1]-3*p2[1]+p3[1])*t3)
            out.append((x,y))
    return out

# profile facing right, box 100 x 150
BODY=[(64,38),(68,46),(70,52),(70.5,55),(75,63),(76.5,65.5),(73,67),(72.6,68.6),(74,70.5),(71.4,72),(73,73.6),(71.8,75.6),(69.4,77.4),(70.6,80.5),(69,84),(64,87),(59,89),(58.5,96),(60,108),(70,122),(86,134),(90,150),
      (12,150),(16,134),(24,124),(38,116),(43,106),(44,96),(40,91)]
HAIR=[(40,91),(30,92),(22,86),(20,76),(22,64),(28,52),(36,44),(48,38),(60,36),(64,38),(62,46),(56,50),(50,56),(46,66),(48,80),(46,90)]
HAT=[(26,66),(24,52),(28,38),(38,27),(52,22),(64,25),(72,33),(75,41),(81,48),(79,50.5),(70,47),(60,45),(48,46),(38,52),(32,62)]

def lady2(d, ox, oy, sc, fill, accent=None, skin=None):
    T=lambda pts:[(ox+x*sc,oy+y*sc) for x,y in pts]
    d.polygon(T(catmull(BODY)),fill=fill)
    d.polygon(T(catmull(HAIR)),fill=fill)
    d.ellipse(T([(42,40),(68,92)]),fill=fill)
    d.polygon(T(catmull(HAT)),fill=fill)
    if accent:
        # hat band + pearls + earring + cigarette holder with smoke
        d.line(T(catmull([(27,56),(30,46),(40,38),(54,35),(66,37),(74,43)],8,False)),fill=accent,width=max(2,int(1.6*sc)))
        for i in range(9):
            a=math.radians(200+i*14)
            x=52+18*math.cos(a)*-1; y=110+9*math.sin(a)*-1
        for i,(x,y) in enumerate([(46,104),(49,107),(52.5,109),(56,110.5),(59.5,111),(63,110.5)]):
            d.ellipse([ox+(x-1.5)*sc,oy+(y-1.5)*sc,ox+(x+1.5)*sc,oy+(y+1.5)*sc],fill=accent)
        d.ellipse([ox+48*sc,oy+80*sc,ox+51.5*sc,oy+83.5*sc],fill=accent)
        # holder from lips forward-down
        d.line([(ox+73.5*sc,oy+72.5*sc),(ox+99*sc,oy+86*sc)],fill=fill,width=max(2,int(1.4*sc)))
        d.line([(ox+92*sc,oy+82.3*sc),(ox+99*sc,oy+86*sc)],fill=accent,width=max(2,int(1.6*sc)))
        smoke=catmull([(99,85),(102,78),(97,70),(101,60),(96,50),(100,40)],10,False)
        d.line(T(smoke),fill=accent,width=max(1,int(0.8*sc)))



def building(d, ox, oy, sc, fill):
    # symmetric parliament: main block, two wings with evenly spaced turrets, central tower + spire; unit 100 x 70
    d.rectangle([ox, oy + 40 * sc, ox + 100 * sc, oy + 70 * sc], fill=fill)
    for x0 in (4, 64):  # wings
        d.rectangle([ox + x0 * sc, oy + 32 * sc, ox + (x0 + 32) * sc, oy + 40 * sc], fill=fill)
        for k in range(3):
            tx = x0 + 4 + k * 12
            d.rectangle([ox + (tx - 2.5) * sc, oy + 26 * sc, ox + (tx + 2.5) * sc, oy + 32 * sc], fill=fill)
            d.polygon([(ox + (tx - 3.2) * sc, oy + 26.2 * sc), (ox + tx * sc, oy + 19 * sc), (ox + (tx + 3.2) * sc, oy + 26.2 * sc)], fill=fill)
    d.rectangle([ox + 42 * sc, oy + 10 * sc, ox + 58 * sc, oy + 40 * sc], fill=fill)
    d.polygon([(ox + 41 * sc, oy + 10.5 * sc), (ox + 50 * sc, oy - 6 * sc), (ox + 59 * sc, oy + 10.5 * sc)], fill=fill)


def windows(d, ox, oy, sc, fill):
    for i in range(12):
        x = ox + (4 + i * 8) * sc
        if 40 <= 4 + i * 8 <= 58:
            continue
        d.rectangle([x, oy + 46 * sc, x + 3 * sc, oy + 60 * sc], fill=fill)


def leaf(img, cx, cy, length, angle, fill, vein):
    L = int(length)
    lay = Image.new('RGBA', (L * 2, L * 2), (0, 0, 0, 0))
    ld = ImageDraw.Draw(lay)
    ld.ellipse([L * 0.5, L * 0.78, L * 1.5, L * 1.22], fill=fill)
    ld.polygon([(L * 1.45, L), (L * 1.75, L * 0.96), (L * 1.75, L * 1.04)], fill=fill)
    ld.line([(L * 0.55, L), (L * 1.5, L)], fill=vein, width=max(2, L // 30))
    for t in (0.75, 0.95, 1.15, 1.32):
        ld.line([(L * t, L), (L * (t - 0.1), L * 0.86)], fill=vein, width=max(1, L // 45))
        ld.line([(L * t, L), (L * (t - 0.1), L * 1.14)], fill=vein, width=max(1, L // 45))
    lay = lay.rotate(angle, resample=Image.BICUBIC)
    img.alpha_composite(lay, (int(cx - L), int(cy - L)))


def crest(d, cx, cy, r, fill, inner, letter, font):
    pts = [(cx - r, cy - r), (cx + r, cy - r), (cx + r, cy + r * 0.2), (cx, cy + r * 1.25), (cx - r, cy + r * 0.2)]
    d.polygon(pts, fill=fill)
    k = 0.8
    pts2 = [(cx - r * k, cy - r * k), (cx + r * k, cy - r * k), (cx + r * k, cy + r * 0.15), (cx, cy + r * 1.05), (cx - r * k, cy + r * 0.15)]
    d.polygon(pts2, fill=inner)
    d.text((cx, cy + r * 0.05), letter, font=font, fill=fill, anchor='mm')


def barcode(d, x, y, w, h, seed):
    rnd = random.Random(seed)
    d.rectangle([x, y, x + w, y + h], fill=(250, 250, 246))
    cx = x + w * 0.06
    while cx < x + w * 0.94:
        bw = rnd.choice([1, 1, 2, 3]) * max(1, w // 120)
        if rnd.random() < 0.55:
            d.rectangle([cx, y + h * 0.08, cx + bw - 1, y + h * 0.78], fill=(20, 20, 20))
        cx += bw
    f = F(OSW4, h * 0.18)
    d.text((x + w / 2, y + h * 0.9), ' '.join(str(rnd.randint(10000, 99999)) for _ in range(2)), font=f, fill=(20, 20, 20), anchor='mm')


def warning_box(img, x, y, w, h, line1, line2, color=(255, 255, 255), ink=(15, 15, 15)):
    d = ImageDraw.Draw(img)
    d.rectangle([x, y, x + w, y + h], fill=ink)
    p = mm(1.2)
    d.rectangle([x + p, y + p, x + w - p, y + h - p], fill=color)
    f1 = fit_font(d, line1, OSW7, w - 4 * p, h * 0.30)
    f2 = fit_font(d, line2, OSW7, w - 4 * p, h * 0.26)
    d.text((x + w / 2, y + h * 0.33), line1, font=f1, fill=ink, anchor='mm')
    d.text((x + w / 2, y + h * 0.68), line2, font=f2, fill=ink, anchor='mm')


WARNINGS = [
    ('SMOKING KILLS', 'FASTER THAN WOLVES'),
    ('WARNING', 'SMOKE SCARES NOTHING AWAY'),
    ('SMOKING CAUSES', 'SHORTNESS OF BREATH'),
    ('TOBACCO IS', 'HIGHLY ADDICTIVE'),
    ('YOUR LUNGS', 'NEED THIS COLD AIR'),
    ('CIGARETTES', 'WILL NOT KEEP YOU WARM'),
]


# ---------------------------------------------------------------- brands
class Brand:
    key = ''
    name = ''

    def front(self):
        raise NotImplementedError

    def back(self):
        return self.front()

    def side(self, right):
        img = face(D, H, self.base)
        d = ImageDraw.Draw(img)
        f = F(OSW7, mm(4.2))
        f2 = F(OSW4, mm(2.6))
        lay = Image.new('RGBA', (mm(H), mm(D)), (0, 0, 0, 0))
        ld = ImageDraw.Draw(lay)
        if right:
            ld.text((mm(H) / 2, mm(D) * 0.38), '20 CLASS A CIGARETTES', font=f, fill=self.ink, anchor='mm')
            ld.text((mm(H) / 2, mm(D) * 0.72), 'TAR 10 mg  NICOTINE 0.8 mg  CO 10 mg', font=f2, fill=self.ink, anchor='mm')
        else:
            ld.text((mm(H) / 2, mm(D) * 0.38), self.name.upper(), font=F(CIN7, mm(5)), fill=self.ink, anchor='mm')
            ld.text((mm(H) / 2, mm(D) * 0.74), 'MADE IN CANADA  ·  FABRIQUÉ AU CANADA', font=f2, fill=self.ink, anchor='mm')
        lay = lay.rotate(90 if right else -90, expand=True)
        img.alpha_composite(lay)
        return img

    def top(self):
        img = face(W, D, self.base)
        d = ImageDraw.Draw(img)
        text_c(d, mm(W) / 2, mm(D) / 2, self.name.upper(), F(CIN7, mm(5.5)), self.ink)
        return img

    def bottom(self):
        img = face(W, D, self.base)
        d = ImageDraw.Draw(img)
        text_c(d, mm(W) / 2, mm(D) * 0.35, 'LOT 0%d7B  ·  2014' % (len(self.name)), F(OSW4, mm(3)), self.ink)
        text_c(d, mm(W) / 2, mm(D) * 0.68, 'KEEP DRY  ·  KEEP AWAY FROM FIRE', F(OSW4, mm(2.6)), self.ink)
        return img

    def back_default(self, seed):
        img = face(W, H, self.base)
        d = ImageDraw.Draw(img)
        self.decorate_back(img, d)
        w1, w2 = WARNINGS[seed % len(WARNINGS)]
        warning_box(img, mm(4), mm(H * 0.50), mm(W - 8), mm(H * 0.25), w1, w2)
        barcode(d, mm(W * 0.28), mm(H * 0.80), mm(W * 0.44), mm(H * 0.13), seed)
        return img

    def decorate_back(self, img, d):
        text_c(d, mm(W) / 2, mm(H * 0.30), self.name.upper(), fit_font(d, self.name.upper(), CIN9, mm(W - 7), mm(7)), self.ink)


class Chapgirl(Brand):
    key, name = 'chapgirl', 'Chapgirl'
    base, ink = (110, 28, 36, 255), (226, 186, 110, 255)

    def front(self):
        img = face(W, H, self.base)
        d = ImageDraw.Draw(img)
        for i in range(0, mm(W), mm(2.4)):
            d.line([(i, 0), (i, mm(H))], fill=(98, 22, 30, 255), width=mm(0.8))
        gold, dark = self.ink, (70, 16, 22, 255)
        d.rectangle([mm(2.5), mm(2.5), mm(W - 2.5), mm(H - 2.5)], outline=gold, width=mm(0.9))
        d.rectangle([mm(4), mm(4), mm(W - 4), mm(H - 4)], outline=gold, width=mm(0.35))
        for cx, cy in ((4, 4), (W - 4, 4), (4, H - 4), (W - 4, H - 4)):
            d.regular_polygon((mm(cx), mm(cy), mm(1.8)), 4, rotation=45, fill=gold)
        text_c(d, mm(W) / 2, mm(9.5), 'SINCE 1928', F(CIN7, mm(3.4)), gold, spacing=mm(0.6))
        # cameo
        cx, cy, rx, ry = mm(W / 2), mm(38), mm(15), mm(18)
        d.ellipse([cx - rx - mm(1.2), cy - ry - mm(1.2), cx + rx + mm(1.2), cy + ry + mm(1.2)], fill=gold)
        d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=(238, 222, 196, 255))
        mask = Image.new('L', img.size, 0)
        ImageDraw.Draw(mask).ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=255)
        lay = Image.new('RGBA', img.size, (0, 0, 0, 0))
        lady2(ImageDraw.Draw(lay), cx - mm(55 * 0.25), cy - mm(19.5), mm(0.25), dark, accent=(190, 140, 70, 255))
        img.paste(lay, (0, 0), ImageChops.multiply(mask, lay.getchannel('A')))
        text_c(d, mm(W) / 2, mm(66), 'Chapgirl', F(SCRIPT, mm(15)), gold, stroke=mm(0.25), sfill=dark)
        d.line([(mm(12), mm(74)), (mm(W - 12), mm(74))], fill=gold, width=mm(0.4))
        text_c(d, mm(W) / 2, mm(78.5), 'CHERRY  ·  20 CIGARETTES', F(CIN7, mm(3)), gold)
        return img

    def back(self):
        return self.back_default(1)

    def decorate_back(self, img, d):
        d.rectangle([mm(2.5), mm(2.5), mm(W - 2.5), mm(H - 2.5)], outline=self.ink, width=mm(0.9))
        text_c(d, mm(W) / 2, mm(H * 0.30), 'Chapgirl', F(SCRIPT, mm(13)), self.ink)


class Milston(Brand):
    key, name = 'milston', 'Milston'
    base, ink = (240, 236, 226, 255), (150, 22, 30, 255)

    def front(self):
        img = face(W, H, self.base)
        d = ImageDraw.Draw(img)
        red, gold = (176, 26, 34, 255), (196, 160, 82, 255)
        d.rectangle([0, 0, mm(W), mm(22)], fill=red)
        d.rectangle([0, mm(22), mm(W), mm(23.2)], fill=gold)
        text_c(d, mm(W) / 2, mm(11), 'FULL FLAVOR', F(OSW7, mm(4.2)), (250, 240, 230, 255), spacing=mm(0.8))
        crest(d, mm(W / 2), mm(35), mm(7), red, self.base, 'M', F(SERIF9, mm(8)))
        f = fit_font(d, 'MILSTON', SERIF9, mm(W - 8), mm(14))
        text_c(d, mm(W) / 2, mm(56), 'MILSTON', f, red, stroke=mm(0.3), sfill=(90, 10, 16, 255))
        d.line([(mm(8), mm(64)), (mm(W - 8), mm(64))], fill=gold, width=mm(0.6))
        text_c(d, mm(W) / 2, mm(69), 'CLASSIC', F(CIN7, mm(4)), (40, 40, 40, 255), spacing=mm(0.9))
        d.rectangle([0, mm(H - 9), mm(W), mm(H)], fill=red)
        text_c(d, mm(W) / 2, mm(H - 4.5), '20 FILTER CIGARETTES', F(OSW4, mm(3.2)), (250, 240, 230, 255))
        return img

    def back(self):
        return self.back_default(0)


def frost(d, red):
    # snow cap + icicles along the chevron roof edge
    rnd = random.Random(5)
    ice, shade = (246, 250, 255, 255), (200, 218, 236, 255)
    edge = [(0, 40), (W / 2 - 2, 26), (W / 2 + 2, 26), (W, 40)]
    for (x0, y0), (x1, y1) in zip(edge, edge[1:]):
        n = max(2, int(abs(x1 - x0) / 2.2))
        for i in range(n):
            t = (i + 0.5) / n
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            L = rnd.uniform(4, 10) if rnd.random() < 0.6 else rnd.uniform(1.5, 3.5)
            d.polygon([(mm(x - 0.9), mm(y - 0.3)), (mm(x + 0.9), mm(y - 0.3)), (mm(x + 0.1), mm(y + L))], fill=shade)
            d.polygon([(mm(x - 0.6), mm(y - 0.3)), (mm(x + 0.3), mm(y - 0.3)), (mm(x - 0.1), mm(y + L * 0.85))], fill=ice)
        d.line([(mm(x0), mm(y0) - mm(0.6)), (mm(x1), mm(y1) - mm(0.6))], fill=ice, width=mm(1.6))
    d.rectangle([0, 0, mm(W), mm(2.2)], fill=ice)
    for i in range(14):
        x = rnd.uniform(0, W); y = rnd.uniform(3, 20)
        d.text((mm(x), mm(y)), '*', font=F(OSW7, mm(rnd.uniform(2, 3.5))), fill=(255, 255, 255, 200), anchor='mm')


class Marlbrrro(Brand):
    key, name = 'marlbrrro', 'Marlbrrro'
    base, ink = (244, 241, 233, 255), (30, 26, 26, 255)

    def front(self):
        img = face(W, H, self.base)
        d = ImageDraw.Draw(img)
        red = (190, 30, 38, 255)
        # chevron roof
        d.polygon([(0, 0), (mm(W), 0), (mm(W), mm(40)), (mm(W / 2 + 2), mm(26)), (mm(W / 2 - 2), mm(26)), (0, mm(40))], fill=red)
        d.ellipse([mm(W / 2 - 4), mm(19), mm(W / 2 + 4), mm(27)], fill=(225, 190, 110, 255))
        d.ellipse([mm(W / 2 - 3), mm(20), mm(W / 2 + 3), mm(26)], fill=red)
        d.polygon([(mm(W / 2 - 2), mm(24.5)), (mm(W / 2 - 2.2), mm(21.5)), (mm(W / 2 - 1), mm(22.8)), (mm(W / 2), mm(21)), (mm(W / 2 + 1), mm(22.8)), (mm(W / 2 + 2.2), mm(21.5)), (mm(W / 2 + 2), mm(24.5))], fill=(225, 190, 110, 255))
        f = fit_font(d, 'Marlbrrro', SERIF9, mm(W - 6), mm(14))
        text_c(d, mm(W) / 2, mm(55), 'Marlbrrro', f, self.ink)
        text_c(d, mm(W) / 2, mm(66), 'FILTER CIGARETTES', F(OSW4, mm(3.6)), self.ink, spacing=mm(0.5))
        text_c(d, mm(W) / 2, mm(80), '20', F(SERIF7, mm(6)), red)
        return img

    def back(self):
        return self.back_default(2)

    def decorate_back(self, img, d):
        d.polygon([(0, 0), (mm(W), 0), (mm(W), mm(26)), (mm(W / 2), mm(16)), (0, mm(26))], fill=(190, 30, 38, 255))
        text_c(d, mm(W) / 2, mm(H * 0.38), 'Marlbrrro', F(SERIF9, mm(8)), self.ink)


class Parliamint(Brand):
    key, name = 'parliamint', 'Parliamint'
    base, ink = (24, 46, 86, 255), (236, 240, 244, 255)

    def front(self):
        img = face(W, H, self.base)
        d = ImageDraw.Draw(img)
        white, mint = (236, 240, 244, 255), (110, 196, 160, 255)
        for y in range(mm(H)):
            t = y / mm(H)
            d.line([(0, y), (mm(W), y)], fill=(int(18 + 20 * t), int(36 + 30 * t), int(72 + 26 * t), 255))
        d.rectangle([0, mm(50), mm(W), mm(H)], fill=white)
        building(d, mm(5), mm(28), mm(0.45), white)
        windows(d, mm(5), mm(28), mm(0.45), (24, 46, 86, 255))
        d.rectangle([0, mm(49), mm(W), mm(50.5)], fill=mint)
        f = fit_font(d, 'PARLIAMINT', CIN9, mm(W - 6), mm(9))
        text_c(d, mm(W) / 2, mm(12), 'PARLIAMINT', f, white)
        leaf(img, mm(W * 0.30), mm(66), mm(9), 25, mint, (60, 140, 110, 255))
        leaf(img, mm(W * 0.40), mm(68), mm(7), -15, (90, 176, 140, 255), (50, 120, 95, 255))
        text_c(d, mm(W * 0.62), mm(66), 'MENTHOL', F(OSW7, mm(4.6)), (40, 120, 92, 255))
        text_c(d, mm(W) / 2, mm(80), 'RECESSED FILTER  ·  20', F(OSW4, mm(3.2)), self.base)
        return img

    def back(self):
        return self.back_default(3)


class Bent(Brand):
    key, name = 'bent', 'Bent'
    base, ink = (246, 246, 244, 255), (22, 44, 96, 255)

    def front(self):
        img = face(W, H, self.base)
        d = ImageDraw.Draw(img)
        blue = (32, 72, 150, 255)
        light = (120, 160, 214, 255)
        # bent band: thick arc on left side
        d.pieslice([mm(-70), mm(-10), mm(18), mm(H + 10)], -60, 60, fill=blue)
        d.pieslice([mm(-70), mm(-4), mm(12), mm(H + 4)], -60, 60, fill=self.base)
        d.pieslice([mm(-74), mm(-14), mm(24), mm(H + 14)], -50, 50, fill=None, outline=light, width=mm(1.2))
        f = fit_font(d, 'BENT', OSW7, mm(W - 18), mm(22))
        text_c(d, mm(W * 0.6), mm(38), 'BENT', f, self.ink)
        text_c(d, mm(W * 0.6), mm(52), 'BLUE  ·  8', F(OSW4, mm(5)), blue, spacing=mm(0.5))
        d.rounded_rectangle([mm(W * 0.6 - 10), mm(60), mm(W * 0.6 + 10), mm(66)], radius=mm(1), fill=blue)
        text_c(d, mm(W * 0.6), mm(63), 'SMOOTH', F(OSW7, mm(3.4)), (246, 246, 244, 255))
        text_c(d, mm(W) / 2, mm(80), '20 CIGARETTES', F(OSW4, mm(3.2)), self.ink)
        return img

    def back(self):
        return self.back_default(4)


class Caramel(Brand):
    key, name = 'caramel', 'Caramel'
    base, ink = (204, 168, 108, 255), (70, 40, 20, 255)

    def front(self):
        img = face(W, H, self.base)
        d = ImageDraw.Draw(img)
        brown = (92, 54, 26, 255)
        dark = (62, 36, 18, 255)
        text_c(d, mm(W) / 2, mm(12), 'CARAMEL', fit_font(d, 'CARAMEL', SLAB, mm(W - 6), mm(11)), brown)
        # mountains + pines
        d.polygon([(0, mm(62)), (mm(10), mm(44)), (mm(18), mm(52)), (mm(30), mm(36)), (mm(44), mm(54)), (mm(50), mm(48)), (mm(W), mm(58)), (mm(W), mm(66)), (0, mm(66))], fill=(176, 136, 80, 255))
        d.polygon([(mm(26), mm(41)), (mm(30), mm(36)), (mm(34), mm(41)), (mm(31), mm(40)), (mm(30), mm(42)), (mm(29), mm(40))], fill=(240, 226, 196, 255))
        for x in (5, 9, 47, 51):
            d.polygon([(mm(x), mm(52)), (mm(x - 2.2), mm(62)), (mm(x + 2.2), mm(62))], fill=(110, 80, 44, 255))
        llama(d, mm(11), mm(30), mm(0.30), dark)
        d.rectangle([0, mm(65), mm(W), mm(66)], fill=brown)
        text_c(d, mm(W) / 2, mm(71), 'TURKISH & DOMESTIC', F(OSW4, mm(3.4)), brown)
        text_c(d, mm(W) / 2, mm(75.5), 'BLEND CIGARETTES', F(OSW4, mm(3.4)), brown)
        text_c(d, mm(W) / 2, mm(82), '20', F(SLAB, mm(4.6)), brown)
        return img

    def back(self):
        return self.back_default(5)

    def decorate_back(self, img, d):
        llama(d, mm(W / 2 - 9), mm(10), mm(0.18), (62, 36, 18, 255))
        text_c(d, mm(W) / 2, mm(38), 'CARAMEL', F(SLAB, mm(7)), (92, 54, 26, 255))


BRANDS = [Chapgirl(), Milston(), Marlbrrro(), Parliamint(), Bent(), Caramel()]


# ---------------------------------------------------------------- weathering
def grunge(img, seed, strength=1.0):
    rnd = random.Random(seed)
    w, h = img.size
    # soft multi-scale noise
    noise = Image.effect_noise((w // 8, h // 8), 40).resize((w, h), Image.BICUBIC).filter(ImageFilter.GaussianBlur(6))
    fine = Image.effect_noise((w, h), 18)
    n = ImageChops.add(noise, fine, scale=2.0)
    n = n.point(lambda v: int(255 - (255 - v) * 0.10 * strength))
    rgb = img.convert('RGB')
    rgb = ImageChops.multiply(rgb, Image.merge('RGB', (n, n, n)))
    # a few scuffs/creases
    d = ImageDraw.Draw(rgb)
    for _ in range(0):  # no scuff lines (one read as a cigarette on the llama)
        x, y = rnd.randrange(w), rnd.randrange(h)
        l = rnd.randint(20, 120)
        a = rnd.uniform(0, math.pi)
        c = rnd.randint(215, 240)
        d.line([(x, y), (x + l * math.cos(a), y + l * math.sin(a))], fill=(c, c - 6, c - 14), width=rnd.randint(1, 3))
    rgb = rgb.filter(ImageFilter.GaussianBlur(0.6))
    # slight desaturation toward game palette
    gray = rgb.convert('L').convert('RGB')
    rgb = Image.blend(rgb, gray, 0.12)
    return rgb


def lid_lines(atlas):
    d = ImageDraw.Draw(atlas)
    c = (0, 0, 0)
    # front lid seam
    y = mm(D + LID)
    d.line([(mm(D), y), (mm(D + W), y)], fill=(40, 30, 30), width=3)
    # sides: angled cut from lid seam at front to higher at back
    for x0 in (0, D + W):
        # left side: front edge at x=D (back at 0); right side: front edge at x=D+W (back at 2D+W)
        d.line([(mm(D), mm(D + LID)), (0, mm(D + LID - 6))] if x0 == 0 else [(mm(x0), mm(D + LID)), (mm(x0 + D), mm(D + LID - 6))], fill=(40, 30, 30), width=3)
    # back hinge
    d.line([(mm(2 * D + W), mm(D + LID - 6)), (mm(2 * D + 2 * W), mm(D + LID - 6))], fill=(60, 50, 50), width=2)


def build_atlas(b, seed=0):
    atlas = Image.new('RGBA', ATLAS, b.base)
    atlas.alpha_composite(b.top(), (mm(D), 0))
    atlas.alpha_composite(b.side(False), (0, mm(D)))
    atlas.alpha_composite(b.front(), (mm(D), mm(D)))
    atlas.alpha_composite(b.side(True), (mm(D + W), mm(D)))
    atlas.alpha_composite(b.back(), (mm(2 * D + W), mm(D)))
    atlas.alpha_composite(b.bottom(), (mm(D), mm(D + H)))
    rgb = grunge(atlas, seed * 97 + len(b.key))
    lid_lines(rgb)
    return rgb


if __name__ == '__main__':
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'tex')
    os.makedirs(out, exist_ok=True)
    for i, b in enumerate(BRANDS):
        build_atlas(b, i).save(os.path.join(out, 'pack_%s.png' % b.key))
        print('ok', b.key)

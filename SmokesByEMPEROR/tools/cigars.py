"""Cigar brands (SmokesByEMPEROR): data + textures (wrapper leaf, band, tube label, cedar box atlas).
python3 cigars.py <texdir> [keys...]   -> cigar_wrapper_<k>.png, band_<k>.png, tube_<k>.png, box_<k>.jpg
Box mesh: render_packs.pack_mesh(dims=box_dims(c), s=box_scale(c)) lying flat (z = height, top = lid).
"""
import os, sys, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from packs import F, SCRIPT, SERIF9, SERIF7, CIN7, CIN9, OSW7, OSW4, BEBAS, SLAB, ABRIL, text_c, fit_font, grunge

GOLD, CREAM, BLACK, RED, WHITE = (214, 172, 70), (244, 234, 208), (26, 22, 20), (166, 28, 34), (246, 244, 238)
SILVER, GREEN, BLUE, OLIVE = (196, 198, 204), (34, 92, 54), (30, 54, 112), (110, 120, 44)

# shapes: (length m, radius m, head) head: round | torpedo; press: box-pressed section
SHAPES = {
    'robusto': (.124, .0104, 'round'), 'churchill': (.178, .0094, 'round'), 'corona': (.142, .0085, 'round'),
    'short': (.110, .0102, 'round'), 'torpedo': (.156, .0101, 'torpedo'), 'lancero': (.192, .0074, 'round'),
    'boxpress': (.140, .0100, 'round'),
}


def C(key, name, shape, leaf, band, country, strength, count, sub='', press=False, cuban=False, seed=0):
    """strength 0 mild, 1 medium, 2 full; leaf = (dark, light) rgb; band = dict(bg, ink, trim, panel, motif)."""
    return dict(key=key, name=name, shape=shape, leaf=leaf, band=band, country=country, strength=strength, count=count,
                sub=sub, press=press, cuban=cuban, seed=seed or (abs(hash(key)) % 997 + 11))


CIGARS = [
    C('Partagaz', 'Partagaz', 'robusto', ((48, 27, 12), (104, 62, 30)), dict(bg=RED, ink=GOLD, trim=GOLD, panel=RED, motif='crest'),
      'HABANA', 2, 20, 'SERIE D No. 4', cuban=True, seed=21),
    C('Romero', 'Romero y Julietta', 'churchill', ((64, 37, 17), (134, 84, 44)), dict(bg=RED, ink=GOLD, trim=GOLD, panel=CREAM, motif='balcony'),
      'HABANA', 1, 15, 'CHURCHILL', cuban=True, seed=22),
    C('Hoyo', 'Hoyo de Monterreal', 'corona', ((108, 72, 36), (178, 124, 72)), dict(bg=WHITE, ink=GOLD, trim=GOLD, panel=WHITE, motif='stars'),
      'HABANA', 0, 20, 'LE HOYO', cuban=True, seed=23),
    C('Uppmann', 'H. Uppmann', 'corona', ((72, 42, 20), (142, 92, 50)), dict(bg=WHITE, ink=GOLD, trim=GOLD, panel=CREAM, motif='crest'),
      'HABANA', 1, 20, 'CORONA MAJOR', cuban=True, seed=24),
    C('Pounch', 'Pounch', 'short', ((52, 30, 14), (110, 66, 32)), dict(bg=RED, ink=BLACK, trim=GOLD, panel=CREAM, motif='jester'),
      'HABANA', 1, 20, 'PUNCHITO', cuban=True, seed=25),
    C('Bolivarr', 'Bolivarr', 'torpedo', ((30, 17, 8), (78, 44, 20)), dict(bg=RED, ink=GOLD, trim=GOLD, panel=CREAM, motif='portrait'),
      'HABANA', 2, 15, 'BELICOSOS FINOS', cuban=True, seed=26),
    C('Trinidud', 'Trinidud', 'lancero', ((70, 40, 18), (138, 86, 44)), dict(bg=BLACK, ink=GOLD, trim=GOLD, panel=BLACK, motif='tower'),
      'HABANA', 1, 15, 'FUNDADORES', cuban=True, seed=27),
    C('Arturo', 'Arturo Fuenta', 'churchill', ((80, 48, 22), (152, 98, 54)), dict(bg=RED, ink=GOLD, trim=GREEN, panel=CREAM, motif='crest'),
      'REP. DOMINICANA', 1, 20, 'GRAN RESERVA', seed=28),
    C('Padrone', 'Padrone', 'boxpress', ((36, 20, 10), (86, 50, 24)), dict(bg=WHITE, ink=GOLD, trim=GOLD, panel=WHITE, motif='crown'),
      'NICARAGUA', 2, 20, '1964 ANNIVERSARY', press=True, seed=29),
    C('Macanuto', 'Macanuto', 'corona', ((128, 90, 50), (198, 150, 94)), dict(bg=GREEN, ink=GOLD, trim=GOLD, panel=CREAM, motif='leaf'),
      'REP. DOMINICANA', 0, 20, 'CAFE', seed=30),
    C('Olivva', 'Olivva', 'robusto', ((78, 38, 20), (150, 80, 46)), dict(bg=GREEN, ink=GOLD, trim=GOLD, panel=CREAM, motif='olive'),
      'NICARAGUA', 1, 20, 'SERIE V', seed=31),
    C('Rocky', 'Rocky Pastel', 'torpedo', ((46, 26, 12), (100, 58, 28)), dict(bg=BLUE, ink=SILVER, trim=SILVER, panel=BLUE, motif='star'),
      'HONDURAS', 2, 20, 'VINTAGE 1992', seed=32),
    C('Ashten', 'Ashten', 'churchill', ((112, 74, 38), (182, 130, 76)), dict(bg=BLACK, ink=SILVER, trim=SILVER, panel=BLACK, motif='shield'),
      'REP. DOMINICANA', 0, 15, 'CLASSIC', seed=33),
    C('Quai', "Quai d'Orse", 'corona', ((142, 106, 64), (210, 164, 106)), dict(bg=CREAM, ink=BLACK, trim=BLACK, panel=CREAM, motif='none'),
      'HABANA', 0, 20, 'CORONA', cuban=True, seed=34),
    C('Grandfather', 'My Grandfather', 'boxpress', ((44, 24, 12), (96, 56, 26)), dict(bg=RED, ink=GOLD, trim=GOLD, panel=RED, motif='double'),
      'NICARAGUA', 2, 20, 'LE BIJOU 1922', press=True, seed=35),
    # ---- batch 2 (11-09): 20 more brands
    C('Cuabba', 'Cuabba', 'torpedo', ((60, 34, 16), (126, 78, 38)), dict(bg=(30, 60, 50), ink=GOLD, trim=GOLD, panel=CREAM, motif='leaf'),
      'HABANA', 1, 20, 'EXCLUSIVOS', cuban=True, seed=41),
    C('VegasRobaino', 'Vegas Robaino', 'robusto', ((56, 32, 14), (118, 72, 34)), dict(bg=GOLD, ink=RED, trim=RED, panel=CREAM, motif='stars'),
      'HABANA', 2, 20, 'FAMOSOS', cuban=True, seed=42),
    C('JuanLopes', 'Juan Lopes', 'corona', ((96, 62, 30), (166, 112, 62)), dict(bg=BLUE, ink=GOLD, trim=GOLD, panel=WHITE, motif='crest'),
      'HABANA', 0, 20, 'SELECCION No. 2', cuban=True, seed=43),
    C('RamonAllonzo', 'Ramon Allonzo', 'corona', ((50, 28, 12), (110, 66, 30)), dict(bg=RED, ink=GOLD, trim=GOLD, panel=RED, motif='crown'),
      'HABANA', 2, 20, 'SPECIALLY SELECTED', cuban=True, seed=44),
    C('SanCristobaal', 'San Cristobaal', 'churchill', ((78, 46, 22), (148, 96, 52)), dict(bg=BLACK, ink=GOLD, trim=GOLD, panel=BLACK, motif='tower'),
      'HABANA', 1, 15, 'LA FUERZA', cuban=True, seed=45),
    C('PorLarranya', 'Por Larranya', 'corona', ((112, 74, 38), (184, 130, 76)), dict(bg=WHITE, ink=RED, trim=RED, panel=WHITE, motif='olive'),
      'HABANA', 0, 20, 'PANETELAS', cuban=True, seed=46),
    C('ReyDelMondo', 'El Rey del Mondo', 'lancero', ((122, 84, 44), (192, 140, 84)), dict(bg=CREAM, ink=BLUE, trim=GOLD, panel=CREAM, motif='crown'),
      'HABANA', 0, 15, 'CHOIX SUPREME', cuban=True, seed=47),
    C('SanchoPansa', 'Sancho Pansa', 'torpedo', ((88, 56, 26), (160, 108, 60)), dict(bg=(120, 40, 30), ink=CREAM, trim=GOLD, panel=CREAM, motif='jester'),
      'HABANA', 1, 15, 'BELICOSOS', cuban=True, seed=48),
    C('Diplomatikos', 'Diplomatikos', 'robusto', ((70, 40, 18), (138, 86, 44)), dict(bg=WHITE, ink=BLUE, trim=BLUE, panel=WHITE, motif='shield'),
      'HABANA', 1, 20, 'No. 2', cuban=True, seed=49),
    C('GloriaCubanna', 'La Gloria Cubanna', 'churchill', ((62, 36, 16), (130, 80, 40)), dict(bg=RED, ink=WHITE, trim=GOLD, panel=CREAM, motif='portrait'),
      'HABANA', 1, 15, 'MEDAILLE D\'OR', cuban=True, seed=50),
    C('Fonsecka', 'Fonsecka', 'corona', ((132, 96, 56), (204, 156, 100)), dict(bg=(240, 236, 226), ink=(150, 30, 40), trim=GOLD, panel=WHITE, motif='stars'),
      'REP. DOMINICANA', 0, 20, 'CUBAN LIMITADA', seed=51),
    C('MyUncle', 'My Uncle', 'boxpress', ((40, 22, 10), (92, 54, 24)), dict(bg=BLACK, ink=GOLD, trim=GOLD, panel=BLACK, motif='double'),
      'NICARAGUA', 2, 20, 'LE PETIT', press=True, seed=52),
    C('Perdomio', 'Perdomio', 'churchill', ((58, 34, 16), (124, 76, 36)), dict(bg=(30, 40, 90), ink=SILVER, trim=SILVER, panel=(30, 40, 90), motif='star'),
      'NICARAGUA', 1, 20, 'RESERVE 10TH', seed=53),
    C('LigaPrivatta', 'Liga Privatta', 'robusto', ((24, 14, 8), (64, 38, 18)), dict(bg=(20, 20, 20), ink=(220, 220, 220), trim=(220, 220, 220), panel=(20, 20, 20), motif='none'),
      'NICARAGUA', 2, 20, 'No. 9', seed=54),
    C('AlecBadly', 'Alec Badly', 'robusto', ((54, 30, 14), (116, 70, 32)), dict(bg=(24, 24, 26), ink=(200, 60, 40), trim=SILVER, panel=(24, 24, 26), motif='star'),
      'HONDURAS', 2, 20, 'PRENSADO', seed=55),
    C('FlorDominicanna', 'La Flor Dominicanna', 'torpedo', ((46, 26, 12), (100, 60, 28)), dict(bg=(200, 30, 40), ink=GOLD, trim=BLACK, panel=CREAM, motif='leaf'),
      'REP. DOMINICANA', 2, 15, 'DOUBLE LIGERO', seed=56),
    C('Kamacho', 'Kamacho', 'short', ((44, 24, 10), (98, 58, 26)), dict(bg=(30, 30, 30), ink=(220, 60, 50), trim=(220, 60, 50), panel=(30, 30, 30), motif='shield'),
      'HONDURAS', 2, 20, 'TRIPLE MADURO', seed=57),
    C('JoyaNicaragwa', 'Joya de Nicaragwa', 'corona', ((84, 50, 24), (154, 102, 56)), dict(bg=(40, 90, 130), ink=GOLD, trim=GOLD, panel=CREAM, motif='balcony'),
      'NICARAGUA', 1, 20, 'ANTANO 1970', seed=58),
    C('Tatuajje', 'Tatuajje', 'boxpress', ((48, 28, 12), (106, 64, 30)), dict(bg=(110, 20, 30), ink=CREAM, trim=CREAM, panel=(110, 20, 30), motif='portrait'),
      'NICARAGUA', 2, 20, 'SERIE P', press=True, seed=59),
    C('CuestaRay', 'Cuesta-Ray', 'lancero', ((140, 102, 60), (212, 164, 108)), dict(bg=CREAM, ink=GREEN, trim=GOLD, panel=CREAM, motif='tower'),
      'REP. DOMINICANA', 0, 20, 'CENTENNIAL', seed=60),
]
# the two old cigars only get boxes (their cigars, tubes and textures stay as they are)
OLD = [dict(key='Cohibra', name='Cohibra', count=20, cuban=True, country='HABANA', sub='ROBUSTO', L=.14, R=.0095,
            band=dict(bg=(214, 170, 52), ink=BLACK, trim=BLACK, panel=CREAM, motif='none')),
       dict(key='Montecrusto', name='Montecrusto', count=20, cuban=True, country='HABANA', sub='No. 2', L=.15, R=.0098,
            band=dict(bg=(140, 30, 34), ink=GOLD, trim=GOLD, panel=CREAM, motif='none'))]
STRENGTH_RU = ['мягкая', 'средняя', 'полная']
STRENGTH_EN = ['mild', 'medium', 'full']


def lc(c):
    return c['key'].lower()


def LR(c):
    if 'L' in c:
        return c['L'], c['R']
    L, R, _ = SHAPES[c['shape']]
    return L, R


def box_dims(c):
    """(w, h, d) mm for render_packs.pack_mesh lying flat: w = 10 cigars side by side, h = two layers, d = along the cigars (front to back)."""
    L, R = LR(c)
    n = c['count']
    per_row = 10 if n >= 20 else (8 if n >= 16 else n // 2 + n % 2)
    w = per_row * 2 * R * 1000 + 16
    h = 2 * 2 * R * 1000 + 14
    d = L * 1000 + 20
    return round(w), round(h), round(d)


def box_scale(c):
    w, h, d = box_dims(c)
    return min(2048 / (2 * d + 2 * w), 2048 / (2 * d + h)) * 0.995


# ---------------------------------------------------------------- textures

def motif(d, kind, cx, cy, r, col, bg):
    if kind in ('wreath', 'rose', 'sun', 'anchor'):
        import boxes2
        return boxes2.motif(d, kind, cx, cy, r, col, bg)
    if kind == 'crest':
        d.polygon([(cx - r, cy - r), (cx + r, cy - r), (cx + r, cy + r * .2), (cx, cy + r * 1.1), (cx - r, cy + r * .2)], outline=col, width=max(2, int(r / 8)))
        d.line([(cx, cy - r * .8), (cx, cy + r * .8)], fill=col, width=max(2, int(r / 10)))
        d.line([(cx - r * .8, cy - r * .2), (cx + r * .8, cy - r * .2)], fill=col, width=max(2, int(r / 10)))
    elif kind == 'balcony':
        d.arc([cx - r, cy - r, cx + r, cy + r], 180, 360, fill=col, width=max(2, int(r / 8)))
        for i in range(-3, 4):
            x = cx + i * r / 3.5
            d.line([(x, cy), (x, cy + r * .7)], fill=col, width=max(2, int(r / 12)))
        d.line([(cx - r, cy + r * .7), (cx + r, cy + r * .7)], fill=col, width=max(2, int(r / 8)))
    elif kind in ('stars', 'star'):
        n = 3 if kind == 'stars' else 1
        for i in range(n):
            x = cx + (i - (n - 1) / 2) * r * .9
            rr = r * (.45 if n > 1 else .9)
            pts = []
            for k in range(10):
                a = -math.pi / 2 + k * math.pi / 5
                q = rr if k % 2 == 0 else rr * .42
                pts.append((x + math.cos(a) * q, cy + math.sin(a) * q))
            d.polygon(pts, fill=col)
    elif kind == 'jester':
        d.ellipse([cx - r * .45, cy - r * .1, cx + r * .45, cy + r * .8], outline=col, width=max(2, int(r / 8)))
        for sx in (-1, 0, 1):
            d.line([(cx, cy), (cx + sx * r * .8, cy - r * .7)], fill=col, width=max(2, int(r / 8)))
            d.ellipse([cx + sx * r * .8 - r * .12, cy - r * .82, cx + sx * r * .8 + r * .12, cy - r * .58], fill=col)
    elif kind == 'portrait':
        d.ellipse([cx - r, cy - r * 1.1, cx + r, cy + r * 1.1], outline=col, width=max(2, int(r / 8)))
        d.ellipse([cx - r * .35, cy - r * .7, cx + r * .35, cy + r * .05], fill=col)
        d.pieslice([cx - r * .7, cy, cx + r * .7, cy + r * 1.3], 180, 360, fill=col)
    elif kind == 'tower':
        d.rectangle([cx - r * .35, cy - r * .5, cx + r * .35, cy + r], outline=col, width=max(2, int(r / 8)))
        d.polygon([(cx - r * .5, cy - r * .5), (cx + r * .5, cy - r * .5), (cx, cy - r * 1.1)], fill=col)
        d.rectangle([cx - r * .12, cy + r * .5, cx + r * .12, cy + r], fill=col)
    elif kind == 'crown':
        d.polygon([(cx - r, cy + r * .5), (cx - r, cy - r * .4), (cx - r * .5, cy + r * .05), (cx, cy - r * .7), (cx + r * .5, cy + r * .05),
                   (cx + r, cy - r * .4), (cx + r, cy + r * .5)], fill=col)
    elif kind == 'leaf':
        d.ellipse([cx - r * .5, cy - r, cx + r * .5, cy + r], fill=col)
        d.line([(cx, cy - r * .9), (cx, cy + r * 1.1)], fill=bg, width=max(2, int(r / 10)))
    elif kind == 'olive':
        d.line([(cx - r, cy + r * .6), (cx + r, cy - r * .6)], fill=col, width=max(2, int(r / 8)))
        for t in (.25, .5, .75):
            x, y = cx - r + 2 * r * t, cy + r * .6 - 1.2 * r * t
            d.ellipse([x - r * .2, y - r * .45, x + r * .2, y + r * .05], fill=col)
    elif kind == 'shield':
        d.polygon([(cx - r * .8, cy - r), (cx + r * .8, cy - r), (cx + r * .8, cy + r * .1), (cx, cy + r), (cx - r * .8, cy + r * .1)], fill=col)
        text_c(d, cx, cy - r * .1, 'A', F(SERIF9, r * 1.1), bg)
    elif kind == 'double':
        d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=col, width=max(2, int(r / 8)))
        text_c(d, cx, cy, 'MG', F(CIN9, r * .9), col)


def band(c, path):
    """1024 x 300: u around (front at the middle), v along. Big name in the front panel, country to the sides."""
    b = c['band']
    w, h = 1024, 300
    im = Image.new('RGB', (w, h), b['bg'])
    d = ImageDraw.Draw(im)
    tr = b['trim']
    d.rectangle([0, 10, w, 20], fill=tr)
    d.rectangle([0, h - 20, w, h - 10], fill=tr)
    if b['motif'] == 'double':
        d.rectangle([0, 30, w, 36], fill=tr); d.rectangle([0, h - 36, w, h - 30], fill=tr)
    px0, px1 = w * .27, w * .73
    d.rounded_rectangle([px0, 46, px1, h - 46], radius=18, fill=b['panel'], outline=tr, width=5)
    name = c['name'].upper()
    ink = b['ink'] if b['panel'] != b['ink'] else b['bg']
    if b['panel'] == b['bg'] and b['bg'] in (WHITE, CREAM):
        ink = b['ink']
    lines = name.split(' ', 1) if len(name) > 11 else [name]
    if len(lines) == 1:
        text_c(d, w / 2, h / 2 + 18, name, fit_font(d, name, SERIF9, (px1 - px0) * .9, 88), ink)
    else:
        f = fit_font(d, max(lines, key=len), SERIF9, (px1 - px0) * .9, 64)
        text_c(d, w / 2, h / 2, lines[0], f, ink)
        text_c(d, w / 2, h / 2 + 62, lines[1], f, ink)
    if b['motif'] not in ('none',):
        mc = b['ink'] if b['ink'] != b['bg'] else b['trim']
        motif(d, b['motif'], w / 2, 82 if len(lines) == 1 else 64, 26, mc, b['panel'])
    sc = b['ink'] if b['ink'] != b['bg'] else b['trim']
    text_c(d, w * .135, h / 2, c['country'][:9], fit_font(d, c['country'][:9], CIN7, w * .22, 44), sc)
    text_c(d, w * .865, h / 2, c['country'][:9], fit_font(d, c['country'][:9], CIN7, w * .22, 44), sc)
    im.save(path)


def tube(c, path):
    """1024 x 1792: u around, v along (as tube_label for the old two)."""
    b = c['band']
    w, h = 1024, 1792
    bg = b['bg'] if b['bg'] not in (WHITE, CREAM) else (186, 188, 192)
    if b['bg'] == BLACK:
        bg = (34, 32, 30)
    im = Image.new('RGBA', (w, h), bg + (255,))
    lay = Image.new('RGBA', (h, w), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    stripe = b['trim'] if b['trim'] != bg else b['ink']
    d.rectangle([0, w * .28, h, w * .72], fill=b['panel'] if b['panel'] != bg else BLACK)
    d.rectangle([0, w * .29, h, w * .305], fill=stripe)
    d.rectangle([0, w * .695, h, w * .71], fill=stripe)
    name = c['name'].upper()
    ink = b['ink'] if b['ink'] != (b['panel'] if b['panel'] != bg else BLACK) else stripe
    text_c(d, h * .55, w * .5, name, fit_font(d, name, SERIF9, h * .7, 170), ink)
    if b['motif'] != 'none':
        motif(d, b['motif'], h * .1, w * .5, 70, ink, b['panel'])
    text_c(d, h * .5, w * .17, (c['sub'] + '  ·  ' + c['country']).strip(' ·'), F(CIN7, 50), stripe)
    text_c(d, h * .5, w * .83, 'HECHO A MANO', F(CIN7, 44), stripe)
    lay = lay.rotate(90, expand=True)
    im.alpha_composite(lay)
    grunge(im, c['seed'], 0.6).save(path)


def cedar(w, h, seed):
    """Spanish cedar planks: fine straight grain along x."""
    rnd = random.Random(seed)
    y = np.arange(h)[:, None]
    x = np.arange(w)[None, :]
    warp = np.array(Image.effect_noise((max(2, w // 64), max(2, h // 16)), 60).resize((w, h), Image.BICUBIC), np.float32) / 255
    fine = np.array(Image.effect_noise((w, h), 30).filter(ImageFilter.GaussianBlur(.6)), np.float32) / 255
    ph = y / 9.0 + warp * 6 + np.sin(x / 140.0 + seed) * 1.5
    g = (0.5 + 0.5 * np.sin(ph * math.pi)) ** 4
    base = np.array([186, 118, 74], np.float32)
    dark = np.array([128, 70, 40], np.float32)
    t = (0.25 * g + 0.15 * fine)[..., None]
    img = base * (1 - t) + dark * t
    img *= (0.92 + 0.08 * warp)[..., None]
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), 'RGB')


# 11-09 (user): not every box cedar and alike: lacquered, paper-covered (litho), dark wood with a brass plate, printed card
BOX_STYLE = {}
for k in ('Padrone', 'Trinidud', 'Ashten', 'Grandfather', 'MyUncle', 'Tatuajje', 'LigaPrivatta'): BOX_STYLE[k] = 'lacquer'
for k in ('Romero', 'Hoyo', 'Pounch', 'Bolivarr', 'RamonAllonzo', 'GloriaCubanna', 'PorLarranya', 'ReyDelMondo', 'SanchoPansa'): BOX_STYLE[k] = 'dress'
for k in ('Olivva', 'Rocky', 'Perdomio', 'JoyaNicaragwa', 'AlecBadly'): BOX_STYLE[k] = 'dark'
# 20:15 user: card looked cheap → leather-covered boxes instead
for k in ('Quai', 'FlorDominicanna', 'VegasRobaino', 'Kamacho'): BOX_STYLE[k] = 'leather'
STYLE_RU = {'cedar': 'Кедровая коробка', 'lacquer': 'Лакированная коробка', 'dress': 'Коробка, оклеенная бумагой с литографией',
            'dark': 'Коробка тёмного дерева с латунной табличкой', 'card': 'Картонная коробка', 'leather': 'Коробка, обтянутая кожей'}
STYLE_EN = {'cedar': 'A cedar box', 'lacquer': 'A lacquered box', 'dress': 'A paper-covered litho box',
            'dark': 'A dark wood box with a brass plate', 'card': 'A card box', 'leather': 'A leather-covered box'}


def box_style(c):
    import boxes2
    if c['key'] in boxes2.SPEC: return boxes2.style(c)
    return BOX_STYLE.get(c['key'], 'cedar')


def _faces(c):
    w, h, dd = box_dims(c)
    s = box_scale(c)
    P = lambda v: v * s
    return dict(lid=(P(dd), P(0), P(dd + w), P(dd)), left=(P(0), P(dd), P(dd), P(dd + h)), front=(P(dd), P(dd), P(dd + w), P(dd + h)),
                right=(P(dd + w), P(dd), P(2 * dd + w), P(dd + h)), back=(P(2 * dd + w), P(dd), P(2 * dd + 2 * w), P(dd + h)),
                bottom=(P(dd), P(dd + h), P(dd + w), P(2 * dd + h))), P


def _darkwood(A, seed, base, dark):
    im = cedar(A, A, seed)
    a = np.array(im, np.float32) / np.array([186, 118, 74], np.float32)
    t = np.clip((1.0 - a.mean(2)) * 3.0, 0, 1)[..., None]
    img = np.array(base, np.float32) * (1 - t) + np.array(dark, np.float32) * t
    img *= (0.9 + 0.1 * a.mean(2, keepdims=True))
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), 'RGB')


def _flat(A, col, seed, amount=10):
    n = np.array(Image.effect_noise((A // 4, A // 4), 50).resize((A, A), Image.BICUBIC), np.float32) / 255 - .5
    f = np.array(Image.effect_noise((A, A), 20), np.float32) / 255 - .5
    img = np.array(col, np.float32)[None, None, :] + (n * amount + f * amount * .4)[..., None]
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), 'RGB')


def _gloss(im, rect, strength=26):
    """a soft diagonal sheen on a lacquered face"""
    x0, y0, x1, y1 = [int(v) for v in rect]
    if x1 - x0 < 4 or y1 - y0 < 4: return
    w, h = x1 - x0, y1 - y0
    yy, xx = np.mgrid[0:h, 0:w]
    t = (xx / w * .7 + yy / h * .3)
    g = np.exp(-((t - .35) / .12) ** 2) * strength
    reg = np.array(im.crop((x0, y0, x1, y1)), np.float32) + g[..., None]
    im.paste(Image.fromarray(np.clip(reg, 0, 255).astype(np.uint8), 'RGB'), (x0, y0))


def _name_block(d, rect, c, ink, sub_ink, frac=.7, motif_col=None, motif_bg=None):
    x0, y0, x1, y1 = rect
    lw, lh = x1 - x0, y1 - y0
    name = c['name'].upper()
    text_c(d, (x0 + x1) / 2, y0 + lh * .5, name, fit_font(d, name, SERIF9, lw * frac, lh * .22), ink)
    text_c(d, (x0 + x1) / 2, y0 + lh * .22, c['country'], fit_font(d, c['country'], CIN7, lw * .55, lh * .09), sub_ink)
    sub = (c.get('sub') or '') + '  ·  %d' % c['count']
    text_c(d, (x0 + x1) / 2, y0 + lh * .78, sub.strip(' ·'), fit_font(d, sub.strip(' ·'), CIN7, lw * .6, lh * .075), sub_ink)
    b = c['band']
    if b['motif'] != 'none' and motif_col is not None:
        motif(d, b['motif'], x0 + lw * .09, y0 + lh / 2, lh * .09, motif_col, motif_bg)
        motif(d, b['motif'], x1 - lw * .09, y0 + lh / 2, lh * .09, motif_col, motif_bg)


def box_lacquer(c, path):
    F_, P = _faces(c)
    A = 2048
    b = c['band']
    col = {'Padrone': (232, 228, 216), 'Grandfather': (140, 22, 26), 'Tatuajje': (96, 18, 26)}.get(c['key'], (22, 20, 20))
    if c['key'] == 'LigaPrivatta': col = (34, 34, 36)
    im = _flat(A, col, c['seed'], 6)
    for f in F_.values(): _gloss(im, f, 30 if col[0] < 120 else 14)
    d = ImageDraw.Draw(im)
    gold = b['ink'] if b['ink'] not in (WHITE, CREAM) or col[0] < 120 else GOLD
    if c['key'] == 'Padrone': gold = (176, 140, 52)
    lx0, ly0, lx1, ly1 = F_['lid']
    for i, (m, wd) in enumerate(((P(6), P(1.4)), (P(9), P(.6)))):
        d.rectangle([lx0 + m, ly0 + m, lx1 - m, ly1 - m], outline=gold, width=max(2, int(wd)))
    _name_block(d, (lx0 + P(12), ly0 + P(12), lx1 - P(12), ly1 - P(12)), c, gold, gold, .66, gold, col)
    # front: a gold line under the lid edge, small gold name, brass clasp
    fx0, fy0, fx1, fy1 = F_['front']
    d.line([(fx0, fy0 + P(10)), (fx1, fy0 + P(10))], fill=tuple(int(v * .5) for v in col), width=max(2, int(P(.6))))
    d.rectangle([fx0 + P(4), fy0 + P(3), fx1 - P(4), fy1 - P(3)], outline=gold, width=max(2, int(P(.5))))
    nm = c['name'].upper()
    text_c(d, (fx0 + fx1) / 2, (fy0 + fy1) / 2 + P(5), nm, fit_font(d, nm, CIN7, (fx1 - fx0) * .5, (fy1 - fy0) * .22), gold)
    cx = (fx0 + fx1) / 2
    d.rounded_rectangle([cx - P(5), fy0 + P(6), cx + P(5), fy0 + P(14)], radius=P(1.5), fill=(190, 150, 70), outline=(110, 80, 30), width=max(1, int(P(.4))))
    for k in ('back', 'left', 'right'):
        x0, y0, x1, y1 = F_[k]
        d.line([(x0, y0 + P(10)), (x1, y0 + P(10))], fill=tuple(int(v * .5) for v in col), width=max(2, int(P(.6))))
    for x0, y0, x1, y1 in (F_['left'], F_['right']):
        text_c(d, (x0 + x1) / 2, (y0 + y1) / 2 + P(5), c['country'], fit_font(d, c['country'], CIN7, (x1 - x0) * .6, (y1 - y0) * .26), gold)
    bx0, by0, bx1, by1 = F_['back']
    for t in (.25, .75):
        hx = bx0 + (bx1 - bx0) * t
        d.rectangle([hx - P(6), by0 + P(8), hx + P(6), by0 + P(12)], fill=(170, 136, 64))
    im = im.filter(ImageFilter.GaussianBlur(.5))
    grunge(im, 3, .25).convert('RGB').save(path, quality=90)


def box_dress(c, path):
    F_, P = _faces(c)
    A = 2048
    b = c['band']
    # the paper in the brand's colour; light brands get a deep litho colour so the box is not a plain white slab
    paper = {'Hoyo': (28, 58, 108), 'PorLarranya': (130, 26, 36), 'ReyDelMondo': (26, 50, 104)}.get(c['key'], b['bg'])
    if paper in (WHITE, CREAM): paper = (120, 30, 36)
    im = _flat(A, paper, c['seed'], 12)
    d = ImageDraw.Draw(im)
    gold = GOLD
    edge = b['trim'] if b['trim'] != paper else gold
    # gold paper edging round every face
    for f in F_.values():
        x0, y0, x1, y1 = f
        e = P(3)
        for r in ([x0, y0, x1, y0 + e], [x0, y1 - e, x1, y1], [x0, y0, x0 + e, y1], [x1 - e, y0, x1, y1]):
            d.rectangle(r, fill=edge)
    # lid: a full litho label with an oval medallion
    lx0, ly0, lx1, ly1 = F_['lid']
    m = P(5)
    lab = b['panel'] if b['panel'] in (CREAM, WHITE) else CREAM
    d.rectangle([lx0 + m, ly0 + m, lx1 - m, ly1 - m], fill=paper)
    # sunburst rays behind the medallion, as on old litho lids
    cx0, cy0 = (lx0 + lx1) / 2, (ly0 + ly1) / 2
    ray = tuple(min(255, int(v * 1.25 + 12)) for v in paper)
    R = max(lx1 - lx0, ly1 - ly0)
    lay = Image.new('RGBA', (int(lx1 - lx0), int(ly1 - ly0)), (0, 0, 0, 0))
    rd = ImageDraw.Draw(lay)
    ox, oy = cx0 - int(lx0), cy0 - int(ly0)
    for i in range(36):
        if i % 2: continue
        a0, a1 = i * math.pi / 18, (i + 1) * math.pi / 18
        rd.polygon([(ox, oy), (ox + R * math.cos(a0), oy + R * math.sin(a0)), (ox + R * math.cos(a1), oy + R * math.sin(a1))], fill=ray + (255,))
    im.paste(lay, (int(lx0), int(ly0)), lay)
    d.rectangle([lx0, ly0, lx1, ly0 + m], fill=edge); d.rectangle([lx0, ly1 - m, lx1, ly1], fill=edge)
    d.rectangle([lx0, ly0, lx0 + m, ly1], fill=edge); d.rectangle([lx1 - m, ly0, lx1, ly1], fill=edge)
    # rope border of dots
    step = P(3)
    x = lx0 + m + P(2)
    while x < lx1 - m - P(2):
        for y in (ly0 + m + P(2), ly1 - m - P(2)):
            d.ellipse([x - P(.8), y - P(.8), x + P(.8), y + P(.8)], fill=gold)
        x += step
    y = ly0 + m + P(2)
    while y < ly1 - m - P(2):
        for x in (lx0 + m + P(2), lx1 - m - P(2)):
            d.ellipse([x - P(.8), y - P(.8), x + P(.8), y + P(.8)], fill=gold)
        y += step
    cx, cy = (lx0 + lx1) / 2, (ly0 + ly1) / 2
    rw, rh = (lx1 - lx0) * .34, (ly1 - ly0) * .40
    d.ellipse([cx - rw - P(2), cy - rh - P(2), cx + rw + P(2), cy + rh + P(2)], fill=gold)
    d.ellipse([cx - rw, cy - rh, cx + rw, cy + rh], fill=lab)
    ink = b['ink'] if b['ink'] not in (WHITE, CREAM, GOLD) else paper
    name = c['name'].upper()
    lines = name.split(' ', 1) if len(name) > 12 else [name]
    if len(lines) == 1:
        text_c(d, cx, cy, name, fit_font(d, name, SERIF9, rw * 1.5, rh * .5), ink)
    else:
        f = fit_font(d, max(lines, key=len), SERIF9, rw * 1.4, rh * .36)
        text_c(d, cx, cy - rh * .2, lines[0], f, ink)
        text_c(d, cx, cy + rh * .22, lines[1], f, ink)
    text_c(d, cx, cy - rh * .7, c['country'], fit_font(d, c['country'], CIN7, rw * .9, rh * .14), ink)
    text_c(d, cx, cy + rh * .72, 'HABANA' if c.get('cuban') else 'HECHO A MANO', fit_font(d, 'HECHO A MANO', CIN7, rw * .8, rh * .12), ink)
    if b['motif'] != 'none':
        for sx in (-1, 1):
            motif(d, b['motif'], cx + sx * (lx1 - lx0) * .4, cy, (ly1 - ly0) * .12, gold, b['bg'])
    # front and sides: brand colour paper, a cream panel with the name
    for k in ('front', 'back', 'left', 'right'):
        x0, y0, x1, y1 = F_[k]
        if k in ('front', 'back'):
            px = (x1 - x0) * .25
            d.rectangle([x0 + px, y0 + P(6), x1 - px, y1 - P(5)], fill=lab, outline=gold, width=max(2, int(P(.8))))
            text_c(d, (x0 + x1) / 2, (y0 + y1) / 2 + P(1), name, fit_font(d, name, SERIF9, (x1 - x0) * .44, (y1 - y0) * .3), ink)
        else:
            text_c(d, (x0 + x1) / 2, (y0 + y1) / 2 + P(1), c['country'], fit_font(d, c['country'], CIN7, (x1 - x0) * .6, (y1 - y0) * .24), gold)
        d.line([(x0, y0 + P(10)), (x1, y0 + P(10))], fill=tuple(int(v * .55) for v in paper), width=max(2, int(P(.5))))
    if c.get('cuban'):
        sx = lx0 + (lx1 - lx0) * .12
        d.rectangle([sx, ly1 - P(30), sx + P(14), ly1], fill=(64, 120, 70))
    im = im.filter(ImageFilter.GaussianBlur(.5))
    grunge(im, c['seed'], .7).convert('RGB').save(path, quality=90)


def box_dark(c, path):
    F_, P = _faces(c)
    A = 2048
    b = c['band']
    wood = {'Olivva': ((120, 64, 38), (58, 26, 14)), 'Rocky': ((92, 54, 34), (40, 22, 14)), 'Perdomio': ((110, 50, 34), (52, 20, 14)),
            'JoyaNicaragwa': ((136, 86, 50), (70, 40, 22)), 'AlecBadly': ((64, 44, 34), (24, 16, 12))}.get(c['key'], ((100, 56, 34), (46, 22, 14)))
    im = _darkwood(A, c['seed'], *wood)
    for f in F_.values(): _gloss(im, f, 16)
    d = ImageDraw.Draw(im)
    brass, brass_d = (200, 160, 80), (120, 88, 36)
    lx0, ly0, lx1, ly1 = F_['lid']
    cx, cy = (lx0 + lx1) / 2, (ly0 + ly1) / 2
    pw, ph = (lx1 - lx0) * .3, (ly1 - ly0) * .2
    d.rounded_rectangle([cx - pw, cy - ph, cx + pw, cy + ph], radius=P(3), fill=brass, outline=brass_d, width=max(2, int(P(.8))))
    d.rounded_rectangle([cx - pw + P(2), cy - ph + P(2), cx + pw - P(2), cy + ph - P(2)], radius=P(2), outline=brass_d, width=max(1, int(P(.4))))
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = cx + sx * (pw - P(3.5)), cy + sy * (ph - P(3.5))
            d.ellipse([x - P(1), y - P(1), x + P(1), y + P(1)], fill=brass_d)
    eng = (60, 40, 18)
    name = c['name'].upper()
    text_c(d, cx, cy - ph * .05, name, fit_font(d, name, SERIF9, pw * 1.6, ph * .55), eng)
    sub = (c['country'] + '  ·  %d' % c['count'])
    text_c(d, cx, cy + ph * .6, sub, fit_font(d, sub, CIN7, pw * 1.3, ph * .2), eng)
    # front: lid gap, brass clasp; sides: brass hinges on the back
    fx0, fy0, fx1, fy1 = F_['front']
    for k in ('front', 'back', 'left', 'right'):
        x0, y0, x1, y1 = F_[k]
        d.line([(x0, y0 + P(10)), (x1, y0 + P(10))], fill=(20, 10, 6), width=max(2, int(P(.7))))
    fcx = (fx0 + fx1) / 2
    d.rounded_rectangle([fcx - P(6), fy0 + P(5), fcx + P(6), fy0 + P(16)], radius=P(1.5), fill=brass, outline=brass_d, width=max(1, int(P(.5))))
    d.ellipse([fcx - P(1.5), fy0 + P(11), fcx + P(1.5), fy0 + P(14)], fill=brass_d)
    bx0, by0, bx1, by1 = F_['back']
    for t in (.2, .8):
        hx = bx0 + (bx1 - bx0) * t
        d.rectangle([hx - P(8), by0 + P(7), hx + P(8), by0 + P(13)], fill=brass, outline=brass_d, width=max(1, int(P(.4))))
    im = im.filter(ImageFilter.GaussianBlur(.5))
    grunge(im, c['seed'], .4).convert('RGB').save(path, quality=90)


def box_card(c, path):
    F_, P = _faces(c)
    A = 2048
    b = c['band']
    base = {'Quai': (200, 170, 124)}.get(c['key'], b['bg'] if b['bg'] not in (WHITE, CREAM) else (226, 214, 186))
    im = _flat(A, base, c['seed'], 16)
    d = ImageDraw.Draw(im)
    ink = b['ink'] if b['ink'] != base else BLACK
    if c['key'] == 'Quai': ink = (60, 36, 20)
    stripe = b['trim'] if b['trim'] not in (base,) else ink
    name = c['name'].upper()
    lx0, ly0, lx1, ly1 = F_['lid']
    lh = ly1 - ly0
    # printed: a broad band across the lid with the name, thin rules, small print
    band_col = CREAM if base[0] + base[1] + base[2] < 450 else (b['bg'] if b['bg'] not in (WHITE, CREAM) else (60, 40, 30))
    name_col = ink if band_col == CREAM else CREAM
    if band_col == CREAM and ink in (CREAM, WHITE): name_col = BLACK
    d.rectangle([lx0, ly0 + lh * .3, lx1, ly0 + lh * .7], fill=band_col)
    d.rectangle([lx0, ly0 + lh * .27, lx1, ly0 + lh * .285], fill=stripe)
    d.rectangle([lx0, ly0 + lh * .715, lx1, ly0 + lh * .73], fill=stripe)
    text_c(d, (lx0 + lx1) / 2, ly0 + lh * .5, name, fit_font(d, name, BEBAS, (lx1 - lx0) * .75, lh * .3), name_col)
    small = '%d CIGARS  ·  %s' % (c['count'], c['country'])
    text_c(d, (lx0 + lx1) / 2, ly0 + lh * .14, small, fit_font(d, small, OSW4, (lx1 - lx0) * .6, lh * .08), ink)
    text_c(d, (lx0 + lx1) / 2, ly0 + lh * .86, 'HAND MADE', fit_font(d, 'HAND MADE', OSW4, (lx1 - lx0) * .3, lh * .07), ink)
    if b['motif'] != 'none':
        motif(d, b['motif'], lx0 + (lx1 - lx0) * .1, ly0 + lh * .5, lh * .1, name_col, band_col)
    for k in ('front', 'back', 'left', 'right'):
        x0, y0, x1, y1 = F_[k]
        h = y1 - y0
        d.rectangle([x0, y0 + h * .3, x1, y0 + h * .7], fill=band_col)
        txt = name if k in ('front', 'back') else c['country']
        text_c(d, (x0 + x1) / 2, y0 + h * .5, txt, fit_font(d, txt, BEBAS, (x1 - x0) * .7, h * .32), name_col)
        # the lid of a card box is a shallow cap: its edge shows as a line
        d.line([(x0, y0 + P(8)), (x1, y0 + P(8))], fill=tuple(int(v * .6) for v in base), width=max(2, int(P(.6))))
    im = im.filter(ImageFilter.GaussianBlur(.5))
    grunge(im, c['seed'], 1.0).convert('RGB').save(path, quality=90)


def _leather(A, col, seed):
    """pebbled leather: cell noise bumps, darker creases"""
    rnd = np.random.default_rng(seed)
    small = rnd.random((A // 6, A // 6)).astype(np.float32)
    cells = np.array(Image.fromarray((small * 255).astype(np.uint8)).resize((A, A), Image.NEAREST).filter(ImageFilter.GaussianBlur(2.2)), np.float32) / 255
    edge = np.abs(np.array(Image.fromarray((cells * 255).astype(np.uint8)).filter(ImageFilter.FIND_EDGES), np.float32) / 255)
    big = np.array(Image.effect_noise((A // 32, A // 32), 60).resize((A, A), Image.BICUBIC), np.float32) / 255
    k = 0.86 + 0.14 * cells - 0.5 * np.clip(edge * 3, 0, .5) + 0.08 * (big - .5)
    img = np.array(col, np.float32)[None, None, :] * k[..., None]
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), 'RGB')


def box_leather(c, path):
    F_, P = _faces(c)
    A = 2048
    b = c['band']
    col = {'Quai': (70, 40, 26), 'FlorDominicanna': (110, 20, 26), 'VegasRobaino': (28, 50, 40), 'Kamacho': (24, 22, 22)}.get(c['key'], (60, 34, 22))
    im = _leather(A, col, c['seed'])
    for f in F_.values(): _gloss(im, f, 10)
    d = ImageDraw.Draw(im)
    gold, gold_d = (206, 168, 84), (120, 92, 40)
    stitch = tuple(min(255, int(v * .5 + 120)) for v in col)
    for k, f in F_.items():
        x0, y0, x1, y1 = f
        # stitched seam round each face
        m = P(3)
        step, ln = P(2.4), P(1.4)
        x = x0 + m
        while x < x1 - m - ln:
            for y in (y0 + m, y1 - m):
                d.line([(x, y), (x + ln, y)], fill=stitch, width=max(2, int(P(.35))))
            x += step
        y = y0 + m
        while y < y1 - m - ln:
            for x in (x0 + m, x1 - m):
                d.line([(x, y), (x, y + ln)], fill=stitch, width=max(2, int(P(.35))))
            y += step
        if k != 'lid' and k != 'bottom':
            d.line([(x0, y0 + P(10)), (x1, y0 + P(10))], fill=tuple(int(v * .4) for v in col), width=max(2, int(P(.7))))
    # metal corners on the lid
    lx0, ly0, lx1, ly1 = F_['lid']
    cs = P(9)
    for (ax, ay, sx, sy) in ((lx0, ly0, 1, 1), (lx1, ly0, -1, 1), (lx0, ly1, 1, -1), (lx1, ly1, -1, -1)):
        d.polygon([(ax, ay), (ax + sx * cs, ay), (ax, ay + sy * cs)], fill=gold, outline=gold_d)
    # gold-blocked name with a double rule, motif above
    cx, cy = (lx0 + lx1) / 2, (ly0 + ly1) / 2
    lw, lh = lx1 - lx0, ly1 - ly0
    d.rectangle([lx0 + lw * .12, ly0 + lh * .2, lx1 - lw * .12, ly1 - lh * .2], outline=gold, width=max(2, int(P(.7))))
    d.rectangle([lx0 + lw * .12 + P(2), ly0 + lh * .2 + P(2), lx1 - lw * .12 - P(2), ly1 - lh * .2 - P(2)], outline=gold, width=max(1, int(P(.35))))
    name = c['name'].upper()
    text_c(d, cx, cy + lh * .03, name, fit_font(d, name, SERIF9, lw * .6, lh * .16), gold)
    text_c(d, cx, cy + lh * .2, c['country'], fit_font(d, c['country'], CIN7, lw * .4, lh * .06), gold)
    if b['motif'] != 'none':
        motif(d, b['motif'], cx, cy - lh * .16, lh * .07, gold, col)
    # front: gold clasp + small name; back: hinges
    fx0, fy0, fx1, fy1 = F_['front']
    fcx = (fx0 + fx1) / 2
    d.rounded_rectangle([fcx - P(6), fy0 + P(5), fcx + P(6), fy0 + P(16)], radius=P(1.5), fill=gold, outline=gold_d, width=max(1, int(P(.5))))
    for x0, y0, x1, y1 in (F_['left'], F_['right']):
        text_c(d, (x0 + x1) / 2, (y0 + y1) / 2 + P(5), c['country'], fit_font(d, c['country'], CIN7, (x1 - x0) * .6, (y1 - y0) * .24), gold)
    text_c(d, fcx, (fy0 + fy1) / 2 + P(8), name, fit_font(d, name, CIN7, (fx1 - fx0) * .4, (fy1 - fy0) * .16), gold)
    bx0, by0, bx1, by1 = F_['back']
    for t in (.2, .8):
        hx = bx0 + (bx1 - bx0) * t
        d.rectangle([hx - P(8), by0 + P(7), hx + P(8), by0 + P(13)], fill=gold, outline=gold_d, width=max(1, int(P(.4))))
    im = im.filter(ImageFilter.GaussianBlur(.4))
    grunge(im, c['seed'], .3).convert('RGB').save(path, quality=90)


def box(c, path):
    # 11-09 20:18 v3: every box its own layout inside its style (boxes2.SPEC)
    import boxes2
    if c['key'] in boxes2.SPEC: return boxes2.make(c, path)
    st = box_style(c)
    if st == 'lacquer': return box_lacquer(c, path)
    if st == 'dress': return box_dress(c, path)
    if st == 'dark': return box_dark(c, path)
    if st == 'card': return box_card(c, path)
    if st == 'leather': return box_leather(c, path)
    return box_cedar(c, path)


def box_cedar(c, path):
    """Atlas for render_packs.pack_mesh(dims=box_dims(c), s=box_scale(c)): row0 top (lid) | row1 left front right back | row2 bottom."""
    w, h, dd = box_dims(c)
    s = box_scale(c)
    A = 2048
    im = cedar(A, A, c.get('seed', 7))
    d = ImageDraw.Draw(im)
    P = lambda v: v * s
    b = c['band']
    # lid: x d..d+w, y 0..d (front edge at y = d)
    lx0, lx1, ly0, ly1 = P(dd), P(dd + w), P(0), P(dd)
    m = P(10)
    paper = b['panel'] if b['panel'] in (WHITE, CREAM) else CREAM
    d.rectangle([lx0 + m, ly0 + m, lx1 - m, ly1 - m], fill=paper)
    d.rectangle([lx0 + m + P(3), ly0 + m + P(3), lx1 - m - P(3), ly1 - m - P(3)], outline=b['trim'] if b['trim'] != paper else GOLD, width=max(3, int(P(1.6))))
    d.rectangle([lx0 + m + P(6), ly0 + m + P(6), lx1 - m - P(6), ly1 - m - P(6)], outline=b['bg'] if b['bg'] != paper else BLACK, width=max(2, int(P(.8))))
    # the label reads from the front (lid width along the cigars)
    lw, lh = int(lx1 - lx0 - 2 * m - P(14)), int(ly1 - ly0 - 2 * m - P(14))
    lay = Image.new('RGBA', (lw, lh), (0, 0, 0, 0))
    ld = ImageDraw.Draw(lay)
    ink = b['ink'] if b['ink'] != paper else BLACK
    if b['bg'] not in (WHITE, CREAM):
        ld.rectangle([0, lh * .32, lw, lh * .68], fill=b['bg'])
        ink_name = b['ink'] if b['ink'] != b['bg'] else GOLD
    else:
        ink_name = b['ink'] if b['ink'] not in (WHITE, CREAM) else BLACK
    name = c['name'].upper()
    text_c(ld, lw / 2, lh / 2, name, fit_font(ld, name, SERIF9, lw * .7, lh * .24), ink_name)
    text_c(ld, lw / 2, lh * .2, c['country'], fit_font(ld, c['country'], CIN7, lw * .6, lh * .1), ink)
    sub = (c.get('sub') or '') + '  ·  %d' % c['count']
    text_c(ld, lw / 2, lh * .8, sub, fit_font(ld, sub, CIN7, lw * .7, lh * .08), ink)
    if b['motif'] != 'none':
        mc = ink_name if b['bg'] not in (WHITE, CREAM) else ink
        motif(ld, b['motif'], lw * .075, lh / 2, lh * .09, mc, b['bg'] if b['bg'] not in (WHITE, CREAM) else paper)
        motif(ld, b['motif'], lw * .925, lh / 2, lh * .09, mc, b['bg'] if b['bg'] not in (WHITE, CREAM) else paper)
    im.paste(lay, (int(lx0 + m + P(7)), int(ly0 + m + P(7))), lay)
    # front: x d..d+w, y d..d+h : brand burned into the wood, lid gap line
    fx0, fx1, fy0, fy1 = P(dd), P(dd + w), P(dd), P(dd + h)
    burn = (70, 34, 16)
    d.line([(fx0, fy0 + P(10)), (fx1, fy0 + P(10))], fill=(96, 52, 28), width=max(2, int(P(.6))))
    text_c(d, (fx0 + fx1) / 2, (fy0 + fy1) / 2 + P(4), name, fit_font(d, name, CIN9, (fx1 - fx0) * .8, (fy1 - fy0) * .36), burn)
    # back hinge line, sides burned country
    bx0, bx1 = P(2 * dd + w), P(2 * dd + 2 * w)
    d.line([(bx0, fy0 + P(10)), (bx1, fy0 + P(10))], fill=(96, 52, 28), width=max(2, int(P(.6))))
    for sx0 in (P(0), P(dd + w)):
        d.line([(sx0, fy0 + P(10)), (sx0 + P(dd), fy0 + P(10))], fill=(96, 52, 28), width=max(2, int(P(.6))))
        text_c(d, sx0 + P(dd) / 2, (fy0 + fy1) / 2 + P(4), c['country'], fit_font(d, c['country'], CIN7, P(dd) * .6, (fy1 - fy0) * .3), burn)
    # cuban warranty seal: green strip over the front edge of the lid onto the front, left part
    if c.get('cuban'):
        sx = lx0 + (lx1 - lx0) * .12
        sw = P(14)
        d.rectangle([sx, ly1 - P(30), sx + sw, ly1], fill=(64, 120, 70))
        d.rectangle([sx, fy0, sx + sw, fy0 + P(20)], fill=(64, 120, 70))
        for yy in (ly1 - P(24), fy0 + P(8)):
            d.rectangle([sx + P(2), yy, sx + sw - P(2), yy + P(4)], fill=(222, 214, 180))
    # bottom: plain with a stamp
    text_c(d, P(dd + w / 2), P(dd + h + dd / 2), 'HECHO EN ' + c['country'], fit_font(d, 'HECHO EN ' + c['country'], CIN7, P(w) * .8, P(8)), burn)
    im = im.filter(ImageFilter.GaussianBlur(.5))
    grunge(im, 3, .5).convert('RGB').save(path, quality=90)


def wrapper_tex(c, path):
    import wood_leaf_tex as wl
    wl.wrapper(path, c['seed'], c['leaf'][0], c['leaf'][1])


if __name__ == '__main__':
    tex = sys.argv[1]
    only = set(sys.argv[2:])
    for c in CIGARS:
        if only and c['key'] not in only: continue
        k = lc(c)
        wrapper_tex(c, os.path.join(tex, 'cigar_wrapper_%s.png' % k))
        band(c, os.path.join(tex, 'band_%s.png' % k))
        tube(c, os.path.join(tex, 'tube_%s.png' % k))
        box(c, os.path.join(tex, 'box_%s.jpg' % k))
        print('cigar tex', k)
    for c in OLD:
        if only and c['key'] not in only: continue
        c.setdefault('seed', 5)
        box(c, os.path.join(tex, 'box_%s.jpg' % lc(c)))
        print('box', lc(c))

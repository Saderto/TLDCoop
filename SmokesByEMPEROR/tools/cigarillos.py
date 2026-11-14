"""Cigarillos (SmokesByEMPEROR, 11-13): catalogue, held/world meshes (numpy), wrapper textures, packaging meshes + atlases.
Texture layout of a cigarillo = the cigarettes' (cigvar.py): 512 x 1024, body rows 0..896 (top = lit end, mouth end at the bottom,
v linear along the length), end discs in the last 128 rows (mouth end x 0..128, lit end x 128..256); so Burn, PrintSide and FilterStain
work on them unchanged.
python3 cigarillos.py <bakedir> <texdir> <meshdir> [brand ...]"""
import sys, os, math, random, struct
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from packs import F, SCRIPT, SERIF9, SERIF7, CIN7, CIN9, OSW7, OSW4, BEBAS, SLAB, ABRIL, text_c, fit_font, grunge

_fit_latin = fit_font


def fit_font(d, text, fname, *a, **k):
    """The pack fonts are Latin subsets: Russian text (Погарр) takes the same face's Cyrillic subset."""
    if any('\u0400' <= ch <= '\u04ff' for ch in text): fname = fname.replace('-latin-', '-cyrillic-')
    return _fit_latin(d, text, fname, *a, **k)


TW, TH, BODY = 512, 1024, 896
V0 = 1.0 - BODY / TH

# name: length m, radius m, tip length m (0 = none), real seconds
FORMATS = {
    # checked 11-13 against shop pages / Wikipedia: Cafe Creme 78-80 x 8 mm, Black & Mild 5" x ring 30, Swisher cigarillo 4 3/8" x 28,
    # Backwoods 4 1/2" tapered with a frayed foot, Villiger Export 4" x ring 37 square, Toscano 13-16.5 mm thick and halved (Toscanello)
    'mini': (.080, .0040, 0, 105),
    'slim': (.082, .0034, 0, 95),
    'tip': (.127, .0058, .030, 135),
    'classic': (.111, .0055, 0, 150),
    'square': (.102, .0066, 0, 150),
    'stumpen': (.100, .0060, 0, 150),
    'rustic': (.114, .0062, 0, 165),
    'toscano': (.078, .0074, 0, 165),
}
# kind: width, height, thickness, mm. Sizes follow from the cigarillos inside (5 or 10 in a row, or 2 x 5); no shop page gave the pack sizes.
PACKS = {
    'tin': (90, 85, 13),
    'pack10': (46, 88, 19),
    'box5': (62, 119, 14),
    'box5tip': (66, 135, 15),
    'box5wide': (74, 121, 17),
    'box5short': (82, 86, 18),
    'pouch': (84, 152, 13),
    'bundle': (40, 114, 24),
}
BASE = {'box5tip': 'box5', 'box5wide': 'box5', 'box5short': 'box5'}
LEAF = {
    'claro': ((120, 78, 40), (176, 126, 72)),
    'colorado': ((84, 48, 24), (142, 90, 48)),
    'maduro': ((50, 28, 14), (98, 58, 30)),
    'oscuro': ((26, 16, 10), (62, 38, 22)),
    'green': ((96, 104, 52), (158, 162, 96)),
    'paper': ((92, 56, 30), (120, 76, 42)),
}
GOLD, CREAM, WHITE, BLACK = (206, 166, 82), (236, 226, 200), (242, 240, 234), (24, 22, 22)
WOOD = (196, 158, 104)


def V(key, en, ru, leaf='colorado', bonus='', side='', strength=None, accent=None, tip=None, filt=False, fmt=None, rare=False):
    return dict(key=key, en=en, ru=ru, leaf=leaf, bonus=bonus, side=side, strength=strength, accent=accent, tip=tip, filt=filt, fmt=fmt, rare=rare)


def B(key, name, fmt, pack, units, strength, bg, ink, emblem, font, vs, sub='CIGARILLOS', filt=False, tip=None, band=None):
    return dict(key=key, name=name, fmt=fmt, pack=pack, units=units, strength=strength, bg=bg, ink=ink, emblem=emblem, font=font, vs=vs, sub=sub,
                filt=filt, tip=tip, band=band)


BRANDS = [
    B('KafeKreme', 'Kafé Krème', 'mini', 'tin', 10, 0, (232, 218, 186), (92, 52, 28), 'cup', SCRIPT, [
        V('Original', 'Original', 'Ориджинал', 'colorado', 'calm', accent=(150, 60, 40)),
        V('Blue', 'Blue', 'Блю', 'claro', 'fresh', accent=(50, 84, 150)),
        V('Arome', 'Arôme', 'Аром', 'colorado', 'calm', accent=(176, 130, 60)),
        V('Noir', 'Noir', 'Нуар', 'maduro', 'coffee', accent=(40, 30, 28))]),
    B('DannemenMoods', 'Dannemen Moods', 'mini', 'tin', 10, 0, (198, 58, 40), CREAM, 'mono:M', SERIF9, [
        V('Original', 'Original', 'Ориджинал', 'colorado', 'calm', accent=(198, 58, 40)),
        V('Gold', 'Gold', 'Голд', 'claro', 'calm', accent=(200, 160, 70)),
        V('Silver', 'Silver', 'Сильвер', 'claro', 'steady', accent=(168, 172, 180))], filt=True),
    B('Klubmaster', 'Klubmaster', 'mini', 'tin', 10, 1, (26, 60, 44), GOLD, 'clubs', CIN9, [
        V('Sumatra', 'Sumatra', 'Суматра', 'colorado', 'hands', accent=(26, 60, 44)),
        V('Brasil', 'Brasil', 'Бразил', 'maduro', '', 'dry', accent=(70, 36, 22)),
        V('Vanilla', 'Vanilla', 'Ванилла', 'claro', 'sweet', accent=(214, 190, 120))]),
    B('CaptainBlockMinis', 'Captain Block Minis', 'mini', 'pack10', 10, 0, (20, 24, 34), GOLD, 'wheel', CIN7, [
        V('Classic', 'Classic', 'Классик', 'paper', 'calm', accent=(20, 24, 34)),
        V('Cherry', 'Cherry', 'Черри', 'paper', 'sweet', accent=(140, 26, 40)),
        V('Sweets', 'Sweets', 'Свитс', 'paper', 'sweet', accent=(190, 150, 70))], sub='LITTLE CIGARS', filt=True),
    B('AlKapone', 'Al Kapone', 'mini', 'pack10', 10, 1, (30, 26, 24), (216, 196, 150), 'hat', ABRIL, [
        V('Cognac', 'Cognac', 'Коньяк', 'colorado', 'warm', accent=(150, 84, 30)),
        V('Rum', 'Rum', 'Ром', 'maduro', 'warm', accent=(110, 40, 30)),
        V('Slims', 'Slims', 'Слимс', 'colorado', 'steady', accent=(90, 90, 96), fmt='slim'),
        V('Filter', 'Filter', 'Фильтр', 'colorado', '', accent=(60, 80, 60))], sub='SWEETS', filt=True),
    B('BlackWild', 'Black & Wild', 'tip', 'box5', 5, 1, (22, 22, 24), (226, 222, 210), 'mono:B&W', SLAB, [
        V('Original', 'Original', 'Ориджинал', 'maduro', '', accent=(226, 222, 210)),
        V('Wine', 'Wine', 'Вайн', 'maduro', 'warm', accent=(120, 26, 50)),
        V('Cream', 'Cream', 'Крим', 'colorado', 'coffee', accent=(214, 190, 140)),
        V('Jazz', 'Jazz', 'Джаз', 'maduro', '', 'dizzy', accent=(60, 110, 170)),
        V('WoodTip', 'Wood Tip', 'Вуд Тип', 'maduro', '', accent=(176, 130, 76), tip=WOOD)], sub='PIPE TOBACCO CIGARS', tip=WHITE),
    B('SwishaSweets', 'Swisha Sweets', 'tip', 'pouch', 5, 0, (170, 28, 34), (244, 232, 200), 'mono:S', SCRIPT, [
        V('Original', 'Original', 'Ориджинал', 'colorado', 'sweet', accent=(170, 28, 34)),
        V('Grape', 'Grape', 'Грейп', 'colorado', 'sweet', accent=(96, 50, 130)),
        V('Peach', 'Peach', 'Пич', 'claro', 'sweet', accent=(226, 140, 80)),
        V('Tip', 'Tip', 'Тип', 'colorado', 'sweet', accent=(40, 40, 44), tip=(60, 44, 34))], tip=WHITE),
    B('Handelsgolt', 'Handelsgolt', 'tip', 'box5', 5, 0, (236, 228, 204), (120, 26, 30), 'coin', SERIF9, [
        V('SweetClassic', 'Sweet Classic', 'Свит Классик', 'colorado', 'sweet', accent=(190, 150, 60), tip=(214, 176, 80)),
        V('Cherry', 'Cherry', 'Черри', 'colorado', 'sweet', accent=(170, 26, 40), tip=(170, 26, 40)),
        V('Vanilla', 'Vanilla', 'Ванилла', 'claro', 'sweet', accent=(226, 200, 110), tip=(232, 208, 120))], sub='SWEETS'),
    B('Backwuds', 'Backwuds', 'rustic', 'pouch', 5, 2, (70, 44, 26), (236, 214, 160), 'pines', SLAB, [
        V('Original', 'Original', 'Ориджинал', 'maduro', 'warm', accent=(150, 84, 36)),
        V('Honey', 'Honey', 'Хани', 'colorado', 'sweet', accent=(214, 160, 50)),
        V('SweetAromatic', 'Sweet Aromatic', 'Свит Ароматик', 'colorado', 'calm', accent=(60, 110, 70)),
        V('RussianCream', 'Russian Cream', 'Рашн Крим', 'maduro', '', 'dry', accent=(226, 220, 206))], sub='WILD & MILD SMOKES'),
    B('Toskanello', 'Toskanello', 'rustic', 'box5', 5, 2, (236, 226, 200), (60, 40, 28), 'mono:T', CIN9, [
        V('Classico', 'Classico', 'Классико', 'oscuro', '', 'dry', accent=(150, 30, 30)),
        V('Caffe', 'Caffè', 'Кафе', 'oscuro', 'coffee', accent=(90, 54, 30)),
        V('Grappa', 'Grappa', 'Граппа', 'oscuro', 'warm', accent=(60, 100, 60)),
        V('Anice', 'Anice', 'Аниче', 'oscuro', 'fresh', accent=(60, 110, 150))], sub='SIGARI ITALIANI'),
    B('Williger', 'Williger', 'classic', 'box5', 5, 1, (150, 26, 30), WHITE, 'cross', SERIF9, [
        V('Export', 'Export', 'Экспорт', 'colorado', 'hands', accent=(150, 26, 30), fmt='square'),
        V('Premium', 'Premium', 'Премиум', 'colorado', 'hands', accent=(190, 150, 70)),
        V('Kiel', 'Kiel', 'Киль', 'maduro', 'hands', accent=(40, 60, 90), fmt='tip', tip=(226, 208, 150))], sub='SEIT 1888'),
    B('CubanMini', 'Mini', 'classic', 'tin', 10, 2, (210, 170, 60), BLACK, 'cuban', CIN9, [
        V('Cohibra', 'Cohibra Mini', 'Cohibra Мини', 'colorado', '', 'dizzy', accent=(226, 150, 30), rare=True),
        V('Montecrusto', 'Montecrusto Mini', 'Montecrusto Мини', 'colorado', '', 'dizzy', accent=(110, 60, 30), rare=True),
        V('Partagaz', 'Partagaz Mini', 'Partagaz Мини', 'maduro', '', 'dizzy', accent=(170, 30, 30), rare=True),
        V('Romero', 'Romero Mini', 'Romero Мини', 'claro', '', 'dizzy', accent=(150, 30, 40), rare=True)], sub='HABANA', band=True),
    B('Koltz', 'Koltz', 'tip', 'box5', 5, 0, (236, 232, 220), (30, 60, 110), 'horseshoe', OSW7, [
        V('Mild', 'Mild', 'Майлд', 'colorado', 'calm', accent=(30, 60, 110)),
        V('Rum', 'Rum', 'Ром', 'colorado', 'warm', accent=(120, 60, 30), tip=(226, 200, 150)),
        V('Wine', 'Wine', 'Вайн', 'colorado', 'warm', accent=(120, 26, 50), tip=(214, 150, 160))], sub='TIPPED · CANADA', tip=WHITE),
    B('OldFort', 'Old Fort', 'tip', 'box5', 5, 1, (40, 34, 30), GOLD, 'tower', CIN9, [
        V('Rum', 'Rum', 'Ром', 'maduro', 'warm', accent=(150, 80, 30), tip=(226, 200, 150)),
        V('Wine', 'Wine', 'Вайн', 'maduro', 'warm', accent=(120, 26, 50), tip=(214, 150, 160)),
        V('Natural', 'Natural', 'Нэчурал', 'maduro', '', accent=(90, 100, 70))], sub='RUM DIPPED · CANADA', tip=WHITE),
    B('PrimeThyme', 'Prime Thyme', 'mini', 'pack10', 10, 0, (240, 236, 226), (40, 40, 44), 'clock', OSW7, [
        V('Vanilla', 'Vanilla', 'Ванилла', 'paper', 'sweet', accent=(226, 196, 100)),
        V('Cherry', 'Cherry', 'Черри', 'paper', 'sweet', accent=(176, 26, 44)),
        V('Peach', 'Peach', 'Пич', 'paper', 'sweet', accent=(232, 140, 80))], sub='LITTLE CIGARS', filt=True),
    B('WhiteHowl', 'White Howl', 'classic', 'box5', 5, 1, (244, 240, 230), (30, 34, 60), 'owl', SLAB, [
        V('NewYorker', 'New Yorker', 'Нью-Йоркер', 'colorado', '', accent=(30, 34, 60)),
        V('Sweets', 'Sweets', 'Свитс', 'colorado', 'sweet', accent=(176, 30, 40)),
        V('Grape', 'Grape', 'Грейп', 'colorado', 'sweet', accent=(96, 50, 130))]),
    B('DutchMastaz', 'Dutch Mastaz', 'classic', 'box5', 5, 1, (60, 36, 22), GOLD, 'portrait', CIN9, [
        V('Palma', 'Palma', 'Пальма', 'colorado', 'calm', accent=(150, 110, 50)),
        V('Honey', 'Honey', 'Хани', 'claro', 'sweet', accent=(214, 160, 50)),
        V('Corona', 'Corona', 'Корона', 'maduro', '', 'dry', accent=(110, 30, 30))]),
    B('Fillies', 'Fillies', 'classic', 'box5', 5, 1, (236, 220, 170), (130, 30, 30), 'star', SLAB, [
        V('Blunt', 'Blunt', 'Блант', 'colorado', '', accent=(130, 30, 30), fmt='stumpen'),
        V('Sweet', 'Sweet', 'Свит', 'colorado', 'sweet', accent=(200, 120, 40)),
        V('Cheroot', 'Cheroot', 'Черут', 'maduro', '', 'dry', accent=(60, 60, 40), fmt='stumpen')]),
    B('Tiparello', 'Tiparello', 'tip', 'box5', 5, 0, (226, 60, 40), WHITE, 'mono:T', BEBAS, [
        V('Regular', 'Regular', 'Регуляр', 'colorado', '', accent=(226, 60, 40)),
        V('Menthol', 'Menthol', 'Ментол', 'colorado', 'fresh', accent=(40, 140, 110)),
        V('Aromatic', 'Aromatic', 'Ароматик', 'colorado', 'calm', accent=(110, 70, 150))], tip=WHITE),
    B('HavATundra', 'Hav-A-Tundra', 'tip', 'box5', 5, 0, (232, 200, 110), (70, 50, 30), 'pinesun', SLAB, [
        V('Jewels', 'Jewels', 'Джуэлс', 'colorado', 'calm', accent=(70, 50, 30)),
        V('Sweet', 'Sweet', 'Свит', 'colorado', 'sweet', accent=(176, 60, 40))], tip=WOOD),
    B('Winchestr', 'Winchestr', 'mini', 'pack10', 10, 1, (120, 26, 28), GOLD, 'rifles', CIN9, [
        V('Classic', 'Classic', 'Классик', 'paper', 'hands', accent=(120, 26, 28)),
        V('Menthol', 'Menthol', 'Ментол', 'paper', 'fresh', accent=(30, 110, 80)),
        V('Sweet', 'Sweet', 'Свит', 'paper', 'sweet', accent=(200, 150, 60))], sub='LITTLE CIGARS', filt=True),
    B('Meharees', "Meharee's", 'mini', 'pack10', 10, 1, (226, 196, 130), (80, 44, 24), 'pyramid', SERIF9, [
        V('Java', 'Java', 'Ява', 'colorado', 'hands', accent=(150, 60, 30)),
        V('Ecuador', 'Ecuador', 'Эквадор', 'claro', 'calm', accent=(60, 120, 80)),
        V('Brasil', 'Brasil', 'Бразил', 'maduro', '', 'dry', accent=(70, 40, 24)),
        V('SweetOrient', 'Sweet Orient', 'Свит Ориент', 'colorado', 'sweet', accent=(130, 50, 110))]),
    B('Panthyr', 'Panthyr', 'mini', 'tin', 10, 0, (24, 24, 28), (226, 180, 60), 'cat', BEBAS, [
        V('Mignon', 'Mignon', 'Миньон', 'colorado', 'steady', accent=(226, 180, 60)),
        V('Blue', 'Blue', 'Блю', 'claro', 'fresh', accent=(60, 110, 190)),
        V('Dessert', 'Dessert', 'Десерт', 'colorado', 'sweet', accent=(214, 150, 110))]),
    B('LaPazzWilde', 'La Pazz', 'rustic', 'box5', 5, 2, (226, 200, 120), (60, 40, 24), 'sun', ABRIL, [
        V('WildeHavana', 'Wilde Havana', 'Вильде Гавана', 'colorado', '', 'dizzy', accent=(170, 60, 30)),
        V('WildeBrasil', 'Wilde Brasil', 'Вильде Бразил', 'maduro', '', 'dry', accent=(60, 90, 50)),
        V('WildeCigarillos', 'Wilde Cigarillos', 'Вильде Сигариллос', 'colorado', 'warm', accent=(40, 60, 110))], sub='WILDE'),
    B('VaskoDaGamma', 'Vasko da Gamma', 'classic', 'box5', 5, 1, (24, 44, 84), GOLD, 'ship', CIN9, [
        V('Sumatra', 'Sumatra', 'Суматра', 'colorado', 'steady', accent=(24, 44, 84)),
        V('Brasil', 'Brasil', 'Бразил', 'maduro', '', 'dry', accent=(80, 40, 24)),
        V('CapaDeOro', 'Capa de Oro', 'Капа де Оро', 'claro', 'calm', accent=(206, 166, 82))]),
    B('DovidoffMini', 'Dovidoff Mini', 'mini', 'tin', 10, 0, (244, 242, 236), (30, 30, 32), 'mono:D', SCRIPT, [
        V('Silver', 'Silver', 'Сильвер', 'claro', 'calm', accent=(168, 172, 180)),
        V('Gold', 'Gold', 'Голд', 'colorado', 'calm', accent=(200, 160, 70)),
        V('Platinum', 'Platinum', 'Платинум', 'claro', 'steady', accent=(120, 130, 150))]),
    B('RosslyStumpen', 'Rössly', 'stumpen', 'box5', 5, 2, (40, 80, 50), CREAM, 'horseshoe', SLAB, [
        V('Nr7', 'Nr. 7', '№ 7', 'maduro', '', 'dry', accent=(40, 80, 50)),
        V('Kiel', 'Kiel', 'Киль', 'maduro', 'hands', accent=(150, 110, 50), fmt='tip', tip=(226, 208, 150))], sub='STUMPEN'),
    B('Pogarr', 'Погарр', 'mini', 'pack10', 10, 1, (30, 50, 90), GOLD, 'mono:П', SERIF9, [
        V('Classic', 'Classic', 'Классик', 'paper', 'hands', accent=(30, 50, 90)),
        V('Cherry', 'Cherry', 'Вишня', 'paper', 'sweet', accent=(150, 26, 44)),
        V('Vanilla', 'Vanilla', 'Ваниль', 'paper', 'sweet', accent=(214, 186, 100))], sub='СИГАРИЛЛЫ', filt=True),
    B('Farrias', 'Farrias', 'classic', 'box5', 5, 2, (236, 226, 196), (110, 26, 26), 'crown', ABRIL, [
        V('Superiores', 'Superiores', 'Супериорес', 'maduro', '', 'dizzy', accent=(110, 26, 26)),
        V('Mini', 'Mini', 'Мини', 'colorado', 'warm', accent=(40, 70, 50), fmt='mini')], sub='PUROS'),
    B('Mandalai', 'Mandalai', 'rustic', 'bundle', 5, 0, (222, 206, 166), (120, 40, 30), 'pagoda', CIN9, [
        V('GreenLeaf', 'Green Leaf', 'Грин Лиф', 'green', 'calm', accent=(90, 120, 60), filt=True),
        V('SweetBark', 'Sweet Bark', 'Свит Барк', 'green', 'sweet', accent=(150, 90, 40), filt=True)], sub='CHEROOTS'),
]


for _b in BRANDS:
    if _b['pack'] == 'box5':
        if _b['key'] == 'Toskanello': _b['pack'], _b['fmt'] = 'box5short', 'toscano'
        elif _b['fmt'] == 'tip': _b['pack'] = 'box5tip'
        elif _b['fmt'] in ('square', 'stumpen', 'rustic') or _b['key'] == 'Williger': _b['pack'] = 'box5wide'
    if _b['key'] == 'CubanMini': _b['fmt'] = 'mini'     # Cuban minis are plain 82 x 8 mm, cut at both ends


def all_varieties():
    for b in BRANDS:
        for v in b['vs']:
            yield b, v


def fmt_of(b, v): return v['fmt'] or b['fmt']
def filt_of(b, v): return bool(v['filt'] or (b['filt'] and fmt_of(b, v) in ('mini', 'slim')))
def tip_of(b, v): return (v['tip'] or b['tip'] or WHITE) if fmt_of(b, v) == 'tip' else None
def strength_of(b, v): return b['strength'] if v['strength'] is None else v['strength']
def item_id(b, v): return 'Cgl' + b['key'] + '_' + v['key']
def tex_name(b, v): return 'cgl_%s_%s.jpg' % (b['key'].lower(), v['key'].lower())
def pack_tex(b, v): return 'cglpack_%s_%s.jpg' % (b['key'].lower(), v['key'].lower())
def mesh_name(b, v): return 'cgl_' + fmt_of(b, v)
def pack_mesh_name(b): return 'cglpack_' + b['pack']


# ---------------------------------------------------------------- meshes
def profile(fmt):
    """Rings (z, r) from the mouth end to the lit end."""
    L, R, T, _ = FORMATS[fmt]
    if fmt in ('mini', 'slim', 'square', 'stumpen'):
        return [(0, R), (L * .5, R), (L, R)]
    if fmt == 'tip':
        rt = R * .62
        return [(0, rt * 1.08), (.004, rt * 1.08), (.006, rt * .92), (T - .004, rt * .96), (T - .0005, R * .9), (T, R * .9), (T + .004, R),
                (L * .6, R), (L, R * .97)]
    if fmt == 'classic':
        return [(0, R * .45), (.0015, R * .7), (.004, R * .9), (.008, R), (L * .5, R), (L, R * .98)]
    if fmt == 'rustic':      # narrow unfinished head, belly past the middle, open foot
        return [(L * i / 14, R * (.48 + .52 * math.sin(math.pi * (.10 + .62 * i / 14)) ** .8)) for i in range(15)]
    if fmt == 'toscano':     # half of a Toscano: narrow end in the mouth, the cut belly is lit
        return [(L * i / 10, R * (.62 + .38 * math.sin(math.pi / 2 * i / 10) ** 1.2)) for i in range(11)]
    raise KeyError(fmt)


def cig_mesh(fmt, seg=32):
    L, R, T, _ = FORMATS[fmt]
    prof = profile(fmt)
    rnd = random.Random(7)
    V, N, U, Tr = [], [], [], []
    th = 2 * math.pi * np.arange(seg + 1) / seg + math.pi / 2
    sq = fmt == 'square'

    def shape(c, s):
        if not sq: return 1.0
        # rounded square: superellipse of power 6
        return 1.0 / (abs(c) ** 6 + abs(s) ** 6) ** (1 / 6) * .88
    bump = {}
    rows = []
    for k, (z, r) in enumerate(prof):
        row = []
        # slope for the normals
        z0, r0 = prof[max(0, k - 1)]; z1, r1 = prof[min(len(prof) - 1, k + 1)]
        sl = (r1 - r0) / max(1e-6, z1 - z0)
        for i in range(seg + 1):
            c, s = math.cos(th[i]), math.sin(th[i])
            rr = r * shape(c, s)
            if fmt in ('rustic', 'toscano'):
                j = i % seg
                rr *= 1 + bump.setdefault((k, j), rnd.uniform(-.06, .06))
                if fmt == 'rustic' and k == len(prof) - 1: z = L - bump.setdefault(('z', j), rnd.uniform(0, .004))
            n = np.array([c, s, -sl]); n /= np.linalg.norm(n)
            V.append((rr * c, rr * s, z)); N.append(tuple(n)); U.append((i / seg, V0 + (1 - V0) * z / L))
            row.append(len(V) - 1)
        rows.append(row)
    for k in range(len(rows) - 1):
        for i in range(seg):
            a, b, c, d = rows[k][i], rows[k][i + 1], rows[k + 1][i + 1], rows[k + 1][i]
            Tr += [(a, b, c), (a, c, d)]

    def disc(z, r, cu, sign, dent):
        ring = []
        for i in range(seg + 1):
            c, s = math.cos(th[i]), math.sin(th[i])
            rr = r * shape(c, s)
            V.append((rr * c, rr * s, z)); N.append((0, 0, sign))
            U.append((cu + .125 * .92 * (rr * c) / r * sign, .0625 + .0625 * .92 * (rr * s) / r))
            ring.append(len(V) - 1)
        V.append((0, 0, z + dent)); N.append((0, 0, sign)); U.append((cu, .0625))
        ctr = len(V) - 1
        for i in range(seg):
            a, b = ring[i], ring[i + 1]
            Tr.append((ctr, b, a) if sign < 0 else (ctr, a, b))
    disc(0.0, prof[0][1], .125, -1.0, .0002)
    disc(L - (.004 if fmt == 'rustic' else 0), prof[-1][1] * (.93 if fmt == 'rustic' else 1), .375, 1.0, -.0006)
    return np.array(V, np.float32), np.array(N, np.float32), np.array(U, np.float32), np.array(Tr, np.int32)


def pack_layout(kind):
    w, h, d = PACKS[kind]
    s = 1024.0 / max(2 * w + 2 * d, h + d)
    return w, h, d, s


def box_mesh(kind):
    """Box standing on z = 0, front faces -Y (Blender). Atlas 1024: front (0,0) w x h | back | left | right in a row; top and bottom under them."""
    w, h, d, s = pack_layout(kind)
    x0, x1, y0, y1, z1 = -w / 2000, w / 2000, -d / 2000, d / 2000, h / 1000
    def uv(x, y): return (x * s / 1024, 1 - y * s / 1024)
    faces = [
        ([(x0, y0, 0), (x1, y0, 0), (x1, y0, z1), (x0, y0, z1)], [(0, h), (w, h), (w, 0), (0, 0)], (0, -1, 0)),
        ([(x1, y1, 0), (x0, y1, 0), (x0, y1, z1), (x1, y1, z1)], [(w, h), (2 * w, h), (2 * w, 0), (w, 0)], (0, 1, 0)),
        ([(x0, y1, 0), (x0, y0, 0), (x0, y0, z1), (x0, y1, z1)], [(2 * w, h), (2 * w + d, h), (2 * w + d, 0), (2 * w, 0)], (-1, 0, 0)),
        ([(x1, y0, 0), (x1, y1, 0), (x1, y1, z1), (x1, y0, z1)], [(2 * w + d, h), (2 * w + 2 * d, h), (2 * w + 2 * d, 0), (2 * w + d, 0)], (1, 0, 0)),
        ([(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], [(0, h + d), (w, h + d), (w, h), (0, h)], (0, 0, 1)),
        ([(x0, y1, 0), (x1, y1, 0), (x1, y0, 0), (x0, y0, 0)], [(w, h + d), (2 * w, h + d), (2 * w, h), (w, h)], (0, 0, -1)),
    ]
    V, N, U, Tr = [], [], [], []
    for vs, uvs, n in faces:
        i0 = len(V)
        for p, q in zip(vs, uvs):
            V.append(p); N.append(n); U.append(uv(*q))
        Tr += [(i0, i0 + 1, i0 + 2), (i0, i0 + 2, i0 + 3)]
    return np.array(V, np.float32), np.array(N, np.float32), np.array(U, np.float32), np.array(Tr, np.int32)


def smooth_normals(V, Tr):
    V = np.array(V, np.float64); N = np.zeros_like(V)
    for a, b, c in Tr:
        n = np.cross(V[b] - V[a], V[c] - V[a])
        N[a] += n; N[b] += n; N[c] += n
    return N / np.maximum(1e-12, np.linalg.norm(N, axis=1))[:, None]


def tin_mesh(kind):
    """Hinged tin: rounded corners, a lid a little wider than the body (the rim), bevelled edges. Front (the lid) faces -Y."""
    w, h, d, s = pack_layout(kind)
    W, H, D, rc = w / 1000, h / 1000, d / 1000, .009
    def uv(x, y): return (x * s / 1024, 1 - y * s / 1024)

    def ring(off, y):
        pts = []
        r = rc + off
        for cx, cz, a0 in ((W / 2 - rc, rc, -90), (W / 2 - rc, H - rc, 0), (-W / 2 + rc, H - rc, 90), (-W / 2 + rc, rc, 180)):
            for i in range(7):
                a = math.radians(a0 + 15 * i)
                pts.append((cx + r * math.cos(a), y, cz + r * math.sin(a)))
        return pts
    lid, bev = .0055, .0011
    # (offset, y, hard edge before?) from the back to the front
    prof = [(-bev, D / 2), (0, D / 2 - bev), (0, -D / 2 + lid), (.0006, -D / 2 + lid), (.0006, -D / 2 + bev), (-bev + .0006, -D / 2)]
    V, U, Tr, groups = [], [], [], []
    n = 28
    def strip(p0, p1):
        r0, r1 = ring(*p0), ring(*p1)
        i0 = len(V)
        for k, r in enumerate((r0, r1)):
            for i in range(n + 1):
                V.append(r[i % n])
                y = (p0, p1)[k][1]
                U.append(uv(2 * w + (y / D + .5) * d, h * (.1 + .8 * i / n)))
        for i in range(n):
            a, b, c, e = i0 + i, i0 + i + 1, i0 + n + 1 + i + 1, i0 + n + 1 + i
            Tr.extend([(a, b, c), (a, c, e)])
    for k in range(len(prof) - 1):
        strip(prof[k], prof[k + 1])

    def cap(p, front):
        r = ring(*p); i0 = len(V)
        for x, y, z in r:
            V.append((x, y, z)); U.append(uv(x * 1000 + w / 2, h - z * 1000) if front else uv(w + (w / 2 - x * 1000), h - z * 1000))
        V.append((0, p[1], H / 2)); U.append(uv(w / 2, h / 2) if front else uv(1.5 * w, h / 2))
        for i in range(n):
            a, b = i0 + i, i0 + (i + 1) % n
            Tr.append((i0 + n, a, b) if front else (i0 + n, b, a))
    cap(prof[-1], True); cap(prof[0], False)
    N = smooth_normals(V, Tr)
    return np.array(V, np.float32), N.astype(np.float32), np.array(U, np.float32), np.array(Tr, np.int32)


def pouch_mesh(kind):
    """Soft foil pouch: a pillow, flat welded seams at the top and the bottom, thin at the sides."""
    w, h, d, s = pack_layout(kind)
    W, H, D = w / 1000, h / 1000, d / 1000
    nx, nz = 14, 28
    V, U, Tr = [], [], []
    def sm(t): t = min(1.0, max(0.0, t)); return t * t * (3 - 2 * t)
    for side in (0, 1):
        i0 = len(V)
        for j in range(nz + 1):
            for i in range(nx + 1):
                u, v = i / nx, j / nz
                z = v * H
                t = D / 2 * math.sin(math.pi * u) ** .45 * sm((z - .010) / .028) * sm((H - .014 - z) / .030)
                t = max(t, .0004)
                t *= 1 + .05 * math.sin(u * 17 + v * 9 + side * 2) * sm(t / (D / 2))     # a few soft dents
                x = (u - .5) * W
                V.append((x, -t if side == 0 else t, z))
                U.append(((u * w if side == 0 else w + (1 - u) * w) * s / 1024, 1 - (h - z * 1000) * s / 1024))
        for j in range(nz):
            for i in range(nx):
                a = i0 + j * (nx + 1) + i; b = a + 1; c = a + nx + 2; e = a + nx + 1
                Tr.extend([(a, b, c), (a, c, e)] if side == 0 else [(a, c, b), (a, e, c)])
    N = smooth_normals(V, Tr)
    return np.array(V, np.float32), N.astype(np.float32), np.array(U, np.float32), np.array(Tr, np.int32)


def bundle_mesh(kind):
    """Five cheroots (3 behind, 2 in front) held by a paper band round the middle. Cheroots use the 'back' part of the atlas, the band's face the
    rows .30-.70 of the 'front' part, the rest of the band the plain 'left' part, the cut ends the 'bottom' part."""
    w, h, d, s = pack_layout(kind)
    H, r = h / 1000, .0062
    def uv(x, y): return (x * s / 1024, 1 - y * s / 1024)
    V, U, Tr = [], [], []
    seg = 12
    rnd = random.Random(3)
    for n, (cx, cy) in enumerate(((-2 * r, .0052), (0, .0052), (2 * r, .0052), (-r, -.0055), (r, -.0055))):
        # the same shape and wrapper as the single cheroot (profile 'rustic'), the mouth end down
        Lc = FORMATS['rustic'][0]
        zs = [(z / Lc * H, rr) for z, rr in profile('rustic')]
        zs[-1] = (H - rnd.uniform(0, .004), zs[-1][1])
        i0 = len(V)
        for k, (z, rr) in enumerate(zs):
            for i in range(seg + 1):
                a = 2 * math.pi * i / seg + n
                V.append((cx + rr * math.cos(a), cy + rr * math.sin(a), z)); U.append(uv(w + w * i / seg, h - z * 1000))
        for k in range(len(zs) - 1):
            for i in range(seg):
                a, b = i0 + k * (seg + 1) + i, i0 + k * (seg + 1) + i + 1
                Tr.extend([(a, b, b + seg + 1), (a, b + seg + 1, a + seg + 1)])
        for (z, rr), top in ((zs[0], False), (zs[-1], True)):
            j0 = len(V)
            u0 = w if top else 0
            for i in range(seg):
                a = 2 * math.pi * i / seg
                V.append((cx + rr * math.cos(a), cy + rr * math.sin(a), z)); U.append(uv(u0 + w * (.5 + .3 * math.cos(a)), h + d * (.5 + .3 * math.sin(a))))
            V.append((cx, cy, z)); U.append(uv(u0 + .5 * w, h + d / 2))
            for i in range(seg):
                a, b = j0 + i, j0 + (i + 1) % seg
                Tr.append((j0 + seg, a, b) if top else (j0 + seg, b, a))
    # the band
    # 11-13 (the owner's shot): the cheroots' bellies came through the band and its face was smeared round the corners
    hx, y0, y1, rc = 3 * r + .0012, -.0055 - r - .0012, .0052 + r + .0012, r + .0008
    ring = []
    for cx, cy, a0 in ((hx - rc, y0 + rc, -90), (hx - rc, y1 - rc, 0), (-hx + rc, y1 - rc, 90), (-hx + rc, y0 + rc, 180)):
        for i in range(7):
            a = math.radians(a0 + 15 * i)
            ring.append((cx + rc * math.cos(a), cy + rc * math.sin(a)))
    n = len(ring)
    z0, z1 = H * .30, H * .70
    # the face (y = y0, from -x to +x) is the stretch between the last corner's end and the first corner's start
    # the label is unrolled along the band: arc length from the middle of the face, so nothing is stretched at the corners;
    # past the label's width the band is plain paper
    per = [0.0]
    for i in range(1, n + 1):
        ax, ay = ring[i - 1]; bx, by = ring[i % n]
        per.append(per[-1] + math.hypot(bx - ax, by - ay))
    total = per[-1]
    # the middle of the face lies between the end of the last corner (index n-1 ... wraps) and the start of the first (index 0)
    mid = (per[n - 1] + total) / 2
    i0 = len(V)
    for z in (z0, z1):
        for i in range(n + 1):
            x, y = ring[i % n]
            sarc = (per[i] - mid) % total
            if sarc > total / 2: sarc -= total          # signed arc length from the face's middle, m (the ring runs towards -x there)
            um = w / 2 + sarc * 1000
            # 11-14 (the owner's shot: "emptiness at the joins"): past the label the band took another part of the atlas, and the one
            # strip between the two ran across the green wrapper. Now the label's own edge column goes on round (plain paper, its two rules)
            um = min(max(um, 1.0), w - 1.0)
            U.append(uv(um, h - z * 1000))
            V.append((x, y, z))
    for i in range(n):
        a, b = i0 + i, i0 + i + 1
        Tr.extend([(a, b, b + n + 1), (a, b + n + 1, a + n + 1)])
    # the band's inside (it was see-through from within): the same strip again, facing in
    j0 = len(V)
    V.extend(V[i0:j0])
    # plain paper inside (the label's own picture showed there, mirrored): the label's edge column
    for z in (z0, z1):
        for i in range(n + 1): U.append(uv(1.0, h - z * 1000))
    for i in range(n):
        a, b = j0 + i, j0 + i + 1
        Tr.extend([(a, b + n + 1, b), (a, a + n + 1, b + n + 1)])
    N = smooth_normals(V, Tr)
    return np.array(V, np.float32), N.astype(np.float32), np.array(U, np.float32), np.array(Tr, np.int32)


def pack_mesh(kind):
    return tin_mesh(kind) if kind == 'tin' else pouch_mesh(kind) if kind == 'pouch' else bundle_mesh(kind) if kind == 'bundle' else box_mesh(kind)


def rx(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]], np.float32)


def ry(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]], np.float32)


def rz(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]], np.float32)


def write_fp(path, v, n, uv, tris):
    # as npz_to_fpmesh.write: Blender (x, y, z) -> Unity (-x, z, -y)
    Uv = np.stack([-v[:, 0], v[:, 2], -v[:, 1]], 1)
    Nn = np.stack([-n[:, 0], n[:, 2], -n[:, 1]], 1)
    t = tris[:, [0, 2, 1]].reshape(-1)
    with open(path, 'wb') as f:
        f.write(struct.pack('<ii', len(Uv), len(t)))
        f.write(Uv.astype('<f4').tobytes()); f.write(Nn.astype('<f4').tobytes()); f.write(uv.astype('<f4').tobytes()); f.write(t.astype('<i4').tobytes())


def rest_fp(path, m, R):
    v, n, uv, t = m
    v, n = v @ R.T, n @ R.T
    lo, hi = v.min(0), v.max(0)
    v = v - np.array([(lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, lo[2]], np.float32)
    write_fp(path, v, n, uv, t)


def held_fp(path, m):
    v, n, uv, t = m
    R = rx(math.pi / 2)   # Blender +Z -> Unity +Z, the mouth end stays at the origin
    write_fp(path, v @ R.T, n @ R.T, uv, t)


# ---------------------------------------------------------------- cigarillo textures
_leaf_cache = {}


def leaf_img(tone, seed):
    key = (tone, seed % 4)
    if key not in _leaf_cache:
        import wood_leaf_tex as wl
        tmp = '/tmp/_cgl_leaf_%s_%d.png' % key
        c0, c1 = LEAF[tone]
        wl.wrapper(tmp, 11 + key[1] * 7, c0, c1, W=1024, H=1792, turns=1.6)
        im = Image.open(tmp).convert('RGB').resize((TW, BODY), Image.LANCZOS)
        if tone == 'paper':   # homogenised tobacco paper: smooth, no veins to speak of
            im = Image.blend(im.filter(ImageFilter.GaussianBlur(5)), im, .25)
        _leaf_cache[key] = im
    return _leaf_cache[key].copy()


def disc_img(kind, col, seed=1):
    w = 128
    rnd = random.Random(seed)
    if kind == 'tobacco':
        im = Image.new('RGB', (w, w), (66, 38, 18))
        d = ImageDraw.Draw(im)
        for _ in range(700):
            x, y = rnd.uniform(0, w), rnd.uniform(0, w)
            l, a = rnd.uniform(2, 7), rnd.uniform(0, 3.14)
            d.line([(x, y), (x + l * math.cos(a), y + l * math.sin(a))], fill=rnd.choice([(44, 24, 10), (104, 62, 26), (132, 84, 38), (84, 48, 20)]), width=rnd.randint(1, 2))
        return im
    im = Image.new('RGB', (w, w), col)
    d = ImageDraw.Draw(im)
    for _ in range(900):
        x, y = rnd.uniform(0, w), rnd.uniform(0, w)
        k = rnd.randint(-16, 10)
        d.point((x, y), fill=tuple(max(0, min(255, c + k)) for c in col))
    if kind == 'tip':     # a mouthpiece: a wall and the bore
        d.ellipse([50, 50, 78, 78], fill=(30, 22, 18))
        d.ellipse([3, 3, w - 3, w - 3], outline=tuple(max(0, c - 40) for c in col), width=5)
    else:
        d.ellipse([3, 3, w - 3, w - 3], outline=tuple(max(0, c - 22) for c in col), width=4)
    return im.filter(ImageFilter.GaussianBlur(.5))


def cig_texture(b, v, path):
    fmt = fmt_of(b, v)
    L, R, T, _ = FORMATS[fmt]
    seed = sum(map(ord, b['key'] + v['key']))
    im = Image.new('RGB', (TW, TH), (0, 0, 0))
    body = leaf_img(v['leaf'], seed)
    d = ImageDraw.Draw(body)
    acc = v['accent'] or b['ink']
    mouth = ('leaf', None)
    if fmt == 'tip':
        tip = tip_of(b, v)
        th = int(round(BODY * T / L))
        t = Image.new('RGB', (TW, th), tip)
        if tip == WOOD:
            td = ImageDraw.Draw(t)
            rnd = random.Random(seed)
            for _ in range(60):
                x = rnd.uniform(0, TW)
                td.line([(x, 0), (x + rnd.uniform(-6, 6), th)], fill=tuple(c - rnd.randint(8, 30) for c in tip), width=rnd.randint(1, 3))
        t = ImageChops.multiply(t, Image.effect_noise((TW, th), 10).convert('RGB').point(lambda q: 232 + q // 11))
        body.paste(t, (0, BODY - th))
        d.rectangle([0, BODY - th - 3, TW, BODY - th], fill=(30, 20, 14))
        mouth = ('tip', tip)
    elif filt_of(b, v):
        fh = int(round(BODY * .020 / L))
        fc = (150, 96, 52) if v['leaf'] != 'green' else (176, 152, 98)
        t = Image.new('RGB', (TW, fh), fc)
        t = ImageChops.multiply(t, Image.effect_noise((TW, fh), 14).convert('RGB').point(lambda q: 226 + q // 9))
        body.paste(t, (0, BODY - fh))
        d.rectangle([0, BODY - fh - 5, TW, BODY - fh - 1], fill=GOLD)
        mouth = ('filter', (236, 232, 218) if v['leaf'] != 'green' else (150, 128, 84))
    if b['band']:
        y = BODY - int(BODY * .30)
        d.rectangle([0, y - 34, TW, y + 34], fill=acc)
        d.rectangle([0, y - 34, TW, y - 28], fill=GOLD); d.rectangle([0, y + 28, TW, y + 34], fill=GOLD)
        d.ellipse([TW * .5 - 30, y - 30, TW * .5 + 30, y + 30], fill=GOLD)
        d.ellipse([TW * .5 - 22, y - 22, TW * .5 + 22, y + 22], fill=acc)
    if b['key'] == 'LaPazzWilde':     # the uncut, ragged foot: darker torn edge of the leaf at the lit end
        rnd = random.Random(seed)
        for x in range(0, TW, 6):
            hgt = rnd.randint(10, 46)
            d.polygon([(x, 0), (x + 6, 0), (x + 3 + rnd.randint(-4, 4), hgt)], fill=(40, 24, 12))
    im.paste(body, (0, 0))
    if mouth[0] == 'leaf':
        c0, c1 = LEAF[v['leaf']]
        im.paste(disc_img('tobacco', None, seed) if fmt in ('mini', 'slim', 'square', 'stumpen', 'rustic', 'toscano') else disc_img('plain', c0, seed), (0, BODY))
    else:
        im.paste(disc_img(mouth[0], mouth[1], seed), (0, BODY))
    im.paste(disc_img('tobacco', None, seed + 1), (128, BODY))
    im.save(path, quality=92)


# ---------------------------------------------------------------- packaging
def emblem(d, kind, cx, cy, r, col, bg, b=None, v=None):
    w = max(2, int(r / 9))
    if kind.startswith('mono:'):
        s = kind[5:]
        d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=col, width=w)
        d.ellipse([cx - r * .86, cy - r * .86, cx + r * .86, cy + r * .86], outline=col, width=max(1, w // 2))
        text_c(d, cx, cy, s, fit_font(d, s, SERIF9, r * 1.3, r * 1.1), col)
    elif kind == 'cup':
        d.pieslice([cx - r * .8, cy - r * .9, cx + r * .6, cy + r * .7], 0, 180, fill=col)
        d.arc([cx + r * .35, cy - r * .2, cx + r, cy + r * .4], -90, 90, fill=col, width=w)
        d.ellipse([cx - r, cy + r * .6, cx + r * .8, cy + r * .85], fill=col)
        for dx in (-.4, 0, .4):
            pts = [(cx - r * .1 + dx * r + math.sin(t * 3) * r * .08, cy - r * .2 - t * r * .8) for t in np.linspace(0, 1, 12)]
            d.line(pts, fill=col, width=max(1, w // 2))
    elif kind == 'clubs':
        for dx, dy in ((0, -.45), (-.45, .1), (.45, .1)):
            d.ellipse([cx + (dx - .4) * r, cy + (dy - .4) * r, cx + (dx + .4) * r, cy + (dy + .4) * r], fill=col)
        d.polygon([(cx, cy), (cx - r * .3, cy + r), (cx + r * .3, cy + r)], fill=col)
    elif kind == 'wheel':
        d.ellipse([cx - r * .75, cy - r * .75, cx + r * .75, cy + r * .75], outline=col, width=w + 1)
        d.ellipse([cx - r * .2, cy - r * .2, cx + r * .2, cy + r * .2], fill=col)
        for k in range(8):
            a = k * math.pi / 4
            d.line([(cx, cy), (cx + math.cos(a) * r, cy + math.sin(a) * r)], fill=col, width=w)
            d.ellipse([cx + math.cos(a) * r - w, cy + math.sin(a) * r - w, cx + math.cos(a) * r + w, cy + math.sin(a) * r + w], fill=col)
    elif kind == 'hat':
        d.ellipse([cx - r, cy + r * .1, cx + r, cy + r * .55], fill=col)
        d.rounded_rectangle([cx - r * .55, cy - r * .7, cx + r * .55, cy + r * .35], radius=int(r * .25), fill=col)
        d.rectangle([cx - r * .55, cy + r * .02, cx + r * .55, cy + r * .2], fill=bg)
    elif kind in ('pines', 'pinesun'):
        if kind == 'pinesun':
            d.ellipse([cx - r * .5, cy - r * .9, cx + r * .5, cy + r * .1], fill=col)
        for dx, sc in ((-.6, .7), (0, 1.0), (.6, .8)):
            x = cx + dx * r
            for k in range(3):
                yy = cy + r * .9 - (k + 1) * r * .5 * sc
                ww = r * (.42 - k * .1) * sc
                d.polygon([(x - ww, yy + r * .5 * sc), (x + ww, yy + r * .5 * sc), (x, yy - r * .1 * sc)], fill=col if kind == 'pines' else bg, outline=col)
            d.rectangle([x - r * .04, cy + r * .9, x + r * .04, cy + r], fill=col)
    elif kind == 'cross':
        d.rounded_rectangle([cx - r, cy - r, cx + r, cy + r], radius=int(r * .2), fill=col)
        d.rectangle([cx - r * .2, cy - r * .62, cx + r * .2, cy + r * .62], fill=bg)
        d.rectangle([cx - r * .62, cy - r * .2, cx + r * .62, cy + r * .2], fill=bg)
    elif kind == 'horseshoe':
        d.arc([cx - r * .75, cy - r * .9, cx + r * .75, cy + r * .6], 150, 390, fill=col, width=int(r * .28))
        for sx in (-1, 1):
            d.rectangle([cx + sx * r * .66 - r * .14, cy + r * .2, cx + sx * r * .66 + r * .14, cy + r * .9], fill=col)
    elif kind == 'tower':
        d.rectangle([cx - r * .45, cy - r * .5, cx + r * .45, cy + r], fill=col)
        for k in range(-2, 3):
            if k % 2 == 0:
                d.rectangle([cx + k * r * .22 - r * .11, cy - r * .85, cx + k * r * .22 + r * .11, cy - r * .5], fill=col)
        d.rectangle([cx - r * .14, cy + r * .4, cx + r * .14, cy + r], fill=bg)
        d.line([(cx - r, cy + r), (cx + r, cy + r)], fill=col, width=w)
    elif kind == 'clock':
        d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=col, width=w + 1)
        d.line([(cx, cy), (cx, cy - r * .65)], fill=col, width=w); d.line([(cx, cy), (cx + r * .45, cy + r * .2)], fill=col, width=w)
        for k in range(12):
            a = k * math.pi / 6
            d.line([(cx + math.cos(a) * r * .82, cy + math.sin(a) * r * .82), (cx + math.cos(a) * r * .95, cy + math.sin(a) * r * .95)], fill=col, width=max(1, w // 2))
    elif kind == 'owl':
        d.ellipse([cx - r * .75, cy - r * .7, cx + r * .75, cy + r], fill=col)
        d.polygon([(cx - r * .75, cy - r * .3), (cx - r * .6, cy - r), (cx - r * .2, cy - r * .55)], fill=col)
        d.polygon([(cx + r * .75, cy - r * .3), (cx + r * .6, cy - r), (cx + r * .2, cy - r * .55)], fill=col)
        for sx in (-1, 1):
            d.ellipse([cx + sx * r * .32 - r * .26, cy - r * .42, cx + sx * r * .32 + r * .26, cy + r * .1], fill=bg)
            d.ellipse([cx + sx * r * .32 - r * .1, cy - r * .26, cx + sx * r * .32 + r * .1, cy - r * .06], fill=col)
        d.polygon([(cx - r * .1, cy + r * .08), (cx + r * .1, cy + r * .08), (cx, cy + r * .32)], fill=bg)
    elif kind == 'portrait':
        d.ellipse([cx - r * .8, cy - r, cx + r * .8, cy + r], outline=col, width=w)
        d.ellipse([cx - r * .28, cy - r * .5, cx + r * .28, cy + r * .1], fill=col)
        d.ellipse([cx - r * .6, cy - r * .62, cx + r * .6, cy - r * .36], fill=col)       # a wide brimmed hat
        d.pieslice([cx - r * .6, cy + r * .05, cx + r * .6, cy + r * 1.3], 180, 360, fill=col)
    elif kind == 'star':
        pts = []
        for k in range(10):
            a = -math.pi / 2 + k * math.pi / 5
            q = r if k % 2 == 0 else r * .42
            pts.append((cx + math.cos(a) * q, cy + math.sin(a) * q))
        d.polygon(pts, fill=col)
    elif kind == 'rifles':
        for sx in (-1, 1):
            d.line([(cx - sx * r, cy + r * .7), (cx + sx * r, cy - r * .7)], fill=col, width=w + 2)
            d.polygon([(cx - sx * r, cy + r * .7), (cx - sx * r * .55, cy + r * .2), (cx - sx * r * .4, cy + r * .5)], fill=col)
        d.ellipse([cx - r * .22, cy - r * .22, cx + r * .22, cy + r * .22], fill=col)
    elif kind == 'pyramid':
        d.ellipse([cx + r * .2, cy - r * .9, cx + r * .8, cy - r * .3], fill=col)
        d.polygon([(cx - r, cy + r * .8), (cx - r * .2, cy - r * .4), (cx + r * .6, cy + r * .8)], fill=col)
        d.polygon([(cx + r * .1, cy + r * .8), (cx + r * .6, cy + r * .1), (cx + r, cy + r * .8)], outline=col, fill=bg)
        d.line([(cx - r, cy + r * .8), (cx + r, cy + r * .8)], fill=col, width=w)
    elif kind == 'cat':
        d.ellipse([cx - r * .8, cy - r * .55, cx + r * .8, cy + r * .85], fill=col)
        d.polygon([(cx - r * .8, cy - r * .1), (cx - r * .75, cy - r), (cx - r * .2, cy - r * .45)], fill=col)
        d.polygon([(cx + r * .8, cy - r * .1), (cx + r * .75, cy - r), (cx + r * .2, cy - r * .45)], fill=col)
        for sx in (-1, 1):
            d.polygon([(cx + sx * r * .18, cy + r * .0), (cx + sx * r * .58, cy - r * .2), (cx + sx * r * .5, cy + r * .12)], fill=bg)
        d.polygon([(cx - r * .1, cy + r * .3), (cx + r * .1, cy + r * .3), (cx, cy + r * .45)], fill=bg)
    elif kind == 'sun':
        d.ellipse([cx - r * .5, cy - r * .5, cx + r * .5, cy + r * .5], fill=col)
        for k in range(16):
            a = k * math.pi / 8
            d.line([(cx + math.cos(a) * r * .62, cy + math.sin(a) * r * .62), (cx + math.cos(a) * r, cy + math.sin(a) * r)], fill=col, width=w)
    elif kind == 'ship':
        d.polygon([(cx - r, cy + r * .4), (cx + r, cy + r * .4), (cx + r * .7, cy + r * .85), (cx - r * .7, cy + r * .85)], fill=col)
        for dx, hh in ((-.45, .8), (.1, 1.2), (.6, .7)):
            x = cx + dx * r
            d.line([(x, cy + r * .4), (x, cy + r * .4 - hh * r)], fill=col, width=w)
            d.polygon([(x + w, cy + r * .3), (x + w, cy + r * .4 - hh * r + r * .08), (x + r * .42, cy + r * .2)], fill=col)
    elif kind == 'crown':
        d.polygon([(cx - r, cy + r * .5), (cx - r, cy - r * .4), (cx - r * .5, cy + r * .05), (cx, cy - r * .7), (cx + r * .5, cy + r * .05),
                   (cx + r, cy - r * .4), (cx + r, cy + r * .5)], fill=col)
        d.rectangle([cx - r, cy + r * .6, cx + r, cy + r * .8], fill=col)
    elif kind == 'coin':
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=col)
        d.ellipse([cx - r * .8, cy - r * .8, cx + r * .8, cy + r * .8], outline=bg, width=w)
        text_c(d, cx, cy, 'H', F(SERIF9, r * 1.1), bg)
    elif kind == 'pagoda':
        for k in range(4):
            yy = cy + r * .8 - k * r * .42
            ww = r * (1 - k * .2)
            d.polygon([(cx - ww, yy), (cx + ww, yy), (cx + ww * .7, yy - r * .16), (cx - ww * .7, yy - r * .16)], fill=col)
            d.rectangle([cx - ww * .5, yy - r * .42, cx + ww * .5, yy - r * .16], fill=col)
        d.line([(cx, cy - r * .8), (cx, cy - r * 1.1)], fill=col, width=w)
    elif kind == 'cuban':
        import cigars as cg
        m = {'Cohibra': 'portrait', 'Montecrusto': 'crest', 'Partagaz': 'crown', 'Romero': 'balcony'}[v['key']]
        cg.motif(d, m, cx, cy, r * .8, col, bg)


def metal(w, h, col, seed):
    """A painted tin: flat colour with a soft diagonal sheen."""
    im = Image.new('RGB', (w, h), col)
    g = Image.linear_gradient('L').rotate(-35, expand=True).resize((w, h)).filter(ImageFilter.GaussianBlur(30))
    g = g.point(lambda q: 228 + int(27 * math.sin(q / 255 * math.pi * 2.2)))
    return ImageChops.multiply(im, Image.merge('RGB', (g, g, g)))


def lum(c): return .3 * c[0] + .59 * c[1] + .11 * c[2]


def pack_texture(b, v, path):
    w, h, d_, s = pack_layout(b['pack'])
    kind = BASE.get(b['pack'], b['pack'])
    W, H, D = int(w * s), int(h * s), int(d_ * s)
    seed = sum(map(ord, b['key'] + v['key']))
    bg, ink = b['bg'], b['ink']
    acc = v['accent'] or ink
    if abs(lum(acc) - lum(bg)) < 40:
        acc = ink
    at = Image.new('RGB', (1024, 1024), bg)

    def base(ww, hh):
        if kind == 'tin': return metal(ww, hh, bg, seed)
        if kind == 'pouch':
            im = metal(ww, hh, bg, seed)
            dd = ImageDraw.Draw(im)
            # welded seams: a crimped foil band at the top (with the tear notch line) and at the bottom
            foil = tuple(min(255, int(c * .55 + 120)) for c in bg)
            for y0, y1 in ((0, int(hh * .075)), (int(hh * .95), hh)):
                dd.rectangle([0, y0, ww, y1], fill=foil)
                for y in range(y0 + 3, y1 - 1, 6):
                    dd.line([(0, y), (ww, y)], fill=tuple(max(0, c - 46) for c in foil), width=2)
            dd.line([(0, int(hh * .075) + 2), (ww, int(hh * .075) + 2)], fill=tuple(max(0, c - 40) for c in bg), width=3)
            for x in (0, ww - 3):
                dd.rectangle([x, 0, x + 3, hh], fill=tuple(max(0, c - 20) for c in foil))
            return im
        if kind == 'bundle':
            im = Image.new('RGB', (ww, hh), bg)
            n = Image.effect_noise((ww, hh), 22).convert('RGB').point(lambda q: 222 + q // 8)
            return ImageChops.multiply(im, n)
        return Image.new('RGB', (ww, hh), bg)

    # front
    f = base(W, H)
    d = ImageDraw.Draw(f)
    m = int(W * .06)
    name = b['name'] if b['key'] != 'CubanMini' else v['en'].replace(' Mini', '')
    var = v['en'] if b['key'] != 'CubanMini' else 'MINI'
    if b['key'] == 'Pogarr': var = v['ru']
    if kind == 'tin':      # the lid: a double line following the rounded corners
        rr = int(9 * s) - m // 2
        d.rounded_rectangle([m, m, W - m, H - m], radius=rr, outline=ink, width=max(3, int(H * .010)))
        d.rounded_rectangle([m + 10, m + 10, W - m - 10, H - m - 10], radius=rr - 8, outline=acc, width=3)
    if kind == 'bundle':     # only the paper band is seen: rows .30-.70
        d.rectangle([0, H * .30, W, H * .31], fill=acc); d.rectangle([0, H * .69, W, H * .70], fill=acc)
        emblem(d, b['emblem'], W / 2, H * .385, W * .17, ink, bg, b, v)
        text_c(d, W / 2, H * .49, name, fit_font(d, name, b['font'], W * .74, H * .05), ink)
        d.rectangle([W * .14, H * .54, W * .86, H * .60], fill=acc)
        text_c(d, W / 2, H * .57, var.upper(), fit_font(d, var.upper(), OSW7, W * .62, H * .04), bg if abs(lum(bg) - lum(acc)) > 60 else ink)
        text_c(d, W / 2, H * .645, b['sub'], fit_font(d, b['sub'], OSW4, W * .6, H * .03), ink)
    else:
        if kind == 'pouch' and b['emblem'] in ('pines', 'pinesun'):
            # a forest line across the pouch
            rnd = random.Random(seed)
            for x in range(-10, W + 20, int(W * .09)):
                hh = rnd.uniform(.10, .2) * H
                d.polygon([(x - W * .06, H * .62), (x + W * .06, H * .62), (x, H * .62 - hh)], fill=tuple(max(0, c - 30) for c in bg))
            d.rectangle([0, H * .62, W, H], fill=tuple(max(0, c - 30) for c in bg))
        if kind in ('box5', 'pack10'):
            d.rectangle([0, 0, W, int(H * .05)], fill=acc); d.rectangle([0, H - int(H * .05), W, H], fill=acc)
            d.rectangle([m, int(H * .08), W - m, H - int(H * .08)], outline=ink, width=3)
        r = min(W * .26, H * .17)
        emblem(d, b['emblem'], W / 2, H * .27, r, ink, bg, b, v)
        text_c(d, W / 2, H * .50, name, fit_font(d, name, b['font'], W - 3 * m, H * .12), ink)
        d.rectangle([m * 2, H * .59, W - m * 2, H * .68], fill=acc)
        text_c(d, W / 2, H * .635, var.upper(), fit_font(d, var.upper(), OSW7, (W - 5 * m), H * .055), bg if abs(lum(bg) - lum(acc)) > 60 else ink)
        text_c(d, W / 2, H * .75, b['sub'], fit_font(d, b['sub'], OSW4, W - 4 * m, H * .04), ink)
        t = '%d' % b['units']
        text_c(d, W / 2, H * .86, t, F(SERIF9, H * .07), ink)
        if kind == 'bundle':
            d.line([(0, H * .93), (W, H * .93)], fill=(150, 60, 40), width=6)    # the string round the bundle
            d.line([(0, H * .07), (W, H * .07)], fill=(150, 60, 40), width=6)
    at.paste(f, (0, 0))
    # back
    k = base(W, H)
    d = ImageDraw.Draw(k)
    if kind == 'tin':
        d.rounded_rectangle([m, m, W - m, H - m], radius=int(H * .08), outline=ink, width=3)
        text_c(d, W / 2, H * .36, name, fit_font(d, name, b['font'], W * .6, H * .16), ink)
        text_c(d, W / 2, H * .62, b['sub'], fit_font(d, b['sub'], OSW4, W * .5, H * .07), ink)
    else:
        emblem(d, b['emblem'], W / 2, H * .30, W * .16, ink, bg, b, v)
        text_c(d, W / 2, H * .52, name, fit_font(d, name, b['font'], W - 4 * m, H * .07), ink)
        d.rectangle([m * 2, H * .72, W - m * 2, H * .90], fill=WHITE, outline=BLACK, width=3)
        wtxt = 'SMOKING KILLS' if b['key'] != 'Pogarr' else 'КУРЕНИЕ УБИВАЕТ'
        text_c(d, W / 2, H * .81, wtxt, fit_font(d, wtxt, OSW7, W - 6 * m, H * .05), BLACK)
    if kind == 'bundle':     # the cheroot's own wrapper, as on the single one
        ct = Image.open(os.path.join(os.path.dirname(path), tex_name(b, v))).convert('RGB')
        k = ct.crop((0, 0, TW, BODY)).resize((W, H), Image.LANCZOS)
    at.paste(k, (W, 0))
    # sides, top, bottom
    for i in range(2):
        sd = base(D, H)
        dd = ImageDraw.Draw(sd)
        lay = Image.new('RGBA', (int(H * .7), D), (0, 0, 0, 0))
        ld = ImageDraw.Draw(lay)
        if kind not in ('tin', 'pouch', 'bundle'): text_c(ld, lay.width / 2, D / 2, name.upper(), fit_font(ld, name.upper(), OSW7, lay.width * .9, D * .5), ink + (255,))
        lay = lay.rotate(90 if i == 0 else -90, expand=True)
        sd.paste(lay, (0, int(H * .15)), lay)
        at.paste(sd, (2 * W + i * D, 0))
    for i in range(2):
        tp = base(W, D)
        dd = ImageDraw.Draw(tp)
        if i == 0:
            text_c(dd, W / 2, D / 2, var.upper(), fit_font(dd, var.upper(), OSW7, W * .7, D * .5), ink)
        at.paste(tp, (i * W, H))
    if kind == 'bundle':
        at.paste(ct.crop((128, BODY, 256, TH)).resize((W, D)), (W, H)); at.paste(ct.crop((0, BODY, 128, TH)).resize((W, D)), (0, H))
    at = grunge(at, seed, .8)
    at.convert('RGB').save(path, quality=90)


# ---------------------------------------------------------------- into the mod
STATS = [(6, 30, 1, 1.5), (8, 35, 1.5, 1.8), (10, 40, 2, 2.2)]   # fatigue %, hunger min, health %, counts as cigarettes
FMT_RU = {'mini': 'мини', 'slim': 'тонкая мини', 'tip': 'с мундштуком', 'classic': 'классическая', 'square': 'квадратная',
          'stumpen': 'штумпен, обрезана с двух концов', 'rustic': 'грубая скрутка', 'toscano': 'тосканская половинка, толстая и неровная'}
FMT_EN = {'mini': 'mini', 'slim': 'slim mini', 'tip': 'tipped', 'classic': 'classic', 'square': 'square pressed', 'stumpen': 'stumpen, cut at both ends',
          'rustic': 'rough rolled', 'toscano': 'a Tuscan half, thick and lumpy'}
STR_RU, STR_EN = ['лёгкая', 'средняя', 'крепкая'], ['mild', 'medium', 'strong']
PACK_RU = {'tin': 'в жестянке', 'pack10': 'в пачке', 'box5': 'в коробочке', 'pouch': 'в пакете', 'bundle': 'в связке'}
PACK_EN = {'tin': 'in a tin', 'pack10': 'in a pack', 'box5': 'in a box', 'pouch': 'in a pouch', 'bundle': 'in a bundle'}


# the bonus / downside lines, as the cigarettes' (gen_catalog.py)
BONUS = {
    '': ('', ''),
    'fresh': ('«Свежий вдох»: выносливость восстанавливается на 10% быстрее 30 мин.', '"Fresh breath": stamina comes back 10% faster for 30 min.'),
    'coffee': ('Кофейный вкус: ещё −2% усталости.', 'Coffee flavour: another −2% fatigue.'),
    'sweet': ('Сладкий вкус: голод притуплён на 10 мин дольше.', 'Sweet flavour: hunger dulled 10 min longer.'),
    'warm': ('«Тёплый дым»: ощущаемая температура +1°C на 30 мин.', '"Warm smoke": feels-like temperature +1°C for 30 min.'),
    'calm': ('«Спокойствие»: прицел качается на 25% меньше 30 мин.', '"Calm": aim sway 25% less for 30 min.'),
    'hands': ('«Твёрдые пальцы»: шанс развести огонь +10% на 30 мин.', '"Steady fingers": fire-starting chance +10% for 30 min.'),
    'steady': ('«Ровное дыхание»: на бегу выносливость тратится на 15% медленнее 30 мин.', '"Steady breath": sprinting drains stamina 15% slower for 30 min.'),
}
SIDE = {
    '': ('', ''),
    'dry': ('Минус: «Сухость во рту» — жажда растёт на 15% быстрее 30 мин.', 'Downside: "Dry mouth": thirst builds 15% faster for 30 min.'),
    'dizzy': ('Минус: «Головокружение» — прицел качается на 30% сильнее 30 мин.', 'Downside: "Dizzy": aim sway 30% more for 30 min.'),
}


def grip_of(fmt):
    """Radius where the fingers hold it (about 25 mm from the mouth end), m: the fingers open by this, not by the thickest place
    (11-13: tipped, Toskanello and Backwuds hung in the air between fingers opened for their bellies)."""
    prof = profile(fmt)
    z = .025
    for (z0, r0), (z1, r1) in zip(prof, prof[1:]):
        if z0 <= z <= z1:
            r = r0 + (r1 - r0) * (z - z0) / max(1e-9, z1 - z0)
            break
    else:
        r = prof[-1][1]
    return r * (.9 if fmt == 'square' else 1.0)


def stop_of(b, v):
    fmt = fmt_of(b, v)
    if fmt == 'tip': return FORMATS[fmt][2] + .006
    if fmt in ('mini', 'slim'): return .027 if filt_of(b, v) else .022
    return .030


def num_ru(x): return ('%g' % x).replace('.', ',')


def texts(b, v):
    import cigarillo_memes as mm
    k = b['key'] + '_' + v['key']
    fmt = fmt_of(b, v)
    st = strength_of(b, v)
    f, h, hp, _ = STATS[st]
    en_brand = 'Pogarr' if b['key'] == 'Pogarr' else b['name']
    if b['key'] == 'CubanMini':
        nr, ne = 'Сигариллы ' + v['en'], v['en'] + ' cigarillos'
    else:
        nr, ne = 'Сигариллы %s %s' % (b['name'], v['ru'] if b['key'] == 'Pogarr' else v['en']), '%s %s cigarillos' % (en_brand, v['en'])
    fr = FMT_RU[fmt] + (', с фильтром' if filt_of(b, v) else '')
    fe = FMT_EN[fmt] + (', with a filter' if filt_of(b, v) else '')
    dr = '%s %s %s: %s, %s. %d штук %s. Гильотина не нужна. Сигарилла: усталость −%s%%, голод на 10%% медленнее %d мин, здоровье −%s%%.' % (
        mm.MEME[k][0], mm.BLURB[b['key']][0], v['ru'], STR_RU[st], fr, b['units'], PACK_RU[BASE.get(b['pack'], b['pack'])], num_ru(f), h, num_ru(hp))
    de = '%s %s %s: %s, %s. %d %s. No cutter needed. A cigarillo: fatigue −%g%%, hunger 10%% slower for %d min, condition −%g%%.' % (
        mm.MEME[k][1], mm.BLURB[b['key']][1], v['en'], STR_EN[st], fe, b['units'], PACK_EN[BASE.get(b['pack'], b['pack'])], f, h, hp)
    for key, tab in ((v['bonus'], BONUS), (v['side'], SIDE)):
        r_, e_ = tab[key]
        if r_:   # a cigarillo's bonus and downside last 45 min (cigarettes: 30)
            dr += ' ' + r_.replace('30 мин', '45 мин'); de += ' ' + e_.replace('30 min', '45 min')
    return (nr, dr), (ne, de)


def emit(tex, meshdir, src, out):
    import json, shutil
    def cs(x): return '"' + x.replace('\\', '\\\\').replace('"', '\\"') + '"'
    for sub in ('textures', 'meshes', 'data', 'icons'):
        os.makedirs(os.path.join(out, sub), exist_ok=True)
    for f in os.listdir(meshdir):
        shutil.copy(os.path.join(meshdir, f), os.path.join(out, 'meshes', f))
    ru, en, rows = {}, {}, []
    for b, v in all_varieties():
        iid = item_id(b, v)
        (nr, dr), (ne, de) = texts(b, v)
        ru['SMOKES_%s_NAME' % iid], ru['SMOKES_%s_DESC' % iid] = nr, dr
        en['SMOKES_%s_NAME' % iid], en['SMOKES_%s_DESC' % iid] = ne, de
        for t in (tex_name(b, v), pack_tex(b, v)):
            shutil.copy(os.path.join(tex, t), os.path.join(out, 'textures', t))
        rows.append('        new(%s, %s, %s, %s, %s, %s, %d, %s, %s, %s, %d, %sf, %df, %.4ff),' % (
            cs(iid), cs(b['key']), cs(pack_mesh_name(b)), cs(pack_tex(b, v)), cs(tex_name(b, v)), cs(mesh_name(b, v)), strength_of(b, v), cs(v['bonus']),
            'true' if v['rare'] else 'false', cs(v['side']), b['units'], ('%.3f' % stop_of(b, v)).rstrip('0'), FORMATS[fmt_of(b, v)][3], grip_of(fmt_of(b, v))))
    ru.update({'SMOKES_UNIT_CGL_ONE': 'сигарилла', 'SMOKES_UNIT_CGL_MANY': 'сигарилл', 'SMOKES_UNIT_CGL_SHORT': 'шт.'})
    en.update({'SMOKES_UNIT_CGL_ONE': 'cigarillo', 'SMOKES_UNIT_CGL_MANY': 'cigarillos', 'SMOKES_UNIT_CGL_SHORT': 'pcs'})
    for lang, d in (('ru', ru), ('en', en)):
        with open(os.path.join(out, 'data', 'text_cgl_%s.json' % lang), 'w', encoding='utf-8') as fp:
            json.dump(d, fp, ensure_ascii=False, indent=1)
    with open(os.path.join(src, 'CigarilloCatalog.cs'), 'w', encoding='utf-8') as fp:
        fp.write('namespace SmokesByEMPEROR;\n\n/// <summary>Generated by tools/cigarillos.py: the cigarillos (one pack item each). Stop = how far from the mouth end it burns down to, m;\n'
                 '/// Seconds = real seconds it burns (a regular cigarette is 90); Grip = its radius where the fingers hold it, m.</summary>\npublic static class CigarilloCatalog\n{\n'
                 '    public sealed record Var(string Id, string Brand, string PackMesh, string PackTexture, string CigTexture, string CigMesh, int Strength, string Bonus, bool Rare, string Side,\n'
                 '        int Units, float Stop, float Seconds, float Grip);\n\n    public static readonly Var[] All =\n    {\n' + '\n'.join(rows) + '\n    };\n}\n')
    print('emitted', len(rows))


# ---------------------------------------------------------------- main
if __name__ == '__main__':
    bake, tex, meshdir = sys.argv[1:4]
    only = set(sys.argv[4:])
    for p in (bake, tex, meshdir): os.makedirs(p, exist_ok=True)
    along = ry(math.pi / 2)
    flat = rz(math.pi) @ rx(-math.pi / 2)
    for fmt in FORMATS:
        m = cig_mesh(fmt)
        np.savez(os.path.join(bake, 'cgl_%s.npz' % fmt), verts=m[0], normals=m[1], uvs=m[2], tris=m[3])
        rest_fp(os.path.join(meshdir, 'cgl_%s.fpmesh' % fmt), m, along)
        held_fp(os.path.join(meshdir, 'cgl_%s_held.fpmesh' % fmt), m)
    for kind in PACKS:
        m = pack_mesh(kind)
        np.savez(os.path.join(bake, 'cglpack_%s.npz' % kind), verts=m[0], normals=m[1], uvs=m[2], tris=m[3])
        rest_fp(os.path.join(meshdir, 'cglpack_%s.fpmesh' % kind), m, flat)
    n = 0
    for b, v in all_varieties():
        if only and b['key'] not in only: continue
        cig_texture(b, v, os.path.join(tex, tex_name(b, v)))
        pack_texture(b, v, os.path.join(tex, pack_tex(b, v)))
        n += 1
    if len(sys.argv) > 5 and sys.argv[4] == '--emit':
        pass
    print('cigarillos:', len(BRANDS), 'brands,', sum(len(b['vs']) for b in BRANDS), 'varieties;', n, 'drawn')

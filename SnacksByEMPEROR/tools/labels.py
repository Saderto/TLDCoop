"""Package art for every item. python3 labels.py [ids] -> ../tex/<Id>.png (front|back side by side for bags), ../prev/<Id>_label.png"""
import sys, os, math, random
from PIL import Image, ImageDraw, ImageFilter
import art as A
from art import rgb, mix
from catalog import NOODLES, SNACKS, SIZES

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', 'tex')
SEAL = .06
K = 2  # supersampling


def canvas(shape, bg='#ffffff'):
    W, H, T = SIZES[shape]
    fw = 512
    fh = int(round(fw * H / W / 4)) * 4
    return Image.new('RGB', (fw * K, fh * K), rgb(bg)), fw, fh


def seals(im, col, line=None):
    w, h = im.size
    s = int(h * SEAL)
    A.crimp_band(im, 0, s, col)
    A.crimp_band(im, h - s, h, col)
    return s


# ------------------------------------------------------------------ backs

NUTR = {
    'ru': ('Пищевая ценность в 100 г', ['Белки', 'Жиры', 'Углеводы', 'Энергетическая ценность'], 'Состав', 'Хранить в сухом месте при температуре не выше +25 °C.', 'Масса нетто'),
    'en': ('Nutrition per 100 g', ['Protein', 'Fat', 'Carbohydrate', 'Energy'], 'Ingredients', 'Store in a cool dry place.', 'Net wt'),
    'ja': ('Nutrition per 100 g', ['Protein', 'Fat', 'Carbohydrate', 'Energy'], 'Ingredients', 'Store in a cool dry place. 直射日光を避けて保存', 'Net wt'),
    'ko': ('Nutrition per 100 g', ['Protein', 'Fat', 'Carbohydrate', 'Energy'], 'Ingredients', 'Store in a cool dry place. 직사광선을 피하세요', 'Net wt'),
}


def back(spec, bg, ink, accent, size, ingredients, how=None, maker='', seal_col=None):
    shape = spec['shape']
    im, fw, fh = canvas(shape, bg)
    w, h = im.size
    lang = spec.get('lang', 'en'); L = NUTR[lang if lang in NUTR else 'en']
    top = int(h * SEAL) + 30 if shape in SIZES else 30
    # brand strip
    A.rrect(im, (40, top, w - 40, top + 130), 20, accent)
    A.fit_text(im, (70, top + 15, w - 70, top + 115), spec['en'] if lang != 'ru' else spec['ru'], 'oswald', bg if bg != '#ffffff' else '#ffffff')
    y = top + 170
    if how:
        A.text(im, (60, y), how[0], A.font('oswald', 54), ink, anchor='lm'); y += 50
        y = A.lines(im, 60, y, how[1:], A.font('ptsans', 38), ink, maxw=w - 120) + 20
    A.text(im, (60, y), L[2], A.font('oswald', 50), ink, anchor='lm'); y += 40
    y = A.lines(im, 60, y, [ingredients], A.font('ptsans', 34), ink, maxw=w - 120) + 20
    # nutrition table
    kcal100 = spec['kcal'] / max(.01, spec['kg']) / 10
    A.rrect(im, (50, y, w - 50, y + 300), 14, None, outline=ink, width=4)
    A.text(im, (75, y + 35), L[0], A.font('ptsans_b', 38), ink, anchor='lm')
    vals = [f'{kcal100 * .1 / 4:.1f} г' if lang == 'ru' else f'{kcal100 * .1 / 4:.1f} g', f'{kcal100 * .45 / 9:.1f} г' if lang == 'ru' else f'{kcal100 * .45 / 9:.1f} g',
            f'{kcal100 * .45 / 4:.1f} г' if lang == 'ru' else f'{kcal100 * .45 / 4:.1f} g', f'{kcal100:.0f} ккал' if lang == 'ru' else f'{kcal100:.0f} kcal']
    d = ImageDraw.Draw(im)
    for i, (n, v) in enumerate(zip(L[1], vals)):
        yy = y + 90 + i * 52
        d.line([(70, yy - 22), (w - 70, yy - 22)], fill=rgb(ink), width=2)
        d.text((80, yy), n, font=A.font('ptsans', 36), fill=rgb(ink), anchor='lm')
        d.text((w - 80, yy), v, font=A.font('ptsans_b', 36), fill=rgb(ink), anchor='rm')
    y += 330
    y = A.lines(im, 60, y, [L[3], maker], A.font('ptsans', 32), ink, maxw=w - 120) + 10
    bh = 150
    by = min(y + 10, h - top - bh - 80)
    A.barcode(im, w - 400, by, 330, bh, seed=hash(spec['id']) % 1000, digits=f"4 6{abs(hash(spec['id'])) % 100000:05d} {abs(hash(spec['en'])) % 1000000:06d}")
    A.text(im, (80, by + 60), f"{L[4]}", A.font('ptsans', 36), ink, anchor='lm')
    A.text(im, (80, by + 120), size, A.font('oswald', 70), ink, anchor='lm')
    if shape in SIZES: seals(im, seal_col or bg)
    return im


def finish(front, backim, sid):
    w, h = front.size
    sheet = Image.new('RGB', (w * 2, h))
    sheet.paste(A.sheen(front, 46), (0, 0))
    sheet.paste(A.sheen(backim, 30), (w, 0))
    sheet = sheet.resize((w, h // 1), Image.LANCZOS) if False else sheet.resize((sheet.size[0] // K, sheet.size[1] // K), Image.LANCZOS)
    os.makedirs(OUT, exist_ok=True)
    sheet.save(os.path.join(OUT, sid + '.png'))
    return sheet


# ------------------------------------------------------------------ noodle bricks

def Rolltan(s):
    im, fw, fh = canvas('brick', '#ffcc00')
    w, h = im.size
    im.paste(A.vgrad(w, h, '#ffd84a', '#f5a300'))
    A.burst(im, w / 2, h * .62, 28, '#ffffff', alpha=40)
    sl = int(h * SEAL)
    A.poly(im, [(0, sl), (w, sl), (w, sl + 230), (0, sl + 300)], '#d71920')
    A.text(im, (w / 2, sl + 130), 'РОЛЛТАН', A.font('inter_black', 150), '#ffffff', stroke=8, stroke_fill='#7a0d10', skew=-.18, shadow='#000000', blur=6)
    A.rrect(im, (w * .12, sl + 320, w * .88, sl + 420), 48, '#ffffff')
    A.text(im, (w / 2, sl + 370), 'ПО-ДОМАШНЕМУ', A.font('oswald', 64), '#d71920')
    A.noodle_bowl(im, w * .5, h * .64, w * .36, broth='#f0c060', bowl='#ffffff', bowl_rim='#d71920', toppings=('egg', 'scallion'), seed=2)
    A.chicken(im, w * .2, h * .84, w * .1)
    A.rrect(im, (w * .38, h * .8, w * .95, h * .9), 30, '#d71920')
    A.text(im, (w * .665, h * .85), 'С КУРИЦЕЙ', A.font('oswald', 70), '#ffffff')
    A.badge(im, w * .82, sl + 520, 90, '#2e7d32')
    A.text(im, (w * .82, sl + 505), '85', A.font('oswald', 70), '#ffffff')
    A.text(im, (w * .82, sl + 560), 'грамм', A.font('ptsans_b', 28), '#ffffff')
    seals(im, '#d71920')
    b = back(s, '#ffd84a', '#3a1d00', '#d71920', '85 г', 'лапша (мука пшеничная, масло пальмовое, соль), приправа (соль, сахар, ароматизатор «курица», лук, перец), масло растительное.',
             how=('Приготовление', 'Залейте 400 мл кипятка, накройте и подождите 3 минуты. Помешайте.'), maker='Изготовитель: ООО «Роллтан-Север», Россия.')
    return finish(im, b, s['id'])


def Mivinka(s):
    im, fw, fh = canvas('brick', '#1d4fa0')
    w, h = im.size
    im.paste(A.vgrad(w, h, '#2a63c4', '#0f2d66'))
    sl = int(h * SEAL)
    A.ellipse(im, (-w * .3, h * .45, w * 1.3, h * 1.3), '#f68b1f')
    A.text(im, (w / 2, sl + 150), 'Мивинка', A.font('ptserif_bi', 180), '#ffffff', stroke=6, stroke_fill='#0f2d66', shadow='#000000', blur=8, rotate=6)
    A.text(im, (w / 2, sl + 300), 'ЛАПША БЫСТРОГО ПРИГОТОВЛЕНИЯ', A.font('oswald4', 46), '#ffd54f')
    A.noodle_bowl(im, w * .5, h * .58, w * .33, broth='#c27a3a', bowl='#f2f2f2', bowl_rim='#1d4fa0', toppings=('meat', 'scallion'), seed=6, pattern='band')
    A.rrect(im, (w * .08, h * .78, w * .92, h * .9), 20, '#ffffff')
    A.text(im, (w / 2, h * .84), 'СО ВКУСОМ ГОВЯДИНЫ', A.font('oswald', 66), '#c0392b')
    A.text(im, (w * .88, h * .92), '60 г', A.font('oswald', 54), '#ffffff', anchor='rm')
    seals(im, '#0f2d66')
    b = back(s, '#e9f0fb', '#0f2d66', '#1d4fa0', '60 г', 'лапша (мука, масло, соль, яичный порошок), приправа «говядина» (соль, сахар, перец, чеснок, укроп).',
             how=('Як готувати / Как готовить', 'Залейте 350 мл кипятка, накройте на 4 минуты.'), maker='Изготовитель: «Мивинка», Харьков.')
    return finish(im, b, s['id'])


def MiGoreng(s):
    im, fw, fh = canvas('brick', '#ffffff')
    w, h = im.size
    sl = int(h * SEAL)
    A.poly(im, [(0, 0), (w, 0), (w, h * .33), (0, h * .45)], '#1b7e3a')
    A.poly(im, [(0, h * .45), (w, h * .33), (w, h * .36), (0, h * .48)], '#ffcc00')
    A.text(im, (w * .5, sl + 110), 'Indomay', A.font('pacifico', 150), '#ffffff', stroke=6, stroke_fill='#d32f2f', rotate=4)
    A.text(im, (w * .5, sl + 290), 'MI GORENG', A.font('inter_black', 112), '#ffffff', stroke=6, stroke_fill='#0b4d22')
    A.text(im, (w * .5, sl + 390), 'MIE GORENG · FRIED NOODLES', A.font('oswald', 40), '#ffcc00')
    # a plate of fried noodles: no broth
    A.noodle_bowl(im, w * .48, h * .67, w * .38, broth='#9c5a22', bowl='#ffffff', bowl_rim='#e0e0e0', toppings=('egg', 'scallion', 'chili'), steam=True, seed=9, noodle_col='#c98a3c')
    A.rrect(im, (w * .06, h * .85, w * .7, h * .92), 16, '#d32f2f')
    A.text(im, (w * .38, h * .885), 'SWEET SOY · NO SOUP', A.font('oswald', 46), '#ffffff')
    A.badge(im, w * .84, h * .885, 85, '#ffcc00', ring='#d32f2f')
    A.text(im, (w * .84, h * .885), '85g', A.font('oswald', 54), '#d32f2f')
    seals(im, '#1b7e3a')
    b = back(s, '#ffffff', '#0b4d22', '#1b7e3a', '85 g', 'Noodles (wheat flour, palm oil, salt), sweet soy sauce, seasoning oil, chilli powder, fried shallots.',
             how=('How to cook', 'Boil noodles in 400 ml of water for 3 minutes, drain, mix with seasoning and sauce.'), maker='Indomay Food Co., Jakarta.')
    return finish(im, b, s['id'])


def Shine(s):
    im, fw, fh = canvas('brick', '#c8102e')
    w, h = im.size
    im.paste(A.vgrad(w, h, '#e0262e', '#9a0b1c'))
    sl = int(h * SEAL)
    A.rrect(im, (w * .1, sl + 40, w * .9, sl + 420), 30, '#111111')
    A.text(im, (w / 2, sl + 180), '샤인', A.font('cjk', 210), '#ffffff', stroke=4, stroke_fill='#000000')
    A.text(im, (w / 2, sl + 350), 'SHINE RAMYUN', A.font('bebas', 110), '#ffd400')
    A.noodle_bowl(im, w * .5, h * .63, w * .36, broth='#d0381e', bowl='#111111', bowl_rim='#333333', toppings=('mushroom', 'scallion', 'chili'), seed=12, noodle_col='#f0cd7c')
    A.flames(im, 0, h - sl, w, h * .1, seed=3, alpha=220)
    A.text(im, (w / 2, h * .83), 'HOT & SPICY', A.font('bebas', 110), '#ffffff', stroke=5, stroke_fill='#7a0010')
    A.text(im, (w * .9, h * .9), '120g', A.font('oswald', 56), '#ffffff', anchor='rm')
    seals(im, '#9a0b1c')
    b = back(s, '#fff3e0', '#5a0000', '#c8102e', '120 g', 'Noodles (wheat flour, potato starch, palm oil), soup base (salt, red pepper powder, beef extract, garlic), dried shiitake, green onion.',
             how=('How to cook', 'Add noodles, soup base and flakes to 550 ml of boiling water. Cook 4½ minutes.'), maker='Shine Foods, Seoul.')
    return finish(im, b, s['id'])


def FireChicken(s):
    im, fw, fh = canvas('brick', '#111111')
    w, h = im.size
    im.paste(A.radial(w // 4, h // 4, '#3a0b0b', '#0a0a0a').resize((w, h)))
    sl = int(h * SEAL)
    A.flames(im, 0, h * .5, w, h * .3, seed=7, n=11, alpha=200)
    A.text(im, (w / 2, sl + 110), 'SAMJAN', A.font('alfa', 120), '#ffffff')
    A.text(im, (w / 2, sl + 250), '불닭볶음면', A.font('cjk', 96), '#ff3b1f', stroke=4, stroke_fill='#000000')
    A.chicken(im, w * .5, h * .5, w * .17, body='#ffffff', comb='#ff2a00', angry=True)
    A.noodle_bowl(im, w * .5, h * .74, w * .3, broth='#a3160b', bowl='#1d1d1d', bowl_rim='#ff3b1f', toppings=('chili', 'scallion'), seed=14, noodle_col='#e86a2a', chopsticks=False)
    A.rrect(im, (w * .05, h * .84, w * .95, h * .91), 14, '#ff3b1f')
    A.text(im, (w / 2, h * .875), 'FIRE CHICKEN · EXTREMELY HOT', A.font('oswald', 50), '#000000')
    seals(im, '#1d1d1d')
    b = back(s, '#1d1d1d', '#ffffff', '#ff3b1f', '140 g', 'Noodles (wheat flour, tapioca starch, palm oil), hot chicken sauce (capsicum extract, chilli, soy, chicken), seaweed flakes, sesame.',
             how=('How to cook', 'Boil noodles in 600 ml of water for 5 minutes. Drain, leave 8 spoons of water, add sauce. WARNING: very hot.'), maker='Samjan Foods, Seoul.')
    return finish(im, b, s['id'])


def Chikin(s):
    im, fw, fh = canvas('brick', '#f39200')
    w, h = im.size
    im.paste(A.vgrad(w, h, '#f6a21d', '#e47a00'))
    sl = int(h * SEAL)
    for k in range(9):
        A.ellipse(im, (w * .05 + k * w * .11, sl + 30, w * .13 + k * w * .11, sl + 110), '#ffffff', alpha=70)
    A.rrect(im, (w * .08, sl + 140, w * .92, sl + 440), 24, '#ffffff')
    A.text(im, (w / 2, sl + 230), 'チキンラーメン', A.font('cjk', 96), '#e2001a')
    A.text(im, (w / 2, sl + 370), 'Chikin Ramen', A.font('playfair_l', 100), '#5a2a00')
    A.noodle_bowl(im, w * .5, h * .66, w * .34, broth='#e2b04c', bowl='#ffffff', bowl_rim='#e2001a', toppings=('egg', 'scallion'), seed=21, pattern='band')
    A.text(im, (w * .5, h * .855), 'NISSUN · SINCE 1958', A.font('oswald', 58), '#ffffff', stroke=3, stroke_fill='#7a3d00')
    A.text(im, (w * .5, h * .9), '85g · 3 MIN', A.font('oswald4', 46), '#5a2a00')
    seals(im, '#e47a00')
    b = back(s, '#fff4e0', '#5a2a00', '#e2001a', '85 g', 'Noodles (wheat flour, chicken extract, soy sauce, salt, spices), palm oil.',
             how=('How to cook', 'Pour 450 ml of boiling water over the block, cover, wait 3 minutes. お湯をかけて3分'), maker='Nissun Food Products, Osaka.')
    return finish(im, b, s['id'])


# ------------------------------------------------------------------ cups and tray (other layouts: side wrap + lid)

def wrap_sheet(w, h):
    return Image.new('RGB', (w * K, h * K), (255, 255, 255))


def Doshik(s):
    # tray texture 1024x1024: lid in the top-left (0..0.62, 0.45..1), side strip at the bottom (0..1, 0..0.42)
    W = H = 1024
    im = Image.new('RGB', (W * K, H * K), rgb('#c62828'))
    w, h = im.size
    # lid region (x 0..0.62w, y 0..0.55h)
    lx, ly = int(w * .62), int(h * .55)
    lid = A.vgrad(lx, ly, '#e53935', '#b71c1c')
    A.burst(lid, lx * .5, ly * .55, 24, '#ffffff', alpha=25)
    A.text(lid, (lx / 2, ly * .2), 'ДОШИК', A.font('inter_black', 210), '#ffffff', stroke=10, stroke_fill='#5a0000', shadow='#000000', blur=8)
    A.text(lid, (lx / 2, ly * .36), '도시크 · ЛАПША В ЛОТКЕ', A.font('cjk', 54), '#ffeb3b')
    A.noodle_bowl(lid, lx * .5, ly * .64, lx * .24, broth='#b5642c', bowl='#ffffff', bowl_rim='#ffeb3b', toppings=('meat', 'scallion'), seed=31)
    A.rrect(lid, (lx * .1, ly * .84, lx * .9, ly * .95), 30, '#ffeb3b')
    A.text(lid, (lx / 2, ly * .895), 'ГОВЯДИНА · 90 г', A.font('oswald', 70), '#b71c1c')
    A.text(lid, (lx * .93, ly * .07), 'ОТКРЫВАТЬ ЗДЕСЬ', A.font('ptsans_b', 32), '#ffffff', anchor='rm')
    im.paste(lid, (0, 0))
    # side strip (y 0.58h..h)
    sy = int(h * .58)
    side = A.vgrad(w, h - sy, '#f5f5f5', '#d6d6d6')
    sd = ImageDraw.Draw(side)
    for k in range(6):
        x = k * w / 6
        A.text(side, (x + w / 12, (h - sy) * .3), 'ДОШИК', A.font('inter_black', 90), '#c62828')
        A.text(side, (x + w / 12, (h - sy) * .62), 'говядина', A.font('ptsans_b', 48), '#333333')
    im.paste(side, (0, sy))
    # right column: back text (on the lid's underside nobody sees; used for the bottom)
    b = Image.new('RGB', (w - lx, ly), rgb('#ffffff'))
    A.lines(b, 30, 40, ['Лапша «Дошик»', 'Залейте 500 мл кипятка до линии, закройте крышку на 4 минуты.', 'Масса нетто 90 г'], A.font('ptsans', 40), '#333333', maxw=w - lx - 60)
    im.paste(b, (lx, 0))
    out = im.resize((W, H), Image.LANCZOS)
    out.save(os.path.join(OUT, s['id'] + '.png'))
    return out


def cup_art(s, side_fn, lid_fn, wall='#ffffff'):
    """Cup texture 1024x1024: side wrap in the top 70% (v .3..1), lid disc centred at (.15,.15) r .14, open food at (.5,.15) r .14."""
    W = H = 1024
    im = Image.new('RGB', (W * K, H * K), rgb(wall))
    w, h = im.size
    sh = int(h * .7)
    side = Image.new('RGB', (w, sh), rgb(wall))
    side_fn(side)
    im.paste(side, (0, 0))
    lid = Image.new('RGB', (int(w * .3), int(w * .3)), (230, 230, 230))
    lid_fn(lid)
    m = Image.new('L', lid.size, 0); ImageDraw.Draw(m).ellipse((0, 0, lid.size[0], lid.size[1]), fill=255)
    im.paste(lid, (int(w * .15 - lid.size[0] / 2), int(h * .85 - lid.size[1] / 2)), m)
    # food inside (for the brewed one)
    fd = Image.new('RGB', lid.size, rgb(s['broth']))
    A.noodle_bowl(fd, fd.size[0] / 2, fd.size[1] * .5, fd.size[0] * .5, broth=s['broth'], bowl=s['broth'], bowl_rim=s['broth'], toppings=s['top'], steam=False, chopsticks=False, seed=3)
    im.paste(fd, (int(w * .5 - fd.size[0] / 2), int(h * .85 - fd.size[1] / 2)), m)
    out = im.resize((W, H), Image.LANCZOS)
    out.save(os.path.join(OUT, s['id'] + '.png'))
    return out


def KappNoodle(s):
    def side(im):
        w, h = im.size
        im.paste(A.vgrad(w, h, '#ffffff', '#eef3f5'))
        A.rrect(im, (0, h * .62, w, h * .72), 0, '#e60012')
        A.rrect(im, (0, h * .74, w, h * .78), 0, '#00a0c6')
        for k in range(2):
            x = w * (.25 + .5 * k)
            A.text(im, (x, h * .3), 'KAPP', A.font('alfa', 180), '#e60012')
            A.text(im, (x, h * .5), 'NOODLE', A.font('alfa', 150), '#e60012')
            A.text(im, (x, h * .67), 'SEAFOOD', A.font('bebas', 110), '#ffffff')
            A.text(im, (x, h * .88), 'カップヌードル · 70g', A.font('cjk', 60), '#333333')
    def lid(im):
        w, h = im.size
        im.paste(A.radial(w // 4, h // 4, '#ffffff', '#cfd8dc').resize((w, h)))
        A.text(im, (w / 2, h * .42), 'KAPP', A.font('alfa', 120), '#e60012')
        A.text(im, (w / 2, h * .62), 'open here', A.font('ptsans_b', 40), '#555555')
    return cup_art(s, side, lid)


def BiggLanch(s):
    def side(im):
        w, h = im.size
        im.paste(A.vgrad(w, h, '#1f7a3a', '#0d4a22'))
        for k in range(2):
            x = w * (.25 + .5 * k)
            A.rrect(im, (x - w * .2, h * .1, x + w * .2, h * .52), 40, '#ffd200')
            A.text(im, (x, h * .22), 'БИГГ', A.font('inter_black', 170), '#0d4a22')
            A.text(im, (x, h * .4), 'ЛАНЧ', A.font('inter_black', 170), '#e53935')
            A.noodle_bowl(im, x, h * .74, w * .1, broth=s['broth'], bowl='#ffffff', bowl_rim='#ffd200', toppings=('mushroom', 'meat'), seed=40 + k)
            A.text(im, (x, h * .94), 'ГОВЯДИНА С ГРИБАМИ · 100 г', A.font('oswald', 54), '#ffffff')
    def lid(im):
        w, h = im.size
        im.paste(A.radial(w // 4, h // 4, '#ffe14d', '#f2b700').resize((w, h)))
        A.text(im, (w / 2, h * .45), 'БИГГ', A.font('inter_black', 110), '#0d4a22')
        A.text(im, (w / 2, h * .66), 'ЛАНЧ', A.font('inter_black', 90), '#e53935')
    return cup_art(s, side, lid)


def AkaiKitsune(s):
    def side(im):
        w, h = im.size
        im.paste(A.vgrad(w, h, '#d7261e', '#a3160f'))
        for k in range(3):
            x = w * (1 / 6 + k / 3)
            A.ellipse(im, (x - w * .12, h * .08, x + w * .12, h * .08 + w * .24), '#ffffff')
            A.fox(im, x, h * .08 + w * .12, w * .08)
            A.text(im, (x, h * .6), '赤いきつね', A.font('cjk', 96), '#ffffff')
            A.text(im, (x, h * .77), 'AKAI KITSUNE', A.font('bebas', 90), '#ffe0a0')
            A.text(im, (x, h * .9), 'UDON · 100g', A.font('oswald4', 54), '#ffffff')
    def lid(im):
        w, h = im.size
        im.paste(A.radial(w // 4, h // 4, '#ffffff', '#f3d6d0').resize((w, h)))
        A.fox(im, w / 2, h * .4, w * .2)
        A.text(im, (w / 2, h * .78), 'うどん', A.font('cjk', 70), '#d7261e')
    return cup_art(s, side, lid)


# ------------------------------------------------------------------ snacks

def Leiz(s):
    im, fw, fh = canvas('bag', '#2e9a3e')
    w, h = im.size
    im.paste(A.vgrad(w, h, '#43b649', '#1f7a2e'))
    sl = int(h * SEAL)
    A.ellipse(im, (w * .12, sl + 40, w * .88, sl + 40 + w * .76), '#ffd200')
    A.burst(im, w / 2, sl + 40 + w * .38, 30, '#ffffff', r=w * .38, alpha=70)
    A.rrect(im, (w * .06, sl + 220, w * .94, sl + 420), 40, '#e4002b')
    A.text(im, (w / 2, sl + 318), 'ЛЕЙЗ', A.font('inter_black', 190), '#ffffff', stroke=6, stroke_fill='#8c001a', shadow='#000000', blur=5)
    A.chips(im, w * .5, h * .62, w * .36, col='#f6d36b', edge='#cf9a2a', n=11, seed=4, spice='#3c8d2f')
    A.rrect(im, (w * .1, h * .8, w * .9, h * .9), 28, '#ffffff')
    A.text(im, (w / 2, h * .85), 'СМЕТАНА И ЗЕЛЕНЬ', A.font('oswald', 74), '#1f7a2e')
    A.text(im, (w * .9, h * .925), '140 г', A.font('oswald', 54), '#ffffff', anchor='rm')
    seals(im, '#1f7a2e')
    b = back(s, '#eaf6ea', '#123d18', '#1f7a2e', '140 г', 'картофель, масло подсолнечное, приправа «сметана и зелень» (сыворотка молочная, соль, укроп, лук, петрушка).', maker='Изготовитель: «Лейз Снек Компани», Россия.')
    return finish(im, b, s['id'])


def Chitoz(s):
    im, fw, fh = canvas('bag', '#ff7a00')
    w, h = im.size
    im.paste(A.vgrad(w, h, '#ff9a1a', '#e85d00'))
    sl = int(h * SEAL)
    rnd = random.Random(3)
    for k in range(40):
        x, y = rnd.random() * w, sl + rnd.random() * (h - 2 * sl)
        A.ellipse(im, (x - 22, y - 16, x + 22, y + 16), '#b33c00', alpha=90)
    A.text(im, (w / 2, sl + 200), 'Chitoz', A.font('pacifico', 220), '#ffffff', stroke=10, stroke_fill='#1b5e20', shadow='#000000', blur=6, rotate=8)
    A.cheetah_paw(im, w * .8, sl + 420, 70, '#1b5e20')
    A.puffs(im, w * .5, h * .62, w * .42, col='#ffa726', seed=5)
    A.rrect(im, (w * .12, h * .8, w * .88, h * .9), 50, '#1b5e20')
    A.text(im, (w / 2, h * .85), 'CHEESY · СЫРНЫЕ', A.font('oswald', 72), '#ffffff')
    A.text(im, (w * .9, h * .925), '85 g', A.font('oswald', 54), '#ffffff', anchor='rm')
    seals(im, '#e85d00')
    b = back(s, '#fff1e0', '#4a1d00', '#e85d00', '85 g', 'Corn meal, vegetable oil, cheese seasoning (whey, cheddar cheese, salt, paprika extract).', maker='Chitoz Snacks Inc.')
    return finish(im, b, s['id'])


def Pringlz(s):
    W = H = 1024
    im = Image.new('RGB', (W * K, H * K), rgb('#d2232a'))
    w, h = im.size
    sh = int(h * .75)
    side = A.vgrad(w, sh, '#e2262d', '#b4141b')
    for k in range(2):
        x = w * (.25 + .5 * k)
        A.mustache_face(side, x, sh * .3, w * .1)
        A.text(side, (x, sh * .55), "Pringlz", A.font('abril', 190), '#ffffff', stroke=4, stroke_fill='#7a0000')
        A.text(side, (x, sh * .68), 'ORIGINAL', A.font('bebas', 120), '#ffffff')
        A.chips(side, x, sh * .85, w * .1, col='#f7d27a', edge='#d99a32', n=6, seed=8 + k, ridges=False)
    A.text(side, (w * .5, sh * .96), '165 g · ONCE YOU POP...', A.font('oswald4', 48), '#ffffff')
    im.paste(side, (0, 0))
    # lid (centre .125,.125 r .12) and bottom (.375,.125)
    lid = A.radial(120, 120, '#e8e8e8', '#9e9e9e').resize((int(w * .25), int(w * .25)))
    A.text(lid, (lid.size[0] / 2, lid.size[1] / 2), 'Pringlz', A.font('abril', 80), '#d2232a')
    im.paste(lid, (0, int(h - lid.size[1])))
    bot = A.radial(120, 120, '#d8d8d8', '#8a8a8a').resize((int(w * .25), int(w * .25)))
    im.paste(bot, (int(w * .25), int(h - bot.size[1])))
    out = im.resize((W, H), Image.LANCZOS)
    out.save(os.path.join(OUT, s['id'] + '.png'))
    return out


def Estrelya(s):
    im, fw, fh = canvas('bag', '#c4161c')
    w, h = im.size
    im.paste(A.radial(w // 4, h // 4, '#e8382f', '#8f0d12').resize((w, h)))
    sl = int(h * SEAL)
    A.star(im, w * .5, sl + 210, 190, '#ffd400', outline='#9a6b00', width=6)
    A.text(im, (w / 2, sl + 460), 'Estrelya', A.font('playfair_l', 170), '#ffffff', stroke=5, stroke_fill='#5a0000', shadow='#000000', blur=6)
    A.chips(im, w * .5, h * .66, w * .36, col='#f2b24a', edge='#c5671f', n=12, seed=11, spice='#b3261e', ridges=True)
    A.rrect(im, (w * .08, h * .82, w * .92, h * .9), 20, '#ffd400')
    A.text(im, (w / 2, h * .86), 'PAPRIKA · ПАПРИКА', A.font('oswald', 68), '#8f0d12')
    A.text(im, (w * .9, h * .925), '130 g', A.font('oswald', 54), '#ffffff', anchor='rm')
    seals(im, '#8f0d12')
    b = back(s, '#fff0ea', '#4a0505', '#c4161c', '130 g', 'Potatoes, sunflower oil, paprika seasoning (sugar, salt, paprika, onion, garlic, tomato powder).', maker='Estrelya AB, Göteborg.')
    return finish(im, b, s['id'])


def Leningrad(s):
    im, fw, fh = canvas('bag', '#efe3c4')
    w, h = im.size
    im.paste(A.vgrad(w, h, '#f3e7c9', '#e0cfa6'))
    sl = int(h * SEAL)
    d = ImageDraw.Draw(im)
    A.rrect(im, (40, sl + 30, w - 40, h - sl - 30), 10, None, outline='#b0281f', width=10)
    A.rrect(im, (60, sl + 50, w - 60, h - sl - 50), 6, None, outline='#b0281f', width=3)
    A.text(im, (w / 2, sl + 150), 'ХРУСТЯЩИЙ', A.font('oldstd', 104), '#b0281f')
    A.text(im, (w / 2, sl + 270), 'КАРТОФЕЛЬ', A.font('oldstd', 104), '#b0281f')
    A.rrect(im, (w * .14, sl + 340, w * .86, sl + 440), 4, '#b0281f')
    A.text(im, (w / 2, sl + 390), 'ЛЕНИНГРАДСКИЙ', A.font('ptserif_b', 70), '#f3e7c9')
    A.chips(im, w * .5, h * .64, w * .34, col='#f1d38c', edge='#b88a3a', n=10, seed=17)
    A.star(im, w * .5, h * .83, 50, '#b0281f')
    A.text(im, (w / 2, h * .9), 'Масса нетто 100 г · Цена 46 коп.', A.font('ptserif_b', 46), '#3a2a1a')
    seals(im, '#d6c398')
    b = back(s, '#f3e7c9', '#3a2a1a', '#b0281f', '100 г', 'картофель, масло растительное, соль поваренная пищевая.', maker='Ленинградская фабрика хрустящего картофеля. ГОСТ.')
    return finish(im, b, s['id'])


def Kiryushki(s):
    im, fw, fh = canvas('smallbag', '#1a1a1a')
    w, h = im.size
    im.paste(A.vgrad(w, h, '#2b2b2b', '#111111'))
    sl = int(h * SEAL)
    A.poly(im, [(0, sl + 60), (w, sl), (w, sl + 300), (0, sl + 360)], '#e30613')
    A.text(im, (w / 2, sl + 180), 'КИРЮШКИ', A.font('inter_black', 150), '#ffffff', stroke=6, stroke_fill='#000000', rotate=5)
    A.croutons(im, w * .5, h * .62, w * .4, col='#5a3418', top='#7a4a24', seed=13, spice='#c62828')
    A.rrect(im, (w * .08, h * .8, w * .92, h * .9), 18, '#ffcc00')
    A.text(im, (w / 2, h * .85), 'РЖАНЫЕ · БЕКОН', A.font('oswald', 70), '#111111')
    seals(im, '#111111')
    b = back(s, '#2b2b2b', '#ffffff', '#e30613', '40 г', 'сухари ржаные (мука ржаная, вода, соль, дрожжи), масло подсолнечное, ароматизатор «бекон».', maker='Изготовитель: «Кирюшки», Россия.')
    return finish(im, b, s['id'])


def Korochki(s):
    im, fw, fh = canvas('smallbag', '#10235a')
    w, h = im.size
    im.paste(A.vgrad(w, h, '#1b3a8a', '#0b1a44'))
    sl = int(h * SEAL)
    A.ellipse(im, (w * .1, sl + 20, w * .9, sl + 20 + w * .8), '#c9a227')
    A.text(im, (w / 2, sl + 20 + w * .32), '4', A.font('playfair_l', 360), '#0b1a44')
    A.text(im, (w / 2, sl + 20 + w * .68), 'КОРОЧКИ', A.font('oswald', 92), '#0b1a44')
    A.croutons(im, w * .5, h * .7, w * .3, col='#a8743d', top='#c9925a', seed=15, spice='#e8e8e8')
    A.rrect(im, (w * .06, h * .82, w * .94, h * .9), 12, '#c9a227')
    A.text(im, (w / 2, h * .86), 'ХОЛОДЕЦ С ХРЕНОМ', A.font('oswald', 64), '#0b1a44')
    seals(im, '#0b1a44')
    b = back(s, '#e8ecf7', '#0b1a44', '#1b3a8a', '40 г', 'сухари из хлеба ржано-пшеничного, масло растительное, ароматизатор «холодец с хреном».', maker='Изготовитель: «Четыре корочки», Россия.')
    return finish(im, b, s['id'])


def Khrustam(s):
    im, fw, fh = canvas('smallbag', '#5b2a86')
    w, h = im.size
    im.paste(A.vgrad(w, h, '#7a3fb0', '#43206a'))
    sl = int(h * SEAL)
    A.text(im, (w / 2, sl + 140), 'ХРУСТАМ', A.font('roboto_black', 140), '#ffffff', skew=-.25, stroke=5, stroke_fill='#2a0f45')
    A.text(im, (w / 2, sl + 270), 'БАГЕТНЫЕ СУХАРИКИ', A.font('oswald4', 54), '#f0e0ff')
    A.croutons(im, w * .45, h * .62, w * .4, col='#d7a55a', top='#ecc27f', seed=19, long=True)
    # a garlic bulb
    gx, gy = w * .78, h * .76
    A.ellipse(im, (gx - 60, gy - 50, gx + 60, gy + 50), '#f5f0e6', outline='#bdb3a0', width=4)
    A.poly(im, [(gx - 12, gy - 50), (gx, gy - 95), (gx + 12, gy - 50)], '#e8e0cc')
    A.rrect(im, (w * .08, h * .84, w * .64, h * .91), 30, '#ffffff')
    A.text(im, (w * .36, h * .875), 'С ЧЕСНОКОМ', A.font('oswald', 62), '#5b2a86')
    seals(im, '#43206a')
    b = back(s, '#f3ecfa', '#2a0f45', '#5b2a86', '60 г', 'сухари из пшеничного багета, масло подсолнечное, соль, чеснок сушёный, ароматизатор «чеснок».', maker='Изготовитель: «Хрустам», Россия.')
    return finish(im, b, s['id'])


def Seeds(s):
    im, fw, fh = canvas('bag', '#111111')
    w, h = im.size
    im.paste(A.vgrad(w, h, '#1c1c1c', '#050505'))
    sl = int(h * SEAL)
    A.sunflower(im, w * .5, sl + 260, 230)
    A.text(im, (w / 2, sl + 560), 'ОТ МАРТЫНА', A.font('ptserif_b', 104), '#e6b422', shadow='#000000', blur=6)
    A.text(im, (w / 2, sl + 650), 'семечки отборные', A.font('ptserif_bi', 74), '#ffffff')
    A.seeds(im, w * .5, h * .75, w * .4, n=70)
    A.rrect(im, (w * .12, h * .86, w * .88, h * .91), 8, '#e6b422')
    A.text(im, (w / 2, h * .885), 'ЖАРЕНЫЕ · 150 г', A.font('oswald', 54), '#111111')
    seals(im, '#050505')
    b = back(s, '#1c1c1c', '#e6b422', '#e6b422', '150 г', 'семена подсолнечника жареные. Может содержать фрагменты шелухи.', maker='Изготовитель: «От Мартына», Россия.')
    return finish(im, b, s['id'])


def Oreshkin(s):
    im, fw, fh = canvas('smallbag', '#ffcf00')
    w, h = im.size
    im.paste(A.vgrad(w, h, '#ffe04a', '#f2b600'))
    sl = int(h * SEAL)
    A.text(im, (w / 2, sl + 150), 'ОРЕШКИН', A.font('comfortaa', 128), '#6d3b0f', stroke=4, stroke_fill='#ffffff')
    # a peanut character
    cx, cy = w * .5, sl + 380
    A.ellipse(im, (cx - 120, cy - 120, cx + 120, cy + 40), '#d29a5a', outline='#7a4a1c', width=6)
    A.ellipse(im, (cx - 100, cy + 10, cx + 100, cy + 170), '#d29a5a', outline='#7a4a1c', width=6)
    A.ellipse(im, (cx - 50, cy - 60, cx - 20, cy - 30), '#3e2723'); A.ellipse(im, (cx + 20, cy - 60, cx + 50, cy - 30), '#3e2723')
    ImageDraw.Draw(im).arc((cx - 45, cy - 40, cx + 45, cy + 10), 20, 160, fill=rgb('#3e2723'), width=6)
    A.peanuts(im, w * .5, h * .72, w * .38)
    A.rrect(im, (w * .08, h * .84, w * .92, h * .91), 30, '#6d3b0f')
    A.text(im, (w / 2, h * .875), 'СОЛЁНЫЙ АРАХИС', A.font('oswald', 64), '#ffe04a')
    seals(im, '#f2b600')
    b = back(s, '#fff7d6', '#4a2a0a', '#6d3b0f', '100 г', 'арахис жареный, соль, масло растительное.', maker='Изготовитель: «Орешкин», Россия.')
    return finish(im, b, s['id'])


def Starpom(s):
    im, fw, fh = canvas('smallbag', '#0b2545')
    w, h = im.size
    im.paste(A.vgrad(w, h, '#13315c', '#081a33'))
    sl = int(h * SEAL)
    d = ImageDraw.Draw(im)
    for k in range(5):
        y = h * .42 + k * 34
        d.arc((-50, y - 30, w + 50, y + 60), 200, 340, fill=rgb('#3e7cb1'), width=6)
    A.anchor_icon(im, w * .5, sl + 170, 110, '#f4d35e')
    A.text(im, (w / 2, sl + 380), 'СТАРПОМ', A.font('ptserif_b', 120), '#ffffff', stroke=3, stroke_fill='#000000')
    A.squid(im, w * .5, h * .66, w * .42)
    A.rrect(im, (w * .08, h * .83, w * .92, h * .9), 10, '#f4d35e')
    A.text(im, (w / 2, h * .865), 'КАЛЬМАР СУШЁНЫЙ', A.font('oswald', 64), '#081a33')
    seals(im, '#081a33')
    b = back(s, '#e6eef7', '#081a33', '#13315c', '70 г', 'кальмар (филе), соль, сахар, усилитель вкуса, перец.', maker='Изготовитель: «Старпом», Мурманск.')
    return finish(im, b, s['id'])


def Kinozal(s):
    im, fw, fh = canvas('bag', '#ffffff')
    w, h = im.size
    d = ImageDraw.Draw(im)
    for k in range(0, 10):
        if k % 2 == 0: d.rectangle((k * w / 10, 0, (k + 1) * w / 10, h), fill=rgb('#d62828'))
    sl = int(h * SEAL)
    A.rrect(im, (w * .08, sl + 60, w * .92, sl + 400), 40, '#101820')
    A.text(im, (w / 2, sl + 170), 'КИНОЗАЛ', A.font('inter_black', 170), '#ffd400')
    A.text(im, (w / 2, sl + 320), 'ПОПКОРН', A.font('oswald', 76), '#ffffff')
    A.popcorn(im, w * .5, h * .64, w * .36, n=36)
    A.rrect(im, (w * .12, h * .82, w * .88, h * .9), 30, '#101820')
    A.text(im, (w / 2, h * .86), 'СОЛЁНЫЙ · 100 г', A.font('oswald', 66), '#ffd400')
    seals(im, '#d62828')
    b = back(s, '#ffffff', '#101820', '#d62828', '100 г', 'кукуруза для попкорна, масло кокосовое, соль.', maker='Изготовитель: «Кинозал», Россия.')
    return finish(im, b, s['id'])


def Yozhik(s):
    im, fw, fh = canvas('bigbag', '#7fd1f0')
    w, h = im.size
    im.paste(A.vgrad(w, h, '#a6e3fa', '#4fb6e3'))
    sl = int(h * SEAL)
    for k in range(7):
        A.ellipse(im, (k * w / 6 - 80, sl + 30 + (k % 2) * 40, k * w / 6 + 100, sl + 140 + (k % 2) * 40), '#ffffff', alpha=200)
    A.text(im, (w / 2, sl + 260), 'Ёжик', A.font('ptserif_bi', 230), '#e65100', stroke=8, stroke_fill='#ffffff', shadow='#004466', blur=6)
    A.hedgehog(im, w * .5, sl + 520, 150)
    A.corn_sticks(im, w * .5, h * .7, w * .36)
    A.rrect(im, (w * .08, h * .84, w * .92, h * .91), 30, '#e65100')
    A.text(im, (w / 2, h * .875), 'КУКУРУЗНЫЕ ПАЛОЧКИ · СЛАДКИЕ', A.font('oswald', 50), '#ffffff')
    seals(im, '#4fb6e3')
    b = back(s, '#eaf7fd', '#0b3b52', '#e65100', '140 г', 'крупа кукурузная, сахарная пудра, масло растительное, соль.', maker='Изготовитель: «Ёжик», Россия.')
    return finish(im, b, s['id'])


# ------------------------------------------------------------------ brewed noodles: one bowl texture per kind

def brewed(s):
    """Bowl texture 1024: wall in the left half (u 0..0.5, v 0..0.5 = bottom-left quarter), food disc centred (.75,.75) r .24."""
    W = H = 1024
    im = Image.new('RGB', (W * K, H * K), (240, 236, 228))
    w, h = im.size
    # wall: bottom-left quarter, white ceramic with a coloured band in the brand colour
    band = {'Rolltan': '#d71920', 'Mivinka': '#1d4fa0', 'MiGoreng': '#1b7e3a', 'Shine': '#111111', 'FireChicken': '#ff3b1f', 'Chikin': '#e2001a'}.get(s['id'], '#8d6e63')
    wall = A.vgrad(w // 2, h // 2, '#f7f3ea', '#d9d2c4')
    dw = ImageDraw.Draw(wall)
    dw.rectangle((0, h * .5 * .2, w // 2, h * .5 * .28), fill=rgb(band))
    im.paste(wall, (0, h // 2))
    # food disc
    fd = Image.new('RGB', (int(w * .5), int(w * .5)), rgb(s['broth']))
    A.noodle_bowl(fd, fd.size[0] / 2, fd.size[1] * .5, fd.size[0] * .52, broth=s['broth'], bowl=s['broth'], bowl_rim=s['broth'], toppings=s['top'], steam=False, chopsticks=False, seed=7,
                  noodle_col='#c98a3c' if s['id'] in ('MiGoreng',) else ('#e86a2a' if s['id'] == 'FireChicken' else '#f3d58a'))
    # squash the noodle ellipse into a disc: the bowl picture is drawn flattened, so stretch vertically
    fd = fd.crop((0, int(fd.size[1] * .28), fd.size[0], int(fd.size[1] * .72))).resize(fd.size, Image.BICUBIC)
    im.paste(fd, (int(w * .5), 0))
    out = im.resize((W, H), Image.LANCZOS)
    out.save(os.path.join(OUT, s['id'] + 'Hot.png'))
    return out


ALL = {f.__name__: f for f in [Rolltan, Mivinka, MiGoreng, Shine, FireChicken, Chikin, Doshik, KappNoodle, BiggLanch, AkaiKitsune,
                                Leiz, Chitoz, Pringlz, Estrelya, Leningrad, Kiryushki, Korochki, Khrustam, Seeds, Oreshkin, Starpom, Kinozal, Yozhik]}

if __name__ == '__main__':
    only = sys.argv[1:]
    specs = {s['id']: s for s in NOODLES + SNACKS}
    for sid, fn in ALL.items():
        if only and sid not in only: continue
        fn(specs[sid]); print('label', sid)
    for s in NOODLES:
        if s['shape'] == 'brick' and (not only or s['id'] in only or 'Hot' in only):
            brewed(s); print('brewed', s['id'])

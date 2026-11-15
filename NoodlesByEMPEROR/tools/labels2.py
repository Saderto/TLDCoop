import math
"""Data-driven package art for every kind in catalog.NOODLES (labels.py holds the ten hand-made ones, kept as they are).
python3 labels2.py [ids] -> ../tex/<Id>.png (+ <Id>Hot.png for the ones served in a bowl, pot or plate)."""
import sys, os, math, zlib
from PIL import Image, ImageDraw
import art as A
from art import rgb, mix
import labels as L
from labels import K, SEAL
from catalog import NOODLES, SIZES, CUPS
from food_top import food

OUT = L.OUT
os.makedirs(OUT, exist_ok=True)


def lum(c):
    r, g, b = rgb(c)[:3]
    return (.299 * r + .587 * g + .114 * b) / 255


def on(c, light='#ffffff', dark='#1c1c1c'):
    return dark if lum(c) > .62 else light


def script(t):
    for ch in t:
        o = ord(ch)
        if 0x3040 <= o <= 0x30ff or 0x4e00 <= o <= 0x9fff or 0xac00 <= o <= 0xd7af or 0x3130 <= o <= 0x318f: return 'cjk'
    for ch in t:
        if 0x400 <= ord(ch) <= 0x4ff: return 'ru'
    return 'lat'


def fnt(t, lat='alfa', ru='inter_black'):
    sc = script(t)
    if sc == 'cjk': return 'cjk'
    if sc == 'ru': return ru
    # the display faces are Latin-only: anything with other marks goes to Inter
    return lat if all(ord(c) < 128 for c in t) else 'inter_black'


def fit(im, box, t, fill, lat='alfa', ru='inter_black', **kw):
    return A.fit_text(im, box, t, fnt(t, lat, ru), fill, **kw)


def vtext(im, cx, y0, y1, t, fill, maxw, **kw):
    n = len(t)
    size = int(min(maxw, (y1 - y0) / n * .92))
    f = A.font('cjk', size)
    step = (y1 - y0) / n
    for i, ch in enumerate(t):
        A.text(im, (cx, y0 + step * (i + .5)), ch, f, fill, **kw)


def seed(s, k=0):
    return zlib.crc32((s['id'] + str(k)).encode()) % 100000


def pic(im, cx, cy, r, s, bowl='#ffffff', rim=None, **kw):
    tops = tuple(t for t in s['top'] if t in ('egg', 'scallion', 'meat', 'shrimp', 'tofu', 'chili', 'mushroom', 'corn'))
    ncol = '#9a8160' if s.get('soba') else '#f3efe2' if s.get('rice') else '#f3d58a'
    if s.get('dry'): ncol = mix(ncol, s['broth'], .45)
    A.noodle_bowl(im, cx, cy, r, broth=s['broth'], bowl=bowl, bowl_rim=rim or s['ac'], toppings=tops, seed=seed(s) % 97, noodle_col=ncol, **kw)


def mascot(im, s, cx, cy, r):
    m = s.get('mascot')
    if m == 'chicken': A.chicken(im, cx, cy, r, body='#ffffff', comb='#e53935', angry=s['spicy'] >= 2)
    elif m == 'fox': A.fox(im, cx, cy, r)
    elif m == 'face': A.mustache_face(im, cx, cy, r)
    elif m == 'raccoon':
        A.ellipse(im, (cx - r, cy - r * .8, cx + r, cy + r * .8), '#8d8d8d', outline='#3a3a3a', width=5)
        for sx in (-1, 1):
            A.poly(im, [(cx + sx * r * .9, cy - r * .5), (cx + sx * r * .75, cy - r * 1.15), (cx + sx * r * .35, cy - r * .75)], '#5a5a5a', outline='#3a3a3a', width=4)
            A.ellipse(im, (cx + sx * r * .42 - r * .3, cy - r * .3, cx + sx * r * .42 + r * .3, cy + r * .15), '#2a2a2a')
            A.ellipse(im, (cx + sx * r * .42 - r * .1, cy - r * .16, cx + sx * r * .42 + r * .1, cy + r * .04), '#ffffff')
        A.ellipse(im, (cx - r * .35, cy + r * .1, cx + r * .35, cy + r * .7), '#f1f1f1')
        A.ellipse(im, (cx - r * .12, cy + r * .2, cx + r * .12, cy + r * .4), '#1a1a1a')
    elif m == 'pig':
        A.ellipse(im, (cx - r, cy - r * .85, cx + r, cy + r * .85), '#2b2b2b', outline='#000000', width=5)
        for sx in (-1, 1):
            A.poly(im, [(cx + sx * r * .85, cy - r * .5), (cx + sx * r * .8, cy - r * 1.1), (cx + sx * r * .4, cy - r * .75)], '#2b2b2b', outline='#000000', width=4)
            A.ellipse(im, (cx + sx * r * .4 - r * .09, cy - r * .3, cx + sx * r * .4 + r * .09, cy - r * .12), '#ffffff')
        A.ellipse(im, (cx - r * .38, cy + r * .02, cx + r * .38, cy + r * .5), '#f48fb1', outline='#ad1457', width=4)
        for sx in (-1, 1): A.ellipse(im, (cx + sx * r * .15 - r * .06, cy + r * .18, cx + sx * r * .15 + r * .06, cy + r * .34), '#ad1457')


def peppers(im, cx, cy, n, size):
    for i in range(n):
        x = cx + (i - (n - 1) / 2) * size * 1.15
        A.poly(im, [(x - size * .35, cy - size * .25), (x + size * .25, cy - size * .4), (x + size * .45, cy + size * .1), (x - size * .1, cy + size * .5), (x - size * .45, cy + size * .25)], '#e53935', outline='#7f0000', width=3)
        A.poly(im, [(x + size * .2, cy - size * .38), (x + size * .45, cy - size * .6), (x + size * .5, cy - size * .45)], '#2e7d32')


def wt(s):
    return f"{int(round(s['kg'] * 1000 / 5) * 5)} {'г' if s['lang'] == 'ru' else 'g'}"


# ------------------------------------------------------------------ pillow fronts

def t_band(s, im, w, h, sl):
    im.paste(A.vgrad(w, h, s['c1'], s['c2']))
    A.burst(im, w / 2, h * .62, 28, '#ffffff', alpha=38)
    ac = s['ac']
    A.poly(im, [(0, sl), (w, sl), (w, sl + h * .17), (0, sl + h * .22)], ac)
    fit(im, (w * .07, sl + h * .015, w * .93, sl + h * .16), s['nat'], on(ac), stroke=6, stroke_fill=mix(ac, '#000000', .55) if on(ac) == '#ffffff' else '#ffffff', skew=-.12)
    A.rrect(im, (w * .12, sl + h * .235, w * .88, sl + h * .3), 40, '#ffffff')
    fit(im, (w * .16, sl + h * .242, w * .84, sl + h * .293), s['sub'], ac if lum(ac) < .7 else '#333333', lat='bebas', ru='oswald')
    pic(im, w * .5, h * .62, min(w * .36, h * .21), s, rim=ac)
    if s.get('mascot'): mascot(im, s, w * .16, h * .86, w * .09)
    x0 = w * .3 if s.get('mascot') else w * .06
    A.rrect(im, (x0, h * .81, w * .95, h * .9), 26, ac)
    fit(im, (x0 + w * .03, h * .82, w * .92, h * .89), s['fl'], on(ac), lat='bebas', ru='oswald')
    if s['spicy']: peppers(im, w * .82, h * .46, s['spicy'], w * .07)


def t_arc(s, im, w, h, sl):
    im.paste(A.vgrad(w, h, s['c1'], s['c2']))
    ac = s['ac']
    A.ellipse(im, (-w * .3, h * .45, w * 1.3, h * 1.3), ac)
    fit(im, (w * .08, sl + h * .02, w * .92, sl + h * .2), s['nat'], '#ffffff', lat='pacifico', ru='ptserif_bi', stroke=6, stroke_fill=s['c2'], shadow='#000000', blur=8, rotate=5)
    fit(im, (w * .1, sl + h * .215, w * .9, sl + h * .255), s['sub'], mix(ac, '#ffffff', .5), lat='bebas', ru='oswald4')
    pic(im, w * .5, h * .58, min(w * .34, h * .2), s, bowl='#f6f6f6', rim=s['c1'], pattern='band')
    if s.get('mascot'): mascot(im, s, w * .82, h * .36, w * .1)
    A.rrect(im, (w * .08, h * .79, w * .92, h * .9), 20, '#ffffff')
    fit(im, (w * .11, h * .8, w * .89, h * .89), s['fl'], '#c0392b' if lum(s['c1']) > .3 else s['c1'], lat='bebas', ru='oswald')
    if s['spicy']: peppers(im, w * .5, h * .755, s['spicy'], w * .06)


def t_block(s, im, w, h, sl):
    im.paste(A.vgrad(w, h, s['c1'], s['c2']))
    ac = s['ac']; pl = s.get('plate', '#111111')
    A.rrect(im, (w * .09, sl + h * .025, w * .91, sl + h * .3), 30, pl)
    fit(im, (w * .14, sl + h * .035, w * .86, sl + h * .2), s['nat'], on(pl), stroke=3, stroke_fill='#000000')
    fit(im, (w * .14, sl + h * .21, w * .86, sl + h * .285), s['sub'] + (' ' + s['ven'].upper() if s['ven'] and s['ven'].upper() not in s['sub'] else ''), ac, lat='bebas')
    pic(im, w * .5, h * .61, min(w * .36, h * .2), s, bowl='#151515', rim='#3a3a3a')
    if s['spicy'] >= 2: A.flames(im, 0, h - sl, w, h * (.05 + .02 * s['spicy']), seed=seed(s) % 50, alpha=220)
    fit(im, (w * .06, h * .78, w * .94, h * .875), s['fl'], '#ffffff', lat='bebas', stroke=5, stroke_fill=mix(s['c2'], '#000000', .5))
    A.text(im, (w * .92, h * .905), wt(s), A.font('oswald', int(w * .05)), '#ffffff', anchor='rm')


def hot_chicken(im, cx, cy, r, fire=True):
    """The line's own mascot: a round white chicken with red cheeks, cross brows and a breath of fire."""
    if fire:
        for col, k in (('#e53935', 1.0), ('#ff9800', .78), ('#ffeb3b', .5)):
            A.poly(im, [(cx + r * .55, cy - r * .12), (cx + r * (.9 + .5 * k), cy - r * (.25 + .55 * k)), (cx + r * (1.0 + .5 * k), cy - r * .15), (cx + r * (1.2 + .9 * k), cy - r * (.05 + .25 * k)),
                        (cx + r * (1.0 + .55 * k), cy + r * .12), (cx + r * (.95 + .5 * k), cy + r * (.2 + .35 * k)), (cx + r * .55, cy + r * .12)], col)
    for sx in (-1, 1):   # feet
        A.poly(im, [(cx + sx * r * .28, cy + r * .9), (cx + sx * r * .1, cy + r * 1.18), (cx + sx * r * .3, cy + r * 1.08), (cx + sx * r * .5, cy + r * 1.18)], '#ff9800', outline='#222222', width=4)
    A.poly(im, [(cx - r * .7, cy + r * .1), (cx - r * 1.15, cy - r * .2), (cx - r * 1.0, cy + r * .2), (cx - r * 1.2, cy + r * .35), (cx - r * .75, cy + r * .6)], '#ffffff', outline='#222222', width=5)
    for k in range(3):   # comb
        A.ellipse(im, (cx - r * .36 + k * r * .26, cy - r * (1.22 if k == 1 else 1.1), cx - r * .02 + k * r * .26, cy - r * .7), '#e3121a', outline='#222222', width=4)
    A.ellipse(im, (cx - r * .82, cy - r * .9, cx + r * .82, cy + r * 1.0), '#ffffff', outline='#222222', width=6)
    for sx in (-1, 1):
        A.ellipse(im, (cx + sx * r * .42 - r * .2, cy + r * .0, cx + sx * r * .42 + r * .2, cy + r * .36), '#ff5a3c')          # cheeks
        A.ellipse(im, (cx + sx * r * .34 - r * .1, cy - r * .36, cx + sx * r * .34 + r * .1, cy - r * .12), '#1a1a1a')
        ImageDraw.Draw(im).line([(cx + sx * r * .1, cy - r * .36), (cx + sx * r * .56, cy - r * .56)], fill=(26, 26, 26), width=max(5, int(r * .09)))
    A.poly(im, [(cx - r * .14, cy - r * .08), (cx + r * .14, cy - r * .08), (cx + r * .0, cy + r * .2)], '#ffb300', outline='#222222', width=4)
    A.ellipse(im, (cx - r * .2, cy + r * .48, cx + r * .2, cy + r * .82), '#f2f2f2', outline='#bdbdbd', width=3)


def wordmark(im, box, t, rotate=3):
    x0, y0, x1, y1 = box
    f = A.fit_font(t, 'inter_black', x1 - x0, y1 - y0)
    A.text(im, ((x0 + x1) / 2 + 8, (y0 + y1) / 2 + 10), t, f, '#1a1a1a', stroke=16, stroke_fill='#1a1a1a', rotate=rotate, alpha=150)
    A.text(im, ((x0 + x1) / 2, (y0 + y1) / 2), t, f, '#e3121a', stroke=14, stroke_fill='#ffffff', rotate=rotate)


def t_fire(s, im, w, h, sl):
    im.paste(A.vgrad(w, h, s['c1'], s['c2']))
    ac = s['ac']; ink = on(s['c1'])
    A.burst(im, w * .2, h * .45, 18, '#ffffff', alpha=22)
    if s['id'] in ('BuldakCream',):
        for k in range(8): A.rrect(im, (k * w / 8, 0, (k + .5) * w / 8, h), 0, '#ffffff', alpha=110)
    A.text(im, (w * .06, sl + h * .06), s['sub'], A.font('pacifico', int(w * .055)), ink, anchor='lm')
    tag = s['ven'] if s['ven'] != 'Original' else ''
    if tag:
        f = A.fit_font(tag, 'pacifico', w * .52, h * .1)
        A.text(im, (w * .68, sl + h * .075), tag, f, ac, stroke=7, stroke_fill='#1a1a1a' if lum(ac) > .45 else '#ffffff', rotate=4)
    wordmark(im, (w * .33, sl + h * .14, w * .96, sl + h * .33), s['en'].split(' ')[0])
    f = A.fit_font(s['nat'], 'cjk', w * .6, h * .14)
    A.text(im, (w * .65, sl + h * .42), s['nat'], f, '#e3121a', stroke=9, stroke_fill='#ffffff')
    fit(im, (w * .36, sl + h * .5, w * .95, sl + h * .545), 'HOT CHICKEN FLAVOR RAMEN', ink, lat='bebas')
    hot_chicken(im, w * .19, sl + h * .33, w * .13)
    A.flames(im, w * .3, h * .93, w * .7, h * .2, seed=seed(s) % 40 + 3, n=7, alpha=215)
    pic(im, w * .64, h * .74, min(w * .27, h * .19), s, bowl='#151515', rim='#3a3a3a', chopsticks=False)
    peppers(im, w * .17, h * .74, min(s['spicy'], 2), w * .08)
    if s['spicy'] > 2: peppers(im, w * .17, h * .82, s['spicy'] - 2, w * .08)
    if s.get('nat2'):
        A.rrect(im, (w * .04, h * .865, w * .3, h * .93), 18, '#e3121a', outline='#ffffff', width=5)
        fit(im, (w * .06, h * .872, w * .28, h * .923), s['nat2'], '#ffffff')


def t_jp(s, im, w, h, sl):
    im.paste(A.vgrad(w, h, s['c1'], s['c2']))
    ac = s['ac']
    for k in range(9):
        A.ellipse(im, (w * .05 + k * w * .11, sl + h * .012, w * .13 + k * w * .11, sl + h * .012 + w * .08), '#ffffff', alpha=70)
    A.rrect(im, (w * .08, sl + h * .09, w * .92, sl + h * .31), 24, '#ffffff')
    fit(im, (w * .12, sl + h * .1, w * .88, sl + h * .2), s['nat'], '#e2001a' if lum(ac) > .6 else ac)
    fit(im, (w * .12, sl + h * .21, w * .88, sl + h * .295), s['sub'] + (' · ' + s['ven'] if s['ven'] else ''), '#3a2a1a', lat='playfair_l')
    pic(im, w * .5, h * .63, min(w * .34, h * .2), s, rim=ac if lum(ac) < .7 else '#e2001a', pattern='band')
    fit(im, (w * .08, h * .8, w * .92, h * .875), s['fl'], '#ffffff', lat='bebas', stroke=4, stroke_fill=mix(s['c2'], '#000000', .55))
    A.text(im, (w * .5, h * .9), wt(s) + ' · 3 MIN', A.font('oswald4', int(w * .042)), on(s['c2']))
    if s['spicy']: peppers(im, w * .84, h * .44, s['spicy'], w * .06)


def t_tate(s, im, w, h, sl):
    im.paste(A.vgrad(w, h, s['c1'], s['c2']))
    ac = s['ac']; dark = s['dark']; ink = '#ffffff' if dark else '#2a1a12'
    A.rrect(im, (w * .7, sl + h * .02, w * .94, h - sl - h * .02), 14, ac)
    vtext(im, w * .82, sl + h * .035, h - sl - h * .035, s['nat'], on(ac), w * .2)
    r = min(w * .3, h * .2)
    cy = h * .44
    A.ellipse(im, (w * .36 - r * 1.12, cy - r * 1.12, w * .36 + r * 1.12, cy + r * 1.12), ac if not dark else mix(ac, '#000000', .3))
    pic(im, w * .36, cy + r * .1, r * .82, s, bowl='#ffffff' if not dark else '#1a1a1a', rim=on(ac, '#ffd54f', '#b71c1c'), chopsticks=False)
    fit(im, (w * .06, sl + h * .02, w * .66, sl + h * .08), s['sub'], ink, lat='bebas')
    # the flavour, vertical, in a smaller column under the picture
    fl = s['fl']
    if script(fl) == 'cjk' and h > w * 1.6:
        vtext(im, w * .36, cy + r * 1.25, h - sl - h * .09, fl.replace(' ', ''), ink, w * .15)
    else:
        fit(im, (w * .05, h * .72, w * .67, h * .82), fl, ink)
    fit(im, (w * .06, h - sl - h * .08, w * .66, h - sl - h * .025), (s['ven'] or s['en']).upper() + ' · ' + wt(s), ac if lum(ac) < .6 or dark else '#333333', lat='bebas')


def t_split(s, im, w, h, sl):
    im.paste(Image.new('RGB', (w, h), (255, 255, 255)))
    ac = s['ac']
    A.poly(im, [(0, 0), (w, 0), (w, h * .33), (0, h * .44)], s['c1'])
    A.poly(im, [(0, h * .44), (w, h * .33), (w, h * .36), (0, h * .47)], ac)
    fit(im, (w * .1, sl + h * .01, w * .9, sl + h * .15), s['nat'], '#ffffff', lat='pacifico', stroke=6, stroke_fill=mix(s['c2'], '#000000', .2), rotate=4)
    line2 = (s['ven'] or s['sub']).upper()
    fit(im, (w * .1, sl + h * .17, w * .9, sl + h * .26), line2, '#ffffff', lat='inter_black', stroke=5, stroke_fill=s['c2'])
    pic(im, w * .48, h * .66, min(w * .37, h * .21), s, bowl='#ffffff', rim='#dddddd')
    A.rrect(im, (w * .05, h * .84, w * .72, h * .915), 16, '#d32f2f' if lum(s['c1']) > .25 and s['c1'] != '#d32f2f' else s['c2'])
    fit(im, (w * .08, h * .848, w * .69, h * .907), s['fl'], '#ffffff', lat='bebas')
    A.badge(im, w * .85, h * .875, w * .085, ac, ring=on(ac, '#ffffff', '#d32f2f'))
    fit(im, (w * .79, h * .855, w * .91, h * .895), wt(s), on(ac, '#ffffff', '#d32f2f'), lat='bebas')
    if s['spicy']: peppers(im, w * .8, h * .52, s['spicy'], w * .06)


def t_stripe(s, im, w, h, sl):
    ac = s['ac']
    im.paste(Image.new('RGB', (w, h), rgb('#fff8e1')))
    A.rrect(im, (0, 0, w, h * .36), 0, s['c1'])
    A.rrect(im, (0, h * .36, w, h * .375), 0, ac if lum(ac) < .8 else s['c2'])
    A.rrect(im, (0, h * .8, w, h), 0, s['c2'])
    A.rrect(im, (0, h * .785, w, h * .8), 0, ac if lum(ac) < .8 else s['c1'])
    x0 = w * .3 if s.get('mascot') else w * .07
    if s.get('mascot'): mascot(im, s, w * .16, sl + h * .13, w * .1)
    fit(im, (x0, sl + h * .02, w * .93, sl + h * .2), s['nat'], on(s['c1']), lat='alfa', ru='ptserif_bi', stroke=5, stroke_fill=mix(s['c1'], '#000000', .5) if on(s['c1']) == '#ffffff' else '#ffffff')
    fit(im, (w * .1, sl + h * .215, w * .9, sl + h * .27), s['sub'], mix(on(s['c1']), s['c1'], .15), lat='bebas', ru='oswald')
    pic(im, w * .5, h * .58, min(w * .34, h * .18), s, rim=s['c1'])
    fit(im, (w * .07, h * .815, w * .93, h * .9), s['fl'], on(s['c2']), lat='bebas', ru='oswald')
    if s['spicy']: peppers(im, w * .84, h * .43, s['spicy'], w * .055)


def t_kraft(s, im, w, h, sl):
    im.paste(A.vgrad(w, h, s['c1'], s['c2']))
    ac = s['ac']; dark = s['dark']; ink = '#f5efe0' if dark else '#2a2a2a'
    A.rrect(im, (w * .06, sl + h * .02, w * .94, h - sl - h * .02), 10, None, outline=ac, width=6)
    A.rrect(im, (w * .08, sl + h * .033, w * .92, h - sl - h * .033), 6, None, outline=ac, width=2)
    fit(im, (w * .14, sl + h * .06, w * .86, sl + h * .2), s['nat'], ac, lat='playfair_l', ru='ptserif_b')
    ImageDraw.Draw(im).line([(w * .25, sl + h * .225), (w * .75, sl + h * .225)], fill=rgb(ac), width=4)
    fit(im, (w * .14, sl + h * .24, w * .86, sl + h * .29), s['sub'], ink, lat='bebas')
    pic(im, w * .5, h * .6, min(w * .3, h * .17), s, bowl='#fafafa' if not dark else '#161616', rim=ac, chopsticks=False)
    fit(im, (w * .14, h * .78, w * .86, h * .85), s['fl'], ink, lat='bebas')
    A.text(im, (w * .5, h * .885), wt(s), A.font('oswald4', int(w * .04)), ac)
    if s['spicy']: peppers(im, w * .5, h * .74, s['spicy'], w * .05)


PILLOW = dict(band=t_band, arc=t_arc, block=t_block, fire=t_fire, jp=t_jp, tate=t_tate, split=t_split, stripe=t_stripe, kraft=t_kraft)
ING = {
    'ru': ('лапша (мука пшеничная, масло пальмовое, соль), приправа ({f}), сушёные овощи.', ('Приготовление', 'Залейте {w} мл кипятка, накройте и подождите 3 минуты.'), 'Изготовитель: «{b}».'),
    'en': ('Noodles (wheat flour, palm oil, salt), seasoning ({f}), dried vegetables.', ('How to cook', 'Add to {w} ml of boiling water. Cook for 3 minutes.'), '{b} Foods Co.'),
    'ja': ('Noodles (wheat flour, oil, salt), soup ({f}), dried garnish. めん、スープ、かやく', ('How to cook · 作り方', 'Boil in {w} ml of water for 3 minutes. 熱湯3分'), '{b} Foods, Japan.'),
    'ko': ('Noodles (wheat flour, potato starch, palm oil), soup base ({f}), flakes. 면, 스프, 후레이크', ('How to cook · 조리방법', 'Boil in {w} ml of water for 4 minutes. 끓는 물 4분'), '{b} Foods, Korea.'),
    'zh': ('Noodles (wheat flour, palm oil, salt), sauce ({f}), dried vegetables. 麵餅、調味包', ('How to cook · 食用方法', 'Add {w} ml of boiling water, cover for 3 minutes. 沸水沖泡3分鐘'), '{b} Foods.'),
}


def pillow(s):
    im, fw, fh = L.canvas(s['shape'], s['c1'])
    w, h = im.size
    sl = int(h * SEAL)
    PILLOW.get(s['tpl'], t_band)(s, im, w, h, sl)
    L.seals(im, mix(s['c2'], '#000000', .15))
    ing, how, maker = ING[s['lang']]
    ru = s['lang'] == 'ru'
    bg = mix(s['c1'], '#ffffff', .82) if lum(s['c1']) > .2 else '#f3efe6'
    ink = mix(s['c2'], '#000000', .55) if lum(s['c2']) < .7 else '#2a2a2a'
    acc = s['c2'] if lum(s['c2']) < .55 else (s['ac'] if lum(s['ac']) < .55 else '#333333')
    b = L.back(s, bg, ink, acc, wt(s), ing.format(f=s['ruf'] if ru else s['enf']), how=(how[0], how[1].format(w=int(s['water'] * 1000))), maker=maker.format(b=s['ru' if ru else 'en'].split('«')[-1].split('»')[0] if ru else s['en']))
    return L.finish(im, b, s['id'])


# ------------------------------------------------------------------ cups, bowls, buckets

def cup(s):
    W = H = 1024
    im = Image.new('RGB', (W * K, H * K), rgb(s['c1']))
    w, h = im.size
    sh = int(h * .7)
    side = A.vgrad(w, sh, s['c1'], s['c2'])
    tpl = s['tpl']; ac = s['ac']; dark = lum(s['c1']) < .5
    ink = '#ffffff' if dark else '#222222'
    reps = 3   # a print every 120 degrees: with two the words ran round past what one can see
    # the wall is painted at its true proportions (way round : height) and squeezed into the sheet afterwards,
    # else the print came out stretched sideways on the wide bowls (0.3.0 test)
    from catalog import CUPS
    R0_, R1_, H_ = CUPS[s['shape']]
    w_sheet = w
    w = max(w, int(sh * math.pi * (R0_ + R1_) / H_))
    side = A.vgrad(w, sh, s['c1'], s['c2'])
    rw = w / reps
    band = s.get('band')
    if tpl == 'cupA' and band:
        A.rrect(side, (0, sh * .62, w, sh * .74), 0, band)
        A.rrect(side, (0, sh * .76, w, sh * .79), 0, mix(band, '#00a0c6', .7) if s['brand'] == 'Kapp' else ac)
    if tpl == 'cupK':
        A.rrect(side, (0, sh * .06, w, sh * .075), 0, ac); A.rrect(side, (0, sh * .925, w, sh * .94), 0, ac)
    for k in range(reps):
        x = rw * (k + .5)
        lat_main = s['sub'].split(' · ')[0]
        if tpl == 'cupA':
            words = lat_main.split(' ')
            if len(words) >= 2 and s['brand'] == 'Kapp':
                fit(side, (x - rw * .4, sh * .12, x + rw * .4, sh * .36), words[0], ac); fit(side, (x - rw * .4, sh * .37, x + rw * .4, sh * .58), words[1], ac)
            else:
                fit(side, (x - rw * .42, sh * .1, x + rw * .42, sh * .34), s['nat'], ac, stroke=4, stroke_fill=on(ac, '#ffffff', '#000000') if dark else '#ffffff')
                fit(side, (x - rw * .42, sh * .37, x + rw * .42, sh * .56), lat_main, ink, lat='alfa')
            if band: fit(side, (x - rw * .42, sh * .635, x + rw * .42, sh * .725), s['fl'], on(band), lat='bebas')
            fit(side, (x - rw * .42, sh * .83, x + rw * .42, sh * .93), (s['nat'] if s['brand'] == 'Kapp' else s['ven'].upper()) + ' · ' + wt(s), ink if not band or s['brand'] == 'Kapp' else ac, lat='bebas')
        elif tpl == 'cupC':
            r = min(rw * .34, sh * .22)
            A.ellipse(side, (x - r, sh * .07, x + r, sh * .07 + 2 * r), '#ffffff')
            if s.get('mascot'): mascot(side, s, x, sh * .07 + r, r * .66)
            else: pic(side, x, sh * .07 + r, r * .82, s)
            fit(side, (x - rw * .44, sh * .55, x + rw * .44, sh * .7), s['fl'].split(' · ')[0], '#ffffff')
            fit(side, (x - rw * .44, sh * .72, x + rw * .44, sh * .83), (s['ven'] or lat_main).upper(), mix(ac, '#ffe0a0', .6) if lum(ac) > .8 else ac, lat='bebas')
            fit(side, (x - rw * .4, sh * .86, x + rw * .4, sh * .94), s['sub'] + ' · ' + wt(s), '#ffffff', lat='bebas')
        elif tpl == 'cupK':
            fit(side, (x - rw * .4, sh * .12, x + rw * .4, sh * .42), s['nat'], ac)
            fit(side, (x - rw * .26, sh * .45, x + rw * .26, sh * .68), s['fl'], '#ffffff')
            fit(side, (x - rw * .42, sh * .72, x + rw * .42, sh * .8), s['sub'], ac, lat='bebas')
            fit(side, (x - rw * .42, sh * .82, x + rw * .42, sh * .89), (s['ven'].upper() + ' · ' + wt(s)), '#e8e0d0', lat='bebas')
        elif tpl == 'fire':
            tag = s['ven'] if s['ven'] != 'Original' else ''
            if tag:
                f = A.fit_font(tag, 'pacifico', rw * .5, sh * .14)
                A.text(side, (x + rw * .12, sh * .1), tag, f, ac, stroke=7, stroke_fill='#1a1a1a' if lum(ac) > .45 else '#ffffff', rotate=4)
            A.text(side, (x - rw * .44, sh * .08), s['sub'], A.font('pacifico', int(rw * .06)), ink, anchor='lm')
            wordmark(side, (x - rw * .42, sh * .2, x + rw * .44, sh * .5), s['en'].split(' ')[0])
            f = A.fit_font(s['nat'], 'cjk', rw * .5, sh * .14)
            A.text(side, (x + rw * .16, sh * .61), s['nat'], f, '#e3121a', stroke=8, stroke_fill='#ffffff')
            hot_chicken(side, x - rw * .28, sh * .72, rw * .1)
            fit(side, (x - rw * .1, sh * .72, x + rw * .44, sh * .79), 'HOT CHICKEN FLAVOR RAMEN', ink, lat='bebas')
            peppers(side, x + rw * .17, sh * .88, s['spicy'], rw * .075)
        else:   # plates (cupB) and every pack brand's cup
            pl = ac
            A.rrect(side, (x - rw * .4, sh * .08, x + rw * .4, sh * .5), 40, pl)
            nat = s['nat']
            parts = nat.split(' ') if (' ' in nat and script(nat) != 'cjk') else [nat]
            if len(parts) == 2:
                fit(side, (x - rw * .34, sh * .11, x + rw * .34, sh * .28), parts[0], on(pl, '#ffffff', s['c2'])); fit(side, (x - rw * .34, sh * .3, x + rw * .34, sh * .47), parts[1], on(pl, '#ffffff', '#e53935'))
            else:
                fit(side, (x - rw * .34, sh * .11, x + rw * .34, sh * .34), nat, on(pl, '#ffffff', s['c2']))
                fit(side, (x - rw * .34, sh * .36, x + rw * .34, sh * .46), lat_main if lat_main != nat else (s['ven'] or s['en']).upper(), on(pl, '#ffffff', '#333333'), lat='bebas', ru='oswald')
            pic(side, x, sh * .7, min(rw * .2, sh * .14), s, rim=pl)
            fit(side, (x - rw * .45, sh * .88, x + rw * .45, sh * .96), s['fl'] + ' · ' + wt(s), '#ffffff' if lum(s['c2']) < .6 else '#222222', lat='bebas', ru='oswald')
    from PIL import ImageChops
    side = ImageChops.offset(side, int(rw / 2), 0)   # the icon camera looks at u=0: put a label centre there, not the seam
    w = w_sheet
    side = side.resize((w, sh), Image.LANCZOS)
    im.paste(side, (0, 0))
    # lid
    S = int(w * .3)
    lidc = s['c1'] if tpl in ('cupK', 'fire') or s['brand'] in ('Marutyan',) else (ac if tpl not in ('cupA',) else '#ffffff')
    if s['brand'] == 'Marutyan' and s['id'] != 'AkaiKitsune': lidc = '#ffffff'
    if tpl == 'cupA' and lum(s['c1']) < .5: lidc = s['c1']
    lid = A.radial(S // 4, S // 4, mix(lidc, '#ffffff', .25), mix(lidc, '#000000', .12)).resize((S, S))
    li = on(lidc, '#ffffff', s['c2'] if lum(s['c2']) < .5 else '#333333')
    if s.get('mascot') and tpl != 'cupA':
        if tpl == 'fire': hot_chicken(lid, S * .3, S * .52, S * .13, fire=False); wordmark(lid, (S * .44, S * .3, S * .9, S * .5), s['en'].split(' ')[0])
        else: mascot(lid, s, S * .5, S * .4, S * .18)
        fit(lid, (S * .46, S * .56, S * .86, S * .72) if tpl == 'fire' else (S * .22, S * .66, S * .78, S * .84), s['nat'] if tpl == 'fire' else s['fl'].split(' · ')[0], '#e3121a' if tpl == 'fire' else ('#d7261e' if lidc == '#ffffff' else li), **(dict(stroke=5, stroke_fill='#ffffff') if tpl == 'fire' else {}))
    else:
        fit(lid, (S * .17, S * .26, S * .83, S * .54), s['nat'], ac if lidc in ('#ffffff',) or tpl == 'cupK' or (tpl == 'cupA' and lidc == s['c1']) else li)
        fit(lid, (S * .24, S * .58, S * .76, S * .72), (s['ven'] or s['sub'].split(' · ')[0]).upper(), li if tpl != 'cupK' else '#e8e0d0', lat='bebas')
    m = Image.new('L', (S, S), 0); ImageDraw.Draw(m).ellipse((0, 0, S, S), fill=255)
    im.paste(lid, (int(w * .15 - S / 2), int(h * .85 - S / 2)), m)
    out = im.resize((W, H), Image.LANCZOS)
    S1 = int(W * .3); fd = food(s, S1)
    m = Image.new('L', (S1, S1), 0); ImageDraw.Draw(m).ellipse((0, 0, S1, S1), fill=255)
    out.paste(fd, (int(W * .5 - S1 / 2), int(H * .85 - S1 / 2)), m)
    out.save(os.path.join(OUT, s['id'] + '.png'))


# ------------------------------------------------------------------ trays and boxes

def tray(s):
    W = H = 1024
    im = Image.new('RGB', (W * K, H * K), rgb(s['c2']))
    w, h = im.size
    lx, ly = int(w * (.62 if s['shape'] == 'tray' else .55)), int(h * .55)
    ac = s['ac']; dark = lum(s['c1']) < .6
    lid = A.vgrad(lx, ly, s['c1'], s['c2'])
    A.burst(lid, lx * .5, ly * .55, 24, '#ffffff' if dark else ac, alpha=25)
    if s['tpl'] == 'lid' and s['brand'] in ('Ichiran',):
        A.rrect(lid, (lx * .05, ly * .05, lx * .95, ly * .95), 16, None, outline=ac, width=8)
    ink = '#ffffff' if dark else ac
    fit(lid, (lx * .1, ly * .06, lx * .9, ly * .3), s['nat'], ink, stroke=10 if dark else 0, stroke_fill=mix(s['c2'], '#000000', .6))
    fit(lid, (lx * .12, ly * .31, lx * .88, ly * .4), s['sub'], ac if dark else '#333333', lat='bebas', ru='oswald')
    pic(lid, lx * .5, ly * .62, min(lx * .24, ly * .2), s, rim=ac)
    A.rrect(lid, (lx * .1, ly * .84, lx * .9, ly * .95), 30, ac)
    fit(lid, (lx * .13, ly * .85, lx * .87, ly * .94), s['fl'], on(ac, '#ffffff', mix(s['c2'], '#000000', .3)), ru='oswald')
    if s['spicy']: peppers(lid, lx * .84, ly * .5, min(3, s['spicy']), lx * .06)
    im.paste(lid, (0, 0))
    sy = int(h * .58)
    from catalog import TRAYS
    Wm, Dm, Hm = TRAYS[s['shape']]
    P = 2 * (Wm + Dm)
    sh2 = h - sy
    w2 = int(sh2 * P * .97 / Hm)          # true proportions of the wall, squeezed into the sheet below
    side = A.vgrad(w2, sh2, s['c1'], s['c2'])
    A.rrect(side, (0, sh2 * .78, w2, sh2), 0, ac)
    ink = '#ffffff' if lum(s['c1']) < .6 else (s['c2'] if lum(s['c2']) < .45 else '#222222')
    fl = (s['vru'] or s['fl']) if s['lang'] == 'ru' else (s['ven'] or s['fl']).upper()
    for cen, face in [(Wm / 2, Wm), (Wm + Dm / 2, Dm), (1.5 * Wm + Dm, Wm), (2 * Wm + 1.5 * Dm, Dm)]:
        x = (1 - cen / P) * w2; hw = face / P * w2 * .4
        fit(side, (x - hw, sh2 * .1, x + hw, sh2 * .52), s['nat'], ink)
        fit(side, (x - hw, sh2 * .56, x + hw, sh2 * .72), s['sub'].split(' · ')[0], ink, lat='bebas', ru='oswald')
        fit(side, (x - hw, sh2 * .81, x + hw, sh2 * .97), fl, on(ac, '#ffffff', '#222222'), lat='bebas', ru='oswald')
    side = side.resize((w, sh2), Image.LANCZOS)
    im.paste(side, (0, sy))
    out = im.resize((W, H), Image.LANCZOS)
    out.save(os.path.join(OUT, s['id'] + '.png'))


# ------------------------------------------------------------------ brewed, in a bowl / donburi / pot / plate

def hot(s):
    W = 1024
    im = Image.new('RGB', (W, W), (240, 236, 228))
    q = W // 2
    v = s['vessel']
    band = s['ac'] if .12 < lum(s['ac']) < .85 else (s['c1'] if .12 < lum(s['c1']) < .85 else s['c2'])
    if v == 'Pot':
        wall = A.vgrad(q, q, '#f6d25a', '#c9961e')
        d = ImageDraw.Draw(wall, 'RGBA')
        for y, a in ((q * .12, 110), (q * .2, 50), (q * .62, 40)):
            d.rectangle((0, y, q, y + q * .035), fill=(255, 248, 200, a))
        d.rectangle((0, q * .9, q, q), fill=(120, 80, 10, 70))
    elif v == 'Donburi':
        wall = A.vgrad(q, q, '#262626', '#0c0c0c')
        d = ImageDraw.Draw(wall)
        d.rectangle((0, q * .1, q, q * .2), fill=rgb('#b71c1c')); d.rectangle((0, q * .22, q, q * .235), fill=rgb('#d4af37'))
        for k in range(12):   # a wave pattern round the wall
            x = k * q / 12
            d.arc((x, q * .45, x + q / 12 * 1.4, q * .75), 180, 360, fill=rgb('#d4af37'), width=3)
    elif v == 'Plate':
        wall = A.vgrad(q, q, '#fbf9f4', '#e4dfd3')
        ImageDraw.Draw(wall).rectangle((0, q * .06, q, q * .11), fill=rgb(band))
    else:
        wall = A.vgrad(q, q, '#f7f3ea', '#d9d2c4')
        ImageDraw.Draw(wall).rectangle((0, q * .2, q, q * .28), fill=rgb(band))
    im.paste(wall, (0, q))
    im.paste(food(s, q), (q, 0))
    im.save(os.path.join(OUT, s['id'] + 'Hot.png'))


def make(s):
    if not s.get('hand') or s['kind'] != 'pillow':   # the hand-made cups and tray were redone by the templates in 0.4.0 (stretched print)
        {'pillow': pillow, 'cup': cup, 'tray': tray}[s['kind']](s)
    if s['vessel']: hot(s)


if __name__ == '__main__':
    only = sys.argv[1:]
    for s in NOODLES:
        if only and s['id'] not in only and s['brand'] not in only: continue
        make(s); print('label', s['id'], flush=True)

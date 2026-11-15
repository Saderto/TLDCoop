"""Flat, graphic pack art (0.2): every brand one layout, three flavours by palette. python3 labels2.py [brand ids] -> ../tex/<Id>.png"""
import sys, os, math
from PIL import Image, ImageDraw
import art as A
from art import rgb, mix
from catalog import BRANDS

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', 'tex')
K = 3
FW = 512


def spaced(im, xy, txt, f, fill, sp=.12, anchor='mm'):
    """Letter-spaced small caps line, centred on xy."""
    if isinstance(f, tuple): f = A.font(*f)
    d = ImageDraw.Draw(im)
    gap = f.size * sp
    ws = [d.textlength(c, font=f) for c in txt]
    total = sum(ws) + gap * (len(txt) - 1)
    x = xy[0] - total / 2 if anchor == 'mm' else xy[0]
    for c, w in zip(txt, ws):
        d.text((x, xy[1]), c, font=f, fill=rgb(fill), anchor='lm')
        x += w + gap
    return total


def fit_spaced(im, cx, y, txt, name, fill, maxw, size, sp=.12):
    while size > 10:
        f = A.font(name, size)
        d = ImageDraw.Draw(im)
        total = sum(d.textlength(c, font=f) for c in txt) + f.size * sp * (len(txt) - 1)
        if total <= maxw: break
        size = int(size * .94)
    spaced(im, (cx, y), txt, f, fill, sp)


def rule(im, x0, x1, y, col, w=4):
    ImageDraw.Draw(im).line([(x0, y), (x1, y)], fill=rgb(col), width=w)


def seal_bands(im, share, col):
    if share <= 0: return 0
    w, h = im.size
    s = int(h * share)
    d = ImageDraw.Draw(im)
    for y0 in (0, h - s):
        d.rectangle((0, y0, w, y0 + s), fill=rgb(col))
        for y in range(y0 + 4, y0 + s - 2, 9):
            d.line([(0, y), (w, y)], fill=mix(col, '#000000', .18), width=3)
    return s


def side_bands(im, share, col):
    w, h = im.size
    s = int(w * share)
    d = ImageDraw.Draw(im)
    for x0 in (0, w - s):
        d.rectangle((x0, 0, x0 + s, h), fill=rgb(col))
        for x in range(x0 + 4, x0 + s - 2, 9):
            d.line([(x, 0), (x, h)], fill=mix(col, '#000000', .18), width=3)
    return s


def chip(im, cx, cy, rx, ry, rot, fill, edge):
    return X.piece(im, CUR['food'], cx, cy, rx, rot)


def chip_flat(im, cx, cy, rx, ry, rot, fill, edge):
    lay = Image.new('RGBA', (int(rx * 2.6), int(rx * 2.6)), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay); c = lay.size[0] / 2
    d.ellipse((c - rx, c - ry, c + rx, c + ry), fill=rgb(edge) + (255,))
    d.ellipse((c - rx * .9, c - ry * .9 - ry * .06, c + rx * .9, c + ry * .86), fill=rgb(fill) + (255,))
    d.arc((c - rx * .62, c - ry * .62, c + rx * .62, c + ry * .62), 200, 300, fill=mix(fill, '#ffffff', .55) + (255,), width=max(3, int(rx * .06)))
    lay = lay.rotate(rot, Image.BICUBIC)
    im.paste(lay, (int(cx - c), int(cy - c)), lay)


# ------------------------------------------------------------------ back side (shared, tidy)

LIDROT = int(os.environ.get('LIDROT', 90))


def back_panel(b, v, w, h, top_pad=0, bot_pad=0, left_pad=0):
    bg, fg, acc, ink = v[4]
    light = mix(bg, '#ffffff', .9) if sum(rgb(bg)) < 560 else rgb(bg)
    im = Image.new('RGB', (w, h), light)
    ru = b['lang'] == 'ru'
    x0, x1 = left_pad + int(w * .08), w - left_pad - int(w * .08)
    y = top_pad + int(h * .05)
    d = ImageDraw.Draw(im)
    band = int(h * .09)
    d.rectangle((x0, y, x1, y + band), fill=rgb(bg))
    name = b['ru' if ru else 'en'].upper()
    f = A.fit_font(name, 'oswald', (x1 - x0) * .9, band * .6)
    d.text(((x0 + x1) / 2, y + band / 2), name, font=f, fill=rgb(fg), anchor='mm')
    y += band + int(h * .03)
    fb = A.font('ptsans_b', int(w * .042)); fr = A.font('ptsans', int(w * .036))
    d.text((x0, y), (v[1] if ru else v[2]).capitalize(), font=fb, fill=rgb(ink), anchor='lt'); y += int(fb.size * 1.7)
    d.text((x0, y), 'Состав:' if ru else 'Ingredients:', font=fb, fill=rgb(ink), anchor='lt'); y += int(fb.size * 1.35)
    y = A.lines(im, x0, y, [b['ingr']], fr, ink, maxw=x1 - x0) + int(h * .02)
    kc = b['kcal'] / b['kg'] / 10
    rows = [('Белки' if ru else 'Protein', f'{kc * .08 / 4:.1f}'), ('Жиры' if ru else 'Fat', f'{kc * .5 / 9:.1f}'), ('Углеводы' if ru else 'Carbohydrate', f'{kc * .42 / 4:.1f}'),
            ('Энергия, ккал' if ru else 'Energy, kcal', f'{kc:.0f}')]
    rh = int(fr.size * 1.55)
    if y + rh * 5 + h * .3 < h - bot_pad:
        d.text((x0, y), 'В 100 г:' if ru else 'Per 100 g:', font=fb, fill=rgb(ink), anchor='lt'); y += int(fb.size * 1.5)
        for n, val in rows:
            d.line([(x0, y), (x1, y)], fill=mix(ink, light, .6), width=2)
            d.text((x0, y + rh / 2), n, font=fr, fill=rgb(ink), anchor='lm')
            d.text((x1, y + rh / 2), val, font=fb, fill=rgb(ink), anchor='rm')
            y += rh
        d.line([(x0, y), (x1, y)], fill=mix(ink, light, .6), width=2)
        y += int(h * .02)
    # bottom: net weight and barcode
    bh = int(h * .11); by = h - bot_pad - bh - int(h * .07)
    keep = im.copy()
    if A.lines(im, x0, y, [b['maker']], fr, mix(ink, light, .3), maxw=x1 - x0) > by - int(h * .015): im.paste(keep, (0, 0))   # no room: the maker's line is left out
    d = ImageDraw.Draw(im)
    bw = int((x1 - x0) * .46)
    d.rectangle((x1 - bw, by, x1, by + bh), fill=(255, 255, 255))
    import random
    rnd = random.Random(b['id'] + v[0]); cx = x1 - bw + 10
    while cx < x1 - 12:
        t = rnd.choice([2, 2, 4, 6]) * K // 2
        if rnd.random() < .55: d.rectangle((cx, by + 8, cx + t - 1, by + bh - 8), fill=(20, 20, 20))
        cx += t
    if bh > w * .13:
        d.text((x0, by + bh * .3), 'Масса нетто' if ru else 'Net weight', font=fr, fill=rgb(ink), anchor='lm')
        d.text((x0, by + bh * .75), b['net'], font=A.font('oswald', int(w * .075)), fill=rgb(ink), anchor='lm')
    else:
        d.text((x0, by + bh * .5), b['net'], font=A.font('oswald', int(w * .07)), fill=rgb(ink), anchor='lm')
    return im


# ------------------------------------------------------------------ fronts: one function per brand, drawn on (w,h)

def f_Leiz(im, b, v):
    bg, fg, acc, ink = v[4]; w, h = im.size
    im.paste(rgb(bg), (0, 0, w, h))
    cy = h * .36; r = w * .36
    A.ellipse(im, (w / 2 - r, cy - r, w / 2 + r, cy + r), acc)
    A.ellipse(im, (w / 2 - r * .86, cy - r * .86, w / 2 + r * .86, cy + r * .86), mix(acc, '#ffffff', .25))
    # ribbon
    rh = h * .12
    A.poly(im, [(w * .04, cy - rh / 2), (w * .96, cy - rh / 2 - h * .02), (w * .96, cy + rh / 2 - h * .02), (w * .04, cy + rh / 2)], '#e03131' if bg != '#c92a2a' else '#7a0c0c')
    A.text(im, (w / 2, cy - h * .014), 'Leiz', A.fit_font('Leiz', 'lobster', w * .6, rh * .95), '#ffffff', rotate=1.2)
    for (dx, dy, rot, s) in [(-.17, .0, 25, 1.0), (.16, -.01, -20, 1.05), (0, .05, 5, 1.15)]:
        chip(im, w * (.5 + dx), h * (.66 + dy), w * .17 * s, w * .115 * s, rot, '#f7d774', '#d9a53a')
    fit_spaced(im, w / 2, h * .835, v[3], 'oswald', fg, w * .8, int(w * .085), .1)
    rule(im, w * .3, w * .7, h * .875, fg, 4)
    spaced(im, (w / 2, h * .905), 'POTATO CRISPS · ' + b['net'].upper(), ('oswald4', int(w * .04)), fg, .12)


def f_Pringlz(im, b, v):
    """Front panel of a tube wrap (drawn on the panel only)."""
    bg, fg, acc, ink = v[4]; w, h = im.size
    cy = h * .25; r = w * .2
    A.ellipse(im, (w / 2 - r, cy - r, w / 2 + r, cy + r), fg)
    # a face: two dots, a bow-shaped moustache
    for s in (-1, 1):
        A.ellipse(im, (w / 2 + s * r * .38 - r * .09, cy - r * .3, w / 2 + s * r * .38 + r * .09, cy - r * .12), ink)
        A.poly(im, [(w / 2, cy + r * .12), (w / 2 + s * r * .42, cy - r * .02), (w / 2 + s * r * .78, cy + r * .2), (w / 2 + s * r * .5, cy + r * .42), (w / 2, cy + r * .3)], ink)
    A.poly(im, [(w / 2 - r * .5, cy - r * .55), (w / 2 + r * .5, cy - r * .55), (w / 2 + r * .32, cy - r * .86), (w / 2 - r * .32, cy - r * .86)], ink)
    A.text(im, (w / 2, h * .5), 'Pringlz', A.fit_font('Pringlz', 'abril', w * .8, h * .14), fg)
    rule(im, w * .25, w * .75, h * .59, acc, 5)
    fit_spaced(im, w / 2, h * .645, v[3], 'oswald', acc, w * .8, int(w * .085), .1)
    for (dx, rot) in [(-.12, 15), (.12, -15), (0, 0)]:
        chip(im, w * (.5 + dx), h * .8, w * .2, w * .1, rot, '#f7d774', '#d9a53a')
    spaced(im, (w / 2, h * .93), 'POTATO CRISPS · ' + b['net'].upper(), ('oswald4', int(w * .042)), fg, .12)


def f_Estrelya(im, b, v):
    bg, fg, acc, ink = v[4]; w, h = im.size
    im.paste(rgb(bg), (0, 0, w, h))
    m = w * .06
    A.rrect(im, (m, h * .09, w - m, h * .91), 0, None, outline=acc, width=5)
    A.star(im, w / 2, h * .24, w * .2, acc)
    A.text(im, (w / 2, h * .42), 'Estrelya', A.fit_font('Estrelya', 'playfair_l', w * .74, h * .1), fg)
    rule(im, w * .25, w * .75, h * .485, acc, 4)
    spaced(im, (w / 2, h * .515), 'POTATO CRISPS', ('oswald4', int(w * .045)), fg, .25)
    for (dx, dy, rot, s) in [(-.13, 0, 20, 1), (.13, 0, -20, 1), (0, .035, 0, 1.1)]:
        chip(im, w * (.5 + dx), h * (.66 + dy), w * .17 * s, w * .11 * s, rot, '#f3c969', '#c98a2b')
    A.rrect(im, (w * .18, h * .78, w * .82, h * .84), 0, acc)
    fit_spaced(im, w / 2, h * .81, v[3], 'oswald', ink, w * .58, int(w * .07), .12)
    spaced(im, (w / 2, h * .875), 'SEDAN 1957 · ' + b['net'].upper(), ('oswald4', int(w * .036)), fg, .15)


def f_Leningrad(im, b, v):
    bg, fg, acc, ink = v[4]; w, h = im.size
    im.paste(rgb(bg), (0, 0, w, h))
    m = w * .07
    A.rrect(im, (m, h * .1, w - m, h * .9), 0, None, outline=fg, width=7)
    A.rrect(im, (m + 16, h * .1 + 16, w - m - 16, h * .9 - 16), 0, None, outline=fg, width=2)
    # the Admiralty spire: a column and a needle
    cx = w / 2; by = h * .4
    A.poly(im, [(cx - w * .012, by - h * .24), (cx + w * .012, by - h * .24), (cx + w * .03, by - h * .09), (cx - w * .03, by - h * .09)], fg)
    A.poly(im, [(cx, by - h * .3), (cx + w * .008, by - h * .24), (cx - w * .008, by - h * .24)], fg)
    A.rrect(im, (cx - w * .07, by - h * .09, cx + w * .07, by - h * .03), 0, fg)
    A.rrect(im, (cx - w * .18, by - h * .03, cx + w * .18, by), 0, fg)
    A.text(im, (w / 2, h * .49), 'ХРУСТЯЩИЙ', A.fit_font('ХРУСТЯЩИЙ', 'oldstd', w * .68, h * .07), fg)
    A.text(im, (w / 2, h * .575), 'КАРТОФЕЛЬ', A.fit_font('КАРТОФЕЛЬ', 'oldstd', w * .68, h * .07), fg)
    A.rrect(im, (w * .18, h * .635, w * .82, h * .71), 0, fg)
    fit_spaced(im, w / 2, h * .672, 'ЛЕНИНГРАДСКИЙ', 'ptserif_b', bg, w * .58, int(w * .06), .08)
    fit_spaced(im, w / 2, h * .77, v[3], 'ptserif_b', ink, w * .6, int(w * .058), .15)
    spaced(im, (w / 2, h * .835), 'ГОСТ · 100 Г · ЦЕНА 46 КОП.', ('ptserif_b', int(w * .034)), ink, .1)


def f_Chitoz(im, b, v):
    bg, fg, acc, ink = v[4]; w, h = im.size
    im.paste(rgb(bg), (0, 0, w, h))
    # diagonal stripes, tone on tone
    lay = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
    for k in range(-6, 14):
        x = k * w * .16
        d.polygon([(x, h), (x + w * .08, h), (x + w * .08 + h * .5, 0), (x + h * .5, 0)], fill=mix(bg, '#ffffff' if bg != '#1a1a1a' else '#ff922b', .12) + (255,))
    im.paste(lay, (0, 0), lay)
    A.text(im, (w / 2, h * .27), 'Chitoz', A.fit_font('Chitoz', 'pacifico', w * .82, h * .2), fg, stroke=int(w * .012), stroke_fill=ink, rotate=6)
    # paw: clean circles
    cx, cy, r = w * .5, h * .58, w * .17
    A.ellipse(im, (cx - r, cy - r * .55, cx + r, cy + r * 1.05), acc)
    for dx, dy in [(-1.05, -.75), (-.38, -1.3), (.38, -1.3), (1.05, -.75)]:
        A.ellipse(im, (cx + r * dx - r * .36, cy + r * dy - r * .42, cx + r * dx + r * .36, cy + r * dy + r * .42), acc)
    A.rrect(im, (w * .14, h * .79, w * .86, h * .87), int(h * .04), fg)
    fit_spaced(im, w / 2, h * .83, v[3], 'oswald', bg if bg != '#1a1a1a' else '#e03131', w * .62, int(w * .08), .1)
    spaced(im, (w / 2, h * .905), 'CORN PUFFS · ' + b['net'].upper(), ('oswald4', int(w * .04)), fg, .15)


def f_Kiryushki(im, b, v):
    bg, fg, acc, ink = v[4]; w, h = im.size
    im.paste(rgb(bg), (0, 0, w, h))
    A.poly(im, [(0, h * .2), (w, h * .12), (w, h * .4), (0, h * .48)], acc)
    A.text(im, (w / 2, h * .3), 'КИРЮШКИ', A.fit_font('КИРЮШКИ', 'inter_black', w * .84, h * .13), fg if acc != '#fab005' else '#1c1c1c', rotate=4.2)
    # a small heap of rye croutons (rendered pieces)
    for i, (dx, dy, rot) in enumerate([(-.2, -.02, 18), (.2, -.03, -24), (-.05, .03, -8), (.1, .075, 12), (-.2, .085, -30)]):
        X.piece(im, 'cube', w * (.5 + dx), h * (.62 + dy), w * .2, rot, seed=i)
    fit_spaced(im, w / 2, h * .82, v[3], 'oswald', acc, w * .8, int(w * .1), .15)
    spaced(im, (w / 2, h * .885), 'РЖАНЫЕ СУХАРИКИ · ' + b['net'].upper(), ('oswald4', int(w * .042)), fg, .1)


def f_Korochki(im, b, v):
    bg, fg, acc, ink = v[4]; w, h = im.size
    im.paste(rgb(bg), (0, 0, w, h))
    cy = h * .46; r = h * .3
    cx = w * .27
    A.ellipse(im, (cx - r, cy - r, cx + r, cy + r), acc)
    A.ellipse(im, (cx - r * .88, cy - r * .88, cx + r * .88, cy + r * .88), None, outline=bg, width=4)
    A.text(im, (cx, cy - r * .05), '4', A.fit_font('4', 'playfair_l', r * 1.2, r * 1.25), bg)
    x0 = w * .52
    A.text(im, (x0 + w * .21, h * .33), 'КОРОЧКИ', A.fit_font('КОРОЧКИ', 'inter_black', w * .42, h * .14), fg)
    rule(im, x0, x0 + w * .42, h * .45, fg, 4)
    spaced(im, (x0 + w * .21, h * .52), 'СУХАРИКИ', ('oswald4', int(w * .045)), '#ffffff', .3)
    A.rrect(im, (w * .06, h * .76, w * .94, h * .89), 0, acc)
    fit_spaced(im, w / 2, h * .825, v[3], 'oswald', ink, w * .8, int(w * .065), .1)
    spaced(im, (x0 + w * .21, h * .63), b['net'].upper(), ('oswald', int(w * .05)), fg, .1)


def f_Khrustam(im, b, v):
    bg, fg, acc, ink = v[4]; w, h = im.size
    im.paste(rgb(bg), (0, 0, w, h))
    # a zigzag "crunch" band
    pts = []
    n = 8
    for k in range(n + 1):
        pts.append((w * k / n, h * (.5 + (.03 if k % 2 else -.03))))
    lower = [(x, y + h * .22) for x, y in pts][::-1]
    A.poly(im, pts + lower, ink)
    # vertical wordmark
    A.text(im, (w / 2, h * .28), 'ХРУСТАМ', A.fit_font('ХРУСТАМ', 'roboto_black', w * .84, h * .09), fg, skew=-.2)
    rule(im, w * .12, w * .88, h * .35, acc, 5)
    spaced(im, (w / 2, h * .385), 'БАГЕТНЫЕ СУХАРИКИ', ('oswald4', int(w * .058)), fg, .12)
    # baguette croutons (rendered pieces)
    for i, (dx, dy, rot) in enumerate([(-.24, -.01, 20), (0, .01, -12), (.24, -.01, 28)]):
        X.piece(im, 'slab', w * (.5 + dx), h * (.61 + dy), w * .14, rot, seed=i)
    fit_spaced(im, w / 2, h * .8, v[3], 'oswald', acc, w * .84, int(w * .1), .1)
    spaced(im, (w / 2, h * .865), b['net'].upper(), ('oswald', int(w * .07)), fg, .1)


def f_Martyn(im, b, v):
    bg, fg, acc, ink = v[4]; w, h = im.size
    im.paste(rgb(bg), (0, 0, w, h))
    cx, cy, r = w / 2, h * .32, w * .25
    pumpkin = v[0] == 'Pumpkin'
    n = 16
    for k in range(n):
        a = 2 * math.pi * k / n
        A.poly(im, [(cx + r * .5 * math.cos(a - .2), cy + r * .5 * math.sin(a - .2)), (cx + r * math.cos(a), cy + r * math.sin(a)), (cx + r * .5 * math.cos(a + .2), cy + r * .5 * math.sin(a + .2))], acc)
    A.ellipse(im, (cx - r * .55, cy - r * .55, cx + r * .55, cy + r * .55), fg if not pumpkin else acc)
    A.ellipse(im, (cx - r * .43, cy - r * .43, cx + r * .43, cy + r * .43), bg)
    # one seed in the middle
    sc = '#f4f1e6' if pumpkin else (fg if bg == '#141414' else '#141414')
    A.poly(im, [(cx, cy - r * .3), (cx + r * .15, cy), (cx + r * .1, cy + r * .24), (cx, cy + r * .3), (cx - r * .1, cy + r * .24), (cx - r * .15, cy)], sc)
    A.text(im, (w / 2, h * .59), 'ОТ МАРТЫНА', A.fit_font('ОТ МАРТЫНА', 'ptserif_b', w * .8, h * .08), fg)
    rule(im, w * .2, w * .8, h * .65, acc, 4)
    spaced(im, (w / 2, h * .69), 'СЕМЕЧКИ', ('oswald4', int(w * .055)), fg, .35)
    A.rrect(im, (w * .2, h * .76, w * .8, h * .84), 0, acc)
    fit_spaced(im, w / 2, h * .8, v[3], 'oswald', bg if bg != '#f4f1e6' else '#ffffff', w * .52, int(w * .07), .12)
    spaced(im, (w / 2, h * .89), b['net'].upper(), ('oswald', int(w * .05)), fg, .1)


def f_Oreshkin(im, b, v):
    """Front panel of a can wrap."""
    bg, fg, acc, ink = v[4]; w, h = im.size
    cx, cy, r = w / 2, h * .3, h * .13
    # a peanut: two overlapping circles with a waist
    for dy, s in ((-.75, 1.0), (.75, 1.08)):
        A.ellipse(im, (cx - r * s, cy + r * dy - r * s, cx + r * s, cy + r * dy + r * s), acc)
    for k in range(-2, 3):
        ImageDraw.Draw(im).arc((cx - r * 1.4, cy + k * r * .55 - r * .5, cx + r * 1.4, cy + k * r * .55 + r * .5), 20, 160, fill=mix(acc, ink, .35), width=4)
    A.text(im, (w / 2, h * .62), 'ОРЕШКИН', A.fit_font('ОРЕШКИН', 'inter_black', w * .84, h * .13), fg)
    rule(im, w * .15, w * .85, h * .71, acc, 5)
    fit_spaced(im, w / 2, h * .78, v[3], 'oswald', acc, w * .8, int(w * .08), .1)
    spaced(im, (w / 2, h * .9), 'ЖАРЕНЫЙ АРАХИС · ' + b['net'].upper(), ('oswald4', int(w * .045)), fg, .1)


def f_Starpom(im, b, v):
    bg, fg, acc, ink = v[4]; w, h = im.size
    im.paste(rgb(bg), (0, 0, w, h))
    d = ImageDraw.Draw(im)
    # waves at the bottom
    for k in range(3):
        y = h * (.8 + k * .06)
        pts = [(x, y + h * .018 * math.sin(x / w * 2 * math.pi * 6 + k)) for x in range(0, w + 1, 6)]
        d.line(pts, fill=mix(bg, '#ffffff', .25), width=5)
    cx = w * .22; cy = h * .45; r = h * .22
    A.ellipse(im, (cx - r * 1.25, cy - r * 1.25, cx + r * 1.25, cy + r * 1.25), None, outline=acc, width=6)
    A.anchor_icon(im, cx, cy, r, acc)
    x0 = w * .42
    A.text(im, (x0 + w * .25, h * .34), 'СТАРПОМ', A.fit_font('СТАРПОМ', 'ptserif_b', w * .5, h * .2), fg)
    rule(im, x0, x0 + w * .5, h * .47, acc, 4)
    fit_spaced(im, x0 + w * .25, h * .57, v[3], 'oswald', acc, w * .5, int(h * .12), .12)
    spaced(im, (x0 + w * .25, h * .69), 'К ПЕННОМУ · ' + b['net'].upper(), ('oswald4', int(h * .06)), fg, .12)


def f_Kinozal(im, b, v):
    """Front of a carton: stripes, a ticket-shaped plate."""
    bg, fg, acc, ink = v[4]; w, h = im.size
    im.paste(rgb(fg), (0, 0, w, h))
    d = ImageDraw.Draw(im)
    n = 7
    for k in range(n):
        if k % 2 == 0: d.rectangle((k * w / n, 0, (k + 1) * w / n, h), fill=rgb(bg))
    A.rrect(im, (w * .08, h * .2, w * .92, h * .56), int(w * .04), ink)
    for s in (0, 1):   # ticket notches
        x = w * (.08 if s == 0 else .92)
        A.ellipse(im, (x - w * .04, h * .38 - w * .04, x + w * .04, h * .38 + w * .04), bg if s == 0 else (bg if (n - 1) % 2 == 0 else fg))
    A.text(im, (w / 2, h * .32), 'КИНОЗАЛ', A.fit_font('КИНОЗАЛ', 'inter_black', w * .7, h * .1), acc if acc != ink else '#ffd400')
    rule(im, w * .2, w * .8, h * .4, '#ffffff', 3)
    spaced(im, (w / 2, h * .46), 'ПОПКОРН', ('oswald', int(w * .085)), '#ffffff', .35)
    A.rrect(im, (w * .16, h * .68, w * .84, h * .78), int(h * .05), ink)
    fit_spaced(im, w / 2, h * .73, v[3], 'oswald', '#ffffff', w * .56, int(w * .08), .12)
    A.rrect(im, (w * .3, h * .84, w * .7, h * .91), int(h * .035), '#ffffff')
    spaced(im, (w / 2, h * .875), b['net'].upper(), ('oswald', int(w * .06)), ink, .1)


def f_Yozhik(im, b, v):
    bg, fg, acc, ink = v[4]; w, h = im.size
    im.paste(rgb(bg), (0, 0, w, h))
    cx, cy, r = w / 2, h * .46, w * .27
    A.ellipse(im, (cx - r * 1.3, cy - r * 1.35, cx + r * 1.3, cy + r * .75), mix(bg, '#ffffff', .35) if bg != '#f8f0dc' else '#ffffff')
    # hedgehog: a half-disc of spikes, a round face
    spike = ink if bg != '#5c3a21' else '#2b1a0e'
    n = 13
    for k in range(n):
        a = math.pi * (1.0 + k / (n - 1))
        A.poly(im, [(cx + r * .55 * math.cos(a - .16), cy + r * .55 * math.sin(a - .16)), (cx + r * 1.05 * math.cos(a), cy + r * 1.05 * math.sin(a)), (cx + r * .55 * math.cos(a + .16), cy + r * .55 * math.sin(a + .16))], spike)
    A.poly(im, [(cx - r * .8, cy), (cx + r * .8, cy)] + [(cx + r * .8 * math.cos(math.pi * (2 - t / 30)), cy + r * .8 * math.sin(math.pi * (2 - t / 30))) for t in range(31)], spike)
    A.ellipse(im, (cx - r * .8, cy - r * .22, cx + r * .8, cy + r * .5), '#f3d9b1')
    for s in (-1, 1):
        A.ellipse(im, (cx + s * r * .3 - r * .07, cy + r * .02, cx + s * r * .3 + r * .07, cy + r * .16), '#222222')
    A.ellipse(im, (cx - r * .1, cy + r * .2, cx + r * .1, cy + r * .36), '#222222')
    A.text(im, (w / 2, h * .19), 'Ёжик', A.fit_font('Ёжик', 'ptserif_bi', w * .7, h * .13), acc)
    spaced(im, (w / 2, h * .7), 'КУКУРУЗНЫЕ ПАЛОЧКИ', ('oswald', int(w * .058)), fg if bg != '#74c0fc' else ink, .12)
    A.rrect(im, (w * .2, h * .76, w * .8, h * .84), int(h * .04), acc)
    fit_spaced(im, w / 2, h * .8, v[3], 'oswald', '#ffffff', w * .5, int(w * .075), .15)
    spaced(im, (w / 2, h * .885), b['net'].upper(), ('oswald', int(w * .05)), fg if bg != '#74c0fc' else ink, .1)


FRONT = {k[2:]: f for k, f in globals().items() if k.startswith('f_')}
GENERIC = None


import extra as X
CUR = {'food': 'chip'}


def draw_front(im, b, v):
    from catalog import RARE
    import catalog_series as CS
    CUR['food'] = {'ridged': 'ridged', 'tortilla': 'triangle'}.get(b.get('kind'), 'chip')
    CUR['dna'] = X.dna(b)
    if b.get('gen'): X.bgpat(im, b, v)
    FRONT.get(b.get('layout', b['id']), GENERIC)(im, b, v)
    season = RARE.get(b['id'] + v[0])
    X.decor(im, b, v, season, CS.SEASON[season][0 if b['lang'] == 'ru' else 1] if season else None)


def make(b, v):
    sid = b['id'] + v[0]
    bg, fg, acc, ink = v[4]
    sh = b['shape']
    os.makedirs(OUT, exist_ok=True)
    if sh == 'bag':
        W, H, T = b['size']
        fh = int(round(FW * H / W / 4)) * 4
        front = Image.new('RGB', (FW * K, fh * K), rgb(bg))
        draw_front(front, b, v)
        seal = b.get('seal', .06); ss = b.get('side_seal', 0)
        sealcol = mix(bg, '#000000', .15)
        s = seal_bands(front, seal, sealcol)
        back = back_panel(b, v, FW * K, fh * K, top_pad=s, bot_pad=s, left_pad=int(FW * K * ss))
        seal_bands(back, seal, sealcol)
        if ss: side_bands(front, ss, sealcol); side_bands(back, ss, sealcol)
        sheet = Image.new('RGB', (FW * K * 2, fh * K)); sheet.paste(front, (0, 0)); sheet.paste(back, (FW * K, 0))
        out = sheet.resize((FW * 2, fh), Image.LANCZOS)
    elif sh in ('tube', 'can'):
        # 1024x1024: wrap in the top 75% (front panel centred at u=.25, back at .75), lid and bottom below
        S = 1024 * K
        im = Image.new('RGB', (S, S), rgb(bg))
        hh = int(S * .75)
        if sh == 'can':
            # a short wide can: panels are drawn in true proportion, then squeezed into their share of the wrap
            panel = Image.new('RGB', (hh, hh), rgb(bg))
            draw_front(panel, b, v)
            im.paste(panel.resize((int(S * .3), hh), Image.LANCZOS), (int(S * .1), 0))
            bp = back_panel(b, v, int(hh * .9 * 1.33), int(hh * .9)).resize((int(S * .36), int(hh * .9)), Image.LANCZOS)
        else:
            panel = Image.new('RGB', (S // 2, hh), rgb(bg))
            draw_front(panel, b, v)
            im.paste(panel, (0, 0))
            bp = back_panel(b, v, int(S * .36), int(hh * .9))
        im.paste(bp, (int(S * .57), int(hh * .05)))
        d = ImageDraw.Draw(im)
        if sh == 'can':   # metal rims top and bottom of the wrap
            for y0 in (0, hh - int(hh * .05)):
                d.rectangle((0, y0, S, y0 + int(hh * .05)), fill=(190, 192, 196))
                d.line([(0, y0 + int(hh * .025)), (S, y0 + int(hh * .025))], fill=(140, 142, 146), width=4)
        q = S // 4
        lidcol = (236, 236, 232) if sh == 'tube' else rgb(mix(bg, '#ffffff', .1))
        d.rectangle((0, hh, q, S), fill=lidcol)
        d.ellipse((q * .12, hh + q * .12, q * .88, hh + q * .88), outline=rgb(mix(lidcol, '#000000', .15)), width=6)
        name = b.get('name') or {'Pringlz': 'Pringlz', 'Leiz': 'Leiz', 'Oreshkin': 'ОРЕШКИН'}.get(b.get('layout', b['id']), b['en'].split(' ')[0])
        lidfont = ('cjk' if any(ord(c) > 0x2e7f for c in name) else b['font']) if b.get('gen') else ('lobster' if name == 'Leiz' else 'abril' if sh == 'tube' else 'inter_black')
        f = A.fit_font(name, lidfont, q * .55, q * .2)
        d.text((q / 2, hh + q / 2), name, font=f, fill=rgb(bg if sh == 'tube' else fg), anchor='mm')
        d.rectangle((q, hh, 2 * q, S), fill=(176, 178, 182))
        for rr in (.8, .55, .3):
            d.ellipse((q * 1.5 - q * rr / 2, hh + q / 2 - q * rr / 2, q * 1.5 + q * rr / 2, hh + q / 2 + q * rr / 2), outline=(150, 152, 156), width=5)
        im.paste(im.crop((0, hh, q, hh + q)).rotate(LIDROT), (0, hh))
        out = im.resize((1024, 1024), Image.LANCZOS)
    elif sh == 'box':
        # atlas 1024: v .2..1 = sides: front u 0..0.3, right .3..0.5, back .5..0.8, left .8..1; top u 0..0.3 v 0..0.2; bottom u .3..0.6
        S = 1024 * K
        im = Image.new('RGB', (S, S), rgb(bg))
        hh = int(S * .8)
        fr = Image.new('RGB', (int(S * .3), hh), rgb(bg)); draw_front(fr, b, v); im.paste(fr, (0, 0))
        bk = Image.new('RGB', (int(S * .3), hh), rgb(bg)); draw_front(bk, b, v)
        bp = back_panel(b, v, int(S * .3 * .84), int(hh * .5))
        bk.paste(bp, (int(S * .3 * .08), int(hh * .42)))
        im.paste(bk, (int(S * .5), 0))
        for x0 in (.3, .8):
            sd = Image.new('RGB', (int(S * .2), hh), rgb(fg)); d = ImageDraw.Draw(sd)
            n = 5
            for k in range(n):
                if k % 2 == 0: d.rectangle((k * sd.size[0] / n, 0, (k + 1) * sd.size[0] / n, hh), fill=rgb(bg))
            A.text(sd, (sd.size[0] / 2, hh / 2), 'КИНОЗАЛ', A.fit_font('КИНОЗАЛ', 'inter_black', hh * .6, sd.size[0] * .5), ink, rotate=90, stroke=10, stroke_fill='#ffffff')
            im.paste(sd, (int(S * x0), 0))
        d = ImageDraw.Draw(im)
        d.rectangle((0, hh, int(S * .3), S), fill=rgb(ink))
        f = A.fit_font('КИНОЗАЛ', 'inter_black', S * .22, S * .08)
        d.text((S * .15, (hh + S) / 2), 'КИНОЗАЛ', font=f, fill=rgb('#ffd400'), anchor='mm')
        d.rectangle((int(S * .3), hh, int(S * .6), S), fill=(214, 206, 190))
        import random
        rnd = random.Random(7)
        # the heap on top: rendered popped kernels, packed tight (its own picture, so nothing spills onto the bottom of the box)
        fw, fh = S - int(S * .6), S - hh
        fill = Image.new('RGB', (fw, fh), (226, 196, 120))
        for i in range(520):
            cx, cy = rnd.uniform(-fw * .03, fw * 1.03), rnd.uniform(-fh * .08, fh * 1.08)
            X.piece(fill, 'pop', cx, cy, fw * rnd.uniform(.034, .05), rnd.uniform(0, 360), seed=i)
        im.paste(fill, (int(S * .6), hh))
        out = im.resize((1024, 1024), Image.LANCZOS)
    out = X.finish(out); out.save(os.path.join(OUT, sid + '.png'))
    return out


if __name__ == '__main__':
    only = sys.argv[1:]
    for b in BRANDS:
        if only and b['id'] not in only: continue
        for v in b['var']:
            make(b, v); print('label', b['id'] + v[0])

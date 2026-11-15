"""Shared layouts for the brands of catalog.GEN: a wordmark, an emblem, the food and the flavour, arranged eleven ways.
python3 labels3.py [brand ids] -> ../tex/<Id>.png (the first thirteen brands go through labels2's own layouts)."""
import sys, os, math, random
from PIL import Image, ImageDraw
import art as A
from art import rgb, mix
import labels2 as L
from catalog import BRANDS

K = L.K


def cjk(t): return any(ord(c) > 0x2e7f for c in t)


def T(im, cx, cy, txt, font, fill, maxw, maxh, rotate=0, stroke=0, stroke_fill=None):
    f = A.fit_font(txt, 'cjk' if cjk(txt) else font, maxw, maxh)
    A.text(im, (cx, cy), txt, f, fill, rotate=rotate, stroke=stroke, stroke_fill=stroke_fill)


NARROW = 1.0


def vis(col, on):
    """col if it reads on the background, else white or near-black."""
    a, b = sum(rgb(col)) / 3, sum(rgb(on)) / 3
    return col if abs(a - b) > 70 else ('#ffffff' if b < 128 else '#1a1a1a')


def SP(im, cx, y, txt, fill, maxw, size, sp=.1, bold=True):
    size *= NARROW
    if cjk(txt): T(im, cx, y, txt, 'cjk', fill, maxw, size * 1.25)
    else: L.fit_spaced(im, cx, y, txt, 'oswald' if bold else 'oswald4', fill, maxw, int(size), sp)


def layer(size):
    lay = Image.new('RGBA', (int(size), int(size)), (0, 0, 0, 0))
    return lay, ImageDraw.Draw(lay), int(size) / 2


def put(im, lay, cx, cy, rot=0):
    if rot: lay = lay.rotate(rot, Image.BICUBIC)
    im.paste(lay, (int(cx - lay.size[0] / 2), int(cy - lay.size[1] / 2)), lay)


def C(c, a=255): return rgb(c) + (a,)


# ------------------------------------------------------------------ food, flat: three or so pieces around (cx, cy), r = cluster radius
def piece(im, kind, cx, cy, s, rot, seed=0):
    lay, d, c = layer(s * 2.8)
    lw = max(3, int(s * .07))
    def blob(box, fill, edge): d.ellipse(box, fill=C(edge)); d.ellipse((box[0] + lw, box[1] + lw, box[2] - lw, box[3] - lw * 1.4), fill=C(fill))
    if kind in ('chip', 'ridged', 'rind', 'crescent', 'pop'):
        fill, edge = {'chip': ('#f7d774', '#d9a53a'), 'ridged': ('#f5cf5d', '#cf9730'), 'rind': ('#e8b877', '#b8793a'), 'crescent': ('#e0863a', '#a8571c'), 'pop': ('#fff6dc', '#e2c27a')}[kind]
        if kind == 'pop':
            for dx, dy, rr in ((-.4, .1, .55), (.4, .15, .5), (0, -.35, .6), (0, .3, .45)):
                blob((c + s * (dx - rr), c + s * (dy - rr), c + s * (dx + rr), c + s * (dy + rr)), fill, edge)
        elif kind == 'crescent':
            d.pieslice((c - s, c - s * .6, c + s, c + s * .6), 200, 340, fill=C(edge))
            d.chord((c - s, c - s * .75, c + s, c + s * .45), 200, 340, fill=C(edge))
            d.arc((c - s, c - s * .55, c + s, c + s * .7), 205, 335, fill=C(fill), width=int(s * .3))
        else:
            blob((c - s, c - s * .68, c + s, c + s * .68), fill, edge)
            if kind == 'ridged':
                for k in range(-3, 4):
                    x = k * .24; hh = .68 * math.sqrt(1 - x * x) * .78
                    d.line([(c + x * s, c - hh * s), (c + x * s, c + hh * s)], fill=C(edge), width=lw)
            elif kind == 'rind':
                for k in range(5): d.ellipse((c - s * .6 + k * s * .28, c - s * .2 + (k % 2) * s * .2, c - s * .45 + k * s * .28, c - s * .05 + (k % 2) * s * .2), fill=C('#f6d9a8'))
            else:
                d.arc((c - s * .62, c - s * .42, c + s * .62, c + s * .42), 200, 300, fill=C(mix(fill, '#ffffff', .55)), width=lw)
    elif kind in ('triangle', 'cone'):
        fill, edge = ('#f2a93b', '#c2761a') if kind == 'triangle' else ('#f2c14e', '#c98a1b')
        pts = [(c, c - s), (c + s * .95, c + s * .75), (c - s * .95, c + s * .75)] if kind == 'triangle' else [(c - s * .9, c + s * .5), (c + s * .9, c), (c - s * .6, c - s * .55)]
        d.polygon(pts, fill=C(edge))
        cc = (sum(p[0] for p in pts) / 3, sum(p[1] for p in pts) / 3)
        d.polygon([(cc[0] + (x - cc[0]) * .8, cc[1] + (y - cc[1]) * .8) for x, y in pts], fill=C(fill))
        if kind == 'triangle':
            r = random.Random(seed)
            for _ in range(7):
                x, y = cc[0] + r.uniform(-.25, .25) * s, cc[1] + r.uniform(-.2, .35) * s
                d.ellipse((x - lw * .6, y - lw * .6, x + lw * .6, y + lw * .6), fill=C('#c2410c'))
        else: d.ellipse((c - s * .95, c + s * .15, c - s * .55, c + s * .75), fill=C(edge))
    elif kind in ('puff', 'flip', 'curl', 'paw', 'roll', 'stick', 'strip', 'wave', 'jerk'):
        col = {'jerk': '#6b3a1e', 'puff': '#ff9f1c', 'flip': '#e9b872', 'curl': '#f2b33d', 'paw': '#f0a830', 'roll': '#c2410c', 'stick': '#e2a84b', 'strip': '#b3261e', 'wave': '#f4b183'}[kind]
        edge = mix(col, '#000000', .25)
        w = int(s * {'stick': .16, 'strip': .3, 'jerk': .42, 'wave': .26, 'roll': .3}.get(kind, .42))
        if kind == 'paw':
            for dx, dy, rr in ((0, .25, .55), (-.55, -.3, .25), (0, -.55, .27), (.55, -.3, .25)):
                d.ellipse((c + s * (dx - rr), c + s * (dy - rr), c + s * (dx + rr), c + s * (dy + rr)), fill=C(col), outline=C(edge), width=lw)
        elif kind in ('stick', 'roll', 'strip', 'jerk'):
            d.rounded_rectangle((c - s, c - w / 2, c + s, c + w / 2), w // 2 if kind not in ('strip', 'jerk') else 6, fill=C(col), outline=C(edge), width=lw)
            if kind == 'roll': d.ellipse((c + s - w * .9, c - w / 2, c + s, c + w / 2), fill=C(edge))
        else:
            a0, a1 = (200, 340) if kind != 'curl' else (150, 390)
            box = (c - s * .8, c - s * .5, c + s * .8, c + s * 1.1)
            d.arc(box, a0, a1, fill=C(edge), width=w + lw * 2)
            d.arc(box, a0 + 2, a1 - 2, fill=C(col), width=w)
            if kind == 'wave': d.arc((c - s * .8, c - s * 1.3, c + s * .8, c + s * .3), 20, 160, fill=C(col), width=w)
    elif kind in ('cube', 'slab', 'cracker', 'sheet'):
        col = {'cube': '#a8743d', 'slab': '#b07a3a', 'cracker': '#f0c987', 'sheet': '#1f4d2b'}[kind]
        edge = mix(col, '#000000', .3)
        hx, hy = {'cube': (.6, .6), 'slab': (1, .32), 'cracker': (.75, .75), 'sheet': (.95, .55)}[kind]
        d.rounded_rectangle((c - s * hx, c - s * hy, c + s * hx, c + s * hy), int(s * .1), fill=C(col), outline=C(edge), width=lw)
        if kind == 'cracker':
            for i in (-1, 0, 1):
                for j in (-1, 0, 1): d.ellipse((c + i * s * .4 - lw, c + j * s * .4 - lw, c + i * s * .4 + lw, c + j * s * .4 + lw), fill=C(edge))
        elif kind == 'sheet':
            for i in range(-3, 4): d.line([(c + i * s * .25, c - s * hy + lw), (c + i * s * .25, c + s * hy - lw)], fill=C(mix(col, '#ffffff', .15)), width=lw)
        else: d.rounded_rectangle((c - s * hx + lw * 2, c - s * hy + lw * 2, c + s * hx - lw * 2, c - s * hy * .2), int(s * .06), fill=C(mix(col, '#ffffff', .2)))
    elif kind in ('seed', 'kernel', 'nut', 'crumb', 'ball', 'fish'):
        col = {'ball': '#c98b3c', 'fish': '#b9c2c9', 'seed': '#2a2a2a', 'kernel': '#f2b705', 'nut': '#e3b574', 'crumb': '#e9a93a'}[kind]
        edge = mix(col, '#000000', .3) if kind != 'seed' else '#000000'
        if kind == 'seed':
            d.polygon([(c - s * 1.1, c), (c - s * .2, c - s * .6), (c + s * 1.12, c), (c - s * .2, c + s * .6)], fill=C('#e8e2d0'))
            d.ellipse((c - s * 1.05, c - s * .58, c + s * .42, c + s * .58), fill=C('#e8e2d0'))
            d.polygon([(c - s, c), (c - s * .2, c - s * .5), (c + s, c), (c - s * .2, c + s * .5)], fill=C(col))
            d.ellipse((c - s * .95, c - s * .48, c + s * .35, c + s * .48), fill=C(col))
            for k in (-.2, .05): d.line([(c - s * .7, c + s * k), (c + s * .6, c + s * k * .3)], fill=C('#e8e2d0'), width=lw)
        elif kind == 'nut':
            for dx in (-.42, .42): d.ellipse((c + s * (dx - .55), c - s * .5, c + s * (dx + .55), c + s * .5), fill=C(col), outline=C(edge), width=lw)
            d.rectangle((c - s * .3, c - s * .3, c + s * .3, c + s * .3), fill=C(col))
        elif kind == 'ball':
            d.ellipse((c - s * .62, c - s * .62, c + s * .62, c + s * .62), fill=C(col), outline=C(edge), width=lw)
            d.arc((c - s * .4, c - s * .4, c + s * .4, c + s * .4), 200, 290, fill=C(mix(col, '#ffffff', .5)), width=lw)
        elif kind == 'fish':
            d.ellipse((c - s, c - s * .4, c + s * .45, c + s * .4), fill=C(col), outline=C(edge), width=lw)
            d.polygon([(c + s * .3, c), (c + s, c - s * .45), (c + s, c + s * .45)], fill=C(col), outline=C(edge))
            d.ellipse((c - s * .75, c - s * .15, c - s * .6, c), fill=C(edge))
        elif kind == 'kernel':
            d.rounded_rectangle((c - s * .6, c - s * .7, c + s * .6, c + s * .7), int(s * .45), fill=C(col), outline=C(edge), width=lw)
        else:
            r = random.Random(seed)
            for _ in range(9):
                x, y, l, a = c + r.uniform(-.7, .7) * s, c + r.uniform(-.5, .5) * s, r.uniform(.3, .6) * s, r.uniform(0, 3.14)
                d.line([(x - l * math.cos(a), y - l * math.sin(a)), (x + l * math.cos(a), y + l * math.sin(a))], fill=C(col), width=lw * 2)
    elif kind in ('ring', 'pretzel'):
        col = '#c98b3c' if kind == 'ring' else '#8a4b1c'
        w = int(s * .26)
        if kind == 'ring': d.ellipse((c - s * .8, c - s * .8, c + s * .8, c + s * .8), outline=C(col), width=w); d.arc((c - s * .7, c - s * .7, c + s * .7, c + s * .7), 200, 280, fill=C(mix(col, '#ffffff', .4)), width=lw)
        else:
            for dx in (-.42, .42): d.ellipse((c + s * (dx - .5), c - s * .75, c + s * (dx + .5), c + s * .25), outline=C(col), width=w)
            d.arc((c - s * .9, c - s * .5, c + s * .9, c + s * .95), 20, 160, fill=C(col), width=w)
            for x, y in ((-.6, -.3), (.1, -.62), (.55, .1), (-.2, .78)): d.rectangle((c + s * x, c + s * y, c + s * x + lw, c + s * y + lw), fill=C('#ffffff'))
    put(im, lay, cx, cy, rot)


piece_flat = piece
piece = L.X.piece


def food(im, kind, cx, cy, r, seed=1):
    arr = (L.CUR.get('dna', 0) >> 8) % 4
    if arr == 1: return L.X.pile(im, kind, cx, cy, r * 1.25, 7, seed + L.CUR.get('dna', 0) % 97, .5 if kind not in ('seed', 'kernel', 'ball') else .3)
    if arr == 2 and kind not in ('stick', 'strip', 'roll', 'slab', 'jerk', 'fish', 'crumb', 'sheet', 'seed', 'kernel', 'ball'):
        piece(im, kind, cx - r * .25, cy + r * .12, r * .78, 14, seed); piece(im, kind, cx + r * .42, cy - r * .1, r * .6, -24, seed + 1); return
    lay = {'stick': [(0, -.35, 12, 1.1), (-.05, 0, -6, 1.15), (.05, .35, 4, 1.05)], 'strip': [(0, -.35, 8, 1.1), (0, .05, -5, 1.1), (0, .42, 3, 1.0)],
           'roll': [(0, -.35, 10, 1.05), (0, .05, -8, 1.1), (0, .42, 2, 1.0)], 'slab': [(0, -.35, 6, 1.0), (0, .1, -5, 1.05)],
           'seed': [(-.5, -.1, 30, .55), (.45, -.2, -40, .55), (0, .3, 10, .6), (-.1, -.5, -15, .5)], 'kernel': [(-.5, 0, 20, .5), (.5, -.05, -25, .5), (0, .1, 0, .55)], 'ball': [(-.5, 0, 0, .55), (.5, -.05, 0, .55), (0, .15, 0, .6)],
           'jerk': [(0, -.3, 8, 1.05), (0, .25, -6, 1.0)], 'fish': [(-.1, -.3, 8, .85), (.1, .25, -6, .85)],
           'crumb': [(0, 0, 0, 1.0)], 'sheet': [(-.1, -.15, -8, .9), (.15, .2, 6, .9)]}.get(kind, [(-.52, .02, 22, .72), (.5, -.03, -18, .75), (0, .16, 4, .85)])
    for dx, dy, rot, s in lay: piece(im, kind, cx + dx * r, cy + dy * r, r * .62 * s, rot, seed)


# ------------------------------------------------------------------ emblems, one colour
def emblem(im, kind, cx, cy, r, col, bg):
    d = ImageDraw.Draw(im); c = rgb(col); w = max(4, int(r * .09))
    if kind in ('none', None): return
    if kind == 'flame':
        A.poly(im, [(cx, cy - r), (cx + r * .7, cy + r * .1), (cx + r * .45, cy + r * .9), (cx - r * .45, cy + r * .9), (cx - r * .7, cy + r * .2), (cx - r * .25, cy - r * .2)], col)
        A.poly(im, [(cx, cy - r * .2), (cx + r * .3, cy + r * .45), (cx, cy + r * .9), (cx - r * .3, cy + r * .45)], bg)
    elif kind == 'waves':
        for k in (-.5, 0, .5):
            for i in range(-2, 2): d.arc((cx + i * r * .5, cy + k * r - r * .25, cx + (i + 1) * r * .5, cy + k * r + r * .25), 180 if i % 2 else 0, 360 if i % 2 else 180, fill=c, width=w)
    elif kind == 'horn':
        A.poly(im, [(cx - r, cy + r * .5), (cx + r, cy - r * .1), (cx + r * .6, cy - r * .8)], col); A.ellipse(im, (cx - r * 1.1, cy + r * .1, cx - r * .6, cy + r * .8), col)
    elif kind == 'nut':
        for dy in (-.45, .45): A.ellipse(im, (cx - r * .55, cy + r * (dy - .55), cx + r * .55, cy + r * (dy + .55)), col)
    elif kind in ('sunring', 'starring'):
        if kind == 'sunring':
            for k in range(16):
                a = k * math.pi / 8
                d.line([(cx + r * .62 * math.cos(a), cy + r * .62 * math.sin(a)), (cx + r * math.cos(a), cy + r * math.sin(a))], fill=c, width=w)
            A.ellipse(im, (cx - r * .5, cy - r * .5, cx + r * .5, cy + r * .5), col)
        else:
            d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=c, width=w); A.star(im, cx, cy, r * .7, col)
    elif kind == 'mill':
        A.poly(im, [(cx - r * .3, cy + r), (cx + r * .3, cy + r), (cx + r * .18, cy - r * .1), (cx - r * .18, cy - r * .1)], col)
        for a in (45, 135, 225, 315):
            x, y = cx + r * math.cos(math.radians(a)), cy - r * .2 + r * math.sin(math.radians(a))
            d.line([(cx, cy - r * .2), (x, y)], fill=c, width=w * 2)
    elif kind == 'wheat':
        d.line([(cx, cy + r), (cx, cy - r * .6)], fill=c, width=w)
        for k in range(4):
            y = cy - r * .7 + k * r * .35
            for sx in (-1, 1): A.ellipse(im, (cx + sx * r * .08 - (r * .34 if sx < 0 else 0), y - r * .13, cx + sx * r * .08 + (r * .34 if sx > 0 else 0), y + r * .13), col)
        A.ellipse(im, (cx - r * .12, cy - r * 1.05, cx + r * .12, cy - r * .65), col)
    elif kind == 'bear':
        A.ellipse(im, (cx - r * .8, cy - r * .55, cx + r * .8, cy + r * .9), col)
        for sx in (-1, 1): A.ellipse(im, (cx + sx * r * .62 - r * .3, cy - r * .9, cx + sx * r * .62 + r * .3, cy - r * .3), col)
        A.ellipse(im, (cx - r * .32, cy + r * .1, cx + r * .32, cy + r * .65), bg)
        A.ellipse(im, (cx - r * .12, cy + r * .18, cx + r * .12, cy + r * .36), col)
        for sx in (-1, 1): A.ellipse(im, (cx + sx * r * .35 - r * .07, cy - r * .12, cx + sx * r * .35 + r * .07, cy + r * .02), bg)
    elif kind == 'pig':
        A.ellipse(im, (cx - r * .85, cy - r * .7, cx + r * .85, cy + r * .85), col)
        for sx in (-1, 1): A.poly(im, [(cx + sx * r * .85, cy - r * .95), (cx + sx * r * .3, cy - r * .6), (cx + sx * r * .8, cy - r * .2)], col)
        A.ellipse(im, (cx - r * .38, cy + r * .05, cx + r * .38, cy + r * .55), bg)
        for sx in (-1, 1):
            A.ellipse(im, (cx + sx * r * .16 - r * .07, cy + r * .2, cx + sx * r * .16 + r * .07, cy + r * .4), col)
            A.ellipse(im, (cx + sx * r * .38 - r * .07, cy - r * .25, cx + sx * r * .38 + r * .07, cy - r * .11), bg)
    elif kind == 'tower':
        A.poly(im, [(cx, cy - r), (cx + r * .14, cy - r * .35), (cx + r * .3, cy - r * .35), (cx + r * .3, cy + r * .5), (cx + r * .7, cy + r * .5), (cx + r * .7, cy + r), (cx - r * .7, cy + r), (cx - r * .7, cy + r * .5),
                    (cx - r * .3, cy + r * .5), (cx - r * .3, cy - r * .35), (cx - r * .14, cy - r * .35)], col)
        A.star(im, cx, cy - r * 1.05, r * .2, col)
    elif kind == 'kettle':
        A.ellipse(im, (cx - r * .7, cy - r * .3, cx + r * .7, cy + r), col)
        A.poly(im, [(cx + r * .55, cy + r * .2), (cx + r * 1.1, cy - r * .35), (cx + r * 1.2, cy - r * .2), (cx + r * .65, cy + r * .55)], col)
        d.arc((cx - r * .5, cy - r * .95, cx + r * .5, cy + r * .1), 180, 360, fill=c, width=w)
        A.ellipse(im, (cx - r * .12, cy - r * .5, cx + r * .12, cy - r * .26), col)


# ------------------------------------------------------------------ the layouts
def _p(im, b, v):
    bg, fg, acc, ink = v[4]; w, h = im.size
    return bg, fg, acc, ink, w, h


def t_burst(im, b, v):
    bg, fg, acc, ink, w, h = _p(im, b, v)
    cy, r = h * .34, w * .37
    A.ellipse(im, (w / 2 - r, cy - r, w / 2 + r, cy + r), mix(bg, '#ffffff', .22)); A.ellipse(im, (w / 2 - r * .84, cy - r * .84, w / 2 + r * .84, cy + r * .84), acc)
    rh = min(h * .13, w * .2)
    A.poly(im, [(w * .04, cy - rh / 2 + h * .01), (w * .96, cy - rh / 2 - h * .012), (w * .96, cy + rh / 2 - h * .012), (w * .04, cy + rh / 2 + h * .01)], ink)
    T(im, w / 2, cy - h * .004, b['name'], b['font'], '#ffffff', w * .8, rh * .74, rotate=1.3)
    food(im, b['food'], w / 2, h * .65, w * .3)
    SP(im, w / 2, h * .835, v[3], fg, w * .84, w * .085)
    L.rule(im, w * .3, w * .7, h * .878, fg, 4)
    SP(im, w / 2, h * .91, b['sub'] + ' · ' + b['net'].upper(), fg, w * .86, w * .04, bold=False)


def t_band(im, b, v):
    bg, fg, acc, ink, w, h = _p(im, b, v)
    emblem(im, b['emb'], w / 2, h * .15, w * .12, acc, bg)
    y0, y1 = (h * .26, h * .44) if b['emb'] != 'none' else (h * .12, h * .34)
    ImageDraw.Draw(im).rectangle((0, y0, w, y1), fill=rgb('#ffffff'))
    ImageDraw.Draw(im).rectangle((0, y1, w, y1 + h * .012), fill=rgb(acc))
    T(im, w / 2, (y0 + y1) / 2, b['name'], b['font'], bg if sum(rgb(bg)) < 560 else ink, w * .84, (y1 - y0) * .66)
    SP(im, w / 2, y1 + h * .055, b['sub'], fg, w * .84, w * .045, bold=False)
    food(im, b['food'], w / 2, h * .66, w * .29)
    A.rrect(im, (w * .1, h * .8, w * .9, h * .875), int(h * .04), acc)
    SP(im, w / 2, h * .8375, v[3], ink if sum(rgb(acc)) > 380 else '#ffffff', w * .72, w * .07)
    SP(im, w / 2, h * .925, b['net'].upper(), fg, w * .5, w * .045)


def t_frame(im, b, v):
    bg, fg, acc, ink, w, h = _p(im, b, v)
    m = w * .06
    ImageDraw.Draw(im).rectangle((m, h * .075, w - m, h * .925), outline=rgb(acc), width=5)
    e = b['emb'] != 'none'
    if e: emblem(im, b['emb'], w / 2, h * .2, w * .11, acc, bg)
    ny = h * .37 if e else h * .22
    T(im, w / 2, ny, b['name'], b['font'], fg, w * .78, h * .12)
    L.rule(im, w * .28, w * .72, ny + h * .075, acc, 4)
    SP(im, w / 2, ny + h * .11, b['sub'], fg, w * .76, w * .042, bold=False)
    food(im, b['food'], w / 2, h * .64 if e else h * .56, w * .27)
    ImageDraw.Draw(im).rectangle((w * .14, h * .79, w * .86, h * .86), fill=rgb(acc))
    SP(im, w / 2, h * .825, v[3], ink if sum(rgb(acc)) > 380 else '#ffffff', w * .66, w * .062)
    SP(im, w / 2, h * .895, b['net'].upper(), fg, w * .5, w * .04, bold=False)


def t_diag(im, b, v):
    bg, fg, acc, ink, w, h = _p(im, b, v)
    A.poly(im, [(0, h * .52), (w, h * .36), (w, h), (0, h)], ink)
    A.poly(im, [(0, h * .52), (w, h * .36), (w, h * .375), (0, h * .535)], acc)
    emblem(im, b['emb'], w * .5, h * .12, w * .09, acc, bg)
    T(im, w / 2, h * .27 if b['emb'] != 'none' else h * .22, b['name'], b['font'], fg, w * .86, h * .15, rotate=5)
    food(im, b['food'], w / 2, h * .62, w * .29)
    SP(im, w / 2, h * .815, v[3], vis(acc, ink), w * .84, w * .085)
    SP(im, w / 2, h * .885, b['sub'], '#ffffff', w * .84, w * .042, bold=False)
    SP(im, w / 2, h * .93, b['net'].upper(), '#ffffff', w * .5, w * .04, bold=False)


def t_stripes(im, b, v):
    bg, fg, acc, ink, w, h = _p(im, b, v)
    d = ImageDraw.Draw(im); n = 9
    for k in range(n):
        if k % 2: d.rectangle((k * w / n, 0, (k + 1) * w / n, h), fill=mix(bg, '#ffffff', .14))
    A.rrect(im, (w * .08, h * .12, w * .92, h * .42), int(w * .05), ink)
    T(im, w / 2, h * .235, b['name'], b['font'], vis(acc, ink), w * .74, h * .13)
    L.rule(im, w * .22, w * .78, h * .325, fg, 3)
    SP(im, w / 2, h * .365, b['sub'], fg, w * .74, w * .042, bold=False)
    food(im, b['food'], w / 2, h * .62, w * .29)
    A.rrect(im, (w * .12, h * .79, w * .88, h * .87), int(h * .04), acc)
    SP(im, w / 2, h * .83, v[3], vis(ink, acc), w * .68, w * .068)
    SP(im, w / 2, h * .92, b['net'].upper(), fg, w * .5, w * .045)


def t_retro(im, b, v):
    bg, fg, acc, ink, w, h = _p(im, b, v)
    d = ImageDraw.Draw(im); m = w * .055
    d.rectangle((m, h * .07, w - m, h * .93), outline=rgb(fg), width=6); d.rectangle((m + 14, h * .07 + 14, w - m - 14, h * .93 - 14), outline=rgb(fg), width=2)
    emblem(im, b['emb'], w / 2, h * .22, w * .13, fg, bg)
    T(im, w / 2, h * .42, b['name'], b['font'], fg, w * .76, h * .1)
    d.rectangle((w * .14, h * .5, w * .86, h * .56), fill=rgb(fg))
    SP(im, w / 2, h * .53, v[3], bg, w * .66, w * .055)
    food(im, b['food'], w / 2, h * (.7 if NARROW == 1 else .66), w * .2)
    sub = b['sub'].split(' · ')
    sub = sub[:2]; step = max(h * .034, w * .036 * NARROW * 1.5)
    for i, s in enumerate(sub): SP(im, w / 2, h * .9 - step * (len(sub) - i), s, ink, w * .76, w * .036, bold=False)
    SP(im, w / 2, h * .9, b['net'].upper(), ink, w * .5, w * .036)


def t_big(im, b, v):
    bg, fg, acc, ink, w, h = _p(im, b, v)
    words = b['name'].split(' ')
    if len(words) > 1 and not cjk(b['name']):
        for i, wd in enumerate(words[:2]): T(im, w / 2, h * (.17 + i * .15), wd, b['font'], fg, w * .86, h * .13)
        y = h * .42
    else:
        T(im, w / 2, h * .22, b['name'], b['font'], fg, w * .86, h * .2); y = h * .37
    SP(im, w / 2, y, b['sub'], fg, w * .84, w * .042, bold=False)
    ImageDraw.Draw(im).rectangle((0, h * .47, w, h), fill=rgb(ink))
    ImageDraw.Draw(im).rectangle((0, h * .47, w, h * .485), fill=rgb(acc))
    food(im, b['food'], w / 2, h * .65, w * .27)
    SP(im, w / 2, h * .83, v[3], vis(acc, ink), w * .84, w * .085)
    SP(im, w / 2, h * .91, b['net'].upper(), '#ffffff', w * .5, w * .042, bold=False)


def t_badge(im, b, v):
    bg, fg, acc, ink, w, h = _p(im, b, v)
    cy, r = h * .3, w * .36
    A.ellipse(im, (w / 2 - r, cy - r, w / 2 + r, cy + r), acc); A.ellipse(im, (w / 2 - r * .9, cy - r * .9, w / 2 + r * .9, cy + r * .9), ink)
    ImageDraw.Draw(im).ellipse((w / 2 - r * .8, cy - r * .8, w / 2 + r * .8, cy + r * .8), outline=rgb(acc), width=3)
    e = b['emb'] != 'none'
    if e: emblem(im, b['emb'], w / 2, cy - r * .38, r * .26, acc, ink)
    T(im, w / 2, cy + (r * .2 if e else 0), b['name'], b['font'], '#ffffff', r * 1.36, r * (.4 if e else .6))
    SP(im, w / 2, cy + r * (.58 if e else .5), b['sub'], acc, r * 1.0, w * .034, bold=False)
    food(im, b['food'], w / 2, h * .67, w * .26)
    SP(im, w / 2, h * .83, v[3], acc, w * .84, w * .08)
    L.rule(im, w * .3, w * .7, h * .875, fg, 3)
    SP(im, w / 2, h * .91, b['net'].upper(), fg, w * .5, w * .042, bold=False)


def t_window(im, b, v):
    bg, fg, acc, ink, w, h = _p(im, b, v)
    T(im, w / 2, h * .15, b['name'], b['font'], fg, w * .84, h * .13)
    SP(im, w / 2, h * .25, b['sub'], fg, w * .84, w * .042, bold=False)
    A.rrect(im, (w * .1, h * .31, w * .9, h * .72), int(w * .08), mix(bg, '#ffffff', .88))
    food(im, b['food'], w / 2, h * .515, w * .29)
    A.rrect(im, (w * .16, h * .76, w * .84, h * .84), int(h * .04), acc)
    SP(im, w / 2, h * .8, v[3], ink if sum(rgb(acc)) > 380 else '#ffffff', w * .6, w * .065)
    SP(im, w / 2, h * .9, b['net'].upper(), fg, w * .5, w * .045)


def t_triangle(im, b, v):
    bg, fg, acc, ink, w, h = _p(im, b, v)
    A.poly(im, [(w / 2, h * .08), (w * .97, h * .6), (w * .03, h * .6)], ink)
    A.poly(im, [(w / 2, h * .135), (w * .89, h * .575), (w * .11, h * .575)], bg)
    A.poly(im, [(w * .02, h * .34), (w * .98, h * .31), (w * .98, h * .45), (w * .02, h * .48)], ink)
    T(im, w / 2, h * .393, b['name'], b['font'], acc, w * .86, h * .1, rotate=1.8)
    emblem(im, b['emb'], w / 2, h * .235, w * .075, acc, bg)
    food(im, b['food'], w / 2, h * .7, w * .26)
    SP(im, w / 2, h * .845, v[3], fg, w * .86, w * .085)
    SP(im, w / 2, h * .915, b['sub'] + ' · ' + b['net'].upper(), fg, w * .86, w * .04, bold=False)


def t_asian(im, b, v):
    bg, fg, acc, ink, w, h = _p(im, b, v)
    d = ImageDraw.Draw(im)
    A.ellipse(im, (w * .08, h * .1, w * .72, h * .1 + w * .64), mix(bg, '#ffffff', .2))
    food(im, b['food'], w * .4, h * .1 + w * .32, w * .25)
    d.rectangle((w * .76, 0, w * .95, h * .7), fill=rgb(ink))
    name = b['name']
    if cjk(name):
        n = len(name); ch = min(w * .15, h * .6 / n)
        f = A.font('cjk', int(ch))
        for i, c in enumerate(name): d.text((w * .855, h * .08 + ch * .6 + i * ch * 1.12), c, font=f, fill=rgb(acc), anchor='mm')
    else:
        T(im, w * .855, h * .35, name, b['font'], acc, h * .62, w * .13, rotate=90)
    T(im, w * .4, h * .68, v[3], 'inter_black', fg, w * .6, h * .1)
    SP(im, w * .42, h * .77, b['sub'], fg, w * .68, w * .05, bold=False)
    d.rectangle((0, h * .83, w, h * .9), fill=rgb(acc))
    SP(im, w / 2, h * .865, (v[2] + ' · ' + b['net']).upper(), ink, w * .86, w * .042)


TPL = {k[2:]: f for k, f in globals().items() if k.startswith('t_')}


def generic(im, b, v):
    global NARROW
    NARROW = 1.7 if im.size[0] / im.size[1] < .5 else 1.0
    TPL[b['tpl']](im, b, v)


L.GENERIC = generic


def make(b, v):
    sh = b['shape']
    if sh not in ('carton', 'cup'): return L.make(b, v)
    bg, fg, acc, ink = v[4]
    S = 1024 * K
    im = Image.new('RGB', (S, S), rgb(bg)); d = ImageDraw.Draw(im)
    if sh == 'carton':
        # v .2..1 the sides: front u 0..0.3, right .3..0.5, back .5..0.8, left .8..1; top u 0..0.3 v 0..0.2; bottom u .3..0.6. Each face drawn in true proportion.
        W, D, H = b['size']; hh = int(S * .8); fw = int(hh * W / H); sw = max(int(hh * D / H), 40)
        fr = Image.new('RGB', (fw, hh), rgb(bg)); L.draw_front(fr, b, v); im.paste(fr.resize((int(S * .3), hh), Image.LANCZOS), (0, 0))
        bk = Image.new('RGB', (fw, hh), rgb(bg))
        bp = L.back_panel(b, v, int(fw * .86), int(hh * .86)); bk.paste(bp, (int(fw * .07), int(hh * .07)))
        im.paste(bk.resize((int(S * .3), hh), Image.LANCZOS), (int(S * .5), 0))
        for x0 in (.3, .8):
            sd = Image.new('RGB', (sw, hh), rgb(ink))
            T(sd, sw / 2, hh / 2, b['name'], b['font'], vis(acc, ink), hh * .6, sw * .55, rotate=90 if not cjk(b['name']) else 0)
            im.paste(sd.resize((int(S * .2) + 1, hh), Image.LANCZOS), (int(S * x0), 0))
        tw = int(S * .3); th = S - hh
        tp = Image.new('RGB', (int(th * W / D), th), rgb(ink)); T(tp, tp.size[0] / 2, th / 2, b['name'], b['font'], '#ffffff', tp.size[0] * .7, th * .5)
        im.paste(tp.resize((tw, th), Image.LANCZOS), (0, hh))
        d.rectangle((tw, hh, 2 * tw, S), fill=mix(ink, '#808080', .25))
        d.rectangle((2 * tw, hh, S, S), fill=mix(ink, '#808080', .25))
    else:
        # cup: wrap in the top 70% (front centred at u=.25), foil lid a circle at uv (.15,.15) r .14
        hh = int(S * .7); R, H = b['size']
        pw = int(hh * (2 * math.pi * R * .5) / H)
        fr = Image.new('RGB', (int(hh * .72), hh), rgb(bg)); L.draw_front(fr, b, v)
        fr = fr.resize((int(S / 2 * hh * .72 / pw), hh), Image.LANCZOS); im.paste(fr, (S // 4 - fr.size[0] // 2, 0))
        bp = L.back_panel(b, v, int(pw * .8), int(hh * .86)).resize((int(S * .4), int(hh * .86)), Image.LANCZOS)
        im.paste(bp, (int(S * .55), int(hh * .07)))
        d.rectangle((0, hh, S, S), fill=mix(mix(bg, '#000000', .35), '#808080', .2))
        cx, cy, r = S * .15, S * .85, S * .14
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=rgb(bg))
        lid = Image.new('RGB', (int(r * 2), int(r * 2)), rgb(bg)); T(lid, r, r, b['name'], b['font'], fg, r * 1.4, r * .5)
        lid = lid.rotate(L.LIDROT); mask = Image.new('L', lid.size, 0); ImageDraw.Draw(mask).ellipse((0, 0, lid.size[0] - 1, lid.size[1] - 1), fill=255)
        im.paste(lid, (int(cx - r), int(cy - r)), mask)
    os.makedirs(L.OUT, exist_ok=True)
    L.X.finish(im.resize((1024, 1024), Image.LANCZOS)).save(os.path.join(L.OUT, b['id'] + v[0] + '.png'))


if __name__ == '__main__':
    only = sys.argv[1:]
    for b in BRANDS:
        if only and b['id'] not in only: continue
        for v in b['var']: make(b, v)
        print('labels', b['id'], len(b['var']))

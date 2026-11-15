"""Top view of brewed noodles filling the whole bowl: a square picture whose inscribed circle is the food's surface.
python3 food_top.py -> patches the food disc of every brewed texture in ../tex (bowl sheets and cup sheets)."""
import math, random, os, sys
from PIL import Image, ImageDraw, ImageFilter, ImageChops
from art import rgb, mix
from catalog import NOODLES

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(HERE, '..', 'tex')


def strand(rnd, c, R, step, n):
    """A wandering curly strand kept inside radius R."""
    a = rnd.random() * 2 * math.pi
    rr = R * math.sqrt(rnd.random()) * .95
    x, y = c + rr * math.cos(a), c + rr * math.sin(a)
    h = rnd.random() * 2 * math.pi
    turn = (rnd.random() - .5) * .5
    ph = rnd.random() * 6; fr = .55 + rnd.random() * .5; amp = .35 + rnd.random() * .45
    pts = []
    for i in range(n):
        pts.append((x, y))
        hh = h + amp * math.sin(ph + i * fr)
        x += step * math.cos(hh); y += step * math.sin(hh)
        h += turn * .12 + (rnd.random() - .5) * .25
        dx, dy = x - c, y - c
        d = math.hypot(dx, dy)
        if d > R * .86:
            # steer along the wall and back in
            back = math.atan2(-dy, -dx)
            diff = (back - h + math.pi) % (2 * math.pi) - math.pi
            h += diff * .35
            if d > R:
                x, y = c + dx / d * R, c + dy / d * R
    return pts


def noodle_top(S, broth, toppings, noodle_col='#f3d58a', dry=False, seed=7, thick=False):
    rnd = random.Random(seed)
    K = 2; s = S * K; c = s / 2
    im = Image.new('RGB', (s, s), rgb(broth))
    # broth: darker toward the wall, a few fat droplets
    lay = Image.new('RGBA', (s, s), (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
    for k in range(26):
        a = rnd.random() * 6.28; rr = s * .47 * math.sqrt(rnd.random()); r0 = s * (.006 + rnd.random() * .012)
        d.ellipse((c + rr * math.cos(a) - r0, c + rr * math.sin(a) - r0, c + rr * math.cos(a) + r0, c + rr * math.sin(a) + r0), outline=(255, 240, 200, 90), width=2)
    im.paste(lay, (0, 0), lay)
    R = s * (.485 if dry else .455)
    nc = rgb(noodle_col); dark = mix(noodle_col, '#6b4a16', .45); light = mix(noodle_col, '#ffffff', .35)
    w = max(3, int(s * (.027 if thick else .017)))
    step = s * (.016 if thick else .012)
    # three layers, lower ones darker: the heap has depth
    for layer, (count, shade) in enumerate([(45, .30), (50, .14), (50, 0)] if thick else [(70, .30), (80, .14), (80, 0)]):
        lay = Image.new('RGBA', (s, s), (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
        col = mix(noodle_col, '#5a3c10', shade)
        for k in range(count):
            pts = strand(rnd, c, R, step, 40 + int(rnd.random() * 50))
            d.line(pts, fill=dark + (255,), width=w + 4, joint='curve')
            d.line(pts, fill=col + (255,), width=w, joint='curve')
            if layer == 2:
                d.line([(x - w * .2, y - w * .2) for x, y in pts], fill=light + (150,), width=max(1, w // 3), joint='curve')
        im.paste(lay, (0, 0), lay)
    d = ImageDraw.Draw(im)

    def spot(rmax=.8):
        a = rnd.random() * 6.28; rr = s * .5 * rmax * math.sqrt(rnd.random())
        return c + rr * math.cos(a), c + rr * math.sin(a)

    def rot_poly(cx, cy, pts, ang, fill, outline, width=3):
        ca, sa = math.cos(ang), math.sin(ang)
        d.polygon([(cx + x * ca - y * sa, cy + x * sa + y * ca) for x, y in pts], fill=rgb(fill), outline=rgb(outline), width=width)

    u = s / 100
    for tp in toppings:
        if tp == 'egg':
            ex, ey = c + s * .17, c - s * .13
            d.ellipse((ex - 11 * u, ey - 9 * u, ex + 11 * u, ey + 9 * u), fill=rgb('#fbfaf3'), outline=rgb('#d8d2c0'), width=3)
            d.ellipse((ex - 5.5 * u, ey - 5 * u, ex + 5.5 * u, ey + 5 * u), fill=rgb('#f6a821'), outline=rgb('#e08a10'), width=3)
            d.ellipse((ex - 3 * u, ey - 3.4 * u, ex - .5 * u, ey - 1.4 * u), fill=rgb('#ffd272'))
        if tp == 'meat':
            for i in range(5):
                a = 2.2 + i * .42; rr = s * (.22 + .05 * (i % 2))
                rot_poly(c + rr * math.cos(a), c + rr * math.sin(a), [(-6 * u, -3.5 * u), (5.5 * u, -4 * u), (6.5 * u, 3 * u), (-5 * u, 4 * u)], rnd.random() * 3, '#8d4a2b', '#5d2f1a', 4)
        if tp == 'tofu':
            for i in range(3):
                a = 3.6 + i * .8; rr = s * .2
                rot_poly(c + rr * math.cos(a), c + rr * math.sin(a), [(-9 * u, -6 * u), (9 * u, -6.5 * u), (9.5 * u, 6 * u), (-8.5 * u, 6.5 * u)], rnd.random() * 3, '#e0a050', '#a86a20', 4)
        if tp == 'mushroom':
            for i in range(6):
                x, y = spot(.75)
                d.ellipse((x - 5 * u, y - 4 * u, x + 5 * u, y + 4 * u), fill=rgb('#8d6e63'), outline=rgb('#4e342e'), width=3)
                d.ellipse((x - 2.5 * u, y - 2 * u, x + 2.5 * u, y + 2 * u), fill=rgb('#a1887f'))
        if tp == 'shrimp':
            for i in range(4):
                x, y = spot(.7); a0 = rnd.random() * 360
                d.arc((x - 6 * u, y - 6 * u, x + 6 * u, y + 6 * u), a0, a0 + 250, fill=rgb('#ff8a65'), width=int(3.4 * u))
                d.arc((x - 6 * u, y - 6 * u, x + 6 * u, y + 6 * u), a0, a0 + 250, fill=rgb('#ffccbc'), width=int(1.1 * u))
        if tp == 'scallion':
            for i in range(34):
                x, y = spot(.9)
                d.ellipse((x - 1.9 * u, y - 1.9 * u, x + 1.9 * u, y + 1.9 * u), fill=rgb('#66bb6a'), outline=rgb('#2e7d32'), width=3)
                d.ellipse((x - .7 * u, y - .7 * u, x + .7 * u, y + .7 * u), fill=rgb('#c8e6c9'))
        if tp == 'chili':
            for i in range(26):
                x, y = spot(.9); a = rnd.random() * 3
                rot_poly(x, y, [(-2.2 * u, -.9 * u), (2.2 * u, -.9 * u), (2.2 * u, .9 * u), (-2.2 * u, .9 * u)], a, '#d32f2f', '#7f0000', 2)
        if tp == 'corn':
            for i in range(30):
                x, y = spot(.9)
                d.ellipse((x - 1.7 * u, y - 1.4 * u, x + 1.7 * u, y + 1.4 * u), fill=rgb('#ffd54f'), outline=rgb('#f9a825'), width=2)
        if tp == 'cheese':
            a = 1.0 + rnd.random(); rr = s * .12
            rot_poly(c + rr * math.cos(a), c + rr * math.sin(a), [(-12 * u, -12 * u), (12 * u, -12 * u), (12 * u, 12 * u), (-12 * u, 12 * u)], rnd.random(), '#ffc928', '#e09a00', 4)
            rot_poly(c + rr * math.cos(a) - 3 * u, c + rr * math.sin(a) - 3 * u, [(-7 * u, -7 * u), (5 * u, -7 * u), (-7 * u, 5 * u)], 0, '#ffe07a', '#ffe07a', 1)
        if tp == 'nori':
            for i in range(2):
                a = -.6 + i * .55; rr = s * .33
                rot_poly(c + rr * math.cos(a), c + rr * math.sin(a), [(-7 * u, -11 * u), (7 * u, -11 * u), (7 * u, 11 * u), (-7 * u, 11 * u)], a + 1.3, '#1f3a26', '#0f1f14', 3)
        if tp == 'kimchi':
            for i in range(9):
                x, y = spot(.75); a = rnd.random() * 3
                rot_poly(x, y, [(-6 * u, -2.5 * u), (0, -4 * u), (6 * u, -2 * u), (5 * u, 3 * u), (-1 * u, 4 * u), (-6 * u, 2 * u)], a, '#e2572b', '#a82a10', 3)
                rot_poly(x, y, [(-3 * u, -1 * u), (2 * u, -1.5 * u), (2 * u, 1 * u), (-3 * u, 1.2 * u)], a, '#f6c9a0', '#f6c9a0', 1)
        if tp == 'sesame':
            for i in range(70):
                x, y = spot(.92); a = rnd.random() * 3
                rot_poly(x, y, [(-1.1 * u, -.5 * u), (1.1 * u, -.5 * u), (1.1 * u, .5 * u), (-1.1 * u, .5 * u)], a, '#f5e9c8' if i % 4 else '#2a2a2a', '#8a7a50', 1)
        if tp == 'chashu':
            for i in range(2):
                a = 2.6 + i * .75; rr = s * .24
                x, y = c + rr * math.cos(a), c + rr * math.sin(a)
                d.ellipse((x - 11 * u, y - 9.5 * u, x + 11 * u, y + 9.5 * u), fill=rgb('#c98a6a'), outline=rgb('#8a4a30'), width=5)
                d.ellipse((x - 7 * u, y - 6 * u, x + 7 * u, y + 6 * u), outline=rgb('#f2d5c0'), width=5)
                d.ellipse((x - 3 * u, y - 2.5 * u, x + 3 * u, y + 2.5 * u), fill=rgb('#a86a4a'))
        if tp == 'narutomaki':
            for i in range(2):
                a = .9 + i * .6; rr = s * .26
                x, y = c + rr * math.cos(a), c + rr * math.sin(a)
                pts = [(x + (6.5 + .9 * math.cos(k * 1.57)) * u * math.cos(k * .3927), y + (6.5 + .9 * math.cos(k * 1.57)) * u * math.sin(k * .3927)) for k in range(16)]
                d.polygon(pts, fill=rgb('#fffaf5'), outline=rgb('#d8cfc8'), width=3)
                d.arc((x - 4 * u, y - 4 * u, x + 4 * u, y + 4 * u), 0, 300, fill=rgb('#f06292'), width=int(1.3 * u))
                d.arc((x - 2 * u, y - 2 * u, x + 2 * u, y + 2 * u), 120, 420, fill=rgb('#f06292'), width=int(1.1 * u))
        if tp == 'tempura':
            x, y = c - s * .12, c + s * .1
            for k in range(40):
                a = rnd.random() * 6.28; rr = 15 * u * math.sqrt(rnd.random()); r0 = (2.5 + rnd.random() * 3) * u
                d.ellipse((x + rr * math.cos(a) - r0, y + rr * math.sin(a) - r0, x + rr * math.cos(a) + r0, y + rr * math.sin(a) + r0), fill=mix('#e8b860', '#c98a30', rnd.random()), outline=rgb('#a86a1a'), width=2)
    # the wall's shadow on the food
    sh = Image.new('L', (s, s), 0)
    ImageDraw.Draw(sh).ellipse((s * .015, s * .015, s * .985, s * .985), fill=255)
    sh = sh.filter(ImageFilter.GaussianBlur(s * .025))
    dark_im = Image.new('RGB', (s, s), mix(broth, '#000000', .55))
    im = Image.composite(im, dark_im, sh)
    return im.resize((S, S), Image.LANCZOS)


def noodle_colour(n):
    base = '#9a8160' if n.get('soba') else '#f3efe2' if n.get('rice') else '#f3d58a'
    return mix(base, n['broth'], .45) if n.get('dry') else base


def food(n, S):
    return noodle_top(S, n['broth'], n['top'], noodle_colour(n), bool(n.get('dry')), sum(map(ord, n['id'])), bool(n.get('thick')))

"""Pack textures for TeaCoffeeByEMPEROR, one 1024x1024 per product, painted onto the layout of its model (tools/models.py layout.json).
python3 labels.py <out_dir> [ids...]   -> <out_dir>/textures/<Id>.png"""
import sys, os, json, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit import *
from catalog import OLD as ALL

OUT = sys.argv[1]
ONLY = sys.argv[2:]
LAY = json.load(open(os.path.join(OUT, 'layout.json')))
os.makedirs(os.path.join(OUT, 'textures'), exist_ok=True)

GOLD = (214, 178, 96, 255); GOLD_D = (150, 116, 52, 255); CREAM = (244, 234, 210, 255); INK = (28, 24, 22, 255); WHITE = (250, 250, 248, 255)
RED = (176, 32, 36, 255)


def rgba(c, a=255): return tuple(c[:3]) + (a,)


def shade(c, k): return tuple(int(max(0, min(255, v * k))) for v in c[:3]) + (255,)


def info_block(c, x, y, w, p, lines_ru, lines_en, col, size=12, f='pt-serif-400', fb='pt-serif-700'):
    """the small print on the back: name, sort, how to brew, servings. English only (the packs are sold in Canada)."""
    c.text(x + w / 2, y, p['en'].upper(), fb, size * 1.25, col, maxw=w)
    y += size * 1.9
    for t in lines_en:
        c.text(x + w / 2, y, t, f, size, col, maxw=w); y += size * 1.4
    y += size * .5
    c.text(x + w / 2, y, 'Store in a cool dry place.', f, size * .9, col, maxw=w); y += size * 1.35
    c.text(x + w / 2, y, 'Net wt see front.', f, size * .9, col, maxw=w); y += size * 1.35
    return y


def brew_lines(p):
    if p['kind'] == 'tea':
        ru = [p['sort_ru'].capitalize() + '.', 'Ложку на кружку, залить кипятком,', 'настоять 5 минут.', f'Порций: {p["servings"]}.']
        en = [p['sort_en'].capitalize() + '.', 'One spoon per cup, pour on boiling', 'water, steep 5 minutes.', f'Servings: {p["servings"]}.']
    else:
        ru = [p['sort_ru'].capitalize() + '.', 'Ложку на кружку, залить горячей', 'водой, дать отстояться.', f'Порций: {p["servings"]}.']
        en = [p['sort_en'].capitalize() + '.', 'One spoon per cup, add hot water,', 'let it settle.', f'Servings: {p["servings"]}.']
    return ru, en


# ------------------------------------------------------------------ model painters: each fills the parts of its layout
def back_fit(c, p, cx, y0, h, bc, seed, k, lines=None, col=None, wmax=300, part=.3):
    """the small print on the back of a round pack: drawn unstretched, squeezed by k, centred at cx (wraps across the seam)"""
    W = int(min(wmax, 1024 * k * part)); h = int(h); bw = int(round(W / k)); size = max(6.5, min(12, W / 25))
    strip = Canvas(W, h, bc[:3])
    ru, en = lines or brew_lines(p)
    yb = info_block(strip, W * .04, size * 1.6, W * .92, p, ru, en, col or ((42, 26, 20, 255) if .3 * bc[0] + .59 * bc[1] + .11 * bc[2] > 120 else (244, 234, 210, 255)), size)
    if h - yb > 60 and W > 150: barcode(strip, W / 2 - 60, h - 50, 120, 30, seed)
    paste_wrap(c, strip.im.resize((bw * SS, h * SS), Image.LANCZOS), cx - bw / 2, y0)
    return bw


def paint_tin(p, L, body_bg, front, lid_col, lid_top, back_col=None, metal_col=(176, 176, 180), lid_band=None, seed=1, wear=.5, native=False):
    """front(c, cx, ya, yb) draws on an unstretched canvas; native: it already knows the canvas width (c.W), else it was written for 1024"""
    c = Canvas(1024, 1024, (150, 150, 150))
    m = metal(1024, 1024, metal_col, seed)
    c.im.alpha_composite(m.resize((1024 * SS, 1024 * SS)))
    x0, y0, x1, y1 = L['body']; k = kof(L)
    def body(t, W, h):
        t.rect(0, 0, W, h, fill=body_bg)
        if native or W <= 1024: front(t, W / 2, 0, h); return
        o = Canvas(1024, h, body_bg[:3]); front(o, 512, 0, h)
        xc = int((W - 1024) / 2); tile = o.im.crop((0, 0, 160 * SS, o.im.height))     # the ground of the design repeats every 160 px: carry it on to both sides
        for xx in range(xc % 160 - 160, W, 160): t.im.paste(tile, (xx * SS, 0))
        t.im.alpha_composite(o.im, (int((W - 1024) / 2) * SS, 0))
    fit(c, (x0, y0 + 8, x1, y1 - 14), k, body)
    bc = back_col or CREAM
    back_fit(c, p, 0, y0 + 75, y1 - y0 - 150, bc, seed, k, col=INK)
    # lid side
    lx0, ly0, lx1, ly1 = L['lidside']
    c.rect(lx0, ly0, lx1, ly1, fill=lid_col)
    if lid_band: lid_band(c, ly0, ly1)
    # lid top
    cx, cy, r = L['top']
    c.circle(cx, cy, r + 4, fill=lid_col)
    lid_top(c, cx, cy, r)
    # bottom: bare metal with a stamp
    bx, by, br = L['bottom']
    c.circle(bx, by, br - 10, outline=(140, 140, 144, 255), width=3)
    c.text(bx, by, 'LOT ' + str(1000 + seed * 37 % 9000), 'DejaVuSansMono', 12, (120, 120, 124, 255))
    im = c.final()
    im = wear_edges(im, (0, y0 + 8, 1024, y1 - 14), wear, seed)
    im = texturize(im, .05, seed, 30, .02)
    return scratches(im, 60, seed)


def paint_box(p, L, faces, bg, seed=1, paper=True, back_bg=None):
    c = Canvas(1024, 1024, bg[:3])
    for k in ('front', 'back', 'left', 'right', 'top', 'bottom'):
        x0, y0, x1, y1 = L[k]
        c.rect(x0 - 3, y0 - 3, x1 + 3, y1 + 3, fill=bg if k != 'back' else (back_bg or bg))
    for k, f in faces.items():
        if f: f(c, *L[k])
    if 'back' not in faces or faces['back'] is None:
        x0, y0, x1, y1 = L['back']
        w = x1 - x0
        ru, en = brew_lines(p)
        tcol = INK if sum((back_bg or bg)[:3]) > 380 else CREAM
        info_block(c, x0 + 8, y0 + max(24, (y1 - y0) * {'pack_paper': .21, 'bag_valve': .36, 'doypack': .40, 'sack': .28}.get(p.get('model'), 0) + 10), w - 16, p, ru, en, tcol, max(8, min(13, w / 22)))
        barcode(c, x0 + w / 2 - 50, y1 - 52, 100, 28, seed)
    im = c.final()
    if paper: im = texturize(im, .06, seed, 26, .025, fibres=.04)
    return im


def seal_lines(c, x0, y0, x1, y1, col, step=5):
    y = y0
    while y < y1:
        c.line([(x0, y), (x1, y)], col, 1.2); y += step


# ------------------------------------------------------------------ pictures
def reindeer(c, x, y, s, col):
    c.ellipse(x - s * .55, y - s * .28, x + s * .5, y + s * .12, fill=col)          # body
    c.poly([(x + s * .35, y - s * .2), (x + s * .62, y - s * .55), (x + s * .75, y - s * .5), (x + s * .52, y - s * .08)], fill=col)  # neck
    c.ellipse(x + s * .6, y - s * .66, x + s * .88, y - s * .48, fill=col)          # head
    for ax in (.66, .74):
        c.line([(x + s * ax, y - s * .64), (x + s * (ax - .08), y - s * .95), (x + s * (ax - .2), y - s * 1.02)], col, s * .035)
        c.line([(x + s * (ax - .05), y - s * .8), (x + s * (ax + .06), y - s * .92)], col, s * .03)
    for lx, ph in ((-.42, 0), (-.28, 1), (.22, 1), (.36, 0)):
        c.line([(x + s * lx, y), (x + s * (lx + .06 * (1 if ph else -1)), y + s * .45)], col, s * .055)
    c.poly([(x - s * .55, y - s * .2), (x - s * .66, y - s * .3), (x - s * .6, y - s * .12)], fill=col)


def sled(c, x, y, s, col):
    c.rect(x - s * .5, y - s * .28, x + s * .45, y - s * .02, fill=col)
    c.rect(x - s * .45, y - s * .55, x + s * .25, y - s * .28, fill=col, r=s * .06)
    c.line([(x - s * .62, y + s * .08), (x + s * .6, y + s * .08), (x + s * .7, y - s * .06)], col, s * .05)
    for k in (-.35, .3): c.line([(x + s * k, y - s * .02), (x + s * k, y + s * .08)], col, s * .04)


def aurora(c, x0, x1, y, h, cols, seed=2):
    rng = random.Random(seed)
    lay = c.layer()
    for i, col in enumerate(cols):
        ph = rng.uniform(0, 6)
        for k in range(int(x0), int(x1), 3):
            t = (k - x0) / (x1 - x0)
            yy = y + math.sin(t * 5 + ph) * h * .25 + i * h * .18
            hh = h * (.5 + .5 * math.sin(t * 9 + ph * 2) ** 2)
            lay.line([(k, yy), (k, yy - hh)], rgba(col, 60), 3)
    lay.im = lay.im.filter(ImageFilter.GaussianBlur(6 * SS))
    c.over(lay)


def snowflake(c, x, y, r, col, w=2):
    for i in range(6):
        a = math.radians(60 * i)
        ex, ey = x + r * math.cos(a), y + r * math.sin(a)
        c.line([(x, y), (ex, ey)], col, w)
        for f in (.45, .7):
            mx, my = x + r * f * math.cos(a), y + r * f * math.sin(a)
            for s in (-1, 1):
                b = a + s * math.radians(45)
                c.line([(mx, my), (mx + r * .28 * math.cos(b), my + r * .28 * math.sin(b))], col, w * .8)


def locomotive(c, x, y, s, col):
    c.rect(x - s * .6, y - s * .3, x + s * .2, y, fill=col)                       # boiler
    c.circle(x - s * .6, y - s * .15, s * .15, fill=col)
    c.rect(x + s * .2, y - s * .55, x + s * .55, y, fill=col)                    # cab
    c.rect(x + s * .15, y - s * .6, x + s * .62, y - s * .52, fill=col)
    c.rect(x - s * .45, y - s * .55, x - s * .33, y - s * .3, fill=col)          # chimney
    c.poly([(x - s * .5, y - s * .55), (x - s * .28, y - s * .55), (x - s * .25, y - s * .63), (x - s * .53, y - s * .63)], fill=col)
    for wx in (-.45, -.15, .35):
        c.circle(x + s * wx, y + s * .05, s * .13, fill=col)
    c.poly([(x - s * .75, y + s * .1), (x - s * .6, y - s * .05), (x - s * .6, y + s * .1)], fill=col)
    c.line([(x - s * .8, y + s * .2), (x + s * .8, y + s * .2)], col, s * .03)


def cameo(c, cx, cy, rx, ry, bg, fg, rim):
    c.ellipse(cx - rx - 8, cy - ry - 8, cx + rx + 8, cy + ry + 8, fill=rim)
    c.ellipse(cx - rx, cy - ry, cx + rx, cy + ry, fill=bg)
    s = ry * 1.6
    # profile facing left: forehead, nose, lips, chin, neck, wig queue
    pts = [(.10, -.42), (.02, -.36), (-.02, -.28), (-.03, -.22), (-.09, -.15), (-.04, -.12), (-.05, -.08), (-.03, -.05), (-.05, -.02),
           (-.02, .03), (.0, .07), (.04, .10), (.05, .22), (.0, .34), (.32, .34), (.30, .20), (.24, .10), (.30, .02), (.34, -.12), (.32, -.30), (.24, -.42)]
    c.poly([(cx + x * s, cy + y * s) for x, y in pts], fill=fg)
    for k in range(4):  # wig curls
        c.circle(cx + .28 * s, cy + (-.22 + k * .08) * s, .045 * s, fill=fg)
    c.circle(cx - .02 * s, cy - .21 * s, .035 * s, outline=bg, width=2)        # monocle
    c.line([(cx + .01 * s, cy - .18 * s), (cx + .06 * s, cy + .02 * s)], bg, 1.5)


def bergamot(c, x, y, s, fruit, leafc):
    leaf(c, x, y, s * 1.0, s * .42, -150, leafc, shade(leafc, .7))
    leaf(c, x, y, s * .9, s * .38, -40, leafc, shade(leafc, .7))
    for dx, dy in ((-.25, .32), (.25, .36)):
        c.circle(x + dx * s, y + dy * s, s * .3, fill=fruit)
        c.circle(x + dx * s - s * .09, y + dy * s - s * .1, s * .08, fill=shade(fruit, 1.25))


def ink_mountains(c, x0, x1, base, layers, seed=4):
    rng = random.Random(seed)
    for i, (col, hmin, hmax) in enumerate(layers):
        pts = [(x0, base)]
        x = x0
        while x < x1:
            pts.append((x, base - rng.uniform(hmin, hmax)))
            x += rng.uniform(30, 70)
        pts.append((x1, base - rng.uniform(hmin, hmax))); pts.append((x1, base))
        lay = c.layer(); lay.poly(pts, fill=col); lay.im = lay.im.filter(ImageFilter.GaussianBlur(1.5 * SS)); c.over(lay)


def seal(c, x, y, s, txt, col=(190, 30, 30, 255)):
    c.rect(x - s / 2, y - s / 2, x + s / 2, y + s / 2, fill=col, r=s * .08)
    c.rect(x - s / 2 + 3, y - s / 2 + 3, x + s / 2 - 3, y + s / 2 - 3, outline=(245, 220, 200, 255), width=1.5, r=s * .06)
    n = len(txt)
    if n <= 2:
        for i, ch in enumerate(txt): c.text(x, y - s * .22 + i * s * .44, ch, CJK, s * .4, (250, 236, 220, 255))
    else:
        cols = [txt[:2], txt[2:4]]
        for j, col2 in enumerate(cols):
            for i, ch in enumerate(col2):
                c.text(x + s * .2 - j * s * .4, y - s * .2 + i * s * .4, ch, CJK, s * .36, (250, 236, 220, 255))


def cliff(c, x, y, s, rock, pine):
    c.poly([(x - s * .5, y), (x - s * .38, y - s * .55), (x - s * .2, y - s * .62), (x - s * .05, y - s * .95), (x + s * .12, y - s * 1.0),
            (x + s * .2, y - s * .7), (x + s * .35, y - s * .5), (x + s * .5, y)], fill=rock)
    for k in range(4):
        px_ = x - s * .02 + k * s * .045; py = y - s * 1.0
        c.poly([(px_, py - s * .3), (px_ - s * .07, py), (px_ + s * .07, py)], fill=pine)


def cloud(c, x, y, s, col, outline=None):
    for dx, dy, r in ((-.5, 0, .3), (-.2, -.15, .38), (.15, -.1, .33), (.45, .02, .26), (0, .1, .3)):
        c.circle(x + dx * s, y + dy * s, r * s, fill=col)
    if outline:
        for dx, dy, r in ((-.2, -.15, .38), (.15, -.1, .33)):
            c.arc(x + dx * s, y + dy * s, r * s * .55, 180, 330, outline, 2)


def mate_gourd(c, x, y, s, body, metal_c, rim):
    c.ellipse(x - s * .42, y - s * .5, x + s * .42, y + s * .4, fill=body)
    c.rect(x - s * .28, y - s * .62, x + s * .28, y - s * .44, fill=rim, r=s * .04)
    c.line([(x + s * .05, y - s * .5), (x + s * .38, y - s * 1.05)], metal_c, s * .05)
    c.rect(x + s * .32, y - s * 1.12, x + s * .46, y - s * 1.02, fill=metal_c, r=s * .03)
    for k in range(5):
        c.arc(x, y - s * .05, s * (.18 + k * .04), 200 + k * 8, 340 - k * 8, shade(body, 1.25), 2)


def hibiscus(c, x, y, s, petal, dark, stamen):
    for i in range(5):
        a = -90 + 72 * i
        leaf(c, x, y, s, s * .9, a, petal)
    for i in range(5):
        a = -90 + 72 * i + 36
        leaf(c, x, y, s * .45, s * .15, a, shade(petal, .75))
    c.circle(x, y, s * .2, fill=dark)
    c.line([(x, y), (x + s * .15, y - s * .7)], stamen, s * .05)
    for k in range(6):
        c.circle(x + s * .15 + math.cos(k) * s * .06, y - s * .72 + math.sin(k) * s * .06, s * .035, fill=(240, 200, 60, 255))


def pyramids(c, x, base, s, col):
    c.poly([(x - s, base), (x - s * .35, base - s * .7), (x + s * .3, base)], fill=col)
    c.poly([(x - s * .05, base), (x + s * .45, base - s * .5), (x + s * .95, base)], fill=shade(col, .85))


def palm(c, x, base, s, col):
    pts = [(x + math.sin(t / 10 * 1.2) * s * .15, base - t / 10 * s) for t in range(11)]
    c.line(pts, col, s * .07)
    tx, ty = pts[-1]
    for a in (-160, -125, -90, -55, -20, 200):
        leaf(c, tx, ty, s * .55, s * .16, a + 0, col)


def fireweed(c, x, base, h, petal, leafc, seed=1):
    rng = random.Random(seed)
    c.line([(x, base), (x + 4, base - h)], shade(leafc, .8), 3)
    for k in range(6):
        t = .15 + k * .09
        leaf(c, x, base - h * t, h * .22, h * .05, -150 if k % 2 else -30, leafc)
    for k in range(18):
        t = .55 + k * .025
        yy = base - h * t; xx = x + 4 * t
        side = -1 if k % 2 else 1
        fx = xx + side * h * .05 * (1 - (t - .55) * 1.8)
        sz = h * .045 * (1 - (t - .55) * 1.6)
        if sz > 1:
            for j in range(4): leaf(c, fx, yy, sz, sz * .7, 45 + 90 * j, petal)
            c.circle(fx, yy, sz * .25, fill=(250, 240, 250, 255))
    c.poly([(x + 4 - h * .02, base - h * .99), (x + 4, base - h * 1.06), (x + 4 + h * .02, base - h * .99)], fill=shade(petal, .8))


def folk_band(c, x0, x1, y, h, col, col2):
    k = x0
    while k < x1:
        cx = k + h / 2
        c.poly([(cx, y), (cx + h / 2, y + h / 2), (cx, y + h), (cx - h / 2, y + h / 2)], fill=col)
        c.poly([(cx, y + h * .25), (cx + h / 4, y + h / 2), (cx, y + h * .75), (cx - h / 4, y + h / 2)], fill=col2)
        k += h


def elephant(c, x, y, s, col, blanket, trim):
    c.ellipse(x - s * .55, y - s * .55, x + s * .4, y + s * .05, fill=col)                      # body
    c.circle(x + s * .45, y - s * .45, s * .25, fill=col)                                          # head
    c.ellipse(x + s * .2, y - s * .62, x + s * .5, y - s * .2, fill=shade(col, .85))            # ear
    c.line([(x + s * .62, y - s * .35), (x + s * .7, y - s * .05), (x + s * .62, y + s * .1), (x + s * .52, y + s * .12)], col, s * .1)  # trunk
    for lx in (-.45, -.25, .1, .28):
        c.rect(x + s * lx, y - s * .1, x + s * (lx + .14), y + s * .3, fill=col)
    c.line([(x - s * .55, y - s * .35), (x - s * .66, y - s * .1)], col, s * .03)
    c.poly([(x - s * .35, y - s * .55), (x + s * .15, y - s * .55), (x + s * .1, y - s * .12), (x - s * .3, y - s * .12)], fill=blanket)
    dots_band(c, x - s * .3, y - s * .14, x + s * .1, s * .025, trim)
    c.poly([(x + s * .62, y - s * .42), (x + s * .72, y - s * .4), (x + s * .64, y - s * .36)], fill=(250, 250, 240, 255))  # tusk


def crane(c, x, y, s, white, black, red):
    c.ellipse(x - s * .35, y - s * .12, x + s * .25, y + s * .12, fill=white)                    # body
    c.line([(x + s * .2, y - s * .05), (x + s * .45, y - s * .35), (x + s * .62, y - s * .38)], black, s * .05)   # neck
    c.circle(x + s * .62, y - s * .38, s * .05, fill=white); c.circle(x + s * .63, y - s * .42, s * .025, fill=red)
    c.line([(x + s * .66, y - s * .37), (x + s * .82, y - s * .33)], (200, 170, 80, 255), s * .02)
    c.poly([(x - s * .1, y - s * .05), (x - s * .3, y - s * .75), (x + s * .15, y - s * .2)], fill=white)   # wing up
    c.poly([(x - s * .3, y - s * .75), (x - s * .22, y - s * .6), (x - s * .1, y - s * .45), (x - s * .02, y - s * .3), (x - s * .12, y - s * .38)], fill=black)
    c.poly([(x - s * .05, y + s * .02), (x - s * .4, y + s * .5), (x + s * .1, y + s * .1)], fill=white)    # wing down
    c.line([(x - s * .3, y + s * .05), (x - s * .7, y + s * .2)], black, s * .03)
    c.line([(x - s * .28, y + s * .08), (x - s * .68, y + s * .25)], black, s * .03)


def plum_branch(c, x, y, s, wood, blossom):
    pts = [(x, y), (x + s * .3, y - s * .1), (x + s * .55, y - s * .3), (x + s * .9, y - s * .35)]
    c.line(pts, wood, s * .04)
    c.line([(x + s * .3, y - s * .1), (x + s * .4, y + s * .15)], wood, s * .025)
    for (bx, by) in ((x + s * .2, y - s * .08), (x + s * .5, y - s * .3), (x + s * .75, y - s * .36), (x + s * .4, y + s * .12), (x + s * .9, y - s * .33)):
        for j in range(5): leaf(c, bx, by, s * .06, s * .06, 72 * j, blossom)
        c.circle(bx, by, s * .015, fill=(230, 180, 60, 255))


def skipper(c, x, y, s, skin, beard, cap, trim, dark):
    c.circle(x, y, s * .38, fill=skin)
    c.poly([(x - s * .38, y - s * .02), (x - s * .4, y + s * .25), (x - s * .2, y + s * .5), (x + s * .2, y + s * .5), (x + s * .4, y + s * .25),
            (x + s * .38, y - s * .02), (x + s * .2, y + s * .15), (x - s * .2, y + s * .15)], fill=beard)
    c.poly([(x - s * .45, y - s * .22), (x + s * .45, y - s * .22), (x + s * .38, y - s * .5), (x - s * .38, y - s * .5)], fill=cap)
    c.rect(x - s * .48, y - s * .26, x + s * .48, y - s * .18, fill=dark)
    c.circle(x, y - s * .38, s * .07, fill=trim)
    c.circle(x - s * .13, y - s * .05, s * .035, fill=dark); c.circle(x + s * .13, y - s * .05, s * .035, fill=dark)
    c.line([(x - s * .2, y + s * .2), (x + s * .2, y + s * .2)], shade(beard, .7), s * .04)
    c.line([(x + s * .1, y + s * .22), (x + s * .38, y + s * .3)], dark, s * .04)                     # pipe stem
    c.rect(x + s * .34, y + s * .2, x + s * .46, y + s * .36, fill=dark, r=s * .02)
    steam(c, x + s * .4, y + s * .18, s * .4, rgba(trim, 160), 2, 2, s * .08)


def wheel(c, x, y, r, col, w=6):
    c.circle(x, y, r, outline=col, width=w)
    c.circle(x, y, r * .25, outline=col, width=w * .8)
    for i in range(8):
        a = math.radians(45 * i)
        c.line([(x + r * .25 * math.cos(a), y + r * .25 * math.sin(a)), (x + r * 1.25 * math.cos(a), y + r * 1.25 * math.sin(a))], col, w * .8)
        c.circle(x + r * 1.28 * math.cos(a), y + r * 1.28 * math.sin(a), w * 1.1, fill=col)


def cup_steam(c, x, y, s, cupc, coffee, steamc):
    c.poly([(x - s * .45, y - s * .3), (x + s * .45, y - s * .3), (x + s * .35, y + s * .25), (x - s * .35, y + s * .25)], fill=cupc)
    c.ellipse(x - s * .45, y - s * .38, x + s * .45, y - s * .22, fill=cupc)
    c.ellipse(x - s * .4, y - s * .36, x + s * .4, y - s * .24, fill=coffee)
    c.arc(x + s * .45, y - s * .05, s * .17, -80, 80, cupc, s * .07)
    c.ellipse(x - s * .7, y + s * .2, x + s * .7, y + s * .36, fill=cupc)
    steam(c, x, y - s * .42, s * .55, steamc, s * .05, 3, s * .22)


def volcano(c, x, base, s, rock, lava, glow):
    lay = c.layer(); lay.circle(x, base - s * .95, s * .5, fill=rgba(glow, 120)); lay.im = lay.im.filter(ImageFilter.GaussianBlur(25 * SS)); c.over(lay)
    c.poly([(x - s * 1.1, base), (x - s * .18, base - s * .9), (x + s * .18, base - s * .9), (x + s * 1.1, base)], fill=rock)
    for k, (dx, L) in enumerate(((-.1, .5), (.05, .7), (.12, .35))):
        c.poly([(x + dx * s - s * .04, base - s * .9), (x + dx * s + s * .04, base - s * .9), (x + dx * s * 2.5, base - s * (.9 - L)), (x + dx * s * 2.4 - s * .04, base - s * (.9 - L * .7))], fill=lava)
    for k in range(9):
        a = math.radians(-90 + (k - 4) * 14)
        c.line([(x, base - s * .95), (x + math.cos(a) * s * .6, base - s * .95 + math.sin(a) * s * .6)], lava, s * .025)
    for k in range(6):
        cloud(c, x + (k - 2.5) * s * .12, base - s * (1.55 + (k % 2) * .12), s * .22, (40, 36, 36, 255))


def troika(c, x, y, s, col):
    for k, dx in enumerate((-.42, 0, .42)):
        hx = x + dx * s; hy = y + (0 if k == 1 else s * .06)
        c.poly([(hx - s * .1, hy + s * .3), (hx - s * .12, hy - s * .1), (hx - s * .04, hy - s * .38), (hx + s * .1, hy - s * .5),
                (hx + s * .2, hy - s * .42), (hx + s * .16, hy - s * .34), (hx + s * .06, hy - s * .3), (hx + s * .1, hy + s * .3)], fill=col)
        c.poly([(hx - s * .04, hy - s * .38), (hx - s * .14, hy - s * .3), (hx - s * .12, hy - s * .1)], fill=shade(col, .7))  # mane
    c.arc(x, y - s * .55, s * .28, 200, 340, col, s * .05)   # the duga over the middle horse
    c.circle(x, y - s * .85, s * .05, fill=(240, 200, 60, 255))


def three(c, x, y, s, brown, cream):
    """3 in 1: a coffee bean, a sugar cube and a drop of cream, joined by plus signs."""
    bx = x - s * 1.25
    c.ellipse(bx - s * .32, y - s * .45, bx + s * .32, y + s * .45, fill=brown)
    c.line([(bx, y - s * .4), (bx - s * .08, y), (bx, y + s * .4)], (246, 204, 56, 255), s * .07)
    c.poly([(x - s * .35, y - s * .15), (x, y - s * .38), (x + s * .35, y - s * .15), (x, y + .08 * s)], fill=(255, 255, 255, 255))
    c.poly([(x - s * .35, y - s * .15), (x, y + .08 * s), (x, y + s * .42), (x - s * .35, y + s * .2)], fill=(222, 222, 226, 255))
    c.poly([(x + s * .35, y - s * .15), (x, y + .08 * s), (x, y + s * .42), (x + s * .35, y + s * .2)], fill=(200, 200, 206, 255))
    dx = x + s * 1.25
    c.circle(dx, y + s * .12, s * .3, fill=cream)
    c.poly([(dx - s * .26, y), (dx, y - s * .5), (dx + s * .26, y)], fill=cream)
    for px_ in (x - s * .62, x + s * .62):
        c.line([(px_ - s * .14, y), (px_ + s * .14, y)], brown, s * .07); c.line([(px_, y - s * .14), (px_, y + s * .14)], brown, s * .07)


def lighthouse(c, x, base, s, ink, bg):
    c.poly([(x - s * .18, base), (x - s * .1, base - s * .9), (x + s * .1, base - s * .9), (x + s * .18, base)], fill=None, outline=ink, width=4)
    for k in range(3):
        y0 = base - s * (.15 + k * .25)
        c.line([(x - s * (.17 - k * .025), y0), (x + s * (.17 - k * .025), y0)], ink, 3)
    c.rect(x - s * .13, base - s * 1.08, x + s * .13, base - s * .9, outline=ink, width=4)
    c.poly([(x - s * .16, base - s * 1.08), (x, base - s * 1.22), (x + s * .16, base - s * 1.08)], fill=ink)
    for a in (-20, -8, 8, 20):
        r = math.radians(a)
        c.line([(x + s * .16, base - s * 1.0), (x + s * .16 + s * .9 * math.cos(r), base - s * 1.0 + s * .9 * math.sin(r))], ink, 2)
        c.line([(x - s * .16, base - s * 1.0), (x - s * .16 - s * .9 * math.cos(r), base - s * 1.0 + s * .9 * math.sin(r))], ink, 2)
    for k in range(5):
        c.arc(x - s * .4 + k * s * .2, base + s * .08, s * .1, 200, 340, ink, 3)


def coffee_branch(c, x, y, s, leafc, cherry):
    c.line([(x - s * .6, y + s * .2), (x + s * .6, y - s * .2)], shade(leafc, .6), s * .03)
    for k in range(5):
        t = -.5 + k * .25
        bx, by = x + t * s, y - t * s * .33
        leaf(c, bx, by, s * .35, s * .14, -60 + (k % 2) * -60, leafc, shade(leafc, .7))
        for j in range(3):
            c.circle(bx + (j - 1) * s * .05, by + s * .06, s * .045, fill=cherry)


def llama(c, x, base, s, col):
    c.ellipse(x - s * .4, base - s * .55, x + s * .25, base - s * .25, fill=col)
    c.rect(x + s * .1, base - s * 1.0, x + s * .24, base - s * .35, fill=col)
    c.ellipse(x + s * .08, base - s * 1.1, x + s * .38, base - s * .95, fill=col)
    c.poly([(x + s * .12, base - s * 1.05), (x + s * .14, base - s * 1.22), (x + s * .19, base - s * 1.06)], fill=col)
    for lx in (-.32, -.18, .05, .17): c.rect(x + s * lx, base - s * .3, x + s * (lx + .07), base, fill=col)


def cezve(c, x, y, s, copper, handle, coffee):
    c.poly([(x - s * .3, y + s * .45), (x + s * .3, y + s * .45), (x + s * .2, y - s * .05), (x + s * .26, y - s * .35), (x - s * .3, y - s * .35), (x - s * .2, y - s * .05)], fill=copper)
    c.poly([(x - s * .3, y - s * .35), (x - s * .42, y - s * .45), (x - s * .26, y - s * .42)], fill=copper)
    c.line([(x + s * .25, y - s * .22), (x + s * 1.05, y - s * .5)], handle, s * .07)
    c.ellipse(x - s * .28, y - s * .4, x + s * .26, y - s * .3, fill=coffee)
    for k in range(3):
        c.line([(x - s * .25, y + s * (.05 + k * .12)), (x + s * .25, y + s * (.05 + k * .12))], shade(copper, 1.2), 2)


def crescent(c, x, y, r, col, bg):
    c.circle(x, y, r, fill=col); c.circle(x + r * .35, y - r * .1, r * .85, fill=bg)
    star(c, x + r * .55, y, r * .3, col)


def arabesque_band(c, x0, x1, y, h, col):
    k = x0
    while k < x1:
        c.arc(k + h / 2, y + h / 2, h * .45, 180, 360, col, 2)
        c.arc(k + h, y + h / 2, h * .45, 0, 180, col, 2)
        c.circle(k + h * .75, y + h / 2, h * .08, fill=col)
        k += h


def chicory(c, x, y, s, petal, centre):
    for i in range(14):
        a = 360 * i / 14
        r = math.radians(a)
        L = s
        pts = [(x, y), (x + L * math.cos(r - .12), y + L * math.sin(r - .12)), (x + L * 1.02 * math.cos(r), y + L * 1.02 * math.sin(r)), (x + L * math.cos(r + .12), y + L * math.sin(r + .12))]
        c.poly(pts, fill=petal)
        c.line([(x + L * .35 * math.cos(r), y + L * .35 * math.sin(r)), (x + L * .9 * math.cos(r), y + L * .9 * math.sin(r))], shade(petal, .8), 1.5)
    c.circle(x, y, s * .18, fill=centre)
    for k in range(10):
        a = math.radians(36 * k)
        c.circle(x + s * .14 * math.cos(a), y + s * .14 * math.sin(a), s * .03, fill=(30, 50, 140, 255))


def icebreaker(c, x, base, s, hull, deck, ice, sea):
    c.poly([(x - s, base - s * .05), (x + s * .55, base - s * .05), (x + s * .95, base - s * .35), (x + s * .5, base + s * .12), (x - s * .95, base + s * .12)], fill=hull)
    c.rect(x - s * .7, base - s * .38, x + s * .2, base - s * .05, fill=deck)
    c.rect(x - s * .55, base - s * .6, x - s * .05, base - s * .38, fill=deck)
    for k in range(5): c.rect(x - s * .5 + k * s * .09, base - s * .55, x - s * .45 + k * s * .09, base - s * .48, fill=hull)
    c.rect(x - s * .35, base - s * .82, x - s * .22, base - s * .6, fill=hull)
    c.rect(x - s * .37, base - s * .86, x - s * .2, base - s * .8, fill=(200, 40, 40, 255))
    c.line([(x + s * .05, base - s * .38), (x + s * .05, base - s * .9)], deck, 3)
    rng = random.Random(3)
    for k in range(9):
        fx = x + s * (.6 + k * .14) + rng.uniform(-8, 8); fy = base + rng.uniform(-6, 18)
        w = s * rng.uniform(.08, .2)
        c.poly([(fx - w, fy), (fx - w * .6, fy - w * .45), (fx + w * .7, fy - w * .35), (fx + w, fy + w * .1)], fill=ice)
    c.rect(x - s * 1.2, base + s * .12, x + s * 2, base + s * .3, fill=sea)


# ------------------------------------------------------------------ the products
def T_PolarCaravan(p, L):
    navy = (22, 34, 66, 255)
    def front(c, cx, y0, y1):
        x0, x1 = cx - 250, cx + 250
        c.rect(0, y0, 1024, y1, fill=navy)
        aurora(c, x0 - 60, x1 + 60, y0 + 230, 170, [(80, 220, 160), (60, 180, 220), (160, 120, 230)], 5)
        for k in range(26):
            rr = random.Random(k)
            c.circle(rr.uniform(x0 - 60, x1 + 60), rr.uniform(y0 + 20, y0 + 220), rr.uniform(1, 2.6), fill=(240, 240, 255, 220))
        # the whole caravan stands on the snow: on the sky it would vanish (same navy)
        hill = [(x0 - 80, y1 - 215), (cx - 150, y1 - 262), (cx + 40, y1 - 250), (cx + 190, y1 - 268), (x1 + 80, y1 - 222), (x1 + 80, y1), (x0 - 80, y1)]
        c.poly(hill, fill=(222, 230, 240, 255))
        # they go to the right: the sled behind, the reindeer pulling it
        sled(c, cx - 145, y1 - 152, 70, navy)
        c.line([(cx - 100, y1 - 170), (cx + 140, y1 - 183)], rgba(navy, 220), 2.5)
        for i, xx in enumerate((cx - 40, cx + 50, cx + 140)):
            reindeer(c, xx, y1 - 177 + (i % 2) * 3, 60, navy)
        border(c, x0, y0 + 14, x1, y1 - 14, GOLD, 3, 8)
        c.text(cx, y1 - 96, 'POLAR', 'ruslan-display-400', 64, GOLD, maxw=440)
        c.text(cx, y1 - 42, 'CARAVAN', 'ruslan-display-400', 56, GOLD, maxw=440)
        c.text(cx, y0 + 40, 'ПОЛЯРНЫЙ КАРАВАНЪ · BLACK TEA', 'pt-serif-700', 15, GOLD, maxw=470)
    def lid_band(c, a, b):
        greek_band(c, 0, a + 30, 1024, 26, GOLD)
    def lid_top(c, cx, cy, r):
        c.circle(cx, cy, r - 6, outline=GOLD, width=4)
        star(c, cx, cy, r * .62, GOLD, 8, .35, -90)
        c.circle(cx, cy, r * .14, fill=navy)
        c.text_arc(cx, cy, r * .82, 'POLAR CARAVAN', 'pt-serif-700', 15, GOLD, -90, 1)
    return paint_tin(p, L, navy, front, navy, lid_top, lid_band=lid_band, metal_col=(190, 168, 110), seed=11)


def T_MorningWhistle(p, L):
    cream = (246, 236, 214, 255); red = (184, 38, 34, 255)
    def front(c, x0, y0, x1, y1):
        w, h = x1 - x0, y1 - y0; cx = (x0 + x1) / 2
        c.rect(x0, y0, x1, y1, fill=cream)
        c.circle(cx + w * .18, y0 + h * .62, h * .34, fill=(246, 170, 60, 255))
        sun_rays(c, cx + w * .18, y0 + h * .62, h * .38, h * .7, 14, (240, 190, 90, 255))
        c.rect(x0, y0 + h * .62, x1, y1, fill=cream)
        steam(c, cx - w * .2, y0 + h * .56, h * .45, (255, 255, 255, 230), 10, 3, 24)
        locomotive(c, cx - w * .05, y0 + h * .78, h * .42, (40, 30, 30, 255))
        c.rect(x0, y0, x1, y0 + 6, fill=red); c.rect(x0, y1 - 6, x1, y1, fill=red)
        c.text(x0 + 12, y0 + 30, 'MORNING', 'abril-fatface-400', h * .2, red, anchor='lm', maxw=w * .55)
        c.text(x0 + 14, y0 + 30 + h * .2, 'WHISTLE', 'abril-fatface-400', h * .2, red, anchor='lm', maxw=w * .55)
        c.text(x1 - 12, y1 - h * .11, 'Morning Whistle · black tea · 10 bags', 'pt-serif-700', h * .07, (60, 40, 30, 255), anchor='rm', maxw=w * .9)
    def side(c, x0, y0, x1, y1):
        c.rect(x0, y0, x1, y1, fill=red)
        c.text((x0 + x1) / 2, (y0 + y1) / 2 - 12, 'BLACK TEA', 'oswald-600', (x1 - x0) * .22, cream)
        c.text((x0 + x1) / 2, (y0 + y1) / 2 + 18, 'BLACK TEA', 'oswald-600', (x1 - x0) * .18, cream)
    def top(c, x0, y0, x1, y1):
        c.rect(x0, y0, x1, y1, fill=red)
        c.text((x0 + x1) / 2, (y0 + y1) / 2, 'MORNING WHISTLE', 'abril-fatface-400', (y1 - y0) * .32, cream, maxw=(x1 - x0) * .9)
        c.line([(x0 + 20, y0 + 10), (x1 - 20, y0 + 10)], rgba(cream, 160), 1)
    return paint_box(p, L, dict(front=front, left=side, right=side, top=top, bottom=None, back=None), cream, 21)


def T_CountBergamot(p, L):
    blue = (64, 76, 138, 255); silver = (210, 212, 222, 255)
    def front(c, cx, y0, y1):
        c.rect(0, y0, 1024, y1, fill=blue)
        for k in range(0, 1024, 32):
            c.line([(k, y0), (k, y1)], rgba(shade(blue, 1.12), 255), 10)
        c.rect(cx - 230, y0 + 20, cx + 230, y1 - 20, fill=shade(blue, .9))
        border(c, cx - 230, y0 + 20, cx + 230, y1 - 20, silver, 3, 9)
        cameo(c, cx, y0 + 210, 110, 140, (232, 230, 240, 255), blue, silver)
        bergamot(c, cx - 150, y0 + 330, 70, (210, 196, 60, 255), (60, 120, 70, 255))
        bergamot(c, cx + 150, y0 + 330, 70, (210, 196, 60, 255), (60, 120, 70, 255))
        c.text(cx, y0 + 410, 'Count Bergamot', 'great-vibes-400', 72, silver, maxw=430)
        c.text(cx, y0 + 470, 'EARL GREY BLEND', 'old-standard-tt-700', 26, silver, spacing=3, maxw=420)
        c.text(cx, y0 + 505, 'BLACK TEA WITH OIL OF BERGAMOT', 'cinzel-700', 15, silver, maxw=420)
    def lid_band(c, a, b): dots_band(c, 0, (a + b) / 2, 1024, 5, silver, 22)
    def lid_top(c, cx, cy, r):
        c.circle(cx, cy, r - 8, outline=silver, width=3)
        c.text(cx, cy - 8, 'CB', 'great-vibes-400', r * .9, silver)
        c.text(cx, cy + r * .55, '· 1874 ·', 'cinzel-700', 14, silver)
    return paint_tin(p, L, blue, front, blue, lid_top, lid_band=lid_band, metal_col=(200, 200, 206), seed=12)


def T_JadeRiver(p, L):
    jade = (44, 120, 92, 255); paper = (238, 234, 220, 255)
    def front(c, x0, y0, x1, y1):
        w, h = x1 - x0, y1 - y0; cx = (x0 + x1) / 2
        c.rect(x0, y0, x1, y1, fill=paper)
        ink_mountains(c, x0, x1, y0 + h * .62, [((150, 170, 160, 255), h * .22, h * .36), ((100, 130, 120, 255), h * .12, h * .25), ((50, 80, 70, 255), h * .04, h * .12)], 7)
        pts = [(x0, y0 + h * .66), (cx - w * .1, y0 + h * .63), (cx + w * .2, y0 + h * .68), (x1, y0 + h * .64), (x1, y0 + h * .72), (cx + w * .1, y0 + h * .75), (x0, y0 + h * .7)]
        c.poly(pts, fill=(120, 180, 160, 255))
        c.rect(x0, y0 + h * .74, x1, y1, fill=jade)
        c.text(cx, y0 + h * .82, 'JADE RIVER', 'tenor-sans-400', w * .17, paper, spacing=3, maxw=w * .9)
        c.text(cx, y0 + h * .9, 'GREEN TEA · LOOSE LEAF', 'tenor-sans-400', w * .065, paper, maxw=w * .9)
        c.text(cx, y0 + h * .955, 'Jade River', 'pt-serif-700', w * .07, paper, maxw=w * .9)
        seal(c, x1 - w * .17, y0 + h * .14, w * .2, '翡翠')
        for i, ch in enumerate('綠茶'):
            c.text(x0 + w * .14, y0 + h * .1 + i * w * .16, ch, CJK, w * .14, INK)
    def side(c, x0, y0, x1, y1):
        c.rect(x0, y0, x1, y1, fill=jade)
        for i, ch in enumerate('翡翠江綠茶'):
            c.text((x0 + x1) / 2, y0 + 40 + i * (x1 - x0) * .6, ch, CJK, (x1 - x0) * .5, paper)
    def top(c, x0, y0, x1, y1):
        c.rect(x0, y0, x1, y1, fill=jade); c.text((x0 + x1) / 2, (y0 + y1) / 2, 'JADE RIVER', 'tenor-sans-400', (y1 - y0) * .35, paper, maxw=(x1 - x0) * .9)
    return paint_box(p, L, dict(front=front, left=side, right=side, top=top, bottom=None, back=None), paper, 22, back_bg=paper)


def T_Yunling(p, L):
    paper = (236, 230, 214, 255); red = (186, 36, 40, 255); blue = (40, 60, 120, 255)
    c = Canvas(1024, 1024, paper[:3])
    cx, cy, r = L['top']
    c.circle(cx, cy, r + 10, fill=paper)
    c.circle(cx, cy, r * .93, outline=red, width=4); c.circle(cx, cy, r * .86, outline=red, width=1.5)
    c.text_arc(cx, cy, r * .76, '雲嶺古樹普洱茶 · 雲南七子餅茶', CJK, 30, red, -90, 4)
    c.text_arc(cx, cy, r * .76, 'YUNLING · PU-ERH TEA CAKE · 357 G', 'cinzel-700', 18, blue, 90, 2, inside=True)
    c.circle(cx, cy, r * .5, outline=red, width=3)
    for i, ch in enumerate('普洱'):
        c.text(cx - r * .13 + i * r * .26, cy - r * .2, ch, CJK, r * .24, red)
    c.text(cx, cy + r * .1, '雲嶺', CJK, r * .2, blue)
    seal(c, cx, cy + r * .33, r * .16, '茶', red)
    for k in range(12):
        a = math.radians(30 * k)
        c.circle(cx + r * .64 * math.cos(a), cy + r * .64 * math.sin(a), 5, fill=red)
    bx, by, br = L['bottom']
    c.circle(bx, by, br + 10, fill=paper)
    for k in range(32):  # the paper gathered under the cake
        a = math.radians(360 * k / 32)
        c.line([(bx + br * .12 * math.cos(a), by + br * .12 * math.sin(a)), (bx + br * .98 * math.cos(a + .1), by + br * .98 * math.sin(a + .1))], (200, 192, 172, 255), 5)
    c.circle(bx, by, br * .14, fill=(214, 206, 188, 255))
    c.text(bx, by + br * .5, '雲嶺茶廠', CJK, 26, rgba(red, 160))
    im = texturize(c.final(), .07, 23, 40, .03, fibres=.10)
    return im


def T_CloudCliff(p, L):
    teal = (24, 102, 112, 255); gold = (222, 190, 110, 255)
    def front(c, cx, y0, y1):
        c.rect(0, y0, 1024, y1, fill=teal)
        h = y1 - y0
        cliff(c, cx + 90, y1 - 70, 240, (40, 60, 64, 255), (20, 40, 34, 255))
        for k, (dx, dy, s) in enumerate(((-150, 150, 80), (-40, 90, 60), (160, 200, 70), (-200, 300, 90), (60, 320, 110))):
            cloud(c, cx + dx, y0 + dy, s, (236, 240, 238, 255), teal)
        c.rect(cx - 250, y1 - 92, cx + 250, y1 - 18, fill=shade(teal, .7))
        c.text(cx, y1 - 64, 'CLOUD CLIFF', 'cinzel-900', 46, gold, spacing=4, maxw=480)
        c.text(cx, y1 - 30, 'OOLONG · 烏龍茶', CJK, 18, gold, maxw=470)
        c.text(cx - 160, y0 + 48, 'Cloud Cliff', 'philosopher-700', 30, gold, maxw=300)
    def lid_band(c, a, b): zigzag(c, 0, a + 30, 1024, 40, gold, 22, 3)
    def lid_top(c, cx, cy, r):
        cloud(c, cx, cy + 6, r * .55, gold); c.circle(cx, cy, r - 8, outline=gold, width=3)
        c.text(cx, cy + r * .6, '烏龍', CJK, 24, gold)
    return paint_tin(p, L, teal, front, teal, lid_top, lid_band=lid_band, metal_col=(190, 170, 120), seed=13)


def T_PampaVerde(p, L):
    yel = (238, 202, 62, 255); grn = (30, 110, 54, 255)
    def front(c, x0, y0, x1, y1):
        w, h = x1 - x0, y1 - y0; cx = (x0 + x1) / 2
        c.rect(x0, y0, x1, y1, fill=yel)
        c.circle(cx, y0 + h * .45, w * .3, fill=(250, 226, 120, 255))
        sun_rays(c, cx, y0 + h * .45, w * .32, w * .52, 18, (250, 220, 110, 255), None, 0, 360)
        c.rect(x0, y0 + h * .58, x1, y1, fill=grn)
        for k in range(30):
            gx = x0 + k * w / 29; c.poly([(gx - 5, y0 + h * .585), (gx, y0 + h * .53), (gx + 5, y0 + h * .585)], fill=grn)
        mate_gourd(c, cx, y0 + h * .48, w * .32, (120, 70, 36, 255), (200, 200, 205, 255), (210, 180, 90, 255))
        c.text(cx, y0 + h * .14, 'Pampa', 'lobster-400', w * .26, grn, maxw=w * .85)
        c.text(cx, y0 + h * .24, 'Verde', 'lobster-400', w * .26, grn, maxw=w * .85)
        c.text(cx, y0 + h * .7, 'YERBA MATE', 'oswald-700', w * .15, yel, maxw=w * .85)
        c.text(cx, y0 + h * .78, 'CON PALO · YERBA MATE', 'oswald-400', w * .08, yel, maxw=w * .85)
        c.text(cx, y0 + h * .86, '500 g', 'oswald-400', w * .08, yel)
        seal_lines(c, x0, y0, x1, y0 + 20, rgba(shade(yel, .8)), 4)
    def side(c, x0, y0, x1, y1):
        c.rect(x0, y0, x1, y1, fill=grn); c.text((x0 + x1) / 2, (y0 + y1) / 2, 'MATE', 'oswald-700', (x1 - x0) * .4, yel)
    return paint_box(p, L, dict(front=front, left=side, right=side, top=None, bottom=None, back=None), yel, 24)


def T_NileRose(p, L):
    crim = (128, 18, 38, 255); gold = (226, 186, 100, 255)
    def front(c, x0, y0, x1, y1):
        w, h = x1 - x0, y1 - y0; cx = (x0 + x1) / 2
        c.rect(x0, y0, x1, y1, fill=crim)
        border(c, x0 + 10, y0 + 10, x1 - 10, y1 - 10, gold, 2, 7)
        pyramids(c, cx - w * .1, y0 + h * .78, w * .28, rgba(shade(gold, .8)))
        palm(c, x1 - w * .2, y0 + h * .79, h * .3, rgba(shade(crim, .55)))
        c.rect(x0 + 18, y0 + h * .78, x1 - 18, y0 + h * .8, fill=gold)
        hibiscus(c, cx, y0 + h * .38, w * .3, (230, 60, 90, 255), (90, 0, 20, 255), (250, 230, 200, 255))
        c.text(cx, y0 + h * .08, 'NILE ROSE', 'cinzel-decorative-700', w * .14, gold, maxw=w * .85)
        c.text(cx, y0 + h * .86, 'KARKADE · HIBISCUS', 'cinzel-700', w * .065, gold, maxw=w * .85)
        c.text(cx, y0 + h * .925, 'Nile Rose · hibiscus tea', 'pt-serif-700', w * .06, gold, maxw=w * .85)
    def side(c, x0, y0, x1, y1):
        c.rect(x0, y0, x1, y1, fill=crim)
        for k in range(6): star(c, (x0 + x1) / 2, y0 + 30 + k * (y1 - y0 - 60) / 5, (x1 - x0) * .2, gold, 8, .5)
    return paint_box(p, L, dict(front=front, left=side, right=side, top=None, bottom=None, back=None), crim, 25, back_bg=CREAM)


def T_FireweedMeadow(p, L):
    kraft = (190, 150, 104, 255); red = (170, 40, 40, 255)
    def front(c, x0, y0, x1, y1):
        w, h = x1 - x0, y1 - y0; cx = (x0 + x1) / 2
        c.rect(x0, y0, x1, y1, fill=kraft)
        folk_band(c, x0, x1, y0 + h * .2, w * .07, red, (240, 220, 180, 255))
        folk_band(c, x0, x1, y0 + h * .88, w * .07, red, (240, 220, 180, 255))
        for k, (dx, hh) in enumerate(((-.3, .52), (-.12, .6), (.08, .55), (.26, .62), (.38, .46))):
            fireweed(c, cx + dx * w, y0 + h * .86, h * hh, (214, 60, 140, 255), (60, 110, 50, 255), k)
        c.rect(x0 + w * .1, y0 + h * .3, x1 - w * .1, y0 + h * .42, fill=(244, 232, 206, 255), r=8)
        c.text(cx, y0 + h * .335, 'Fireweed Meadow', 'yeseva-one-400', w * .12, red, maxw=w * .75)
        c.text(cx, y0 + h * .39, 'FERMENTED FIREWEED TEA', 'oswald-600', w * .05, (70, 50, 40, 255), maxw=w * .75)
        c.text(cx, y0 + h * .27, 'КИПРЕЙНЫЙ ЛУГ · ИВАН-ЧАЙ', 'oswald-400', w * .045, (60, 40, 30, 255), maxw=w * .8)
        seal_lines(c, x0, y0, x1, y0 + 18, rgba(shade(kraft, .8)), 4)
    def side(c, x0, y0, x1, y1):
        c.rect(x0, y0, x1, y1, fill=kraft); folk_band(c, x0, x1, y0 + 40, (x1 - x0) * .25, red, (240, 220, 180, 255))
    return paint_box(p, L, dict(front=front, left=side, right=side, top=None, bottom=None, back=None), kraft, 26, back_bg=kraft)


def T_BombaySpice(p, L):
    saff = (232, 142, 30, 255); mag = (164, 26, 90, 255); cream = (250, 236, 200, 255)
    def front(c, x0, y0, x1, y1):
        w, h = x1 - x0, y1 - y0; cx = (x0 + x1) / 2
        c.rect(x0, y0, x1, y1, fill=saff)
        c.rect(x0, y0, x0 + w * .08, y1, fill=mag); c.rect(x1 - w * .08, y0, x1, y1, fill=mag)
        for k in range(8):
            paisley(c, x0 + w * .04, y0 + 40 + k * h / 8, w * .03, 20, cream)
            paisley(c, x1 - w * .04, y0 + 40 + k * h / 8, w * .03, 200, cream)
        c.ellipse(cx - w * .34, y0 + h * .3, cx + w * .34, y0 + h * .66, fill=(250, 196, 90, 255))
        elephant(c, cx - w * .02, y0 + h * .56, w * .5, (110, 60, 80, 255), mag, cream)
        c.text(cx, y0 + h * .09, 'BOMBAY', 'abril-fatface-400', w * .2, mag, maxw=w * .78)
        c.text(cx, y0 + h * .19, 'SPICE', 'abril-fatface-400', w * .2, mag, maxw=w * .78)
        c.rect(x0 + w * .1, y0 + h * .72, x1 - w * .1, y0 + h * .9, fill=mag)
        c.text(cx, y0 + h * .77, 'MASALA CHAI', 'cinzel-900', w * .1, cream, maxw=w * .75)
        c.text(cx, y0 + h * .85, 'Bombay Spice · masala chai', 'pt-serif-700', w * .06, cream, maxw=w * .75)
    def side(c, x0, y0, x1, y1):
        c.rect(x0, y0, x1, y1, fill=mag)
        for k in range(7): paisley(c, (x0 + x1) / 2, y0 + 50 + k * (y1 - y0 - 80) / 6, (x1 - x0) * .2, 30 + k * 40, saff, cream)
    def top(c, x0, y0, x1, y1): c.rect(x0, y0, x1, y1, fill=mag)
    return paint_box(p, L, dict(front=front, left=side, right=side, top=top, bottom=None, back=None), saff, 27, back_bg=cream)


def T_FrostMint(p, L):
    ice = (226, 240, 248, 255); blue = (34, 92, 150, 255); mint = (60, 160, 100, 255)
    def front(c, x0, y0, x1, y1):
        w, h = x1 - x0, y1 - y0; cx = (x0 + x1) / 2
        c.rect(x0, y0, x1, y1, fill=ice)
        for k in range(8):
            rr = random.Random(k)
            snowflake(c, rr.uniform(x0 + 20, x1 - 20), rr.uniform(y0 + 20, y1 - 20), rr.uniform(10, 26), (180, 210, 235, 255), 2)
        for a, s in ((-60, 1.0), (-120, .9), (-20, .8), (-160, .75)):
            leaf(c, x1 - w * .24, y0 + h * .6, h * .45 * s, h * .2 * s, a, mint, (230, 250, 240, 255))
        c.text(x0 + 16, y0 + h * .3, 'FROST', 'montserrat-800', h * .24, blue, anchor='lm', maxw=w * .55)
        c.text(x0 + 16, y0 + h * .55, 'MINT', 'montserrat-800', h * .24, mint, anchor='lm', maxw=w * .55)
        c.text(x0 + 16, y0 + h * .76, 'PEPPERMINT TEA · 10 BAGS', 'montserrat-500', h * .07, blue, anchor='lm', maxw=w * .55)
        c.text(x0 + 16, y0 + h * .88, 'Frost Mint · caffeine free', 'montserrat-500', h * .07, blue, anchor='lm', maxw=w * .55)
    def side(c, x0, y0, x1, y1):
        c.rect(x0, y0, x1, y1, fill=blue); snowflake(c, (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) * .3, ice, 3)
    def top(c, x0, y0, x1, y1):
        c.rect(x0, y0, x1, y1, fill=blue); c.text((x0 + x1) / 2, (y0 + y1) / 2, 'FROST MINT', 'montserrat-800', (y1 - y0) * .35, ice, maxw=(x1 - x0) * .9)
    return paint_box(p, L, dict(front=front, left=side, right=side, top=top, bottom=None, back=None), ice, 28)


def T_SilverNeedle(p, L):
    pearl = (236, 234, 228, 255); silver = (150, 154, 162, 255); ink = (40, 40, 46, 255)
    def front(c, cx, y0, y1):
        c.rect(0, y0, 1024, y1, fill=pearl)
        for k in range(0, 1024, 48):
            c.arc(k, y1 - 30, 30, 180, 360, rgba(silver, 120), 2)
        crane(c, cx + 40, y0 + 200, 190, (252, 252, 250, 255), ink, (200, 30, 30, 255))
        plum_branch(c, cx - 230, y0 + 120, 260, (70, 50, 40, 255), (240, 190, 200, 255))
        for i, ch in enumerate('白毫銀針'):
            c.text(cx - 190, y0 + 230 + i * 52, ch, CJK, 46, ink)
        seal(c, cx + 200, y0 + 330, 46, '白茶')
        c.text(cx + 30, y0 + 430, 'BAISHAN', 'cinzel-900', 46, silver, spacing=6, maxw=380)
        c.text(cx + 30, y0 + 478, 'SILVER NEEDLE', 'cinzel-700', 30, silver, spacing=3, maxw=380)
        c.text(cx + 30, y0 + 515, 'Silver Needle · white tea', 'pt-serif-700', 18, ink, maxw=380)
        c.rect(cx - 240, y0 + 16, cx + 240, y0 + 22, fill=silver)
    def lid_band(c, a, b): c.rect(0, a + 20, 1024, b - 20, fill=(200, 202, 208, 255))
    def lid_top(c, cx, cy, r):
        c.circle(cx, cy, r - 6, outline=ink, width=2)
        for j in range(5): leaf(c, cx, cy, r * .5, r * .5, 72 * j - 90, (240, 190, 200, 255))
        c.circle(cx, cy, r * .1, fill=(220, 170, 60, 255))
    return paint_tin(p, L, pearl, front, (196, 198, 204, 255), lid_top, lid_band=lid_band, metal_col=(205, 206, 210), seed=14, wear=.2)


# ---- coffee
def C_SkipperMocha(p, L):
    red = (124, 30, 26, 255); cream = (240, 226, 196, 255); navy = (26, 36, 60, 255)
    def front(c, cx, y0, y1):
        c.rect(0, y0, 1024, y1, fill=red)
        c.rect(0, y0 + 40, 1024, y0 + 52, fill=cream); c.rect(0, y1 - 52, 1024, y1 - 40, fill=cream)
        wheel(c, cx, y0 + 230, 120, rgba(shade(red, 1.25)), 10)
        c.circle(cx, y0 + 230, 118, fill=cream)
        c.circle(cx, y0 + 230, 108, outline=navy, width=3)
        skipper(c, cx, y0 + 240, 150, (232, 190, 160, 255), (236, 236, 232, 255), navy, (220, 180, 80, 255), (40, 30, 26, 255))
        c.text(cx, y0 + 410, 'SKIPPER MOCHA', 'abril-fatface-400', 62, cream, maxw=470)
        c.text(cx, y0 + 460, 'GROUND COFFEE · MEDIUM ROAST', 'oswald-600', 22, cream, maxw=470)
        c.text(cx, y0 + 494, 'Skipper Mocha · ground coffee · 500 g', 'pt-serif-700', 20, cream, maxw=470)
    def lid_band(c, a, b):
        c.rect(0, a + 30, 1024, a + 40, fill=cream)
        for k in range(0, 1024, 60): wheel(c, k + 30, (a + b) / 2 + 20, 14, cream, 3)
    def lid_top(c, cx, cy, r):
        c.circle(cx, cy, r - 8, outline=cream, width=4)
        c.text(cx, cy, 'SM', 'abril-fatface-400', r * .7, cream)
        c.text_arc(cx, cy, r * .78, 'SKIPPER MOCHA · EST. 1902', 'oswald-600', 16, cream, -90, 2)
    return paint_tin(p, L, red, front, red, lid_top, lid_band=lid_band, metal_col=(180, 176, 170), seed=31)


def C_Instanta(p, L):
    c = Canvas(1024, 1024, (60, 34, 20))
    x0, y0, x1, y1 = L['body']
    # glass with coffee granules behind it
    from labels2 import glass_body
    glass_body(c, L, (62, 34, 18), 5)
    hz = L['bodyz'][1]
    ly0 = y1 - (y1 - y0) * L['label'][1] / hz; ly1 = y1 - (y1 - y0) * L['label'][0] / hz
    red = (196, 30, 32, 255); white = (252, 248, 240, 255)
    k = kof(L); hw = int(270 / k); h = ly1 - ly0
    def lab(t, W, h):
        cx = W / 2
        t.rect(0, 0, W, h, fill=red); t.rect(0, 6, W, 10, fill=white); t.rect(0, h - 10, W, h - 6, fill=white)
        t.text(cx, h * .3, 'Instanta', 'pacifico-400', h * .3, white, maxw=440)
        cup_steam(t, cx, h * .7, h * .32, white, (90, 50, 26, 255), rgba(white, 220))
        t.text(cx - 190, h * .72, 'INSTANT', 'oswald-700', h * .08, white)
        t.text(cx - 190, h * .8, 'COFFEE', 'oswald-700', h * .08, white)
        t.text(cx + 190, h * .72, 'PURE', 'oswald-700', h * .08, white)
        t.text(cx + 190, h * .8, 'GRANULES', 'oswald-700', h * .08, white)
        t.text(cx, h - h * .07, '200 g · 100 cups', 'oswald-400', h * .06, white)
    fit(c, (512 - hw, ly0, 512 + hw, ly1), k, lab)
    bw = int(min(300, 1024 * k * .3) / k) + 16
    c.rect(0, ly0, bw / 2, ly1, fill=red); c.rect(1024 - bw / 2, ly0, 1024, ly1, fill=red)
    back_fit(c, p, 0, ly0 + 20, h - 40, red, 5, k, col=white)
    lx0, ly0b, lx1, ly1b = L['lidside']
    c.rect(lx0, ly0b, lx1, ly1b, fill=(200, 34, 34, 255))
    for k in range(0, 1024, 12): c.line([(k, ly0b), (k, ly1b)], (150, 20, 20, 255), 3)
    cx2, cy2, r = L['top']
    c.circle(cx2, cy2, r + 4, fill=(200, 34, 34, 255))
    c.text(cx2, cy2, 'Instanta', 'pacifico-400', r * .45, white)
    c.circle(cx2, cy2, r - 10, outline=white, width=3)
    bx, by, br = L['bottom']
    c.circle(bx, by, br + 4, fill=(70, 40, 22, 255))
    im = texturize(c.final(), .04, 32, 30, .015)
    return im


def C_VulcanoNero(p, L):
    black = (20, 20, 22, 255); red = (196, 36, 24, 255); orange = (250, 140, 30, 255)
    def front(c, x0, y0, x1, y1):
        w, h = x1 - x0, y1 - y0; cx = (x0 + x1) / 2
        c.rect(x0, y0, x1, y1, fill=black)
        volcano(c, cx, y0 + h * .78, w * .5, (48, 44, 44, 255), orange, (250, 90, 20))
        c.rect(x0, y0 + h * .78, x1, y1, fill=red)
        c.text(cx, y0 + h * .11, 'VULCANO', 'bebas-neue-400', w * .3, (240, 240, 236, 255), maxw=w * .9)
        c.text(cx, y0 + h * .22, 'NERO', 'bebas-neue-400', w * .3, red, maxw=w * .9)
        c.text(cx, y0 + h * .835, 'ESPRESSO · TOSTATURA SCURA', 'oswald-600', w * .065, (250, 240, 230, 255), maxw=w * .9)
        c.text(cx, y0 + h * .9, 'Vulcano Nero · espresso · 250 g', 'oswald-400', w * .06, (250, 240, 230, 255), maxw=w * .9)
        for k in range(5): c.circle(x0 + w * (.2 + k * .15), y0 + h * .955, w * .02, fill=orange if k < 5 else black)
    def side(c, x0, y0, x1, y1):
        c.rect(x0, y0, x1, y1, fill=black); c.rect(x0, y0 + (y1 - y0) * .78, x1, y1, fill=red)
        c.text((x0 + x1) / 2, (y0 + y1) / 2, 'NERO', 'bebas-neue-400', (x1 - x0) * .45, red)
    im = paint_box(p, L, dict(front=front, left=side, right=side, top=None, bottom=None, back=None), black, 33, paper=False, back_bg=(30, 30, 32, 255))
    # foil sheen
    return texturize(im, .14, 33, 60, .02)


def C_SweetTroika(p, L):
    yel = (246, 204, 56, 255); brown = (96, 52, 26, 255); cream = (252, 240, 214, 255)
    def front(c, x0, y0, x1, y1):
        w, h = x1 - x0, y1 - y0; cx = (x0 + x1) / 2
        c.rect(x0, y0, x1, y1, fill=yel)
        c.rect(x0, y0 + h * .5, x1, y1, fill=brown)
        three(c, cx - w * .08, y0 + h * .38, w * .2, brown, cream)
        c.text(cx, y0 + h * .1, 'Sweet', 'yeseva-one-400', w * .17, brown, maxw=w * .85)
        c.text(cx, y0 + h * .2, 'Troika', 'yeseva-one-400', w * .17, brown, maxw=w * .85)
        c.circle(x1 - w * .2, y0 + h * .62, w * .16, fill=(220, 40, 40, 255))
        c.text(x1 - w * .2, y0 + h * .6, '3 in 1', 'russo-one-400', w * .09, cream)
        c.text(x1 - w * .2, y0 + h * .655, '3 in 1', 'oswald-600', w * .05, cream)
        for k in range(4):  # sticks
            c.rect(x0 + w * (.08 + k * .07), y0 + h * .56, x0 + w * (.12 + k * .07), y0 + h * .85, fill=yel, r=4)
            c.rect(x0 + w * (.08 + k * .07), y0 + h * .56, x0 + w * (.12 + k * .07), y0 + h * .6, fill=(220, 40, 40, 255))
        c.text(cx, y0 + h * .91, 'СЛАДКАЯ ТРОЙКА · COFFEE · SUGAR · CREAMER', 'oswald-600', w * .05, cream, maxw=w * .9)
        c.text(cx, y0 + h * .96, 'coffee drink · 10 sticks', 'oswald-400', w * .045, cream, maxw=w * .9)
    def side(c, x0, y0, x1, y1):
        c.rect(x0, y0, x1, y1, fill=brown); c.text((x0 + x1) / 2, (y0 + y1) / 2, '3in1', 'russo-one-400', (x1 - x0) * .32, yel)
    def top(c, x0, y0, x1, y1): c.rect(x0, y0, x1, y1, fill=yel); c.text((x0 + x1) / 2, (y0 + y1) / 2, 'SWEET TROIKA', 'oswald-700', (y1 - y0) * .3, brown, maxw=(x1 - x0) * .9)
    return paint_box(p, L, dict(front=front, left=side, right=side, top=top, bottom=None, back=None), yel, 34, back_bg=cream)


def C_NorthLighthouse(p, L):
    kraft = (176, 136, 92, 255); ink = (24, 22, 20, 255); paper = (244, 238, 224, 255)
    def front(c, x0, y0, x1, y1):
        w, h = x1 - x0, y1 - y0; cx = (x0 + x1) / 2
        c.rect(x0, y0, x1, y1, fill=kraft)
        seal_lines(c, x0, y0, x1, y0 + 22, rgba(shade(kraft, .85)), 4)
        c.rect(x0 + w * .12, y0 + h * .3, x1 - w * .12, y0 + h * .86, fill=ink, r=6)
        c.rect(x0 + w * .15, y0 + h * .315, x1 - w * .15, y0 + h * .845, outline=paper, width=1.5, r=4)
        lighthouse(c, cx, y0 + h * .62, w * .28, paper, ink)
        c.text(cx, y0 + h * .37, 'NORTH', 'oswald-600', w * .12, paper, spacing=4, maxw=w * .7)
        c.text(cx, y0 + h * .69, 'LIGHTHOUSE', 'oswald-600', w * .1, paper, spacing=2, maxw=w * .7)
        c.text(cx, y0 + h * .735, 'ROASTERS', 'oswald-400', w * .05, paper, spacing=4, maxw=w * .7)
        c.text(cx, y0 + h * .78, 'ETHIOPIA · SIDAMO · 250 g', 'oswald-400', w * .045, paper, maxw=w * .7)
        c.text(cx, y0 + h * .815, 'North Lighthouse · fresh roast', 'oswald-400', w * .042, paper, maxw=w * .7)
        c.circle(cx, y0 + h * .2, w * .07, fill=(60, 54, 48, 255)); c.circle(cx, y0 + h * .2, w * .04, fill=(30, 28, 26, 255))  # valve
        c.rect(x0 + w * .62, y0 + h * .9, x0 + w * .95, y0 + h * .96, fill=paper)
        c.text(x0 + w * .785, y0 + h * .93, 'ROASTED 03.11', 'DejaVuSansMono', w * .03, (40, 60, 160, 255))
    def side(c, x0, y0, x1, y1): c.rect(x0, y0, x1, y1, fill=kraft)
    return paint_box(p, L, dict(front=front, left=side, right=side, top=None, bottom=None, back=None), kraft, 35, back_bg=kraft)


def C_AndesAltura(p, L):
    green = (22, 66, 48, 255); gold = (222, 188, 104, 255)
    def front(c, x0, y0, x1, y1):
        w, h = x1 - x0, y1 - y0; cx = (x0 + x1) / 2
        c.rect(x0, y0, x1, y1, fill=green)
        seal_lines(c, x0, y0, x1, y0 + 22, rgba(shade(green, 1.3)), 4)
        mountains(c, x0, x1, y0 + h * .62, [(x0 + w * .1, y0 + h * .48), (x0 + w * .3, y0 + h * .36), (x0 + w * .45, y0 + h * .46), (x0 + w * .65, y0 + h * .3),
                                              (x0 + w * .85, y0 + h * .44), (x1 - w * .05, y0 + h * .4)], (40, 100, 74, 255), (236, 236, 230, 255), 2)
        llama(c, x0 + w * .3, y0 + h * .62, w * .2, (20, 40, 30, 255))
        coffee_branch(c, cx, y0 + h * .7, w * .45, (70, 140, 80, 255), (190, 30, 40, 255))
        c.text(cx, y0 + h * .17, 'ANDES', 'cinzel-900', w * .18, gold, spacing=6, maxw=w * .85)
        c.text(cx, y0 + h * .25, 'ALTURA', 'cinzel-700', w * .1, gold, spacing=8, maxw=w * .85)
        c.text(cx, y0 + h * .83, 'CAFÉ DE COLOMBIA · 1800 m', 'oswald-600', w * .06, gold, maxw=w * .85)
        c.text(cx, y0 + h * .88, 'Andes Altura · Colombian coffee', 'oswald-400', w * .05, gold, maxw=w * .85)
        c.circle(cx, y0 + h * .085, w * .05, fill=shade(green, .7)); c.circle(cx, y0 + h * .085, w * .028, fill=(14, 30, 22, 255))
    def side(c, x0, y0, x1, y1): c.rect(x0, y0, x1, y1, fill=green)
    im = paint_box(p, L, dict(front=front, left=side, right=side, top=None, bottom=None, back=None), green, 36, paper=False, back_bg=green)
    return texturize(im, .10, 36, 50, .02)


def C_SultanCardamom(p, L):
    red = (146, 18, 28, 255); gold = (226, 184, 92, 255); dark = (60, 10, 14, 255)
    def front(c, x0, y0, x1, y1):
        w, h = x1 - x0, y1 - y0; cx = (x0 + x1) / 2
        c.rect(x0, y0, x1, y1, fill=red)
        seal_lines(c, x0, y0, x1, y0 + 18, rgba(shade(red, 1.25)), 4)
        arabesque_band(c, x0, x1, y0 + h * .2, w * .08, gold)
        arabesque_band(c, x0, x1, y0 + h * .9, w * .08, gold)
        c.poly([(cx, y0 + h * .3), (x1 - w * .1, y0 + h * .4), (x1 - w * .1, y0 + h * .78), (x0 + w * .1, y0 + h * .78), (x0 + w * .1, y0 + h * .4)], fill=dark)
        cezve(c, cx - w * .1, y0 + h * .55, w * .32, (196, 110, 60, 255), (60, 40, 30, 255), (40, 20, 10, 255))
        steam(c, cx - w * .12, y0 + h * .44, h * .1, rgba(gold, 200), 3, 3, 10)
        crescent(c, cx + w * .25, y0 + h * .38, w * .07, gold, dark)
        c.text(cx, y0 + h * .1, 'SULTAN', 'cinzel-decorative-700', w * .17, gold, maxw=w * .88)
        c.text(cx, y0 + h * .82, 'KAKULELİ TÜRK KAHVESİ', 'cinzel-700', w * .055, gold, maxw=w * .85)
        c.text(cx, y0 + h * .86, 'Sultan Cardamom · Turkish coffee', 'pt-serif-700', w * .05, gold, maxw=w * .85)
        c.text(cx, y0 + h * .155, 'CARDAMOM', 'cinzel-700', w * .07, gold, spacing=3, maxw=w * .85)
    def side(c, x0, y0, x1, y1):
        c.rect(x0, y0, x1, y1, fill=red); arabesque_band(c, x0, x1, (y0 + y1) / 2, (x1 - x0) * .3, gold)
    im = paint_box(p, L, dict(front=front, left=side, right=side, top=None, bottom=None, back=None), red, 37, paper=False, back_bg=red)
    return texturize(im, .09, 37, 50, .02)


def C_BlueBlossom(p, L):
    sky = (96, 146, 206, 255); white = (248, 248, 246, 255); navy = (24, 40, 96, 255)
    def front(c, cx, y0, y1):
        c.rect(0, y0, 1024, y1, fill=sky)
        for k in range(0, 1024, 40): c.line([(k, y0), (k + 40, y1)], rgba(shade(sky, 1.08)), 14)
        c.ellipse(cx - 220, y0 + 60, cx + 220, y1 - 60, fill=white)
        c.ellipse(cx - 208, y0 + 72, cx + 208, y1 - 72, outline=navy, width=3)
        for (dx, dy, s) in ((-70, -60, 70), (60, -90, 55), (20, 30, 62), (-110, 60, 44), (120, 20, 40)):
            c.line([(cx + dx, y0 + 300 + dy), (cx + dx * .3, y0 + 470)], (60, 110, 60, 255), 4)
        for (dx, dy, s) in ((-70, -60, 70), (60, -90, 55), (20, 30, 62), (-110, 60, 44), (120, 20, 40)):
            chicory(c, cx + dx, y0 + 300 + dy, s, (80, 120, 220, 255), (240, 240, 250, 255))
        c.text(cx, y0 + 140, 'Blue Blossom', 'old-standard-tt-700', 48, navy, maxw=360)
        c.text(cx, y0 + 470, 'ROASTED GROUND CHICORY', 'oswald-600', 20, navy, maxw=330)
        c.text(cx, y0 + 500, 'СИНИЙ ЦВЕТ · ЦИКОРИЙ', 'oswald-400', 16, navy, maxw=330)
    def lid_band(c, a, b):
        for k in range(0, 1024, 36): chicory(c, k + 18, (a + b) / 2, 12, white, navy)
    def lid_top(c, cx, cy, r):
        chicory(c, cx, cy, r * .6, (80, 120, 220, 255), white); c.circle(cx, cy, r - 6, outline=white, width=3)
    return paint_tin(p, L, sky, front, navy, lid_top, lid_band=lid_band, metal_col=(186, 186, 190), seed=38)


def C_Icebreaker(p, L):
    navy = (16, 34, 70, 255); white = (246, 248, 250, 255); cyan = (110, 200, 230, 255)
    c = Canvas(1024, 1024, (186, 188, 192))
    c.im.alpha_composite(can_metal(9).resize((1024 * SS, 1024 * SS)))
    x0, y0, x1, y1 = L['body']; k = kof(L); H = y1 - y0; fx = L['front']
    c.rect(x0, y0 + 10, x1, y1 - 12, fill=navy)
    for j in range(3): c.rect(0, y0 + 34 + j * 10, 1024, y0 + 38 + j * 10, fill=cyan)
    def art(t, W, h):
        # the front was drawn for a 480 px wide field: draw it so, then bring it down to what the eye takes in of a can at once
        o = Canvas(480, 560, navy[:3]); o.im = Image.new('RGBA', (480 * SS, 560 * SS), (0, 0, 0, 0)); o.d = ImageDraw.Draw(o.im, 'RGBA'); cx = 240; hh = 560
        icebreaker(o, cx - 40, hh * .58, 170, (30, 30, 34, 255), white, (230, 244, 250, 255), (40, 80, 130, 255))
        o.text(cx, hh * .16, 'ICEBREAKER', 'russo-one-400', 82, white, maxw=460)
        o.text(cx, hh * .27, 'ЛЕДОКОЛ', 'russo-one-400', 38, cyan, spacing=4, maxw=460)
        o.text(cx, hh * .83, 'COLD BREW COFFEE', 'oswald-600', 24, white, maxw=460)
        o.text(cx, hh * .9, '250 ml · serve cold', 'oswald-400', 20, white, maxw=460)
        sc = min(W / 480, (h - 70) / 560); im = o.im.resize((int(480 * sc * SS), int(560 * sc * SS)), Image.LANCZOS)
        t.im.alpha_composite(im, (int((W * SS - im.width) / 2), int(58 * SS + ((h - 70) * SS - im.height) / 2)))
    fw = 1024 * .33
    fit(c, (fx - fw / 2, y0 + 10, fx + fw / 2, y1 - 12), k, art)
    back_fit(c, p, fx + 512, y0 + H * .3, H * .5, navy, 39, k, lines=(['Холодный кофе, готов к питью.', 'Встряхнуть, открыть, пить.', 'Огонь не нужен.'],
             ['Cold brew, ready to drink.', 'Shake, open, drink.', 'No fire needed.']), col=white)
    im = texturize(c.final(), .04, 39, 30, .015)
    return scratches(im, 40, 39)


def C_FieldCoffee(p, L):
    olive = (86, 92, 56, 255); ink = (24, 26, 18, 255)
    def front(c, x0, y0, x1, y1):
        w, h = x1 - x0, y1 - y0; cx = (x0 + x1) / 2
        c.rect(x0, y0, x1, y1, fill=olive)
        seal_lines(c, x0, y0, x1, y0 + 14, rgba(shade(olive, .8)), 3)
        seal_lines(c, x0, y1 - 14, x1, y1, rgba(shade(olive, .8)), 3)
        star(c, cx, y0 + h * .2, w * .08, ink)
        c.text(cx, y0 + h * .36, 'COFFEE', 'russo-one-400', w * .2, ink, maxw=w * .8)
        c.text(cx, y0 + h * .46, 'INSTANT WITH SUGAR', 'oswald-600', w * .065, ink, maxw=w * .85)
        c.text(cx, y0 + h * .58, 'ПК-3', 'russo-one-400', w * .16, ink)
        c.text(cx, y0 + h * .69, 'FIELD COFFEE FC-3', 'oswald-600', w * .07, ink, maxw=w * .85)
        c.text(cx, y0 + h * .76, 'ARMY RATION · 1 SERVING', 'oswald-400', w * .05, ink, maxw=w * .85)
        c.text(cx, y0 + h * .86, 'ПАРТИЯ 0412 · ГОДЕН ДО 12.2031', 'DejaVuSansMono', w * .04, ink, maxw=w * .85)
    im = paint_box(p, L, dict(front=front, left=None, right=None, top=None, bottom=None, back=None), olive, 40, paper=False, back_bg=olive)
    return texturize(im, .12, 40, 50, .02)


# ------------------------------------------------------------------ cups
def cup_glass(liquid, seed=1):
    """a faceted glass of tea in a nickel-silver glass-holder."""
    lay = LAY['tea_glass']
    c = Canvas(1024, 1024, (180, 180, 186))
    nick = (198, 198, 204)
    c.im.alpha_composite(metal(1024, 1024, nick, seed).resize((1024 * SS, 1024 * SS)))
    x0, y0, x1, y1 = lay['glass']
    tea = tuple(liquid)
    c.rect(x0, y0, x1, y1, fill=rgba(shade(tea, 1.0)))
    c.rect(x0, y0, x1, y0 + 80, fill=(214, 220, 222, 255))       # empty glass above the tea
    c.rect(x0, y0 + 76, x1, y0 + 84, fill=rgba(shade(tea, 1.35)))
    for k in range(20):   # facet edges catch the light
        xx = x0 + (k + .5) * 1024 / 20
        c.rect(xx - 20, y0 + 84, xx - 14, y1, fill=rgba(shade(tea, 1.2), 70))
    hx0, hy0, hx1, hy1 = lay['holder']
    # holder: a filigree belt with ovals, a beaded band at the top, a plain foot
    c.rect(hx0, hy0 + 20, hx1, hy0 + 26, fill=(120, 120, 126, 255))
    dots_band(c, hx0, hy0 + 40, hx1, 5, (230, 230, 236, 255), 18)
    for k in range(0, 1024, 128):
        cx_ = k + 64; cy_ = hy0 + 140
        c.ellipse(cx_ - 46, cy_ - 60, cx_ + 46, cy_ + 60, outline=(110, 110, 116, 255), width=5)
        c.ellipse(cx_ - 30, cy_ - 42, cx_ + 30, cy_ + 42, outline=(230, 230, 236, 255), width=3)
        star(c, cx_, cy_, 18, (110, 110, 116, 255), 6, .45)
        c.line([(k, cy_), (k + 18, cy_ - 30), (k + 18, cy_ + 30), (k, cy_)], (110, 110, 116, 255), 3)
    c.rect(hx0, hy1 - 70, hx1, hy1 - 64, fill=(110, 110, 116, 255))
    ix0, iy0, ix1, iy1 = lay['inner']
    c.rect(ix0, iy0 + 50, ix1, iy1, fill=(214, 220, 222, 255))
    c.rect(ix0, iy1 - 8, ix1, iy1, fill=rgba(shade(tea, 1.2)))
    lx, ly, lr = lay['liquid']
    c.circle(lx, ly, lr + 6, fill=rgba(shade(tea, .85)))
    lay2 = c.layer(); lay2.ellipse(lx - lr * .6, ly - lr * .5, lx + lr * .1, ly - lr * .1, fill=(255, 255, 255, 50)); lay2.im = lay2.im.filter(ImageFilter.GaussianBlur(20 * SS)); c.over(lay2)
    c.circle(lx, ly, lr * .98, outline=rgba(shade(tea, 1.3)), width=4)
    # the handle's strip lies right above the tea disc: painted last so the tea never shows on the handle (owner 11-15: red spot)
    hx0_, hy0_, hx1_, hy1_ = lay['handle']
    hm = metal(1024, hy1_ - hy0_ + 6, nick, seed).resize((1024 * SS, (hy1_ - hy0_ + 6) * SS))
    c.im.paste(hm, (0, (hy0_ - 4) * SS))
    im = texturize(c.final(), .03, seed, 30, .01)
    return scratches(im, 30, seed, (240, 240, 245), 50)


def cup_mug(liquid, seed=1):
    """a cream enamel camp mug with a navy rim, a few chips."""
    lay = LAY['mug']
    enamel = (238, 234, 222, 255); navy = (28, 40, 70, 255)
    c = Canvas(1024, 1024, enamel[:3])
    x0, y0, x1, y1 = lay['outside']
    c.rect(x0, y0, x1, y1, fill=enamel)
    c.rect(x0, y0, x1, y0 + 18, fill=navy)
    c.rect(x0, y1 - 22, x1, y1, fill=navy)
    rng = random.Random(seed)
    for _ in range(9):   # chips in the enamel
        cx_, cy_ = rng.uniform(0, 1024), rng.choice([rng.uniform(20, 60), rng.uniform(y1 - 60, y1 - 20), rng.uniform(60, 380)])
        r = rng.uniform(3, 9)
        c.poly([(cx_ + r * math.cos(a) * rng.uniform(.6, 1.2), cy_ + r * math.sin(a) * rng.uniform(.6, 1.2)) for a in np.linspace(0, 6.2, 8)], fill=(30, 30, 34, 255))
    rx0, ry0, rx1, ry1 = lay['rim']
    c.rect(rx0, ry0, rx1, ry1, fill=navy)
    ix0, iy0, ix1, iy1 = lay['inner']
    c.rect(ix0, iy0, ix1, iy1, fill=enamel)
    c.rect(ix0, iy1 - 14, ix1, iy1, fill=rgba(shade(liquid, 1.4)))   # the coffee line on the wall
    hx0, hy0, hx1, hy1 = lay['handle']
    c.rect(hx0, hy0, hx1, hy1, fill=enamel); c.rect(hx0, hy0, hx1, hy0 + 8, fill=navy); c.rect(hx0, hy1 - 8, hx1, hy1, fill=navy)
    lx, ly, lr = lay['liquid']
    c.circle(lx, ly, lr + 6, fill=rgba(liquid))
    cr = rgba(shade(liquid, 1.9) if sum(liquid) < 300 else shade(liquid, 1.25))
    c.circle(lx, ly, lr * .99, outline=cr, width=10)   # crema/foam ring at the wall
    rng2 = np.random.default_rng(seed)
    for _ in range(40):
        a = rng2.uniform(0, 6.28); rr = lr * rng2.uniform(.75, .97)
        c.circle(lx + rr * math.cos(a), ly + rr * math.sin(a), rng2.uniform(2, 5), fill=cr)
    lay2 = c.layer(); lay2.ellipse(lx - lr * .6, ly - lr * .5, lx + lr * .1, ly - lr * .1, fill=(255, 255, 255, 40)); lay2.im = lay2.im.filter(ImageFilter.GaussianBlur(20 * SS)); c.over(lay2)
    bx, by, br = lay['bottom']
    c.circle(bx, by, br + 6, fill=navy)
    im = texturize(c.final(), .03, seed, 30, .01)
    return im


PAINTERS = {k[2:]: v for k, v in list(globals().items()) if k[:2] in ('T_', 'C_') and callable(v)}

if __name__ == '__main__':
    for p in ALL:
        if ONLY and p['id'] not in ONLY and 'cups' not in ONLY: continue
        if p['id'] in PAINTERS and (not ONLY or p['id'] in ONLY):
            im = PAINTERS[p['id']](p, LAY[p['model']])
            im.convert('RGB').save(os.path.join(OUT, 'textures', p['id'] + '.png'), optimize=True)
            print('pack', p['id'])
        if p.get('cup') and (not ONLY or 'cups' in ONLY or p['id'] in ONLY):
            im = (cup_glass if p['cup'] == 'glass' else cup_mug)(p['liquid'], hash(p['id']) % 1000)
            im.convert('RGB').save(os.path.join(OUT, 'textures', p['id'] + 'Cup.png'), optimize=True)

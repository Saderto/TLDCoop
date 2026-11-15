"""Pack fronts, second generation (0.5.0). The user: «этикетки пачек очень похожи между марками, как будто на всё 3-4 шаблона».
Every brand now gets its own mix of: composition (14), dish picture (10), background (13), logo lettering, flavour tag shape (6) and a brand sign.
STYLE[brand] is fixed by the brand's place in the catalogue (coprime steps, so no two brands share the whole mix) and can be overridden in OVERRIDE.
Variants of a brand share the composition and differ by colours and by what is in the dish, as real lines do.
python3 labels3.py [ids|brands]  (packs only; cups and trays stay in labels2)."""
import sys, os, math, random
from PIL import Image, ImageDraw, ImageFilter
import art as A
from art import rgb, mix
import labels as L
import labels2 as B
from labels2 import lum, on, seed, peppers, wt, script
from catalog import NOODLES, B as BRANDS
from food_top import food

NF = '/usr/share/fonts/truetype/nanum/'
NO = '/usr/share/fonts/opentype/noto/'
LAT = ['alfa', 'abril', 'lobster', 'cinzel', 'bebas', 'pacifico', 'playfair_l', 'comfortaa', 'yanone', 'ubuntu_b', 'roboto_black', 'oswald_l']
RUS = ['inter_black', 'playfair', 'lobster', 'ptserif_bi', 'comfortaa', 'ubuntu_b', 'yanone', 'oldstd', 'ptnarrow_b', 'roboto_black']
KOR = [NF + 'NanumSquareEB.ttf', NF + 'NanumBrush.ttf', NO + 'NotoSerifCJK-Black.ttc', NF + 'NanumSquareRoundEB.ttf', NF + 'NanumPen.ttf', NO + 'NotoSansCJK-Black.ttc', NF + 'NanumMyeongjoExtraBold.ttf', NF + 'NanumBarunpenB.ttf']
HAN = [NO + 'NotoSansCJK-Black.ttc', NO + 'NotoSerifCJK-Black.ttc', '/usr/share/fonts/truetype/arphic/ukai.ttc', NO + 'NotoSansCJK-Bold.ttc', NO + 'NotoSerifCJK-Bold.ttc']
LAYOUTS = 'ABCDEFGHIJKLMN'
DISHES = ['top', 'side', 'pot', 'lift', 'plate', 'block', 'pan', 'fork', 'sq', 'window']
BIGSIGN_CREATURES_ONLY = False
HERO = ['top', 'side', 'ingr', 'mascot', 'plate', 'ingr', 'badge', 'mascot', 'ingr', 'badge']   # what each of DISHES became
PATTERNS = ['burst', 'stripes', 'dots', 'waves', 'sun', 'plain', 'checker', 'rays', 'zig', 'halves', 'frame', 'squiggle', 'scales']
TAGS = ['pill', 'ribbon', 'bar', 'circle', 'plain', 'box']
SIGNS = ['sun', 'star', 'mountain', 'wave', 'fish', 'chili', 'flame', 'crown', 'leaf', 'bowl', 'sticks', 'moon', 'bolt', 'heart', 'wheat', 'lotus', 'drop', 'gem', 'torii', 'lantern', 'fan', 'cat', 'panda', 'elephant', 'monster', 'ghost', 'rooster', 'fox', 'anchor', 'flower', 'egg', 'crab']
SEALS = ['circle', 'diamond', 'hex', 'shield', 'square', 'oct']
OVERRIDE = {
    'Baishan': dict(sign='elephant'), 'BabyStar': dict(sign='star'), 'MamyMonster': dict(sign='monster'), 'Naeng': dict(sign='wave'),
    'Nagasaki': dict(sign='anchor'), 'Miyeok': dict(sign='wave', dish='pot'), 'Budae': dict(dish='pot', sign='flame'), 'CheeseR': dict(sign='moon'),
    'Chapaguri': dict(dish='plate'), 'Gotochi': dict(sign='torii'), 'Umaka': dict(sign='lantern'), 'Xiaomian': dict(sign='chili'), 'LaoTan': dict(sign='fish'),
    'Yipee': dict(sign='bolt'), 'HonestPrice': dict(layout='M', pattern='plain', dish='block', sign=None, tag='plain'), 'AlexSofa': dict(sign='heart'),
    'KespeKhan': dict(sign='sun'), 'Lagman': dict(sign='gem', dish='plate'), 'Miojo': dict(sign='rooster'), 'AjiMen': dict(sign='mountain'), 'Amina': dict(sign='crown'),
    'MaKuali': dict(sign='crab'), 'Omachy': dict(sign='lotus'), 'Payles': dict(sign='sun'), 'Supermi': dict(sign='star'), 'Sarimi': dict(sign='flower'),
    'TungI': dict(sign='gem'), 'Regan': dict(dish='plate', sign='fan'), 'Sutah': dict(sign='mountain'), 'Choyz': dict(sign='egg'),
}


def styles():
    st = {}
    packs = [b for b in BRANDS if b['tpl'] != 'fire']
    for i, b in enumerate(packs):
        d = dict(layout=LAYOUTS[(i * 5 + 3) % 14], dish=DISHES[(i * 3 + 1) % 10], pattern=PATTERNS[(i * 7 + 2) % 13], tag=TAGS[(i * 5 + 1) % 6],
                 lat=LAT[(i * 5) % len(LAT)], ru=RUS[(i * 3) % len(RUS)], kor=KOR[(i * 3) % len(KOR)], han=HAN[(i * 2) % len(HAN)],
                 sign=SIGNS[(i * 11 + 4) % len(SIGNS)], seal=SEALS[i % 6], plate=('oval', 'none', 'rect', 'none', 'skew', 'none', 'round')[i % 7])
        d['n'] = i
        d.update(OVERRIDE.get(b['key'], {}))
        # user 11-14: «не на всех же пачках рисунок лапши, где-то маскот, где-то ещё что-то»: a bowl stays on about a third of the brands,
        # the rest show the flavour's main ingredient, the brand's own sign made big, or a seal with the flavour. (The fork picture was dropped: never read well.)
        if b['key'] != 'HonestPrice':
            d['dish'] = HERO[DISHES.index(d['dish'])]
        if b.get('mascot') or any(n.get('mascot') for n in NOODLES if n['brand'] == b['key']): d['dish'] = 'mascot'
        # (11-14: replacing the plain-glyph circles by food was tried and asked about; the user found the circles fine, so they stay)
        elif BIGSIGN_CREATURES_ONLY and d['dish'] == 'mascot' and d.get('sign') not in ('rooster', 'fox', 'cat', 'panda', 'elephant', 'monster', 'ghost'): d['dish'] = ('side', 'ingr', 'top', 'ingr')[(i // 2) % 4]
        st[b['key']] = d
    return st


STYLE = styles()


def fname(t, st):
    sc = script(t)
    if sc == 'ru': return st['ru']
    if sc == 'cjk': return st['kor'] if any(0xac00 <= ord(c) <= 0xd7af for c in t) else st['han']
    return st['lat'] if all(ord(c) < 128 for c in t) else 'inter_black'


def fit(im, box, t, fill, st, **kw):
    return A.fit_text(im, box, t, fname(t, st), fill, **kw)


def small(im, box, t, fill, **kw):
    return B.fit(im, box, t, fill, lat='bebas', ru='oswald', **kw)


# ------------------------------------------------------------------ backgrounds

def background(im, s, st, w, h):
    c1, c2, ac = s['c1'], s['c2'], s['ac']
    im.paste(A.vgrad(w, h, c1, c2))
    p = st['pattern']
    lay = Image.new('RGBA', (w, h), (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
    lite = rgb('#ffffff' if lum(c1) < .75 else c2)[:3]
    a = 34 if lum(c1) < .75 else 46
    if p == 'burst': A.burst(im, w * .5, h * .6, 26, '#ffffff' if lum(c1) < .75 else ac, alpha=40)
    elif p == 'stripes':
        for k in range(-20, 40, 2): d.polygon([(k * w * .07, 0), ((k + 1) * w * .07, 0), ((k + 1) * w * .07 - h * .5, h), (k * w * .07 - h * .5, h)], fill=lite + (a,))
    elif p == 'dots':
        step = w / 11
        for j in range(int(h / step) + 2):
            for i in range(13): d.ellipse(((i + .5 * (j % 2)) * step - step * .16, j * step - step * .16, (i + .5 * (j % 2)) * step + step * .16, j * step + step * .16), fill=lite + (a + 14,))
    elif p == 'waves':
        for j in range(18):
            y = h * j / 16
            d.line([(x, y + math.sin(x / w * 14 + j) * h * .012) for x in range(0, w + 8, 8)], fill=lite + (a + 10,), width=int(h * .008))
    elif p == 'sun': d.ellipse((w * .5 - w * .62, h * .62 - w * .62, w * .5 + w * .62, h * .62 + w * .62), fill=rgb(mix(c1, '#ffffff' if lum(c1) < .75 else ac, .22))[:3] + (255,))
    elif p == 'checker':
        q = w / 12
        for j in range(3):
            for i in range(13):
                if (i + j) % 2 == 0: d.rectangle((i * q, h * .93 - (j + 1) * q, (i + 1) * q, h * .93 - j * q), fill=rgb(ac)[:3] + (255,))
    elif p == 'rays':
        for k in range(14):
            a0 = math.radians(k * 13); a1 = math.radians(k * 13 + 6)
            d.polygon([(0, h * .06), (math.cos(a0) * h * 2, h * .06 + math.sin(a0) * h * 2), (math.cos(a1) * h * 2, h * .06 + math.sin(a1) * h * 2)], fill=lite + (a,))
    elif p == 'zig':
        for j in range(3):
            y = h * (.78 + j * .05); z = w / 14
            d.line([(i * z, y + (z * .5 if i % 2 else 0)) for i in range(16)], fill=rgb(ac)[:3] + (255,), width=int(h * .012))
    elif p == 'halves':
        pts = [(0, h * .5)] + [(x, h * .5 + math.sin(x / w * 9) * h * .025) for x in range(0, w + 8, 8)] + [(w, h), (0, h)]
        d.polygon(pts, fill=rgb(ac)[:3] + (255,))
    elif p == 'frame':
        m = w * .045
        d.rectangle((m, h * .075, w - m, h * .925), outline=rgb(ac)[:3] + (255,), width=int(w * .012))
        d.rectangle((m * 1.7, h * .075 + m * .7, w - m * 1.7, h * .925 - m * .7), outline=rgb(ac)[:3] + (255,), width=int(w * .004))
    elif p == 'squiggle':
        r = random.Random(seed(s) % 50)
        for k in range(26):
            x0, y0 = r.uniform(0, w), r.uniform(h * .08, h * .92); ph = r.uniform(0, 6)
            d.line([(x0 + t, y0 + math.sin(t / w * 60 + ph) * w * .012) for t in range(0, int(w * .14), 6)], fill=lite + (a + 16,), width=int(w * .008))
    elif p == 'scales':
        q = w / 8
        for j in range(int(h / (q * .5)) + 2):
            for i in range(10):
                cx = (i + .5 * (j % 2)) * q; cy = j * q * .5
                d.arc((cx - q * .5, cy - q * .5, cx + q * .5, cy + q * .5), 0, 180, fill=lite + (a + 6,), width=int(w * .006))
    A.paste(im, lay)


# ------------------------------------------------------------------ dishes

def disc(s, r):
    S = max(8, int(2 * r)); f = food(s, S).convert('RGB')
    m = Image.new('L', (S * 2, S * 2), 0); ImageDraw.Draw(m).ellipse((0, 0, S * 2 - 1, S * 2 - 1), fill=255)
    return f, m.resize((S, S), Image.LANCZOS)


def strands(d, pts_fn, n, col, wd):
    for k in range(n): d.line(pts_fn(k), fill=rgb(col)[:3] + (255,), width=wd, joint='curve')


def lifted(im, cx, cy, r, ncol):
    """Noodles lifted out of the bowl: pinched between the chopsticks, a short tuft above them. Drawn after the lettering,
    so the sticks are never hidden and the noodles do not just hang cut off (user 11-14)."""
    top = cy - r * .72
    def path(k, off=0.):
        pts = [(cx + (k - 4) * r * .028 + off + math.sin(k * 2.1) * r * .02, top - r * .1 - abs(math.sin(k * 1.7)) * r * .05)]   # the tuft over the sticks
        for i in range(11):
            t = i / 10; spread = .028 + .05 * t   # pinched at the sticks, fanning out to the bowl
            pts.append((cx + (k - 4) * r * spread + off + math.sin(t * 5 + k) * r * .03 * t, top + t * (cy + r * .08 - top)))
        return pts
    lay = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
    strands(d, lambda k: path(k), 9, mix(ncol, '#8a5a12', .18), int(r * .05))
    strands(d, lambda k: path(k, r * .012), 9, ncol, int(r * .03))
    for dy, c in ((r * .05, '#4e342e'), (-r * .05, '#6d4c41')):
        d.line([(cx - r * .3, top + dy + r * .03), (cx + r * 1.1, top - r * .42 + dy * 2.2)], fill=rgb(c)[:3] + (255,), width=int(r * .06))
    A.paste(im, lay)


OUTL = (70, 40, 20, 255)


def ingredient(d, k, cx, cy, r, rot=0.):
    """One ingredient drawn flat with a dark outline on the layer d. r = half its size."""
    ca, sa = math.cos(rot), math.sin(rot)
    def T(x, y): return (cx + (x * ca - y * sa) * r, cy + (x * sa + y * ca) * r)
    def P(pts, fill, ow=.05): d.polygon([T(*q) for q in pts], fill=fill, outline=OUTL, width=max(2, int(r * ow)))
    def E(x, y, rx, ry, fill, out=True):
        pts = [(x + math.cos(t / 14 * math.pi) * rx, y + math.sin(t / 14 * math.pi) * ry) for t in range(28)]
        d.polygon([T(*q) for q in pts], fill=fill, outline=OUTL if out else None, width=max(2, int(r * .05)))
    def Ln(pts, fill, wd): d.line([T(*q) for q in pts], fill=fill, width=max(2, int(r * wd)), joint='curve')
    if k == 'shrimp':   # one curled body, thick at the head and thin at the tail, with segment lines, a tail fan, legs and feelers
        A0, A1, n = 215., -45., 26
        def pt(t, side):
            a = math.radians(A0 + (A1 - A0) * t); mid = .56; half = .25 * (1 - t * .55)
            rr = mid + side * half
            return (math.cos(a) * rr, -math.sin(a) * rr * .95 + .05)
        for t in (.15, .3, .45, .6):   # legs under the belly
            x, y = pt(t, -1); Ln([(x, y), (x * .55, y * .55 + .08)], OUTL, .035)
        a = math.radians(A1); tx, ty = pt(1, 0)
        P([(tx, ty), (tx + .34, ty + .3), (tx + .08, ty + .36)], (239, 83, 80, 255)); P([(tx, ty), (tx - .12, ty + .42), (tx + .1, ty + .38)], (239, 83, 80, 255))
        P([pt(i / n, 1) for i in range(n + 1)] + [pt(i / n, -1) for i in range(n, -1, -1)], (255, 138, 101, 255))
        for t in (.2, .34, .48, .62, .76, .9):
            Ln([pt(t, 1), pt(t + .02, -1)], (216, 67, 21, 255), .035)
        Ln([pt(i / n, .55) for i in range(3, n - 2)], (255, 204, 188, 255), .05)
        hx, hy = pt(0, 0); E(hx - .02, hy + .1, .2, .24, (255, 138, 101, 255)); E(hx + .04, hy + .12, .05, .05, (20, 20, 20, 255), False)
        Ln([(hx - .08, hy + .3), (hx - .1, hy + .7), (hx + .2, hy + .95)], OUTL, .03); Ln([(hx + .02, hy + .32), (hx + .15, hy + .62), (hx + .5, hy + .72)], OUTL, .03)
    elif k == 'chili':
        P([(-.85, .55), (-.55, .1), (-.05, -.25), (.5, -.45), (.75, -.3), (.6, .0), (.15, .35), (-.4, .6)], (229, 57, 53, 255))
        Ln([(-.5, .3), (.0, -.05), (.45, -.25)], (255, 138, 128, 255), .05)
        P([(.5, -.48), (.78, -.3), (.9, -.5), (.7, -.62)], (56, 142, 60, 255)); Ln([(.8, -.5), (1.0, -.8)], (56, 142, 60, 255), .08)
    elif k == 'cheese':
        P([(-.9, .45), (.9, .1), (.9, .6), (-.9, .85)], (255, 179, 0, 255)); P([(-.9, .45), (.35, -.6), (.9, .1)], (255, 213, 79, 255))
        for x, y, q in ((.2, -.05, .12), (-.25, .3, .08), (.5, .38, .1), (-.55, .62, .07), (.1, .55, .06)): E(x, y, q, q * .8, (255, 160, 0, 255))
    elif k == 'drumstick':
        Ln([(.25, .25), (.8, .75)], OUTL, .22); Ln([(.25, .25), (.8, .75)], (255, 248, 225, 255), .14)
        E(.82, .68, .13, .13, (255, 248, 225, 255)); E(.72, .84, .13, .13, (255, 248, 225, 255))
        E(-.2, -.2, .62, .5, (198, 124, 60, 255)); E(-.32, -.32, .3, .2, (224, 160, 96, 255), False)
    elif k == 'steak':   # a cut of meat: fat rim, red flesh with marbling, the round bone
        def blob(sc): return [(math.cos(i / 20 * math.pi) * (.92 + .1 * math.cos(i / 20 * math.pi * 2 + .8)) * sc - .0, math.sin(i / 20 * math.pi) * (.62 + .07 * math.sin(i / 20 * math.pi * 3)) * sc) for i in range(40)]
        P(blob(1.0), (255, 236, 200, 255)); P(blob(.84), (198, 62, 52, 255), .03)
        for pts in ([(-.5, -.25), (-.2, -.1), (.1, -.3), (.4, -.15)], [(-.55, .15), (-.25, .28), (.05, .12)], [(.2, .3), (.45, .12), (.62, .2)], [(-.1, -.02), (.15, .05)]): Ln(pts, (239, 154, 140, 255), .045)
        E(-.42, -.02, .13, .11, (255, 248, 230, 255)); E(-.42, -.02, .05, .04, (215, 170, 150, 255), False)
    elif k == 'mushroom':
        P([(-.22, .0), (.22, .0), (.3, .85), (-.3, .85)], (255, 243, 224, 255))
        d.pieslice([T(-.9, -.75)[0], T(-.9, -.75)[1], T(.9, .55)[0], T(.9, .55)[1]] if rot == 0 else [cx - .9 * r, cy - .75 * r, cx + .9 * r, cy + .55 * r], 180, 360, fill=(141, 92, 58, 255), outline=OUTL, width=max(2, int(r * .05)))
        for x, y in ((-.4, -.4), (.15, -.5), (.5, -.3)): E(x, y, .09, .07, (188, 143, 104, 255), False)
    elif k == 'egg':
        P([(-.9, .0), (-.6, -.6), (.1, -.75), (.8, -.4), (.92, .2), (.45, .7), (-.35, .68)], (255, 255, 255, 255)); E(.05, -.02, .36, .34, (255, 179, 0, 255)); E(-.05, -.14, .1, .07, (255, 236, 179, 255), False)
    elif k == 'corn':   # a cob lying aslant, rows of kernels, the husk peeled back at the stalk end
        ta = math.radians(-28); c2, s2 = math.cos(ta), math.sin(ta)
        def R(x, y): return (x * c2 - y * s2, x * s2 + y * c2)
        def half(x): return .36 * (1 - max(0., (x - .1) / .95) ** 2.2) ** .5 if x < 1.05 else 0
        for sg, col in ((1, (104, 159, 56, 255)), (-1, (124, 179, 66, 255))):   # husk leaves
            P([R(-.55, sg * .1), R(-.75, sg * .5), R(-1.25, sg * .78), R(-1.05, sg * .3), R(-.95, 0)], col)
            Ln([R(-.62, sg * .12), R(-1.12, sg * .62)], (85, 139, 47, 255), .03)
        xs = [-.7 + i * 1.72 / 24 for i in range(25)]
        P([R(x, -half(x)) for x in xs] + [R(x, half(x)) for x in xs[::-1]], (245, 166, 35, 255))
        for i in range(10):   # kernels: staggered rows that shrink toward the tip
            x = -.6 + i * .165; h = half(x) - .05; nrow = 4 if h > .2 else 3 if h > .12 else 2
            for j in range(nrow):
                y = (-h + (j + .5) * 2 * h / nrow) if nrow else 0
                q = R(x + (.04 if j % 2 else -.04), y); E(q[0], q[1], .075, min(.075, h / nrow * .82), (255, 224, 102, 255), False)
                q2 = R(x + (.04 if j % 2 else -.04) - .02, y - .02); E(q2[0], q2[1], .025, .02, (255, 245, 190, 255), False)
    elif k == 'kimchi':   # half a napa cabbage in red pepper paste
        P([(-.5, .9), (-.72, .2), (-.6, -.5), (-.25, -.9), (.25, -.9), (.6, -.5), (.72, .2), (.5, .9)], (139, 195, 74, 255))
        for sx in (-1, 1):
            for y in (-.55, -.15, .25): E(sx * .62, y, .16, .2, (104, 159, 56, 255))
        P([(-.34, .9), (-.46, .1), (-.3, -.55), (0, -.75), (.3, -.55), (.46, .1), (.34, .9)], (255, 253, 231, 255), .03)
        for o in (-.22, 0, .22): Ln([(o * .9, .85), (o * 1.1, -.45)], (220, 231, 177, 255), .05)
        for x, y in ((-.3, -.2), (.15, -.4), (.3, .1), (-.15, .3), (.05, .6), (-.35, .65), (.38, .55), (0, -.05), (-.05, -.6), (.3, -.35)): E(x, y, .09, .07, (229, 57, 53, 255), False)
    elif k == 'fish':
        P([(.5, 0), (.95, -.42), (.85, 0), (.95, .42)], (96, 125, 139, 255))
        P([(-.2, -.4), (.2, -.72), (.3, -.36)], (96, 125, 139, 255))
        E(-.1, 0, .72, .4, (176, 190, 197, 255)); P([(-.75, -.1), (-.3, -.38), (.4, -.25), (.55, 0), (-.2, -.05)], (120, 144, 156, 255), .0)
        E(-.52, -.08, .08, .08, (255, 255, 255, 255)); E(-.52, -.08, .035, .035, (20, 20, 20, 255), False); Ln([(-.34, -.25), (-.3, .0), (-.36, .25)], OUTL, .035)
    else:   # scallion
        for o, c in ((-.3, (104, 159, 56, 255)), (0, (124, 179, 66, 255)), (.3, (85, 139, 47, 255))):
            Ln([(o * .4, .5), (o * .6, .1), (o * 1.6, -.85)], OUTL, .2); Ln([(o * .4, .5), (o * .6, .1), (o * 1.6, -.85)], c, .13)   # the green ends inside the white root, never past its sides
        # the white stalk ends: one outlined bunch that the green grows out of (three bare white strokes read as stray lines, user 11-14)
        P([(-.27, .4), (.27, .4), (.2, .92), (-.2, .92)], (250, 250, 235, 255))
        for o in (-.065, .065): Ln([(o, .5), (o, .88)], (205, 215, 180, 255), .025)


def main_ingredients(s):
    t = (s['enf'] + ' ' + s['fl']).lower(); top = s.get('top') or ()
    out = []
    for k, words in (('fish', ('fish', 'anchovy', 'bonito')), ('shrimp', ('shrimp', 'seafood', 'tom yum', 'prawn', 'crab', 'laksa')), ('cheese', ('chee', 'carbonara', 'cream')), ('drumstick', ('chicken', 'ayam', 'pollo', 'galinha', 'gallina')),
                     ('kimchi', ('kimchi',)), ('mushroom', ('mushroom',)), ('steak', ('beef', 'pork', 'bacon', 'meat', 'tonkotsu', 'lamb', 'rendang', 'bulgogi')), ('corn', ('corn',)), ('egg', ('egg',))):
        if any(x in t for x in words) or (k in top) or (k == 'steak' and ('meat' in top or 'chashu' in top)): out.append(k)
    if s['spicy'] >= 2 or 'chili' in top: out.append('chili')
    out.append('scallion')
    return out[:3]


def hero(im, cx, cy, r, s, st, kind):
    ac = s['ac']; lay = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
    if kind == 'ingr':
        ks = main_ingredients(s)
        d.ellipse((cx - r, cy - r * .8 + r * .25, cx + r, cy + r * .8 + r * .25), fill=(0, 0, 0, 40))
        # side by side, none over another (user 11-14: «ингредиенты заслоняют друг друга»): the main one on the left, two smaller ones in a column on the right
        if len(ks) == 1: ingredient(d, ks[0], cx, cy, r * .7)
        else:
            ingredient(d, ks[0], cx - r * .36, cy + r * .02, r * .56)
            if len(ks) > 2: ingredient(d, ks[1], cx + r * .6, cy - r * .38, r * .3); ingredient(d, ks[2], cx + r * .6, cy + r * .28, r * .3)   # nothing below cy + .6 r: the flavour tag sits under the picture
            else: ingredient(d, ks[1], cx + r * .6, cy, r * .36)
        return A.paste(im, lay)
    if kind == 'mascot':
        ring = on(ac); d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=rgb(ac)[:3] + (255,), outline=rgb(ring)[:3] + (255,), width=int(r * .05))
        d.ellipse((cx - r * .86, cy - r * .86, cx + r * .86, cy + r * .86), outline=rgb(ring)[:3] + (255,), width=max(2, int(r * .02)))
        A.paste(im, lay)
        if s.get('mascot'): return B.mascot(im, s, cx, cy, r * .6)
        return HEAT_FREE['sign'](im, cx, cy, r * .62, s, dict(st, seal='none'))
    # badge: a toothed seal with the flavour in it
    A.badge(im, cx, cy, r, ac, ring=on(ac), teeth=24)
    w0 = s['fl'].split(' · ')[0]; words = w0.split(' ')
    ink = on(ac, '#ffffff', mix(s['c2'], '#000000', .4) if lum(s['c2']) < .5 else '#222222')
    if len(words) >= 2 and all(ord(c) < 0x3000 for c in w0):
        m = (len(words) + 1) // 2
        HEAT_FREE['small'](im, (cx - r * .62, cy - r * .4, cx + r * .62, cy - r * .02), ' '.join(words[:m]), ink); HEAT_FREE['small'](im, (cx - r * .62, cy + r * .02, cx + r * .62, cy + r * .4), ' '.join(words[m:]), ink)
    else: HEAT_FREE['small'](im, (cx - r * .64, cy - r * .3, cx + r * .64, cy + r * .3), w0, ink)


HEAT_FREE = {}


def dish(im, cx, cy, r, s, st, kind=None):
    kind = kind or st['dish']; ac = s['ac']
    if kind in ('ingr', 'mascot', 'badge'): return hero(im, cx, cy, r, s, st, kind)
    ncol = '#9a8160' if s.get('soba') else '#f3efe2' if s.get('rice') else '#f3d58a'
    if kind == 'side': return B.pic(im, cx, cy, r, s, rim=ac)
    if kind == 'lift':
        B.pic(im, cx, cy + r * .25, r * .85, s, rim=ac, chopsticks=False)
        return lifted(im, cx, cy, r, ncol)
    if kind == 'fork':
        # a plate of noodles and a fork above it with a wound ball, a few strands still running down to the plate
        dish(im, cx, cy + r * .42, r * .66, s, st, kind='plate')
        lay = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
        bx, by = cx - r * .08, cy - r * .42; dark = mix(ncol, '#8a5a12', .3)
        for k in range(5):
            pts = [(bx + (k - 2) * r * .07 + math.sin(t * 4 + k) * r * .03, by + r * .15 + t * r * .62) for t in [i / 8 for i in range(9)]]
            d.line(pts, fill=rgb(dark)[:3] + (255,), width=int(r * .05), joint='curve'); d.line([(x + r * .01, y) for x, y in pts], fill=rgb(ncol)[:3] + (255,), width=int(r * .028), joint='curve')
        steel, edge = (214, 216, 220, 255), (120, 124, 130, 255)
        hx, hy = bx + r * .95, by - r * .78   # the handle's end, up and to the right
        ux, uy = (hx - bx) / math.hypot(hx - bx, hy - by), (hy - by) / math.hypot(hx - bx, hy - by); px, py = -uy, ux
        d.line([(bx + ux * r * .3, by + uy * r * .3), (hx, hy)], fill=edge, width=int(r * .12)); d.line([(bx + ux * r * .3, by + uy * r * .3), (hx, hy)], fill=steel, width=int(r * .085))
        for k in (-1.5, -.5, .5, 1.5):   # tines, their tips showing under the ball
            a0 = (bx + ux * r * .3 + px * k * r * .075, by + uy * r * .3 + py * k * r * .075); a1 = (bx - ux * r * .42 + px * k * r * .075, by - uy * r * .42 + py * k * r * .075)
            d.line([a0, a1], fill=edge, width=int(r * .06)); d.line([a0, a1], fill=steel, width=int(r * .035))
        d.ellipse((bx - r * .34, by - r * .27, bx + r * .34, by + r * .27), fill=rgb(ncol)[:3] + (255,), outline=rgb(dark)[:3] + (255,), width=int(r * .03))
        for k in range(5):   # the winding
            yy = by - r * .2 + k * r * .1; hw = r * .33 * math.sqrt(max(0., 1 - ((yy - by) / (r * .27)) ** 2))
            d.arc((bx - hw, yy - r * .07, bx + hw, yy + r * .07), 10, 170, fill=rgb(dark)[:3] + (255,), width=int(r * .028))
        return A.paste(im, lay)
    if kind in ('block', 'window'):
        lay = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
        bx = (cx - r, cy - r * .78, cx + r, cy + r * .78)
        if kind == 'window': d.rounded_rectangle((bx[0] - r * .1, bx[1] - r * .1, bx[2] + r * .1, bx[3] + r * .1), int(r * .22), fill=(250, 246, 232, 255), outline=rgb(ac)[:3] + (255,), width=int(r * .05))
        rnd = s['lang'] == 'ko'
        m = Image.new('L', im.size, 0); md = ImageDraw.Draw(m)
        (md.ellipse if rnd else md.rounded_rectangle)(*(((cx - r * .8, cy - r * .7, cx + r * .8, cy + r * .7),) if rnd else ((cx - r * .88, cy - r * .66, cx + r * .88, cy + r * .66), int(r * .12))), fill=255)
        bl = Image.new('RGBA', im.size, rgb(mix(ncol, '#b07a1a', .25))[:3] + (255,)); bd = ImageDraw.Draw(bl)
        for j in range(22):
            y = cy - r * .75 + j * r * .07
            bd.line([(x, y + math.sin(x / r * 9 + j * 1.7) * r * .035) for x in range(int(cx - r), int(cx + r), 6)], fill=rgb(mix(ncol, '#ffffff', .25 if j % 2 else 0))[:3] + (255,), width=int(r * .04))
        lay.paste(bl, (0, 0), m)
        if kind == 'window': d.line([(bx[0] + r * .1, bx[1] + r * .15), (bx[0] + r * .5, bx[1] - r * .02)], fill=(255, 255, 255, 150), width=int(r * .06))
        A.soft_shadow(im, (cx - r * .8, cy + r * .5, cx + r * .8, cy + r * .9), alpha=70)
        return A.paste(im, lay)
    # top views: the real toppings of this very kind, in different crockery
    A.soft_shadow(im, (cx - r, cy + r * .55, cx + r, cy + r * 1.12), alpha=90)
    lay = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
    fr = r * .8
    if kind == 'top':
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(250, 250, 248, 255), outline=rgb(ac)[:3] + (255,), width=int(r * .07))
        for k in range(24):
            a = math.radians(k * 15); d.rectangle((cx + math.cos(a) * r * .9 - r * .025, cy + math.sin(a) * r * .9 - r * .025, cx + math.cos(a) * r * .9 + r * .025, cy + math.sin(a) * r * .9 + r * .025), fill=rgb(ac)[:3] + (255,))
    elif kind == 'pot':
        for sx in (-1, 1): d.rounded_rectangle((cx + sx * r * 1.0 - r * .2, cy - r * .22, cx + sx * r * 1.0 + r * .2, cy + r * .22), int(r * .08), fill=(245, 190, 40, 255), outline=(150, 100, 10, 255), width=int(r * .03))
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(250, 205, 60, 255), outline=(170, 115, 15, 255), width=int(r * .04)); fr = r * .86
    elif kind == 'plate':
        d.ellipse((cx - r * 1.08, cy - r * 1.08, cx + r * 1.08, cy + r * 1.08), fill=(252, 252, 250, 255), outline=(205, 205, 200, 255), width=int(r * .03))
        d.ellipse((cx - r * .86, cy - r * .86, cx + r * .86, cy + r * .86), outline=rgb(ac)[:3] + (255,), width=int(r * .025)); fr = r * .74
    elif kind == 'pan':
        d.line([(cx + r * .6, cy - r * .6), (cx + r * 1.35, cy - r * 1.3)], fill=(30, 30, 30, 255), width=int(r * .2))
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(38, 38, 38, 255), outline=(90, 90, 90, 255), width=int(r * .05)); fr = r * .84
    elif kind == 'sq':
        d.rounded_rectangle((cx - r, cy - r, cx + r, cy + r), int(r * .18), fill=(28, 24, 24, 255), outline=(170, 30, 30, 255), width=int(r * .07)); fr = r * .82
    A.paste(im, lay)
    f, m = disc(s, fr)
    im.paste(f, (int(cx - fr), int(cy - fr)), m)


# ------------------------------------------------------------------ brand signs

def sign(im, cx, cy, r, s, st):
    k = st.get('sign')
    if not k: return
    ac = s['ac']; ink = on(ac); col = rgb(ink)[:3] + (255,)
    lay = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
    q = r * .62
    def seal():
        sh = st['seal']
        if sh == 'none': return
        n = {'diamond': 4, 'hex': 6, 'oct': 8, 'square': 4}.get(sh, 0); rot = 45 if sh == 'square' else (22.5 if sh == 'oct' else 0)
        if sh == 'circle': d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=rgb(ac)[:3] + (255,), outline=col, width=int(r * .08))
        elif sh == 'shield': d.polygon([(cx - r * .85, cy - r), (cx + r * .85, cy - r), (cx + r * .85, cy + r * .2), (cx, cy + r * 1.05), (cx - r * .85, cy + r * .2)], fill=rgb(ac)[:3] + (255,), outline=col)
        else: d.polygon([(cx + math.cos(math.radians(rot + i * 360 / n)) * r * 1.1, cy + math.sin(math.radians(rot + i * 360 / n)) * r * 1.1) for i in range(n)], fill=rgb(ac)[:3] + (255,), outline=col)
    if k in ('rooster', 'fox', 'cat', 'panda', 'elephant', 'monster', 'ghost'):
        if k == 'rooster': A.chicken(im, cx, cy, r * .95, body='#ffffff', comb='#e53935'); return
        if k == 'fox': A.fox(im, cx, cy, r * .95); return
        if k == 'cat':
            for sx in (-1, 1): d.polygon([(cx + sx * r * .85, cy - r * .2), (cx + sx * r * .7, cy - r * 1.0), (cx + sx * r * .2, cy - r * .6)], fill=(255, 167, 38, 255), outline=(60, 40, 20, 255))
            d.ellipse((cx - r * .9, cy - r * .7, cx + r * .9, cy + r * .8), fill=(255, 183, 77, 255), outline=(60, 40, 20, 255), width=int(r * .05))
            for sx in (-1, 1): d.ellipse((cx + sx * r * .38 - r * .1, cy - r * .15, cx + sx * r * .38 + r * .1, cy + r * .12), fill=(40, 30, 20, 255)); d.line([(cx + sx * r * .2, cy + r * .35), (cx + sx * r * 1.0, cy + r * .25)], fill=(60, 40, 20, 255), width=int(r * .03))
            d.polygon([(cx - r * .1, cy + r * .2), (cx + r * .1, cy + r * .2), (cx, cy + r * .34)], fill=(216, 67, 21, 255))
        elif k == 'panda':
            for sx in (-1, 1): d.ellipse((cx + sx * r * .7 - r * .3, cy - r * .95, cx + sx * r * .7 + r * .3, cy - r * .35), fill=(30, 30, 30, 255))
            d.ellipse((cx - r * .9, cy - r * .75, cx + r * .9, cy + r * .85), fill=(250, 250, 250, 255), outline=(30, 30, 30, 255), width=int(r * .05))
            for sx in (-1, 1): d.ellipse((cx + sx * r * .38 - r * .22, cy - r * .25, cx + sx * r * .38 + r * .22, cy + r * .22), fill=(30, 30, 30, 255)); d.ellipse((cx + sx * r * .36 - r * .07, cy - r * .08, cx + sx * r * .36 + r * .07, cy + r * .06), fill=(255, 255, 255, 255))
            d.ellipse((cx - r * .12, cy + r * .28, cx + r * .12, cy + r * .46), fill=(30, 30, 30, 255))
        elif k == 'elephant':
            for sx in (-1, 1): d.ellipse((cx + sx * r * .75 - r * .5, cy - r * .7, cx + sx * r * .75 + r * .5, cy + r * .5), fill=(236, 239, 241, 255), outline=(84, 110, 122, 255), width=int(r * .05))
            d.ellipse((cx - r * .62, cy - r * .8, cx + r * .62, cy + r * .5), fill=(250, 250, 250, 255), outline=(84, 110, 122, 255), width=int(r * .05))
            d.line([(cx, cy + r * .1), (cx, cy + r * .8), (cx + r * .3, cy + r * 1.0)], fill=(84, 110, 122, 255), width=int(r * .2), joint='curve'); d.line([(cx, cy + r * .1), (cx, cy + r * .8), (cx + r * .3, cy + r * 1.0)], fill=(250, 250, 250, 255), width=int(r * .12), joint='curve')
            for sx in (-1, 1): d.ellipse((cx + sx * r * .28 - r * .07, cy - r * .3, cx + sx * r * .28 + r * .07, cy - r * .16), fill=(38, 50, 56, 255))
        elif k == 'monster':
            d.rounded_rectangle((cx - r * .9, cy - r * .8, cx + r * .9, cy + r * .9), int(r * .4), fill=(41, 121, 255, 255), outline=(13, 71, 161, 255), width=int(r * .06))
            for sx in (-1, 1): d.polygon([(cx + sx * r * .7, cy - r * .7), (cx + sx * r * .9, cy - r * 1.2), (cx + sx * r * .4, cy - r * .78)], fill=(255, 235, 59, 255))
            d.ellipse((cx - r * .35, cy - r * .5, cx + r * .35, cy + r * .1), fill=(255, 255, 255, 255)); d.ellipse((cx - r * .12, cy - r * .3, cx + r * .12, cy - r * .06), fill=(20, 20, 20, 255))
            d.chord((cx - r * .55, cy + r * .0, cx + r * .55, cy + r * .75), 0, 180, fill=(20, 20, 60, 255))
            for i in range(4): d.polygon([(cx - r * .4 + i * r * .22, cy + r * .38), (cx - r * .2 + i * r * .22, cy + r * .38), (cx - r * .3 + i * r * .22, cy + r * .55)], fill=(255, 255, 255, 255))
        elif k == 'ghost':
            d.pieslice((cx - r * .8, cy - r, cx + r * .8, cy + r * .6), 180, 360, fill=(250, 250, 250, 255)); d.rectangle((cx - r * .8, cy - r * .2, cx + r * .8, cy + r * .6), fill=(250, 250, 250, 255))
            for i in range(4): d.pieslice((cx - r * .8 + i * r * .4, cy + r * .35, cx - r * .4 + i * r * .4, cy + r * .85), 0, 180, fill=(250, 250, 250, 255))
            for sx in (-1, 1): d.ellipse((cx + sx * r * .3 - r * .14, cy - r * .4, cx + sx * r * .3 + r * .14, cy - r * .02), fill=(213, 0, 0, 255))
        return A.paste(im, lay)
    seal()
    P = lambda pts: d.polygon([(cx + x * q, cy + y * q) for x, y in pts], fill=col)
    E = lambda x0, y0, x1, y1, **kw: d.ellipse((cx + x0 * q, cy + y0 * q, cx + x1 * q, cy + y1 * q), **(kw or dict(fill=col)))
    Ln = lambda pts, wd=.14: d.line([(cx + x * q, cy + y * q) for x, y in pts], fill=col, width=max(2, int(q * wd)), joint='curve')
    if k == 'sun':
        E(-.5, -.5, .5, .5)
        for i in range(12): a = math.radians(i * 30); Ln([(math.cos(a) * .68, math.sin(a) * .68), (math.cos(a) * 1.0, math.sin(a) * 1.0)], .1)
    elif k in ('star', 'flower'):
        n = 5 if k == 'star' else 8; inn = .42 if k == 'star' else .7
        P([(math.cos(math.radians(-90 + i * 180 / n)) * (1 if i % 2 == 0 else inn), math.sin(math.radians(-90 + i * 180 / n)) * (1 if i % 2 == 0 else inn)) for i in range(2 * n)])
        if k == 'flower': E(-.3, -.3, .3, .3, fill=rgb(ac)[:3] + (255,))
    elif k == 'mountain': P([(-1, .7), (-.25, -.8), (.15, -.1), (.45, -.5), (1, .7)]); d.polygon([(cx - .25 * q, cy - .8 * q), (cx - .5 * q, cy - .3 * q), (cx - .25 * q, cy - .42 * q), (cx - .02 * q, cy - .3 * q)], fill=rgb(ac)[:3] + (255,))
    elif k == 'wave':
        for j in (-.45, 0, .45): Ln([(-1 + i * .1, j + math.sin(i * .9) * .16) for i in range(21)], .13)
    elif k == 'fish': E(-.8, -.42, .45, .42); P([(.3, 0), (1, -.5), (1, .5)]); E(-.55, -.18, -.37, 0, fill=rgb(ac)[:3] + (255,))
    elif k == 'chili': P([(-.9, .1), (-.3, -.35), (.5, -.3), (.9, .0), (.4, .35), (-.4, .5)]); Ln([(.75, -.1), (1.0, -.6)], .16)
    elif k in ('flame', 'drop'): P([(0, -1), (.55, -.1), (.6, .45), (0, .95), (-.6, .45), (-.55, -.1)] if k == 'drop' else [(0, -1), (.35, -.35), (.7, -.55), (.6, .5), (0, .95), (-.6, .5), (-.75, -.3), (-.3, -.1)])
    elif k == 'crown': P([(-.9, .6), (-.9, -.5), (-.45, .0), (0, -.8), (.45, .0), (.9, -.5), (.9, .6)])
    elif k == 'leaf':   # a pointed leaf with a stalk, a midrib and side veins (the old blunt shape read as a grain: user 11-14)
        P([(-.75, .75), (-.8, .1), (-.45, -.55), (.2, -.9), (.85, -.85), (.9, -.2), (.55, .45), (-.1, .8)])
        vein = rgb(ac)[:3] + (255,)
        d.line([(cx - 1.0 * q, cy + 1.0 * q), (cx + .7 * q, cy - .7 * q)], fill=vein, width=max(2, int(q * .08)))
        for t in (-.35, 0., .35):
            bx, by = cx + t * q, cy - t * q
            d.line([(bx, by), (bx + .1 * q, by - .5 * q)], fill=vein, width=max(2, int(q * .05))); d.line([(bx, by), (bx + .5 * q, by - .1 * q)], fill=vein, width=max(2, int(q * .05)))
        d.line([(cx - 1.0 * q, cy + 1.0 * q), (cx - .7 * q, cy + .7 * q)], fill=col, width=max(2, int(q * .12)))
    elif k == 'bowl': d.chord((cx - q, cy - q * .9, cx + q, cy + q * .9), 0, 180, fill=col); Ln([(-.5, -.3), (-.4, -.8)], .1); Ln([(0, -.3), (.1, -.8)], .1); Ln([(.5, -.3), (.6, -.8)], .1)
    elif k == 'sticks': Ln([(-.8, .9), (.5, -.9)], .13); Ln([(-.4, .9), (.9, -.9)], .13)
    elif k == 'moon': E(-.85, -.85, .85, .85); E(-.35, -.95, 1.05, .55, fill=rgb(ac)[:3] + (255,))
    elif k == 'bolt': P([(.2, -1), (-.6, .15), (-.05, .15), (-.25, 1), (.65, -.25), (.1, -.25)])
    elif k == 'heart': E(-.9, -.75, 0, .15); E(0, -.75, .9, .15); P([(-.86, -.1), (.86, -.1), (0, .95)])
    elif k == 'wheat':
        Ln([(0, 1), (0, -.9)], .08)
        for j in range(5):
            for sx in (-1, 1): E(sx * .12 if sx > 0 else -.5, -.8 + j * .32, sx * .5 if sx > 0 else -.12, -.5 + j * .32)
    elif k == 'lotus':
        for a0 in (-60, -30, 0, 30, 60): a = math.radians(a0 - 90); P([(0, .6), (math.cos(a) * 1.0 - .18, math.sin(a) * 1.0 + .5), (math.cos(a) * 1.15, math.sin(a) * 1.15 + .5), (math.cos(a) * 1.0 + .18, math.sin(a) * 1.0 + .5)])
        Ln([(-.9, .75), (.9, .75)], .12)
    elif k == 'gem': P([(-.9, -.2), (-.45, -.8), (.45, -.8), (.9, -.2), (0, .95)])
    elif k == 'torii': Ln([(-1, -.7), (1, -.7)], .2); Ln([(-.8, -.3), (.8, -.3)], .12); Ln([(-.55, -.7), (-.6, .95)], .16); Ln([(.55, -.7), (.6, .95)], .16)
    elif k == 'lantern': E(-.6, -.6, .6, .7); Ln([(-.3, -.75), (.3, -.75)], .18); Ln([(-.3, .85), (.3, .85)], .18); Ln([(0, -.75), (0, -1.05)], .08)
    elif k == 'fan': d.pieslice((cx - q, cy - q * .7, cx + q, cy + q * 1.3), 200, 340, fill=col); E(-.12, .2, .12, .44)
    elif k == 'anchor': A.anchor_icon(lay, cx, cy, q * 1.1, ink)
    elif k == 'egg': E(-.65, -.9, .65, .9); E(-.3, -.15, .3, .45, fill=(255, 179, 0, 255))
    elif k == 'crab':
        E(-.6, -.25, .6, .55)
        for sx in (-1, 1): Ln([(sx * .5, 0), (sx * .95, -.5)], .12); E(sx * .95 - .22, -.85, sx * .95 + .22, -.4); Ln([(sx * .5, .4), (sx * 1.0, .7)], .1)
    A.paste(im, lay)


# ------------------------------------------------------------------ logo, tag

def logo(im, box, s, st, col=None, rotate=0, plate=True):
    """The brand's name in its own lettering, on its own kind of plate."""
    x0, y0, x1, y1 = box; ac = s['ac']; t = s['nat']
    pl = st['plate'] if plate else 'none'
    if pl != 'none':
        lay = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(lay); f = rgb(ac)[:3] + (255,); o = rgb(on(ac))[:3] + (255,)
        if pl == 'oval': d.ellipse((x0, y0, x1, y1), fill=f, outline=o, width=int((y1 - y0) * .04))
        elif pl == 'rect': d.rectangle((x0, y0, x1, y1), fill=f)
        elif pl == 'round': d.rounded_rectangle((x0, y0, x1, y1), int((y1 - y0) * .3), fill=f, outline=o, width=int((y1 - y0) * .03))
        elif pl == 'skew': d.polygon([(x0 + (y1 - y0) * .25, y0), (x1, y0), (x1 - (y1 - y0) * .25, y1), (x0, y1)], fill=f)
        A.paste(im, lay)
        col = on(ac); px, py = (x1 - x0) * (.16 if pl == 'oval' else .08), (y1 - y0) * (.2 if pl == 'oval' else .12)
        fit(im, (x0 + px, y0 + py, x1 - px, y1 - py), t, col, st, rotate=rotate)
    else:
        col = col or ('#ffffff' if lum(s['c1']) < .62 else (ac if lum(ac) < .55 else s['c2'] if lum(s['c2']) < .5 else '#222222'))
        fit(im, box, t, col, st, stroke=7, stroke_fill=mix(s['c2'], '#000000', .55) if lum(col) > .5 else '#ffffff', rotate=rotate)


def sub(im, box, s, col=None):
    col = col or ('#ffffff' if lum(s['c1']) < .62 else '#333333')
    small(im, box, s['sub'], col, stroke=5, stroke_fill=mix(s['c2'], '#000000', .55) if lum(col) > .5 else '#ffffff')


def tag(im, box, s, st):
    """The flavour, in the brand's own kind of tag."""
    x0, y0, x1, y1 = box; ac = s['ac']; k = st['tag']; t = s['fl']
    lay = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
    base = '#ffffff' if lum(ac) < .8 and k in ('ribbon', 'box') else ac
    f = rgb(base)[:3] + (255,); ink = on(base, '#ffffff', mix(s['c2'], '#000000', .4) if lum(s['c2']) < .5 else '#222222')
    hh = y1 - y0
    if k == 'pill': d.rounded_rectangle(box, int(hh / 2), fill=f)
    elif k == 'ribbon': d.polygon([(x0, y0), (x1, y0), (x1 - hh * .4, (y0 + y1) / 2), (x1, y1), (x0, y1), (x0 + hh * .4, (y0 + y1) / 2)], fill=f)
    elif k == 'bar': d.polygon([(x0 + hh * .3, y0), (x1, y0), (x1 - hh * .3, y1), (x0, y1)], fill=f)
    elif k == 'box': d.rectangle(box, fill=f, outline=rgb(ac)[:3] + (255,), width=int(hh * .08))
    elif k == 'circle':
        d.rounded_rectangle(box, int(hh * .2), fill=rgb(mix(s['c2'], '#000000', .35))[:3] + (235,)); ink = '#ffffff'
    elif k == 'plain': ink = '#ffffff' if lum(s['c2']) < .62 else '#222222'
    A.paste(im, lay)
    small(im, (x0 + hh * .5, y0 + hh * .12, x1 - hh * .5, y1 - hh * .12), t, ink, **(dict(stroke=5, stroke_fill=mix(s['c2'], '#000000', .55) if lum(ink) > .5 else '#ffffff') if k == 'plain' else {}))


def heat(im, cx, cy, n, size):
    """How hot it is: the peppers on a white plate of their own, drawn last of all (user 11-14: in places they were lost behind the picture)."""
    ww = n * size * 1.15 + size * .7; hh = size * 1.55
    cx = min(max(cx, ww / 2 + size * .3), im.size[0] - ww / 2 - size * .3)
    lay = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
    d.rounded_rectangle((cx - ww / 2, cy - hh / 2 - size * .05, cx + ww / 2, cy + hh / 2 - size * .05), int(hh / 2), fill=(255, 255, 255, 255), outline=(127, 0, 0, 255), width=max(3, int(size * .07)))
    A.paste(im, lay)
    peppers(im, cx, cy, n, size)


def extras(im, s, st, w, h, pep=(.5, .5), pr=.055, weight=(.84, .9)):
    if s['spicy']: heat(im, w * pep[0], h * pep[1], s['spicy'], w * pr)
    if weight: small(im, (w * (weight[0] - .1), h * (weight[1] - .012), w * (weight[0] + .1), h * (weight[1] + .012)), wt(s), '#ffffff' if lum(s['c2']) < .62 else '#222222')


# ------------------------------------------------------------------ compositions (y runs from the top seal sl to h - sl)

def lay_A(s, st, im, w, h, sl):       # name on top, dish below, tag at the foot
    logo(im, (w * .08, sl + h * .03, w * .92, sl + h * .2), s, st); sub(im, (w * .15, sl + h * .215, w * .85, sl + h * .255), s)
    dish(im, w * .5, h * .58, min(w, h * .8) * .3, s, st); sign(im, w * .14, h * .4, w * .075, s, st)
    tag(im, (w * .08, h * .8, w * .92, h * .88), s, st); extras(im, s, st, w, h, pep=(.82, .4))


def lay_B(s, st, im, w, h, sl):       # dish on top, name in the middle
    dish(im, w * .5, sl + h * .26, min(w, h * .8) * .29, s, st)
    logo(im, (w * .07, h * .52, w * .93, h * .7), s, st); sub(im, (w * .15, h * .71, w * .85, h * .75), s)
    tag(im, (w * .12, h * .79, w * .88, h * .87), s, st); sign(im, w * .13, sl + h * .1, w * .07, s, st); extras(im, s, st, w, h, pep=(.84, sl / h + .1))


def lay_C(s, st, im, w, h, sl):       # a medallion in the centre
    A.ellipse(im, (w * .1, h * .5 - w * .4, w * .9, h * .5 + w * .4), s['ac'])
    A.ellipse(im, (w * .13, h * .5 - w * .37, w * .87, h * .5 + w * .37), mix(s['c1'], '#000000', .1))
    dish(im, w * .5, h * .5, min(w, h * .8) * .27, s, st)
    logo(im, (w * .1, sl + h * .02, w * .9, sl + h * .15), s, st, plate=False); sub(im, (w * .2, h * .775, w * .8, h * .81), s)
    tag(im, (w * .15, h * .83, w * .85, h * .9), s, st); sign(im, w * .13, h * .78, w * .06, s, st); extras(im, s, st, w, h, pep=(.86, .76), weight=None)


def lay_D(s, st, im, w, h, sl):       # everything on the slant, dish running off the corner
    A.poly(im, [(0, h * .46), (w, h * .2), (w, h * .42), (0, h * .68)], s['ac'])
    fit(im, (w * .06, h * .3, w * .94, h * .56), s['nat'], on(s['ac']), st, rotate=14)
    dish(im, w * .7, h * .78, min(w, h * .8) * .33, s, st); sub(im, (w * .08, sl + h * .03, w * .7, sl + h * .075), s)
    sign(im, w * .84, sl + h * .08, w * .08, s, st); tag(im, (w * .04, h * .82, w * .44, h * .89), s, st); extras(im, s, st, w, h, pep=(.2, .74), weight=None)


def lay_E(s, st, im, w, h, sl):       # a standing name strip on the left
    A.rrect(im, (0, 0, w * .3, h), 0, s['ac'])
    t = s['nat']
    if script(t) == 'cjk' and len(t) <= 7: B.vtext(im, w * .15, sl + h * .04, h * .8, t, on(s['ac']), w * .2)
    else:
        f = A.fit_font(t, fname(t, st), h * .7, w * .2); A.text(im, (w * .15, h * .45), t, f, on(s['ac']), rotate=90)
    sign(im, w * .15, h * .87, w * .08, dict(s, ac=on(s['ac'])), st)
    dish(im, w * .65, h * .5, min(w, h * .8) * .27, s, st); sub(im, (w * .34, sl + h * .04, w * .96, sl + h * .085), s)
    tag(im, (w * .36, h * .8, w * .96, h * .88), s, st); extras(im, s, st, w, h, pep=(.65, .2), weight=(.8, .92))


def lay_F(s, st, im, w, h, sl):       # the dish fills the pack, the name lies across it
    big = st['dish'] not in ('ingr', 'mascot', 'badge')     # a bowl may run under the tag, a sign or ingredients may not
    dish(im, w * .5, h * (.6 if big else .54), min(w, h * .8) * (.52 if big else .34), s, st, kind=st['dish'] if st['dish'] in ('top', 'pot', 'plate', 'pan', 'sq', 'ingr', 'mascot', 'badge') else 'top')
    A.rrect(im, (0, sl + h * .02, w, sl + h * .2), 0, mix(s['c1'], '#000000', .15))
    logo(im, (w * .06, sl + h * .03, w * .78, sl + h * .19), s, st, plate=False, col='#ffffff'); sign(im, w * .88, sl + h * .11, w * .07, s, st)
    tag(im, (w * .2, h * .84, w * .8, h * .91), s, st); extras(im, s, st, w, h, pep=(.5, sl / h + .25), weight=None)


def lay_G(s, st, im, w, h, sl):       # name in the middle, dish rising from the bottom edge
    dish(im, w * .5, h * (.96 if st['dish'] in ('top', 'pot', 'plate', 'pan') else .8), min(w, h * .8) * (.42 if st['dish'] in ('top', 'pot', 'plate', 'pan') else .22), s, st, kind=st['dish'] if st['dish'] in ('top', 'pot', 'plate', 'pan', 'ingr', 'mascot', 'badge') else 'top')
    logo(im, (w * .08, h * .22, w * .92, h * .46), s, st); sub(im, (w * .15, h * .47, w * .85, h * .51), s)
    sign(im, w * .5, sl + h * .08, w * .075, s, st); tag(im, (w * .14, h * .55, w * .86, h * .63), s, st); extras(im, s, st, w, h, pep=(.5, .68), weight=None)


def lay_H(s, st, im, w, h, sl):       # an old-fashioned frame with a cartouche
    m = w * .06; ac = s['ac']
    d = ImageDraw.Draw(im); d.rectangle((m, sl + m * .5, w - m, h - sl - m * .5), outline=rgb(ac)[:3], width=int(w * .014))
    d.rectangle((m * 1.6, sl + m * 1.1, w - m * 1.6, h - sl - m * 1.1), outline=rgb(ac)[:3], width=int(w * .005))
    logo(im, (w * .14, sl + h * .06, w * .86, sl + h * .22), s, st); sub(im, (w * .2, sl + h * .235, w * .8, sl + h * .27), s)
    dish(im, w * .5, h * .57, min(w, h * .8) * .25, s, st)
    for sx in (-1, 1): sign(im, w * (.5 + sx * .32), h * .57, w * .05, s, st)
    tag(im, (w * .16, h * .78, w * .84, h * .85), s, st); extras(im, s, st, w, h, pep=(.5, .885), pr=.04, weight=None)


def lay_I(s, st, im, w, h, sl):       # all lettering: the name as big as the pack
    t = s['nat']; col = s['ac'] if abs(lum(s['ac']) - lum(s['c1'])) > .25 else on(s['c1'])
    parts = t.split(' ') if (' ' in t and script(t) != 'cjk') else ([t[:len(t) // 2 + len(t) % 2], t[len(t) // 2 + len(t) % 2:]] if script(t) == 'cjk' and len(t) >= 4 else [t])
    y = sl + h * .03; hh = h * .42 / len(parts)
    for p in parts: fit(im, (w * .05, y, w * .95, y + hh * .96), p, col, st, stroke=6, stroke_fill=mix(s['c2'], '#000000', .5) if lum(col) > .5 else '#ffffff'); y += hh
    sub(im, (w * .1, sl + h * .46, w * .9, sl + h * .5), s)
    dish(im, w * .32, h * .73, min(w, h * .8) * .22, s, st); sign(im, w * .8, h * .64, w * .08, s, st)
    tag(im, (w * .56, h * .75, w * .97, h * .82), s, st); extras(im, s, st, w, h, pep=(.78, .87), weight=None)


def lay_J(s, st, im, w, h, sl):       # two colours, dish astride the border
    A.rrect(im, (0, h * .5, w, h), 0, s['ac'])
    logo(im, (w * .08, sl + h * .04, w * .92, sl + h * .22), s, st, plate=False); sub(im, (w * .15, sl + h * .235, w * .85, sl + h * .275), s)
    dish(im, w * .5, h * .55, min(w, h * .8) * .27, s, st); sign(im, w * .14, h * .86, w * .07, dict(s, ac=s['c1']), st)
    x0, y0, x1, y1 = w * .28, h * .81, w * .95, h * .89
    small(im, (x0, y0, x1, y1), s['fl'], on(s['ac'])); extras(im, s, st, w, h, pep=(.84, .36), weight=None)


def lay_K(s, st, im, w, h, sl):       # name in the corner, big dish opposite
    logo(im, (w * .05, sl + h * .03, w * .68, sl + h * .2), s, st); sign(im, w * .85, sl + h * .11, w * .09, s, st)
    sub(im, (w * .05, sl + h * .215, w * .68, sl + h * .255), s)
    dish(im, w * .62, h * .62, min(w, h * .8) * .32, s, st)
    tag(im, (w * .04, h * .82, w * .6, h * .9), s, st); extras(im, s, st, w, h, pep=(.16, .45), pr=.05)


def lay_L(s, st, im, w, h, sl):       # a ribbon across the middle
    dish(im, w * .5, h * .7, min(w, h * .8) * .26, s, st)
    ac = s['ac']; y0, y1 = h * .3, h * .46
    A.poly(im, [(0, y0), (w, y0), (w - w * .05, (y0 + y1) / 2), (w, y1), (0, y1), (w * .05, (y0 + y1) / 2)], ac)
    fit(im, (w * .1, y0 + h * .012, w * .9, y1 - h * .012), s['nat'], on(ac), st)
    sign(im, w * .5, sl + h * .11, w * .09, s, st); sub(im, (w * .15, h * .47, w * .85, h * .505), s)
    tag(im, (w * .1, h * .86, w * .9, h * .93), s, dict(st, tag='plain')); extras(im, s, st, w, h, pep=(.84, .6), weight=None)


def lay_M(s, st, im, w, h, sl):       # spare: thin rules, name small, flavour large, dish cut by the edge
    ink = '#ffffff' if lum(s['c1']) < .62 else (s['c2'] if lum(s['c2']) < .5 else '#222222'); d = ImageDraw.Draw(im)
    for y in (sl + h * .04, sl + h * .2): d.line([(w * .08, y), (w * .92, y)], fill=rgb(ink)[:3], width=int(h * .004))
    fit(im, (w * .08, sl + h * .055, w * .7, sl + h * .185), s['nat'], ink, st); sign(im, w * .84, sl + h * .12, w * .06, s, st)
    small(im, (w * .08, sl + h * .22, w * .6, sl + h * .3), s['fl'], s['ac'] if abs(lum(s['ac']) - lum(s['c1'])) > .25 else ink)
    dish(im, w * .78, h * .62, min(w, h * .8) * .36, s, st); sub(im, (w * .08, h * .86, w * .6, h * .9), s, col=ink); extras(im, s, st, w, h, pep=(.2, .6), weight=(.2, .8))


def lay_N(s, st, im, w, h, sl):       # flavour standing on a band at the right
    ac = s['ac']; A.rrect(im, (w * .8, 0, w, h), 0, ac)
    t = s['fl'].split(' · ')[0]
    f = A.fit_font(t, 'bebas' if all(ord(c) < 128 for c in t) else fname(t, st), h * .7, w * .13); A.text(im, (w * .9, h * .5), t, f, on(ac), rotate=-90)
    logo(im, (w * .05, sl + h * .04, w * .76, sl + h * .22), s, st, plate=False); sub(im, (w * .05, sl + h * .235, w * .76, sl + h * .275), s)
    dish(im, w * .4, h * .6, min(w, h * .8) * .29, s, st); sign(im, w * .12, h * .86, w * .07, s, st)
    small(im, (w * .24, h * .83, w * .76, h * .885), wt(s) + ' · ' + s['sub'].split(' · ')[0], '#ffffff' if lum(s['c2']) < .62 else '#222222')
    if s['spicy']: heat(im, w * .5, h * .925, s['spicy'], w * .045)


LATER = None


def later(f):
    def g(*a, **k):
        if LATER is None: return f(*a, **k)
        LATER.append((f, a, k))
    return g


HEAT = heat; LIFT = lifted
HEAT_FREE.update(sign=sign, small=small)
logo, sub, tag, sign, small, fit, heat = later(logo), later(sub), later(tag), later(sign), later(small), later(fit), later(heat)
lifted = later(lifted)

LAY = {k: globals()['lay_' + k] for k in LAYOUTS}


def front(s, im, w, h, sl):
    st = STYLE[s['brand']]
    background(im, s, st, w, h)
    lay = st['layout'] if s['shape'] != 'long' else 'A'
    global LATER
    LATER = []
    try: LAY[lay](s, st, im, w, h, sl)
    finally: q, LATER = LATER, None
    for f, a, k in sorted(q, key=lambda e: 2 if e[0] is HEAT else 1 if e[0] is LIFT else 0): f(*a, **k)


def run(lay, s, st, im, w, h, sl=0):
    """One composition painted on im with the lettering queued behind the picture."""
    global LATER
    LATER = []
    try: LAY[lay](s, st, im, w, h, sl)
    finally: q, LATER = LATER, None
    for f, a, k in sorted(q, key=lambda e: 2 if e[0] is HEAT else 1 if e[0] is LIFT else 0): f(*a, **k)


# ------------------------------------------------------------------ cups, bowls, buckets (0.5.1: the user wanted them as varied as the packs)

CUPLAY = 'ABCGHIKM'          # compositions that keep inside their own panel (no bands running off the edge)
WALLPAT = ['stripes', 'dots', 'waves', 'plain', 'squiggle', 'scales', 'plain', 'dots']
KEEP = ('Buldak', 'Kapp')    # real-pack looks made on purpose: left as they are


def cup3(s):
    from catalog import CUPS
    from PIL import ImageChops
    st = STYLE[s['brand']]; i = st['n']
    W = H = 1024; K = L.K
    im = Image.new('RGB', (W * K, H * K), rgb(s['c1']))
    w, h = im.size; sh = int(h * .7); ac = s['ac']
    R0_, R1_, H_ = CUPS[s['shape']]
    ww = max(w, int(sh * math.pi * (R0_ + R1_) / H_)); reps = 3; rw = ww / reps
    side = Image.new('RGB', (ww, sh))
    background(side, s, dict(st, pattern=WALLPAT[i % 8]), ww, sh)
    ring = i % 4                                   # rings round the cup
    if ring == 1: A.rrect(side, (0, sh * .015, ww, sh * .04), 0, ac); A.rrect(side, (0, sh * .96, ww, sh * .985), 0, ac)
    elif ring == 2: A.rrect(side, (0, sh * .93, ww, sh), 0, ac)
    elif ring == 3: A.rrect(side, (0, 0, ww, sh * .07), 0, ac)
    cw = int(min(rw * .9, sh * 1.15)); top = int(sh * .08); ch = int(sh * .84)
    lay = CUPLAY[(i * 3 + 1) % 8]
    for k in range(reps):
        x0 = int(rw * (k + .5) - cw / 2)
        cell = side.crop((x0, top, x0 + cw, top + ch))
        run(lay, s, st, cell, cw, ch)
        side.paste(cell, (x0, top))
    side = ImageChops.offset(side, int(rw / 2), 0)   # the icon camera looks at u=0: a print's centre goes there, not the seam
    im.paste(side.resize((w, sh), Image.LANCZOS), (0, 0))
    # lid
    S = int(w * .3); kind = (i * 2 + 1) % 5
    lidc = (ac, '#ffffff', s['c1'], ac, s['c2'])[kind]
    lid = A.radial(S // 4, S // 4, mix(lidc, '#ffffff', .25), mix(lidc, '#000000', .12)).resize((S, S))
    li = on(lidc, '#ffffff', s['c2'] if lum(s['c2']) < .5 else '#333333')
    hi = ac if abs(lum(ac) - lum(lidc)) > .3 else li
    s2 = dict(s, c1=lidc, c2=lidc)
    def emblem(cx, cy, r):
        if s.get('mascot'): B.mascot(lid, s, cx, cy, r)
        else: sign(lid, cx, cy, r, s if lidc != ac else dict(s, ac=on(ac)), st)
    flv = s['fl'].split(' · ')[0]
    if kind == 0:      # emblem above, name, flavour
        emblem(S * .5, S * .25, S * .1); fit(lid, (S * .15, S * .36, S * .85, S * .6), s['nat'], hi, st); small(lid, (S * .24, S * .64, S * .76, S * .76), flv, li)
    elif kind == 1:    # a ring and a band across
        ImageDraw.Draw(lid).ellipse((S * .06, S * .06, S * .94, S * .94), outline=rgb(ac)[:3], width=int(S * .025))
        A.rrect(lid, (0, S * .36, S, S * .62), 0, ac); fit(lid, (S * .14, S * .385, S * .86, S * .595), s['nat'], on(ac), st)
        small(lid, (S * .26, S * .2, S * .74, S * .31), s['sub'].split(' · ')[0], '#333333'); small(lid, (S * .26, S * .67, S * .74, S * .79), flv, '#333333')
    elif kind == 2:    # the dish in the middle, the name over it
        dish(lid, S * .5, S * .58, S * .26, s, st, kind='top'); fit(lid, (S * .16, S * .12, S * .84, S * .32), s['nat'], hi, st, stroke=5, stroke_fill=mix(lidc, '#000000', .5) if lum(hi) > .5 else '#ffffff')
    elif kind == 3:    # two halves
        d = ImageDraw.Draw(lid); d.rectangle((0, S * .52, S, S), fill=rgb('#ffffff' if lum(ac) < .8 else s['c2'])[:3])
        fit(lid, (S * .18, S * .2, S * .82, S * .48), s['nat'], li, st); small(lid, (S * .24, S * .58, S * .76, S * .72), flv, '#333333' if lum(ac) < .8 else '#ffffff'); emblem(S * .5, S * .83, S * .07)
    else:              # a plate with the name, emblem beside
        A.rrect(lid, (S * .14, S * .3, S * .86, S * .56), int(S * .06), ac); fit(lid, (S * .18, S * .33, S * .82, S * .53), s['nat'], on(ac), st)
        emblem(S * .5, S * .17, S * .08); small(lid, (S * .24, S * .62, S * .76, S * .75), flv, on(lidc))
    m = Image.new('L', (S, S), 0); ImageDraw.Draw(m).ellipse((0, 0, S, S), fill=255)
    im.paste(lid, (int(w * .15 - S / 2), int(h * .85 - S / 2)), m)
    out = im.resize((W, H), Image.LANCZOS)
    S1 = int(W * .3); fd = food(s, S1)
    m = Image.new('L', (S1, S1), 0); ImageDraw.Draw(m).ellipse((0, 0, S1, S1), fill=255)
    out.paste(fd, (int(W * .5 - S1 / 2), int(H * .85 - S1 / 2)), m)
    out.save(os.path.join(L.OUT, s['id'] + '.png'))


def tray3(s):
    from catalog import TRAYS
    st = STYLE[s['brand']]; i = st['n']
    W = H = 1024; K = L.K
    im = Image.new('RGB', (W * K, H * K), rgb(s['c2']))
    w, h = im.size; ac = s['ac']
    lx, ly = int(w * (.62 if s['shape'] == 'tray' else .55)), int(h * .55)
    Wm_, Dm_, _h = TRAYS[s['shape']]
    px = int(ly * Wm_ / Dm_)                # the lid is painted at its true proportions and squeezed into its place on the sheet
    lid = Image.new('RGB', (px, ly)); background(lid, s, st, px, ly)
    lay = {'M': 'A', 'I': 'A', 'C': 'A'}.get(st['layout'], st['layout']) if s['brand'] != 'Doshik' else st['layout']   # spare and medallion compositions looked empty or odd on a box lid
    st2 = dict(st, dish='top') if st['dish'] in ('sq', 'window', 'block', 'lift') else st
    run(lay, s, st2, lid, px, ly)
    lid = lid.resize((lx, ly), Image.LANCZOS)
    im.paste(lid, (0, 0))
    sy = int(h * .58); Wm, Dm, Hm = TRAYS[s['shape']]; P = 2 * (Wm + Dm); sh2 = h - sy
    w2 = int(sh2 * P * .97 / Hm)          # true proportions of the wall, squeezed into the sheet below
    side = Image.new('RGB', (w2, sh2)); background(side, s, dict(st, pattern=WALLPAT[i % 8]), w2, sh2)
    v = i % 3; ink = '#ffffff' if lum(s['c1']) < .6 else (s['c2'] if lum(s['c2']) < .45 else '#222222')
    fl = (s['vru'] or s['fl']) if s['lang'] == 'ru' else (s['ven'] or s['fl']).upper()
    if v == 0: A.rrect(side, (0, sh2 * .78, w2, sh2), 0, ac)
    elif v == 1: A.rrect(side, (0, 0, w2, sh2 * .22), 0, ac)
    else: A.rrect(side, (0, sh2 * .3, w2, sh2 * .72), 0, ac)
    for cen, face in [(Wm / 2, Wm), (Wm + Dm / 2, Dm), (1.5 * Wm + Dm, Wm), (2 * Wm + 1.5 * Dm, Dm)]:
        x = (1 - cen / P) * w2; hw = face / P * w2 * .4
        if v == 0:
            fit(side, (x - hw, sh2 * .1, x + hw, sh2 * .52), s['nat'], ink, st); small(side, (x - hw, sh2 * .56, x + hw, sh2 * .72), s['sub'].split(' · ')[0], ink); small(side, (x - hw, sh2 * .81, x + hw, sh2 * .97), fl, on(ac, '#ffffff', '#222222'))
        elif v == 1:
            small(side, (x - hw, sh2 * .04, x + hw, sh2 * .18), fl, on(ac, '#ffffff', '#222222')); fit(side, (x - hw, sh2 * .3, x + hw, sh2 * .72), s['nat'], ink, st); small(side, (x - hw, sh2 * .78, x + hw, sh2 * .93), s['sub'].split(' · ')[0], ink)
        else:
            small(side, (x - hw, sh2 * .08, x + hw, sh2 * .24), s['sub'].split(' · ')[0], ink); fit(side, (x - hw, sh2 * .34, x + hw, sh2 * .68), s['nat'], on(ac), st); small(side, (x - hw, sh2 * .78, x + hw, sh2 * .94), fl, ink)
    im.paste(side.resize((w, sh2), Image.LANCZOS), (0, sy))
    im.resize((W, H), Image.LANCZOS).save(os.path.join(L.OUT, s['id'] + '.png'))


def use(s):
    if s['kind'] in ('cup', 'tray'): return s['brand'] not in KEEP and s['tpl'] != 'fire'
    return s['kind'] == 'pillow' and s['tpl'] != 'fire' and not s.get('hand')


def install():
    """labels2.pillow paints its fronts through here from now on."""
    for k in list(B.PILLOW):
        if k != 'fire': B.PILLOW[k] = front


install()

if __name__ == '__main__':
    only = sys.argv[1:]
    for s in NOODLES:
        if not use(s) or (only and s['id'] not in only and s['brand'] not in only): continue
        {'pillow': B.pillow, 'cup': cup3, 'tray': tray3}[s['kind']](s); print('label', s['id'], flush=True)

"""Cigar box atlases v3 (11-09 20:18, user: styles good but boxes inside one style still look alike).
Every box gets its own lid layout, pattern, colour and font inside its style. Used by cigars.box().
Atlas layout = render_packs.pack_mesh: row0 lid | row1 left front right back | row2 bottom."""
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from packs import F, SCRIPT, SERIF9, SERIF7, CIN7, CIN9, OSW7, OSW4, BEBAS, SLAB, ABRIL, text_c, fit_font, grunge

GOLD, GOLD_D, CREAM, BLACK, WHITE = (210, 170, 80), (118, 90, 40), (244, 234, 208), (26, 22, 20), (246, 244, 238)
SILVER = (200, 202, 208)

# key: (style, variant, main colour, accent, font)
SPEC = {
    # lacquer
    'Trinidud': ('lacquer', 'frame', (22, 20, 20), GOLD, SERIF9),
    'Padrone': ('lacquer', 'monogram', (236, 232, 220), (170, 134, 50), ABRIL),
    'Ashten': ('lacquer', 'deco', (18, 30, 60), SILVER, CIN9),
    'Grandfather': ('lacquer', 'sash', (150, 22, 28), GOLD, SERIF7),
    'MyUncle': ('lacquer', 'band', (20, 20, 22), (170, 30, 36), CIN9),
    'Tatuajje': ('lacquer', 'lattice', (96, 18, 28), GOLD, ABRIL),
    'LigaPrivatta': ('lacquer', 'minimal', (44, 44, 46), (230, 230, 230), OSW7),
    # paper litho
    'Romero': ('dress', 'rays/oval', (160, 26, 32), GOLD, SERIF9),
    'Hoyo': ('dress', 'diamond/circle', (28, 58, 108), GOLD, CIN9),
    'Pounch': ('dress', 'stripes/ribbon', (196, 146, 40), (150, 30, 30), SLAB),
    'Bolivarr': ('dress', 'border/portrait', (108, 58, 30), GOLD, SERIF9),
    'RamonAllonzo': ('dress', 'checker/shield', (28, 82, 52), GOLD, CIN9),
    'GloriaCubanna': ('dress', 'dots/oval', (176, 72, 96), GOLD, SERIF9),
    'PorLarranya': ('dress', 'split/ribbon', (170, 28, 36), GOLD, ABRIL),
    'ReyDelMondo': ('dress', 'rays/rect', (26, 50, 104), GOLD, SERIF7),
    'SanchoPansa': ('dress', 'diamond/oval', (84, 40, 92), GOLD, SLAB),
    # dark wood
    'Olivva': ('dark', 'plate', ((120, 64, 38), (58, 26, 14)), (200, 160, 80), SERIF9),
    'Rocky': ('dark', 'oval', ((92, 58, 38), (40, 24, 16)), (200, 160, 80), SCRIPT),
    'Perdomio': ('dark', 'inlay', ((116, 48, 34), (54, 18, 12)), (226, 196, 140), CIN9),
    'JoyaNicaragwa': ('dark', 'corners', ((150, 98, 56), (78, 46, 24)), (200, 160, 80), SERIF7),
    'AlecBadly': ('dark', 'glass', ((60, 42, 32), (22, 14, 10)), SILVER, OSW7),
    # leather
    'Quai': ('leather', 'tooled', (76, 44, 28), GOLD, SERIF9),
    'FlorDominicanna': ('leather', 'quilt', (112, 20, 28), GOLD, CIN9),
    'VegasRobaino': ('leather', 'croc', (30, 56, 42), GOLD, SERIF7),
    'Kamacho': ('leather', 'strap', (26, 24, 24), (196, 150, 96), BEBAS),
    # cedar
    'Cohibra': ('cedar', 'label', 'natural', None, SERIF9),
    'Arturo': ('cedar', 'label', 'wash', (150, 24, 30), SERIF9),
    'Montecrusto': ('cedar', 'gilt', 'dark', None, CIN9),
    'Partagaz': ('cedar', 'ribbon', 'honey', (176, 30, 36), SERIF9),
    'Uppmann': ('cedar', 'slide', 'blond', None, SERIF7),
    'Macanuto': ('cedar', 'paint', 'natural', (52, 92, 64), ABRIL),
    'Cuabba': ('cedar', 'crate', 'grey', None, SLAB),
    'JuanLopes': ('cedar', 'corner', 'amber', (34, 58, 120), CIN9),
    'SanCristobaal': ('cedar', 'seal', 'red', None, SERIF9),
    'Diplomatikos': ('cedar', 'frame', 'natural', (34, 58, 120), CIN7),
    'Fonsecka': ('cedar', 'tissue', 'blond', None, SERIF7),
    'CuestaRay': ('cedar', 'panel', 'honey', (38, 84, 56), SCRIPT),
}


def _cg():
    import cigars
    return cigars


def faces(c):
    cg = _cg()
    w, h, dd = cg.box_dims(c)
    s = cg.box_scale(c)
    P = lambda v: v * s
    return dict(lid=(P(dd), P(0), P(dd + w), P(dd)), left=(P(0), P(dd), P(dd), P(dd + h)), front=(P(dd), P(dd), P(dd + w), P(dd + h)),
                right=(P(dd + w), P(dd), P(2 * dd + w), P(dd + h)), back=(P(2 * dd + w), P(dd), P(2 * dd + 2 * w), P(dd + h)),
                bottom=(P(dd), P(dd + h), P(dd + w), P(2 * dd + h))), P


def dk(col, k):
    return tuple(max(0, min(255, int(v * k))) for v in col)


def lt(col, k):
    return tuple(max(0, min(255, int(v + (255 - v) * k))) for v in col)


def flat(A, col, amount=10, seed=0):
    rng = np.random.default_rng(seed)
    n = np.array(Image.fromarray((rng.random((A // 8, A // 8)) * 255).astype(np.uint8)).resize((A, A), Image.BICUBIC), np.float32) / 255 - .5
    f = rng.random((A, A)).astype(np.float32) - .5
    img = np.array(col, np.float32)[None, None, :] + (n * amount + f * amount * .4)[..., None]
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), 'RGB')


def gloss(im, rect, strength=26, at=.35):
    x0, y0, x1, y1 = [int(v) for v in rect]
    if x1 - x0 < 4 or y1 - y0 < 4: return
    w, h = x1 - x0, y1 - y0
    yy, xx = np.mgrid[0:h, 0:w]
    t = (xx / w * .7 + yy / h * .3)
    g = np.exp(-((t - at) / .12) ** 2) * strength
    reg = np.array(im.crop((x0, y0, x1, y1)), np.float32) + g[..., None]
    im.paste(Image.fromarray(np.clip(reg, 0, 255).astype(np.uint8), 'RGB'), (x0, y0))


def clip_layer(im, rect, draw_fn):
    """draw on a layer the size of rect (coords local to it) and paste: nothing spills onto the next face"""
    x0, y0, x1, y1 = [int(v) for v in rect]
    lay = Image.new('RGBA', (x1 - x0, y1 - y0), (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(lay), x1 - x0, y1 - y0)
    im.paste(lay, (x0, y0), lay)


def name_of(c):
    return c['name'].upper()


def side_text(d, F_, c, P, ink, font=CIN7, gap_col=None, gap=10):
    for k in ('left', 'right'):
        x0, y0, x1, y1 = F_[k]
        text_c(d, (x0 + x1) / 2, (y0 + y1) / 2 + P(gap / 2), c['country'], fit_font(d, c['country'], font, (x1 - x0) * .6, (y1 - y0) * .24), ink)
    if gap_col is not None:
        for k in ('front', 'back', 'left', 'right'):
            x0, y0, x1, y1 = F_[k]
            d.line([(x0, y0 + P(gap)), (x1, y0 + P(gap))], fill=gap_col, width=max(2, int(P(.6))))


def clasp(d, F_, P, col, dark, kind='rect'):
    return  # 20:57 user: painted clasps look flat
    fx0, fy0, fx1, fy1 = F_['front']
    cx = (fx0 + fx1) / 2
    if kind == 'round':
        d.ellipse([cx - P(4), fy0 + P(6), cx + P(4), fy0 + P(14)], fill=col, outline=dark, width=max(1, int(P(.4))))
    else:
        d.rounded_rectangle([cx - P(6), fy0 + P(5), cx + P(6), fy0 + P(16)], radius=P(1.5), fill=col, outline=dark, width=max(1, int(P(.5))))
        d.ellipse([cx - P(1.5), fy0 + P(11), cx + P(1.5), fy0 + P(14)], fill=dark)


def hinges(d, F_, P, col, dark):
    return  # 20:57 user: painted hinges look flat
    bx0, by0, bx1, by1 = F_['back']
    for t in (.2, .8):
        hx = bx0 + (bx1 - bx0) * t
        d.rectangle([hx - P(8), by0 + P(7), hx + P(8), by0 + P(13)], fill=col, outline=dark, width=max(1, int(P(.4))))


def star(d, cx, cy, r, col):
    pts = []
    for k in range(10):
        a = -math.pi / 2 + k * math.pi / 5
        q = r if k % 2 == 0 else r * .42
        pts.append((cx + math.cos(a) * q, cy + math.sin(a) * q))
    d.polygon(pts, fill=col)


# ------------------------------------------------------------------ lacquer
def lacquer(c, path, var, col, acc, font):
    cg = _cg()
    F_, P = faces(c)
    A = 2048
    im = flat(A, col, 5, c['seed'])
    matte = var == 'minimal'
    for f in F_.values(): gloss(im, f, 0 if matte else (30 if sum(col) < 300 else 14))
    d = ImageDraw.Draw(im)
    lx0, ly0, lx1, ly1 = F_['lid']
    lw, lh = lx1 - lx0, ly1 - ly0
    cx, cy = (lx0 + lx1) / 2, (ly0 + ly1) / 2
    name = name_of(c)
    mot = c['band']['motif']
    if var == 'frame':
        for m, wd in ((P(6), P(1.4)), (P(9), P(.6))):
            d.rectangle([lx0 + m, ly0 + m, lx1 - m, ly1 - m], outline=acc, width=max(2, int(wd)))
        text_c(d, cx, cy, name, fit_font(d, name, font, lw * .5, lh * .2), acc)
        text_c(d, cx, ly0 + lh * .22, c['country'], fit_font(d, c['country'], CIN7, lw * .4, lh * .08), acc)
        if mot != 'none':
            for sx in (-1, 1): motif(d, mot, cx + sx * lw * .34, cy, min(lh * .07, lw * .05), acc, col)
    elif var == 'monogram':
        r = lh * .3
        d.ellipse([cx - r, cy - r - lh * .05, cx + r, cy + r - lh * .05], outline=acc, width=max(3, int(P(1.2))))
        d.ellipse([cx - r + P(3), cy - r - lh * .05 + P(3), cx + r - P(3), cy + r - lh * .05 - P(3)], outline=acc, width=max(1, int(P(.4))))
        text_c(d, cx, cy - lh * .05, name[0], F(font, int(r * 1.3)), acc)
        motif(d, 'crown', cx, cy - r - lh * .12, lh * .06, acc, col)
        text_c(d, cx, ly1 - lh * .14, name, fit_font(d, name, CIN7, lw * .4, lh * .08), acc)
    elif var == 'deco':
        # art-deco fans in the corners, thin parallel lines, name in the middle
        for (ax, ay, a0) in ((lx0, ly0, 0), (lx1, ly0, 90), (lx1, ly1, 180), (lx0, ly1, 270)):
            for i, rr in enumerate((lh * .34, lh * .27, lh * .2)):
                d.pieslice([ax - rr, ay - rr, ax + rr, ay + rr], a0, a0 + 90, outline=acc, width=max(2, int(P(.6))))
            for k in range(1, 6):
                a = math.radians(a0 + k * 15)
                d.line([(ax, ay), (ax + math.cos(a) * lh * .2, ay + math.sin(a) * lh * .2)], fill=acc, width=max(1, int(P(.4))))
        for yy in (cy - lh * .17, cy - lh * .15, cy + lh * .15, cy + lh * .17):
            d.line([(lx0 + lw * .25, yy), (lx1 - lw * .25, yy)], fill=acc, width=max(1, int(P(.4))))
        text_c(d, cx, cy, name, fit_font(d, name, font, lw * .45, lh * .18), acc)
    elif var == 'sash':
        def draw(ld, w, h):
            band = h * .26
            ld.polygon([(0, h * .75), (w * .35, 0), (w * .35 + band * 1.3, 0), (band * 1.3, h * .75 + band * .0), (0, h * .75 + band)], fill=(20, 18, 18, 255))
            ld.polygon([(w, h * .25), (w * .65, h), (w * .65 - band * 1.3, h), (w - band * 1.3, h * .25), (w, h * .25 - band)], fill=(20, 18, 18, 255))
        clip_layer(im, F_['lid'], draw)
        d = ImageDraw.Draw(im)
        d.rectangle([lx0 + lw * .2, cy - lh * .17, lx1 - lw * .2, cy + lh * .17], fill=(20, 18, 18), outline=acc, width=max(2, int(P(.8))))
        text_c(d, cx, cy, name, fit_font(d, name, font, lw * .5, lh * .17), acc)
        text_c(d, cx, ly0 + lh * .12, c['country'], fit_font(d, c['country'], CIN7, lw * .4, lh * .07), acc)
    elif var == 'band':
        d.rectangle([lx0, cy - lh * .18, lx1, cy + lh * .18], fill=acc)
        for yy in (cy - lh * .22, cy + lh * .22):
            d.line([(lx0, yy), (lx1, yy)], fill=GOLD, width=max(2, int(P(.5))))
        text_c(d, cx, cy, name, fit_font(d, name, font, lw * .6, lh * .2), GOLD)
        text_c(d, cx, ly0 + lh * .15, c['country'] + '  ·  %d' % c['count'], fit_font(d, c['country'] + '  ·  %d' % c['count'], CIN7, lw * .5, lh * .07), GOLD)
    elif var == 'lattice':
        def draw(ld, w, h):
            s = h / 5
            for i in range(-12, 24):
                ld.line([(i * s, 0), (i * s + h, h)], fill=acc + (255,), width=max(1, int(P(.35))))
                ld.line([(i * s, h), (i * s + h, 0)], fill=acc + (255,), width=max(1, int(P(.35))))
        clip_layer(im, F_['lid'], draw)
        d = ImageDraw.Draw(im)
        d.ellipse([cx - lw * .26, cy - lh * .26, cx + lw * .26, cy + lh * .26], fill=(20, 12, 14), outline=acc, width=max(3, int(P(1))))
        text_c(d, cx, cy, name, fit_font(d, name, font, lw * .4, lh * .16), acc)
    elif var == 'minimal':
        d.line([(lx0 + lw * .08, ly0 + lh * .3), (lx0 + lw * .55, ly0 + lh * .3)], fill=acc, width=max(2, int(P(.4))))
        f = fit_font(d, name, font, lw * .45, lh * .14)
        d.text((lx0 + lw * .08, ly0 + lh * .34), name, font=f, fill=acc)
        sub = (c.get('sub') or '') + '  /  %d' % c['count']
        d.text((lx0 + lw * .08, ly0 + lh * .55), sub, font=fit_font(d, sub, OSW4, lw * .35, lh * .06), fill=dk(acc, .7))
    # sides
    gapc = dk(col, .5)
    side_text(d, F_, c, P, acc, CIN7 if var != 'minimal' else OSW4, gapc)
    fx0, fy0, fx1, fy1 = F_['front']
    if var in ('frame', 'deco', 'band'):
        d.rectangle([fx0 + P(4), fy0 + P(3), fx1 - P(4), fy1 - P(3)], outline=acc, width=max(2, int(P(.5))))
    if var != 'minimal':
        text_c(d, (fx0 + fx1) / 2, (fy0 + fy1) / 2 + P(6), name, fit_font(d, name, CIN7, (fx1 - fx0) * .45, (fy1 - fy0) * .2), acc)
        clasp(d, F_, P, (190, 150, 70) if acc != SILVER else SILVER, (100, 76, 30) if acc != SILVER else (110, 110, 116), 'round' if var in ('deco', 'lattice') else 'rect')
    hinges(d, F_, P, (170, 136, 64) if acc != SILVER else SILVER, (100, 76, 30))
    im = im.filter(ImageFilter.GaussianBlur(.5))
    grunge(im, 3, .25).convert('RGB').save(path, quality=90)


# ------------------------------------------------------------------ paper litho
def dress(c, path, var, paper, acc, font):
    cg = _cg()
    F_, P = faces(c)
    A = 2048
    bgp, med = var.split('/')
    im = flat(A, paper, 12, c['seed'])
    d = ImageDraw.Draw(im)
    lab = CREAM
    edge = acc
    lx0, ly0, lx1, ly1 = F_['lid']
    lw, lh = lx1 - lx0, ly1 - ly0
    cx, cy = (lx0 + lx1) / 2, (ly0 + ly1) / 2
    hi = lt(paper, .18)
    lo = dk(paper, .78)

    def pattern(ld, w, h):
        if bgp == 'rays':
            R = max(w, h)
            for i in range(0, 36, 2):
                a0, a1 = i * math.pi / 18, (i + 1) * math.pi / 18
                ld.polygon([(w / 2, h / 2), (w / 2 + R * math.cos(a0), h / 2 + R * math.sin(a0)), (w / 2 + R * math.cos(a1), h / 2 + R * math.sin(a1))], fill=hi + (255,))
        elif bgp == 'diamond':
            s = h / 6
            for i in range(-2, int(w / s) + 3):
                for j in range(-1, 8):
                    x, y = i * s + (j % 2) * s / 2, j * s / 2 * 1.0
                    ld.polygon([(x, y - s * .3), (x + s * .3, y), (x, y + s * .3), (x - s * .3, y)], fill=hi + (255,))
        elif bgp == 'stripes':
            s = h / 10
            for i in range(0, int(w / s) + 2, 2):
                ld.rectangle([i * s, 0, i * s + s, h], fill=lt(paper, .45) + (255,))
        elif bgp == 'checker':
            s = h / 8
            for i in range(int(w / s) + 1):
                for j in range(9):
                    if (i + j) % 2: ld.rectangle([i * s, j * s, i * s + s, j * s + s], fill=lo + (255,))
        elif bgp == 'dots':
            s = h / 9
            for i in range(int(w / s) + 2):
                for j in range(10):
                    x, y = i * s + (j % 2) * s / 2, j * s
                    ld.ellipse([x - s * .14, y - s * .14, x + s * .14, y + s * .14], fill=lt(paper, .3) + (255,))
        elif bgp == 'split':
            ld.rectangle([0, h * .5, w, h], fill=CREAM + (255,))
            ld.rectangle([0, h * .5 - P(1.5), w, h * .5 + P(1.5)], fill=acc + (255,))
        elif bgp == 'border':
            m = P(10)
            ld.rectangle([m, m, w - m, h - m], fill=lt(paper, .08) + (255,), outline=acc + (255,), width=max(2, int(P(1))))
            for (x, y) in ((m, m), (w - m, m), (m, h - m), (w - m, h - m)):
                ld.ellipse([x - P(4), y - P(4), x + P(4), y + P(4)], fill=acc + (255,))
    clip_layer(im, F_['lid'], pattern)
    d = ImageDraw.Draw(im)
    # edging round every face
    for f in F_.values():
        x0, y0, x1, y1 = f
        e = P(3)
        for r in ([x0, y0, x1, y0 + e], [x0, y1 - e, x1, y1], [x0, y0, x0 + e, y1], [x1 - e, y0, x1, y1]):
            d.rectangle(r, fill=edge)
    name = name_of(c)
    ink = dk(paper, .8) if sum(paper) < 500 else BLACK
    mot = c['band']['motif']
    lines = name.split(' ', 1) if len(name) > 12 else [name]

    def name_in(cx_, cy_, w_, h_, col_):
        if len(lines) == 1:
            text_c(d, cx_, cy_, name, fit_font(d, name, font, w_, h_), col_)
        else:
            f = fit_font(d, max(lines, key=len), font, w_, h_ * .72)
            text_c(d, cx_, cy_ - h_ * .4, lines[0], f, col_)
            text_c(d, cx_, cy_ + h_ * .45, lines[1], f, col_)
    if med in ('oval', 'circle', 'tall'):
        rw, rh = {'oval': (lw * .34, lh * .4), 'circle': (lh * .4, lh * .4), 'tall': (lw * .22, lh * .44)}[med]
        d.ellipse([cx - rw - P(2), cy - rh - P(2), cx + rw + P(2), cy + rh + P(2)], fill=acc)
        d.ellipse([cx - rw, cy - rh, cx + rw, cy + rh], fill=lab)
        name_in(cx, cy, rw * 1.5, rh * .4, ink)
        text_c(d, cx, cy - rh * .7, c['country'], fit_font(d, c['country'], CIN7, rw * .9, rh * .13), ink)
        if mot != 'none':
            gap = lw / 2 - rw - P(5)
            for sx in (-1, 1): motif(d, mot, cx + sx * (rw + P(2) + gap / 2), cy, min(lh * .09, gap * .32), acc, paper)
    elif med == 'ribbon':
        y = cy + (lh * .0 if bgp != 'split' else -lh * .25)
        rw, rh = lw * .36, lh * .14
        for sx in (-1, 1):
            d.polygon([(cx + sx * rw, y - rh * .7), (cx + sx * (rw + lw * .08), y - rh * .7), (cx + sx * (rw + lw * .05), y), (cx + sx * (rw + lw * .08), y + rh * .7 + rh * .3), (cx + sx * rw, y + rh)], fill=dk(acc, .7))
        d.rectangle([cx - rw, y - rh, cx + rw, y + rh], fill=acc)
        name_in(cx, y, rw * 1.7, rh * 1.2, CREAM if sum(acc) < 500 else BLACK)
        if bgp == 'split':
            text_c(d, cx, ly0 + lh * .75, c['country'], fit_font(d, c['country'], CIN9, lw * .5, lh * .12), paper)
        elif mot != 'none':
            motif(d, mot, cx, ly0 + lh * .2, lh * .1, acc, paper)
            text_c(d, cx, ly1 - lh * .17, c['country'], fit_font(d, c['country'], CIN7, lw * .4, lh * .08), dk(acc, .8) if bgp == 'stripes' else (ink if sum(paper) > 400 else CREAM))
    elif med == 'shield':
        sw, sh = lh * .36, lh * .42
        d.polygon([(cx - sw, cy - sh), (cx + sw, cy - sh), (cx + sw, cy + sh * .2), (cx, cy + sh), (cx - sw, cy + sh * .2)], fill=acc)
        sw2, sh2 = sw - P(3), sh - P(3)
        d.polygon([(cx - sw2, cy - sh2), (cx + sw2, cy - sh2), (cx + sw2, cy + sh2 * .2), (cx, cy + sh2), (cx - sw2, cy + sh2 * .2)], fill=lab)
        name_in(cx, cy - sh * .15, sw * 1.6, sh * .3, ink)
        for sx in (-1, 1):
            g = lw / 2 - sw - P(4)
            text_c(d, cx + sx * (sw + P(2) + g / 2), cy, c['country'] if sx < 0 else '%d' % c['count'], fit_font(d, c['country'], CIN7, g * .7, lh * .08), CREAM)
    elif med == 'portrait':
        r = min(lh * .34, lw * .14)
        px = lx0 + lw * .22
        d.ellipse([px - r - P(2), cy - r - P(2), px + r + P(2), cy + r + P(2)], fill=acc)
        d.ellipse([px - r, cy - r, px + r, cy + r], fill=lab)
        motif(d, c['band']['motif'], px, cy, r * .8, dk(paper, .9), lab)
        text_c(d, px, cy - r * .05, name[0], F(SERIF9, int(r * .8)), dk(paper, .9))
        r = min(r, lw * .14)
        name_in(lx0 + lw * .66, cy, lw * .36, lh * .2, acc)
        text_c(d, lx0 + lw * .66, cy + lh * .27, c['country'], fit_font(d, c['country'], CIN7, lw * .26, lh * .07), acc)
    elif med == 'rect':
        rw, rh = lw * .3, lh * .3
        d.rectangle([cx - rw - P(2), cy - rh - P(2), cx + rw + P(2), cy + rh + P(2)], fill=acc)
        d.rectangle([cx - rw, cy - rh, cx + rw, cy + rh], fill=lab)
        name_in(cx, cy + rh * .1, rw * 1.6, rh * .4, ink)
        motif(d, mot if mot != 'none' else 'crown', cx, cy - rh * .62, rh * .2, acc, lab)
    # front and sides: a cream panel with the name on front/back, country on the sides; panel shapes differ by medallion
    for k in ('front', 'back', 'left', 'right'):
        x0, y0, x1, y1 = F_[k]
        if k in ('front', 'back'):
            px = (x1 - x0) * (.25 if med not in ('ribbon', 'portrait') else .1)
            if bgp == 'split':
                d.rectangle([x0, (y0 + y1) / 2, x1, y1], fill=CREAM)
            elif med in ('oval', 'circle', 'tall'):
                d.rounded_rectangle([x0 + px, y0 + P(6), x1 - px, y1 - P(5)], radius=P(5), fill=lab, outline=acc, width=max(2, int(P(.8))))
            else:
                d.rectangle([x0 + px, y0 + P(6), x1 - px, y1 - P(5)], fill=lab, outline=acc, width=max(2, int(P(.8))))
            if bgp == 'split':
                text_c(d, (x0 + x1) / 2, y0 + (y1 - y0) * .75, name, fit_font(d, name, font, (x1 - x0) * .6, (y1 - y0) * .3), paper)
            else:
                text_c(d, (x0 + x1) / 2, (y0 + y1) / 2 + P(2), name, fit_font(d, name, font, (x1 - x0) * (.44 if px > (x1 - x0) * .2 else .6), (y1 - y0) * .3), ink)
        else:
            text_c(d, (x0 + x1) / 2, (y0 + y1) / 2 + P(1), c['country'], fit_font(d, c['country'], CIN7, (x1 - x0) * .6, (y1 - y0) * .24), acc)
        d.line([(x0, y0 + P(10)), (x1, y0 + P(10))], fill=dk(paper, .55), width=max(2, int(P(.5))))
    if c.get('cuban'):
        sx = lx0 + lw * .12
        d.rectangle([sx, ly1 - P(30), sx + P(14), ly1], fill=(64, 120, 70))
        fx0, fy0 = F_['front'][0], F_['front'][1]
        d.rectangle([sx, fy0, sx + P(14), fy0 + P(20)], fill=(64, 120, 70))
    im = im.filter(ImageFilter.GaussianBlur(.5))
    grunge(im, c['seed'], .7).convert('RGB').save(path, quality=90)


# ------------------------------------------------------------------ dark wood
def darkwood(A, seed, base, dark):
    cg = _cg()
    im = cg.cedar(A, A, seed)
    a = np.array(im, np.float32) / np.array([186, 118, 74], np.float32)
    t = np.clip((1.0 - a.mean(2)) * 3.0, 0, 1)[..., None]
    img = np.array(base, np.float32) * (1 - t) + np.array(dark, np.float32) * t
    img *= (0.9 + 0.1 * a.mean(2, keepdims=True))
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), 'RGB')


def dark(c, path, var, wood, brass, font):
    cg = _cg()
    F_, P = faces(c)
    A = 2048
    im = darkwood(A, c['seed'], *wood)
    for f in F_.values(): gloss(im, f, 16)
    d = ImageDraw.Draw(im)
    brass_d = dk(brass, .55)
    lx0, ly0, lx1, ly1 = F_['lid']
    lw, lh = lx1 - lx0, ly1 - ly0
    cx, cy = (lx0 + lx1) / 2, (ly0 + ly1) / 2
    name = name_of(c)
    eng = (58, 38, 18)
    if var == 'plate':
        pw, ph = lw * .3, lh * .2
        d.rounded_rectangle([cx - pw, cy - ph, cx + pw, cy + ph], radius=P(3), fill=brass, outline=brass_d, width=max(2, int(P(.8))))
        for sx in (-1, 1):
            for sy in (-1, 1):
                x, y = cx + sx * (pw - P(3.5)), cy + sy * (ph - P(3.5))
                d.ellipse([x - P(1), y - P(1), x + P(1), y + P(1)], fill=brass_d)
        text_c(d, cx, cy - ph * .05, name, fit_font(d, name, font, pw * 1.6, ph * .55), eng)
        text_c(d, cx, cy + ph * .6, c['country'], fit_font(d, c['country'], CIN7, pw * 1.2, ph * .2), eng)
    elif var == 'oval':
        rw, rh = lw * .3, lh * .3
        d.ellipse([cx - rw, cy - rh, cx + rw, cy + rh], fill=brass, outline=brass_d, width=max(2, int(P(.8))))
        d.ellipse([cx - rw + P(2.5), cy - rh + P(2.5), cx + rw - P(2.5), cy + rh - P(2.5)], outline=brass_d, width=max(1, int(P(.4))))
        text_c(d, cx, cy, c['name'], fit_font(d, c['name'], font, rw * 1.5, rh * .5), eng)
        star(d, cx, cy - rh * .62, rh * .12, eng)
    elif var == 'inlay':
        # a light maple band across the lid, the name burned into it, thin dark strings either side
        d.rectangle([lx0, cy - lh * .16, lx1, cy + lh * .16], fill=brass)
        for yy in (cy - lh * .19, cy + lh * .19):
            d.rectangle([lx0, yy - P(.6), lx1, yy + P(.6)], fill=(230, 210, 160))
        text_c(d, cx, cy, name, fit_font(d, name, font, lw * .6, lh * .18), (70, 30, 16))
        for sx in (-1, 1): star(d, cx + sx * lw * .4, cy, lh * .07, (90, 40, 20))
        brass = (200, 160, 80); brass_d = (110, 80, 34)
    elif var == 'corners':
        m = P(5)
        d.rectangle([lx0 + m, ly0 + m, lx1 - m, ly1 - m], outline=dk(wood[1], .7), width=max(3, int(P(1.2))))
        d.rectangle([lx0 + m + P(3), ly0 + m + P(3), lx1 - m - P(3), ly1 - m - P(3)], outline=lt(wood[0], .15), width=max(1, int(P(.5))))
        cs = P(14)
        for (ax, ay, sx, sy) in ((lx0, ly0, 1, 1), (lx1, ly0, -1, 1), (lx0, ly1, 1, -1), (lx1, ly1, -1, -1)):
            d.polygon([(ax, ay), (ax + sx * cs, ay), (ax + sx * cs, ay + sy * P(3)), (ax + sx * P(3), ay + sy * P(3)), (ax + sx * P(3), ay + sy * cs), (ax, ay + sy * cs)], fill=brass, outline=brass_d)
        text_c(d, cx, cy, name, fit_font(d, name, font, lw * .6, lh * .2), brass)
        motif(d, c['band']['motif'] if c['band']['motif'] != 'none' else 'crown', cx, cy - lh * .25, lh * .08, brass, wood[0])
    elif var == 'glass':
        # 20:57 user: the cigars under glass looked bad -> silver double frame, engraved name
        m = P(7)
        d.rectangle([lx0 + m, ly0 + m, lx1 - m, ly1 - m], outline=brass, width=max(2, int(P(.8))))
        d.rectangle([lx0 + m + P(3), ly0 + m + P(3), lx1 - m - P(3), ly1 - m - P(3)], outline=brass, width=max(1, int(P(.35))))
        d.line([(cx - lw * .22, cy + lh * .13), (cx + lw * .22, cy + lh * .13)], fill=brass, width=max(1, int(P(.4))))
        text_c(d, cx, cy - lh * .02, name, fit_font(d, name, font, lw * .5, lh * .2), brass)
        text_c(d, cx, cy + lh * .22, c['country'], fit_font(d, c['country'], CIN7, lw * .3, lh * .06), brass)
    side_text(d, F_, c, P, brass if var != 'inlay' else (220, 196, 140), CIN7, (20, 10, 6))
    fx0, fy0, fx1, fy1 = F_['front']
    if var in ('glass', 'oval'):
        text_c(d, (fx0 + fx1) / 2, (fy0 + fy1) / 2 + P(7), name, fit_font(d, name, CIN7, (fx1 - fx0) * .4, (fy1 - fy0) * .18), brass)
    clasp(d, F_, P, (200, 160, 80) if brass != SILVER else SILVER, (110, 80, 34) if brass != SILVER else (110, 110, 116), 'round' if var == 'oval' else 'rect')
    hinges(d, F_, P, (200, 160, 80) if brass != SILVER else SILVER, (110, 80, 34))
    im = im.filter(ImageFilter.GaussianBlur(.5))
    grunge(im, c['seed'], .4).convert('RGB').save(path, quality=90)


# ------------------------------------------------------------------ leather
def leather_tex(A, col, seed, kind='pebble'):
    rng = np.random.default_rng(seed)
    if kind == 'croc':
        # crocodile scales: rectangular cells in rows, darker grooves
        img = np.zeros((A, A), np.float32)
        yy, xx = np.mgrid[0:A, 0:A].astype(np.float32)
        ch = 34.0
        row = (yy // ch).astype(int)
        cwid = 46.0 + (row % 3) * 8
        off = (row * 17) % 46
        cx = ((xx + off) % cwid) / cwid
        cy = (yy % ch) / ch
        groove = np.minimum(np.minimum(cx, 1 - cx) * cwid, np.minimum(cy, 1 - cy) * ch)
        k = np.clip(groove / 5, 0, 1) * .25 + .75
        bump = 1 - ((cx - .5) ** 2 + (cy - .5) ** 2) * .5
        k = k * (.9 + .1 * bump)
    else:
        small = rng.random((A // 6, A // 6)).astype(np.float32)
        cells = np.array(Image.fromarray((small * 255).astype(np.uint8)).resize((A, A), Image.NEAREST).filter(ImageFilter.GaussianBlur(2.2)), np.float32) / 255
        edge = np.abs(np.array(Image.fromarray((cells * 255).astype(np.uint8)).filter(ImageFilter.FIND_EDGES), np.float32) / 255)
        k = 0.86 + 0.14 * cells - 0.5 * np.clip(edge * 3, 0, .5)
    big = np.array(Image.fromarray((rng.random((A // 32, A // 32)) * 255).astype(np.uint8)).resize((A, A), Image.BICUBIC), np.float32) / 255
    k = k + 0.08 * (big - .5)
    img = np.array(col, np.float32)[None, None, :] * k[..., None]
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), 'RGB')


def stitches(d, rect, P, col, m=3):
    x0, y0, x1, y1 = rect
    m = P(m)
    step, ln = P(2.4), P(1.4)
    x = x0 + m
    while x < x1 - m - ln:
        for y in (y0 + m, y1 - m):
            d.line([(x, y), (x + ln, y)], fill=col, width=max(2, int(P(.35))))
        x += step
    y = y0 + m
    while y < y1 - m - ln:
        for x in (x0 + m, x1 - m):
            d.line([(x, y), (x, y + ln)], fill=col, width=max(2, int(P(.35))))
        y += step


def leather(c, path, var, col, acc, font):
    cg = _cg()
    F_, P = faces(c)
    A = 2048
    im = leather_tex(A, col, c['seed'], 'croc' if var == 'croc' else 'pebble')
    for f in F_.values(): gloss(im, f, 10)
    d = ImageDraw.Draw(im)
    acc_d = dk(acc, .55)
    stitch = lt(col, .45)
    lx0, ly0, lx1, ly1 = F_['lid']
    lw, lh = lx1 - lx0, ly1 - ly0
    cx, cy = (lx0 + lx1) / 2, (ly0 + ly1) / 2
    name = name_of(c)
    if var == 'quilt':
        def draw(ld, w, h):
            s = h / 4
            for i in range(-6, int(w / s) + 6):
                ld.line([(i * s, 0), (i * s + h, h)], fill=dk(col, .55) + (255,), width=max(2, int(P(.6))))
                ld.line([(i * s, h), (i * s + h, 0)], fill=dk(col, .55) + (255,), width=max(2, int(P(.6))))
            for i in range(-6, int(w / s) + 6):
                for j in range(0, 9):
                    x, y = i * s + (j % 2) * s / 2, j * s / 2
                    ld.ellipse([x - P(1.1), y - P(1.1), x + P(1.1), y + P(1.1)], fill=acc + (255,))
        clip_layer(im, F_['lid'], draw)
        d = ImageDraw.Draw(im)
        d.rounded_rectangle([cx - lw * .22, cy - lh * .13, cx + lw * .22, cy + lh * .13], radius=P(2), fill=acc, outline=acc_d, width=max(2, int(P(.6))))
        text_c(d, cx, cy, name, fit_font(d, name, font, lw * .4, lh * .14), dk(col, .8))
    elif var == 'croc':
        pw, ph = lw * .24, lh * .14
        d.rectangle([cx - pw, cy - ph, cx + pw, cy + ph], fill=acc, outline=acc_d, width=max(2, int(P(.6))))
        text_c(d, cx, cy, name, fit_font(d, name, font, pw * 1.7, ph * .9), dk(col, .7))
        for sx in (-1, 1):
            for sy in (-1, 1):
                x, y = cx + sx * (pw - P(2)), cy + sy * (ph - P(2))
                d.ellipse([x - P(.7), y - P(.7), x + P(.7), y + P(.7)], fill=acc_d)
    elif var == 'strap':
        # a tan strap across the lid with a buckle, the name blind-embossed (darker) on the leather
        tx = cx  # 20:57 user: strap removed
        text_c(d, tx + P(.6), cy - lh * .06 + P(.6), name, fit_font(d, name, font, lw * .5, lh * .26), dk(col, .3))
        text_c(d, tx, cy - lh * .06, name, fit_font(d, name, font, lw * .5, lh * .26), acc)
        text_c(d, tx, cy + lh * .2, c['country'], fit_font(d, c['country'], OSW4, lw * .3, lh * .07), acc)
    else:  # tooled
        cs = P(9)
        for (ax, ay, sx, sy) in ((lx0, ly0, 1, 1), (lx1, ly0, -1, 1), (lx0, ly1, 1, -1), (lx1, ly1, -1, -1)):
            d.polygon([(ax, ay), (ax + sx * cs, ay), (ax, ay + sy * cs)], fill=acc, outline=acc_d)
        d.rectangle([lx0 + lw * .12, ly0 + lh * .2, lx1 - lw * .12, ly1 - lh * .2], outline=acc, width=max(2, int(P(.7))))
        d.rectangle([lx0 + lw * .12 + P(2), ly0 + lh * .2 + P(2), lx1 - lw * .12 - P(2), ly1 - lh * .2 - P(2)], outline=acc, width=max(1, int(P(.35))))
        text_c(d, cx, cy, name, fit_font(d, name, font, lw * .6, lh * .16), acc)
        text_c(d, cx, cy + lh * .18, c['country'], fit_font(d, c['country'], CIN7, lw * .4, lh * .06), acc)
    for k, f in F_.items():
        stitches(d, f, P, stitch)
    side_text(d, F_, c, P, acc if var != 'strap' else lt(col, .3), CIN7, dk(col, .4))
    fx0, fy0, fx1, fy1 = F_['front']
    if var == 'strap':
        text_c(d, (fx0 + fx1) / 2, (fy0 + fy1) / 2 + P(8), name, fit_font(d, name, font, (fx1 - fx0) * .4, (fy1 - fy0) * .2), acc)
    else:
        clasp(d, F_, P, acc, acc_d)
        text_c(d, (fx0 + fx1) / 2, (fy0 + fy1) / 2 + P(8), name, fit_font(d, name, CIN7, (fx1 - fx0) * .4, (fy1 - fy0) * .16), acc)
    hinges(d, F_, P, acc if var != 'strap' else SILVER, acc_d)
    im = im.filter(ImageFilter.GaussianBlur(.4))
    grunge(im, c['seed'], .3).convert('RGB').save(path, quality=90)


# ------------------------------------------------------------------ cedar
# ------------------------------------------------------------------ cedar (v4, 20:35 user: the cedar boxes still all look alike)
# every cedar box: own wood tone, own lid layout, own front (not the burned name everywhere)
TONES = {
    'natural': None,
    'wash': ((150, 144, 132), (238, 232, 220)),     # white-washed
    'blond': ((150, 116, 72), (242, 214, 160)),
    'dark': ((34, 18, 12), (112, 62, 36)),          # dark stained
    'honey': ((128, 66, 20), (236, 158, 66)),
    'grey': ((70, 66, 60), (172, 164, 150)),        # weathered crate
    'red': ((84, 26, 16), (190, 92, 62)),           # mahogany stain
    'amber': ((104, 52, 22), (210, 128, 66)),
}


def retone(a, tone, mask=None):
    """map the wood luminance onto a two-colour ramp (a: float array)"""
    if TONES.get(tone) is None: return a
    lo_c, hi_c = (np.array(v, np.float32) for v in TONES[tone])
    L = a.mean(-1)
    ref = L[mask] if mask is not None else L
    lo, hi = np.percentile(ref, 2), np.percentile(ref, 98)
    t = np.clip((L - lo) / max(1, hi - lo), 0, 1)[..., None]
    out = lo_c + (hi_c - lo_c) * (.3 + .45 * t)  # soft: the stretched grain looked cartoonish
    if mask is None: return out
    a = a.copy(); a[mask] = out[mask]
    return a


def paper_strip(d, rect, paper, ink, edge, text, font):
    x0, y0, x1, y1 = rect
    d.rectangle(rect, fill=paper, outline=edge, width=max(2, int((y1 - y0) * .04)))
    text_c(d, (x0 + x1) / 2, (y0 + y1) / 2, text, fit_font(d, text, font, (x1 - x0) * .8, (y1 - y0) * .5), ink)


def cedar(c, path, var, tone, acc, font):
    cg = _cg()
    F_, P = faces(c)
    fx0, fy0, fx1, fy1 = F_['front']
    fw, fh = fx1 - fx0, fy1 - fy0
    fcx, fcy = (fx0 + fx1) / 2, (fy0 + fy1) / 2 + P(4)
    name = name_of(c)
    if var == 'label':
        # the classic box with the full paper label (v1)
        cg.box_cedar(c, path)
        if tone != 'natural' or acc is not None:
            im = Image.open(path).convert('RGB')
            a = np.array(im, np.float32)
            wood = (a[..., 0] - a[..., 1] > 48) & (a[..., 1] - a[..., 2] < 70) & (a[..., 0] > 80) & (a[..., 1] > 58) & (a[..., 2] > 34)
            wood = np.array(Image.fromarray((wood * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(3))) > 0
            a = retone(a, tone, wood)
            im = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
            if acc is not None:
                # a paper strip over the burned front name
                d = ImageDraw.Draw(im)
                paper_strip(d, (fx0 + fw * .1, fy0 + P(13), fx1 - fw * .1, fy1 - P(4)), CREAM, acc, GOLD, name, font)
            im.save(path, quality=90)
        return
    A = 2048
    im = cg.cedar(A, A, c['seed'])
    a = retone(np.array(im, np.float32), tone)
    im = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
    lx0, ly0, lx1, ly1 = F_['lid']
    lw, lh = lx1 - lx0, ly1 - ly0
    cx, cy = (lx0 + lx1) / 2, (ly0 + ly1) / 2
    b = c['band']
    mot = b['motif']
    wood_mid = tuple(int(v) for v in a[int(cy), int(lx0):int(lx1)].mean(0))
    burn = dk(wood_mid, .35)
    front = 'burn'
    gap = dk(wood_mid, .6)
    if var == 'paint':
        # painted box, worn through to the wood on edges; stencil lettering
        paint = np.array(flat(A, acc, 14, c['seed']), np.float32)
        wear = np.array(Image.effect_noise((A // 16, A // 16), 70).resize((A, A), Image.BICUBIC), np.float32) / 255
        alpha = np.zeros((A, A), np.float32)
        yy, xx = np.mgrid[0:A, 0:A]
        for k in ('lid', 'left', 'front', 'right', 'back', 'bottom'):
            x0, y0, x1, y1 = [int(v) for v in F_[k]]
            ex = np.minimum(np.minimum(xx - x0, x1 - xx), np.minimum(yy - y0, y1 - yy)).astype(np.float32)
            inside = (xx >= x0) & (xx < x1) & (yy >= y0) & (yy < y1)
            al = np.clip(ex / P(6), 0, 1) * .95 - (wear - .45) * 1.2
            alpha[inside] = np.clip(al, 0, .95)[inside]
        a = np.array(im, np.float32) * (1 - alpha[..., None]) + paint * alpha[..., None]
        im = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
        d = ImageDraw.Draw(im)
        r = lh * .3
        d.ellipse([cx - r * 1.5, cy - r, cx + r * 1.5, cy + r], outline=CREAM, width=max(3, int(P(1))))
        d.ellipse([cx - r * 1.5 + P(3), cy - r + P(3), cx + r * 1.5 - P(3), cy + r - P(3)], outline=CREAM, width=max(1, int(P(.4))))
        text_c(d, cx, cy + r * .05, name, fit_font(d, name, font, r * 2.4, r * .5), CREAM)
        motif(d, mot if mot not in ('none', 'crest', 'shield') else 'leaf', cx, cy - r * .58, r * .15, CREAM, acc)
        text_c(d, cx, cy + r * .6, c['country'], fit_font(d, c['country'], CIN7, r * 1.4, r * .15), CREAM)
        front, burn, gap = 'stencil', CREAM, dk(acc, .55)
    elif var == 'crate':
        # weathered crate: dovetails, big black stencil across lid and front, a rubber stamp
        d = ImageDraw.Draw(im)
        for k in ('front', 'back', 'left', 'right'):
            x0, y0, x1, y1 = F_[k]
            th = (y1 - y0) / 4
            for i in range(0, 4, 2):
                for xx_, s in ((x0, 1), (x1, -1)):
                    d.polygon([(xx_, y0 + i * th), (xx_ + s * P(6), y0 + i * th + th * .15), (xx_ + s * P(6), y0 + (i + 1) * th - th * .15), (xx_, y0 + (i + 1) * th)], fill=dk(wood_mid, .8))
        for t in (.12, .88):
            d.line([(lx0 + lw * t, ly0), (lx0 + lw * t, ly1)], fill=dk(wood_mid, .55), width=max(3, int(P(1.2))))
        ink = (28, 26, 24)
        text_c(d, cx, cy - lh * .05, name, fit_font(d, name, BEBAS, lw * .66, lh * .34), ink)
        def stamp(ld, w, h):
            ld.rounded_rectangle([w * .58, h * .66, w * .84, h * .88], radius=h * .03, outline=(150, 30, 30, 200), width=max(2, int(P(.8))))
            text_c(ld, w * .71, h * .77, c['country'] + ' · %d' % c['count'], fit_font(ld, c['country'] + ' · %d' % c['count'], OSW7, w * .22, h * .1), (150, 30, 30, 200))
        clip_layer(im, F_['lid'], stamp)
        front, burn = 'stencil', ink
    elif var == 'gilt':
        # dark stained box, gold leaf frame and lettering
        d = ImageDraw.Draw(im)
        m = P(6)
        d.rectangle([lx0 + m, ly0 + m, lx1 - m, ly1 - m], outline=GOLD, width=max(3, int(P(.9))))
        d.rectangle([lx0 + m + P(3), ly0 + m + P(3), lx1 - m - P(3), ly1 - m - P(3)], outline=GOLD_D, width=max(1, int(P(.4))))
        for sx in (-1, 1):
            for sy in (-1, 1):
                star(d, cx + sx * (lw / 2 - m - P(7)), cy + sy * (lh / 2 - m - P(7)), P(3), GOLD)
        text_c(d, cx, cy + lh * .04, name, fit_font(d, name, font, lw * .62, lh * .22), GOLD)
        motif(d, mot if mot != 'none' else 'crown', cx, cy - lh * .22, lh * .09, GOLD, wood_mid)
        text_c(d, cx, cy + lh * .24, c['country'], fit_font(d, c['country'], CIN7, lw * .3, lh * .07), GOLD)
        front, burn = 'gilt', GOLD
    elif var == 'ribbon':
        d = ImageDraw.Draw(im)
        rw = lw * .1
        rx = lx0 + lw * .28
        d.rectangle([rx - rw / 2, ly0, rx + rw / 2, ly1], fill=acc)
        bx0, by0, bx1, by1 = F_['back']
        d.rectangle([rx - rw / 2, fy0, rx + rw / 2, fy1], fill=acc)
        brx = bx1 - (rx - lx0)
        d.rectangle([brx - rw / 2, by0, brx + rw / 2, by1], fill=acc)
        d.rectangle([lx0 + lw * .4, cy - lh * .22, lx1 - lw * .08, cy + lh * .22], fill=CREAM, outline=GOLD, width=max(2, int(P(.8))))
        text_c(d, lx0 + lw * .66, cy, name, fit_font(d, name, font, lw * .44, lh * .16), acc)
        text_c(d, lx0 + lw * .66, cy - lh * .14, c['country'], fit_font(d, c['country'], CIN7, lw * .3, lh * .06), acc)
        d.ellipse([rx - rw * .9, cy - rw * .9, rx + rw * .9, cy + rw * .9], fill=GOLD, outline=dk(acc, .6))
        front = 'none'
    elif var == 'slide':
        # sliding lid: grooves, finger notch, branded square at the far end; plain front
        d = ImageDraw.Draw(im)
        for yy in (ly0 + P(4), ly1 - P(4)):
            d.line([(lx0, yy), (lx1, yy)], fill=dk(wood_mid, .5), width=max(2, int(P(.7))))
        d.ellipse([cx - P(10), ly1 - P(15), cx + P(10), ly1 - P(6)], fill=dk(wood_mid, .75), outline=dk(wood_mid, .5))
        sq = lh * .26
        d.rectangle([lx0 + lw * .08, cy - sq, lx0 + lw * .08 + sq * 2, cy + sq], outline=burn, width=max(3, int(P(1))))
        text_c(d, lx0 + lw * .08 + sq, cy, name[0], fit_font(d, name[0], font, sq * 1.4, sq * 1.3), burn)
        text_c(d, lx0 + lw * .68, cy - lh * .04, name, fit_font(d, name, font, lw * .4, lh * .2), burn)
        text_c(d, lx0 + lw * .68, cy + lh * .16, c['country'], fit_font(d, c['country'], CIN7, lw * .3, lh * .06), burn)
        front = 'none'
    elif var == 'panel':
        # a cream-painted lid panel with a script name; a coloured stripe round the box
        d = ImageDraw.Draw(im)
        m = P(9)
        d.rounded_rectangle([lx0 + m, ly0 + m, lx1 - m, ly1 - m], radius=P(6), fill=CREAM, outline=acc, width=max(3, int(P(1))))
        text_c(d, cx, cy - lh * .02, c['name'], fit_font(d, c['name'], SCRIPT, lw * .6, lh * .3), acc)
        d.line([(cx - lw * .2, cy + lh * .17), (cx + lw * .2, cy + lh * .17)], fill=acc, width=max(2, int(P(.6))))
        text_c(d, cx, cy + lh * .26, c['country'], fit_font(d, c['country'], CIN7, lw * .3, lh * .06), acc)
        for k in ('front', 'back', 'left', 'right'):
            x0, y0, x1, y1 = F_[k]
            d.rectangle([x0, y1 - P(9), x1, y1 - P(5)], fill=acc)
        front = 'none'
    elif var == 'corner':
        d = ImageDraw.Draw(im)
        def draw(ld, w, h):
            ld.polygon([(0, 0), (w * .38, 0), (0, h * .75)], fill=acc + (255,))
            ld.line([(w * .38, 0), (0, h * .75)], fill=GOLD + (255,), width=max(2, int(P(1))))
        clip_layer(im, F_['lid'], draw)
        d = ImageDraw.Draw(im)
        motif(d, mot if mot != 'none' else 'anchor', lx0 + lw * .1, ly0 + lh * .22, lh * .1, GOLD, acc)
        text_c(d, lx0 + lw * .6, cy + lh * .1, name, fit_font(d, name, font, lw * .55, lh * .2), burn)
        text_c(d, lx0 + lw * .6, cy + lh * .3, c['country'], fit_font(d, c['country'], CIN7, lw * .3, lh * .06), burn)
        front = 'strip'
    elif var == 'frame':
        # painted frame round the lid edge, small white label in the middle; painted band on the front
        d = ImageDraw.Draw(im)
        m = P(5)
        d.rectangle([lx0, ly0, lx1, ly1], outline=acc, width=int(m * 2))
        lw2, lh2 = lw * .2, lh * .2
        d.rectangle([cx - lw2, cy - lh2, cx + lw2, cy + lh2], fill=WHITE, outline=acc, width=max(2, int(P(1))))
        d.rectangle([cx - lw2 + P(2), cy - lh2 + P(2), cx + lw2 - P(2), cy + lh2 - P(2)], outline=acc, width=max(1, int(P(.4))))
        text_c(d, cx, cy + lh2 * .15, name, fit_font(d, name, font, lw2 * 1.7, lh2 * .4), acc)
        motif(d, mot if mot != 'none' else 'star', cx, cy - lh2 * .5, lh2 * .22, acc, WHITE)
        front = 'band'
    elif var == 'tissue':
        # wrapped in white tissue paper over the lid and the top of the front, a round seal
        tiss = np.array(flat(A, (240, 238, 232), 18, c['seed'] + 3), np.float32)
        cr = np.array(Image.effect_noise((A // 4, A // 4), 90).resize((A, A), Image.BICUBIC).filter(ImageFilter.GaussianBlur(2)), np.float32) / 255
        tiss = tiss * (.9 + cr[..., None] * .14)
        a = np.array(im, np.float32)
        for rect, al in ((F_['lid'], .9), ((fx0, fy0, fx1, fy0 + fh * .45), .9), (F_['left'], .0)):
            x0, y0, x1, y1 = [int(v) for v in rect]
            a[y0:y1, x0:x1] = a[y0:y1, x0:x1] * (1 - al) + tiss[y0:y1, x0:x1] * al
        bx0, by0, bx1, by1 = [int(v) for v in F_['back']]
        a[by0:int(by0 + fh * .45), bx0:bx1] = a[by0:int(by0 + fh * .45), bx0:bx1] * .1 + tiss[by0:int(by0 + fh * .45), bx0:bx1] * .9
        im = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
        d = ImageDraw.Draw(im)
        r = lh * .22
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(176, 30, 36), outline=GOLD, width=max(3, int(P(1))))
        d.ellipse([cx - r * .82, cy - r * .82, cx + r * .82, cy + r * .82], outline=GOLD, width=max(1, int(P(.4))))
        text_c(d, cx, cy, name, fit_font(d, name, font, r * 1.5, r * .3), CREAM)
        motif(d, mot if mot != 'none' else 'star', cx, cy - r * .5, r * .16, GOLD, (176, 30, 36))
        text_c(d, cx, cy + r * .5, c['country'], fit_font(d, c['country'], CIN7, r * 1.1, r * .14), GOLD)
        gap = (200, 196, 188)
        front = 'tissue'
    elif var == 'seal':
        # a big burned seal ring with the name round it... kept simple: a ring, initial, name below
        d = ImageDraw.Draw(im)
        r = lh * .26
        d.ellipse([cx - r, cy - r - lh * .06, cx + r, cy + r - lh * .06], outline=GOLD, width=max(4, int(P(1.4))))
        d.ellipse([cx - r * .8, cy - r * .8 - lh * .06, cx + r * .8, cy + r * .8 - lh * .06], outline=GOLD, width=max(1, int(P(.5))))
        text_c(d, cx, cy - lh * .06, name[0], fit_font(d, name[0], font, r, r * 1.1), GOLD)
        text_c(d, cx, cy + lh * .3, name, fit_font(d, name, font, lw * .5, lh * .12), GOLD)
        for sx in (-1, 1):
            d.line([(cx + sx * r * 1.3, cy - lh * .06), (cx + sx * lw * .4, cy - lh * .06)], fill=GOLD, width=max(2, int(P(.6))))
        front, burn = 'corner', GOLD
    d = ImageDraw.Draw(im)
    for k in ('front', 'back', 'left', 'right'):
        x0, y0, x1, y1 = F_[k]
        d.line([(x0, y0 + P(10)), (x1, y0 + P(10))], fill=gap, width=max(2, int(P(.6))))
    if front in ('burn', 'stencil'):
        text_c(d, fcx, fcy, name, fit_font(d, name, BEBAS if front == 'stencil' else CIN9, fw * .66, fh * .36), burn)
    elif front == 'gilt':
        d.rectangle([fx0 + P(4), fy0 + P(13), fx1 - P(4), fy1 - P(4)], outline=GOLD, width=max(2, int(P(.6))))
        text_c(d, fcx, fcy + P(1), name, fit_font(d, name, font, fw * .4, fh * .22), GOLD)
    elif front == 'strip':
        paper_strip(d, (fx0 + fw * .25, fy0 + P(15), fx1 - fw * .25, fy1 - P(6)), CREAM, acc, acc, name, font)
    elif front == 'band':
        d.rectangle([fx0, fy0 + fh * .5, fx1, fy0 + fh * .8], fill=acc)
        text_c(d, fcx, fy0 + fh * .65, name, fit_font(d, name, font, fw * .5, fh * .2), WHITE)
    elif front == 'tissue':
        text_c(d, fcx, fy0 + fh * .72, name, fit_font(d, name, CIN9, fw * .5, fh * .2), burn)
    elif front == 'corner':
        text_c(d, fx1 - fw * .2, fy1 - fh * .25, name, fit_font(d, name, font, fw * .28, fh * .16), burn)
    for k in ('left', 'right'):
        x0, y0, x1, y1 = F_[k]
        text_c(d, (x0 + x1) / 2, (y0 + y1) / 2 + P(4), c['country'], fit_font(d, c['country'], CIN7, (x1 - x0) * .6, (y1 - y0) * .3), burn)
    if c.get('cuban'):
        sx = lx0 + lw * .88
        d.rectangle([sx, ly1 - P(30), sx + P(14), ly1], fill=(64, 120, 70))
        d.rectangle([sx, fy0, sx + P(14), fy0 + P(20)], fill=(64, 120, 70))
    im = im.filter(ImageFilter.GaussianBlur(.5))
    grunge(im, 3, .5).convert('RGB').save(path, quality=90)


# 20:34 user: the little men (portrait) on Bolivarr/Gloria looked poor, and the shield (crest) repeated on other boxes
MOTIF = {'Bolivarr': 'wreath', 'GloriaCubanna': 'rose', 'Arturo': 'sun', 'JuanLopes': 'anchor', 'Diplomatikos': 'star',
         # 20:57 user: the jester read as a jellyfish; balconies ran over the lines
         'Pounch': 'clover', 'SanchoPansa': 'moon', 'Romero': 'heart', 'JoyaNicaragwa': 'diamond'}


def motif(d, kind, cx, cy, r, col, bg):
    w = max(2, int(r / 8))
    if kind == 'wreath':
        for sx in (-1, 1):
            for i in range(7):
                a = math.radians(200 - i * 26) if sx < 0 else math.radians(-20 + i * 26)
                a = math.pi - a if sx < 0 else a
                x, y = cx + sx * abs(math.cos(math.radians(110 + i * 22))) * r * .85, cy + r * .85 - i * r * .27
                x = cx + sx * r * .8 * math.sin(math.radians(25 + i * 22))
                y = cy + r * .8 * math.cos(math.radians(25 + i * 22))
                lw_, lh_ = r * .16, r * .3
                ang = (25 + i * 22) * sx
                leaf = [(x + math.cos(math.radians(ang + t)) * (lh_ if t % 180 == 0 else lw_), y - math.sin(math.radians(ang + t)) * (lh_ if t % 180 == 0 else lw_)) for t in (0, 90, 180, 270)]
                d.polygon(leaf, fill=col)
        d.arc([cx - r * .8, cy - r * .8, cx + r * .8, cy + r * .8], 20, 160, fill=col, width=w)
    elif kind == 'rose':
        for k in range(5):
            a = k * 2 * math.pi / 5 - math.pi / 2
            px, py = cx + math.cos(a) * r * .42, cy + math.sin(a) * r * .42
            d.ellipse([px - r * .4, py - r * .4, px + r * .4, py + r * .4], fill=col)
        d.ellipse([cx - r * .32, cy - r * .32, cx + r * .32, cy + r * .32], fill=bg)
        d.arc([cx - r * .22, cy - r * .22, cx + r * .22, cy + r * .22], 0, 300, fill=col, width=w)
        d.ellipse([cx - r * .08, cy - r * .08, cx + r * .08, cy + r * .08], fill=col)
    elif kind == 'sun':
        d.ellipse([cx - r * .45, cy - r * .45, cx + r * .45, cy + r * .45], fill=col)
        for k in range(12):
            a = k * math.pi / 6
            d.polygon([(cx + math.cos(a - .12) * r * .55, cy + math.sin(a - .12) * r * .55), (cx + math.cos(a) * r, cy + math.sin(a) * r),
                       (cx + math.cos(a + .12) * r * .55, cy + math.sin(a + .12) * r * .55)], fill=col)
    elif kind == 'anchor':
        d.ellipse([cx - r * .18, cy - r * .95, cx + r * .18, cy - r * .6], outline=col, width=w)
        d.line([(cx, cy - r * .6), (cx, cy + r * .85)], fill=col, width=w + 1)
        d.line([(cx - r * .45, cy - r * .35), (cx + r * .45, cy - r * .35)], fill=col, width=w)
        d.arc([cx - r * .75, cy - r * .1, cx + r * .75, cy + r * .9], 20, 160, fill=col, width=w + 1)
        for sx in (-1, 1):
            x = cx + sx * r * .72
            d.polygon([(x, cy + r * .2), (x - sx * r * .05, cy + r * .5), (x + sx * r * .2, cy + r * .42)], fill=col)
    elif kind == 'heart':
        d.ellipse([cx - r * .9, cy - r * .7, cx, cy + r * .1], fill=col)
        d.ellipse([cx, cy - r * .7, cx + r * .9, cy + r * .1], fill=col)
        d.polygon([(cx - r * .86, cy - r * .2), (cx + r * .86, cy - r * .2), (cx, cy + r * .9)], fill=col)
    elif kind == 'moon':
        d.ellipse([cx - r * .8, cy - r * .8, cx + r * .8, cy + r * .8], fill=col)
        d.ellipse([cx - r * .45, cy - r * .9, cx + r * 1.0, cy + r * .6], fill=bg)
    elif kind == 'clover':
        for a in (-90, 30, 150):
            px_, py_ = cx + math.cos(math.radians(a)) * r * .4, cy - r * .1 + math.sin(math.radians(a)) * r * .4
            d.ellipse([px_ - r * .38, py_ - r * .38, px_ + r * .38, py_ + r * .38], fill=col)
        d.line([(cx, cy + r * .1), (cx + r * .2, cy + r * .9)], fill=col, width=max(2, int(r / 6)))
    elif kind == 'diamond':
        d.polygon([(cx, cy - r), (cx + r * .7, cy), (cx, cy + r), (cx - r * .7, cy)], fill=col)
        d.polygon([(cx, cy - r * .55), (cx + r * .38, cy), (cx, cy + r * .55), (cx - r * .38, cy)], fill=bg)
        d.polygon([(cx, cy - r * .3), (cx + r * .2, cy), (cx, cy + r * .3), (cx - r * .2, cy)], fill=col)
    else:
        _cg().motif(d, kind, cx, cy, r, col, bg)


def make(c, path):
    if c['key'] in MOTIF:
        c = dict(c, band=dict(c['band'], motif=MOTIF[c['key']]))
    st, var, col, acc, font = SPEC[c['key']]
    {'lacquer': lacquer, 'dress': dress, 'dark': dark, 'leather': leather, 'cedar': cedar}[st](c, path, var, col, acc, font)


def style(c):
    return SPEC.get(c['key'], ('cedar',))[0]

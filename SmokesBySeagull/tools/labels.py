"""Band / tube / pouch label images for cigars, pouch, lighter (PIL)."""
import os, sys, math, random
from PIL import Image, ImageDraw, ImageFont, ImageFilter
from packs import F, SCRIPT, SERIF9, SERIF7, CIN7, CIN9, OSW7, OSW4, BEBAS, SLAB, ABRIL, text_c, fit_font, grunge


def band_cohibra(path):
    # wraps the cigar: 1024 x 160 (circumference ~ 61 mm, height 18 mm)
    w, h = 1024, 300
    im = Image.new('RGBA', (w, h), (214, 170, 52, 255))
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, w, 22], fill=(30, 26, 22, 255))
    d.rectangle([0, h - 22, w, h], fill=(30, 26, 22, 255))
    sq = 16
    for row in range(3):
        for i in range(w // sq):
            if (i + row) % 2 == 0:
                y = 34 + row * sq
                d.rectangle([i * sq, y, i * sq + sq - 1, y + sq - 1], fill=(30, 26, 22, 255))
                y2 = h - 34 - (row + 1) * sq
                d.rectangle([i * sq, y2, i * sq + sq - 1, y2 + sq - 1], fill=(30, 26, 22, 255))
    # front panel (centre of the image faces viewer)
    d.rounded_rectangle([w * 0.30, 90, w * 0.70, h - 90], radius=10, fill=(246, 238, 214, 255), outline=(30, 26, 22, 255), width=4)
    text_c(d, w / 2, h / 2, 'COHIBRA', fit_font(d, 'COHIBRA', SERIF9, w * 0.36, 90), (30, 26, 22, 255))
    text_c(d, w * 0.15, h / 2, 'HABANA', F(CIN7, 40), (30, 26, 22, 255))
    text_c(d, w * 0.85, h / 2, 'HABANA', F(CIN7, 40), (30, 26, 22, 255))
    im.convert('RGB').save(path)


def axes(d, cx, cy, s, fill, wd):
    for sgn in (-1, 1):
        x0, y0 = cx - sgn * s, cy + s
        x1, y1 = cx + sgn * s * 0.8, cy - s * 0.8
        d.line([(x0, y0), (x1, y1)], fill=fill, width=wd)
        # head
        hx, hy = x1, y1
        d.polygon([(hx - sgn * s * 0.05, hy - s * 0.05), (hx + sgn * s * 0.35, hy - s * 0.05), (hx + sgn * s * 0.45, hy + s * 0.35), (hx + sgn * s * 0.1, hy + s * 0.25)], fill=fill)


def band_montecrusto(path):
    w, h = 1024, 300
    im = Image.new('RGBA', (w, h), (140, 30, 34, 255))
    d = ImageDraw.Draw(im)
    gold = (222, 186, 104, 255)
    d.rectangle([0, 14, w, 24], fill=gold)
    d.rectangle([0, h - 24, w, h - 14], fill=gold)
    d.ellipse([w / 2 - 70, h / 2 - 80, w / 2 + 70, h / 2 + 60], fill=(246, 238, 220, 255), outline=gold, width=6)
    axes(d, w / 2, h / 2 - 10, 46, (140, 30, 34, 255), 9)
    text_c(d, w * 0.24, h / 2, 'MONTE', F(CIN9, 64), gold)
    text_c(d, w * 0.76, h / 2, 'CRUSTO', F(CIN9, 64), gold)
    text_c(d, w / 2, h - 50, 'HABANA', F(CIN7, 26), gold)
    im.convert('RGB').save(path)


def tube_label(path, brand):
    # tube circumference ~ 69 mm, label area 120 mm tall -> 1024 x 1792 (u around, v along)
    w, h = 1024, 1792
    if brand == 'cohibra':
        bg, ink, acc = (22, 20, 18, 255), (214, 170, 52, 255), (246, 238, 214, 255)
    else:
        bg, ink, acc = (186, 188, 192, 255), (140, 30, 34, 255), (60, 50, 50, 255)
    im = Image.new('RGBA', (w, h), bg)
    lay = Image.new('RGBA', (h, w), (0, 0, 0, 0))  # draw along the tube, then rotate
    d = ImageDraw.Draw(lay)
    name = 'COHIBRA' if brand == 'cohibra' else 'MONTECRUSTO'
    if brand == 'cohibra':
        d.rectangle([0, w * 0.30, h, w * 0.70], fill=ink)
        for i in range(0, h, 28):
            if (i // 28) % 2 == 0:
                d.rectangle([i, w * 0.30, i + 27, w * 0.33], fill=bg)
                d.rectangle([i, w * 0.67, i + 27, w * 0.70], fill=bg)
        text_c(d, h * 0.5, w * 0.5, name, fit_font(d, name, SERIF9, h * 0.62, 190), bg)
        text_c(d, h * 0.5, w * 0.20, 'ROBUSTO  ·  HECHO A MANO', F(CIN7, 50), acc)
    else:
        d.rectangle([0, w * 0.28, h, w * 0.72], fill=ink)
        d.rectangle([0, w * 0.30, h, w * 0.31], fill=(222, 186, 104, 255))
        d.rectangle([0, w * 0.69, h, w * 0.70], fill=(222, 186, 104, 255))
        text_c(d, h * 0.56, w * 0.5, name, fit_font(d, name, CIN9, h * 0.62, 170), (240, 230, 214, 255))
        axes(d, h * 0.14, w * 0.5, 80, (240, 230, 214, 255), 14)
        text_c(d, h * 0.5, w * 0.18, 'No. 2  ·  TORPEDO', F(CIN7, 50), acc)
    lay = lay.rotate(90, expand=True)
    im.alpha_composite(lay)
    grunge(im, 7, 0.6).save(path)


def ship_emblem(im, cx, cy, r, gold, cream, navy, ss=4):
    """Round medallion with a three-masted sailing ship on waves, drawn 4x and downsampled for smooth edges."""
    S = int(r * 2.2) * ss
    lay = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    c = S / 2
    R = r * ss
    d.ellipse([c - R, c - R, c + R, c + R], fill=gold)
    d.ellipse([c - R * 0.93, c - R * 0.93, c + R * 0.93, c + R * 0.93], fill=navy)
    d.ellipse([c - R * 0.88, c - R * 0.88, c + R * 0.88, c + R * 0.88], fill=cream)
    ink = navy
    u = R / 100.0  # emblem units, 100 = radius

    def P(x, y):
        return (c + x * u, c + y * u)

    def curve(p0, p1, p2, n=24):
        out = []
        for i in range(n + 1):
            t = i / n
            out.append(P((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
                         (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]))
        return out
    # hull: sheer line rising to bow (right) and stern (left), round bottom
    hull = curve((-62, 22), (0, 30), (66, 14)) + [P(56, 26)] + curve((56, 26), (0, 52), (-52, 40))[1:] + [P(-60, 30)]
    d.polygon(hull, fill=ink)
    d.line(curve((-60, 28), (0, 36), (60, 22)), fill=gold, width=int(2.2 * u))  # gold rub strake
    # masts
    masts = [(-26, -58), (6, -72), (36, -50)]
    for x, top in masts:
        d.line([P(x, 26), P(x, top)], fill=ink, width=int(3 * u))
    # bowsprit
    d.line([P(60, 18), P(86, 4)], fill=ink, width=int(2.6 * u))
    # square sails, billowing to the right (wind from stern)
    for x, top in masts:
        h = 24 - top
        rows = [(top + 6, top + h * 0.40, 1.0), (top + h * 0.45, top + h * 0.85, 1.25)]
        for y0, y1, wmul in rows:
            w0, w1 = 13 * wmul * 0.85, 13 * wmul
            belly = 5 * wmul
            pts = [P(x - w0, y0), P(x + w0, y0)] + curve((x + w0, y0), (x + w1 + belly, (y0 + y1) / 2), (x + w1, y1))[1:] \
                + [P(x - w1, y1)] + curve((x - w1, y1), (x - w1 + belly, (y0 + y1) / 2), (x - w0, y0))[1:]
            d.polygon(pts, fill=ink)
    # jib from foremast to bowsprit
    d.polygon([P(39, -44), P(84, 6)] + curve((84, 6), (60, 4), (40, 14))[1:], fill=ink)
    # pennant
    d.polygon([P(6, -72), P(24, -68), P(6, -64)], fill=gold)
    # waves: three scalloped rows clipped to the medallion
    wav = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    wd = ImageDraw.Draw(wav)
    for k, (y, amp, wdt) in enumerate([(44, 5, 3.2), (56, 5, 2.8), (68, 4, 2.4)]):
        pts = []
        for i in range(0, 241):
            x = -120 + i
            pts.append(P(x, y + amp * math.sin((x + k * 9) / 7.0) ** 2 * -1))
        wd.line(pts, fill=ink, width=int(wdt * u))
    mask = Image.new('L', (S, S), 0)
    ImageDraw.Draw(mask).ellipse([c - R * 0.86, c - R * 0.86, c + R * 0.86, c + R * 0.86], fill=255)
    wav.putalpha(Image.composite(wav.getchannel('A'), Image.new('L', (S, S), 0), mask))
    lay.alpha_composite(wav)
    lay = lay.resize((S // ss, S // ss), Image.LANCZOS)
    im.alpha_composite(lay, (int(cx - lay.width / 2), int(cy - lay.height / 2)))


def pouch_label(path):
    # front | back side by side, 2048 x 1365 (90 x 120 mm each)
    w, h = 2048, 1365
    half = w // 2
    im = Image.new('RGBA', (w, h), (36, 40, 46, 255))
    d = ImageDraw.Draw(im)
    gold, cream = (212, 178, 98, 255), (236, 228, 208, 255)
    for x0 in (0, half):
        d.rectangle([x0 + 40, 40, x0 + half - 40, h - 40], outline=gold, width=8)
        d.rectangle([x0, 0, x0 + half, 26], fill=(70, 72, 78, 255))  # crimp
        for i in range(0, half, 14):
            d.line([(x0 + i, 0), (x0 + i, 26)], fill=(52, 54, 60, 255), width=4)
    # front
    cx = half / 2
    ship_emblem(im, cx, 335, 150, gold, cream, (36, 40, 46, 255))
    text_c(d, cx, 600, 'CAPTAIN', F(BEBAS, 210), cream)
    text_c(d, cx, 790, 'BLOCK', F(SLAB, 220), gold)
    d.line([(cx - 300, 920), (cx + 300, 920)], fill=gold, width=6)
    text_c(d, cx, 990, 'AROMATIC PIPE TOBACCO', F(OSW7, 54), cream)
    text_c(d, cx, 1090, '50 g', F(OSW4, 64), cream)
    # back
    bx = half + half / 2
    text_c(d, bx, 220, 'CAPTAIN BLOCK', F(BEBAS, 120), gold)
    d.rectangle([bx - 380, 400, bx + 380, 700], fill=(240, 240, 236, 255))
    d.rectangle([bx - 370, 410, bx + 370, 690], fill=(20, 20, 20, 255))
    d.rectangle([bx - 360, 420, bx + 360, 680], fill=(240, 240, 236, 255))
    text_c(d, bx, 500, 'PIPES ARE NOT', F(OSW7, 90), (20, 20, 20, 255))
    text_c(d, bx, 610, 'A SAFE CHOICE EITHER', F(OSW7, 70), (20, 20, 20, 255))
    text_c(d, bx, 900, 'Cavendish & Burley blend', F(SCRIPT, 90), cream)
    text_c(d, bx, 1060, 'RESEAL AFTER USE', F(OSW4, 48), cream)
    grunge(im, 11, 0.8).save(path)


if __name__ == '__main__':
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    band_cohibra(os.path.join(out, 'band_cohibra.png'))
    band_montecrusto(os.path.join(out, 'band_montecrusto.png'))
    tube_label(os.path.join(out, 'tube_cohibra.png'), 'cohibra')
    tube_label(os.path.join(out, 'tube_montecrusto.png'), 'montecrusto')
    pouch_label(os.path.join(out, 'pouch_captainblock.png'))
    print('ok')

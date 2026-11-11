"""Cigarette texture: u around (1024), v along (2048) for 84 mm; filter 25 mm at v=0."""
import os, sys, random
from PIL import Image, ImageDraw, ImageFilter, ImageChops
from packs import F, SERIF9, OSW7, CIN7, SCRIPT

L_MM, FIL_MM = 84.0, 25.0
TW, TH = 1024, 2048


def cork(w, h, seed=3):
    rnd = random.Random(seed)
    im = Image.new('RGB', (w, h), (204, 138, 70))
    d = ImageDraw.Draw(im)
    for _ in range(26000):
        x, y = rnd.uniform(0, w), rnd.uniform(0, h)
        rx, ry = rnd.uniform(1.2, 4.5), rnd.uniform(0.8, 3)
        c = rnd.choice([(176, 104, 46), (186, 114, 52), (168, 96, 40), (214, 150, 84), (224, 162, 96), (150, 84, 34)])
        d.ellipse([x - rx, y - ry, x + rx, y + ry], fill=c)
    im = im.filter(ImageFilter.GaussianBlur(1.1))
    n = Image.effect_noise((w, h), 30).convert('RGB')
    im = ImageChops.multiply(im, n.point(lambda v: 200 + v // 5))
    return im


def paper(w, h):
    im = Image.new('RGB', (w, h), (240, 238, 232))
    d = ImageDraw.Draw(im)
    for x in range(0, w, 6):
        d.line([(x, 0), (x, h)], fill=(232, 230, 224), width=1)
    n = Image.effect_noise((w, h), 10).convert('RGB')
    return ImageChops.multiply(im, n.point(lambda v: 235 + v // 12))


def cigarette_tex(path, brand_text='MILSTON', ink=(170, 26, 34), ring=(196, 160, 82)):
    im = Image.new('RGB', (TW, TH))
    fil = int(TH * FIL_MM / L_MM)
    im.paste(paper(TW, TH - fil), (0, 0))
    im.paste(cork(TW, fil), (0, TH - fil))  # v=0 at image bottom
    d = ImageDraw.Draw(im)
    # tipping-paper edge (slightly lighter overlap) + thin rings
    d.rectangle([0, TH - fil - 6, TW, TH - fil], fill=(214, 156, 92))
    d.rectangle([0, TH - fil - 26, TW, TH - fil - 20], fill=ring)
    d.rectangle([0, TH - fil - 34, TW, TH - fil - 31], fill=ring)
    # side seam
    d.line([(TW * 0.02, 0), (TW * 0.02, TH - fil)], fill=(226, 224, 216), width=3)
    # brand along the cigarette, facing u=0.5
    lay = Image.new('RGBA', (600, 120), (0, 0, 0, 0))
    ld = ImageDraw.Draw(lay)
    ld.text((300, 60), brand_text, font=F(SERIF9, 70), fill=ink + (255,), anchor='mm')
    lay = lay.rotate(90, expand=True)
    im.paste(lay, (int(TW * 0.5 - 60), TH - fil - 120 - 600), lay)
    im.save(path)


def end_tex(path, kind):
    w = 512
    rnd = random.Random(1)
    if kind == 'filter':
        im = Image.new('RGB', (w, w), (236, 232, 218))
        d = ImageDraw.Draw(im)
        for _ in range(5000):
            x, y = rnd.uniform(0, w), rnd.uniform(0, w)
            c = rnd.randint(205, 248)
            d.ellipse([x - 1.5, y - 1.5, x + 1.5, y + 1.5], fill=(c, c - 3, c - 14))
        d.ellipse([20, 20, w - 20, w - 20], outline=(214, 206, 186), width=14)
        im = im.filter(ImageFilter.GaussianBlur(1))
    else:
        im = Image.new('RGB', (w, w), (74, 42, 18))
        d = ImageDraw.Draw(im)
        for _ in range(2500):
            x, y = rnd.uniform(0, w), rnd.uniform(0, w)
            l = rnd.uniform(4, 22)
            import math
            a = rnd.uniform(0, 3.14)
            c = rnd.choice([(48, 26, 10), (110, 66, 28), (140, 90, 40), (90, 52, 20)])
            d.line([(x, y), (x + l * math.cos(a), y + l * math.sin(a))], fill=c, width=rnd.randint(2, 4))
    im.save(path)


if __name__ == '__main__':
    out = sys.argv[1]
    cigarette_tex(os.path.join(out, 'cigarette_milston.png'))
    end_tex(os.path.join(out, 'cig_filter_end.png'), 'filter')
    end_tex(os.path.join(out, 'cig_tobacco_end.png'), 'tobacco')
    print('ok')

"""Hand-drawn, chalky lighter like the journal's own collection pictures (white brush shape on transparent).
python3 draw_lighter_art.py <outdir>  -> coll_lighter_big.png (512), coll_lighter_icon.png (64), coll_lighter_shape.png (256, for unfound cells)"""
import sys, os, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

S = 1024
rng = np.random.default_rng(7)

def noise(scale, seed):
    r = np.random.default_rng(seed)
    n = max(2, S // scale)
    a = r.random((n, n)).astype(np.float32)
    return np.asarray(Image.fromarray((a * 255).astype(np.uint8)).resize((S, S), Image.BICUBIC), np.float32) / 255

def shape(simple=False):
    m = Image.new('L', (S, S), 0)
    d = ImageDraw.Draw(m)
    W = 18 if simple else 26           # stroke
    # body
    bx0, bx1, by0, by1 = 330, 650, 470, 900
    d.rounded_rectangle((bx0, by0, bx1, by1), 46, fill=255)
    # seam line between body and closed part of the case (cut into the body)
    d.rectangle((bx0 + 2, by0 + 92, bx1 - 2, by0 + 92 + W * .55), fill=0)
    # chimney (outline with holes) on top of the body
    cx0, cx1, cy0, cy1 = 372, 560, 250, 470
    d.rectangle((cx0, cy0, cx1, cy1), outline=255, width=W)
    if not simple:
        for row in range(3):
            for col in range(3):
                x = cx0 + 42 + col * 52; y = cy0 + 48 + row * 52
                d.ellipse((x - 13, y - 13, x + 13, y + 13), fill=255)
    # flint wheel at the right of the chimney
    d.ellipse((560, 300, 640, 380), outline=255, width=W)
    d.ellipse((590, 330, 610, 350), fill=255)
    # open lid hinged on the right, tilted out
    lid = Image.new('L', (S, S), 0); ld = ImageDraw.Draw(lid)
    ld.rounded_rectangle((650, 250, 820, 470), 40, outline=255, width=W)
    ld.rectangle((650, 400, 820, 470), fill=255)
    lid = lid.rotate(-18, center=(650, 470), resample=Image.BICUBIC)
    m = Image.fromarray(np.maximum(np.asarray(m), np.asarray(lid)))
    d = ImageDraw.Draw(m)
    # hinge
    d.rounded_rectangle((636, 440, 676, 520), 14, fill=255)
    # flame: teardrop above the chimney
    pts = []
    fx, fy = 466, 228
    for i in range(80):
        t = i / 79 * 2 * math.pi
        r = 70 * (1 - math.sin(t)) ** 0.9 * 0.5 + 6
        x = fx + r * math.cos(t) * 1.0
        y = fy - 70 + 70 * math.sin(t) * 1.25 - (40 if math.sin(t) < 0 else 0) * (-math.sin(t))
        pts.append((x, y))
    d.polygon(pts, fill=255)
    # inner flame cut
    if not simple:
        ip = [(fx + (x - fx) * .45, fy - 20 + (y - fy) * .5) for x, y in pts]
        d.polygon(ip, fill=0)
    return np.asarray(m, np.float32) / 255

def chalk(m, grain=True, seed=1):
    blur = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(6)), np.float32) / 255
    n = noise(48, seed) * .5 + noise(16, seed + 1) * .3 + noise(5, seed + 2) * .2
    a = np.clip((blur - .5 + (n - .5) * .55) * 9 + .5, 0, 1)
    if grain:
        g = noise(3, seed + 3) * .6 + noise(10, seed + 4) * .4
        a *= np.clip(.62 + g * .55, 0, 1)
        # a few dry-brush streaks
        streak = np.asarray(Image.fromarray((noise(30, seed + 5) * 255).astype(np.uint8)).resize((S, S // 40)).resize((S, S), Image.BICUBIC), np.float32) / 255
        a *= np.clip(.7 + streak * .45, 0, 1)
        # specks around the shape
        sp = Image.new('L', (S, S), 0); sd = ImageDraw.Draw(sp)
        r = random.Random(seed)
        for _ in range(26):
            x, y = r.randint(200, 860), r.randint(120, 950)
            if blur[y, x] > .05: continue
            rr = r.choice([3, 4, 5, 7, 9])
            sd.ellipse((x - rr, y - rr, x + rr, y + rr), fill=r.randint(150, 255))
        a = np.maximum(a, np.asarray(sp, np.float32) / 255 * .8)
    return a

def save(a, size, path, col=(232, 232, 230), pad=.08):
    ys, xs = np.where(a > .05)
    cx, cy = (xs.min() + xs.max()) / 2, (ys.min() + ys.max()) / 2
    half = max(xs.max() - xs.min(), ys.max() - ys.min()) / 2 * (1 + pad)
    box = (int(cx - half), int(cy - half), int(cx + half), int(cy + half))
    rgba = np.zeros((S, S, 4), np.uint8)
    rgba[..., 0], rgba[..., 1], rgba[..., 2] = col
    rgba[..., 3] = (np.clip(a, 0, 1) * 255).astype(np.uint8)
    Image.fromarray(rgba).crop(box).resize((size, size), Image.LANCZOS).save(path)

out = sys.argv[1]; os.makedirs(out, exist_ok=True)
save(chalk(shape()), 512, os.path.join(out, 'coll_lighter_big.png'), pad=.35)
save(chalk(shape(simple=True), grain=False, seed=4), 64, os.path.join(out, 'coll_lighter_icon.png'), pad=.04)
save(chalk(shape(simple=True), grain=False, seed=9), 256, os.path.join(out, 'coll_lighter_shape.png'))

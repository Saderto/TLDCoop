"""Effect icons: white glyphs, 128 px -> ../fx/fx_<id>.png"""
import os, math
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__)); OUT = os.path.join(HERE, '..', 'fx'); os.makedirs(OUT, exist_ok=True)
K = 4; S = 128 * K; W = (255, 255, 255, 255); Z = (0, 0, 0, 0)
def new(): im = Image.new('RGBA', (S, S), Z); return im, ImageDraw.Draw(im)
def P(*a): return [(x * K, y * K) for x, y in zip(a[::2], a[1::2])]
def save(im, n): im.resize((128, 128), Image.LANCZOS).save(os.path.join(OUT, 'fx_' + n + '.png'))
def rot(im, a): return im.rotate(a, resample=Image.BICUBIC)
def teardrop(d, cx, cy, w, h, fill=W):
    pts = []
    for i in range(60):
        t = i / 60 * 2 * math.pi; r = math.sin(t / 2) ** 1.0
        pts.append(((cx + w * math.sin(t) * (0.55 + .45 * math.sin(t / 2))) * K, (cy - h * math.cos(t / 2) * 2 + h) * K))
    d.polygon(pts, fill=fill)
def wave(d, x, y0, y1, amp, w, n=2.0):
    pts = [((x + amp * math.sin((y - y0) / (y1 - y0) * n * 2 * math.pi)) * K, y * K) for y in range(y0, y1 + 1)]
    d.line(pts, fill=W, width=w * K, joint='curve')
    for p in (pts[0], pts[-1]): d.ellipse((p[0] - w * K / 2, p[1] - w * K / 2, p[0] + w * K / 2, p[1] + w * K / 2), fill=W)

# seeds: three striped sunflower seeds in a fan
im, d = new()
for a, dx in ((28, -30), (0, 0), (-28, 30)):
    l, ld = new(); ld.ellipse(P(46, 22, 82, 104), fill=W); ld.polygon(P(46, 50, 64, 14, 82, 50), fill=W)
    for x in (56, 64, 72): ld.line(P(x, 40, x, 96), fill=Z, width=3 * K)
    l = rot(l, a); im.alpha_composite(l.transform(l.size, Image.AFFINE, (1, 0, -dx * K * .9, 0, 1, -abs(dx) * K * .25)))
d = ImageDraw.Draw(im); d.ellipse(P(84, 6, 122, 36), fill=Z); d.ellipse(P(88, 10, 118, 32), fill=W)
for x in (60, 70, 80): d.line(P(x - 14, 8 + (x - 60) * .3, x - 6, 12 + (x - 60) * .3), fill=W, width=4 * K)
save(im, 'stoneaim')

# peanut with a bolt
im, d = new(); d.ellipse(P(34, 10, 94, 66), fill=W); d.ellipse(P(30, 56, 98, 120), fill=W); d.rectangle(P(46, 50, 82, 72), fill=W)
d.polygon(P(70, 26, 48, 68, 62, 68, 54, 102, 82, 56, 66, 56), fill=Z)
im = rot(im, -18); save(im, 'nutpower')

# jerky strip with a bite
im, d = new()
top = [(x, 44 + 7 * math.sin(x / 11)) for x in range(12, 117, 2)]; bot = [(x, 86 + 7 * math.sin(x / 11 + 1)) for x in range(116, 11, -2)]
d.polygon([(x * K, y * K) for x, y in top + bot], fill=W)
for cx, cy in ((116, 56), (112, 70), (118, 82)): d.ellipse(P(cx - 9, cy - 9, cx + 9, cy + 9), fill=Z)
for x0 in (26, 50, 74): d.line([((x0 + i) * K, (58 + 5 * math.sin((x0 + i) / 5) + i * .5) * K) for i in range(0, 16)], fill=Z, width=4 * K)
im = rot(im, 20); save(im, 'sinew')

# candy in a wrapper
im, d = new(); d.ellipse(P(38, 38, 90, 90), fill=W); d.polygon(P(42, 64, 8, 38, 8, 90), fill=W); d.polygon(P(86, 64, 120, 38, 120, 90), fill=W)
d.arc(P(48, 48, 80, 80), 200, 340, fill=Z, width=5 * K); d.line(P(20, 54, 30, 64, 20, 74), fill=Z, width=3 * K); d.line(P(108, 54, 98, 64, 108, 74), fill=Z, width=3 * K)
im = rot(im, 25); d = ImageDraw.Draw(im)
d.polygon(P(86, 12, 98, 8, 110, 12, 122, 8, 122, 36, 110, 40, 98, 36, 86, 40), fill=W); d.line(P(98, 10, 98, 36), fill=Z, width=2 * K); d.line(P(110, 12, 110, 38), fill=Z, width=2 * K)
save(im, 'sugarmap')

# chilli pepper
im, d = new()
pts = []
for i in range(41):
    t = i / 40; x = 30 + 70 * t; y = 34 + 62 * t ** 1.6; w = 17 * (1 - t) ** .7 + 1; pts.append((x + w * .6, y - w))
for i in range(40, -1, -1):
    t = i / 40; x = 30 + 70 * t; y = 34 + 62 * t ** 1.6; w = 17 * (1 - t) ** .7 + 1; pts.append((x - w * .6, y + w))
d.polygon([(x * K, y * K) for x, y in pts], fill=W); d.ellipse(P(16, 22, 50, 54), fill=W)
d.line(P(30, 30, 22, 8), fill=W, width=8 * K); d.ellipse(P(18, 4, 26, 12), fill=W)
d.line(P(44, 42, 80, 78), fill=Z, width=3 * K)
save(im, 'pepperrun')

# greasy hand with drops
im, d = new(); d.rounded_rectangle(P(34, 56, 88, 118), radius=14 * K, fill=W)
for x, y in ((34, 26), (48, 14), (62, 18), (76, 30)): d.rounded_rectangle(P(x, y, x + 12, 80), radius=6 * K, fill=W)
l, ld = new(); ld.rounded_rectangle(P(58, 70, 72, 112), radius=7 * K, fill=W); l = rot(l, -48); im.alpha_composite(l.transform(l.size, Image.AFFINE, (1, 0, -36 * K, 0, 1, 12 * K)))
d = ImageDraw.Draw(im)
for cx, cy, s in ((108, 34, 1.0), (20, 96, .8)):
    d.polygon(P(cx, cy - 16 * s, cx - 9 * s, cy + 2, cx + 9 * s, cy + 2), fill=W); d.ellipse(P(cx - 9 * s, cy - 6 * s, cx + 9 * s, cy + 12 * s), fill=W)
d.ellipse(P(52, 84, 70, 102), fill=Z)
save(im, 'greasy')

# cracker and crumbs
im, d = new(); l, ld = new(); ld.rounded_rectangle(P(22, 22, 88, 88), radius=8 * K, fill=W)
for x in (38, 55, 72):
    for y in (38, 55, 72): ld.ellipse(P(x - 4, y - 4, x + 4, y + 4), fill=Z)
ld.polygon(P(88, 22, 60, 22, 70, 36, 64, 46, 78, 50, 88, 62), fill=Z)
im.alpha_composite(rot(l, 12)); d = ImageDraw.Draw(im)
for x, y, r in ((98, 70, 7), (110, 92, 5), (86, 104, 6), (104, 112, 4), (66, 112, 4), (116, 54, 4)): d.polygon(P(x - r, y, x, y - r, x + r, y, x, y + r), fill=W)
save(im, 'ballast')

# open mouth, a crouton with sharp corners inside
im, d = new(); d.ellipse(P(6, 22, 122, 110), fill=W); d.ellipse(P(18, 34, 110, 98), fill=Z)
for x in (34, 50, 66, 82): d.polygon(P(x - 7, 33, x + 7, 33, x, 47), fill=W)
l, ld = new(); ld.polygon(P(40, 52, 88, 48, 92, 84, 44, 88), fill=W); ld.line(P(52, 62, 78, 60), fill=Z, width=4 * K); ld.line(P(54, 74, 80, 72), fill=Z, width=4 * K)
im.alpha_composite(rot(l, -8))
save(im, 'palate')

# a puffed bag carried by the wind
im, d = new(); l, ld = new()
ld.rounded_rectangle(P(52, 26, 112, 104), radius=20 * K, fill=W); ld.rectangle(P(56, 14, 108, 30), fill=W); ld.rectangle(P(56, 100, 108, 116), fill=W)
for x in range(60, 108, 8): ld.line(P(x, 14, x, 22), fill=Z, width=2 * K); ld.line(P(x, 108, x, 116), fill=Z, width=2 * K)
ld.ellipse(P(68, 50, 96, 78), fill=Z)
im.alpha_composite(rot(l, -14)); d = ImageDraw.Draw(im)
for y, x0, x1 in ((40, 6, 40), (64, 2, 34), (88, 10, 44)): d.line(P(x0, y, x1, y), fill=W, width=6 * K); d.ellipse(P(x0 - 3, y - 3, x0 + 3, y + 3), fill=W); d.ellipse(P(x1 - 3, y - 3, x1 + 3, y + 3), fill=W)
save(im, 'windblown')
ids = ['stoneaim', 'nutpower', 'sinew', 'sugarmap', 'pepperrun', 'ballast', 'greasy', 'palate', 'windblown']
sh = Image.new('RGBA', (140 * len(ids), 140), (40, 44, 50, 255))
for i, n in enumerate(ids): sh.alpha_composite(Image.open(os.path.join(OUT, 'fx_' + n + '.png')), (i * 140 + 6, 6))
sh.save(os.path.join(HERE, '..', 'prev', 'fx_sheet.png'))

"""Effect icons for the status panel: white glyphs, 128 px -> ../fx/fx_<key>.png"""
import os, math
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__)); OUT = os.path.join(HERE, '..', 'fx'); os.makedirs(OUT, exist_ok=True)
K = 4; S = 128 * K; W = (255, 255, 255, 255); Z = (0, 0, 0, 0)
def new(): im = Image.new('RGBA', (S, S), Z); return im, ImageDraw.Draw(im)
def P(*a): return [(x * K, y * K) for x, y in zip(a[::2], a[1::2])]
def save(im, n): im.resize((128, 128), Image.LANCZOS).save(os.path.join(OUT, 'fx_' + n + '.png'))

# hardy: a shield with a heart cut out
im, d = new()
d.polygon(P(64, 8, 112, 24, 112, 62, 100, 94, 64, 120, 28, 94, 16, 62, 16, 24), fill=W)
d.ellipse(P(42, 40, 66, 64), fill=Z); d.ellipse(P(62, 40, 86, 64), fill=Z); d.polygon(P(43, 57, 85, 57, 64, 86), fill=Z)
save(im, 'hardy')

# antidote: a flask with a cross
im, d = new()
d.rectangle(P(50, 8, 78, 20), fill=W); d.rectangle(P(54, 18, 74, 48), fill=W)
d.polygon(P(54, 44, 74, 44, 108, 104, 100, 120, 28, 120, 20, 104), fill=W)
d.rectangle(P(58, 66, 70, 110), fill=Z); d.rectangle(P(42, 82, 86, 94), fill=Z)
save(im, 'antidote')

# map: a folded map with a route and a mark
im, d = new()
d.polygon(P(10, 28, 44, 14, 84, 28, 118, 14, 118, 100, 84, 114, 44, 100, 10, 114), fill=W)
d.line(P(44, 14, 44, 100), fill=Z, width=3 * K); d.line(P(84, 28, 84, 114), fill=Z, width=3 * K)
for a, b in (((22, 92), (34, 78)), ((40, 70), (54, 62)), ((62, 60), (76, 68)), ((84, 72), (96, 60))):
    d.line(P(*a, *b), fill=Z, width=5 * K)
d.ellipse(P(92, 36, 112, 56), fill=Z); d.ellipse(P(98, 42, 106, 50), fill=W)
save(im, 'map')

# nosleep: a wide open eye with lashes
im, d = new()
pts = [(8 + 112 * t, 68 - 34 * math.sin(math.pi * t)) for t in [i / 40 for i in range(41)]] + [(120 - 112 * t, 68 + 34 * math.sin(math.pi * t)) for t in [i / 40 for i in range(41)]]
d.polygon([(x * K, y * K) for x, y in pts], fill=W)
d.ellipse(P(42, 46, 86, 90), fill=Z); d.ellipse(P(54, 58, 74, 78), fill=W)
for x0, y0, x1, y1 in ((64, 26, 64, 8), (40, 32, 32, 16), (88, 32, 96, 16), (20, 44, 8, 32), (108, 44, 120, 32)):
    d.line(P(x0, y0, x1, y1), fill=W, width=7 * K)
save(im, 'nosleep')

# relaxed: a leaf drifting down over two soft waves
im, d = new()
l = Image.new('RGBA', (S, S), Z); ld = ImageDraw.Draw(l)
pts = [(64 + 22 * math.sin(math.pi * t) , 12 + 72 * t) for t in [i / 30 for i in range(31)]] + [(64 - 22 * math.sin(math.pi * t), 84 - 72 * t) for t in [i / 30 for i in range(31)]]
ld.polygon([(x * K, y * K) for x, y in pts], fill=W); ld.line(P(64, 20, 64, 96), fill=Z, width=3 * K); ld.line(P(64, 84, 64, 100), fill=W, width=5 * K)
im.alpha_composite(l.rotate(-32, resample=Image.BICUBIC, center=(64 * K, 56 * K)))
d = ImageDraw.Draw(im)
for y in (104, 118):
    d.line([((14 + x) * K, (y + 4 * math.sin(x / 9)) * K) for x in range(0, 101, 2)], fill=W, width=6 * K, joint='curve')
save(im, 'relaxed')

# vitc: a citrus slice
im, d = new()
d.ellipse(P(10, 10, 118, 118), fill=W); d.ellipse(P(22, 22, 106, 106), fill=Z)
for i in range(8):
    a0 = i * 45 + 5; d.pieslice(P(28, 28, 100, 100), a0, a0 + 35, fill=W)
d.ellipse(P(58, 58, 70, 70), fill=Z)
save(im, 'vitc')

# longheat: a mug with a tall flame of steam
im, d = new()
d.rectangle(P(22, 70, 86, 118), fill=W); d.ellipse(P(22, 106, 86, 124), fill=W)
d.ellipse(P(76, 78, 112, 110), fill=W); d.ellipse(P(84, 86, 102, 102), fill=Z); d.rectangle(P(60, 78, 86, 110), fill=W)
fl = [(54, 62), (36, 48), (40, 30), (52, 8), (58, 26), (68, 18), (74, 40), (70, 54), (62, 62)]
d.polygon([(x * K, y * K) for x, y in fl], fill=W); d.polygon(P(54, 58, 48, 46, 56, 32, 62, 46), fill=Z)
save(im, 'longheat')

sh = Image.new('RGBA', (7 * 140, 140), (40, 44, 48, 255))
for i, n in enumerate(('hardy', 'antidote', 'map', 'nosleep', 'relaxed', 'vitc', 'longheat')): sh.alpha_composite(Image.open(os.path.join(OUT, 'fx_' + n + '.png')), (i * 140 + 6, 6))
sh.save('fx_sheet.png')

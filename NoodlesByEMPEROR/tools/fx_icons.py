"""Effect icons for the item panel (white glyphs like the game's own), ../src/fxicons/<key>.png 128 px, embedded in the dll."""
import os, math
from PIL import Image, ImageDraw
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'src', 'fxicons'); K = 4; S = 128 * K; W = (255, 255, 255, 255); Z = (0, 0, 0, 0)
def P(x): return x * S / 100
def new(): im = Image.new('RGBA', (S, S), Z); return im, ImageDraw.Draw(im)
def save(im, k): im.resize((128, 128), Image.LANCZOS).save(os.path.join(OUT, k + '.png'))
def drop(d, cx, cy, r, fill=W):
    d.ellipse((P(cx - r), P(cy - r * .2), P(cx + r), P(cy + r * 1.8)), fill=fill); d.polygon([(P(cx), P(cy - r * 1.7)), (P(cx - r * .92), P(cy + r * .45)), (P(cx + r * .92), P(cy + r * .45))], fill=fill)
def bowl(d, cy=58, hw=40):
    d.pieslice((P(50 - hw), P(cy - hw), P(50 + hw), P(cy + hw)), 0, 180, fill=W); d.rectangle((P(50 - hw * .4), P(cy + hw * .9), P(50 + hw * .4), P(cy + hw * 1.05)), fill=W)

# broth reserve: a bowl with a big drop over it
im, d = new(); bowl(d, 54, 40); d.rectangle((P(6), P(48), P(94), P(54)), fill=W); drop(d, 50, 22, 11); save(im, 'brothreserve')
# whetted appetite: plate between fork and knife
im, d = new()
d.ellipse((P(26), P(26), P(74), P(74)), outline=W, width=int(P(7))); d.ellipse((P(40), P(40), P(60), P(60)), fill=W)
for x in (6, 12, 18): d.rectangle((P(x - 1.6), P(14), P(x + 1.6), P(40)), fill=W)
d.rectangle((P(4.4), P(36), P(19.6), P(42)), fill=W); d.rectangle((P(9.5), P(40), P(14.5), P(88)), fill=W)
d.polygon([(P(84), P(12)), (P(94), P(22)), (P(94), P(52)), (P(84), P(52))], fill=W); d.rectangle((P(84), P(50)), ) if False else None
d.rectangle((P(84), P(50), P(89.5), P(88)), fill=W); save(im, 'appetite')
# empty calories: a hollow flame with nothing inside
def flame(sc, cx=50, cy=54):
    pts = [(0, -46), (14, -26), (20, -34), (30, -8), (32, 14), (24, 34), (8, 44), (-8, 44), (-24, 34), (-32, 14), (-28, -6), (-18, -18), (-14, -4), (-8, -24)]
    return [(P(cx + x * sc), P(cy + y * sc)) for x, y in pts]
im, d = new(); d.polygon(flame(1.0), fill=W); d.polygon(flame(.62, 50, 60), fill=Z); save(im, 'emptycal')
# heartburn: a heart with a flame cut out of it
im, d = new()
d.ellipse((P(8), P(14), P(52), P(58)), fill=W); d.ellipse((P(48), P(14), P(92), P(58)), fill=W); d.polygon([(P(10), P(46)), (P(90), P(46)), (P(50), P(92))], fill=W)
d.polygon(flame(.5, 50, 50), fill=Z); save(im, 'heartburn')
# streaming tears: an eye with three drops under it
im, d = new()
pts = [(P(50 + 44 * math.cos(a)), P(34 - 22 * math.sin(a) * (1 if math.sin(a) > 0 else .8))) for a in [i * math.pi / 30 for i in range(60)]]
d.polygon(pts, fill=W); d.ellipse((P(38), P(22), P(62), P(46)), fill=Z); d.ellipse((P(45), P(29), P(55), P(39)), fill=W)
for x, y in ((26, 64), (50, 72), (74, 64)): drop(d, x, y, 7)
save(im, 'tears')
W2 = Image.new('RGBA', (5 * 140, 140), (60, 60, 60, 255))
for i, k in enumerate(('brothreserve', 'appetite', 'emptycal', 'heartburn', 'tears')): t = Image.open(os.path.join(OUT, k + '.png')); W2.paste(t, (i * 140 + 6, 6), t)
W2.save('/tmp/claude-0/-home-user/53abbfaa-fe81-5225-90fd-f288248012d9/scratchpad/fxi.png')

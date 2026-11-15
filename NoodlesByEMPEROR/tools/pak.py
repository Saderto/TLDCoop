"""Packs every picture and model of the mod into noodles1.pak, noodles2.pak… (each under 23 MB) for src/Assets.cs.
Item textures go in as JPG (no alpha, a third of the PNG's size). Usage: pak.py <out dir>"""
import os, struct, sys, io
from PIL import Image
from catalog import NOODLES
from food_top import food, rgb
from PIL import ImageDraw, ImageFilter
HERE = os.path.dirname(os.path.abspath(__file__)); R = os.path.join(HERE, '..')
out = sys.argv[1]; os.makedirs(out, exist_ok=True)
files = []
def jpg(p, maxw=None):
    im = Image.open(p).convert('RGB')
    if maxw and im.size[0] > maxw: im = im.resize((maxw, int(im.size[1] * maxw / im.size[0])), Image.LANCZOS)
    b = io.BytesIO(); im.save(b, 'JPEG', quality=88, subsampling=1); return b.getvalue()
def pot(s, N=256):
    """What lies in the pot, skillet or can while the noodles cook: the game's food surface shows the whole picture (uv 0..1),
    so it is the bowl's broth all over with the bowl's noodles in the middle."""
    K = 3; f = food(s, N * K); m = Image.new('L', f.size, 0)
    ImageDraw.Draw(m).ellipse((N * K * .05, N * K * .05, N * K * .95, N * K * .95), fill=255)
    m = m.filter(ImageFilter.GaussianBlur(N * K * .012))
    w = int(N * K * .86); f = f.resize((w, w), Image.LANCZOS); m = m.resize((w, w), Image.LANCZOS)
    im = Image.new('RGB', (N * K, N * K), rgb(s['broth'])); o = (N * K - w) // 2; im.paste(f, (o, o), m)
    b = io.BytesIO(); im.resize((N, N), Image.LANCZOS).save(b, 'JPEG', quality=88, subsampling=1); return b.getvalue()
def icon(p):
    return open(p, 'rb').read()
for f in sorted(os.listdir(os.path.join(R, 'meshes'))): files.append(('meshes/' + f, open(os.path.join(R, 'meshes', f), 'rb').read()))
for s in NOODLES:
    sid = s['id']
    files.append((f'textures/{sid}.jpg', jpg(os.path.join(R, 'tex', sid + '.png'))))
    if s['vessel']: files.append((f'textures/{sid}Hot.jpg', jpg(os.path.join(R, 'tex', sid + 'Hot.png'), 768)))
    files.append((f'pots/{sid}.jpg', pot(s)))
    for n in (sid, sid + 'Hot'): files.append((f'icons/{n}.png', icon(os.path.join(R, 'icons', n + '.png'))))
parts, cur, size = [], [], 0
for f in files:
    if cur and size + len(f[1]) > 23_000_000: parts.append(cur); cur, size = [], 0
    cur.append(f); size += len(f[1])
parts.append(cur)
for k, part in enumerate(parts, 1):
    with open(os.path.join(out, 'noodles%d.pak' % k), 'wb') as o:
        o.write(struct.pack('<i', len(part)))
        for n, b in part:
            n = n.encode(); o.write(struct.pack('<i', len(n))); o.write(n); o.write(struct.pack('<i', len(b))); o.write(b)
    print('noodles%d.pak' % k, len(part), 'files', sum(len(b) for _, b in part) // 1000, 'KB')

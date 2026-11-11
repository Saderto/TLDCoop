"""Painted textures: cigar wrapper leaf, pipe briar, cigar foot. u around, v along."""
import os, sys, math, random
from PIL import Image, ImageDraw, ImageFilter, ImageChops


def smooth_noise(w, h, cell, seed, amp=1.0):
    rnd = random.Random(seed)
    small = Image.new('L', (max(2, w // cell), max(2, h // cell)))
    small.putdata([rnd.randint(0, 255) for _ in range(small.size[0] * small.size[1])])
    return small.resize((w, h), Image.BICUBIC)


def colorize(gray, c0, c1):
    r = gray.point(lambda v: int(c0[0] + (c1[0] - c0[0]) * v / 255))
    g = gray.point(lambda v: int(c0[1] + (c1[1] - c0[1]) * v / 255))
    b = gray.point(lambda v: int(c0[2] + (c1[2] - c0[2]) * v / 255))
    return Image.merge('RGB', (r, g, b))


def wrapper(path, seed=1, c0=(62, 34, 16), c1=(132, 80, 42), W=1024, H=2048, turns=2.2):
    """Cigar wrapper: mottled leaf, spiral seam, branching veins. Image x = around, y = along (top = head)."""
    rnd = random.Random(seed)
    base = ImageChops.add(smooth_noise(W, H, 64, seed), smooth_noise(W, H, 12, seed + 1), scale=2)
    base = ImageChops.add(base, smooth_noise(W, H, 3, seed + 2), scale=1.6).filter(ImageFilter.GaussianBlur(1))
    im = colorize(base.point(lambda v: min(255, int(v * 1.0))), c0, c1)
    d = ImageDraw.Draw(im)
    # spiral seam: around goes 'turns' times along the length -> lines x = (y/H*turns*W) mod W
    for off in (0.0,):
        pts = []
        for y in range(0, H + 1, 4):
            x = (y / H * turns * W + off * W) % W
            pts.append((x, y))
        segs, cur = [], [pts[0]]
        for a, b in zip(pts, pts[1:]):
            if abs(b[0] - a[0]) > W / 2:
                segs.append(cur); cur = [b]
            else:
                cur.append(b)
        segs.append(cur)
        for s in segs:
            if len(s) > 1:
                d.line(s, fill=(40, 20, 8), width=3)
                d.line([(x + 4, y) for x, y in s], fill=(150, 98, 56), width=2)
    # veins: main veins perpendicular-ish to the seam, with branches
    for _ in range(26):
        x, y = rnd.uniform(0, W), rnd.uniform(0, H)
        ang = math.atan2(H / turns, -W) + rnd.uniform(-0.4, 0.4)
        L = rnd.uniform(120, 420)
        wv = rnd.uniform(1.5, 3.5)
        col = (rnd.randint(118, 150), rnd.randint(72, 92), rnd.randint(38, 52))
        pts = []
        for k in range(30):
            t = k / 29
            pts.append((x + math.cos(ang) * L * t + math.sin(t * 6) * 6, y + math.sin(ang) * L * t))
        d.line(pts, fill=col, width=int(wv))
        for k in range(3, 28, 5):
            bx, by = pts[k]
            ba = ang + rnd.choice([-1, 1]) * rnd.uniform(0.5, 0.9)
            bl = rnd.uniform(30, 90)
            d.line([(bx, by), (bx + math.cos(ba) * bl, by + math.sin(ba) * bl)], fill=col, width=max(1, int(wv * 0.5)))
    im = im.filter(ImageFilter.GaussianBlur(0.8))
    # tiny dark spots (tobacco "oil" spots)
    d = ImageDraw.Draw(im)
    for _ in range(140):
        x, y, r = rnd.uniform(0, W), rnd.uniform(0, H), rnd.uniform(1, 3.5)
        d.ellipse([x - r, y - r, x + r, y + r], fill=(48, 26, 12))
    im.save(path)


def foot(path, seed=2):
    W = 512
    rnd = random.Random(seed)
    im = Image.new('RGB', (W, W), (70, 40, 18))
    d = ImageDraw.Draw(im)
    for _ in range(4000):
        x, y = rnd.uniform(0, W), rnd.uniform(0, W)
        a = rnd.uniform(0, math.pi)
        l = rnd.uniform(6, 30)
        c = rnd.choice([(52, 28, 10), (96, 58, 26), (124, 80, 38), (84, 48, 20), (140, 96, 50)])
        d.line([(x, y), (x + math.cos(a) * l, y + math.sin(a) * l)], fill=c, width=rnd.randint(2, 5))
    # wrapper/binder rings at the edge
    d.ellipse([4, 4, W - 4, W - 4], outline=(46, 24, 10), width=10)
    d.ellipse([16, 16, W - 16, W - 16], outline=(110, 70, 34), width=6)
    im.filter(ImageFilter.GaussianBlur(0.7)).save(path)


def briar(path, seed=3, W=1024, H=1024):
    """Straight/flame grain running along v, waving with u; bird's-eye dots; warm reddish brown."""
    rnd = random.Random(seed)
    warp = smooth_noise(W, H, 160, seed)
    warp2 = smooth_noise(W, H, 25, seed + 7)
    px = []
    wp, wp2 = warp.load(), warp2.load()
    im = Image.new('RGB', (W, H))
    p = im.load()
    for y in range(H):
        for x in range(W):
            u = x / W
            ph = u * 46 + (wp[x, y] - 128) / 128 * 1.4 + (wp2[x, y] - 128) / 128 * 0.08
            g = 0.5 + 0.5 * math.sin(ph * math.pi * 2)
            g = g ** 3  # thin dark lines
            base = (92, 42, 20)
            dark = (44, 18, 8)
            t = 0.25 + 0.75 * g
            mod = 0.85 + 0.15 * (wp[x, y] / 255)
            p[x, y] = tuple(int((base[i] * (1 - t * 0.45) + dark[i] * t * 0.45) * mod) for i in range(3))
    d = ImageDraw.Draw(im)
    for _ in range(500):
        x, y, r = rnd.uniform(0, W), rnd.uniform(0, H), rnd.uniform(0.8, 2.0)
        d.ellipse([x - r, y - r, x + r, y + r], fill=(58, 20, 8))
    im.filter(ImageFilter.GaussianBlur(0.6)).save(path)


if __name__ == '__main__':
    out = sys.argv[1]
    wrapper(os.path.join(out, 'cigar_wrapper_cohibra.png'), 1, (70, 40, 18), (140, 88, 46))
    wrapper(os.path.join(out, 'cigar_wrapper_montecrusto.png'), 5, (52, 28, 12), (112, 66, 32))
    foot(os.path.join(out, 'cigar_foot.png'))
    briar(os.path.join(out, 'pipe_briar.png'))
    print('ok')

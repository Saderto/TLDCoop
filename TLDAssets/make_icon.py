# EMPEROR's mods icon: own drawing (no game art). Writes icon.ico + icon.png next to the given path.
import sys, os, math
from PIL import Image, ImageDraw, ImageFilter
N = 1024
def bez(p0, p1, p2, p3, n=40):
    out = []
    for i in range(n + 1):
        t = i / n; u = 1 - t
        out.append((u**3*p0[0] + 3*u*u*t*p1[0] + 3*u*t*t*p2[0] + t**3*p3[0],
                    u**3*p0[1] + 3*u*u*t*p1[1] + 3*u*t*t*p2[1] + t**3*p3[1]))
    return out
def make():
    im = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    # background: dusk sky, rounded tile
    bg = Image.new('RGBA', (N, N)); d = ImageDraw.Draw(bg)
    for y in range(N):
        k = y / N
        d.line([(0, y), (N, y)], fill=(int(58 - 30*k), int(74 - 36*k), int(84 - 38*k), 255))
    # low warm glow on the horizon
    glow = Image.new('RGBA', (N, N)); gd = ImageDraw.Draw(glow)
    gd.ellipse((N*0.05, N*0.45, N*0.95, N*0.95), fill=(214, 150, 80, 110))
    glow = glow.filter(ImageFilter.GaussianBlur(N*0.08)); bg.alpha_composite(glow)
    d = ImageDraw.Draw(bg)
    # mountains
    d.polygon([(0, N*0.80), (N*0.22, N*0.58), (N*0.36, N*0.68), (N*0.58, N*0.47), (N*0.80, N*0.66), (N, N*0.56), (N, N), (0, N)], fill=(196, 206, 212, 255))
    d.polygon([(N*0.58, N*0.47), (N*0.50, N*0.555), (N*0.56, N*0.535), (N*0.60, N*0.57), (N*0.65, N*0.525)], fill=(240, 244, 246, 255))
    d.polygon([(0, N*0.88), (N*0.30, N*0.74), (N*0.55, N*0.83), (N*0.78, N*0.72), (N, N*0.82), (N, N), (0, N)], fill=(232, 237, 240, 255))
    # firs
    for x, h in [(0.12, 0.17), (0.19, 0.12), (0.84, 0.15), (0.91, 0.20)]:
        cx = N*x; base = N*0.93; hh = N*h
        d.polygon([(cx, base - hh), (cx - hh*0.27, base), (cx + hh*0.27, base)], fill=(30, 40, 42, 255))
    # seagull: two arched wings meeting at a small body
    cx, cy, s = N*0.5, N*0.31, N*0.40
    L = bez((cx, cy + s*0.10), (cx - s*0.30, cy - s*0.45), (cx - s*0.75, cy - s*0.30), (cx - s*1.05, cy + s*0.05))
    Lb = bez((cx - s*1.05, cy + s*0.05), (cx - s*0.70, cy - s*0.02), (cx - s*0.35, cy - s*0.06), (cx, cy + s*0.26))
    R = [(2*cx - x, y) for x, y in L]; Rb = [(2*cx - x, y) for x, y in Lb]
    poly = L + Lb[1:] + list(reversed(Rb))[1:] + list(reversed(R))[1:]
    sh = Image.new('RGBA', (N, N)); ImageDraw.Draw(sh).polygon([(x + N*0.012, y + N*0.016) for x, y in poly], fill=(0, 0, 0, 120))
    bg.alpha_composite(sh.filter(ImageFilter.GaussianBlur(N*0.01)))
    ImageDraw.Draw(bg).polygon(poly, fill=(250, 250, 248, 255))
    # rounded tile mask + thin frame
    mask = Image.new('L', (N, N)); ImageDraw.Draw(mask).rounded_rectangle((N*0.03, N*0.03, N*0.97, N*0.97), N*0.16, fill=255)
    im.paste(bg, (0, 0), mask)
    ImageDraw.Draw(im).rounded_rectangle((N*0.03, N*0.03, N*0.97, N*0.97), N*0.16, outline=(20, 26, 28, 255), width=int(N*0.018))
    return im
if __name__ == '__main__':
    out = sys.argv[1]
    im = make()
    im.resize((256, 256), Image.LANCZOS).save(os.path.join(out, 'icon.png'))
    im.resize((256, 256), Image.LANCZOS).save(os.path.join(out, 'icon.ico'), sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])

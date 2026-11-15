"""0.6 review sheets: every brand with all its flavours, new ones framed yellow, seasonal ones marked. python3 sheet6.py <out prefix>"""
import sys, os
from PIL import Image, ImageDraw
import catalog
from catalog import BRANDS, COUNTRY, RARE
import art as A
pre = sys.argv[1]
old = set(l.strip() for l in open('../old_ids.txt'))
IC = 128; PER = 15; W = PER * IC + 48
fb = A.font('inter_black', 24); fs = A.font('ptsans', 14)
def block(b):
    n = len(b['var']); rows = (n + PER - 1) // PER; h = 44 + rows * (IC + 34) + 8
    im = Image.new('RGB', (W, h), '#23262b'); d = ImageDraw.Draw(im)
    d.rounded_rectangle((8, 0, W - 8, h - 6), 12, fill='#30343a')
    nn = sum(1 for v in b['var'] if b['id'] + v[0] not in old)
    d.text((24, 22), f"{b['ru']}  ·  {COUNTRY[b['country']][0]}  ·  видов {n}, новых {nn}", font=fb, fill='#f1f3f5', anchor='lm')
    for k, v in enumerate(b['var']):
        x = 24 + (k % PER) * IC; y = 44 + (k // PER) * (IC + 34); sid = b['id'] + v[0]
        if sid not in old: d.rounded_rectangle((x + 1, y + 1, x + IC - 5, y + IC + 28), 8, outline='#ffd43b', width=2)
        ic = Image.open(f"../icons/{sid}.png").convert('RGBA').resize((IC - 12, IC - 12), Image.LANCZOS); im.paste(ic, (x + 4, y + 4), ic)
        t = v[1]
        while d.textlength(t, font=fs) > IC - 10 and len(t) > 4: t = t[:-2]
        d.text((x + IC / 2 - 2, y + IC + 12), t, font=fs, fill='#ffa8a8' if RARE.get(sid) else '#ced4da', anchor='mm')
    return im
blocks = [block(b) for b in BRANDS]; page, hh, k = [], 0, 1
def flush():
    global page, hh, k
    if not page: return
    im = Image.new('RGB', (W, hh + 70), '#23262b'); d = ImageDraw.Draw(im)
    d.text((24, 34), f"Снеки 0.6, лист {k}. Жёлтая рамка: новый вкус. Красная подпись: сезонная редкая пачка.", font=A.font('inter_black', 30), fill='#ffd43b', anchor='lm'); y = 66
    for b in page: im.paste(b, (0, y)); y += b.size[1]
    im.save(f'{pre}{k}.jpg', quality=86); print(f'{pre}{k}.jpg', im.size); page, hh = [], 0; k += 1
for b in blocks:
    if hh + b.size[1] > 5200: flush()
    page.append(b); hh += b.size[1]
flush()

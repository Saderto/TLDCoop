"""Review sheets by country. python3 sheet.py <out.png> <title> new|all <country codes…>"""
import sys
from PIL import Image, ImageDraw
import catalog
from catalog import BRANDS, COUNTRY
import art as A
out, title, which, codes = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4:]
new = {g['id'] for g in catalog.GEN2}
IC = 132; W = 2040
fb = A.font('inter_black', 22); fs = A.font('ptsans', 15); fh = A.font('inter_black', 34)
im = Image.new('RGB', (W, 9000), '#23262b'); d = ImageDraw.Draw(im); y = 24
d.text((24, y + 20), title, font=A.font('inter_black', 40), fill='#f1f3f5', anchor='lm'); y += 64
for c in codes:
    bs = [b for b in BRANDS if b['country'] == c and (which == 'all' or b['id'] in new)]
    if not bs: continue
    name = {'WW': 'Мировые марки, есть везде'}.get(c, COUNTRY[c][0])
    d.text((24, y + 24), f"{name}: марок {len(bs)}, видов {sum(len(b['var']) for b in bs)}", font=fh, fill='#ffd43b', anchor='lm'); y += 56
    x = 20
    for b in bs:
        n = len(b['var']); bw = n * IC + 20
        if x + bw > W - 10: x = 20; y += IC + 78
        d.rounded_rectangle((x, y, x + bw - 8, y + IC + 70), 12, fill='#30343a')
        t = b['ru']
        while d.textlength(t, font=fb) > bw - 28 and len(t) > 4: t = t[:-2]
        d.text((x + 12, y + 18), t, font=fb, fill='#f1f3f5', anchor='lm')
        for k, v in enumerate(b['var']):
            ic = Image.open(f"../icons/{b['id'] + v[0]}.png").convert('RGBA').resize((IC - 6, IC - 6), Image.LANCZOS)
            im.paste(ic, (x + 10 + k * IC, y + 32), ic)
            t = v[1]
            while d.textlength(t, font=fs) > IC - 6 and len(t) > 4: t = t[:-2]
            d.text((x + 10 + k * IC + IC / 2 - 3, y + IC + 50), t, font=fs, fill='#ced4da', anchor='mm')
        x += bw
    y += IC + 92
im.crop((0, 0, W, y)).save(out); print(out, y)

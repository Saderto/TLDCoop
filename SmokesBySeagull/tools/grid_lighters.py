"""labelled grid of the lighter previews: python3 grid_lighters.py <dir> <out.png>"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw, ImageFont
import ast
src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'lighter_designs.py')).read()
import re
rows = re.findall(r"\('(Lighter\w+)', '([^']+)', '([^']+)', '(\w+)', .*, (\d)\),", src)
D, OUT = sys.argv[1], sys.argv[2]
cols, tw, th = 6, 300, 400
n = len(rows)
im = Image.new('RGB', (cols * tw, ((n + cols - 1) // cols) * th), (236, 236, 236))
f = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 17)
fb = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', 15)
d = ImageDraw.Draw(im)
for i, (key, ru, en, kind, rar) in enumerate(rows):
    x, y = (i % cols) * tw, (i // cols) * th
    a = Image.open(os.path.join(D, key + '.png')).convert('RGBA').resize((300, 350))
    im.paste(a, (x, y - 10), a)
    d.text((x + 8, y + 8), '%d' % (i + 1), font=fb, fill=(90, 90, 90))
    d.text((x + tw - 60, y + 8), '★' * int(rar), font=fb, fill=(200, 140, 20))
    d.text((x + tw / 2, y + 352), ru, font=f, fill=(30, 30, 30), anchor='mm')
    d.text((x + tw / 2, y + 376), kind, font=fb, fill=(120, 120, 128), anchor='mm')
im.save(OUT)
print(n)

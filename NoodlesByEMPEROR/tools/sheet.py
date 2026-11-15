"""Preview sheets of every kind: ../previews/noodles_v3_<part>.png"""
import os, textwrap
from PIL import Image, ImageDraw, ImageFont
from catalog import NOODLES
R = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
Fb = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', 17)
F = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 14)
T = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', 30)
def sheet(name, title, items, cols=6):
    cw, ch, ic = 330, 250, 150
    rows = (len(items) + cols - 1) // cols
    W = Image.new('RGB', (cw * cols, ch * rows + 64), (34, 36, 40)); d = ImageDraw.Draw(W)
    d.text((20, 16), f'{title}: {len(items)}', font=T, fill=(240, 240, 240))
    for i, n in enumerate(items):
        x = (i % cols) * cw; y = 64 + (i // cols) * ch
        d.rectangle([x + 5, y + 5, x + cw - 5, y + ch - 5], fill=(52, 55, 61))
        for k, suf in enumerate(['', 'Hot']):
            im = Image.open(os.path.join(R, 'icons', n['id'] + suf + '.png')).convert('RGBA').resize((ic, ic), Image.LANCZOS)
            W.paste(im, (x + 12 + k * (ic + 6), y + 10), im)
        nm = n['ru'].replace('Лапша ', '')
        for j, line in enumerate(textwrap.wrap(nm, 30)[:2]): d.text((x + 14, y + 166 + j * 21), line, font=Fb, fill=(255, 255, 255))
        d.text((x + 14, y + 212), textwrap.shorten(n['ruf'], 40, placeholder='…'), font=F, fill=(185, 185, 185))
    W.save(os.path.join(R, 'previews', name + '.png')); print(name, W.size)
new = set(open(os.path.join(R, 'tools', 'batch2_ids.txt')).read().split())
bul = [n for n in NOODLES if n['brand'] == 'Buldak']
ko = [n for n in NOODLES if n['lang'] == 'ko' and n['brand'] != 'Buldak' and n['id'] not in new]
ja = [n for n in NOODLES if n['lang'] == 'ja' and n['id'] not in new]
rest = [n for n in NOODLES if n['lang'] not in ('ko', 'ja') and n['id'] not in new]
nw = [n for n in NOODLES if n['id'] in new]
import sys
if 'old' in sys.argv:
    sheet('noodles_v3_1_buldak', 'Линейка «Больдак»', bul)
    sheet('noodles_v3_2_korea', 'Корея', ko)
    sheet('noodles_v3_3_japan', 'Япония', ja)
    sheet('noodles_v3_4_world', 'Россия, Юго-Восточная Азия, Китай и другие', rest)
sheet('noodles_v4_new_asia', 'Новое в 0.3.0: Корея, Япония, Китай, Тайвань', [n for n in nw if n['lang'] in ('ko', 'ja', 'zh')], cols=8)
sheet('noodles_v4_new_world', 'Новое в 0.3.0: остальной мир', [n for n in nw if n['lang'] not in ('ko', 'ja', 'zh')], cols=8)

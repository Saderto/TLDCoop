"""Catalogue page of the cigarillos: python3 cgl_page.py <iconsdir> <singlesdir> <out.html>"""
import sys, os, io, base64, json, html
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image
import cigarillos as C, cigarillo_memes as mm
ICONS, CIGS, OUT = sys.argv[1:4]

def uri(path, size, crop=True):
    im = Image.open(path).convert('RGBA')
    if crop: im = im.crop(im.getbbox())
    im.thumbnail(size, Image.LANCZOS)
    b = io.BytesIO(); im.save(b, 'WEBP', quality=82)
    return 'data:image/webp;base64,' + base64.b64encode(b.getvalue()).decode()

def tm(sec): return '%d мин %02d с' % (sec // 60, sec % 60)
STR = ['лёгкая', 'средняя', 'крепкая']
FMT = {'mini': 'мини', 'slim': 'тонкая мини', 'tip': 'с мундштуком', 'classic': 'классическая', 'square': 'квадратная', 'stumpen': 'штумпен', 'rustic': 'грубая скрутка', 'toscano': 'тосканская'}
PK = {'tin': 'жестянка', 'pack10': 'пачка', 'box5': 'коробочка', 'pouch': 'мягкий пакет', 'bundle': 'связка'}
e = html.escape
brands = []
for b in C.BRANDS:
    cards = []
    for v in b['vs']:
        iid = C.item_id(b, v); k = b['key'] + '_' + v['key']
        fmt = C.fmt_of(b, v); st = C.strength_of(b, v); L, R, T, sec = C.FORMATS[fmt]
        (nr, dr), _ = C.texts(b, v)
        plus = C.BONUS[v['bonus']][0].replace('30 мин', '45 мин'); minus = C.SIDE[v['side']][0].replace('30 мин', '45 мин').replace('Минус: ', '')
        f, h, hp, cnt = C.STATS[st]
        if v['bonus'] == 'coffee': f += 2
        if v['bonus'] == 'sweet': h += 10
        chips = [STR[st], FMT[fmt] + (', фильтр' if C.filt_of(b, v) else ''), '%d × %g мм' % (round(L * 1000), round(R * 2000, 1)), tm(sec)]
        cards.append('''<article class="card" data-s="%d" data-p="%s" data-q="%s">
<div class="pics"><img class="pk" alt="Упаковка %s" src="%s"><img class="cg" alt="Сигарилла %s" src="%s"></div>
<h3>%s%s</h3><p class="meme">%s</p>
<ul class="chips">%s</ul>
<dl><dt>Усталость</dt><dd>−%s%%</dd><dt>Голод медленнее</dt><dd>%d мин</dd><dt>Здоровье</dt><dd>−%s%%</dd></dl>
%s%s</article>''' % (st, C.BASE.get(b['pack'], b['pack']), e((nr + ' ' + b['name']).lower()), e(nr), uri(os.path.join(ICONS, iid + '.png'), (220, 220)), e(nr), uri(os.path.join(CIGS, iid + '.png'), (300, 120)),
            e(nr.replace('Сигариллы ', '')), ' <span class="rare">редкая</span>' if v['rare'] else '', e(mm.MEME[k][0]), ''.join('<li>%s</li>' % e(c) for c in chips),
            C.num_ru(f), h, C.num_ru(hp), '<p class="plus">%s</p>' % e(plus) if plus else '', '<p class="minus">%s</p>' % e(minus) if minus else ''))
    pk = C.BASE.get(b['pack'], b['pack'])
    brands.append('<section class="brand" id="b-%s"><header><h2>%s</h2><p>%s %d штук, %s. Видов: %d.</p></header><div class="grid">%s</div></section>' % (
        b['key'].lower(), e(b['name']), e(mm.BLURB[b['key']][0]), b['units'], PK[pk], len(b['vs']), '\n'.join(cards)))
nav = ''.join('<a href="#b-%s">%s</a>' % (b['key'].lower(), e(b['name'])) for b in C.BRANDS)
page = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'cgl_page_tpl.html'), encoding='utf-8').read()
page = page.replace('{{NAV}}', nav).replace('{{BRANDS}}', '\n'.join(brands)).replace('{{NB}}', str(len(C.BRANDS))).replace('{{NV}}', str(sum(len(b['vs']) for b in C.BRANDS)))
open(OUT, 'w', encoding='utf-8').write(page)
print(len(page) // 1024, 'KB')

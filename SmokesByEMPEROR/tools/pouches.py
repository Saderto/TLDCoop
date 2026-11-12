"""Pipe tobacco pouches (SmokesByEMPEROR): data, label atlases (front | back, like labels.pouch_label), C# + texts.
python3 pouches.py <texdir> <srcdir> <distdir>   (icons: render_icons.py picks the jobs from here)"""
import os, sys, json, math, random
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from packs import F, SCRIPT, SERIF9, SERIF7, CIN7, CIN9, OSW7, OSW4, BEBAS, SLAB, ABRIL, text_c, fit_font, grunge
from labels import ship_emblem


def P(key, name, top, bottom, sub, bg, ink, acc, tob, fat, hp, hunger, bonus='', hours=0.0, emblem='ship', ru='', en=''):
    return dict(key=key, name=name, top=top, bottom=bottom, sub=sub, bg=bg, ink=ink, acc=acc, tob=tob, fat=fat, hp=hp, hunger=hunger,
                bonus=bonus, hours=hours, emblem=emblem, ru=ru, en=en)


POUCHES = [
    P('CaptainBlockCherry', 'Captain Block Cherry', 'CAPTAIN', 'BLOCK', 'CHERRY PIPE TOBACCO', (120, 20, 34), (236, 228, 208), (226, 186, 110),
      (96, 34, 26), 7, 1, 45, ru='Вишнёвый. Пахнет летом, которого здесь не будет. Голод притуплён дольше.',
      en='Cherry. Smells of a summer that will not come here. Dulls hunger for longer.'),
    P('CaptainBlockRoyal', 'Captain Block Royal', 'CAPTAIN', 'BLOCK', 'ROYAL PIPE TOBACCO', (24, 22, 20), (226, 186, 110), (236, 228, 208),
      (78, 44, 18), 10, 1.5, 35, ru='Крепкий медовый. Для капитанов, у которых корабль замёрз во льду.',
      en='Strong, with honey. For captains whose ship froze into the ice.'),
    P('Erinmoor', 'Erinmoor', 'ERIN', 'MOOR', 'FRUITY FLAKE', (232, 196, 70), (34, 52, 110), (34, 52, 110), (150, 96, 40), 7, 1, 35, 'calm', 1,
      'flake', ru='Фруктовый флейк. Курится спокойно, руки перестают дрожать. Почти.',
      en='A fruity flake. Smokes calmly, your hands stop shaking. Almost.'),
    P('Amfora', 'Amfora Full Aroma', 'AMFORA', 'FULL AROMA', 'CARAMEL CAVENDISH', (110, 40, 26), (240, 220, 180), (214, 160, 70), (110, 62, 26),
      6, 1, 40, emblem='vase', ru='Карамельный кавендиш. Сладкий дым, после которого консервы кажутся не такими грустными.',
      en='Caramel cavendish. Sweet smoke that makes tinned food look less sad.'),
    P('MacBarren', 'Mac Barren Latakia', 'MAC BARREN', 'LATAKIA', 'SMOKED ORIENTAL BLEND', (34, 70, 46), (236, 228, 208), (180, 40, 40),
      (40, 26, 16), 7, 1, 35, 'warm', 2, 'tartan', ru='Копчёная латакия. Пахнет костром, даже когда костра нет. Греет два часа.',
      en='Smoked latakia. Smells of a campfire even when there is none. Warms for two hours.'),
    P('Petersun', 'Petersun Irish Flake', 'PETERSUN', 'IRISH FLAKE', 'VIRGINIA & PERIQUE', (28, 84, 52), (226, 186, 110), (236, 228, 208),
      (140, 70, 30), 12, 2, 35, emblem='clover', ru='Крепкий ирландский флейк. Будит лучше кофе и вредит сильнее.',
      en='A strong Irish flake. Wakes you better than coffee and hurts you more.'),
    P('Borkum', 'Borkum Reef Whisky', 'BORKUM', 'REEF', 'WHISKY FLAVOURED', (30, 28, 26), (220, 150, 60), (236, 228, 208), (96, 52, 20),
      8, 1, 35, 'warm', 1, 'anchor', ru='С виски. Виски в нём больше, чем осталось у вас в рюкзаке.',
      en='With whisky. More whisky in it than is left in your backpack.'),
    P('Clann', 'Clann Aromatic', 'CLANN', 'AROMATIC', 'MILD CAVENDISH', (236, 236, 228), (28, 92, 60), (28, 92, 60), (150, 100, 52),
      5, .5, 35, emblem='clover', ru='Мягкий кавендиш. Для тех, кто курит трубку ради уюта, а не ради выживания.',
      en='A mild cavendish. For those who smoke a pipe for comfort, not survival.'),
    P('DunchillEM', 'Dunchill Early Morning', 'DUNCHILL', 'EARLY MORNING', 'VIRGINIA & ORIENTAL', (196, 214, 226), (24, 36, 70), (24, 36, 70),
      (160, 110, 56), 7, 1, 35, 'fresh', 1, 'sun', ru='Утренний. Свежий вдох, как будто за окном не минус сорок.',
      en='Morning blend. A fresh breath, as if it were not minus forty outside.'),
    # ---- batch 2 (11-09): 20 more
    P('SamuelGowith', 'Samuel Gowith Full Virginia', 'SAMUEL', 'GOWITH', 'FULL VIRGINIA FLAKE', (40, 30, 24), (226, 186, 110), (196, 70, 40),
      (60, 34, 18), 11, 1.5, 40, 'warm', 1, 'castle', ru='Тёмный флейк из Кендала. Пахнет как старый английский паб, в котором топят печь.',
      en='Dark flake from Kendal. Smells like an old English pub with the stove going.'),
    P('Orlick', 'Orlick Golden Sliced', 'ORLICK', 'GOLDEN SLICED', 'VIRGINIA & PERIQUE', (214, 170, 60), (40, 30, 20), (120, 30, 30),
      (170, 110, 50), 8, 1, 35, emblem='flake', ru='Золотые ломтики. Золото здесь только в табаке и в закатах.',
      en='Golden slices. The only gold here is in tobacco and sunsets.'),
    P('CornellDeal', 'Cornell & Deal Pirate Kake', 'CORNELL', '& DEAL', 'PIRATE KAKE', (20, 20, 22), (236, 228, 208), (200, 40, 40),
      (44, 28, 16), 12, 2, 35, emblem='skull', ru='Пиратский кейк. Пиратом здесь не станешь, но выглядеть суровым поможет.',
      en='Pirate kake. You will not turn pirate here, but you will look tough.'),
    P('Escudoo', 'Escudoo Navy De Luxe', 'ESCUDOO', 'NAVY DE LUXE', 'PERIQUE CURLY CUT', (30, 50, 100), (236, 228, 208), (214, 176, 92),
      (120, 70, 36), 9, 1.5, 35, 'calm', 1, 'coin', ru='Табачные монетки. Курится ровно, руки становятся твёрже.',
      en='Tobacco coins. Burns evenly, your hands get steadier.'),
    P('PrinceAlbertt', 'Prince Albertt', 'PRINCE', 'ALBERTT', 'CRIMP CUT BURLEY', (170, 30, 36), (236, 228, 208), (236, 228, 208),
      (150, 104, 56), 6, 1, 40, emblem='crown', ru='Принц в жестяной банке. У вас в банке разве что тушёнка.',
      en='A prince in a tin. All you keep in tins is stew.'),
    P('HalfHalv', 'Half & Halv', 'HALF', '& HALV', 'BURLEY & BRIGHT', (230, 220, 200), (30, 60, 120), (180, 40, 40),
      (160, 116, 64), 6, 1, 40, emblem='halves', ru='Наполовину берли, наполовину вирджиния. Как ваш день: наполовину выживание, наполовину ходьба.',
      en='Half burley, half Virginia. Like your day: half survival, half walking.'),
    P('SirWalterRally', 'Sir Walter Rally', 'SIR WALTER', 'RALLY', 'MILD AROMATIC', (30, 70, 50), (226, 186, 110), (226, 186, 110),
      (150, 100, 52), 5, .5, 40, emblem='portrait', ru='Сэр Уолтер привёз табак в Англию. До Великого Медвежьего не добрался.',
      en='Sir Walter brought tobacco to England. He never made it to Great Bear.'),
    P('Vellvet', 'Vellvet', 'VELLVET', 'SMOOTH CUT', 'PIPE TOBACCO', (150, 26, 40), (236, 228, 208), (226, 186, 110),
      (140, 90, 46), 5, .5, 40, 'calm', 1, 'ribbon', ru='Бархатный. Курится мягко, как будто метель за стеной не про вас.',
      en='Velvet. Smokes so soft the blizzard outside seems to be about someone else.'),
    P('LaneUnlimited', 'Lane Unlimited 1-Q', 'LANE', 'UNLIMITED', 'BLACK CAVENDISH', (24, 24, 26), (226, 186, 110), (150, 110, 180),
      (40, 26, 18), 6, 1, 45, emblem='star', ru='Чёрный кавендиш с ванилью. Неограниченный, в отличие от ваших спичек.',
      en='Black cavendish with vanilla. Unlimited, unlike your matches.'),
    P('Rattreys', "Rattrey's Old Gowrie", "RATTREY'S", 'OLD GOWRIE', 'VIRGINIA & PERIQUE', (196, 40, 40), (236, 228, 208), (30, 30, 30),
      (130, 76, 36), 9, 1.5, 35, 'warm', 1, 'tartan', ru='Шотландская банка с клетчатым бортиком. Шотландцы знают про холод всё.',
      en='A Scottish tin with a tartan rim. The Scots know everything about cold.'),
    P('Stanwel', 'Stanwel Melange', 'STANWEL', 'MELANGE', 'DANISH MIXTURE', (210, 196, 160), (60, 40, 30), (140, 40, 30),
      (140, 92, 48), 6, 1, 40, emblem='pipe', ru='Датская смесь. Датчане умеют устраивать уют даже в такую погоду.',
      en='A Danish mixture. The Danes can make cosy out of weather like this.'),
    P('WOLarssen', 'W.O. Larssen Classic', 'W.O.', 'LARSSEN', 'CLASSIC MIXTURE', (20, 40, 80), (214, 176, 92), (214, 176, 92),
      (150, 96, 46), 6, 1, 40, 'fresh', 1, 'anchor', ru='Классика из Копенгагена. Свежий вдох, будто с моря, только море замёрзло.',
      en='A Copenhagen classic. A fresh breath as if off the sea, only the sea is frozen.'),
    P('KentuckyBurd', 'Kentucky Burd', 'KENTUCKY', 'BURD', 'DARK FIRED BURLEY', (90, 50, 26), (236, 210, 150), (236, 210, 150),
      (70, 40, 20), 11, 2, 35, 'warm', 2, 'bird', ru='Огнём высушенный берли. Пахнет дымом сильнее, чем ваша печка.',
      en='Fire-cured burley. Smells smokier than your stove.'),
    P('Troust', 'Troust Aromatic Cavendish', 'TROUST', 'AROMATIC', 'CAVENDISH', (100, 140, 60), (240, 236, 220), (60, 40, 20),
      (130, 80, 40), 5, .5, 45, emblem='leafsprig', ru='Голландский ароматный. Сладкий дым, голод притуплён дольше.',
      en='A Dutch aromatic. Sweet smoke, dulls hunger for longer.'),
    P('HolgerDansk', 'Holger Dansk Black & Bourbon', 'HOLGER', 'DANSK', 'BLACK & BOURBON', (30, 26, 24), (210, 150, 70), (236, 228, 208),
      (60, 36, 20), 7, 1, 40, 'warm', 1, 'shield', ru='С бурбоном. Бурбона в нём меньше, чем хочется, но греет.',
      en='With bourbon. Less bourbon than you would like, but it warms.'),
    P('Mixture97', 'Mixture 97', 'MIXTURE', '97', 'ROYAL MIXTURE', (120, 20, 30), (236, 228, 208), (214, 176, 92),
      (150, 96, 50), 6, 1, 40, emblem='crown', ru='Королевская смесь. На два номера лучше той, что с семьдесят девятым.',
      en='A royal mixture. Two numbers better than the one with seventy-nine.'),
    P('HearthGnome', 'Hearth & Gnome', 'HEARTH', '& GNOME', 'LATAKIA BLEND', (70, 40, 26), (236, 210, 150), (200, 60, 40),
      (50, 30, 18), 8, 1.5, 35, 'warm', 2, 'gnome', ru='Латакия с гномом на банке. Гном выглядит так, будто знает, где припасы.',
      en='Latakia with a gnome on the tin. The gnome looks like he knows where the supplies are.'),
    P('GLPeas', 'G.L. Peas Westminster', 'G.L.', 'PEAS', 'WESTMINSTER', (40, 60, 40), (226, 186, 110), (226, 186, 110),
      (90, 54, 26), 8, 1, 35, 'calm', 1, 'tower', ru='Вестминстерская смесь. Часы на башне показывают, что вы опять не спали.',
      en='A Westminster blend. The clock tower says you did not sleep again.'),
    P('McClellan', 'McClellan Virginia Woods', 'McCLELLAN', 'VIRGINIA WOODS', 'MATURED VIRGINIA', (196, 180, 140), (40, 60, 30), (40, 60, 30),
      (170, 114, 54), 9, 1.5, 35, 'fresh', 1, 'leafsprig', ru='Выдержанная вирджиния. Лес на банке такой же, как за дверью, только зелёный.',
      en='Matured Virginia. The woods on the tin are like those outside, only green.'),
    P('Plantta', 'Plantta Danish Black Vanilla', 'PLANTTA', 'DANISH BLACK', 'VANILLA CAVENDISH', (240, 236, 226), (20, 20, 20), (190, 150, 60),
      (50, 32, 20), 5, .5, 45, emblem='flake', ru='Чёрный кавендиш с ванилью. Пахнет как кондитерская, которой здесь нет.',
      en='Black cavendish with vanilla. Smells like a pastry shop that is not here.'),
]

# the new batch (from Samuel Gowith on): more kinds of bonus, and the strong ones have a downside
_NEW = [x['key'] for x in POUCHES][[x['key'] for x in POUCHES].index('SamuelGowith'):]
_EXTRA = {'Orlick': 'hands', 'CornellDeal': 'hands', 'Mixture97': 'hands', 'Troust': 'hands', 'PrinceAlbertt': 'steady', 'HalfHalv': 'steady',
          'LaneUnlimited': 'steady', 'Stanwel': 'steady', 'SirWalterRally': 'calm', 'Plantta': 'calm'}
for _p in POUCHES:
    _p['side'] = ''
    if _p['key'] not in _NEW: continue
    if not _p['bonus'] and _p['key'] in _EXTRA: _p['bonus'], _p['hours'] = _EXTRA[_p['key']], 1.0
    _p['side'] = 'dizzy' if _p['fat'] >= 11 else 'dry' if _p['fat'] >= 8 else ''

BON = {'warm': ('«Тёплый дым»: ощущаемая температура +1°C на %s.', '"Warm smoke": feels-like temperature +1°C for %s.'),
       'calm': ('«Спокойствие»: прицел качается на 25%% меньше %s.', '"Calm": aim sway 25%% less for %s.'),
       'fresh': ('«Свежий вдох»: %s.', '"Fresh breath": %s.'),
       'hands': ('«Твёрдые пальцы»: шанс развести огонь +10%% на %s.', '"Steady fingers": fire-starting chance +10%% for %s.'),
       'steady': ('«Ровное дыхание»: на бегу выносливость тратится на 15%% медленнее %s.', '"Steady breath": sprinting drains stamina 15%% slower for %s.')}
SIDE = {'dry': ('Минус: «Сухость во рту» — жажда растёт на 15%% быстрее %s.', 'Downside: "Dry mouth": thirst builds 15%% faster for %s.'),
        'dizzy': ('Минус: «Головокружение» — прицел качается на 30%% сильнее %s.', 'Downside: "Dizzy": aim sway 30%% more for %s.')}


def emblem(im, d, kind, cx, cy, r, c1, c2, bg):
    if kind == 'ship':
        ship_emblem(im, cx, cy, r, c1 + (255,), c2 + (255,), bg + (255,))
    elif kind == 'flake':
        for i in range(5):
            y = cy - r * .6 + i * r * .3
            d.rounded_rectangle([cx - r, y, cx + r, y + r * .22], radius=r * .1, fill=c1)
    elif kind == 'vase':
        d.ellipse([cx - r * .6, cy - r * .3, cx + r * .6, cy + r * .9], fill=c1)
        d.rectangle([cx - r * .25, cy - r * .9, cx + r * .25, cy - r * .2], fill=c1)
        d.arc([cx - r * .8, cy - r * .8, cx - r * .1, cy + r * .1], 90, 270, fill=c1, width=int(r * .1))
        d.arc([cx + r * .1, cy - r * .8, cx + r * .8, cy + r * .1], 270, 90, fill=c1, width=int(r * .1))
    elif kind == 'tartan':
        for i in range(-3, 4):
            d.rectangle([cx + i * r * .3 - r * .05, cy - r, cx + i * r * .3 + r * .05, cy + r], fill=c2)
            d.rectangle([cx - r, cy + i * r * .3 - r * .05, cx + r, cy + i * r * .3 + r * .05], fill=c2)
        d.rectangle([cx - r, cy - r, cx + r, cy + r], outline=c1, width=int(r * .08))
    elif kind == 'clover':
        for a in (0, 120, 240):
            x, y = cx + math.cos(math.radians(a - 90)) * r * .45, cy + math.sin(math.radians(a - 90)) * r * .45
            d.ellipse([x - r * .45, y - r * .45, x + r * .45, y + r * .45], fill=c1)
        d.line([(cx, cy), (cx + r * .3, cy + r)], fill=c1, width=int(r * .12))
    elif kind == 'anchor':
        w = int(r * .12)
        d.line([(cx, cy - r * .8), (cx, cy + r * .8)], fill=c1, width=w)
        d.line([(cx - r * .45, cy - r * .45), (cx + r * .45, cy - r * .45)], fill=c1, width=w)
        d.ellipse([cx - r * .18, cy - r * 1.05, cx + r * .18, cy - r * .7], outline=c1, width=w)
        d.arc([cx - r * .8, cy - r * .1, cx + r * .8, cy + r * .95], 0, 180, fill=c1, width=w)
    elif kind == 'sun':
        for k in range(12):
            a = math.radians(k * 30)
            d.line([(cx + math.cos(a) * r * .55, cy + math.sin(a) * r * .55), (cx + math.cos(a) * r, cy + math.sin(a) * r)], fill=c1, width=int(r * .08))
        d.ellipse([cx - r * .45, cy - r * .45, cx + r * .45, cy + r * .45], fill=c1)

    elif kind == 'castle':
        d.rectangle([cx - r * .7, cy - r * .2, cx + r * .7, cy + r * .8], fill=c1)
        for k in range(-3, 4, 2):
            d.rectangle([cx + k * r * .2 - r * .1, cy - r * .45, cx + k * r * .2 + r * .1, cy - r * .2], fill=c1)
        d.rectangle([cx - r * .2, cy - r * .9, cx + r * .2, cy - r * .2], fill=c1)
        d.chord([cx - r * .2, cy + r * .3, cx + r * .2, cy + r * .9], 180, 360, fill=bg)
        d.rectangle([cx - r * .2, cy + r * .6, cx + r * .2, cy + r * .8], fill=bg)
    elif kind == 'skull':
        d.ellipse([cx - r * .6, cy - r * .8, cx + r * .6, cy + r * .4], fill=c1)
        d.rectangle([cx - r * .35, cy + r * .2, cx + r * .35, cy + r * .6], fill=c1)
        for sx in (-1, 1):
            d.ellipse([cx + sx * r * .28 - r * .17, cy - r * .35, cx + sx * r * .28 + r * .17, cy - r * .02], fill=bg)
            d.line([(cx + sx * r * .9, cy + r * .5), (cx - sx * r * .9, cy + r * 1.0)], fill=c1, width=int(r * .14))
    elif kind == 'coin':
        for k, (dx, dy) in enumerate(((-.35, .15), (.3, -.1), (0, -.45))):
            x, y = cx + dx * r, cy + dy * r
            d.ellipse([x - r * .45, y - r * .45, x + r * .45, y + r * .45], fill=c1, outline=c2, width=int(r * .05))
            d.ellipse([x - r * .12, y - r * .12, x + r * .12, y + r * .12], fill=bg)
    elif kind == 'crown':
        d.polygon([(cx - r, cy + r * .5), (cx - r, cy - r * .4), (cx - r * .5, cy + r * .05), (cx, cy - r * .7), (cx + r * .5, cy + r * .05),
                   (cx + r, cy - r * .4), (cx + r, cy + r * .5)], fill=c1)
        for x in (-r, 0, r):
            d.ellipse([cx + x - r * .12, cy - r * .85 + (0 if x == 0 else r * .3), cx + x + r * .12, cy - r * .61 + (0 if x == 0 else r * .3)], fill=c1)
    elif kind == 'halves':
        d.pieslice([cx - r, cy - r, cx + r, cy + r], 90, 270, fill=c1)
        d.pieslice([cx - r, cy - r, cx + r, cy + r], 270, 90, fill=c2)
    elif kind == 'portrait':
        d.ellipse([cx - r * .8, cy - r, cx + r * .8, cy + r], outline=c1, width=int(r * .08))
        d.ellipse([cx - r * .3, cy - r * .6, cx + r * .3, cy + r * .05], fill=c1)
        d.pieslice([cx - r * .6, cy, cx + r * .6, cy + r * 1.2], 180, 360, fill=c1)
        d.polygon([(cx - r * .45, cy - r * .55), (cx + r * .45, cy - r * .55), (cx + r * .2, cy - r * .85), (cx - r * .2, cy - r * .85)], fill=c1)
    elif kind == 'ribbon':
        for k in range(3):
            y = cy - r * .5 + k * r * .5
            d.line([(cx - r + t * r * .02, y + math.sin(t / 10) * r * .12) for t in range(0, 101, 5)], fill=c1, width=int(r * .14))
    elif kind == 'star':
        pts = []
        for k in range(10):
            a = -math.pi / 2 + k * math.pi / 5
            q = r if k % 2 == 0 else r * .42
            pts.append((cx + math.cos(a) * q, cy + math.sin(a) * q))
        d.polygon(pts, fill=c1)
    elif kind == 'pipe':
        d.rounded_rectangle([cx + r * .1, cy - r * .5, cx + r * .7, cy + r * .5], radius=r * .2, fill=c1)
        d.line([(cx + r * .2, cy + r * .25), (cx - r * .9, cy - r * .05)], fill=c1, width=int(r * .16))
        for k in range(3):
            d.arc([cx + r * (.2 + k * .12), cy - r * (1.1 + k * .1), cx + r * (.6 + k * .12), cy - r * (.6 + k * .1)], 180, 360, fill=c2, width=int(r * .05))
    elif kind == 'bird':
        d.ellipse([cx - r * .55, cy - r * .3, cx + r * .45, cy + r * .4], fill=c1)
        d.ellipse([cx + r * .2, cy - r * .65, cx + r * .7, cy - r * .15], fill=c1)
        d.polygon([(cx + r * .68, cy - r * .45), (cx + r * 1.0, cy - r * .35), (cx + r * .68, cy - r * .3)], fill=c2)
        d.polygon([(cx - r * .5, cy - r * .05), (cx - r * 1.0, cy - r * .3), (cx - r * .9, cy + r * .2)], fill=c1)
        d.polygon([(cx - r * .3, cy - r * .2), (cx + r * .1, cy - r * .9), (cx + r * .2, cy - r * .1)], fill=c2)
    elif kind == 'leafsprig':
        d.line([(cx - r * .8, cy + r * .8), (cx + r * .6, cy - r * .7)], fill=c1, width=int(r * .08))
        for t in (.2, .45, .7):
            x, y = cx - r * .8 + 1.4 * r * t, cy + r * .8 - 1.5 * r * t
            d.ellipse([x - r * .45, y - r * .18, x + r * .05, y + r * .12], fill=c1)
            d.ellipse([x - r * .05, y - r * .3, x + r * .45, y], fill=c1)
    elif kind == 'shield':
        d.polygon([(cx - r * .8, cy - r), (cx + r * .8, cy - r), (cx + r * .8, cy + r * .1), (cx, cy + r), (cx - r * .8, cy + r * .1)], fill=c1)
        d.line([(cx, cy - r * .85), (cx, cy + r * .75)], fill=c2, width=int(r * .1))
        d.line([(cx - r * .65, cy - r * .3), (cx + r * .65, cy - r * .3)], fill=c2, width=int(r * .1))
    elif kind == 'gnome':
        d.polygon([(cx - r * .5, cy - r * .1), (cx, cy - r * 1.1), (cx + r * .5, cy - r * .1)], fill=c2)
        d.ellipse([cx - r * .3, cy - r * .2, cx + r * .3, cy + r * .25], fill=c1)
        d.polygon([(cx - r * .45, cy + r * .1), (cx + r * .45, cy + r * .1), (cx, cy + r * .95)], fill=c1)
        d.ellipse([cx - r * .1, cy - r * .02, cx + r * .1, cy + r * .16], fill=c2)
    elif kind == 'tower':
        d.rectangle([cx - r * .3, cy - r * .5, cx + r * .3, cy + r], fill=c1)
        d.polygon([(cx - r * .4, cy - r * .5), (cx + r * .4, cy - r * .5), (cx, cy - r * 1.1)], fill=c1)
        d.ellipse([cx - r * .2, cy - r * .3, cx + r * .2, cy + r * .1], fill=bg)
        d.line([(cx, cy - r * .1), (cx, cy - r * .25)], fill=c1, width=int(r * .05))
        d.line([(cx, cy - r * .1), (cx + r * .12, cy - r * .1)], fill=c1, width=int(r * .05))


def label(p, path):
    w, h = 2048, 1365
    half = w // 2
    bg, ink, acc = p['bg'], p['ink'], p['acc']
    im = Image.new('RGBA', (w, h), bg + (255,))
    d = ImageDraw.Draw(im)
    dark = tuple(max(0, int(c * .75)) for c in bg)
    for x0 in (0, half):
        d.rectangle([x0 + 40, 40, x0 + half - 40, h - 40], outline=acc, width=8)
        d.rectangle([x0, 0, x0 + half, 26], fill=dark)
        for i in range(0, half, 14):
            d.line([(x0 + i, 0), (x0 + i, 26)], fill=tuple(max(0, int(c * .6)) for c in bg), width=4)
    cx = half / 2
    emblem(im, d, p['emblem'], cx, 335, 150, acc, ink, bg)
    d = ImageDraw.Draw(im)
    text_c(d, cx, 600, p['top'], fit_font(d, p['top'], BEBAS, half - 160, 210), ink)
    text_c(d, cx, 790, p['bottom'], fit_font(d, p['bottom'], SLAB, half - 160, 200), acc)
    d.line([(cx - 300, 920), (cx + 300, 920)], fill=acc, width=6)
    text_c(d, cx, 990, p['sub'], fit_font(d, p['sub'], OSW7, half - 200, 54), ink)
    text_c(d, cx, 1090, '50 g', F(OSW4, 64), ink)
    bx = half + half / 2
    text_c(d, bx, 220, p['name'].upper(), fit_font(d, p['name'].upper(), BEBAS, half - 160, 120), acc)
    d.rectangle([bx - 380, 400, bx + 380, 700], fill=(240, 240, 236))
    d.rectangle([bx - 370, 410, bx + 370, 690], fill=(20, 20, 20))
    d.rectangle([bx - 360, 420, bx + 360, 680], fill=(240, 240, 236))
    text_c(d, bx, 500, 'PIPES ARE NOT', F(OSW7, 90), (20, 20, 20))
    text_c(d, bx, 610, 'A SAFE CHOICE EITHER', F(OSW7, 70), (20, 20, 20))
    text_c(d, bx, 900, p['sub'].title(), fit_font(d, p['sub'].title(), SCRIPT, half - 160, 90), ink)
    text_c(d, bx, 1060, 'RESEAL AFTER USE', F(OSW4, 48), ink)
    grunge(im, len(p['key']), 0.8).convert('RGB').save(path, quality=90)


def hours(h, ru):
    if ru:
        return '1 ч' if h == 1 else '%g ч' % h
    return '1 h' if h == 1 else '%g h' % h


if __name__ == '__main__':
    TEX, SRC, DIST = sys.argv[1:4]
    ru, en = {}, {}
    for p in POUCHES:
        k = p['key'].lower()
        label(p, os.path.join(TEX, 'pouch_%s.jpg' % k))
        Image.open(os.path.join(TEX, 'pouch_%s.jpg' % k)).save(os.path.join(DIST, 'textures', 'pouch_%s.jpg' % k), quality=90)
        sr = 'Трубочный табак, 20 закурок. Трубка: усталость −%g%%, голод на 10%% медленнее %g мин, здоровье −%g%%.' % (p['fat'], p['hunger'], p['hp'])
        se = 'Pipe tobacco, 20 fills. A pipe: fatigue −%g%%, hunger 10%% slower for %g min, condition −%g%%.' % (p['fat'], p['hunger'], p['hp'])
        if p['bonus'] == 'fresh':
            sr += ' «Свежий вдох» на %s.' % hours(p['hours'], True); se += ' "Fresh breath" for %s.' % hours(p['hours'], False)
        elif p['bonus']:
            sr += ' ' + BON[p['bonus']][0] % hours(p['hours'], True); se += ' ' + BON[p['bonus']][1] % hours(p['hours'], False)
        if p['side']:
            sr += ' ' + SIDE[p['side']][0] % hours(1, True); se += ' ' + SIDE[p['side']][1] % hours(1, False)
        ru['SMOKES_Pouch%s_NAME' % p['key']] = 'Табак %s' % p['name']
        en['SMOKES_Pouch%s_NAME' % p['key']] = '%s tobacco' % p['name']
        ru['SMOKES_Pouch%s_DESC' % p['key']] = p['ru'] + ' ' + sr + ' «Набить трубку» набивает именно этим табаком.'
        en['SMOKES_Pouch%s_DESC' % p['key']] = p['en'] + ' ' + se + ' «Fill the pipe» uses this very tobacco.'
        print('pouch', k)
    ru['SMOKES_MSG_TOBACCO_USED'] = 'Трубка набита: {0}'
    en['SMOKES_MSG_TOBACCO_USED'] = 'Pipe filled with: {0}'
    for L, dct in (('ru', ru), ('en', en)):
        json.dump(dct, open(os.path.join(DIST, 'data', 'text_pouch_%s.json' % L), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    lines = ['namespace SmokesByEMPEROR;', '', '/// <summary>Generated by tools/pouches.py: pipe tobacco pouches (the old Captain Block stays in Defs).</summary>',
             'public static class PouchCatalog', '{',
             '    public sealed record Pouch(string Key, float Fatigue, float Health, float Hunger, string Bonus, float Hours, float R, float G, float B, string Side);', '',
             '    public static readonly Pouch[] All =', '    {']
    for p in POUCHES:
        r, g, b = (c / 255 for c in p['tob'])
        lines.append('        new("%s", %gf, %gf, %gf, "%s", %gf, %.3ff, %.3ff, %.3ff, "%s"),' % (p['key'], p['fat'], p['hp'], p['hunger'], p['bonus'], p['hours'], r, g, b, p['side']))
    lines += ['    };', '}', '']
    open(os.path.join(SRC, 'PouchCatalog.cs'), 'w').write('\n'.join(lines))

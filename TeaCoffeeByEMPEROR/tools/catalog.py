"""The mod's products: one pack item (found, traded, brewed) and the drink it gives. Numbers are my choice (11-14) unless the owner says otherwise.
model: tools/models.py name. cup: 'glass' (tea in a glass-holder) / 'mug' (enamel mug) / None (drunk from the pack itself).
servings: units in a found pack. warm: x the game's tea warmth (FreezingBuff), awake: x the game's coffee alertness (FatigueBuff),
rest: sleep heals more (the game's rest buff), heal: condition slowly back (the game's condition-over-time buff), kcal and thirst of one cup.
liquid: colour of the drink in the cup (RGB). rare: weight in loot (100 common)."""

TEAS = [
    dict(id='PolarCaravan', model='tin_tea', servings=8, ru='Полярный караван', en='Polar Caravan', sort_ru='чёрный чай, крупный лист', sort_en='black tea, large leaf',
         warm=1.2, awake=.6, kcal=5, thirst=30, liquid=(118, 46, 14), loot=100,
         d_ru='Крепкий чёрный чай, какой возили через тундру в жестяных банках. Хорошо греет и немного бодрит.',
         d_en='Strong black tea, the kind once carried across the tundra in tins. Warms well and wakes you a little.'),
    dict(id='MorningWhistle', model='box_bags', servings=10, ru='Утренний гудок', en='Morning Whistle', sort_ru='чёрный чай в пакетиках', sort_en='black tea bags',
         warm=1.0, awake=.5, kcal=5, thirst=30, liquid=(128, 52, 18), loot=120,
         d_ru='Чёрный чай в пакетиках, заваривается быстро. Простая кружка, чтобы проснуться.',
         d_en='Black tea in bags, quick to brew. A plain cup to wake up with.'),
    dict(id='CountBergamot', model='tin_tall', servings=8, ru='Граф Бергамот', en='Count Bergamot', sort_ru='чёрный чай с бергамотом', sort_en='black tea with bergamot',
         warm=1.0, awake=.3, rest=True, kcal=5, thirst=30, liquid=(132, 58, 20), loot=70,
         d_ru='Чёрный чай с маслом бергамота. После него крепче спится: сон лечит лучше.',
         d_en='Black tea with oil of bergamot. You sleep sounder after it: rest heals more.'),
    dict(id='JadeRiver', model='box_tall', servings=6, ru='Нефритовая река', en='Jade River', sort_ru='зелёный чай', sort_en='green tea',
         warm=.8, awake=.4, heal=True, kcal=0, thirst=35, liquid=(170, 150, 60), loot=80,
         d_ru='Зелёный чай с горных плантаций. Утоляет жажду и понемногу возвращает силы.',
         d_en='Green tea from mountain gardens. Quenches thirst and slowly brings strength back.'),
    dict(id='Yunling', model='cake', servings=12, ru='Юньлин', en='Yunling', sort_ru='прессованный пуэр', sort_en='pressed pu-erh',
         warm=1.8, awake=.3, kcal=5, thirst=30, liquid=(80, 30, 12), loot=35, warm_hours=2.0,
         d_ru='Блин прессованного пуэра в бумажной обёртке. Тёмный, землистый, греет дольше других.',
         d_en='A cake of pressed pu-erh in its paper wrap. Dark and earthy, it warms longer than the others.'),
    dict(id='CloudCliff', model='tin_low', servings=8, ru='Облачный утёс', en='Cloud Cliff', sort_ru='улун', sort_en='oolong',
         warm=1.0, awake=.7, kcal=0, thirst=35, liquid=(176, 110, 36), loot=50,
         d_ru='Улун из скальных садов. Бодрит, но не сушит: жажда уходит лучше, чем от кофе.',
         d_en='Oolong from cliff gardens. Wakes you without drying you out.'),
    dict(id='PampaVerde', model='pack_paper', servings=6, ru='Пампа Верде', en='Pampa Verde', sort_ru='мате', sort_en='yerba mate',
         warm=.8, awake=1.1, kcal=10, thirst=30, liquid=(150, 140, 50), loot=45,
         d_ru='Мате, травяной напиток пастухов пампы. Бодрит почти как кофе.',
         d_en='Yerba mate, the herdsmen\'s drink of the pampas. Almost as strong as coffee.'),
    dict(id='NileRose', model='box_flat', servings=6, ru='Нильская роза', en='Nile Rose', sort_ru='каркаде', sort_en='hibiscus tea',
         warm=.8, heal=True, kcal=15, thirst=45, liquid=(150, 20, 34), loot=50,
         d_ru='Кислый рубиновый чай из лепестков гибискуса. Лучше всех утоляет жажду и понемногу лечит.',
         d_en='Sour ruby tea of hibiscus petals. Best at quenching thirst, and heals a little.'),
    dict(id='FireweedMeadow', model='pack_paper', servings=6, ru='Кипрейный луг', en='Fireweed Meadow', sort_ru='иван-чай', sort_en='fireweed tea',
         warm=1.0, rest=True, kcal=5, thirst=30, liquid=(150, 80, 30), loot=60,
         d_ru='Ферментированный иван-чай. Без кофеина: успокаивает, и сон лечит лучше.',
         d_en='Fermented fireweed tea. No caffeine: it calms you, and sleep heals more.'),
    dict(id='BombaySpice', model='box_tall', servings=6, ru='Пряный Бомбей', en='Bombay Spice', sort_ru='масала-чай', sort_en='masala chai',
         warm=2.0, awake=.4, kcal=40, thirst=25, liquid=(176, 120, 70), loot=40,
         d_ru='Чёрный чай с имбирём, кардамоном, корицей и гвоздикой. Греет сильнее любого другого.',
         d_en='Black tea with ginger, cardamom, cinnamon and cloves. Warms more than any other.'),
    dict(id='FrostMint', model='box_bags', servings=10, ru='Ледяная мята', en='Frost Mint', sort_ru='мятный чай в пакетиках', sort_en='peppermint tea bags',
         warm=.6, heal=True, kcal=0, thirst=40, liquid=(170, 160, 80), loot=70,
         d_ru='Мятный чай в пакетиках. Свежий, утоляет жажду и понемногу возвращает силы.',
         d_en='Peppermint tea in bags. Fresh, quenches thirst and slowly brings strength back.'),
    dict(id='SilverNeedle', model='tin_tall', servings=6, ru='Серебряные иглы Байшань', en='Baishan Silver Needle', sort_ru='белый чай', sort_en='white tea',
         warm=1.0, awake=.6, rest=True, heal=True, kcal=0, thirst=35, liquid=(204, 168, 90), loot=10,
         d_ru='Редкий белый чай из одних почек. Понемногу всего: тепло, бодрость, крепкий сон и силы.',
         d_en='A rare white tea of buds only. A little of everything: warmth, alertness, sound sleep and strength.'),
]

COFFEES = [
    dict(id='SkipperMocha', model='tin_coffee', servings=8, ru='Шкипер Мокко', en='Skipper Mocha', sort_ru='молотый кофе', sort_en='ground coffee',
         warm=1.0, awake=1.0, kcal=5, thirst=25, liquid=(54, 30, 16), loot=100,
         d_ru='Молотый кофе средней обжарки в большой банке. Обычный крепкий кофе.',
         d_en='Medium roast ground coffee in a big tin. Plain strong coffee.'),
    dict(id='Instanta', model='jar', servings=10, ru='Инстанта', en='Instanta', sort_ru='растворимый кофе', sort_en='instant coffee',
         warm=1.0, awake=.8, kcal=5, thirst=25, liquid=(70, 38, 18), loot=120,
         d_ru='Растворимый кофе в стеклянной банке. Слабее молотого, зато его много.',
         d_en='Instant coffee in a glass jar. Weaker than ground, but there is plenty.'),
    dict(id='VulcanoNero', model='brick', servings=8, ru='Вулкано Неро', en='Vulcano Nero', sort_ru='эспрессо, тёмная обжарка', sort_en='espresso, dark roast',
         warm=1.0, awake=1.6, kcal=5, thirst=10, liquid=(34, 18, 10), loot=40,
         d_ru='Эспрессо тёмной обжарки в вакуумном брикете. Бодрит сильнее всех, но почти не утоляет жажду.',
         d_en='Dark roast espresso in a vacuum brick. The strongest wake-up, but it hardly quenches thirst.'),
    dict(id='SweetTroika', model='box_sticks', servings=10, ru='Сладкая тройка', en='Sweet Troika', sort_ru='кофе 3 в 1 в стиках', sort_en='3-in-1 coffee sticks',
         warm=1.0, awake=.6, kcal=90, thirst=25, liquid=(150, 100, 64), loot=110,
         d_ru='Кофе, сахар и сухие сливки в стиках. Сладкий и сытный, бодрит слабо.',
         d_en='Coffee, sugar and creamer in sticks. Sweet and filling, a mild wake-up.'),
    dict(id='NorthLighthouse', model='bag_valve', servings=8, ru='Северный маяк', en='North Lighthouse', sort_ru='свежая обжарка', sort_en='fresh roast',
         warm=1.2, awake=1.2, kcal=5, thirst=25, liquid=(50, 28, 14), loot=45,
         d_ru='Кофе маленькой обжарочной с побережья, в пакете с клапаном. Бодрит и хорошо греет.',
         d_en='Coffee from a small coastal roaster, in a valve bag. Wakes you and warms well.'),
    dict(id='AndesAltura', model='bag_valve', servings=8, ru='Анды Альтура', en='Andes Altura', sort_ru='колумбийский кофе', sort_en='Colombian coffee',
         warm=1.0, awake=1.1, heal=True, kcal=5, thirst=25, liquid=(58, 32, 16), loot=40,
         d_ru='Высокогорный колумбийский кофе. Мягкий: бодрит и понемногу возвращает силы.',
         d_en='High-grown Colombian coffee. Smooth: wakes you and slowly brings strength back.'),
    dict(id='SultanCardamom', model='pack_paper', servings=6, ru='Султан Кардамон', en='Sultan Cardamom', sort_ru='кофе по-турецки с кардамоном', sort_en='Turkish coffee with cardamom',
         warm=1.6, awake=1.0, kcal=10, thirst=15, liquid=(44, 24, 12), loot=40,
         d_ru='Кофе тончайшего помола с кардамоном, варится по-турецки. Густой, греет сильнее обычного.',
         d_en='Finely ground coffee with cardamom, made the Turkish way. Thick, warms more than usual.'),
    dict(id='BlueBlossom', model='tin_tall', servings=8, ru='Синий цветок', en='Blue Blossom', sort_ru='цикорий', sort_en='chicory',
         warm=1.2, rest=True, kcal=15, thirst=30, liquid=(80, 44, 20), loot=60,
         d_ru='Напиток из жареного корня цикория. Похож на кофе, но без кофеина: греет и не мешает крепкому сну.',
         d_en='A drink of roasted chicory root. Coffee-like but caffeine-free: it warms and lets you sleep sound.'),
    dict(id='Icebreaker', model='can', servings=1, cup=None, ru='Ледокол', en='Icebreaker', sort_ru='холодный кофе в банке', sort_en='canned cold brew',
         warm=0, awake=1.0, kcal=60, thirst=35, liquid=(60, 34, 18), loot=60, cold=True,
         d_ru='Холодный кофе в банке, пьётся сразу, без огня. Бодрит, но не греет.',
         d_en='Cold brew coffee in a can, drunk straight away, no fire. Wakes you up but gives no warmth.'),
    dict(id='FieldCoffee', model='sachet', servings=1, ru='Полевой кофе ПК-3', en='Field Coffee FC-3', sort_ru='армейский растворимый кофе', sort_en='army instant coffee',
         warm=1.0, awake=.9, kcal=40, thirst=25, liquid=(66, 36, 18), loot=70,
         d_ru='Порция растворимого кофе с сахаром из армейского пайка. Одна кружка.',
         d_en='One serving of instant coffee with sugar from an army ration. One cup.'),
]

for t in TEAS: t.setdefault('cup', 'glass'); t['kind'] = 'tea'
for c in COFFEES: c.setdefault('cup', 'mug'); c['kind'] = 'coffee'
OLD = TEAS + COFFEES
for p in OLD:
    p['hand'] = True   # painted by hand in labels.py
    p['cuptex'] = None

# ------------------------------------------------------------------ 0.2.0: the big catalogue (cat_*.py), numbers by kind
LIQ = {'black': (118, 46, 14), 'amber': (176, 110, 36), 'copper': (140, 62, 20), 'green': (170, 150, 60), 'jade': (150, 170, 60), 'matcha': (104, 140, 44),
       'gold': (214, 160, 40), 'yellow': (212, 184, 80), 'pale': (206, 170, 96), 'dark': (80, 30, 12), 'hei': (104, 48, 18), 'ruby': (150, 20, 34),
       'pink': (214, 130, 140), 'purple': (120, 60, 110), 'blue': (40, 70, 160), 'olive': (150, 140, 50), 'rooibos': (160, 60, 24), 'orange': (220, 120, 40),
       'milktea': (184, 132, 88), 'straw': (190, 150, 70), 'herb': (176, 156, 70), 'coffee': (54, 30, 16), 'espresso': (34, 18, 10), 'coffeel': (84, 46, 20),
       'latte': (150, 100, 64), 'cocoa': (92, 52, 34), 'malt': (150, 104, 62), 'cream': (226, 204, 160), 'broth': (120, 70, 30)}
K = {
 # tea
 'black': dict(kind='tea', warm=1.2, awake=.6, kcal=5, thirst=30, liq='black', cup='glass'),
 'blackm': dict(kind='tea', warm=1.4, awake=.5, kcal=70, thirst=25, liq='milktea', cup='mug'),
 'green': dict(kind='tea', warm=.8, awake=.4, heal=True, kcal=0, thirst=35, liq='green', cup='piala'),
 'jgreen': dict(kind='tea', warm=.8, awake=.4, heal=True, kcal=0, thirst=35, liq='jade', cup='piala'),
 'matcha': dict(kind='tea', warm=.8, awake=.9, heal=True, kcal=5, thirst=30, liq='matcha', cup='piala'),
 'roast': dict(kind='tea', warm=1.0, awake=.2, rest=True, kcal=0, thirst=35, liq='amber', cup='piala'),
 'oolong': dict(kind='tea', warm=1.0, awake=.7, kcal=0, thirst=35, liq='amber', cup='piala'),
 'yancha': dict(kind='tea', warm=1.3, awake=.7, kcal=0, thirst=30, liq='copper', cup='piala'),
 'yellow': dict(kind='tea', warm=.9, awake=.4, heal=True, kcal=0, thirst=35, liq='yellow', cup='piala'),
 'sheng': dict(kind='tea', warm=1.2, awake=.7, kcal=0, thirst=35, liq='gold', cup='piala'),
 'shou': dict(kind='tea', warm=1.8, awake=.3, kcal=5, thirst=30, liq='dark', cup='piala', warm_hours=2.0),
 'hei': dict(kind='tea', warm=1.6, awake=.3, kcal=5, thirst=30, liq='hei', cup='piala', warm_hours=1.5),
 'pwhite': dict(kind='tea', warm=1.0, awake=.3, rest=True, heal=True, kcal=0, thirst=35, liq='pale', cup='piala'),
 'pblack': dict(kind='tea', warm=1.3, awake=.6, kcal=5, thirst=30, liq='black', cup='glass'),
 'herb': dict(kind='tea', warm=.8, rest=True, kcal=0, thirst=40, liq='herb', cup='glass'),
 'herbh': dict(kind='tea', warm=.8, heal=True, kcal=0, thirst=40, liq='herb', cup='glass'),
 'rooibos': dict(kind='tea', warm=1.0, rest=True, kcal=0, thirst=40, liq='rooibos', cup='glass'),
 'mate': dict(kind='tea', warm=.8, awake=1.1, kcal=10, thirst=30, liq='olive', cup='mug'),
 'blue': dict(kind='tea', warm=.8, rest=True, kcal=0, thirst=40, liq='blue', cup='glass'),
 'grain': dict(kind='tea', warm=1.0, rest=True, kcal=10, thirst=40, liq='straw', cup='piala'),
 # coffee
 'coffee': dict(kind='coffee', warm=1.0, awake=1.0, kcal=5, thirst=25, liq='coffee', cup='mug'),
 'dark': dict(kind='coffee', warm=1.0, awake=1.4, kcal=5, thirst=15, liq='espresso', cup='mug'),
 'light': dict(kind='coffee', warm=1.0, awake=1.1, heal=True, kcal=5, thirst=25, liq='coffeel', cup='mug'),
 'rare': dict(kind='coffee', warm=1.2, awake=1.2, heal=True, rest=True, kcal=5, thirst=25, liq='coffeel', cup='mug'),
 'spice': dict(kind='coffee', warm=1.6, awake=1.0, kcal=10, thirst=15, liq='espresso', cup='mug'),
 'milkc': dict(kind='coffee', warm=1.0, awake=.6, kcal=90, thirst=25, liq='latte', cup='mug'),
 'instant': dict(kind='coffee', warm=1.0, awake=.8, kcal=5, thirst=25, liq='coffee', cup='mug'),
 'decaf': dict(kind='coffee', warm=1.0, awake=0, rest=True, kcal=5, thirst=25, liq='coffee', cup='mug'),
 'canc': dict(kind='coffee', warm=0, awake=1.0, kcal=30, thirst=35, liq='coffee', cup=None, cold=True, servings=1),
 'cant': dict(kind='tea', warm=0, awake=.4, kcal=10, thirst=40, liq='black', cup=None, cold=True, servings=1),
 # other drinks
 'sub': dict(kind='drink', warm=1.2, rest=True, kcal=15, thirst=30, liq='coffee', cup='mug'),
 'cocoa': dict(kind='drink', warm=1.6, rest=True, kcal=140, thirst=20, liq='cocoa', cup='mug'),
 'malt': dict(kind='drink', warm=1.3, rest=True, kcal=120, thirst=25, liq='malt', cup='mug'),
 'milk': dict(kind='drink', warm=1.8, rest=True, kcal=120, thirst=25, liq='cream', cup='mug'),
 'butter': dict(kind='drink', warm=2.0, awake=.4, kcal=130, thirst=25, liq='milktea', cup='piala'),
 'honey': dict(kind='drink', warm=2.0, heal=True, kcal=90, thirst=25, liq='amber', cup='mug'),
 'fruit': dict(kind='drink', warm=1.0, heal=True, kcal=100, thirst=35, liq='yellow', cup='mug'),
 'husk': dict(kind='drink', warm=1.0, awake=.5, heal=True, kcal=5, thirst=35, liq='copper', cup='glass'),
 'tonic': dict(kind='drink', warm=1.4, heal=True, rest=True, kcal=20, thirst=25, liq='dark', cup='piala'),
 'guarana': dict(kind='drink', warm=.6, awake=1.6, kcal=10, thirst=25, liq='copper', cup='mug'),
 'broth': dict(kind='drink', warm=1.6, heal=True, kcal=60, thirst=20, liq='broth', cup='mug'),
}
SERV = {'cake': 12, 'cake_small': 6, 'tuocha': 6, 'tablet': 6, 'tuo_mini': 1, 'mushroom': 8, 'melon': 14, 'ball': 1, 'tangerine': 1, 'coin': 1, 'brick_tea': 10, 'tile': 8,
        'square': 8, 'square_mini': 1, 'briquette': 4, 'log': 30, 'bamboo': 5, 'basket': 10, 'tube': 8, 'tube_slim': 6, 'tin_square': 8, 'tin_small': 5,
        'tin_wide': 8, 'tin_tea': 8, 'tin_tall': 8, 'tin_low': 8, 'tin_coffee': 8, 'jar': 10, 'jar_small': 6, 'jar_wide': 10, 'doypack': 8, 'sack': 10,
        'carton': 8, 'box_small': 5, 'box_wide': 10, 'box_bags': 10, 'box_tall': 6, 'box_flat': 6, 'box_sticks': 10, 'pack_paper': 6, 'bag_valve': 8,
        'brick': 8, 'sachet': 1, 'stick': 1, 'barrel': 8, 'bottle': 8, 'can': 1, 'can_short': 1, 'can_wide': 1}
import cat_pressed, cat_teas, cat_coffee, cat_drinks, cat_more
class _M: pass
def _mod(rows):
    m = _M(); m.ROWS = rows; return m
import zlib
# owner 11-15: more kinds of cups. By what is drunk, then a steady pick from the product's name so the same kind does not always get the same cup.
CAMP = {'CampfireCoffee', 'RationFive', 'GrenadierBeefTea', 'GoldenRooster', 'BivouacEssence', 'RestStop', 'PeoplesBlend', 'MeridianInstant', 'BogatyrOat'}
def vessel(p):
    cup = p['cup']
    if not cup: return cup
    h = zlib.crc32(p['id'].encode()) % 12
    k = p['k']
    if p['id'] in CAMP: return 'camp'
    if k in ('dark', 'spice'): return 'espresso'
    if k in ('jgreen', 'matcha', 'roast'): return 'yunomi'
    if k in ('milkc', 'cocoa', 'malt', 'milk', 'honey', 'fruit', 'blackm'): return 'ceramic' if h % 3 else 'mug'
    if k in ('coffee', 'instant', 'light', 'rare', 'decaf', 'sub', 'guarana', 'mate'): return ('mug', 'ceramic', 'ceramic', 'mug', 'camp', 'ceramic')[h % 6]
    if cup == 'glass': return 'glass' if h % 3 else 'ceramic'
    return cup
NEW = []
for line, mod in (('pressed', cat_pressed), ('tea', cat_teas), ('coffee', cat_coffee), ('drink', cat_drinks)) + tuple((k, _mod(v)) for k, v in cat_more.ROWS.items()):
    for (i, model, k, ru, en, sru, sen, dru, den, loot, ex) in mod.ROWS:
        p = dict(K[k]); p.update(id=i, model=model, k=k, line=line, ru=ru, en=en, sort_ru=sru, sort_en=sen, d_ru=dru, d_en=den, loot=loot)
        p.setdefault('servings', SERV[model])
        p.update(ex)
        p['liquid'] = LIQ[p['liq']]
        p['cup'] = vessel(p)
        p['cuptex'] = ('cup_' + p['cup'] + '_' + p['liq'] + ('_%d' % (zlib.crc32(p['id'].encode()) % 6) if p['cup'] == 'ceramic' else '_%d' % (zlib.crc32(p['id'].encode()) // 7 % 30) if p['cup'] == 'glass' else '')) if p['cup'] else None
        NEW.append(p)
for p in OLD: p['line'] = p['kind']
ALL = OLD + NEW

# ------------------------------------------------------------------ 0.4.0: the drinks' own effects (owner's picks 11-14; none repeats the game, FoodPack, Noodles, Snacks or Smokes)
# key: (helpful, game hours (0 = a property of the drink), ru, en, what it does ru, en)
FX = {
 'hardy': (True, 3, 'Живучесть', 'Hardiness', 'три часа состояние от голода, жажды, холода и усталости падает на 20% медленнее', 'for three hours condition lost to hunger, thirst, cold and exhaustion drops 20% slower'),
 'antidote': (True, 3, 'Противоядие', 'Antidote', 'три часа химическое отравление выходит на четверть быстрее', 'for three hours chemical poisoning clears a quarter faster'),
 'vitc': (True, 3, 'Крепкие связки', 'Strong Ligaments', 'шанс растянуть лодыжку или запястье на склоне вдвое ниже', 'half the chance of a sprained ankle or wrist on a slope'),
 'map': (True, 2, 'Картограф', 'Mapmaker', 'два часа уголь наносит на карту на четверть больше местности', 'for two hours charcoal maps a quarter more ground'),
 'longheat': (True, 0, 'Долгий жар', 'Lasting Heat', 'остывает вдвое медленнее', 'cools twice as slowly'),
 'nosleep': (False, 2, 'Не спится', 'Wide Awake', 'два часа не лечь спать, пока шкала усталости заполнена больше чем на 40%', 'for two hours you cannot go to sleep while the fatigue bar is more than 40% full'),
 'relaxed': (False, 1, 'Расслабленность', 'Mellow', 'час бег на 10% медленнее', 'for an hour you sprint 10% slower'),
}
_FXK = {'tonic': 'hardy', 'broth': 'hardy', 'milk': 'antidote longheat', 'blackm': 'antidote', 'milkc': 'antidote', 'mate': 'map', 'guarana': 'map nosleep',
        'butter': 'longheat', 'cocoa': 'longheat', 'dark': 'nosleep'}
_FXI = {'ImmortalHerb': 'hardy', 'BirchMushroom': 'hardy', 'SayanWing': 'hardy', 'RoyalMilkTea': 'antidote', 'YukiAuLait': 'antidote', 'Nightcap': 'antidote',
        'ScarletHip': 'vitc', 'BuckthornNoon': 'vitc', 'GoheungYuja': 'vitc', 'FiveFlavours': 'vitc', 'NileRose': 'vitc', 'TaigaBlend': 'vitc', 'DachaCurrant': 'vitc',
        'CranberryKissel': 'vitc', 'PampaVerde': 'map', 'VulcanoNero': 'nosleep', 'DoubleCharge': 'nosleep',
        'BeeBalm': 'relaxed', 'JulyLinden': 'relaxed', 'MeadowOregano': 'relaxed', 'ProvenceVerveine': 'relaxed'}
for p in ALL:
    f = (_FXK.get(p.get('k', ''), '') + ' ' + _FXI.get(p['id'], '')).split()
    p['fx'] = [k for k in FX if k in f]
assert all(i in {p['id'] for p in ALL} for i in _FXI), [i for i in _FXI if i not in {p['id'] for p in ALL}]
ids = [p['id'] for p in ALL]; assert len(ids) == len(set(ids)), [i for i in ids if ids.count(i) > 1]
names = [p['ru'] for p in ALL] + [p['en'] for p in ALL]; assert len(names) == len(set(names)), [i for i in names if names.count(i) > 1]


# Meme descriptions (user rule 11-15): funny two-beat texts replace the plain ones.
import memes1 as _m1, memes2 as _m2, memes3 as _m3, memes4 as _m4
MEMES = {}
for _m in (_m1, _m2, _m3, _m4):
    MEMES.update(_m.M)
for p in ALL:
    p['d_ru'], p['d_en'] = MEMES[p['id']]

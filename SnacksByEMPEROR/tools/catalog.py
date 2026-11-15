"""SnacksByEMPEROR 0.3: world brands sold everywhere and local ones by country. One source for models, labels and the C# catalogue.
The first thirteen brands (catalog_head.py) have hand-made layouts; the rest are described here and drawn by the shared layouts of labels3.py."""
import zlib
from catalog_head import BRANDS as HEAD

NOODLES = []

COUNTRY = {'WW': ('весь мир', 'worldwide'), 'CA': ('Канада', 'Canada'), 'US': ('США', 'USA'), 'RU': ('Россия', 'Russia'), 'SU': ('СССР', 'USSR'),
           'JP': ('Япония', 'Japan'), 'KR': ('Корея', 'Korea'), 'CN': ('Китай', 'China'), 'GB': ('Великобритания', 'United Kingdom'),
           'DE': ('Германия', 'Germany'), 'MX': ('Мексика', 'Mexico'), 'FR': ('Франция', 'France'), 'ES': ('Испания', 'Spain'),
           'IN': ('Индия', 'India'), 'TH': ('Таиланд', 'Thailand'), 'SE': ('Швеция', 'Sweden'), 'AU': ('Австралия', 'Australia'),
           'IT': ('Италия', 'Italy'), 'NL': ('Нидерланды', 'Netherlands'), 'PL': ('Польша', 'Poland'), 'UA': ('Украина', 'Ukraine'), 'BY': ('Беларусь', 'Belarus'),
           'TR': ('Турция', 'Turkey'), 'ID': ('Индонезия', 'Indonesia'), 'PH': ('Филиппины', 'Philippines'), 'BR': ('Бразилия', 'Brazil'), 'NO': ('Норвегия', 'Norway'),
           'FI': ('Финляндия', 'Finland'), 'ZA': ('ЮАР', 'South Africa'), 'GR': ('Греция', 'Greece'), 'IL': ('Израиль', 'Israel')}

# kind: ru type, en type, kcal per 100 g, thirst, eat s, default pack (shape, size), food glyph, ingredients ru / en
KIND = {
    'crisps':   ('Чипсы', 'crisps', 535, -8, 4, ('bag', (.19, .27, .07)), 'chip', 'картофель, масло растительное, соль, приправа.', 'Potatoes, vegetable oil, salt, seasoning.'),
    'ridged':   ('Рифлёные чипсы', 'ridged crisps', 535, -8, 4, ('bag', (.20, .28, .075)), 'ridged', 'картофель, масло растительное, соль, приправа.', 'Potatoes, vegetable oil, salt, seasoning.'),
    'tortilla': ('Кукурузные чипсы', 'tortilla chips', 500, -8, 4, ('bag', (.19, .27, .07)), 'triangle', 'кукурузная мука, масло, соль, приправа.', 'Corn masa flour, vegetable oil, salt, seasoning.'),
    'puffs':    ('Кукурузные снеки', 'corn puffs', 550, -6, 4, ('bag', (.18, .25, .085)), 'puff', 'крупа кукурузная, масло, приправа, соль.', 'Corn meal, vegetable oil, seasoning, salt.'),
    'cones':    ('Кукурузные рожки', 'corn cones', 520, -6, 4, ('bag', (.17, .24, .08)), 'cone', 'мука кукурузная, масло, соль, приправа.', 'Corn meal, vegetable oil, salt, seasoning.'),
    'croutons': ('Сухарики', 'croutons', 400, -5, 3, ('bag', (.12, .17, .035)), 'cube', 'сухари ржаные, масло, соль, ароматизатор.', 'Rye bread, vegetable oil, salt, flavouring.'),
    'seeds':    ('Семечки', 'seeds', 580, -6, 25, ('bag', (.14, .20, .03)), 'seed', 'семена подсолнечника жареные, соль.', 'Roasted sunflower seeds, salt.'),
    'nuts':     ('Орешки', 'nuts', 600, -8, 6, ('can', (.042, .085)), 'nut', 'орехи жареные, масло, соль.', 'Roasted nuts, vegetable oil, salt.'),
    'cornnuts': ('Жареная кукуруза', 'toasted corn', 450, -8, 6, ('bag', (.12, .18, .035)), 'kernel', 'кукуруза жареная, масло, соль.', 'Toasted corn, vegetable oil, salt.'),
    'popcorn':  ('Попкорн', 'popcorn', 400, -4, 5, ('carton', (.11, .05, .17)), 'pop', 'кукуруза, патока, сахар, арахис.', 'Popcorn, molasses, sugar, peanuts.'),
    'pretzel':  ('Крендельки', 'pretzels', 380, -8, 4, ('bag', (.17, .24, .06)), 'pretzel', 'мука пшеничная, соль, солод, дрожжи.', 'Wheat flour, salt, malt, yeast.'),
    'sticks':   ('Соломка', 'sticks', 390, -6, 4, ('carton', (.075, .025, .19)), 'stick', 'мука пшеничная, масло, соль, дрожжи.', 'Wheat flour, vegetable oil, salt, yeast.'),
    'potsticks': ('Картофельная соломка', 'potato sticks', 540, -8, 4, ('bag', (.14, .22, .05)), 'stick', 'картофель, масло, соль.', 'Potatoes, vegetable oil, salt.'),
    'choco':    ('Палочки в глазури', 'coated biscuit sticks', 500, -2, 4, ('carton', (.085, .022, .15)), 'stick', 'мука пшеничная, сахар, какао, масло.', 'Wheat flour, sugar, cocoa mass, vegetable oil.'),
    'cracker':  ('Крекеры', 'crackers', 480, -6, 4, ('carton', (.07, .045, .22)), 'cracker', 'мука пшеничная, масло, соль, разрыхлитель.', 'Wheat flour, vegetable oil, salt, raising agent.'),
    'bagel':    ('Сушки', 'sushki rings', 340, -3, 5, ('bag', (.16, .22, .05)), 'ring', 'мука пшеничная, сахар, масло, соль.', 'Wheat flour, sugar, oil, salt.'),
    'rinds':    ('Свиные шкварки', 'pork rinds', 540, -8, 4, ('bag', (.16, .23, .07)), 'rind', 'свиная шкурка жареная, соль.', 'Fried pork rinds, salt.'),
    'shrimp':   ('Креветочные чипсы', 'shrimp chips', 490, -7, 4, ('bag', (.17, .25, .07)), 'wave', 'мука пшеничная, креветка, масло, соль.', 'Wheat flour, shrimp, vegetable oil, salt.'),
    'rice':     ('Рисовые крекеры', 'rice crackers', 400, -6, 4, ('bag', (.16, .22, .045)), 'crescent', 'рис, соевый соус, сахар, арахис.', 'Rice, soy sauce, sugar, peanuts.'),
    'seaweed':  ('Хрустящие водоросли', 'crispy seaweed', 350, -8, 3, ('bag', (.11, .21, .03)), 'sheet', 'водоросли нори, масло, соль.', 'Seaweed, vegetable oil, salt.'),
    'strips':   ('Острые полоски', 'spicy strips', 330, -10, 5, ('bag', (.13, .19, .025)), 'strip', 'мука пшеничная, масло, перец чили, соль.', 'Wheat gluten, vegetable oil, chilli, salt.'),
    'cup':      ('Картофельные палочки', 'potato sticks', 500, -7, 4, ('cup', (.036, .10)), 'stick', 'картофель, масло, соль, приправа.', 'Potatoes, vegetable oil, salt, seasoning.'),
    'namkeen':  ('Намкин', 'namkeen', 550, -8, 5, ('bag', (.15, .22, .045)), 'crumb', 'нутовая мука, масло, специи, соль.', 'Gram flour, vegetable oil, spices, salt.'),
    'rings':    ('Луковые кольца', 'onion rings', 500, -7, 4, ('bag', (.18, .25, .08)), 'ring', 'мука кукурузная, масло, лук, соль.', 'Corn meal, vegetable oil, onion powder, salt.'),
    'jerky':    ('Вяленая говядина', 'beef jerky', 300, -10, 8, ('bag', (.15, .21, .025)), 'jerk', 'говядина, соевый соус, сахар, специи.', 'Beef, soy sauce, sugar, spices.'),
    'fish':     ('Сушёная рыба', 'dried fish', 250, -10, 8, ('bag', (.14, .20, .025)), 'fish', 'рыба сушёная, соль.', 'Dried fish, salt.'),
    'rusk':     ('Сухари', 'sweet rusks', 390, -3, 5, ('bag', (.17, .26, .07)), 'slab', 'мука пшеничная, сахар, масло, яйцо.', 'Wheat flour, sugar, butter, egg.'),
    'drazhe':   ('Драже', 'coated peanuts', 480, -2, 5, ('carton', (.09, .03, .13)), 'ball', 'арахис, сахар, какао.', 'Peanuts, sugar, cocoa.'),
    'coated':   ('Арахис в хрустящей корочке', 'coated peanuts', 520, -7, 5, ('bag', (.14, .20, .04)), 'ball', 'арахис, мука, крахмал, специи, соль.', 'Peanuts, flour, starch, spices, salt.'),
    'mix':      ('Снек-микс', 'snack mix', 450, -7, 5, ('bag', (.19, .27, .08)), 'crumb', 'хлопья, крендельки, сухарики, приправа.', 'Cereal squares, pretzels, rye chips, seasoning.'),
    'rolls':    ('Кукурузные трубочки', 'rolled tortilla chips', 500, -10, 4, ('bag', (.17, .26, .065)), 'roll', 'кукурузная мука, масло, чили, лайм, соль.', 'Corn masa flour, vegetable oil, chilli, lime, salt.'),
}


def G(id, country, kind, name, rub, enb, tpl, font, style, col, emb, g, sub, var, lang=None, **kw):
    """One shared-layout brand. name: the wordmark as printed; rub/enb: the brand in the game's texts; col: (brand colour, accent);
    g: net grams; sub: the product line as printed; var: (suffix, ru, en, as printed, flavour colour)."""
    return dict(id=id, country=country, kind=kind, name=name, rub=rub, enb=enb, tpl=tpl, font=font, style=style, col=col, emb=emb, g=g, sub=sub, var=var, lang=lang, **kw)


GEN = [
    # ---------------------------------------------------------------- sold everywhere
    G('Doritoz', 'WW', 'tortilla', 'Doritoz', 'Доритоз', 'Doritoz', 'triangle', 'alfa', 'solid', ('#111111', '#ffd43b'), 'flame', 150, 'TORTILLA CHIPS', [
        ('Nacho', 'сыр начо', 'nacho cheese', 'NACHO CHEESE', '#e8590c'), ('Ranch', 'соус ранч', 'cool ranch', 'COOL RANCH', '#1864ab'),
        ('Chili', 'сладкий чили', 'sweet chilli', 'SWEET CHILI', '#862e9c'), ('Hot', 'огненные', 'flamin hot', 'FLAMIN HOT', '#c92a2a'), ('Salsa', 'сальса', 'salsa', 'SALSA VERDE', '#2b8a3e')]),
    G('Rufflz', 'WW', 'ridged', 'Rufflz', 'Раффлз', 'Rufflz', 'band', 'inter_black', 'solid', ('#ffffff', '#ffd43b'), 'waves', 150, 'RIDGED POTATO CRISPS', [
        ('Original', 'оригинальные', 'original', 'ORIGINAL', '#1c5fb8'), ('Cheddar', 'чеддер и сметана', 'cheddar & sour cream', 'CHEDDAR & SOUR CREAM', '#e67700'),
        ('BBQ', 'барбекю', 'barbecue', 'BARBECUE', '#7a2e0e'), ('Onion', 'сметана и лук', 'sour cream & onion', 'SOUR CREAM & ONION', '#2b8a3e')]),
    G('Bugelz', 'WW', 'cones', 'Bugelz', 'Бьюгелз', 'Bugelz', 'diag', 'lobster', 'solid', ('#12264d', '#ffd43b'), 'horn', 100, 'CRISPY CORN CONES', [
        ('Original', 'оригинальные', 'original', 'ORIGINAL', '#d6336c'), ('Nacho', 'сыр начо', 'nacho cheese', 'NACHO CHEESE', '#f08c00'), ('BBQ', 'барбекю', 'barbecue', 'SMOKIN BBQ', '#a61e4d')]),
    G('Planterz', 'WW', 'nuts', 'Planterz', 'Плантерз', 'Planterz', 'frame', 'playfair_l', 'solid', ('#f8e08e', '#f8e08e'), 'nut', 180, 'PEANUTS', [
        ('Salted', 'солёный арахис', 'salted peanuts', 'SALTED', '#0b4f8a'), ('Honey', 'арахис в меду', 'honey roasted', 'HONEY ROASTED', '#9c5a00'),
        ('Dry', 'сухой обжарки', 'dry roasted', 'DRY ROASTED', '#7a1f1f'), ('Cashew', 'кешью', 'cashews', 'CASHEWS', '#1b5e20')], size=(.045, .10)),
    G('Tuk', 'WW', 'cracker', 'TUK', 'Тук', 'Tuk', 'big', 'inter_black', 'solid', ('#ffe066', '#ffd43b'), 'none', 100, 'SALTED CRACKERS', [
        ('Original', 'оригинальные', 'original', 'ORIGINAL', '#f2b705'), ('Cheese', 'сыр', 'cheese', 'CHEESE', '#f08c00'),
        ('Paprika', 'паприка', 'paprika', 'PAPRIKA', '#d9480f'), ('Bacon', 'бекон', 'bacon', 'BACON', '#c2255c')]),
    G('Ritzy', 'WW', 'cracker', 'Ritzy', 'Ритци', 'Ritzy', 'badge', 'abril', 'brand', ('#c1121f', '#ffd43b'), 'sunring', 200, 'CRACKERS', [
        ('Original', 'оригинальные', 'original', 'ORIGINAL', '#ffd43b'), ('Cheese', 'сыр', 'cheese', 'CHEESE', '#ff922b'), ('Wheat', 'цельнозерновые', 'whole wheat', 'WHOLE WHEAT', '#d8b56a')],
      size=(.14, .06, .20)),
    # ---------------------------------------------------------------- Canada
    G('OldDatch', 'CA', 'crisps', 'Old Datch', 'Олд Датч', 'Old Datch', 'retro', 'alfa', 'light', ('#f6efe0', '#9b1c1c'), 'mill', 220, 'POTATO CHIPS · CROUSTILLES', [
        ('Ketchup', 'кетчуп', 'ketchup', 'KETCHUP', '#c1121f'), ('Dressed', 'со всеми приправами', 'all dressed', 'ALL DRESSED', '#6a1b9a'),
        ('Dill', 'маринованный огурец', 'dill pickle', 'DILL PICKLE', '#2b7a0b'), ('Vinegar', 'соль и уксус', 'salt & vinegar', 'SALT & VINEGAR', '#0b5394'),
        ('Ripple', 'рифлёные с солью', 'ripple salted', 'RIPPLE', '#b45309')], shape='carton', size=(.19, .10, .25)),
    G('Hokinz', 'CA', 'puffs', 'Hokinz', 'Хокинз Чизиз', 'Hokinz Cheezies', 'stripes', 'alfa', 'brand', ('#c1121f', '#ffd43b'), 'none', 210, 'CHEEZIES', [
        ('Original', 'чеддер', 'cheddar', 'REAL CHEDDAR', '#ffd43b'), ('Jalapeno', 'халапеньо', 'jalapeno', 'JALAPEÑO', '#8ce99a'), ('White', 'белый чеддер', 'white cheddar', 'WHITE CHEDDAR', '#f8f9fa')]),
    G('MissViki', 'CA', 'crisps', 'Miss Viki’s', 'Мисс Вики', 'Miss Viki’s', 'frame', 'playfair_l', 'light', ('#f7f1e3', '#3a2a1a'), 'wheat', 200, 'KETTLE COOKED CHIPS', [
        ('Salt', 'морская соль', 'sea salt', 'SEA SALT', '#1c7ed6'), ('Jalapeno', 'халапеньо', 'jalapeno', 'JALAPEÑO', '#2f9e44'),
        ('Chili', 'сладкий чили и сметана', 'sweet chili & sour cream', 'SWEET CHILI & SOUR CREAM', '#d6336c'), ('Malt', 'соль и солодовый уксус', 'sea salt & malt vinegar', 'SALT & MALT VINEGAR', '#7048e8')]),
    G('Hikori', 'CA', 'potsticks', 'Hikori Stiks', 'Хикори Стикс', 'Hikori Stiks', 'diag', 'bebas', 'solid', ('#1a1a1a', '#ffd43b'), 'none', 150, 'POTATO STICKS', [
        ('Original', 'с дымком', 'hickory smoke', 'ORIGINAL', '#b3541e'), ('BBQ', 'барбекю', 'barbecue', 'BBQ', '#8a1c1c'), ('Cheddar', 'чеддер', 'cheddar', 'CHEDDAR', '#e8930c')]),
    G('GreatBear', 'CA', 'crisps', 'Great Bear', 'Грейт Бэр', 'Great Bear', 'badge', 'cinzel', 'dark', ('#14213d', '#fca311'), 'bear', 180, 'ISLAND KETTLE CHIPS', [
        ('Maple', 'кленовый бекон', 'maple bacon', 'MAPLE BACON', '#d9480f'), ('Salmon', 'копчёный лосось', 'smoked salmon', 'SMOKED SALMON', '#f783ac'),
        ('Salt', 'морская соль', 'sea salt', 'SEA SALT', '#74c0fc'), ('Moose', 'рагу из лося', 'moose stew', 'MOOSE STEW', '#a9744f')]),
    # ---------------------------------------------------------------- USA
    G('Fritoz', 'US', 'tortilla', 'Fritoz', 'Фритоз', 'Fritoz', 'burst', 'alfa', 'solid', ('#ffffff', '#ffd43b'), 'none', 260, 'CORN CHIPS', [
        ('Original', 'оригинальные', 'original', 'ORIGINAL', '#d9480f'), ('Chili', 'чили с сыром', 'chili cheese', 'CHILI CHEESE', '#8a1c1c'),
        ('BBQ', 'барбекю с мёдом', 'honey bbq', 'HONEY BBQ', '#5c3317'), ('Scoops', 'ложечки', 'scoops', 'SCOOPS!', '#1864ab')], food='curl'),
    G('KrakerJak', 'US', 'popcorn', 'Kraker Jak', 'Крекер Джек', 'Kraker Jak', 'retro', 'alfa', 'light', ('#fdf6e3', '#b91c1c'), 'starring', 85, 'CARAMEL COATED POPCORN & PEANUTS', [
        ('Original', 'карамель и арахис', 'caramel & peanuts', 'THE ORIGINAL', '#1d3a8a'), ('Toffee', 'сливочная ириска', 'butter toffee', 'BUTTER TOFFEE', '#b45309'),
        ('Peanut', 'двойной арахис', 'extra peanuts', 'EXTRA PEANUTS', '#b91c1c')], thirst=-3),
    G('KornNutz', 'US', 'cornnuts', 'KORN NUTZ', 'Корн Натс', 'Korn Nutz', 'big', 'bebas', 'solid', ('#ffffff', '#ffd43b'), 'none', 113, 'CRUNCHY CORN KERNELS', [
        ('Original', 'оригинальная', 'original', 'ORIGINAL', '#1c5fb8'), ('Ranch', 'ранч', 'ranch', 'RANCH', '#2b8a3e'),
        ('Chile', 'чили пиканте', 'chile picante', 'CHILE PICANTE', '#c92a2a'), ('BBQ', 'барбекю', 'barbecue', 'BBQ', '#5c2e0e')]),
    G('Snaiderz', 'US', 'pretzel', 'Snaiderz', 'Снайдерз', 'Snaiderz', 'window', 'playfair_l', 'solid', ('#ffffff', '#ffd43b'), 'none', 250, 'PRETZELS OF HANOVER', [
        ('Mini', 'мини, с солью', 'mini salted', 'MINI PRETZELS', '#0b3d91'), ('Mustard', 'мёд и горчица', 'honey mustard & onion', 'HONEY MUSTARD', '#b8860b'),
        ('Jalapeno', 'халапеньо', 'jalapeno', 'JALAPEÑO', '#2b7a0b'), ('Sourdough', 'на закваске', 'sourdough', 'SOURDOUGH', '#7a4a1e')]),
    G('Maks', 'US', 'rinds', 'Mak’s', 'Макс', 'Mak’s', 'band', 'alfa', 'solid', ('#ffffff', '#ffd43b'), 'pig', 140, 'PORK SKINS', [
        ('Original', 'оригинальные', 'original', 'ORIGINAL', '#b91c1c'), ('BBQ', 'барбекю', 'barbecue', 'BAR-B-Q', '#7a2e0e'), ('Hot', 'острые', 'hot & spicy', 'HOT & SPICY', '#e8590c')]),
    G('Deivid', 'US', 'seeds', 'DEIVID', 'Дэйвид', 'Deivid', 'stripes', 'inter_black', 'brand', ('#b91c1c', '#ffffff'), 'none', 150, 'SUNFLOWER SEEDS', [
        ('Original', 'оригинальные', 'original', 'ORIGINAL', '#ffd43b'), ('Ranch', 'ранч', 'ranch', 'RANCH', '#74c0fc'),
        ('BBQ', 'барбекю', 'barbecue', 'BAR-B-Q', '#ffa94d'), ('Dill', 'маринованный огурец', 'dill pickle', 'DILL PICKLE', '#8ce99a')]),
    # ---------------------------------------------------------------- Russia and the USSR
    G('Moskovskiy', 'SU', 'crisps', 'МОСКОВСКИЙ', 'Московский картофель', 'Moskovskiy', 'retro', 'oldstd', 'light', ('#f3ead2', '#8a1c1c'), 'tower', 50, 'КАРТОФЕЛЬ ХРУСТЯЩИЙ В ЛОМТИКАХ', [
        ('Salt', 'с солью', 'salted', 'С СОЛЬЮ', '#8a1c1c'), ('Mushroom', 'с грибами', 'with mushrooms', 'С ГРИБАМИ', '#5c4420'), ('Cheese', 'с сыром', 'with cheese', 'С СЫРОМ', '#a86a00')],
      lang='ru', shape='carton', size=(.12, .045, .17)),
    G('Babuliny', 'RU', 'seeds', 'Бабулины', 'Бабулины', 'Babuliny', 'frame', 'ptserif_bi', 'solid', ('#fff3d6', '#ffd43b'), 'sunring', 200, 'СЕМЕЧКИ ЖАРЕНЫЕ', [
        ('Black', 'чёрные', 'black sunflower', 'ЧЁРНЫЕ', '#232323'), ('White', 'белые', 'white sunflower', 'БЕЛЫЕ', '#8a6d3b'),
        ('Salt', 'с морской солью', 'sea salted', 'С МОРСКОЙ СОЛЬЮ', '#0b5394'), ('Pumpkin', 'тыквенные', 'pumpkin', 'ТЫКВЕННЫЕ', '#2b6b1f')], lang='ru'),
    G('Chelnochok', 'RU', 'bagel', 'Челночок', 'Челночок', 'Chelnochok', 'window', 'comfortaa', 'solid', ('#ffffff', '#ffe066'), 'none', 200, 'СУШКИ', [
        ('Poppy', 'с маком', 'with poppy seeds', 'С МАКОМ', '#3b5bdb'), ('Salt', 'с солью', 'salted', 'С СОЛЬЮ', '#c2255c'),
        ('Vanilla', 'ванильные', 'vanilla', 'ВАНИЛЬНЫЕ', '#e8930c'), ('Mustard', 'горчичные', 'mustard', 'ГОРЧИЧНЫЕ', '#5c940d')], lang='ru'),
    G('Birka', 'RU', 'croutons', 'БИРКА', 'Бирка', 'Birka', 'diag', 'inter_black', 'solid', ('#1a1a1a', '#ffd43b'), 'none', 60, 'ГРЕНКИ К ПЕННОМУ', [
        ('Garlic', 'чеснок', 'garlic', 'ЧЕСНОК', '#e8930c'), ('Horseradish', 'хрен', 'horseradish', 'ХРЕН', '#2b8a3e'),
        ('Adjika', 'аджика', 'adjika', 'АДЖИКА', '#c92a2a'), ('Cheese', 'сыр', 'cheese', 'СЫР', '#f2b705')], lang='ru', size=(.13, .20, .04), food='slab'),
    G('KChayu', 'SU', 'sticks', 'К ЧАЮ', 'К чаю', 'K Chayu', 'retro', 'oldstd', 'light', ('#f1ead8', '#1f3a68'), 'kettle', 200, 'СОЛОМКА', [
        ('Sweet', 'сладкая', 'sweet', 'СЛАДКАЯ', '#b02a37'), ('Salt', 'солёная', 'salted', 'СОЛЁНАЯ', '#1f3a68'), ('Poppy', 'с маком', 'with poppy seeds', 'С МАКОМ', '#3a3a3a')],
      lang='ru', size=(.09, .035, .24), thirst=-3),
    # ---------------------------------------------------------------- Japan
    G('Karubi', 'JP', 'crisps', 'カルビ', 'Каруби', 'Karubi', 'asian', 'cjk', 'solid', ('#ffffff', '#ffd43b'), 'none', 60, 'ポテトチップス', [
        ('Usushio', 'лёгкая соль', 'lightly salted', 'うすしお味', '#e8590c'), ('Consomme', 'консоме', 'consomme punch', 'コンソメパンチ', '#c92a2a'),
        ('Norishio', 'нори и соль', 'seaweed salt', 'のりしお', '#2b8a3e'), ('Shoyu', 'соевый соус', 'soy sauce', 'しょうゆ', '#5c2e0e')], size=(.17, .21, .06)),
    G('Ebisen', 'JP', 'shrimp', 'えびせん', 'Эбисэн', 'Ebisen', 'big', 'cjk', 'solid', ('#ffffff', '#ffd43b'), 'none', 85, 'やめられない とまらない', [
        ('Original', 'креветка', 'shrimp', 'えび', '#d6336c'), ('Wasabi', 'васаби', 'wasabi', 'わさび', '#5c940d'), ('Ume', 'слива умэ', 'pickled plum', '梅', '#a61e4d')]),
    G('Poki', 'JP', 'choco', 'Poki', 'Поки', 'Poki', 'big', 'inter_black', 'solid', ('#ffffff', '#ffd43b'), 'none', 70, 'チョコレート スティック', [
        ('Choco', 'шоколад', 'chocolate', 'チョコレート', '#d90429'), ('Strawberry', 'клубника', 'strawberry', 'いちご', '#f06595'),
        ('Matcha', 'чай матча', 'matcha', '抹茶', '#5c940d'), ('Almond', 'миндаль', 'almond crush', 'アーモンド', '#7a4a1e')]),
    G('Kakipi', 'JP', 'rice', '柿ピー', 'Каки-пи', 'Kaki-pi', 'asian', 'cjk', 'solid', ('#ffffff', '#ffd43b'), 'none', 90, '柿の種とピーナッツ', [
        ('Original', 'соевый соус', 'soy sauce', '醤油', '#e8590c'), ('Wasabi', 'васаби', 'wasabi', 'わさび', '#2f9e44'), ('Ume', 'слива умэ', 'plum', '梅しそ', '#c2255c')]),
    G('Jagariko', 'JP', 'cup', 'じゃがりこ', 'Дзягарико', 'Jagariko', 'band', 'cjk', 'solid', ('#ffffff', '#ffd43b'), 'none', 60, 'POTATO STICKS', [
        ('Salad', 'салат', 'salad', 'サラダ', '#2b8a3e'), ('Cheese', 'сыр', 'cheese', 'チーズ', '#e8930c'), ('Butter', 'картофель с маслом', 'buttered potato', 'じゃがバター', '#b8860b')]),
    # ---------------------------------------------------------------- Korea
    G('Saeukkang', 'KR', 'shrimp', '새우깡', 'Сэуккан', 'Saeukkang', 'burst', 'cjk', 'solid', ('#ffffff', '#ffd43b'), 'none', 90, 'SHRIMP CRACKERS', [
        ('Original', 'креветка', 'shrimp', '오리지널', '#d9480f'), ('Spicy', 'острые', 'spicy', '매운맛', '#a61e1e'), ('Truffle', 'чёрный трюфель', 'black truffle', '블랙 트러플', '#2b2b2b')], food='stick'),
    G('HoneyButter', 'KR', 'crisps', '허니버터칩', 'Хани Батер Чип', 'Honey Butter Chip', 'window', 'cjk', 'solid', ('#ffffff', '#7a4a00'), 'none', 60, 'HONEY BUTTER CHIP', [
        ('Honey', 'мёд и масло', 'honey butter', '허니버터', '#f2b705'), ('Maple', 'клён и сыр', 'maple cheese', '메이플 치즈', '#d9480f'), ('Cherry', 'цветы вишни', 'cherry blossom', '체리블라썸', '#f783ac')]),
    G('Kkokkal', 'KR', 'cones', '꼬깔콘', 'Ккоккалькон', 'Kkokkalcorn', 'diag', 'cjk', 'solid', ('#12264d', '#ffd43b'), 'none', 72, 'CORN SNACK', [
        ('Roast', 'жареная кукуруза', 'roasted corn', '군옥수수맛', '#e8930c'), ('Nutty', 'ореховые', 'nutty', '고소한맛', '#c1121f'), ('Spicy', 'острые', 'spicy', '매콤달콤', '#7048e8')]),
    # ---------------------------------------------------------------- China
    G('WangWang', 'CN', 'rice', '旺旺', 'Ван-Ван', 'Wang Wang', 'badge', 'cjk', 'brand', ('#d90429', '#ffd43b'), 'none', 84, '米果', [
        ('Snow', 'снежные', 'snow rice crackers', '雪饼', '#ffffff'), ('Senbei', 'сэмбэй', 'senbei', '仙贝', '#ffd43b'), ('Crisp', 'хрустики', 'mini crisps', '小小酥', '#ffa94d')]),
    G('LaLong', 'CN', 'strips', '辣龙', 'Ла Лун', 'La Long', 'asian', 'cjk', 'solid', ('#ffffff', '#ffd43b'), 'none', 106, '辣条', [
        ('Big', 'большие полоски', 'big strips', '大面筋', '#c1121f'), ('Kiss', 'жгучий поцелуй', 'kiss burn', '亲嘴烧', '#e8590c'), ('Small', 'малые полоски', 'small strips', '小面筋', '#7a0c0c')]),
    G('ChaCha', 'CN', 'seeds', '恰恰', 'Ча-Ча', 'Cha Cha', 'band', 'cjk', 'solid', ('#ffffff', '#ffd43b'), 'sunring', 160, '香瓜子', [
        ('Original', 'пять специй', 'five spice', '五香', '#c1121f'), ('Caramel', 'карамель', 'caramel', '焦糖', '#b45309'), ('Pecan', 'орех пекан', 'pecan', '山核桃', '#1b5e20')]),
    # ---------------------------------------------------------------- United Kingdom
    G('Wolkers', 'GB', 'crisps', 'Wolkers', 'Уолкерс', 'Wolkers', 'burst', 'inter_black', 'solid', ('#ffffff', '#ffd43b'), 'none', 45, 'CRISPS', [
        ('Salted', 'с солью', 'ready salted', 'READY SALTED', '#c1121f'), ('Cheese', 'сыр и лук', 'cheese & onion', 'CHEESE & ONION', '#1864ab'),
        ('Vinegar', 'соль и уксус', 'salt & vinegar', 'SALT & VINEGAR', '#2b8a3e'), ('Prawn', 'креветочный коктейль', 'prawn cocktail', 'PRAWN COCKTAIL', '#e64980'),
        ('Chicken', 'жареная курица', 'roast chicken', 'ROAST CHICKEN', '#e8930c')], size=(.14, .19, .05)),
    G('Monstr', 'GB', 'puffs', 'MONSTR MANCH', 'Монстр Манч', 'Monstr Manch', 'big', 'alfa', 'solid', ('#ffffff', '#ffd43b'), 'none', 72, 'BAKED CORN SNACK', [
        ('Onion', 'маринованный лук', 'pickled onion', 'PICKLED ONION', '#5f3dc4'), ('Beef', 'ростбиф', 'roast beef', 'ROAST BEEF', '#8a3b12'), ('Hot', 'огненные', 'flamin hot', 'FLAMIN HOT', '#d9480f')], food='paw'),
    G('Twiglitz', 'GB', 'sticks', 'Twiglitz', 'Твиглитс', 'Twiglitz', 'diag', 'abril', 'solid', ('#1a1a1a', '#ffd43b'), 'none', 105, 'KNOBBLY WHEAT STICKS', [
        ('Original', 'дрожжевой экстракт', 'original', 'ORIGINAL', '#b91c1c'), ('Curry', 'карри', 'curry', 'CURRY', '#b8860b'), ('Worcester', 'вустерский соус', 'worcester sauce', 'WORCESTER SAUCE', '#5c2e0e')],
      shape='bag', size=(.15, .22, .05)),
    G('MrPorki', 'GB', 'rinds', 'Mr. Porki', 'Мистер Порки', 'Mr. Porki', 'frame', 'alfa', 'dark', ('#1a1a1a', '#f1c232'), 'pig', 40, 'PORK SCRATCHINGS', [
        ('Original', 'оригинальные', 'original', 'ORIGINAL', '#f1c232'), ('Crispy', 'хрустящие', 'crispy strips', 'CRISPY STRIPS', '#ff922b'), ('Hot', 'острые', 'hot', 'HOT', '#e03131')], size=(.12, .16, .04)),
    # ---------------------------------------------------------------- Germany
    G('Fanni', 'DE', 'crisps', 'fanni-frisch', 'Фанни-фриш', 'Fanni-frisch', 'band', 'inter_black', 'solid', ('#ffffff', '#ffd43b'), 'sunring', 150, 'CHIPSFRISCH', [
        ('Ungarisch', 'по-венгерски', 'hungarian paprika', 'UNGARISCH', '#c1121f'), ('Oriental', 'ориентал', 'oriental', 'ORIENTAL', '#e8930c'),
        ('Peperoni', 'пеперони', 'pepperoni', 'PEPERONI', '#7a0c0c'), ('Sour', 'сметана', 'sour cream', 'SOUR CREAM', '#2b8a3e')]),
    G('Flippis', 'DE', 'puffs', 'Flippis', 'Флиппис', 'Flippis', 'window', 'lobster', 'solid', ('#ffffff', '#ffd43b'), 'none', 200, 'ERDNUSSFLIPS', [
        ('Klassik', 'арахисовые', 'peanut classic', 'KLASSISCH', '#b45309'), ('Honig', 'мёд', 'honey', 'HONIG', '#e8930c'), ('Paprika', 'паприка', 'paprika', 'PAPRIKA', '#c1121f')], food='flip'),
    G('Salzletten', 'DE', 'sticks', 'Salzletten', 'Зальцлеттен', 'Salzletten', 'big', 'yanone', 'solid', ('#ffffff', '#ffd43b'), 'none', 250, 'SALZSTANGEN', [
        ('Klassik', 'классические', 'classic', 'KLASSISCH', '#0b3d91'), ('Sesam', 'кунжут', 'sesame', 'SESAM', '#b8860b'), ('Laugen', 'брецель', 'pretzel style', 'LAUGEN', '#7a4a1e')], size=(.085, .04, .26)),
    # ---------------------------------------------------------------- Mexico
    G('Sabritoz', 'MX', 'crisps', 'Sabritoz', 'Сабритоз', 'Sabritoz', 'burst', 'lobster', 'solid', ('#ffffff', '#ffd43b'), 'none', 160, 'PAPAS FRITAS', [
        ('Sal', 'с солью', 'salted', 'CON SAL', '#f2b705'), ('Adobadas', 'адобо', 'adobadas', 'ADOBADAS', '#c1121f'),
        ('Limon', 'лимон', 'lime', 'LIMÓN', '#5c940d'), ('Habanero', 'хабанеро', 'habanero', 'HABANERO', '#d9480f')]),
    G('Takiz', 'MX', 'rolls', 'TAKIZ', 'Такиз', 'Takiz', 'diag', 'alfa', 'solid', ('#1a1a1a', '#ffd43b'), 'flame', 280, 'TORTILLA ENROLLADA', [
        ('Fuego', 'огонь: чили и лайм', 'fuego', 'FUEGO', '#6a1b9a'), ('Nitro', 'нитро: хабанеро', 'nitro', 'NITRO', '#c1121f'),
        ('Guacamole', 'гуакамоле', 'guacamole', 'GUACAMOLE', '#2b8a3e'), ('Blue', 'синее пламя', 'blue heat', 'BLUE HEAT', '#1c5fb8')]),
    G('Churritoz', 'MX', 'potsticks', 'Churritoz', 'Чурритоз', 'Churritoz', 'stripes', 'pacifico', 'brand', ('#0b7285', '#ffd43b'), 'none', 120, 'CHURRITOS DE MAÍZ', [
        ('Limon', 'лимон и соль', 'lime & salt', 'LIMÓN Y SAL', '#c0eb75'), ('Chile', 'чили', 'chilli', 'CHILE', '#ff8787'), ('Queso', 'сыр', 'cheese', 'QUESO', '#ffd43b')]),
    # ---------------------------------------------------------------- France, Spain
    G('Bretz', 'FR', 'ridged', 'Bretz', 'Бретц', 'Bretz', 'frame', 'abril', 'solid', ('#ffffff', '#ffd43b'), 'none', 125, 'CHIPS ONDULÉES', [
        ('Poulet', 'жареная курица', 'braised chicken', 'POULET BRAISÉ', '#d9480f'), ('Fromage', 'сыр из Юры', 'Jura cheese', 'FROMAGE DU JURA', '#b8860b'),
        ('Chevre', 'козий сыр и перец', 'goat cheese & chilli', 'CHÈVRE PIMENT', '#a61e4d'), ('Nature', 'с солью', 'salted', 'NATURE', '#0b3d91')]),
    G('Fakundo', 'ES', 'seeds', 'Fakundo', 'Факундо', 'Fakundo', 'retro', 'alfa', 'light', ('#fff4d6', '#b91c1c'), 'sunring', 160, 'PIPAS GIGANTES', [
        ('Sal', 'с солью', 'salted', 'CON SAL', '#b91c1c'), ('Sinsal', 'без соли', 'unsalted', 'SIN SAL', '#1c5fb8'), ('Tijuana', 'тихуана, острые', 'tijuana spicy', 'TIJUANA', '#2b7a0b')]),
    # ---------------------------------------------------------------- India, Thailand, Australia
    G('Haldiramz', 'IN', 'namkeen', 'Haldiramz', 'Халдирамз', 'Haldiramz', 'badge', 'playfair_l', 'brand', ('#c1121f', '#ffd43b'), 'none', 200, 'NAMKEEN', [
        ('Bhujia', 'алу бхуджия', 'aloo bhujia', 'ALOO BHUJIA', '#ffd43b'), ('Moong', 'мунг дал', 'moong dal', 'MOONG DAL', '#c0eb75'),
        ('Khatta', 'кхатта митха', 'khatta meetha', 'KHATTA MEETHA', '#ffa94d'), ('Navratan', 'навратан', 'navratan mix', 'NAVRATAN MIX', '#f783ac')]),
    G('Kurkurz', 'IN', 'puffs', 'Kurkurz', 'Куркурз', 'Kurkurz', 'diag', 'lobster', 'solid', ('#12264d', '#ffd43b'), 'none', 90, 'CRUNCHY CORN STICKS', [
        ('Masala', 'масала', 'masala munch', 'MASALA MUNCH', '#e8590c'), ('Chutney', 'зелёный чатни', 'green chutney', 'GREEN CHUTNEY', '#2b8a3e'), ('Chilli', 'чили', 'chilli chatka', 'CHILLI CHATKA', '#c1121f')], food='stick'),
    G('TaoKeNoy', 'TH', 'seaweed', 'Tao Ke Noy', 'Тао Кэ Ной', 'Tao Ke Noy', 'asian', 'inter_black', 'solid', ('#ffffff', '#ffd43b'), 'none', 32, 'CRISPY SEAWEED', [
        ('Classic', 'классические', 'classic', 'CLASSIC', '#2b8a3e'), ('Spicy', 'острые', 'hot & spicy', 'HOT & SPICY', '#c1121f'), ('Tomyum', 'том-ям', 'tom yum', 'TOM YUM', '#e8590c')]),
    G('Twistiz', 'AU', 'puffs', 'Twistiz', 'Твистиз', 'Twistiz', 'stripes', 'alfa', 'brand', ('#f2b705', '#c1121f'), 'none', 90, 'CORN TWISTS', [
        ('Cheese', 'сыр', 'cheese', 'CHEESE', '#c1121f'), ('Chicken', 'курица', 'chicken', 'CHICKEN', '#1c5fb8'), ('Chilli', 'чили', 'chilli', 'CHILLI', '#2b2b2b')]),
]

from art import mix as _mix, rgb as _rgb


def _hex(c): return c if isinstance(c, str) else '#%02x%02x%02x' % tuple(c)


def _pal(b, col):
    brand, acc = b['col']
    lum = sum(_rgb(col)) / 3
    if b['style'] == 'solid':   # the pack is the flavour's colour
        return (col, '#ffffff' if lum < 190 else '#1a1a1a', acc if lum < 190 else '#c1121f', _hex(_mix(col, '#000000', .62)))
    if b['style'] == 'black':   # a premium line: black pack, the flavour as the accent
        return ('#17181a', '#ffffff', col, '#000000')
    if b['style'] == 'dark':
        return (brand, '#ffffff', col, '#000000')
    if b['style'] == 'light':   # paper, one ink per flavour
        return (brand, col, col, _hex(_mix(col, '#000000', .55)))
    return (brand, '#ffffff', col, _hex(_mix(brand, '#000000', .6)))   # brand colour, flavour as the accent


def _expand(g):
    k = KIND[g['kind']]
    shape, size = k[5]
    shape = g.get('shape', shape)
    size = g.get('size') or (size if shape == k[5][0] else None)
    if 'size' not in g and shape == 'bag':   # no two brands' bags quite alike
        j = zlib.crc32(g['id'].encode())
        size = (round(size[0] * (.92 + (j % 17) / 100), 3), round(size[1] * (.92 + (j // 17 % 17) / 100), 3), size[2])
    ru = (g['lang'] or '') == 'ru'
    grams = g['g']
    b = dict(id=g['id'], country=g['country'], kind=g['kind'], shape=shape, size=size, name=g['name'], tpl=g['tpl'], font=g['font'], emb=g['emb'], sub=g['sub'],
             food=g.get('food', k[6]), col=g['col'], style=g['style'], gen=True,
             ru=f"{g.get('name_ru', k[0])} «{g['rub']}»", en=f"{g['enb']} {k[1]}", kg=round(grams / 1000 + (.03 if shape in ('can', 'cup') else .008), 3), kcal=round(grams * k[2] / 100 / 10) * 10,
             thirst=g.get('thirst', k[3]), eat=k[4], lang='ru' if ru else 'en', net=f'{grams} г' if ru else f'{grams} g',
             ingr=k[7] if ru else k[8], maker=g['rub'] if ru else g['enb'] + ', ' + COUNTRY[g['country']][1].replace('worldwide', 'Intl.'))
    b['var'] = [(s, r, e, lab, _pal(g, c)) for s, r, e, lab, c in g['var']]
    return b


for h in HEAD:
    h.setdefault('kind', {'Leiz': 'crisps', 'Pringlz': 'crisps', 'Estrelya': 'crisps', 'Leningrad': 'crisps', 'Chitoz': 'puffs', 'Kiryushki': 'croutons', 'Korochki': 'croutons',
                          'Khrustam': 'croutons', 'Martyn': 'seeds', 'Oreshkin': 'nuts', 'Starpom': 'seafood', 'Kinozal': 'popcorn', 'Yozhik': 'sweetsticks'}[h['id']])
import catalog_more
GEN2 = catalog_more.rows(G)
BRANDS = HEAD + [_expand(g) for g in GEN + GEN2]

# ---- 0.5: product lines and rare seasonal packs
import copy, catalog_series as CS
_BY = {b['id']: b for b in BRANDS}
def _vpal(b, col):
    if b.get('gen'): return _pal(b, col)
    return (col, '#ffffff', '#ffd43b', _hex(_mix(col, '#000000', .62)))
RARE = {}
import catalog_flavours as CF
for bid, suf, ru, en, lab, col, season in CS.LIMITED + CF.MORE_LIMITED:
    b = _BY[bid]; b['var'] = list(b['var']) + [(suf, ru, en, lab, _vpal(b, col))]; RARE[bid + suf] = season
for par, suf, kind, line, ru, en, grams, jr, je, var, o in CS.SERIES:
    p = _BY[par]; k = KIND[kind]; b = copy.deepcopy(p)
    shape = o.get('shape', p['shape']); rus = p['lang'] == 'ru'
    b.update(id=par + suf, parent=par, layout=p.get('layout', par), kind=kind, series=line, shape=shape, ru=ru, en=en, food=o.get('food', k[6]), baked=o.get('baked', False), joke=(jr, je),
             kg=round(grams / 1000 + (.03 if shape in ('can', 'cup', 'tube') else .008), 3), kcal=round(grams * k[2] * (.82 if o.get('baked') else 1) / 100 / 10) * 10,
             thirst=k[3], eat=k[4], net=f'{grams} г' if rus else f'{grams} g')
    if 'style' in o: b['style'] = o['style']
    if 'size' in o: b['size'] = o['size']
    elif shape == 'bag' and p['shape'] != 'bag': b['size'] = KIND['crisps'][5][1]
    elif shape == 'bag': b['size'] = (round(p['size'][0] * 1.06, 3), round(p['size'][1] * 1.04, 3), p['size'][2])
    b['var'] = [(s_, r_, e_, l_, _vpal(b, c_)) for s_, r_, e_, l_, c_ in var]
    BRANDS.append(b); _BY[b['id']] = b

OLD_IDS = {b['id'] + v[0] for b in BRANDS for v in b['var']}   # before 0.6
for bid, rows in CF.MORE.items():
    b = _BY[bid]; b['var'] = list(b['var']) + [(s_, r_, e_, l_, _vpal(b, c_)) for s_, r_, e_, l_, c_ in rows]

# 0.7.1: the user asked to drop popcorn from the mod
BRANDS = [b for b in BRANDS if b['kind'] != 'popcorn']

SNACKS = []
for b in BRANDS:
    for v in b['var']:
        SNACKS.append(dict(id=b['id'] + v[0], brand=b, shape=b['shape'], ru=b['ru'], en=b['en'], ruf=v[1], enf=v[2], label=v[3], pal=v[4],
                           pack='sachet' if b['id'] == 'Starpom' else b['shape'], country=COUNTRY[b['country']],
                           kg=b['kg'], kcal=b['kcal'], thirst=b['thirst'], eat=b['eat'], lang=b['lang'], rare=RARE.get(b['id'] + v[0])))

if __name__ == '__main__':
    import collections
    ids = [s['id'] for s in SNACKS]
    assert len(ids) == len(set(ids)), [i for i, n in collections.Counter(ids).items() if n > 1]
    print(len(BRANDS), 'brands', len(SNACKS), 'kinds')
    for c, n in collections.Counter(b['country'] for b in BRANDS).items(): print(c, n, sum(len(b['var']) for b in BRANDS if b['country'] == c))

"""Every kind of instant noodles the mod has, in one place: the label painter, the mesh builder, the icon renderer and the C# catalogue read this.
BRANDS -> NOODLES (flat list of dicts). Stats: kg = dry weight; water = litres to brew; kcal; thirst = the game's m_ReduceThirst for the brewed one
(positive quenches); warm = warmth, 1 = the game's tea; spicy 0..4."""
import zlib

# metres: width x height (lying) x puffed thickness of the pillow packs
SIZES = {
    'brick': (.115, .165, .035),
    'kpack': (.140, .150, .045),   # Korean: nearly square, a round cake inside
    'jpack': (.125, .160, .030),
    'mini': (.095, .125, .028),
    'long': (.070, .250, .026),    # straight stick noodles
    'big': (.150, .200, .048),
    # snack shapes kept for the shared tools
    'bag': (.19, .27, .07), 'smallbag': (.12, .17, .04), 'bigbag': (.22, .34, .09),
}
# cup-like: bottom radius, top radius, height
CUPS = {
    'cup': (.033, .045, .105), 'minicup': (.030, .041, .075), 'tallcup': (.031, .043, .128), 'bigcup': (.040, .055, .120),
    'bowl': (.050, .070, .075), 'kbowl': (.052, .082, .085), 'bucket': (.050, .068, .115), 'ufo': (.072, .090, .048),
}
# box-like: width, depth, height
TRAYS = {'tray': (.150, .115, .060), 'box': (.165, .165, .045)}

# dry kg, water L, kcal
BASE = {
    'brick': (.085, .40, 390), 'kpack': (.120, .55, 510), 'jpack': (.100, .50, 440), 'mini': (.060, .35, 280), 'long': (.090, .45, 330), 'big': (.130, .55, 560),
    'cup': (.070, .30, 330), 'minicup': (.040, .20, 180), 'tallcup': (.075, .35, 340), 'bigcup': (.100, .50, 470),
    'bowl': (.100, .45, 430), 'kbowl': (.110, .50, 480), 'bucket': (.110, .50, 490), 'ufo': (.120, .50, 540), 'tray': (.090, .50, 430), 'box': (.130, .50, 560),
}
WHERE = {
    'cup': (' в стакане', ' cup'), 'minicup': (' в стаканчике', ' mini cup'), 'tallcup': (' в стакане', ' cup'), 'bigcup': (' в большом стакане', ' big cup'),
    'bowl': (' в миске', ' bowl'), 'kbowl': (' в большой миске', ' big bowl'), 'bucket': (' в ведёрке', ' bucket'), 'ufo': (' в круглой коробке', ' round box'),
    'tray': (' в лотке', ' tray'), 'box': (' в коробке', ' box'),
}

# broth colours
CH, BF, RED, FIRE, MISO, SHOYU, TONK, SHIO, SEA, CURRY, JJA, TOM, CREAM, CHS, KIM, TY, TYC, PHO, LAKSA, SOY, GRN = (
    '#e8b04a', '#b9692d', '#c4381c', '#9e1b10', '#c98f45', '#8a5a2b', '#eadcc0', '#ecd9a0', '#e3c98a', '#d39a1e', '#2b1a12', '#d2452a', '#f1dcae', '#f2b73a',
    '#d4502a', '#e0662a', '#eb8a4a', '#c9a36a', '#e08a3a', '#8a4a1c', '#a8b85a')


def V(id, vru, ven, ruf, enf, fl, broth, top, spicy=0, **kw):
    return dict(id=id, vru=vru, ven=ven, ruf=ruf, enf=enf, fl=fl, broth=broth, top=top, spicy=spicy, **kw)


B = []


def brand(key, ru, en, lang, tpl, shape, nat, sub, c1, c2, ac, variants, **kw):
    B.append(dict(key=key, ru=ru, en=en, lang=lang, tpl=tpl, shape=shape, nat=nat, sub=sub, c1=c1, c2=c2, ac=ac, variants=variants, **kw))


# ------------------------------------------------------------------ Korea
brand('Buldak', 'Больдак', 'Burndak', 'ko', 'fire', 'kpack', '볼닭볶음면', 'Samjan', '#1c1c1c', '#000000', '#ffd400', [
    V('FireChicken', '', 'Original', 'та самая из челленджей: первая вилка вкусно, вторая больно, третья уже снимается на видео', 'the one from the challenges: first bite tasty, second bite painful, third bite goes on video', 'HOT CHICKEN · ORIGINAL', FIRE, ('chili', 'scallion', 'sesame', 'nori'), 3, dry=1, nat2='오리지널', c1='#1c1c1c', c2='#000000', ac='#ffd400'),
    V('Buldak2x', '2× Острый', '2x Spicy', 'для тех, кому оригинал показался детским питанием. Курица на пачке плачет не от счастья', 'for those who found the original to be baby food. The chicken on the pack is not crying with joy', '2X SPICY · EXTREMELY HOT', '#7f0d08', ('chili', 'scallion', 'sesame'), 4, dry=1, nat2='핵불닭', c1='#e02020', c2='#8e0000', ac='#ffe600'),
    V('BuldakCarbo', 'Карбонара', 'Carbonara', 'розовая и милая, как котёнок. Котёнок с огнемётом', 'pink and cute as a kitten. A kitten with a flamethrower', 'CARBONARA · HOT CHICKEN', '#e8b98a', ('chili', 'scallion', 'cheese'), 2, dry=1, nat2='까르보', c1='#f9c6d8', c2='#f48fb1', ac='#ffe14d'),
    V('BuldakCheese', 'Сырный', 'Cheese', 'сыр честно пытается потушить пожар. Пожар побеждает', 'the cheese honestly tries to put the fire out. The fire wins', 'CHEESE · HOT CHICKEN', '#e09a2a', ('cheese', 'chili', 'scallion'), 2, dry=1, nat2='치즈', c1='#ffd21f', c2='#f5b400', ac='#00a4a6'),
    V('BuldakJjajang', 'Чаджан', 'Jjajang', 'чёрная, как твоё будущее после второй пачки', 'black as your future after the second pack', 'JJAJANG · BLACK BEAN', JJA, ('scallion', 'chili', 'meat'), 2, dry=1, nat2='짜장', c1='#c6dd3c', c2='#9ab81e', ac='#2e7d32'),
    V('BuldakCurry', 'Карри', 'Curry', 'карри и огненная курица: когда одного ожога мало, берут два разных', 'curry meets fire chicken: when one burn is not enough, take two different ones', 'CURRY · HOT CHICKEN', CURRY, ('chili', 'scallion', 'corn'), 2, dry=1, nat2='커리', c1='#ffb300', c2='#f57c00', ac='#6a1b9a'),
    V('BuldakRose', 'Розе', 'Rose', 'томат со сливками. Звучит нежно, но это ловушка', 'tomato with cream. Sounds gentle, but it is a trap', 'ROSE · TOMATO CREAM', '#e0705a', ('chili', 'scallion', 'cheese'), 2, dry=1, nat2='로제', c1='#ff8a80', c2='#e53935', ac='#fff3e0'),
    V('BuldakKimchi', 'Кимчи', 'Kimchi', 'кимчи добавили, чтобы было чем закусить боль. Не помогло', 'kimchi was added to take the edge off the pain. It did not', 'KIMCHI · HOT CHICKEN', KIM, ('kimchi', 'scallion', 'sesame'), 3, dry=1, nat2='김치', c1='#e64a19', c2='#b71c1c', ac='#c6ff00'),
    V('BuldakStew', 'Суп-рагу', 'Stew', 'тот же пожар, только теперь его можно пить', 'the same fire, only now you can drink it', 'STEW TYPE · WITH SOUP', '#b3200f', ('chili', 'scallion', 'egg'), 3, nat2='불닭탕면', c1='#d32f2f', c2='#1a0000', ac='#ffffff'),
    V('BuldakHabanero', 'Хабанеро-лайм', 'Habanero Lime', 'лайм освежает ровно полсекунды, дальше работает хабанеро', 'the lime refreshes for exactly half a second, then the habanero takes over', 'HABANERO LIME', '#c4571c', ('chili', 'scallion', 'corn'), 3, dry=1, nat2='하바네로라임', c1='#6a2c91', c2='#3a1259', ac='#b6e034'),
    V('BuldakCream', 'Сливочная карбонара', 'Cream Carbonara', 'самая мягкая в линейке. Это как самый добрый медведь на Великом Медведе', 'the mildest of the line. Like the kindest bear on Great Bear Island', 'CREAM CARBONARA', CREAM, ('cheese', 'scallion', 'corn'), 1, dry=1, nat2='크림까르보', c1='#fde4ec', c2='#c8f0e0', ac='#f06292'),
    V('BuldakQuattro', 'Четыре сыра', 'Quattro Cheese', 'четыре сыра против одной курицы. Курица всё равно выигрывает', 'four cheeses against one chicken. The chicken still wins', 'QUATTRO CHEESE', '#e8a83a', ('cheese', 'chili', 'scallion'), 2, dry=1, nat2='콰트로치즈', c1='#f7a21b', c2='#ef6c00', ac='#fff59d'),
    V('BuldakYakisoba', 'Якисоба', 'Yakisoba', 'японский соус пришёл с миром. Его никто не спросил', 'the Japanese sauce came in peace. Nobody asked it', 'YAKISOBA · HOT CHICKEN', '#6d3a16', ('scallion', 'nori', 'chili'), 2, dry=1, nat2='야끼소바', c1='#6d4c41', c2='#3e2723', ac='#ffcc80'),
    V('BuldakCorn', 'Кукуруза', 'Corn', 'сладкая кукуруза для отвода глаз. Глаза всё равно слезятся', 'sweet corn as a distraction. Your eyes water anyway', 'CORN · HOT CHICKEN', '#e0a52a', ('corn', 'chili', 'scallion'), 1, dry=1, nat2='콘불닭', c1='#fff176', c2='#c0ca33', ac='#2e7d32'),
    V('BuldakMala', 'Мала', 'Mala', 'сначала горит рот, потом он немеет, и гореть уже нечему. Забота о клиенте', 'first your mouth burns, then it goes numb and there is nothing left to burn. Customer care', 'MALA · SICHUAN PEPPER', '#8a1410', ('chili', 'scallion', 'sesame', 'mushroom'), 4, dry=1, nat2='마라', c1='#ad1457', c2='#4a0a2a', ac='#ffd600'),
    V('BuldakCup', '', 'Original', 'оригинальная, но в стакане: бежать с ним к воде удобнее', 'the original, but in a cup: easier to carry while you run for water', 'ORIGINAL', FIRE, ('chili', 'scallion', 'sesame', 'nori'), 3, dry=1, shape='bigcup', nat2='오리지널', c1='#1c1c1c', c2='#000000', ac='#ffd400'),
    V('Buldak2xCup', '2× Острый', '2x Spicy', 'вдвое острее, в стакане. Стакан одноразовый, воспоминания навсегда', 'twice as hot, in a cup. The cup is disposable, the memories are forever', '2X SPICY', '#7f0d08', ('chili', 'scallion', 'sesame'), 4, dry=1, shape='bigcup', nat2='핵불닭', c1='#e02020', c2='#8e0000', ac='#ffe600'),
    V('BuldakCarboCup', 'Карбонара', 'Carbonara', 'розовый стакан боли. В мороз греет лучше костра, проверять на свой страх и риск', 'a pink cup of pain. Warms better than a campfire in the frost, try at your own risk', 'CARBONARA', '#e8b98a', ('chili', 'scallion', 'cheese'), 2, dry=1, shape='bigcup', nat2='까르보', c1='#f9c6d8', c2='#f48fb1', ac='#ffe14d'),
    V('BuldakCheeseBowl', 'Сырный', 'Cheese', 'большая миска на двоих. Вторым обычно оказывается сожаление', 'a big bowl for two. The second one is usually regret', 'CHEESE', '#e09a2a', ('cheese', 'chili', 'scallion'), 2, dry=1, shape='kbowl', nat2='치즈', c1='#ffd21f', c2='#f5b400', ac='#00a4a6'),
], mascot='chicken')

brand('Shine', 'Шайн Рамён', 'Shine Ramyun', 'ko', 'block', 'kpack', '샤인', 'SHINE RAMYUN', '#e0262e', '#9a0b1c', '#ffd400', [
    V('Shine', '', '', 'острая говядина с грибами', 'spicy beef and mushroom', 'HOT & SPICY', RED, ('mushroom', 'scallion', 'chili'), 2, hand=1, shape='brick', kg=.12, water=.55, kcal=500, thirst=16, warm=1.7, prefix_ru='Острая лапша', en_full='Shine Ramyun hot noodles'),
    V('ShineBlack', 'Блэк', 'Black', 'наваристый костный бульон, говядина, чеснок', 'rich bone broth, beef and garlic', 'BLACK · PREMIUM', '#b5552a', ('mushroom', 'scallion', 'meat', 'egg'), 1, c1='#2a2a2a', c2='#000000', ac='#d4af37', plate='#8a0b1c', nat2='블랙'),
    V('ShineRed', 'Ред', 'Red', 'намного острее обычной', 'much hotter than the regular one', 'RED · SUPER SPICY', '#a81408', ('mushroom', 'scallion', 'chili'), 3, c1='#ff1744', c2='#7f0000', ac='#ffffff', nat2='레드'),
    V('ShineGold', 'Голд', 'Gold', 'куриный бульон с чесноком', 'chicken broth with garlic', 'GOLD · CHICKEN BROTH', '#e0a83a', ('scallion', 'egg', 'chili'), 1, c1='#f0c040', c2='#b8860b', ac='#7a0010', dark=0, plate='#7a0010', nat2='골드'),
    V('ShineStirFry', 'Поккым', 'Stir Fry', 'жареная, с острым соусом, без бульона', 'stir-fried with a spicy sauce, no broth', 'STIR FRY · NO SOUP', '#a8381a', ('mushroom', 'scallion', 'chili'), 2, dry=1, c1='#bf360c', c2='#3e1005', ac='#ffd400', nat2='볶음면'),
    V('ShineCup', '', '', 'острая говядина с грибами', 'spicy beef and mushroom', 'HOT & SPICY', RED, ('mushroom', 'scallion', 'chili'), 2, shape='tallcup'),
    V('ShineBowl', '', '', 'острая говядина с грибами', 'spicy beef and mushroom', 'HOT & SPICY', RED, ('mushroom', 'scallion', 'chili'), 2, shape='kbowl'),
])
brand('Jin', 'Джин Рамён', 'Jin Ramyun', 'ko', 'band', 'kpack', '진라면', 'OTTOGGI · JIN RAMYUN', '#ffd740', '#ffab00', '#1565c0', [
    V('JinMild', 'Мягкий', 'Mild', 'говяжий бульон, совсем не острый', 'beef broth, not spicy at all', '순한맛 · MILD', '#c98a4a', ('scallion', 'egg', 'meat'), 0, ac='#1565c0'),
    V('JinSpicy', 'Острый', 'Spicy', 'говяжий бульон с перцем', 'beef broth with pepper', '매운맛 · SPICY', RED, ('scallion', 'chili', 'meat'), 2, ac='#d50000'),
    V('JinCup', 'Острый', 'Spicy', 'говяжий бульон с перцем', 'beef broth with pepper', '매운맛 · SPICY', RED, ('scallion', 'chili', 'meat'), 2, ac='#d50000', shape='tallcup'),
], dark=0)
brand('Neoguri', 'Ноугури', 'Nouguri', 'ko', 'arc', 'kpack', '노구리', 'NOUGURI · UDON', '#e65100', '#bf360c', '#fff3e0', [
    V('NeoguriSpicy', 'Острый', 'Spicy', 'толстая лапша удон, морепродукты и ламинария', 'thick udon noodles, seafood and kelp', '얼큰한맛 · SPICY SEAFOOD', '#c9461e', ('shrimp', 'nori', 'scallion', 'chili'), 2, thick=1),
    V('NeoguriMild', 'Мягкий', 'Mild', 'толстая лапша удон, морепродукты и ламинария', 'thick udon noodles, seafood and kelp', '순한맛 · MILD SEAFOOD', SEA, ('shrimp', 'nori', 'scallion'), 0, thick=1, c1='#f9a825', c2='#ef6c00'),
], mascot='raccoon')
brand('Chapa', 'Чапагэти', 'Chapagetti', 'ko', 'split', 'kpack', '짜파게띠', 'CHAPAGETTI', '#33691e', '#1b3a0e', '#ffd600', [
    V('Chapagetti', '', '', 'лапша в соусе из чёрных бобов, без бульона', 'noodles in black bean sauce, no broth', 'BLACK BEAN SAUCE · 짜장', JJA, ('scallion', 'meat', 'corn'), 0, dry=1),
    V('ChapaSichuan', 'Сачхон', 'Sichuan', 'острый соус из чёрных бобов, без бульона', 'spicy black bean sauce, no broth', '사천 · SPICY BLACK BEAN', '#3a160e', ('scallion', 'meat', 'chili'), 2, dry=1, c1='#b71c1c', c2='#4a0a0a'),
])
brand('Ansung', 'Ансон Мён', 'Ansung Myun', 'ko', 'stripe', 'kpack', '안송탕면', 'ANSUNG TANGMYUN', '#ff8f00', '#e65100', '#ffffff', [
    V('Ansung', '', '', 'бульон на соевой пасте, мягкий вкус', 'soybean paste broth, a mild taste', 'SOYBEAN PASTE BROTH', MISO, ('scallion', 'mushroom', 'egg'), 1),
])
brand('SamjanR', 'Самджан Рамён', 'Samjan Ramyun', 'ko', 'band', 'kpack', '삼잔라면', 'SAMJAN · SINCE 1963', '#ff9800', '#ef6c00', '#b71c1c', [
    V('SamjanOriginal', '', 'Original', 'первая корейская лапша: бульон с ветчиной', 'the first Korean ramyun: ham broth', 'ORIGINAL · HAM FLAVOUR', '#d9772e', ('scallion', 'meat', 'egg'), 1),
    V('SamjanCheese', 'Сырный', 'Cheese', 'бульон с ветчиной и сыром', 'ham broth with cheese', 'CHEESE', '#e8a23a', ('cheese', 'scallion', 'meat'), 1, c1='#ffe082', c2='#ffb300'),
], dark=0)
brand('Palto', 'Пальто', 'Palto', 'ko', 'split', 'kpack', '팔토', 'PALTO', '#1565c0', '#0d2f66', '#ff5252', [
    V('PaltoBibim', 'Бибим Мён', 'Bibim Myun', 'холодная лапша в кисло-сладком остром соусе, без бульона', 'cold noodles in a sweet-and-sour hot sauce, no broth', '비빔면 · SWEET & SPICY', '#c4321e', ('egg', 'scallion', 'sesame', 'kimchi'), 2, dry=1),
    V('PaltoKoko', 'Коко Мён', 'Kokomyun', 'прозрачный куриный бульон с зелёным перцем', 'clear chicken broth with green chilli', '꼬꼬면 · WHITE CHICKEN BROTH', '#ecdcae', ('scallion', 'egg', 'chili'), 2, c1='#fffde7', c2='#fff59d', ac='#2e7d32', dark=0),
    V('PaltoKing', 'Ван Ттуккон', 'King Lid', 'большой стакан с широкой крышкой, острая говядина', 'a big cup with a wide lid, spicy beef', '왕뚜껑 · KING LID', RED, ('scallion', 'chili', 'meat', 'egg'), 2, shape='kbowl', c1='#6a1b9a', c2='#311b92', ac='#ffd600'),
])
brand('Yukgae', 'Юккэджан', 'Yukgaejang', 'ko', 'block', 'bowl', '육개장', 'YUKGAEJANG BOWL', '#c62828', '#7f0000', '#ffffff', [
    V('Yukgae', '', '', 'острый говяжий суп, тонкая лапша', 'spicy beef soup with thin noodles', 'SPICY BEEF SOUP', RED, ('scallion', 'egg', 'meat', 'narutomaki'), 2),
])
brand('KimchiM', 'Чонга Кимчи Мён', 'Chonga Kimchi Myun', 'ko', 'stripe', 'kpack', '김치면', 'CHONGA KIMCHI MYUN', '#d84315', '#8a2206', '#fff8e1', [
    V('KimchiMyun', '', '', 'бульон с квашеной капустой кимчи', 'broth with fermented kimchi cabbage', 'KIMCHI RAMYUN', KIM, ('kimchi', 'scallion', 'chili'), 2),
    V('KimchiCup', '', '', 'бульон с квашеной капустой кимчи', 'broth with fermented kimchi cabbage', 'KIMCHI RAMYUN', KIM, ('kimchi', 'scallion', 'chili'), 2, shape='cup'),
])
brand('Gomtang', 'Сари Комтан', 'Sari Gomtang', 'ko', 'kraft', 'kpack', '곰탕면', 'SARI GOMTANG', '#f5f0e1', '#e0d6bd', '#1a237e', [
    V('Gomtang', '', '', 'молочно-белый бульон на говяжьих костях', 'milky white beef bone broth', 'BEEF BONE BROTH', TONK, ('scallion', 'meat', 'egg'), 0),
], dark=0)
brand('Jjamppong', 'Чин Чамппон', 'Jin Jjamppong', 'ko', 'block', 'kpack', '진짬뽕', 'JIN JJAMPPONG', '#b71c1c', '#3e0a0a', '#ffab00', [
    V('Jjamppong', '', '', 'острый суп с морепродуктами и дымком', 'a spicy seafood soup with a smoky note', 'SPICY SEAFOOD', '#c2321a', ('shrimp', 'mushroom', 'scallion', 'chili'), 3, thick=1),
])
brand('Teumsae', 'Тхымсэ', 'Teumsae', 'ko', 'block', 'kpack', '틈새라면', 'TEUMSAE RAMYUN', '#212121', '#000000', '#ff1744', [
    V('Teumsae', '', '', 'одна из самых острых: красный бульон-огонь', 'one of the hottest: a fiery red broth', 'DANGEROUSLY HOT', '#9c1208', ('chili', 'scallion', 'egg'), 4),
], plate='#b71c1c')
brand('Yeul', 'Ёль Рамён', 'Yeul Ramyun', 'ko', 'band', 'kpack', '열라면', 'YEUL RAMYUN', '#d50000', '#8e0000', '#ffea00', [
    V('Yeul', '', '', 'жгучий говяжий бульон', 'a burning beef broth', 'EXTRA HOT', '#b01c0c', ('chili', 'scallion', 'meat'), 3),
])
brand('Kalguksu', 'Мёльчи Кальгуксу', 'Myeolchi Kalguksu', 'ko', 'kraft', 'kpack', '멸치칼국수', 'ANCHOVY KALGUKSU', '#e8f5e9', '#a5d6a7', '#1b5e20', [
    V('Kalguksu', '', '', 'плоская лапша в бульоне из анчоусов, не острая', 'flat noodles in anchovy broth, not spicy', 'ANCHOVY BROTH · NOT FRIED', SHIO, ('scallion', 'nori', 'egg'), 0, thick=1),
], dark=0)
brand('Sesame', 'Чхамккэ Рамён', 'Chamkkae Ramyun', 'ko', 'band', 'bigcup', '참깨라면', 'SESAME RAMYUN', '#fff3c4', '#ffd54f', '#5d4037', [
    V('SesameCup', '', '', 'кунжут и яичные хлопья в остром бульоне', 'sesame and egg flakes in a spicy broth', 'SESAME & EGG', '#d8642a', ('egg', 'sesame', 'scallion', 'chili'), 2),
    V('SesamePack', '', '', 'кунжут и яичные хлопья в остром бульоне', 'sesame and egg flakes in a spicy broth', 'SESAME & EGG', '#d8642a', ('egg', 'sesame', 'scallion', 'chili'), 2, shape='kpack'),
], dark=0)

# ------------------------------------------------------------------ Japan
brand('Chikin', 'Ниссун Чикин Рамен', 'Nissun Chikin Ramen', 'ja', 'jp', 'jpack', 'チキンラーメン', 'Chikin Ramen', '#f6a21d', '#e47a00', '#e2001a', [
    V('Chikin', '', '', 'классическая куриная', 'the classic chicken', 'SINCE 1958', '#d9a441', ('egg', 'scallion'), 0, hand=1, shape='brick', kg=.085, water=.45, kcal=380, thirst=22, warm=1.0, en_full='Nissun Chikin Ramen'),
    V('ChikinDon', '', '', 'классическая куриная, с сушёным яйцом', 'the classic chicken, with dried egg', 'どんぶり · WITH EGG', '#d9a441', ('egg', 'scallion', 'narutomaki'), 0, shape='bowl'),
], dark=0)
brand('Kapp', 'Капп Нудл', 'Kapp Noodle', 'ja', 'cupA', 'cup', 'カップヌードル', 'KAPP NOODLE', '#ffffff', '#eef3f5', '#e60012', [
    V('KappNoodle', 'Морепродукты', 'Seafood', 'морепродукты', 'seafood', 'SEAFOOD', SEA, ('shrimp', 'corn', 'scallion'), 0, hand=1, kg=.07, water=.3, kcal=330, thirst=20, warm=1.0, ru_full='Лапша «Капп Нудл» в стакане', en_full='Kapp Noodle cup'),
    V('KappSoy', 'Оригинальный', 'Original', 'соевый бульон, креветки, яйцо и мясные кубики', 'soy broth with shrimp, egg and meat cubes', 'ORIGINAL', SHOYU, ('shrimp', 'egg', 'meat', 'scallion'), 0, band='#e60012'),
    V('KappCurry', 'Карри', 'Curry', 'густое японское карри', 'thick Japanese curry', 'CURRY', CURRY, ('meat', 'corn', 'scallion'), 1, band='#ef6c00', ac='#ef6c00'),
    V('KappChili', 'Чили-томат', 'Chili Tomato', 'томатный бульон с перцем', 'tomato broth with chilli', 'CHILI TOMATO', TOM, ('meat', 'corn', 'chili', 'scallion'), 2, band='#c62828', ac='#c62828'),
    V('KappMiso', 'Мисо', 'Miso', 'бульон на мисо с имбирём', 'miso broth with ginger', 'MISO', MISO, ('meat', 'corn', 'scallion'), 0, band='#8d5a1e', ac='#8d5a1e'),
    V('KappCheese', 'Сырное карри', 'Cheese Curry', 'карри с расплавленным сыром', 'curry with melted cheese', 'CHEESE CURRY', '#e0a428', ('cheese', 'meat', 'corn'), 1, band='#fbc02d', ac='#f57f17'),
    V('KappBig', 'Кинг', 'King', 'двойная порция в соевом бульоне', 'a double helping in soy broth', 'KING SIZE · ORIGINAL', SHOYU, ('shrimp', 'egg', 'meat', 'scallion'), 0, shape='bigcup', band='#e60012'),
], dark=0)
brand('Sappuro', 'Саппуро Итибан', 'Sappuro Ichiban', 'ja', 'tate', 'jpack', 'サップロ一番', 'SAPPURO ICHIBAN', '#fff8e1', '#ffecb3', '#c62828', [
    V('SappuroShoyu', 'Сёю', 'Shoyu', 'соевый бульон', 'soy sauce broth', 'しょうゆ味', SHOYU, ('narutomaki', 'scallion', 'nori', 'egg'), 0, ac='#c62828'),
    V('SappuroMiso', 'Мисо', 'Miso', 'бульон на мисо с овощами', 'miso broth with vegetables', 'みそラーメン', MISO, ('corn', 'scallion', 'meat'), 0, ac='#ef6c00', c1='#fff3e0', c2='#ffcc80'),
    V('SappuroShio', 'Сио', 'Shio', 'светлый солёный бульон с кунжутом', 'a light salt broth with sesame', '塩らーめん', SHIO, ('sesame', 'scallion', 'egg', 'corn'), 0, ac='#00838f', c1='#e0f7fa', c2='#b2ebf2'),
    V('SappuroTonkotsu', 'Тонкоцу', 'Tonkotsu', 'густой свиной бульон', 'a thick pork bone broth', 'とんこつ', TONK, ('chashu', 'scallion', 'nori', 'sesame'), 0, ac='#ad1457', c1='#fce4ec', c2='#f8bbd0'),
    V('SappuroYakisoba', 'Якисоба', 'Yakisoba', 'жареная лапша в соусе, без бульона', 'fried noodles in sauce, no broth', 'ソースやきそば', SOY, ('nori', 'scallion', 'meat'), 0, dry=1, ac='#4e342e', c1='#efebe9', c2='#d7ccc8'),
    V('SappuroMisoCup', 'Мисо', 'Miso', 'бульон на мисо с овощами', 'miso broth with vegetables', 'みそラーメン', MISO, ('corn', 'scallion', 'meat'), 0, ac='#ef6c00', c1='#fff3e0', c2='#ffcc80', shape='bowl'),
], dark=0)
brand('Marutyan', 'Марутян', 'Maruchyan', 'ja', 'cupC', 'bowl', 'マルチャン', 'MARUCHYAN', '#d7261e', '#a3160f', '#ffffff', [
    V('AkaiKitsune', 'Акай Кицунэ', 'Akai Kitsune', 'удон с жареным тофу', 'udon with fried tofu', 'UDON', '#c99a4a', ('tofu', 'scallion'), 0, hand=1, kg=.1, water=.45, kcal=440, thirst=24, warm=1.2, ru_full='Удон «Акай Кицунэ»', en_full='Akai Kitsune udon'),
    V('MidoriTanuki', 'Мидори-но Тануки', 'Midori no Tanuki', 'гречневая соба с темпурой', 'buckwheat soba with tempura', '緑のたぬき · SOBA', '#7a4a26', ('tempura', 'scallion', 'narutomaki'), 0, c1='#2e7d32', c2='#1b5e20', mascot='raccoon', soba=1),
    V('KuroiButa', 'Курой Бута Карри', 'Kuroi Buta Curry', 'удон в карри со свининой', 'curry udon with pork', '黒い豚カレー · UDON', '#a8741c', ('chashu', 'scallion', 'corn'), 1, c1='#212121', c2='#000000', ac='#ffca28', mascot='pig', thick=1),
    V('SeimenShoyu', 'Сэймэн Сёю', 'Seimen Shoyu', 'лапша как свежая, соевый бульон', 'noodles like fresh ones, soy broth', '正麺 醤油味', SHOYU, ('chashu', 'scallion', 'nori', 'egg'), 0, shape='jpack', tpl='tate', c1='#fffde7', c2='#fff9c4', ac='#b71c1c', dark=0, nat='正麺'),
    V('SeimenMiso', 'Сэймэн Мисо', 'Seimen Miso', 'лапша как свежая, бульон на мисо', 'noodles like fresh ones, miso broth', '正麺 味噌味', MISO, ('chashu', 'corn', 'scallion'), 0, shape='jpack', tpl='tate', c1='#fff3e0', c2='#ffe0b2', ac='#e65100', dark=0, nat='正麺'),
], mascot='fox')
brand('Donbei', 'Домбэй', 'Dombei', 'ja', 'cupB', 'bowl', 'どんべい', 'DOMBEI', '#f9a825', '#f57f17', '#1b5e20', [
    V('DonbeiKitsune', 'Кицунэ Удон', 'Kitsune Udon', 'толстый удон с большим куском жареного тофу', 'thick udon with a big piece of fried tofu', 'きつねうどん', '#c9a052', ('tofu', 'scallion', 'narutomaki'), 0, thick=1),
    V('DonbeiSoba', 'Тэмпура Соба', 'Tempura Soba', 'гречневая соба с хрустящей темпурой', 'buckwheat soba with crisp tempura', '天ぷらそば', '#7a4a26', ('tempura', 'scallion'), 0, c1='#00695c', c2='#004d40', ac='#ffecb3', soba=1),
], dark=0)
brand('UFO', 'Ю.Ф.О.', 'U.F.O.', 'ja', 'cupA', 'ufo', '焼そば', 'U.F.O. YAKISOBA', '#111111', '#000000', '#ffea00', [
    V('UFO', 'Якисоба', 'Yakisoba', 'жареная лапша в густом соусе, без бульона', 'fried noodles in a thick sauce, no broth', 'ソース焼そば', SOY, ('nori', 'meat', 'scallion'), 0, dry=1, band='#e60012'),
    V('UFOShio', 'Сио-якисоба', 'Shio Yakisoba', 'жареная лапша с солью и лимоном, без бульона', 'fried noodles with salt and lemon, no broth', '塩焼そば', '#d8c890', ('nori', 'shrimp', 'scallion', 'sesame'), 0, dry=1, band='#00acc1', c1='#e0f7fa', c2='#80deea', ac='#006064', dark=0),
])
brand('Peyangu', 'Пеянгу', 'Peyangu', 'ja', 'lid', 'box', 'ペヤングー', 'PEYANGU', '#ffffff', '#f5f5f5', '#c62828', [
    V('Peyangu', 'Якисоба', 'Yakisoba', 'жареная лапша с соусом, без бульона', 'fried noodles with sauce, no broth', 'ソースやきそば', SOY, ('nori', 'meat', 'scallion', 'sesame'), 0, dry=1),
    V('PeyanguHell', 'Гоку-гэки-кара', 'Goku Geki Kara', 'адски острая якисоба, без бульона', 'hellishly hot yakisoba, no broth', '獄激辛やきそば', '#6a1208', ('chili', 'nori', 'scallion'), 4, dry=1, c1='#b71c1c', c2='#1a0000', ac='#ffea00', dark=1),
    V('Yomise', 'Ёмисэ-но Якисоба', 'Yomise no Yakisoba', 'якисоба с ярмарки, с горчичным майонезом, без бульона', 'festival-stall yakisoba with mustard mayo, no broth', '夜店の焼そば', SOY, ('nori', 'egg', 'scallion', 'meat'), 0, dry=1, c1='#1a237e', c2='#0d1442', ac='#ffca28', dark=1, nat='一平ちゃん', brand_ru='Иппэй-тян', brand_en='Ippei-chan', sub='IPPEI-CHAN'),
], dark=0)
brand('Demae', 'Демаэ Иттё', 'Demae Itcho', 'ja', 'jp', 'jpack', '出前一兆', 'Demae Itcho', '#d32f2f', '#b71c1c', '#ffd600', [
    V('DemaeSesame', 'Кунжутное масло', 'Sesame Oil', 'соевый бульон с кунжутным маслом', 'soy broth with sesame oil', 'ごまラー油', SHOYU, ('scallion', 'egg', 'sesame', 'narutomaki'), 0),
    V('DemaeTonkotsu', 'Тонкоцу', 'Tonkotsu', 'свиной бульон', 'pork bone broth', 'とんこつ', TONK, ('chashu', 'scallion', 'sesame'), 0, c1='#f8bbd0', c2='#f06292', ac='#880e4f'),
    V('DemaeMayu', 'Чёрный чеснок', 'Black Garlic', 'свиной бульон с маслом жжёного чеснока', 'pork broth with burnt garlic oil', '黒マー油とんこつ', '#b8a488', ('chashu', 'scallion', 'nori'), 1, c1='#37474f', c2='#101518', ac='#ffd600'),
    V('DemaeSeafood', 'Морепродукты', 'Seafood', 'бульон с морепродуктами', 'seafood broth', 'シーフード', SEA, ('shrimp', 'scallion', 'narutomaki'), 0, c1='#1976d2', c2='#0d47a1', ac='#ffffff'),
    V('DemaeSpicy', 'Острый', 'Spicy', 'острый соевый бульон', 'a spicy soy broth', '辛口 · SPICY', RED, ('chili', 'scallion', 'egg'), 2, c1='#ff6f00', c2='#bf360c', ac='#ffffff'),
])
brand('Charumera', 'Тярумэра', 'Charumera', 'ja', 'stripe', 'jpack', 'チャルメーラ', 'CHARUMERA', '#ff7043', '#e64a19', '#fff8e1', [
    V('CharumeraShoyu', 'Сёю', 'Shoyu', 'соевый бульон как в уличной лапшичной', 'soy broth as at a street noodle cart', 'しょうゆ', SHOYU, ('narutomaki', 'scallion', 'nori'), 0),
    V('CharumeraMiso', 'Мисо', 'Miso', 'бульон на мисо как в уличной лапшичной', 'miso broth as at a street noodle cart', 'みそ', MISO, ('corn', 'scallion', 'meat'), 0, c1='#ffb300', c2='#ff8f00', ac='#4e342e'),
], mascot='face')
brand('Hakata', 'Хаката Бо-рамен', 'Hakata Bo-Ramen', 'ja', 'tate', 'long', '博多棒ラーメン', 'HAKATA', '#fafafa', '#eeeeee', '#b71c1c', [
    V('HakataTonkotsu', 'Тонкоцу', 'Tonkotsu', 'прямая тонкая лапша, свиной бульон', 'straight thin noodles, pork bone broth', 'とんこつ', TONK, ('chashu', 'scallion', 'sesame', 'nori'), 0),
    V('HakataMayu', 'Кумамото', 'Kumamoto', 'прямая лапша, свиной бульон с чёрным чесноком', 'straight noodles, pork broth with black garlic', '熊本 黒マー油', '#b8a488', ('chashu', 'scallion', 'nori'), 1, c1='#263238', c2='#101518', ac='#ffb300', dark=1),
], dark=0)
brand('Rao', 'Ра-О', 'Ra-O', 'ja', 'cupK', 'bowl', 'ラオー', 'RA-O · PREMIUM', '#1a1a1a', '#000000', '#d4af37', [
    V('RaoMiso', 'Мисо', 'Miso', 'ресторанный рамен: густое мисо', 'restaurant-style ramen: a rich miso', '味噌', MISO, ('chashu', 'corn', 'scallion', 'egg'), 0),
    V('RaoTonkotsu', 'Тонкоцу', 'Tonkotsu', 'ресторанный рамен: свиной бульон', 'restaurant-style ramen: pork bone broth', '豚骨', TONK, ('chashu', 'scallion', 'nori', 'egg'), 0, c1='#4a148c', c2='#1a0033'),
    V('RaoShoyu', 'Сёю', 'Shoyu', 'ресторанный рамен: соевый бульон', 'restaurant-style ramen: soy broth', '醤油', SHOYU, ('chashu', 'narutomaki', 'scallion', 'nori'), 0, c1='#7f0000', c2='#2a0000'),
])
brand('Ichiran', 'Итиран', 'Ichiran', 'ja', 'lid', 'box', '一欄', 'ICHIRAN · TONKOTSU', '#1b1b1b', '#000000', '#c62828', [
    V('Ichiran', 'Тонкоцу', 'Tonkotsu', 'подарочная коробка: свиной бульон с красным соусом', 'a gift box: pork broth with a red sauce', 'とんこつ · 秘伝のたれ', TONK, ('chashu', 'scallion', 'chili'), 1),
])

# ------------------------------------------------------------------ Russia and neighbours
brand('Rolltan', 'Роллтан', 'Rolltan', 'ru', 'band', 'brick', 'РОЛЛТАН', 'ПО-ДОМАШНЕМУ', '#ffd84a', '#f5a300', '#d71920', [
    V('Rolltan', '', '', 'по-домашнему, с курицей', 'home-style chicken', 'С КУРИЦЕЙ', '#e8b04a', ('egg', 'scallion'), 0, hand=1, kg=.085, water=.4, kcal=390, thirst=22, warm=1.0, en_full='Rolltan noodles'),
    V('RolltanBeef', 'Говядина', 'Beef', 'с говядиной', 'beef', 'С ГОВЯДИНОЙ', BF, ('meat', 'scallion'), 0),
    V('RolltanMushroom', 'Грибы', 'Mushroom', 'с грибами', 'mushroom', 'С ГРИБАМИ', '#b58a55', ('mushroom', 'scallion'), 0, ac='#6d4c41'),
    V('RolltanBacon', 'Бекон и сыр', 'Bacon & Cheese', 'со вкусом бекона и сыра', 'bacon and cheese flavour', 'БЕКОН И СЫР', '#e0a040', ('meat', 'cheese', 'scallion'), 0, ac='#bf360c'),
    V('RolltanSpicy', 'Острая курица', 'Hot Chicken', 'с курицей и перцем', 'chicken with pepper', 'ОСТРАЯ КУРИЦА', RED, ('chili', 'egg', 'scallion'), 1, c1='#ff7043', c2='#d84315', ac='#7f0000'),
], dark=0, mascot='chicken')
brand('Mivinka', 'Мивинка', 'Mivinka', 'ru', 'arc', 'mini', 'Мивинка', 'ЛАПША БЫСТРОГО ПРИГОТОВЛЕНИЯ', '#2a63c4', '#0f2d66', '#f68b1f', [
    V('Mivinka', '', '', 'со вкусом говядины', 'beef flavour', 'СО ВКУСОМ ГОВЯДИНЫ', '#b9692d', ('meat', 'scallion'), 0, hand=1, shape='brick', kg=.06, water=.35, kcal=300, thirst=20, warm=1.0, en_full='Mivinka noodles'),
    V('MivinkaChicken', 'Курица', 'Chicken', 'со вкусом курицы', 'chicken flavour', 'СО ВКУСОМ КУРИЦЫ', CH, ('egg', 'scallion'), 0, ac='#fdd835'),
    V('MivinkaShrimp', 'Креветка', 'Shrimp', 'со вкусом креветок', 'shrimp flavour', 'СО ВКУСОМ КРЕВЕТОК', SEA, ('shrimp', 'scallion'), 0, ac='#ff8a80'),
    V('MivinkaSpicy', 'Острая', 'Hot', 'острая, со вкусом говядины', 'hot, beef flavour', 'ОСТРАЯ', RED, ('chili', 'meat', 'scallion'), 1, ac='#e53935'),
    V('MivinkaCheese', 'Сыр и зелень', 'Cheese & Herbs', 'со вкусом сыра и зелени', 'cheese and herbs flavour', 'СЫР И ЗЕЛЕНЬ', '#e8c060', ('cheese', 'scallion'), 0, ac='#8bc34a'),
])
brand('Doshik', 'Дошик', 'Doshik', 'ru', 'lid', 'tray', 'ДОШИК', '도시크 · ЛАПША В ЛОТКЕ', '#e53935', '#b71c1c', '#ffeb3b', [
    V('Doshik', '', '', 'говядина', 'beef', 'ГОВЯДИНА · 90 г', '#b5642c', ('meat', 'scallion'), 0, hand=1, kg=.1, water=.5, kcal=430, thirst=24, warm=1.1, ru_full='Лапша «Дошик» в лотке', en_full='Doshik noodle tray'),
    V('DoshikChicken', 'Курица', 'Chicken', 'курица', 'chicken', 'КУРИЦА · 90 г', CH, ('egg', 'scallion'), 0, c1='#fdd835', c2='#f9a825', ac='#d32f2f', dark=0),
    V('DoshikPork', 'Свинина', 'Pork', 'свинина', 'pork', 'СВИНИНА · 90 г', '#c98050', ('chashu', 'scallion'), 0, c1='#ec407a', c2='#ad1457'),
    V('DoshikMushroom', 'Грибы', 'Mushroom', 'грибы', 'mushroom', 'ГРИБЫ · 90 г', '#b58a55', ('mushroom', 'scallion'), 0, c1='#8d6e63', c2='#4e342e'),
    V('DoshikShrimp', 'Острая креветка', 'Spicy Shrimp', 'острая, с креветкой', 'spicy shrimp', 'ОСТРАЯ КРЕВЕТКА · 90 г', '#d85a2a', ('shrimp', 'chili', 'scallion'), 2, c1='#ff7043', c2='#bf360c'),
    V('DoshikKimchi', 'Кимчи', 'Kimchi', 'острая, с кимчи', 'spicy, with kimchi', 'КИМЧИ · 90 г', KIM, ('kimchi', 'scallion', 'chili'), 2, c1='#43a047', c2='#1b5e20', ac='#ff5252'),
    V('DoshikBeefSpicy', 'Острая говядина', 'Spicy Beef', 'острая говядина', 'spicy beef', 'ОСТРАЯ ГОВЯДИНА · 90 г', RED, ('meat', 'chili', 'scallion'), 2, c1='#212121', c2='#000000', ac='#ff3d00'),
])
brand('Bigg', 'Бигг Ланч', 'Bigg Lanch', 'ru', 'cupB', 'bigcup', 'БИГГ ЛАНЧ', 'БИГГ ЛАНЧ', '#1f7a3a', '#0d4a22', '#ffd200', [
    V('BiggLanch', '', '', 'говядина и грибы', 'beef and mushroom', 'ГОВЯДИНА С ГРИБАМИ', '#a65e2c', ('mushroom', 'meat'), 0, hand=1, kg=.1, water=.5, kcal=480, thirst=24, warm=1.2, ru_full='Лапша «Бигг Ланч» в большом стакане', en_full='Bigg Lanch big cup'),
    V('BiggChicken', 'Курица', 'Chicken', 'курица с зеленью', 'chicken with herbs', 'КУРИЦА С ЗЕЛЕНЬЮ', CH, ('egg', 'scallion', 'corn'), 0, c1='#1565c0', c2='#0d2f66'),
    V('BiggBacon', 'Бекон', 'Bacon', 'бекон с сыром', 'bacon with cheese', 'БЕКОН С СЫРОМ', '#d89040', ('meat', 'cheese', 'scallion'), 0, c1='#c62828', c2='#7f0000'),
    V('BiggPuree', 'Острый говяжий', 'Spicy Beef', 'говядина с перцем', 'beef with pepper', 'ОСТРАЯ ГОВЯДИНА', RED, ('meat', 'chili', 'scallion'), 1, c1='#212121', c2='#000000'),
])
brand('BigBum', 'Биг Бум', 'Big Boom', 'ru', 'band', 'big', 'БИГ БУМ', 'ЛАПША + СОУС', '#e53935', '#8e0000', '#ffeb3b', [
    V('BigBumBeef', 'Говядина', 'Beef', 'с говядиной и томатным соусом', 'beef with a tomato sauce', 'ГОВЯДИНА + ТОМАТНЫЙ СОУС', '#b85a2a', ('meat', 'scallion', 'chili'), 1),
    V('BigBumChicken', 'Курица', 'Chicken', 'с курицей и соусом сальса', 'chicken with a salsa sauce', 'КУРИЦА + СОУС САЛЬСА', '#e0a040', ('egg', 'scallion', 'corn'), 1, c1='#fb8c00', c2='#e65100', ac='#ffffff'),
    V('BigBumPork', 'Свинина', 'Pork', 'со свининой и сырным соусом', 'pork with a cheese sauce', 'СВИНИНА + СЫРНЫЙ СОУС', '#d89848', ('chashu', 'cheese', 'scallion'), 0, c1='#7b1fa2', c2='#4a148c'),
])
brand('Anakom', 'Анакомыч', 'Anakomych', 'ru', 'stripe', 'mini', 'Анакомыч', 'ВЕРМИШЕЛЬ ЗА 3 МИНУТЫ', '#43a047', '#2e7d32', '#fffde7', [
    V('AnakomChicken', 'Курица', 'Chicken', 'тонкая вермишель с курицей', 'thin vermicelli with chicken', 'С КУРИЦЕЙ', CH, ('egg', 'scallion'), 0),
    V('AnakomBeef', 'Говядина', 'Beef', 'тонкая вермишель с говядиной', 'thin vermicelli with beef', 'С ГОВЯДИНОЙ', BF, ('meat', 'scallion'), 0, c1='#e53935', c2='#b71c1c'),
    V('AnakomShashlik', 'Шашлык', 'Shashlik', 'тонкая вермишель со вкусом шашлыка', 'thin vermicelli, shashlik flavour', 'ШАШЛЫК', '#b06028', ('meat', 'scallion', 'chili'), 0, c1='#6d4c41', c2='#3e2723'),
])

# ------------------------------------------------------------------ South-East Asia, China, the rest
brand('Indomay', 'Индомэй', 'Indomay', 'en', 'split', 'brick', 'Indomay', 'MI GORENG', '#1b7e3a', '#0b4d22', '#ffcc00', [
    V('MiGoreng', 'Ми Горенг', 'Mi Goreng', 'сладкий соевый соус, без бульона', 'sweet soy sauce, no broth', 'SWEET SOY · NO SOUP', '#8a4a1c', ('egg', 'scallion', 'chili'), 0, dry=1, hand=1, kg=.085, water=.4, kcal=460, thirst=-6, warm=.6, ru_full='Жареная лапша «Индомэй Ми Горенг»', en_full='Indomay Mi Goreng fried noodles'),
    V('MiGorengRendang', 'Ми Горенг Ренданг', 'Mi Goreng Rendang', 'пряная говядина ренданг, без бульона', 'spiced rendang beef, no broth', 'RENDANG · SPICY BEEF', '#7a3a16', ('meat', 'scallion', 'chili'), 2, dry=1, c1='#6a1b1a', c2='#3b0d0c'),
    V('MiGorengPedas', 'Ми Горенг Педас', 'Mi Goreng Pedas', 'острая жареная лапша, без бульона', 'hot fried noodles, no broth', 'PEDAS · HOT & SPICY', '#9a3414', ('egg', 'chili', 'scallion'), 3, dry=1, c1='#d32f2f', c2='#7f0000'),
    V('MiGorengSatay', 'Ми Горенг Сатэ', 'Mi Goreng Satay', 'арахисовый соус сатэ, без бульона', 'peanut satay sauce, no broth', 'SATAY · PEANUT SAUCE', '#9a6a2a', ('meat', 'scallion', 'sesame'), 1, dry=1, c1='#ef6c00', c2='#bf360c'),
    V('IndomaySoto', 'Сото Ми', 'Soto Mie', 'куриный суп с лаймом и куркумой', 'chicken soup with lime and turmeric', 'SOTO MIE · LIME CHICKEN', '#e0c040', ('egg', 'scallion', 'corn'), 1, c1='#9ccc65', c2='#558b2f', sub='SOTO MIE'),
    V('IndomayKari', 'Кари Аям', 'Kari Ayam', 'куриное карри', 'chicken curry', 'KARI AYAM · CHICKEN CURRY', CURRY, ('egg', 'scallion', 'chili'), 2, c1='#fbc02d', c2='#f57f17', ac='#b71c1c', sub='KARI AYAM'),
])
brand('Sedap', 'Ми Сэдап', 'Mie Sedap', 'en', 'band', 'brick', 'Mie Sedap', 'GORENG', '#e53935', '#b71c1c', '#ffeb3b', [
    V('SedapGoreng', 'Горенг', 'Goreng', 'жареная лапша с хрустящим луком, без бульона', 'fried noodles with crisp shallots, no broth', 'GORENG · CRISPY SHALLOTS', SOY, ('egg', 'scallion', 'tempura'), 1, dry=1),
    V('SedapKorean', 'Корейская острая', 'Korean Spicy', 'острая курица по-корейски, без бульона', 'Korean-style hot chicken, no broth', 'KOREAN SPICY CHICKEN', FIRE, ('chili', 'scallion', 'sesame'), 3, dry=1, c1='#212121', c2='#000000', ac='#ff1744'),
])
brand('Mama', 'Мама', 'Mama', 'en', 'stripe', 'mini', 'MAMA', 'ORIENTAL STYLE', '#fdd835', '#f9a825', '#d32f2f', [
    V('MamaTomYum', 'Том Ям', 'Tom Yum', 'кисло-острый суп с креветками', 'hot and sour shrimp soup', 'SHRIMP TOM YUM', TY, ('shrimp', 'chili', 'scallion', 'mushroom'), 2),
    V('MamaCreamy', 'Том Ям сливочный', 'Creamy Tom Yum', 'кисло-острый суп с кокосовым молоком', 'hot and sour soup with coconut milk', 'SHRIMP CREAMY TOM YUM', TYC, ('shrimp', 'chili', 'scallion', 'mushroom'), 2, c1='#ff8a65', c2='#e64a19', ac='#ffffff'),
    V('MamaPork', 'Свинина', 'Pork', 'рубленая свинина с чесноком', 'minced pork with garlic', 'MINCED PORK', '#c9925a', ('chashu', 'scallion'), 0, c1='#f48fb1', c2='#d81b60', ac='#ffffff'),
    V('MamaGreenCurry', 'Зелёное карри', 'Green Curry', 'тайское зелёное карри', 'Thai green curry', 'GREEN CURRY', GRN, ('chili', 'scallion', 'mushroom'), 2, c1='#7cb342', c2='#33691e', ac='#fffde7'),
    V('MamaCup', 'Том Ям', 'Tom Yum', 'кисло-острый суп с креветками', 'hot and sour shrimp soup', 'SHRIMP TOM YUM', TY, ('shrimp', 'chili', 'scallion', 'mushroom'), 2, shape='cup'),
], dark=0)
brand('YumYum', 'Ям-Ям', 'Yum-Yum', 'en', 'arc', 'mini', 'YumYum', 'THAI NOODLES', '#00897b', '#004d40', '#ffca28', [
    V('YumYumDuck', 'Утка', 'Duck', 'бульон с уткой', 'duck broth', 'DUCK FLAVOUR', '#b8763a', ('meat', 'scallion'), 0),
    V('YumYumSeafood', 'Морепродукты', 'Seafood', 'острый бульон с морепродуктами', 'a spicy seafood broth', 'SPICY SEAFOOD', TY, ('shrimp', 'chili', 'scallion'), 2, c1='#1e88e5', c2='#0d47a1'),
])
brand('WaiWai', 'Вай-Вай', 'Wai-Wai', 'en', 'band', 'mini', 'WAI WAI', 'READY TO EAT', '#ffeb3b', '#fbc02d', '#d32f2f', [
    V('WaiWai', '', '', 'непальская лапша с курицей и специями', 'Nepali noodles with chicken and spices', 'CHICKEN · MASALA', '#d8a030', ('scallion', 'chili', 'egg'), 1),
], dark=0)
brand('HauHau', 'Хау Хау', 'Hau Hau', 'en', 'stripe', 'mini', 'Hảo Hảo', 'MÌ TÔM CHUA CAY', '#ec407a', '#c2185b', '#fffde7', [
    V('HauHau', '', '', 'кисло-острая креветка по-вьетнамски', 'Vietnamese hot and sour shrimp', 'TÔM CHUA CAY', TY, ('shrimp', 'chili', 'scallion'), 2),
])
brand('Vifo', 'Вифо', 'Vifo', 'en', 'kraft', 'brick', 'VIFO', 'PHỞ · RICE NOODLES', '#fafafa', '#e8f5e9', '#2e7d32', [
    V('VifoPhoBo', 'Фо Бо', 'Pho Bo', 'рисовая лапша в говяжьем бульоне с бадьяном', 'rice noodles in beef broth with star anise', 'PHỞ BÒ · BEEF', PHO, ('meat', 'scallion', 'chili'), 0, rice=1),
    V('VifoPhoGa', 'Фо Га', 'Pho Ga', 'рисовая лапша в курином бульоне', 'rice noodles in chicken broth', 'PHỞ GÀ · CHICKEN', '#e0cc8a', ('egg', 'scallion', 'chili'), 0, rice=1, ac='#f9a825'),
    V('VifoBowl', 'Фо Бо', 'Pho Bo', 'рисовая лапша в говяжьем бульоне с бадьяном', 'rice noodles in beef broth with star anise', 'PHỞ BÒ · BEEF', PHO, ('meat', 'scallion', 'chili'), 0, rice=1, shape='bowl', tpl='cupB'),
], dark=0)
brand('Kun', 'Мастер Кун', 'Master Kun', 'zh', 'cupB', 'bucket', '孔師傅', 'MASTER KUN', '#c62828', '#7f0000', '#ffd600', [
    V('KunBeef', 'Тушёная говядина', 'Braised Beef', 'густой бульон с тушёной говядиной', 'a thick braised beef broth', '紅燒牛肉麵', '#9a4a1e', ('meat', 'scallion', 'chili'), 1),
    V('KunSpicy', 'Острая говядина', 'Spicy Beef', 'говядина в остром масле', 'beef in hot oil', '香辣牛肉麵', '#a8200e', ('meat', 'chili', 'scallion'), 3, c1='#ff6f00', c2='#bf360c'),
    V('KunCabbage', 'Квашеная капуста', 'Pickled Cabbage', 'говядина с квашеной капустой', 'beef with pickled cabbage', '老壇酸菜牛肉麵', '#b8862a', ('kimchi', 'meat', 'scallion'), 1, c1='#6a1b9a', c2='#38006b', shape='big', tpl='block'),
    V('KunSeafood', 'Морепродукты', 'Seafood', 'бульон с морепродуктами', 'seafood broth', '鮮蝦魚板麵', SEA, ('shrimp', 'narutomaki', 'scallion'), 0, c1='#0277bd', c2='#01579b'),
])
brand('DanDan', 'Сычуань Дандань', 'Sichuan Dandan', 'zh', 'block', 'brick', '擔擔麵', 'SICHUAN DAN DAN', '#bf360c', '#3e1005', '#ffd54f', [
    V('DanDan', '', '', 'лапша в остром кунжутном соусе, без бульона', 'noodles in a hot sesame sauce, no broth', 'SESAME · CHILLI OIL', '#a84a1a', ('meat', 'scallion', 'chili', 'sesame'), 3, dry=1),
])
brand('ManHan', 'Мань Хань', 'Man Han', 'zh', 'cupK', 'bucket', '滿漢', 'MAN HAN FEAST', '#3e2723', '#1b0f0b', '#d4af37', [
    V('ManHan', 'Говядина', 'Beef', 'тайваньский суп с настоящими кусками говядины', 'Taiwanese soup with real chunks of beef', '蔥燒牛肉麵', '#8a4420', ('meat', 'scallion', 'chili'), 1),
])
brand('Meggi', 'Мэгги', 'Meggi', 'en', 'band', 'mini', 'Meggi', '2-MINUTE NOODLES', '#fdd835', '#fbc02d', '#d32f2f', [
    V('MeggiMasala', 'Масала', 'Masala', 'индийские специи масала', 'Indian masala spices', 'MASALA', '#d8982a', ('scallion', 'chili', 'corn'), 1),
    V('MeggiKari', 'Кари', 'Kari', 'малайзийское карри', 'Malaysian curry', 'KARI · CURRY', CURRY, ('egg', 'chili', 'scallion'), 2, ac='#e65100'),
    V('MeggiAsam', 'Асам Лакса', 'Asam Laksa', 'кислый рыбный суп', 'a sour fish soup', 'ASAM LAKSA', '#c85a2a', ('shrimp', 'chili', 'scallion'), 2, ac='#6a1b9a'),
], dark=0)
brand('Prima', 'Прима Лакса', 'Prima Laksa', 'en', 'kraft', 'big', 'PRIMA', 'SINGAPORE LAKSA', '#b71c1c', '#7f0000', '#ffd54f', [
    V('PrimaLaksa', '', '', 'толстая лапша в кокосовом карри', 'thick noodles in coconut curry', 'LAKSA LA MIAN', LAKSA, ('shrimp', 'tofu', 'chili', 'egg'), 2, thick=1),
    V('PrimaChilliCrab', 'Чили-краб', 'Chilli Crab', 'соус чили-краб, без бульона', 'chilli crab sauce, no broth', 'CHILLI CRAB LA MIAN', '#c4421e', ('shrimp', 'egg', 'chili', 'scallion'), 2, dry=1, thick=1, c1='#e65100', c2='#8a2a00', sub='SINGAPORE CHILLI CRAB', brand_ru='Прима', brand_en='Prima'),
])
brand('LuckyMi', 'Лаки Ми', 'Lucky Mi', 'en', 'split', 'mini', 'Lucky Mi', 'PANCIT CANTON', '#ef6c00', '#e65100', '#fff176', [
    V('PancitCanton', 'Пансит Кантон', 'Pancit Canton', 'филиппинская жареная лапша с каламанси, без бульона', 'Filipino fried noodles with calamansi, no broth', 'CALAMANSI', '#b8782a', ('scallion', 'egg', 'chili'), 1, dry=1),
    V('PancitChilimansi', 'Пансит Чилиманси', 'Pancit Chilimansi', 'острая, с каламанси, без бульона', 'hot, with calamansi, no broth', 'CHILIMANSI', '#a84a1a', ('chili', 'scallion', 'egg'), 2, dry=1, c1='#c62828', c2='#7f0000'),
])
brand('TopRamen', 'Топ Рамен', 'Top Ramen', 'en', 'band', 'brick', 'TOP RAMEN', 'OODLES OF NOODLES', '#1e88e5', '#0d47a1', '#ffeb3b', [
    V('TopChicken', 'Курица', 'Chicken', 'американская классика: курица', 'the American classic: chicken', 'CHICKEN FLAVOR', CH, ('egg', 'scallion', 'corn'), 0, c1='#fb8c00', c2='#e65100', ac='#ffffff'),
    V('TopBeef', 'Говядина', 'Beef', 'американская классика: говядина', 'the American classic: beef', 'BEEF FLAVOR', BF, ('meat', 'scallion'), 0, c1='#e53935', c2='#b71c1c', ac='#ffffff'),
    V('TopShrimp', 'Креветка', 'Shrimp', 'американская классика: креветка', 'the American classic: shrimp', 'SHRIMP FLAVOR', SEA, ('shrimp', 'scallion'), 0, c1='#ec407a', c2='#ad1457', ac='#ffffff'),
    V('TopChili', 'Чили', 'Chili', 'американская классика: перец чили', 'the American classic: chili', 'CHILI FLAVOR', RED, ('chili', 'scallion', 'meat'), 2, c1='#2e7d32', c2='#1b5e20', ac='#ffeb3b'),
])

# ------------------------------------------------------------------ batch 2 (0.3.0): more brands, user's list of 11-14

def more(key, *vs):
    next(b for b in B if b['key'] == key)['variants'].extend(vs)


# Korea
brand('Chapaguri', 'Чапагури', 'Chapaguri', 'ko', 'split', 'kpack', '짜파구리', 'RAM-DON', '#4e342e', '#1b0f0b', '#ff7043', [
    V('Chapaguri', '', '', 'та самая «рам-дон» из кино про подвал: две лапши в одной пачке, без бульона. Стейк ищи сам', 'the very "ram-don" from the film about the basement: two noodles in one pack, no broth. Find the steak yourself', 'RAM-DON · JJAJANG + UDON', JJA, ('meat', 'scallion', 'chili'), 1, dry=1, thick=1),
    V('ChapaguriSpicy', 'Острый', 'Spicy', 'рам-дон поострее, без бульона. Богатые соседи сверху такое не едят', 'a hotter ram-don, no broth. The rich folks upstairs do not eat this', 'SPICY RAM-DON', '#3a140c', ('meat', 'chili', 'scallion'), 3, dry=1, thick=1, c1='#b71c1c', c2='#1b0f0b', ac='#ffd600'),
    V('ChapaguriCup', '', '', 'рам-дон в стакане, без бульона', 'ram-don in a cup, no broth', 'RAM-DON', JJA, ('meat', 'scallion'), 1, dry=1, thick=1, shape='bigcup'),
])
brand('CheeseR', 'Чиз Рамён', 'Cheese Ramyun', 'ko', 'band', 'kpack', '치즈라면', 'REAL CHEESE RAMYUN', '#ffd740', '#ff8f00', '#c62828', [
    V('CheeseRamyun', '', '', 'острый бульон с пакетиком сырного порошка', 'a spicy broth with a sachet of cheese powder', 'REAL CHEESE', CHS, ('cheese', 'scallion', 'chili'), 1),
    V('CheeseRamyunFire', 'Острый', 'Spicy', 'сыр и двойной перец', 'cheese and double chilli', 'SPICY CHEESE', '#d8641e', ('cheese', 'chili', 'scallion'), 3, c1='#e53935', c2='#8e0000', ac='#ffd740'),
    V('CheeseRamyunBowl', '', '', 'сырный рамён в миске', 'cheese ramyun in a bowl', 'REAL CHEESE', CHS, ('cheese', 'scallion', 'corn'), 1, shape='kbowl'),
], dark=0)
brand('Nagasaki', 'Нагасаки Чамппон', 'Nagasaki Jjamppong', 'ko', 'kraft', 'kpack', '나가사끼짬뽕', 'NAGASAKI JJAMPPONG', '#fafafa', '#e3ecf0', '#c62828', [
    V('NagasakiJjamppong', '', '', 'белый бульон с морепродуктами и зелёным перцем: выглядит невинно, жжётся честно', 'a white seafood broth with green chilli: looks innocent, burns honestly', 'WHITE SEAFOOD BROTH', TONK, ('shrimp', 'scallion', 'chili', 'mushroom'), 2),
    V('NagasakiCup', '', '', 'белый острый бульон с морепродуктами', 'a white spicy seafood broth', 'WHITE SEAFOOD BROTH', TONK, ('shrimp', 'scallion', 'chili'), 2, shape='tallcup'),
], dark=0)
brand('Sutah', 'Сута Рамён', 'Sutah Ramyun', 'ko', 'arc', 'kpack', '수타면', 'SUTAH · HAND-PULLED', '#c62828', '#6a0d0d', '#ffe082', [
    V('Sutah', '', '', 'упругая лапша «ручной вытяжки» в остром говяжьем бульоне', 'springy "hand-pulled" noodles in a spicy beef broth', 'SPICY BEEF', RED, ('meat', 'scallion', 'mushroom', 'chili'), 2),
    V('SutahBowl', '', '', 'острый говяжий бульон, большая миска', 'spicy beef broth, a big bowl', 'SPICY BEEF', RED, ('meat', 'scallion', 'chili'), 2, shape='kbowl'),
])
brand('Rabokki', 'Рабокки', 'Rabokki', 'ko', 'cupB', 'bigcup', '라볶이', 'RABOKKI', '#ff5252', '#c2185b', '#fff176', [
    V('Rabokki', '', '', 'рамён в сладко-остром соусе от токпокки, без бульона', 'ramyun in sweet and hot tteokbokki sauce, no broth', 'TTEOKBOKKI SAUCE', '#b8281a', ('scallion', 'egg', 'chili', 'sesame'), 2, dry=1),
    V('RabokkiCheese', 'Сырный', 'Cheese', 'соус токпокки под сырной шапкой, без бульона', 'tteokbokki sauce under a cap of cheese, no broth', 'CHEESE TTEOKBOKKI', '#cf6a28', ('cheese', 'scallion', 'chili'), 2, dry=1, c1='#ffb300', c2='#f57c00', ac='#c62828'),
    V('RabokkiRose', 'Розе', 'Rose', 'соус токпокки со сливками, без бульона', 'tteokbokki sauce with cream, no broth', 'ROSE TTEOKBOKKI', '#e08a6a', ('cheese', 'scallion', 'meat'), 1, dry=1, c1='#f8bbd0', c2='#f06292', ac='#ad1457'),
])
brand('Miyeok', 'Миёк Рамён', 'Miyeok Ramyun', 'ko', 'kraft', 'kpack', '미역국라면', 'SEAWEED SOUP RAMYUN', '#e0f2f1', '#b2dfdb', '#004d40', [
    V('Miyeok', '', '', 'корейский суп из водорослей, который варят на день рождения. Праздник посреди метели', 'the Korean seaweed soup cooked for birthdays. A party in the middle of a blizzard', 'BEEF · SEAWEED', '#b8a878', ('nori', 'meat', 'scallion', 'sesame'), 0),
], dark=0)
brand('Budae', 'Пудэ Ччигэ', 'Budae Jjigae', 'ko', 'block', 'kpack', '부대찌개면', 'ARMY STEW RAMYUN', '#37474f', '#102027', '#ff7043', [
    V('Budae', '', '', '«армейское рагу»: острый бульон, колбаса, фасоль и сыр. Всё, что нашлось на складе', '"army stew": spicy broth, sausage, beans and cheese. Everything the depot had', 'ARMY STEW', '#b83a1c', ('meat', 'cheese', 'kimchi', 'scallion'), 2),
    V('BudaeBowl', '', '', 'армейское рагу в большой миске', 'army stew in a big bowl', 'ARMY STEW', '#b83a1c', ('meat', 'cheese', 'kimchi'), 2, shape='kbowl'),
])
brand('Naeng', 'Нэнмён', 'Naengmyeon', 'ko', 'tate', 'long', '냉면', 'NAENGMYEON', '#e1f5fe', '#b3e5fc', '#01579b', [
    V('NaengMul', 'Муль', 'Mul', 'гречневая лапша для ледяного бульона. Лёд на Великом Медведе бесплатный, но заваривают её всё же горячей', 'buckwheat noodles for an icy broth. Ice is free on Great Bear, but you still brew them hot', 'MUL · COLD BROTH', '#d8cfae', ('egg', 'scallion', 'sesame'), 0, soba=1),
    V('NaengBibim', 'Бибим', 'Bibim', 'гречневая лапша в остром красном соусе, без бульона', 'buckwheat noodles in a hot red sauce, no broth', 'BIBIM · SPICY', '#a8281a', ('egg', 'chili', 'sesame', 'scallion'), 2, soba=1, dry=1, c1='#ffebee', c2='#ffcdd2', ac='#b71c1c'),
], dark=0)

# Japan
more('Marutyan',
    V('KonKitsune', 'Кон-но Кицунэ Соба', 'Kon no Kitsune Soba', 'гречневая соба с жареным тофу', 'buckwheat soba with fried tofu', '紺のきつねそば', '#7a4a26', ('tofu', 'scallion', 'narutomaki'), 0, soba=1, c1='#1a237e', c2='#0d1442', ac='#ffecb3', mascot='fox'),
    V('SeimenShio', 'Сэймэн Сио', 'Seimen Shio', 'прозрачный солёный бульон', 'a clear salt broth', '正麺 塩味', SHIO, ('chashu', 'scallion', 'corn'), 0, shape='jpack', tpl='jp', c1='#fafafa', c2='#e0f2f1', ac='#00897b', dark=0),
    V('SeimenTonkotsu', 'Сэймэн Тонкоцу', 'Seimen Tonkotsu', 'белый свиной бульон', 'a white pork broth', '正麺 豚骨味', TONK, ('chashu', 'scallion', 'sesame'), 0, shape='jpack', tpl='jp', c1='#fff8e1', c2='#ffe0b2', ac='#bf360c', dark=0),
)
brand('Moko', 'Моко Танмэн', 'Moko Tanmen', 'ja', 'cupK', 'tallcup', '蒙古タンメン', 'MOKO TANMEN', '#c62828', '#5d0a0a', '#ffd600', [
    V('MokoTanmen', '', '', 'мисо с огненным тофу: самый острый стакан Японии. Крышку открывают с уважением', 'miso with fiery tofu: the hottest cup in Japan. Open the lid with respect', '辛旨味噌', '#b8401c', ('tofu', 'chili', 'scallion', 'meat'), 3),
    V('MokoHokkyoku', 'Хоккёку', 'Hokkyoku', '«Северный полюс»: ещё острее. Название подходит к погоде, вкус нет', '"North Pole": hotter still. The name suits the weather, the taste does not', '北極ラーメン', '#8e1408', ('tofu', 'chili', 'scallion'), 4, c1='#8e0000', c2='#1a0000'),
])
brand('BabyStar', 'Бэби Стар', 'Baby Star', 'ja', 'arc', 'mini', 'ベビースター', 'BABY STAR · RAMEN SNACK', '#ff7043', '#e64a19', '#fff59d', [
    V('BabyStarChicken', 'Курица', 'Chicken', 'лапша-снек: вообще-то её грызут сухой, но кипятком заливать никто не запретил', 'a noodle snack: you are meant to crunch it dry, but nobody forbade boiling water', 'チキン味', CH, ('scallion', 'corn'), 0),
    V('BabyStarYakisoba', 'Якисоба', 'Yakisoba', 'лапша-снек со вкусом соуса якисоба, без бульона', 'a noodle snack with yakisoba sauce flavour, no broth', 'ソース焼そば味', SOY, ('nori', 'scallion'), 0, dry=1, c1='#6d4c41', c2='#3e2723', ac='#ffca28'),
    V('BabyStarSpicy', 'Острая', 'Spicy', 'лапша-снек с перцем', 'a noodle snack with chilli', 'うま辛チキン', RED, ('chili', 'scallion'), 2, c1='#d32f2f', c2='#7f0000', ac='#fff59d'),
])
brand('CupStar', 'Кап Стар', 'Cup Star', 'ja', 'cupA', 'cup', 'カップスター', 'CUP STAR', '#fffde7', '#fff9c4', '#d32f2f', [
    V('CupStarShoyu', 'Сёю', 'Shoyu', 'соевый бульон', 'soy broth', '醤油', SHOYU, ('chashu', 'scallion', 'narutomaki'), 0, band='#d32f2f'),
    V('CupStarMiso', 'Мисо', 'Miso', 'бульон на мисо', 'miso broth', '味噌', MISO, ('corn', 'scallion', 'meat'), 0, band='#ef6c00', ac='#ef6c00'),
    V('CupStarTonkotsu', 'Тонкоцу', 'Tonkotsu', 'белый свиной бульон', 'white pork broth', '豚骨', TONK, ('chashu', 'scallion', 'sesame'), 0, band='#8d6e63', ac='#6d4c41'),
    V('CupStarShio', 'Сио', 'Shio', 'лёгкий солёный бульон', 'a light salt broth', '塩', SHIO, ('corn', 'scallion', 'shrimp'), 0, band='#0097a7', ac='#00838f'),
], dark=0)
brand('SuperCup', 'Супер Кап 1.5', 'Super Cup 1.5', 'ja', 'cupC', 'bigcup', 'スーパーカップ', 'SUPER CUP 1.5', '#1565c0', '#0d2f66', '#ffeb3b', [
    V('SuperCupShoyu', 'Сёю', 'Shoyu', 'полторы порции в соевом бульоне', 'one and a half helpings in soy broth', '1.5倍 · 醤油', SHOYU, ('chashu', 'scallion', 'egg'), 0),
    V('SuperCupTonkotsu', 'Тонкоцу', 'Tonkotsu', 'полторы порции в свином бульоне', 'one and a half helpings in pork broth', '1.5倍 · 豚骨', TONK, ('chashu', 'scallion', 'sesame'), 0, c1='#f5f5f5', c2='#d7ccc8', ac='#bf360c', dark=0),
    V('SuperCupMiso', 'Мисо', 'Miso', 'полторы порции в мисо', 'one and a half helpings in miso', '1.5倍 · 味噌', MISO, ('corn', 'scallion', 'meat'), 0, c1='#ef6c00', c2='#bf360c', ac='#fff8e1'),
])
brand('Umaka', 'Умакаттян', 'Umakacchan', 'ja', 'jp', 'jpack', 'うまかっちゃん', 'Umakacchan', '#ff9800', '#ef6c00', '#ffffff', [
    V('Umakacchan', '', '', 'тонкоцу с острова Кюсю, свиной бульон с ароматным маслом', 'tonkotsu from Kyushu, pork broth with scented oil', '九州の味 · 豚骨', TONK, ('chashu', 'scallion', 'sesame'), 0),
    V('UmakaTakana', 'Караси Такана', 'Karashi Takana', 'тонкоцу с острой маринованной горчицей', 'tonkotsu with hot pickled mustard greens', '博多 からし高菜', '#e0b878', ('chashu', 'kimchi', 'scallion', 'sesame'), 2, c1='#2e7d32', c2='#1b5e20', ac='#ffeb3b'),
])
brand('Gotochi', 'Гототи Рамен', 'Gotochi Ramen', 'ja', 'tate', 'jpack', 'ご当地ラーメン', 'GOTOCHI · LOCAL RAMEN', '#fff8e1', '#ffe0b2', '#b71c1c', [
    V('GotochiSapporo', 'Саппоро Мисо', 'Sapporo Miso', 'мисо с севера, с кукурузой и маслом. Там тоже знают, что такое зима', 'miso from the north with corn and butter. They know winter there too', '札幌 味噌', MISO, ('corn', 'cheese', 'chashu', 'scallion'), 0),
    V('GotochiHakodate', 'Хакодатэ Сио', 'Hakodate Shio', 'прозрачный солёный бульон из портового города', 'a clear salt broth from a port town', '函館 塩', SHIO, ('chashu', 'scallion', 'narutomaki'), 0, c1='#e1f5fe', c2='#b3e5fc', ac='#01579b'),
    V('GotochiKitakata', 'Китаката Сёю', 'Kitakata Shoyu', 'соевый бульон и широкая волнистая лапша', 'soy broth and broad wavy noodles', '喜多方 醤油', SHOYU, ('chashu', 'scallion', 'narutomaki'), 0, thick=1, c1='#efebe9', c2='#d7ccc8', ac='#4e342e'),
    V('GotochiKumamoto', 'Кумамото Чёрный', 'Kumamoto Black', 'тонкоцу с чёрным маслом жжёного чеснока', 'tonkotsu with black burnt-garlic oil', '熊本 黒マー油', '#bfa888', ('chashu', 'nori', 'scallion', 'sesame'), 0, c1='#212121', c2='#000000', ac='#ffca28', dark=1),
], dark=0)
brand('Gotsumori', 'Гоцумори', 'Gotsumori', 'ja', 'lid', 'box', 'ごつ盛り', 'GOTSUMORI · 1.5', '#f9a825', '#f57f17', '#b71c1c', [
    V('GotsumoriYakisoba', 'Якисоба', 'Yakisoba', 'гора жареной лапши с пакетиком майонеза, без бульона', 'a mountain of fried noodles with a sachet of mayo, no broth', 'ソース焼そば · マヨ付き', SOY, ('nori', 'meat', 'scallion', 'egg'), 0, dry=1),
    V('GotsumoriShio', 'Сио-якисоба', 'Shio Yakisoba', 'жареная лапша с солью и майонезом, без бульона', 'fried noodles with salt and mayo, no broth', '塩焼そば · マヨ付き', '#d8c890', ('nori', 'shrimp', 'scallion', 'sesame'), 0, dry=1, c1='#4fc3f7', c2='#0288d1', ac='#01579b'),
], dark=0)
brand('KareUdon', 'Карэ Удон', 'Kare Udon', 'ja', 'cupB', 'bowl', 'カレーうどん', 'KARE UDON', '#6d4c41', '#3e2723', '#ffca28', [
    V('KareUdon', '', '', 'удон в густом карри. Рубашку после него можно не стирать, а выбрасывать', 'udon in thick curry. Do not wash the shirt afterwards, just throw it away', 'カレーうどん', CURRY, ('meat', 'scallion', 'tofu'), 1, thick=1, ru_full='Удон «Карэ Удон» в миске'),
])

# China, Taiwan
brand('Baishan', 'Байсян', 'Baishan', 'zh', 'band', 'brick', '白像', 'BAISHAN · WHITE ELEPHANT', '#fbc02d', '#f57f17', '#b71c1c', [
    V('BaishanBone', 'Костный бульон', 'Bone Broth', 'наваристый бульон на свиной кости', 'a rich pork bone broth', '大骨麵', TONK, ('meat', 'scallion', 'egg'), 0),
    V('BaishanChicken', 'Курица с грибами', 'Chicken Mushroom', 'курица с шиитаке', 'chicken with shiitake', '香菇燉雞麵', CH, ('mushroom', 'scallion', 'meat'), 0, c1='#7cb342', c2='#33691e', ac='#fff9c4'),
    V('BaishanSpicy', 'Острая говядина', 'Spicy Beef', 'говядина в остром масле', 'beef in hot oil', '香辣牛肉麵', '#a8200e', ('meat', 'chili', 'scallion'), 3, c1='#d32f2f', c2='#7f0000', ac='#ffeb3b'),
], dark=0)
brand('TungI', 'Тун-И', 'Tung-I', 'zh', 'block', 'big', '統壹', 'TUNG-I', '#6a1b9a', '#38006b', '#ffd54f', [
    V('TungICabbage', 'Квашеная капуста', 'Pickled Cabbage', 'говядина с квашеной капустой из старой бочки', 'beef with pickled cabbage from an old crock', '老壇酸菜牛肉麵', '#b8862a', ('kimchi', 'meat', 'scallion', 'chili'), 2),
    V('TungIBeef', 'Тушёная говядина', 'Braised Beef', 'густой бульон с тушёной говядиной', 'a thick braised beef broth', '紅燒牛肉麵', '#9a4a1e', ('meat', 'scallion'), 1, c1='#c62828', c2='#7f0000'),
    V('TungIPork', 'Свиной фарш', 'Minced Pork', 'тайваньская классика с жареным луком и фаршем', 'a Taiwanese classic with fried shallots and mince', '肉燥麵', '#c99a55', ('meat', 'scallion', 'egg'), 0, c1='#ff8f00', c2='#e65100', ac='#ffffff', shape='brick', tpl='band'),
])
brand('SuanLa', 'Суаньлафэнь', 'Suan La Fen', 'zh', 'cupK', 'bucket', '酸辣粉', 'SUAN LA FEN', '#b71c1c', '#4a0000', '#ffd54f', [
    V('SuanLaFen', '', '', 'стеклянная лапша из батата: сначала кисло, потом остро, потом снова хочется', 'sweet potato glass noodles: sour first, hot next, then you want more', '重慶酸辣粉', '#a8301a', ('chili', 'scallion', 'sesame', 'meat'), 3, rice=1),
    V('SuanLaFenMala', 'Мала', 'Mala', 'стеклянная лапша с онемляющим перцем', 'glass noodles with numbing pepper', '麻辣粉', '#8e1408', ('chili', 'sesame', 'scallion'), 4, rice=1, c1='#4a148c', c2='#1a0033'),
])
brand('Xiaomian', 'Чунцин Сяомянь', 'Chongqing Xiaomian', 'zh', 'stripe', 'brick', '重慶小麵', 'CHONGQING XIAOMIAN', '#d84315', '#7f1d00', '#ffe082', [
    V('Xiaomian', '', '', 'уличная лапша из Чунцина: масло чили и сычуаньский перец', 'street noodles from Chongqing: chilli oil and Sichuan pepper', '麻辣小麵', '#a8200e', ('chili', 'scallion', 'sesame'), 3),
    V('XiaomianPea', 'Ваньцза', 'Wanza', 'с жёлтым горохом и мясным фаршем, почти без бульона', 'with yellow peas and minced meat, almost no broth', '豌雜麵', '#b8641e', ('meat', 'corn', 'scallion', 'chili'), 2, dry=1, c1='#f9a825', c2='#e65100', ac='#4e1a00'),
])
brand('LaoTan', 'Лао Тань', 'Lao Tan', 'zh', 'arc', 'big', '老壇', 'LAO TAN · SOUR FISH', '#2e7d32', '#0d3b12', '#ffeb3b', [
    V('LaoTanFish', 'Кислая рыба', 'Sour Fish', 'рыбный бульон с квашеной горчицей. Рыбу ловить не надо, уже хорошо', 'fish broth with pickled mustard greens. No ice fishing needed, already a win', '酸菜魚麵', '#d8c070', ('kimchi', 'chili', 'scallion', 'narutomaki'), 2),
])
brand('Regan', 'Уханьская Жэгань', 'Wuhan Regan', 'zh', 'kraft', 'brick', '熱乾麵', 'WUHAN HOT DRY NOODLES', '#f5e6c8', '#e0c890', '#8a3a12', [
    V('Reganmian', '', '', '«горячая сухая лапша»: густая кунжутная паста, без бульона. Название честное', '"hot dry noodles": thick sesame paste, no broth. An honest name', '武漢熱乾麵', '#a8763a', ('scallion', 'sesame', 'chili', 'kimchi'), 1, dry=1),
], dark=0)
brand('WeiLi', 'Вэй Ли', 'Wei Li', 'zh', 'lid', 'box', '維利', 'WEI LI', '#e53935', '#b71c1c', '#fff176', [
    V('WeiLiZhajiang', 'Чжацзян', 'Zhajiang', 'тайваньская лапша с мясной пастой, без бульона', 'Taiwanese noodles with meat paste, no broth', '炸醬麵', '#8a5a2b', ('meat', 'scallion', 'egg'), 0, dry=1),
    V('WeiLiSesame', 'Кунжутная курица', 'Sesame Oil Chicken', 'курица на кунжутном масле с имбирём: тайваньское средство от холода', 'chicken in sesame oil with ginger: the Taiwanese cure for the cold', '麻油雞麵', '#c99040', ('meat', 'mushroom', 'scallion', 'sesame'), 0, shape='bucket', tpl='cupK', c1='#4e342e', c2='#1b0f0b', ac='#ffca28', warm=1.6),
])

# South-East Asia
brand('PopMi', 'Поп Ми', 'Pop Mi', 'en', 'cupC', 'cup', 'Pop Mi', 'MI INSTAN CUP', '#e53935', '#b71c1c', '#ffeb3b', [
    V('PopMiAyam', 'Аям', 'Ayam', 'индонезийский стакан: курица', 'an Indonesian cup: chicken', 'RASA AYAM', CH, ('meat', 'scallion', 'corn'), 0),
    V('PopMiBawang', 'Аям Баванг', 'Ayam Bawang', 'курица с жареным луком', 'chicken with fried onion', 'RASA AYAM BAWANG', '#e0b860', ('meat', 'scallion', 'egg'), 0, c1='#fbc02d', c2='#f57f17', ac='#b71c1c', dark=0),
    V('PopMiBaso', 'Басо', 'Baso', 'бульон с говяжьими фрикадельками', 'broth with beef meatballs', 'RASA BASO', BF, ('meat', 'scallion'), 0, c1='#6d4c41', c2='#3e2723'),
    V('PopMiPedas', 'Педас Довер', 'Pedas Dower', 'курица с перцем «до распухших губ»', 'chicken with chilli "till your lips swell"', 'PEDAS DOWER', '#b8281a', ('meat', 'chili', 'scallion'), 3, c1='#212121', c2='#000000', ac='#ff1744'),
])
brand('Sarimi', 'Сарими', 'Sarimie', 'en', 'arc', 'mini', 'Sarimie', 'MI INSTAN', '#43a047', '#1b5e20', '#ffeb3b', [
    V('SarimiBawang', 'Аям Баванг', 'Ayam Bawang', 'курица с жареным луком', 'chicken with fried onion', 'AYAM BAWANG', '#e0b860', ('meat', 'scallion', 'egg'), 0),
    V('SarimiSoto', 'Сото', 'Soto', 'жёлтый суп сото с лаймом', 'yellow soto soup with lime', 'SOTO KOYA', '#d8b040', ('meat', 'egg', 'scallion'), 1, c1='#fdd835', c2='#f9a825', ac='#1b5e20', dark=0),
    V('SarimiDuo', 'Иси 2', 'Isi 2', 'две лепёшки в одной пачке, жареная, без бульона', 'two cakes in one pack, fried, no broth', 'ISI 2 · GORENG', '#b8782a', ('egg', 'scallion', 'chili'), 1, dry=1, shape='big', c1='#e53935', c2='#8e0000'),
])
brand('Supermi', 'Супер Ми', 'Supermie', 'en', 'stripe', 'brick', 'Supermie', 'SEJAK 1968', '#1e88e5', '#0d47a1', '#ffeb3b', [
    V('SupermiBawang', 'Аям Баванг', 'Ayam Bawang', 'курица с жареным луком, вкус индонезийского детства', 'chicken with fried onion, the taste of an Indonesian childhood', 'AYAM BAWANG', '#e0b860', ('meat', 'scallion', 'egg'), 0),
    V('SupermiKaldu', 'Калду Аям', 'Kaldu Ayam', 'простой куриный бульон', 'plain chicken broth', 'KALDU AYAM', CH, ('egg', 'scallion'), 0, c1='#fb8c00', c2='#e65100'),
    V('SupermiGoreng', 'Горенг', 'Goreng', 'жареная, со сладким соевым соусом, без бульона', 'fried, with sweet soy sauce, no broth', 'MI GORENG', '#8a5a2b', ('egg', 'scallion', 'chili'), 1, dry=1, c1='#c62828', c2='#7f0000'),
])
brand('MamyMonster', 'Мами Монстр', 'Mamy Monster', 'en', 'arc', 'mini', 'MAMY', 'MONSTER · SNACK NOODLES', '#1e88e5', '#0d47a1', '#ffeb3b', [
    V('MamyMonsterChicken', 'Курица', 'Chicken', 'малайзийская лапша-снек с синим монстром: крошат в пачке и едят сухой, но заварить тоже можно', 'Malaysian snack noodles with a blue monster: crush in the pack and eat dry, or brew them', 'CHICKEN', CH, ('scallion', 'corn'), 0),
    V('MamyMonsterBBQ', 'Барбекю', 'BBQ', 'лапша-снек со вкусом барбекю', 'snack noodles with barbecue flavour', 'BBQ', '#a8501e', ('scallion', 'meat'), 0, c1='#e53935', c2='#8e0000'),
])
brand('Daebag', 'Дэбаг', 'Daebag', 'en', 'cupK', 'bigcup', 'GHOST PEPPER', 'DAEBAG · GHOST PEPPER', '#151515', '#000000', '#ff1744', [
    V('GhostPepper', 'Перец-призрак', 'Ghost Pepper', 'чёрная лапша с перцем-призраком, без бульона. «Больдак» рядом с ней отдыхает', 'black noodles with ghost pepper, no broth. Burndak is a holiday next to it', 'SPICY CHICKEN · DRY', '#5a0e06', ('chili', 'meat', 'sesame'), 4, dry=1, soba=1),
    V('GhostPepperSoup', 'Перец-призрак Кимчи', 'Ghost Pepper Kimchi', 'чёрная лапша в бульоне с кимчи. Призрак приходит через минуту', 'black noodles in kimchi broth. The ghost arrives a minute later', 'KIMCHI JJIGAE', '#a81408', ('kimchi', 'chili', 'scallion'), 4, soba=1, ac='#ffab00'),
])
brand('MaKuali', 'Ма Куали', 'Ma Kuali', 'en', 'kraft', 'big', 'MA KUALI', 'PENANG · MALAYSIA', '#fafafa', '#eceff1', '#e65100', [
    V('PenangWhiteCurry', 'Пенангское белое карри', 'Penang White Curry', 'кокосовое карри с пастой из чили: мягкое на вид, злое внутри', 'coconut curry with chilli paste: mild to look at, mean inside', 'PENANG WHITE CURRY', '#e0a050', ('shrimp', 'tofu', 'chili', 'egg'), 3),
    V('PenangPrawn', 'Пенангская креветка', 'Penang Hokkien Prawn', 'густой креветочный бульон по-хоккиенски', 'a thick Hokkien prawn broth', 'HOKKIEN PRAWN', '#c85a28', ('shrimp', 'egg', 'scallion', 'chili'), 2, ac='#c62828'),
    V('PenangTomYum', 'Пенангский том-ям', 'Penang Tom Yum', 'кисло-острый бульон с лемонграссом', 'a hot and sour broth with lemongrass', 'RED TOM YUM', TY, ('shrimp', 'mushroom', 'chili'), 2, ac='#2e7d32'),
], dark=0)
brand('Omachy', 'Омачи', 'Omachy', 'en', 'block', 'brick', 'Omachy', 'MÌ KHOAI TÂY', '#8e0000', '#3e0000', '#ffd54f', [
    V('OmachyBeef', 'Тушёная говядина', 'Beef Stew', 'вьетнамская лапша из картофеля, бульон с тушёной говядиной', 'Vietnamese potato noodles, braised beef broth', 'BÒ HẦM', '#9a4a1e', ('meat', 'scallion', 'chili'), 1),
    V('OmachyRibs', 'Свиные рёбрышки', 'Pork Ribs', 'бульон на свиных рёбрышках с овощами', 'pork rib broth with vegetables', 'SƯỜN HẦM NGŨ QUẢ', '#c99a55', ('meat', 'corn', 'scallion'), 0, c1='#2e7d32', c2='#0d3b12'),
    V('OmachySpaghetti', 'Соус спагетти', 'Spaghetti Sauce', 'лапша с томатно-мясным соусом, без бульона', 'noodles with tomato and meat sauce, no broth', 'XỐT SPAGHETTI', TOM, ('meat', 'cheese', 'scallion'), 0, dry=1, c1='#ef6c00', c2='#bf360c'),
])
brand('Saigon', 'Сайгон Бун', 'Saigon Bun', 'en', 'cupB', 'bowl', 'Sài Gòn', 'BÚN · RICE NOODLES', '#00897b', '#004d40', '#fff176', [
    V('SaigonBunBo', 'Бун Бо Хюэ', 'Bun Bo Hue', 'рисовая лапша в остром говяжьем бульоне с лемонграссом', 'rice noodles in a spicy beef broth with lemongrass', 'BÚN BÒ HUẾ', '#c85a28', ('meat', 'chili', 'scallion'), 2, rice=1),
    V('SaigonHuTieu', 'Ху Тьеу', 'Hu Tieu', 'рисовая лапша в свином бульоне с креветкой', 'rice noodles in pork broth with shrimp', 'HỦ TIẾU NAM VANG', PHO, ('shrimp', 'meat', 'scallion'), 0, rice=1, c1='#f9a825', c2='#e65100', ac='#004d40'),
    V('SaigonMienGa', 'Мьен Га', 'Mien Ga', 'стеклянная лапша с курицей', 'glass noodles with chicken', 'MIẾN GÀ', CH, ('meat', 'mushroom', 'scallion'), 0, rice=1, c1='#7cb342', c2='#33691e', ac='#ffffff'),
])
brand('Payles', 'Пэйлес', 'Payles', 'en', 'band', 'mini', 'Payles', 'XTRA BIG', '#fdd835', '#f9a825', '#d32f2f', [
    V('PaylesChilimansi', 'Экстра Биг', 'Xtra Big', 'филиппинская бюджетная жареная лапша с перцем и каламанси, без бульона', 'Filipino budget fried noodles with chilli and calamansi, no broth', 'CHILIMANSI', '#a84a1a', ('chili', 'scallion', 'egg'), 2, dry=1, shape='brick'),
    V('PaylesBatchoy', 'Батчой', 'Batchoy', 'суп батчой из Ла-Паса: свинина и жареный чеснок', 'La Paz batchoy soup: pork and fried garlic', 'LA PAZ BATCHOY', '#c99a55', ('meat', 'egg', 'scallion'), 0, c1='#ef6c00', c2='#bf360c', ac='#fff176'),
    V('PaylesBulalo', 'Булало', 'Bulalo', 'бульон на говяжьей мозговой кости', 'a beef marrow bone broth', 'BULALO', BF, ('meat', 'corn', 'scallion'), 0, c1='#6d4c41', c2='#3e2723', ac='#fff176'),
], dark=0)
more('Indomay',
    V('IndomayOnion', 'Онион Чикен', 'Onion Chicken', 'версия для Нигерии, где её едят больше, чем дома: курица с луком', 'the Nigerian version, eaten there more than at home: chicken and onion', 'NIGERIA · ONION CHICKEN', CH, ('egg', 'scallion', 'meat'), 0, c1='#ffeb3b', c2='#f9a825', ac='#d32f2f', dark=0),
    V('IndomayPepper', 'Пеппер Суп', 'Pepper Soup', 'нигерийский перечный суп с курицей. Перец в названии не для красоты', 'Nigerian chicken pepper soup. The pepper in the name is not there for show', 'NIGERIA · CHICKEN PEPPER SOUP', '#b8401c', ('meat', 'chili', 'scallion'), 3, c1='#2e7d32', c2='#0d3b12', ac='#ffeb3b'),
)
more('Prima',
    V('PrimaCurry', 'Карри', 'Curry', 'сингапурское карри на кокосовом молоке', 'Singapore curry on coconut milk', 'SINGAPORE CURRY', '#d39a1e', ('tofu', 'egg', 'chili', 'shrimp'), 2, c1='#f9a825', c2='#e65100', ac='#3e2723'),
)
more('Mama',
    V('MamaTomYumBowl', 'Том-ям', 'Tom Yum', 'том-ям с креветкой, большой стакан', 'shrimp tom yum, a big cup', 'SHRIMP TOM YUM · JUMBO', TY, ('shrimp', 'mushroom', 'chili', 'scallion'), 2, shape='bigcup', c1='#ef6c00', c2='#bf360c', ac='#fff176'),
)

# India, Russia and neighbours
brand('Yipee', 'Йиппи', 'Yipee', 'en', 'arc', 'mini', 'Yipee!', 'ROUND NOODLES', '#f4511e', '#bf360c', '#ffeb3b', [
    V('YipeeMagic', 'Мэджик Масала', 'Magic Masala', 'круглая лепёшка лапши с индийскими пряностями', 'a round noodle cake with Indian spices', 'MAGIC MASALA', '#d39a1e', ('corn', 'scallion', 'chili'), 1),
    V('YipeeMood', 'Муд Масала', 'Mood Masala', 'масала поострее, с двумя пакетиками специй', 'a hotter masala with two spice sachets', 'MOOD MASALA', '#c85a28', ('chili', 'corn', 'scallion'), 2, c1='#c62828', c2='#7f0000'),
    V('YipeeAtta', 'Атта', 'Atta', 'лапша из цельнозерновой муки', 'whole-wheat noodles', 'POWER UP ATTA', '#c98f45', ('corn', 'scallion'), 1, soba=1, c1='#7cb342', c2='#33691e'),
])
brand('Choyz', 'Чойз', 'Choyz', 'ru', 'band', 'brick', 'ЧОЙЗ', 'ЛАПША БЫСТРОГО ПРИГОТОВЛЕНИЯ', '#ffb300', '#ef6c00', '#b71c1c', [
    V('ChoyzChicken', 'Курица', 'Chicken', 'со вкусом курицы', 'chicken flavour', 'КУРИЦА', CH, ('egg', 'scallion'), 0),
    V('ChoyzBeef', 'Говядина', 'Beef', 'со вкусом говядины', 'beef flavour', 'ГОВЯДИНА', BF, ('meat', 'scallion'), 0, c1='#c62828', c2='#7f0000', ac='#ffeb3b'),
    V('ChoyzChili', 'Острая курица', 'Spicy Chicken', 'курица с перцем', 'chicken with chilli', 'ОСТРАЯ КУРИЦА', RED, ('chili', 'egg', 'scallion'), 2, c1='#d84315', c2='#7f1d00', ac='#ffeb3b'),
    V('ChoyzCup', 'Курица', 'Chicken', 'со вкусом курицы', 'chicken flavour', 'КУРИЦА', CH, ('egg', 'scallion', 'corn'), 0, shape='cup'),
], dark=0)
brand('AlexSofa', 'Александр и Софа', 'Alexander & Sofa', 'ru', 'kraft', 'mini', 'Александр и Софа', 'ВЕРМИШЕЛЬ БЫСТРОГО ПРИГОТОВЛЕНИЯ', '#fff8e1', '#ffe0b2', '#6d4c41', [
    V('AlexSofaChicken', 'Курица', 'Chicken', 'со вкусом курицы. Кто такие Александр и Софа, не знает никто', 'chicken flavour. Nobody knows who Alexander and Sofa are', 'КУРИЦА', CH, ('egg', 'scallion'), 0),
    V('AlexSofaBeef', 'Говядина', 'Beef', 'со вкусом говядины', 'beef flavour', 'ГОВЯДИНА', BF, ('meat', 'scallion'), 0, ac='#b71c1c'),
    V('AlexSofaMushroom', 'Грибы', 'Mushroom', 'со вкусом грибов', 'mushroom flavour', 'ГРИБЫ', '#b89a6a', ('mushroom', 'scallion'), 0, ac='#33691e'),
], dark=0)
brand('HonestPrice', 'Честная цена', 'Honest Price', 'ru', 'kraft', 'mini', 'ЧЕСТНАЯ ЦЕНА', 'ЛАПША. ПРОСТО ЛАПША', '#ffffff', '#f0f0f0', '#d50000', [
    V('HonestChicken', 'Курица', 'Chicken', 'самая дешёвая. Курица в составе указана из вежливости', 'the cheapest there is. Chicken is on the list out of politeness', 'ВКУС КУРИЦЫ', CH, ('scallion',), 0),
    V('HonestBeef', 'Говядина', 'Beef', 'самая дешёвая. На дизайн пачки денег не хватило, на говядину тоже', 'the cheapest there is. No money left for the pack design, nor for the beef', 'ВКУС ГОВЯДИНЫ', BF, ('scallion',), 0),
], dark=0)
brand('KespeKhan', 'Кеспе-хан', 'Kespe Khan', 'ru', 'arc', 'brick', 'КЕСПЕ-ХАН', 'ҚАЗАҚСТАН · ЛАПША', '#00acc1', '#006064', '#ffd600', [
    V('KespeBeef', 'Говядина', 'Beef', 'казахстанская домашняя лапша в говяжьем бульоне', 'Kazakh home-style noodles in beef broth', 'ГОВЯДИНА ПО-ДОМАШНЕМУ', BF, ('meat', 'scallion'), 0, thick=1),
    V('KespeLamb', 'Баранина', 'Lamb', 'бульон на баранине с зеленью', 'lamb broth with herbs', 'БАРАНИНА', '#b8823a', ('meat', 'scallion', 'chili'), 1, thick=1, c1='#43a047', c2='#1b5e20'),
    V('KespeKazy', 'Казы', 'Kazy', 'со вкусом конской колбасы казы. На Великом Медведе коней нет, так что только так', 'with the taste of kazy horse sausage. No horses on Great Bear, so this is your only way', 'КАЗЫ', '#a8603a', ('meat', 'scallion'), 0, thick=1, c1='#8e0000', c2='#3e0000'),
    V('KespeCup', 'Говядина', 'Beef', 'домашняя лапша в говяжьем бульоне', 'home-style noodles in beef broth', 'ГОВЯДИНА', BF, ('meat', 'scallion'), 0, thick=1, shape='bigcup'),
])
brand('Lagman', 'Лагман-экспресс', 'Lagman Express', 'ru', 'block', 'big', 'ЛАГМАН', 'ЛАГМАН-ЭКСПРЕСС · ЎЗБЕКИСТОН', '#1565c0', '#0d2f66', '#ffca28', [
    V('LagmanBeef', '', '', 'узбекский лагман: тянутая лапша в густой подливе с говядиной и перцем', 'Uzbek lagman: pulled noodles in a thick gravy with beef and peppers', 'С ГОВЯДИНОЙ', '#c8502a', ('meat', 'chili', 'scallion', 'corn'), 1, thick=1),
    V('LagmanKovurma', 'Ковурма', 'Kovurma', 'жареный лагман, без бульона', 'fried lagman, no broth', 'КОВУРМА · ЖАРЕНЫЙ', '#a85a2a', ('meat', 'egg', 'chili', 'scallion'), 1, thick=1, dry=1, c1='#e65100', c2='#8a2a00'),
])

# Europe, the Americas
brand('Amina', 'Амина', 'Amina', 'en', 'stripe', 'mini', 'Amina', 'ZUPA BŁYSKAWICZNA', '#fdd835', '#f9a825', '#c62828', [
    V('AminaRosol', 'Росул', 'Rosol', 'польский куриный бульон с вермишелью', 'Polish chicken broth with vermicelli', 'ROSÓŁ Z KURY', CH, ('scallion', 'egg'), 0),
    V('AminaBarszcz', 'Барщ', 'Barszcz', 'красный борщ с лапшой. Да, борщ быстрого приготовления существует', 'red borscht with noodles. Yes, instant borscht exists', 'BARSZCZ CZERWONY', '#a8183a', ('scallion', 'egg'), 0, c1='#ad1457', c2='#6a0032', ac='#fff176', dark=1),
    V('AminaZurek', 'Журек', 'Zurek', 'кислый ржаной суп с колбасой', 'sour rye soup with sausage', 'ŻUREK', '#e0d2a8', ('meat', 'egg', 'scallion'), 0, c1='#8d6e63', c2='#4e342e', ac='#fff8e1', dark=1),
    V('AminaPomidor', 'Помидорова', 'Pomidorowa', 'томатный суп', 'tomato soup', 'POMIDOROWA', TOM, ('scallion', 'cheese'), 0, c1='#e53935', c2='#b71c1c', ac='#ffffff', dark=1),
], dark=0)
brand('Pott', 'Потт Нудл', 'Pott Noodle', 'en', 'cupA', 'tallcup', 'POTT NOODLE', 'POTT NOODLE', '#2e7d32', '#1b5e20', '#ffeb3b', [
    V('PottChicken', 'Курица с грибами', 'Chicken & Mushroom', 'главная еда британского студента. Готовить не умеешь, чайник есть', 'the staple of the British student. Cannot cook, owns a kettle', 'CHICKEN & MUSHROOM', '#e0c890', ('mushroom', 'corn', 'scallion'), 0, band='#ffeb3b'),
    V('PottBeef', 'Говядина с томатом', 'Beef & Tomato', 'говядина с томатом', 'beef and tomato', 'BEEF & TOMATO', TOM, ('meat', 'corn', 'scallion'), 0, c1='#c62828', c2='#7f0000', band='#ffeb3b'),
    V('PottCurry', 'Карри', 'Original Curry', 'карри из закусочной', 'chip shop curry', 'ORIGINAL CURRY', CURRY, ('corn', 'scallion'), 1, c1='#f9a825', c2='#e65100', ac='#1b5e20', band='#1b5e20'),
    V('PottBadBoy', 'Бомбей Бэд Бой', 'Bombay Bad Boy', 'самый острый в линейке, по британским меркам', 'the hottest of the lot, by British standards', 'BOMBAY BAD BOY', '#b8401c', ('chili', 'corn', 'scallion'), 3, c1='#212121', c2='#000000', ac='#ff1744', band='#ff1744'),
    V('PottKing', 'Кинг Курица с грибами', 'King Chicken & Mushroom', 'то же самое, но больше', 'the same, only bigger', 'KING · CHICKEN & MUSHROOM', '#e0c890', ('mushroom', 'corn', 'scallion'), 0, shape='bigcup', band='#ffeb3b'),
])
brand('Terrina', 'Фюнф Минутен', 'Funf Minuten', 'en', 'cupB', 'cup', '5 Minuten', 'TERRINA · FERTIG IN 5 MINUTEN', '#d32f2f', '#8e0000', '#ffeb3b', [
    V('TerrinaRahm', 'Лапша в сливочном соусе', 'Nudeln in Rahmsauce', 'немецкая лапша в сливочном соусе с зеленью, без бульона', 'German noodles in a cream and herb sauce, no broth', 'NUDELN IN RAHMSAUCE', CREAM, ('scallion', 'cheese', 'mushroom'), 0, dry=1),
    V('TerrinaBolognese', 'Болоньезе', 'Bolognese', 'спагетти болоньезе из стакана, без бульона. Итальянцам не показывать', 'spaghetti bolognese from a cup, no broth. Do not show it to Italians', 'SPAGHETTI BOLOGNESE', TOM, ('meat', 'cheese'), 0, dry=1, c1='#2e7d32', c2='#1b5e20'),
    V('TerrinaHuhn', 'Куриный суп', 'Huhner-Nudeltopf', 'куриный суп с лапшой', 'chicken noodle soup', 'HÜHNER-NUDELTOPF', CH, ('meat', 'corn', 'scallion'), 0, c1='#f9a825', c2='#e65100', ac='#b71c1c'),
])
brand('Sopita', 'Ла Сопита', 'La Sopita', 'en', 'cupC', 'cup', 'La Sopita', 'SOPA INSTANTÁNEA', '#00897b', '#004d40', '#ffeb3b', [
    V('SopitaCamaron', 'Креветка, лайм и хабанеро', 'Shrimp, Lime & Habanero', 'мексиканский стакан: креветка, лайм и хабанеро. Сверху ещё льют острый соус', 'a Mexican cup: shrimp, lime and habanero. People add hot sauce on top', 'CAMARÓN, LIMÓN Y HABANERO', '#d8641e', ('shrimp', 'chili', 'corn', 'scallion'), 3, c1='#7cb342', c2='#33691e', ac='#ff3d00'),
    V('SopitaPiquin', 'Креветка с чили пикин', 'Shrimp & Chile Piquin', 'креветка с перцем пикин', 'shrimp with piquin chilli', 'CAMARÓN CON CHILE PIQUÍN', '#d0502a', ('shrimp', 'chili', 'scallion'), 2, c1='#e53935', c2='#8e0000'),
    V('SopitaPollo', 'Курица', 'Chicken', 'курица с овощами', 'chicken with vegetables', 'POLLO', CH, ('corn', 'scallion', 'meat'), 0, c1='#fbc02d', c2='#f57f17', ac='#004d40', dark=0),
    V('SopitaRes', 'Говядина', 'Beef', 'говядина', 'beef', 'RES', BF, ('meat', 'corn', 'scallion'), 0, c1='#6d4c41', c2='#3e2723'),
])
brand('AjiMen', 'Ахи-но-мэн', 'Aji-no-men', 'en', 'band', 'brick', 'Aji-no-men', 'SOPA INSTANTÁNEA · PERÚ', '#e53935', '#b71c1c', '#ffffff', [
    V('AjiMenGallina', 'Курица', 'Gallina', 'перуанский суп из курицы-несушки', 'Peruvian hen soup', 'SABOR A GALLINA', CH, ('egg', 'scallion', 'corn'), 0),
    V('AjiMenCarne', 'Говядина', 'Carne', 'говяжий бульон', 'beef broth', 'SABOR A CARNE', BF, ('meat', 'scallion'), 0, c1='#6d4c41', c2='#3e2723'),
    V('AjiMenPicante', 'Острая курица', 'Gallina Picante', 'курица с перцем ахи', 'hen with aji pepper', 'GALLINA PICANTE', '#d0802a', ('egg', 'chili', 'scallion'), 2, c1='#f9a825', c2='#e65100', ac='#b71c1c'),
])
brand('Miojo', 'Миожо', 'Miojo', 'en', 'split', 'brick', 'Miojo', 'MACARRÃO INSTANTÂNEO', '#fdd835', '#f57f17', '#c62828', [
    V('MiojoGalinha', 'Деревенская курица', 'Galinha Caipira', 'бразильская классика: деревенская курица. Готова за три минуты, как и обещано', 'the Brazilian classic: farmyard chicken. Ready in three minutes, as promised', 'GALINHA CAIPIRA', CH, ('egg', 'scallion', 'corn'), 0),
    V('MiojoCarne', 'Говядина', 'Carne', 'говядина', 'beef', 'CARNE', BF, ('meat', 'scallion'), 0, c1='#c62828', c2='#7f0000', ac='#fff176'),
    V('MiojoPicanha', 'Пиканья', 'Picanha', 'со вкусом стейка пиканья с углей. Стейка внутри нет', 'with the taste of chargrilled picanha steak. No steak inside', 'PICANHA', '#a8501e', ('meat', 'scallion', 'chili'), 1, c1='#3e2723', c2='#1b0f0b', ac='#ffca28', dark=1),
    V('MiojoTomate', 'Томат', 'Tomate', 'томатный', 'tomato', 'TOMATE', TOM, ('cheese', 'scallion'), 0, c1='#e53935', c2='#b71c1c', ac='#fff176', dark=1),
], dark=0)

# ------------------------------------------------------------------ flatten

def _jit(s, a):
    return 1 + ((zlib.crc32(s.encode()) % 1000) / 1000 - .5) * a


NOODLES = []
for b in B:
    for v in b['variants']:
        s = dict(b); s.pop('variants'); s.update(v)
        sh = s['shape']
        bru, ben = s.get('brand_ru', b['ru']), s.get('brand_en', b['en'])
        wr, we = WHERE.get(sh, ('', ''))
        s['ru'] = s.get('ru_full') or f"{s.get('prefix_ru', 'Удон' if 'удон' in s['ruf'][:12] else 'Лапша')} «{bru}{(' ' + s['vru']) if s['vru'] else ''}»{wr}"
        s['en'] = s.get('en_full') or f"{ben}{(' ' + s['ven']) if s['ven'] else ''}{we if we else ' noodles'}"
        kg, water, kcal = BASE[sh]
        dry = s.get('dry', 0)
        s.setdefault('kg', round(kg * _jit(s['id'], .12), 3))
        s.setdefault('water', water)
        s.setdefault('kcal', int(round(kcal * _jit(s['id'] + 'k', .14) / 10) * 10))
        s.setdefault('thirst', (-6 - 2 * s['spicy']) if dry else (24 - 3 * s['spicy']))
        s.setdefault('warm', round((1 + .4 * s['spicy']) * (.75 if dry else 1), 2))
        s.setdefault('dark', b.get('dark', 1))
        s['dry'] = dry
        s['brand'] = b['key']
        s['kind'] = 'pillow' if sh in SIZES else 'cup' if sh in CUPS else 'tray'
        s['vessel'] = None if s['kind'] == 'cup' else ('Pot' if s['lang'] == 'ko' else 'Plate' if dry else 'Donburi' if s['lang'] in ('ja', 'zh') else 'Bowl')
        s['mesh'] = f"{sh}{zlib.crc32(s['id'].encode()) % 3}" if s['kind'] == 'pillow' else sh
        s['hotmesh'] = sh + 'Hot' if s['kind'] == 'cup' else s['vessel'] + 'Hot'
        s['hottex'] = s['id'] if s['kind'] == 'cup' else s['id'] + 'Hot'
        NOODLES.append(s)
SNACKS = []
ids = [s['id'] for s in NOODLES]
ids2 = ids + [i + 'Hot' for i in ids]
assert len(ids2) == len(set(ids2)), [i for i in ids2 if ids2.count(i) > 1]
names = [s['ru'] for s in NOODLES]
dups = sorted({n for n in names if names.count(n) > 1})
assert not dups, dups

if __name__ == '__main__':
    import collections
    print(len(NOODLES), 'kinds,', len(B), 'brands')
    print(collections.Counter(s['lang'] for s in NOODLES), collections.Counter(s['shape'] for s in NOODLES))
    for s in NOODLES: print(f"{s['id']:18} {s['ru']:55} {s['en']:40} {s['kcal']} kcal w{s['warm']} t{s['thirst']} {s['vessel']}")

"""MatchboxesByEMPEROR catalogue (11-14): 12 series x 10 labels = 120 labels, every label with its own figure (game-icons.net, CC BY 3.0).
A series has one box kind and one label layout; inside a series every label has its own picture and name.
Row: (id, icon, RU name, EN name)."""

KINDS = {
    # id: (RU, EN, base prefab, matches, size mm (L, W, H), loot weight)
    'Std':      ('Спичечный коробок', 'Matchbox', 'GEAR_WoodMatches', 40, (50, 36, 14), 50),
    'Storm':    ('Охотничьи спички', 'Storm matches', 'GEAR_WoodMatches', 20, (60, 42, 20), 12),
    'Long':     ('Каминные спички', 'Fireplace matches', 'GEAR_WoodMatches', 25, (110, 52, 16), 12),
    'Book':     ('Спичечная книжечка', 'Matchbook', 'GEAR_PackMatches', 20, (50, 38, 6), 20),
    'Souvenir': ('Сувенирный коробок', 'Souvenir matchbox', 'GEAR_WoodMatches', 100, (100, 70, 22), 6),
}

SERIES = [
    # key, kind, layout, RU title, EN title, labels
    ('Beasts', 'Std', 'soviet', 'Звери Великого Медведя', 'Beasts of the Great Bear', [
        ('Wolf', 'wolf-head', 'Волк', 'Wolf'),
        ('PolarBear', 'polar-bear', 'Белый медведь', 'Polar bear'),
        ('Deer', 'deer', 'Олень', 'Deer'),
        ('Hare', 'rabbit', 'Заяц', 'Hare'),
        ('Fox', 'fox', 'Лиса', 'Fox'),
        ('Raven', 'raven', 'Ворон', 'Raven'),
        ('Beaver', 'beaver', 'Бобр', 'Beaver'),
        ('Lynx', 'lynx-head', 'Рысь', 'Lynx'),
        ('Owl', 'barn-owl', 'Сова', 'Owl'),
        ('Bison', 'bison', 'Бизон', 'Bison'),
    ]),
    ('Fire', 'Std', 'poster', 'Береги лес от пожара!', 'Protect the forest from fire!', [
        ('Campfire', 'campfire', 'Уходя, туши костёр', 'Put out your campfire'),
        ('Tree', 'burning-tree', 'Лес — наше богатство', 'The forest is our wealth'),
        ('Extinguisher', 'fire-extinguisher', 'Огнетушитель на месте', 'Keep the extinguisher handy'),
        ('Axe', 'fire-axe', 'Топор на пожарном щите', 'The axe belongs on the fire board'),
        ('Blaze', 'burning-forest', 'Огонь не щадит никого', 'Fire spares no one'),
        ('Patrol', 'fire-shield', 'Пожарный дозор', 'Fire watch'),
        ('MatchTip', 'match-tip', 'Спичка — не игрушка', 'A match is not a toy'),
        ('Grass', 'fire-zone', 'Не поджигай сухую траву', 'Never burn dry grass'),
        ('Water', 'water-drop', 'Вода сильнее огня', 'Water beats fire'),
        ('Camp', 'forest-camp', 'Привал без пожара', 'Make camp, not wildfire'),
    ]),
    ('Coast', 'Std', 'coast', 'Маяки и суда побережья', 'Lights and ships of the coast', [
        ('Lighthouse', 'lighthouse', 'Маяк', 'Lighthouse'),
        ('Sailboat', 'sailboat', 'Парусник', 'Sailboat'),
        ('Freighter', 'cargo-ship', 'Сухогруз', 'Freighter'),
        ('Anchor', 'anchor', 'Якорь', 'Anchor'),
        ('Helm', 'ship-wheel', 'Штурвал', 'Helm'),
        ('Seiner', 'fishing-boat', 'Сейнер', 'Fishing boat'),
        ('Iceberg', 'iceberg', 'Айсберг', 'Iceberg'),
        ('Whale', 'sperm-whale', 'Кашалот', 'Sperm whale'),
        ('Buoy', 'buoy', 'Бакен', 'Buoy'),
        ('Diver', 'diving-helmet', 'Водолаз', 'Diver'),
    ]),
    ('Sea', 'Std', 'fish', 'Обитатели северных вод', 'Creatures of northern waters', [
        ('Salmon', 'salmon', 'Лосось', 'Salmon'),
        ('Angler', 'angler-fish', 'Морской чёрт', 'Anglerfish'),
        ('Crab', 'crab', 'Камчатский краб', 'King crab'),
        ('Shrimp', 'shrimp', 'Северная креветка', 'Northern shrimp'),
        ('Squid', 'squid', 'Кальмар', 'Squid'),
        ('Octopus', 'octopus', 'Осьминог', 'Octopus'),
        ('Jellyfish', 'jellyfish', 'Медуза', 'Jellyfish'),
        ('Herring', 'fish-escape', 'Сельдь', 'Herring'),
        ('Starfish', 'sea-star', 'Морская звезда', 'Starfish'),
        ('Seahorse', 'seahorse', 'Морской конёк', 'Seahorse'),
    ]),
    ('Winter', 'Std', 'sport', 'Зимние игры', 'Winter games', [
        ('Skier', 'skier', 'Лыжные гонки', 'Cross-country skiing'),
        ('Skates', 'ice-skate', 'Фигурное катание', 'Figure skating'),
        ('Hockey', 'hockey', 'Хоккей', 'Ice hockey'),
        ('Snowboard', 'snowboard', 'Сноуборд', 'Snowboard'),
        ('Curling', 'curling-stone', 'Кёрлинг', 'Curling'),
        ('Skis', 'skis', 'Прыжки с трамплина', 'Ski jumping'),
        ('Snowman', 'snowman', 'Снежная баба', 'Snowman'),
        ('Climber', 'mountain-climbing', 'Альпинизм', 'Mountaineering'),
        ('Cup', 'trophy', 'Кубок чемпионов', 'Champions cup'),
        ('Medal', 'medal', 'Золотая медаль', 'Gold medal'),
    ]),
    ('Space', 'Std', 'cosmos', 'Дорога к звёздам', 'Road to the stars', [
        ('Rocket', 'rocket', 'Ракета', 'Rocket'),
        ('Sputnik', 'satellite', 'Спутник', 'Satellite'),
        ('Saturn', 'ringed-planet', 'Сатурн', 'Saturn'),
        ('Moon', 'moon-orbit', 'Луна', 'The Moon'),
        ('Cosmonaut', 'astronaut-helmet', 'Космонавт', 'Cosmonaut'),
        ('Meteor', 'falling-star', 'Метеор', 'Meteor'),
        ('Comet', 'comet-spark', 'Комета', 'Comet'),
        ('Telescope', 'telescope', 'Телескоп', 'Telescope'),
        ('Shuttle', 'space-shuttle', 'Космический челнок', 'Space shuttle'),
        ('Observatory', 'observatory', 'Обсерватория', 'Observatory'),
    ]),
    ('Brands', 'Std', 'brand', 'Спички со всего света', 'Matches of the world', [
        ('SwenVesta', 'swan', 'Swen Vesta', 'Swen Vesta'),
        ('Diamund', 'cut-diamond', 'Diamund', 'Diamund'),
        ('EnglundsGlory', 'battleship', "Englund's Glory", "Englund's Glory"),
        ('BrayantMae', 'beehive', 'Brayant & Mae', 'Brayant & Mae'),
        ('Caravella', 'caravel', 'Caravella', 'Caravella'),
        ('LyonBrand', 'lion', 'Lyon Brand', 'Lyon Brand'),
        ('Tigerr', 'tiger-head', 'Tigerr', 'Tigerr'),
        ('GentlemansChoice', 'top-hat', "Gentleman's Choice", "Gentleman's Choice"),
        ('RedRooster', 'rooster', 'Red Rooster', 'Red Rooster'),
        ('Jumbo', 'elephant', 'Jumbo Safety', 'Jumbo Safety'),
    ]),
    ('Japan', 'Book', 'japan', 'Бары и чайные Токио', 'Tokyo bars and tea rooms', [
        ('Hashi', 'japanese-bridge', 'Bar Hashi', 'Bar Hashi'),
        ('Chochin', 'paper-lantern', 'Chochin', 'Chochin'),
        ('Pagoda', 'pagoda', 'Hotel Pagoda', 'Hotel Pagoda'),
        ('Bonsai', 'bonsai-tree', 'Bonsai Club', 'Bonsai Club'),
        ('Kimono', 'kimono', 'Kimono Lounge', 'Kimono Lounge'),
        ('Sushi', 'sushis', 'Sushi Tora', 'Sushi Tora'),
        ('Tsuru', 'crane', 'Kissa Tsuru', 'Kissa Tsuru'),
        ('Katana', 'katana', 'Katana Bar', 'Katana Bar'),
        ('Kabuto', 'samurai-helmet', 'Kabuto', 'Kabuto'),
        ('Hasu', 'lotus-flower', 'Hasu Tea', 'Hasu Tea'),
    ]),
    ('Hunt', 'Storm', 'hunter', 'Охотничьи штормовые', 'Hunter storm matches', [
        ('Tracks', 'deer-track', 'По следу', 'On the trail'),
        ('Trap', 'wolf-trap', 'Капкан', 'Leghold trap'),
        ('Tent', 'camping-tent', 'Ночёвка', 'Night camp'),
        ('Pass', 'mountains', 'Перевал', 'Mountain pass'),
        ('Horn', 'hunting-horn', 'Охотничий рог', 'Hunting horn'),
        ('Crossbow', 'crossbow', 'Арбалет', 'Crossbow'),
        ('Bow', 'bow-arrow', 'Лук и стрелы', 'Bow and arrow'),
        ('Binoculars', 'binoculars', 'Бинокль', 'Binoculars'),
        ('Rod', 'fishing-pole', 'Удочка', 'Fishing rod'),
        ('Duck', 'duck', 'Утиная охота', 'Duck hunt'),
    ]),
    ('Hearth', 'Long', 'hearth', 'Уютный вечер', 'Hearthside evenings', [
        ('Candles', 'candle-holder', 'Подсвечник', 'Candlestick'),
        ('Fireplace', 'fireplace', 'Камин', 'Fireplace'),
        ('Rocker', 'rocking-chair', 'Кресло-качалка', 'Rocking chair'),
        ('Cat', 'cat', 'Кот у огня', 'Cat by the fire'),
        ('Lantern', 'old-lantern', 'Старый фонарь', 'Old lantern'),
        ('Armchair', 'armchair', 'Кресло', 'Armchair'),
        ('Book', 'book-cover', 'Книга', 'A good book'),
        ('Cuckoo', 'cuckoo-clock', 'Часы с кукушкой', 'Cuckoo clock'),
        ('Coffee', 'coffee-cup', 'Чашка кофе', 'Cup of coffee'),
        ('Violin', 'violin', 'Скрипка', 'Violin'),
    ]),
    ('Regions', 'Souvenir', 'souvenir', 'Привет с Великого Медведя', 'Greetings from Great Bear', [
        ('MysteryLake', 'boat-fishing', 'Таинственное озеро', 'Mystery Lake'),
        ('CoastalHighway', 'tow-truck', 'Прибрежное шоссе', 'Coastal Highway'),
        ('PleasantValley', 'windmill', 'Тихая долина', 'Pleasant Valley'),
        ('Timberwolf', 'mountaintop', 'Гора Тимбервульф', 'Timberwolf Mountain'),
        ('BrokenRailroad', 'steam-locomotive', 'Разрушенная железная дорога', 'Broken Railroad'),
        ('HushedRiver', 'waterfall', 'Долина Тихой реки', 'Hushed River Valley'),
        ('BleakInlet', 'factory', 'Унылый залив', 'Bleak Inlet'),
        ('AshCanyon', 'rope-bridge', 'Пепельный каньон', 'Ash Canyon'),
        ('Muskeg', 'frog', 'Забытое болото', 'Forlorn Muskeg'),
        ('MountainTown', 'church', 'Горный городок', 'Mountain Town'),
    ]),
    ('Diners', 'Book', 'diner', 'Закусочные и клубы', 'Diners and night clubs', [
        ('Martini', 'martini', 'Martini Lounge', 'Martini Lounge'),
        ('BigJoes', 'hamburger', "Big Joe's Burgers", "Big Joe's Burgers"),
        ('HotDog', 'hot-dog', 'Hot Dog Hut', 'Hot Dog Hut'),
        ('Luigis', 'pizza-slice', "Luigi's Pizza", "Luigi's Pizza"),
        ('NightOwl', 'coffee-mug', 'Night Owl Coffee', 'Night Owl Coffee'),
        ('Bierhaus', 'beer-stein', 'Bierhaus', 'Bierhaus'),
        ('ClubMic', 'microphone', 'Club Mic', 'Club Mic'),
        ('StrikeLanes', 'bowling-pin', 'Strike Lanes', 'Strike Lanes'),
        ('RoyalFlush', 'poker-hand', 'Royal Flush', 'Royal Flush'),
        ('BlueSax', 'saxophone', 'Blue Sax', 'Blue Sax'),
    ]),
]

# Icons already used as figures elsewhere in our mods (Smokes lighter engravings): never on a label.
TAKEN = {'wolf-howl', 'bear-head', 'eagle-head', 'maple-leaf', 'crowned-skull', 'card-ace-spades'}


def labels():
    """every label: dict(id, series, kind, layout, icon, ru, en, n (1-based in series))"""
    out = []
    for key, kind, layout, sru, sen, rows in SERIES:
        for i, (lid, icon, ru, en) in enumerate(rows):
            out.append(dict(id=key + '_' + lid, series=key, kind=kind, layout=layout, icon=icon, ru=ru, en=en, n=i + 1,
                            sru=sru, sen=sen, total=len(rows)))
    return out


def check():
    ls = labels()
    icons = [l['icon'] for l in ls]
    dup = {i for i in icons if icons.count(i) > 1}
    assert not dup, dup
    assert not (set(icons) & TAKEN), set(icons) & TAKEN
    ids = [l['id'] for l in ls]
    assert len(ids) == len(set(ids))
    return len(ls)


if __name__ == '__main__':
    print(check(), 'labels, all figures unique')

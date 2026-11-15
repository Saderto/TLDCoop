"""0.4: brands that were still missing, with their own flavours. Rows for catalog.G (see catalog.py)."""


def rows(G):
    return [
    # ---------------------------------------------------------------- USA
    G('SanChips', 'US', 'crisps', 'SanChips', 'Сан Чипс', 'SanChips', 'window', 'comfortaa', 'solid', ('#ffffff', '#ffd43b'), 'none', 184, 'MULTIGRAIN SNACKS', [
        ('Original', 'оригинальные', 'original', 'ORIGINAL', '#1c7ed6'), ('Cheddar', 'чеддер', 'harvest cheddar', 'HARVEST CHEDDAR', '#e8590c'),
        ('Salsa', 'садовая сальса', 'garden salsa', 'GARDEN SALSA', '#c1121f'), ('Onion', 'французский лук', 'french onion', 'FRENCH ONION', '#2b8a3e')], food='cracker'),
    G('Ketl', 'US', 'crisps', 'KETL', 'Кетл', 'Ketl', 'frame', 'bebas', 'solid', ('#ffffff', '#ffffff'), 'none', 142, 'KETTLE COOKED POTATO CHIPS', [
        ('Salt', 'морская соль', 'sea salt', 'SEA SALT', '#1971c2'), ('Vinegar', 'соль и уксус', 'sea salt & vinegar', 'SEA SALT & VINEGAR', '#0b7285'),
        ('Dijon', 'мёд и дижонская горчица', 'honey dijon', 'HONEY DIJON', '#b8860b'), ('Jalapeno', 'халапеньо', 'jalapeno', 'JALAPEÑO', '#2b8a3e'),
        ('BBQ', 'барбекю', 'backyard barbeque', 'BACKYARD BARBEQUE', '#a61e1e')]),
    G('Tostitoz', 'US', 'tortilla', 'Tostitoz', 'Тоститоз', 'Tostitoz', 'badge', 'lobster', 'brand', ('#12264d', '#ffd43b'), 'none', 283, 'TORTILLA CHIPS', [
        ('Restaurant', 'ресторанные', 'restaurant style', 'RESTAURANT STYLE', '#ffd43b'), ('Scoops', 'ложечки', 'scoops', 'SCOOPS!', '#74c0fc'), ('Lime', 'с лаймом', 'hint of lime', 'HINT OF LIME', '#8ce99a')]),
    G('Fanyunz', 'US', 'rings', 'Fanyunz', 'Фаньюнз', 'Fanyunz', 'burst', 'alfa', 'solid', ('#ffffff', '#ffd43b'), 'none', 163, 'ONION FLAVORED RINGS', [
        ('Original', 'оригинальные', 'original', 'ORIGINAL', '#2b8a3e'), ('Hot', 'огненные', 'flamin hot', 'FLAMIN HOT', '#c1121f'), ('Steak', 'стейкхаус', 'steakhouse onion', 'STEAKHOUSE ONION', '#5c2e0e')]),
    G('Goldfysh', 'US', 'cracker', 'Goldfysh', 'Голдфиш', 'Goldfysh', 'window', 'lobster', 'solid', ('#ffffff', '#ffd43b'), 'none', 187, 'BAKED SNACK CRACKERS', [
        ('Cheddar', 'чеддер', 'cheddar', 'CHEDDAR', '#e8590c'), ('Pizza', 'пицца', 'pizza', 'PIZZA', '#c1121f'),
        ('Pretzel', 'крендель', 'pretzel', 'PRETZEL', '#7a4a1e'), ('Parmesan', 'пармезан', 'parmesan', 'PARMESAN', '#2b8a3e')], shape='bag', size=(.14, .23, .06), food='fish'),
    G('ChizIt', 'US', 'cracker', 'CHIZ-IT', 'Чиз-Ит', 'Chiz-It', 'big', 'alfa', 'solid', ('#ffffff', '#ffd43b'), 'none', 350, 'BAKED SNACK CRACKERS', [
        ('Original', 'оригинальные', 'original', 'ORIGINAL', '#c1121f'), ('White', 'белый чеддер', 'white cheddar', 'WHITE CHEDDAR', '#1c5fb8'),
        ('Toasty', 'сильно поджаренные', 'extra toasty', 'EXTRA TOASTY', '#7a2e0e'), ('Hot', 'острые', 'hot & spicy', 'HOT & SPICY', '#e8590c')], size=(.16, .06, .23)),
    G('Komboz', 'US', 'pretzel', 'KOMBOZ', 'Комбоз', 'Komboz', 'diag', 'inter_black', 'solid', ('#1a1a1a', '#ffd43b'), 'none', 178, 'STUFFED SNACKS', [
        ('Cheddar', 'крендель с чеддером', 'cheddar cheese pretzel', 'CHEDDAR CHEESE PRETZEL', '#c1121f'), ('Pizza', 'пиццерия', 'pizzeria pretzel', 'PIZZERIA PRETZEL', '#2b8a3e'),
        ('Buffalo', 'соус баффало', 'buffalo blue cheese', 'BUFFALO BLUE CHEESE', '#1c5fb8')], food='roll'),
    G('CheksMix', 'US', 'mix', 'Cheks Mix', 'Чекс Микс', 'Cheks Mix', 'stripes', 'alfa', 'brand', ('#1c5fb8', '#ffd43b'), 'none', 248, 'SNACK MIX', [
        ('Traditional', 'традиционный', 'traditional', 'TRADITIONAL', '#ffd43b'), ('Bold', 'смелый', 'bold party blend', 'BOLD PARTY BLEND', '#ff8787'), ('Cheddar', 'чеддер', 'cheddar', 'CHEDDAR', '#ffa94d')]),
    G('Smartfud', 'US', 'popcorn', 'Smartfud', 'Смартфуд', 'Smartfud', 'window', 'comfortaa', 'dark', ('#161616', '#ffd43b'), 'none', 155, 'POPCORN', [
        ('White', 'белый чеддер', 'white cheddar', 'WHITE CHEDDAR', '#f8f9fa'), ('Kettle', 'сладко-солёный', 'kettle corn', 'KETTLE CORN', '#ffa94d'), ('Butter', 'как в кино', 'movie theater butter', 'MOVIE THEATER BUTTER', '#ffd43b')],
      shape='bag', size=(.20, .29, .09), thirst=-5),
    G('Wanderful', 'US', 'nuts', 'Wanderful', 'Уандерфул', 'Wanderful', 'big', 'inter_black', 'dark', ('#111111', '#9ad14b'), 'none', 170, 'PISTACHIOS', [
        ('Salted', 'жареные солёные', 'roasted & salted', 'ROASTED & SALTED', '#9ad14b'), ('Chili', 'чили и лайм', 'chili roasted', 'CHILI ROASTED', '#ff6b6b'),
        ('Pepper', 'соль и перец', 'salt & pepper', 'SALT & PEPPER', '#ced4da'), ('NoShells', 'очищенные', 'no shells', 'NO SHELLS', '#ffd43b')], shape='bag', size=(.15, .22, .05), name_ru='Фисташки'),
    G('BluDaimond', 'US', 'nuts', 'Blu Daimond', 'Блю Даймонд', 'Blu Daimond', 'frame', 'playfair_l', 'solid', ('#ffffff', '#ffd43b'), 'none', 170, 'ALMONDS', [
        ('Smoke', 'с дымком', 'smokehouse', 'SMOKEHOUSE', '#5c2e0e'), ('Wasabi', 'васаби и соевый соус', 'wasabi & soy sauce', 'WASABI & SOY SAUCE', '#2b8a3e'),
        ('Vinegar', 'соль и уксус', 'salt n vinegar', 'SALT N VINEGAR', '#1c7ed6'), ('Honey', 'в меду', 'honey roasted', 'HONEY ROASTED', '#b8860b')], size=(.04, .11), name_ru='Миндаль'),
    G('JekLinks', 'US', 'jerky', 'Jek Links', 'Джек Линкс', 'Jek Links', 'badge', 'alfa', 'dark', ('#161616', '#f2b705'), 'none', 80, 'BEEF JERKY', [
        ('Original', 'оригинальная', 'original', 'ORIGINAL', '#f2b705'), ('Teriyaki', 'терияки', 'teriyaki', 'TERIYAKI', '#40c057'), ('Peppered', 'с перцем', 'peppered', 'PEPPERED', '#e03131')]),
    # ---------------------------------------------------------------- Canada
    G('Hampti', 'CA', 'crisps', 'Hampti Dampti', 'Хампти Дампти', 'Hampti Dampti', 'big', 'lobster', 'solid', ('#ffffff', '#ffd43b'), 'none', 245, 'POTATO CHIPS · CROUSTILLES', [
        ('BBQ', 'барбекю', 'bar-b-q', 'BAR-B-Q', '#a61e1e'), ('Ketchup', 'кетчуп', 'ketchup', 'KETCHUP', '#e03131'),
        ('Clam', 'сметана и моллюски', 'sour cream & clam', 'SOUR CREAM & CLAM', '#1c7ed6'), ('Party', 'смесь для вечеринки', 'party mix', 'PARTY MIX', '#7048e8')]),
    G('Koverd', 'CA', 'crisps', 'Koverd Bridj', 'Коверд Бридж', 'Koverd Bridj', 'retro', 'playfair_l', 'light', ('#f4ecd8', '#7a1f1f'), 'mill', 170, 'OLD FASHIONED KETTLE CHIPS · NEW BRUNSWICK', [
        ('Salt', 'морская соль', 'sea salt', 'SEA SALT', '#1f3a68'), ('Storm', 'штормовые: всё сразу', 'storm chips', 'STORM CHIPS', '#3a3a3a'),
        ('Dill', 'сливочный укроп', 'creamy dill', 'CREAMY DILL', '#2b5d2b'), ('BBQ', 'сладкое копчёное барбекю', 'smokin sweet bbq', 'SMOKIN SWEET BBQ', '#7a1f1f')]),
    G('YamYam', 'CA', 'crisps', 'Yam Yam', 'Ям Ям', 'Yam Yam', 'stripes', 'pacifico', 'brand', ('#f2b705', '#c1121f'), 'none', 150, 'CROUSTILLES DU QUÉBEC', [
        ('Nature', 'с солью', 'plain', 'NATURE', '#c1121f'), ('Ketchup', 'кетчуп', 'ketchup', 'KETCHUP', '#a61e4d'), ('Vinaigre', 'уксус', 'vinegar', 'VINAIGRE', '#1c5fb8')]),
    # ---------------------------------------------------------------- Russia, USSR, neighbours
    G('Nasha', 'RU', 'crisps', 'НАША КАРТОШКА', 'Наша картошка', 'Nasha Kartoshka', 'band', 'inter_black', 'solid', ('#ffffff', '#ffd43b'), 'sunring', 80, 'ЧИПСЫ-ПЛАСТИНКИ', [
        ('Sour', 'сметана и лук', 'sour cream & onion', 'СМЕТАНА И ЛУК', '#2b8a3e'), ('Bacon', 'бекон', 'bacon', 'БЕКОН', '#c1121f'),
        ('Crab', 'краб', 'crab', 'КРАБ', '#e8590c'), ('Cheese', 'сыр', 'cheese', 'СЫР', '#f2b705')], lang='ru'),
    G('Sukhoplav', 'RU', 'fish', 'СУХОПЛАВ', 'Сухоплав', 'Sukhoplav', 'diag', 'inter_black', 'solid', ('#0b2545', '#f4d35e'), 'none', 70, 'РЫБКА К ПЕННОМУ', [
        ('Vobla', 'вобла', 'roach', 'ВОБЛА', '#1864ab'), ('Smelt', 'корюшка', 'smelt', 'КОРЮШКА', '#0b7285'),
        ('Rings', 'кольца кальмара', 'squid rings', 'КОЛЬЦА КАЛЬМАРА', '#862e9c'), ('Pike', 'щучка', 'pike straws', 'ЩУЧКА', '#2b6b1f')], lang='ru'),
    G('Blyuz', 'RU', 'nuts', 'Блюз', 'Блюз', 'Blyuz', 'frame', 'ptserif_bi', 'dark', ('#14142b', '#f1c232'), 'none', 130, 'ОТБОРНЫЕ ОРЕХИ', [
        ('Pistachio', 'фисташки', 'pistachios', 'ФИСТАШКИ', '#9ad14b'), ('Almond', 'миндаль', 'almonds', 'МИНДАЛЬ', '#e8a15c'),
        ('Cashew', 'кешью', 'cashews', 'КЕШЬЮ', '#f1c232'), ('Hazelnut', 'фундук', 'hazelnuts', 'ФУНДУК', '#c98b3c')], lang='ru', shape='bag', size=(.13, .20, .04)),
    G('Kupecheskie', 'RU', 'rusk', 'Купеческие', 'Купеческие', 'Kupecheskie', 'retro', 'oldstd', 'light', ('#f5ecd6', '#6b2d0f'), 'wheat', 250, 'СУХАРИ СДОБНЫЕ', [
        ('Raisin', 'с изюмом', 'with raisins', 'С ИЗЮМОМ', '#6b2d0f'), ('Poppy', 'с маком', 'with poppy seeds', 'С МАКОМ', '#2f3542'),
        ('Vanilla', 'ванильные', 'vanilla', 'ВАНИЛЬНЫЕ', '#9c6a00'), ('Sugar', 'сахарные', 'sugared', 'САХАРНЫЕ', '#a61e4d')], lang='ru'),
    G('YuzhnayaNoch', 'SU', 'drazhe', 'ЮЖНАЯ НОЧЬ', 'Южная ночь', 'Yuzhnaya Noch', 'retro', 'oldstd', 'light', ('#efe6cf', '#1f2a5c'), 'starring', 150, 'ДРАЖЕ · АРАХИС', [
        ('Sugar', 'арахис в сахаре', 'sugared peanuts', 'В САХАРЕ', '#a61e4d'), ('Cocoa', 'арахис в какао', 'cocoa peanuts', 'В КАКАО', '#5c2e0e'), ('Sesame', 'арахис в кунжуте', 'sesame peanuts', 'В КУНЖУТЕ', '#8a6d00')], lang='ru'),
    G('Flink', 'UA', 'croutons', 'ФЛІНК', 'Флинк', 'Flink', 'diag', 'inter_black', 'solid', ('#1a1a1a', '#ffd43b'), 'none', 70, 'СУХАРИКИ ПШЕНИЧНО-ЖИТНІ', [
        ('Crab', 'краб', 'crab', 'КРАБ', '#e8590c'), ('Bacon', 'бекон', 'bacon', 'БЕКОН', '#c1121f'),
        ('Aspic', 'холодец с хреном', 'aspic with horseradish', 'ХОЛОДЕЦЬ З ХРІНОМ', '#2b8a3e'), ('Sour', 'сметана с зеленью', 'sour cream & herbs', 'СМЕТАНА ІЗ ЗЕЛЕННЮ', '#1c7ed6')]),
    G('Anega', 'BY', 'crisps', 'АНЕГА', 'Анега', 'Anega', 'burst', 'inter_black', 'solid', ('#ffffff', '#ffd43b'), 'none', 75, 'ЧЫПСЫ БУЛЬБЯНЫЯ', [
        ('Bacon', 'бекон', 'bacon', 'БЕКОН', '#a61e1e'), ('Sour', 'сметана и лук', 'sour cream & onion', 'СМЯТАНА І ЦЫБУЛЯ', '#2b8a3e'), ('Crab', 'краб', 'crab', 'КРАБ', '#e8590c')]),
    # ---------------------------------------------------------------- Japan
    G('Umaibou', 'JP', 'puffs', 'うまい坊', 'Умайбо', 'Umaibou', 'big', 'cjk', 'solid', ('#ffffff', '#ffd43b'), 'none', 6, 'コーンスナック', [
        ('Potage', 'кукурузный суп', 'corn potage', 'コーンポタージュ', '#f2b705'), ('Cheese', 'сыр', 'cheese', 'チーズ', '#e8590c'), ('Mentai', 'икра минтая', 'mentaiko', 'めんたい', '#e64980'),
        ('Takoyaki', 'такояки', 'takoyaki', 'たこ焼', '#7a2e0e'), ('Teriyaki', 'терияки-бургер', 'teriyaki burger', 'てりやきバーガー', '#2b8a3e')], size=(.045, .14, .03), food='stick'),
    G('Puretsu', 'JP', 'sticks', 'Puretsu', 'Пурэцу', 'Puretsu', 'band', 'inter_black', 'solid', ('#ffffff', '#ffd43b'), 'none', 69, 'プリッツ', [
        ('Salad', 'салат', 'salad', 'サラダ', '#2b8a3e'), ('Tomato', 'томат', 'tomato', '熟トマト', '#c1121f'), ('Butter', 'жареное масло', 'roast butter', 'ローストバター', '#b8860b')], size=(.08, .024, .155)),
    G('Karamucho', 'JP', 'crisps', 'カラムーチャ', 'Карамуча', 'Karamucha', 'triangle', 'cjk', 'solid', ('#111111', '#ffd43b'), 'flame', 55, 'ポテトチップス', [
        ('Chili', 'острый чили', 'hot chili', 'ホットチリ味', '#c1121f'), ('Ume', 'кислая слива', 'sour plum', 'すっぱムーチャ 梅', '#a61e4d'), ('Nori', 'нори и соль', 'seaweed salt', 'のり塩', '#2b8a3e')]),
    G('HappiTan', 'JP', 'rice', 'ハッピータン', 'Хаппи Тан', 'Happi Tan', 'window', 'cjk', 'solid', ('#ffffff', '#c1121f'), 'none', 96, 'おせんべい', [
        ('Original', 'сладко-солёные', 'sweet & salty', 'あまじょっぱい', '#f08c00'), ('Powder', 'двойная посыпка', 'double powder', 'パウダー250%', '#c1121f'), ('Butter', 'с маслом', 'butter', 'バター味', '#f2b705')], food='slab'),
    G('KyarameruKon', 'JP', 'puffs', 'キャラメルコン', 'Карамель Кон', 'Kyarameru Kon', 'burst', 'cjk', 'solid', ('#ffffff', '#ffd43b'), 'none', 70, 'CARAMEL CORN', [
        ('Original', 'карамель', 'caramel', 'キャラメル', '#c1121f'), ('Almond', 'миндаль', 'almond', 'アーモンド', '#7a4a1e'), ('Matcha', 'матча', 'matcha', '抹茶', '#5c940d')], thirst=-2),
    # ---------------------------------------------------------------- Korea
    G('PokaChip', 'KR', 'crisps', '포카침', 'Пока Чип', 'Poka Chip', 'band', 'cjk', 'solid', ('#ffffff', '#ffd43b'), 'none', 66, 'POTATO CHIP', [
        ('Original', 'оригинальные', 'original', '오리지널', '#1c5fb8'), ('Onion', 'лук', 'onion', '어니언맛', '#2b8a3e'), ('Cheese', 'сладкий сыр', 'sweet cheese', '스윗치즈맛', '#f08c00')]),
    G('Kkobuk', 'KR', 'puffs', '꼬북침', 'Ккобук Чип', 'Kkobuk Chip', 'asian', 'cjk', 'solid', ('#ffffff', '#ffd43b'), 'none', 80, '네겹 스낵', [
        ('Corn', 'кукурузный суп', 'corn soup', '콘스프맛', '#f2b705'), ('Choco', 'шоколадные чуррос', 'choco churros', '초코츄러스맛', '#5c2e0e'), ('Vanilla', 'сладкая ваниль', 'sweet vanilla', '달콩인절미', '#b8860b')], food='ridged'),
    G('BananaKick', 'KR', 'puffs', '바나나콕', 'Банана Кок', 'Banana Kok', 'window', 'cjk', 'solid', ('#ffffff', '#7a4a00'), 'none', 75, 'BANANA SNACK', [
        ('Banana', 'банан', 'banana', '바나나', '#f2c200'), ('Strawberry', 'клубника', 'strawberry', '딸기', '#f06595'), ('Choco', 'шоколад', 'chocolate', '초코', '#7a4a1e')], food='flip', thirst=-2),
    G('Yangpa', 'KR', 'rings', '양파링', 'Янпарин', 'Yangparing', 'badge', 'cjk', 'brand', ('#2b8a3e', '#ffd43b'), 'none', 84, 'ONION RINGS', [
        ('Original', 'оригинальные', 'original', '오리지널', '#ffd43b'), ('Spicy', 'острые', 'spicy', '매콤한맛', '#ff8787'), ('Garlic', 'чеснок', 'garlic', '마늘맛', '#f8f9fa')]),
    # ---------------------------------------------------------------- China
    G('Leshi', 'CN', 'crisps', '乐士', 'Лэши', 'Leshi', 'burst', 'cjk', 'solid', ('#ffffff', '#ffd43b'), 'none', 70, '薯片', [
        ('Cucumber', 'огурец', 'cucumber', '黄瓜味', '#2f9e44'), ('Crayfish', 'острые раки', 'spicy crayfish', '小龙虾味', '#c1121f'),
        ('Tomato', 'томат', 'tomato', '番茄味', '#e8590c'), ('BBQ', 'шашлык', 'barbecue', '烧烤味', '#7a2e0e')]),
    G('Shanghaojia', 'CN', 'shrimp', '上好嘉', 'Шанхаоцзя', 'Shanghaojia', 'big', 'cjk', 'solid', ('#ffffff', '#ffd43b'), 'none', 80, '膨化食品', [
        ('Shrimp', 'креветочные', 'prawn crackers', '鲜虾片', '#e8590c'), ('Onion', 'луковые кольца', 'onion rings', '洋葱圈', '#2b8a3e'), ('Garden', 'овощные', 'garden puffs', '田园泡', '#1c7ed6')]),
    G('SanzhiHuli', 'CN', 'nuts', '三只狐狸', 'Три лисы', 'Three Foxes', 'frame', 'cjk', 'solid', ('#ffffff', '#ffd43b'), 'none', 160, '坚果', [
        ('Pecan', 'пекан', 'pecans', '碧根果', '#7a4a1e'), ('Macadamia', 'макадамия', 'macadamia', '夏威夷果', '#b8860b'), ('Pistachio', 'фисташки', 'pistachios', '开心果', '#5c940d')],
      shape='bag', size=(.15, .21, .05)),
    # ---------------------------------------------------------------- United Kingdom
    G('HulaHupz', 'GB', 'rings', 'Hula Hupz', 'Хула Хупс', 'Hula Hupz', 'stripes', 'alfa', 'brand', ('#c1121f', '#ffd43b'), 'none', 34, 'POTATO RINGS', [
        ('Original', 'с солью', 'original', 'ORIGINAL', '#ffd43b'), ('Beef', 'говядина барбекю', 'bbq beef', 'BBQ BEEF', '#ffa94d'),
        ('Vinegar', 'соль и уксус', 'salt & vinegar', 'SALT & VINEGAR', '#74c0fc'), ('Cheese', 'сыр и лук', 'cheese & onion', 'CHEESE & ONION', '#8ce99a')], size=(.13, .17, .05)),
    G('Wotsitz', 'GB', 'puffs', 'Wotsitz', 'Вотситс', 'Wotsitz', 'diag', 'lobster', 'solid', ('#12264d', '#ffd43b'), 'none', 60, 'BAKED CORN PUFFS', [
        ('Cheesy', 'очень сырные', 'really cheesy', 'REALLY CHEESY', '#f08c00'), ('Hot', 'огненные', 'flamin hot', 'FLAMIN HOT', '#c1121f'), ('Bacon', 'бекон', 'sizzling bacon', 'SIZZLING BACON', '#a61e4d')], food='flip'),
    G('Kwaverz', 'GB', 'crisps', 'Kwaverz', 'Квейверс', 'Kwaverz', 'window', 'pacifico', 'solid', ('#ffffff', '#ffd43b'), 'none', 54, 'CURLY POTATO SNACK', [
        ('Cheese', 'сыр', 'cheese', 'CHEESE', '#f2b705'), ('Prawn', 'креветочный коктейль', 'prawn cocktail', 'PRAWN COCKTAIL', '#e64980'), ('Vinegar', 'соль и уксус', 'salt & vinegar', 'SALT & VINEGAR', '#1c7ed6')], food='curl'),
    G('MakKoys', 'GB', 'ridged', 'MakKoy’s', 'МакКойз', 'MakKoy’s', 'big', 'alfa', 'dark', ('#111111', '#ffd43b'), 'none', 45, 'RIDGE CUT CRISPS', [
        ('Steak', 'стейк на огне', 'flame grilled steak', 'FLAME GRILLED STEAK', '#e03131'), ('Vinegar', 'соль и солодовый уксус', 'salt & malt vinegar', 'SALT & MALT VINEGAR', '#4dabf7'),
        ('Cheddar', 'чеддер и лук', 'cheddar & onion', 'CHEDDAR & ONION', '#51cf66'), ('Thai', 'тайская сладкая курица', 'thai sweet chicken', 'THAI SWEET CHICKEN', '#ffa94d')], size=(.14, .19, .05)),
    # ---------------------------------------------------------------- Germany
    G('Lorentz', 'DE', 'ridged', 'Kranchips', 'Кранчипс', 'Kranchips', 'burst', 'inter_black', 'solid', ('#ffffff', '#ffd43b'), 'none', 150, 'KNUSPRIGE KARTOFFELCHIPS', [
        ('Paprika', 'паприка', 'paprika', 'PAPRIKA', '#c1121f'), ('Western', 'вестерн', 'western style', 'WESTERN STYLE', '#7a2e0e'),
        ('Cheese', 'сыр и лук', 'cheese & onion', 'CHEESE & ONION', '#2b8a3e'), ('Salted', 'с солью', 'salted', 'GESALZEN', '#1c5fb8')]),
    G('PomBer', 'DE', 'crisps', 'Pom-Bär', 'Пом-Бер', 'Pom-Bär', 'badge', 'comfortaa', 'brand', ('#f2b705', '#c1121f'), 'bear', 75, 'KARTOFFELSNACK', [
        ('Original', 'оригинальные', 'original', 'ORIGINAL', '#c1121f'), ('Ketchup', 'кетчуп', 'ketchup', 'KETCHUP', '#a61e4d'), ('Sour', 'сметана', 'sour cream', 'SOUR CREAM', '#2b8a3e')], food='paw'),
    G('NikNaks', 'DE', 'coated', 'NikNak’s', 'НикНакс', 'NikNak’s', 'diag', 'alfa', 'solid', ('#1a1a1a', '#ffd43b'), 'none', 125, 'ERDNÜSSE IM TEIGMANTEL', [
        ('Original', 'паприка', 'original paprika', 'ORIGINAL', '#c1121f'), ('Double', 'двойной хруст', 'double crunch', 'DOUBLE CRUNCH', '#e8590c'), ('BBQ', 'барбекю', 'barbecue', 'BBQ', '#5c2e0e')]),
    G('Ultche', 'DE', 'nuts', 'ültche', 'Ильтье', 'Ültche', 'band', 'inter_black', 'solid', ('#ffffff', '#ffd43b'), 'nut', 200, 'ERDNÜSSE', [
        ('Salted', 'солёный', 'salted', 'GESALZEN', '#0b3d91'), ('Plain', 'без соли', 'unsalted', 'UNGESALZEN', '#2b8a3e'), ('Spicy', 'пикантный', 'spicy', 'PIKANT GEWÜRZT', '#c1121f')]),
    # ---------------------------------------------------------------- Mexico, Brazil
    G('Barsel', 'MX', 'crisps', 'Chips Barsel', 'Чипс Барсель', 'Chips Barsel', 'triangle', 'bebas', 'solid', ('#111111', '#ffd43b'), 'flame', 170, 'PAPAS FRITAS EN OLLA', [
        ('Fuego', 'огонь', 'fuego', 'FUEGO', '#6a1b9a'), ('Jalapeno', 'халапеньо', 'jalapeno', 'JALAPEÑO', '#2b8a3e'),
        ('Sal', 'морская соль', 'sea salt', 'SAL DE MAR', '#1c7ed6'), ('Adobadas', 'адобо', 'adobadas', 'ADOBADAS', '#c1121f')]),
    G('Nishikava', 'MX', 'coated', 'Nishikava', 'Нишикава', 'Nishikava', 'asian', 'inter_black', 'solid', ('#ffffff', '#ffd43b'), 'none', 180, 'CACAHUATE ESTILO JAPONÉS', [
        ('Clasico', 'классический', 'classic', 'CLÁSICO', '#c1121f'), ('Limon', 'лимон', 'lime', 'LIMÓN', '#5c940d'), ('Enchilado', 'с чили', 'chilli', 'ENCHILADO', '#e8590c')]),
    G('Torsida', 'BR', 'puffs', 'Torsida', 'Торсида', 'Torsida', 'diag', 'alfa', 'solid', ('#12264d', '#ffd43b'), 'none', 70, 'SALGADINHO DE TRIGO', [
        ('Churrasco', 'шурраско', 'churrasco', 'CHURRASCO', '#a61e1e'), ('Pimenta', 'мексиканский перец', 'mexican pepper', 'PIMENTA MEXICANA', '#e8590c'),
        ('Bacon', 'бекон', 'bacon', 'BACON', '#c2255c'), ('Costela', 'рёбрышки с лаймом', 'ribs with lime', 'COSTELA COM LIMÃO', '#2b8a3e')], food='ring'),
    G('Fandangoz', 'BR', 'puffs', 'Fandangoz', 'Фандангос', 'Fandangoz', 'burst', 'lobster', 'solid', ('#ffffff', '#ffd43b'), 'none', 140, 'SALGADINHO DE MILHO', [
        ('Presunto', 'ветчина', 'ham', 'PRESUNTO', '#e64980'), ('Queijo', 'сыр', 'cheese', 'QUEIJO', '#f08c00'), ('Churrasco', 'шурраско', 'churrasco', 'CHURRASCO', '#7a2e0e')], food='curl'),
    # ---------------------------------------------------------------- France, Spain, Italy, Benelux, Poland, Greece, Turkey
    G('Viko', 'FR', 'crisps', 'Viko', 'Вико', 'Viko', 'band', 'abril', 'solid', ('#ffffff', '#ffd43b'), 'none', 135, 'CHIPS DE PICARDIE', [
        ('Classique', 'классические', 'classic', 'LA CLASSIQUE', '#c1121f'), ('Poulet', 'жареная курица', 'roast chicken', 'POULET RÔTI', '#e8930c'), ('Barbecue', 'барбекю', 'barbecue', 'BARBECUE', '#5c2e0e')]),
    G('Belen', 'FR', 'cracker', 'Belen', 'Белен', 'Belen', 'frame', 'playfair_l', 'solid', ('#ffffff', '#ffd43b'), 'none', 100, 'BISCUITS APÉRITIF', [
        ('Monaco', 'эмменталь', 'emmental', 'MONACO EMMENTAL', '#c1121f'), ('Croustilles', 'сырные', 'cheese crisps', 'CROUSTILLES FROMAGE', '#f08c00'), ('Chipster', 'воздушные лепестки', 'salted petals', 'CHIPSTER SALÉ', '#1c5fb8')],
      size=(.11, .05, .18)),
    G('Risketoz', 'ES', 'puffs', 'Risketoz', 'Рискетос', 'Risketoz', 'stripes', 'alfa', 'brand', ('#e8590c', '#ffd43b'), 'none', 120, 'APERITIVO DE MAÍZ', [
        ('Queso', 'сыр', 'cheese', 'QUESO', '#ffd43b'), ('Jamon', 'хамон', 'ham', 'JAMÓN', '#ffc9c9'), ('Barbacoa', 'барбекю', 'barbecue', 'BARBACOA', '#ffa94d')], food='flip'),
    G('SanKarlo', 'IT', 'crisps', 'San Karlo', 'Сан Карло', 'San Karlo', 'frame', 'abril', 'solid', ('#ffffff', '#ffd43b'), 'none', 150, 'PATATINE', [
        ('Classica', 'классические', 'classic', 'CLASSICA', '#c1121f'), ('Rustica', 'деревенские', 'rustic', 'RUSTICA', '#7a4a1e'),
        ('Lime', 'лайм и розовый перец', 'lime & pink pepper', 'LIME E PEPE ROSA', '#5c940d'), ('Paprika', 'паприка', 'paprika', 'PAPRIKA', '#e8590c')]),
    G('NonnaPia', 'IT', 'bagel', 'Nonna Pia', 'Нонна Пиа', 'Nonna Pia', 'retro', 'playfair_l', 'light', ('#f6efdc', '#1b5e20'), 'wheat', 250, 'TARALLI PUGLIESI', [
        ('Classici', 'классические', 'classic', 'CLASSICI', '#1b5e20'), ('Finocchio', 'с фенхелем', 'fennel', 'AL FINOCCHIO', '#5c7a00'),
        ('Peperoncino', 'с перчиком', 'chilli', 'AL PEPERONCINO', '#b91c1c'), ('Cipolla', 'с луком', 'onion', 'ALLA CIPOLLA', '#7a3b8f')], name_ru='Таралли', thirst=-6),
    G('Kroki', 'NL', 'crisps', 'Kroki', 'Кроки', 'Kroki', 'big', 'alfa', 'solid', ('#ffffff', '#ffd43b'), 'none', 175, 'CHIPS', [
        ('Naturel', 'с солью', 'salted', 'NATUREL', '#c1121f'), ('Paprika', 'паприка', 'paprika', 'PAPRIKA', '#1c5fb8'),
        ('Bolognese', 'болоньезе', 'bolognese', 'BOLOGNESE', '#2b8a3e'), ('Pickles', 'пиклз', 'pickles', 'PICKLES', '#5c940d')]),
    G('Lajkonyk', 'PL', 'sticks', 'Lajkonyk', 'Лайконик', 'Lajkonyk', 'retro', 'alfa', 'light', ('#fdf3d8', '#c1121f'), 'starring', 200, 'PALUSZKI', [
        ('Solone', 'с солью', 'salted', 'SOLONE', '#c1121f'), ('Sezam', 'с кунжутом', 'sesame', 'Z SEZAMEM', '#9c6a00'), ('Mak', 'с маком', 'poppy seed', 'Z MAKIEM', '#1f3a68')], shape='bag', size=(.11, .26, .04)),
    G('Tsakiri', 'GR', 'crisps', 'ΤΣΑΚΙΡΙ', 'Цакири', 'Tsakiri', 'band', 'inter_black', 'solid', ('#ffffff', '#ffd43b'), 'none', 120, 'ΠΑΤΑΤΑΚΙΑ', [
        ('Salt', 'с солью', 'salted', 'ΑΛΑΤΙ', '#1c5fb8'), ('Oregano', 'орегано', 'oregano', 'ΡΙΓΑΝΗ', '#2b8a3e'), ('BBQ', 'барбекю', 'barbecue', 'BARBECUE', '#a61e1e')]),
    G('Tadim', 'TR', 'seeds', 'Tadim', 'Тадым', 'Tadim', 'diag', 'inter_black', 'solid', ('#12264d', '#ffd43b'), 'sunring', 180, 'AY ÇEKİRDEĞİ', [
        ('Tuzlu', 'солёные', 'salted', 'TUZLU', '#c1121f'), ('Siyah', 'чёрные', 'black', 'SİYAH', '#2b2b2b'), ('Kabak', 'тыквенные', 'pumpkin', 'KABAK ÇEKİRDEĞİ', '#2b8a3e')]),
    # ---------------------------------------------------------------- Scandinavia
    G('OLV', 'SE', 'puffs', 'OLV', 'ОЛВ', 'OLV', 'big', 'inter_black', 'solid', ('#ffffff', '#ffd43b'), 'none', 160, 'CHEEZ DOODLZ', [
        ('Cheez', 'сырные', 'cheez doodles', 'OSTBÅGAR', '#f08c00'), ('Grill', 'гриль', 'grill', 'GRILL', '#a61e1e'), ('Sourcream', 'сметана и лук', 'sourcream & onion', 'SOURCREAM & ONION', '#2b8a3e')], food='flip'),
    G('Sorland', 'NO', 'crisps', 'Sørlandsflak', 'Сёрланнсфлак', 'Sorlandsflak', 'retro', 'oldstd', 'light', ('#f3ecd9', '#1f3a68'), 'waves', 190, 'HÅNDKOKTE POTETCHIPS', [
        ('Havsalt', 'морская соль', 'sea salt', 'HAVSALT', '#1f3a68'), ('Paprika', 'паприка', 'paprika', 'PAPRIKA', '#b91c1c'), ('Chili', 'испанский чили', 'spanish chilli', 'SPANSK CHILI', '#9c4a00')]),
    G('Taffell', 'FI', 'crisps', 'Taffell', 'Таффель', 'Taffell', 'stripes', 'alfa', 'brand', ('#1c5fb8', '#ffd43b'), 'none', 235, 'PERUNALASTUJA', [
        ('Broadway', 'сметана и лук', 'sour cream & onion', 'BROADWAY', '#8ce99a'), ('Grill', 'гриль', 'grill', 'GRILLI', '#ffa94d'), ('Juusto', 'сыр', 'cheese', 'JUUSTO', '#ffd43b')]),
    # ---------------------------------------------------------------- India, South-East Asia, Australia, Africa, Middle East
    G('Balaaji', 'IN', 'ridged', 'Balaaji', 'Баладжи', 'Balaaji', 'burst', 'inter_black', 'solid', ('#ffffff', '#ffd43b'), 'none', 135, 'WAFERS', [
        ('Salted', 'с солью', 'simply salted', 'SIMPLY SALTED', '#1c5fb8'), ('Masala', 'масала', 'masala masti', 'MASALA MASTI', '#c1121f'),
        ('Tomato', 'томат', 'tomato twist', 'TOMATO TWIST', '#e8590c'), ('Chaat', 'чаат', 'chaat chaska', 'CHAAT CHASKA', '#5c940d')]),
    G('MadEngles', 'IN', 'tortilla', 'Mad Engles', 'Мэд Энглс', 'Mad Engles', 'triangle', 'alfa', 'solid', ('#111111', '#ffd43b'), 'none', 72, 'TRIANGLE CHIPS', [
        ('Achaari', 'ачари', 'achaari masti', 'ACHAARI MASTI', '#5c940d'), ('Tomato', 'томат', 'tomato madness', 'TOMATO MADNESS', '#c1121f'), ('Masala', 'масала', 'very peri peri', 'VERY PERI PERI', '#e8590c')]),
    G('Hanamy', 'TH', 'shrimp', 'Hanamy', 'Ханами', 'Hanamy', 'window', 'lobster', 'solid', ('#ffffff', '#ffd43b'), 'none', 60, 'PRAWN CRACKERS', [
        ('Original', 'оригинальные', 'original', 'ORIGINAL', '#e8590c'), ('Tomyum', 'том-ям', 'tom yum', 'TOM YUM', '#c1121f'), ('Nori', 'нори', 'seaweed', 'NORI SEAWEED', '#2b8a3e')], food='stick'),
    G('KoKe', 'TH', 'coated', 'Ko-Ke', 'Ко-Кэ', 'Ko-Ke', 'band', 'alfa', 'solid', ('#ffffff', '#ffd43b'), 'nut', 230, 'COATED PEANUTS', [
        ('Coconut', 'кокосовые сливки', 'coconut cream', 'COCONUT CREAM', '#1c5fb8'), ('Tomyum', 'том-ям', 'tom yum', 'TOM YUM', '#c1121f'),
        ('Wasabi', 'васаби', 'nori wasabi', 'NORI WASABI', '#2b8a3e'), ('Shrimp', 'креветка', 'shrimp', 'SHRIMP', '#e64980')], shape='can', size=(.04, .11)),
    G('Chitatto', 'ID', 'ridged', 'Chitatto', 'Читатто', 'Chitatto', 'diag', 'alfa', 'solid', ('#1a1a1a', '#ffd43b'), 'none', 68, 'KERIPIK KENTANG', [
        ('Sapi', 'говядина на углях', 'beef barbeque', 'SAPI PANGGANG', '#a61e1e'), ('Ayam', 'курица со специями', 'spiced chicken', 'AYAM BUMBU', '#e8930c'), ('Goreng', 'ми горенг', 'mi goreng', 'RASA MI GORENG', '#c1121f')]),
    G('Kutela', 'ID', 'crisps', 'Kutela', 'Кутела', 'Kutela', 'window', 'pacifico', 'solid', ('#ffffff', '#ffd43b'), 'none', 180, 'KERIPIK SINGKONG', [
        ('Original', 'оригинальные', 'original', 'ORIGINAL', '#2b8a3e'), ('Balado', 'баладо, острые', 'balado', 'BALADO', '#c1121f'), ('BBQ', 'барбекю', 'barbeque', 'BARBEQUE', '#7a2e0e')], name_ru='Чипсы из маниоки'),
    G('Piatoz', 'PH', 'crisps', 'Piatoz', 'Пиатос', 'Piatoz', 'big', 'inter_black', 'solid', ('#ffffff', '#ffd43b'), 'none', 85, 'POTATO CRISPS', [
        ('Cheese', 'сыр', 'cheese', 'CHEESE', '#1c5fb8'), ('Sour', 'сметана и лук', 'sour cream & onion', 'SOUR CREAM & ONION', '#2b8a3e'), ('BBQ', 'барбекю', 'roadhouse barbecue', 'ROADHOUSE BARBECUE', '#a61e1e')], food='cracker'),
    G('BoiBawang', 'PH', 'cornnuts', 'Boi Bawang', 'Бой Баванг', 'Boi Bawang', 'burst', 'alfa', 'solid', ('#ffffff', '#ffd43b'), 'none', 100, 'CORNICK', [
        ('Garlic', 'чеснок', 'garlic', 'GARLIC', '#b8860b'), ('Chili', 'чили и сыр', 'chili cheese', 'CHILI CHEESE', '#e8590c'),
        ('Adobo', 'адобо', 'adobo', 'ADOBO', '#5c2e0e'), ('Lechon', 'жареная курица', 'lechon manok', 'LECHON MANOK', '#c1121f')]),
    G('Smifs', 'AU', 'ridged', 'Smif’s', 'Смифс', 'Smif’s', 'band', 'alfa', 'solid', ('#ffffff', '#ffd43b'), 'none', 170, 'CRINKLE CUT CHIPS', [
        ('Original', 'оригинальные', 'original', 'ORIGINAL', '#1c5fb8'), ('Chicken', 'курица', 'chicken', 'CHICKEN', '#2b8a3e'),
        ('Vinegar', 'соль и уксус', 'salt & vinegar', 'SALT & VINEGAR', '#e64980'), ('Cheese', 'сыр и лук', 'cheese & onion', 'CHEESE & ONION', '#f08c00')]),
    G('Sheips', 'AU', 'cracker', 'Sheips', 'Шейпс', 'Sheips', 'diag', 'inter_black', 'solid', ('#1a1a1a', '#ffd43b'), 'none', 175, 'BAKED BISCUITS', [
        ('BBQ', 'барбекю', 'barbecue', 'BARBECUE', '#a61e1e'), ('Pizza', 'пицца', 'pizza', 'PIZZA', '#2b8a3e'),
        ('Chicken', 'курица', 'chicken crimpy', 'CHICKEN CRIMPY', '#e8930c'), ('Cheddar', 'чеддер', 'cheddar', 'CHEDDAR', '#1c5fb8')], size=(.10, .05, .19)),
    G('Symba', 'ZA', 'crisps', 'Symba', 'Симба', 'Symba', 'badge', 'alfa', 'brand', ('#c1121f', '#ffd43b'), 'sunring', 120, 'POTATO CHIPS', [
        ('Beef', 'копчёная говядина', 'smoked beef', 'SMOKED BEEF', '#ffa94d'), ('Chutney', 'фруктовый чатни', 'fruit chutney', 'FRUIT CHUTNEY', '#ffd43b'),
        ('Vinegar', 'соль и уксус', 'salt & vinegar', 'SALT & VINEGAR', '#74c0fc'), ('Cheese', 'сыр и лук', 'cheese & onion', 'CHEESE & ONION', '#8ce99a')]),
    G('Bambah', 'IL', 'puffs', 'Bambah', 'Бамба', 'Bambah', 'window', 'comfortaa', 'solid', ('#ffffff', '#ffd43b'), 'none', 80, 'PEANUT PUFFS', [
        ('Peanut', 'арахисовые', 'peanut', 'PEANUT', '#1c5fb8'), ('Nougat', 'с нугой внутри', 'nougat filled', 'NOUGAT FILLED', '#7a4a1e'), ('Sweet', 'сладкие красные', 'sweet strawberry', 'SWEET RED', '#e03131')], food='flip'),
    G('Bisly', 'IL', 'puffs', 'Bisly', 'Бисли', 'Bisly', 'diag', 'alfa', 'solid', ('#12264d', '#ffd43b'), 'none', 70, 'WHEAT SNACK', [
        ('Grill', 'гриль', 'grill', 'GRILL', '#a61e1e'), ('Onion', 'лук', 'onion', 'ONION', '#2b8a3e'), ('Falafel', 'фалафель', 'falafel', 'FALAFEL', '#b8860b'), ('Pizza', 'пицца', 'pizza', 'PIZZA', '#e8590c')], food='roll'),
    ]

"""Everything the mod sells, in one place: the model builder, the label painter and the C# catalogue read this.
Stats: kg = item weight; kcal; thirst = the game's m_ReduceThirst (positive quenches, negative makes you thirsty);
water = litres of drinking water to brew; warm = warmth from the hot broth, 1 = the game's tea; eat = seconds to eat."""

NOODLES = [
    # id, shape, ru name, en name, ru flavour, en flavour, raw kg, water L, kcal brewed, thirst brewed, warm, broth colour, toppings, spicy
    dict(id='Rolltan', shape='brick', ru='Лапша «Роллтан»', en='Rolltan noodles', ruf='по-домашнему, с курицей', enf='home-style chicken',
         kg=.085, water=.4, kcal=390, thirst=22, warm=1.0, broth='#e8b04a', top=('egg', 'scallion'), lang='ru'),
    dict(id='Mivinka', shape='brick', ru='Лапша «Мивинка»', en='Mivinka noodles', ruf='со вкусом говядины', enf='beef flavour',
         kg=.06, water=.35, kcal=300, thirst=20, warm=1.0, broth='#b9692d', top=('meat', 'scallion'), lang='ru'),
    dict(id='MiGoreng', shape='brick', ru='Жареная лапша «Индомэй Ми Горенг»', en='Indomay Mi Goreng fried noodles', ruf='сладкий соевый соус, без бульона', enf='sweet soy sauce, no broth',
         kg=.085, water=.4, kcal=460, thirst=-6, warm=.6, broth='#8a4a1c', top=('egg', 'scallion', 'chili'), lang='en'),
    dict(id='Shine', shape='brick', ru='Острая лапша «Шайн Рамён»', en='Shine Ramyun hot noodles', ruf='острая говядина с грибами', enf='spicy beef and mushroom',
         kg=.12, water=.55, kcal=500, thirst=16, warm=1.7, broth='#c4381c', top=('mushroom', 'scallion', 'chili'), lang='ko', spicy=1),
    dict(id='FireChicken', shape='brick', ru='Лапша «Самджан Огненный цыплёнок»', en='Samjan Fire Chicken noodles', ruf='обжигающе острая, без бульона', enf='scorching hot, no broth',
         kg=.14, water=.6, kcal=530, thirst=-12, warm=2.3, broth='#9e1b10', top=('chili', 'scallion'), lang='ko', spicy=2),
    dict(id='Chikin', shape='brick', ru='Лапша «Ниссун Чикин Рамен»', en='Nissun Chikin Ramen', ruf='классическая куриная', enf='the classic chicken',
         kg=.085, water=.45, kcal=380, thirst=22, warm=1.0, broth='#d9a441', top=('egg', 'scallion'), lang='ja'),
    dict(id='Doshik', shape='tray', ru='Лапша «Дошик» в лотке', en='Doshik noodle tray', ruf='говядина', enf='beef',
         kg=.1, water=.5, kcal=430, thirst=24, warm=1.1, broth='#b5642c', top=('meat', 'scallion'), lang='ru'),
    dict(id='KappNoodle', shape='cup', ru='Лапша «Капп Нудл» в стакане', en='Kapp Noodle cup', ruf='морепродукты', enf='seafood',
         kg=.07, water=.3, kcal=330, thirst=20, warm=1.0, broth='#e3c98a', top=('shrimp', 'corn', 'scallion'), lang='en'),
    dict(id='BiggLanch', shape='bigcup', ru='Лапша «Бигг Ланч» в большом стакане', en='Bigg Lanch big cup', ruf='говядина и грибы', enf='beef and mushroom',
         kg=.1, water=.5, kcal=480, thirst=24, warm=1.2, broth='#a65e2c', top=('mushroom', 'meat'), lang='ru'),
    dict(id='AkaiKitsune', shape='bowl', ru='Удон «Акай Кицунэ»', en='Akai Kitsune udon', ruf='с жареным тофу', enf='with fried tofu',
         kg=.1, water=.45, kcal=440, thirst=24, warm=1.2, broth='#c99a4a', top=('tofu', 'scallion'), lang='ja'),
]

SNACKS = []  # snacks live in SnacksByEMPEROR

# metres: width x height (lying) x puffed thickness; textures: front width 512 px
SIZES = {
    'brick': (.115, .165, .035),
    'bag': (.19, .27, .07),
    'smallbag': (.12, .17, .04),
    'bigbag': (.22, .34, .09),
}

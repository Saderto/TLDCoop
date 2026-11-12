"""Varieties into the mod: src/Catalog.cs, data/text_var_{ru,en}.json, cigarette meshes (.fpmesh), textures copied to dist.
python3 gen_catalog.py <bakedir> <texdir> <srcdir> <distroot (Mods/SmokesByEMPEROR)>"""
import sys, os, json, math, shutil, struct
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import catalog

B, TEX, SRC, DIST = sys.argv[1:5]

PACK_MESH = {'chapgirl': 'pack_chapgirl', 'sobrania': 'pack_milston'}

BLURB = {
    'belomor': ('Папиросы с картой канала на пачке.', 'Papirosy with a map of the canal on the pack.'),
    'yawa': ('Московские сигареты в золотой пачке.', 'Moscow cigarettes in a gold pack.'),
    'devol': ('Чёрные сигареты с чертёнком.', 'Black cigarettes with a little devil.'),
    'natsherman': ('Длинные нью-йоркские сигареты в цветной бумаге.', 'Long New York cigarettes in coloured paper.'),
    'djarumm': ('Индонезийский кретек с гвоздикой, трещит при затяжке.', 'Indonesian kretek with cloves, crackles on a drag.'),
    'treasurrer': ('Лондонские сигареты в металлической бумаге.', 'London cigarettes in metallic paper.'),
    'newpork': ('Ментоловые сигареты с парусом на пачке.', 'Menthol cigarettes with a sail on the pack.'),
    'vague': ('Тонкие французские сигареты с цветами на бумаге.', 'Slim French cigarettes with flowers on the paper.'),
    'glamoor': ('Тонкие сигареты с вензелями, для гламура в метель.', 'Slim cigarettes with flourishes, for glamour in a blizzard.'),
    'ezze': ('Корейские суперслимы с волной на пачке.', 'Korean superslims with a wave on the pack.'),
    'ln': ('Простые сигареты без лишних слов.', 'Plain cigarettes, no fuss.'),
    'galloises': ('Французские сигареты с крылатым шлемом.', 'French cigarettes with a winged helmet.'),
    'gitanos': ('Французские сигареты с танцовщицей в дыму.', 'French cigarettes with a dancer in the smoke.'),
    'lucky': ('Сигареты с мишенью на пачке.', 'Cigarettes with a target on the pack.'),
    'pell': ('Сигареты с гербом и львами.', 'Cigarettes with a crest and lions.'),
    'dunchill': ('Лондонские сигареты в строгой пачке.', 'London cigarettes in a strict pack.'),
    'dovidoff': ('Женевские сигареты с росписью на пачке.', 'Geneva cigarettes with a signature on the pack.'),
    'rothmunz': ('Сигареты «из Лондона» с синими полосами.', 'Cigarettes "of London" with blue stripes.'),
    'benson': ('Сигареты в золотой пачке.', 'Cigarettes in a gold pack.'),
    'milston': ('Классические сигареты с гербом.', 'Classic cigarettes with a crest.'),
    'marlbrrro': ('Сигареты для ковбоев, которые мёрзнут.', 'Cigarettes for cowboys who are freezing.'),
    'parliamint': ('Ментоловые сигареты с утопленным фильтром.', 'Menthol cigarettes with a recessed filter.'),
    'bent': ('Сигареты с синей дугой на пачке.', 'Cigarettes with a blue arc on the pack.'),
    'caramel': ('Турецкий табак и лама на пачке.', 'Turkish blend and a llama on the pack.'),
    'chapgirl': ('Ароматные сигареты в пачке с дамой в шляпке.', 'Aromatic cigarettes in a pack with a lady in a cloche hat.'),
    'sobrania': ('Изысканные сигареты старого Лондона в пачке с короной.', 'Refined old-London cigarettes in a crowned pack.'),
}
MEME = {  # tex_key: (ru, en) joke line that opens the description
    'belomor_classic': ('Папиросы. Мундштук мнут гармошкой, а мороз мнёт всё остальное.', 'Papirosy. You crimp the mouthpiece; the frost crimps everything else.'),
    'belomor_leningrad': ('Ленинградские. Тут тоже белые ночи, только очень долгие и злые.', 'Leningrad. White nights here too, only longer and angrier.'),
    'belomor_moscow': ('Московские. До Москвы далеко, до волка близко.', 'Moscow. Moscow is far, the wolf is near.'),
    'belomor_northern': ('Северные. Север подтверждает: да, это он.', 'Northern. The North confirms: yes, this is it.'),
    'belomor_jubilee': ('Юбилейные. Отметь ещё один выживший день.', 'Jubilee. Celebrate one more day survived.'),
    'yawa_goldclassic': ('Золотая классика. Вкус девяностых и такой же уровень безопасности.', 'Gold classic. The taste of the nineties and the same level of safety.'),
    'yawa_goldoriginal': ('Оригинальная. Оригинальнее, чем рецепт ухи из одной рыбины.', 'Original. More original than a fish soup made from one fish.'),
    'yawa_whitegold': ('Белое золото. Снег тоже белый, но за него ничего не дают.', 'White gold. Snow is white too, but nobody pays for it.'),
    'yawa_blue': ('Синие. Как губы после ночи без костра.', 'Blue. Like your lips after a night without a fire.'),
    'yawa_long100': ('Сотка. Сто миллиметров удовольствия, пока не отморозишь пальцы.', 'The hundred. A hundred millimetres of joy before your fingers freeze.'),
    'devol_pink': ('Чёрные с розовым. Чертёнок на пачке тоже любит розовое.', 'Black with pink. The little devil on the pack likes pink too.'),
    'devol_special': ('Специальные. Сделка с дьяволом: тепло сейчас, кашель потом.', 'Special. A deal with the devil: warmth now, the cough later.'),
    'devol_cherry': ('Вишнёвые. Вишни тут не растут, растёт только тоска.', 'Cherry. No cherries grow here, only gloom.'),
    'devol_chocolate': ('Шоколадные. Батончик ты съел ещё вчера, остался только дым.', 'Chocolate. You ate the bar yesterday; only the smoke is left.'),
    'devol_mint': ('Мятные чёрные. Мрачно, но свежо.', 'Black mint. Grim, but fresh.'),
    'devol_gold': ('Чёрное золото. Нефть тоже чёрное золото, но её не покуришь.', 'Black gold. Oil is black gold too, but you cannot smoke it.'),
    'natsherman_fantasiapink': ('Розовая фантазия. В реальности тут всё белое.', 'Pink fantasy. In reality everything here is white.'),
    'natsherman_fantasiablue': ('Голубая фантазия: тёплый дом, горячий душ, свет.', 'Blue fantasy: a warm house, a hot shower, electricity.'),
    'natsherman_fantasiayellow': ('Жёлтая фантазия. Как солнце, которого не видно неделю.', 'Yellow fantasy. Like the sun you have not seen in a week.'),
    'natsherman_fantasialilac': ('Сиреневая фантазия. Цвет твоих пальцев через час на ветру.', 'Lilac fantasy. The colour of your fingers after an hour in the wind.'),
    'natsherman_fantasiagreen': ('Зелёная фантазия. Весна. Говорят, она бывает.', 'Green fantasy. Spring. They say it exists.'),
    'natsherman_classicbrown': ('Коричневые нью-йоркские. В Нью-Йорке сейчас тоже холодно, утешься.', 'Brown New York ones. It is cold in New York too, take comfort.'),
    'natsherman_blackgold': ('Чёрное с золотом. Курить в смокинге, которого у тебя нет.', 'Black and gold. To smoke in the tuxedo you do not have.'),
    'natsherman_naturals': ('Натуральные. Единственное натуральное, что не пытается тебя съесть.', 'Naturals. The only natural thing that is not trying to eat you.'),
    'djarumm_black': ('Кретек с гвоздикой. Трещит, как костёр, только греет меньше.', 'Clove kretek. Crackles like a fire, just warms less.'),
    'djarumm_blackcappuccino': ('Капучино. Кофемашины нет, есть только это.', 'Cappuccino. There is no coffee machine, only this.'),
    'djarumm_blackmenthol': ('Чёрный ментол с гвоздикой. Тропики и Арктика в одной затяжке.', 'Black menthol with cloves. The tropics and the Arctic in one drag.'),
    'djarumm_super': ('Супер. Индонезийцы курят их на жаре, ты — в минус сорок.', 'Super. Indonesians smoke these in the heat; you, at minus forty.'),
    'djarumm_splash': ('С капсулой. Всплеск, которого так не хватает в этой тишине.', 'With a capsule. The splash this silence is missing.'),
    'treasurrer_silver': ('Серебряные. Блестят, как наст под луной.', 'Silver. They shine like the crust of snow under the moon.'),
    'treasurrer_gold': ('Золотые. Самые дорогие сигареты в мире, где деньги ничего не стоят.', 'Gold. The most expensive cigarettes in a world where money is worthless.'),
    'treasurrer_black': ('Чёрные с золотом. Для выживания с достоинством.', 'Black and gold. For surviving with dignity.'),
    'treasurrer_aluminiumblue': ('Алюминиевые. Не путать с банкой из-под консервов.', 'Aluminium. Not to be confused with an empty tin can.'),
    'treasurrer_original': ('Оригинальные. Сокровище, которое не надо выкапывать из снега.', 'Original. Treasure you do not have to dig out of the snow.'),
    'newpork_menthol': ('Ментол. Парус на пачке, а ветер — снаружи.', 'Menthol. A sail on the pack and the wind outside.'),
    'newpork_mentholgold': ('Золотой ментол. Свежесть, которой и так вокруг полно.', 'Gold menthol. Freshness, as if there was not enough around.'),
    'newpork_red': ('Красные. Для тех, кому ментола хватает от погоды.', 'Red. For those who get enough menthol from the weather.'),
    'newpork_platinum': ('Платиновые. Лёгкие, как последний патрон, который бережёшь.', 'Platinum. Light, like the last bullet you are saving.'),
    'newpork_nonmenthol': ('Без ментола. Холодно и так.', 'Non-menthol. It is cold enough already.'),
    'vague_bleue': ('Синие цветочки на бумаге. Других цветов тут до весны не будет, а весны не будет.', 'Blue flowers on the paper. No other flowers until spring, and there is no spring.'),
    'vague_lilas': ('Сиреневые. Пахнут Парижем, если долго не мыться.', 'Lilac. Smells of Paris if you have not washed in a while.'),
    'vague_menthe': ('Мятные листики. Единственная зелень на сотню километров.', 'Mint leaves. The only greenery for a hundred kilometres.'),
    'vague_arome': ('С золотыми вензелями. Курить их надо, отставив мизинец, даже в варежке.', 'With gold flourishes. Smoke them with your pinkie out, even in a mitten.'),
    'vague_rose': ('Розовые с цветами. Волк смотрит на тебя с недоумением.', 'Pink with flowers. The wolf looks at you, puzzled.'),
    'vague_noir': ('Чёрные с серебром. Стильно умирать от холода — тоже искусство.', 'Black and silver. Freezing to death in style is an art too.'),
    'glamoor_azure': ('Лазурные. Цвет неба, которое ты видел последний раз неделю назад.', 'Azure. The colour of the sky you last saw a week ago.'),
    'glamoor_amber': ('Янтарные вензеля. Гламур на минус тридцать пять.', 'Amber flourishes. Glamour at minus thirty-five.'),
    'glamoor_lilac': ('Сиреневые. На обложке журнала, которого больше никто не выпускает.', 'Lilac. From the cover of a magazine no one prints any more.'),
    'glamoor_menthol': ('Ментоловый гламур. Холодно и снаружи, и внутри.', 'Menthol glamour. Cold outside and in.'),
    'glamoor_pink': ('Розовые. Медведь не оценит, но ты-то знаешь.', 'Pink. The bear will not appreciate it, but you know.'),
    'glamoor_gold': ('Золотые суперслимы. Самое ценное, что ты нашёл в этом доме.', 'Gold superslims. The most valuable thing you found in this house.'),
    'ezze_blue': ('Тонкие, как надежда на спасательный вертолёт.', 'Slim as the hope of a rescue helicopter.'),
    'ezze_silver': ('Серебряные и тонкие. Как лёд на озере, по которому ты зря пошёл.', 'Silver and thin. Like the ice on the lake you should not have crossed.'),
    'ezze_gold': ('Золотые. Ценнее только спички.', 'Gold. Only matches are worth more.'),
    'ezze_goldenleaf': ('Золотой лист. Остальные листья давно под снегом.', 'A golden leaf. All the other leaves are under the snow.'),
    'ezze_black': ('Чёрные тонкие. Как ночь, только короче.', 'Black and slim. Like the night, only shorter.'),
    'ezze_menthol': ('Ментол. Чтобы холод был не только в пальцах.', 'Menthol. So the cold is not just in your fingers.'),
    'ezze_change': ('С капсулой. Нажмёшь, и всё изменится. Кроме погоды.', 'With a capsule. Click it and everything changes. Except the weather.'),
    'ln_red': ('Красные. Без лишних слов, как и всё в этом лесу.', 'Red. No fuss, like everything in this forest.'),
    'ln_blue': ('Синие. Честные сигареты для честного выживания.', 'Blue. Honest cigarettes for honest survival.'),
    'ln_silver': ('Серебряные. Лёгкие, как рюкзак в начале игры.', 'Silver. Light as your backpack on day one.'),
    'ln_loft': ('Лофт. Лучший лофт в округе — чердак с дырявой крышей.', 'Loft. The best loft around is an attic with a leaky roof.'),
    'ln_menthol': ('Ментол. Свежо, как ветер в лицо на перевале.', 'Menthol. Fresh as the wind in your face on the pass.'),
    'galloises_brunes': ('Тёмный табак без фильтра. Французы так курили на войне, тебе тоже подойдёт.', 'Dark tobacco, no filter. The French smoked these at war; they will do for you.'),
    'galloises_blondesrouge': ('Крылатый шлем на пачке. Улететь отсюда он не поможет.', 'A winged helmet on the pack. It will not help you fly out of here.'),
    'galloises_blondesbleu': ('«Свобода всегда». Кроме свободы от холода.', 'Freedom always. Except freedom from the cold.'),
    'galloises_blondesblanc': ('Белые. Лёгкие, по-французски элегантные, по-канадски замёрзшие.', 'White. Light, elegant the French way, frozen the Canadian way.'),
    'galloises_caporal': ('Капораль в жёлтой бумаге. Крепкие, как приказ сержанта.', "Caporal in yellow paper. Strong as a sergeant's order."),
    'gitanos_brunes': ('Без фильтра, с танцовщицей. Она танцует, чтобы согреться.', 'No filter, with a dancer. She dances to keep warm.'),
    'gitanos_blondes': ('Светлый табак. Танцовщица на пачке тоже блондинка.', 'Blond tobacco. The dancer on the pack is a blonde too.'),
    'gitanos_filtre': ('С фильтром. Танцы танцами, а лёгкие одни.', 'Filtered. Dancing is dancing, but you only get one pair of lungs.'),
    'gitanos_mais': ('Кукурузная бумага. Еды из неё не выйдет, проверено.', 'Corn paper. You cannot eat it, we checked.'),
    'gitanos_blanches': ('Белые. Танцовщица в снегу почти не видна.', 'White. The dancer is nearly invisible in the snow.'),
    'lucky_red': ('«Поджарено морозом». Мишень на пачке для удобства волков.', "It's frosted. A target on the pack, for the wolves' convenience."),
    'lucky_blue': ('Синяя мишень. Медведю всё равно, какого она цвета.', 'A blue target. The bear does not care what colour it is.'),
    'lucky_silver': ('Серебряная мишень. Удача тоже бывает лёгкой.', 'A silver target. Luck comes in light too.'),
    'lucky_original': ('Без фильтра, как в старые добрые времена, когда было электричество.', 'Unfiltered, like the good old days when there was electricity.'),
    'lucky_clickroll': ('Щёлкнул, покатил. Главное — не по склону.', 'Click and roll. Just not down the slope.'),
    'lucky_ice': ('Удача со льдом. Льда тут хватает, с удачей сложнее.', 'Luck on the rocks. Plenty of ice here, luck is harder to find.'),
    'pell_red': ('«Через тернии к снегу». Львы на гербе тоже мёрзнут.', 'Through hardships to the snow. The lions on the crest are cold too.'),
    'pell_blue': ('Синие, как небо в ясный морозный день, который бывает раз в неделю.', 'Blue as a clear frosty sky, which happens once a week.'),
    'pell_silver': ('Серебристые. Подходят к седине, которая появилась после первой встречи с медведем.', 'Silver. Matches the grey hair you got after your first bear.'),
    'pell_orange': ('Оранжевые, как сигнальная ракета. Только поджигать надо с другого конца.', 'Orange like a flare. Only you light the other end.'),
    'pell_nanokings': ('Нанокороли. Королевство маленькое, но своё.', 'Nano kings. A small kingdom, but yours.'),
    'pell_menthol': ('Ментол. Для тех, кому мало минус тридцати.', 'Menthol. For those who find minus thirty not enough.'),
    'pell_amber': ('Янтарные, как свет из окна чужого дома, где кто-то уже живёт.', "Amber like the light in a stranger's window where someone already lives."),
    'dunchill_international': ('Международные. Курить с видом, будто у вас есть паспорт и куда ехать.', 'International. Smoke as if you had a passport and somewhere to go.'),
    'dunchill_finecutblack': ('Мелкая резка, чёрная пачка. Строго, как Серая Мать до первой чашки чая.', 'Fine cut, black pack. Stern as the Grey Mother before her first cup of tea.'),
    'dunchill_finecutblue': ('Мелкая резка, синяя пачка. Джентльмен, у которого кончились спички.', 'Fine cut, blue pack. A gentleman who ran out of matches.'),
    'dunchill_finecutwhite': ('Белая пачка в белом снегу. Лондонский туман по-канадски.', 'A white pack in the white snow. London fog, Canadian style.'),
    'dunchill_masterblend': ('Смесь мастера. Мастер, судя по всему, тоже здесь застрял.', 'A master blend. The master, it seems, got stuck out here too.'),
    'dunchill_infinite': ('Бесконечные. В отличие от вашего запаса.', 'Infinite. Unlike your supply.'),
    'dovidoff_classic': ('Роспись на пачке. Автограф человека, который вас никогда не найдёт.', 'A signature on the pack. The autograph of someone who will never find you.'),
    'dovidoff_gold': ('Золотые. Курить, оттопырив мизинец, если он ещё гнётся.', 'Gold. Smoke with your little finger out, if it still bends.'),
    'dovidoff_white': ('Белые и швейцарские. Нейтралитет между вами и метелью.', 'White and Swiss. Neutrality between you and the blizzard.'),
    'dovidoff_black': ('Чёрные. Женева ночью, Канада всегда.', 'Black. Geneva at night, Canada all the time.'),
    'dovidoff_evolve': ('«Эволюция». Вы тоже эволюционировали: теперь едите сырую рыбу.', 'Evolve. You evolved too: now you eat raw fish.'),
    'dovidoff_reach': ('Тонкие. Дотянуться до них проще, чем до Милтона.', 'Slim. Easier to reach than Milton.'),
    'rothmunz_red': ('«Из Лондона». Посылка шла долго.', 'Of London. The parcel took a while.'),
    'rothmunz_blue': ('Синие полосы, как следы шин на снегу, по которым уже никто не проедет.', 'Blue stripes, like tyre tracks in the snow no one will drive again.'),
    'rothmunz_silver': ('Серебро. Лёгкие, как ваш рюкзак после стычки с волком.', 'Silver. Light as your backpack after a wolf fight.'),
    'rothmunz_demi': ('Деми, то есть половинка. Как и ваш запас еды.', 'Demi, meaning half. Like your food supply.'),
    'rothmunz_nanokings': ('Нанокороли из Лондона. Королевство уменьшилось вместе с пайком.', 'Nano kings of London. The kingdom shrank with the rations.'),
    'benson_gold': ('Золотая пачка. Самое ценное, что вы нашли в этом доме.', 'A gold pack. The most valuable thing in this house.'),
    'benson_silver': ('Серебряная пачка. Второе место в конкурсе находок.', 'A silver pack. Second place in the finders contest.'),
    'benson_blue': ('Синие. Хорошо смотрятся на фоне синих губ.', 'Blue. They look great against blue lips.'),
    'benson_black': ('Чёрные с золотом. Для выживальщика с претензией.', 'Black and gold. For a survivor with pretensions.'),
    'benson_menthol': ('Ментол в золотой рамке. Холод тоже бывает дорогим.', 'Menthol in a gold frame. Cold can be expensive too.'),
    'benson_specialfilter': ('Длинный фильтр. Пока дойдёте до табака, успеете подумать о жизни.', 'A long filter. By the time you reach the tobacco you will have thought about life.'),
    'milston_red': ('Крепкие, как недоверие Серой Матери. Классика, которую курили ещё до того, как погас свет.', "Strong as the Grey Mother's distrust. The classic people smoked before the lights went out."),
    'milston_blue': ('Синие, как губы после проруби. Помягче красных, но ненамного.', 'Blue as your lips after the ice hole. Softer than the reds, but not by much.'),
    'milston_silver': ('Серебро. Единственное серебро, которое можно найти в Милтоне.', 'Silver. The only silver you will ever find in Milton.'),
    'milston_white': ('Почти воздух. Курить их на морозе всё равно что дышать, только дороже.', 'Almost air. Smoking these in the frost is like breathing, only pricier.'),
    'milston_xstyleblue': ('Тонкие, как ваш запас дров к концу метели.', 'Thin as your firewood pile at the end of a blizzard.'),
    'milston_compact': ('Короткие. Как раз до того, как волк подойдёт поближе.', 'Short. Just long enough until the wolf gets closer.'),
    'milston_xsimpulse': ('Щёлк — и во рту зима. Как будто снаружи её не хватало.', 'Click, and there is winter in your mouth. As if there was not enough outside.'),
    'marlbrrro_red': ('Для настоящих ковбоев, которые мёрзнут. Лошади нет, пальцы не гнутся, но стиль остался.', "For real cowboys who are freezing. No horse, fingers won't bend, but the style remains."),
    'marlbrrro_gold': ('Золотая крыша на пачке. Своей крыши над головой у вас пока нет.', 'A gold roof on the pack. You still have no roof of your own.'),
    'marlbrrro_silver': ('Лёгкие. Для ковбоя, который уже не так уверен в себе.', 'Light. For a cowboy who is not so sure of himself any more.'),
    'marlbrrro_touch': ('Короткие: докурить, пока не отмёрз нос.', 'Short: finish it before your nose freezes off.'),
    'marlbrrro_iceblast': ('Ледяной взрыв. В Великом Медвежьем это называется «вторник».', 'Ice Blast. On Great Bear they call that a Tuesday.'),
    'marlbrrro_crafted': ('«Ручная работа». Как и всё, что вы ели последний месяц.', 'Crafted. Like everything you have eaten this past month.'),
    'marlbrrro_black': ('Чёрные, как ночь без фонаря. Курить с умным видом.', 'Black as a night without a lantern. Smoke with a knowing look.'),
    'parliamint_nightblue': ('Ментол с утопленным фильтром. Холода вокруг показалось мало, решили добавить и во рту.', "Menthol with a recessed filter. As if there wasn't enough cold around, they put some in your mouth too."),
    'parliamint_aquablue': ('Цвета воды в проруби. Туда же тянет и смотреть.', 'The colour of ice-hole water. And just as tempting to stare into.'),
    'parliamint_silverblue': ('Серебристо-синие. Аристократ с обмороженными ушами.', 'Silver blue. An aristocrat with frostbitten ears.'),
    'parliamint_platinum': ('Платина. Звучит дорого, пахнет ментолом и дымом костра.', 'Platinum. Sounds expensive, smells of menthol and campfire.'),
    'parliamint_reserve': ('Резерв. На чёрный день, которых тут только и бывают.', 'Reserve. For a rainy day, and here every day is one.'),
    'parliamint_soho': ('Тонкие и модные. В Сохо так ходят, в Отрадной долине так замерзают.', 'Slim and stylish. In Soho people walk like this; in Pleasant Valley they freeze like this.'),
    'bent_blue': ('Белая пачка в белом снегу: найти её уже достижение.', 'A white pack in white snow: finding it is an achievement.'),
    'bent_silver': ('Серебро на белом. Ищите по запаху.', 'Silver on white. Search by smell.'),
    'bent_white': ('Белые-белые. Совсем как всё вокруг на ближайшие сто километров.', 'White, white. Just like everything for the next hundred kilometres.'),
    'bent_nanotek': ('Нанотехнологии. Последние, что остались после Тихого апокалипсиса.', 'Nanotech. The last of it left after the Quiet Apocalypse.'),
    'bent_crystal': ('Чистые, как лёд на озере. Не проверяйте лёд, проверяйте пачку.', "Clear as lake ice. Don't test the ice, test the pack."),
    'bent_iswitch': ('Два щелчка: один для вкуса, второй чтобы проверить, живы ли пальцы.', 'Two clicks: one for flavour, one to check your fingers still work.'),
    'bent_core': ('Синяя пачка с характером. Синий фильтр, синий узор, синий нос.', 'A blue pack with character. Blue filter, blue pattern, blue nose.'),
    'caramel_yellow': ('Лама не знает, как попала в канадскую глушь. Вы тоже.', 'The llama has no idea how it ended up in the Canadian wilds. Neither do you.'),
    'caramel_blue': ('Синяя лама. Замёрзла, как и вы.', 'A blue llama. Frozen, just like you.'),
    'caramel_original': ('Без фильтра. Как и ваши мысли после недели в одиночестве.', 'No filter. Just like your thoughts after a week alone.'),
    'caramel_activate': ('Щёлкните капсулу и почувствуйте себя чуть менее мёртвым.', 'Click the capsule and feel slightly less dead.'),
    'caramel_compact': ('Компактные, как ваш рюкзак после того, как вы выкинули всё лишнее. Кроме сигарет.', 'Compact, like your backpack after you threw out everything spare. Except the cigarettes.'),
    'caramel_black': ('Чёрная лама. Говорят, она приносит удачу. Говорят — ложь.', 'A black llama. They say it brings luck. They lie.'),
    'caramel_desertsand': ('Пустынный песок. Тёплый, как воспоминание, которого у вас здесь нет.', 'Desert sand. Warm, like a memory you do not have out here.'),
    'chapgirl_red': ('Красные, как щёки после метели. Дама в шляпке не мёрзнет никогда.', 'Red as your cheeks after a blizzard. The lady in the hat never gets cold.'),
    'chapgirl_brown': ('Коричневая бумага, коричневый фильтр. Под цвет дров, которые вы рубите третий час.', 'Brown paper, brown filter. Matches the firewood you have been chopping for three hours.'),
    'chapgirl_green': ('Сладкие и зелёные. Единственная зелень, которую вы увидите до весны.', 'Sweet and green. The only green you will see before spring.'),
    'chapgirl_gold': ('Золотой фильтр. Ценнее только целая банка персиков.', 'A gold filter. Only an unopened can of peaches is worth more.'),
    'chapgirl_purple': ('Фиолетовые, как небо в аврору. Успейте докурить, пока не заискрили провода.', 'Purple as the aurora sky. Finish it before the wires start sparking.'),
    'chapgirl_vanilla': ('Пахнут ванилью и печеньем, которого у вас нет.', 'They smell of vanilla and of the cookies you do not have.'),
    'chapgirl_cherry': ('Вишнёвые. Волки чуют запах за километр и думают, что вы десерт.', 'Cherry. Wolves smell it a kilometre away and decide you are dessert.'),
    'chapgirl_coffee': ('Кофе и сигарета сразу. Завтрак выживальщика, если кофе кончился ещё в ноябре.', 'Coffee and a cigarette in one. A survivor breakfast when the coffee ran out back in November.'),
    'chapgirl_eclipsenoir': ('Чёрная пачка, чёрная сигарета, чёрный юмор. Для тех, кто пережил Тихий апокалипсис и не хочет об этом говорить.', 'Black pack, black cigarette, black humour. For those who lived through the Quiet Apocalypse and will not talk about it.'),
    'chapgirl_ash': ('Серые, как небо, как снег у дороги и как ваше настроение на сотый день.', 'Grey like the sky, the roadside snow and your mood on day one hundred.'),
    'sobrania_blackrussian': ('Чёрная бумага, золотой фильтр. Курить медленно, с лицом «медведь подождёт».', 'Black paper, gold filter. Smoke it slowly, with a face that says the bear can wait.'),
    'sobrania_cocktailpink': ('Розовые. Медведь пощадит: ему неловко нападать на такого модника.', 'Pink. The bear will spare you: it feels awkward attacking someone this fancy.'),
    'sobrania_cocktailblue': ('Голубые, как лёд на озере, на который лучше не выходить.', 'Blue as the lake ice you had better not step on.'),
    'sobrania_cocktailgreen': ('Зелёные, как ваше лицо после первой затяжки.', 'Green as your face after the first drag.'),
    'sobrania_cocktailyellow': ('Жёлтые, как снег, который не стоит есть.', 'Yellow, like the snow you should not eat.'),
    'sobrania_cocktaillilac': ('Сиреневые и редкие. Нашли? Сфотографируйте, а то никто не поверит.', 'Lilac and rare. Found one? Take a picture, nobody will believe you.'),
    'sobrania_whitegold': ('Белые с золотом. Их курят в шубе, даже если шуба из кролика.', 'White and gold. Smoked in a fur coat, even if the coat is rabbit.'),
    'sobrania_classic': ('Классика старого Лондона. Лондон далеко, но манеры можно взять с собой.', 'An old London classic. London is far away, but manners travel well.'),
}
STRENGTH_NAME = [('лёгкие', 'light'), ('средние', 'medium'), ('крепкие', 'strong'), ('очень крепкие', 'very strong')]
BONUS = {
    '': ('', ''),
    'fresh': ('«Свежий вдох»: выносливость восстанавливается на 10% быстрее 30 мин.', '"Fresh breath": stamina comes back 10% faster for 30 min.'),
    'coffee': ('Кофейный вкус: ещё −2% усталости.', 'Coffee flavour: another −2% fatigue.'),
    'sweet': ('Сладкий вкус: голод притуплён на 10 мин дольше.', 'Sweet flavour: hunger dulled 10 min longer.'),
    'warm': ('«Тёплый дым»: ощущаемая температура +1°C на 30 мин.', '"Warm smoke": feels-like temperature +1°C for 30 min.'),
    'calm': ('«Спокойствие»: прицел качается на 25% меньше 30 мин.', '"Calm": aim sway 25% less for 30 min.'),
    'hands': ('«Твёрдые пальцы»: шанс развести огонь +10% на 30 мин.', '"Steady fingers": fire-starting chance +10% for 30 min.'),
    'steady': ('«Ровное дыхание»: на бегу выносливость тратится на 15% медленнее 30 мин.', '"Steady breath": sprinting drains stamina 15% slower for 30 min.'),
}
SIDE = {
    '': ('', ''),
    'dry': ('Минус: «Сухость во рту» — жажда растёт на 15% быстрее 30 мин.', 'Downside: "Dry mouth": thirst builds 15% faster for 30 min.'),
    'dizzy': ('Минус: «Головокружение» — прицел качается на 30% сильнее 30 мин.', 'Downside: "Dizzy": aim sway 30% more for 30 min.'),
}
BLURB6 = {
    'kosmoss': ('Сигареты с ракетой и планетой на пачке.', 'Cigarettes with a rocket and a planet on the pack.'),
    'primma': ('Простые крепкие сигареты, классика без затей.', 'Plain strong cigarettes, a no-frills classic.'),
    'pyotr': ('Сигареты с парусником, названные в честь Петра. Второго.', 'Cigarettes with a sailing ship, named after Pyotr. The Second.'),
    'optimma': ('Сигареты с двойной волной на пачке.', 'Cigarettes with a double swoosh on the pack.'),
    'chesterfeld': ('Сигареты с гербом и росписью на пачке.', 'Cigarettes with a crest and a signature on the pack.'),
    'phillip': ('Сигареты с дождевой тучкой вместо герба.', 'Cigarettes with a rain cloud instead of a crest.'),
    'wesst': ('Сигареты с огромным названием на пачке.', 'Cigarettes with a huge name on the pack.'),
    'lambert': ('Английские сигареты с бутылочкой на пачке.', 'English cigarettes with a little bottle on the pack.'),
    'jps': ('Строгие сигареты с вензелем на пачке.', 'Strict cigarettes with a monogram on the pack.'),
    'mayfire': ('Сигареты с огоньком на пачке.', 'Cigarettes with a flame on the pack.'),
    'silkkut': ('Очень лёгкие сигареты с разрезанным шёлком на пачке.', 'Very light cigarettes with cut silk on the pack.'),
    'stuyvesand': ('Сигареты с полосками на пачке.', 'Cigarettes with stripes on the pack.'),
    'vicerroy': ('Сигареты со щитом на пачке.', 'Cigarettes with a shield on the pack.'),
    'salemm': ('Ментоловые сигареты с ведьминой шляпой на пачке.', 'Menthol cigarettes with a witch hat on the pack.'),
    'kooll': ('Ментоловые сигареты с пингвином на пачке.', 'Menthol cigarettes with a penguin on the pack.'),
    'spirrit': ('Сигареты из табака без добавок, с солнцем над горами.', 'Additive-free tobacco cigarettes with a sun over the mountains.'),
    'mewius': ('Японские сигареты с крылом на пачке.', 'Japanese cigarettes with a wing on the pack.'),
    'hopp': ('Короткие японские сигареты с луком и стрелой.', 'Short Japanese cigarettes with a bow and arrow.'),
    'muratty': ('Сигареты для дипломатов, с короной и птицей.', 'Cigarettes for diplomats, with a crown and a bird.'),
    'sovereing': ('Сигареты с золотой монетой на пачке.', 'Cigarettes with a gold coin on the pack.'),
}
MEME6 = {
    'kosmoss_classic': ('До космоса далеко, но звёзды над Великим Медвежьим видно отлично.', 'Space is far away, but you can see the stars over Great Bear just fine.'),
    'kosmoss_light': ('Лёгкие, как невесомость. Жаль, что рюкзак весит по-прежнему.', 'Light as zero gravity. Shame your backpack still weighs the same.'),
    'kosmoss_long100': ('Длинные, как полярная ночь, и тянутся столько же.', 'Long as the polar night, and they last about as long.'),
    'primma_original': ('Без фильтра, без жалоб и без шансов для лёгких.', 'No filter, no complaints, no chance for your lungs.'),
    'primma_filter': ('С фильтром. Пусть фильтр хоть что-то отфильтрует.', 'With a filter. Let the filter filter something at least.'),
    'primma_nostalgia': ('Вкус времён, когда главной угрозой были очереди, а не волки.', 'The taste of times when queues were the threat, not wolves.'),
    'pyotr_classic': ('Первый прорубил окно в Европу. Второй прорубил лунку на озере.', 'The First cut a window to Europe. The Second cut a hole in the lake ice.'),
    'pyotr_navy': ('Флотские. Корабль на пачке есть, а до ближайшего порта пешком.', 'Navy. There is a ship on the pack, and the nearest port is a walk away.'),
    'pyotr_light': ('Лёгкие. Император одобрил бы, если бы не замёрз.', 'Light. The emperor would approve, if he had not frozen.'),
    'optimma_blue': ('Оптимальный выбор, когда других нет.', 'The optimal choice when there is no other.'),
    'optimma_red': ('Красные. Оптимально для тех, кто уже всё решил.', 'Red. Optimal for those who have already made up their mind.'),
    'optimma_silver': ('Серебряные. Оптимально мало вреда. Оптимально.', 'Silver. Optimally little harm. Optimally.'),
    'chesterfeld_red': ('Смесь холодных табаков. Других тут и не бывает.', 'A blend of cold tobaccos. There are no other kinds here.'),
    'chesterfeld_blue': ('Синие, как губы после ночёвки без спальника.', 'Blue as your lips after a night without a bedroll.'),
    'chesterfeld_original': ('Без фильтра, как в старых фильмах про суровых мужчин.', 'No filter, like in old films about tough men.'),
    'phillip_blue': ('Раньше было лучше. Раньше было теплее.', 'It was better before. It was warmer before.'),
    'phillip_red': ('Крепкие и мрачные, как прогноз на неделю.', 'Strong and gloomy, like the weekly forecast.'),
    'phillip_novel': ('Тонкие, как надежда, что спасатели ещё летят.', 'Slim as the hope that the rescue is still coming.'),
    'wesst_red': ('Иди на запад. Только там тоже снег.', 'Go west. There is snow there too, though.'),
    'wesst_silver': ('Серебряные. Запад остаётся западом.', 'Silver. The west is still the west.'),
    'wesst_ice': ('Со льдом. Будто льда вокруг мало.', 'With ice. As if there were not enough ice around.'),
    'lambert_original': ('Ламберт курил, Боттлер наливал. Хорошая была компания.', 'Lambert smoked, Bottler poured. A good firm it was.'),
    'lambert_blue': ('Синие. Боттлер уже налил, Ламберт ещё держится.', 'Blue. Bottler has poured, Lambert is still holding on.'),
    'lambert_menthol': ('Ментол. Бутылку на пачке можно не искать: она пустая.', 'Menthol. Do not look for the bottle on the pack: it is empty.'),
    'jps_black': ('Чёрные, как ночь без фонаря. Молитесь, чтобы она была не последней.', 'Black as a night without a lantern. Pray it is not your last.'),
    'jps_blue': ('Синие. Джон молился о тепле. Ответа пока нет.', 'Blue. John prayed for warmth. No answer yet.'),
    'jps_silver': ('Серебряные. Особенные, как и каждый пережитый день.', 'Silver. Special, like every day you survive.'),
    'mayfire_original': ('Огонь на пачке не греет. Зато дым немного греет.', 'The fire on the pack does not warm you. The smoke does, a little.'),
    'mayfire_skyblue': ('Небесно-голубые. Небо здесь редко такое.', 'Sky blue. The sky here is rarely like that.'),
    'mayfire_gold': ('Золотые. Огонь стоит дороже золота, спросите кого угодно.', 'Gold. Fire is worth more than gold, ask anyone.'),
    'silkkut_purple': ('Мягкие, как шёлк. Шёлк, к сожалению, не греет.', 'Smooth as silk. Silk, sadly, does not keep you warm.'),
    'silkkut_silver': ('Почти без смол. Почти как не курить, только с дымом.', 'Almost no tar. Almost like not smoking, only with smoke.'),
    'silkkut_blue': ('Синий шёлк, разрезанный ножом. Нож лучше оставить для мяса.', 'Blue silk cut with a knife. Better keep the knife for meat.'),
    'stuyvesand_red': ('Интернэшнл. Международный, а курить всё равно одному.', 'International. International, and still you smoke alone.'),
    'stuyvesand_blue': ('Синие полоски. Песок попадает везде, снег тоже.', 'Blue stripes. Sand gets everywhere, and so does snow.'),
    'stuyvesand_silver': ('Серебряные. Питер на пачке выглядит бодрее вас.', 'Silver. Peter on the pack looks livelier than you.'),
    'vicerroy_red': ('Щит на пачке. От волка не защитит.', 'A shield on the pack. It will not stop a wolf.'),
    'vicerroy_blue': ('Синие. Вице-король без королевства, как и вы.', 'Blue. A viceroy without a kingdom, just like you.'),
    'vicerroy_black': ('Чёрные. Щит есть, доспехов нет.', 'Black. There is a shield, but no armour.'),
    'salemm_green': ('Ведьмин ментол. Колдовать не научит, но освежит.', 'Witch menthol. Will not teach you magic, but it refreshes.'),
    'salemm_silver': ('Серебряные. Против оборотней не помогут, против волков тоже.', 'Silver. No good against werewolves, nor against wolves.'),
    'salemm_black': ('Чёрные с ментолом. Шляпа на пачке, а на вас только шапка-ушанка.', 'Black menthol. A hat on the pack, and you only have a toque.'),
    'kooll_green': ('Пингвин на пачке единственный, кому тут комфортно.', 'The penguin on the pack is the only one comfortable here.'),
    'kooll_blue': ('Синие. Сохраняйте спокойствие, как пингвин.', 'Blue. Keep calm, like a penguin.'),
    'kooll_extraice': ('Экстра-лёд. Как будто снаружи его мало.', 'Extra ice. As if there were not enough outside.'),
    'spirrit_yellow': ('Без добавок. Зато с последствиями.', 'No additives. Consequences included.'),
    'spirrit_turquoise': ('Бирюзовые. Цвет летнего озера, которого вы больше не увидите.', 'Turquoise. The colour of a summer lake you will not see again.'),
    'spirrit_perique': ('Перик. Крепкий, тёмный, честный.', 'Perique. Strong, dark, honest.'),
    'mewius_skyblue': ('Мяу. Японцы знают толк в мягком дыме.', 'Meow. The Japanese know their way around mild smoke.'),
    'mewius_original': ('Оригинальные. Крыло на пачке, а лететь некуда.', 'Original. A wing on the pack, and nowhere to fly.'),
    'mewius_optionpurple': ('Фиолетовые, со сладким вкусом. Котики одобряют.', 'Purple, with a sweet taste. Cats approve.'),
    'hopp_original': ('Короткие. Надежда умирает последней, сигарета чуть раньше.', 'Short. Hope dies last, the cigarette a bit earlier.'),
    'hopp_menthol': ('Ментол. Стрела летит вверх, а температура вниз.', 'Menthol. The arrow flies up, the temperature goes down.'),
    'hopp_superlight': ('Супер лёгкие. Надежды в них больше, чем табака.', 'Super light. More hope in them than tobacco.'),
    'muratty_ambassador': ('Для послов. Ваше посольство: печка и запас дров.', 'For ambassadors. Your embassy: a stove and a woodpile.'),
    'muratty_blue': ('Синие. Дипломатия с волками пока не удаётся.', 'Blue. Diplomacy with wolves is not working out yet.'),
    'muratty_white': ('Белые. Флаг такого же цвета, только волки его не понимают.', 'White. The flag is the same colour, but the wolves do not get it.'),
    'sovereing_blue': ('Монета на пачке. Купить на неё здесь нечего.', 'A coin on the pack. There is nothing to buy with it here.'),
    'sovereing_red': ('Красные. Король на монете тоже выглядит замёрзшим.', 'Red. The king on the coin looks frozen too.'),
    'sovereing_silver': ('Серебряные. Ценятся на вес, а весят мало.', 'Silver. Valued by weight, and they weigh little.'),
}
BLURB.update(BLURB6)
MEME.update(MEME6)
PAPER = {(240, 238, 232): ('белая', 'white'), (128, 82, 50): ('коричневая', 'brown'), (78, 48, 30): ('тёмно-коричневая', 'dark brown'),
         (26, 24, 24): ('чёрная', 'black'), (196, 196, 196): ('серая', 'grey')}
FILTER = {'cork': ('пробковый', 'cork'), 'white': ('белый', 'white'), 'black': ('чёрный', 'black'), 'gold': ('золотой', 'gold'),
          'brown': ('коричневый', 'brown'), 'color': ('цветной', 'coloured'), 'none': ('без фильтра', 'no filter'), 'papirosa': ('гильза', 'paper tube')}
TAIL = ('20 штук.', '20 in a pack.')


def fmt_num(x):
    return ('%g' % x).replace('.', ',')


def texts(bkey, bname, v, tk=''):
    sr, se = STRENGTH_NAME[v['strength']]
    fat, hung, hp, _ = catalog.STRENGTH[v['strength']]
    if v['bonus'] == 'coffee':
        fat += 2
    if v['bonus'] == 'sweet':
        hung += 10
    pr, pe = PAPER.get(tuple(v['paper']), ('цветная', 'coloured'))
    fr, fe = FILTER[v['filter']]
    br, be = BONUS[v['bonus']]
    xr, xe = SIDE[v.get('side', '')]
    br, be = (br + ' ' + xr).strip(), (be + ' ' + xe).strip()
    mr, me = MEME.get(tk, BLURB[bkey])
    ru = (f"{mr} {v['name_ru']}: {sr}, бумага {pr}, фильтр {fr}. {TAIL[0]} "
          f"Сигарета: усталость −{fmt_num(fat)}%, голод на 10% медленнее {fmt_num(hung)} мин, здоровье −{fmt_num(hp)}%. {br}").strip()
    en = (f"{me} {v['name_en']}: {se}, {pe} paper, {fe} filter. {TAIL[1]} "
          f"A cigarette: fatigue −{fat:g}%, hunger 10% slower for {hung:g} min, condition −{hp:g}%. {be}").strip()
    return (f"Сигареты {bname} {v['name_en']}", ru), (f"{bname} {v['name_en']} cigarettes", en)


# ---- fpmesh (same conventions as npz_to_fpmesh.py)
def rx(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]], np.float32)


def ry(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]], np.float32)


def write(path, v, n, uv, tris):
    U = np.stack([-v[:, 0], v[:, 2], -v[:, 1]], 1)
    N = np.stack([-n[:, 0], n[:, 2], -n[:, 1]], 1)
    t = tris[:, [0, 2, 1]].reshape(-1)
    with open(path, 'wb') as f:
        f.write(struct.pack('<ii', len(U), len(t)))
        f.write(U.astype('<f4').tobytes())
        f.write(N.astype('<f4').tobytes())
        f.write(uv.astype('<f4').tobytes())
        f.write(t.astype('<i4').tobytes())


def meshes(fmt):
    d = np.load(os.path.join(B, 'cig_%s.npz' % fmt))
    v, n, uv, t = d['verts'].astype(np.float32), d['normals'].astype(np.float32), d['uvs'].astype(np.float32), d['tris']
    # lying at rest along X
    R = ry(math.pi / 2)
    vr, nr = v @ R.T, n @ R.T
    lo, hi = vr.min(0), vr.max(0)
    vr = vr - np.array([(lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, lo[2]], np.float32)
    write(os.path.join(DIST, 'meshes', 'cig_%s.fpmesh' % fmt), vr, nr, uv, t)
    # held: mouth (filter, z=0) at the origin, lit end towards Unity +Z
    vh = v.copy()
    vh[:, 2] -= vh[:, 2].min()
    R = rx(math.pi / 2)
    write(os.path.join(DIST, 'meshes', 'cig_%s_held.fpmesh' % fmt), vh @ R.T, n @ R.T, uv, t)


def cs_str(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'


if __name__ == '__main__':
    ru, en = {}, {}
    rows, fmts = [], set()
    for bkey, bname, _, v in catalog.all_varieties():
        iid = catalog.item_id(bname, v)
        tk = catalog.tex_key(bkey, v)
        (nr, dr), (ne, de) = texts(bkey, bname, v, tk)
        ru['SMOKES_%s_NAME' % iid], ru['SMOKES_%s_DESC' % iid] = nr, dr
        en['SMOKES_%s_NAME' % iid], en['SMOKES_%s_DESC' % iid] = ne, de
        fmts.add(v['fmt'])
        shutil.copy(os.path.join(TEX, 'pack_%s.jpg' % tk), os.path.join(DIST, 'textures', 'pack_%s.jpg' % tk))
        shutil.copy(os.path.join(TEX, 'cig_%s.jpg' % tk), os.path.join(DIST, 'textures', 'cig_%s.jpg' % tk))
        rows.append('        new(%s, %s, %s, %s, %s, %s, %d, %s, %s, %s),' % (
            cs_str(iid), cs_str(bkey), cs_str(catalog.pack_mesh(bkey, v)), cs_str('pack_%s.jpg' % tk), cs_str('cig_%s.jpg' % tk),
            cs_str('cig_' + v['fmt']), v['strength'], cs_str(v['bonus']), 'true' if v['rare'] else 'false', cs_str(v.get('side', ''))))
    for f in fmts:
        meshes(f)
    fx = {'Fresh': (('Свежий вдох', 'Ментол освежает: выносливость восстанавливается на 10% быстрее.'), ('Fresh breath', 'Menthol freshness: stamina comes back 10% faster.')),
          'Warm': (('Тёплый дым', 'Ощущаемая температура на 1°C выше.'), ('Warm smoke', 'Feels-like temperature 1°C higher.')),
          'Calm': (('Спокойствие', 'Прицел качается на 25% меньше.'), ('Calm', 'Aim sway 25% less.')),
          'Hands': (('Твёрдые пальцы', 'Шанс развести огонь на 10% выше.'), ('Steady fingers', 'Fire-starting chance 10% higher.')),
          'Steady': (('Ровное дыхание', 'На бегу выносливость тратится на 15% медленнее.'), ('Steady breath', 'Sprinting drains stamina 15% slower.')),
          'Dry': (('Сухость во рту', 'Крепкий дым: жажда растёт на 15% быстрее.'), ('Dry mouth', 'Strong smoke: thirst builds 15% faster.')),
          'Dizzy': (('Головокружение', 'Слишком крепко: прицел качается на 30% сильнее.'), ('Dizzy', 'Too strong: aim sway 30% more.'))}
    for k, (r, e) in fx.items():
        ru['SMOKES_FX_%s_NAME' % k], ru['SMOKES_FX_%s_DESC' % k] = r
        en['SMOKES_FX_%s_NAME' % k], en['SMOKES_FX_%s_DESC' % k] = e
    for lang, d in (('ru', ru), ('en', en)):
        with open(os.path.join(DIST, 'data', 'text_var_%s.json' % lang), 'w', encoding='utf-8') as fp:
            json.dump(d, fp, ensure_ascii=False, indent=1)
    with open(os.path.join(SRC, 'Catalog.cs'), 'w', encoding='utf-8') as fp:
        fp.write('namespace SmokesByEMPEROR;\n\n/// <summary>Generated by tools/gen_catalog.py from tools/catalog.py: the cigarette varieties (one pack item each).</summary>\n')
        fp.write('public static class Catalog\n{\n    public sealed record Var(string Id, string Brand, string PackMesh, string PackTexture, string CigTexture, string CigMesh, int Strength, string Bonus, bool Rare, string Side);\n\n')
        fp.write('    public static readonly Var[] All =\n    {\n' + '\n'.join(rows) + '\n    };\n}\n')
    print('varieties', len(rows), 'formats', sorted(fmts))

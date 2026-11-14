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

# batch 7 (11-14): Canada, USSR / Russia, the world, three new shapes
BLURB7 = {
    'exporteh': ('Канадские сигареты с кленовым листом на пачке.', 'Canadian cigarettes with a maple leaf on the pack.'),
    'dumorrier': ('Канадские сигареты в строгой пачке с росписью.', 'Canadian cigarettes in a formal pack with a signature.'),
    'playerz': ('Флотские сигареты с моряком в спасательном круге.', 'Navy cigarettes with a sailor in a lifebuoy.'),
    'kazbekk': ('Папиросы с всадником на фоне гор.', 'Papirosy with a horseman against the mountains.'),
    'gertsegovina': ('Дорогие папиросы в тёмной коробке с золотом.', 'Expensive papirosy in a dark box with gold.'),
    'pomir': ('Дешёвые крепкие сигареты с путником в горах.', 'Cheap strong cigarettes with a wanderer in the mountains.'),
    'tu143': ('Болгарские сигареты с самолётом на пачке.', 'Bulgarian cigarettes with an airliner on the pack.'),
    'stewardesa': ('Болгарские сигареты с крылатой эмблемой.', 'Bulgarian cigarettes with a winged badge.'),
    'soyuz': ('Сигареты в честь стыковки двух кораблей на орбите.', 'Cigarettes in honour of two spacecraft docking in orbit.'),
    'rothhandel': ('Немецкие сигареты с ладонью на пачке.', 'German cigarettes with an open hand on the pack.'),
    'doublehappy': ('Китайские сигареты с парным знаком радости.', 'Chinese cigarettes with the paired sign of joy.'),
    'moar': ('Длинные коричневые сигареты, сто двадцать миллиметров.', 'Long brown cigarettes, a hundred and twenty millimetres.'),
    'cigaronni': ('Армянские сигареты с очень длинным мундштуком.', 'Armenian cigarettes with a very long mouthpiece.'),
    'gudang': ('Индонезийский кретек: табак с гвоздикой, свёрнут конусом.', 'Indonesian kretek: tobacco with cloves, rolled into a cone.'),
    'pattaka': ('Индийские биди: табак в листе, перевязан ниткой.', 'Indian bidis: tobacco in a leaf, tied with a thread.'),
}
MEME7 = {
    'exporteh_green': ('Экспорт, который так никуда и не уехал. Как и ты.', 'An export that never went anywhere. Much like you.'),
    'exporteh_blue': ('Средние. Извиняются за дым раньше, чем ты выдохнешь.', 'Medium. They apologise for the smoke before you breathe out.'),
    'exporteh_gold': ('Мягкие, как канадское «простите». Волки, правда, не извиняются.', 'Smooth as a Canadian "sorry". The wolves never say it, though.'),
    'dumorrier_signature': ('Роспись на пачке есть. Осталось найти того, кто примет чек.', 'The pack is signed. Now find someone who takes cheques.'),
    'dumorrier_distinct': ('Изысканные. Курить, отставив мизинец в рукавице.', 'Refined. To be smoked with a pinky raised inside the mitten.'),
    'dumorrier_noir': ('Чёрные, как ночь без сияния. С сиянием, впрочем, не веселее.', 'Black as a night without the aurora. Not that the aurora helps.'),
    'playerz_navycut': ('Флотская нарезка. Флота нет, нарезка осталась.', 'Navy cut. The navy is gone, the cut remains.'),
    'playerz_filter': ('Моряк на пачке смотрит вдаль. Там тоже лёд.', 'The sailor on the pack looks into the distance. More ice there.'),
    'playerz_light': ('Лёгкие. Спасательный круг в комплект не входит.', 'Light. Lifebuoy not included.'),
    'kazbekk_classic': ('Всадник скачет к вершине уже сто лет. Ты к маяку идёшь дольше.', 'The rider has galloped for the peak a hundred years. Your walk to the lighthouse takes longer.'),
    'kazbekk_summit': ('Свежие, как воздух на перевале. Где тебя и продует.', 'Fresh as the air on a mountain pass. Where you catch your chill.'),
    'kazbekk_nightrider': ('Ночной всадник. Днём его всё равно не видно в метели.', 'The night rider. By day the blizzard hides him anyway.'),
    'gertsegovina_classic': ('Говорят, их крошили в трубку. Трубка у тебя как раз есть.', 'They say these were crumbled into a pipe. You happen to have one.'),
    'gertsegovina_lux': ('Люкс. Единственная роскошь на сто километров вокруг.', 'Lux. The only luxury for a hundred kilometres around.'),
    'gertsegovina_emerald': ('Изумрудные. Камней внутри нет, проверяли.', 'Emerald. No stones inside, we checked.'),
    'pomir_classic': ('Путник на пачке идёт с посохом в горы. Узнаёшь себя?', 'The wanderer on the pack walks into the mountains with a staff. Look familiar?'),
    'pomir_iceaxe': ('Ледоруб. Бьёт по лёгким примерно так же.', 'Ice Axe. Hits the lungs in about the same way.'),
    'pomir_basecamp': ('С фильтром: уступка тем, кто собирается вернуться с горы.', 'With a filter: a concession to those who plan to come back down.'),
    'tu143_classic': ('Самолёт на пачке ещё летит. В отличие от твоего.', 'The plane on the pack is still flying. Unlike yours.'),
    'tu143_lights': ('Лёгкие. Набирают высоту плавно, без воздушных ям.', 'Lights. A smooth climb, no air pockets.'),
    'tu143_businessclass': ('Бизнес-класс: тот же сугроб, но с золотым ободком.', 'Business class: the same snowdrift, with a gold rim.'),
    'stewardesa_classic': ('«Желаем приятного полёта». Посадка вышла жёсткой.', '"Have a pleasant flight." The landing came out rough.'),
    'stewardesa_sky': ('Небесные с ментолом. Небо тут и так холодное.', 'Sky with menthol. The sky here is cold enough already.'),
    'stewardesa_firstclass': ('Первый класс. Плед и горячий чай добудешь сам.', 'First class. Find your own blanket and hot tea.'),
    'soyuz_special': ('Два корабля состыковались на орбите. Тебе бы с костром состыковаться.', 'Two ships docked in orbit. You would settle for docking with a campfire.'),
    'soyuz_lights': ('Лёгкие, как рукопожатие в невесомости.', 'Light as a handshake in zero gravity.'),
    'soyuz_orbit': ('С орбиты сияние красивое. Снизу оно включает всё, что не просили.', 'From orbit the aurora is pretty. Down here it switches on everything nobody asked for.'),
    'rothhandel_ohnefilter': ('Без фильтра и без компромиссов. Ладонь на пачке говорит «стой».', 'No filter and no compromise. The hand on the pack says "stop".'),
    'rothhandel_filter': ('С фильтром. Немцы подумали и всё-таки добавили.', 'With a filter. The Germans thought it over and added one after all.'),
    'rothhandel_blau': ('Синие. Крепость по расписанию, строго в меру.', 'Blue. Strength on schedule, strictly in measure.'),
    'doublehappy_red': ('Двойная радость: нашёл пачку и нашёл спички.', 'Double joy: found a pack and found matches.'),
    'doublehappy_gold': ('Золотой дракон. Огнём дышишь ты, он только на пачке.', 'Golden dragon. You do the fire-breathing, he just sits on the pack.'),
    'doublehappy_jade': ('Нефритовые с ментолом. Радость та же, дыхание свежее.', 'Jade with menthol. Same joy, fresher breath.'),
    'moar_red': ('Сто двадцать миллиметров. Потому что ста было мало.', 'A hundred and twenty millimetres. Because a hundred was not enough.'),
    'moar_menthol': ('Длинные и мятные. Хватит на весь переход через озеро.', 'Long and minty. Lasts the whole walk across the lake.'),
    'moar_beige': ('Бежевые. Самая длинная сладость в рюкзаке.', 'Beige. The longest sweet thing in your pack.'),
    'cigaronni_black': ('Мундштука больше, чем табака. Зато пальцы далеко от огня.', 'More mouthpiece than tobacco. Keeps your fingers far from the fire, though.'),
    'cigaronni_white': ('Белые. Выглядят дорого даже в заброшенной рыбацкой хижине.', 'White. They look expensive even in an abandoned fishing hut.'),
    'cigaronni_blue': ('Синие и лёгкие. Курить недолго, держать удобно.', 'Blue and light. A short smoke, a comfortable hold.'),
    'gudang_merah': ('Потрескивает гвоздикой, как костёр из сырой ели.', 'Crackles with cloves like a fire of damp spruce.'),
    'gudang_djaja': ('Сладкий дым с пряностями. Медведь учует издалека.', 'Sweet spiced smoke. The bear will smell it from afar.'),
    'gudang_klobot': ('Завёрнуты в кукурузный лист. Початок от трубки привет передаёт.', 'Wrapped in a corn husk. The corncob pipe says hello.'),
    'pattaka_redthread': ('«Паттака» значит хлопушка. Бьёт соответственно.', '"Pattaka" means firecracker. It hits accordingly.'),
    'pattaka_greenthread': ('Лист, нитка и мята. Сделано руками, которым было теплее, чем твоим.', 'A leaf, a thread and mint. Made by hands warmer than yours.'),
    'pattaka_mango': ('С манго. Ближайшее манго в десяти тысячах километров к югу.', 'With mango. The nearest mango is ten thousand kilometres south.'),
}
BLURB.update(BLURB7)
MEME.update(MEME7)
BLURB8 = {
    'chunghwah': ('Китайские сигареты с воротами на пачке. Дома их дарят начальству.', 'Chinese cigarettes with a gate on the pack. Back home they are a gift for the boss.'),
    'hongta': ('Китайские сигареты с пагодой на холме.', 'Chinese cigarettes with a pagoda on a hill.'),
    'yellowcrane': ('Китайские сигареты с журавлём и башней.', 'Chinese cigarettes with a crane and a tower.'),
    'pandah': ('Китайские сигареты с пандой.', 'Chinese cigarettes with a panda.'),
    'yuxee': ('Китайские сигареты из табачной провинции Юньнань.', 'Chinese cigarettes from the tobacco province of Yunnan.'),
    'zhongnan': ('Пекинские сигареты с волнами на пачке.', 'Beijing cigarettes with waves on the pack.'),
    'sevenwolfs': ('Китайские сигареты с воющим волком.', 'Chinese cigarettes with a howling wolf.'),
    'lotusking': ('Китайские сигареты с лотосом.', 'Chinese cigarettes with a lotus.'),
    'goldenleef': ('Китайские сигареты с золотым листом.', 'Chinese cigarettes with a golden leaf.'),
    'sevnstars': ('Японские сигареты с угольным фильтром.', 'Japanese cigarettes with a charcoal filter.'),
    'peece': ('Японские сигареты с голубем и веткой.', 'Japanese cigarettes with a dove and a twig.'),
    'goldenbatt': ('Самые старые японские сигареты, с летучими мышами.', 'The oldest Japanese cigarettes, with bats on the pack.'),
    'hilyte': ('Японские сигареты с ромовой отдушкой.', 'Japanese cigarettes with a touch of rum.'),
    'wakabah': ('Дешёвые японские сигареты с молодым листком.', 'Cheap Japanese cigarettes with a young leaf.'),
    'shinsey': ('Простые японские сигареты послевоенных лет.', 'Plain Japanese cigarettes of the post-war years.'),
    'cherrie': ('Японские сигареты с цветущей сакурой.', 'Japanese cigarettes with cherry blossom.'),
    'asahee': ('Старинные японские папиросы с бумажным мундштуком.', 'Old Japanese mouthpiece cigarettes with a paper tube.'),
    'raizon': ('Корейские сигареты с котом на пачке.', 'Korean cigarettes with a cat on the pack.'),
    'vinatabak': ('Вьетнамские сигареты со звездой в венке.', 'Vietnamese cigarettes with a star in a wreath.'),
    'djisamsue': ('Индонезийский кретек ручной скрутки, без фильтра.', 'Hand-rolled Indonesian kretek, no filter.'),
    'charminarr': ('Крепкие индийские сигареты с четырьмя минаретами.', 'Strong Indian cigarettes with four minarets.'),
    'longglife': ('Тайваньские сигареты со знаком долголетия.', 'Taiwanese cigarettes with the sign of long life.'),
}
MEME8 = {
    'chunghwah_soft': ('Такие дарят начальству. Начальство здесь теперь ты.', 'These are a gift for the boss. Out here the boss is you.'),
    'chunghwah_hard': ('Твёрдая пачка. Не помнётся, даже если на неё сядет лось.', 'Hard pack. Will not crumple even if a moose sits on it.'),
    'chunghwah_golden': ('Золотые ворота. Открываются в метель, как и все остальные.', 'Golden gate. Opens onto a blizzard, like every other one.'),
    'hongta_classic': ('Пагода на холме. До неё дальше, чем до любой здешней шахты.', 'A pagoda on a hill. Farther away than any mine you know.'),
    'hongta_blue': ('Мягкая синяя пачка. Мягкого здесь больше ничего нет.', 'A soft blue pack. Nothing else out here is soft.'),
    'hongta_red': ('Красная пагода. На снегу видно издалека, волкам тоже.', 'Red pagoda. Easy to spot on the snow, for wolves too.'),
    'yellowcrane_n1916': ('Номер 1916. Журавль давно улетел, башня осталась.', 'No. 1916. The crane flew off long ago, the tower stayed.'),
    'yellowcrane_blue': ('Журавли на зиму улетают на юг. Умные птицы.', 'Cranes fly south for the winter. Clever birds.'),
    'yellowcrane_gold': ('Золотая башня. Вид с неё, говорят, лучше, чем с вышки.', 'Golden tower. The view is said to beat the lookout.'),
    'pandah_classic': ('Панда тоже медведь. Но этот хотя бы не гонится.', 'A panda is a bear too. At least this one does not chase you.'),
    'pandah_bamboo': ('С ментолом. Бамбук тут не растёт, рогоз не предлагать.', 'With menthol. No bamboo grows here, and cattail does not count.'),
    'pandah_night': ('Ночная панда. Спит шестнадцать часов, тебе столько не дадут.', 'Night panda. Sleeps sixteen hours a day, you will not get that many.'),
    'yuxee_classic': ('Нефритовый ручей. Местные ручьи зимой молчат.', 'Jade stream. The local streams keep quiet in winter.'),
    'yuxee_jade': ('С ментолом. Холодок как от воды из проруби.', 'With menthol. Cool as water from an ice hole.'),
    'yuxee_soft': ('Мягкие. Для вечера у печки, когда дрова уже наколоты.', 'Soft ones. For an evening by the stove, once the wood is chopped.'),
    'zhongnan_blue8': ('Восьмёрка. Море на пачке не замерзает, в отличие от здешнего.', 'The eight. The sea on the pack never freezes, unlike the local one.'),
    'zhongnan_white5': ('Пятёрка. Лёгкие, как первый снег.', 'The five. Light as the first snow.'),
    'zhongnan_night1': ('Единичка. Почти воздух, только платный.', 'The one. Nearly air, only you pay for it.'),
    'sevenwolfs_white': ('Семь волков на пачке. За дверью их, бывает, больше.', 'Seven wolves on the pack. Outside the door there may be more.'),
    'sevenwolfs_blue': ('Синяя луна. Выть на неё не обязательно.', 'Blue moon. Howling at it is optional.'),
    'sevenwolfs_red': ('Красная стая. Эти хотя бы не кусаются.', 'Red pack. These at least do not bite.'),
    'lotusking_yellow': ('Король лотосов. Царство его далеко и там тепло.', 'King of the lotuses. His kingdom is far away and warm.'),
    'lotusking_blue': ('Синий лотос с ментолом. Цветёт на воде, а не на льду.', 'Blue lotus with menthol. Blooms on water, not on ice.'),
    'lotusking_pink': ('Розовый цвет. Единственный на всём острове.', 'Pink blossom. The only one on the whole island.'),
    'goldenleef_imperial': ('Императорский лист. Императору и положено.', 'Imperial leaf. Only fitting for an emperor.'),
    'goldenleef_harvest': ('Урожай собран. Тебе осталось собрать рогоз.', 'The harvest is in. All you have left to gather is cattail.'),
    'goldenleef_sky': ('Небесный лист с ментолом. Небо здесь щедрое только на снег.', 'Sky leaf with menthol. The sky here is generous only with snow.'),
    'sevnstars_classic': ('Семь звёзд. В ясную ночь над озером их побольше.', 'Seven stars. On a clear night over the lake there are a few more.'),
    'sevnstars_menthol': ('Семь звёзд с ментолом. Мороз в подарок.', 'Seven stars with menthol. Frost comes free.'),
    'sevnstars_black': ('Уголь в фильтре. Тот самый, что ты носишь из шахты.', 'Charcoal in the filter. The very kind you carry out of the mine.'),
    'peece_navy': ('Короткие и без фильтра. Мир и покой на двадцать затяжек.', 'Short and plain. Peace and quiet for twenty drags.'),
    'peece_lights': ('Лёгкий мир. Голубь с веткой сюда не долетит, вороны долетят.', 'A light peace. The dove will not make it here, the crows will.'),
    'peece_royal': ('Королевский аромат. Пахнет ванилью, а не оленьей шкурой.', 'Royal aroma. Smells of vanilla, not of deer hide.'),
    'goldenbatt_classic': ('Курили ещё при керосиновых лампах. Лампа у тебя как раз есть.', 'Smoked back when lamps burned oil. You happen to have one.'),
    'goldenbatt_filter': ('Летучая мышь к удаче. В пещерах проверишь.', 'A bat means luck. Check it in the caves.'),
    'goldenbatt_mild': ('Мягкие. Мыши на пачке золотые, в шахте обычные.', 'Mild. Golden bats on the pack, ordinary ones in the mine.'),
    'hilyte_blue': ('С запахом рома. Самого рома в пачке нет, не ищи.', 'Smells of rum. No actual rum in the pack, do not look.'),
    'hilyte_menthol': ('Высокий свет с ментолом. Как сияние, только без проводов.', 'High light with menthol. Like the aurora, minus the wires.'),
    'hilyte_white': ('Белая пачка. На снегу потерять проще простого.', 'White pack. Easiest thing to lose in the snow.'),
    'wakabah_green': ('Молодой лист. Дёшево и сердито, как костёр из палок.', 'Young leaf. Cheap and honest, like a fire of sticks.'),
    'wakabah_spring': ('Весенняя мята. Весну здесь обещают давно.', 'Spring mint. Spring has been promised here for a long time.'),
    'wakabah_autumn': ('Осенний лист. Осень была, ты её помнишь.', 'Autumn leaf. There was an autumn, you remember it.'),
    'shinsey_plain': ('Новая жизнь каждое утро. Если пережил ночь.', 'A new life every morning. If you made it through the night.'),
    'shinsey_redsun': ('Красное солнце. Греет лучше здешнего.', 'Red sun. Warmer than the local one.'),
    'shinsey_filter': ('С фильтром. Новая жизнь, но поосторожнее.', 'With a filter. A new life, only more careful.'),
    'cherrie_sakura': ('Сакура цветёт неделю. Пачки хватит примерно на столько же.', 'Cherry blossom lasts a week. So does the pack, more or less.'),
    'cherrie_yozakura': ('Ночная сакура. Любоваться при фонаре.', 'Night blossom. Best admired by lantern light.'),
    'cherrie_classic': ('Красная классика. Лепестки не облетят, они нарисованы.', 'Classic red. The petals will not fall, they are printed.'),
    'asahee_morning': ('Утреннее солнце. Мундштук длинный, чтобы рукавицы не жечь.', 'Morning sun. The tube is long, to keep the mittens unburnt.'),
    'asahee_fuji': ('Гора Фудзи. Повыше Волчьей горы, и снега там поменьше.', 'Mount Fuji. Taller than Timberwolf Mountain, with less snow.'),
    'asahee_imperial': ('Императорский подарок. Вручается лично, зимой и без церемоний.', 'An imperial gift. Handed over in person, in winter, no ceremony.'),
    'raizon_blue': ('У кота девять жизней. У тебя одна, береги.', 'The cat has nine lives. You have one, look after it.'),
    'raizon_black': ('Чёрный кот. Дорогу тут перебегают в основном волки.', 'Black cat. Around here it is mostly wolves that cross your path.'),
    'raizon_ice': ('Ледяной кот с ментолом. Гладить не советуем.', 'Ice cat with menthol. Petting is not advised.'),
    'vinatabak_gold': ('Из страны, где не бывает снега. Трудно поверить.', 'From a country that never sees snow. Hard to believe.'),
    'vinatabak_red': ('Красная звезда. Пахнет тёплым дождём, которого тут нет.', 'Red star. Smells of warm rain, of which there is none here.'),
    'vinatabak_menthol': ('Ментол с большой реки. Она, говорят, не замерзает.', 'Menthol from the big river. It never freezes, they say.'),
    'djisamsue_kretek': ('Два, три, пять. В сумме десять, к удаче.', 'Two, three, five. Ten in all, for luck.'),
    'djisamsue_magnum': ('Толстый кретек. Гвоздика трещит громче костра.', 'A fat kretek. The cloves crackle louder than the campfire.'),
    'djisamsue_super': ('Пряный дым. В избушке будет пахнуть до весны.', 'Spiced smoke. The cabin will smell of it till spring.'),
    'charminarr_plain': ('Четыре башни, ни одного фильтра.', 'Four towers and not a single filter.'),
    'charminarr_gold': ('С золотым фильтром. Башни те же, кашель потише.', 'With a gold filter. Same towers, a quieter cough.'),
    'charminarr_special': ('Особые. Для тех, кто никуда не спешит. Здесь спешить некуда.', 'Special. For people in no hurry. Nowhere to hurry here anyway.'),
    'longglife_yellow': ('Долгая жизнь. Название честно ничего не обещает.', 'Long life. The name honestly promises nothing.'),
    'longglife_white': ('Белая пачка долголетия. Ирония в комплект не входит.', 'The white pack of long life. Irony not included.'),
    'longglife_blue': ('Долгая жизнь с ментолом. Начни с того, что переживи ночь.', 'Long life with menthol. Start by getting through the night.'),
}
BLURB.update(BLURB8)
MEME.update(MEME8)
BLURB9 = {
    'boyarrd': ('Французские сигареты из тёмного табака.', 'French cigarettes of dark tobacco.'),
    'dukados': ('Испанские сигареты из чёрного табака.', 'Spanish cigarettes of black tobacco.'),
    'f7': ('Сигареты из Восточной Германии, простые и надёжные.', 'East German cigarettes, plain and dependable.'),
    'drinna': ('Югославские сигареты с мостом через реку.', 'Yugoslav cigarettes with a bridge over the river.'),
    'karpatzi': ('Румынские сигареты с горами на пачке.', 'Romanian cigarettes with mountains on the pack.'),
    'delikados': ('Мексиканские сигареты, овальные в сечении.', 'Mexican cigarettes, oval in section.'),
    'farros': ('Мексиканские сигареты с маяком, в рисовой бумаге.', 'Mexican cigarettes with a lighthouse, in rice paper.'),
    'kleopatra': ('Египетские сигареты с пирамидами.', 'Egyptian cigarettes with pyramids.'),
    'larkk': ('Американские сигареты с угольным фильтром.', 'American cigarettes with a charcoal filter.'),
    'eeve': ('Длинные дамские сигареты с цветами.', 'Long ladies\' cigarettes with flowers.'),
    'kravena': ('Британские сигареты с чёрным котом.', 'British cigarettes with a black cat.'),
    'troikka': ('Российские сигареты с тройкой лошадей.', 'Russian cigarettes with a team of three horses.'),
    'maksimm': ('Российские сигареты покрепче и подешевле.', 'Russian cigarettes, stronger and cheaper.'),
    'egoriy': ('Российские сигареты со щитом и копьём.', 'Russian cigarettes with a shield and a spear.'),
    'donskoy': ('Сигареты с юга России, с шашками на пачке.', 'Cigarettes from the south of Russia, with sabres on the pack.'),
}
MEME9 = {
    'boyarrd_mais': ('Маисовая бумага. Гаснет сама, раскуривай заново, времени полно.', 'Maize paper. Goes out by itself, relight it, you have time.'),
    'boyarrd_caporal': ('Капральский табак. Рядовым не положено, но рядовых тут нет.', 'Corporal\'s tobacco. Not for privates, but there are none here.'),
    'boyarrd_legere': ('Лёгкие. Для храбрецов, которые берегут горло.', 'Light ones. For the brave who mind their throat.'),
    'dukados_negro': ('Чёрный табак. Чернее только ночь без фонаря.', 'Black tobacco. Only a night without a lantern is blacker.'),
    'dukados_rubio': ('Светлый табак в красной пачке. Сиеста в комплект не входит.', 'Blond tobacco in a red pack. Siesta not included.'),
    'dukados_azul': ('Голубые с ментолом. Море тут такого цвета не бывает.', 'Blue with menthol. The sea here never is that colour.'),
    'f7_original': ('Без фильтра и без излишеств. План по дыму выполнен.', 'No filter, no frills. The smoke quota has been met.'),
    'f7_filter': ('С фильтром. Прогресс дошёл и до нас.', 'With a filter. Progress got here too.'),
    'f7_blau': ('Синие мягкие. Дефицит, брали по блату.', 'Mild blue ones. Scarce, got through a friend.'),
    'drinna_classic': ('Без фильтра. Мост на пачке стоит четыреста лет, пачка столько не проживёт.', 'No filter. The bridge on the pack has stood four hundred years, the pack will not.'),
    'drinna_filter': ('С фильтром. Река зелёная, дым синий.', 'With a filter. The river is green, the smoke is blue.'),
    'drinna_jedina': ('Единственная и лёгкая. Других на полке всё равно не было.', 'The one and only, and light. Nothing else on the shelf anyway.'),
    'karpatzi_fara': ('Без фильтра. Горный воздух, только наоборот.', 'No filter. Mountain air, only the other way round.'),
    'karpatzi_filtru': ('С фильтром. Горы на пачке пониже здешних, и медведей там меньше.', 'With a filter. The mountains on the pack are lower than ours, with fewer bears.'),
    'karpatzi_munte': ('Гора с ментолом. Свежо, как на перевале.', 'Mountain with menthol. Fresh as a mountain pass.'),
    'delikados_ovalados': ('Овальные. Со стола не скатятся, удобно.', 'Oval ones. Will not roll off the table, handy.'),
    'delikados_filtro': ('С фильтром. Сделано не спеша, кури так же.', 'With a filter. Made without hurry, smoke them likewise.'),
    'delikados_dorados': ('Золотые. Золота тут нет, зато есть название.', 'Golden ones. No gold in here, but the name is nice.'),
    'farros_clasicos': ('Рисовая бумага. Риса в ней не больше, чем в твоей кладовке.', 'Rice paper. About as much rice in it as in your pantry.'),
    'farros_filtro': ('Маяк с фильтром. Светит в тумане, особенно если затянуться.', 'A lighthouse with a filter. Shines in the fog, more so on a drag.'),
    'farros_mentol': ('Ментоловые. Здешний маяк давно погас, этот ещё горит.', 'Menthol ones. The local lighthouse went dark long ago, this one still glows.'),
    'kleopatra_golden': ('Золотой царь. Пирамиды строили дольше, чем ты куришь пачку.', 'Golden king. The pyramids took longer to build than a pack takes to smoke.'),
    'kleopatra_red': ('Красная царица. В пустыне тоже холодно по ночам, говорят.', 'The red queen. Deserts get cold at night too, they say.'),
    'kleopatra_nile': ('Нил с ментолом. Крокодилов нет, зато есть волки.', 'The Nile with menthol. No crocodiles here, but there are wolves.'),
    'larkk_red': ('Уголь в фильтре. Жаворонков здесь нет, только вороны.', 'Charcoal in the filter. No larks around here, only crows.'),
    'larkk_milds': ('Мягкие. Птичка маленькая, дым соответствующий.', 'Mild ones. A small bird, and smoke to match.'),
    'larkk_black': ('Тройной уголь. Почти печка, только карманная.', 'Triple charcoal. Almost a stove, only pocket-sized.'),
    'eeve_garden': ('Сад в кармане. Единственный, что цветёт этой зимой.', 'A garden in your pocket. The only one in bloom this winter.'),
    'eeve_menthol': ('Мятный сад. Длинные, хватит на долгий разговор с собой.', 'A mint garden. Long ones, enough for a long talk with yourself.'),
    'eeve_rose': ('Розы. Шипов нет, остальное на месте.', 'Roses. No thorns, everything else included.'),
    'kravena_cork': ('С пробковым мундштуком. Кот уверяет, что горлу не вредит.', 'Cork tipped. The cat insists it is kind to the throat.'),
    'kravena_menthol': ('Ментоловый кот. Мышей не ловит, зато освежает.', 'Menthol cat. Catches no mice, but it does refresh.'),
    'kravena_blackcat': ('Чёрный кот без фильтра. К несчастью для лёгких.', 'A black cat with no filter. Bad luck for the lungs.'),
    'troikka_classic': ('Тройка мчится по снегу. Тебе бы такую до Милтона.', 'Three horses flying over the snow. You could use them on the way to Milton.'),
    'troikka_gold': ('Золотая тройка. Бубенцы не прилагаются.', 'Golden three. Sleigh bells not included.'),
    'troikka_zima': ('Зима с ментолом. Как будто зимы тут мало.', 'Winter with menthol. As if there were not enough winter here.'),
    'maksimm_red': ('По максимуму. Меньше тут и не выжить.', 'To the max. Anything less will not keep you alive here.'),
    'maksimm_silver': ('Серебристые. Максимум, но вполсилы.', 'Silver ones. The max, at half strength.'),
    'maksimm_night': ('Ночные и крепкие. Для тех, кто сторожит костёр.', 'Strong night ones. For those keeping watch over the fire.'),
    'egoriy_red': ('Змей бросил первым. Ты пока держишься.', 'The dragon quit first. You are still holding on.'),
    'egoriy_blue': ('Синие. Копьё на пачке, топорик у тебя.', 'Blue ones. A spear on the pack, a hatchet in your hand.'),
    'egoriy_white': ('Белые лёгкие. Подвиг небольшой, но ежедневный.', 'Light white ones. A small feat, but a daily one.'),
    'donskoy_dark': ('Тёмный табак с тихой реки. Здесь тоже тихо, слишком.', 'Dark tobacco from the quiet river. Quiet here too, a bit too quiet.'),
    'donskoy_light': ('Светлый табак. Пахнет степью и летом, которого не будет.', 'Light tobacco. Smells of steppe and of a summer that is not coming.'),
    'donskoy_steppe': ('Степные без фильтра. Ветер тот же, только холоднее.', 'Steppe ones, no filter. Same wind, only colder.'),
}
BLURB.update(BLURB9)
MEME.update(MEME9)
BLURB10 = {
    'canclassics': ('Канадские сигареты с горами на пачке.', 'Canadian cigarettes with mountains on the pack.'),
    'numero7': ('Канадские сигареты с семёркой.', 'Canadian cigarettes with a seven.'),
    'bellmont': ('Канадские сигареты в синей пачке.', 'Canadian cigarettes in a blue pack.'),
    'matinay': ('Мягкие канадские сигареты для спокойного дня.', 'Mild Canadian cigarettes for a quiet afternoon.'),
    'macdonnald': ('Старые канадские сигареты с шотландской клеткой.', 'Old Canadian cigarettes with a Scottish tartan.'),
    'sportsmann': ('Канадские сигареты рыбаков и охотников.', 'Canadian cigarettes of anglers and hunters.'),
    'peterjaxon': ('Канадские сигареты с гербом.', 'Canadian cigarettes with a crest.'),
    'marktenn': ('Канадские сигареты с большой десяткой.', 'Canadian cigarettes with a big ten.'),
    'stolitchnye': ('Советские сигареты с башней и звездой.', 'Soviet cigarettes with a tower and a star.'),
    'astrra': ('Простые советские сигареты с астрой.', 'Plain Soviet cigarettes with an aster.'),
    'dymokk': ('Самые дешёвые советские сигареты.', 'The cheapest Soviet cigarettes.'),
    'novostt': ('Советские сигареты с газетой на пачке.', 'Soviet cigarettes with a newspaper on the pack.'),
    'laikka': ('Советские сигареты в честь собаки в космосе.', 'Soviet cigarettes named after the dog in space.'),
    'drugg': ('Советские сигареты с овчаркой.', 'Soviet cigarettes with a shepherd dog.'),
    'okhotnitchyi': ('Советские охотничьи сигареты с уткой.', "Soviet hunters' cigarettes with a duck."),
    'polyott': ('Советские овальные сигареты с самолётом.', 'Soviet oval cigarettes with an aeroplane.'),
    'severr': ('Советские папиросы с северным солнцем.', 'Soviet papirosy with a northern sun.'),
    'lubitelskiye': ('Советские папиросы для знатоков.', 'Soviet papirosy for those who know.'),
    'vattra': ('Крепкие сигареты с костром на пачке.', 'Strong cigarettes with a campfire on the pack.'),
    'orbitta': ('Советские сигареты с планетой.', 'Soviet cigarettes with a planet.'),
    'arktikka': ('Ментоловые сигареты с айсбергом.', 'Menthol cigarettes with an iceberg.'),
    'ligerros': ('Кубинские сигареты из чёрного табака, с парусником.', 'Cuban cigarettes of black tobacco, with a sailboat.'),
    'btt': ('Болгарские сигареты с тремя буквами.', 'Bulgarian cigarettes with three letters.'),
    'rodoppi': ('Болгарские сигареты с горами.', 'Bulgarian cigarettes with mountains.'),
    'opall': ('Болгарские сигареты с драгоценным камнем.', 'Bulgarian cigarettes with a gemstone.'),
    'shipkka': ('Болгарские сигареты с башней на перевале.', 'Bulgarian cigarettes with a tower on a mountain pass.'),
    'fenixx': ('Болгарские сигареты с фениксом.', 'Bulgarian cigarettes with a phoenix.'),
    'sportt': ('Польские сигареты с факелом.', 'Polish cigarettes with a torch.'),
    'popularrne': ('Самые простые польские сигареты.', 'The plainest Polish cigarettes.'),
    'klubbowe': ('Польские сигареты с трефой.', 'Polish cigarettes with a club suit.'),
    'karmen': ('Польские сигареты с розой.', 'Polish cigarettes with a rose.'),
    'spartta': ('Чехословацкие сигареты со шлемом.', 'Czechoslovak cigarettes with a helmet.'),
    'pettra': ('Чехословацкие сигареты с вензелем.', 'Czechoslovak cigarettes with a monogram.'),
    'startt': ('Чехословацкие сигареты со стрелками.', 'Czechoslovak cigarettes with arrows.'),
    'sopianna': ('Венгерские сигареты с римской колонной.', 'Hungarian cigarettes with a Roman column.'),
    'symfonia': ('Венгерские сигареты с лирой.', 'Hungarian cigarettes with a lyre.'),
    'kossutth': ('Крепкие венгерские сигареты с гербом.', 'Strong Hungarian cigarettes with a crest.'),
    'karro': ('Восточногерманские сигареты в клетку, очень крепкие.', 'East German cigarettes in a chequer, very strong.'),
    'clubb': ('Восточногерманские сигареты «как за границей».', 'East German cigarettes "just like abroad".'),
    'moravva': ('Югославские сигареты с рекой и солнцем.', 'Yugoslav cigarettes with a river and a sun.'),
    'filter58': ('Югославские сигареты с номером на пачке.', 'Yugoslav cigarettes with a number on the pack.'),
}
MEME10 = {
    'canclassics_original': ('Горы как за окном. Только эти не пытаются тебя убить.', 'Mountains like the ones outside. Only these are not trying to kill you.'),
    'canclassics_silver': ('Серебряные. Цвета неба перед метелью.', 'Silver. The colour of the sky before a blizzard.'),
    'canclassics_select': ('Отборные. Отбирать тут особо не из чего, бери.', 'Select. Not much to select from out here, take them.'),
    'numero7_red': ('Счастливая семёрка. Счастье здесь считают в спичках.', 'Lucky seven. Luck out here is counted in matches.'),
    'numero7_blue': ('Синяя семёрка. Семь затяжек до тишины.', 'Blue seven. Seven drags to silence.'),
    'numero7_menthol': ('Семёрка с ментолом. Удача с холодком.', 'Seven with menthol. Luck with a chill.'),
    'bellmont_regular': ('Гладкие, как лёд на озере. Лёд, правда, трещит.', 'Smooth as lake ice. The ice does crack, though.'),
    'bellmont_milds': ('Мягкие. Голубые, как тень на снегу.', 'Milds. Blue like a shadow on the snow.'),
    'bellmont_silver': ('Самые лёгкие. Почти как просто постоять на крыльце.', 'The lightest. Almost like just standing on the porch.'),
    'matinay_special': ('Для тихого дня. Тихих дней тут много, шумные хуже.', 'For a quiet afternoon. Plenty of those here, the loud ones are worse.'),
    'matinay_silver': ('Очень мягкие. Солнце на пачке греет лучше настоящего.', 'Extra mild. The sun on the pack is warmer than the real one.'),
    'matinay_menthol': ('С ментолом. Дневной сеанс, кино не будет.', 'With menthol. A matinee, minus the film.'),
    'macdonnald_plain': ('Без фильтра. Девушка с пачки передаёт привет.', 'No filter. The lass from the pack sends her regards.'),
    'macdonnald_special': ('Особые. Клетка шотландская, мороз канадский.', 'Special. Scottish tartan, Canadian frost.'),
    'macdonnald_menthol': ('С ментолом. Волынка в комплект не входит.', 'With menthol. Bagpipes not included.'),
    'sportsmann_plain': ('Без фильтра. Ушёл на рыбалку, вернусь к весне.', 'No filter. Gone fishing, back by spring.'),
    'sportsmann_filter': ('С фильтром. Пока клюёт, не кури. Не клюёт, кури.', 'With a filter. If they bite, do not smoke. If they do not, smoke.'),
    'sportsmann_lake': ('Озёрные мягкие. Лунку сверли сам.', 'Lake Mild. Drill the ice hole yourself.'),
    'peterjaxon_gold': ('Золотые. Герб есть, замка нет.', 'Gold. There is a crest, but no castle.'),
    'peterjaxon_blue': ('Синие. Всего чуть больше, чем нужно.', 'Blue. A little more of everything than you need.'),
    'peterjaxon_black': ('Чёрные и полные. Как ночь без сияния.', 'Black and full. Like a night without the aurora.'),
    'marktenn_plain': ('Без фильтра. Десять из десяти, кашель в подарок.', 'Plain end. Ten out of ten, the cough comes free.'),
    'marktenn_filter': ('С фильтром. Крестик на пачке не клад отмечает.', 'With a filter. The X on the pack does not mark treasure.'),
    'marktenn_light': ('Лёгкие. Десятка с натяжкой.', 'Light. A ten, at a stretch.'),
    'stolitchnye_classic': ('Столичные. До столицы отсюда дальше, чем до Луны.', 'From the capital. The capital is farther from here than the Moon.'),
    'stolitchnye_redstar': ('Красная звезда. Светит, но не греет.', 'Red star. It shines but gives no warmth.'),
    'stolitchnye_night': ('Ночная столица. Огней там больше, чем у тебя спичек.', 'The capital at night. More lights there than you have matches.'),
    'astrra_plain': ('Без фильтра. Через тернии, но не к звёздам, а к печке.', 'No filter. Through thorns, not to the stars but to the stove.'),
    'astrra_filter': ('С фильтром. Астра цветёт до заморозков. Они уже были.', 'With a filter. Asters bloom until the frost. The frost has been.'),
    'astrra_white': ('Белая астра. Единственный цветок в сугробе.', 'White aster. The only flower in the snowdrift.'),
    'dymokk_plain': ('Дымок. Дёшево, сердито, зато свой.', 'A little smoke. Cheap, harsh, but your own.'),
    'dymokk_strong': ('Крепкие. Дымка много, названию не верь.', 'Strong. Plenty of smoke, never mind the name.'),
    'dymokk_filter': ('С фильтром. Роскошь по меркам дымка.', "With a filter. Luxury by this brand's standards."),
    'novostt_morning': ('Утренний выпуск. Новость одна: снег.', 'Morning edition. One headline: snow.'),
    'novostt_evening': ('Вечерний выпуск. Новость та же, но темнее.', 'Evening edition. Same headline, only darker.'),
    'novostt_special': ('Экстренный выпуск. Кофе ещё остался.', 'Special issue. There is still some coffee left.'),
    'laikka_classic': ('Первая на орбите. Ты первый в этой избушке за много лет.', 'First in orbit. You are the first in this cabin for years.'),
    'laikka_sputnik': ('Спутник. Лёгкие, летают высоко.', 'Sputnik. Light, and they fly high.'),
    'laikka_red': ('Красные без фильтра. Собаки здесь попроще и позлее.', 'Red, no filter. The dogs here are plainer and meaner.'),
    'drugg_red': ('Друг. Единственный, что не убежал.', 'A friend. The only one that did not run off.'),
    'drugg_gold': ('Золотые. Друг познаётся в метели.', 'Gold. A friend in a blizzard is a friend indeed.'),
    'drugg_guard': ('Сторожевые, крепкие. Волков не отгонят, но ты не один.', 'Guard, strong. Will not scare the wolves, but you are not alone.'),
    'okhotnitchyi_plain': ('Охотничьи. Утка улетела, сигарета осталась.', "Hunters' own. The duck flew off, the cigarette stayed."),
    'okhotnitchyi_marsh': ('Болотные. Для долгого сидения в камышах.', 'Marsh. For the long wait in the reeds.'),
    'okhotnitchyi_filter': ('С фильтром. Олень дым чует, имей в виду.', 'With a filter. The deer can smell smoke, mind that.'),
    'polyott_plain': ('Овальные. Взлёт разрешён, посадка как получится.', 'Oval. Cleared for take-off, landing as it comes.'),
    'polyott_night': ('Ночной рейс. Один уже упал где-то рядом.', 'Night flight. One has already come down somewhere near.'),
    'polyott_filter': ('С фильтром. Летают ниже, зато мягче.', 'With a filter. They fly lower, but softer.'),
    'severr_classic': ('Север. Ты и так на нём.', 'North. You are already there.'),
    'severr_polar': ('Полярная ночь. Мундштук длинный, ночь длиннее.', 'Polar night. The tube is long, the night is longer.'),
    'severr_aurora': ('Сияние. Красиво, пока не загудели провода.', 'Aurora. Pretty until the wires start to hum.'),
    'lubitelskiye_classic': ('Любительские. Профессионалы тут не выживают.', 'For amateurs. Professionals do not last out here.'),
    'lubitelskiye_strong': ('Крепкие. Любителям не рекомендуются.', 'Strong. Not advised for amateurs.'),
    'lubitelskiye_fine': ('Тонкой резки. Для тех, кто понимает.', 'Fine cut. For those who know.'),
    'vattra_plain': ('Костёр. Береги огонь, второй пачки может не быть.', 'Campfire. Keep the fire, there may not be a second pack.'),
    'vattra_filter': ('С фильтром. Греет почти как настоящий.', 'With a filter. Warms almost like the real thing.'),
    'vattra_ember': ('Угольки. Раздуй, и будет костёр. Шутка.', 'Embers. Blow on them and you get a campfire. Just kidding.'),
    'orbitta_classic': ('Ещё один виток. И ещё один день.', 'One more turn around. And one more day.'),
    'orbitta_light': ('Лёгкие. Почти невесомость.', 'Light. Close to weightless.'),
    'orbitta_luna': ('Луна с ментолом. Холодная и далеко.', 'The Moon with menthol. Cold and far away.'),
    'arktikka_menthol': ('Ментол. Холоднее, чем кажется.', 'Menthol. Colder than it looks.'),
    'arktikka_ice': ('Двойной лёд. Как будто одного тут мало.', 'Double ice. As if one were not enough here.'),
    'arktikka_polar': ('Полярные крепкие. Белых медведей нет, чёрные есть.', 'Polar strong. No polar bears here, black ones though.'),
    'ligerros_negro': ('Чёрный табак с острова, где не бывает зимы.', 'Black tobacco from an island that has no winter.'),
    'ligerros_dulce': ('Сладкая бумага. Облизни губы, это не мороз.', 'Sweet paper. Lick your lips, that is not the frost.'),
    'ligerros_filtro': ('С фильтром. Парусник лёгкий, табак нет.', 'With a filter. The boat is light, the tobacco is not.'),
    'btt_red': ('Три буквы. Приличные.', 'Three letters. Decent ones.'),
    'btt_blue': ('Лёгкие. Балканы далеко, пачка рядом.', 'Lights. The Balkans are far, the pack is near.'),
    'btt_gold': ('Золотые в чёрном. Для особого случая: пережил неделю.', 'Gold on black. For a special occasion: you lasted a week.'),
    'rodoppi_classic': ('Горы пониже здешних и потеплее.', 'Mountains lower than ours, and warmer.'),
    'rodoppi_plain': ('Без фильтра. Горный лист как он есть.', 'No filter. Mountain leaf as it is.'),
    'rodoppi_green': ('Горная мята. Дышится как на вершине.', 'Mountain mint. Breathes like a summit.'),
    'opall_blue': ('Синий опал. Камешек дешёвый, зато свой.', 'Blue opal. A cheap little stone, but yours.'),
    'opall_white': ('Белый опал. На снегу не терять.', 'White opal. Do not drop it in the snow.'),
    'opall_fire': ('Огненный опал. Спички всё равно нужны.', 'Fire opal. You still need matches.'),
    'shipkka_plain': ('Без фильтра. Держи перевал.', 'No filter. Hold the pass.'),
    'shipkka_filter': ('С фильтром. Башня стоит, и ты постой.', 'With a filter. The tower stands, so can you.'),
    'shipkka_pass': ('Горный перевал. На верёвке и с сигаретой в зубах не лезь.', 'Mountain pass. Do not climb a rope with one in your teeth.'),
    'fenixx_red': ('Восстал из пепла. Пепел стряхивай.', 'Risen from the ashes. Do tap the ash off.'),
    'fenixx_gold': ('Золотой феникс. Горит и не сгорает, в отличие от пачки.', 'Golden phoenix. Burns and is not consumed, unlike the pack.'),
    'fenixx_ash': ('Пепельные. Цвета вчерашнего костра.', "Ash grey. The colour of yesterday's campfire."),
    'sportt_plain': ('Без фильтра. Быстрее, выше, дымнее.', 'No filter. Faster, higher, smokier.'),
    'sportt_filter': ('С фильтром. Спорт тут один: добежать до двери.', 'With a filter. One sport out here: reach the door.'),
    'sportt_blue': ('Лёгкие. Для бега от волков не помогут.', 'Light. No help when running from wolves.'),
    'popularrne_plain': ('Популярные. Были у всех, остались у тебя.', 'Popular. Everybody had them, you still do.'),
    'popularrne_filter': ('С фильтром. Популярность растёт.', 'With a filter. Popularity on the rise.'),
    'popularrne_strong': ('Крепкие. Популярны у тех, кто выжил.', 'Strong. Popular with the survivors.'),
    'klubbowe_red': ('Клубные. Клуб закрыт, членов один.', 'Club cigarettes. The club is shut, one member left.'),
    'klubbowe_black': ('Крепкие. Трефы к дальней дороге.', 'Strong. Clubs mean a long road ahead.'),
    'klubbowe_white': ('Лёгкие. Вход только для своих.', 'Light. Members only.'),
    'karmen_red': ('Любовь свободна. Табак тоже ничей.', 'Love is a rebellious bird. So is free tobacco.'),
    'karmen_rose': ('Розовые. Тореадор, смелее.', 'Rose. Toreador, be bold.'),
    'karmen_black': ('Хабанера. Танцуй, пока тепло.', 'Habanera. Dance while it is warm.'),
    'spartta_red': ('Это курево. Со щитом или на щите.', 'This is smoking. With your shield or on it.'),
    'spartta_white': ('Белые. Спартанские условия прилагаются.', 'White. Spartan conditions included.'),
    'spartta_blue': ('Синие мягкие. Триста затяжек не обещаем.', 'Mild blue. No promise of three hundred drags.'),
    'pettra_classic': ('Пражский вечер. Пива не будет.', 'A Prague evening. There will be no beer.'),
    'pettra_blue': ('Голубые. Вензель красивый, остальное скромнее.', 'Blue. A fine monogram, the rest is humbler.'),
    'pettra_gold': ('Золотые сладкие. Для гостей, которых не будет.', 'Sweet gold. For the guests who are not coming.'),
    'startt_plain': ('На старт. Внимание. Кашель.', 'On your marks. Get set. Cough.'),
    'startt_filter': ('С фильтром. Финиш там же, где старт.', 'With a filter. The finish is where the start was.'),
    'startt_red': ('Красная линия. За неё не заступать.', 'Red line. Do not step over it.'),
    'sopianna_classic': ('Римская дорога. Сюда она не дошла.', 'A Roman road. It never got this far.'),
    'sopianna_plain': ('Без фильтра. Колонна стоит две тысячи лет, постой и ты.', 'No filter. The column has stood two thousand years, stand a while too.'),
    'sopianna_light': ('Лёгкие. Империи падают, пачки кончаются.', 'Light. Empires fall, packs run out.'),
    'symfonia_red': ('Аллегро. Быстро и с огоньком.', 'Allegro. Quick and with fire.'),
    'symfonia_blue': ('Адажио. Медленно, как зимний вечер.', 'Adagio. Slow as a winter evening.'),
    'symfonia_gold': ('Финал. С кофе, если остался.', 'Finale. With coffee, if any is left.'),
    'kossutth_plain': ('Без фильтра. Крепкие, как речь с трибуны.', 'No filter. Strong as a speech from the stand.'),
    'kossutth_filter': ('С фильтром. Речь та же, но покороче.', 'With a filter. Same speech, only shorter.'),
    'kossutth_red': ('Красные. Аплодисментов не будет.', 'Red. There will be no applause.'),
    'karro_plain': ('Без фильтра. Крепче только печная труба.', 'No filter. Only a stovepipe is stronger.'),
    'karro_red': ('Красные крепкие. Клетка на пачке, слёзы на глазах.', 'Strong red. A chequer on the pack, tears in the eyes.'),
    'karro_filter': ('С фильтром. Уступка слабым.', 'With a filter. A concession to the weak.'),
    'clubb_classic': ('Как за границей. Ты, собственно, за ней.', 'Just like abroad. Which is where you are.'),
    'clubb_blue': ('Синие. Вкус мировой, магазин за углом. Был.', 'Blue. World taste, corner shop. Once.'),
    'clubb_menthol': ('С ментолом. Клубная карта не нужна.', 'With menthol. No club card needed.'),
    'moravva_classic': ('Вниз по тихой реке. Здешние реки молчат подо льдом.', 'Down the slow river. The rivers here are silent under the ice.'),
    'moravva_plain': ('Без фильтра. Река медленная, дым быстрый.', 'No filter. A slow river, a quick smoke.'),
    'moravva_gold': ('Золотые. Солнце над водой, которой не видно.', 'Gold. A sun over water you cannot see.'),
    'filter58_classic': ('Пятьдесят восемь. На один лучше прошлогодних.', "Fifty-eight. One better than last year's."),
    'filter58_blue': ('Синие лёгкие. Номер тот же.', 'Light blue. Same number.'),
    'filter58_red': ('Красные крепкие. Считай затяжки, не годы.', 'Strong red. Count the drags, not the years.'),
}
BLURB.update(BLURB10)
MEME.update(MEME10)
import texts11
BLURB.update(texts11.BLURB11)
MEME.update({catalog.tex_key(k, v): texts11.MEME11[k][i] for k in catalog.BATCH11 for i, v in enumerate(catalog.BRANDS[k][2])})
import texts12
BLURB.update(texts12.BLURB_FIX)
MEME.update({catalog.tex_key(k, catalog.BRANDS[k][2][i]): t for (k, i), t in texts12.MEME_FIX.items()})
import texts13, texts13b, texts13c, texts13d
for _d in (texts13.MEME13, texts13b.MEME13B, texts13c.MEME13C, texts13d.MEME13D):
    MEME.update({catalog.tex_key(k, v): _d[k][i] for k in _d for i, v in enumerate(catalog.BRANDS[k][2])})
import texts13e
MEME.update({catalog.tex_key(k, catalog.BRANDS[k][2][i]): t for (k, i), t in texts13e.MEME13E.items()})
PAPER = {(226, 200, 128): ('маисовая', 'maize'), (240, 238, 232): ('белая', 'white'), (128, 82, 50): ('коричневая', 'brown'), (78, 48, 30): ('тёмно-коричневая', 'dark brown'),
         (26, 24, 24): ('чёрная', 'black'), (196, 196, 196): ('серая', 'grey'),
         (236, 226, 200): ('кремовая', 'cream'), (238, 232, 214): ('кремовая', 'cream'), (214, 190, 150): ('бежевая', 'beige'),
         (214, 190, 130): ('кукурузный лист', 'corn husk'), (124, 112, 62): ('лист тенду', 'tendu leaf'), (112, 120, 66): ('лист тенду', 'tendu leaf'),
         (132, 110, 58): ('лист тенду', 'tendu leaf')}
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
    lr, le = f"бумага {pr}, фильтр {fr}", f"{pe} paper, {fe} filter"
    if bkey in catalog.BATCH7 or bkey in catalog.BATCH8 or bkey in catalog.BATCH9 or bkey in catalog.BATCH10 or bkey in catalog.BATCH11:
        # batch 7: said properly when there is no filter, a long mouthpiece, or a leaf instead of paper
        wr, we = (f"обёртка: {pr}", f"wrapped in a {pe}") if 'лист' in pr else (f"бумага {pr}", f"{pe} paper")
        tr, te = (("без фильтра", "no filter") if v['filter'] == 'none' else
                  (f"длинный {fr} мундштук", f"long {fe} mouthpiece") if v['fmt'] == 'cigaronni' else
                  ("бумажная гильза", "paper tube mouthpiece") if v['filter'] == 'papirosa' else (f"фильтр {fr}", f"{fe} filter"))
        lr, le = f"{wr}, {tr}", f"{we}, {te}"
    ru = (f"{mr} {v['name_ru']}: {sr}, {lr}. {TAIL[0]} "
          f"Сигарета: усталость −{fmt_num(fat)}%, голод на 10% медленнее {fmt_num(hung)} мин, здоровье −{fmt_num(hp)}%. {br}").strip()
    en = (f"{me} {v['name_en']}: {se}, {le}. {TAIL[1]} "
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

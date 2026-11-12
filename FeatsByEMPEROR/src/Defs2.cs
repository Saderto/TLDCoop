using System.Collections.Generic;

namespace FeatsByEMPEROR
{
    /// <summary>Second batch: 37 feats (types 1012..1048, badges ...e0d..e31).</summary>
    internal static class Defs2
    {
        public const string Cook = "Cook", HotMeal = "HotMeal", Coffee = "CoffeeLover", Tea = "TeaDrinker", Hoarder = "Hoarder",
            Sweet = "SweetTooth", Tanner = "Tanner", Smith = "Blacksmith", Sharpener = "Sharpener", Burglar = "Burglar",
            Torch = "Torchbearer", Lamp = "Lamplighter", Hearth = "Hearthkeeper", LightSleep = "LightSleeper", IceWalker = "IceWalker",
            IceCutter = "IceCutter", Together = "Together", Phoenix = "Phoenix", Prometheus = "Prometheus", Minimalist = "Minimalist",
            Forest = "ForestDweller", Vegetarian = "Vegetarian", Fortune = "Fortune", Canner = "Canner", Stew = "StewMaster",
            EarlyBird = "EarlyBird", Ritual = "MorningRitual", Sun = "SunWorshipper", Home = "Homeowner", Nomad = "Nomad",
            Traveler = "Traveler", Perfect = "Perfectionist", Gunsmith = "Gunsmith", Lucky = "LuckyHand", Train = "Machinist",
            Merchant = "Merchant", Soda = "SodaLover";

        static int _n;
        static Def D(string id, float target, string nameRu, string nameEn, string taskRu, string taskEn, string bonusRu, string bonusEn,
            string unitRu = "", string unitEn = "", int dec = 0)
        {
            int i = _n++;
            return new Def
            {
                Id = id, Type = 1012 + i, Target = target, Guid = "5a0c7e21d4b94f3c8e6a1b2f9d3c4e" + (0x0d + i).ToString("x2"),
                NameRu = nameRu, NameEn = nameEn, TaskRu = taskRu, TaskEn = taskEn, BonusRu = bonusRu, BonusEn = bonusEn,
                UnitRu = unitRu, UnitEn = unitEn, Decimals = dec,
            };
        }

        const string Days = "дн.", DaysEn = "days";

        public static readonly List<Def> New = new()
        {
            D(Cook, 300, "Повар", "Cook", "Приготовьте 300 блюд.", "Cook 300 dishes.",
                "Готовка на 25% быстрее.", "Cooking is 25% faster."),
            D(HotMeal, 200, "Горячий обед", "Hot Meal", "Съешьте или выпейте 200 горячих блюд и напитков.", "Eat or drink 200 hot dishes and drinks.",
                "Тепло от горячей еды и напитков держится вдвое дольше.", "Warmth from hot food and drinks lasts twice as long."),
            D(Coffee, 300, "Кофеман", "Coffee Lover", "Выпейте 300 кружек кофе.", "Drink 300 cups of coffee.",
                "Кофе снимает усталость на 50% сильнее.", "Coffee relieves fatigue 50% more."),
            D(Tea, 300, "Чаёвник", "Tea Drinker", "Выпейте 300 кружек травяного чая.", "Drink 300 cups of herbal tea.",
                "Эффекты чая длятся вдвое дольше.", "Tea effects last twice as long."),
            D(Hoarder, 500, "Запасливый", "Hoarder", "Съешьте 500 кг еды.", "Eat 500 kg of food.",
                "Еда в инвентаре портится на 20% медленнее.", "Food in your inventory spoils 20% slower.", "кг", "kg", 1),
            D(Sweet, 200, "Сладкоежка", "Sweet Tooth", "Съешьте 200 шоколадок, батончиков и других сладостей.", "Eat 200 chocolate bars, snack bars and other sweets.",
                "Сладкое даёт на 10% больше калорий.", "Sweets give 10% more calories."),
            D(Tanner, 50, "Кожевник", "Tanner", "Выделайте 50 шкур.", "Cure 50 hides.",
                "Шкуры и кишки выделываются на 30% быстрее.", "Hides and guts cure 30% faster."),
            D(Smith, 50, "Кузнец", "Blacksmith", "Выкуйте 50 вещей на наковальне.", "Forge 50 items at the anvil.",
                "Ковка на 30% быстрее и требует на 20% меньше металлолома.", "Forging is 30% faster and needs 20% less scrap metal."),
            D(Sharpener, 200, "Точильщик", "Sharpener", "Заточите инструменты 200 раз.", "Sharpen tools 200 times.",
                "Заточка возвращает на 50% больше состояния.", "Sharpening restores 50% more condition."),
            D(Burglar, 200, "Взломщик", "Burglar", "Вскройте ломом 200 замков и ящиков.", "Pry open 200 locks and crates.",
                "Вскрытие на 40% быстрее, лом изнашивается вдвое медленнее.", "Prying is 40% faster and the prybar wears out half as fast."),
            D(Torch, 50, "Факельщик", "Torchbearer", "Пронесите горящий факел 50 км.", "Carry a burning torch for 50 km.",
                "Факелы горят на 30% дольше.", "Torches burn 30% longer.", "км", "km", 1),
            D(Lamp, 20, "Фонарщик", "Lamplighter", "Сожгите в лампе 20 л керосина.", "Burn 20 L of kerosene in lamps.",
                "Лампа тратит керосин на 25% медленнее.", "Lamps burn kerosene 25% slower.", "л", "L", 2),
            D(Hearth, 500, "Хранитель очага", "Hearthkeeper", "Держите свои костры горящими в сумме 500 часов.", "Keep your own fires burning for 500 hours in total.",
                "Костры горят на 15% дольше на том же топливе.", "Fires burn 15% longer on the same fuel.", "ч", "h", 1),
            D(LightSleep, 300, "Чуткий сон", "Light Sleeper", "Проспите 300 часов в спальнике под открытым небом.", "Sleep 300 hours in a bedroll under the open sky.",
                "Приближение хищника будит вас.", "An approaching predator wakes you up.", "ч", "h", 1),
            D(IceWalker, 20, "Ледоход", "Ice Walker", "Пройдите 20 км по льду.", "Walk 20 km on ice.",
                "Тонкий лёд выдерживает вас вдвое дольше.", "Thin ice holds you twice as long.", "км", "km", 2),
            D(IceCutter, 100, "Ледорез", "Ice Cutter", "Прорубите 100 лунок.", "Cut 100 fishing holes.",
                "Лунка прорубается вдвое быстрее.", "Fishing holes are cut twice as fast."),
            D(Together, 100, "Вдвоём легче", "Better Together", "Проведите 100 часов рядом с другим игроком (до 20 м). В одиночной игре его не открыть.",
                "Spend 100 hours near another player (within 20 m). Can't be unlocked in single player.",
                "Рядом с другом усталость растёт на 15% медленнее.", "Near a friend, fatigue grows 15% slower.", "ч", "h", 1),
            D(Phoenix, 10, "Феникс", "Phoenix", "Погибните 10 раз, прожив перед этим в той игре не меньше 20 дней.", "Die 10 times after surviving at least 20 days in that game.",
                "Один раз за игру смерть отменяется, и вы приходите в себя с 10% состояния.", "Once per game, death is cancelled and you come to with 10% condition."),
            D(Prometheus, 30, "Прометей", "Prometheus", "Проживите 30 дней подряд, ни разу не зажигая огонь спичкой или зажигалкой.",
                "Survive 30 days in a row without lighting a fire with a match or a lighter.",
                "Увеличительное стекло разжигает огонь и в облачную погоду, а не только при ярком солнце.",
                "The magnifying lens starts fires in cloudy weather too, not only in bright sun.", Days, DaysEn, 1),
            D(Minimalist, 10, "Минималист", "Minimalist", "Проживите 10 дней подряд, ни разу не неся больше 10 кг груза (надетая одежда не считается).",
                "Survive 10 days in a row without ever carrying more than 10 kg (worn clothes don't count).",
                "Пока груз не больше 10 кг, вы ходите и бегаете на 10% быстрее.", "While you carry 10 kg or less, you walk and run 10% faster.", Days, DaysEn, 1),
            D(Forest, 30, "Лесной житель", "Forest Dweller", "Проживите 30 дней подряд, не заходя в здания и пещеры.",
                "Survive 30 days in a row without entering buildings or caves.",
                "На улице на 3 °C теплее, но в домах на 4 °C холоднее.", "Outdoors you feel 3 °C warmer, but indoors 4 °C colder.", Days, DaysEn, 1),
            D(Vegetarian, 20, "Вегетарианец", "Vegetarian", "Проживите 20 дней подряд без мяса и рыбы.", "Survive 20 days in a row without eating meat or fish.",
                "Растительная еда и консервы дают на 25% больше калорий.", "Plant food and canned food give 25% more calories.", Days, DaysEn, 1),
            D(Fortune, 365, "Фортуна", "Fortune", "Встретьте 365 рассветов (во всех играх вместе).", "See 365 sunrises (all games together).",
                "Каждое утро случайный бонус на день: на 2 °C теплее, или усталость на 10% медленнее, или еда на 10% калорийнее, или ходьба на 5% быстрее.",
                "Every morning a random bonus for the day: 2 °C warmer, or fatigue 10% slower, or food 10% more calories, or walking 5% faster."),
            D(Canner, 200, "Консервщик", "Canner", "Откройте 200 банок.", "Open 200 cans.",
                "Банки открываются любым способом без потери еды и без износа инструмента.", "Cans open any way you like with no food lost and no tool wear."),
            D(Stew, 100, "Мастер рагу", "Stew Master", "Сварите 100 рагу по рецептам.", "Cook 100 stews from recipes.",
                "Рагу даёт на 25% больше калорий, а его согревающий эффект длится вдвое дольше.", "Stews give 25% more calories and their warming effect lasts twice as long."),
            D(EarlyBird, 100, "Ранняя пташка", "Early Bird", "Проснитесь до 6 утра 100 раз.", "Wake up before 6 AM 100 times.",
                "С 5 до 9 утра все действия выполняются на 10% быстрее.", "From 5 to 9 AM all actions are 10% faster."),
            D(Ritual, 100, "Утренний ритуал", "Morning Ritual", "Выпейте кофе или чай в первый час после сна 100 раз.", "Drink coffee or tea within an hour of waking up 100 times.",
                "После утренней кружки 30 минут усталость не растёт.", "After the morning cup, fatigue doesn't grow for 30 minutes."),
            D(Sun, 300, "Солнцепоклонник", "Sun Worshipper", "Проведите 300 часов на улице в ясную погоду.", "Spend 300 hours outdoors in clear weather.",
                "В ясный день на солнце на 3 °C теплее.", "On a clear day you feel 3 °C warmer in the sun.", "ч", "h", 1),
            D(Home, 100, "Домовладелец", "Homeowner", "Проведите 100 ночей в одном и том же доме.", "Spend 100 nights in the same house.",
                "Дом, где вы чаще всего спите, становится вашим: сон там восстанавливает на 20% больше, а еда портится вдвое медленнее.",
                "The house you sleep in most becomes yours: sleep there restores 20% more and food spoils half as fast."),
            D(Nomad, 50, "Кочевник", "Nomad", "Переночуйте в 50 разных зданиях и пещерах.", "Spend a night in 50 different buildings and caves.",
                "Спальник весит вдвое меньше и изнашивается вдвое медленнее.", "Bedrolls weigh half as much and wear out half as fast."),
            D(Traveler, 17, "Путешественник", "Traveler", "Побывайте в 17 регионах.", "Visit 17 regions.",
                "После перехода в другой регион час бодрости: усталость не растёт.", "After moving to another region, an hour of vigor: fatigue doesn't grow."),
            D(Perfect, 100, "Перфекционист", "Perfectionist", "Почините вещи до 100% 100 раз.", "Repair items to 100% 100 times.",
                "Вещь, починенная до 100%, первые сутки не изнашивается.", "An item repaired to 100% doesn't wear out for the first day."),
            D(Gunsmith, 500, "Оружейник", "Gunsmith", "Изготовьте 500 патронов.", "Craft 500 cartridges.",
                "При изготовлении патронов шанс 25% получить лишний.", "When crafting ammo, 25% chance to get an extra cartridge."),
            D(Lucky, 20, "Счастливая рука", "Lucky Hand", "Найдите 20 редких вещей (револьвер, винтовка, ракетница и т. п.).", "Find 20 rare items (revolver, rifle, flare gun and so on).",
                "При первом обыске контейнера шанс 5% найти дополнительную вещь.", "On the first search of a container, 5% chance to find an extra item."),
            D(Train, 200, "Машинист", "Engineer", "Проедьте на поезде 200 км.", "Travel 200 km by train.",
                "Поезд разгоняется быстрее и тратит меньше топлива (нужно дополнение «Железная дорога»).", "The train speeds up faster and uses less fuel (needs the Iron Road content).", "км", "km", 1),
            D(Merchant, 50, "Купец", "Merchant", "Совершите 50 обменов с Торговцем.", "Complete 50 trades with the Trader.",
                "Доверие Торговца растёт вдвое быстрее (нужно дополнение «Байки с Дальней территории»).", "The Trader's trust grows twice as fast (needs Tales from the Far Territory)."),
            D(Soda, 200, "Газировщик", "Soda Lover", "Выпейте 200 банок газировки.", "Drink 200 cans of soda.",
                "Газировка утоляет жажду вдвое сильнее и даёт на 50% больше калорий.", "Soda quenches thirst twice as much and gives 50% more calories."),
        };
    }
}

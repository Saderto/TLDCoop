using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Чем игрок сейчас занят (разделывает тушу, разводит костёр, спит…). Уходит вместе с позицией,
    // у остальных показывается под ником, чтобы было видно, почему человек стоит на месте.
    public static class Activity
    {
        private static readonly string[] Names =
        {
            "",                         // 0 ничего
            "спит",                     // 1
            "ждёт, пока идёт время",    // 2
            "разделывает тушу",         // 3
            "разбирает",                // 4
            "мастерит",                 // 5
            "чинит",                    // 6
            "разводит костёр",          // 7
            "у костра",                 // 8
            "готовит",                  // 9
            "рыбачит",                  // 10
            "собирает",                 // 11
            "обыскивает",               // 12
            "строит укрытие",           // 13
            "открывает банку",          // 14
            "лечится",                  // 15
            "осматривает вещь",         // 16
            "в рюкзаке",                // 17
            "смотрит карту",            // 18
            "в журнале",                // 19
            "выбирает, сколько спать",  // 20
            "занят",                    // 21
            "переодевается",            // 22
            "лезет по тросу",           // 23
            "ранен — подними его",      // 24 (Game/Death.cs)
            "ест",                      // 25
            "пьёт",                     // 26
            "лечит друга",              // 27 (Game/Death.cs)
            "заправляет поезд",         // 28 (Iron Road)
            "смотрит на поезд",         // 29
            "у ящика обмена",           // 30
            "ведёт поезд",              // 31 (Game/TrainSync.cs)
            "едет в поезде",            // 32
            "схватка со зверем!",       // 33
            "приходит в себя",          // 34
            "провалился под лёд",       // 35 (у остальных фигура по грудь в воде на месте полыньи)
            "в машине",                 // 36 (Game/Cars.cs: фигура сидит на его месте)
            "встаёт",                   // 37 (после схватки: камера игры поднимается с земли)
            "погиб",                    // 38 (Game/Death.cs: экран смерти; фигура лежит, поднять нельзя)
            "делает укол",              // 39 (шприц первой помощи или военный стимулятор: фигура колет себя в бедро)
        };

        // 0.19.34: те же подписи по-английски (язык игры решает).
        private static readonly string[] NamesEn =
        {
            "", "sleeping", "passing time", "harvesting a carcass", "breaking down", "crafting", "repairing",
            "starting a fire", "at the fire", "cooking", "fishing", "harvesting", "searching", "building a shelter",
            "opening a can", "treating", "examining", "in the backpack", "looking at the map", "in the journal",
            "choosing how long to sleep", "busy", "changing clothes", "climbing a rope", "wounded — help them up",
            "eating", "drinking", "treating a friend", "fuelling the train", "looking at the train", "at the exchange box",
            "driving the train", "riding the train", "fighting an animal!", "coming round", "fell through the ice",
            "in the car", "getting up", "dead", "injecting",
        };

        private static int _lastCamMode = -1;

        public static string Name(byte code) => code < Names.Length ? (UI.L.T(Names[code], code < NamesEn.Length ? NamesEn[code] : Names[code])) : "";

        public const byte Downed = 24, Eating = 25, Drinking = 26, Treating = 27, FellThroughIce = 35, InCar = 36, GettingUp = 37, Dead = 38, Injecting = 39;

        // 0.19.34: укол шприцем. Пока шприц в руках (игра сама проигрывает укол в бедро) и ещё немного после того,
        // как игра применила его (EmergencyStim.ApplyEmergencyStim), у остальных фигура колет себя в бедро.
        internal static float InjectUntil;
        internal static bool IsStim(string prefab) => prefab != null && (prefab.Contains("EmergencyStim") || prefab.Contains("MilitaryStim"));

        // Провалился под лёд: игра гасит экран и потом ставит игрока на безопасное место. Остальным ~9 с
        // показываем его там, где он провалился (IcePos), а не там, куда его вытащила игра.
        private const float IceSeconds = 9f;
        private static float _iceUntil;
        private static bool _fellPrev;
        private static Vector3 _lastOnIce;
        private static float _lastOnIceAt = -100f;
        public static Vector3 IcePos { get; private set; }
        public static bool InIce => Time.unscaledTime < _iceUntil;

        // Каждый кадр (дёшево): ловим момент провала, пока игра не переставила игрока.
        public static void TickIce()
        {
            try
            {
                if (!GameApi.InGame()) { _fellPrev = false; return; }
                var icm = GameManager.GetIceCrackingManager();
                if (icm == null) return;
                if (GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _) && (icm.IsInsideFallTrigger() || icm.IsInsideTrigger()))
                {
                    _lastOnIce = new Vector3(x, y, z);
                    _lastOnIceAt = Time.unscaledTime;
                }
                bool fell = icm.FellInRecently();
                if (fell && !_fellPrev)
                {
                    IcePos = Time.unscaledTime - _lastOnIceAt < 5f ? _lastOnIce : new Vector3(x, y, z);
                    _iceUntil = Time.unscaledTime + IceSeconds;
                    _next = 0f;
                }
                _fellPrev = fell;
            }
            catch { }
        }
        public static byte Override;   // занятие, которое задаёт сам мод (ранен, лечит друга)

        // Ест или пьёт: игра начинает это в UseFoodInventoryItem / DrinkFromWaterSupply и заканчивает
        // в OnEatingComplete / OnDrinkWaterComplete. Окна у этого нет, поэтому ловим сами вызовы.
        internal static byte Consume;
        internal static float ConsumeSince;
        internal static string ConsumeItem = "";   // что ест или пьёт — у остальных это в руке фигуры

        private static byte _cached;
        private static float _next;

        // Своё занятие: проверяем открытые окна игры два раза в секунду.
        public static byte Current()
        {
            if (Override != 0) return Override;
            float now = Time.unscaledTime;
            if (now < _next) return _cached;
            _next = now + 0.5f;
            try { _cached = Detect(); } catch { _cached = 0; }
            return _cached;
        }

        private static byte Detect()
        {
            if (!GameApi.InGame()) return 0;
            if (InIce) return FellThroughIce;
            var rest = GameManager.GetRestComponent();
            if (rest != null && rest.IsSleeping()) return 1;
            var pt = GameManager.GetPassTime();
            if (pt != null && pt.IsPassingTime()) return 2;
            var pm = GameManager.GetPlayerManagerComponent();
            if (pm != null && pm.PlayerIsClimbing()) return 23;
            var st = GameManager.GetPlayerStruggleComponent();
            if (st != null)
            {
                if (st.InStruggle()) return 33;
                // После схватки камера игры лежит (GetupDelay = 3), потом поднимается (ReturnToStart = 4) —
                // это и есть «встаёт»: у остальных фигура в это время садится и поднимается с земли.
                int camMode = (int)st.m_StruggleCameraMode;
                if (st.m_Active && camMode != _lastCamMode)
                {
                    _lastCamMode = camMode;
                    MelonLoader.MelonLogger.Msg($"[схватка] камера после схватки: режим {camMode}{(st.m_KnockedOut ? ", без сознания" : "")}");
                }
                if (!st.m_Active) _lastCamMode = -1;
                if (st.m_Active && camMode == 4) return GettingUp;
                // WaitingToRecover() у игры остаётся true и после того, как встал (режим камеры сбрасывается только
                // в начале следующей схватки), поэтому смотрим ещё и на m_Active — иначе фигура лежит до новой схватки.
                if (st.m_Active && (st.WaitingToRecover() || st.m_KnockedOut)) return 34;
            }
            bool inTrain = false;
            try { inTrain = Il2CppTLD.SteamTrain.SteamTrain.IsInsideSteamTrain; } catch { }
            if (Consume != 0)
            {
                if (Time.unscaledTime - ConsumeSince < 90f) return Consume;
                Consume = 0;
            }
            // Блюда и консервы игра иногда начинает есть мимо UseFoodInventoryItem (из котелка, после открывания банки):
            // тогда узнаём по полоске действия и по тому, что игра запомнила съедаемое.
            if (pm != null && On<Panel_GenericProgressBar>())
            {
                GearItem food = null;
                try { food = pm.m_FoodItemEaten; } catch { }
                if (food != null)
                {
                    bool drink = food.m_FoodItem != null && food.m_FoodItem.m_IsDrink;
                    return drink ? Drinking : Eating;
                }
            }
            if (Time.unscaledTime < InjectUntil) return Injecting;
            try
            {
                var inHands = pm != null ? pm.m_ItemInHands : null;
                if (inHands != null && IsStim(ItemSync.PrefabName(inHands))) return Injecting;
            }
            catch { }
            if (On<Panel_BodyHarvest>()) return 3;
            if (On<Panel_BreakDown>()) return 4;
            if (On<Panel_Crafting>()) return 5;
            if (On<Panel_Repair>()) return 6;
            if (On<Panel_FireStart>()) return 7;
            if (On<Panel_FeedFire>()) return 8;
            if (On<Panel_Cooking>() || On<Panel_CookWater>()) return 9;
            if (On<Panel_IceFishing>() || On<Panel_IceFishingHoleClear>()) return 10;
            if (On<Panel_Harvest>()) return 11;
            if (On<Panel_IslandTransitExchangeBox>()) return 30;
            if (On<Panel_Container>()) return 12;
            if (On<Panel_SnowShelterBuild>()) return 13;
            if (On<Panel_CanOpening>()) return 14;
            if (On<Panel_FirstAid>() || On<Panel_Affliction>()) return 15;
            if (On<Panel_Inventory_Examine>()) return 16;
            if (On<Panel_Clothing>()) return 22;
            if (On<Panel_Inventory>()) return 17;
            if (On<Panel_Map>()) return 18;
            if (On<Panel_Log>()) return 19;
            if (On<Panel_Rest>()) return 20;
            if (On<Panel_TransferTrainTowerLiquid>()) return 28;
            if (On<Panel_TrainStatus>()) return 29;
            if (On<Panel_GenericProgressBar>()) return 21;
            if (inTrain) return TrainSync.IsOwner ? (byte)31 : (byte)32;
            if (Cars.LocalInside()) return InCar;
            return 0;
        }

        private static bool On<T>() where T : Panel_Base
        {
            try { return InterfaceManager.TryGetPanel<T>(out var p) && p.IsEnabled(); }
            catch { return false; }
        }
    }

    // 0.19.34: укол применён — ещё 1,5 с показываем его остальным (на случай, если шприц в руках не держался).
    [HarmonyPatch(typeof(EmergencyStim), nameof(EmergencyStim.ApplyEmergencyStim))]
    internal static class Patch_StimApplied
    {
        private static void Postfix()
        {
            Activity.InjectUntil = Time.unscaledTime + 1.5f;
            MelonLoader.MelonLogger.Msg("[укол] шприц применён — остальные видят укол");
        }
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.UseFoodInventoryItem))]
    internal static class Patch_EatStart
    {
        private static void Postfix(GearItem gi, bool __result)
        {
            if (!__result) return;
            Activity.Consume = gi != null && gi.m_FoodItem != null && gi.m_FoodItem.m_IsDrink ? Activity.Drinking : Activity.Eating;
            Activity.ConsumeItem = gi != null ? ItemSync.PrefabName(gi) : "";
            Activity.ConsumeSince = Time.unscaledTime;
        }
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.DrinkFromWaterSupply))]
    internal static class Patch_DrinkStart
    {
        private static void Postfix(WaterSupply ws, bool __result)
        {
            if (!__result) return;
            Activity.Consume = Activity.Drinking;
            Activity.ConsumeItem = "";
            try { var gi = ws?.GetComponent<GearItem>(); if (gi != null) Activity.ConsumeItem = ItemSync.PrefabName(gi); } catch { }
            Activity.ConsumeSince = Time.unscaledTime;
        }
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.OnEatingComplete))]
    internal static class Patch_EatEnd
    {
        private static void Prefix() => Activity.Consume = 0;
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.OnDrinkWaterComplete))]
    internal static class Patch_DrinkEnd
    {
        private static void Prefix() => Activity.Consume = 0;
    }
}

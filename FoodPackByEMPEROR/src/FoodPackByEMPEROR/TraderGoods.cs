using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;
using Il2CppTLD.Trader;
using MelonLoader;
using UnityEngine;

namespace FoodPackByEMPEROR
{
    /// <summary>FoodPack goods at the trader (1.1.8), in place of the old TraderPatches + TraderSakes.AddTo/MarkBought.
    /// Sold: everything of the pack that is found in the world as loot (cans, snacks, drinks, alcohol, tea and coffee packs),
    /// the soda cans of the drinks add-on, the eight sakes and the three craft ingredients. Not sold: recipe cards and cooked dishes.
    /// Each aurora's set is chosen by the aurora's number (the same on every machine); a bought one is marked in the trader's own
    /// saved state. Only a paid offer is marked and owed; owed goods are put into the exchange box if the game did not put them.</summary>
    public static class TraderGoods
    {
        const string Prefix = "Seagull_Trade_";
        const string Due = "Seagull_Due_";

        const string Gut = "0e552f88cb8b6b2419cdad907519fa1c";
        const string Cloth = "5eb0ecbeb0e0ca74d9b62520611c3725";
        const string Crow = "d47689c13241611419a66d4449e280b3";
        const string Ptarmigan = "a19980035a051384f947e8ea44731387";
        const string Maple = "32eeada95dbb4bd4db8887149f516d2e";
        const string Birch = "52225fe8d4f285845ac464124cb2ef46";
        const string Coal = "0742fba2a302a64418216eead1646397";
        const string Hides = "d4cbecc3ac5fef14bb982e380ee992da";
        const string Plants = "e1e63420b320a6e4d9393432918f5d67";
        const string Fish = "1e172dba4b73b9c49b418f4f2d0ac1f3";
        const string Meat = "e4abb03f915b9334f9d1e13f1fca665b";
        const string MeatOrFish = "473132e5ce883ab4eb72fb53594b7190";
        const string Kerosene = "095871882a4a44fbab401411f1165c05";
        const string Water = "ee84a2e67c4c34f139368b6c21b44216";

        /// <summary>Kind: 0 a gear item by count, 1 a category by count, 2 a category by weight (kg), 3 a liquid (litres).</summary>
        sealed class Ask
        {
            public int Kind; public string Guid; public float N;
            public Ask(int kind, string guid, float n) { Kind = kind; Guid = guid; N = n; }
        }
        static Ask GearN(string g, int n) { return new Ask(0, g, n); }
        static Ask CatN(string g, int n) { return new Ask(1, g, n); }
        static Ask CatKg(string g, float kg) { return new Ask(2, g, kg); }
        static Ask Litres(string g, float l) { return new Ask(3, g, l); }

        sealed class Offer
        {
            public string Id; public string Slot; public int Trust; public int PerSet; public Ask[] Asks;
        }

        sealed class SlotDef
        {
            public string Name; public int Trust; public int Show;
            public SlotDef(string name, int trust, int show) { Name = name; Trust = trust; Show = show; }
        }

        /// <summary>How many different goods of a kind one aurora's set shows.</summary>
        static readonly SlotDef[] Slots =
        {
            new SlotDef("can", 50, 2), new SlotDef("snack", 50, 2), new SlotDef("drink", 50, 1), new SlotDef("soda", 50, 2),
            new SlotDef("tea", 100, 1), new SlotDef("alcohol", 150, 1), new SlotDef("sake", 200, 1),
        };

        /// <summary>Prices by tier, three ways each; a good always asks the same one (chosen by its name).</summary>
        static Ask[] Price(int tier, string id)
        {
            int way = (int)(OneTimeSpawns.Fnv("TraderPrice|" + id) % 3u);
            switch (tier)
            {
                case 1:
                    if (way == 0) return new[] { CatKg(MeatOrFish, 2f), GearN(Cloth, 2) };
                    if (way == 1) return new[] { GearN(Cloth, 2), GearN(Crow, 4) };
                    return new[] { GearN(Gut, 1), GearN(Cloth, 2) };
                case 2:
                    if (way == 0) return new[] { CatKg(MeatOrFish, 3f), GearN(Cloth, 3) };
                    if (way == 1) return new[] { GearN(Gut, 2), GearN(Cloth, 2) };
                    return new[] { CatN(Hides, 1), CatKg(MeatOrFish, 2f) };
                case 3:
                    if (way == 0) return new[] { CatN(Hides, 1), GearN(Gut, 2), GearN(Cloth, 2) };
                    if (way == 1) return new[] { CatKg(MeatOrFish, 5f), GearN(Maple, 1) };
                    return new[] { CatN(Hides, 2), GearN(Crow, 4) };
                default:
                    if (way == 0) return new[] { CatN(Hides, 2), GearN(Gut, 2), Litres(Kerosene, 1f) };
                    if (way == 1) return new[] { CatKg(MeatOrFish, 8f), GearN(Cloth, 4) };
                    return new[] { CatN(Hides, 3), GearN(Birch, 1) };
            }
        }

        static readonly List<Offer> offers = new List<Offer>();
        static readonly Dictionary<string, Offer> byId = new Dictionary<string, Offer>();
        static bool ready;

        static void Add(string id, string slot, int trust, int perSet, Ask[] asks)
        {
            if (byId.ContainsKey(id)) return;
            var o = new Offer { Id = id, Slot = slot, Trust = trust, PerSet = perSet, Asks = asks };
            offers.Add(o);
            byId[id] = o;
        }

        static void A(string id, string slot, int tier)
        {
            int trust = slot == "tea" ? 100 : slot == "alcohol" ? 150 : tier >= 4 ? 150 : tier == 3 ? 100 : 50;
            Add(id, slot, trust, slot == "alcohol" ? 1 : 2, Price(tier, id));
        }

        static void Prepare()
        {
            if (ready) return;
            ready = true;
            // the craft ingredients, always there (unchanged since 1.1.x)
            Add("Sauerkraut", null, 50, 2, new[] { CatKg(Fish, 5f), Litres(Water, 2f), GearN(Maple, 2) });
            Add("WhippedCream", null, 100, 2, new[] { CatKg(Meat, 3f), Litres(Water, 1f), GearN(Ptarmigan, 4) });
            Add("IrishWhiskey", null, 200, 1, new[] { CatN(Hides, 3), Litres(Kerosene, 2f), GearN(Crow, 4) });
            // the sakes, one an aurora (unchanged)
            Add("Sake_Junmai", "sake", 200, 1, new[] { CatN(Hides, 3), GearN(Gut, 3), GearN(Cloth, 4) });
            Add("Sake_Ginjo", "sake", 200, 1, new[] { CatN(Plants, 12), Litres(Kerosene, 3f), GearN(Crow, 4) });
            Add("Sake_Koshu", "sake", 200, 1, new[] { CatKg(Fish, 6f), Litres(Water, 4f), GearN(Maple, 3) });
            Add("Sake_Nigori", "sake", 200, 1, new[] { CatKg(Meat, 6f), GearN(Gut, 2), GearN(Ptarmigan, 6) });
            Add("Sake_Honjozo", "sake", 300, 1, new[] { CatKg(MeatOrFish, 12f), GearN(Birch, 5), GearN(Maple, 6), GearN(Cloth, 8) });
            Add("Sake_Daiginjo", "sake", 300, 1, new[] { CatN(Plants, 15), GearN(Crow, 6), Litres(Water, 4f), GearN(Gut, 2) });
            Add("Sake_Genshu", "sake", 300, 1, new[] { CatN(Hides, 4), Litres(Kerosene, 4f), GearN(Coal, 10) });
            Add("Sake_Taru", "sake", 300, 1, new[] { CatKg(Meat, 10f), CatN(Hides, 3), GearN(Crow, 6) });
            // what lies in the world as loot (data/loot.json), by kind and price tier
            A("Surstromming", "can", 2);
            A("Kimchi", "can", 2);
            A("PickledHerring", "can", 2);
            A("BraisedBeef", "can", 3);
            A("Sprats", "can", 2);
            A("ClamChowder", "can", 2);
            A("CornedBeef", "can", 3);
            A("CannedSalmon", "can", 2);
            A("PeaSoup", "can", 2);
            A("ChickenNoodle", "can", 2);
            A("SmokedOysters", "can", 2);
            A("Borscht", "can", 2);
            A("SaltedMushrooms", "can", 2);
            A("SquashSpread", "can", 2);
            A("LingonberryJam", "can", 3);
            A("CloudberryJam", "can", 3);
            A("HazelnutSpread", "can", 4);
            A("FieldRation", "can", 4);
            A("Fabada", "can", 3);
            A("LiverPate", "can", 2);
            A("CodRoe", "can", 2);
            A("Natto", "can", 2);
            A("CannedBread", "can", 3);
            A("Ajvar", "can", 2);
            A("PickledBeets", "can", 2);
            A("MusselsOil", "can", 2);
            A("Kefir", "drink", 1);
            A("CranberryDrink", "drink", 1);
            A("OrangeJuice", "drink", 1);
            A("MapleWater", "drink", 1);
            A("ProteinShake", "drink", 2);
            A("SportsDrink", "drink", 1);
            A("RootBeer", "drink", 1);
            A("Mead", "alcohol", 4);
            A("Akvavit", "alcohol", 4);
            A("Icewine", "alcohol", 4);
            A("DarkRum", "alcohol", 4);
            A("TomatoJuice", "drink", 1);
            A("PickleBrine", "drink", 1);
            A("BirchSap", "drink", 1);
            A("RyeRusks", "snack", 2);
            A("Salmiakki", "snack", 1);
            A("SalmonStrips", "snack", 1);
            A("Hardtack", "snack", 2);
            A("DriedCranberries", "snack", 1);
            A("SunflowerSeeds", "snack", 2);
            A("Halva", "snack", 2);
            A("Peppermints", "snack", 1);
            A("Pastila", "snack", 2);
            A("DriedRoach", "snack", 1);
            A("MintCake", "snack", 2);
            A("PickledGarlic", "snack", 1);
            A("CoffeeBeans", "snack", 2);
            A("Oscypek", "snack", 2);
            A("Churchkhela", "snack", 2);
            A("CandiedGinger", "snack", 1);
            A("DriedBlueberries", "snack", 1);
            A("Soju_A_peach", "alcohol", 3);
            A("Soju_A_green_apple", "alcohol", 3);
            A("Soju_A_grapefruit", "alcohol", 3);
            A("Soju_A_plum", "alcohol", 3);
            A("Soju_B_wild_berries", "alcohol", 3);
            A("Soju_B_yuzu", "alcohol", 3);
            A("Soju_B_strawberry", "alcohol", 3);
            A("Soju_B_muscat", "alcohol", 3);
            A("LabradorTeaPack", "tea", 2);
            A("SpruceTeaPack", "tea", 2);
            A("DarkRoastPack", "tea", 2);
            A("ChagaTeaPack", "tea", 2);
            A("ChamomileTeaPack", "tea", 2);
            A("GingerTeaPack", "tea", 2);
            A("BlackTeaPack", "tea", 2);
            A("YerbaMatePack", "tea", 2);
            // the cans and bottles of the drinks add-on, when it is installed; litre bottles are a tier dearer
            int sodas = 0;
            foreach (ItemDef def in Addons.Items)
            {
                if (def == null || !def.Drink || def.CardFor != null) continue;
                Add(def.Id, "soda", 50, 2, Price(def.WeightKg >= 0.8f ? 2 : 1, def.Id));
                sodas++;
            }
            // anything else that lies in the world as loot (a new kind of the pack or of an add-on) is sold by the same rules
            int auto = 0;
            foreach (List<LootEntry> table in Loot.Tables.Values)
            {
                if (table == null) continue;
                foreach (LootEntry le in table)
                {
                    if (le == null || string.IsNullOrEmpty(le.Item) || byId.ContainsKey(le.Item)) continue;
                    ItemDef def;
                    if (!Items.TryGet("GEAR_Seagull_" + le.Item, out def) || def.CardFor != null) continue;
                    if (def.CooksInto != null) A(def.Id, "tea", 2);
                    else if (def.AlcoholPercent > 0f) A(def.Id, "alcohol", def.WeightKg >= 0.65f ? 4 : 3);
                    else if (def.Drink) A(def.Id, "drink", def.Calories >= 400f ? 2 : 1);
                    else if (def.WeightKg <= 0.25f) A(def.Id, "snack", def.Calories <= 350f ? 1 : 2);
                    else A(def.Id, "can", def.Calories <= 500f ? 2 : def.Calories < 1000f ? 3 : 4);
                    auto++;
                }
            }
            // only goods the pack really has
            for (int i = offers.Count - 1; i >= 0; i--)
            {
                ItemDef d;
                if (Items.TryGet("GEAR_Seagull_" + offers[i].Id, out d)) continue;
                MelonLogger.Warning("Trader: " + offers[i].Id + " is not an item of the pack, not offered");
                byId.Remove(offers[i].Id);
                offers.RemoveAt(i);
            }
            MelonLogger.Msg("Trader: " + offers.Count + " goods (" + sodas + " drinks of add-ons, " + auto + " more by the loot lists)");
        }

        static readonly Dictionary<string, TradeExchangeData> built = new Dictionary<string, TradeExchangeData>();
        static readonly HashSet<string> told = new HashSet<string>();

        public static void Log(string where, Exception e)
        {
            if (told.Add(where)) MelonLogger.Error(where + ": " + e);
        }

        public static bool IsOurs(TradeExchangeData d)
        {
            return d != null && d.m_TradeExchangeName != null && d.m_TradeExchangeName.StartsWith(Prefix, StringComparison.Ordinal);
        }

        public static string IdOf(TradeExchangeData d)
        {
            return IsOurs(d) ? d.m_TradeExchangeName.Substring(Prefix.Length) : null;
        }

        static ExchangeItem Item(Ask a)
        {
            var e = new ExchangeItem();
            switch (a.Kind)
            {
                case 0:
                    e.m_ExchangeItemType = (ExchangeItemType)0;
                    e.m_GearItemData = new AssetReferenceGearItemData(a.Guid);
                    e.m_Amount = (int)a.N;
                    break;
                case 1:
                    e.m_ExchangeItemType = (ExchangeItemType)2;
                    e.m_ItemCategory = new AssetReferenceExchangeItemCategory(a.Guid);
                    e.m_Amount = (int)a.N;
                    break;
                case 2:
                    e.m_ExchangeItemType = (ExchangeItemType)3;
                    e.m_ItemCategory = new AssetReferenceExchangeItemCategory(a.Guid);
                    e.m_Weight = ItemWeight.FromKilograms(a.N);
                    break;
                default:
                    e.m_ExchangeItemType = (ExchangeItemType)5;
                    e.m_LiquidType = new AssetReferenceLiquidType(a.Guid);
                    e.m_LiquidVolume = ItemLiquidVolume.FromLiters(a.N);
                    break;
            }
            return e;
        }

        static TradeExchangeData Build(Offer o)
        {
            TradeExchangeData known;
            if (built.TryGetValue(o.Id, out known)) return known;
            var d = new TradeExchangeData();
            d.m_TradeExchangeName = Prefix + o.Id;
            d.m_GainedItem = new AssetReferenceGearItemData(Items.ReferenceOf(o.Id));
            d.m_GainedItemAmount = 1;
            d.m_MaxExchangeInSingleTrade = 1;
            d.m_IsSpecialRequest = false;
            // the game copies both lists when a trade is accepted; null here makes the accept button do nothing
            d.m_UnlockedExchanges = new Il2CppSystem.Collections.Generic.List<AssetReferenceTradeExchange>();
            d.m_UnlockedConversations = new Il2CppSystem.Collections.Generic.List<AssetReferenceTraderConversation>();
            var text = new LocalizedString();
            text.m_LocalizationID = "SEAGULL_" + o.Id + "_DESC";
            d.m_Description = text;
            var asks = new Il2CppSystem.Collections.Generic.List<ExchangeItem>();
            var cheap = new Il2CppSystem.Collections.Generic.List<ExchangeItem>();
            foreach (Ask a in o.Asks) { asks.Add(Item(a)); cheap.Add(Item(a)); }
            d.m_ExchangeItems = asks;
            d.m_CheapExchangeItems = cheap;
            built[o.Id] = d;
            return d;
        }

        static int SetNumber(TraderState state)
        {
            return state.m_LastAuroraNum + (Data.Settings.TestKeys ? TraderSakes.TestSetOffset : 0);
        }

        static string BoughtMark(string id, int aurora, int nth)
        {
            return "Seagull_Bought_" + id + "_" + OneTimeSpawns.Text(aurora) + (nth > 1 ? "_" + OneTimeSpawns.Text(nth) : "");
        }

        static int BoughtCount(TraderState state, string id, int aurora, int limit)
        {
            var names = state.m_UnlockedExchangeNames;
            int n = 0;
            if (names != null)
                for (int i = 1; i <= limit; i++)
                    if (names.Contains(BoughtMark(id, aurora, i))) n++;
            return n;
        }

        static string shownKey;
        static List<Offer> shown = new List<Offer>();

        /// <summary>This set: per kind, its first Show goods ordered by a hash of the aurora's number, then the ingredients.</summary>
        static List<Offer> Shown(int trust, int aurora)
        {
            Prepare();
            string key = OneTimeSpawns.Text(aurora) + "|" + OneTimeSpawns.Text(trust);
            if (key == shownKey) return shown;
            var list = new List<Offer>();
            string set = OneTimeSpawns.Text(aurora);
            foreach (SlotDef s in Slots)
            {
                if (trust < s.Trust) continue;
                var pool = new List<KeyValuePair<uint, Offer>>();
                foreach (Offer o in offers)
                {
                    if (o.Slot != s.Name || trust < o.Trust) continue;
                    // the sakes keep the roll they had before 1.1.8
                    uint roll = s.Name == "sake" ? OneTimeSpawns.Fnv("TraderSake|" + set + "|" + o.Id)
                                                 : OneTimeSpawns.Fnv("TraderGoods|" + set + "|" + s.Name + "|" + o.Id);
                    pool.Add(new KeyValuePair<uint, Offer>(roll, o));
                }
                foreach (var p in pool.OrderBy(p => p.Key).Take(s.Show)) list.Add(p.Value);
            }
            foreach (Offer o in offers)
                if (o.Slot == null && trust >= o.Trust) list.Add(o);
            shownKey = key;
            shown = list;
            return list;
        }

        public static void AddTo(TraderManager tm, Il2CppSystem.Collections.Generic.List<TradeExchangeData> list)
        {
            TraderState state = tm.m_CurrentState;
            if (state == null || list == null) return;
            int aurora = SetNumber(state);
            foreach (Offer o in Shown(tm.CurrentTrust, aurora))
            {
                int left = o.PerSet - BoughtCount(state, o.Id, aurora, o.PerSet);
                if (left <= 0) continue;
                TradeExchangeData d = Build(o);
                d.m_MaxExchangeInSingleTrade = left;
                bool there = false;
                foreach (TradeExchangeData x in list)
                    if (x != null && x.m_TradeExchangeName == d.m_TradeExchangeName) { there = true; break; }
                if (!there) list.Add(d);
            }
        }

        // ---- owed goods: "Seagull_Due_<id>#n" lines in the trader's saved state, cleared by the next completed trade

        static void ClearOwed(TraderState state)
        {
            var names = state.m_UnlockedExchangeNames;
            if (names == null) return;
            for (int i = names.Count - 1; i >= 0; i--)
                if (names[i] != null && names[i].StartsWith(Due, StringComparison.Ordinal)) names.RemoveAt(i);
        }

        static void Owe(TraderState state, string id, int amount)
        {
            if (state.m_UnlockedExchangeNames == null) state.m_UnlockedExchangeNames = new Il2CppSystem.Collections.Generic.List<string>();
            var names = state.m_UnlockedExchangeNames;
            int k = 0;
            for (int i = 0; i < Math.Max(1, amount); i++)
            {
                while (names.Contains(Due + id + "#" + OneTimeSpawns.Text(k))) k++;
                names.Add(Due + id + "#" + OneTimeSpawns.Text(k));
            }
        }

        static Dictionary<string, int> Owed(TraderState state)
        {
            var owed = new Dictionary<string, int>();
            if (state == null || state.m_UnlockedExchangeNames == null) return owed;
            foreach (string n in state.m_UnlockedExchangeNames)
            {
                if (n == null || !n.StartsWith(Due, StringComparison.Ordinal)) continue;
                int hash = n.LastIndexOf('#');
                string id = hash > Due.Length ? n.Substring(Due.Length, hash - Due.Length) : n.Substring(Due.Length);
                int c;
                owed[id] = owed.TryGetValue(id, out c) ? c + 1 : 1;
            }
            return owed;
        }

        /// <summary>Before the game completes the trade: only a paid offer of ours is counted as bought and written down as owed.</summary>
        public static void MarkBought(TraderManager tm)
        {
            TraderState state = tm.m_CurrentState;
            ActiveTradeState trade = state != null ? state.m_ActiveTrade : null;
            if (state == null || trade == null || trade.m_ExchangeStates == null) return;
            Prepare();
            int aurora = SetNumber(state);
            ClearOwed(state);
            foreach (var es in trade.m_ExchangeStates)
            {
                TradeExchangeData d = es != null ? es.m_Exchange : null;
                string id = d != null ? IdOf(d) : null;
                if (id == null) continue;
                if (!d.IsFullyExchanged()) { MelonLogger.Msg("Trader: " + id + " in the trade, not paid"); continue; }
                int amount = Math.Max(1, d.m_GainedItemAmount);
                Owe(state, id, amount);
                Offer o;
                int per = byId.TryGetValue(id, out o) ? o.PerSet : 1;
                int have = BoughtCount(state, id, aurora, per);
                for (int k = 0; k < amount && have < per; k++)
                {
                    have++;
                    state.m_UnlockedExchangeNames.Add(BoughtMark(id, aurora, have));
                }
                MelonLogger.Msg("Trader: " + id + " x" + amount + " bought (" + have + " of " + per + " this set, set " + aurora + ")");
            }
        }

        /// <summary>A paid offer of ours in this trade.</summary>
        public static bool AnyPaid(ActiveTradeState trade)
        {
            if (trade == null || trade.m_ExchangeStates == null) return false;
            foreach (var es in trade.m_ExchangeStates)
            {
                TradeExchangeData d = es != null ? es.m_Exchange : null;
                if (d != null && IsOurs(d) && d.IsFullyExchanged()) return true;
            }
            return false;
        }

        /// <summary>Called just before the exchange box panel opens with the rewards. The game puts rewards into the first list of
        /// the box (ExchangeBox.PrepareAndShowRewards adds to m_Exchanges[0]) and the panel shows that list, so ours go there too.</summary>
        public static void Deliver(ExchangeBox box)
        {
            TraderManager tm = GameManager.GetTraderManager();
            var owed = Owed(tm != null ? tm.m_CurrentState : null);
            if (owed.Count == 0) return;
            var lists = box.m_Exchanges;
            if (lists == null || lists.Count == 0) { MelonLogger.Msg("Trader: rewards shown, but the box has no list"); return; }
            var first = lists[0];
            if (first == null) { MelonLogger.Msg("Trader: rewards shown, but the reward list is empty"); return; }
            var present = new Dictionary<string, int>();
            foreach (GearItemObject o in first)
            {
                GearItem gi = o != null ? o.m_GearItem : null;
                ItemDef def;
                if (gi != null && Items.TryGet(gi.name, out def))
                {
                    int c;
                    present[def.Id] = present.TryGetValue(def.Id, out c) ? c + 1 : 1;
                }
            }
            foreach (KeyValuePair<string, int> pair in owed)
            {
                int have, added = 0;
                if (!present.TryGetValue(pair.Key, out have)) have = 0;
                for (int i = have; i < pair.Value; i++)
                {
                    GearItem gi = Items.Spawn("GEAR_Seagull_" + pair.Key);
                    if (gi == null) { MelonLogger.Msg("Trader: " + pair.Key + " could not be made"); break; }
                    gi.gameObject.SetActive(false);
                    gi.transform.SetParent(box.transform, true);
                    gi.transform.localPosition = Vector3.zero;
                    gi.transform.localRotation = Quaternion.identity;
                    first.Add(new GearItemObject(gi));
                    added++;
                }
                MelonLogger.Msg("Trader: reward " + pair.Key + ": owed " + pair.Value + ", put by the game " + have + ", added " + added);
            }
        }

        public static GearItemData DataOf(TradeExchangeData d)
        {
            string id = IdOf(d);
            if (id == null) return null;
            GearItem t = Items.Template(id);
            return t != null ? t.m_GearItemData : null;
        }

        public static Texture2D IconOf(string id)
        {
            ItemDef def;
            return Items.TryGet("GEAR_Seagull_" + id, out def) && def.Icon != null ? Assets.Texture(def.Icon, false) : null;
        }
    }

    [HarmonyPatch]
    internal static class TraderGoodsPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(TraderManager), "GetAvailableTradeExchanges")]
        private static void Available(TraderManager __instance, ref Il2CppSystem.Collections.Generic.List<TradeExchangeData> __result)
        {
            try
            {
                // a copy: the list may be the trader's own saved set, and ours are not part of it
                var copy = new Il2CppSystem.Collections.Generic.List<TradeExchangeData>();
                if (__result != null)
                    foreach (TradeExchangeData d in __result)
                        if (!TraderGoods.IsOurs(d)) copy.Add(d);
                TraderGoods.AddTo(__instance, copy);
                __result = copy;
            }
            catch (Exception e) { TraderGoods.Log("Trader offers", e); }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TraderManager), "CompleteTrade")]
        private static void Completed(TraderManager __instance)
        {
            try { TraderGoods.MarkBought(__instance); }
            catch (Exception e) { TraderGoods.Log("Trader purchase", e); }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TradeExchangeData), "GetGainedItemData")]
        private static bool Gained(TradeExchangeData __instance, ref GearItemData __result)
        {
            if (!TraderGoods.IsOurs(__instance)) return true;
            __result = TraderGoods.DataOf(__instance);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TradeExchangeData), "GetGainedItemIcon")]
        private static bool Icon(TradeExchangeData __instance, ref Texture2D __result)
        {
            string id = TraderGoods.IdOf(__instance);
            if (id == null) return true;
            __result = TraderGoods.IconOf(id);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TradeExchangeData), "IsGainedItemValidInCurrentGamemode")]
        private static bool Valid(TradeExchangeData __instance, ref bool __result)
        {
            if (!TraderGoods.IsOurs(__instance)) return true;
            __result = true;
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ActiveTradeState), "HasRewards")]
        private static void Rewards(ActiveTradeState __instance, ref bool __result)
        {
            if (__result) return;
            try { if (TraderGoods.AnyPaid(__instance)) __result = true; }
            catch (Exception e) { TraderGoods.Log("Trader rewards", e); }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Panel_ExchangeBox), "ShowExchangeBox")]
        private static void Showing(ExchangeBox exchangeBox)
        {
            try
            {
                if (exchangeBox != null && exchangeBox.m_ActiveTrade != null && exchangeBox.m_ActiveTrade.m_TradeState == ActiveTradeState.State.Completed)
                    TraderGoods.Deliver(exchangeBox);
            }
            catch (Exception e) { TraderGoods.Log("Trader delivery", e); }
        }
    }
}

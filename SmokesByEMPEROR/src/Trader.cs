using Il2CppTLD.Gear;
using Il2CppTLD.Trader;

namespace SmokesByEMPEROR;

/// <summary>Smokes at the trader (Tales DLC), on top of the set he rolls each aurora (none of his own offers is pushed out).
/// Trust 100: two cigarette packs (varieties chosen by the aurora) and the Zappo lighter; trust 200: a cigar in a tube, a cigar box,
/// a tobacco pouch (each brand/kind chosen by the aurora; pipes are not sold, they lie in four fixed places) and the cutter. One of each per set: a bought one is marked in the trader's own
/// state ("SeagullSmoke_Bought_&lt;id&gt;_&lt;aurora&gt;"), which the game saves and the coop mod shares, so the limit is the same for all players.
/// Nothing random: every machine builds the same offers.</summary>
public static class TraderSmokes
{
    const string Prefix = "SeagullSmoke_Trade_";

    // what he asks, in the kinds his own offers use (GUIDs are the game's, as in FoodPack's trader)
    static class G
    {
        public const string Gut = "0e552f88cb8b6b2419cdad907519fa1c", Cloth = "5eb0ecbeb0e0ca74d9b62520611c3725", Crow = "d47689c13241611419a66d4449e280b3",
            Ptarmigan = "a19980035a051384f947e8ea44731387", Maple = "32eeada95dbb4bd4db8887149f516d2e", Birch = "52225fe8d4f285845ac464124cb2ef46";
        public const string Hides = "d4cbecc3ac5fef14bb982e380ee992da", Plants = "e1e63420b320a6e4d9393432918f5d67", MeatOrFish = "473132e5ce883ab4eb72fb53594b7190";
        public const string Kerosene = "095871882a4a44fbab401411f1165c05", Water = "ee84a2e67c4c34f139368b6c21b44216";
    }

    abstract record Ask;
    record GearN(string Guid, int N) : Ask;
    record CatN(string Guid, int N) : Ask;
    record CatKg(string Guid, float Kg) : Ask;
    record Litres(string Guid, float L) : Ask;

    /// <summary>A slot of the set: from its candidates, Count different ones are offered per aurora, one of each.</summary>
    record Slot(string Name, int Trust, Func<Def, bool> Pick, int Count, Ask[] Asks);

    static readonly Slot[] Slots =
    {
        new("pack", 100, d => d.Kind == Kind.Pack && d.CigTexture != null && !d.Rare && !d.Cigarillo, 2, new Ask[] { new CatKg(G.MeatOrFish, 2f), new Litres(G.Water, 1f) }),
        new("cigarillo", 150, d => d.Kind == Kind.Pack && d.Cigarillo && !d.Rare, 1, new Ask[] { new CatKg(G.MeatOrFish, 3f), new Litres(G.Water, 1f) }),
        new("lighter", 100, d => d.Kind == Kind.Lighter, 1, new Ask[] { new GearN(G.Cloth, 3), new GearN(G.Crow, 2), new Litres(G.Kerosene, 1f) }),
        new("refill", 100, d => d.Kind == Kind.Refill, 1, new Ask[] { new GearN(G.Cloth, 2), new Litres(G.Kerosene, .5f) }),
        new("tube", 200, d => d.Kind == Kind.Tube, 1, new Ask[] { new CatN(G.Hides, 1), new CatKg(G.MeatOrFish, 3f) }),
        new("box", 200, d => d.Kind == Kind.Box, 1, new Ask[] { new CatN(G.Hides, 3), new CatKg(G.MeatOrFish, 8f), new GearN(G.Maple, 3) }),
        new("pouch", 200, d => d.Kind == Kind.Pouch, 1, new Ask[] { new CatN(G.Plants, 8), new GearN(G.Crow, 3) }),
        new("cutter", 200, d => d.Kind == Kind.Cutter, 1, new Ask[] { new GearN(G.Gut, 3), new Litres(G.Kerosene, 1f) }),
    };

    static readonly Dictionary<string, TradeExchangeData> built = new();
    static readonly Dictionary<string, GearItem> templates = new();

    public static bool IsOurs(TradeExchangeData? d) => d != null && d.m_TradeExchangeName != null && d.m_TradeExchangeName.StartsWith(Prefix, StringComparison.Ordinal);

    public static string? IdOf(TradeExchangeData d) => IsOurs(d) ? d.m_TradeExchangeName.Substring(Prefix.Length) : null;

    static uint Fnv(string s)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char ch in s) { h ^= ch; h *= 16777619; }
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return h;
        }
    }

    static ExchangeItem Item(Ask a)
    {
        var e = new ExchangeItem();
        switch (a)
        {
            case GearN g:
                e.m_ExchangeItemType = ExchangeItemType.GearAmount;
                e.m_GearItemData = new AssetReferenceGearItemData(g.Guid);
                e.m_Amount = g.N;
                break;
            case CatN c:
                e.m_ExchangeItemType = ExchangeItemType.CategoryAmount;
                e.m_ItemCategory = new AssetReferenceExchangeItemCategory(c.Guid);
                e.m_Amount = c.N;
                break;
            case CatKg k:
                e.m_ExchangeItemType = ExchangeItemType.CategoryWeight;
                e.m_ItemCategory = new AssetReferenceExchangeItemCategory(k.Guid);
                e.m_Weight = ItemWeight.FromKilograms(k.Kg);
                break;
            case Litres l:
                e.m_ExchangeItemType = ExchangeItemType.Liquid;
                e.m_LiquidType = new AssetReferenceLiquidType(l.Guid);
                e.m_LiquidVolume = ItemLiquidVolume.FromLiters(l.L);
                break;
        }
        return e;
    }

    static TradeExchangeData Build(string id, Slot s)
    {
        if (built.TryGetValue(id, out var known)) return known;
        var d = new TradeExchangeData
        {
            m_TradeExchangeName = Prefix + id,
            m_GainedItem = new AssetReferenceGearItemData(Defs.RefPrefix + id),
            m_GainedItemAmount = 1,
            m_MaxExchangeInSingleTrade = 1,
            m_IsSpecialRequest = false,
            m_Description = new LocalizedString { m_LocalizationID = "SMOKES_" + id + "_DESC" },
        };
        var asks = new Il2CppSystem.Collections.Generic.List<ExchangeItem>();
        var cheap = new Il2CppSystem.Collections.Generic.List<ExchangeItem>();
        foreach (var a in s.Asks) { asks.Add(Item(a)); cheap.Add(Item(a)); }
        d.m_ExchangeItems = asks;
        d.m_CheapExchangeItems = cheap;
        return built[id] = d;
    }

    static string Bought(string id, int aurora) => $"SeagullSmoke_Bought_{id}_{aurora}";

    /// <summary>What this set offers: per slot, its first Count candidates ordered by a hash of the aurora's number.</summary>
    static IEnumerable<(string id, Slot slot)> Shown(int trust, int aurora)
    {
        foreach (var s in Slots)
        {
            if (trust < s.Trust) continue;
            foreach (var d in Defs.All.Where(s.Pick).OrderBy(d => Fnv($"SmokeTrade|{aurora}|{s.Name}|{d.Id}")).Take(s.Count))
                yield return (d.Id, s);
        }
    }

    public static void AddTo(TraderManager tm, Il2CppSystem.Collections.Generic.List<TradeExchangeData> list)
    {
        var state = tm.m_CurrentState;
        if (state == null || list == null) return;
        int aurora = state.m_LastAuroraNum;
        var names = state.m_UnlockedExchangeNames;
        foreach (var (id, slot) in Shown(tm.CurrentTrust, aurora))
        {
            if (names != null && names.Contains(Bought(id, aurora))) continue;
            list.Add(Build(id, slot));
        }
    }

    public static void MarkBought(TraderManager tm)
    {
        var state = tm.m_CurrentState;
        var trade = state?.m_ActiveTrade;
        if (state == null || trade?.m_ExchangeStates == null) return;
        int aurora = state.m_LastAuroraNum;
        foreach (var es in trade.m_ExchangeStates)
        {
            var d = es?.m_Exchange;
            var id = d != null ? IdOf(d) : null;
            if (id == null) continue;
            state.m_UnlockedExchangeNames ??= new Il2CppSystem.Collections.Generic.List<string>();
            if (state.m_UnlockedExchangeNames.Contains(Bought(id, aurora))) continue;
            state.m_UnlockedExchangeNames.Add(Bought(id, aurora));
            Safe.Info($"Trader: {id} bought (set {aurora})");
        }
    }

    /// <summary>A hidden copy kept alive: its data is what the trade shows and gives.</summary>
    public static GearItemData? DataOf(TradeExchangeData d)
    {
        var id = IdOf(d);
        if (id == null) return null;
        if (!templates.TryGetValue(id, out var t) || t == null)
        {
            t = Items.Spawn(id)!;
            if (t == null) return null;
            t.gameObject.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(t.gameObject);
            templates[id] = t;
        }
        return t.m_GearItemData;
    }
}

[HarmonyPatch]
internal static class TraderPatches
{
    [HarmonyPostfix, HarmonyPatch(typeof(TraderManager), nameof(TraderManager.GetAvailableTradeExchanges))]
    static void Available(TraderManager __instance, ref Il2CppSystem.Collections.Generic.List<TradeExchangeData> __result)
    {
        try
        {
            // a copy: the list may be the trader's own saved set, and ours are not part of it
            var copy = new Il2CppSystem.Collections.Generic.List<TradeExchangeData>();
            if (__result != null)
                foreach (var d in __result)
                    if (!TraderSmokes.IsOurs(d)) copy.Add(d);
            TraderSmokes.AddTo(__instance, copy);
            __result = copy;
        }
        catch (Exception e) { Safe.Log("Trader offers", e); }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(TraderManager), nameof(TraderManager.CompleteTrade))]
    static void Completed(TraderManager __instance)
    {
        try { TraderSmokes.MarkBought(__instance); }
        catch (Exception e) { Safe.Log("Trader purchase", e); }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(TradeExchangeData), nameof(TradeExchangeData.GetGainedItemData))]
    static bool Gained(TradeExchangeData __instance, ref GearItemData __result)
    {
        if (!TraderSmokes.IsOurs(__instance)) return true;
        __result = TraderSmokes.DataOf(__instance)!;
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(TradeExchangeData), nameof(TradeExchangeData.GetGainedItemIcon))]
    static bool Icon(TradeExchangeData __instance, ref Texture2D __result)
    {
        var id = TraderSmokes.IdOf(__instance);
        if (id == null) return true;
        var def = Defs.ById(id);
        __result = def != null ? Assets.Texture(def.Icon, compress: false)! : null!;
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(TradeExchangeData), nameof(TradeExchangeData.IsGainedItemValidInCurrentGamemode))]
    static bool Valid(TradeExchangeData __instance, ref bool __result)
    {
        if (!TraderSmokes.IsOurs(__instance)) return true;
        __result = true;
        return false;
    }
}

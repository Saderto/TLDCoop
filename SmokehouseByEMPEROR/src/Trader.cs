using Il2CppTLD.Trader;

namespace SmokehouseByEMPEROR;

/// <summary>Smoked food at the trader (Tales DLC), on top of the set he rolls each aurora (none of his own offers is pushed out).
/// Trust 100: two hot-smoked pieces; trust 200: one cold-smoked. Which ones is chosen by the aurora's number, the same on every machine.
/// A bought one is marked in the trader's own state, which the game saves and the coop mod shares.</summary>
public static class TraderSmoked
{
    const string Prefix = "SeagullSH_Trade_";

    // what he asks, in the kinds his own offers use (GUIDs are the game's, as in the other EMPEROR mods)
    static class G
    {
        public const string Gut = "0e552f88cb8b6b2419cdad907519fa1c", Cloth = "5eb0ecbeb0e0ca74d9b62520611c3725", Crow = "d47689c13241611419a66d4449e280b3";
        public const string Hides = "d4cbecc3ac5fef14bb982e380ee992da";
    }

    record Ask(string Guid, int N, bool Category = false);
    record Slot(string Name, int Trust, Func<Def, bool> Pick, int Count, Ask[] Asks);

    static readonly Slot[] Slots =
    {
        new("hot", 100, d => d.Species != null && d.Hot && !d.Salted, 2, new[] { new Ask(G.Cloth, 2), new Ask(G.Crow, 2) }),
        new("cold", 200, d => d.Species != null && !d.Hot && !d.Salted, 1, new[] { new Ask(G.Hides, 1, true), new Ask(G.Gut, 2) }),
        // «Заготовки» (cans, sausages) are not sold: the owner took the trader out of that chain (11-12)
    };

    static readonly Dictionary<string, TradeExchangeData> built = new();

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
        if (a.Category)
        {
            e.m_ExchangeItemType = ExchangeItemType.CategoryAmount;
            e.m_ItemCategory = new AssetReferenceExchangeItemCategory(a.Guid);
        }
        else
        {
            e.m_ExchangeItemType = ExchangeItemType.GearAmount;
            e.m_GearItemData = new AssetReferenceGearItemData(a.Guid);
        }
        e.m_Amount = a.N;
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
            m_Description = new LocalizedString { m_LocalizationID = "SMOKEHOUSE_" + id + "_DESC" },
        };
        var asks = new Il2CppSystem.Collections.Generic.List<ExchangeItem>();
        var cheap = new Il2CppSystem.Collections.Generic.List<ExchangeItem>();
        foreach (var a in s.Asks) { asks.Add(Item(a)); cheap.Add(Item(a)); }
        d.m_ExchangeItems = asks;
        d.m_CheapExchangeItems = cheap;
        return built[id] = d;
    }

    static string Bought(string id, int aurora) => $"SeagullSH_Bought_{id}_{aurora}";

    public static void AddTo(TraderManager tm, Il2CppSystem.Collections.Generic.List<TradeExchangeData> list)
    {
        var state = tm.m_CurrentState;
        if (state == null || list == null) return;
        int aurora = state.m_LastAuroraNum;
        var names = state.m_UnlockedExchangeNames;
        foreach (var s in Slots)
        {
            if (tm.CurrentTrust < s.Trust) continue;
            foreach (var d in Defs.All.Where(s.Pick).OrderBy(d => Fnv($"SHTrade|{aurora}|{s.Name}|{d.Id}")).Take(s.Count))
            {
                if (names != null && names.Contains(Bought(d.Id, aurora))) continue;
                list.Add(Build(d.Id, s));
            }
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
                    if (!TraderSmoked.IsOurs(d)) copy.Add(d);
            TraderSmoked.AddTo(__instance, copy);
            __result = copy;
        }
        catch (Exception e) { Safe.Log("Trader offers", e); }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(TraderManager), nameof(TraderManager.CompleteTrade))]
    static void Completed(TraderManager __instance)
    {
        try { TraderSmoked.MarkBought(__instance); }
        catch (Exception e) { Safe.Log("Trader purchase", e); }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(TradeExchangeData), nameof(TradeExchangeData.GetGainedItemData))]
    static bool Gained(TradeExchangeData __instance, ref GearItemData __result)
    {
        var id = TraderSmoked.IdOf(__instance);
        if (id == null) return true;
        __result = Items.Template(id)?.m_GearItemData!;
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(TradeExchangeData), nameof(TradeExchangeData.GetGainedItemIcon))]
    static bool Icon(TradeExchangeData __instance, ref Texture2D __result)
    {
        var id = TraderSmoked.IdOf(__instance);
        if (id == null) return true;
        var def = Defs.ById(id);
        __result = def != null ? Assets.Texture(def.Icon, compress: false)! : null!;
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(TradeExchangeData), nameof(TradeExchangeData.IsGainedItemValidInCurrentGamemode))]
    static bool Valid(TradeExchangeData __instance, ref bool __result)
    {
        if (!TraderSmoked.IsOurs(__instance)) return true;
        __result = true;
        return false;
    }
}

using Il2CppTLD.Gear;
using Il2CppTLD.Trader;

namespace SmokesByEMPEROR;

/// <summary>Our goods reach the exchange box (11-15, friend's report: paid for, never appeared).
/// The game gives a reward only when the gained item's address is one of its own (ActiveTradeState.HasRewards checks
/// m_GainedItem.RuntimeKeyIsValid), so a trade of only our goods ended with nothing, and the box never listed ours either.
/// Now: when the trade is completed every paid offer of ours is written into the trader's saved state as owed
/// ("SeagullSmoke_Due_&lt;id&gt;#n", saved by the game and shared by the coop mod), the trade counts as having rewards,
/// and when the box shows the rewards whatever the game did not put in is added. Owed lines are cleared by the next trade.</summary>
internal static class TraderDelivery
{
    const string Due = "SeagullSmoke_Due_";

    public static void ClearOwed(TraderState state)
    {
        var names = state.m_UnlockedExchangeNames;
        if (names == null) return;
        for (int i = names.Count - 1; i >= 0; i--)
            if (names[i] != null && names[i].StartsWith(Due, StringComparison.Ordinal)) names.RemoveAt(i);
    }

    public static void Owe(TraderState state, string id, int amount)
    {
        state.m_UnlockedExchangeNames ??= new Il2CppSystem.Collections.Generic.List<string>();
        var names = state.m_UnlockedExchangeNames;
        int k = 0;
        for (int i = 0; i < Math.Max(1, amount); i++)
        {
            while (names.Contains($"{Due}{id}#{k}")) k++;
            names.Add($"{Due}{id}#{k}");
        }
    }

    static Dictionary<string, int> Owed(TraderState? state)
    {
        var owed = new Dictionary<string, int>();
        if (state?.m_UnlockedExchangeNames == null) return owed;
        foreach (var n in state.m_UnlockedExchangeNames)
        {
            if (n == null || !n.StartsWith(Due, StringComparison.Ordinal)) continue;
            int hash = n.LastIndexOf('#');
            var id = hash > Due.Length ? n.Substring(Due.Length, hash - Due.Length) : n.Substring(Due.Length);
            owed[id] = owed.TryGetValue(id, out var c) ? c + 1 : 1;
        }
        return owed;
    }

    /// <summary>A paid offer of ours in this trade.</summary>
    public static bool AnyPaid(ActiveTradeState? trade)
    {
        if (trade?.m_ExchangeStates == null) return false;
        foreach (var es in trade.m_ExchangeStates)
        {
            var d = es?.m_Exchange;
            if (d != null && TraderSmokes.IsOurs(d) && d.IsFullyExchanged()) return true;
        }
        return false;
    }

    /// <summary>Called just before the exchange box panel opens with the rewards. The game puts rewards into the first list of the box
    /// (ExchangeBox.PrepareAndShowRewards adds to m_Exchanges[0]) and the panel shows that list, so ours go there too.</summary>
    public static void Deliver(ExchangeBox box)
    {
        var owed = Owed(GameManager.GetTraderManager()?.m_CurrentState);
        if (owed.Count == 0) return;
        var lists = box.m_Exchanges;
        if (lists == null || lists.Count == 0) { Safe.Info("Trader: rewards shown, but the box has no list"); return; }
        var last = lists[0];
        if (last == null) { Safe.Info("Trader: rewards shown, but the reward list is empty"); return; }
        var present = new Dictionary<string, int>();
        foreach (var o in last)
        {
            var gi = o?.m_GearItem;
            if (gi != null && Defs.TryGet(gi.name, out var def)) present[def.Id] = present.TryGetValue(def.Id, out var c) ? c + 1 : 1;
        }
        foreach (var (id, n) in owed)
        {
            int have = present.TryGetValue(id, out var h) ? h : 0, added = 0;
            for (int i = have; i < n; i++)
            {
                var gi = Items.Spawn(id);
                if (gi == null) { Safe.Info($"Trader: {id} could not be made"); break; }
                gi.gameObject.SetActive(false);
                gi.transform.SetParent(box.transform, true);
                gi.transform.localPosition = Vector3.zero;
                gi.transform.localRotation = Quaternion.identity;
                last.Add(new GearItemObject(gi));
                added++;
            }
            Safe.Info($"Trader: reward {id}: owed {n}, put by the game {have}, added {added}");
        }
    }
}

[HarmonyPatch]
internal static class TraderDeliveryPatches
{
    [HarmonyPostfix, HarmonyPatch(typeof(ActiveTradeState), nameof(ActiveTradeState.HasRewards))]
    static void Rewards(ActiveTradeState __instance, ref bool __result)
    {
        if (__result) return;
        try { if (TraderDelivery.AnyPaid(__instance)) __result = true; }
        catch (Exception e) { Safe.Log("Trader rewards", e); }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Panel_ExchangeBox), nameof(Panel_ExchangeBox.ShowExchangeBox))]
    static void Showing(ExchangeBox exchangeBox)
    {
        try
        {
            if (exchangeBox?.m_ActiveTrade != null && exchangeBox.m_ActiveTrade.m_TradeState == ActiveTradeState.State.Completed)
                TraderDelivery.Deliver(exchangeBox);
        }
        catch (Exception e) { Safe.Log("Trader delivery", e); }
    }
}

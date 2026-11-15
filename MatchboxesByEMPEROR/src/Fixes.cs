using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace MatchboxesByEMPEROR;

/// <summary>0.3.0, after the first test in game (11-14). What the game does with matches, read from its code:
/// MatchesItem.Ignite makes the rest of the box a new item (PlayerManager.InstantiateItemInPlayerInventory(item, units − 1, DoNotStack)),
/// keeps it in m_ClonedStack and takes the StackableItem off the match in the hand; when the match burns out, DestroyAndUnequip(true)
/// puts the hands away and calls EquipItem(m_ClonedStack). The light-source wheel lists items by the names in m_LightSourceRadialOrder.</summary>
internal static class Inv
{
    public static IEnumerable<GearItem> All()
    {
        var inv = GameManager.GetInventoryComponent();
        var items = inv != null ? inv.m_Items : null;
        if (items == null) yield break;
        for (int i = 0; i < items.Count; i++)
        {
            GearItem? g = null;
            try { var o = items[i]; g = o != null ? o.m_GearItem : null; } catch { }
            if (g != null && g) yield return g;
        }
    }

    public static bool Usable(GearItem g) => g.m_MatchesItem != null && g.m_StackableItem != null && g.m_StackableItem.m_Units > 0 && !g.m_WornOut;

    /// <summary>A box of ours to take next: the same label first, then the same material, the emptiest first (finish the open box).</summary>
    public static GearItem? Box(MatchesType type, string? preferId = null)
    {
        GearItem? best = null;
        int bestScore = int.MaxValue;
        foreach (var g in All())
        {
            if (!Defs.Is(g, out var d) || !Usable(g) || g.m_MatchesItem.m_MatchesType != type) continue;
            int score = (d.Id == preferId ? 0 : 100000) + g.m_StackableItem.m_Units;
            if (score < bestScore) { best = g; bestScore = score; }
        }
        return best;
    }
}

/// <summary>Our boxes in the light-source wheel: they have their own names, which the wheel's list does not know. One wooden box and
/// one matchbook of ours are shown when the game's own are not in the pack (the wheel has few places).</summary>
[HarmonyPatch(typeof(Panel_ActionsRadial), nameof(Panel_ActionsRadial.GetGearItemsForRadial))]
internal static class WheelPatch
{
    static bool Place(Il2CppStringArray order, Il2CppSystem.Collections.Generic.List<GearItem> list, string name, GearItem g)
    {
        int at = -1;
        for (int i = 0; i < order.Length && i < list.Count; i++) if (order[i] == name && list[i] == null) { at = i; break; }
        if (at < 0) for (int i = 0; i < list.Count; i++) if (list[i] == null) { at = i; break; }
        if (at < 0) return false;
        list[at] = g;
        return true;
    }

    static void Postfix(Il2CppStringArray itemOrder, ref int items, Il2CppSystem.Collections.Generic.List<GearItem> __result)
    {
        try
        {
            if (itemOrder == null || __result == null) return;
            bool light = false;
            foreach (var n in itemOrder) if (n == "GEAR_WoodMatches" || n == "GEAR_PackMatches") { light = true; break; }
            if (!light) return;
            bool wood = false, card = false;
            for (int i = 0; i < __result.Count; i++)
            {
                var g = __result[i];
                if (g == null || g.m_MatchesItem == null) continue;
                if (g.m_MatchesItem.m_MatchesType == MatchesType.WoodMatches) wood = true; else card = true;
            }
            // 0.3.2: the list is the wheel itself, one place per name of the order, empty places are null (read from the game's code;
            // the 0.3.1 test: a box added after the last place was never drawn). Ours take the place of the game's matches of that type.
            int added = 0;
            if (!wood && Inv.Box(MatchesType.WoodMatches) is { } w && Place(itemOrder, __result, "GEAR_WoodMatches", w)) added++;
            if (!card && Inv.Box(MatchesType.CardboardMatches) is { } c && Place(itemOrder, __result, "GEAR_PackMatches", c)) added++;
            if (added > 0) { items += added; Safe.Debug($"Wheel: {added} of our boxes put into the light sources, {__result.Count} places, count {items}"); }
        }
        catch (Exception e) { Safe.Log("Light wheel", e); }
    }
}

/// <summary>The next match after one burns out. The game's own way (EquipItem right after putting the hands away) did not take our
/// box in the 0.2.1 test, so we remember what it wanted and equip it ourselves as soon as the hands are free.</summary>
[HarmonyPatch(typeof(MatchesItem), nameof(MatchesItem.DestroyAndUnequip))]
internal static class NextMatch
{
    static GearItem? rest;
    static string? id;
    static MatchesType type;
    static float since = -100f;
    static bool pending;

    static void Prefix(MatchesItem __instance, bool tryEquipNext)
    {
        try
        {
            var g = __instance.m_GearItem;
            if (!Defs.Is(g, out var def)) return;
            var c = __instance.m_ClonedStack;
            bool alive = c != null && c;
            Safe.Debug($"Match out: {def.Id}, next wanted {tryEquipNext}, rest of the box {(alive ? c!.name + " with " + (c.m_StackableItem != null ? c.m_StackableItem.m_Units : -1) + " matches, in the pack " + c.m_InPlayerInventory : "none")}");
            // 0.3.2 (the owner): a lit match thrown away with the holster key is followed by the next one from the box, as after
            // a burnt one. Only then: the same call comes when the player takes another item, and that must not be undone.
            if (!tryEquipNext && Time.time - holsterAt > 3f) { pending = false; return; }
            rest = alive ? c : null; id = def.Id; type = __instance.m_MatchesType; since = Time.time; pending = true;
        }
        catch (Exception e) { Safe.Log("Match out", e); }
    }

    static float holsterAt = -100f;

    public static void Tick()
    {
        try
        {
            var pm0 = GameManager.GetPlayerManagerComponent();
            var h = pm0 != null ? pm0.m_ItemInHands : null;
            if (h != null && h && h.m_MatchesItem != null && h.m_MatchesItem.IsBurning() && Defs.Is(h, out _)
                && (InputManager.GetHolsterPressed(pm0) || Input.GetKeyDown(KeyCode.H))) { holsterAt = Time.time; Safe.Debug("Next match: the lit match is being thrown away"); }
        }
        catch { }
        if (!pending) return;
        try
        {
            float t = Time.time - since;
            if (t < .15f) return;
            var pm = GameManager.GetPlayerManagerComponent();
            if (pm == null || t > 4f) { pending = false; return; }
            var held = pm.m_ItemInHands;
            if (held != null && held && held.m_MatchesItem != null && !held.m_MatchesItem.IsBurning() && Inv.Usable(held)) { Safe.Debug("Next match: the game took " + held.name + " itself"); pending = false; return; }
            if (held != null && held) { if (t > 1.5f) pending = false; return; }       // the burnt match still going away, or the player took something else
            if (InterfaceManager.IsOverlayActiveImmediate() || !pm.IsReadyToEquip()) return;
            GearItem? next = rest != null && rest && rest.m_InPlayerInventory && Inv.Usable(rest) ? rest : Inv.Box(type, id);
            pending = false;
            if (next == null) { Safe.Debug("Next match: no box left"); return; }
            Safe.Debug($"Next match: taking {next.name} ({next.m_StackableItem.m_Units} matches) after {t:0.00} s");
            pm.EquipItem(next, false);
        }
        catch (Exception e) { pending = false; Safe.Log("Next match", e); }
    }
}

/// <summary>0.2.1 test: taking the game's own matches from the ground ended in an error inside the game (NullReference in
/// PlayerManager.AddItemToPlayerInventory) and the matches were gone. The cause is not known yet, so: what is being taken is written
/// to the log, and if the game falls over on matches they are put into the pack directly.</summary>
[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.AddItemToPlayerInventory))]
internal static class PickupGuard
{
    static string Describe(GearItem? g)
    {
        if (g == null) return "null";
        try
        {
            if (!g) return "destroyed item";
            var st = g.m_StackableItem;
            return $"{g.name}, data {(g.m_GearItemData != null ? g.m_GearItemData.name : "NONE")}, prefab ref {(g.m_GearItemData != null && g.m_GearItemData.m_PrefabReference != null ? g.m_GearItemData.m_PrefabReference.AssetGUID : "NONE")}, stack {(st != null && st ? st.m_Units + " units, shares " + (st.m_ShareStackWithGear?.Length ?? -1) + ", split item " + (st.m_InstantiateStackItem != null) : "none")}, in pack {g.m_InPlayerInventory}, in container {g.m_InsideContainer}";
        }
        catch (Exception e) { return "unreadable: " + e.Message; }
    }

    static void Prefix(GearItem gi, out string? __state)
    {
        __state = null;
        try
        {
            if (gi == null) { __state = "null item"; return; }
            if (gi.m_MatchesItem == null) return;
            __state = Describe(gi);
            Safe.Debug("Pickup of matches: " + __state);
        }
        catch { }
    }

    static Exception? Finalizer(Exception? __exception, GearItem gi, string? __state, ref GearItem __result)
    {
        if (__exception == null) return null;
        try
        {
            MelonLogger.Warning($"Pickup failed inside the game for: {__state ?? Describe(gi)}");
            int n = 0;
            foreach (var g in Inv.All())
            {
                if (g.m_MatchesItem == null) continue;
                if (n++ < 200 && (g.m_StackableItem == null || !g.m_StackableItem || g.m_GearItemData == null)) MelonLogger.Warning("  odd matches in the pack: " + Describe(g));
            }
            MelonLogger.Warning($"  {n} match items in the pack; held: {Describe(GameManager.GetPlayerManagerComponent()?.m_ItemInHands)}");
            if (gi != null && gi && gi.m_MatchesItem != null && !gi.m_InPlayerInventory)
            {
                GameManager.GetInventoryComponent().AddGear(gi, false);
                MelonLogger.Warning("  the matches were put into the pack directly: in pack " + gi.m_InPlayerInventory);
                __result = gi;
                return null;
            }
        }
        catch (Exception e) { Safe.Log("Pickup guard", e); }
        return __exception;
    }
}

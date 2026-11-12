namespace SmokesByEMPEROR;

/// <summary>The game gives a first aid item no button in the inventory (it is used from the first aid panel), so ours get the equip button,
/// named for what it does («Закурить», «Обрезать», «Открыть»), and that button does our action.</summary>
[HarmonyPatch]
internal static class InventoryButton
{
    static bool Ours(GearItem? gi, out Def def) => Defs.Is(gi, out def) && def.Kind != Kind.Lit && def.Kind != Kind.Lighter;

    [HarmonyPostfix, HarmonyPatch(typeof(ItemDescriptionPage), nameof(ItemDescriptionPage.UpdateButtons))]
    static void Buttons(ItemDescriptionPage __instance, GearItem gi)
    {
        try
        {
            if (!Ours(gi, out var def)) return;
            string text = Texts.Get("SMOKES_ACT_" + def.Kind.ToString().ToUpperInvariant());
            if (__instance.m_MouseButtonEquip != null) __instance.m_MouseButtonEquip.SetActive(true);
            if (__instance.m_Label_MouseButtonEquip != null) __instance.m_Label_MouseButtonEquip.text = text;
            if (__instance.m_UIButton_ButtonEquip != null)
            {
                __instance.m_UIButton_ButtonEquip.gameObject.SetActive(true);
                __instance.m_UIButton_ButtonEquip.isEnabled = true;
            }
        }
        catch (Exception e)
        {
            Safe.Log("Inventory button", e);
        }
    }

    [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(Panel_Inventory), nameof(Panel_Inventory.OnEquip))]
    static bool Equip(Panel_Inventory __instance)
    {
        GearItem? gi = null;
        try { gi = __instance.GetCurrentlySelectedItem()?.m_GearItem; }
        catch (Exception e) { Safe.Log("Selected item", e); }
        if (!Ours(gi, out var def)) { Smoking.PickedInMenu(gi); return true; }
        try
        {
            Safe.Info("Inventory button: " + def.Id);
            Use.Do(gi!, def);
        }
        catch (Exception e)
        {
            Safe.Log("Use from the inventory " + def.Id, e);
        }
        try
        {
            if (__instance.IsEnabled()) __instance.RefreshTable();
        }
        catch (Exception e)
        {
            Safe.Log("Inventory refresh", e);
        }
        return false;
    }
}

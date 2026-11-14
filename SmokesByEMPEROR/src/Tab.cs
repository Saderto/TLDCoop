namespace SmokesByEMPEROR;

/// <summary>An inventory tab of our own («Табак» / «Tobacco»): a copy of the game's last filter button put after it, showing only our items;
/// our items leave every other tab (first aid included) and stay under «All».</summary>
[HarmonyPatch]
internal static class Tab
{
    public const string FilterName = "SMOKES_TAB";
    static GameObject? button;
    static UIButton? ui;
    static IntPtr builtFor;
    static bool logged;

    // our items that leave the game's tabs; lighters and their refills stay under «Fire starting» too
    static bool Ours(GearItem? gi) => Defs.Is(gi, out var d) && d.Kind != Kind.Lighter && d.Kind != Kind.Refill;
    // 11-10: what our tab shows: everything of ours, lighters and refills included
    static bool InTab(GearItem? gi) => Defs.Is(gi, out _);

    [HarmonyPostfix, HarmonyPatch(typeof(Panel_Inventory), nameof(Panel_Inventory.Enable), new[] { typeof(bool) })]
    static void Enabled(Panel_Inventory __instance, bool enable)
    {
        if (!enable || !Settings.SmokesTab.Value) return;
        try { Build(__instance); }
        catch (Exception e) { Safe.Log("Tobacco tab", e); }
    }

    static void Build(Panel_Inventory p)
    {
        if (button != null && button && builtFor == p.Pointer) return;
        var buttons = p.m_FilterButtons;
        if (buttons == null || buttons.Length < 2) return;
        if (!logged)
        {
            logged = true;
            var names = new List<string>();
            foreach (var b in buttons)
                if (b != null) names.Add($"{b.name} sprite {b.normalSprite} at {b.transform.localPosition:F0}");
            Safe.Info($"Inventory filter buttons: {string.Join("; ", names)}; all = {p.m_FilterAllButton?.name}");
        }
        UIButton last = buttons[buttons.Length - 1], prev = buttons[buttons.Length - 2];
        var step = last.transform.localPosition - prev.transform.localPosition;
        button = UnityEngine.Object.Instantiate(last.gameObject, last.transform.parent);
        button.name = "Button_SmokesTab";
        button.transform.localPosition = last.transform.localPosition + step;
        button.transform.localScale = last.transform.localScale;
        ui = button.GetComponent<UIButton>();
        // the click goes straight to us; the copied onClick (the game's own filter change) is dropped
        if (ui != null && ui.onClick != null) ui.onClick.Clear();
        UIEventListener.Get(button).onClick = (UIEventListener.VoidDelegate)new Action<GameObject>(_ => Select(p));
        string sprite = PickSprite(last);
        if (sprite != "" && ui != null)
        {
            ui.normalSprite = sprite;
            ui.hoverSprite = sprite;
            ui.pressedSprite = sprite;
            ui.disabledSprite = sprite;
        }
        foreach (var lab in button.GetComponentsInChildren<UILabel>(true)) lab.text = Texts.Get(FilterName);
        builtFor = p.Pointer;
        Safe.Info($"Tobacco tab added after {last.name} at {button.transform.localPosition:F0}, icon {sprite}");
    }

    /// <summary>An icon from the button's own atlas: a match, a fire or a torch, in that order.</summary>
    static string PickSprite(UIButton b)
    {
        try
        {
            var sp = b.GetComponentInChildren<UISprite>(true);
            var atlas = sp != null ? sp.atlas : null;
            if (atlas == null) return "";
            var all = new List<string>();
            foreach (var d in atlas.spriteList) if (d != null) all.Add(d.name);
            Safe.Info($"Tab atlas {atlas.name}: {all.Count} sprites; candidates {string.Join(", ", all.FindAll(n => n.Contains("match") || n.Contains("fire") || n.Contains("torch") || n.Contains("smok") || n.Contains("cig")))}");
            foreach (var want in new[] { "smok", "cig", "match", "torch", "fire" })
            {
                var hit = all.Find(n => n.ToLowerInvariant().Contains(want) && n.ToLowerInvariant().Contains("filter"));
                hit ??= all.Find(n => n.ToLowerInvariant().Contains(want));
                if (hit != null) return hit;
            }
        }
        catch (Exception e)
        {
            Safe.Log("Tab icon", e);
        }
        return "";
    }

    static bool selected;

    static void Select(Panel_Inventory p)
    {
        try
        {
            selected = true;
            p.m_FilterName = FilterName;
            p.m_SelectedFilterButton = ui;
            p.UpdateFilteredInventoryList();
            p.RefreshTable();
            p.RefreshFilterIconColors();
            Labels(p);
            Safe.Info("Tobacco tab selected");
        }
        catch (Exception e)
        {
            Safe.Log("Tobacco tab select", e);
        }
    }

    // any other filter button: our tab is no longer the one shown
    [HarmonyPrefix, HarmonyPatch(typeof(Panel_Inventory), nameof(Panel_Inventory.OnFilterChange))]
    static void OtherFilter(UIButton filterButtonClicked)
    {
        if (ui == null || filterButtonClicked == null || filterButtonClicked.Pointer != ui.Pointer) selected = false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Panel_Inventory), nameof(Panel_Inventory.ItemPassesFilter), new[] { typeof(GearItem), typeof(string) })]
    static bool Filter(Panel_Inventory __instance, GearItem pi, string filterName, ref bool __result)
    {
        try
        {
            if (Smoking.IsSmokedPipe(pi)) { __result = false; return false; }
            if (!Settings.SmokesTab.Value) return true;
            if (filterName == FilterName || (selected && __instance.m_FilterName == FilterName))
            {
                __result = InTab(pi);
                return false;
            }
            if (!Ours(pi)) return true;
            bool all = __instance.m_SelectedFilterButton != null && __instance.m_FilterAllButton != null && __instance.m_SelectedFilterButton.Pointer == __instance.m_FilterAllButton.Pointer;
            if (all) return true;
            __result = false;
            return false;
        }
        catch
        {
            return true;
        }
    }

    // 11-13 (user: our tab's button is not lit like the other sections when it is the one open): the game colours only its own
    // filter buttons (m_FilterButtons); ours gets the same colours from the panel: selected, or unselected with or without items
    [HarmonyPostfix, HarmonyPatch(typeof(Panel_Inventory), nameof(Panel_Inventory.RefreshFilterIconColors))]
    static void Colors(Panel_Inventory __instance)
    {
        try
        {
            if (ui == null || button == null || !button) return;
            var p = __instance;
            bool sel = selected && p.m_FilterName == FilterName;
            Color c, h;
            if (sel) { c = p.m_ColorFilterSelected; h = p.m_ColorFilterSelectedHover; }
            else
            {
                bool empty = false;
                try { empty = p.FilterHasNoItems(FilterName); } catch { }
                c = empty ? p.m_ColorFilterUnselectedEmpty : p.m_ColorFilterUnselectedNonempty;
                h = empty ? p.m_ColorFilterUnselectedEmptyHover : p.m_ColorFilterUnselectedNonemptyHover;
            }
            ui.defaultColor = c;
            ui.hover = h;
            ui.pressed = h;
            var target = ui.tweenTarget != null ? ui.tweenTarget : button;
            var w = target.GetComponent<UIWidget>();
            if (w != null) w.color = c;
            if (!colorLogged) { colorLogged = true; Safe.Info($"Tobacco tab colours: selected {sel}, colour {c}, target {target.name}, widget {(w != null)}"); }
        }
        catch (Exception e) { Safe.Log("Tobacco tab colours", e); }
    }
    static bool colorLogged;

    // 11-10: the title over the grid said «Всё» and the empty text was the game's: our tab's name and «no tobacco»
    static void Labels(Panel_Inventory p)
    {
        if (!selected || p.m_FilterName != FilterName) return;
        try
        {
            if (p.m_CategoryLabel != null) p.m_CategoryLabel.text = Texts.Get(FilterName);
            if (p.m_LabelNoItems != null) p.m_LabelNoItems.text = Texts.Get("SMOKES_TAB_EMPTY");
        }
        catch (Exception e) { Safe.Log("Tobacco tab labels", e); }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Panel_Inventory), nameof(Panel_Inventory.UpdateFilteredInventoryList))]
    static void AfterList(Panel_Inventory __instance) => Labels(__instance);

    [HarmonyPostfix, HarmonyPatch(typeof(Panel_Inventory), nameof(Panel_Inventory.RefreshTable))]
    static void AfterTable(Panel_Inventory __instance) => Labels(__instance);
}

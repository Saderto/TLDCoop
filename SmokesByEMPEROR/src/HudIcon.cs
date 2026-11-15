namespace SmokesByEMPEROR;

/// <summary>Bottom right HUD (the equipped item popup with the burn bar): our lit items are torch copies, so the game draws its torch/match
/// picture there. While one of ours is in the hand that sprite is hidden and a picture of the cigarette, cigar or pipe is drawn in its place.</summary>
[HarmonyPatch]
internal static class HudIcon
{
    sealed class Swap { public UISprite S = null!; public UITexture T = null!; }
    static readonly List<Swap> swaps = new();
    static readonly HashSet<string> logged = new();
    static bool on;

    static bool Replace(string? sprite)
    {
        if (string.IsNullOrEmpty(sprite)) return false;
        var s = sprite.ToLowerInvariant();
        return s.Contains("torch") || s.Contains("match") || s.Contains("flare");
    }

    [HarmonyPostfix, HarmonyPatch(typeof(EquipItemPopup), nameof(EquipItemPopup.Update))]
    static void Update(EquipItemPopup __instance)
    {
        try
        {
            var gi = GameManager.GetPlayerManagerComponent()?.m_ItemInHands;
            Def? def = null;
            bool ours = gi != null && Defs.Is(gi, out def) && def.Kind == Kind.Lit;
            if (!ours) { if (on) Off(); menuSeen = false; checkAt = -1f; return; }
            if (Reshow(__instance, gi!)) return;
            var tex = Assets.Texture("icons/hud_" + (def!.IconId ?? def.Id) + ".png", compress: false, mipmaps: false)
                   ?? Assets.Texture(def.Icon, compress: false, mipmaps: false);
            if (tex == null) return;
            foreach (var s in __instance.GetComponentsInChildren<UISprite>(true))
            {
                if (s == null || s == __instance.m_SpriteFillDuration) continue;
                var name = s.spriteName;
                if (logged.Add(s.gameObject.name + ":" + name)) Safe.Info($"HUD equip sprite {s.gameObject.name} = {name} ({s.width}x{s.height})");
                if (!Replace(name)) continue;
                var sw = swaps.Find(x => x.S != null && x.S.Pointer == s.Pointer);
                if (sw == null || sw.T == null || !sw.T)
                {
                    var go = new GameObject("SmokesHudIcon") { layer = s.gameObject.layer };
                    go.transform.SetParent(s.transform, false);
                    var t = go.AddComponent<UITexture>();
                    if (sw == null) swaps.Add(sw = new Swap { S = s });
                    sw.T = t;
                }
                int side = Mathf.Max(s.width, s.height);
                if (sw.T.mainTexture != tex) sw.T.mainTexture = tex;
                if (sw.T.width != side) sw.T.width = side;
                if (sw.T.height != side) sw.T.height = side;
                sw.T.depth = s.depth + 1;
                var c = s.color; sw.T.color = new Color(1f, 1f, 1f, c.a);
                if (!sw.T.gameObject.activeSelf) sw.T.gameObject.SetActive(true);
                if (s.enabled) s.enabled = false;
                on = true;
            }
        }
        catch (Exception e) { Safe.Log("HUD icon", e); }
    }

    static bool menuSeen;
    static float checkAt = -1f;
    static int step;

    /// <summary>11-15 (the owner: after the inventory the white cigarette picture at the bottom right was gone). The smoke stays in the
    /// hand through a menu, and our picture hangs on the game's own sprite that we switch off. After a menu closes the picture is
    /// looked at: if it is not seen, first the game's own «show what is in the hand» is called (what it does on taking an item), and
    /// if that did not bring it back, the game's sprites are given back for one frame and swapped anew. Each step goes to the log
    /// with what the popup looked like. True: nothing more is to be done with the popup on this frame.</summary>
    static bool Reshow(EquipItemPopup popup, GearItem gi)
    {
        bool overlay = false;
        try { overlay = InterfaceManager.IsOverlayActiveImmediate(); } catch { }
        if (overlay) { menuSeen = true; checkAt = -1f; return false; }
        if (menuSeen) { menuSeen = false; step = 0; checkAt = Time.unscaledTime + .6f; }
        if (checkAt < 0f || Time.unscaledTime < checkAt) return false;
        if (Shown())
        {
            if (step > 0) Safe.Info($"HUD: the smoke's picture is back after a menu (step {step}; {State(popup)})");
            checkAt = -1f;
            return false;
        }
        step++;
        string before = State(popup);
        if (step > 2) { Safe.Info($"HUD: the smoke's picture did not come back after a menu ({before})"); checkAt = -1f; return false; }
        Safe.Info($"HUD: the smoke's picture is not seen after a menu, step {step} ({before})");
        checkAt = Time.unscaledTime + 1f;
        try
        {
            if (step == 1) { popup.ShowEquippedItem(gi); return false; }
            Off();
            return true;
        }
        catch (Exception e) { Safe.Log("HUD icon after a menu", e); return false; }
    }

    static bool Shown()
    {
        foreach (var sw in swaps)
            try { if (sw.T != null && sw.T && sw.T.gameObject.activeInHierarchy && sw.T.finalAlpha > .05f) return true; } catch { }
        return false;
    }

    static string State(EquipItemPopup popup)
    {
        try
        {
            var w = popup.m_AmmoWidget;
            string a = w == null ? "ammo widget none" : $"ammo widget active {w.gameObject.activeInHierarchy} alpha {w.alpha:0.##} final {w.finalAlpha:0.##}";
            var pw = popup.m_EquipPopupWidget;
            if (pw != null) a += $", popup widget active {pw.gameObject.activeInHierarchy} alpha {pw.alpha:0.##}";
            foreach (var sw in swaps)
                if (sw.S != null && sw.S && sw.S.gameObject.activeInHierarchy)
                    return a + $", {sw.S.gameObject.name} active (enabled {sw.S.enabled}, alpha {sw.S.alpha:0.##})"
                             + (sw.T != null && sw.T ? $", ours active {sw.T.gameObject.activeInHierarchy} final {sw.T.finalAlpha:0.##}" : ", ours gone");
            return a + ", no torch sprite active";
        }
        catch { return "?"; }
    }

    static void Off()
    {
        on = false;
        foreach (var sw in swaps)
        {
            try
            {
                if (sw.S != null && sw.S) sw.S.enabled = true;
                if (sw.T != null && sw.T) sw.T.gameObject.SetActive(false);
            }
            catch { }
        }
    }
}

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
            if (!ours) { if (on) Off(); return; }
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

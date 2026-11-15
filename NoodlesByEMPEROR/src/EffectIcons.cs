using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace NoodlesByEMPEROR;

/// <summary>Effect icons on the item's panel, where the game shows what a dish gives (backpack, inspect view, cooking panel): the game's own
/// row (DishEffectInfo), filled with our effects, both for the pack and for the cooked bowl (user 11-15: «не текстом, а иконка как у другой еды»).
/// Same way as FoodPack's EffectRow: our picture goes on a UITexture over the game's sprite; the tooltip is opened by us on mouse-over,
/// because the game only opens it for icons it filled itself.</summary>
[HarmonyPatch(typeof(DishEffectInfo), nameof(DishEffectInfo.ShowDishBenefits))]
internal static class EffectIcons
{
    const string TexName = "NoodlesEffectTexture";
    static readonly Dictionary<string, Texture2D?> icons = new();
    static readonly Dictionary<IntPtr, List<DishEffectInfoIcon>> extra = new();
    static readonly Dictionary<IntPtr, float> alphas = new();
    static readonly Dictionary<IntPtr, IntPtr> showing = new();
    static readonly List<DishEffectInfoIcon> filled = new();
    static readonly HashSet<IntPtr> opened = new();
    static readonly Dictionary<IntPtr, (UILabel.Overflow how, int width)> sizing = new();
    static bool logged;

    static Texture2D? Icon(string key)
    {
        if (icons.TryGetValue(key, out var have)) return have;
        Texture2D? tex = null;
        using (var s = typeof(EffectIcons).Assembly.GetManifestResourceStream("NoodlesByEMPEROR.Icons." + key + ".png"))
        {
            if (s != null)
            {
                using var ms = new MemoryStream(); s.CopyTo(ms);
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                ImageConversion.LoadImage(tex, (Il2CppStructArray<byte>)ms.ToArray(), true);
                tex.hideFlags = HideFlags.HideAndDontSave;
            }
        }
        return icons[key] = tex;
    }

    static bool Prefix(DishEffectInfo __instance, GameObject __0, ref bool __result)
    {
        // the same item is shown again every frame: our row stays as it is, so the tooltip's timer is not reset
        if (__0 == null || !showing.TryGetValue(__instance.Pointer, out var item) || item != __0.Pointer) return true;
        __result = true;
        return false;
    }

    static void Postfix(DishEffectInfo __instance, GameObject __0, ref bool __result, bool __runOriginal)
    {
        if (!__runOriginal) return;
        showing.Remove(__instance.Pointer);
        try
        {
            var slots = new List<DishEffectInfoIcon>();
            if (__instance.m_DishEffectIcons != null)
                foreach (var s in __instance.m_DishEffectIcons) if (s != null) slots.Add(s);
            if (extra.TryGetValue(__instance.Pointer, out var more)) slots.AddRange(more.Where(m => m != null));
            if (slots.Count == 0) return;
            // whatever we left on the row for the previous item goes away first
            foreach (var s in slots)
            {
                Release(s); Unfit(s.m_Label);
                var ours = Our(s, false);
                if (ours != null) ours.enabled = false;
                if (s.m_Sprite != null)
                {
                    s.m_Sprite.enabled = true;
                    if (alphas.TryGetValue(s.m_Sprite.Pointer, out var a)) s.m_Sprite.alpha = a;
                }
            }
            if (more != null) foreach (var s in more) if (s != null) s.gameObject.SetActive(false);

            var g = __0 != null ? __0.GetComponent<GearItem>() : null;
            if (g == null || !Defs.Is(g, out var def)) return;
            var fx = Effects.Of(def).Where(f => Icon(f.Key) != null).ToList();
            if (fx.Count == 0) return;
            var free = slots.Where(s => !s.gameObject.activeSelf).ToList();
            foreach (var f in fx)
            {
                DishEffectInfoIcon slot;
                if (free.Count > 0) { slot = free[0]; free.RemoveAt(0); }
                else
                {
                    var o = UnityEngine.Object.Instantiate(slots[0].gameObject, slots[0].transform.parent);
                    o.name = "NoodlesEffect";
                    slot = o.GetComponent<DishEffectInfoIcon>();
                    if (!extra.TryGetValue(__instance.Pointer, out more)) more = extra[__instance.Pointer] = new List<DishEffectInfoIcon>();
                    more.Add(slot);
                }
                Fill(slot, f, slots[0]);
            }
            __instance.m_DishEffectGrid?.Reposition();
            __result = true;
            showing[__instance.Pointer] = __0!.Pointer;
            if (!logged) { logged = true; Safe.Info($"Effect icons: {def.Id} shows {string.Join(", ", fx.Select(f => f.Key))}"); }
        }
        catch (Exception e) { Safe.Log("Effect icons", e); }
    }

    static UITexture? Our(DishEffectInfoIcon slot, bool make)
    {
        var sprite = slot.m_Sprite;
        if (sprite == null) return null;
        var t = sprite.transform.parent != null ? sprite.transform.parent.Find(TexName) : null;
        if (t != null) return t.GetComponent<UITexture>();
        if (!make) return null;
        var o = new GameObject(TexName) { layer = sprite.gameObject.layer };
        o.transform.SetParent(sprite.transform.parent, false);
        o.transform.localPosition = sprite.transform.localPosition;
        o.transform.localRotation = sprite.transform.localRotation;
        o.transform.localScale = sprite.transform.localScale;
        var u = o.AddComponent<UITexture>();
        u.width = sprite.width; u.height = sprite.height; u.depth = sprite.depth + 1;
        return u;
    }

    static void Release(DishEffectInfoIcon s)
    {
        filled.Remove(s);
        if (opened.Remove(s.Pointer) && s.m_Tooltip != null)
        {
            var h = s.m_Tooltip.GetComponent<HoverTooltip>();
            if (h != null && h.m_IsHovering) h.OnHover(false);
        }
    }

    /// <summary>Every frame: the tooltip of the icon under the mouse.</summary>
    public static void Hover()
    {
        if (filled.Count == 0) return;
        try
        {
            filled.RemoveAll(s => s == null);
            var mouse = Input.mousePosition;
            foreach (var s in filled)
            {
                var h = s.m_Tooltip != null ? s.m_Tooltip.GetComponent<HoverTooltip>() : null;
                if (h == null) continue;
                bool over = s.gameObject.activeInHierarchy && Under(s, mouse);
                if (over && !opened.Contains(s.Pointer)) { opened.Add(s.Pointer); if (!h.m_IsHovering) h.OnHover(true); }
                else if (!over && opened.Remove(s.Pointer) && h.m_IsHovering) h.OnHover(false);
            }
        }
        catch (Exception e) { filled.Clear(); Safe.Log("Effect tooltip", e); }
    }

    static bool Under(DishEffectInfoIcon s, Vector3 mouse)
    {
        var w = s.m_Sprite;
        if (w == null) return false;
        var cam = NGUITools.FindCameraForLayer(w.gameObject.layer);
        if (cam == null) return false;
        var c = w.worldCorners;
        var a = cam.WorldToScreenPoint(c[0]); var b = cam.WorldToScreenPoint(c[2]);
        return mouse.x >= Mathf.Min(a.x, b.x) && mouse.x <= Mathf.Max(a.x, b.x) && mouse.y >= Mathf.Min(a.y, b.y) && mouse.y <= Mathf.Max(a.y, b.y);
    }

    static void Fill(DishEffectInfoIcon slot, Effects.Fx f, DishEffectInfoIcon first)
    {
        if (!filled.Contains(slot)) filled.Add(slot);
        bool rus = Texts.Russian;
        try
        {
            var map = new DishEffectInfoUI.DishEffectUIMapping
            {
                m_EffectType = (IFoodEffect.EffectType)0,
                m_SpriteName = slot.m_Sprite != null ? slot.m_Sprite.spriteName : "",
                m_EffectText = new LocalizedString { m_LocalizationID = "NOODLES_FX_" + f.Key },
            };
            slot.ShowDishEffect(map, f.Good ? IFoodEffect.Effect.Benefit : IFoodEffect.Effect.Detriment);
        }
        catch (Exception e) { Safe.Log("Effect icon, the game's way", e); }
        var ours = Our(slot, true);
        if (ours != null)
        {
            ours.mainTexture = Icon(f.Key);
            ours.enabled = true;
            if (slot.m_Sprite != null)
            {
                if (!alphas.ContainsKey(slot.m_Sprite.Pointer)) alphas[slot.m_Sprite.Pointer] = slot.m_Sprite.alpha;
                slot.m_Sprite.alpha = .01f;
            }
        }
        string name = rus ? f.Ru : f.En, desc = rus ? f.RuD : f.EnD;
        var col = f.Good ? first.m_BenefitColor : first.m_DetrimentColor;
        if (ours != null) ours.color = col;
        if (slot.m_Label != null) { slot.m_Label.text = name; slot.m_Label.color = col; Fit(slot.m_Label); }
        if (slot.m_TooltipLabel != null) slot.m_TooltipLabel.text = name + "\n" + char.ToUpper(desc[0]) + desc.Substring(1) + ".";
        Arrows(slot.m_BenefitArrows, f.Good);
        Arrows(slot.m_DetrimentArrows, !f.Good);
        slot.gameObject.SetActive(true);
    }

    static void Fit(UILabel l)
    {
        if (!sizing.ContainsKey(l.Pointer)) sizing[l.Pointer] = (l.overflowMethod, l.width);
        var was = sizing[l.Pointer];
        l.overflowMethod = UILabel.Overflow.ShrinkContent;
        l.width = Math.Min(was.width > 0 && was.how != UILabel.Overflow.ResizeFreely ? was.width : 150, 150);
        l.ProcessText();
    }

    static void Unfit(UILabel? l)
    {
        if (l == null || !sizing.TryGetValue(l.Pointer, out var was)) return;
        l.overflowMethod = was.how; l.width = was.width; sizing.Remove(l.Pointer);
    }

    static void Arrows(Il2CppReferenceArray<GameObject>? arrows, bool on)
    {
        if (arrows == null) return;
        for (int i = 0; i < arrows.Length; i++) if (arrows[i] != null) arrows[i].SetActive(on && i == 0);
    }
}

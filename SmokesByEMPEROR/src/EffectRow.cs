using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SmokesByEMPEROR;

/// <summary>Our effects as icons in the game's own row of dish effects (the item card in the pack, the recipe book, looking at an item),
/// the way FoodPackByEMPEROR and TeaCoffeeByEMPEROR do it (user 11-15: our smokes showed no effect icons in the inventory). The game fills its row first (warmth, alertness...), ours are added after:
/// a free slot of the row or a copy of one, with our picture over the game's sprite, the effect's name, and its description in the tooltip.</summary>
[HarmonyPatch(typeof(DishEffectInfo), nameof(DishEffectInfo.ShowDishBenefits))]
internal static class EffectRow
{
    const string TexName = "SmokesEffectTexture";
    const int MaxLabelWidth = 150;

    static readonly Dictionary<string, Texture2D?> icons = new();
    static readonly Dictionary<IntPtr, List<DishEffectInfoIcon>> extra = new();     // row -> slots we added to it
    static readonly Dictionary<IntPtr, List<DishEffectInfoIcon>> used = new();      // row -> slots we filled last time
    static readonly Dictionary<IntPtr, float> alphas = new();
    static readonly Dictionary<IntPtr, IntPtr> showing = new();                     // row -> the item it shows (ours only)
    static readonly List<DishEffectInfoIcon> filled = new();
    static readonly HashSet<IntPtr> opened = new();
    static readonly Dictionary<IntPtr, (UILabel.Overflow how, int width)> sizing = new();
    static readonly Dictionary<IntPtr, List<UIWidget>> hidden = new();             // slot -> the game's pictures we switched off in it
    static bool logged, noRow;

    public sealed record F(string Key, bool Good);

    /// <summary>What smoking this item gives, as the status lines it will put on: dulled hunger, the blend's bonus, its downside.</summary>
    public static List<F> Of(Def def)
    {
        var list = new List<F>();
        SmokeStats? st = null;
        switch (def.Kind)
        {
            case Kind.Pack: st = Defs.Stats(SmokeType.Cigarette, def); break;
            case Kind.Pouch: st = Defs.Stats(SmokeType.Pipe, def); break;
            case Kind.Tube: case Kind.Cigar: case Kind.CigarCut: case Kind.Box:
                var d = def;
                for (int i = 0; i < 4 && d != null && d.Lit == null && d.Next != null; i++) d = Defs.ById(d.Next);
                var lit = d?.Lit != null ? Defs.ById(d.Lit) : null;
                if (lit != null) st = Defs.Stats(SmokeType.Cigar, lit);
                break;
        }
        if (st == null) return list;
        if (st.HungerMinutes > 0f) list.Add(new F("Hunger", true));
        if (st.Bonus.Length > 0) list.Add(new F(char.ToUpperInvariant(st.Bonus[0]) + st.Bonus.Substring(1), true));
        if (st.Side.Length > 0) list.Add(new F(char.ToUpperInvariant(st.Side[0]) + st.Side.Substring(1), false));
        return list;
    }

    public static Texture2D? Icon(string key)
    {
        if (icons.TryGetValue(key, out var t)) return t;
        Texture2D? tex = null;
        try
        {
            using var s = typeof(EffectRow).Assembly.GetManifestResourceStream("SmokesByEMPEROR.Icons.fx_" + key.ToLowerInvariant() + ".png");
            if (s != null)
            {
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                ImageConversion.LoadImage(tex, (Il2CppStructArray<byte>)ms.ToArray(), true);
                tex.hideFlags = HideFlags.HideAndDontSave;
            }
        }
        catch (Exception e) { Safe.Log("Effect icon " + key, e); }
        return icons[key] = tex;
    }

    /// <summary>The same item shown again: the row stays as it is (rebuilding it every frame keeps resetting the tooltip's timer).</summary>
    static bool Prefix(DishEffectInfo __instance, GameObject __0, ref bool __result)
    {
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
            // what we did to this row last time is undone first
            if (used.TryGetValue(__instance.Pointer, out var was))
            {
                foreach (var s in was)
                {
                    if (s == null) continue;
                    Release(s);
                    Unfit(s.m_Label);
                    var tx = Our(s, false);
                    if (tx != null) tx.enabled = false;
                    if (s.m_Sprite != null && alphas.TryGetValue(s.m_Sprite.Pointer, out var a)) s.m_Sprite.alpha = a;
                    Unhide(s);
                }
                was.Clear();
            }
            if (extra.TryGetValue(__instance.Pointer, out var ours))
                foreach (var s in ours) if (s != null) s.gameObject.SetActive(false);

            var gi = __0 != null ? __0.GetComponent<GearItem>() : null;
            if (gi == null || !Defs.Is(gi, out var def)) return;
            var fx = Of(def).Where(f => Icon(f.Key) != null).ToList();
            if (fx.Count == 0) return;

            var slots = new List<DishEffectInfoIcon>();
            if (__instance.m_DishEffectIcons != null)
                foreach (var s in __instance.m_DishEffectIcons) if (s != null) slots.Add(s);
            if (slots.Count == 0) { if (!noRow) { noRow = true; Safe.Info("Effect row: the game's row has no slots"); } return; }
            var free = slots.Where(s => !s.gameObject.activeSelf).ToList();
            if (ours != null) free.AddRange(ours.Where(s => s != null && !s.gameObject.activeSelf));
            if (!used.TryGetValue(__instance.Pointer, out was)) was = used[__instance.Pointer] = new List<DishEffectInfoIcon>();
            foreach (var f in fx)
            {
                DishEffectInfoIcon slot;
                if (free.Count > 0) { slot = free[0]; free.RemoveAt(0); }
                else
                {
                    var o = UnityEngine.Object.Instantiate(slots[0].gameObject, slots[0].transform.parent);
                    o.name = "SmokesEffect";
                    slot = o.GetComponent<DishEffectInfoIcon>();
                    if (!extra.TryGetValue(__instance.Pointer, out ours)) ours = extra[__instance.Pointer] = new List<DishEffectInfoIcon>();
                    ours.Add(slot);
                }
                Fill(slot, f!, slots[0]);
                was.Add(slot);
            }
            __instance.m_DishEffectGrid?.Reposition();
            __result = true;
            showing[__instance.Pointer] = __0.Pointer;
            if (!logged) { logged = true; Safe.Info($"Effect row: {def.Id} shows {string.Join(", ", fx.Select(f => f!.Key))}"); }
        }
        catch (Exception e) { Safe.Log("Effect icons", e); }
    }

    static UITexture? Our(DishEffectInfoIcon slot, bool make)
    {
        var sprite = slot.m_Sprite;
        if (sprite == null) return null;
        var tr = sprite.transform.parent != null ? sprite.transform.parent.Find(TexName) : null;
        if (tr != null) return tr.GetComponent<UITexture>();
        if (!make) return null;
        var go = new GameObject(TexName) { layer = sprite.gameObject.layer };
        go.transform.SetParent(sprite.transform.parent, false);
        go.transform.localPosition = sprite.transform.localPosition;
        go.transform.localRotation = sprite.transform.localRotation;
        go.transform.localScale = sprite.transform.localScale;
        var t = go.AddComponent<UITexture>();
        t.width = sprite.width; t.height = sprite.height; t.depth = sprite.depth + 1;
        return t;
    }

    static void Fill(DishEffectInfoIcon slot, F f, DishEffectInfoIcon first)
    {
        if (!filled.Contains(slot)) filled.Add(slot);
        try
        {
            var map = new DishEffectInfoUI.DishEffectUIMapping
            {
                m_EffectType = default,
                m_SpriteName = slot.m_Sprite != null ? slot.m_Sprite.spriteName : "",
                m_EffectText = new LocalizedString { m_LocalizationID = "SMOKES_FX_" + f.Key + "_DESC" },
            };
            slot.ShowDishEffect(map, (IFoodEffect.Effect)(f.Good ? 1 : -1));
        }
        catch (Exception e) { Safe.Log("Effect icon, the game's way", e); }
        var tx = Our(slot, true);
        if (tx != null)
        {
            tx.mainTexture = Icon(f.Key);
            tx.enabled = true;
            if (slot.m_Sprite != null)
            {
                if (!alphas.ContainsKey(slot.m_Sprite.Pointer)) alphas[slot.m_Sprite.Pointer] = slot.m_Sprite.alpha;
                slot.m_Sprite.alpha = .01f;
            }
        }
        string name = Texts.Get("SMOKES_FX_" + f.Key + "_NAME");
        string desc = Texts.Get("SMOKES_FX_" + f.Key + "_DESC");
        var col = f.Good ? first.m_BenefitColor : first.m_DetrimentColor;
        if (tx != null) tx.color = col;
        if (slot.m_Label != null) { slot.m_Label.text = name; slot.m_Label.color = col; Fit(slot.m_Label); }
        if (slot.m_TooltipLabel != null) slot.m_TooltipLabel.text = name + "\n" + desc;
        Arrows(slot.m_BenefitArrows, f.Good);
        Arrows(slot.m_DetrimentArrows, !f.Good);
        slot.gameObject.SetActive(true);
        if (tx != null) Hide(slot, tx);
    }

    /// <summary>The game's own pictures of the slot (its sprite and whatever lies under it) are switched off while ours is shown:
    /// a faint alpha did not hold, the game's heart showed under our picture (user 11-15). Arrows and the tooltip stay.</summary>
    static void Hide(DishEffectInfoIcon slot, UITexture ours)
    {
        if (!hidden.TryGetValue(slot.Pointer, out var list)) list = hidden[slot.Pointer] = new List<UIWidget>();
        foreach (var w in slot.GetComponentsInChildren<UIWidget>(true))
        {
            if (w == null || w.Pointer == ours.Pointer) continue;
            bool picture = w.TryCast<UISprite>() != null || w.TryCast<UITexture>() != null || w.TryCast<UI2DSprite>() != null;
            // only the game's icon goes: its dark square under the icon (EffectIconBG) stays as the back of ours (user 11-15: «нет заднего фона»)
            bool keep = !picture || slot.m_Sprite == null || w.Pointer != slot.m_Sprite.Pointer || Inside(w.transform, slot.m_Tooltip) || Inside(w.transform, slot.m_BenefitArrows) || Inside(w.transform, slot.m_DetrimentArrows);
            if (keep || !w.enabled) continue;
            w.enabled = false;
            if (!list.Any(x => x != null && x.Pointer == w.Pointer)) list.Add(w);
        }
    }

    static void Unhide(DishEffectInfoIcon slot)
    {
        if (!hidden.TryGetValue(slot.Pointer, out var list)) return;
        foreach (var w in list) if (w != null) w.enabled = true;
        hidden.Remove(slot.Pointer);
    }

    static bool Inside(Transform t, GameObject? root) => root != null && (t == root.transform || t.IsChildOf(root.transform));

    static bool Inside(Transform t, Il2CppReferenceArray<GameObject>? roots)
    {
        if (roots == null) return false;
        for (int i = 0; i < roots.Length; i++) if (Inside(t, roots[i])) return true;
        return false;
    }

    static string Path(Transform t, Transform root)
    {
        string s = t.name;
        for (var p = t.parent; p != null && t != root; p = p.parent) { s = p.name + "/" + s; if (p == root) break; }
        return s;
    }

    static void Fit(UILabel l)
    {
        if (!sizing.ContainsKey(l.Pointer)) sizing[l.Pointer] = (l.overflowMethod, l.width);
        var (how, width) = sizing[l.Pointer];
        l.overflowMethod = UILabel.Overflow.ShrinkContent;
        l.width = Math.Min(width > 0 && how != UILabel.Overflow.ResizeFreely ? width : MaxLabelWidth, MaxLabelWidth);
        l.ProcessText();
    }

    static void Unfit(UILabel? l)
    {
        if (l == null || !sizing.TryGetValue(l.Pointer, out var s)) return;
        l.overflowMethod = s.how; l.width = s.width;
        sizing.Remove(l.Pointer);
    }

    static void Arrows(Il2CppReferenceArray<GameObject>? arrows, bool on)
    {
        if (arrows == null) return;
        for (int i = 0; i < arrows.Length; i++)
            if (arrows[i] != null) arrows[i].SetActive(on && i == 0);
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

    /// <summary>The game opens a tooltip only for its own filling of a slot: for ours the mouse is watched here.</summary>
    public static void Hover()
    {
        if (filled.Count == 0) return;
        try
        {
            filled.RemoveAll(s => s == null);
            var mouse = Input.mousePosition;
            foreach (var s in filled)
            {
                if (hidden.TryGetValue(s.Pointer, out var off))
                    foreach (var w in off) if (w != null && w.enabled) w.enabled = false;
                var h = s.m_Tooltip != null ? s.m_Tooltip.GetComponent<HoverTooltip>() : null;
                if (h == null) continue;
                bool over = s.gameObject.activeInHierarchy && Under(s, mouse);
                if (over && !opened.Contains(s.Pointer))
                {
                    opened.Add(s.Pointer);
                    if (!h.m_IsHovering) h.OnHover(true);
                }
                else if (!over && opened.Remove(s.Pointer) && h.m_IsHovering) h.OnHover(false);
            }
        }
        catch (Exception e) { filled.Clear(); Safe.Log("Effect tooltip", e); }
    }

    static bool Under(DishEffectInfoIcon s, Vector3 mouse)
    {
        UIWidget? w = s.m_Sprite;
        if (w == null) return false;
        var cam = NGUITools.FindCameraForLayer(w.gameObject.layer);
        if (cam == null) return false;
        var c = w.worldCorners;
        var a = cam.WorldToScreenPoint(c[0]); var b = cam.WorldToScreenPoint(c[2]);
        return mouse.x >= Mathf.Min(a.x, b.x) && mouse.x <= Mathf.Max(a.x, b.x) && mouse.y >= Mathf.Min(a.y, b.y) && mouse.y <= Mathf.Max(a.y, b.y);
    }
}

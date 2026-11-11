namespace SmokesBySeagull;

/// <summary>11-10: «Зажигалки» / «Lighters» in the journal's Collections: a row of its own with «N / 29» and a hand-drawn lighter
/// (row icon and the big picture, like the game's own collections), and on «Select» a page of its own inside the journal: the collections
/// list is hidden the way the game hides it for its own sub-pages, the journal frame, tabs and «Back» stay, and our grid is drawn in the
/// list's place (IMGUI: Label/DrawTexture, mouse from Input). Every design in collection order: a found one with its picture and name,
/// one not found yet as a faint drawn shape and «???». «Back» (and Esc, which calls it) returns to the list.
/// 0.1.113: the row is built like «Памятки» (Mementos), not like the last row («Осмотренные места», a map page), and the game's own
/// list controls are off while our page is open.</summary>
[HarmonyPatch]
internal static class Journal
{
    const string NameId = "SMOKES_COLL_LIGHTERS";
    const string BigArt = "icons/coll_lighter_big.png", RowArt = "icons/coll_lighter_icon.png", ShapeArt = "icons/coll_lighter_shape.png";
    static bool open;
    static int closedFrame = -10;
    static int selected;
    static Texture? savedImage;
    static Texture? shownImage;
    static bool loggedList, loggedLabels;

    // what we hid / changed when our page opened, put back on close
    static GameObject? hidSub, hidSelect;
    static UILabel? header;
    static string? headerWas;
    static bool drawHeader;

    static bool IsOurs(CollectionListItemInfo? i) => i != null && i.m_NameLocID == NameId;

    static CollectionListItemInfo? Selected(Panel_Log p)
    {
        try
        {
            var list = p.m_CollectionList;
            int i = p.m_CollectionListSelectedIndex;
            if (list == null || i < 0 || i >= list.Count || list[i] == null) return null;
            return list[i].GetItemInfo() ?? list[i].m_ItemInfo;
        }
        catch { return null; }
    }

    static CollectionListItemInfo Info(Il2CppSystem.Collections.Generic.List<CollectionListItemInfo>? from)
    {
        CollectionListItemInfo? like = null;
        if (from != null)
            foreach (var x in from)
                if (x != null && !IsOurs(x) && x.m_CollectionType == Panel_Log.CollectionsType.Mementos) { like = x; break; }
        return new CollectionListItemInfo
        {
            m_NameLocID = NameId,
            m_DescLocID = NameId + "_DESC",
            m_ListIconName = like?.m_ListIconName ?? "ico_collections_mementos",
            m_BigIconName = like?.m_BigIconName ?? "Collections_large_mementos",
            m_ProgressString = $"{Lighters.Found} / {Lighters.Total}",
            m_SubScreenToOpen = like?.m_SubScreenToOpen ?? Panel_Log.WhatIKnowType.SelectScreen,
            m_CollectionType = Panel_Log.CollectionsType.Mementos,
        };
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Panel_Log), nameof(Panel_Log.GenerateCollectionSelectScreenDummyData))]
    static void Data(Il2CppSystem.Collections.Generic.List<CollectionListItemInfo> dummyDataList)
    {
        try
        {
            if (dummyDataList == null) return;
            Lighters.SyncFromPack();
            for (int i = dummyDataList.Count - 1; i >= 0; i--)
                if (IsOurs(dummyDataList[i])) dummyDataList.RemoveAt(i);
            dummyDataList.Add(Info(dummyDataList));
            if (!loggedList)
            {
                loggedList = true;
                var names = new List<string>();
                foreach (var x in dummyDataList) if (x != null) names.Add($"{x.m_NameLocID} [{x.m_CollectionType}] icon {x.m_ListIconName} big {x.m_BigIconName} {x.m_ProgressString}");
                Safe.Info("Journal collections: " + string.Join("; ", names));
            }
        }
        catch (Exception e) { Safe.Log("Journal row", e); }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Panel_Log), nameof(Panel_Log.BuildCollectionsList))]
    static void Built(Panel_Log __instance)
    {
        try
        {
            bool found = false;
            var list = __instance.m_CollectionList;
            if (list != null)
                foreach (var it in list)
                    if (it != null && IsOurs(it.GetItemInfo() ?? it.m_ItemInfo)) { found = true; break; }
            Safe.Info($"Journal: collection list built, {list?.Count ?? 0} rows, lighters row {(found ? "shown" : "missing, adding it")}");
            if (!found)
            {
                // the list did not come from that data: one more entry the way the game adds its own
                var data = __instance.m_CollectionDataList;
                var info = Info(data);
                if (data != null)
                {
                    bool inData = false;
                    foreach (var x in data) if (IsOurs(x)) { inData = true; break; }
                    if (!inData) data.Add(info);
                }
                int at = list?.Count ?? 0;
                __instance.AddEntryToList(at, info);
                Safe.Info($"Journal: lighters row added at {at}, now {__instance.m_CollectionList?.Count ?? 0} rows");
            }
            RowIcon(__instance);
        }
        catch (Exception e) { Safe.Log("Journal list", e); }
    }

    // ---- the row's icon: our drawn lighter on top of the row's sprite (which gets hidden), following its colour/alpha
    static UISprite? rowSprite;
    static UITexture? rowTex;

    static void RowIcon(Panel_Log p)
    {
        try
        {
            var list = p.m_CollectionList;
            if (list == null) return;
            UISprite? s = null;
            // 11-10 user: «Записки» and «Особые рецепты» showed our lighter: rows are reused, our overlay stayed on their sprites.
            // Every row that is not ours loses our overlay and gets its own sprite back.
            foreach (var it in list)
            {
                if (it == null || it.m_CollectionIcon == null) continue;
                var sp = it.m_CollectionIcon;
                if (IsOurs(it.GetItemInfo() ?? it.m_ItemInfo)) { s ??= sp; continue; }
                var old = sp.transform.Find("SmokesCollIcon");
                if (old != null)
                {
                    if (rowTex != null && rowTex && old.gameObject == rowTex.gameObject) rowTex = null;
                    UnityEngine.Object.Destroy(old.gameObject);
                    sp.enabled = true;
                }
            }
            if (s == null) return;
            var tex = Assets.Texture(RowArt, compress: false, mipmaps: false);
            if (tex == null) return;
            if (rowSprite == null || !rowSprite || rowSprite.Pointer != s.Pointer || rowTex == null || !rowTex)
            {
                rowSprite = s;
                var had = s.transform.Find("SmokesCollIcon");
                if (had != null) UnityEngine.Object.Destroy(had.gameObject);
                var go = new GameObject("SmokesCollIcon") { layer = s.gameObject.layer };
                go.transform.SetParent(s.transform, false);
                rowTex = go.AddComponent<UITexture>();
                rowTex.mainTexture = tex;
            }
            int side = Mathf.Max(s.width, s.height);
            if (rowTex.width != side) rowTex.width = side;
            if (rowTex.height != side) rowTex.height = side;
            rowTex.depth = s.depth + 1;
            rowTex.color = s.color;
            if (s.enabled) s.enabled = false;
        }
        catch (Exception e) { Safe.Log("Journal row icon", e); }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Panel_Log), nameof(Panel_Log.UpdateCollectionsPage))]
    static void Page(Panel_Log __instance)
    {
        try
        {
            var img = __instance.m_CollectionsImage;
            if (IsOurs(Selected(__instance)))
            {
                if (__instance.m_CollectionsNameLabel != null) __instance.m_CollectionsNameLabel.text = Texts.Get(NameId);
                if (__instance.m_CollectionsDescLabel != null) __instance.m_CollectionsDescLabel.text = Texts.Get("SMOKES_COLL_COUNT", Lighters.Found, Lighters.Total);
                if (img != null)
                {
                    var t = Assets.Texture(BigArt, compress: false);
                    if (t != null && img.mainTexture != t)
                    {
                        savedImage = img.mainTexture;
                        img.mainTexture = t;
                        shownImage = t;
                    }
                }
            }
            else if (img != null && shownImage != null && img.mainTexture == shownImage)
            {
                img.mainTexture = savedImage;
                shownImage = null;
            }
            RowIcon(__instance);
        }
        catch (Exception e) { Safe.Log("Journal page", e); }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Panel_Log), nameof(Panel_Log.OnCollectionsSubScreen))]
    static bool Select(Panel_Log __instance)
    {
        if (open) return false;
        try
        {
            if (!IsOurs(Selected(__instance))) return true;
            Lighters.SyncFromPack();
            Open(__instance);
            return false;
        }
        catch (Exception e) { Safe.Log("Journal select", e); return false; }
    }

    // the game's list keys/mouse must not move the hidden list under our page
    [HarmonyPrefix, HarmonyPatch(typeof(Panel_Log), nameof(Panel_Log.DoControlsCollectionList))]
    static bool Controls() => !open;

    [HarmonyPrefix, HarmonyPatch(typeof(Panel_Log), nameof(Panel_Log.OnBack))]
    static bool Back(Panel_Log __instance)
    {
        if (open) { Close(__instance); return false; }
        return Time.frameCount - closedFrame > 2;
    }

    static bool Under(Transform? t, GameObject? root)
    {
        if (root == null) return false;
        for (; t != null; t = t.parent) if (t.gameObject.Pointer == root.Pointer) return true;
        return false;
    }

    static void Open(Panel_Log p)
    {
        selected = -1;
        header = null; headerWas = null; drawHeader = true;
        var sub = p.m_CollectionsSubSectionObject;
        // the page title «Коллекции»: if it lives outside the list we hide, we just rename it
        try
        {
            string ru = "КОЛЛЕКЦИИ", en = "COLLECTIONS";
            var lines = new List<string>();
            foreach (var l in p.GetComponentsInChildren<UILabel>(false))
            {
                if (l == null) continue;
                string t = (l.text ?? "").Trim();
                if (!loggedLabels && t.Length > 0 && t.Length < 40) lines.Add($"{l.gameObject.name}='{t}'{(Under(l.transform, sub) ? " (list)" : "")}");
                string u = t.ToUpperInvariant();
                if (header == null && (u == ru || u == en)) header = l;
            }
            if (!loggedLabels) { loggedLabels = true; Safe.Info("Journal labels: " + string.Join("; ", lines)); }
            if (header != null && !Under(header.transform, sub))
            {
                headerWas = header.text;
                header.text = Texts.Get(NameId);
                drawHeader = false;
            }
            else header = null;
        }
        catch (Exception e) { Safe.Log("Journal header", e); header = null; }
        try
        {
            if (sub != null && sub.activeSelf) { sub.SetActive(false); hidSub = sub; }
            var btn = p.m_CollectionSelectSubScreenButton;
            if (btn != null && btn.activeSelf) { btn.SetActive(false); hidSelect = btn; }
        }
        catch (Exception e) { Safe.Log("Journal hide list", e); }
        open = true;
        Safe.Info($"Journal: lighters page opened, {Lighters.Found} of {Lighters.Total}, list hidden {hidSub != null}, title {(drawHeader ? "drawn" : "renamed")}");
    }

    static void Close(Panel_Log? p)
    {
        open = false;
        closedFrame = Time.frameCount;
        try
        {
            if (hidSub != null && hidSub) hidSub.SetActive(true);
            if (hidSelect != null && hidSelect) hidSelect.SetActive(true);
            if (header != null && header && headerWas != null) header.text = headerWas;
        }
        catch (Exception e) { Safe.Log("Journal close", e); }
        hidSub = hidSelect = null; header = null; headerWas = null;
    }

    // ---- our page

    static Vector2 mouse;
    static bool click;
    static Font? font;
    static bool fontTried;
    static float nextRowCheck;

    public static void Tick()
    {
        try { if (rowTex != null && rowTex && rowSprite != null && rowSprite) rowTex.color = rowSprite.color; } catch { }
        // rows can be refilled without our hooks: re-check which row carries our icon twice a second while the journal is up
        if (rowSprite != null && Time.unscaledTime >= nextRowCheck)
        {
            nextRowCheck = Time.unscaledTime + .5f;
            try { var lp = InterfaceManager.GetPanel<Panel_Log>(); if (lp != null && lp.gameObject.activeInHierarchy) RowIcon(lp); } catch { }
        }
        if (!open) return;
        Panel_Log? p = null;
        try
        {
            p = InterfaceManager.GetPanel<Panel_Log>();
            // the journal closed or went to another page: put the list back for next time
            if (p == null || !p.gameObject.activeInHierarchy || (hidSub != null && hidSub && hidSub.transform.parent != null && !hidSub.transform.parent.gameObject.activeInHierarchy))
            { Close(p); return; }
        }
        catch { Close(p); return; }
        var mp = Input.mousePosition;
        mouse = new Vector2(mp.x, Screen.height - mp.y);
        click = Input.GetMouseButtonDown(0);
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Backspace)) Close(p);
    }

    static Font? PickFont()
    {
        if (fontTried) return font;
        fontTried = true;
        try
        {
            string want = Texts.Russian ? "Nazhdak" : "Creatinin";
            foreach (var f in Resources.FindObjectsOfTypeAll<Font>())
                if (f != null && f.name.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0) { font = f; break; }
            Safe.Info("Journal font: " + (font != null ? font.name : "default"));
        }
        catch (Exception e) { Safe.Log("Journal font", e); }
        return font;
    }

    static GUIStyle Style(float size, TextAnchor a, Color c, bool wrap = false)
    {
        var s = new GUIStyle(GUI.skin.label) { alignment = a, fontSize = Mathf.Max(8, Mathf.RoundToInt(size)), wordWrap = wrap, clipping = TextClipping.Overflow };
        s.normal.textColor = c;
        var f = PickFont();
        if (f != null) s.font = f;
        return s;
    }

    static void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    static void Frame(Rect r, Color c, float w)
    {
        Fill(new Rect(r.x, r.y, r.width, w), c);
        Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
        Fill(new Rect(r.x, r.y, w, r.height), c);
        Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
    }

    static void Picture(Rect r, Texture? t, Color tint)
    {
        if (t == null) return;
        var old = GUI.color;
        GUI.color = tint;
        GUI.DrawTexture(r, t, ScaleMode.ScaleToFit, true);
        GUI.color = old;
    }

    static readonly Color Teal = new(.43f, .51f, .49f, 1f);
    static readonly Color Body = new(.78f, .78f, .78f, 1f);

    public static void Draw()
    {
        if (!open) return;
        try
        {
            float K = Screen.height / 1080f;
            float ox = (Screen.width - 1920f * K) / 2f;
            Rect R(float x, float y, float w, float h) => new(ox + x * K, y * K, w * K, h * K);
            var white = new Color(1, 1, 1, .92f);

            if (drawHeader) GUI.Label(R(280, 154, 800, 40), Texts.Get(NameId).ToUpperInvariant(), Style(25 * K, TextAnchor.MiddleLeft, white));
            GUI.Label(R(280, 200, 790, 34), Texts.Get("SMOKES_COLL_COUNT", Lighters.Found, Lighters.Total).ToUpperInvariant(), Style(21 * K, TextAnchor.MiddleLeft, Teal));

            const int cols = 6;
            const float cell = 122f, gap = 11f, gx = 274f, gy = 246f;
            int hover = -1;
            var all = Lighters.All;
            var shape = Assets.Texture(ShapeArt, compress: false);
            for (int i = 0; i < all.Length; i++)
            {
                var r = R(gx + (i % cols) * (cell + gap), gy + (i / cols) * (cell + gap), cell, cell);
                bool has = Lighters.Has(all[i].Id);
                bool over = r.Contains(mouse);
                if (over) hover = i;
                if (over && click) { selected = i; click = false; }
                Fill(r, i == selected ? new Color(1, 1, 1, .16f) : over ? new Color(1, 1, 1, .09f) : new Color(0, 0, 0, .28f));
                if (i == selected) Frame(r, new Color(1, 1, 1, .45f), Mathf.Max(1f, K));
                var inner = new Rect(r.x + 10 * K, r.y + 10 * K, r.width - 20 * K, r.height - 20 * K);
                if (has)
                {
                    var def = Defs.ById(all[i].Id);
                    Picture(inner, def != null ? Assets.Texture(def.Icon, compress: false) : null, Color.white);
                }
                else
                {
                    Picture(new Rect(r.x + 26 * K, r.y + 20 * K, r.width - 52 * K, r.height - 40 * K), shape, new Color(1, 1, 1, .13f));
                    GUI.Label(r, Texts.Get("SMOKES_COLL_UNKNOWN"), Style(26 * K, TextAnchor.MiddleCenter, new Color(1, 1, 1, .42f)));
                }
            }

            int show = hover >= 0 ? hover : selected;
            if (show >= 0 && show < all.Length)
            {
                var d = all[show];
                bool has = Lighters.Has(d.Id);
                var def = Defs.ById(d.Id);
                string name = has ? Texts.Get("SMOKES_" + d.Id + "_NAME") : Texts.Get("SMOKES_COLL_UNKNOWN");
                string desc = has ? Texts.Get("SMOKES_" + d.Id + "_DESC") : Texts.Get("SMOKES_COLL_UNKNOWN_DESC");
                GUI.Label(R(1170, 150, 580, 50), name.ToUpperInvariant(), Style(25 * K, TextAnchor.MiddleCenter, white, true));
                if (has) Picture(R(1280, 250, 360, 360), def != null ? Assets.Texture(def.Icon, compress: false) : null, Color.white);
                else Picture(R(1330, 280, 260, 300), Assets.Texture(BigArt, compress: false), new Color(1, 1, 1, .22f));
                GUI.Label(R(1170, 640, 580, 240), desc, Style(20 * K, TextAnchor.UpperCenter, Body, true));
                GUI.Label(R(1170, 900, 580, 34), $"{show + 1} / {all.Length}", Style(19 * K, TextAnchor.MiddleCenter, Teal));
            }
            else
            {
                GUI.Label(R(1170, 150, 580, 50), Texts.Get(NameId).ToUpperInvariant(), Style(25 * K, TextAnchor.MiddleCenter, white));
                GUI.Label(R(1170, 196, 580, 60), Texts.Get(NameId + "_DESC"), Style(19 * K, TextAnchor.UpperCenter, Body, true));
                Picture(R(1290, 290, 340, 340), Assets.Texture(BigArt, compress: false), Color.white);
            }
        }
        catch (Exception e) { Safe.Log("Journal page draw", e); Close(InterfaceManager.GetPanel<Panel_Log>()); }
    }
}

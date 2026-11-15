namespace MatchboxesByEMPEROR;

/// <summary>«Спичечные этикетки» / «Matchbox labels» in the journal's Collections (built like SmokesByEMPEROR's «Зажигалки» row,
/// both live side by side): the row with «N / 120», and on «Select» a page of our own: series on the left, that series' ten labels
/// in the middle (found ones printed, missing ones «???»), the label under the mouse large on the right.</summary>
[HarmonyPatch]
internal static class Journal
{
    const string NameId = "MATCHBOX_COLL";
    const string BigArt = "icons/coll_big.png", RowArt = "icons/coll_icon.png", ShapeArt = "icons/coll_shape.png";
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
            m_ProgressString = $"{Collection.Found} / {Defs.All.Count}",
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
            Collection.SyncFromPack();
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
            Safe.Info($"Journal: collection list built, {list?.Count ?? 0} rows, labels row {(found ? "shown" : "missing, adding it")}");
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
                Safe.Info($"Journal: labels row added at {at}, now {__instance.m_CollectionList?.Count ?? 0} rows");
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
                var old = sp.transform.Find("MatchboxCollIcon");
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
                var had = s.transform.Find("MatchboxCollIcon");
                if (had != null) UnityEngine.Object.Destroy(had.gameObject);
                var go = new GameObject("MatchboxCollIcon") { layer = s.gameObject.layer };
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
                if (__instance.m_CollectionsDescLabel != null) __instance.m_CollectionsDescLabel.text = Texts.Get("COLL_COUNT", Collection.Found, Defs.All.Count);
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
            Collection.SyncFromPack();
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
        Safe.Info($"Journal: labels page opened, {Collection.Found} of {Defs.All.Count}, list hidden {hidSub != null}, title {(drawHeader ? "drawn" : "renamed")}");
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

    static int series;

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
            GUI.Label(R(280, 200, 790, 34), Texts.Get("COLL_COUNT", Collection.Found, Defs.All.Count).ToUpperInvariant(), Style(21 * K, TextAnchor.MiddleLeft, Teal));

            // series on the left
            var all = Catalog.Series;
            for (int i = 0; i < all.Length; i++)
            {
                var r = R(274, 246 + i * 58, 330, 52);
                bool over = r.Contains(mouse);
                if (over && click) { series = i; selected = -1; click = false; }
                Fill(r, i == series ? new Color(1, 1, 1, .16f) : over ? new Color(1, 1, 1, .09f) : new Color(0, 0, 0, .28f));
                if (i == series) Frame(r, new Color(1, 1, 1, .45f), Mathf.Max(1f, K));
                int n = Collection.FoundIn(all[i]);
                GUI.Label(R(288, 246 + i * 58, 250, 52), Texts.Get("SERIES_" + all[i].Id), Style(18 * K, TextAnchor.MiddleLeft, n == all[i].Count ? new Color(.95f, .85f, .55f, 1f) : white, true));
                GUI.Label(R(540, 246 + i * 58, 56, 52), $"{n}/{all[i].Count}", Style(17 * K, TextAnchor.MiddleRight, Teal));
            }

            // that series' labels in the middle: two columns of five
            var s = all[Mathf.Clamp(series, 0, all.Length - 1)];
            var labels = Defs.All.Where(d => d.Series.Id == s.Id).ToList();
            int hover = -1;
            var shape = Assets.Texture(ShapeArt, compress: false);
            const float cw = 250f, chh = 136f, gap = 10f, gx = 622f, gy = 246f;
            for (int i = 0; i < labels.Count; i++)
            {
                var r = R(gx + (i % 2) * (cw + gap), gy + (i / 2) * (chh + gap), cw, chh);
                bool has = Collection.Has(labels[i].Id);
                bool over = r.Contains(mouse);
                if (over) hover = i;
                if (over && click) { selected = i; click = false; }
                Fill(r, i == selected ? new Color(1, 1, 1, .16f) : over ? new Color(1, 1, 1, .09f) : new Color(0, 0, 0, .28f));
                if (i == selected) Frame(r, new Color(1, 1, 1, .45f), Mathf.Max(1f, K));
                var inner = new Rect(r.x + 8 * K, r.y + 8 * K, r.width - 16 * K, r.height - 16 * K);
                if (has) Picture(inner, Assets.Texture(labels[i].Flat, compress: false), Color.white);
                else
                {
                    Picture(inner, shape, new Color(1, 1, 1, .1f));
                    GUI.Label(r, Texts.Get("COLL_UNKNOWN"), Style(26 * K, TextAnchor.MiddleCenter, new Color(1, 1, 1, .42f)));
                }
            }

            int show = hover >= 0 ? hover : selected;
            if (show >= 0 && show < labels.Count)
            {
                var d = labels[show];
                bool has = Collection.Has(d.Id);
                string name = has ? Texts.Get("LABEL_" + d.Id) : Texts.Get("COLL_UNKNOWN");
                GUI.Label(R(1170, 150, 580, 50), name.ToUpperInvariant(), Style(25 * K, TextAnchor.MiddleCenter, white, true));
                if (has)
                {
                    Picture(R(1190, 220, 540, 400), Assets.Texture(d.Flat, compress: false), Color.white);
                    Picture(R(1385, 630, 150, 150), Assets.Texture(d.Icon, compress: false), Color.white);
                    GUI.Label(R(1170, 790, 580, 34), Texts.Get("KIND_" + d.Kind.Id), Style(20 * K, TextAnchor.MiddleCenter, Body));
                }
                else
                {
                    Picture(R(1330, 300, 260, 260), shape, new Color(1, 1, 1, .2f));
                    GUI.Label(R(1170, 640, 580, 160), Texts.Get("COLL_UNKNOWN_DESC"), Style(20 * K, TextAnchor.UpperCenter, Body, true));
                }
                GUI.Label(R(1170, 900, 580, 34), Texts.Get("COLL_WHERE", Texts.Get("SERIES_" + s.Id), d.Label.N, s.Count), Style(19 * K, TextAnchor.MiddleCenter, Teal));
            }
            else
            {
                GUI.Label(R(1170, 150, 580, 50), Texts.Get(NameId).ToUpperInvariant(), Style(25 * K, TextAnchor.MiddleCenter, white));
                GUI.Label(R(1170, 196, 580, 80), Texts.Get("COLL_DESC"), Style(19 * K, TextAnchor.UpperCenter, Body, true));
                Picture(R(1290, 300, 340, 340), Assets.Texture(BigArt, compress: false), Color.white);
                GUI.Label(R(1170, 680, 580, 40), Texts.Get("COLL_SERIES", Texts.Get("SERIES_" + s.Id), Collection.FoundIn(s), s.Count), Style(20 * K, TextAnchor.MiddleCenter, Teal, true));
            }
        }
        catch (Exception e) { Safe.Log("Journal page draw", e); Close(InterfaceManager.GetPanel<Panel_Log>()); }
    }
}

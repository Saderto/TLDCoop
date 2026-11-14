namespace SmokesByEMPEROR;

/// <summary>Where smokes turn up, in place of part of what the game itself draws (nothing on top):
/// containers: a share of the game's matches and snacks rolled into a container becomes one of our items;
/// lying in a scene: a share of the matches and snacks that survive their spawn roll on the first visit is swapped for one of ours at the same spot.
/// Every roll comes from the world seed and the place (FNV), so coop players and reloads get the same; the world itself (its save) keeps
/// what was placed, so nothing is placed twice and a new game in the same slot rolls anew.</summary>
[HarmonyPatch]
internal static class Loot
{
    // vanilla item -> share of its draws that become ours (container, world)
    static readonly Dictionary<string, (float box, float world)> Sources = new()
    {
        // 11-09 user: replacing made matches rarer: containers only get smokes added now (Extra), nothing taken away
        ["GEAR_PackMatches"] = (0f, .15f),
        ["GEAR_CandyBar"] = (0f, .05f),
        ["GEAR_GranolaBar"] = (0f, .05f),
        ["GEAR_EnergyBar"] = (0f, .05f),
        ["GEAR_BeefJerky"] = (0f, .04f),
        ["GEAR_Soda"] = (0f, .04f),
        ["GEAR_SodaGrape"] = (0f, .04f),
        ["GEAR_SodaOrange"] = (0f, .04f),
    };

    static List<(string id, float w)>? table;
    static float total;

    /// <summary>What can be found and how often: packs most, then cigars, tobacco, pipes, the lighter and the cutter; boxes and rare packs seldom.</summary>
    const Kind Cgl = (Kind)100;   // the cigarillos' own share of the finds (they are packs otherwise)
    // 11-14 (user: "cigarettes a third less often"): a third of the packs' share of the finds is now nothing at all (Nothing),
    // so cigarettes turn up a third less often and everything else exactly as before
    const float PacksLess = 1f / 3f;
    const string Nothing = "";
    static void Build()
    {
        table = new();
        table.Add((Nothing, 50f * PacksLess));
        total += 50f * PacksLess;
        // base weight of one item inside its kind; each kind then gets a fixed share of all finds, so more brands
        // spread the same share thinner instead of crowding out the lighter or the cutter (pipes: fixed places only)
        var share = new Dictionary<Kind, float>
        {
            [Kind.Pack] = 50f * (1f - PacksLess), [Cgl] = 10f, [Kind.Tube] = 11.9f,   // 11-13: cigarillos take 10 of the packs' 60   // 11-09: every cigar found is in its tube (bare cigars only come out of tubes and boxes)
            [Kind.Box] = 2.04f, [Kind.Pouch] = 8f,
            [Kind.Lighter] = 7f, [Kind.Cutter] = 3f,   // 11-10: 29 collectible lighters (rarer designs weigh less)
            [Kind.Refill] = 4f,
        };
        var raw = new List<(string id, Kind k, float w)>();
        var sum = new Dictionary<Kind, float>();
        foreach (var d in Defs.All)
        {
            if (!share.ContainsKey(d.Kind)) continue;
            var kind = d.Cigarillo ? Cgl : d.Kind;
            float w = d.Kind == Kind.Pack ? (d.CigTexture != null ? (d.Rare ? .3f : 1f) : 0f) : d.Kind == Kind.Lighter ? (d.Rarity >= 3 ? .25f : d.Rarity == 2 ? .55f : 1f) : 1f;
            if (w <= 0f) continue;
            raw.Add((d.Id, kind, w));
            sum[kind] = (sum.TryGetValue(kind, out var t) ? t : 0f) + w;
        }
        foreach (var (id, k, w) in raw)
        {
            float x = w * share[k] / sum[k];
            table.Add((id, x));
            total += x;
        }
        Safe.Info($"Loot: {table.Count - 1} items can be found");
    }

    static string Pick(float r)
    {
        if (table == null) Build();
        float x = r * total;
        foreach (var (id, w) in table!)
        {
            if (x < w) return id;
            x -= w;
        }
        return table![^1].id;
    }

    static uint Fnv(string s)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char ch in s) { h ^= ch; h *= 16777619; }
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return h;
        }
    }

    static float Roll(string s) => (Fnv(s) % 100000) / 100000f;

    static string Cm(Vector3 p) => $"{Mathf.RoundToInt(p.x * 100)},{Mathf.RoundToInt(p.y * 100)},{Mathf.RoundToInt(p.z * 100)}";

    static string Clean(string n) => n == null ? "" : n.Replace("(Clone)", "").Trim();

    static int Seed => GameManager.m_SceneTransitionData != null ? GameManager.m_SceneTransitionData.m_GameRandomSeed : 0;

    [HarmonyPostfix, HarmonyPatch(typeof(Il2Cpp.Container), nameof(Il2Cpp.Container.PopulateWithRandomGear))]
    static void InContainer(Il2Cpp.Container __instance)
    {
        try
        {
            float scale = Settings.LootContainer.Value / 100f;
            var drawn = __instance.m_GearToInstantiate;
            if (scale <= 0f || drawn == null) return;
            string place = Cm(__instance.transform.position);
            Cabinet(__instance, drawn, place);
            Extra(__instance, drawn, place, scale);
            for (int i = 0; i < drawn.Count; i++)
            {
                string name = Clean(drawn[i]);
                if (!Sources.TryGetValue(name, out var share)) continue;
                string key = $"{Seed}|{place}|{i}|smokes";
                if (Roll(key) >= share.box * scale) continue;
                string id = Pick(Roll(key + "|what"));
                if (id == Nothing) continue;
                drawn[i] = Defs.Prefix + id;
                Safe.Info($"Loot: {id} in a container at {place} instead of {name}");
            }
        }
        catch (Exception e) { Safe.Log("Loot container", e); }
    }

    /// <summary>11-09: replacing matches and snacks alone was far too rare (log 1718: no finds in a whole evening outside cabinets):
    /// every container also gets one roll of its own, on top of what the game put in. Furniture of several drawers shares one roll.</summary>
    static void Extra(Il2Cpp.Container c, Il2CppSystem.Collections.Generic.List<string> drawn, string place, float scale)
    {
        float chance = Settings.LootAny.Value / 100f * scale;
        if (chance <= 0f) return;
        int drawers = 1;
        try { var par = c.transform.parent; if (par != null) drawers = Math.Max(1, par.GetComponentsInChildren<Il2Cpp.Container>(true).Length); } catch { }
        chance /= Math.Min(drawers, 4);
        string key = $"{Seed}|{place}|extra|smokes";
        if (Roll(key) >= chance) return;
        string id = Pick(Roll(key + "|what"));
        if (id == Nothing) return;
        if (drawn.Contains(Defs.Prefix + id)) return;   // 11-10: the game populated this container twice (log: same pack twice)
        drawn.Add(Defs.Prefix + id);
        Safe.Info($"Loot: {id} added to a container ({Clean(c.gameObject.name)}) at {place}");
    }

    static List<(string id, float w)>? cabinetTable;
    static float cabinetTotal;

    /// <summary>11-09: filing cabinets (offices) get one extra roll of their own: a pack of cigarettes or a cigar in its tube.</summary>
    static void Cabinet(Il2Cpp.Container c, Il2CppSystem.Collections.Generic.List<string> drawn, string place)
    {
        string n = Clean(c.gameObject.name);
        Safe.Debug($"Loot: container {n} at {place}");
        if (n.IndexOf("File", StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("Filing", StringComparison.OrdinalIgnoreCase) < 0) return;
        float chance = Settings.LootCabinet.Value / 100f * Settings.LootContainer.Value / 100f;
        // a cabinet is several drawers, each its own container (log 1718: three finds in one cabinet): the chance is for the whole cabinet
        int drawers = 1;
        try { var par = c.transform.parent; if (par != null) drawers = Math.Max(1, par.GetComponentsInChildren<Il2Cpp.Container>(true).Length); } catch { }
        chance /= drawers;
        string key = $"{Seed}|{place}|cabinet|smokes";
        if (Roll(key) >= chance) return;
        if (cabinetTable == null)
        {
            if (table == null) Build();
            cabinetTable = new();
            foreach (var (id, w) in table!)
            {
                if (id == Nothing) { cabinetTable.Add((id, w)); cabinetTotal += w; continue; }
                var d = Defs.ById(id);
                if (d == null || (d.Kind != Kind.Pack && d.Kind != Kind.Tube)) continue;
                cabinetTable.Add((id, w));
                cabinetTotal += w;
            }
        }
        if (cabinetTable.Count == 0) return;
        float x = Roll(key + "|what") * cabinetTotal;
        string pick = cabinetTable[^1].id;
        foreach (var (id, w) in cabinetTable) { if (x < w) { pick = id; break; } x -= w; }
        if (pick == Nothing || drawn.Contains(Defs.Prefix + pick)) return;
        drawn.Add(Defs.Prefix + pick);
        Safe.Info($"Loot: {pick} added to a filing cabinet ({n}) at {place}");
    }

    [HarmonyPostfix, HarmonyPatch(typeof(GearItem), nameof(GearItem.RollSpawnChance))]
    static void World(GearItem __instance)
    {
        try
        {
            float scale = Settings.LootWorld.Value / 100f;
            if (scale <= 0f || __instance == null) return;
            var go = __instance.gameObject;
            if (!go.activeSelf || !Sources.TryGetValue(Clean(go.name), out var share)) return;
            if (__instance.m_InPlayerInventory || __instance.m_InsideContainer || __instance.m_BeenInPlayerInventory || go.GetComponentInParent<Il2Cpp.Container>() != null) return;
            var t = go.transform;
            string key = $"{Seed}|{Cm(t.position)}|smokes";
            if (Roll(key) >= share.world * scale) return;
            string what = Pick(Roll(key + "|what"));
            if (what == Nothing) return;
            MelonLoader.MelonCoroutines.Start(Swap(go, what, t.position, t.rotation));
        }
        catch (Exception e) { Safe.Log("Loot world", e); }
    }

    static System.Collections.IEnumerator Swap(GameObject vanilla, string id, Vector3 pos, Quaternion rot)
    {
        yield return null;   // after the game's own start of the item: a failed roll has removed it by now
        try
        {
            if (vanilla == null || !vanilla.activeInHierarchy) yield break;
            var old = vanilla.GetComponent<GearItem>();
            if (old == null || old.m_InPlayerInventory) yield break;
            var gi = Items.Spawn(id);
            if (gi == null) yield break;
            gi.m_RolledSpawnChance = true;
            if (Defs.ById(id) is { } sd) Lighters.RandomFill(gi, sd);
            var root = GearManager.m_GearCategory;
            if (root != null) gi.transform.SetParent(root.transform, true);   // saved with the scene, like a dropped item
            gi.transform.position = pos;
            gi.transform.rotation = rot;
            UnityEngine.Object.Destroy(vanilla);
            Safe.Info($"Loot: {id} lying at {Cm(pos)} instead of {Clean(vanilla.name)}");
        }
        catch (Exception e) { Safe.Log("Loot swap", e); }
    }
}

/// <summary>11-09: smokes added to a container never showed up (log 1718 had three in a cabinet, the user found nothing there): the
/// game makes container contents from the prefab name through its asset system, which does not know our names. Ours are made here.</summary>
[HarmonyPatch(typeof(Il2Cpp.Container), nameof(Il2Cpp.Container.InstantiateGearPrefabInContainer))]
internal static class ContainerSpawnPatch
{
    static bool Prefix(Il2Cpp.Container __instance, string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName) || !Defs.TryGet(prefabName, out var def)) return true;
        try
        {
            var gi = GearItem.InstantiateGearItem(Defs.Prefix + def.Id);
            if (gi == null) { Safe.Info($"Loot: {def.Id} could not be made for a container"); return false; }
            gi.m_RolledSpawnChance = true;
            Lighters.RandomFill(gi, def);
            __instance.AddGear(gi);
            Safe.Info($"Loot: {def.Id} put into a container ({__instance.gameObject.name})");
        }
        catch (Exception e) { Safe.Log("Loot into container", e); }
        return false;
    }
}

/// <summary>11-10: the game makes a container's contents only when it is first opened, and the prefix above never showed a line
/// in any log (hundreds of our names drawn, none made). Our items are now made on the first of the opening calls that runs,
/// and taken out of the name list so the game never sees them. Patched by hand, one overload at a time, so a missing or
/// renamed method only skips itself.</summary>
internal static class ContainerOpenPatch
{
    static readonly string[] Hooks = { "BeginContainerOpen", "ShowItemsAfterSearch", "ShowContainerContents", "InstantiateContents" };

    public static void Apply(HarmonyLib.Harmony h)
    {
        var pre = new HarmonyLib.HarmonyMethod(typeof(ContainerOpenPatch).GetMethod(nameof(Prefix), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
        foreach (var name in Hooks)
            foreach (var m in typeof(Il2Cpp.Container).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
            {
                if (m.Name != name) continue;
                try { h.Patch(m, prefix: pre); Safe.Debug($"Loot: patched Container.{name}"); }
                catch (Exception e) { Safe.Log($"Loot patch {name}", e); }
            }
    }

    static void Prefix(Il2Cpp.Container __instance) => Make(__instance, "open");

    public static void Make(Il2Cpp.Container c, string why)
    {
        try
        {
            var list = c == null ? null : c.m_GearToInstantiate;
            if (list == null || list.Count == 0) return;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                string n = list[i];
                if (string.IsNullOrEmpty(n) || !Defs.TryGet(n, out var def)) continue;
                list.RemoveAt(i);
                var gi = GearItem.InstantiateGearItem(Defs.Prefix + def.Id);
                if (gi == null) { Safe.Info($"Loot: {def.Id} could not be made for a container"); continue; }
                gi.m_RolledSpawnChance = true;
                Lighters.RandomFill(gi, def);
                c.AddGear(gi);
                Safe.Info($"Loot: {def.Id} put into a container ({c.gameObject.name}) on {why}");
            }
        }
        catch (Exception e) { Safe.Log("Loot make on open", e); }
    }
}

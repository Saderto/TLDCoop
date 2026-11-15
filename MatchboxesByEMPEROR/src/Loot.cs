namespace MatchboxesByEMPEROR;

/// <summary>Containers searched for the first time: some of the game's own matches become a labelled box of the same material
/// (wooden box → one of our wooden kinds, matchbook → a matchbook), and a few pieces of furniture get a box of their own.
/// Found boxes are partly used: 40–100% of their matches.
/// 0.3.5, by the owner's loot rule of 11-15 (the way SmokesByEMPEROR and SnacksByEMPEROR do it): doors and drawers of one piece of
/// furniture are separate containers at the very same spot, so the roll also takes the door's number and the extra box goes to one door
/// only; a find is only a NAME in the container's list until the container is opened; a second populate of the same container adds nothing.
/// Rolls are seeded by the world's seed, the scene and the place: in co-op and after a reload everyone finds the same.</summary>
[HarmonyPatch(typeof(Container), nameof(Container.PopulateWithRandomGear))]
internal static class Loot
{
    public static uint Fnv(string s)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char ch in s) { h ^= ch; h *= 16777619; }
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return h;
        }
    }

    static string Place(Vector3 p) => $"{Mathf.RoundToInt(p.x * 10)}|{Mathf.RoundToInt(p.y * 10)}|{Mathf.RoundToInt(p.z * 10)}";

    /// <summary>The containers of one piece of furniture: how many there are, how many sit at this container's spot, and this one's number
    /// among those (in the scene's own order, the same for every player).</summary>
    static (int drawers, int same, int mine) Furniture(Container c, string place)
    {
        int drawers = 1, same = 1, mine = 0;
        try
        {
            var par = c.transform.parent;
            if (par == null) return (1, 1, 0);
            var all = par.GetComponentsInChildren<Container>(true);
            drawers = Math.Max(1, all.Length);
            int n = 0;
            foreach (var o in all)
            {
                if (o == null || Place(o.transform.position) != place) continue;
                if (o.Pointer == c.Pointer) mine = n;
                n++;
            }
            same = Math.Max(1, n);
        }
        catch { }
        return (drawers, same, mine);
    }

    public static string Key(Container c, out int drawers, out int same, out int mine)
    {
        int seed = 0;
        try { seed = GameManager.m_SceneTransitionData != null ? GameManager.m_SceneTransitionData.m_GameRandomSeed : 0; } catch { }
        string place = Place(c.transform.position);
        (drawers, same, mine) = Furniture(c, place);
        return $"{seed}|{GameManager.m_ActiveScene}|{place}";
    }

    static void Postfix(Container __instance)
    {
        if (__instance == null) return;
        try
        {
            var names = __instance.m_GearToInstantiate;
            if (names == null) return;
            string key = Key(__instance, out int drawers, out int same, out int mine);
            // the game's own matches of this door: each door rolls for itself (its number is in the seed)
            var rnd = new System.Random(unchecked((int)Fnv($"EMBswap|{key}|{mine}")));
            for (int i = 0; i < names.Count; i++)
            {
                string n = names[i] ?? "";
                bool wood = n.Equals("GEAR_WoodMatches", StringComparison.OrdinalIgnoreCase), card = n.Equals("GEAR_PackMatches", StringComparison.OrdinalIgnoreCase);
                if (!(wood || card) || rnd.NextDouble() * 100.0 >= Settings.ReplaceVanilla.Value) continue;
                var d = Defs.Roll(rnd, card);
                names[i] = Defs.Prefix + d.Id;
                Safe.Debug($"Matchboxes: {n} in {__instance.name} (door {mine + 1} of {same}) → {d.Id}");
            }
            // a box of its own: the chance is for the piece of furniture, and one of its doors gets it
            float chance = Settings.LootChance.Value * Math.Min(same, 4) / Math.Min(drawers, 4);
            if ((Fnv("EMB|" + key) % 10000) / 100f >= chance) return;
            if (same > 1 && Fnv("EMBwhich|" + key) % (uint)same != (uint)mine) return;
            var extra = Defs.Roll(new System.Random(unchecked((int)Fnv("EMBpick|" + key))));
            string name = Defs.Prefix + extra.Id;
            if (names.Contains(name)) return;            // the game sometimes populates one container twice
            names.Add(name);
            Safe.Debug($"Matchboxes: {extra.Id} drawn for {__instance.name} (doors {same}/{drawers}, mine {mine})");
        }
        catch (Exception e) { Safe.Log("Matchbox loot", e); }
    }

    /// <summary>One of ours for a container, partly used; how much is fixed by the place and the label.</summary>
    public static bool Put(Container c, Def d, int nth)
    {
        var g = Items.Spawn(d.Id);
        if (g == null) return false;
        string key = Key(c, out _, out _, out int mine);
        float part = .4f + (Fnv($"EMBleft|{key}|{mine}|{d.Id}|{nth}") % 1000) / 1000f * .6f;
        if (g.m_StackableItem is { } st) st.m_Units = Math.Max(1, (int)Math.Round(d.Kind.Matches * part));
        g.m_RolledSpawnChance = true;
        c.AddGear(g);
        return true;
    }
}

/// <summary>The game makes a container's contents from prefab names through its asset system, which does not know ours, and only when the
/// container is first opened. Ours are made on the first of the opening calls that runs and taken out of the list, so the game never sees
/// them. Patched by hand, one method at a time, so a missing or renamed one only skips itself.</summary>
internal static class ContainerOpenPatch
{
    static readonly string[] Hooks = { "BeginContainerOpen", "ShowItemsAfterSearch", "InstantiateContents" };

    public static void Apply(HarmonyLib.Harmony h)
    {
        var pre = new HarmonyLib.HarmonyMethod(typeof(ContainerOpenPatch).GetMethod(nameof(Prefix), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
        int n = 0;
        foreach (var name in Hooks)
            foreach (var m in typeof(Container).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
            {
                if (m.Name != name) continue;
                try { h.Patch(m, prefix: pre); n++; }
                catch (Exception e) { Safe.Log($"Loot patch {name}", e); }
            }
        Safe.Info($"Loot: {n} container opening calls watched");
    }

    static void Prefix(Container __instance)
    {
        try
        {
            var list = __instance == null ? null : __instance.m_GearToInstantiate;
            if (list == null || list.Count == 0) return;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                string n = list[i];
                if (string.IsNullOrEmpty(n) || !Defs.TryGet(n, out var def)) continue;
                list.RemoveAt(i);
                if (Loot.Put(__instance!, def, i)) Safe.Debug($"Matchboxes: {def.Id} put into {__instance!.gameObject.name}");
                else Safe.Info($"Matchboxes: {def.Id} could not be made for a container");
            }
        }
        catch (Exception e) { Safe.Log("Matchbox loot on open", e); }
    }
}

/// <summary>A second line: if the game still asks for one of ours by name, it is made here.</summary>
[HarmonyPatch(typeof(Container), nameof(Container.InstantiateGearPrefabInContainer))]
internal static class ContainerSpawnPatch
{
    static bool Prefix(Container __instance, string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName) || !Defs.TryGet(prefabName, out var def)) return true;
        try { Loot.Put(__instance, def, 0); }
        catch (Exception e) { Safe.Log("Matchbox loot into container", e); }
        return false;
    }
}

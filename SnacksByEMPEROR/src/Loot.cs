namespace SnacksByEMPEROR;

/// <summary>Snacks turn up in containers searched for the first time: kitchen cupboards, fridges and food boxes more often.
/// The roll is seeded by the world's seed, the scene and the container's place, so in coop every player finds the same thing in the same container.
/// Done the way SmokesByEMPEROR does after its fixes (user 11-15: «можешь поучиться у треда про табак, там мы это чинили»):
/// the find is only a name in the container's list until the container is opened (so nothing is made twice or saved before the search);
/// doors and drawers of one piece of furniture are separate containers at the very same spot, so they share one roll and one of them gets the find.</summary>
[HarmonyPatch(typeof(Container), nameof(Container.PopulateWithRandomGear))]
internal static class Loot
{
    static readonly string[] Kitchen = { "kitchen", "cabinet", "cupboard", "fridge", "refrigerator", "freezer", "pantry", "food", "vending", "counter", "stove", "oven", "shelf" };
    static readonly string[] Never = { "corpse", "body", "toolbox", "tool", "firstaid", "medic", "gun", "ammo", "safe", "mailbox", "washer", "dryer" };

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

    static void Postfix(Container __instance)
    {
        try
        {
            if (__instance == null || __instance.m_IsCorpse) return;
            var drawn = __instance.m_GearToInstantiate;
            if (drawn == null) return;
            string n = (__instance.gameObject.name ?? "").ToLowerInvariant();
            if (Never.Any(n.Contains)) return;
            bool kitchen = Kitchen.Any(n.Contains);
            int seed = 0;
            try { seed = GameManager.m_SceneTransitionData != null ? GameManager.m_SceneTransitionData.m_GameRandomSeed : 0; } catch { }
            string place = Place(__instance.transform.position);
            string key = $"{seed}|{GameManager.m_ActiveScene}|{place}";
            // a cupboard of four doors is four containers: the chance is for the piece of furniture, not for each door
            var (drawers, same, mine) = Furniture(__instance, place);
            float chance = (kitchen ? Mod.KitchenChance : Mod.LootChance) * Math.Min(same, 4) / Math.Min(drawers, 4);
            float roll = (Fnv("SNK|" + key) % 10000) / 100f;
            if (roll >= chance) return;
            if (same > 1 && Fnv("SNKwhich|" + key) % (uint)same != (uint)mine) return;   // one of the doors gets the find
            // a seasonal pack is rare: one find in twelve is one of them
            bool rare = Fnv("SNKrare|" + key) % 12 == 0;
            var pool = Defs.Sellable.Where(d => d.Rare == rare).ToArray();
            if (pool.Length == 0) pool = Defs.Sellable.ToArray();
            var def = pool[Fnv("SNKpick|" + key) % (uint)pool.Length];
            string name = Defs.Prefix + def.Id;
            if (drawn.Contains(name)) return;   // the game sometimes populates a container twice
            drawn.Add(name);
            Safe.Debug($"Loot: {def.Id} drawn for {__instance.name} ({(kitchen ? "kitchen" : "other")}, doors {same}/{drawers}, mine {mine})");
        }
        catch (Exception e) { Safe.Log("Snack loot", e); }
    }
}

/// <summary>The game makes a container's contents from prefab names through its asset system, which does not know ours, and only when the
/// container is first opened. Ours are made on the first of the opening calls that runs and taken out of the list, so the game never sees them.
/// Patched by hand, one method at a time, so a missing or renamed one only skips itself.</summary>
internal static class ContainerOpenPatch
{
    static readonly string[] Hooks = { "BeginContainerOpen", "ShowItemsAfterSearch", "InstantiateContents" };

    public static void Apply(HarmonyLib.Harmony h)
    {
        var pre = new HarmonyLib.HarmonyMethod(typeof(ContainerOpenPatch).GetMethod(nameof(Prefix), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
        foreach (var name in Hooks)
            foreach (var m in typeof(Container).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
            {
                if (m.Name != name) continue;
                try { h.Patch(m, prefix: pre); }
                catch (Exception e) { Safe.Log($"Loot patch {name}", e); }
            }
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
                var gi = Items.Spawn(def.Id);
                if (gi == null) { Safe.Info($"Loot: {def.Id} could not be made for a container"); continue; }
                gi.m_RolledSpawnChance = true;
                __instance!.AddGear(gi);
                Safe.Debug($"Loot: {def.Id} put into {__instance.gameObject.name}");
            }
        }
        catch (Exception e) { Safe.Log("Snack loot on open", e); }
    }
}

/// <summary>A second line: if the game still asks for one of ours by name, it is made here.</summary>
[HarmonyPatch(typeof(Container), nameof(Container.InstantiateGearPrefabInContainer))]
internal static class ContainerSpawnPatch
{
    static bool Prefix(Container __instance, string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName) || !Defs.TryGet(prefabName, out var def)) return true;
        try
        {
            var gi = Items.Spawn(def.Id);
            if (gi != null) { gi.m_RolledSpawnChance = true; __instance.AddGear(gi); }
        }
        catch (Exception e) { Safe.Log("Snack loot into container", e); }
        return false;
    }
}

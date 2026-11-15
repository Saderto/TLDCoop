namespace MatchboxesByEMPEROR;

/// <summary>Containers searched for the first time: some of the game's own matches become a labelled box of the same material
/// (wooden box → one of our wooden kinds, matchbook → a matchbook), and a few containers get a box of their own.
/// Found boxes are partly used: 40–100% of their matches.</summary>
[HarmonyPatch(typeof(Container), nameof(Container.PopulateWithRandomGear))]
internal static class Loot
{
    static System.Random rnd = new();

    static void Postfix(Container __instance)
    {
        if (__instance == null) return;
        try
        {
            // the same roll for everyone in the world (co-op: host and friend open the same container and must see the same box):
            // seeded by the world's seed, the scene and where the container stands
            var pos = __instance.transform.position;
            int seed = GameManager.m_SceneTransitionData != null ? GameManager.m_SceneTransitionData.m_GameRandomSeed : 0;
            unchecked
            {
                seed = seed * 31 + StableHash(GameManager.m_ActiveScene ?? "");
                seed = seed * 31 + Mathf.RoundToInt(pos.x * 10f);
                seed = seed * 31 + Mathf.RoundToInt(pos.y * 10f);
                seed = seed * 31 + Mathf.RoundToInt(pos.z * 10f);
            }
            rnd = new System.Random(seed);
            var names = __instance.m_GearToInstantiate;
            if (names != null)
                for (int i = 0; i < names.Count; i++)
                {
                    string n = names[i] ?? "";
                    bool wood = n.Equals("GEAR_WoodMatches", StringComparison.OrdinalIgnoreCase), card = n.Equals("GEAR_PackMatches", StringComparison.OrdinalIgnoreCase);
                    if (!(wood || card) || rnd.NextDouble() * 100.0 >= Settings.ReplaceVanilla.Value) continue;
                    var d = Defs.Roll(rnd, card);
                    names[i] = Defs.Prefix + d.Id;
                    Safe.Debug($"Matchboxes: {n} in {__instance.name} (to make) → {d.Id}");
                }
            var items = new Il2CppSystem.Collections.Generic.List<GearItem>();
            __instance.GetItems(items);
            foreach (var g in items)
            {
                if (g == null) continue;
                string n = Defs.Clean(g.name);
                bool wood = n == "GEAR_WoodMatches", card = n == "GEAR_PackMatches";
                if (!(wood || card) || rnd.NextDouble() * 100.0 >= Settings.ReplaceVanilla.Value) continue;
                var d = Defs.Roll(rnd, card);
                if (Put(__instance, d)) { __instance.DestroyGear(g); Safe.Debug($"Matchboxes: {n} in {__instance.name} → {d.Id}"); }
            }
            if (rnd.NextDouble() * 100.0 < Settings.LootChance.Value)
            {
                var d = Defs.Roll(rnd);
                if (Put(__instance, d)) Safe.Debug($"Matchboxes: {d.Id} put into {__instance.name}");
            }
        }
        catch (Exception e) { Safe.Log("Matchbox loot", e); }
    }

    static int StableHash(string t)
    {
        unchecked { int h = 17; foreach (char ch in t) h = h * 31 + ch; return h; }
    }

    static bool Put(Container c, Def d)
    {
        var g = Items.Spawn(d.Id);
        if (g == null) return false;
        if (g.m_StackableItem is { } st) st.m_Units = Math.Max(1, (int)Math.Round(d.Kind.Matches * (.4f + (float)rnd.NextDouble() * .6f)));
        c.AddGear(g);
        return true;
    }
}

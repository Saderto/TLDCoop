namespace JellyfishByEMPEROR;

/// <summary>Empty glass jars turn up in containers searched for the first time: 4% of them, 10% on the coast (my choice, 11-12).</summary>
[HarmonyPatch(typeof(Container), nameof(Container.PopulateWithRandomGear))]
internal static class Loot
{
    static readonly System.Random rnd = new();

    static void Postfix(Container __instance)
    {
        try
        {
            if (__instance == null) return;
            bool coast = Defs.CoastScenes.Contains(GameManager.m_ActiveScene ?? "");
            if (rnd.NextDouble() * 100.0 >= (coast ? Tune.JarLootCoast : Tune.JarLoot)) return;
            var g = Items.Spawn("Jar");
            if (g == null) return;
            __instance.AddGear(g);
            Safe.Debug($"Jar put into {__instance.name}");
        }
        catch (Exception e) { Safe.Log("Jar loot", e); }
    }
}

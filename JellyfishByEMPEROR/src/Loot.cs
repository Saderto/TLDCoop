namespace JellyfishByEMPEROR;

/// <summary>Empty glass jars turn up in containers searched for the first time: about 2.7% of them, 6.7% on the coast (4 and 10 at first; the owner, 11-14: a third rarer).</summary>
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

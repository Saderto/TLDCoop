using System.Reflection;

namespace SmokehouseByEMPEROR;

/// <summary>Eating smoked food starts effects of FoodPackByEMPEROR when that mod is installed (no hard link between the dlls):
/// meat gives «Долгая сытость» (10% slower calorie burn, 4 h); salt adds «Солёное» (thirst +15%, 2 h) and, on fish, the hypothermia-risk effect (15%, 6 h), on meat slower tiredness (10%, 3 h). Salt no longer changes how long anything keeps.
/// Without FoodPack the food is plain food.</summary>
[HarmonyPatch(typeof(GearItem), nameof(GearItem.ApplyBuffs))]
internal static class Effects
{
    static bool looked;
    static MethodInfo? start;
    static Type? useType;

    static void Look()
    {
        if (looked) return;
        looked = true;
        try
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "FoodPackByEMPEROR");
            var eat = asm?.GetType("FoodPackByEMPEROR.EatPatch");
            useType = asm?.GetType("FoodPackByEMPEROR.EffectUse");
            start = eat?.GetMethod("StartEffect", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Safe.Info("FoodPack effects: " + (start != null && useType != null ? "linked" : "FoodPackByEMPEROR not found, smoked food has no extra effects"));
        }
        catch (Exception e) { Safe.Log("FoodPack link", e); }
    }

    static void Start(string id, float value, float hours, string cause, float share)
    {
        if (start == null || useType == null) return;
        var use = Activator.CreateInstance(useType)!;
        useType.GetField("Id")!.SetValue(use, id);
        useType.GetField("Value")!.SetValue(use, (float?)value);
        useType.GetField("Hours")!.SetValue(use, (float?)hours);
        var ps = start.GetParameters();
        var args = new object?[ps.Length];
        args[0] = use; args[1] = cause; args[2] = share;
        for (int i = 3; i < ps.Length; i++) args[i] = ps[i].ParameterType == typeof(bool) ? true : ps[i].DefaultValue;
        start.Invoke(null, args);
    }

    static void Postfix(GearItem __instance, float normalizedValue)
    {
        try
        {
            if (normalizedValue <= 0f || !Defs.Is(__instance, out var def) || def.Species == null) return;
            Look();
            string cause = "SMOKEHOUSE_" + def.Id + "_NAME";
            // 11-11 owner: a salted piece has the salt's effects only, not the plain piece's as well
            if (def.Salted)
            {
                if (def.Species.Fish) Start("Hardening", .15f, 6f, cause, normalizedValue);
                else Start("Tireless", .10f, 3f, cause, normalizedValue);
                Start("Salty", .15f, 2f, cause, normalizedValue);
            }
            else if (def.Species.Fish) Start("DeepRest", .10f, 6f, cause, normalizedValue);
            else Start("LongFullness", .10f, 4f, cause, normalizedValue);
        }
        catch (Exception e)
        {
            Safe.Log("Smoked food effects", e);
        }
    }
}

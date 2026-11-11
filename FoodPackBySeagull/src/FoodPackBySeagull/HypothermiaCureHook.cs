using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(Hypothermia), "GetNumHoursWarmForCure")]
internal static class HypothermiaCureHook
{
	private static void Postfix(ref float __result)
	{
		__result *= Fx.Less("WarmingBrew");
	}
}

using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch]
internal static class AimHooks
{
	[HarmonyPostfix]
	[HarmonyPatch(typeof(GunItem), "GetSwayIncreasePerSecond")]
	private static void Gun(ref float __result)
	{
		__result *= Fx.Less("SteadyHand") * Fx.More("Tremor");
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(BowItem), "GetSwayIncreasePerSecond")]
	private static void Bow(ref float __result)
	{
		__result *= Fx.Less("SteadyHand") * Fx.More("Tremor");
	}
}

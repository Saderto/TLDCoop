using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(Freezing), "CalculateBodyTemperature")]
internal static class FeltTemperatureHook
{
	private static void Postfix(ref float __result)
	{
		__result += Fx.V("FireBlood") - Fx.V("Chill");
	}
}

using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(Freezing), "CalculateBodyTemperature")]
internal static class FeltTemperatureHook
{
	private static void Postfix(ref float __result)
	{
		__result += Fx.V("FireBlood") - Fx.V("Chill");
	}
}

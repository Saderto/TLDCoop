using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(Wind), "CalculateWindChill")]
internal static class WindHook
{
	private static void Postfix(ref float __result)
	{
		__result *= Fx.Less("WindProof") * Fx.More("WideVessels");
	}
}

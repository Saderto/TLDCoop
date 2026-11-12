using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(Inventory), "GetExtraScentIntensity")]
internal static class SmellHook
{
	[HarmonyPriority(800)]
	private static void Postfix(ref float __result)
	{
		__result += Fx.V("Stink") * 100f;
	}
}

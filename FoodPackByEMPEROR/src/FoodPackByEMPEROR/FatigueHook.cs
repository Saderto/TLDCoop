using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(Fatigue), "CalculateFatigueIncrease")]
internal static class FatigueHook
{
	private static void Postfix(ref float __result)
	{
		if (Fx.On("CoffeeCharge"))
		{
			__result = 0f;
			return;
		}
		__result *= Fx.Less("Tireless", "GinjoAroma", "StrongHealth") * Fx.More("Drowsy");
	}
}

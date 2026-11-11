using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(Hypothermia), "CheckForHypothermia")]
internal static class HypothermiaRiskHook
{
	private static void Prefix(Hypothermia __instance, out float __state)
	{
		__state = __instance.m_ElapsedHours;
	}

	private static void Postfix(Hypothermia __instance, float __state)
	{
		float num = __instance.m_ElapsedHours - __state;
		if (num > 0f)
		{
			__instance.m_ElapsedHours = __state + num * Fx.Less("Hardening", "WarmInside") * Fx.More("Sweating");
		}
	}
}

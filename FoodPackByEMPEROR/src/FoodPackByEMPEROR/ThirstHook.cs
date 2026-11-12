using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(Thirst), "Update")]
internal static class ThirstHook
{
	private static void Prefix(Thirst __instance, out float __state)
	{
		__state = __instance.m_CurrentThirst;
	}

	private static void Postfix(Thirst __instance, float __state)
	{
		float num = __instance.m_CurrentThirst - __state;
		if (num > 0f)
		{
			__instance.m_CurrentThirst = __state + num * Fx.Less("StrongHealth", "Aged") * Fx.More("Salty");
		}
	}
}

using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(PlayerMovement), "Update")]
internal static class SprintHook
{
	private static void Prefix(PlayerMovement __instance, out float[] __state)
	{
		__state = new float[2] { __instance.m_SprintStaminaRecoverPerHour, __instance.m_SprintStaminaUsagePerSecond };
		__instance.m_SprintStaminaRecoverPerHour *= Fx.More("SecondWind", "RicePower") * Fx.Less("Breathless") * (1f + Fx.V2("IrishSpirit"));
		Encumber encumberComponent = GameManager.GetEncumberComponent();
		if (encumberComponent != null && encumberComponent.IsEncumbered())
		{
			__instance.m_SprintStaminaUsagePerSecond *= Fx.Less("StrongBack");
		}
	}

	private static void Postfix(PlayerMovement __instance, float[] __state)
	{
		__instance.m_SprintStaminaRecoverPerHour = __state[0];
		__instance.m_SprintStaminaUsagePerSecond = __state[1];
	}
}

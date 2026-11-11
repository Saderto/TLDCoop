using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(Rest), "UpdateWhenSleeping")]
internal static class SleepHook
{
	private static void Prefix(out float __state)
	{
		__state = GameManager.GetFatigueComponent()?.m_CurrentFatigue ?? (-1f);
	}

	private static void Postfix(float __state)
	{
		Fatigue fatigueComponent = GameManager.GetFatigueComponent();
		if (!(fatigueComponent == null) && !(__state < 0f))
		{
			float num = __state - fatigueComponent.m_CurrentFatigue;
			if (num > 0f)
			{
				fatigueComponent.m_CurrentFatigue = __state - num * Fx.More("DeepRest") * Fx.Less("RestlessSleep");
			}
		}
	}
}

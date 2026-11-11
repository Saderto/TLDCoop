using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(BloodLoss), "MaybeApplyDamage")]
internal static class BloodHook
{
	private static void Prefix(BloodLoss __instance, out float[] __state)
	{
		__state = new float[2] { __instance.m_HPDrainPerHour, __instance.m_HPDrainPerHourBandaged };
		float num = Fx.Less("ThickBlood");
		__instance.m_HPDrainPerHour *= num;
		__instance.m_HPDrainPerHourBandaged *= num;
	}

	private static void Postfix(BloodLoss __instance, float[] __state)
	{
		__instance.m_HPDrainPerHour = __state[0];
		__instance.m_HPDrainPerHourBandaged = __state[1];
	}
}

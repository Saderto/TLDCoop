using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(Sprains), "Update")]
internal static class SlopeHook
{
	private static void Prefix(Sprains __instance, out float[] __state)
	{
		__state = new float[2] { __instance.m_BaseChanceWhenMovingOnSlope, __instance.m_ChanceIncreaseEachDegreeAboveMinSlope };
		float num = Fx.Less("SureStep") * Fx.More("WobblyLegs");
		__instance.m_BaseChanceWhenMovingOnSlope *= num;
		__instance.m_ChanceIncreaseEachDegreeAboveMinSlope *= num;
	}

	private static void Postfix(Sprains __instance, float[] __state)
	{
		__instance.m_BaseChanceWhenMovingOnSlope = __state[0];
		__instance.m_ChanceIncreaseEachDegreeAboveMinSlope = __state[1];
	}
}

using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(FallDamage), "ApplyFallDamage")]
internal static class FallHook
{
	private static void Prefix(FallDamage __instance, out float[] __state)
	{
		__state = new float[2] { __instance.m_DamagePerMeter, __instance.m_DamagePerMeterFromRope };
		float num = Fx.Less("StrongBones");
		__instance.m_DamagePerMeter *= num;
		__instance.m_DamagePerMeterFromRope *= num;
	}

	private static void Postfix(FallDamage __instance, float[] __state)
	{
		__instance.m_DamagePerMeter = __state[0];
		__instance.m_DamagePerMeterFromRope = __state[1];
	}
}

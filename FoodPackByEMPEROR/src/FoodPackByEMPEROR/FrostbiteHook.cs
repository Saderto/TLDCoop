using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(Frostbite), "UpdateFrostbite")]
internal static class FrostbiteHook
{
	private static void Prefix(Frostbite __instance, out float[] __state)
	{
		__state = new float[3] { __instance.m_DamagePerHourBase, __instance.m_DamagePerHourIncreasePerDegree, __instance.m_MaxDamagePerHour };
		float num = Fx.Less("FrostResist") * (1f - Fx.V2("PolarHardening"));
		__instance.m_DamagePerHourBase *= num;
		__instance.m_DamagePerHourIncreasePerDegree *= num;
		__instance.m_MaxDamagePerHour *= num;
	}

	private static void Postfix(Frostbite __instance, float[] __state)
	{
		__instance.m_DamagePerHourBase = __state[0];
		__instance.m_DamagePerHourIncreasePerDegree = __state[1];
		__instance.m_MaxDamagePerHour = __state[2];
	}
}

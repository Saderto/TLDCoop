using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;

namespace FoodPackBySeagull;

[HarmonyPatch]
internal static class SprainHealHooks
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(SprainedAnkle), "UpdateSprainedAnkle")]
	private static void AnkleBefore(SprainedAnkle __instance, int index, out float[]? __state)
	{
		__state = Read(__instance.m_ElapsedHoursList, index);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SprainedAnkle), "UpdateSprainedAnkle")]
	private static void AnkleAfter(SprainedAnkle __instance, int index, float[]? __state)
	{
		Speed(__instance.m_ElapsedHoursList, index, __state);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SprainedWrist), "UpdateSprainedWrist")]
	private static void WristBefore(SprainedWrist __instance, int index, out float[]? __state)
	{
		__state = Read(__instance.m_ElapsedHoursList, index);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SprainedWrist), "UpdateSprainedWrist")]
	private static void WristAfter(SprainedWrist __instance, int index, float[]? __state)
	{
		Speed(__instance.m_ElapsedHoursList, index, __state);
	}

	private static float[]? Read(List<float> list, int index)
	{
		if (index < 0 || index >= list.Count)
		{
			return null;
		}
		return new float[2]
		{
			list[index],
			list.Count
		};
	}

	private static void Speed(List<float> list, int index, float[]? state)
	{
		if (state != null && list.Count == (int)state[1])
		{
			float num = list[index] - state[0];
			if (num > 0f)
			{
				list[index] = state[0] + num * Fx.More("FastHealing");
			}
		}
	}
}

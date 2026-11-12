using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace FoodPackByEMPEROR;

[HarmonyPatch]
internal static class InfectionHooks
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(InfectionRisk), "UpdateInfectionRisk")]
	private static void Before(InfectionRisk __instance, int index, out float[]? __state)
	{
		__state = null;
		List<float> currentInfectionChanceList = __instance.m_CurrentInfectionChanceList;
		if (index >= 0 && index < currentInfectionChanceList.Count)
		{
			__state = new float[2]
			{
				currentInfectionChanceList[index],
				currentInfectionChanceList.Count
			};
			currentInfectionChanceList[index] = __state[0] * Fx.Less("Immunity");
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(InfectionRisk), "UpdateInfectionRisk")]
	private static void After(InfectionRisk __instance, int index, float[]? __state)
	{
		List<float> currentInfectionChanceList = __instance.m_CurrentInfectionChanceList;
		if (__state != null && currentInfectionChanceList.Count == (int)__state[1])
		{
			currentInfectionChanceList[index] = __state[0];
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(InfectionRisk), "GetCurrentRisk")]
	private static void Shown(ref int __result)
	{
		__result = Mathf.RoundToInt((float)__result * Fx.Less("Immunity"));
	}
}

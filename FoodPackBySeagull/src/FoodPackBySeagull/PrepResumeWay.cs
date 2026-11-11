using System;
using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(Panel_Cooking), "OnCookRecipe")]
internal static class PrepResumeWay
{
	private static void Prefix()
	{
		PrepResume.cooking++;
	}

	private static Exception? Finalizer(Exception? __exception)
	{
		PrepResume.cooking = Math.Max(0, PrepResume.cooking - 1);
		return __exception;
	}
}

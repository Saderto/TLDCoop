using System;
using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(CookingPotItem), "SetRuinedFoodMaterial")]
internal static class BurntLookPatch
{
	private static void Postfix(CookingPotItem __instance)
	{
		try
		{
			CookingLooks.Burnt(__instance);
		}
		catch (Exception e)
		{
			Safe.Log("Burnt look", e);
		}
	}
}

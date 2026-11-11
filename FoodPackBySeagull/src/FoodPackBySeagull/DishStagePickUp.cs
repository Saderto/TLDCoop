using System;
using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(CookingPotItem), "PickUpCookedGearItem")]
internal static class DishStagePickUp
{
	private static void Prefix(CookingPotItem __instance, out GearItem? __state)
	{
		__state = __instance.m_GearItemBeingCooked;
	}

	private static void Postfix(GearItem? __state)
	{
		try
		{
			CookingLooks.Stage(__state, raw: false);
		}
		catch (Exception e)
		{
			Safe.Log("Dish stage", e);
		}
	}
}

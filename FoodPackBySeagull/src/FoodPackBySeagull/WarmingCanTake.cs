using System;
using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(CookingPotItem), "CanPickupItemWhileCooking")]
internal static class WarmingCanTake
{
	private static void Postfix(CookingPotItem __instance, ref bool __result)
	{
		try
		{
			GearItem gearItemBeingCooked = __instance.m_GearItemBeingCooked;
			if (!__result && gearItemBeingCooked != null && DishWarming.Warming(gearItemBeingCooked))
			{
				__result = true;
			}
		}
		catch (Exception e)
		{
			Safe.Log("Dish warming", e);
		}
	}
}

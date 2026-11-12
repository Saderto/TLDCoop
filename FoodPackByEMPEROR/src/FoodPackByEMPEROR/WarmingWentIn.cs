using System;
using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(CookingPotItem), "StartCooking")]
internal static class WarmingWentIn
{
	private static void Postfix(CookingPotItem __instance, GearItem gearItemToCook)
	{
		try
		{
			DishWarming.WentIn(__instance, gearItemToCook);
			if (gearItemToCook != null)
			{
				__instance.SetUpGrubMesh();
			}
		}
		catch (Exception e)
		{
			Safe.Log("Dish warming", e);
		}
	}
}

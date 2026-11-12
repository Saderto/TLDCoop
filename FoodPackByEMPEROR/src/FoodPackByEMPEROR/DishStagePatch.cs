using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch]
internal static class DishStagePatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		yield return AccessTools.Method(typeof(CookingPotItem), "SetUpGrubMesh");
	}

	private static void Postfix(CookingPotItem __instance)
	{
		try
		{
			GearItem gearItemBeingCooked = __instance.m_GearItemBeingCooked;
			bool flag = __instance.GetCookingState() == CookingPotItem.CookingState.Cooking;
			bool flag2 = gearItemBeingCooked != null && DishWarming.Made(gearItemBeingCooked);
			CookingLooks.Stage(gearItemBeingCooked, flag && flag2);
			if (flag && !flag2)
			{
				CookingLooks.DoneLook(__instance);
			}
		}
		catch (Exception e)
		{
			Safe.Log("Dish stage", e);
		}
	}
}

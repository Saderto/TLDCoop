using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch]
internal static class WarmingWays
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		yield return AccessTools.Method(typeof(Panel_Cooking), "OnCookNormalFood");
		yield return AccessTools.Method(typeof(Cookable), "MaybeStartWarmingUpDueToNearbyFire");
		yield return AccessTools.Method(typeof(PlayerManager), "OnFoodOpeningComplete");
		yield return AccessTools.Method(typeof(PlayerManager), "OnSmashComplete");
		yield return AccessTools.Method(typeof(GearPlacePoint), "MaybeInstantiateDummyCookingPot");
	}

	private static void Prefix()
	{
		DishWarming.Enter();
	}

	private static Exception? Finalizer(Exception? __exception)
	{
		DishWarming.Leave();
		return __exception;
	}
}

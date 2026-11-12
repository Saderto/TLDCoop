using System;
using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(Panel_Cooking), "OnRecipePrepSuccess")]
internal static class RecipeWay
{
	private static void Prefix()
	{
		DishWarming.EnterRecipe();
	}

	private static Exception? Finalizer(Exception? __exception)
	{
		DishWarming.LeaveRecipe();
		return __exception;
	}
}

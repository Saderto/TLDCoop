using System;
using HarmonyLib;
using Il2CppTLD.Cooking;
using UnityEngine;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(CookableItem), "GetInventoryIcon")]
internal static class RecipeIconPatch
{
	private static void Postfix(CookableItem __instance, ref Texture2D __result)
	{
		try
		{
			string text = Recipes.DishOf(__instance.m_Recipe);
			if (text != null && Items.TryGet("GEAR_Seagull_" + text, out ItemDef def) && def.Icon != null)
			{
				Texture2D texture2D = Assets.Texture(def.Icon, compress: false);
				if ((object)texture2D != null)
				{
					__result = texture2D;
				}
			}
		}
		catch (Exception e)
		{
			Safe.Log("Recipe icon", e);
		}
	}
}

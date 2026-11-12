using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using Il2CppTLD.Gear;
using UnityEngine;

namespace FoodPackByEMPEROR;

[HarmonyPatch]
internal static class BlueprintPatches
{
	[HarmonyPostfix]
	[HarmonyPatch(typeof(BlueprintManager), "GetAllBlueprints")]
	private static void All(List<BlueprintData> __result)
	{
		try
		{
			if (__result == null)
			{
				return;
			}
			foreach (var value in Recipes.Crafts.Values)
			{
				BlueprintData item = value.bp;
				if (!__result.Contains(item))
				{
					__result.Add(item);
				}
			}
		}
		catch (Exception e)
		{
			Safe.Log("Blueprints", e);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(BlueprintData), "GetCraftingIcon")]
	private static bool Icon(BlueprintData __instance, ref Texture2D __result)
	{
		try
		{
			if (__instance == null || !Recipes.CraftDish.TryGetValue(__instance.name, out string value))
			{
				return true;
			}
			if (Items.TryGet("GEAR_Seagull_" + value, out ItemDef def) && def.Icon != null)
			{
				Texture2D texture2D = Assets.Texture(def.Icon, compress: false);
				if ((object)texture2D != null)
				{
					__result = texture2D;
					return false;
				}
			}
			return true;
		}
		catch (Exception e)
		{
			Safe.Log("Blueprint icon", e);
			return true;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(BlueprintManager), "GetFilteredBlueprints")]
	private static void Filtered(List<BlueprintData> blueprints)
	{
		try
		{
			if (blueprints == null || Recipes.Crafts.Count == 0)
			{
				return;
			}
			int num = GameManager.GetSkillsManager().GetSkill(SkillType.Cooking).GetCurrentTierNumber() + 1;
			foreach (var (item, num2) in Recipes.Crafts.Values)
			{
				if (num < num2 && blueprints.Contains(item))
				{
					blueprints.Remove(item);
				}
			}
		}
		catch (Exception e)
		{
			Safe.Log("Blueprint filter", e);
		}
	}
}

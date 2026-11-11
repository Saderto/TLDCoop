using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using MelonLoader;

namespace FoodPackBySeagull;

[HarmonyPatch]
internal static class ContainerItemPatch
{
	internal static int Chosen;

	internal static int Created;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Container), "BeginContainerOpen")]
	private static void Opening(Container __instance)
	{
		TakeOurs(__instance);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Container), "ShowItemsAfterSearch")]
	private static void Searched(Container __instance)
	{
		TakeOurs(__instance);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Container), "ShowContainerContents")]
	private static void Shown(Container __instance)
	{
		TakeOurs(__instance);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Container), "InstantiateContents")]
	private static void Contents(Container __instance)
	{
		TakeOurs(__instance);
	}

	private static void TakeOurs(Container c)
	{
		try
		{
			List<string> list = c?.m_GearToInstantiate;
			if (list == null)
			{
				return;
			}
			for (int num = list.Count - 1; num >= 0; num--)
			{
				string text = list[num];
				if (Items.TryGet(text, out ItemDef _))
				{
					list.RemoveAt(num);
					Make(c, text);
				}
			}
		}
		catch (Exception e)
		{
			Safe.Log("Container contents", e);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Container), "InstantiateGearPrefabInContainer")]
	private static bool One(Container __instance, string prefabName)
	{
		if (!Items.TryGet(prefabName, out ItemDef _))
		{
			return true;
		}
		Make(__instance, prefabName);
		return false;
	}

	internal static void Make(Container c, string prefabName)
	{
		try
		{
			GearItem gearItem = Items.Spawn(prefabName);
			if (gearItem == null)
			{
				MelonLogger.Warning("Loot: " + prefabName + " could not be made in " + c.name);
				return;
			}
			gearItem.RollGearConditionAndWeight();
			if (gearItem.m_EvolveItem != null)
			{
				gearItem.m_EvolveItem.DoEvolution();
			}
			if (c.m_SkipInspectVO && gearItem.m_Inspect != null)
			{
				gearItem.m_Inspect.m_SkipInspectVO = true;
			}
			c.AddGear(gearItem);
			gearItem.gameObject.SetActive(value: false);
			c.UpdateActiveMeshFromTotalWeight();
			Created++;
			MelonLogger.Msg($"Loot: {prefabName.Substring("GEAR_Seagull_".Length)} made in {c.name} (chosen {Chosen}, created {Created})");
		}
		catch (Exception e)
		{
			Safe.Log("Container item " + prefabName, e);
		}
	}
}

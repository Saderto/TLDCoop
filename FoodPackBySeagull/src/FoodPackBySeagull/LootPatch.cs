using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(Container), "PopulateWithRandomGear")]
internal static class LootPatch
{
	private static void Prefix(Container __instance, out int __state)
	{
		__state = __instance.m_GearToInstantiate?.Count ?? 0;
	}

	private static void Postfix(Container __instance, bool __runOriginal, int __state)
	{
		if (!__runOriginal)
		{
			return;
		}
		try
		{
			bool flag = __instance.IsLocked() || __instance.IsSafeLocked();
			LootTableData lootTableData = ((flag && __instance.m_LockedLootTableData != null) ? __instance.m_LockedLootTableData : __instance.m_LootTableData);
			string text = ((lootTableData != null) ? lootTableData.name : null);
			if (text == null)
			{
				LootTable lootTable = ((flag && __instance.m_LockedLootTablePrefab != null) ? __instance.m_LockedLootTablePrefab : __instance.m_LootTablePrefab);
				if (lootTable != null)
				{
					text = lootTable.name;
				}
			}
			if (text == null || !Loot.Tables.TryGetValue(text.Replace("(Clone)", "").Trim(), out System.Collections.Generic.List<LootEntry> value))
			{
				return;
			}
			Il2CppSystem.Collections.Generic.List<string> gearToInstantiate = __instance.m_GearToInstantiate;
			int count = gearToInstantiate.Count;
			int gameRandomSeed = GameManager.m_SceneTransitionData.m_GameRandomSeed;
			Vector3 position = __instance.transform.position;
			for (int i = __state; i < count; i++)
			{
				if (!Loot.Food.Contains(gearToInstantiate[i]))
				{
					continue;
				}
				float num = Loot.Roll(gameRandomSeed, position, i);
				foreach (LootEntry item in value)
				{
					num -= item.Chance;
					if (!(num >= 0f))
					{
						if (Items.TryGet("GEAR_Seagull_" + item.Item, out ItemDef _))
						{
							gearToInstantiate[i] = "GEAR_Seagull_" + item.Item;
							ContainerItemPatch.Chosen++;
							MelonLogger.Msg($"Loot: {item.Item} chosen for {__instance.name} (chosen {ContainerItemPatch.Chosen}, created {ContainerItemPatch.Created})");
						}
						else if (Loot.Unknown.Add(item.Item))
						{
							MelonLogger.Warning("loot.json names an unknown item: " + item.Item);
						}
						break;
					}
				}
			}
		}
		catch (Exception e)
		{
			Safe.Log("Loot", e);
		}
	}
}

using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;
using Il2CppTLD.Gear;
using MelonLoader;
using UnityEngine;

namespace MarkersByEMPEROR;

internal static class Craft
{
	public static BlueprintData Blueprint;

	private static GearItem s_Result;

	private static GearItem s_Stick;

	private static GearItem s_Cloth;

	private static readonly System.Collections.Generic.Dictionary<string, GearItem> s_Prefabs = new System.Collections.Generic.Dictionary<string, GearItem>();

	public static GearItem Prefab(string name)
	{
		if (s_Prefabs.TryGetValue(name, out var value) && Hud.Alive(value))
		{
			return value;
		}
		value = Hidden(name);
		if (value != null)
		{
			s_Prefabs[name] = value;
		}
		return value;
	}

	private static GearItem Hidden(string name)
	{
		GearItem gearItem = GearItem.InstantiateGearItem(name);
		if (gearItem == null)
		{
			return null;
		}
		gearItem.gameObject.SetActive(value: false);
		Object.DontDestroyOnLoad(gearItem.gameObject);
		return gearItem;
	}

	public static void Tick()
	{
		if (Blueprint != null || GameManager.GetPlayerManagerComponent() == null)
		{
			return;
		}
		Il2CppSystem.Collections.Generic.List<BlueprintData> list = BlueprintManager.Instance?.GetAllBlueprints();
		if (list == null || list.Count == 0)
		{
			return;
		}
		BlueprintData blueprintData = null;
		Il2CppSystem.Collections.Generic.List<BlueprintData>.Enumerator enumerator = list.GetEnumerator();
		while (enumerator.MoveNext())
		{
			BlueprintData current = enumerator.Current;
			if (current != null && current.name.StartsWith("BLUEPRINT_") && current.m_RequiredCraftingLocation == CraftingLocation.Anywhere && !current.m_RequiresLitFire)
			{
				blueprintData = current;
				break;
			}
		}
		if (blueprintData == null)
		{
			return;
		}
		if ((object)s_Result == null)
		{
			s_Result = Hidden("GEAR_SeagullMarker_TrailMarker");
		}
		if ((object)s_Stick == null)
		{
			s_Stick = Hidden("GEAR_Stick");
		}
		if ((object)s_Cloth == null)
		{
			s_Cloth = Hidden("GEAR_Cloth");
		}
		if (s_Result == null || s_Stick == null || s_Cloth == null)
		{
			MelonLogger.Error("Marker blueprint: items not found.");
			Blueprint = blueprintData;
			return;
		}
		BlueprintData blueprintData2 = Object.Instantiate(blueprintData);
		blueprintData2.name = "BP_SeagullMarker_TrailMarker";
		blueprintData2.hideFlags = HideFlags.HideAndDontSave;
		blueprintData2.SetCraftedResult(s_Result);
		blueprintData2.m_CraftedResultCount = 1;
		blueprintData2.m_DurationMinutes = 10;
		blueprintData2.m_RequiredCraftingLocation = CraftingLocation.Anywhere;
		blueprintData2.m_RequiresLitFire = false;
		blueprintData2.m_RequiredTool = null;
		blueprintData2.m_OptionalTools = new Il2CppReferenceArray<ToolsItem>(0L);
		blueprintData2.m_Locked = false;
		blueprintData2.m_IgnoreLockInSurvival = true;
		blueprintData2.m_AppearsInStoryOnly = false;
		blueprintData2.m_AppearsInSurvivalOnly = false;
		blueprintData2.m_XPModesToDisable?.Clear();
		blueprintData2.m_UsesPhoto = false;
		blueprintData2.m_RequiresLight = false;
		blueprintData2.m_RequiredGear = new Il2CppReferenceArray<BlueprintData.RequiredGearItem>(new BlueprintData.RequiredGearItem[2]
		{
			new BlueprintData.RequiredGearItem
			{
				m_Item = s_Stick,
				m_Count = 4,
				m_Units = BlueprintData.RequiredGearItem.Units.Count
			},
			new BlueprintData.RequiredGearItem
			{
				m_Item = s_Cloth,
				m_Count = 2,
				m_Units = BlueprintData.RequiredGearItem.Units.Count
			}
		});
		blueprintData2.m_RequiredLiquid = new Il2CppReferenceArray<BlueprintData.RequiredLiquid>(0L);
		blueprintData2.m_RequiredPowder = new Il2CppReferenceArray<BlueprintData.RequiredPowder>(0L);
		Blueprint = blueprintData2;
		if (!list.Contains(blueprintData2))
		{
			list.Add(blueprintData2);
		}
		MelonLogger.Msg("Trail marker blueprint added (copied from " + blueprintData.name + ").");
	}
}

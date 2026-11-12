using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(BlueprintData), "CanCraftBlueprint")]
internal static class PrepResumeCheck
{
	private sealed class Saved
	{
		public readonly List<(BlueprintData.RequiredGearItem r, int count, ItemWeight weight)> Gear = new List<(BlueprintData.RequiredGearItem, int, ItemWeight)>();

		public readonly List<(BlueprintData.RequiredLiquid r, ItemLiquidVolume volume)> Liquid = new List<(BlueprintData.RequiredLiquid, ItemLiquidVolume)>();

		public readonly List<(BlueprintData.RequiredPowder r, ItemWeight quantity)> Powder = new List<(BlueprintData.RequiredPowder, ItemWeight)>();
	}

	private static void Prefix(BlueprintData __instance, out Saved? __state)
	{
		__state = null;
		try
		{
			if (PrepResume.cooking == 0)
			{
				return;
			}
			InProgressCraftItem inProgressCraftItem = PrepResume.Find(__instance);
			if (inProgressCraftItem == null)
			{
				return;
			}
			Saved saved = new Saved();
			float num = Math.Clamp(inProgressCraftItem.GetPercentRemainingNormalized(), 0f, 1f);
			Il2CppReferenceArray<BlueprintData.RequiredGearItem> requiredGear = __instance.m_RequiredGear;
			if (requiredGear != null)
			{
				for (int i = 0; i < requiredGear.Length; i++)
				{
					BlueprintData.RequiredGearItem requiredGearItem = requiredGear[i];
					if (requiredGearItem != null)
					{
						saved.Gear.Add((requiredGearItem, requiredGearItem.m_Count, requiredGearItem.m_Weight));
						requiredGearItem.m_Count = __instance.NumNeedsConsuming(i, inProgressCraftItem);
						requiredGearItem.m_Weight = __instance.AmountNeedsConsuming(i, inProgressCraftItem);
					}
				}
			}
			Il2CppReferenceArray<BlueprintData.RequiredLiquid> requiredLiquid = __instance.m_RequiredLiquid;
			if (requiredLiquid != null)
			{
				foreach (BlueprintData.RequiredLiquid item in requiredLiquid)
				{
					if (item != null)
					{
						saved.Liquid.Add((item, item.m_Volume));
						item.m_Volume = ItemLiquidVolume.FromLiters(item.m_Volume.ToQuantity(1f) * num);
					}
				}
			}
			Il2CppReferenceArray<BlueprintData.RequiredPowder> requiredPowder = __instance.m_RequiredPowder;
			if (requiredPowder != null)
			{
				foreach (BlueprintData.RequiredPowder item2 in requiredPowder)
				{
					if (item2 != null)
					{
						saved.Powder.Add((item2, item2.m_Quantity));
						item2.m_Quantity = ItemWeight.FromKilograms(item2.m_Quantity.ToQuantity(1f) * num);
					}
				}
			}
			__state = saved;
		}
		catch (Exception e)
		{
			Safe.Log("Recipe resume", e);
		}
	}

	private static Exception? Finalizer(Exception? __exception, Saved? __state)
	{
		if (__state != null)
		{
			foreach (var item8 in __state.Gear)
			{
				BlueprintData.RequiredGearItem item = item8.r;
				int item2 = item8.count;
				ItemWeight item3 = item8.weight;
				item.m_Count = item2;
				item.m_Weight = item3;
			}
			foreach (var item9 in __state.Liquid)
			{
				BlueprintData.RequiredLiquid item4 = item9.r;
				ItemLiquidVolume item5 = item9.volume;
				item4.m_Volume = item5;
			}
			foreach (var item10 in __state.Powder)
			{
				BlueprintData.RequiredPowder item6 = item10.r;
				ItemWeight item7 = item10.quantity;
				item6.m_Quantity = item7;
			}
		}
		return __exception;
	}
}

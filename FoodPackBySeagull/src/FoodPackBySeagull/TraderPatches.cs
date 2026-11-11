using System;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using Il2CppTLD.Gear;
using Il2CppTLD.Trader;
using UnityEngine;

namespace FoodPackBySeagull;

[HarmonyPatch]
internal static class TraderPatches
{
	[HarmonyPostfix]
	[HarmonyPatch(typeof(TraderManager), "GetAvailableTradeExchanges")]
	private static void Available(TraderManager __instance, ref List<TradeExchangeData> __result)
	{
		try
		{
			List<TradeExchangeData> list = new List<TradeExchangeData>();
			if (__result != null)
			{
				List<TradeExchangeData>.Enumerator enumerator = __result.GetEnumerator();
				while (enumerator.MoveNext())
				{
					TradeExchangeData current = enumerator.Current;
					if (!TraderSakes.IsOurs(current))
					{
						list.Add(current);
					}
				}
			}
			TraderSakes.AddTo(__instance, list);
			__result = list;
		}
		catch (Exception e)
		{
			Safe.Log("Trader offers", e);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(TraderManager), "CompleteTrade")]
	private static void Completed(TraderManager __instance)
	{
		try
		{
			TraderSakes.MarkBought(__instance);
		}
		catch (Exception e)
		{
			Safe.Log("Trader purchase", e);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(TradeExchangeData), "GetGainedItemData")]
	private static bool Gained(TradeExchangeData __instance, ref GearItemData __result)
	{
		if (!TraderSakes.IsOurs(__instance))
		{
			return true;
		}
		__result = TraderSakes.DataOf(__instance);
		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(TradeExchangeData), "GetGainedItemIcon")]
	private static bool Icon(TradeExchangeData __instance, ref Texture2D __result)
	{
		string text = TraderSakes.SakeOf(__instance);
		if (text == null)
		{
			return true;
		}
		__result = ((Items.TryGet("GEAR_Seagull_" + text, out ItemDef def) && def.Icon != null) ? Assets.Texture(def.Icon, compress: false) : null);
		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(TradeExchangeData), "IsGainedItemValidInCurrentGamemode")]
	private static bool Valid(TradeExchangeData __instance, ref bool __result)
	{
		if (!TraderSakes.IsOurs(__instance))
		{
			return true;
		}
		__result = true;
		return false;
	}
}

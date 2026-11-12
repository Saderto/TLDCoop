using System;
using HarmonyLib;
using Il2Cpp;

namespace InventorySearchByEMPEROR;

[HarmonyPatch]
internal static class Patches
{
	[HarmonyPostfix]
	[HarmonyPatch(typeof(Panel_Inventory), "UpdateFilteredInventoryList")]
	private static void Inv(Panel_Inventory __instance)
	{
		try
		{
			Search.Filter(__instance.m_FilteredInventoryList, Search.Query);
		}
		catch (Exception e)
		{
			Once.Log("filter inv", e);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Panel_Container), "UpdateFilteredInventoryList")]
	private static void ConInv(Panel_Container __instance)
	{
		try
		{
			Search.Filter(__instance.m_FilteredInventoryList, Search.Query);
		}
		catch (Exception e)
		{
			Once.Log("filter con inv", e);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Panel_Container), "UpdateFilteredContainerList")]
	private static void ConCon(Panel_Container __instance)
	{
		try
		{
			Search.Filter(__instance.m_FilteredContainerList, Search.ConQuery);
		}
		catch (Exception e)
		{
			Once.Log("filter con", e);
		}
	}
}

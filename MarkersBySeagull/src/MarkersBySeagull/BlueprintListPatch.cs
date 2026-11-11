using System;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using Il2CppTLD.Gear;

namespace MarkersBySeagull;

[HarmonyPatch(typeof(BlueprintManager), "GetAllBlueprints")]
internal static class BlueprintListPatch
{
	private static void Postfix(List<BlueprintData> __result)
	{
		try
		{
			if (__result != null && Craft.Blueprint != null && Craft.Blueprint.name.StartsWith("BP_Seagull") && !__result.Contains(Craft.Blueprint))
			{
				__result.Add(Craft.Blueprint);
			}
		}
		catch (Exception e)
		{
			Safe.Log("Blueprints", e);
		}
	}
}

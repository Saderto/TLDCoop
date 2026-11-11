using System;
using HarmonyLib;
using Il2Cpp;

namespace MarkersBySeagull;

[HarmonyPatch(typeof(GearItem), "InstantiateGearItem", new Type[] { typeof(string) })]
internal static class InstantiatePatch
{
	private static void Prefix(ref string prefabName, out bool __state)
	{
		__state = Marker.Is(prefabName);
		if (__state)
		{
			prefabName = "GEAR_Stick";
		}
	}

	private static void Postfix(GearItem __result, bool __state)
	{
		if (!__state || __result == null)
		{
			return;
		}
		try
		{
			Marker.Customize(__result, restored: false);
		}
		catch (Exception e)
		{
			Safe.Log("Marker item", e);
		}
	}
}

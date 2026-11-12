using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace MarkersByEMPEROR;

[HarmonyPatch(typeof(GearItem), "InstantiateAndDeserializeGearItem", new Type[]
{
	typeof(string),
	typeof(string),
	typeof(Transform),
	typeof(bool),
	typeof(bool)
})]
internal static class LoadFromTextPatch
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
			Marker.Customize(__result, restored: true);
		}
		catch (Exception e)
		{
			Safe.Log("Saved marker", e);
		}
	}
}

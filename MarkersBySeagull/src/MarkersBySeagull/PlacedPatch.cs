using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace MarkersBySeagull;

[HarmonyPatch(typeof(PlayerManager), "AttemptToPlaceMesh")]
internal static class PlacedPatch
{
	private static void Prefix(PlayerManager __instance, out GearItem __state)
	{
		__state = null;
		try
		{
			GameObject objectToPlace = __instance.GetObjectToPlace();
			GearItem gearItem = ((objectToPlace != null) ? objectToPlace.GetComponent<GearItem>() : null);
			if (gearItem != null && Marker.Is(gearItem.name))
			{
				__state = gearItem;
			}
		}
		catch (Exception e)
		{
			Safe.Log("Place prefix", e);
		}
	}

	private static void Postfix(PlayerManager __instance, GearItem __state)
	{
		if (__state == null)
		{
			return;
		}
		try
		{
			bool flag = __instance.IsInMeshPlacementMode() && __instance.GetObjectToPlace() == __state.gameObject;
			Diag.Dump(__state, flag ? "placement attempt, still placing" : "placed");
			if (!flag)
			{
				Plant.ViaPlace.Add(__state.GetInstanceID());
				Hud.Track(__state);
				Plant.Force(__state);
				Diag.Dump(__state, "after planting");
			}
		}
		catch (Exception e)
		{
			Safe.Log("Place postfix", e);
		}
	}
}

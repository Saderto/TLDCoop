using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace FoodPackByEMPEROR;

[HarmonyPatch]
internal static class InspectCentre
{
	private static Transform? moved;

	private static readonly HashSet<string> told = new HashSet<string>();

	private static Transform? Model(GearItem g)
	{
		return g.GetComponentsInChildren<MeshRenderer>(includeInactive: true).FirstOrDefault((MeshRenderer r) => r.enabled && r.GetComponent<MeshFilter>()?.sharedMesh != null)?.transform;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(PlayerManager), "EnterInspectGearMode", new Type[]
	{
		typeof(GearItem),
		typeof(Container),
		typeof(IceFishingHole),
		typeof(Harvestable),
		typeof(CookingPotItem)
	})]
	private static void Enter(GearItem __0)
	{
		try
		{
			Back();
			if (__0 == null || !Items.TryGet(__0.name, out ItemDef def) || def.SingleMesh == null || !def.InspectCentred)
			{
				return;
			}
			Transform transform = Model(__0);
			Mesh mesh = ((transform != null) ? transform.GetComponent<MeshFilter>().sharedMesh : null);
			if (!(transform == null) && !(mesh == null))
			{
				Vector3 center = mesh.bounds.center;
				transform.localPosition = new Vector3(0f - center.x, 0f - center.y, 0f - center.z);
				moved = transform;
				if (told.Add(def.Id))
				{
					MelonLogger.Msg($"Inspect {def.Id}: model middle {center.x:0.000} {center.y:0.000} {center.z:0.000} brought to the origin; size {mesh.bounds.size.x:0.000} {mesh.bounds.size.y:0.000} {mesh.bounds.size.z:0.000}");
				}
			}
		}
		catch (Exception e)
		{
			Safe.Log("Inspect centre", e);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(PlayerManager), "ExitInspectGearMode")]
	private static void Exit()
	{
		Back();
	}

	private static void Back()
	{
		try
		{
			if (moved != null)
			{
				Mesh mesh = moved.GetComponent<MeshFilter>()?.sharedMesh;
				if ((object)mesh != null)
				{
					Vector3 center = mesh.bounds.center;
					moved.localPosition = new Vector3(0f - center.x, 0f, 0f - center.z);
				}
			}
		}
		catch (Exception e)
		{
			Safe.Log("Inspect centre back", e);
		}
		moved = null;
	}
}

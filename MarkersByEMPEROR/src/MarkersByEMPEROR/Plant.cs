using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace MarkersByEMPEROR;

internal static class Plant
{
	private const float Bury = 0.12f;

	private const float PlacedExtra = 0.07f;

	public static readonly HashSet<int> ViaPlace = new HashSet<int>();

	private static readonly Dictionary<int, Vector3> s_Done = new Dictionary<int, Vector3>();

	public static void Force(GearItem g)
	{
		s_Done.Remove(g.GetInstanceID());
		Maybe(g);
	}

	public static void Maybe(GearItem g)
	{
		int instanceID = g.GetInstanceID();
		Vector3 position = g.transform.position;
		if (s_Done.TryGetValue(instanceID, out var value) && (value - position).sqrMagnitude < 0.0004f)
		{
			return;
		}
		PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
		try
		{
			if (playerManagerComponent != null && playerManagerComponent.IsInspectModeActive() && playerManagerComponent.GearItemBeingInspected() == g)
			{
				return;
			}
		}
		catch
		{
		}
		bool flag = false;
		try
		{
			flag = playerManagerComponent != null && playerManagerComponent.IsInMeshPlacementMode() && playerManagerComponent.GetObjectToPlace() == g.gameObject;
		}
		catch
		{
		}
		if (flag)
		{
			if (ViaPlace.Add(instanceID))
			{
				MelonLogger.Msg("Marker is being placed.");
			}
			s_Done.Remove(instanceID);
			return;
		}
		Vector3 origin = position + Vector3.up * 1.6f;
		float num = float.MaxValue;
		Vector3 vector = Vector3.zero;
		bool flag2 = false;
		foreach (RaycastHit item in Physics.RaycastAll(origin, Vector3.down, 4f, -1, QueryTriggerInteraction.Ignore))
		{
			if (!(item.collider == null) && !item.collider.transform.IsChildOf(g.transform) && !(item.collider.GetComponentInParent<GearItem>() != null) && item.distance < num)
			{
				num = item.distance;
				vector = item.point;
				flag2 = true;
			}
		}
		if (!s_Done.ContainsKey(instanceID) && flag2 && position.y <= vector.y - 0.12f + 0.01f && position.y >= vector.y - 0.12f - 0.07f - 0.02f)
		{
			s_Done[instanceID] = position;
			return;
		}
		float y = g.transform.eulerAngles.y;
		g.transform.rotation = Quaternion.Euler(0f, y, 0f);
		if (flag2)
		{
			g.transform.position = new Vector3(position.x, vector.y - 0.12f - (ViaPlace.Contains(instanceID) ? 0.07f : 0f), position.z);
		}
		MelonLogger.Msg($"Marker planted: was y {position.y:F2}, ground {(flag2 ? vector.y.ToString("F2") : "none")}, now {g.transform.position.y:F2}, placed {ViaPlace.Contains(instanceID)}.");
		s_Done[instanceID] = g.transform.position;
	}
}

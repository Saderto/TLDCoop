using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace FoodBalanceBySeagull;

[HarmonyPatch]
internal static class SpawnChancePatch
{
	private static MethodBase target;

	private static PropertyInfo prefabName;

	private static PropertyInfo position;

	private static bool Prepare()
	{
		target = AccessTools.Method("GearSpawner.DefaultGearSpawnHandler:GetProbability");
		Type type = AccessTools.TypeByName("GearSpawner.GearSpawnInfo");
		prefabName = ((type == null) ? null : AccessTools.Property(type, "PrefabName"));
		position = ((type == null) ? null : AccessTools.Property(type, "Position"));
		if (target == null || prefabName == null || position == null)
		{
			MelonLogger.Warning("ModComponent's spawn chance not found: mods' food on maps won't change.");
		}
		if (target != null && prefabName != null)
		{
			return position != null;
		}
		return false;
	}

	private static MethodBase TargetMethod()
	{
		return target;
	}

	private static void Postfix(ref float __result, object[] __args)
	{
		try
		{
			object obj = __args[2];
			float num = FoodBalance.OnMap((string)prefabName.GetValue(obj), (Vector3)position.GetValue(obj));
			if (num != 1f)
			{
				__result = Mathf.Clamp(__result * num, 0f, 100f);
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Spawn chance: " + ex.Message);
		}
	}
}

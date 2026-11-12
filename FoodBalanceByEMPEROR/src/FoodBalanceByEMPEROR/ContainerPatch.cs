using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using MelonLoader;

namespace FoodBalanceByEMPEROR;

[HarmonyPatch(typeof(Container), "PopulateWithRandomGear")]
internal static class ContainerPatch
{
	private static void Prefix(Container __instance, out int __state)
	{
		__state = __instance.m_GearToInstantiate?.Count ?? 0;
	}

	[HarmonyPriority(0)]
	private static void Postfix(Container __instance, bool __runOriginal, int __state)
	{
		if (!__runOriginal)
		{
			return;
		}
		try
		{
			Il2CppSystem.Collections.Generic.List<string> gearToInstantiate = __instance.m_GearToInstantiate;
			if (gearToInstantiate == null)
			{
				return;
			}
			int gameRandomSeed = GameManager.m_SceneTransitionData.m_GameRandomSeed;
			string value = FoodBalance.Cm(__instance.transform.position);
			System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
			int num = 0;
			for (int num2 = gearToInstantiate.Count - 1; num2 >= __state; num2--)
			{
				string text = gearToInstantiate[num2];
				float num3 = FoodBalance.InContainer(text);
				if (num3 != 1f)
				{
					float num4 = FoodBalance.Roll($"{FoodBalance.Text(gameRandomSeed)}|{value}|{FoodBalance.Text(num2)}|balance");
					if (num3 < 1f && num4 >= num3)
					{
						gearToInstantiate.RemoveAt(num2);
						num++;
					}
					else if (num3 > 1f && num4 < num3 - 1f)
					{
						list.Add(text);
					}
				}
			}
			foreach (string item in list)
			{
				gearToInstantiate.Add(item);
			}
			if (num > 0 || list.Count > 0)
			{
				MelonLogger.Msg($"Container at {value}: removed {num}, added {list.Count}");
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Container: " + ex);
		}
	}
}

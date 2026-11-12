using System;
using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(PlayerManager), "AddItemCONSOLE")]
internal static class ConsoleAddPatch
{
	private static bool Prefix(PlayerManager __instance, string __0, int __1, float __2, ref GearItem __result)
	{
		if (!Items.TryGet(__0, out ItemDef def))
		{
			return true;
		}
		__result = null;
		for (int i = 0; i < Math.Max(1, __1); i++)
		{
			__result = __instance.InstantiateItemInPlayerInventory("GEAR_Seagull_" + def.Id, 1, __2);
		}
		return false;
	}
}

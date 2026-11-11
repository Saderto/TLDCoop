using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.IntBackedUnit;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(Encumber), "GetEffectiveCarryCapacityKG")]
internal static class CarryHook
{
	private static void Postfix(ref ItemWeight __result)
	{
		float num = Fx.V("Bloat");
		if (num > 0f)
		{
			__result -= ItemWeight.FromKilograms(num);
		}
	}
}

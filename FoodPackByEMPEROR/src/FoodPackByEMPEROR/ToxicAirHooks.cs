using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch]
internal static class ToxicAirHooks
{
	[HarmonyPostfix]
	[HarmonyPatch(typeof(RespiratorManager), "IsProtectionActive")]
	private static void Protection(ref bool __result)
	{
		if (Fx.On("ClearBreath"))
		{
			__result = true;
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(ToxicFog), "Update")]
	private static bool Fog()
	{
		return !Fx.On("ClearBreath");
	}
}

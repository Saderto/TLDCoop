using System;
using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch]
internal static class CabinFeverGuard
{
	private static bool tracking;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(CabinFever), "UpdateCabinFeverTimeTracker")]
	private static void Before()
	{
		tracking = true;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(CabinFever), "UpdateCabinFeverTimeTracker")]
	private static void After()
	{
		tracking = false;
	}

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(CabinFever), "UpdateCabinFeverTimeTracker")]
	private static Exception? Always(Exception? __exception)
	{
		tracking = false;
		return __exception;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(PlayerManager), "InHibernationPreventionIndoorEnvironment")]
	private static bool Indoors(ref bool __result)
	{
		if (!tracking || !Fx.On("IrishSpirit"))
		{
			return true;
		}
		__result = false;
		return false;
	}
}

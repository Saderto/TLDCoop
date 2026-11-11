using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch]
internal static class SprainGuard
{
	private const string Fall = "GAMEPLAY_Fall";

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Sprains), "MaybeSprainWhileMoving")]
	private static bool Moving()
	{
		return !Fx.On("IrishSpirit");
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(FallDamage), "MaybeSprainAnkle")]
	private static bool FallAnkle(ref bool __result)
	{
		return Roll(ref __result);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(FallDamage), "MaybeSprainWrist")]
	private static bool FallWrist(ref bool __result)
	{
		return Roll(ref __result);
	}

	private static bool Roll(ref bool sprained)
	{
		if (!Fx.On("IrishSpirit"))
		{
			return true;
		}
		sprained = false;
		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SprainedAnkle), "SprainedAnkleStart")]
	private static bool Ankle(string causeID)
	{
		if (causeID == "GAMEPLAY_Fall")
		{
			return !Fx.On("IrishSpirit");
		}
		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SprainedWrist), "SprainedWristStart")]
	private static bool Wrist(string causeId)
	{
		if (causeId == "GAMEPLAY_Fall")
		{
			return !Fx.On("IrishSpirit");
		}
		return true;
	}
}

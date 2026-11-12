using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(Freezing), "UpdateFreezingStatus")]
internal static class ColdBarHook
{
	private static void Prefix(Freezing __instance, out float __state)
	{
		__state = __instance.m_CurrentFreezing;
	}

	private static void Postfix(Freezing __instance, float __state)
	{
		float num = __instance.m_CurrentFreezing - __state;
		if (!(num <= 0f))
		{
			float num2 = Fx.Less("PolarHardening", "RiceWarmth", "StrongHealth", "IrishSpirit");
			PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
			Rest restComponent = GameManager.GetRestComponent();
			if (playerManagerComponent != null && !playerManagerComponent.PlayerIsWalking())
			{
				num2 *= Fx.Less("Stillness");
			}
			if (restComponent != null && restComponent.IsSleeping())
			{
				num2 *= Fx.Less("WarmSleep");
			}
			__instance.m_CurrentFreezing = __state + num * num2;
		}
	}
}

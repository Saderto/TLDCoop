using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(PlayerManager), "UpdateCalorieBurnRate")]
internal static class CalorieBurnPatch
{
	public static float BurnWithoutEffects;

	private static void Postfix()
	{
		Hunger hungerComponent = GameManager.GetHungerComponent();
		if (!(hungerComponent == null))
		{
			BurnWithoutEffects = hungerComponent.m_CurrentCalorieBurnPerHour;
			hungerComponent.m_CurrentCalorieBurnPerHour *= Fx.Less("LongFullness", "StrongHealth", "Aged") * (1f - Fx.V2("Tireless"));
		}
	}
}

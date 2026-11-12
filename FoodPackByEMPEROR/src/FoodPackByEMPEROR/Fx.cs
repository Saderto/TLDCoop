using Il2Cpp;

namespace FoodPackByEMPEROR;

internal static class Fx
{
	public static float Now => GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused();

	public static float V(string id)
	{
		return ActiveEffects.Value(id);
	}

	public static bool On(string id)
	{
		return ActiveEffects.Value(id) > 0f;
	}

	public static float Less(params string[] ids)
	{
		float num = 1f;
		foreach (string id in ids)
		{
			num *= 1f - V(id);
		}
		return num;
	}

	public static float More(params string[] ids)
	{
		float num = 1f;
		foreach (string id in ids)
		{
			num *= 1f + V(id);
		}
		return num;
	}

	public static float V2(string id)
	{
		if (On(id))
		{
			EffectDef effectDef = Data.Effect(id);
			if (effectDef != null)
			{
				return effectDef.Value2 ?? effectDef.Value;
			}
		}
		return 0f;
	}
}

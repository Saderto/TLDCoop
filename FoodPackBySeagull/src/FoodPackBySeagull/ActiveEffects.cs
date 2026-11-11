using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;

namespace FoodPackBySeagull;

public static class ActiveEffects
{
	private static readonly Dictionary<string, (float until, float value, float share)> running = new Dictionary<string, (float, float, float)>();

	public static void Set(string id, float until, float value, float share)
	{
		running[id] = (until, value, share);
	}

	public static void Remove(string id)
	{
		running.Remove(id);
	}

	public static void Clear()
	{
		running.Clear();
	}

	public static IEnumerable<(string id, float value, float hoursLeft, float share)> Running()
	{
		float now = GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused();
		return (from e in running
			where now < e.Value.until
			select (Key: e.Key, value: e.Value.value, e.Value.until - now, share: e.Value.share)).ToList();
	}

	public static float Value(string id, float fallback = 0f)
	{
		if (!running.TryGetValue(id, out (float, float, float) value))
		{
			return fallback;
		}
		try
		{
			return (GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused() < value.Item1) ? value.Item2 : fallback;
		}
		catch (NullReferenceException)
		{
			return fallback;
		}
	}
}

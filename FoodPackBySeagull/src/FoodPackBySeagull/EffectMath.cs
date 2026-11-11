using System;

namespace FoodPackBySeagull;

public static class EffectMath
{
	public struct Running
	{
		public string Cause;

		public float End;

		public float Share;

		public float Full;

		public float Value;
	}

	public static Running Add(Running b, float now, string cause, float hours, float full, float share, float value)
	{
		if (b.Cause == cause)
		{
			b.Share = Math.Min(1f, b.Share + share);
			b.End = Math.Min(now + full, Math.Max(b.End, now) + hours);
			b.Value = value;
		}
		else if (now + hours > b.End)
		{
			b.Cause = cause;
			b.Share = share;
			b.Full = full;
			b.End = now + hours;
			b.Value = value;
		}
		return b;
	}

	public static float HeadacheBite(ref float crumbs, ref float lastBite, float now, float share, bool hurting)
	{
		if (now - lastBite > 2f)
		{
			crumbs = 0f;
		}
		crumbs += share;
		lastBite = now;
		if (crumbs < 0.1f && !hurting)
		{
			return 0f;
		}
		float result = crumbs;
		crumbs = 0f;
		return result;
	}

	public static float HeadacheHours(float bite, float left, float full)
	{
		return Math.Min(full, bite + Math.Max(0f, left));
	}

	public static bool WholePortion(ref float saved, float share)
	{
		saved += share;
		bool num = saved >= 0.999f;
		if (num)
		{
			saved = 0f;
		}
		return num;
	}

	public static float AlcoholDose(float liters, float gameScalePercent, float share)
	{
		return liters * gameScalePercent * 0.01f * share;
	}
}

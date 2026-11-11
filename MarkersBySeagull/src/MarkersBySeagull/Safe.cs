using System;
using System.Collections.Generic;
using MelonLoader;

namespace MarkersBySeagull;

internal static class Safe
{
	private static readonly HashSet<string> seen = new HashSet<string>();

	public static void Log(string where, Exception e)
	{
		if (seen.Add(where))
		{
			MelonLogger.Error($"{where}: {e}");
		}
	}
}

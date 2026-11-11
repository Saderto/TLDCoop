using System;
using System.Collections.Generic;

namespace InventorySearchBySeagull;

internal static class Once
{
	private static readonly HashSet<string> seen = new HashSet<string>();

	public static void Log(string where, Exception e)
	{
		if (seen.Add(where))
		{
			Mod.Log.Error(where + ": " + e);
		}
	}
}

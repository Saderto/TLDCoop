using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;

namespace MarkersBySeagull;

[HarmonyPatch(typeof(ConsoleManager), "StoreAutoCompleteGearNames")]
internal static class ConsoleNames
{
	private static void Postfix()
	{
		Fill();
	}

	public static void Fill()
	{
		try
		{
			Dictionary<string, string> searchStringToGearNames = ConsoleManager.m_SearchStringToGearNames;
			if (searchStringToGearNames == null)
			{
				return;
			}
			string[] array = new string[2]
			{
				"GEAR_SeagullMarker_TrailMarker".ToLowerInvariant(),
				"GEAR_SeagullMarker_TrailMarker".Substring(5).ToLowerInvariant()
			};
			foreach (string key in array)
			{
				if (!searchStringToGearNames.ContainsKey(key))
				{
					searchStringToGearNames[key] = "GEAR_SeagullMarker_TrailMarker";
				}
			}
		}
		catch (Exception e)
		{
			Safe.Log("Console names", e);
		}
	}
}

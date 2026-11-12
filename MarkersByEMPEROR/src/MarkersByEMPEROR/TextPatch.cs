using System;
using HarmonyLib;
using Il2Cpp;

namespace MarkersByEMPEROR;

[HarmonyPatch(typeof(Localization), "Get", new Type[] { typeof(string) })]
internal static class TextPatch
{
	private static void Postfix(string key, ref string __result)
	{
		if (key != null && key.StartsWith("MARKERS_", StringComparison.Ordinal))
		{
			__result = Texts.Get(key);
		}
	}
}

using System;
using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(Localization), "Get", new Type[] { typeof(string) })]
internal static class TextPatch
{
	private static void Postfix(string key, ref string __result)
	{
		if (key != null && key.StartsWith("SEAGULL_", StringComparison.Ordinal))
		{
			if (key.StartsWith("SEAGULL_FXTIP_", StringComparison.Ordinal))
			{
				string text = key.Substring("SEAGULL_FXTIP_".Length);
				string text2 = Texts.Get("SEAGULL_FX_" + text + "_DESC");
				__result = Texts.Get("SEAGULL_FX_" + text + "_NAME") + (text2.StartsWith("SEAGULL_", StringComparison.Ordinal) ? "" : ("\n" + text2));
			}
			else
			{
				__result = Texts.Get(key);
			}
		}
	}
}

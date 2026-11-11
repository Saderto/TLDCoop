using HarmonyLib;
using Il2Cpp;

namespace ContainerLabelsBySeagull;

[HarmonyPatch]
internal static class Patches
{
	[HarmonyPostfix]
	[HarmonyPatch(typeof(ContainerInteraction), "GetHoverText")]
	private static void Hover(ContainerInteraction __instance, ref string __result)
	{
		try
		{
			Entry entry = Labels.Get(__instance.m_Container ?? __instance.GetComponent<Container>());
			if (entry != null)
			{
				string text = __result ?? "";
				int num = text.IndexOf('\n');
				string text2 = ((num >= 0) ? text.Substring(0, num) : text);
				string text3 = ((num >= 0) ? text.Substring(num) : "");
				__result = text2 + " - " + Palette.Paint(entry.Text, entry.Color) + text3;
			}
		}
		catch
		{
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Panel_HUD), "Update")]
	private static void HudEncoding(Panel_HUD __instance)
	{
		try
		{
			UILabel label_ObjectName = __instance.m_Label_ObjectName;
			if (label_ObjectName != null && !label_ObjectName.supportEncoding)
			{
				label_ObjectName.supportEncoding = true;
			}
		}
		catch
		{
		}
	}
}

using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gear;
using MelonLoader;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(CraftingOperation), "StartCrafting")]
internal static class PrepResumeStart
{
	private static void Prefix(BlueprintData blueprintData, ref InProgressCraftItem workInProgress, ref float realTimeCraftingDuration, ref float hoursToSpendCrafting)
	{
		try
		{
			if (PrepResume.cooking != 0 && !(workInProgress != null))
			{
				InProgressCraftItem inProgressCraftItem = PrepResume.Find(blueprintData);
				if (!(inProgressCraftItem == null))
				{
					float num = Math.Clamp(inProgressCraftItem.GetPercentRemainingNormalized(), 0.01f, 1f);
					workInProgress = inProgressCraftItem;
					realTimeCraftingDuration *= num;
					hoursToSpendCrafting *= num;
					MelonLogger.Msg($"Recipe resume: {blueprintData.name} from {(1f - num) * 100f:0}%");
				}
			}
		}
		catch (Exception e)
		{
			Safe.Log("Recipe resume", e);
		}
	}
}

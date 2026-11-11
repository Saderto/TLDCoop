using Il2Cpp;
using Il2CppTLD.Gear;

namespace FoodPackBySeagull;

internal static class PrepResume
{
	internal const bool AllRecipes = true;

	internal static int cooking;

	internal static InProgressCraftItem? Find(BlueprintData? bp)
	{
		if (bp == null)
		{
			return null;
		}
		Inventory inventoryComponent = GameManager.GetInventoryComponent();
		if (inventoryComponent == null)
		{
			return null;
		}
		InProgressCraftItem inProgressItem = bp.GetInProgressItem(inventoryComponent, null);
		if (inProgressItem == null || !inProgressItem.ProgressHasStarted() || inProgressItem.IsProgressComplete())
		{
			return null;
		}
		return inProgressItem;
	}
}

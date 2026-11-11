using System;
using Il2Cpp;

namespace FoodPackBySeagull;

public static class DishWarming
{
	private static int warmingDepth;

	private static int recipeDepth;

	private const int Unfinished = -1000000;

	public static void Enter()
	{
		warmingDepth++;
	}

	public static void Leave()
	{
		warmingDepth = Math.Max(0, warmingDepth - 1);
	}

	public static void EnterRecipe()
	{
		recipeDepth++;
	}

	public static void LeaveRecipe()
	{
		recipeDepth = Math.Max(0, recipeDepth - 1);
	}

	private static bool Ours(GearItem g)
	{
		if (Items.TryGet(g.name, out ItemDef def))
		{
			return Recipes.IsCookedDish(def.Id);
		}
		return false;
	}

	public static void TakenOutUnfinished(GearItem? g, float percent)
	{
		if (!(g?.m_FoodItem == null) && Ours(g))
		{
			g.m_FoodItem.m_NumTimesHeatedUp = -1000000 - (int)Math.Round(Math.Max(0f, percent) * 1000f);
		}
	}

	public static bool IsUnfinished(GearItem g, out float percent)
	{
		percent = 0f;
		int num = ((g.m_FoodItem != null) ? g.m_FoodItem.m_NumTimesHeatedUp : 0);
		if (num > -1000000)
		{
			return false;
		}
		percent = (float)(-1000000 - num) / 1000f;
		return true;
	}

	public static void WentIn(CookingPotItem pot, GearItem? g)
	{
		if (!(g == null) && Ours(g))
		{
			if (IsUnfinished(g, out var percent))
			{
				g.m_BeenInPlayerInventory = false;
				g.m_FoodItem.m_NumTimesHeatedUp = 0;
				float num = (g.GetComponent<Cookable>()?.m_CookTimeMinutes ?? 0f) * pot.GetTotalCookMultiplier();
				pot.m_CookingElapsedHours = Math.Min(percent, 0.99f) * num / 60f;
				pot.m_PercentCooked = Math.Min(percent, 0.99f);
			}
			else if (recipeDepth > 0)
			{
				g.m_BeenInPlayerInventory = false;
			}
			else if (warmingDepth > 0)
			{
				g.m_BeenInPlayerInventory = true;
			}
		}
	}

	public static bool Warming(GearItem g)
	{
		if (Ours(g))
		{
			return g.m_BeenInPlayerInventory;
		}
		return false;
	}

	public static bool Made(GearItem g)
	{
		if (Ours(g))
		{
			return !g.m_BeenInPlayerInventory;
		}
		return false;
	}
}

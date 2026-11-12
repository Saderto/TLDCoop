using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;
using Il2CppTLD.Cooking;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;

namespace FoodPackByEMPEROR;

public static class Recipes
{
	private static System.Collections.Generic.List<RecipeDef> defs = new System.Collections.Generic.List<RecipeDef>();

	private static bool added;

	public static readonly System.Collections.Generic.Dictionary<string, RecipeData> Added = new System.Collections.Generic.Dictionary<string, RecipeData>();

	private static readonly System.Collections.Generic.Dictionary<string, GearItem> ingredients = new System.Collections.Generic.Dictionary<string, GearItem>();

	private static RecipeBook? pending;

	public static readonly System.Collections.Generic.Dictionary<string, (BlueprintData bp, int level)> Crafts = new System.Collections.Generic.Dictionary<string, (BlueprintData, int)>();

	public static readonly System.Collections.Generic.Dictionary<string, string> CraftDish = new System.Collections.Generic.Dictionary<string, string>();

	public static string? DishOf(RecipeData? r)
	{
		if (r == null || r.name == null || !r.name.StartsWith("RECIPE_Seagull_", StringComparison.Ordinal))
		{
			return null;
		}
		string id = r.name.Substring("RECIPE_Seagull_".Length);
		return defs.FirstOrDefault((RecipeDef d) => d.Id == id)?.Dish;
	}

	public static bool IsCookedDish(string id)
	{
		string id2 = id;
		return defs.Any((RecipeDef d) => !d.Craft && d.Dish == id2);
	}

	public static RecipeDef? Def(string id)
	{
		string id2 = id;
		return defs.FirstOrDefault((RecipeDef d) => d.Id == id2);
	}

	public static void Load(string root)
	{
		string path = Path.Combine(root, "data", "recipes.json");
		if (File.Exists(path))
		{
			defs = JsonConvert.DeserializeObject<System.Collections.Generic.List<RecipeDef>>(File.ReadAllText(path)) ?? new System.Collections.Generic.List<RecipeDef>();
		}
	}

	private static GearItem Ingredient(string gear)
	{
		if (ingredients.TryGetValue(gear, out GearItem value))
		{
			return value;
		}
		GearItem gearItem = GearItem.InstantiateGearItem(gear);
		if (gearItem == null)
		{
			throw new InvalidOperationException("ingredient not found: " + gear + " (is its DLC missing?)");
		}
		Items.Hide(gearItem);
		return ingredients[gear] = gearItem;
	}

	public static void Later(RecipeBook book)
	{
		pending = book;
	}

	public static void Tick()
	{
		if (pending == null)
		{
			return;
		}
		RecipeBook book = pending;
		pending = null;
		try
		{
			AddAll(book);
		}
		catch (Exception e)
		{
			Safe.Log("Recipes", e);
		}
	}

	public static void AddAll(RecipeBook book)
	{
		if (added)
		{
			foreach (RecipeData value in Added.Values)
			{
				if (!book.AllRecipes.Contains(value))
				{
					book.AllRecipes.Add(value);
				}
			}
			return;
		}
		added = true;
		RecipeData recipeData = null;
		RecipeData recipeData2 = null;
		PowderType powderType = null;
		PowderType powderType2 = null;
		LiquidType liquidType = null;
		LiquidType liquidType2 = null;
		Il2CppSystem.Collections.Generic.List<RecipeData>.Enumerator enumerator2 = book.AllRecipes.GetEnumerator();
		while (enumerator2.MoveNext())
		{
			RecipeData current2 = enumerator2.Current;
			if (current2 == null)
			{
				continue;
			}
			if (current2.name == "RECIPE_StewMeat" && (object)recipeData == null)
			{
				recipeData = current2;
			}
			if (current2.name == "RECIPE_Pancake" && (object)recipeData2 == null)
			{
				recipeData2 = current2;
			}
			BlueprintData dishBlueprint = current2.m_DishBlueprint;
			if (dishBlueprint == null)
			{
				continue;
			}
			if (dishBlueprint.m_RequiredPowder != null)
			{
				foreach (BlueprintData.RequiredPowder item in dishBlueprint.m_RequiredPowder)
				{
					if (item?.m_Powder != null)
					{
						if (item.m_Powder.name == "POWDER_Flour" && (object)powderType == null)
						{
							powderType = item.m_Powder;
						}
						if (item.m_Powder.name == "POWDER_Salt" && (object)powderType2 == null)
						{
							powderType2 = item.m_Powder;
						}
					}
				}
			}
			if (dishBlueprint.m_RequiredLiquid == null)
			{
				continue;
			}
			foreach (BlueprintData.RequiredLiquid item2 in dishBlueprint.m_RequiredLiquid)
			{
				if (item2?.m_Liquid != null)
				{
					if (item2.m_Liquid.name == "LIQUID_WaterPotable" && (object)liquidType == null)
					{
						liquidType = item2.m_Liquid;
					}
					if (item2.m_Liquid.name == "LIQUID_CookingOil" && (object)liquidType2 == null)
					{
						liquidType2 = item2.m_Liquid;
					}
				}
			}
		}
		if (liquidType == null || liquidType2 == null || powderType == null || powderType2 == null)
		{
			foreach (RecipeData item3 in from x in book.AllRecipes.ToArray()
				orderby (!(x != null) || !x.name.StartsWith("RECIPE_")) ? 1 : 0
				select x)
			{
				BlueprintData blueprintData = item3?.m_DishBlueprint;
				if (blueprintData == null)
				{
					continue;
				}
				if (blueprintData.m_RequiredPowder != null)
				{
					foreach (BlueprintData.RequiredPowder item4 in blueprintData.m_RequiredPowder)
					{
						if (item4?.m_Powder != null)
						{
							if (item4.m_Powder.name.Contains("Flour") && (object)powderType == null)
							{
								powderType = item4.m_Powder;
							}
							if (item4.m_Powder.name.Contains("Salt") && (object)powderType2 == null)
							{
								powderType2 = item4.m_Powder;
							}
						}
					}
				}
				if (blueprintData.m_RequiredLiquid == null)
				{
					continue;
				}
				foreach (BlueprintData.RequiredLiquid item5 in blueprintData.m_RequiredLiquid)
				{
					if (item5?.m_Liquid != null)
					{
						if (item5.m_Liquid.name.Contains("WaterPotable") && (object)liquidType == null)
						{
							liquidType = item5.m_Liquid;
						}
						if (item5.m_Liquid.name.Contains("CookingOil") && (object)liquidType2 == null)
						{
							liquidType2 = item5.m_Liquid;
						}
					}
				}
			}
		}
		MelonLogger.Msg($"Recipe models: {recipeData?.name}, {recipeData2?.name}; {liquidType?.name}, {liquidType2?.name}, {powderType?.name}, {powderType2?.name}");
		if (recipeData == null || recipeData2 == null)
		{
			MelonLogger.Error("The game's model recipes weren't found: the mod's recipes are skipped.");
			return;
		}
		System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
		foreach (RecipeDef def in defs)
		{
			if (def.Craft)
			{
				continue;
			}
			try
			{
				RecipeData recipeData3 = UnityEngine.Object.Instantiate(def.Pan ? recipeData2 : recipeData);
				recipeData3.name = "RECIPE_Seagull_" + def.Id;
				recipeData3.hideFlags = HideFlags.HideAndDontSave;
				string localizationID = (def.Named ? ("SEAGULL_REC_" + def.Id + "_NAME") : ("SEAGULL_" + def.Dish + "_NAME"));
				recipeData3.m_RecipeName = new LocalizedString
				{
					m_LocalizationID = localizationID
				};
				recipeData3.m_RecipeShortName = new LocalizedString
				{
					m_LocalizationID = localizationID
				};
				recipeData3.m_RecipeDescription = new LocalizedString
				{
					m_LocalizationID = "SEAGULL_" + def.Dish + "_DESC"
				};
				recipeData3.m_RequiredSkillLevel = def.Skill;
				recipeData3.m_UnlockRule = (def.Card ? RecipeData.UnlockType.CodeUnlock : RecipeData.UnlockType.Unlocked);
				BlueprintData blueprintData2 = UnityEngine.Object.Instantiate(recipeData3.m_DishBlueprint);
				blueprintData2.name = "BP_Seagull_" + def.Id;
				blueprintData2.hideFlags = HideFlags.HideAndDontSave;
				blueprintData2.m_DurationMinutes = def.Minutes;
				blueprintData2.m_RequiresLitFire = true;
				GearItem gearItem = Items.Template(def.Dish) ?? throw new InvalidOperationException("dish not found: " + def.Dish);
				if ((def.Water > 0f && liquidType == null) || (def.Oil > 0f && liquidType2 == null) || (def.FlourKg > 0f && powderType == null) || (def.SaltKg > 0f && powderType2 == null))
				{
					throw new InvalidOperationException("the game's water, oil, flour or salt wasn't found");
				}
				blueprintData2.SetCraftedResult(gearItem);
				recipeData3.m_CookedGearItemData = gearItem.m_GearItemData;
				Il2CppReferenceArray<BlueprintData.RequiredGearItem> il2CppReferenceArray = new Il2CppReferenceArray<BlueprintData.RequiredGearItem>(def.Gear.Count);
				for (int i = 0; i < def.Gear.Count; i++)
				{
					IngredientDef ingredientDef = def.Gear[i];
					il2CppReferenceArray[i] = new BlueprintData.RequiredGearItem
					{
						m_Item = Ingredient(ingredientDef.Gear),
						m_Count = ingredientDef.Count,
						m_Units = ((ingredientDef.Kg > 0f) ? BlueprintData.RequiredGearItem.Units.Kilograms : BlueprintData.RequiredGearItem.Units.Count),
						m_Weight = ItemWeight.FromKilograms(ingredientDef.Kg)
					};
				}
				blueprintData2.m_RequiredGear = il2CppReferenceArray;
				System.Collections.Generic.List<BlueprintData.RequiredLiquid> list2 = new System.Collections.Generic.List<BlueprintData.RequiredLiquid>();
				if (def.Water > 0f && liquidType != null)
				{
					list2.Add(new BlueprintData.RequiredLiquid
					{
						m_Liquid = liquidType,
						m_Volume = ItemLiquidVolume.FromLiters(def.Water)
					});
				}
				if (def.Oil > 0f && liquidType2 != null)
				{
					list2.Add(new BlueprintData.RequiredLiquid
					{
						m_Liquid = liquidType2,
						m_Volume = ItemLiquidVolume.FromLiters(def.Oil)
					});
				}
				blueprintData2.m_RequiredLiquid = new Il2CppReferenceArray<BlueprintData.RequiredLiquid>(list2.ToArray());
				System.Collections.Generic.List<BlueprintData.RequiredPowder> list3 = new System.Collections.Generic.List<BlueprintData.RequiredPowder>();
				if (def.FlourKg > 0f && powderType != null)
				{
					list3.Add(new BlueprintData.RequiredPowder
					{
						m_Powder = powderType,
						m_Quantity = ItemWeight.FromKilograms(def.FlourKg)
					});
				}
				if (def.SaltKg > 0f && powderType2 != null)
				{
					list3.Add(new BlueprintData.RequiredPowder
					{
						m_Powder = powderType2,
						m_Quantity = ItemWeight.FromKilograms(def.SaltKg)
					});
				}
				blueprintData2.m_RequiredPowder = new Il2CppReferenceArray<BlueprintData.RequiredPowder>(list3.ToArray());
				recipeData3.m_DishBlueprint = blueprintData2;
				if (def.AnyPot)
				{
					recipeData3.m_AllowedCookingPots = null;
				}
				book.AllRecipes.Add(recipeData3);
				Added[def.Id] = recipeData3;
			}
			catch (Exception ex)
			{
				MelonLogger.Error("Recipe " + def.Id + " failed: " + ex.Message);
				list.Add(def.Id + " (" + ex.Message + ")");
			}
		}
		MelonLogger.Msg($"Added {Added.Count} recipes of {defs.Count((RecipeDef x) => !x.Craft)}.");
		if (list.Count > 0)
		{
			MelonLogger.Warning("Recipes not added: " + string.Join("; ", list));
		}
		BuildCrafts();
	}

	private static void BuildCrafts()
	{
		Il2CppSystem.Collections.Generic.List<BlueprintData> list = BlueprintManager.Instance?.GetAllBlueprints();
		BlueprintData blueprintData = null;
		if (list != null)
		{
			for (int i = 0; i < 2; i++)
			{
				if (!(blueprintData == null))
				{
					break;
				}
				Il2CppSystem.Collections.Generic.List<BlueprintData>.Enumerator enumerator = list.GetEnumerator();
				while (enumerator.MoveNext())
				{
					BlueprintData b = enumerator.Current;
					if (b != null && (i == 1 || b.name.StartsWith("BLUEPRINT_")) && b.m_RequiredCraftingLocation == CraftingLocation.Anywhere && !b.m_RequiresLitFire && !Crafts.Values.Any<(BlueprintData, int)>(((BlueprintData bp, int level) c) => c.bp == b))
					{
						blueprintData = b;
						break;
					}
				}
			}
		}
		if (blueprintData == null)
		{
			MelonLogger.Error("No game blueprint to copy: the crafting-menu recipes are skipped.");
			return;
		}
		foreach (RecipeDef item in defs.Where((RecipeDef x) => x.Craft))
		{
			try
			{
				BlueprintData blueprintData2 = UnityEngine.Object.Instantiate(blueprintData);
				blueprintData2.name = "BP_Seagull_Craft_" + item.Id;
				blueprintData2.hideFlags = HideFlags.HideAndDontSave;
				GearItem craftedResult = Items.Template(item.Dish) ?? throw new InvalidOperationException("dish not found: " + item.Dish);
				blueprintData2.SetCraftedResult(craftedResult);
				blueprintData2.m_CraftedResultCount = 1;
				blueprintData2.m_DurationMinutes = item.Minutes;
				blueprintData2.m_RequiredCraftingLocation = CraftingLocation.Anywhere;
				blueprintData2.m_RequiresLitFire = false;
				blueprintData2.m_AppliedSkill = SkillType.Cooking;
				blueprintData2.m_ImprovedSkill = SkillType.Cooking;
				blueprintData2.m_RequiredTool = null;
				blueprintData2.m_OptionalTools = new Il2CppReferenceArray<ToolsItem>(0L);
				blueprintData2.m_Locked = false;
				blueprintData2.m_IgnoreLockInSurvival = true;
				blueprintData2.m_AppearsInStoryOnly = false;
				blueprintData2.m_AppearsInSurvivalOnly = false;
				blueprintData2.m_XPModesToDisable?.Clear();
				blueprintData2.m_UsesPhoto = false;
				blueprintData2.m_RequiresLight = false;
				Il2CppReferenceArray<BlueprintData.RequiredGearItem> il2CppReferenceArray = new Il2CppReferenceArray<BlueprintData.RequiredGearItem>(item.Gear.Count);
				for (int j = 0; j < item.Gear.Count; j++)
				{
					IngredientDef ingredientDef = item.Gear[j];
					il2CppReferenceArray[j] = new BlueprintData.RequiredGearItem
					{
						m_Item = Ingredient(ingredientDef.Gear),
						m_Count = ingredientDef.Count,
						m_Units = ((ingredientDef.Kg > 0f) ? BlueprintData.RequiredGearItem.Units.Kilograms : BlueprintData.RequiredGearItem.Units.Count),
						m_Weight = ItemWeight.FromKilograms(ingredientDef.Kg)
					};
				}
				blueprintData2.m_RequiredGear = il2CppReferenceArray;
				blueprintData2.m_RequiredLiquid = new Il2CppReferenceArray<BlueprintData.RequiredLiquid>(0L);
				blueprintData2.m_RequiredPowder = new Il2CppReferenceArray<BlueprintData.RequiredPowder>(0L);
				Crafts[item.Id] = (blueprintData2, item.Skill);
				CraftDish[blueprintData2.name] = item.Dish;
				if (list != null && !list.Contains(blueprintData2))
				{
					list.Add(blueprintData2);
				}
			}
			catch (Exception ex)
			{
				MelonLogger.Error("Crafting recipe " + item.Id + " failed: " + ex.Message);
			}
		}
		MelonLogger.Msg($"Added {Crafts.Count} crafting recipes (copied from {blueprintData.name}).");
	}

	public static System.Collections.Generic.IEnumerable<RecipeData> ForDish(string dish)
	{
		string dish2 = dish;
		return from d in defs
			where d.Dish == dish2 && Added.ContainsKey(d.Id)
			select Added[d.Id];
	}
}

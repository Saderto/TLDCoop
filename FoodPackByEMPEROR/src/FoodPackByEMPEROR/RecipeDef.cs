using System.Collections.Generic;

namespace FoodPackByEMPEROR;

public class RecipeDef
{
	public string Id = "";

	public string Dish = "";

	public int Skill = 1;

	public int Minutes = 60;

	public bool Pan;

	public bool NoFire;

	public bool Card;

	public bool Named;

	public bool Craft;

	public bool AnyPot;

	public List<IngredientDef> Gear = new List<IngredientDef>();

	public float Water;

	public float Oil;

	public float FlourKg;

	public float SaltKg;
}

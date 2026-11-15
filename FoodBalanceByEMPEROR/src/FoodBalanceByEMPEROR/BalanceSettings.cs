using ModSettings;

namespace FoodBalanceByEMPEROR;

internal class BalanceSettings : JsonModSettings
{
	[Section("FOODBALANCE_SECTION_ALL", Localize = true)]
	[Name("FOODBALANCE_GENERAL", Localize = true)]
	// ModSettings 2.2.5 shows the raw text when Localize = true (its check is inverted), so keys go
	// without it: then ModSettings calls Localization.Get, and Words supplies the text.
	[Description("FOODBALANCE_GENERAL_DESC")]
	[Slider(0f, 200f, 21)]
	public float General = 100f;

	[Section("FOODBALANCE_SECTION_MODS", Localize = true)]
	[Name("FOODBALANCE_FOODPACK", Localize = true)]
	[Description("FOODBALANCE_MOD_DESC")]
	[Slider(0f, 200f, 21)]
	public float FoodPack = 100f;

	[Name("FOODBALANCE_WALTZ", Localize = true)]
	[Description("FOODBALANCE_MOD_DESC")]
	[Slider(0f, 200f, 21)]
	public float CalorieWaltz = 100f;

	[Name("FOODBALANCE_RETRO", Localize = true)]
	[Description("FOODBALANCE_MOD_DESC")]
	[Slider(0f, 200f, 21)]
	public float RetroFood = 100f;

	[Name("FOODBALANCE_AUTUMN", Localize = true)]
	[Description("FOODBALANCE_MOD_DESC")]
	[Slider(0f, 200f, 21)]
	public float AutumnComforts = 100f;

	[Name("FOODBALANCE_SEAGULL", Localize = true)]
	[Description("FOODBALANCE_SEAGULL_DESC")]
	[Slider(0f, 200f, 21)]
	public float Seagull = 100f;

	[Name("FOODBALANCE_DRINKS", Localize = true)]
	[Description("FOODBALANCE_DRINKS_DESC")]
	[Slider(0f, 200f, 21)]
	public float Drinks = 100f;

	[Name("FOODBALANCE_SNACKS", Localize = true)]
	[Description("FOODBALANCE_SNACKS_DESC")]
	[Slider(0f, 200f, 21)]
	public float Snacks = 100f;

	[Name("FOODBALANCE_NOODLES", Localize = true)]
	[Description("FOODBALANCE_NOODLES_DESC")]
	[Slider(0f, 200f, 21)]
	public float Noodles = 100f;

	[Name("FOODBALANCE_TEACOFFEE", Localize = true)]
	[Description("FOODBALANCE_TEACOFFEE_DESC")]
	[Slider(0f, 200f, 21)]
	public float TeaCoffee = 100f;

	[Name("FOODBALANCE_SMOKES", Localize = true)]
	[Description("FOODBALANCE_SMOKES_DESC")]
	[Slider(0f, 200f, 21)]
	public float Smokes = 100f;

	[Section("FOODBALANCE_SECTION_GAME", Localize = true)]
	[Name("FOODBALANCE_GAME_BOXES", Localize = true)]
	[Description("FOODBALANCE_GAME_BOXES_DESC")]
	[Slider(0f, 200f, 21)]
	public float GameContainers = 100f;

	[Name("FOODBALANCE_GAME_MAPS", Localize = true)]
	[Description("FOODBALANCE_GAME_MAPS_DESC")]
	[Slider(0f, 100f, 11)]
	public float GameMaps = 100f;

	private static BalanceSettings instance;

	public static BalanceValues Values
	{
		get
		{
			if (instance != null)
			{
				return new BalanceValues
				{
					General = instance.General,
					FoodPack = instance.FoodPack,
					CalorieWaltz = instance.CalorieWaltz,
					RetroFood = instance.RetroFood,
					AutumnComforts = instance.AutumnComforts,
					Seagull = instance.Seagull,
					Drinks = instance.Drinks,
					Snacks = instance.Snacks,
					Noodles = instance.Noodles,
					TeaCoffee = instance.TeaCoffee,
					Smokes = instance.Smokes,
					GameContainers = instance.GameContainers,
					GameMaps = instance.GameMaps
				};
			}
			return new BalanceValues();
		}
	}

	public static void Load()
	{
		instance = new BalanceSettings();
		instance.AddToModSettings("Food Balance");
	}
}

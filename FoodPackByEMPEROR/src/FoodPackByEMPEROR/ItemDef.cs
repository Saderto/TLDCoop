using System.Collections.Generic;

namespace FoodPackByEMPEROR;

public class ItemDef
{
	public string Id = "";

	public string? Group;

	public string BasePrefab = "";

	public string? Texture;

	public Dictionary<string, string> Meshes = new Dictionary<string, string>();

	public string? Icon;

	public string? SourceTexture;

	public string? Package;

	public FoodSound? Sound;

	public string? FoodMesh;

	public List<string>? FoodParts;

	public string? SingleMesh;

	public string? CardFor;

	public string? CooksInto;

	public bool NoDecay;

	public float? InspectLift;

	public bool InspectCentred;

	public bool Cold;

	public bool FullCondition;

	public float? DecayPerDay;

	public float? DecayOutsidePerDay;

	public int? StartCondition;

	public float FoodPoisoningChance;

	public float[]? Poison;

	public int[]? ParasiteRisk;

	public float? GlassBlur;

	public float Calories;

	public float WeightKg;

	public float ThirstChange;

	public bool InParts;

	public bool? MustConsumeAll;

	public bool Drink;

	public float AlcoholPercent;

	public float? AlcoholLiters;

	public float AlcoholUptakeMinutes = 30f;

	public bool Warmth;

	public string? FatigueBuffFrom;

	public float FatigueBuffMul = 1f;

	public List<EffectUse> Effects = new List<EffectUse>();

	public List<VariantDef> Variants = new List<VariantDef>();

	public string? BaseFallback;

	public List<string>? HideParts;

	public ExtraMeshDef? ExtraMesh;

	public bool StartHot;

	public float? CookMinutes;

	public CookingLookDef? CookingLook;
}

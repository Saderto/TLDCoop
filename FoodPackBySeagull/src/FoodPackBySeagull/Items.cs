using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace FoodPackBySeagull;

public static class Items
{
	public const string Prefix = "GEAR_Seagull_";

	private const string RefPrefix = "SEAGULL:";

	private static Dictionary<string, ItemDef> byName = new Dictionary<string, ItemDef>();

	private static readonly Dictionary<string, GearItemData> dataByName = new Dictionary<string, GearItemData>();

	private static readonly HashSet<string> toldCook = new HashSet<string>();

	private const string ExtraName = "OBJ_Seagull_Extra";

	private static readonly HashSet<int> offForGood = new HashSet<int>();

	private static readonly Dictionary<string, bool> baseThere = new Dictionary<string, bool>();

	public static readonly HashSet<int> Hidden = new HashSet<int>();

	private static readonly Dictionary<string, GearItem> templates = new Dictionary<string, GearItem>();

	private static readonly Dictionary<string, GearItem> gameDrinks = new Dictionary<string, GearItem>();

	private static readonly HashSet<string> loggedPivot = new HashSet<string>();

	private static readonly HashSet<string> toldInspect = new HashSet<string>();

	public static string ReferenceOf(string id)
	{
		return "SEAGULL:" + id;
	}

	public static bool FromReference(AssetReference? r, out string name)
	{
		name = "";
		string text = r?.AssetGUID;
		if (text == null || !text.StartsWith("SEAGULL:", StringComparison.Ordinal))
		{
			return false;
		}
		name = "GEAR_Seagull_" + text.Substring("SEAGULL:".Length);
		return true;
	}

	public static void Index()
	{
		byName = Data.Items.ToDictionary((ItemDef i) => "GEAR_Seagull_" + i.Id);
	}

	public static bool TryGet(string? name, out ItemDef def)
	{
		def = null;
		if (string.IsNullOrEmpty(name))
		{
			return false;
		}
		int num = name.IndexOf('(');
		if (num >= 0)
		{
			name = name.Substring(0, num);
		}
		name = name.Trim();
		if (name.StartsWith("GearItemData_Seagull_", StringComparison.Ordinal))
		{
			name = "GEAR_Seagull_" + name.Substring("GearItemData_Seagull_".Length);
		}
		return byName.TryGetValue(name, out def);
	}

	public static void Customize(GearItem g, ItemDef def, bool restored)
	{
		string text = "GEAR_Seagull_" + def.Id;
		g.gameObject.name = text;
		if (!dataByName.TryGetValue(text, out GearItemData value))
		{
			value = UnityEngine.Object.Instantiate(g.m_GearItemData);
			value.name = "GearItemData_Seagull_" + def.Id;
			value.hideFlags = HideFlags.HideAndDontSave;
			value.m_BaseWeight = ItemWeight.FromKilograms(def.WeightKg);
			value.m_LocalizedName = new LocalizedString
			{
				m_LocalizationID = "SEAGULL_" + def.Id + "_NAME"
			};
			value.m_LocalizedDescription = new LocalizedString
			{
				m_LocalizationID = "SEAGULL_" + def.Id + "_DESC"
			};
			value.m_PrefabReference = new AssetReferenceGearItem("SEAGULL:" + def.Id);
			if (def.Sound != null && def.Sound.From != def.BasePrefab)
			{
				try
				{
					GearItemData gearItemData = GameDrink(def.Sound.From)?.m_GearItemData;
					if (gearItemData != null)
					{
						value.m_PickupAudio = gearItemData.m_PickupAudio;
						value.m_StowAudio = gearItemData.m_StowAudio;
						value.m_PutBackAudio = gearItemData.m_PutBackAudio;
					}
				}
				catch (Exception e)
				{
					Safe.Log("Sounds from " + def.Sound.From, e);
				}
			}
			dataByName[text] = value;
		}
		g.m_GearItemData = value;
		if (def.NoDecay)
		{
			value.m_DailyHPDecay = 0f;
		}
		int? startCondition = def.StartCondition;
		if (startCondition.HasValue)
		{
			int valueOrDefault = startCondition.GetValueOrDefault();
			if (!restored)
			{
				g.m_StartCondition = (GearStartCondition)valueOrDefault;
			}
		}
		if (def.FullCondition)
		{
			g.m_StartCondition = GearStartCondition.Perfect;
			Full(g);
		}
		if (def.SingleMesh != null)
		{
			ApplySingleMesh(g, def);
		}
		if (def.HideParts != null)
		{
			HideParts(g, def.HideParts);
		}
		if (def.ExtraMesh != null)
		{
			AddExtraMesh(g, def);
		}
		Texture2D texture2D = ((def.Texture != null) ? Assets.Texture(def.Texture) : null);
		if (def.FoodMesh != null)
		{
			ApplyFoodMesh(g, def, texture2D);
			texture2D = null;
		}
		float? glassBlur;
		foreach (MeshRenderer componentsInChild in g.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
		{
			if (componentsInChild.sharedMaterial == null || componentsInChild.gameObject.name == "OBJ_Seagull_Extra")
			{
				continue;
			}
			Material material = componentsInChild.material;
			if (texture2D != null && (def.SourceTexture == null || material.mainTexture == null || material.mainTexture.name == def.SourceTexture))
			{
				material.mainTexture = texture2D;
			}
			glassBlur = def.GlassBlur;
			if (glassBlur.HasValue)
			{
				float valueOrDefault2 = glassBlur.GetValueOrDefault();
				if (material.HasProperty("_BlurSize"))
				{
					material.SetFloat("_BlurSize", valueOrDefault2);
				}
			}
		}
		foreach (MeshFilter componentsInChild2 in g.GetComponentsInChildren<MeshFilter>(includeInactive: true))
		{
			if (def.Meshes.TryGetValue(componentsInChild2.gameObject.name, out string value2))
			{
				Mesh mesh = Assets.LoadMesh(value2);
				if ((object)mesh != null)
				{
					componentsInChild2.sharedMesh = mesh;
				}
			}
		}
		foreach (string want in def.Meshes.Keys)
		{
			if (g.GetComponentsInChildren<MeshFilter>(includeInactive: true).All((MeshFilter m) => m.gameObject.name != want))
			{
				MelonLogger.Warning(def.BasePrefab + " has no child " + want + ". It has: " + string.Join(", ", from m in g.GetComponentsInChildren<MeshFilter>(includeInactive: true)
					select m.gameObject.name));
			}
		}
		RefreshRenderers(g);
		if (def.CooksInto != null)
		{
			Cookable component = g.GetComponent<Cookable>();
			if ((object)component != null)
			{
				GearItem gearItem = Template(def.CooksInto);
				if ((object)gearItem != null)
				{
					component.m_CookedPrefab = gearItem;
				}
			}
		}
		if (def.CookingLook != null)
		{
			CookingLooks.Apply(g, def);
		}
		FoodItem foodItem = g.m_FoodItem;
		if (foodItem == null)
		{
			return;
		}
		FoodSound sound = def.Sound;
		if (sound != null)
		{
			foodItem.m_EatingAudio = sound.Eat;
			foodItem.m_OpenAndEatingAudio = sound.Open;
			foodItem.m_TimeToOpenAndEatSeconds = sound.Seconds;
			foodItem.m_Packaged = sound.Packaged;
			foodItem.m_GearRequiredToOpen = sound.NeedsGear;
			foodItem.m_OpenedWithCanOpener = sound.CanOpener;
			foodItem.m_OpenedWithKnife = sound.Knife;
			foodItem.m_OpenedWithHatchet = sound.Hatchet;
		}
		if (def.NoDecay)
		{
			foodItem.m_DailyHPDecayInside = 0f;
			foodItem.m_DailyHPDecayOutside = 0f;
		}
		glassBlur = def.DecayPerDay;
		if (glassBlur.HasValue)
		{
			float num = (foodItem.m_DailyHPDecayInside = glassBlur.GetValueOrDefault());
			foodItem.m_DailyHPDecayOutside = def.DecayOutsidePerDay ?? (num * 5f);
		}
		if (def.ParasiteRisk != null)
		{
			foodItem.m_ParasiteRiskPercentIncrease = new Il2CppStructArray<float>(((IEnumerable<int>)def.ParasiteRisk).Select((Func<int, float>)((int x) => x)).ToArray());
		}
		float[] poison = def.Poison;
		if (poison != null && poison.Length == 3)
		{
			foodItem.m_ChanceFoodPoisoning = poison[0];
			foodItem.m_ChanceFoodPoisoningLowCondition = poison[1];
			foodItem.m_ChanceFoodPoisoningRuined = poison[2];
		}
		if (def.FoodPoisoningChance > 0f)
		{
			foodItem.m_ChanceFoodPoisoning = def.FoodPoisoningChance;
		}
		foodItem.m_CaloriesTotal = def.Calories;
		foodItem.m_ReduceThirst = def.ThirstChange;
		bool? mustConsumeAll = def.MustConsumeAll;
		if (mustConsumeAll.HasValue)
		{
			bool valueOrDefault4 = mustConsumeAll.GetValueOrDefault();
			foodItem.m_MustConsumeAll = valueOrDefault4;
		}
		foodItem.m_IsDrink = def.Drink && !def.InParts;
		if (def.Cold)
		{
			foodItem.m_StayHeatedUntilInteraction = false;
		}
		Cookable component2 = g.GetComponent<Cookable>();
		if ((object)component2 != null && component2.m_CookedPrefab == null)
		{
			foodItem.m_HeatedWhenCooked = true;
			if (Recipes.IsCookedDish(def.Id))
			{
				component2.m_CanBePickedUpWhileCooking = false;
			}
			glassBlur = def.CookMinutes;
			if (glassBlur.HasValue)
			{
				float valueOrDefault5 = glassBlur.GetValueOrDefault();
				if (component2.m_CookTimeMinutes != valueOrDefault5)
				{
					if (toldCook.Add(def.Id))
					{
						MelonLogger.Msg($"{def.Id}: cooking time {component2.m_CookTimeMinutes} min, ours {valueOrDefault5}.");
					}
					component2.m_CookTimeMinutes = valueOrDefault5;
				}
			}
		}
		StripGameBuffs(g, def);
		if (def.Warmth && g.m_FreezingBuff == null)
		{
			FreezingBuff freezingBuff = g.gameObject.AddComponent<FreezingBuff>();
			freezingBuff.m_InitialPercentDecrease = 20f;
			freezingBuff.m_RateOfIncreaseScale = 0.5f;
			freezingBuff.m_DurationHours = 1f;
			FreezingBuff freezingBuff2 = GameDrink("GEAR_CoffeeCup")?.m_FreezingBuff;
			if (freezingBuff2 != null)
			{
				freezingBuff.m_HUD = freezingBuff2.m_HUD;
			}
			g.m_FreezingBuff = freezingBuff;
		}
		if (def.FatigueBuffFrom != null)
		{
			FatigueBuff fatigueBuff = GameDrink(def.FatigueBuffFrom)?.m_FatigueBuff ?? GameDrink("GEAR_CoffeeCup")?.m_FatigueBuff;
			if (fatigueBuff == null)
			{
				MelonLogger.Error(def.Id + ": no fatigue relief found in " + def.FatigueBuffFrom + ".");
			}
			else
			{
				FatigueBuff fatigueBuff2 = ((g.m_FatigueBuff != null) ? g.m_FatigueBuff : g.gameObject.AddComponent<FatigueBuff>());
				fatigueBuff2.m_InitialPercentDecrease = fatigueBuff.m_InitialPercentDecrease * def.FatigueBuffMul;
				fatigueBuff2.m_RateOfIncreaseScale = fatigueBuff.m_RateOfIncreaseScale;
				fatigueBuff2.m_DurationHours = fatigueBuff.m_DurationHours;
				fatigueBuff2.m_HUD = fatigueBuff.m_HUD;
				g.m_FatigueBuff = fatigueBuff2;
			}
		}
		g.m_FoodWeight = null;
		g.WeightKG = ItemWeight.FromKilograms(def.WeightKg);
		if (!restored)
		{
			foodItem.m_CaloriesRemaining = def.Calories;
		}
		if (def.StartHot && !restored)
		{
			foodItem.m_HeatPercent = 100f;
		}
	}

	private static void RefreshRenderers(GearItem g)
	{
		List<MeshRenderer> list = new List<MeshRenderer>();
		foreach (MeshRenderer componentsInChild in g.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
		{
			if (componentsInChild != null && !offForGood.Contains(componentsInChild.GetInstanceID()) && !Regex.IsMatch(componentsInChild.gameObject.name, "LOD[1-9]"))
			{
				list.Add(componentsInChild);
			}
		}
		List<SkinnedMeshRenderer> list2 = new List<SkinnedMeshRenderer>();
		foreach (SkinnedMeshRenderer componentsInChild2 in g.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true))
		{
			if (componentsInChild2 != null)
			{
				list2.Add(componentsInChild2);
			}
		}
		g.m_MeshRenderers = new Il2CppReferenceArray<MeshRenderer>(list.ToArray());
		g.m_SkinnedMeshRenderers = new Il2CppReferenceArray<SkinnedMeshRenderer>(list2.ToArray());
	}

	private static void HideParts(GearItem g, List<string> names)
	{
		foreach (MeshFilter componentsInChild in g.GetComponentsInChildren<MeshFilter>(includeInactive: true))
		{
			if (names.Contains(componentsInChild.gameObject.name))
			{
				MeshRenderer component = componentsInChild.GetComponent<MeshRenderer>();
				if (component != null)
				{
					UnityEngine.Object.DestroyImmediate(component);
				}
				UnityEngine.Object.DestroyImmediate(componentsInChild);
			}
		}
	}

	private static void AddExtraMesh(GearItem g, ItemDef def)
	{
		ExtraMeshDef x = def.ExtraMesh;
		if (g.transform.Find("OBJ_Seagull_Extra") != null)
		{
			return;
		}
		MeshRenderer meshRenderer = g.GetComponentsInChildren<MeshRenderer>(includeInactive: true).FirstOrDefault((MeshRenderer r) => r.gameObject.name == x.Like);
		if (meshRenderer == null || meshRenderer.sharedMaterial == null)
		{
			return;
		}
		Mesh mesh = Assets.LoadMesh(x.Mesh);
		if (mesh == null)
		{
			return;
		}
		GameObject gameObject = new GameObject("OBJ_Seagull_Extra");
		gameObject.transform.SetParent(g.transform, worldPositionStays: false);
		gameObject.layer = meshRenderer.gameObject.layer;
		gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
		MeshRenderer meshRenderer2 = gameObject.AddComponent<MeshRenderer>();
		Material material = new Material(meshRenderer.sharedMaterial);
		if (x.Texture != null)
		{
			Texture2D texture2D = Assets.Texture(x.Texture);
			if ((object)texture2D != null)
			{
				material.mainTexture = texture2D;
			}
		}
		if (x.Matte)
		{
			string[] array = new string[5] { "_Glossiness", "_Smoothness", "_GlossMapScale", "_Metallic", "_SpecularIntensity" };
			foreach (string name in array)
			{
				if (material.HasProperty(name))
				{
					material.SetFloat(name, 0f);
				}
			}
		}
		meshRenderer2.sharedMaterial = material;
		meshRenderer2.shadowCastingMode = meshRenderer.shadowCastingMode;
		meshRenderer2.receiveShadows = meshRenderer.receiveShadows;
	}

	public static GearItem? Spawn(string name)
	{
		return GearItem.InstantiateGearItem(name);
	}

	public static string BaseOf(ItemDef def)
	{
		if (def.BaseFallback == null)
		{
			return def.BasePrefab;
		}
		if (!baseThere.TryGetValue(def.BasePrefab, out var value))
		{
			GearItem gearItem = null;
			try
			{
				gearItem = GearItem.InstantiateGearItem(def.BasePrefab);
			}
			catch (Exception e)
			{
				Safe.Log("Looking for " + def.BasePrefab, e);
			}
			value = gearItem != null;
			if (gearItem != null)
			{
				UnityEngine.Object.Destroy(gearItem.gameObject);
			}
			baseThere[def.BasePrefab] = value;
			if (!value)
			{
				MelonLogger.Warning($"{def.Id}: {def.BasePrefab} isn't in the game (its mod not installed?): made from {def.BaseFallback} instead.");
			}
		}
		if (!value)
		{
			return def.BaseFallback;
		}
		return def.BasePrefab;
	}

	public static void Full(GearItem g)
	{
		float currentHP = ((g.m_GearItemData != null && g.m_GearItemData.m_MaxHP > 0f) ? g.m_GearItemData.m_MaxHP : 100f);
		g.CurrentHP = currentHP;
	}

	public static void Hide(GearItem g)
	{
		Hidden.Add(g.GetInstanceID());
		g.gameObject.SetActive(value: false);
		UnityEngine.Object.DontDestroyOnLoad(g.gameObject);
	}

	public static bool IsHidden(GearItem g)
	{
		if (!Hidden.Contains(g.GetInstanceID()))
		{
			return g.gameObject.scene.name == "DontDestroyOnLoad";
		}
		return true;
	}

	public static GearItem? Template(string id)
	{
		if (templates.TryGetValue(id, out GearItem value) && value != null)
		{
			return value;
		}
		value = Spawn("GEAR_Seagull_" + id);
		if (value == null)
		{
			return null;
		}
		Hide(value);
		return templates[id] = value;
	}

	public static GearItem? GameItem(string prefab)
	{
		return GameDrink(prefab);
	}

	private static GearItem? GameDrink(string prefab)
	{
		if (gameDrinks.TryGetValue(prefab, out GearItem value) && value != null)
		{
			return value;
		}
		GearItem gearItem = GearItem.InstantiateGearItem(prefab);
		if (gearItem == null)
		{
			return null;
		}
		Hide(gearItem);
		return gameDrinks[prefab] = gearItem;
	}

	private static void StripGameBuffs(GearItem g, ItemDef def)
	{
		StripLeftovers(g, def);
		g.m_ConditionRestBuff = null;
		g.m_ConditionOverTimeBuff = null;
		g.m_CarryingCapacityBuff = null;
		g.m_WolfIntimidationBuff = null;
		g.m_ClimbingBuff = null;
		g.m_ProtectionBuff = null;
		g.m_WeightReductionBuff = null;
		if (!def.Warmth)
		{
			g.m_FreezingBuff = null;
		}
		if (def.FatigueBuffFrom == null)
		{
			g.m_FatigueBuff = null;
		}
	}

	public static int StripLeftovers(GearItem g, ItemDef def)
	{
		GearItem g2 = g;
		int i = 0;
		All<ConditionRestBuff>();
		All<ConditionOverTimeBuff>();
		All<CarryingCapacityBuff>();
		All<WolfIntimidationBuff>();
		All<ClimbingBuff>();
		All<ProtectionBuff>();
		All<WeightReductionBuff>();
		if (!def.Warmth)
		{
			All<FreezingBuff>();
		}
		if (def.FatigueBuffFrom == null)
		{
			All<FatigueBuff>();
		}
		All<FoodMaxStatBuff>();
		All<FoodStatEffect>();
		All<ResistInsomniaBuff>();
		All<CabinFeverReductionBuff>();
		All<CausesHeadacheDebuff>();
		All<IngestedCarryCapacityBuff>();
		return i;
		void All<T>() where T : notnull, Component
		{
			foreach (T componentsInChild in g2.GetComponentsInChildren<T>(includeInactive: true))
			{
				UnityEngine.Object.DestroyImmediate(componentsInChild);
				i++;
			}
		}
	}

	private static void ApplyFoodMesh(GearItem g, ItemDef def, Texture2D? tex)
	{
		LODGroup component = g.GetComponent<LODGroup>();
		if (component != null)
		{
			UnityEngine.Object.Destroy(component);
		}
		Mesh mesh = Assets.LoadMesh(def.FoodMesh);
		List<string> list = def.FoodParts ?? new List<string>();
		bool flag = false;
		foreach (MeshFilter componentsInChild in g.GetComponentsInChildren<MeshFilter>(includeInactive: true))
		{
			MeshRenderer component2 = componentsInChild.GetComponent<MeshRenderer>();
			if (component2 == null)
			{
				continue;
			}
			string name = componentsInChild.gameObject.name;
			if (Regex.IsMatch(name, "LOD[1-9]"))
			{
				component2.enabled = false;
				offForGood.Add(component2.GetInstanceID());
			}
			else
			{
				if (!list.Contains(name))
				{
					continue;
				}
				if (flag || mesh == null)
				{
					component2.enabled = false;
					offForGood.Add(component2.GetInstanceID());
					continue;
				}
				flag = true;
				componentsInChild.transform.SetParent(g.transform, worldPositionStays: false);
				componentsInChild.transform.localPosition = Vector3.zero;
				componentsInChild.transform.localRotation = Quaternion.identity;
				componentsInChild.transform.localScale = Vector3.one;
				componentsInChild.gameObject.SetActive(value: true);
				componentsInChild.sharedMesh = mesh;
				component2.enabled = true;
				component2.sharedMaterials = new Il2CppReferenceArray<Material>(new Material[1] { component2.sharedMaterial });
				if (tex != null)
				{
					component2.material.mainTexture = tex;
				}
			}
		}
		if (!flag)
		{
			MelonLogger.Warning(def.Id + ": none of its food parts found in " + def.BasePrefab + ": " + string.Join(", ", list));
		}
	}

	private static void ApplySingleMesh(GearItem g, ItemDef def)
	{
		GearItem g2 = g;
		LODGroup component = g2.GetComponent<LODGroup>();
		if (component != null)
		{
			UnityEngine.Object.Destroy(component);
		}
		Mesh mesh = Assets.LoadMesh(def.SingleMesh);
		Il2CppArrayBase<MeshFilter> componentsInChildren = g2.GetComponentsInChildren<MeshFilter>(includeInactive: true);
		float num = 0f;
		foreach (MeshFilter item in componentsInChildren)
		{
			if (item.sharedMesh != null)
			{
				MeshRenderer component2 = item.GetComponent<MeshRenderer>();
				if ((object)component2 != null && component2.enabled && !Regex.IsMatch(item.gameObject.name, "LOD[1-9]"))
				{
					num = Math.Max(num, item.sharedMesh.bounds.size.magnitude * item.transform.lossyScale.x);
				}
			}
		}
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			MeshRenderer component3 = componentsInChildren[i].GetComponent<MeshRenderer>();
			if (i == 0)
			{
				Vector3 vector = ((mesh != null) ? mesh.bounds.center : Vector3.zero);
				if (componentsInChildren[i].transform.parent != g2.transform)
				{
					componentsInChildren[i].transform.SetParent(g2.transform, worldPositionStays: false);
				}
				componentsInChildren[i].transform.localPosition = new Vector3(0f - vector.x, 0f, 0f - vector.z);
				componentsInChildren[i].transform.localRotation = Quaternion.identity;
				componentsInChildren[i].transform.localScale = Vector3.one;
				componentsInChildren[i].gameObject.SetActive(value: true);
				if (mesh != null)
				{
					componentsInChildren[i].sharedMesh = mesh;
				}
				if (component3 != null)
				{
					component3.enabled = true;
					component3.sharedMaterials = new Il2CppReferenceArray<Material>(new Material[1] { component3.sharedMaterial });
				}
			}
			else
			{
				if (component3 != null)
				{
					UnityEngine.Object.DestroyImmediate(component3);
				}
				UnityEngine.Object.DestroyImmediate(componentsInChildren[i]);
			}
		}
		foreach (SkinnedMeshRenderer componentsInChild in g2.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true))
		{
			if (componentsInChild != null)
			{
				UnityEngine.Object.DestroyImmediate(componentsInChild);
			}
		}
		if (componentsInChildren.Length > 0 && componentsInChildren[0] != null)
		{
			Inspect inspect = g2.m_Inspect;
			if ((object)inspect != null)
			{
				GameObject gameObject = componentsInChildren[0].gameObject;
				if (inspect.m_NormalMesh != null && inspect.m_NormalMesh != gameObject)
				{
					inspect.m_NormalMesh = gameObject;
				}
				if (inspect.m_InspectModeMesh != null)
				{
					GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject, gameObject.transform.parent, worldPositionStays: false);
					gameObject2.name = gameObject.name + "_Inspect";
					gameObject2.transform.localPosition = gameObject.transform.localPosition;
					gameObject2.transform.localRotation = gameObject.transform.localRotation;
					gameObject2.transform.localScale = gameObject.transform.localScale;
					gameObject2.SetActive(value: false);
					inspect.m_InspectModeMesh = gameObject2;
					if (toldInspect.Add(def.Id))
					{
						MelonLogger.Msg(def.Id + ": the source's own inspect model replaced by ours");
					}
				}
			}
		}
		if (mesh != null && num > 0f && g2.m_Inspect != null)
		{
			float magnitude = mesh.bounds.size.magnitude;
			if (magnitude > num * 1.1f)
			{
				g2.m_Inspect.m_DistanceFromCamera *= magnitude / num;
				if (!def.InspectCentred)
				{
					g2.m_Inspect.m_Offset = g2.m_Inspect.m_Offset - new Vector3(0f, mesh.bounds.center.y * def.InspectLift.GetValueOrDefault(0.5f), 0f);
				}
			}
		}
		if (!(g2.m_Inspect != null))
		{
			return;
		}
		Vector3 offset = g2.m_Inspect.m_Offset;
		g2.m_Inspect.m_Offset = new Vector3(0f, offset.y, 0f);
		if (Data.Settings.TestKeys && loggedPivot.Add(def.Id) && mesh != null)
		{
			Vector3 vector2 = ((componentsInChildren.Length <= 0) ? Vector3.zero : (componentsInChildren[0].GetComponent<MeshRenderer>()?.bounds.center ?? Vector3.zero));
			MelonLogger.Msg($"Pivot {def.Id}: mesh middle {mesh.bounds.center.x:0.000} {mesh.bounds.center.z:0.000}, inspect offset was {offset.x:0.000} {offset.y:0.000} {offset.z:0.000}, rendered middle from root {(vector2 - g2.transform.position).x:0.000} {(vector2 - g2.transform.position).z:0.000}");
			Il2CppArrayBase<Renderer> componentsInChildren2 = g2.GetComponentsInChildren<Renderer>(includeInactive: true);
			MelonLogger.Msg($"Pivot {def.Id}: renderers {string.Join(", ", componentsInChildren2.Select((Renderer x) => $"{x.gameObject.name}{(x.enabled ? "" : " (off)")} at {(x.bounds.center - g2.transform.position).x:0.000} {(x.bounds.center - g2.transform.position).z:0.000}"))}; normal mesh {((g2.m_Inspect.m_NormalMesh != null) ? g2.m_Inspect.m_NormalMesh.name : "-")}, inspect mesh {((g2.m_Inspect.m_InspectModeMesh != null) ? g2.m_Inspect.m_InspectModeMesh.name : "-")}, children {string.Join(", ", from c in Enumerable.Range(0, g2.transform.childCount)
				select g2.transform.GetChild(c).name + " " + g2.transform.GetChild(c).localPosition.ToString("F3"))}");
		}
	}
}

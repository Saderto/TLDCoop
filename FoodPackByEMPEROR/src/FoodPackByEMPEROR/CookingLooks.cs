using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;

namespace FoodPackByEMPEROR;

public static class CookingLooks
{
	private static readonly Dictionary<string, (Material raw, Material cook, Material burnt)> materials = new Dictionary<string, (Material, Material, Material)>();

	private static readonly Dictionary<string, bool> told = new Dictionary<string, bool>();

	private static string Vessel(CookingPotItem.GrubMeshType t)
	{
		return t switch
		{
			CookingPotItem.GrubMeshType.FryingPan => "pan", 
			CookingPotItem.GrubMeshType.Can => "can", 
			_ => "pot", 
		};
	}

	private static Mesh? MeshOf(CookingLookDef look, string vessel, string stage)
	{
		if (!look.Meshes.TryGetValue(vessel, out Dictionary<string, string> value) || !value.TryGetValue(stage, out var value2))
		{
			return null;
		}
		return Assets.LoadMesh(value2);
	}

	public static void Apply(GearItem g, ItemDef def)
	{
		ItemDef def2 = def;
		CookingLookDef look = def2.CookingLook;
		Cookable component = g.GetComponent<Cookable>();
		if (look == null || component == null)
		{
			return;
		}
		Mesh mesh = MeshOf(look, "pot", "raw");
		if ((object)mesh != null)
		{
			component.m_MeshRawPotStyle = mesh;
		}
		Mesh mesh2 = MeshOf(look, "pot", "cook");
		if ((object)mesh2 != null)
		{
			component.m_MeshPotStyle = mesh2;
		}
		Mesh mesh3 = MeshOf(look, "pan", "raw");
		if ((object)mesh3 != null)
		{
			component.m_MeshRawFryingPanStyle = mesh3;
		}
		Mesh mesh4 = MeshOf(look, "pan", "cook");
		if ((object)mesh4 != null)
		{
			component.m_MeshFryingPanStyle = mesh4;
		}
		Mesh mesh5 = MeshOf(look, "can", "raw");
		if ((object)mesh5 != null)
		{
			component.m_MeshRawCanStyle = mesh5;
		}
		Mesh mesh6 = MeshOf(look, "can", "cook");
		if ((object)mesh6 != null)
		{
			component.m_MeshCanStyle = mesh6;
		}
		Material like;
		if (!materials.TryGetValue(def2.Id, out (Material, Material, Material) value))
		{
			like = null;
			Cookable cookable = Items.GameItem("GEAR_CookedStewMeat")?.GetComponent<Cookable>();
			if (cookable != null && cookable.m_CookingPotMaterialsList != null && cookable.m_CookingPotMaterialsList.Length > 0)
			{
				like = cookable.m_CookingPotMaterialsList[0];
			}
			if (like == null && component.m_CookingPotMaterialsList != null && component.m_CookingPotMaterialsList.Length > 0)
			{
				like = component.m_CookingPotMaterialsList[0];
			}
			if (like == null)
			{
				if (told.TryAdd(def2.Id, value: true))
				{
					MelonLogger.Warning(def2.Id + ": its source has no cooking material to copy; the look in the cookware stays the game's.");
				}
				return;
			}
			value = (materials[def2.Id] = (Make("raw"), Make("cook"), Make("burnt")));
		}
		component.m_CookingPotRawMaterialsList = new Il2CppReferenceArray<Material>(new Material[1] { value.Item1 });
		component.m_CookingPotMaterialsList = new Il2CppReferenceArray<Material>(new Material[1] { value.Item2 });
		component.m_RuinedMaterials = new Il2CppReferenceArray<Material>(new Material[1] { value.Item3 });
		Material Make(string stage)
		{
			Material material = new Material(like)
			{
				name = "Seagull_" + def2.Id + "_" + stage,
				hideFlags = HideFlags.HideAndDontSave
			};
			if (look.Atlas.TryGetValue(stage, out string value2))
			{
				Texture2D texture2D = Assets.Texture(value2, compress: true, mipmaps: false);
				if ((object)texture2D != null)
				{
					material.mainTexture = texture2D;
				}
			}
			return material;
		}
	}

	public static void Stage(GearItem? g, bool raw)
	{
		if (g == null || !Items.TryGet(g.name, out ItemDef def))
		{
			return;
		}
		string text = def.CookingLook?.RawTexture;
		if (text == null || def.Texture == null)
		{
			return;
		}
		Texture2D texture2D = Assets.Texture(def.Texture);
		Texture2D texture2D2 = Assets.Texture(text);
		if (texture2D == null || texture2D2 == null)
		{
			return;
		}
		Texture texture = (raw ? texture2D : texture2D2);
		Texture mainTexture = (raw ? texture2D2 : texture2D);
		foreach (Renderer componentsInChild in g.GetComponentsInChildren<Renderer>(includeInactive: true))
		{
			Material sharedMaterial = componentsInChild.sharedMaterial;
			if (sharedMaterial != null && sharedMaterial.mainTexture == texture)
			{
				sharedMaterial.mainTexture = mainTexture;
			}
		}
	}

	public static void DoneLook(CookingPotItem pot)
	{
		GearItem gearItemBeingCooked = pot.m_GearItemBeingCooked;
		if (!(gearItemBeingCooked == null) && !(pot.m_GrubMeshFilter == null) && Items.TryGet(gearItemBeingCooked.name, out ItemDef def) && def.CookingLook != null)
		{
			Mesh mesh = MeshOf(def.CookingLook, Vessel(pot.m_GrubMeshType), "cook");
			if ((object)mesh != null)
			{
				pot.m_GrubMeshFilter.sharedMesh = mesh;
			}
			if (pot.m_GrubMeshRenderer != null && materials.TryGetValue(def.Id, out (Material, Material, Material) value))
			{
				pot.m_GrubMeshRenderer.sharedMaterial = value.Item2;
			}
		}
	}

	public static void Burnt(CookingPotItem pot)
	{
		GearItem gearItemBeingCooked = pot.m_GearItemBeingCooked;
		if (!(gearItemBeingCooked == null) && !(pot.m_GrubMeshFilter == null) && Items.TryGet(gearItemBeingCooked.name, out ItemDef def) && def.CookingLook != null)
		{
			Mesh mesh = MeshOf(def.CookingLook, Vessel(pot.m_GrubMeshType), "burnt");
			if ((object)mesh != null)
			{
				pot.m_GrubMeshFilter.sharedMesh = mesh;
			}
		}
	}
}

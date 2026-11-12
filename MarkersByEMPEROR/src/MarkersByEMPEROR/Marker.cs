using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;
using MelonLoader;
using UnityEngine;

namespace MarkersByEMPEROR;

internal static class Marker
{
	public const string Id = "TrailMarker";

	public const string Name = "GEAR_SeagullMarker_TrailMarker";

	public const string RefGuid = "SEAGULLMARKER:TrailMarker";

	public const string Base = "GEAR_Stick";

	public const float WeightKg = 0.35f;

	private static GearItemData s_Data;

	public static bool Is(string n)
	{
		if (string.IsNullOrEmpty(n))
		{
			return false;
		}
		int num = n.IndexOf('(');
		if (num >= 0)
		{
			n = n.Substring(0, num);
		}
		n = n.Trim();
		if (!(n == "GEAR_SeagullMarker_TrailMarker"))
		{
			return n == "GearItemData_SeagullMarker_TrailMarker";
		}
		return true;
	}

	public static void Customize(GearItem g, bool restored)
	{
		g.gameObject.name = "GEAR_SeagullMarker_TrailMarker";
		if (s_Data == null)
		{
			s_Data = UnityEngine.Object.Instantiate(g.m_GearItemData);
			s_Data.name = "GearItemData_SeagullMarker_TrailMarker";
			s_Data.hideFlags = HideFlags.HideAndDontSave;
			s_Data.m_BaseWeight = ItemWeight.FromKilograms(0.35f);
			s_Data.m_LocalizedName = new LocalizedString
			{
				m_LocalizationID = "MARKERS_TRAIL_NAME"
			};
			s_Data.m_LocalizedDescription = new LocalizedString
			{
				m_LocalizationID = "MARKERS_TRAIL_DESC"
			};
			s_Data.m_PrefabReference = new AssetReferenceGearItem("SEAGULLMARKER:TrailMarker");
			s_Data.m_DailyHPDecay = 0f;
			s_Data.m_IsPlaceable = true;
		}
		g.m_GearItemData = s_Data;
		g.WeightKG = ItemWeight.FromKilograms(0.35f);
		if (!restored)
		{
			g.m_StartCondition = GearStartCondition.Perfect;
			g.CurrentHP = ((s_Data.m_MaxHP > 0f) ? s_Data.m_MaxHP : 100f);
		}
		foreach (FuelSourceItem componentsInChild in g.GetComponentsInChildren<FuelSourceItem>(includeInactive: true))
		{
			UnityEngine.Object.DestroyImmediate(componentsInChild);
		}
		g.m_FuelSourceItem = null;
		foreach (Harvest componentsInChild2 in g.GetComponentsInChildren<Harvest>(includeInactive: true))
		{
			UnityEngine.Object.DestroyImmediate(componentsInChild2);
		}
		g.m_Harvest = null;
		SingleMesh(g, Assets.LoadMesh("meshes/trail_marker.fpmesh"), Assets.Texture("textures/trail_marker.png"));
		Hud.Track(g);
	}

	private static void SingleMesh(GearItem g, Mesh mesh, Texture2D tex)
	{
		LODGroup component = g.GetComponent<LODGroup>();
		if ((object)component != null)
		{
			UnityEngine.Object.DestroyImmediate(component);
		}
		Il2CppArrayBase<MeshFilter> componentsInChildren = g.GetComponentsInChildren<MeshFilter>(includeInactive: true);
		bool flag = false;
		foreach (MeshFilter item in componentsInChildren)
		{
			MeshRenderer component2 = item.GetComponent<MeshRenderer>();
			if (!flag && component2 != null)
			{
				flag = true;
				if (item.transform.parent != g.transform)
				{
					item.transform.SetParent(g.transform, worldPositionStays: false);
				}
				item.transform.localPosition = Vector3.zero;
				item.transform.localRotation = Quaternion.identity;
				item.transform.localScale = Vector3.one;
				item.gameObject.SetActive(value: true);
				if (mesh != null)
				{
					item.sharedMesh = mesh;
				}
				component2.enabled = true;
				if (component2.sharedMaterial != null)
				{
					component2.sharedMaterials = new Il2CppReferenceArray<Material>(new Material[1] { component2.sharedMaterial });
					if (tex != null)
					{
						component2.material.mainTexture = tex;
					}
				}
			}
			else
			{
				if (component2 != null)
				{
					UnityEngine.Object.DestroyImmediate(component2);
				}
				UnityEngine.Object.DestroyImmediate(item);
			}
		}
		if (mesh != null)
		{
			Bounds bounds = mesh.bounds;
			Il2CppArrayBase<Collider> componentsInChildren2 = g.GetComponentsInChildren<Collider>(includeInactive: true);
			BoxCollider component3 = g.GetComponent<BoxCollider>();
			GameObject gameObject = ((component3 != null) ? g.gameObject : ((componentsInChildren2.Length > 0 && componentsInChildren2[0] != null) ? componentsInChildren2[0].gameObject : g.gameObject));
			foreach (Collider item2 in componentsInChildren2)
			{
				if (item2 != null && item2 != component3)
				{
					UnityEngine.Object.DestroyImmediate(item2);
				}
			}
			if (gameObject != g.gameObject)
			{
				gameObject.transform.localPosition = Vector3.zero;
				gameObject.transform.localRotation = Quaternion.identity;
				gameObject.transform.localScale = Vector3.one;
			}
			if (g.gameObject.layer != 0 && g.gameObject.layer != 2)
			{
				gameObject.layer = g.gameObject.layer;
				Repair.GearLayer = g.gameObject.layer;
			}
			BoxCollider obj = component3 ?? gameObject.AddComponent<BoxCollider>();
			MelonLogger.Msg($"Marker collider on {((gameObject == g.gameObject) ? "the item" : gameObject.name)}, layer {gameObject.layer} (item layer {g.gameObject.layer}).");
			obj.isTrigger = false;
			obj.enabled = true;
			obj.center = bounds.center;
			obj.size = new Vector3(Math.Max(bounds.size.x, 0.08f), Math.Max(bounds.size.y, 0.05f), Math.Max(bounds.size.z, 0.08f));
		}
		if (mesh != null && g.m_Inspect != null)
		{
			g.m_Inspect.m_DistanceFromCamera = Math.Max(g.m_Inspect.m_DistanceFromCamera, 1.6f);
			g.m_Inspect.m_Offset = new Vector3(0f, -0.65f, 0f);
		}
		List<MeshRenderer> list = new List<MeshRenderer>();
		foreach (MeshRenderer componentsInChild in g.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
		{
			if (componentsInChild != null)
			{
				list.Add(componentsInChild);
			}
		}
		g.m_MeshRenderers = new Il2CppReferenceArray<MeshRenderer>(list.ToArray());
		g.m_SkinnedMeshRenderers = new Il2CppReferenceArray<SkinnedMeshRenderer>(0L);
	}

	public static Texture2D Icon()
	{
		return Assets.Texture("icons/TrailMarker.png", compress: false);
	}
}

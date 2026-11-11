using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gear;
using UnityEngine;

namespace FoodPackBySeagull;

[HarmonyPatch]
internal static class InventoryIconPatches
{
	private static Texture2D? Icon(string? name)
	{
		if (!Items.TryGet(name, out ItemDef def) || def.Icon == null)
		{
			return null;
		}
		return Assets.Texture(def.Icon, compress: false);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(GearItem), "GetInventoryIconTexture")]
	private static void OfItem(GearItem __instance, ref Texture2D __result)
	{
		__result = Icon(__instance.name) ?? __result;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Utils), "GetInventoryIconTexture", new Type[] { typeof(GearItem) })]
	private static void ByItem(GearItem gi, ref Texture2D __result)
	{
		if (gi != null)
		{
			__result = Icon(gi.name) ?? __result;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Utils), "GetInventoryIconTexture", new Type[]
	{
		typeof(GearItem),
		typeof(float)
	})]
	private static void ByItemCondition(GearItem gi, ref Texture2D __result)
	{
		if (gi != null)
		{
			__result = Icon(gi.name) ?? __result;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Utils), "GetInventoryIconTexture", new Type[]
	{
		typeof(GearItemData),
		typeof(float)
	})]
	private static void ByData(GearItemData gearItemData, ref Texture2D __result)
	{
		if (gearItemData != null)
		{
			__result = Icon(gearItemData.name.Replace("GearItemData_Seagull_", "GEAR_Seagull_")) ?? __result;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Utils), "GetInventoryIconTextureFromPrefabName")]
	private static void ByName(string prefabName, ref Texture2D __result)
	{
		__result = Icon(prefabName) ?? __result;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Utils), "GetGearCoverflowTexture")]
	private static void Cover(GearItem gi, ref Texture2D __result)
	{
		if (gi != null)
		{
			__result = Icon(gi.name) ?? __result;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Utils), "GetGearCoverflowDamageTexture")]
	private static void CoverDamage(GearItem gi, ref Texture2D __result)
	{
		if (gi != null)
		{
			__result = Icon(gi.name) ?? __result;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Utils), "GetGearCoverflowBlendTexture")]
	private static void CoverBlend(GearItem gi, ref Texture2D __result)
	{
		if (gi != null)
		{
			__result = Icon(gi.name) ?? __result;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(GearItemCoverflow), "SetGearItem")]
	private static void CoverSet(GearItemCoverflow __instance)
	{
		CoverPicture(__instance);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(GearItemCoverflow), "SetDamageTextures")]
	private static void CoverDamageSet(GearItemCoverflow __instance)
	{
		CoverPicture(__instance);
	}

	private static void CoverPicture(GearItemCoverflow c)
	{
		try
		{
			GearItem gearItem = c?.m_GearItem;
			if (gearItem == null)
			{
				return;
			}
			Texture2D texture2D = Icon(gearItem.name);
			if ((object)texture2D == null)
			{
				return;
			}
			if (c.m_Texture != null)
			{
				c.m_Texture.mainTexture = texture2D;
			}
			if (!(c.m_TextureWithDamage != null))
			{
				return;
			}
			c.m_TextureWithDamage.mainTexture = texture2D;
			Material material = c.m_TextureWithDamage.material;
			if (material != null)
			{
				material.mainTexture = texture2D;
				if (material.HasProperty(GearItemCoverflow.DAMAGE_TEXTURE_NAME))
				{
					material.SetTexture(GearItemCoverflow.DAMAGE_TEXTURE_NAME, texture2D);
				}
			}
		}
		catch (Exception e)
		{
			Safe.Log("Inventory picture", e);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(GearSelectCoverflow), "SetGearItemIcon", new Type[] { typeof(GearItem) })]
	private static void Picker(GearSelectCoverflow __instance, GearItem gearItem)
	{
		if (gearItem != null && __instance.m_Texture != null)
		{
			Texture2D texture2D = Icon(gearItem.name);
			if ((object)texture2D != null)
			{
				__instance.m_Texture.mainTexture = texture2D;
			}
		}
	}
}

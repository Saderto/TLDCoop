using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Il2Cpp;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ComplexLogger.Utilities;

public class CommonUtilities
{
	// GearItem.LoadGearItemPrefab was removed from the game in 2.58: the prefab is loaded through Addressables instead
	// (fix by EMPEROR; this used to come from the TLDCompat258 shim).
	private static GearItem LoadGearItemPrefab(string name)
	{
		if (string.IsNullOrEmpty(name)) return null;
		try
		{
			GameObject go = Addressables.LoadAssetAsync<GameObject>(name).WaitForCompletion();
			return (go == null) ? null : go.GetComponent<GearItem>();
		}
		catch
		{
			return null;
		}
	}

	[return: NotNullIfNotNull("name")]
	public static GearItem GetGearItemPrefab(string name)
	{
		return LoadGearItemPrefab(name);
	}

	[return: NotNullIfNotNull("name")]
	public static ToolsItem GetToolItemPrefab(string name)
	{
		return GetGearItemComponentPrefab<ToolsItem>(name).GetComponent<ToolsItem>();
	}

	[return: NotNullIfNotNull("name")]
	public static ClothingItem GetClothingItemPrefab(string name)
	{
		return GetGearItemComponentPrefab<ClothingItem>(name).GetComponent<ClothingItem>();
	}

	public static T GetGearItemComponentPrefab<T>(string name)
	{
		return LoadGearItemPrefab(name).GetComponent<T>();
	}

	[return: NotNullIfNotNull("name")]
	public static string? NormalizeName(string name)
	{
		return Regex.Replace(Regex.Replace(name, "(?:\\(\\d{0,}\\))", string.Empty), "(?:\\s\\d{0,})", string.Empty).Replace("(Clone)", string.Empty, StringComparison.InvariantCultureIgnoreCase).Replace("\0", string.Empty)
			.Trim();
	}

	public static bool IsPlayerAvailable(PlayerManager PlayerManagerComponent)
	{
		if (PlayerManagerComponent == null)
		{
			return false;
		}
		if (PlayerManagerComponent.m_ControlMode != PlayerControlMode.Dead && PlayerManagerComponent.m_ControlMode != PlayerControlMode.InConversation && PlayerManagerComponent.m_ControlMode != PlayerControlMode.Locked)
		{
			return PlayerManagerComponent.m_ControlMode != PlayerControlMode.InFPCinematic;
		}
		return false;
	}

	public static bool IsInterfaceOpen()
	{
		return InterfaceManager.IsOverlayActiveCached();
	}

	public static bool IsGearOperatable(GearItem gi)
	{
		if (gi != null && gi.name != null)
		{
			return gi.name != "GEAR_Null";
		}
		return false;
	}

	public static bool IsGearOperatable<T>(T panel, GearItem gi) where T : Panel_Base
	{
		if (panel != null && gi != null && gi.name != null)
		{
			return gi.name != "GEAR_Null";
		}
		return false;
	}
}

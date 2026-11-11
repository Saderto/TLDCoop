using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(DishEffectInfo), "ShowDishBenefits")]
internal static class EffectRow
{
	private const string TexName = "SeagullEffectTexture";

	private static readonly System.Collections.Generic.Dictionary<string, Texture2D?> icons = new System.Collections.Generic.Dictionary<string, Texture2D>();

	private static readonly System.Collections.Generic.Dictionary<IntPtr, System.Collections.Generic.List<DishEffectInfoIcon>> extra = new System.Collections.Generic.Dictionary<IntPtr, System.Collections.Generic.List<DishEffectInfoIcon>>();

	private static bool again;

	private static readonly System.Collections.Generic.Dictionary<IntPtr, float> alphas = new System.Collections.Generic.Dictionary<IntPtr, float>();

	private static readonly System.Collections.Generic.Dictionary<IntPtr, (IntPtr item, bool hot)> showing = new System.Collections.Generic.Dictionary<IntPtr, (IntPtr, bool)>();

	private static readonly System.Collections.Generic.List<DishEffectInfoIcon> filled = new System.Collections.Generic.List<DishEffectInfoIcon>();

	private static readonly System.Collections.Generic.HashSet<IntPtr> opened = new System.Collections.Generic.HashSet<IntPtr>();

	private static readonly System.Collections.Generic.HashSet<IntPtr> told = new System.Collections.Generic.HashSet<IntPtr>();

	private static bool described;

	private static readonly System.Collections.Generic.Dictionary<IntPtr, (UILabel.Overflow how, int width)> sizing = new System.Collections.Generic.Dictionary<IntPtr, (UILabel.Overflow, int)>();

	private const int MaxLabelWidth = 150;

	private static Texture2D? Icon(string? file)
	{
		if (file == null)
		{
			return null;
		}
		if (icons.TryGetValue(file, out Texture2D value))
		{
			return value;
		}
		Texture2D texture2D = null;
		using Stream stream = typeof(EffectRow).Assembly.GetManifestResourceStream("FoodPackBySeagull.Icons." + file);
		if (stream != null)
		{
			using MemoryStream memoryStream = new MemoryStream();
			stream.CopyTo(memoryStream);
			texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
			texture2D.LoadImage(memoryStream.ToArray(), markNonReadable: true);
			texture2D.hideFlags = HideFlags.HideAndDontSave;
		}
		return icons[file] = texture2D;
	}

	private static bool Hot(GameObject o)
	{
		GearItem component = o.GetComponent<GearItem>();
		if ((object)component != null && component.m_FoodItem != null)
		{
			return component.m_FoodItem.m_HeatPercent > 0f;
		}
		return false;
	}

	private static bool Prefix(DishEffectInfo __instance, GameObject __0, ref bool __result)
	{
		if (again || __0 == null || !showing.TryGetValue(__instance.Pointer, out (IntPtr, bool) value) || value.Item1 != __0.Pointer || value.Item2 != Hot(__0))
		{
			return true;
		}
		__result = true;
		return false;
	}

	private static void Postfix(DishEffectInfo __instance, GameObject __0, ref bool __result, bool __runOriginal)
	{
		if (!__runOriginal)
		{
			return;
		}
		showing.Remove(__instance.Pointer);
		try
		{
			System.Collections.Generic.List<DishEffectInfoIcon> list = new System.Collections.Generic.List<DishEffectInfoIcon>();
			if (__instance.m_DishEffectIcons != null)
			{
				Il2CppSystem.Collections.Generic.List<DishEffectInfoIcon>.Enumerator enumerator = __instance.m_DishEffectIcons.GetEnumerator();
				while (enumerator.MoveNext())
				{
					DishEffectInfoIcon current = enumerator.Current;
					if (current != null)
					{
						list.Add(current);
					}
				}
			}
			if (extra.TryGetValue(__instance.Pointer, out System.Collections.Generic.List<DishEffectInfoIcon> value))
			{
				list.AddRange(value.Where((DishEffectInfoIcon m) => m != null));
			}
			if (list.Count == 0)
			{
				return;
			}
			foreach (DishEffectInfoIcon item in list)
			{
				Release(item);
				Unfit(item.m_Label);
				UITexture uITexture = Our(item, make: false);
				if (uITexture != null)
				{
					uITexture.enabled = false;
				}
				if (item.m_Sprite != null)
				{
					item.m_Sprite.enabled = true;
					if (alphas.TryGetValue(item.m_Sprite.Pointer, out var value2))
					{
						item.m_Sprite.alpha = value2;
					}
				}
			}
			if (value != null)
			{
				foreach (DishEffectInfoIcon item2 in value)
				{
					if (item2 != null)
					{
						item2.gameObject.SetActive(value: false);
					}
				}
			}
			GearItem gearItem = ((__0 != null) ? __0.GetComponent<GearItem>() : null);
			if (gearItem == null || !Items.TryGet(gearItem.name, out ItemDef def))
			{
				return;
			}
			if (!again && Items.StripLeftovers(gearItem, def) > 0)
			{
				MelonLogger.Msg(def.Id + ": the source item's effects were still on it, removed");
				again = true;
				try
				{
					__result = __instance.ShowDishBenefits(__0);
					return;
				}
				finally
				{
					again = false;
				}
			}
			bool hot = gearItem.m_FoodItem != null && gearItem.m_FoodItem.m_HeatPercent > 0f;
			System.Collections.Generic.List<EffectDef> list2 = (from u in def.Effects
				select Data.Effect(u.Id) into e
				where e != null && (!e.HotOnly || hot) && Icon(e.Icon) != null
				select e).Distinct().ToList();
			if (list2.Count == 0)
			{
				return;
			}
			System.Collections.Generic.List<DishEffectInfoIcon> list3 = list.Where((DishEffectInfoIcon s) => !s.gameObject.activeSelf).ToList();
			foreach (EffectDef item3 in list2)
			{
				DishEffectInfoIcon dishEffectInfoIcon;
				if (list3.Count > 0)
				{
					dishEffectInfoIcon = list3[0];
					list3.RemoveAt(0);
				}
				else
				{
					GameObject gameObject = UnityEngine.Object.Instantiate(list[0].gameObject, list[0].transform.parent);
					gameObject.name = "SeagullEffect";
					dishEffectInfoIcon = gameObject.GetComponent<DishEffectInfoIcon>();
					if (!extra.TryGetValue(__instance.Pointer, out value))
					{
						value = (extra[__instance.Pointer] = new System.Collections.Generic.List<DishEffectInfoIcon>());
					}
					value.Add(dishEffectInfoIcon);
				}
				Fill(dishEffectInfoIcon, item3, list[0]);
			}
			__instance.m_DishEffectGrid?.Reposition();
			__result = true;
			showing[__instance.Pointer] = (__0.Pointer, hot);
		}
		catch (Exception e2)
		{
			Safe.Log("Effect icons", e2);
		}
	}

	private static UITexture? Our(DishEffectInfoIcon slot, bool make)
	{
		UISprite sprite = slot.m_Sprite;
		if (sprite == null)
		{
			return null;
		}
		Transform transform = ((sprite.transform.parent != null) ? sprite.transform.parent.Find("SeagullEffectTexture") : null);
		if (transform != null)
		{
			return transform.GetComponent<UITexture>();
		}
		if (!make)
		{
			return null;
		}
		GameObject gameObject = new GameObject("SeagullEffectTexture");
		gameObject.layer = sprite.gameObject.layer;
		gameObject.transform.SetParent(sprite.transform.parent, worldPositionStays: false);
		gameObject.transform.localPosition = sprite.transform.localPosition;
		gameObject.transform.localRotation = sprite.transform.localRotation;
		gameObject.transform.localScale = sprite.transform.localScale;
		UITexture uITexture = gameObject.AddComponent<UITexture>();
		uITexture.width = sprite.width;
		uITexture.height = sprite.height;
		uITexture.depth = sprite.depth + 1;
		return uITexture;
	}

	private static void Release(DishEffectInfoIcon s)
	{
		filled.Remove(s);
		if (opened.Remove(s.Pointer) && s.m_Tooltip != null)
		{
			HoverTooltip component = s.m_Tooltip.GetComponent<HoverTooltip>();
			if ((object)component != null && component.m_IsHovering)
			{
				component.OnHover(hover: false);
			}
		}
	}

	public static void Hover()
	{
		if (filled.Count == 0)
		{
			return;
		}
		try
		{
			filled.RemoveAll((DishEffectInfoIcon s) => s == null);
			Vector3 mousePosition = Input.mousePosition;
			foreach (DishEffectInfoIcon item in filled)
			{
				HoverTooltip hoverTooltip = ((item.m_Tooltip != null) ? item.m_Tooltip.GetComponent<HoverTooltip>() : null);
				if (hoverTooltip == null)
				{
					continue;
				}
				bool flag = item.gameObject.activeInHierarchy && Under(item, mousePosition);
				if (flag && !opened.Contains(item.Pointer))
				{
					opened.Add(item.Pointer);
					if (!hoverTooltip.m_IsHovering)
					{
						hoverTooltip.OnHover(hover: true);
					}
					if (Data.Settings.TestKeys && told.Add(item.Pointer))
					{
						MelonLogger.Msg("Effect tooltip: mouse over \"" + item.m_TooltipLabel?.text?.Replace('\n', ' ') + "\", NGUI hovered " + (UICamera.hoveredObject?.name ?? "-"));
					}
				}
				else if (!flag && opened.Remove(item.Pointer) && hoverTooltip.m_IsHovering)
				{
					hoverTooltip.OnHover(hover: false);
				}
			}
		}
		catch (Exception e)
		{
			filled.Clear();
			Safe.Log("Effect tooltip", e);
		}
	}

	private static bool Under(DishEffectInfoIcon s, Vector3 mouse)
	{
		UIWidget uIWidget = ((s.m_Sprite != null) ? s.m_Sprite : null);
		if (uIWidget == null)
		{
			return false;
		}
		Camera camera = NGUITools.FindCameraForLayer(uIWidget.gameObject.layer);
		if (camera == null)
		{
			return false;
		}
		Il2CppStructArray<Vector3> worldCorners = uIWidget.worldCorners;
		Vector3 vector = camera.WorldToScreenPoint(worldCorners[0]);
		Vector3 vector2 = camera.WorldToScreenPoint(worldCorners[2]);
		if (mouse.x >= Mathf.Min(vector.x, vector2.x) && mouse.x <= Mathf.Max(vector.x, vector2.x) && mouse.y >= Mathf.Min(vector.y, vector2.y))
		{
			return mouse.y <= Mathf.Max(vector.y, vector2.y);
		}
		return false;
	}

	private static void Fill(DishEffectInfoIcon slot, EffectDef e, DishEffectInfoIcon first)
	{
		if (!filled.Contains(slot))
		{
			filled.Add(slot);
		}
		if (Data.Settings.TestKeys && !described)
		{
			described = true;
			try
			{
				System.Collections.Generic.IEnumerable<string> values = from c in slot.GetComponentsInChildren<Component>(includeInactive: true)
					select c.gameObject.name + ":" + c.GetIl2CppType().Name;
				MelonLogger.Msg("Effect slot: " + string.Join(", ", values) + "; tooltip " + ((slot.m_Tooltip != null) ? slot.m_Tooltip.name : "none"));
			}
			catch (Exception e2)
			{
				Safe.Log("Effect slot parts", e2);
			}
		}
		try
		{
			DishEffectInfoUI.DishEffectUIMapping dishEffectUIMapping = new DishEffectInfoUI.DishEffectUIMapping
			{
				m_EffectType = IFoodEffect.EffectType.MaxCondition,
				m_SpriteName = ((slot.m_Sprite != null) ? slot.m_Sprite.spriteName : ""),
				m_EffectText = new LocalizedString
				{
					m_LocalizationID = "SEAGULL_FXTIP_" + e.Id
				}
			};
			slot.ShowDishEffect(dishEffectUIMapping, (!e.Negative) ? IFoodEffect.Effect.Benefit : IFoodEffect.Effect.Detriment);
		}
		catch (Exception e3)
		{
			Safe.Log("Effect icon, the game's way", e3);
		}
		UITexture uITexture = Our(slot, make: true);
		if (uITexture != null)
		{
			uITexture.mainTexture = Icon(e.Icon);
			uITexture.enabled = true;
			if (slot.m_Sprite != null)
			{
				if (!alphas.ContainsKey(slot.m_Sprite.Pointer))
				{
					alphas[slot.m_Sprite.Pointer] = slot.m_Sprite.alpha;
				}
				slot.m_Sprite.alpha = 0.01f;
			}
		}
		string text = Texts.Get("SEAGULL_FX_" + e.Id + "_NAME");
		string text2 = Texts.Get("SEAGULL_FX_" + e.Id + "_DESC");
		Color color = (e.Negative ? first.m_DetrimentColor : first.m_BenefitColor);
		if (slot.m_Label != null)
		{
			slot.m_Label.text = text;
			slot.m_Label.color = color;
			Fit(slot.m_Label);
		}
		if (slot.m_TooltipLabel != null)
		{
			slot.m_TooltipLabel.text = ((string.IsNullOrEmpty(text2) || text2.StartsWith("SEAGULL_")) ? text : (text + "\n" + text2));
		}
		Arrows(slot.m_BenefitArrows, !e.Negative);
		Arrows(slot.m_DetrimentArrows, e.Negative);
		slot.gameObject.SetActive(value: true);
	}

	private static void Fit(UILabel l)
	{
		if (!sizing.ContainsKey(l.Pointer))
		{
			sizing[l.Pointer] = (l.overflowMethod, l.width);
		}
		l.overflowMethod = UILabel.Overflow.ShrinkContent;
		l.width = Math.Min((sizing[l.Pointer].width > 0 && sizing[l.Pointer].how != UILabel.Overflow.ResizeFreely) ? sizing[l.Pointer].width : 150, 150);
		l.ProcessText();
	}

	private static void Unfit(UILabel? l)
	{
		if (!(l == null) && sizing.TryGetValue(l.Pointer, out (UILabel.Overflow, int) value))
		{
			(l.overflowMethod, l.width) = value;
			sizing.Remove(l.Pointer);
		}
	}

	private static void Arrows(Il2CppReferenceArray<GameObject>? arrows, bool on)
	{
		if (arrows == null)
		{
			return;
		}
		for (int i = 0; i < arrows.Length; i++)
		{
			if (arrows[i] != null)
			{
				arrows[i].SetActive(on && i == 0);
			}
		}
	}
}

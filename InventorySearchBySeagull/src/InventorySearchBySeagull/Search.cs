using System;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using SeagullUi;
using UnityEngine;

namespace InventorySearchBySeagull;

internal static class Search
{
	public static string Query = "";

	public static string ConQuery = "";

	private static string s_Shown = "";

	private static string s_ConShown = "";

	private static object s_OpenPanel;

	private static readonly TextBox s_Box = new TextBox
	{
		EscClears = true
	};

	private static readonly TextBox s_ConBox = new TextBox
	{
		EscClears = true
	};

	private static Texture2D s_Shade;

	private static GUIStyle s_X;

	private static bool Ru
	{
		get
		{
			try
			{
				return Localization.Language == "Russian";
			}
			catch
			{
				return false;
			}
		}
	}

	private static string Norm(string s)
	{
		return (s ?? "").ToLowerInvariant().Replace('ё', 'е').Trim();
	}

	private static bool Match(InventoryGridDataItem d, string q)
	{
		if (q.Length == 0 || d == null)
		{
			return true;
		}
		string s = "";
		string s2 = "";
		try
		{
			s = ((d.m_GearItem != null) ? d.m_GearItem.DisplayName : d.DisplayName);
		}
		catch
		{
		}
		try
		{
			s2 = ((d.m_GearItem != null) ? d.m_GearItem.name : "");
		}
		catch
		{
		}
		if (!Norm(s).Contains(q))
		{
			return Norm(s2).Contains(q);
		}
		return true;
	}

	public static void Filter(List<InventoryGridDataItem> list, string query)
	{
		string text = Norm(query);
		if (text.Length == 0 || list == null)
		{
			return;
		}
		for (int num = list.Count - 1; num >= 0; num--)
		{
			if (!Match(list[num], text))
			{
				list.RemoveAt(num);
			}
		}
	}

	private static Panel_Inventory Inv()
	{
		try
		{
			Panel_Inventory panel;
			return (InterfaceManager.TryGetPanel<Panel_Inventory>(out panel) && panel.IsEnabled()) ? panel : null;
		}
		catch
		{
			return null;
		}
	}

	private static Panel_Container Con()
	{
		try
		{
			Panel_Container panel;
			return (InterfaceManager.TryGetPanel<Panel_Container>(out panel) && panel.IsEnabled()) ? panel : null;
		}
		catch
		{
			return null;
		}
	}

	private static void Refresh(Panel_Inventory inv, Panel_Container con)
	{
		try
		{
			if (inv != null)
			{
				inv.m_SelectedItemIndex = 0;
				inv.m_FirstItemDisplayedIndex = 0;
				inv.RefreshTable();
			}
			if (con != null)
			{
				con.m_InventorySelectedItemIndex = 0;
				con.m_InventoryFirstItemDisplayedIndex = 0;
				con.m_ContainerSelectedItemIndex = 0;
				con.m_ContainerFirstItemDisplayedIndex = 0;
				con.ForceRefresh();
			}
		}
		catch (Exception e)
		{
			Once.Log("refresh", e);
		}
	}

	private static void Box(TextBox box, Rect r, string hint, float k)
	{
		if (s_Shade == null)
		{
			s_Shade = new Texture2D(1, 1);
			s_Shade.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.35f));
			s_Shade.Apply();
			s_Shade.hideFlags = HideFlags.HideAndDontSave;
		}
		GUI.DrawTexture(new Rect(r.x - 3f * k, r.y - 3f * k, r.width + 6f * k, r.height + 6f * k), s_Shade);
		Rect position = new Rect(r.xMax - r.height, r.y, r.height, r.height);
		Event current = Event.current;
		if (box.Text.Length > 0 && current != null && current.type == EventType.MouseDown && position.Contains(current.mousePosition))
		{
			box.Text = "";
			box.Focused = false;
			current.Use();
		}
		box.Draw(r, hint, k);
		if (box.Text.Length > 0)
		{
			if (s_X == null)
			{
				s_X = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter,
					fontStyle = FontStyle.Bold
				};
			}
			s_X.fontSize = Mathf.RoundToInt(20f * k);
			s_X.normal.textColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
			GUI.Label(position, "✕", s_X);
		}
	}

	public static void Draw()
	{
		Panel_Inventory panel_Inventory = Inv();
		Panel_Container panel_Container = ((panel_Inventory == null) ? Con() : null);
		object obj = ((object)panel_Inventory) ?? ((object)panel_Container);
		if (obj == null)
		{
			s_OpenPanel = null;
			s_Box.Focused = (s_ConBox.Focused = false);
			InputBlock.Set(on: false);
			return;
		}
		if (obj != s_OpenPanel)
		{
			s_OpenPanel = obj;
			s_Box.Focused = (s_ConBox.Focused = false);
			if (s_Box.Text.Length > 0 || s_ConBox.Text.Length > 0)
			{
				s_Box.Text = (s_ConBox.Text = (Query = (ConQuery = (s_Shown = (s_ConShown = "")))));
				Refresh(panel_Inventory, panel_Container);
			}
		}
		float num = (float)Screen.height / 1080f;
		bool ru = Ru;
		float height = 40f * num;
		Box(s_Box, new Rect(250f * num, 50f * num, 360f * num, height), ru ? "Поиск в рюкзаке…" : "Search the backpack…", num);
		if (panel_Container != null)
		{
			Box(s_ConBox, new Rect(1187f * num + ((float)Screen.width - 1920f * num), 50f * num, 465f * num, height), ru ? "Поиск в контейнере…" : "Search the container…", num);
		}
		else
		{
			s_ConBox.Focused = false;
		}
		InputBlock.Set(s_Box.Focused || s_ConBox.Focused);
		Query = s_Box.Text;
		ConQuery = s_ConBox.Text;
		if (Query != s_Shown || ConQuery != s_ConShown)
		{
			s_Shown = Query;
			s_ConShown = ConQuery;
			Refresh(panel_Inventory, panel_Container);
		}
	}
}

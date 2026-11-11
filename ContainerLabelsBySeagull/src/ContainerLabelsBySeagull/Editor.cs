using System;
using System.Linq;
using Il2Cpp;
using SeagullUi;
using UnityEngine;

namespace ContainerLabelsBySeagull;

internal static class Editor
{
	public static bool Focused;

	private static Container s_For;

	private static readonly TextBox s_Box = new TextBox
	{
		Max = 40
	};

	private static int s_Color = 1;

	private static GUIStyle s_Title;

	private static GUIStyle s_X;

	private static Texture2D[] s_SwatchTex;

	private static Texture2D s_Ring;

	private static Texture2D s_Shade;

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

	private static Texture2D Solid(Color c)
	{
		Texture2D texture2D = new Texture2D(1, 1);
		texture2D.SetPixel(0, 0, c);
		texture2D.Apply();
		texture2D.hideFlags = HideFlags.HideAndDontSave;
		return texture2D;
	}

	private static Color Hex(string h)
	{
		return new Color((float)Convert.ToInt32(h.Substring(0, 2), 16) / 255f, (float)Convert.ToInt32(h.Substring(2, 2), 16) / 255f, (float)Convert.ToInt32(h.Substring(4, 2), 16) / 255f, 1f);
	}

	private static Panel_Container Panel()
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

	public static void Draw()
	{
		Container container = Panel()?.m_Container;
		if (container == null)
		{
			s_For = null;
			Focused = (s_Box.Focused = false);
			InputBlock.Set(on: false);
			return;
		}
		if (container != s_For)
		{
			s_For = container;
			Entry entry = Labels.Get(container);
			s_Box.Text = entry?.Text ?? "";
			s_Box.Focused = false;
			s_Color = entry?.Color ?? 1;
		}
		float num = (float)Screen.height / 1080f;
		bool ru = Ru;
		if (s_SwatchTex == null)
		{
			s_SwatchTex = Palette.All.Select(((string hex, string ru, string en) p) => Solid(Hex(p.hex))).ToArray();
			s_Ring = Solid(Color.white);
			s_Shade = Solid(new Color(0f, 0f, 0f, 0.35f));
		}
		if (s_Title == null)
		{
			s_Title = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleLeft
			};
		}
		s_Title.fontSize = Mathf.RoundToInt(19f * num);
		s_Title.normal.textColor = new Color(0.92f, 0.92f, 0.9f, 1f);
		float num2 = 40f * num;
		float num3 = 30f * num;
		float num4 = 9f * num;
		float num5 = 440f * num;
		Rect rect = new Rect((float)Screen.width / 2f - 60f * num - num5 / 2f, 34f * num, num5, num2);
		GUI.DrawTexture(new Rect(rect.x - 6f * num, rect.y - 6f * num, num5 + 12f * num, num2 + num3 + 22f * num), s_Shade);
		Rect r = new Rect(rect.x, rect.y, num5, num2);
		Rect position = new Rect(r.xMax - num2, r.y, num2, num2);
		Event current = Event.current;
		bool flag = false;
		if (s_Box.Text.Length > 0 && current != null && current.type == EventType.MouseDown && position.Contains(current.mousePosition))
		{
			s_Box.Text = "";
			s_Box.Focused = false;
			flag = true;
			current.Use();
		}
		flag |= s_Box.Draw(r, ru ? "Подпись шкафа, например Мясо" : "Container label, e.g. Meat", num);
		if (s_Box.Text.Length > 0)
		{
			if (s_X == null)
			{
				s_X = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter,
					fontStyle = FontStyle.Bold
				};
			}
			s_X.fontSize = Mathf.RoundToInt(20f * num);
			s_X.normal.textColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
			GUI.Label(position, "✕", s_X);
		}
		float num6 = rect.x + (num5 - ((float)Palette.All.Length * (num3 + num4) - num4)) / 2f;
		float y = rect.yMax + 8f * num;
		for (int i = 0; i < Palette.All.Length; i++)
		{
			Rect position2 = new Rect(num6, y, num3, num3);
			if (i == s_Color)
			{
				GUI.DrawTexture(new Rect(position2.x - 3f * num, position2.y - 3f * num, position2.width + 6f * num, position2.height + 6f * num), s_Ring);
			}
			GUI.DrawTexture(position2, s_SwatchTex[i]);
			if (current != null && current.type == EventType.MouseDown && position2.Contains(current.mousePosition))
			{
				s_Color = i;
				flag = true;
				current.Use();
			}
			num6 += num3 + num4;
		}
		Focused = s_Box.Focused;
		InputBlock.Set(Focused);
		if (flag)
		{
			Labels.Set(container, s_Box.Text, s_Color);
		}
	}
}

using System;
using UnityEngine;

namespace EmperorUi;

internal sealed class TextBox
{
	public string Text = "";

	public bool Focused;

	public int Max = 40;

	public bool EscClears;

	private static Texture2D s_Bg;

	private static Texture2D s_BgFocus;

	private static Texture2D s_Border;

	private static GUIStyle s_Text;

	private static GUIStyle s_Hint;

	private static float s_K;

	private static Texture2D Solid(Color c)
	{
		Texture2D texture2D = new Texture2D(1, 1);
		texture2D.SetPixel(0, 0, c);
		texture2D.Apply();
		texture2D.hideFlags = HideFlags.HideAndDontSave;
		return texture2D;
	}

	private static void Styles(float k)
	{
		if (s_Text == null || !(Math.Abs(s_K - k) < 0.01f))
		{
			s_K = k;
			if ((object)s_Bg == null)
			{
				s_Bg = Solid(new Color(0.8f, 0.8f, 0.78f, 0.88f));
			}
			if ((object)s_BgFocus == null)
			{
				s_BgFocus = Solid(new Color(0.93f, 0.93f, 0.91f, 0.97f));
			}
			if ((object)s_Border == null)
			{
				s_Border = Solid(new Color(1f, 1f, 1f, 0.9f));
			}
			s_Text = new GUIStyle(GUI.skin.label)
			{
				fontSize = Mathf.RoundToInt(19f * k),
				alignment = TextAnchor.MiddleLeft,
				clipping = TextClipping.Clip,
				wordWrap = false
			};
			s_Text.normal.textColor = new Color(0.08f, 0.08f, 0.08f, 1f);
			s_Hint = new GUIStyle(s_Text);
			s_Hint.normal.textColor = new Color(0.25f, 0.25f, 0.25f, 0.75f);
		}
	}

	public bool Draw(Rect r, string hint, float k)
	{
		Styles(k);
		string text = Text;
		Event current = Event.current;
		if (current != null)
		{
			if (current.type == EventType.MouseDown)
			{
				if (r.Contains(current.mousePosition))
				{
					Focused = true;
					current.Use();
				}
				else
				{
					Focused = false;
				}
			}
			else if (Focused && current.type == EventType.KeyDown)
			{
				KeyCode keyCode = current.keyCode;
				char character = current.character;
				switch (keyCode)
				{
				case KeyCode.Backspace:
					if (Text.Length > 0)
					{
						Text = Text.Substring(0, Text.Length - 1);
					}
					current.Use();
					break;
				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					Focused = false;
					current.Use();
					break;
				case KeyCode.Escape:
					if (EscClears)
					{
						Text = "";
					}
					Focused = false;
					current.Use();
					break;
				default:
					if (character != 0 && !char.IsControl(character))
					{
						if (Text.Length < Max)
						{
							Text += character;
						}
						current.Use();
					}
					break;
				}
			}
		}
		GUI.DrawTexture(r, Focused ? s_BgFocus : s_Bg);
		if (Focused)
		{
			float num = Math.Max(1f, 2f * k);
			GUI.DrawTexture(new Rect(r.x, r.y, r.width, num), s_Border);
			GUI.DrawTexture(new Rect(r.x, r.yMax - num, r.width, num), s_Border);
			GUI.DrawTexture(new Rect(r.x, r.y, num, r.height), s_Border);
			GUI.DrawTexture(new Rect(r.xMax - num, r.y, num, r.height), s_Border);
		}
		Rect position = new Rect(r.x + 10f * k, r.y, r.width - 20f * k, r.height);
		if (Text.Length == 0 && !Focused)
		{
			GUI.Label(position, hint, s_Hint);
		}
		else
		{
			bool flag = Focused && (int)(Time.unscaledTime * 2f) % 2 == 0;
			GUI.Label(position, Text + (flag ? "|" : ""), s_Text);
		}
		return Text != text;
	}
}

using Il2Cpp;
using UnityEngine;

namespace MarkersBySeagull;

internal static class MapButton
{
	private static Texture2D s_Bg;

	private static Texture2D s_BgHover;

	private static GUIStyle s_Text;

	private static Texture2D Solid(Color c)
	{
		Texture2D texture2D = new Texture2D(1, 1);
		texture2D.SetPixel(0, 0, c);
		texture2D.Apply();
		texture2D.hideFlags = HideFlags.HideAndDontSave;
		return texture2D;
	}

	public static void Draw()
	{
		bool flag = false;
		try
		{
			flag = InterfaceManager.TryGetPanel<Panel_Map>(out var panel) && panel.IsEnabled();
		}
		catch
		{
		}
		if (flag)
		{
			float num = (float)Screen.height / 1080f;
			Rect position = new Rect((float)Screen.width - 750f * num, 995f * num, 270f * num, 40f * num);
			if ((object)s_Bg == null)
			{
				s_Bg = Solid(new Color(0f, 0f, 0f, 0.75f));
			}
			if ((object)s_BgHover == null)
			{
				s_BgHover = Solid(new Color(0.18f, 0.18f, 0.18f, 0.85f));
			}
			if (s_Text == null)
			{
				s_Text = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter
				};
			}
			s_Text.fontSize = Mathf.RoundToInt(19f * num);
			Event current = Event.current;
			bool flag2 = current != null && position.Contains(current.mousePosition);
			if (current != null && current.type == EventType.MouseDown && flag2)
			{
				Config.HudOn = !Config.HudOn;
				Config.Save();
				current.Use();
			}
			GUI.DrawTexture(position, flag2 ? s_BgHover : s_Bg);
			bool russian = Texts.Russian;
			s_Text.normal.textColor = (Config.HudOn ? new Color(1f, 0.55f, 0.2f, 1f) : new Color(0.6f, 0.6f, 0.6f, 1f));
			string text = ((!russian) ? (Config.HudOn ? "MARKER ICONS: ON" : "MARKER ICONS: OFF") : (Config.HudOn ? "МЕТКИ ВЕШЕК: ВКЛ" : "МЕТКИ ВЕШЕК: ВЫКЛ"));
			GUI.Label(position, text, s_Text);
		}
	}
}

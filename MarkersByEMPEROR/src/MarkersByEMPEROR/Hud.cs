using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace MarkersByEMPEROR;

internal static class Hud
{
	private static readonly List<GearItem> s_Items = new List<GearItem>();

	private static readonly List<(Vector3 pos, float dist)> s_Visible = new List<(Vector3, float)>();

	private static float s_NextScan;

	private static GUIStyle s_Style;

	private static GUIStyle s_Shadow;

	private static float s_NextReport;

	public static void Track(GearItem g)
	{
		if (Alive(g) && !s_Items.Contains(g))
		{
			s_Items.Add(g);
		}
	}

	public static bool Alive(GearItem g)
	{
		try
		{
			return g != null && g.Pointer != IntPtr.Zero && !g.WasCollected && g.gameObject != null;
		}
		catch
		{
			return false;
		}
	}

	private static bool Placed(GearItem g)
	{
		if (!Alive(g))
		{
			return false;
		}
		if (!g.gameObject.activeInHierarchy)
		{
			return false;
		}
		if (g.m_InPlayerInventory || g.m_InsideContainer)
		{
			return false;
		}
		return true;
	}

	public static void Tick()
	{
		s_Visible.Clear();
		if (Time.time >= s_NextScan)
		{
			s_NextScan = Time.time + 3f;
			s_Items.RemoveAll((GearItem g) => !Alive(g));
			foreach (GearItem item in UnityEngine.Object.FindObjectsOfType<GearItem>())
			{
				if (item != null && Marker.Is(item.name))
				{
					Track(item);
					Repair.Maybe(item);
				}
			}
		}
		s_Items.RemoveAll((GearItem g) => !Alive(g));
		foreach (GearItem s_Item in s_Items)
		{
			try
			{
				if (Placed(s_Item))
				{
					Plant.Maybe(s_Item);
				}
			}
			catch (Exception e)
			{
				Safe.Log("Plant", e);
			}
		}
		if (InterfaceManager.IsMainMenuEnabled() || Cursor.visible)
		{
			Report(0);
			return;
		}
		Camera mainCamera = GameManager.GetMainCamera();
		if (mainCamera == null)
		{
			return;
		}
		Vector3 position = mainCamera.transform.position;
		foreach (GearItem s_Item2 in s_Items)
		{
			if (!Placed(s_Item2) || !Config.HudOn)
			{
				continue;
			}
			Vector3 vector = s_Item2.transform.position + Vector3.up * 1.45f;
			float num = Vector3.Distance(position, vector);
			if (!(num < 3f) && !(num > Config.Range))
			{
				Vector3 vector2 = mainCamera.WorldToScreenPoint(vector);
				if (!(vector2.z <= 0f))
				{
					s_Visible.Add((new Vector3(vector2.x, (float)Screen.height - vector2.y, 0f), num));
				}
			}
		}
		Report(s_Visible.Count);
	}

	private static void Report(int shown)
	{
		if (s_Items.Count != 0 && !(Time.time < s_NextReport))
		{
			s_NextReport = Time.time + 600f;
			int value = s_Items.Count(Placed);
			MelonLogger.Msg($"HUD: {s_Items.Count} markers known, {value} placed, {shown} on screen, cursor {(Cursor.visible ? "shown" : "hidden")}.");
		}
	}

	public static void Draw()
	{
		if (s_Visible.Count == 0 || Event.current == null || Event.current.type != EventType.Repaint)
		{
			return;
		}
		if (s_Style == null)
		{
			s_Style = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleCenter,
				fontSize = 15,
				fontStyle = FontStyle.Bold
			};
			s_Style.normal.textColor = new Color(1f, 0.42f, 0.08f, 0.95f);
			s_Shadow = new GUIStyle(s_Style);
			s_Shadow.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
		}
		bool russian = Texts.Russian;
		foreach (var item3 in s_Visible)
		{
			Vector3 item = item3.pos;
			float item2 = item3.dist;
			string text = "▼\n" + Mathf.RoundToInt(item2) + (russian ? " м" : " m");
			Rect position = new Rect(item.x - 40f, item.y - 34f, 80f, 40f);
			GUI.Label(new Rect(position.x + 1f, position.y + 1f, position.width, position.height), text, s_Shadow);
			GUI.Label(position, text, s_Style);
		}
	}
}

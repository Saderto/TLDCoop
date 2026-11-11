using System;
using Il2Cpp;
using Il2CppInterop.Runtime.Injection;
using MelonLoader;
using UnityEngine;

namespace InventorySearchBySeagull;

internal static class InputBlock
{
	private static SeagullSearchInputBlocker s_Ctx;

	private static bool s_On;

	private static bool s_Registered;

	public static void Set(bool on)
	{
		if (on == s_On)
		{
			return;
		}
		try
		{
			if (on)
			{
				if (!s_Registered)
				{
					ClassInjector.RegisterTypeInIl2Cpp<SeagullSearchInputBlocker>();
					s_Registered = true;
				}
				if (s_Ctx == null)
				{
					GameObject gameObject = new GameObject("SeagullSearchInputBlocker");
					UnityEngine.Object.DontDestroyOnLoad(gameObject);
					gameObject.hideFlags = HideFlags.HideAndDontSave;
					s_Ctx = gameObject.AddComponent<SeagullSearchInputBlocker>();
				}
				InputManager.PushContext(s_Ctx);
			}
			else if (s_Ctx != null)
			{
				InputManager.PopContext(s_Ctx);
			}
			s_On = on;
		}
		catch (Exception ex)
		{
			MelonLogger.Error("input block: " + ex);
			s_On = on;
		}
	}
}

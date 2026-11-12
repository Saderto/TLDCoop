using System;
using Il2Cpp;
using Il2CppInterop.Runtime.Injection;
using MelonLoader;
using UnityEngine;

namespace ContainerLabelsByEMPEROR;

internal static class InputBlock
{
	private static EmperorLabelInputBlocker s_Ctx;

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
					ClassInjector.RegisterTypeInIl2Cpp<EmperorLabelInputBlocker>();
					s_Registered = true;
				}
				if (s_Ctx == null)
				{
					GameObject gameObject = new GameObject("EmperorLabelInputBlocker");
					UnityEngine.Object.DontDestroyOnLoad(gameObject);
					gameObject.hideFlags = HideFlags.HideAndDontSave;
					s_Ctx = gameObject.AddComponent<EmperorLabelInputBlocker>();
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

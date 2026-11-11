using System;
using System.Text;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace MarkersBySeagull;

internal static class Diag
{
	private static int s_Count;

	public static void Dump(GearItem g, string when)
	{
		if (s_Count++ > 40)
		{
			return;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(72, 10, stringBuilder2);
			handler.AppendLiteral("Marker [");
			handler.AppendFormatted(when);
			handler.AppendLiteral("] '");
			handler.AppendFormatted(g.name);
			handler.AppendLiteral("' id ");
			handler.AppendFormatted(g.GetInstanceID());
			handler.AppendLiteral(" pos ");
			handler.AppendFormatted(g.transform.position);
			handler.AppendLiteral(" rot ");
			handler.AppendFormatted(g.transform.eulerAngles);
			handler.AppendLiteral(" active ");
			handler.AppendFormatted(g.gameObject.activeInHierarchy);
			handler.AppendLiteral(" inInv ");
			handler.AppendFormatted(g.m_InPlayerInventory);
			handler.AppendLiteral(" inCont ");
			handler.AppendFormatted(g.m_InsideContainer);
			handler.AppendLiteral(" nonInteractive ");
			handler.AppendFormatted(g.NonInteractive);
			handler.AppendLiteral(" layer ");
			handler.AppendFormatted(g.gameObject.layer);
			stringBuilder3.Append(ref handler);
			foreach (Collider componentsInChild in g.GetComponentsInChildren<Collider>(includeInactive: true))
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder4 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(66, 8, stringBuilder2);
				handler.AppendLiteral("\n   collider ");
				handler.AppendFormatted(componentsInChild.GetIl2CppType().Name);
				handler.AppendLiteral(" on '");
				handler.AppendFormatted(componentsInChild.gameObject.name);
				handler.AppendLiteral("' enabled ");
				handler.AppendFormatted(componentsInChild.enabled);
				handler.AppendLiteral(" active ");
				handler.AppendFormatted(componentsInChild.gameObject.activeInHierarchy);
				handler.AppendLiteral(" trigger ");
				handler.AppendFormatted(componentsInChild.isTrigger);
				handler.AppendLiteral(" layer ");
				handler.AppendFormatted(componentsInChild.gameObject.layer);
				handler.AppendLiteral(" bounds ");
				handler.AppendFormatted(componentsInChild.bounds.center);
				handler.AppendLiteral(" size ");
				handler.AppendFormatted(componentsInChild.bounds.size);
				stringBuilder4.Append(ref handler);
			}
			foreach (Rigidbody componentsInChild2 in g.GetComponentsInChildren<Rigidbody>(includeInactive: true))
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder5 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(49, 4, stringBuilder2);
				handler.AppendLiteral("\n   rigidbody on '");
				handler.AppendFormatted(componentsInChild2.gameObject.name);
				handler.AppendLiteral("' kinematic ");
				handler.AppendFormatted(componentsInChild2.isKinematic);
				handler.AppendLiteral(" gravity ");
				handler.AppendFormatted(componentsInChild2.useGravity);
				handler.AppendLiteral(" sleeping ");
				handler.AppendFormatted(componentsInChild2.IsSleeping());
				stringBuilder5.Append(ref handler);
			}
			if (Physics.Raycast(g.transform.position + Vector3.up * 1.6f, Vector3.down, out var hitInfo, 4f, -1, QueryTriggerInteraction.Ignore))
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder6 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(28, 2, stringBuilder2);
				handler.AppendLiteral("\n   first hit down: '");
				handler.AppendFormatted(hitInfo.collider.gameObject.name);
				handler.AppendLiteral("' at y ");
				handler.AppendFormatted(hitInfo.point.y, "F3");
				stringBuilder6.Append(ref handler);
			}
			MelonLogger.Msg(stringBuilder.ToString());
		}
		catch (Exception e)
		{
			Safe.Log("Dump", e);
		}
	}
}

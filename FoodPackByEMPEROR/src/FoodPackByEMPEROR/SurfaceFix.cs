// 1.1.4-1.1.6: grain fix (game detail/normal slots cleared), double-sided meshes, the game's effect icon switched off under ours
namespace FoodPackByEMPEROR {
using Il2Cpp; using UnityEngine; using UnityEngine.Rendering; using MelonLoader;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
public static class SurfaceFix {
  static Texture2D flatNormal;
  static System.Collections.Generic.HashSet<string> logged = new System.Collections.Generic.HashSet<string>();
  /// 1.1.4 (grain fix, from TeaCoffee 0.5.5): set our picture and clear the game material's detail/normal slots (the grain was _DetailOverlay)
  public static void SetMain(Material m, Texture t) {
    m.mainTexture = t;
    try { Flat(m); } catch (System.Exception e) { MelonLogger.Warning("Surface: " + e.Message); }
  }
  static void Flat(Material m) {
    if (m == null) return;
    if (flatNormal == null) {
      flatNormal = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
      var px = new Il2CppStructArray<Color32>(16);
      for (int i = 0; i < 16; i++) px[i] = new Color32(128, 128, 255, 255);
      flatNormal.SetPixels32(px); flatNormal.Apply(false, true);
      Object.DontDestroyOnLoad(flatNormal); flatNormal.hideFlags = HideFlags.HideAndDontSave;
    }
    var names = m.GetTexturePropertyNames();
    string all = "";
    for (int i = 0; i < names.Length; i++) {
      string n = names[i]; all += " " + n;
      string l = n.ToLowerInvariant();
      if (l.Contains("bump") || l.Contains("normal")) m.SetTexture(n, flatNormal);
      else if (l.Contains("detail")) m.SetTexture(n, null);
    }
    if (m.HasProperty("_DetailNormalMapScale")) m.SetFloat("_DetailNormalMapScale", 0f);
    m.DisableKeyword("_DETAIL_MULX2");
    string sh = m.shader != null ? m.shader.name : "?";
    if (logged.Add(sh)) MelonLogger.Msg("surface " + sh + ":" + all);
  }
  /// 1.1.4: the game's shaders do not draw back faces, so insides of lids and cups were see-through: every face gets a back copy (normal reversed)
  public static void DoubleSide(Mesh mesh) {
    try {
      var v = mesh.vertices; var n = mesh.normals; var u = mesh.uv; var t = mesh.triangles;
      int fv = v.Length, fi = t.Length;
      if (fv == 0 || n.Length != fv || u.Length != fv) return;
      var v2 = new Il2CppStructArray<Vector3>(fv * 2); var n2 = new Il2CppStructArray<Vector3>(fv * 2);
      var u2 = new Il2CppStructArray<Vector2>(fv * 2); var c2 = new Il2CppStructArray<Color32>(fv * 2);
      var t2 = new Il2CppStructArray<int>(fi * 2);
      for (int i = 0; i < fv; i++) {
        Vector3 p = v[i], q = n[i]; Vector2 w = u[i];
        v2[i] = p; v2[fv + i] = p; n2[i] = q; n2[fv + i] = -q; u2[i] = w; u2[fv + i] = w;
        c2[i] = new Color32(255, 255, 255, 255); c2[fv + i] = new Color32(255, 255, 255, 255);
      }
      for (int i = 0; i + 2 < fi; i += 3) {
        int a = t[i], b = t[i + 1], c = t[i + 2];
        t2[i] = a; t2[i + 1] = b; t2[i + 2] = c;
        t2[fi + i] = a + fv; t2[fi + i + 1] = c + fv; t2[fi + i + 2] = b + fv;
      }
      mesh.indexFormat = IndexFormat.UInt32;
      mesh.triangles = new Il2CppStructArray<int>(0);
      mesh.vertices = v2; mesh.normals = n2; mesh.uv = u2; mesh.uv2 = u2; mesh.colors32 = c2; mesh.triangles = t2;
    } catch (System.Exception e) { MelonLogger.Warning("Mesh " + mesh.name + ": " + e.Message); }
  }

  /// 1.1.5 (from Smokes 0.1.165): the game's effect sprite showed under our icon; alpha .01 did not hold, so the sprite is switched off
  public static void HideSprite(UIWidget w, float a) {
    w.alpha = a;
    try { if (a < 0.02f) w.enabled = false; } catch { }
  }
  static bool dumped;
  /// 1.1.6: every frame (EffectRow.Hover), for every slot where our picture is on, the game's own icon is switched off whatever its alpha
  /// (1.1.5 looked at alpha only and the game's icon still showed under ours, user 11-15). Its dark back (EffectIconBG) stays.
  public static void Keep(System.Collections.Generic.List<DishEffectInfoIcon> filled) {
    for (int i = 0; i < filled.Count; i++) {
      try {
        var s = filled[i];
        if (s == null || s.m_Sprite == null) continue;
        var w = s.m_Sprite;
        var p = w.transform.parent;
        var t = p != null ? p.Find("SeagullEffectTexture") : null;
        var ours = t != null ? t.GetComponent<UITexture>() : null;
        if (ours == null || !ours.enabled) continue;
        if (!dumped) {
          dumped = true;
          var sb = new System.Text.StringBuilder("Effect row slot:");
          foreach (var x in s.GetComponentsInChildren<UIWidget>(true))
            sb.Append("\n  " + x.gameObject.name + " [" + x.GetIl2CppType().Name + "] a " + x.alpha.ToString("0.##") + " on " + x.enabled + " depth " + x.depth
              + (x.Pointer == w.Pointer ? " = game icon" : "") + (x.Pointer == ours.Pointer ? " = ours" : ""));
          MelonLogger.Msg(sb.ToString());
        }
        if (w.enabled) w.enabled = false;
      } catch { }
    }
  }
}
}

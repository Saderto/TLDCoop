using System;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace TLDShotgun
{
    /// <summary>
    /// Background picture of the «Дробовик» skill in the journal: Mods/TLDShotgun/skill_shotgun.png (the user's own
    /// screenshot: shotgun and shell boxes on a workbench, 1080×540). Put on Panel_Log.m_SkillImageLarge after the game
    /// draws the selected skill; cropped by uvRect to the size of the game's own picture.
    /// </summary>
    internal static class SkillImage
    {
        const string File_ = "skill_shotgun.png";
        static Texture2D _tex;
        static bool _failed;

        internal static void Patch(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(Panel_Log), nameof(Panel_Log.RefreshSelectedSkillDescriptionView)),
                postfix: new HarmonyMethod(typeof(SkillImage), nameof(RefreshPostfix)));
            h.Patch(AccessTools.Method(typeof(Panel_Log), nameof(Panel_Log.ReleaseTempResources_Skills)),
                prefix: new HarmonyMethod(typeof(SkillImage), nameof(ReleasePrefix)));
        }

        static Texture2D Tex()
        {
            if (_tex != null || _failed) return _tex;
            try
            {
                var path = Path.Combine(ShotgunMod.Folder, File_);
                if (!File.Exists(path)) { _failed = true; ShotgunMod.Warn("нет картинки навыка / missing " + path); return null; }
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(t, File.ReadAllBytes(path), false)) { _failed = true; return null; }
                t.name = "TLDShotgun_skill";
                t.wrapMode = TextureWrapMode.Clamp;
                t.hideFlags = HideFlags.DontUnloadUnusedAsset;
                _tex = t;
            }
            catch (Exception e) { _failed = true; ShotgunMod.Warn("skill image: " + e.Message); }
            return _tex;
        }

        static bool Ours(Panel_Log p)
        {
            try
            {
                var label = p.m_SkillName;
                if (label == null) return false;
                string ours = Localization.Get(ShotgunSkill.NameKey);
                return !string.IsNullOrEmpty(ours) && string.Equals(label.text, ours, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        static Rect _gameUv = new Rect(0, 0, 1, 1);
        static bool _shown;

        static void RefreshPostfix(Panel_Log __instance)
        {
            try
            {
                var img = __instance.m_SkillImageLarge;
                if (img == null) return;
                if (!Ours(__instance))
                {
                    if (_shown) { img.uvRect = _gameUv; _shown = false; }
                    return;
                }
                var t = Tex();
                if (t == null) return;
                var cur = img.mainTexture;
                float target = img.width > 0 && img.height > 0 ? (float)img.width / img.height
                             : cur != null && cur.height > 0 ? (float)cur.width / cur.height : 2f;
                if (!_shown) _gameUv = img.uvRect;
                float ours = (float)t.width / t.height;
                Rect uv = new Rect(0, 0, 1, 1);
                if (target > ours) { float hh = ours / target; uv = new Rect(0, (1 - hh) / 2, 1, hh); }
                else if (target < ours) { float ww = target / ours; uv = new Rect((1 - ww) / 2, 0, ww, 1); }
                img.mainTexture = t;
                img.uvRect = uv;
                _shown = true;
            }
            catch (Exception e) { ShotgunMod.Warn("skill image: " + e.Message); }
        }

        // the panel frees the picture it loaded; ours must not go with it
        static void ReleasePrefix(Panel_Log __instance)
        {
            try
            {
                var img = __instance.m_SkillImageLarge;
                if (img != null && _tex != null && img.mainTexture != null && img.mainTexture.Pointer == _tex.Pointer)
                { img.mainTexture = null; img.uvRect = _gameUv; _shown = false; }
            }
            catch { }
        }
    }
}

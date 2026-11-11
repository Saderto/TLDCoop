using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace FeatsBySeagull
{
    /// <summary>
    /// Our feats are plain game objects of the vanilla class Feat_CelestialNavigator (no injected class: the game's GC did not
    /// trace references held by an injected type, texts turned into garbage). For that class IsUnlocked = m_Unlocked and
    /// GetNormalizedProgress = 0/1, SetNormalizedProgress(1) sets m_Unlocked + kicker + saves the profile; FeatsManager only
    /// asks GetFeat(CelestialNavigator) for the vanilla one, so the walking bonus never reads ours. m_Unlocked is synced with
    /// <see cref="Progress"/> both ways (a trainer unlock sets m_Unlocked).
    /// </summary>
    internal static class FeatObjects
    {
        static readonly Dictionary<string, Feat_CelestialNavigator> _feats = new();
        static readonly HashSet<string> _enabled = new();
        static float _nextRefresh;

        public static void RegisterType() { }

        /// <summary>Creates our feats once and adds them to FeatsManager.m_Feats (after the game's own, which FeatsManager.Awake makes).</summary>
        public static void Ensure()
        {
            try
            {
                var list = FeatsManager.m_Feats;
                if (list == null || list.Count == 0) return;  // game feats not made yet; FeatsManager.Awake postfix calls us again
                Feat vanilla = null;
                for (int i = 0; i < list.Count; i++)
                {
                    var f = list[i];
                    if (f == null) continue;
                    if ((int)f.m_FeatType < 1000 && vanilla == null) vanilla = f;
                }
                foreach (var d in Defs.All)
                {
                    if (_feats.TryGetValue(d.Id, out var have) && have != null && have.WasCollected == false)
                    {
                        if (!list.Contains(have)) list.Add(have);
                        continue;
                    }
                    var go = new GameObject("Feat_SGL_" + d.Id);
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    var f = go.AddComponent<Feat_CelestialNavigator>();
                    f.m_Unlocked = Progress.IsUnlocked(d);
                    f.m_WalkingSpeedBoostPercent = 0f;
                    f.m_FeatType = (FeatType)d.Type;
                    f.m_LocalizedDisplayName = Loc(d.KeyName);
                    f.m_LocalizedDescription = Loc(d.KeyDesc);
                    f.m_LocalizedShortDescription = Loc(d.KeyDesc);
                    f.m_LocalizedBonusDescription = Loc(d.KeyBonus);
                    f.m_UnlockedBadge = new AssetReferenceTexture2D(d.Guid);
                    if (vanilla != null)
                    {
                        f.m_LockedBadge = vanilla.m_LockedBadge;
                        f.m_ActionsRadial = vanilla.m_ActionsRadial;
                    }
                    else f.m_LockedBadge = new AssetReferenceTexture2D(d.Guid);
                    // the game's GC does not see references held by our injected class: pin everything only it points to
                    Pin(f.m_LocalizedDisplayName); Pin(f.m_LocalizedDescription); Pin(f.m_LocalizedShortDescription);
                    Pin(f.m_LocalizedBonusDescription); Pin(f.m_UnlockedBadge); Pin(f.m_LockedBadge); Pin(f.m_ActionsRadial);
                    Pin(f); Pin(go);
                    _feats[d.Id] = f;
                    list.Add(f);
                }
                FeatsMod.Log($"подвиги в списке игры / feats in game list: {list.Count}");
            }
            catch (Exception e) { FeatsMod.Error("Ensure feats: " + e); }
        }

        static readonly List<IntPtr> _pins = new();

        static void Pin(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase o)
        {
            try { if (o != null && o.Pointer != IntPtr.Zero) _pins.Add(IL2CPP.il2cpp_gchandle_new(o.Pointer, false)); }
            catch (Exception e) { FeatsMod.Warn("pin: " + e.Message); }
        }

        static LocalizedString Loc(string key)
        {
            var s = new LocalizedString();
            s.m_LocalizationID = key;
            return s;
        }

        public static void ShowUnlocked(Def d)
        {
            try { if (_feats.TryGetValue(d.Id, out var f) && f != null) { f.m_Unlocked = true; f.m_KickerPending = true; } }
            catch (Exception e) { FeatsMod.Error("kicker: " + e.Message); }
        }

        /// <summary>Is this feat unlocked and chosen for the current game. Cached, refreshed every second.</summary>
        public static bool On(string id) => _enabled.Contains(id);

        static void SyncUnlocked()
        {
            foreach (var d in Defs.All)
            {
                if (!_feats.TryGetValue(d.Id, out var f) || f == null || f.WasCollected) continue;
                bool mine = Progress.IsUnlocked(d);
                if (f.m_Unlocked && !mine) Progress.Set(d, 1f);       // unlocked from outside (trainer)
                else if (mine && !f.m_Unlocked) f.m_Unlocked = true;
            }
        }

        public static void Tick(float now, bool inGame)
        {
            if (now < _nextRefresh) return;
            _nextRefresh = now + 1f;
            try { SyncUnlocked(); } catch (Exception e) { FeatsMod.Error("sync: " + e.Message); }
            _enabled.Clear();
            if (!inGame) return;
            try
            {
                var list = FeatEnabledTracker.m_FeatsEnabledThisSandbox;
                if (list == null) return;
                for (int i = 0; i < list.Count; i++)
                {
                    var d = Defs.ByType((int)list[i]);
                    if (d != null && (Progress.IsUnlocked(d) || FeatsMod.IgnoreUnlock.Value)) _enabled.Add(d.Id);
                }
            }
            catch (Exception e) { FeatsMod.Error("enabled list: " + e.Message); _nextRefresh = now + 30f; }
        }

        public static void Invalidate() => _nextRefresh = 0f;

        /// <summary>FeatsManager.GetFeat knows only the game's 13 types and returns null for ours; the pause menu
        /// (Panel_PauseMenu.UpdateSelectedFeats) then hits a null and stops drawing the chosen feats' icons.</summary>
        public static void GetFeatPost(FeatType featType, ref Feat __result)
        {
            try
            {
                if (__result != null || (int)featType < 1000) return;
                var d = Defs.ByType((int)featType);
                if (d != null && _feats.TryGetValue(d.Id, out var f) && f != null && !f.WasCollected) __result = f;
            }
            catch (Exception e) { FeatsMod.Error("GetFeat: " + e.Message); }
        }
    }
}

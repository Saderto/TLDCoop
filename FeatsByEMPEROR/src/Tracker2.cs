using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Il2Cpp;
using UnityEngine;

namespace FeatsByEMPEROR
{
    /// <summary>Per-frame and timed counters of the second batch: distances, hours, streaks, regions, sunrises, predators near a sleeper.</summary>
    internal static class Tracker2
    {
        static Vector3 _last;
        static bool _haveLast;
        static float _torchM, _iceM, _trainM;
        static double _lastH = -1;
        static float _nextSlow, _nextFires, _nextTrainFind, _nextWake;
        static readonly List<Fire> _fires = new();
        static Il2CppTLD.SteamTrain.SteamTrain _train;
        static string _scene = "";
        static string _lastTag = "";
        static int _lastHour = -1;

        // the 17 big regions (+ two that also count): scene names
        static readonly HashSet<string> Regions = new(StringComparer.OrdinalIgnoreCase)
        {
            "LakeRegion", "CoastalRegion", "RuralRegion", "WhalingStationRegion", "MarshRegion", "CrashMountainRegion",
            "MountainTownRegion", "TracksRegion", "RiverValleyRegion", "CanneryRegion", "AshCanyonRegion", "BlackrockRegion",
            "AirfieldRegion", "HubRegion", "MiningRegion", "LongRailTransitionZone", "MountainPassRegion",
            "HighwayTransitionZone", "DesperationBayRegion",
        };

        public static void Reset()
        {
            _haveLast = false; _lastH = -1; _fires.Clear(); _train = null; _nextTrainFind = 0f; _nextFires = 0f; _lastHour = -1;
            Effects2.SceneReset();
        }

        public static void Update()
        {
            try
            {
                var pt = GameManager.GetPlayerTransform();
                var pm = GameManager.GetPlayerManagerComponent();
                if (pt == null || pm == null) { _haveLast = false; return; }
                Vector3 pos = pt.position;
                var mode = pm.GetControlMode();
                float step = _haveLast ? Vector3.Distance(pos, _last) : 0f;
                bool onFoot = _haveLast && mode == PlayerControlMode.Normal && step > 0.001f && step < 3f;
                _last = pos; _haveLast = true;

                if (onFoot)
                {
                    // torch in hands and burning
                    try
                    {
                        var held = pm.m_ItemInHands;
                        var torch = held != null ? held.m_TorchItem : null;
                        if (torch != null && torch.IsBurning())
                        {
                            _torchM += step;
                            if (_torchM >= 25f) { Progress.Add(Defs2.Torch, _torchM / 1000f); _torchM = 0f; }
                        }
                    }
                    catch { }
                    // walking on ice (footstep surface)
                    try
                    {
                        var fs = GameManager.GetFootStepSoundsComponent();
                        string tag = fs != null ? fs.GetMaterialTagForLastFootstep() ?? "" : "";
                        if (tag != _lastTag) { _lastTag = tag; FeatsMod.Log("поверхность / surface: " + tag); }
                        if (tag.IndexOf("ice", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _iceM += step;
                            if (_iceM >= 25f) { Progress.Add(Defs2.IceWalker, _iceM / 1000f); _iceM = 0f; }
                        }
                    }
                    catch { }
                }

                float now = Time.realtimeSinceStartup;
                Train(now, pos);
                if (now < _nextSlow) return;
                _nextSlow = now + 0.5f;
                Slow(now, pos, pm);
            }
            catch (Exception e) { FeatsMod.Error("tracker2: " + e.Message); }
        }

        static void Train(float now, Vector3 pos)
        {
            try
            {
                if (now >= _nextTrainFind && (_train == null || _train.WasCollected || !_train))
                {
                    _nextTrainFind = now + 5f;
                    _train = UnityEngine.Object.FindObjectOfType<Il2CppTLD.SteamTrain.SteamTrain>();
                }
                if (_train == null || _train.WasCollected || !_train) return;
                float v = Math.Abs(_train.Velocity);
                if (v < 0.3f || v > 60f) return;
                if ((_train.transform.position - pos).sqrMagnitude > 35f * 35f) return;
                _trainM += v * Time.deltaTime;
                if (_trainM >= 50f) { Progress.Add(Defs2.Train, _trainM / 1000f); _trainM = 0f; }
            }
            catch { _train = null; }
        }

        static void Slow(float now, Vector3 pos, PlayerManager pm)
        {
            double h = Effects2.Hours();
            if (h < 0) return;
            double dh = _lastH < 0 ? 0 : h - _lastH;
            if (dh < 0 || dh > 30) dh = 0;
            _lastH = h;
            var rest = GameManager.GetRestComponent();
            bool sleeping = rest != null && rest.IsSleeping();
            var w = Effects2.W();

            // scene / region
            string sc = Effects2.Scene();
            if (!string.Equals(sc, _scene, StringComparison.Ordinal))
            {
                _scene = sc;
                if (Regions.Contains(sc))
                {
                    int n = Progress.AddToSet("regions", sc);
                    if (n > 0) Progress.Max(Defs2.Traveler, n);
                    if (w != null)
                    {
                        if (w.LastRegion.Length > 0 && !string.Equals(w.LastRegion, sc, StringComparison.OrdinalIgnoreCase) && Effects2.On(Defs2.Traveler))
                        {
                            Effects2.NoFatigueUntil = Math.Max(Effects2.NoFatigueUntil, h + 1.0);
                            Effects2.Hud("Путешественник: час бодрости", "Traveler: an hour of vigor");
                        }
                        w.LastRegion = sc; Progress.MarkDirty();
                    }
                }
            }

            // sunrise (06:00)
            int hour = (int)Math.Floor(Effects2.HourOfDay());
            if (_lastHour >= 0 && _lastHour < 6 && hour >= 6 && hour < 12)
            {
                Progress.Add(Defs2.Fortune, 1f);
                if (!sleeping) Effects2.RollFortune();
            }
            _lastHour = hour;

            if (dh > 0)
            {
                // own fires burning near us
                if (now >= _nextFires)
                {
                    _nextFires = now + 5f;
                    _fires.Clear();
                    try { foreach (var f in UnityEngine.Object.FindObjectsOfType<Fire>()) if (f != null) _fires.Add(f); } catch { }
                }
                int own = 0;
                foreach (var f in _fires)
                {
                    try
                    {
                        if (f == null || f.WasCollected || !f) continue;
                        if (f.m_StartedByPlayer && f.IsBurning() && (f.transform.position - pos).sqrMagnitude < 30f * 30f) own++;
                    }
                    catch { }
                }
                if (own > 0) Progress.Add(Defs2.Hearth, (float)(dh * own));

                // clear sky
                if (!sleeping && Effects2.OutdoorScene() && !Effects2.IndoorEnv() && Effects2.SunnyNow()) Progress.Add(Defs2.Sun, (float)dh);
            }

            // co-op friend near
            bool near = FriendNear(pos);
            Effects2.NearFriend = near;
            if (near && dh > 0) Progress.Add(Defs2.Together, (float)dh);

            if (w != null && w.FortuneKind < 0 && !sleeping) Effects2.RollFortune();

            // streaks
            if (w != null && w.StartH >= 0)
            {
                if (!Effects2.OutdoorScene()) { w.LastIndoorH = h; }
                float weight = CarriedKg();
                if (weight >= 0f)
                {
                    Effects2.LightLoad = weight <= 10f;
                    if (weight > 10f) w.LastHeavyH = h;
                }
                Progress.Max(Defs2.Forest, Days(h, w.LastIndoorH, w.StartH));
                Progress.Max(Defs2.Vegetarian, Days(h, w.LastMeatH, w.StartH));
                Progress.Max(Defs2.Prometheus, Days(h, w.LastMatchH, w.StartH));
                Progress.Max(Defs2.Minimalist, Days(h, w.LastHeavyH, w.StartH));
            }

            // light sleeper: a predator close by wakes us
            if (sleeping && Effects2.On(Defs2.LightSleep) && now >= _nextWake && PredatorNear(pos, 35f))
            {
                _nextWake = now + 60f;
                try
                {
                    rest.EndSleeping(true);
                    Effects2.Hud("Чуткий сон: рядом хищник!", "Light Sleeper: a predator is near!");
                }
                catch (Exception e) { FeatsMod.Error("wake: " + e.Message); }
            }
        }

        static float Days(double h, double last, double start)
        {
            double from = Math.Max(last, start);
            return from < 0 ? 0f : (float)Math.Max(0.0, (h - from) / 24.0);
        }

        static float _weight = -1f;
        static float _weightAt = -100f;

        /// <summary>Carried weight without worn clothes, kg; -1 if unknown. Refreshed every 3 s.</summary>
        static float CarriedKg()
        {
            float now = Time.realtimeSinceStartup;
            if (now - _weightAt < 3f) return _weight;
            _weightAt = now;
            try
            {
                var enc = GameManager.GetEncumberComponent();
                var inv = GameManager.GetInventoryComponent();
                if (enc == null || inv == null) return _weight = -1f;
                float total = enc.GetGearWeightKG().ToQuantity(1f);
                var items = inv.m_Items;
                if (items != null)
                    for (int i = 0; i < items.Count; i++)
                    {
                        var gi = items[i]?.m_GearItem;
                        if (gi == null || gi.m_ClothingItem == null || !gi.m_ClothingItem.IsWearing()) continue;
                        total -= gi.GetItemWeightKG(false).ToQuantity(1f);
                    }
                return _weight = Math.Max(0f, total);
            }
            catch { return _weight = -1f; }
        }

        static bool PredatorNear(Vector3 pos, float r)
        {
            try
            {
                var list = BaseAiManager.m_BaseAis;
                if (list == null) return false;
                for (int i = 0; i < list.Count; i++)
                {
                    var ai = list[i];
                    if (ai == null) continue;
                    var t = ai.m_AiSubType;
                    if (t != AiSubType.Wolf && t != AiSubType.Bear && t != AiSubType.Cougar) continue;
                    if (ai.m_CurrentMode == AiMode.Dead) continue;
                    if ((ai.transform.position - pos).sqrMagnitude < r * r) return true;
                }
            }
            catch { }
            return false;
        }

        public static void OnWake(float hourOfDay)
        {
            if (hourOfDay >= 3f && hourOfDay < 12f) Effects2.RollFortune();
        }

        // ---- CoopByEMPEROR players (by reflection; no hard link) ----
        static bool _coopChecked;
        static FieldInfo _players;
        static PropertyInfo _localId;
        static MethodInfo _sceneFn;
        static FieldInfo _fId, _fX, _fY, _fZ, _fScene, _fLast;

        static bool FriendNear(Vector3 pos)
        {
            try
            {
                if (!_coopChecked)
                {
                    _coopChecked = true;
                    Type s = null, api = null;
                    foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        s ??= a.GetType("CoopByEMPEROR.Net.Session");
                        api ??= a.GetType("CoopByEMPEROR.Game.GameApi");
                    }
                    if (s != null)
                    {
                        _players = s.GetField("Players", BindingFlags.Public | BindingFlags.Static);
                        _localId = s.GetProperty("LocalId", BindingFlags.Public | BindingFlags.Static);
                    }
                    if (api != null) _sceneFn = api.GetMethod("Scene", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                    var pt = s?.Assembly.GetType("CoopByEMPEROR.Net.Player");
                    if (pt != null)
                    {
                        _fId = pt.GetField("Id"); _fX = pt.GetField("X"); _fY = pt.GetField("Y"); _fZ = pt.GetField("Z");
                        _fScene = pt.GetField("Scene"); _fLast = pt.GetField("LastState");
                    }
                    if (_players != null && _fX != null) FeatsMod.Log("CoopByEMPEROR найден: подвиг «Вдвоём легче» / co-op found for Better Together");
                }
                if (_players == null || _fX == null || _localId == null) return false;
                if (_players.GetValue(null) is not IDictionary dict || dict.Count < 1) return false;
                int me = (int)_localId.GetValue(null);
                string myScene = _sceneFn != null ? _sceneFn.Invoke(null, null) as string : Effects2.Scene();
                float t = Time.unscaledTime;
                foreach (var p in dict.Values)
                {
                    if (p == null || (int)_fId.GetValue(p) == me) continue;
                    if (t - (float)_fLast.GetValue(p) > 10f) continue;
                    if (!string.Equals(_fScene.GetValue(p) as string, myScene, StringComparison.OrdinalIgnoreCase)) continue;
                    var v = new Vector3((float)_fX.GetValue(p), (float)_fY.GetValue(p), (float)_fZ.GetValue(p));
                    if ((v - pos).sqrMagnitude <= 20f * 20f) return true;
                }
            }
            catch { }
            return false;
        }
    }
}

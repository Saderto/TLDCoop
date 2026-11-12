using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Долгие действия (разделка, готовка, крафт, починка, чтение). В одиночной игре на время действия игра
    // ускоряет часы (TimeOfDay.Accelerate). В коопе время общее: у себя ускорение оставляем — круг прогресса
    // идёт как обычно, сытость, жажда, усталость и тепло расходуются за полное время действия, — но
    // остальным время не проматывается: хост рассылает общее время так, будто ничего не ускорялось, а когда
    // действие кончилось, часы игрока возвращаются к общему времени.
    // Проматывать общее время для всех может только сон и ожидание (Game/SleepSync.cs).
    // Ускоряются только потребности самого игрока. Мир идёт по общим часам: пока идёт действие, у себя
    // не прогорают костры (патч Fire.Update), не портятся вещи — ни вокруг, ни в рюкзаке
    // (патч GearItem.DecayOverTODHours), не сохнут шкуры (патч EvolveItem.Update), не готовит котелок (CookingPotItem.Update), не ловят силки и не вялит ящик, а погода в конце возвращается к той, что была (WeatherSync.Freeze).
    // Само действие длится секунды, у остальных за это время проходит минута-другая, разница незаметна.
    public static class ActionTime
    {
        private static bool _active, _rest, _restSeen;
        private static float _startTod, _realStart, _realDur, _dayLen, _scale0, _hours;
        private static int _startDay;

        // Костры на начало действия или сна отдельно. Fire.Update во время действия мы откатываем (патч ниже), но
        // у хоста за сон отдельно на 9 ч костёр всё равно прогорел с ~644 до ~10 мин (друзья, 0.19.44): от чего —
        // не доказано. Поэтому в конце ещё и сверяем с тем, что было в начале, и возвращаем таймер.
        private struct FireSnap { public Fire F; public float E, U, Em, H; }
        private static readonly System.Collections.Generic.List<FireSnap> FireSnaps = new();

        // Свет на начало: керосин, минуты горения, заряд. В конце — то же сверка, что у костров (патчи Update ниже
        // должны держать и так; если за сон всё-таки сгорело, вернём и напишем в лог).
        private struct LightSnap { public GearItem Gi; public int Kind; public float V; public long Fuel; }
        private static readonly System.Collections.Generic.List<LightSnap> LightSnaps = new();

        private static void SnapLights()
        {
            LightSnaps.Clear();
            try
            {
                foreach (var l in UnityEngine.Object.FindObjectsOfType<Il2CppTLD.Gear.KeroseneLampItem>())
                    try { if (l != null && l.IsOn()) LightSnaps.Add(new LightSnap { Gi = l.GetComponent<GearItem>(), Kind = 0, Fuel = l.m_CurrentFuelLiters.m_Units }); } catch { }
                foreach (var t in UnityEngine.Object.FindObjectsOfType<TorchItem>())
                    try { if (t != null && t.IsBurning()) LightSnaps.Add(new LightSnap { Gi = t.GetComponent<GearItem>(), Kind = 1, V = t.m_ElapsedBurnMinutes }); } catch { }
                foreach (var f in UnityEngine.Object.FindObjectsOfType<FlareItem>())
                    try { if (f != null && f.IsBurning()) LightSnaps.Add(new LightSnap { Gi = f.GetComponent<GearItem>(), Kind = 2, V = f.m_ElapsedBurnMinutes }); } catch { }
                foreach (var f in UnityEngine.Object.FindObjectsOfType<FlashlightItem>())
                    try { if (f != null && f.IsOn()) LightSnaps.Add(new LightSnap { Gi = f.GetComponent<GearItem>(), Kind = 3, V = f.m_CurrentBatteryCharge }); } catch { }
            }
            catch { }
        }

        private static void RestoreLights(string what)
        {
            float sharedMin = 0f;
            try { sharedMin = (Time.unscaledTime - _realStart) / Math.Max(1f, _dayLen) * 1440f; } catch { }
            foreach (var s in LightSnaps)
            {
                try
                {
                    var gi = s.Gi;
                    if (gi == null || gi.gameObject == null) continue;
                    string name = ItemSync.PrefabName(gi), fixedText = null;
                    switch (s.Kind)
                    {
                        case 0:
                        {
                            var l = gi.GetComponent<Il2CppTLD.Gear.KeroseneLampItem>();
                            if (l == null || !l.IsOn()) { if (l != null) fixedText = "погас — заново не зажигаю"; break; }
                            long lost = s.Fuel - l.m_CurrentFuelLiters.m_Units;
                            // Керосина за пару минут уходит доли миллилитра: всё, что больше 0.01 л, — лишнее.
                            if (lost > Il2CppTLD.IntBackedUnit.ItemLiquidVolume.Liter.m_Units / 100) { l.m_CurrentFuelLiters = new Il2CppTLD.IntBackedUnit.ItemLiquidVolume(s.Fuel); fixedText = $"сгорело {lost / (float)Il2CppTLD.IntBackedUnit.ItemLiquidVolume.Liter.m_Units:0.00} л керосина — вернул"; }
                            break;
                        }
                        case 1:
                        {
                            var t = gi.GetComponent<TorchItem>();
                            if (t == null || !t.IsBurning()) { if (t != null) fixedText = "догорел — заново не зажигаю"; break; }
                            float lost = t.m_ElapsedBurnMinutes - (s.V + sharedMin);
                            if (lost >= 1f) { t.m_ElapsedBurnMinutes = s.V + sharedMin; fixedText = $"прогорел на {lost:0} мин лишних — вернул"; }
                            break;
                        }
                        case 2:
                        {
                            var f = gi.GetComponent<FlareItem>();
                            if (f == null || !f.IsBurning()) { if (f != null) fixedText = "догорел — заново не зажигаю"; break; }
                            float lost = f.m_ElapsedBurnMinutes - (s.V + sharedMin);
                            if (lost >= 1f) { f.m_ElapsedBurnMinutes = s.V + sharedMin; fixedText = $"прогорел на {lost:0} мин лишних — вернул"; }
                            break;
                        }
                        case 3:
                        {
                            var f = gi.GetComponent<FlashlightItem>();
                            if (f == null || !f.IsOn()) break;
                            if (s.V - f.m_CurrentBatteryCharge > 0.02f) { fixedText = $"заряд {s.V:0.00} → {f.m_CurrentBatteryCharge:0.00} — вернул"; f.m_CurrentBatteryCharge = s.V; }
                            break;
                        }
                    }
                    if (fixedText != null) MelonLogger.Msg($"[время] {name}: за {what} {fixedText}");
                }
                catch { }
            }
            LightSnaps.Clear();
        }

        private static void SnapFires()
        {
            FireSnaps.Clear();
            try
            {
                foreach (var f in UnityEngine.Object.FindObjectsOfType<Fire>())
                {
                    if (f == null) continue;
                    try { if (f.IsBurning()) FireSnaps.Add(new FireSnap { F = f, E = f.m_ElapsedOnTODSeconds, U = f.m_ElapsedOnTODSecondsUnmodified, Em = f.m_EmberTimer, H = f.m_BurningTimeTODHours }); }
                    catch { }
                }
            }
            catch { }
        }

        private static void RestoreFires(string what)
        {
            // Сколько игровых секунд прошло у остальных за это время: столько костёр и должен был прогореть.
            float shared = 0f;
            try { shared = (Time.unscaledTime - _realStart) / Math.Max(1f, _dayLen) * 86400f; } catch { }
            foreach (var s in FireSnaps)
            {
                try
                {
                    var f = s.F;
                    if (f == null || f.gameObject == null) continue;
                    float lost = f.m_ElapsedOnTODSeconds - (s.E + shared);
                    if (lost < 60f) continue;   // прогорел не больше, чем у остальных
                    string id = WorldObjects.IdOf(f);
                    if (!f.IsBurning())
                    {
                        MelonLogger.Msg($"[время] костёр {id}: за {what} прогорел на {lost / 60f:0} мин лишних и погас — заново не разжигаю");
                        continue;
                    }
                    f.m_ElapsedOnTODSeconds = s.E + shared;
                    f.m_ElapsedOnTODSecondsUnmodified = s.U + shared;
                    f.m_EmberTimer = s.Em;
                    f.m_BurningTimeTODHours = s.H + shared / 3600f;
                    MelonLogger.Msg($"[время] костёр {id}: за {what} прогорел на {lost / 60f:0} мин лишних — вернул (осталось {(f.m_MaxOnTODSeconds - f.m_ElapsedOnTODSeconds) / 60f:0} мин)");
                }
                catch { }
            }
            FireSnaps.Clear();
        }

        // Вне коопа не действует никогда: иначе после конца сессии посреди действия костры и порча стояли бы и в своей игре.
        public static bool Active => _active && Session.Mode != Mode.Off;

        // Конец сессии (Session.Stop).
        public static void Reset()
        {
            _active = _rest = _restSeen = false;
        }

        // Только когда в игре есть кто-то ещё: одному время можно проматывать как обычно.
        private static bool Coop
        {
            get
            {
                if (Session.Mode == Mode.Off || !Session.InSharedWorld || !Config.SyncTime) return false;
                float now = Time.unscaledTime;
                foreach (var p in Session.Players.Values)
                    if (p.Id != Session.LocalId && p.InWorld && p.Scene.Length > 0 && now - p.LastState < 10f) return true;
                return false;
            }
        }

        // Общее время: во время действия — время начала плюс столько, сколько прошло бы без ускорения.
        public static float SharedTimeOfDay()
        {
            float t = SharedTotal();
            return t - Mathf.Floor(t);
        }

        // То же с номером дня (день + доля суток).
        public static float SharedTotal()
        {
            if (!_active) return GameApi.TotalTime();
            return _startDay + _startTod + (Time.unscaledTime - _realStart) / Math.Max(1f, _dayLen);
        }

        public static int SharedDay() => Mathf.FloorToInt(SharedTotal());

        // Сон или ожидание отдельно от остальных (Game/SleepSync.cs): то же самое, только до пробуждения.
        public static bool BeginSoloRest(float hours)
        {
            if (_active || !Coop) return false;
            try
            {
                var tod = GameManager.GetTimeOfDayComponent();
                _startTod = tod.GetNormalizedTime();
                _startDay = tod.GetDayNumber();
                _dayLen = tod.GetDayLengthSeconds();
                _scale0 = tod.GetDayLengthScale();
                _realStart = Time.unscaledTime;
                _realDur = 0f;
                _hours = hours;
                _rest = true;
                _restSeen = false;
                _active = true;
                SnapFires();
                SnapLights();
                WeatherSync.Freeze();
                MelonLogger.Msg($"[время] сплю отдельно (~{hours} ч): у остальных время не проматывается");
                return true;
            }
            catch { return false; }
        }

        public static void OnAccelerate(TimeOfDay tod, float realSeconds, float hours)
        {
            if (_active || !Coop || hours < 0.05f || SleepSync.Starting || SleepSync.LocalResting) return;
            try
            {
                _startTod = tod.GetNormalizedTime();
                _startDay = tod.GetDayNumber();
                _dayLen = tod.GetDayLengthSeconds();
                _scale0 = tod.GetDayLengthScale();
                _realStart = Time.unscaledTime;
                _realDur = realSeconds;
                _hours = hours;
                _active = true;
                SnapFires();
                SnapLights();
                WeatherSync.Freeze();
            }
            catch { }
        }

        public static void Tick()
        {
            if (!_active) return;
            if (!GameApi.InGame() || Session.Mode == Mode.Off) { _active = false; _rest = false; WeatherSync.Unfreeze(false); return; }
            if (_rest)
            {
                // Ждём, пока заснёт (затемнение), потом — пока проснётся.
                if (SleepSync.LocalResting) { _restSeen = true; return; }
                if (!_restSeen && Time.unscaledTime - _realStart < 6f) return;
                _rest = false;
                if (!_restSeen)
                {
                    // Сон так и не начался (игра не дала лечь): время не трогаем.
                    _active = false;
                    WeatherSync.Unfreeze(false);
                    MelonLogger.Msg("[время] сон отдельно не начался");
                    return;
                }
                Finish("сон отдельно");
                return;
            }
            // Уснул или стал ждать — это общее время, его не возвращаем.
            if (SleepSync.LocalResting) { _active = false; WeatherSync.Unfreeze(false); return; }
            float elapsed = Time.unscaledTime - _realStart;
            bool done;
            try { done = elapsed > 0.5f && Mathf.Abs(GameManager.GetTimeOfDayComponent().GetDayLengthScale() - _scale0) < 0.001f; }
            catch { done = true; }
            if (!done && elapsed < _realDur + 10f) return;
            Finish($"долгое действие (~{_hours:0.#} ч)");
        }

        private static void Finish(string what)
        {
            float shared = SharedTotal();
            _active = false;
            try
            {
                int before = GameApi.DayNumber();
                GameApi.SetDayAndTime(Mathf.FloorToInt(shared), shared);
                // Таймеры FoodPackByEMPEROR идут от «часов в игре» (GetHoursPlayedNotPaused), их SetDayAndTime не откатывает:
                // эффекты еды стареют за долгое действие так же, как потребности и обычные болезни. Сдвигать их не нужно.
                if (before != GameApi.DayNumber()) MelonLogger.Msg($"[время] день возвращён: {before} → {GameApi.DayNumber()}");
                RestoreFires(what);
                RestoreLights(what);
                WeatherSync.Unfreeze(true);
                MelonLogger.Msg($"[время] {what}: потребности начислены, общее время не промотано, костры, погода и вещи вокруг не ускорились");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[время] " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(TimeOfDay), nameof(TimeOfDay.Accelerate))]
    internal static class Patch_TimeAccelerate
    {
        private static void Prefix(TimeOfDay __instance, float realTimeSeconds, float gameTimeHours) =>
            ActionTime.OnAccelerate(__instance, realTimeSeconds, gameTimeHours);
    }

    [HarmonyPatch(typeof(Fire), nameof(Fire.Update))]
    internal static class Patch_FireUpdateDuringAction
    {
        private static void Prefix(Fire __instance, out (float e, float u, float em, float h) __state)
        {
            __state = ActionTime.Active ? (__instance.m_ElapsedOnTODSeconds, __instance.m_ElapsedOnTODSecondsUnmodified, __instance.m_EmberTimer, __instance.m_BurningTimeTODHours) : default;
        }

        private static void Postfix(Fire __instance, (float e, float u, float em, float h) __state)
        {
            if (!ActionTime.Active) return;
            __instance.m_ElapsedOnTODSeconds = __state.e;
            __instance.m_ElapsedOnTODSecondsUnmodified = __state.u;
            __instance.m_EmberTimer = __state.em;
            __instance.m_BurningTimeTODHours = __state.h;
        }
    }

    // Сушка шкур, кишок и прочего (EvolveItem) во время действия тоже стоит: иначе у делающего шкура уже высохла,
    // а у остальных нет.
    [HarmonyPatch(typeof(EvolveItem), nameof(EvolveItem.Update))]
    internal static class Patch_EvolveDuringAction
    {
        private static void Prefix(EvolveItem __instance, out float __state) => __state = ActionTime.Active ? __instance.m_TimeSpentEvolvingGameHours : -1f;

        private static void Postfix(EvolveItem __instance, float __state)
        {
            if (ActionTime.Active && __state >= 0f) __instance.m_TimeSpentEvolvingGameHours = __state;
        }
    }

    // Котелок тоже стоит: иначе за ожидание отдельно (или долгое действие) еда и вода доготавливались бы,
    // а костёр под ними не прогорал (он стоит выше), и у остальных котелок стал бы готовым — бесплатная готовка.
    [HarmonyPatch(typeof(CookingPotItem), nameof(CookingPotItem.Update))]
    internal static class Patch_CookingDuringAction
    {
        private static bool Prefix() => !ActionTime.Active;
    }

    // Силки не «ловят» за ускоренное время: иначе кролик попадался бы раньше, чем у остальных прошло время.
    [HarmonyPatch(typeof(SnareItem), nameof(SnareItem.Update))]
    internal static class Patch_SnareDuringAction
    {
        private static bool Prefix() => !ActionTime.Active;
    }

    // Ящик для вяления (Tales) тоже стоит.
    [HarmonyPatch(typeof(CuringStorage), nameof(CuringStorage.Update))]
    internal static class Patch_CuringDuringAction
    {
        private static bool Prefix() => !ActionTime.Active;
    }

    // Свет тоже не горит за чужое время: иначе у спавшего отдельно (или долго готовившего) фонарь, факел или фальшфейер
    // на земле догорал на часы раньше, чем у остальных. Как у костра: что игра насчитала за кадр, откатываем.
    // Штормовой фонарь — керосин; факел и фальшфейер — минуты горения; фонарик и фонарь-динамо (DLC) — заряд.
    [HarmonyPatch(typeof(Il2CppTLD.Gear.KeroseneLampItem), nameof(Il2CppTLD.Gear.KeroseneLampItem.Update))]
    internal static class Patch_LampDuringAction
    {
        private static void Prefix(Il2CppTLD.Gear.KeroseneLampItem __instance, out long __state) =>
            __state = ActionTime.Active ? __instance.m_CurrentFuelLiters.m_Units : long.MinValue;

        private static void Postfix(Il2CppTLD.Gear.KeroseneLampItem __instance, long __state)
        {
            if (!ActionTime.Active || __state == long.MinValue) return;
            if (__instance.m_CurrentFuelLiters.m_Units < __state) __instance.m_CurrentFuelLiters = new Il2CppTLD.IntBackedUnit.ItemLiquidVolume(__state);
        }
    }

    [HarmonyPatch(typeof(TorchItem), nameof(TorchItem.Update))]
    internal static class Patch_TorchDuringAction
    {
        private static void Prefix(TorchItem __instance, out float __state) => __state = ActionTime.Active ? __instance.m_ElapsedBurnMinutes : -1f;

        private static void Postfix(TorchItem __instance, float __state)
        {
            if (ActionTime.Active && __state >= 0f && __instance.m_ElapsedBurnMinutes > __state) __instance.m_ElapsedBurnMinutes = __state;
        }
    }

    [HarmonyPatch(typeof(FlareItem), nameof(FlareItem.Update))]
    internal static class Patch_FlareDuringAction
    {
        private static void Prefix(FlareItem __instance, out float __state) => __state = ActionTime.Active ? __instance.m_ElapsedBurnMinutes : -1f;

        private static void Postfix(FlareItem __instance, float __state)
        {
            if (ActionTime.Active && __state >= 0f && __instance.m_ElapsedBurnMinutes > __state) __instance.m_ElapsedBurnMinutes = __state;
        }
    }

    [HarmonyPatch(typeof(FlashlightItem), nameof(FlashlightItem.Update))]
    internal static class Patch_FlashlightDuringAction
    {
        private static void Prefix(FlashlightItem __instance, out float __state) => __state = ActionTime.Active ? __instance.m_CurrentBatteryCharge : -1f;

        private static void Postfix(FlashlightItem __instance, float __state)
        {
            if (ActionTime.Active && __state >= 0f && __instance.m_CurrentBatteryCharge < __state) __instance.m_CurrentBatteryCharge = __state;
        }
    }

    [HarmonyPatch(typeof(GearItem), nameof(GearItem.DecayOverTODHours))]
    internal static class Patch_GearDecayDuringAction
    {
        // Во время действия не портится ничего: ни лежащее вокруг, ни своё в рюкзаке и на себе.
        private static bool Prefix() => !ActionTime.Active;
    }
}

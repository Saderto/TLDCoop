using System;
using System.Reflection;
using MelonLoader;
using CoopByEMPEROR.Net;

namespace CoopByEMPEROR.Game
{
    // Связь с модом-регулятором еды FoodBalanceByEMPEROR (и с FoodPackByEMPEROR) без жёсткой ссылки на их dll.
    //
    // 1) Регулятор решает, сколько еды остаётся в ящиках и на картах при первом посещении. Решение у всех
    //    считается одинаково от зерна мира и места, но только при одинаковых ползунках, поэтому в общем мире
    //    у друзей действуют ползунки хоста: хост кладёт их в правила сервера (Rules.FoodBalance, каждые 2 с),
    //    друг подставляет их в FoodBalance.SettingsOverride (свои настройки при этом не меняются).
    // 2) Оба мода кладут/убирают вещи первого посещения через несколько десятков кадров после загрузки.
    //    Пока они не закончили, ItemSync не считает локацию устоявшейся (WorldSpawnsPending).
    internal static class FoodBalanceLink
    {
        private static bool _tried;
        private static Func<string> _export;
        private static FieldInfo _override;
        private static Func<bool> _balancePending, _foodPending;
        private static string _applied;   // что сейчас стоит в SettingsOverride (null — свои)

        private static void Init()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                Type bal = null, food = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    bal ??= asm.GetType("FoodBalanceByEMPEROR.FoodBalance", false);
                    food ??= asm.GetType("FoodPackByEMPEROR.CoopApi", false);
                }
                const BindingFlags PS = BindingFlags.Public | BindingFlags.Static;
                if (bal != null)
                {
                    var ex = bal.GetMethod("ExportSettings", PS, null, Type.EmptyTypes, null);
                    if (ex != null && ex.ReturnType == typeof(string)) _export = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), ex);
                    var ov = bal.GetField("SettingsOverride", PS);
                    if (ov != null && ov.FieldType == typeof(string)) _override = ov;
                    _balancePending = PendingOf(bal);
                }
                if (food != null) _foodPending = PendingOf(food);
                if (bal != null)
                    MelonLogger.Msg($"[еда] FoodBalanceByEMPEROR найден: настройки хоста {(_export != null && _override != null ? "есть" : "нет")}, ожидание раскладки {(_balancePending != null ? "есть" : "нет")}");
            }
            catch (Exception e) { MelonLogger.Warning("[еда] связь с FoodBalanceByEMPEROR: " + e.Message); }
        }

        // public static bool WorldSpawnsPending { get; } или поле/метод с тем же именем.
        private static Func<bool> PendingOf(Type t)
        {
            const BindingFlags PS = BindingFlags.Public | BindingFlags.Static;
            var p = t.GetProperty("WorldSpawnsPending", PS);
            if (p != null && p.PropertyType == typeof(bool) && p.GetMethod != null)
                return (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), p.GetMethod);
            var m = t.GetMethod("WorldSpawnsPending", PS, null, Type.EmptyTypes, null);
            if (m != null && m.ReturnType == typeof(bool)) return (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), m);
            return null;
        }

        // Хост: свои ползунки для правил сервера ("" — регулятора нет).
        public static string Export()
        {
            Init();
            if (_export == null) return "";
            try { return _export() ?? ""; } catch { return ""; }
        }

        // Каждый кадр сети: в общем мире у друга (и пока грузится копия мира хоста) — ползунки хоста, иначе свои.
        public static void Tick()
        {
            Init();
            if (_override == null) return;
            string want = null;
            if (Session.Mode == Mode.Client && (Session.InSharedWorld || WorldTransfer.LoadingHostWorld))
            {
                string host = Session.CurrentRules.FoodBalance;
                if (!string.IsNullOrEmpty(host)) want = host;
            }
            if (want == _applied) return;
            try
            {
                _override.SetValue(null, want);
                _applied = want;
                MelonLogger.Msg(want == null ? "[еда] регулятор еды: свои настройки" : "[еда] регулятор еды: настройки хоста");
            }
            catch (Exception e) { MelonLogger.Warning("[еда] настройки регулятора: " + e.Message); _override = null; }
        }

        // Моды ещё кладут или убирают вещи первого посещения этой локации.
        public static bool WorldSpawnsPending()
        {
            Init();
            try { return (_balancePending?.Invoke() ?? false) || (_foodPending?.Invoke() ?? false); }
            catch { return false; }
        }
    }
}

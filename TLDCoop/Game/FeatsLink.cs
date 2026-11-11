using System;
using System.Reflection;
using MelonLoader;

namespace TLDCoop.Game
{
    // Связь с модом FeatsBySeagull (новые подвиги) без жёсткой ссылки на его dll.
    // Подвиг «Охотник на волков» прибавляет волку шанс убежать, когда он замечает игрока. Зверями сцены управляет один игрок,
    // поэтому каждый шлёт свою прибавку в AnimalSense, а пока зверь «думает» о другом игроке, мод берёт прибавку этого
    // игрока через делегат CoopApi.BorrowedWolfFleeBonus (как множитель обнаружения у FoodPackBySeagull).
    internal static class FeatsLink
    {
        private static bool _tried;
        private static Func<int> _local;

        private static void Init()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                Type api = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    api = asm.GetType("FeatsBySeagull.CoopApi", false);
                    if (api != null) break;
                }
                if (api == null) return;
                const BindingFlags PS = BindingFlags.Public | BindingFlags.Static;
                var m = api.GetMethod("LocalWolfFleeBonus", PS, null, Type.EmptyTypes, null);
                if (m != null && m.ReturnType == typeof(int)) _local = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), m);
                var f = api.GetField("BorrowedWolfFleeBonus", PS);
                if (f != null && f.FieldType == typeof(Func<int?>)) f.SetValue(null, (Func<int?>)Borrowed);
                MelonLogger.Msg($"[подвиги] FeatsBySeagull найден: прибавка к бегству волков {(_local != null ? "есть" : "нет")}, делегат {(f != null ? "поставлен" : "нет")}");
            }
            catch (Exception e) { MelonLogger.Warning("[подвиги] связь с FeatsBySeagull: " + e.Message); }
        }

        // Зверь думает о другом игроке — его прибавка (нет свежих данных — 0, но не наша собственная).
        private static int? Borrowed()
        {
            if (!AnimalSync.Borrowing || AnimalSync.BorrowId < 0) return null;
            return AnimalSync.Borrowed(out var s) ? s.WolfFlee : 0;
        }

        public static int LocalWolfFlee()
        {
            Init();
            if (_local == null) return 0;
            try { return Math.Clamp(_local(), 0, 100); } catch { return 0; }
        }
    }
}

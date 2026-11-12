using System;
using System.Reflection;
using MelonLoader;

namespace CoopByEMPEROR.Game
{
    // Связь с модом FoodPackByEMPEROR без жёсткой ссылки на его dll (мода может не быть).
    //
    // 1) Его эффекты меняют дальность, с которой звери замечают игрока. Зверями сцены управляет один игрок,
    //    поэтому каждый шлёт свой множитель в AnimalSense, а пока зверь «думает» о другом игроке,
    //    FoodPack берёт множитель этого игрока через делегат CoopApi.BorrowedDetectMul.
    // 2) Эффекты еды (таймеры, накопленная головная боль и т. п.) мод хранит в файле .moddata своего слота.
    //    У друга каждый вход в кооп — новый слот, поэтому состояние едет вместе с его прогрессом у хоста
    //    (Game/PlayerProgress.cs): ExportPlayerState при каждом его сохранении, PlayerStateOverride перед
    //    загрузкой копии мира, ImportPlayerState("") для нового выжившего и после смерти.
    internal static class FoodPackLink
    {
        private static bool _tried;
        private static Func<float> _local;
        private static Func<string> _export;
        private static Action<string> _import;
        private static FieldInfo _override;
        private static FieldInfo _overrideSlot;   // CoopApi.PlayerStateOverrideSlot: подстановку берёт только загрузка этого слота
        private static FieldInfo _saved;          // CoopApi.SavedPlayerState: снимок в момент сохранения игры (SaveGlobalData)
        private static string _last;   // последнее известное состояние своего игрока (из копии мира, импорта или экспорта)

        private static void Init()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                Type api = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    api = asm.GetType("FoodPackByEMPEROR.CoopApi", false);
                    if (api != null) break;
                }
                if (api == null) return;
                const BindingFlags PS = BindingFlags.Public | BindingFlags.Static;
                var m = api.GetMethod("LocalDetectMul", PS, null, Type.EmptyTypes, null);
                if (m != null && m.ReturnType == typeof(float)) _local = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), m);
                var f = api.GetField("BorrowedDetectMul", PS);
                if (f != null && f.FieldType == typeof(Func<float?>))
                    f.SetValue(null, (Func<float?>)BorrowedMul);
                var ex = api.GetMethod("ExportPlayerState", PS, null, Type.EmptyTypes, null);
                if (ex != null && ex.ReturnType == typeof(string)) _export = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), ex);
                var im = api.GetMethod("ImportPlayerState", PS, null, new[] { typeof(string) }, null);
                if (im != null && im.ReturnType == typeof(void)) _import = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), im);
                var ov = api.GetField("PlayerStateOverride", PS);
                if (ov != null && ov.FieldType == typeof(string)) _override = ov;
                var ovs = api.GetField("PlayerStateOverrideSlot", PS);
                if (ovs != null && ovs.FieldType == typeof(string)) _overrideSlot = ovs;
                var sv = api.GetField("SavedPlayerState", PS);
                if (sv != null && sv.FieldType == typeof(string)) _saved = sv;
                MelonLogger.Msg($"[еда] FoodPackByEMPEROR найден: множитель обнаружения {(_local != null ? "есть" : "нет")}, " +
                                $"делегат {(f != null ? "поставлен" : "нет")}, перенос эффектов {(_export != null && _import != null && _override != null ? "есть" : "нет")}");
            }
            catch (Exception e) { MelonLogger.Warning("[еда] связь с FoodPackByEMPEROR: " + e.Message); }
        }

        // Зверь думает о другом игроке — его множитель (нет свежих данных — без эффекта, но не наш собственный).
        private static float? BorrowedMul()
        {
            if (!AnimalSync.Borrowing || AnimalSync.BorrowId < 0) return null;
            return AnimalSync.Borrowed(out var s) && s.Detect > 0f ? s.Detect : 1f;
        }

        public static float LocalDetectMul()
        {
            Init();
            if (_local == null) return 1f;
            try { float v = _local(); return float.IsNaN(v) || v <= 0f ? 1f : v; } catch { return 1f; }
        }

        // ---------- Личное состояние игрока (эффекты еды) ----------

        // Состояние своего игрока одной строкой; null — мода нет, он не умеет или о состоянии ничего не известно.
        // Мод вернул null (менеджер эффектов ещё не создан) — отдаём последнее известное состояние, а не пустое:
        // иначе сохранение сразу после входа затёрло бы эффекты у хоста.
        public static string ExportPlayerState()
        {
            Init();
            if (_export == null) return null;
            try
            {
                // Снимок, сделанный модом в момент сохранения игры, — вместе с остальным прогрессом. Прогресс уходит хосту
                // через несколько секунд, часто уже под экраном загрузки или в меню, где живого состояния нет.
                string s = _saved?.GetValue(null) as string;
                if (s == null && GameApi.InGame()) s = _export();
                if (s != null) _last = s;
                return _last;
            }
            catch (Exception e) { MelonLogger.Warning("[еда] состояние эффектов: " + e.Message); return _last; }
        }

        // Друг, перед загрузкой копии мира: при загрузке мод возьмёт состояние отсюда, а не из .moddata
        // ("" — чистый игрок без эффектов). null — снять (загрузка не состоялась).
        public static void SetOverride(string state, string slot = null)
        {
            Init();
            if (_override == null) return;
            if (state != null) _last = state;
            try
            {
                _overrideSlot?.SetValue(null, state != null ? slot : null);
                _override.SetValue(null, state);
            }
            catch (Exception e) { MelonLogger.Warning("[еда] подстановка эффектов: " + e.Message); }
        }

        // Мир загрузился. Если мод почему-то не забрал подстановку при загрузке, применяем её сами,
        // чтобы она не досталась следующей загрузке (например, своему одиночному сохранению).
        public static void AfterLoad()
        {
            Init();
            if (_override == null) return;
            try
            {
                var left = _override.GetValue(null) as string;
                if (left == null) return;
                _override.SetValue(null, null);
                try { _overrideSlot?.SetValue(null, null); } catch { }
                MelonLogger.Warning("[еда] мод не забрал эффекты при загрузке — применяю после неё");
                ImportPlayerState(left);
            }
            catch (Exception e) { MelonLogger.Warning("[еда] эффекты после загрузки: " + e.Message); }
        }

        // Заменить личное состояние сейчас ("" — убрать все эффекты нашей еды).
        public static void ImportPlayerState(string state)
        {
            Init();
            if (_import == null) return;
            _last = state ?? "";
            try { _import(state ?? ""); }
            catch (Exception e) { MelonLogger.Warning("[еда] замена эффектов: " + e.Message); }
        }
    }
}

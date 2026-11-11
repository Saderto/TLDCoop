using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Il2Cpp;
using Il2CppTLD.Gear;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace TLDTrainer
{
    public sealed class ItemInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Group { get; set; }
    }

    public sealed class BlueprintInfo
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
        public bool Unlocked { get; set; }
    }

    // Список всех предметов игры. Собирается из описаний предметов (GearItemData_*): это маленькие файлы
    // с названием и типом, без 3D-моделей. Грузятся по нескольку за кадр, результат кэшируется в файл.
    internal static class Catalog
    {
        private const int InFlight = 6;

        private static string CachePath => Path.Combine(MelonEnvironment.UserDataDirectory, "TLDTrainer.items.json");

        private const string DataPrefix = "GearItemData_";
        private const int CacheVersion = 3;

        private static List<string> _pending;
        private static bool _logEach;
        private static int _cacheKey;
        private static int _total;
        private static List<ItemInfo> _building;
        private static volatile List<ItemInfo> _ready;
        private static volatile string _error;

        public static object Status()
        {
            if (_ready != null)
            {
                // Предметы FoodPack не кэшируем: мод часто обновляется, список читается из его файлов.
                var pack = new List<ItemInfo>(FoodPacks.Items());
                pack.AddRange(Smokes.Items());
                if (pack.Count == 0) return new { ready = true, items = _ready };
                var known = new HashSet<string>(_ready.Select(i => i.Id), StringComparer.OrdinalIgnoreCase);
                var all = new List<ItemInfo>(_ready);
                foreach (var i in pack)
                    if (known.Add(i.Id)) all.Add(i);
                return new { ready = true, items = all };
            }
            if (_error != null) return new { ready = false, error = _error };
            if (_pending == null && _keysTask == null) Start();
            int done = _total - (_pending?.Count ?? 0);
            return new { ready = false, progress = done, total = _total };
        }

        public static void Rebuild()
        {
            _ready = null;
            _error = null;
            _pending = null;
            try
            {
                File.Delete(CachePath);
            }
            catch
            {
                // нет файла — нечего удалять
            }
        }

        private static void Start()
        {
            MelonLogger.Msg("Предметы: читаю каталог ресурсов игры");
            _pending = null;
            _keysTask = GameCatalog.Keys;
        }

        private static System.Threading.Tasks.Task<List<string>> _keysTask;

        // Ключи читаются в фоновом потоке; когда готовы — начинаем грузить описания предметов.
        private static void TakeKeys()
        {
            var t = _keysTask;
            _keysTask = null;
            if (!t.IsCompletedSuccessfully)
            {
                _error = "Не удалось прочитать каталог игры: " + t.Exception?.GetBaseException().Message;
                return;
            }
            var keys = t.Result.Where(k => k.StartsWith(DataPrefix, StringComparison.Ordinal) && k.IndexOfAny(new[] { '/', '.', ' ' }) < 0).ToList();
            MelonLogger.Msg($"Предметы: найдено {keys.Count}");
            if (keys.Count == 0)
            {
                _error = "В каталоге игры не нашлось описаний предметов.";
                return;
            }
            if (TryLoadCache(keys.Count + GameCatalog.ModGear.Count)) return;
            // Первая сборка: пишем в журнал каждый предмет, чтобы при сбое было видно, на каком.
            _logEach = true;
            _inflight.Clear();
            _pending = keys;
            _total = keys.Count;
            _cacheKey = keys.Count + GameCatalog.ModGear.Count;
            _building = new List<ItemInfo>();
        }

        // Вызывается каждый кадр из игрового потока. Префабы грузятся асинхронно, по нескольку сразу:
        // синхронная загрузка (WaitForCompletion) в игре 2.58 может намертво повесить игру.
        public static void Tick()
        {
            if (_keysTask != null && _keysTask.IsCompleted) TakeKeys();
            if (_pending == null || _ready != null) return;

            for (int i = _inflight.Count - 1; i >= 0; i--)
            {
                var f = _inflight[i];
                bool timedOut = Time.realtimeSinceStartup - f.Started > 15f;
                if (!f.Handle.IsDone && !timedOut) continue;
                _inflight.RemoveAt(i);
                try
                {
                    if (f.Handle.IsDone && f.Handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        var data = f.Handle.Result;
                        if (data != null) _building.Add(Describe(f.Key, data));
                    }
                }
                catch
                {
                    // некоторые ключи не являются предметами — пропускаем
                }
                if (f.Handle.IsDone) Release(f.Handle);
            }

            while (_inflight.Count < InFlight && _pending.Count > 0)
            {
                string key = _pending[_pending.Count - 1];
                _pending.RemoveAt(_pending.Count - 1);
                if (_logEach) MelonLogger.Msg("  предмет: " + key);
                try
                {
                    _inflight.Add(new Loading { Key = key, Handle = Addressables.LoadAssetAsync<GearItemData>(key), Started = Time.realtimeSinceStartup });
                }
                catch
                {
                    // ключ без ресурса — пропускаем
                }
            }

            if (_pending.Count == 0 && _inflight.Count == 0)
            {
                AddModItems(_building);
                _building.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
                _ready = _building;
                _building = null;
                SaveCache(_cacheKey);
                _logEach = false;
                MelonLogger.Msg($"Предметы: список готов, {_ready.Count} шт.");
            }
        }

        // Предметы из модов ModComponent: описаний предметов у них нет, название собираем из имени ресурса.
        private static void AddModItems(List<ItemInfo> list)
        {
            var known = new HashSet<string>(list.Select(i => i.Id), StringComparer.OrdinalIgnoreCase);
            int added = 0;
            foreach (var id in GameCatalog.ModGear)
            {
                if (!known.Add(id)) continue;
                list.Add(new ItemInfo { Id = id, Name = PrettyName(id), Group = "Из модов" });
                added++;
            }
            if (added > 0) MelonLogger.Msg($"Предметы: из модов добавлено {added}");
        }

        private static string PrettyName(string id)
        {
            string s = id.Substring(5).Replace("_MOD", "").Replace('_', ' ');
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (i > 0 && char.IsUpper(c) && (char.IsLower(s[i - 1]) || char.IsDigit(s[i - 1]))) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        private sealed class Loading
        {
            public string Key;
            public AsyncOperationHandle<GearItemData> Handle;
            public float Started;
        }

        private static readonly List<Loading> _inflight = new();

        private static void Release(AsyncOperationHandle<GearItemData> handle)
        {
            try
            {
                Addressables.Release(handle);
            }
            catch
            {
                // уже освобождён
            }
        }

        private static ItemInfo Describe(string key, GearItemData data)
        {
            string id = "GEAR_" + key.Substring(DataPrefix.Length);
            string name = null;
            try
            {
                name = data.DisplayName;
            }
            catch
            {
                // ниже подставим имя ресурса
            }
            if (string.IsNullOrWhiteSpace(name)) name = id.Substring(5);
            return new ItemInfo { Id = id, Name = name, Group = GroupOf(id, data.m_Type) };
        }

        private static readonly string[] WeaponWords = { "Rifle", "Revolver", "Shotgun", "FlareGun", "Bow", "Arrow", "Ammo", "Bullet", "Shell" };
        private static readonly string[] NotWeaponWords = { "Rainbow", "Acorn", "Book", "Note" };
        private static readonly string[] LightWords = { "Lamp", "Torch", "Flare", "Match", "Lighter", "Candle", "Flashlight", "Firestriker", "Tinder" };

        private static string GroupOf(string id, GearType t)
        {
            if (WeaponWords.Any(w => id.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) &&
                !NotWeaponWords.Any(w => id.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0))
                return "Оружие и боеприпасы";
            if ((t & GearType.Food) != 0) return "Еда и напитки";
            if ((t & GearType.Clothing) != 0) return "Одежда";
            if ((t & GearType.FirstAid) != 0) return "Медицина";
            if ((t & GearType.Firestarting) != 0 || LightWords.Any(w => id.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0))
                return "Розжиг и свет";
            if ((t & GearType.Tool) != 0) return "Инструменты";
            if ((t & GearType.Material) != 0) return "Материалы";
            return "Прочее";
        }

        private sealed class Cache
        {
            public int Version { get; set; }
            public int KeyCount { get; set; }
            public List<ItemInfo> Items { get; set; }
        }

        private static bool TryLoadCache(int keyCount)
        {
            try
            {
                if (!File.Exists(CachePath)) return false;
                var c = JsonSerializer.Deserialize<Cache>(File.ReadAllText(CachePath));
                if (c?.Items == null || c.Version != CacheVersion || c.KeyCount != keyCount || c.Items.Count == 0) return false;
                _ready = c.Items;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SaveCache(int keyCount)
        {
            try
            {
                File.WriteAllText(CachePath, JsonSerializer.Serialize(new Cache { Version = CacheVersion, KeyCount = keyCount, Items = _ready }));
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Не удалось сохранить список предметов: " + e.Message);
            }
        }

        // ---------- Чертежи ----------

        private static bool _blueprintsLogged;

        public static List<BlueprintInfo> Blueprints()
        {
            var mgr = BlueprintManager.Instance;
            if (mgr == null) throw new InvalidOperationException("Загрузите сохранение");
            if (!mgr.m_IsInitialized) throw new InvalidOperationException("Игра ещё загружает чертежи, попробуйте через пару секунд");
            var all = mgr.GetAllBlueprints();
            // При первом запросе пишем в журнал каждый чертёж: если игра упадёт, будет видно, на каком.
            bool log = !_blueprintsLogged;
            if (log) MelonLogger.Msg($"Чертежи: всего {all.Count}");
            var list = new List<BlueprintInfo>();
            for (int i = 0; i < all.Count; i++)
            {
                var bp = all[i];
                if (bp == null) continue;
                string id = bp.name;
                if (log) MelonLogger.Msg($"  чертёж {i}: {id}");
                // Название берём так же, как его показывает окно крафта в игре, не трогая префаб предмета.
                string name = null;
                try
                {
                    name = bp.GetDisplayedNameWithCount();
                }
                catch
                {
                    // ниже подставим имя ресурса
                }
                if (string.IsNullOrWhiteSpace(name)) name = id;
                list.Add(new BlueprintInfo
                {
                    Index = i,
                    Name = name,
                    Count = 0,
                    Unlocked = !bp.IsLocked(false),
                });
            }
            if (log) MelonLogger.Msg("Чертежи: список готов");
            _blueprintsLogged = true;
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return list;
        }

        public static void UnlockBlueprint(int index)
        {
            var mgr = BlueprintManager.Instance;
            if (mgr == null) throw new InvalidOperationException("Загрузите сохранение");
            var all = mgr.GetAllBlueprints();
            if (index < 0 || index >= all.Count) throw new ArgumentOutOfRangeException(nameof(index));
            mgr.UnlockWithNotification(all[index]);
        }

        public static void UnlockAllBlueprints()
        {
            var mgr = BlueprintManager.Instance;
            if (mgr == null) throw new InvalidOperationException("Загрузите сохранение");
            var all = mgr.GetAllBlueprints();
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].IsLocked(false)) mgr.UnlockSilent(all[i]);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TLDTrainer.App
{
    internal sealed class Settings
    {
        public string GamePath { get; set; }

        // Назначенные клавиши: id функции → сочетание. null = клавиши по умолчанию.
        public Dictionary<string, string> Binds { get; set; }

        // Включённые слои карты. null = по умолчанию.
        public List<string> MapLayers { get; set; }
        public List<string> MapLayersSeen { get; set; }
        // Включённые группы фильтров карты (с 2.5.15). null = по умолчанию (только карта игры).
        public List<string> MapFilters { get; set; }

        // Телепорт щелчком по карте. null = включён.
        public bool? MapClickTeleport { get; set; }

        // Подложка карты: нарисованная карта из игры (true) или схема рельефа (false). null = карта игры.
        public bool? MapGameBackground { get; set; }

        // Трейнер поверх игры: клавиша показать/скрыть (имя клавиши WPF). null = Insert.
        public string OverlayKey { get; set; }

        // Мини-карта в углу экрана: клавиша и включена ли. null = Home / выключена.
        public string MiniMapKey { get; set; }
        public bool? MiniMapOn { get; set; }

        // Прятать окно трейнера, когда игра на экране (открывать клавишей). null = да.
        public bool? HideWhenPlaying { get; set; }

        // Запускать трейнер вместе с игрой (в фоне, открывается клавишей). null = да.
        public bool? StartWithGame { get; set; }

        // Где стоит окно трейнера поверх игры (левый верхний угол, пиксели экрана). null = по центру игры.
        public int? OverlayX { get; set; }
        public int? OverlayY { get; set; }
        // Размер окна поверх игры (пиксели). null = по размеру игры.
        public int? OverlayW { get; set; }
        public int? OverlayH { get; set; }

        // Где стоит мини-карта (левый верхний угол, пиксели экрана). null = правый верхний угол игры.
        public int? MiniMapX { get; set; }
        // Масштаб мини-карты: сколько метров от центра до края круга. null = 160.
        public int? MiniMapRadius { get; set; }
        public int? MiniMapY { get; set; }

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TLDTrainer", "settings.json");

        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath)) return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
            }
            catch
            {
                // испорченный файл настроек — начинаем с чистого
            }
            return new Settings();
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
    }
}

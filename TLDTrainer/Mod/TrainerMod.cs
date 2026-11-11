using System;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(TLDTrainer.TrainerMod), "TLD Trainer", TLDTrainer.Shared.Features.Version, "Seagull")]
[assembly: MelonGame("Hinterland", "TheLongDark")]
[assembly: HarmonyDontPatchAll]

namespace TLDTrainer
{
    // Встроенная часть трейнера: выполняет команды из окна трейнера и горячие клавиши.
    public class TrainerMod : MelonMod
    {
        private bool _wasInGame;
        private float _saveTimer;

        // Игра загрузилась до главного меню. До этого не трогаем игровой код и не ставим патчи:
        // вызовы во время загрузки игры могут её уронить.
        private bool _ready;
        private int _scenesSeen;

        public override void OnInitializeMelon()
        {
            State.Load();
            Hotkeys.Load();
            Server.Start();
            LoggerInstance.Msg("TLD Trainer загружен.");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (_ready) return;
            _scenesSeen++;
            LoggerInstance.Msg($"Сцена «{sceneName}» загружена.");
            // Ждём главное меню; на случай другого имени сцены — включаемся с третьей загруженной сцены.
            bool menu = sceneName != null && sceneName.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!menu && _scenesSeen < 3) return;
            LoggerInstance.Msg("Включаю трейнер.");
            ApplyPatches();
            // Чтобы игра продолжала работать и выполнять команды, когда активно окно трейнера.
            Application.runInBackground = true;
            _ready = true;
        }

        // Ставим патчи по одному: если какой-то не подходит к этой версии игры, остальные всё равно заработают.
        private void ApplyPatches()
        {
            int ok = 0, failed = 0;
            foreach (var type in typeof(TrainerMod).Assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false).Length == 0) continue;
                try
                {
                    HarmonyInstance.CreateClassProcessor(type).Patch();
                    ok++;
                }
                catch (Exception e)
                {
                    failed++;
                    LoggerInstance.Warning($"Патч {type.Name} не применён: {e.Message}");
                }
            }
            LoggerInstance.Msg($"Патчи: применено {ok}, с ошибкой {failed}.");
        }

        public override void OnUpdate()
        {
            if (!_ready) return;
            bool inGame = InGame();
            Server.InGame = inGame;

            // Игра сама выключает работу в фоне (например, при смене настроек экрана) — тогда при переключении
            // на окно трейнера она замирает и не отвечает. Возвращаем обратно.
            if (!Application.runInBackground)
            {
                Application.runInBackground = true;
                if (!_bgLogged)
                {
                    _bgLogged = true;
                    LoggerInstance.Msg("Игра выключила работу в фоне — включил обратно.");
                }
            }

            KeepWindowed();
            try
            {
                OverlayKeys.Tick();
            }
            catch
            {
                // ввод недоступен — трейнер ловит клавиши сам
            }

            MainThread.Pump();
            try
            {
                Catalog.Tick();
            }
            catch (Exception e)
            {
                LoggerInstance.Warning("[предметы] " + e.Message);
            }

            if (inGame && !_wasInGame) SaveGuard.PlayerDied = false;
            if (!inGame && _wasInGame)
            {
                Cheats.OnLeftGame();
                MapBuilder.Reset();
                GameMap.Reset();
            }
            _wasInGame = inGame;

            if (inGame)
            {
                Hotkeys.Handle();
                Cheats.Tick();
                try
                {
                    MapBuilder.Tick();
                }
                catch (Exception e)
                {
                    LoggerInstance.Warning("[карта] " + e.Message);
                    MapBuilder.Reset();
                }
                try
                {
                    GameMap.Tick();
                }
                catch (Exception e)
                {
                    LoggerInstance.Warning("[карта игры] " + e.Message);
                    GameMap.Fail(e.Message);
                }
            }

            _saveTimer -= Time.unscaledDeltaTime;
            if (_saveTimer <= 0f)
            {
                _saveTimer = 2f;
                State.SaveIfDirty();
            }
        }

        private bool _bgLogged;
        private float _nextScreenCheck;

        // В «эксклюзивном» полноэкранном режиме игра сворачивается, как только активным становится другое окно,
        // и трейнер поверх неё не виден. Переводим в полноэкранный оконный: выглядит так же, но не сворачивается.
        private void KeepWindowed()
        {
            if (Time.unscaledTime < _nextScreenCheck) return;
            _nextScreenCheck = Time.unscaledTime + 1f;
            try
            {
                if (Screen.fullScreen && Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen)
                {
                    Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                    LoggerInstance.Msg("Экран: эксклюзивный полноэкранный режим → полноэкранный оконный (чтобы игра не сворачивалась под трейнером).");
                }
            }
            catch (Exception e)
            {
                _nextScreenCheck = float.MaxValue;
                LoggerInstance.Warning("Экран: не удалось сменить режим: " + e.Message);
            }
        }

        public override void OnGUI()
        {
            if (!_ready) return;
            Toast.Draw();
        }

        public override void OnApplicationQuit()
        {
            State.SaveIfDirty();
        }

        private static bool InGame()
        {
            try
            {
                return !GameManager.IsMainMenuActive() && GameManager.GetConditionComponent() != null;
            }
            catch
            {
                return false;
            }
        }
    }
}

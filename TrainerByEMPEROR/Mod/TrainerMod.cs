using System.IO;
using System;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(TrainerByEMPEROR.TrainerMod), "TrainerByEMPEROR", TrainerByEMPEROR.Shared.Features.Version, "EMPEROR")]
[assembly: MelonGame("Hinterland", "TheLongDark")]
[assembly: HarmonyDontPatchAll]

namespace TrainerByEMPEROR
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
            // the mod was renamed from its old name: move the player's files over once
            Legacy.Move(LoggerInstance, "TLDTrainer", "TrainerByEMPEROR", "TLDTrainer.exe", "");
            Legacy.PutAside(LoggerInstance, Path.Combine(MelonLoader.Utils.MelonEnvironment.ModsDirectory, "TLDTrainer.app.txt"));
            State.Load();
            Hotkeys.Load();
            Server.Start();
            LoggerInstance.Msg("TrainerByEMPEROR загружен.");
            StartApp(open: false);
        }

        // Трейнер (окно) запускается сам: при старте игры в фоне, а если его нет — по клавише Ins прямо в игре.
        // Где он лежит: путь, который записал сам трейнер (Mods\TrainerByEMPEROR.app.txt), иначе Mods\TrainerByEMPEROR\TrainerByEMPEROR.exe.
        private static string FindApp()
        {
            string mods = MelonLoader.Utils.MelonEnvironment.ModsDirectory;
            string file = System.IO.Path.Combine(mods, "TrainerByEMPEROR.app.txt");
            if (System.IO.File.Exists(file))
            {
                string exe = System.IO.File.ReadAllText(file).Trim();
                if (System.IO.File.Exists(exe)) return exe;
            }
            string local = System.IO.Path.Combine(mods, "TrainerByEMPEROR", "TrainerByEMPEROR.exe");
            return System.IO.File.Exists(local) ? local : null;
        }

        public static bool AppRunning()
        {
            foreach (var p in System.Diagnostics.Process.GetProcesses())
            {
                try
                {
                    if (p.ProcessName.StartsWith("TrainerByEMPEROR", StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch
                {
                    // процесс уже закрылся
                }
                finally
                {
                    p.Dispose();
                }
            }
            return false;
        }

        public static void StartApp(bool open)
        {
            try
            {
                if (AppRunning()) return;
                string exe = FindApp();
                if (exe == null)
                {
                    MelonLogger.Msg("Окно трейнера не найдено: положите TrainerByEMPEROR.exe в Mods\\TrainerByEMPEROR или откройте его один раз вручную.");
                    return;
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, open ? "--background --open" : "--background")
                {
                    UseShellExecute = true,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(exe),
                });
                MelonLogger.Msg(open ? "Трейнер запущен по клавише и сейчас откроется поверх игры." : "Трейнер запущен в фоне: окно открывается клавишей поверх игры.");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Не удалось запустить трейнер: " + e.Message);
            }
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

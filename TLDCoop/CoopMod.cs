using System;
using MelonLoader;
using TLDCoop.Net;
using TLDCoop.UI;
using UnityEngine;

[assembly: MelonInfo(typeof(TLDCoop.CoopMod), "TLD Coop", TLDCoop.Version.Number, "IvanC")]
[assembly: MelonGame("Hinterland", "TheLongDark")]
[assembly: HarmonyDontPatchAll]

namespace TLDCoop
{
    // Точка входа мода. Здесь только порядок запуска и вызовы по кадрам,
    // вся логика сети — в Net/Session.cs, работа с игрой — в Game/.
    public class CoopMod : MelonMod
    {
        // Игра 2.58 падает, если трогать её код или ставить патчи до главного меню.
        // Поэтому мод «просыпается» только на сцене MainMenu.
        private bool _ready;
        private int _scenesSeen;
        private ulong _launchJoin;     // SteamID хоста, если игру запустили кнопкой Steam «Присоединиться к игре»
        private float _launchJoinAt;

        public override void OnInitializeMelon()
        {
            Config.Load();
            Game.Rules.Load();
            Game.Bot.Detect();
            Game.DeathPacks.Init();     // свои строки в слоте сохранения (Game/SlotStore.cs)
            Game.PlantRegrow.Init();
            Game.BrokenSaves.Sweep();   // нечитаемые копии мира от 0.19.53 (игра с ними не доходит до меню)
            LoggerInstance.Msg("TLD Coop " + Version.Text + " загружен, жду главное меню.");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (_ready) return;
            _scenesSeen++;
            bool menu = sceneName != null && sceneName.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!menu && _scenesSeen < 3) return;
            _ready = true;
            ApplyPatches();
            // Сеть должна работать, даже когда окно игры не в фокусе.
            Application.runInBackground = true;
            Game.Bot.OnReady();
            bool steam = Steam.Init();
            if (string.IsNullOrWhiteSpace(Config.Name))
            {
                Config.Name = steam ? Steam.MyName() : Environment.UserName;
                Config.Save();
            }
            _launchJoin = steam && !Game.Bot.Active ? Steam.LaunchJoinTarget() : 0;
            _launchJoinAt = Time.unscaledTime + 3f;
            LoggerInstance.Msg($"Готов. Окно мода: {Config.MenuKeyName}.");
            Toast.Show($"TLD Coop: окно мода — {Config.MenuKeyName}");
        }

        // Патчи ставятся по одному классу: если какой-то не подошёл к версии игры, остальные работают.
        private void ApplyPatches()
        {
            int ok = 0, failed = 0;
            foreach (var type in typeof(CoopMod).Assembly.GetTypes())
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
            if (ok + failed > 0) LoggerInstance.Msg($"Патчи: применено {ok}, с ошибкой {failed}.");
            Game.PlantRegrow.Patch(HarmonyInstance);   // TinyTweaks-RespawnablePlants, если он есть
            Game.ModSpawnLink.Patch(HarmonyInstance);  // ModComponent / GearSpawner, если он есть
            Game.AutumnLink.Patch(HarmonyInstance);      // AutumnComforts, если он есть
            Game.CalorieWaltzLink.Patch(HarmonyInstance); // FoodsByCalorieWaltz, если он есть
            Game.TrainerLink.Patch(HarmonyInstance);      // TLDTrainer (только у хоста), если он есть
        }

        public override void OnUpdate()
        {
            if (!_ready) return;
            UI.Widgets.Capture();
            UI.L.Refresh();
            if (Input.GetKeyDown(Config.MenuKey) && !UI.Widgets.Typing) CoopWindow.Toggle();
            if (!Application.runInBackground) Application.runInBackground = true;
            try
            {
                Game.Models.Tick();
                Game.Characters.HintTick();
                if (_launchJoin != 0 && Time.unscaledTime >= _launchJoinAt)
                {
                    ulong host = _launchJoin;
                    _launchJoin = 0;
                    if (Session.Mode == Mode.Off && !Game.GameApi.InGame())
                    {
                        string name = Steam.FriendName(host);
                        LoggerInstance.Msg("Запуск через Steam «Присоединиться»: подключаюсь к " + name);
                        Session.JoinSteam(host, name);
                        Toast.Show("Подключаюсь к " + name + " (Steam «Присоединиться»)");
                    }
                }
                Session.Tick();
                Game.Death.Tick();
                Game.Activity.TickIce();
                Game.NoPause.Tick();
                Game.Bot.Tick();
            }
            catch (Exception e)
            {
                LoggerInstance.Error("[сеть] " + e);
            }
        }

        public override void OnLateUpdate()
        {
            if (_ready) Game.AnimalSync.AfterAiUpdate();   // страховка: к концу кадра игрок не должен остаться у друга
            if (_ready) Game.AnimalSync.TeleportGuard();
            if (_ready) CoopWindow.KeepCursor();
            if (_ready) { try { Game.Death.LateTick(); } catch { } }
            if (_ready) CoopWindow.CheckInvites();
        }

        private string _lastGuiError;

        public override void OnGUI()
        {
            if (!_ready) return;
            try
            {
                CoopWindow.Draw();
                Game.Death.DrawScreen();
            }
            catch (Exception e)
            {
                // OnGUI идёт несколько раз за кадр, одну и ту же ошибку пишем в лог один раз.
                if (e.Message != _lastGuiError) LoggerInstance.Warning("[окно] " + e);
                _lastGuiError = e.Message;
            }
            Toast.Draw();
        }

        public override void OnApplicationQuit()
        {
            Session.Quit();
            Config.Save();
            try { Game.Rules.SaveHost(); } catch { }   // правила хоста, если вышел с открытым окном
        }
    }
}

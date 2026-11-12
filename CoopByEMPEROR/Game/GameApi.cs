using System;
using Il2Cpp;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Всё, что мод читает из игры и меняет в ней, собрано здесь. Если после обновления игры
    // что-то сломалось, чинить почти всегда нужно только этот файл.
    public static class GameApi
    {
        public static bool InGame()
        {
            try
            {
                return !GameManager.IsMainMenuActive() && GameManager.GetConditionComponent() != null
                    && GameManager.GetPlayerTransform() != null && !IsLoading();
            }
            catch
            {
                return false;
            }
        }

        public static bool IsLoading()
        {
            try
            {
                // Экран загрузки держит мод (Game/LoadVeil.cs): локация уже загружена.
                return InterfaceManager.IsPanelLoadingEnabledOrLoading() && !LoadVeil.Holding;
            }
            catch
            {
                return false;
            }
        }

        public static string Scene()
        {
            try
            {
                return GameManager.m_ActiveScene ?? "";
            }
            catch
            {
                return "";
            }
        }

        public static bool TryGetPlayer(out float x, out float y, out float z, out float yaw, out bool crouched)
        {
            x = y = z = yaw = 0f;
            crouched = false;
            var t = GameManager.GetPlayerTransform();
            if (t == null) return false;
            var p = t.position;
            x = p.x;
            y = p.y;
            z = p.z;
            // Повернут игрок камерой, а не телом.
            var cam = GameManager.GetMainCamera();
            yaw = cam != null ? cam.transform.eulerAngles.y : t.eulerAngles.y;
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                crouched = pm != null && pm.PlayerIsCrouched();
            }
            catch
            {
            }
            return true;
        }

        public static void Teleport(float x, float y, float z)
        {
            var pm = GameManager.GetPlayerManagerComponent();
            var t = GameManager.GetPlayerTransform();
            if (pm == null || t == null) return;
            pm.TeleportPlayer(new Vector3(x, y + 0.2f, z), Quaternion.Euler(0f, t.eulerAngles.y, 0f));   // ровно, без наклона
        }

        // Перейти в другую локацию так же, как через дверь или переход (компонент игры LoadScene).
        // Не через GameManager.LoadSceneWithLoadingScreen: тот грузит сцену без того, что игра делает
        // при обычном переходе. После таких переходов (0.7.17) в сохранении хоста осталась только текущая
        // локация, Загадочное озеро пропало; похоже, причина в этом. Так же переходит SkyCoop.
        // key — файл сохранения локации (ItemSync.SceneKey): «сцена_guid» у домиков, деливших одну сцену Unity.
        // Без него игра грузит общую копию сцены, а не тот домик.
        public static void LoadScene(string scene, string key = null)
        {
            try
            {
                var go = new GameObject("TLDCoop_SceneLoader");
                UnityEngine.Object.DontDestroyOnLoad(go);
                var loader = go.AddComponent<Il2Cpp.LoadScene>();
                loader.m_SceneToLoad = scene;
                if (!string.IsNullOrEmpty(key) && key.Length > scene.Length + 1 && key.StartsWith(scene + "_", StringComparison.Ordinal))
                {
                    loader.m_SceneCanBeInstanced = true;
                    loader.m_GUID = key.Substring(scene.Length + 1);
                }
                loader.Activate();
                MelonLoader.MelonLogger.Msg("[переход] через LoadScene в " + scene);
            }
            catch (System.Exception e)
            {
                MelonLoader.MelonLogger.Warning("[переход] LoadScene: " + e.Message + ", перехожу напрямую");
                GameManager.LoadSceneWithLoadingScreen(scene);
            }
        }

        // ---------- Время и погода ----------

        public static float TimeOfDay() => GameManager.GetTimeOfDayComponent().GetNormalizedTime();
        public static int DayNumber() => GameManager.GetTimeOfDayComponent().GetDayNumber();
        public static void SetTimeOfDay(float n) => GameManager.GetTimeOfDayComponent().SetNormalizedTime(n, true);

        // Полное время: номер дня и часы. Одних часов мало: после сна отдельно или долгого действия через полночь
        // игра уже перевела день, и «21:00» ставилось на следующие сутки — игрок уходил вперёд на день.
        public static float TotalTime() => DayNumber() + TimeOfDay();

        public static void SetDayAndTime(int day, float tod)
        {
            var t = GameManager.GetTimeOfDayComponent();
            tod -= Mathf.Floor(tod);
            if (day > 0)
            {
                var ws = t.m_WeatherSystem;
                if (ws != null && t.GetDayNumber() != day)
                {
                    ws.SetDayNumber(day);
                    // Счёт дней в игре может начинаться с нуля или единицы: подстраиваемся по факту.
                    int got = t.GetDayNumber();
                    if (got != day) ws.SetDayNumber(day - (got - day));
                }
            }
            t.SetNormalizedTime(tod, true);
        }

        public static int WeatherStage()
        {
            var w = GameManager.GetWeatherComponent();
            return w != null ? (int)w.GetWeatherStage() : -1;
        }

        public static void SetWeather(int stage)
        {
            var wt = GameManager.GetWeatherTransitionComponent();
            if (wt != null) wt.ActivateWeatherSetImmediate((Il2Cpp.WeatherStage)stage);
        }

        public static int ModeType()
        {
            try { return (int)ExperienceModeManager.GetCurrentExperienceModeType(); } catch { return -1; }
        }

        // Настройки режима «Персонализация» (строка-код, как в сохранении), или "" если режим обычный.
        public static string CustomModeString()
        {
            try
            {
                var m = GameManager.GetExperienceModeManagerComponent();
                return m != null && m.InCustomMode() ? m.GetCurrentCustomModeString() ?? "" : "";
            }
            catch { return ""; }
        }

        // Ставит свои настройки «Персонализации» как у хоста. true — если пришлось менять.
        public static bool MatchCustomMode(string code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            var m = GameManager.GetExperienceModeManagerComponent();
            if (m == null || !m.InCustomMode() || m.GetCurrentCustomModeString() == code) return false;
            return m.SetCurrentCustomModeString(code);
        }

        // ---------- Сообщения ----------

        public static void HudMessage(string text)
        {
            try
            {
                HUDMessage.AddMessage(text, 3f, false, true);
            }
            catch
            {
                UI.Toast.Show(text);
            }
        }
    }
}

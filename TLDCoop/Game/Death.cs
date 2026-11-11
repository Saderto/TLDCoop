using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using TLDCoop.UI;
using UnityEngine;
using UnityEngine.AI;

namespace TLDCoop.Game
{
    // Смерть в кооперативе. Игра проигрывает смерть как обычно и показывает свой экран смерти
    // (Panel_LifeAfterDeath, причина смерти), но сохранение не удаляется (патч HandlePlayerDeath),
    // а дальше в журнал игра не уходит. Поверх экрана смерти мод рисует выбор места; кнопки игры
    // на этом экране = вариант по умолчанию. Игрок появляется в тех же точках, где игра ставит
    // игрока в начале новой кампании (PlayerManager.PickRandomSpawnPoint). Вне кооп-режима
    // смерть обычная.
    public static class Death
    {
        private enum Step { None, Downed, Choosing, Dropping, Moving, Grace }
        private static Step _step;
        private static float _stepTime;
        private static Rules _rules;
        private static Vector3 _deathPos;
        private static string _deathScene = "", _deathKey = "";
        private static bool _neverDieBefore;
        private static readonly Queue<GearItem> DropQueue = new();
        private static bool _inStruggle;   // умер посреди схватки: игра не вернула камеру (см. FixStruggleCamera)
        private static bool _packTried;
        private static Container _pack;
        private static Place _place;
        private static string _sceneRequested;
        private static bool _restoreNeverDie;   // сброс случился вне игры: вернуть m_NeverDie, когда игрок снова в игре
        private static float _restoreAt = -1f;

        public static bool ScreenOpen => _step == Step.Choosing || _step == Step.Downed;
        public static bool IsDowned => _step == Step.Downed;
        public static bool Busy => _step != Step.None;

        // Регионы для «случайного места где угодно» (сцены из каталога игры 2.58).
        private static readonly string[] Regions =
        {
            "LakeRegion", "CoastalRegion", "RuralRegion", "WhalingStationRegion", "CrashMountainRegion",
            "MarshRegion", "MountainTownRegion", "RiverValleyRegion", "CanneryRegion", "AshCanyonRegion",
            "BlackrockRegion", "TracksRegion",
        };

        // Вызывается из патча HandlePlayerDeath. true = смерть наша, обычную обработку пропустить.
        public static bool OnDeath(string why)
        {
            // Друг в своём сохранении умирает по обычным правилам игры: кооперативное «ранен» только в общем мире.
            if (Session.Mode == Mode.Off || (_step == Step.None && !Session.InSharedWorld)) return false;
            if (_step != Step.None) return true;
            _rules = Session.CurrentRules;
            var cond = GameManager.GetConditionComponent();
            _neverDieBefore = cond != null && cond.m_NeverDie;
            var t = GameManager.GetPlayerTransform();
            _deathPos = t != null ? t.position : Vector3.zero;
            _deathScene = GameApi.Scene();
            _deathKey = ItemSync.SceneKey();
            _cause = why ?? "";
            _need = Rescue.Need.Other;
            try { if (cond != null) _need = Rescue.FromCause(cond.GetCauseOfDeath()); } catch { }
            try { _deathTod = GameApi.TimeOfDay(); _deathDay = GameApi.DayNumber(); } catch { }
            _inStruggle = false;
            try { var st = GameManager.GetPlayerStruggleComponent(); _inStruggle = st != null && (st.m_Active || st.InStruggle()); } catch { }
            MelonLogger.Msg($"[смерть] {why}, место {_deathScene} {_deathPos}" + (_inStruggle ? " (в схватке)" : ""));
            // «Ранен — друг поднимет» убрано по просьбе (0.19.67): сразу экран смерти.
            EnterChoosing();
            return true;
        }

        // Рядом (в этой же локации) есть живой друг — значит, есть кому поднять.
        private static bool FriendHere()
        {
            string scene = GameApi.Scene();
            foreach (var p in Session.Players.Values)
                if (Session.With(p, scene) && Time.unscaledTime - p.LastState < 5f && p.Action != Activity.Downed && p.Action != Activity.Dead) return true;
            return false;
        }

        // ---------- Ранен: друг может поднять ----------

        private static void EnterDowned()
        {
            Session.Announce("ранен" + (string.IsNullOrEmpty(_cause) ? "" : " (" + _cause + ")") + " — подойди и подними");
            Chronicle.Add(Session.LocalName + " ранен" + (string.IsNullOrEmpty(_cause) ? "" : " (" + _cause + ")"));
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                pm?.UnequipImmediate(false);
                pm?.SetControlMode(PlayerControlMode.Dead);
                var cond = GameManager.GetConditionComponent();
                if (cond != null) cond.m_NeverDie = true;
            }
            catch (Exception e) { MelonLogger.Warning("[смерть] ранен: " + e.Message); }
            Activity.Override = Activity.Downed;
            _step = Step.Downed;
            _stepTime = Time.unscaledTime;
            _lastTick = _stepTime;
            UnFade();
        }

        // Друг подошёл и поднял.
        public static void OnRevivedBy(string name)
        {
            if (_step != Step.Downed) return;
            Activity.Override = 0;
            Revive();
            try { ConsoleManager.CONSOLE_afflictions_cure(); } catch { }
            try
            {
                var fr = GameManager.GetFreezingComponent();
                if (fr != null) fr.m_CurrentFreezing = 0f;
                var cond = GameManager.GetConditionComponent();
                if (cond != null) cond.m_CurrentHP = Math.Max(cond.m_CurrentHP, cond.m_MaxHP * 0.25f);
            }
            catch { }
            AfterStruggleDeath();
            MelonLogger.Msg("[смерть] поднял " + name);
            Session.Announce("снова на ногах (поднял " + name + ")");
            Chronicle.Add(name + " поднял " + Session.LocalName);
            GameApi.HudMessage(name + " поднял тебя");
            _step = Step.Grace;
            _stepTime = Time.unscaledTime;
            UnFade();
        }

        private static void GiveUp()
        {
            if (_step != Step.Downed) return;
            Activity.Override = 0;
            EnterChoosing();
        }

        private static void EnterChoosing()
        {
            Session.Announce("погиб" + (string.IsNullOrEmpty(_cause) ? "" : " (" + _cause + ")"));
            Chronicle.Add(Session.LocalName + " погиб" + (string.IsNullOrEmpty(_cause) ? "" : " (" + _cause + ")"));
            ShowGameDeathScreen();
            // Пока он на экране смерти, у остальных фигура лежит (раньше с 0 вставала и стояла мёртвой).
            Activity.Override = Activity.Dead;
            _step = Step.Choosing;
            _stepTime = Time.unscaledTime;
            var allowed = AllowedPlaces();
            if (!_rules.PlayerChoosesPlace || allowed.Count < 2) _autoPlace = allowed.Contains(_rules.DefaultPlace) ? _rules.DefaultPlace : allowed[0];
            else _autoPlace = null;
        }

        // ---------- Наблюдение за другом, пока ждёшь воскрешения ----------

        private static bool _spectate;
        private static bool _camSaved;
        private static Vector3 _camLocalPos;
        private static Quaternion _camLocalRot;
        private static float _orbit;

        private static Player SpectateTarget()
        {
            string scene = GameApi.Scene();
            foreach (var p in Session.Players.Values)
                if (Session.With(p, scene) && p.Puppet.Alive && p.Action != Activity.Dead) return p;
            return null;
        }

        private static void SetSpectate(bool on)
        {
            if (on == _spectate) return;
            _spectate = on;
            var cam = GameManager.GetMainCamera();
            if (cam == null) return;
            if (on)
            {
                _camLocalPos = cam.transform.localPosition;
                _camLocalRot = cam.transform.localRotation;
                _camSaved = true;
                UnFade();
            }
            else if (_camSaved)
            {
                cam.transform.localPosition = _camLocalPos;
                cam.transform.localRotation = _camLocalRot;
                _camSaved = false;
            }
        }

        // Из OnLateUpdate: камера медленно облетает друга.
        public static void LateTick()
        {
            if (!_spectate) return;
            if (_step != Step.Choosing) { SetSpectate(false); return; }
            var target = SpectateTarget();
            var cam = GameManager.GetMainCamera();
            if (target == null || cam == null) { SetSpectate(false); return; }
            _orbit += Time.unscaledDeltaTime * 12f;
            var center = target.Puppet.LabelPoint - Vector3.up * 0.8f;
            var pos = center + Quaternion.Euler(0f, _orbit, 0f) * new Vector3(0f, 1.4f, -4f);
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(center - pos, Vector3.up);
        }

        private static Place? _autoPlace;
        private static string _cause = "";
        private static Rescue.Need _need = Rescue.Need.Other;
        private static float _lastTick;
        private static float _deathTod;
        private static int _deathDay;   // выбор не нужен: место задал хост

        // То же, что делает игра при смерти, без удаления сохранения.
        private static void ShowGameDeathScreen()
        {
            try
            {
                if (InterfaceManager.TryGetPanel<Panel_Log>(out var log) && log.IsEnabled()) log.ExitInterfaceOnDeath();
                if (GameManager.m_Rest != null && GameManager.m_Rest.IsSleeping()) GameManager.m_Rest.EndSleeping(true);
                var pm = GameManager.GetPlayerManagerComponent();
                pm?.UnequipImmediate(false);
                pm?.SetControlMode(PlayerControlMode.Dead);
                SetPlayerAudio(false);
                BaseAiManager.ResetAudioLoops();
                GameManager.CancelPendingSave();
                // Panel_LifeAfterDeath в 2.58 часть системы «жизней» и вне её показывает пустой
                // чёрный экран, поэтому надпись о смерти рисует мод (DrawScreen).
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[смерть] экран смерти: " + e);
            }
        }

        private static void SetPlayerAudio(bool on)
        {
            try
            {
                var parent = GameManager.GetPlayerObject()?.transform.parent;
                if (parent == null) return;
                foreach (var cb in parent.GetComponentsInChildren<AudioCallback>()) cb.ToggleDisableAudioEvents(!on);
            }
            catch { }
        }

        // Закрыть экран смерти и вернуть управление.
        private static void Revive()
        {
            try
            {
                if (InterfaceManager.TryGetPanel<Panel_LifeAfterDeath>(out var lad) && lad.IsEnabled()) lad.Enable(false);
                if (InterfaceManager.TryGetPanel<Panel_Log>(out var log) && log.IsEnabled()) log.ExitInterface();
                GameManager.GetPlayerManagerComponent()?.SetControlMode(PlayerControlMode.Normal);
                var cam = GameManager.GetVpFPSCamera();
                if (cam != null) cam.enabled = true;
                SetPlayerAudio(true);
                StopDeathVoice();
                var cond = GameManager.GetConditionComponent();
                if (cond != null) cond.m_NeverDie = true;   // до конца воскрешения второй раз не умираем
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[смерть] воскрешение: " + e);
            }
        }

        // 0.19.51: при смерти игра (Condition.PlayerDeath → PlayPlayerDeathAudio) запускает через PlayerVoice звук смерти,
        // а при смерти от волков — m_DieFromWolfAudio. В одиночной игре дальше экран смерти, у нас игрок встаёт, а звук
        // мог доигрывать (жалоба 11-10: после смерти в схватке рычание волков «замедленное»; что именно звучало, по логу
        // не доказано). При воскрешении глушим то, что ещё играет через PlayerVoice, и пишем в лог, что это было.
        private static void StopDeathVoice()
        {
            try
            {
                var pv = GameManager.GetPlayerVoiceComponent();
                var ar = pv?.m_ActiveRequest;
                if (pv == null || ar == null || ar.m_PlaybackID == 0) { MelonLogger.Msg("[смерть] при воскрешении голос игрока молчит"); return; }
                uint id = ar.m_PlaybackID;
                string name = "";
                try { name = ar.GetEventName() ?? ""; } catch { }
                try { pv.Stop(id); } catch { }
                try { AkSoundEngine.StopPlayingID(id); } catch { }
                MelonLogger.Msg($"[смерть] при воскрешении ещё играл звук голоса игрока «{(name.Length > 0 ? name : ar.m_EventID.ToString())}» — заглушил");
            }
            catch (Exception e) { MelonLogger.Warning("[смерть] звук смерти: " + e.Message); }
        }

        // После того как здоровье уже вернули: с нулевым здоровьем игра считает игрока умирающим и камеру не трогает.
        private static void AfterStruggleDeath()
        {
            if (!_inStruggle) return;
            _inStruggle = false;
            FixStruggleCamera("после смерти в схватке");
        }

        // Смерть посреди схватки. В одиночной игре дальше экран смерти, поэтому PlayerStruggle.OnPlayerDeath камеру
        // не возвращает: слой камеры остаётся от схватки (камера ведёт анимацию схватки), угол камеры — тоже.
        // После воскрешения камера была перекошена, а управление — «зеркальным» (вперёд вело не туда, куда смотришь).
        // Делаем то, что игра делает в конце обычной схватки (GetUpAnimationComplete), без возврата вещи в руки
        // (она уже в рюкзаке), и ставим игрока ровно.
        public static void FixStruggleCamera(string why)
        {
            try
            {
                var st = GameManager.GetPlayerStruggleComponent();
                if (st != null)
                {
                    // GetUpAnimationComplete сразу выходит, если m_Active = false, а OnPlayerDeath его как раз сбрасывает.
                    // Поэтому раньше вызов ничего не делал: оставались слой камеры схватки (перекос, «зеркальное»
                    // управление) и звуковой параметр замедления (ResetGameSettingsToNormal не вызывался).
                    st.m_ItemInHandsAtStruggleStart = null;
                    st.m_Active = true;
                    try { st.GetUpAnimationComplete(); }
                    finally { if (st.m_Active) st.m_Active = false; }
                    try { st.StopMauledMuffle(); } catch { }
                    try { st.StopWeaponSlowdownAudio(); } catch { }
                    try { st.ResetGameSettingsToNormal(); } catch { }
                }
                ResetArms();
                var t = GameManager.GetPlayerTransform();
                if (t != null)
                {
                    float yaw = t.eulerAngles.y;
                    t.rotation = Quaternion.Euler(0f, yaw, 0f);
                    var cam = GameManager.GetVpFPSCamera();
                    if (cam != null) { cam.SetAngle(yaw, 0f); cam.UpdateCameraRotation(); }
                }
                GameManager.GetPlayerManagerComponent()?.SetControlMode(PlayerControlMode.Normal);
                MelonLogger.Msg("[смерть] камера возвращена " + why);
                _camWatchUntil = Time.unscaledTime + 40f; _camLast = ""; _camNext = 0f; _armResets = 1;
            }
            catch (Exception e) { MelonLogger.Warning("[смерть] камера: " + e.Message); }
        }

        // Руки и камера от первого лица (аниматор PlayerAnimation) после смерти в схватке так и оставались в анимации
        // схватки: конец схватки (Trigger_Struggle_End → подъём) не запускался. Анимация шла по кругу и качала камеру
        // (в логе друга наклон 0° → 40° → 70° → 0° каждые полсекунды-секунду при выключенной схватке). PlayerAnimation.Reset —
        // то же, что игра делает при загрузке: параметры по умолчанию, слой камеры 0, состояние «Main.Idle».
        private static int _armResets;
        private static void ResetArms()
        {
            try
            {
                var pa = GameManager.GetPlayerAnimationComponent();
                if (pa == null) return;
                pa.ResetStruggleStartEnd();
                pa.Reset();
                _armResets++;
                MelonLogger.Msg("[смерть] руки и камера от первого лица: сброс анимации схватки (Main.Idle)");
            }
            catch (Exception e) { MelonLogger.Warning("[смерть] сброс рук: " + e.Message); }
        }

        private static int ArmState()
        {
            try
            {
                var an = GameManager.GetPlayerAnimationComponent()?.m_Animator;
                if (an == null || !an.isActiveAndEnabled) return 0;
                return an.GetCurrentAnimatorStateInfo(0).fullPathHash;
            }
            catch { return 0; }
        }

        private static List<Place> AllowedPlaces()
        {
            var list = new List<Place>();
            foreach (Place p in new[] { Place.DeathSpot, Place.NearPlayer, Place.Local, Place.Anywhere })
                if (_rules.IsAllowed(p)) list.Add(p);
            if (list.Count == 0) list.Add(Place.Local);
            return list;
        }

        public static void Choose(Place place)
        {
            if (_step != Step.Choosing) return;
            _place = place;
            MelonLogger.Msg("[смерть] место: " + place);
            if (Activity.Override == Activity.Dead) Activity.Override = 0;
            SetSpectate(false);
            Revive();
            ApplyLossesAndHealth();
            AfterStruggleDeath();
            _packTried = false;
            _pack = null;
            _step = Step.Dropping;
            _stepTime = Time.unscaledTime;
        }

        // Кнопка игры на экране смерти («В главное меню»): воскреснуть в месте по умолчанию.
        public static void ChooseDefault()
        {
            if (_step != Step.Choosing) return;
            var allowed = AllowedPlaces();
            Choose(_autoPlace ?? (allowed.Contains(_rules.DefaultPlace) ? _rules.DefaultPlace : allowed[0]));
        }

        // Лут, стартовый набор, навыки, здоровье. Предметы на землю выкладываются по несколько за кадр (Tick).
        private static void ApplyLossesAndHealth()
        {
            var inv = GameManager.GetInventoryComponent();
            var pm = GameManager.GetPlayerManagerComponent();
            DropQueue.Clear();
            if (_rules.Loot != LootMode.Keep && inv != null && inv.m_Items != null)
            {
                try { pm?.UnequipImmediate(false); } catch { }
                var items = new List<GearItem>();
                for (int i = 0; i < inv.m_Items.Count; i++)
                {
                    var gi = inv.m_Items[i]?.m_GearItem;
                    if (gi != null && !gi.m_CantDropItem && !DeathPacks.IsWaterSupply(gi)) items.Add(gi);   // вода во флягах — служебная, остаётся при игроке
                }
                if (_rules.Loot == LootMode.Lost)
                {
                    foreach (var gi in items)
                    {
                        try
                        {
                            if (gi.m_ClothingItem != null && pm != null && pm.IsWearingClothingItem(gi)) pm.TakeOffClothingItem(gi);
                            inv.DestroyGear(gi);
                        }
                        catch (Exception e) { MelonLogger.Warning("[смерть] удаление: " + e.Message); }
                    }
                    MelonLogger.Msg($"[смерть] лут пропал: {items.Count} предметов");
                }
                else
                {
                    foreach (var gi in items) DropQueue.Enqueue(gi);
                }
            }

            SkillLoss(_rules.SkillLossPercent);

            try { ConsoleManager.CONSOLE_afflictions_cure(); } catch (Exception e) { MelonLogger.Warning("[смерть] лечение: " + e.Message); }
            FoodPackLink.ImportPlayerState("");   // эффекты еды (FoodPackBySeagull) после смерти тоже сброшены
            ModDataLink.ResetPersonal();          // и алкоголь AlcoholMod, свои болезни AfflictionComponent
            try
            {
                var fr = GameManager.GetFreezingComponent();
                if (fr != null) fr.m_CurrentFreezing = 0f;
                var th = GameManager.GetThirstComponent();
                if (th != null) th.m_CurrentThirst = Math.Min(th.m_CurrentThirst, th.m_MaxThirst * 0.5f);
                var fa = GameManager.GetFatigueComponent();
                if (fa != null) fa.m_CurrentFatigue = Math.Min(fa.m_CurrentFatigue, fa.m_MaxFatigue * 0.5f);
                var hu = GameManager.GetHungerComponent();
                if (hu != null) hu.m_CurrentReserveCalories = Math.Max(hu.m_CurrentReserveCalories, hu.m_MaxReserveCalories * 0.5f);
            }
            catch (Exception e) { MelonLogger.Warning("[смерть] показатели: " + e.Message); }

            var cond = GameManager.GetConditionComponent();
            if (cond != null) cond.m_CurrentHP = cond.m_MaxHP * Math.Clamp(_rules.HealthPercent, (byte)1, (byte)100) / 100f;
        }

        private static void SkillLoss(int percent)
        {
            if (percent <= 0) return;
            try
            {
                var sm = GameManager.GetSkillsManager();
                int n = sm.GetNumSkills();
                for (int i = 0; i < n; i++)
                {
                    var s = sm.GetSkillFromIndex(i);
                    if (s == null) continue;
                    int tier = s.GetCurrentTierNumber();
                    int lose = (int)Math.Round(tier * percent / 100.0, MidpointRounding.AwayFromZero);
                    if (lose <= 0) continue;
                    s.SetTier(Math.Max(0, tier - lose), SkillsManager.PointAssignmentMode.AssignInAnyMode);
                }
                MelonLogger.Msg($"[смерть] навыки: -{percent}%");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[смерть] навыки: " + e.Message);
            }
        }

        private static void GiveStarterKit()
        {
            try
            {
                var config = ExperienceModeManager.GetCurrentGameModeConfig();
                var pm = GameManager.GetPlayerManagerComponent();
                var kit = config?.StartGear;
                if (kit != null && pm != null) kit.AddStartingGearToPlayer(pm, null);
                else pm?.m_StartGear?.AddAllToInventory();
                MelonLogger.Msg("[смерть] выдан стартовый набор");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[смерть] стартовый набор: " + e.Message);
            }
        }

        // 40 с после возврата камеры пишем, что с ней происходит (жалоба: «камера ломается и чинится по кругу»).
        private static float _camWatchUntil, _camNext;
        private static string _camLast = "";

        private static void CamWatch()
        {
            float now = Time.unscaledTime;
            if (now > _camWatchUntil || now < _camNext) return;
            _camNext = now + 0.5f;
            try
            {
                var st = GameManager.GetPlayerStruggleComponent();
                var pm = GameManager.GetPlayerManagerComponent();
                var cam = GameManager.GetMainCamera();
                var vp = GameManager.GetVpFPSCamera();
                float roll = cam != null ? Mathf.DeltaAngle(0f, cam.transform.localEulerAngles.z) : 0f;
                string e = cam != null ? $"{Mathf.Round(roll / 10f) * 10f:0}°" : "?";   // крен: у целой камеры 0
                bool fighting = st != null && (st.m_Active || st.InStruggle());
                string state = $"схватка {(st != null && st.m_Active ? "да" : "нет")}/{(st != null && st.InStruggle() ? "в ней" : "-")}, камера схватки {(st != null ? st.m_StruggleCameraMode.ToString() : "?")}, управление {(pm != null ? pm.GetControlMode().ToString() : "?")}, vp {(vp != null && vp.enabled ? "вкл" : "выкл")}, наклон {e}, руки {ArmState()}";
                // Камера всё ещё качается без схватки: ещё раз сбрасываем руки (не больше трёх раз за смерть).
                if (!fighting && Mathf.Abs(roll) > 15f && _armResets < 3 && Time.unscaledTime - _camWatchUntil + 40f > 1f) ResetArms();
                if (state == _camLast) return;
                _camLast = state;
                MelonLogger.Msg($"[смерть] камера через {40f - (_camWatchUntil - now):0.0} с: {state}");
            }
            catch { }
        }

        public static void Tick()
        {
            CamWatch();
            if (_step == Step.None) { if (_restoreNeverDie) RestoreNeverDieLater(); return; }
            // Сессия кончилась или игрок уже в главном меню: правила коопа дальше не применяем.
            if (Session.Mode == Mode.Off || InMainMenu()) { Reset(); return; }
            float now = Time.unscaledTime;
            switch (_step)
            {
                case Step.Downed:
                    // Пока друг лечит, таймер стоит.
                    if (Rescue.TreatedByName() != null || Drag.BeingDragged) _stepTime += now - _lastTick;
                    Drag.LateTick();
                    _lastTick = now;
                    Rescue.DownedTick(_need);
                    if (now - _stepTime > Math.Max((byte)5, _rules.DownedSeconds)) GiveUp();
                    return;

                case Step.Choosing:
                    // Место задал хост: даём посмотреть на экран смерти 6 секунд.
                    if (_autoPlace.HasValue)
                    {
                        if (now - _stepTime > 6f) Choose(_autoPlace.Value);
                        return;
                    }
                    // Не выбрал за 30 секунд — вариант по умолчанию.
                    if (now - _stepTime > 30f)
                    {
                        var allowed = AllowedPlaces();
                        Choose(allowed.Contains(_rules.DefaultPlace) ? _rules.DefaultPlace : allowed[0]);
                    }
                    return;

                case Step.Dropping:
                    // Вещи — в один рюкзак на месте смерти (Game/DeathPacks.cs); не вышло — на землю, как раньше.
                    if (!_packTried)
                    {
                        _packTried = true;
                        _pack = null;
                        if (DropQueue.Count > 0)
                        {
                            var at = GameManager.GetPlayerTransform();
                            var pos = GameApi.Scene() == _deathScene || at == null ? _deathPos : at.position;
                            float yaw = at != null ? at.eulerAngles.y : 0f;
                            try { _pack = DeathPacks.BeginOwn(pos, yaw); } catch (Exception e) { MelonLogger.Warning("[смерть] рюкзак: " + e.Message); }
                        }
                    }
                    for (int i = 0; i < (_pack != null ? 10 : 4) && DropQueue.Count > 0; i++)
                    {
                        var gi = DropQueue.Dequeue();
                        try
                        {
                            var pm = GameManager.GetPlayerManagerComponent();
                            if (gi.m_ClothingItem != null && pm != null && pm.IsWearingClothingItem(gi)) pm.TakeOffClothingItem(gi);
                            if (_pack != null)
                            {
                                GameManager.GetInventoryComponent().RemoveGear(gi.gameObject, true);
                                _pack.AddGear(gi);
                                continue;
                            }
                            int units = gi.m_StackableItem != null ? Math.Max(1, gi.m_StackableItem.m_Units) : 1;
                            gi.Drop(units, false, true, true);
                        }
                        catch (Exception e) { MelonLogger.Warning("[смерть] выброс: " + e.Message); }
                    }
                    if (DropQueue.Count > 0) return;
                    if (_pack != null) { try { DeathPacks.FinishOwn(); } catch (Exception e) { MelonLogger.Warning("[смерть] рюкзак: " + e.Message); } }
                    _pack = null;
                    _packTried = false;
                    if (_rules.StarterKit) GiveStarterKit();
                    _step = Step.Moving;
                    _stepTime = now;
                    _sceneRequested = null;
                    StartMove();
                    return;

                case Step.Moving:
                    TickMove(now);
                    return;

                case Step.Grace:
                    if (now - _stepTime > 5f) Finish();
                    return;
            }
        }

        private static void StartMove()
        {
            switch (_place)
            {
                case Place.NearPlayer:
                    var target = Session.Players.Values.OrderBy(p => p.Scene == _deathScene ? 0 : 1).FirstOrDefault(p => p.InWorld && p.Scene.Length > 0);
                    if (target == null) goto default;
                    Session.GoToPlayer(target.Id);
                    ToGrace();
                    return;

                case Place.DeathSpot:
                    if (GameApi.Scene() == _deathScene && ItemSync.SceneKey() == _deathKey)
                    {
                        GameApi.Teleport(_deathPos.x, _deathPos.y, _deathPos.z);
                        ToGrace();
                        return;
                    }
                    _toDeathSpot = true;
                    LoadThenSpawn(_deathScene, _deathKey);
                    return;

                case Place.Anywhere:
                    var others = Regions.Where(r => r != GameApi.Scene()).ToArray();
                    LoadThenSpawn(others[new System.Random().Next(others.Length)]);
                    return;

                default:
                    // В этой же локации. Если умер в помещении — в последнюю уличную локацию.
                    string scene = GameApi.Scene();
                    if (GameManager.IsOutDoorsScene(scene))
                    {
                        SpawnPoint();
                        ToGrace();
                        return;
                    }
                    string outdoor = GameManager.m_SceneTransitionData?.m_LastOutdoorScene;
                    if (string.IsNullOrEmpty(outdoor)) { ToGrace(); return; }
                    LoadThenSpawn(outdoor);
                    return;
            }
        }

        private static bool _toDeathSpot;

        private static void LoadThenSpawn(string scene, string key = null)
        {
            _sceneRequested = scene;
            MelonLogger.Msg("[смерть] переход в " + scene);
            GameApi.LoadScene(scene, key);
        }

        // Точка появления новой кампании в текущей локации; если их нет — случайная точка рядом.
        private static void SpawnPoint()
        {
            try
            {
                var sp = PlayerManager.PickRandomSpawnPoint();
                if (sp != null)
                {
                    GameManager.GetPlayerManagerComponent().TeleportPlayer(sp.transform.position, sp.transform.rotation);
                    MelonLogger.Msg("[смерть] точка появления " + sp.name + " " + sp.transform.position);
                    return;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[смерть] точка появления: " + e.Message);
            }
            var t = GameManager.GetPlayerTransform();
            RandomLocal(t != null ? t.position : _deathPos);
        }

        private static void TickMove(float now)
        {
            // Ждём загрузки новой сцены, потом ставим в точку появления.
            if (_sceneRequested == null) { ToGrace(); return; }
            if (now - _stepTime > 120f) { ToGrace(); return; }
            if (!GameApi.InGame() || GameApi.Scene() != _sceneRequested) return;
            if (now - _stepTime < 4f) return;
            if (_toDeathSpot) GameApi.Teleport(_deathPos.x, _deathPos.y, _deathPos.z);
            else SpawnPoint();
            _toDeathSpot = false;
            ToGrace();
        }

        private static void ToGrace()
        {
            try { MelonLogger.Msg($"[смерть] время: было день {_deathDay} {_deathTod:0.000}, стало день {GameApi.DayNumber()} {GameApi.TimeOfDay():0.000}"); } catch { }
            _step = Step.Grace;
            _stepTime = Time.unscaledTime;
            GameApi.HudMessage("Ты снова в игре");
            UnFade();
        }

        // Игра при смерти затемняет экран (CameraFade) и сама его не возвращает: смерть мы перехватили.
        private static void UnFade()
        {
            try
            {
                MelonLogger.Msg($"[смерть] затемнение экрана: {CameraFade.CurrentAlpha:0.00}");
                if (CameraFade.CurrentAlpha > 0.01f) CameraFade.FadeIn(1f, 0f, null);
            }
            catch (Exception e) { MelonLogger.Warning("[смерть] затемнение: " + e.Message); }
        }

        private static void Finish()
        {
            var cond = GameManager.GetConditionComponent();
            if (cond != null) cond.m_NeverDie = _neverDieBefore;
            _step = Step.None;
            UnFade();
        }

        private static bool InMainMenu()
        {
            try { return GameManager.IsMainMenuActive(); } catch { return false; }
        }

        // Сессия кончилась (Session.Stop) или мир сменился (выход из мира хоста, главное меню) посреди смерти.
        // Раньше таймеры шли дальше: в меню или в своей игре «ТЫ РАНЕН», «ВЫ ПОГИБЛИ», потери по правилам коопа
        // и загрузка локации прямо из меню. Ещё лежащего поднимаем, как друг: без коопа обычная смерть игры
        // удалила бы сохранение.
        public static void Reset()
        {
            if (_step == Step.None) return;
            var was = _step;
            _step = Step.None;
            MelonLogger.Msg("[смерть] сброс: сессия кончилась или мир сменился (" + was + ")");
            try { SetSpectate(false); } catch { }
            _spectate = false;
            _camSaved = false;
            if (Activity.Override == Activity.Downed || Activity.Override == Activity.Dead) Activity.Override = 0;
            DropQueue.Clear();
            if (_pack != null) { try { DeathPacks.FinishOwn(); } catch (Exception e) { MelonLogger.Warning("[смерть] рюкзак: " + e.Message); } }
            _pack = null;
            _packTried = false;
            _sceneRequested = null;
            _toDeathSpot = false;
            _autoPlace = null;
            if (!GameApi.InGame())
            {
                _inStruggle = false;
                _restoreNeverDie = true;
                _restoreAt = -1f;
                return;
            }
            if (was == Step.Downed || was == Step.Choosing)
            {
                Revive();
                try { ConsoleManager.CONSOLE_afflictions_cure(); } catch { }
                try
                {
                    var fr = GameManager.GetFreezingComponent();
                    if (fr != null) fr.m_CurrentFreezing = 0f;
                    var c = GameManager.GetConditionComponent();
                    if (c != null) c.m_CurrentHP = Math.Max(c.m_CurrentHP, c.m_MaxHP * 0.25f);
                }
                catch { }
                AfterStruggleDeath();
            }
            _inStruggle = false;
            try { var cond = GameManager.GetConditionComponent(); if (cond != null) cond.m_NeverDie = _neverDieBefore; } catch { }
            UnFade();
        }

        // m_NeverDie сохраняется в слот: если сброс был в меню или во время загрузки, снимаем его через пару
        // секунд после входа в игру (после того как игра восстановила сохранение).
        private static void RestoreNeverDieLater()
        {
            if (!GameApi.InGame()) { _restoreAt = -1f; return; }
            if (_restoreAt < 0f) { _restoreAt = Time.unscaledTime + 3f; return; }
            if (Time.unscaledTime < _restoreAt) return;
            _restoreNeverDie = false;
            _restoreAt = -1f;
            if (_neverDieBefore) return;
            try { var cond = GameManager.GetConditionComponent(); if (cond != null) cond.m_NeverDie = false; } catch { }
        }

        // Случайная точка на навигационной сетке (там, где могут ходить звери и игрок) недалеко от origin.
        private static void RandomLocal(Vector3 origin, float radius = 250f)
        {
            if (!Weather_IsOutdoors()) radius = Math.Min(radius, 15f);
            var rnd = new System.Random();
            for (int attempt = 0; attempt < 30; attempt++)
            {
                double a = rnd.NextDouble() * Math.PI * 2, d = radius * (0.3 + 0.7 * rnd.NextDouble());
                var p = new Vector3(origin.x + (float)(Math.Cos(a) * d), origin.y + 50f, origin.z + (float)(Math.Sin(a) * d));
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 80f, NavMesh.AllAreas))
                {
                    GameApi.Teleport(hit.position.x, hit.position.y, hit.position.z);
                    MelonLogger.Msg("[смерть] появился в " + hit.position);
                    return;
                }
            }
            MelonLogger.Msg("[смерть] не нашёл случайную точку, остаюсь на месте");
        }

        private static bool Weather_IsOutdoors()
        {
            try { var w = GameManager.GetWeatherComponent(); return w == null || !w.IsIndoorEnvironment(); } catch { return true; }
        }

        // ---------- Экран смерти ----------

        // Экран смерти в коопе (0.19.24): вид как у игрового экрана после смерти — крупный заголовок, причина красным,
        // места воскрешения карточками со значками на красном пятне. Нажатие на карточку выбирает место.
        // Размечено в точках 1280×720 и растянуто под экран (UI/Widgets.cs), тексты на двух языках.
        public static void DrawScreen()
        {
            if (Session.Mode == Mode.Off) return;
            if (_step != Step.Downed && _step != Step.Choosing) return;
            Widgets.Scaled();
            try
            {
                if (_step == Step.Downed) DrawDowned();
                else DrawChoosing();
            }
            finally { Widgets.Unscaled(); }
        }

        private static void DrawChoosing()
        {
            float sw = Widgets.SW, sh = Widgets.SH, cx = sw / 2f;
            var full = new Rect(0f, 0f, Screen.width, Screen.height);
            UI.Theme.Fill(full, new Color(0f, 0f, 0f, 0.55f));
            UI.Theme.Sprite(full, "vignetteFullscreen", new Color(0f, 0f, 0f, 0.74f));
            float top = sh / 2f - 230f;
            Widgets.Centered(new Rect(0f, top, sw, 60f), L.T("ВЫ ПОГИБЛИ", "YOU DIED"), 44f, UI.Theme.White);
            if (_cause.Length > 0) Widgets.Centered(new Rect(40f, top + 58f, sw - 80f, 30f), _cause, 18f, UI.Theme.Red);
            Widgets.Line(cx - 180f, top + 98f, 360f);

            var allowed = AllowedPlaces();
            int count = _autoPlace.HasValue ? 0 : allowed.Count;
            float y = top + 116f;
            if (_autoPlace.HasValue)
            {
                int left = Math.Max(0, 6 - (int)(Time.unscaledTime - _stepTime));
                Widgets.Centered(new Rect(0f, y, sw, 24f), L.T("КООП", "CO-OP"), 14f, UI.Theme.White);
                Widgets.Centered(new Rect(40f, y + 26f, sw - 80f, 26f),
                    L.T($"Воскреснешь {PlaceText(_autoPlace.Value)} через {left} с", $"You will come back {PlaceText(_autoPlace.Value)} in {left} s"), 16f, UI.Theme.Body);
                y += 70f;
            }
            else
            {
                int left = Math.Max(0, 30 - (int)(Time.unscaledTime - _stepTime));
                Widgets.Centered(new Rect(0f, y, sw, 24f), L.T("КООП · ГДЕ ВОСКРЕСНУТЬ?", "CO-OP · WHERE TO COME BACK?"), 14f, UI.Theme.White);
                Widgets.Centered(new Rect(40f, y + 22f, sw - 80f, 22f),
                    L.T($"Не выберешь за {left} с — {PlaceText(_rules.DefaultPlace)}", $"No choice in {left} s: {PlaceText(_rules.DefaultPlace)}"), 13f, UI.Theme.Caption);
                y += 56f;
                float cw = 180f, gap = 20f, total = count * cw + (count - 1) * gap;
                float x0 = cx - total / 2f;
                for (int i = 0; i < count; i++)
                {
                    var p = allowed[i];
                    if (Widgets.Card(new Rect(x0 + i * (cw + gap), y, cw, 196f), PlaceIcon(p), PlaceTitle(p), PlaceBody(p))) Choose(p);
                }
                y += 210f;
            }
            string loot = _rules.Loot == LootMode.Keep ? L.T("вещи останутся при тебе", "items stay with you")
                : _rules.Loot == LootMode.AtDeathSpot ? L.T("вещи останутся на месте смерти", "items stay at the place of death") : L.T("вещи пропадут", "items are lost");
            string terms = L.T("После воскрешения: здоровье ", "After coming back: health ") + _rules.HealthPercent + "%, " + loot +
                           (_rules.SkillLossPercent > 0 ? L.T(", навыки −", ", skills −") + _rules.SkillLossPercent + "%" : "") +
                           (_rules.StarterKit ? L.T(", стартовый набор", ", starter kit") : "");
            Widgets.Line(cx - 260f, y, 520f, 0.4f);
            Widgets.Centered(new Rect(40f, y + 10f, sw - 80f, 24f), terms, 13f, UI.Theme.Body);
            if (count > 0) Widgets.Centered(new Rect(40f, y + 40f, sw - 80f, 22f), L.T("Нажми на место, чтобы воскреснуть там", "Click a place to come back there"), 13f, UI.Theme.Caption);
            if (SpectateTarget() != null || _spectate)
            {
                var sr = new Rect(sw - 220f, sh - 60f, 190f, 36f);
                if (Widgets.Button(sr, _spectate ? L.T("Перестать смотреть", "Stop watching") : L.T("Смотреть на друга", "Watch a friend"), false, 14f, true)) SetSpectate(!_spectate);
            }
        }

        private static void DrawDowned()
        {
            float sw = Widgets.SW, sh = Widgets.SH;
            UI.Theme.Sprite(new Rect(0f, 0f, Screen.width, Screen.height), "vignetteFullscreen", new Color(0f, 0f, 0f, 0.6f));
            int left = Math.Max(0, Math.Max((byte)5, _rules.DownedSeconds) - (int)(Time.unscaledTime - _stepTime));
            float top = sh * 0.26f;
            Widgets.Centered(new Rect(0f, top, sw, 60f), L.T("ТЫ РАНЕН", "YOU ARE DOWN"), 44f, UI.Theme.White);
            string healer = Rescue.TreatedByName();
            string line = healer != null
                ? L.T($"{healer.Replace("<", "‹")} лечит тебя, таймер остановлен: {left} с", $"{healer.Replace("<", "‹")} is treating you, timer stopped: {left} s")
                : L.T($"Друг может подойти и поднять тебя: {left} с", $"A friend can come and pick you up: {left} s");
            Widgets.Centered(new Rect(40f, top + 58f, sw - 80f, 30f), line, 18f, healer != null ? UI.Theme.Good : UI.Theme.Red);
            Widgets.Line(sw / 2f - 180f, top + 98f, 360f);
            if (_rules.ReviveItems && _need != Rescue.Need.None && Rescue.NeedText(_need) is string nt)
                Widgets.Centered(new Rect(40f, top + 110f, sw - 80f, 26f), L.T("Понадобится: ", "Needed: ") + nt, 15f, UI.Theme.Body);
            float w = 214f;
            if (Widgets.Button(new Rect((sw - w) / 2f, sh - 120f, w, 38f), L.T("Сдаться", "Give up"))) GiveUp();
        }

        private static string PlaceText(Place p) =>
            p == Place.NearPlayer ? L.T("рядом с другим игроком", "next to another player") : p == Place.Local ? L.T("в точке старта этой локации", "at this location's start point") :
            p == Place.DeathSpot ? L.T("на месте смерти", "at the place of death") : L.T("в точке старта любой локации", "at any location's start point");

        private static string PlaceIcon(Place p) =>
            p == Place.NearPlayer ? "icoMap_location" : p == Place.Local ? "icoMap_campfireBurnt" : p == Place.DeathSpot ? "icoMap_grave" : "ico_map";

        private static string PlaceTitle(Place p) =>
            p == Place.NearPlayer ? L.T("Рядом с другом", "Near a friend") : p == Place.Local ? L.T("Старт локации", "Location start") :
            p == Place.DeathSpot ? L.T("Место смерти", "Place of death") : L.T("Любая локация", "Any location");

        private static string PlaceBody(Place p) =>
            p == Place.NearPlayer ? L.T("поднимешься около другого игрока", "get up next to another player") : p == Place.Local ? L.T("в точке старта этой локации", "at this location's start point") :
            p == Place.DeathSpot ? L.T("на том же месте, где смерть", "right where you died") : L.T("в точке старта случайной локации", "at a random location's start point");

    }

    // Сама смерть идёт по-игровому, подменяем только обработку после неё (без удаления сохранения).
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.HandlePlayerDeath))]
    internal static class Patch_HandlePlayerDeath
    {
        private static bool Prefix(string overrideCauseOfDeath)
        {
            string why = overrideCauseOfDeath;
            try { if (string.IsNullOrEmpty(why)) why = GameManager.GetConditionComponent()?.GetCauseOfDeathString(); } catch { }
            return !Death.OnDeath(why);
        }
    }

    // Кнопки игры на экране смерти: в журнал не уходим, «Обмануть смерть» = место по умолчанию.
    [HarmonyPatch(typeof(Panel_LifeAfterDeath), nameof(Panel_LifeAfterDeath.ContinueToLogPanel))]
    internal static class Patch_DeathContinue
    {
        // Переход в журнал игра может сделать и сама по таймеру, поэтому просто не пускаем:
        // выбор остаётся на полосе мода (там же таймер варианта по умолчанию).
        private static bool Prefix() => !Death.ScreenOpen;
    }

    [HarmonyPatch(typeof(Panel_LifeAfterDeath), nameof(Panel_LifeAfterDeath.OnConfirmChoices))]
    internal static class Patch_DeathConfirm
    {
        private static bool Prefix()
        {
            if (!Death.ScreenOpen) return true;
            Death.ChooseDefault();
            return false;
        }
    }

    // Кнопка выхода в главное меню в журнале: в кооп-режиме она воскрешает.
    [HarmonyPatch(typeof(Panel_Log), nameof(Panel_Log.OnQuitToMainMenu))]
    internal static class Patch_DeathQuit
    {
        private static bool Prefix()
        {
            if (!Death.ScreenOpen) return true;
            Death.ChooseDefault();
            return false;
        }
    }
}

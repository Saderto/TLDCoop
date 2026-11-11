using System;
using Il2Cpp;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;
using MelonLoader;
using UnityEngine;

namespace TLDTrainer
{
    internal static class Cheats
    {
        private static float _slowTimer;

        private static bool _invulnerableApplied;
        private static bool _ghostApplied;
        private static string _ghostChecked;
        private static bool _blizzardSuppressApplied;
        private static bool _gameSpeedApplied;
        private static bool _jamApplied;

        // Для множителя расхода: значения показателей в прошлом кадре.
        private static bool _haveLast;
        private static float _lastCalories, _lastThirst, _lastFatigue, _lastFreezing;

        // Исходные значения параметров, которые мы подменяем, чтобы вернуть их при выключении.
        private static IntPtr _pmPtr;
        private static float _origStaminaUsage;
        private static IntPtr _encPtr;
        private static ItemWeight _origCarry, _origCarryExhausted, _origNoSprint, _origNoWalk, _origLow, _origMed, _origHigh;
        private static bool _carryApplied;
        private static IntPtr _ctrlPtr;
        private static float _origAccel, _origMaxVel;
        private static bool _speedApplied;

        private static bool _freezeActive;
        private static float _frozenTime;

        public static void OnLeftGame()
        {
            _haveLast = false;
            _pmPtr = _encPtr = _ctrlPtr = IntPtr.Zero;
            _carryApplied = _speedApplied = false;
            _freezeActive = false;
            State.Set("fly", 0f);
            State.Set("freezetime", 0f);
            if (Fly.Active) Fly.Disable();
            if (_gameSpeedApplied)
            {
                Time.timeScale = 1f;
                _gameSpeedApplied = false;
            }
        }

        // Вызывается каждый кадр, пока игрок в игре.
        public static void Tick()
        {
            Safe("здоровье", GodMode);
            Safe("показатели", Needs);
            Safe("выносливость", Stamina);
            Safe("вес", Carry);
            Safe("невидимость", Stealth);
            Safe("скорость", SuperSpeed);
            Safe("скорость игры", GameSpeed);
            Safe("время", FreezeTime);
            Safe("полёт", Fly.Tick);

            _slowTimer -= Time.unscaledDeltaTime;
            if (_slowTimer > 0f) return;
            _slowTimer = 0.5f;
            Safe("инвентарь", Inventory);
            Safe("болезни", Afflictions);
            Safe("погода", NoBlizzard);
        }

        private static void GodMode()
        {
            var c = GameManager.GetConditionComponent();
            if (c == null) return;
            if (State.On("god"))
            {
                c.m_CurrentHP = c.m_MaxHP;
                c.m_Invulnerable = true;
                _invulnerableApplied = true;
            }
            else if (_invulnerableApplied)
            {
                c.m_Invulnerable = false;
                _invulnerableApplied = false;
            }
        }

        // Ползунок «уровень» в процентах → доля 0…1.
        private static float Level(string id) => Mathf.Clamp01(State.Get(id) / 100f);

        private static void Needs()
        {
            float m = State.Get("drain");
            bool scale = _haveLast && Math.Abs(m - 1f) > 0.001f;

            var h = GameManager.GetHungerComponent();
            if (h != null)
            {
                if (State.On("nohunger")) h.m_CurrentReserveCalories = h.m_MaxReserveCalories * Level("hungerlvl");
                else if (scale && h.m_CurrentReserveCalories < _lastCalories)
                    h.m_CurrentReserveCalories = _lastCalories + (h.m_CurrentReserveCalories - _lastCalories) * m;
                _lastCalories = h.m_CurrentReserveCalories;
            }

            var t = GameManager.GetThirstComponent();
            if (t != null)
            {
                if (State.On("nothirst")) t.m_CurrentThirst = t.m_MaxThirst * (1f - Level("thirstlvl"));
                else if (scale && t.m_CurrentThirst > _lastThirst)
                    t.m_CurrentThirst = Mathf.Min(t.m_MaxThirst, _lastThirst + (t.m_CurrentThirst - _lastThirst) * m);
                _lastThirst = t.m_CurrentThirst;
            }

            var f = GameManager.GetFatigueComponent();
            if (f != null)
            {
                if (State.On("nofatigue")) f.m_CurrentFatigue = f.m_MaxFatigue * (1f - Level("fatiguelvl"));
                else if (scale && f.m_CurrentFatigue > _lastFatigue)
                    f.m_CurrentFatigue = Mathf.Min(f.m_MaxFatigue, _lastFatigue + (f.m_CurrentFatigue - _lastFatigue) * m);
                _lastFatigue = f.m_CurrentFatigue;
            }

            var fr = GameManager.GetFreezingComponent();
            if (fr != null)
            {
                if (State.On("nofreeze")) fr.m_CurrentFreezing = fr.m_MaxFreezing * (1f - Level("freezelvl"));
                else if (scale && fr.m_CurrentFreezing > _lastFreezing)
                    fr.m_CurrentFreezing = Mathf.Min(fr.m_MaxFreezing, _lastFreezing + (fr.m_CurrentFreezing - _lastFreezing) * m);
                _lastFreezing = fr.m_CurrentFreezing;
            }

            _haveLast = true;
        }

        private static void Stamina()
        {
            var pm = GameManager.GetPlayerMovementComponent();
            if (pm == null) return;
            if (pm.Pointer != _pmPtr)
            {
                _pmPtr = pm.Pointer;
                _origStaminaUsage = pm.m_SprintStaminaUsagePerSecond;
            }
            pm.m_SprintStaminaUsagePerSecond = _origStaminaUsage * State.Get("staminamult");
            if (State.On("stamina")) pm.m_SprintStamina = pm.m_MaxSprintStamina;
        }

        private static void Carry()
        {
            var enc = GameManager.GetEncumberComponent();
            if (enc == null) return;
            if (enc.Pointer != _encPtr)
            {
                _encPtr = enc.Pointer;
                _origCarry = enc.m_MaxCarryCapacity;
                _origCarryExhausted = enc.m_MaxCarryCapacityWhenExhausted;
                _origNoSprint = enc.m_NoSprintCarryCapacity;
                _origNoWalk = enc.m_NoWalkCarryCapacity;
                _origLow = enc.m_EncumberLowThreshold;
                _origMed = enc.m_EncumberMedThreshold;
                _origHigh = enc.m_EncumberHighThreshold;
                _carryApplied = false;
            }

            float kg = State.Get("carry");
            if (kg > 0.5f)
            {
                var w = ItemWeight.FromKilograms(kg);
                enc.m_MaxCarryCapacity = w;
                enc.m_MaxCarryCapacityWhenExhausted = w;
                // Пороги «нельзя бежать», «нельзя идти» и степени перегруза в игре заданы отдельными числами (около 40 и 60 кг),
                // поэтому сдвигаем их на столько же, на сколько подняли предел.
                long d = w.m_Units - _origCarry.m_Units;
                ItemWeight Shift(ItemWeight o)
                {
                    o.m_Units += d;
                    return o;
                }
                enc.m_NoSprintCarryCapacity = Shift(_origNoSprint);
                enc.m_NoWalkCarryCapacity = Shift(_origNoWalk);
                enc.m_EncumberLowThreshold = Shift(_origLow);
                enc.m_EncumberMedThreshold = Shift(_origMed);
                enc.m_EncumberHighThreshold = Shift(_origHigh);
                _carryApplied = true;
            }
            else if (_carryApplied)
            {
                enc.m_MaxCarryCapacity = _origCarry;
                enc.m_MaxCarryCapacityWhenExhausted = _origCarryExhausted;
                enc.m_NoSprintCarryCapacity = _origNoSprint;
                enc.m_NoWalkCarryCapacity = _origNoWalk;
                enc.m_EncumberLowThreshold = _origLow;
                enc.m_EncumberMedThreshold = _origMed;
                enc.m_EncumberHighThreshold = _origHigh;
                _carryApplied = false;
            }
        }

        private static void Stealth()
        {
            var pm = GameManager.GetPlayerManagerComponent();
            if (pm == null) return;
            if (State.On("stealth"))
            {
                pm.m_Ghost = true;
                _ghostApplied = true;
            }
            else if (_ghostApplied)
            {
                pm.m_Ghost = false;
                _ghostApplied = false;
            }
            else
            {
                // 2.5.22: m_Ghost пишется в сохранение. Включил невидимость, сохранился, выключил в другой раз —
                // и она приезжала из сохранения навсегда (флаг _ghostApplied живёт только до выхода из игры).
                // Поэтому один раз после каждой загрузки сцены: невидимость в трейнере выключена, а в игре включена — снимаем.
                string key = pm.Pointer.ToString() + "|" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (key == _ghostChecked) return;
                _ghostChecked = key;
                if (pm.m_Ghost)
                {
                    pm.m_Ghost = false;
                    MelonLogger.Msg("[невидимость] выключена в трейнере, а пришла из сохранения — снял");
                }
            }
        }

        private static void SuperSpeed()
        {
            var player = GameManager.GetVpFPSPlayer();
            var ctrl = player != null ? player.Controller : null;
            if (ctrl == null) return;
            if (ctrl.Pointer != _ctrlPtr)
            {
                _ctrlPtr = ctrl.Pointer;
                _origAccel = ctrl.MotorAcceleration;
                _origMaxVel = ctrl.MotorVelocityMax;
                _speedApplied = false;
            }

            if (State.On("superspeed"))
            {
                float k = State.Get("speedmult");
                ctrl.MotorAcceleration = _origAccel * k;
                ctrl.MotorVelocityMax = _origMaxVel * k;
                _speedApplied = true;
            }
            else if (_speedApplied)
            {
                ctrl.MotorAcceleration = _origAccel;
                ctrl.MotorVelocityMax = _origMaxVel;
                _speedApplied = false;
            }
        }

        private static void GameSpeed()
        {
            float s = State.Get("gamespeed");
            bool custom = Math.Abs(s - 1f) > 0.01f;
            // Не трогаем паузу: в меню паузы игра сама ставит timeScale = 0.
            if (GameManager.m_IsPaused || Time.timeScale == 0f) return;
            if (custom)
            {
                Time.timeScale = s;
                _gameSpeedApplied = true;
            }
            else if (_gameSpeedApplied)
            {
                Time.timeScale = 1f;
                _gameSpeedApplied = false;
            }
        }

        private static void FreezeTime()
        {
            var tod = GameManager.GetTimeOfDayComponent();
            if (tod == null) return;
            if (!State.On("freezetime"))
            {
                _freezeActive = false;
                return;
            }
            if (!_freezeActive)
            {
                _frozenTime = tod.GetNormalizedTime();
                _freezeActive = true;
            }
            tod.SetNormalizedTime(_frozenTime, false);
        }

        private static void NoBlizzard()
        {
            if (State.On("noblizzard"))
            {
                WeatherTransition.m_SuppressBlizzards = true;
                _blizzardSuppressApplied = true;
                var w = GameManager.GetWeatherComponent();
                var wt = GameManager.GetWeatherTransitionComponent();
                if (w != null && wt != null && w.IsBlizzard()) wt.ActivateWeatherSetImmediate(WeatherStage.Clear);
            }
            else if (_blizzardSuppressApplied)
            {
                WeatherTransition.m_SuppressBlizzards = false;
                _blizzardSuppressApplied = false;
            }
        }

        private static void Afflictions()
        {
            if (State.On("noinfection"))
            {
                var inf = GameManager.GetInfectionComponent();
                if (inf != null && inf.HasInfection()) inf.Cure();
                var risk = GameManager.GetInfectionRiskComponent();
                if (risk != null && risk.HasInfectionRisk()) risk.Cure();
            }
            if (State.On("nowrist"))
            {
                var w = GameManager.GetSprainedWristComponent();
                if (w != null && w.HasSprainedWrist()) w.Cure();
            }
            if (State.On("noankle"))
            {
                var a = GameManager.GetSprainedAnkleComponent();
                if (a != null && a.HasSprainedAnkle()) a.Cure();
            }
            if (State.On("nopain"))
            {
                var p = GameManager.GetSprainPainComponent();
                if (p != null && p.HasSprainPain()) p.Cure();
            }
        }

        private static void Inventory()
        {
            bool noJam = State.On("nojam");
            if (noJam || _jamApplied)
            {
                GunItem.m_ForceNoJam = noJam;
                _jamApplied = noJam;
            }

            bool durability = State.On("durability");
            bool light = State.On("light");
            if (!durability && !light && !noJam) return;

            var inv = GameManager.GetInventoryComponent();
            if (inv == null || inv.m_Items == null) return;
            var items = inv.m_Items;
            for (int i = 0; i < items.Count; i++)
            {
                GearItem gi = items[i]?.m_GearItem;
                if (gi == null) continue;

                if (durability && gi.GetNormalizedCondition() < 1f) gi.SetNormalizedHP(1f, false);

                if (noJam && gi.m_GunItem != null && gi.m_GunItem.m_IsJammed) gi.m_GunItem.SetJammed(false);

                if (light)
                {
                    var lamp = gi.m_KeroseneLampItem;
                    if (lamp != null) lamp.m_CurrentFuelLiters = lamp.m_MaxFuel;
                    var torch = gi.m_TorchItem;
                    if (torch != null) torch.m_ElapsedBurnMinutes = 0f;
                    var flare = gi.m_FlareItem;
                    if (flare != null) flare.m_ElapsedBurnMinutes = 0f;
                }
            }
        }

        // ---------- Разовые действия ----------

        public static void RunAction(string id)
        {
            switch (id)
            {
                case "heal": Heal(); break;
                case "cure": ConsoleManager.CONSOLE_afflictions_cure(); break;
                case "repair": Repair(); break;
                case "maxskills": MaxSkills(); break;
                case "addweapons": AddWeapons(); break;
                case "quicksave": GameManager.SaveGameAndDisplayHUDMessage(); break;
                default: throw new ArgumentException("Неизвестное действие " + id);
            }
            var def = Shared.Features.Find(id);
            if (def != null) Toast.Show(def.Name + ": готово");
        }

        private static void Heal()
        {
            var c = GameManager.GetConditionComponent();
            if (c != null) c.m_CurrentHP = c.m_MaxHP;
            var h = GameManager.GetHungerComponent();
            if (h != null) h.m_CurrentReserveCalories = h.m_MaxReserveCalories;
            var t = GameManager.GetThirstComponent();
            if (t != null) t.m_CurrentThirst = 0f;
            var f = GameManager.GetFatigueComponent();
            if (f != null) f.m_CurrentFatigue = 0f;
            var fr = GameManager.GetFreezingComponent();
            if (fr != null) fr.m_CurrentFreezing = 0f;
            _haveLast = false;
        }

        private static void Repair()
        {
            var inv = GameManager.GetInventoryComponent();
            if (inv == null || inv.m_Items == null) return;
            for (int i = 0; i < inv.m_Items.Count; i++)
            {
                GearItem gi = inv.m_Items[i]?.m_GearItem;
                if (gi != null) gi.SetNormalizedHP(1f, false);
            }
        }

        private static void MaxSkills()
        {
            var sm = GameManager.GetSkillsManager();
            if (sm == null) return;
            foreach (SkillType st in Enum.GetValues(typeof(SkillType)))
            {
                if (st == SkillType.None) continue;
                sm.IncrementPointsAndNotify(st, 100000, SkillsManager.PointAssignmentMode.AssignInAnyMode);
            }
        }

        private static readonly (string Name, int Count)[] Weapons =
        {
            ("GEAR_Rifle", 1), ("GEAR_RifleAmmoSingle", 50),
            ("GEAR_Revolver", 1), ("GEAR_RevolverAmmoSingle", 50),
            ("GEAR_Bow", 1), ("GEAR_Arrow", 20),
            ("GEAR_FlareGun", 1), ("GEAR_FlareGunAmmoSingle", 10),
            ("GEAR_Knife", 1), ("GEAR_Hatchet", 1),
        };

        private static void AddWeapons()
        {
            foreach (var (name, count) in Weapons)
            {
                try
                {
                    Spawn(name, count);
                }
                catch (Exception e)
                {
                    MelonLogger.Warning($"Не удалось выдать {name}: {e.Message}");
                }
            }
        }

        public static void Spawn(string gearName, int count)
        {
            var pm = GameManager.GetPlayerManagerComponent();
            if (pm == null) throw new InvalidOperationException("Загрузите сохранение");
            // Предметы FoodPack мод создаёт сам, консольная выдача их не знает — выдаём так же, как сам мод.
            if (gearName.StartsWith("GEAR_Seagull_", StringComparison.OrdinalIgnoreCase) || gearName.StartsWith(Smokes.Prefix, StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 0; i < Math.Max(1, count); i++)
                    if (pm.InstantiateItemInPlayerInventory(gearName, 1, 1f, PlayerManager.InventoryInstantiateFlags.None) == null)
                        throw new InvalidOperationException($"Предмет {gearName} не найден — загрузите сохранение с установленным модом (FoodPack или SmokesBySeagull)");
                return;
            }
            var gi = pm.AddItemCONSOLE(gearName, Math.Max(1, count), 1f);
            if (gi == null) throw new InvalidOperationException($"Предмет {gearName} не найден");
        }

        public static void SetTime(int hour)
        {
            var tod = GameManager.GetTimeOfDayComponent();
            if (tod == null) return;
            float n = Mathf.Clamp(hour, 0, 23) / 24f;
            tod.SetNormalizedTime(n, true);
            _frozenTime = n;
        }

        public static void SetWeather(int stage)
        {
            var wt = GameManager.GetWeatherTransitionComponent();
            if (wt == null) return;
            wt.ActivateWeatherSetImmediate((WeatherStage)stage);
        }

        private static void Safe(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                // Ошибка одной функции не должна ломать остальные и спамить лог каждый кадр.
                if (Time.frameCount % 600 == 0) MelonLogger.Warning($"[{what}] {e.Message}");
            }
        }
    }
}

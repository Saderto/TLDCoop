using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace CrankLampByEMPEROR
{
    /// <summary>
    /// The crank, written anew (episode 5's CrankLampItem exists only in the WINTERMUTE executable).
    /// The lamp is a survival FlashlightItem with these rules, after episode 5:
    ///  - hold RMB with the lamp in hands to turn the crank: CrankSecondsToFull seconds fill an empty battery;
    ///  - a full battery gives FullChargeGameHours of light; below 10 % the lamp does not switch on;
    ///  - no high beam and no free charge from the aurora (it is a dynamo, not an aurora flashlight);
    ///  - below 20 % the light flickers.
    /// The charge is the game's own m_CurrentBatteryCharge, so the game saves it with the item.
    /// </summary>
    internal static class Crank
    {
        const float MinChargeToTurnOn = 0.10f;
        const float FlickerBelow = 0.20f;
        const float CrankDegreesPerSecond = 540f;   // one turn per arm loop (0.667 s)

        static FlashlightItem _held;
        static Transform _crankBone;
        static Animator _propAnim;
        static Quaternion _crankBase;
        static float _crankAngle;
        static float _nextBoneSearch;
        static bool _cranking;
        internal static bool Cranking => _cranking && _held != null;
        static float _crankedFor;
        static bool _loggedUnits;
        static bool _hadLamp;
        static float _nextTick;
        static bool _flickering;
        static float _flickerK = 1f, _nextFlicker;
        static readonly System.Collections.Generic.Dictionary<IntPtr, (Light l, float baseI)> _lights = new();

        static void SetLights(float k)
        {
            void Take(Component root, bool inactive)
            {
                if (root == null) return;
                foreach (var l in root.GetComponentsInChildren<Light>(inactive))
                    if (l != null && !_lights.ContainsKey(l.Pointer)) _lights[l.Pointer] = (l, l.intensity);
            }
            if (k < 1f) { Take(_held, true); if (_crankBone != null) Take(_crankBone.root, true); }
            foreach (var kv in _lights)
                if (kv.Value.l != null) kv.Value.l.intensity = kv.Value.baseI * k;
            if (k >= 1f) _lights.Clear();
        }

        internal static bool IsOurs(Component c)
        {
            if (c == null) return false;
            var n = c.gameObject.name;
            return n != null && n.IndexOf("CrankLamp", StringComparison.Ordinal) >= 0;
        }

        internal static void Patch(HarmonyLib.Harmony h)
        {
            var t = typeof(FlashlightItem);
            h.Patch(AccessTools.Method(t, nameof(FlashlightItem.DoRecharge)), prefix: new HarmonyMethod(typeof(Crank), nameof(NoAuroraRecharge)));
            h.Patch(AccessTools.Method(t, nameof(FlashlightItem.EnableLights)), prefix: new HarmonyMethod(typeof(Crank), nameof(NoHighBeam)));
            h.Patch(AccessTools.Method(t, nameof(FlashlightItem.TurnOn)), prefix: new HarmonyMethod(typeof(Crank), nameof(OwnTurnOn)), finalizer: new HarmonyMethod(typeof(Crank), nameof(AfterTurnOn)));
            h.Patch(AccessTools.Method(t, nameof(FlashlightItem.OnTurnOnFXCallback)), postfix: new HarmonyMethod(typeof(Crank), nameof(TurnOnFx)));
            h.Patch(AccessTools.Method(t, nameof(FlashlightItem.OnTurnOnCompleteCallback)), postfix: new HarmonyMethod(typeof(Crank), nameof(TurnOnDone)));
            h.Patch(AccessTools.Method(t, nameof(FlashlightItem.TurnOffImmediate)), prefix: new HarmonyMethod(typeof(Crank), nameof(OwnTurnOffNow)));
            foreach (var (pt, fn) in new[] { (typeof(uint), nameof(CapU)), (typeof(string), nameof(CapS)), (typeof(Il2CppAK.Wwise.Event), nameof(CapE)) })
            {
                try { h.Patch(AccessTools.Method(typeof(GameAudioManager), nameof(GameAudioManager.PlaySound), new[] { pt, typeof(GameObject) }), prefix: new HarmonyMethod(typeof(Crank), fn)); }
                catch (Exception e) { LampMod.Warn("capture " + pt.Name + ": " + e.Message); }
            }
            h.Patch(AccessTools.Method(t, nameof(FlashlightItem.Toggle)), prefix: new HarmonyMethod(typeof(Crank), nameof(OwnToggle)), finalizer: new HarmonyMethod(typeof(Crank), nameof(AfterToggle)));
            h.Patch(AccessTools.Method(t, nameof(FlashlightItem.Update)), prefix: new HarmonyMethod(typeof(Crank), nameof(BeforeUpdate)));
            h.Patch(AccessTools.Method(t, nameof(FlashlightItem.TurnOff)), prefix: new HarmonyMethod(typeof(Crank), nameof(OwnTurnOff)));
            h.Patch(AccessTools.Method(t, nameof(FlashlightItem.DoThreeDaysOfNightBonus)), prefix: new HarmonyMethod(typeof(Crank), nameof(NoAuroraRecharge)));
            foreach (var m in new[] { nameof(FlashlightItem.Update), nameof(FlashlightItem.Toggle), nameof(FlashlightItem.TurnOn), nameof(FlashlightItem.IsOn), nameof(FlashlightItem.IsLit), nameof(FlashlightItem.IsLow),
                nameof(FlashlightItem.OnTurnOnFXCallback), nameof(FlashlightItem.OnTurnOnCompleteCallback), nameof(FlashlightItem.UpdateAudio), nameof(FlashlightItem.ResumeLight) })
            {
                try
                {
                    h.Patch(AccessTools.Method(t, m, Type.EmptyTypes), prefix: new HarmonyMethod(typeof(Crank), nameof(ScopeIn)) { priority = Priority.First },
                        finalizer: new HarmonyMethod(typeof(Crank), nameof(ScopeOutFinal)));
                }
                catch (Exception e) { LampMod.Warn("scope " + m + ": " + e.Message); }
            }
            h.Patch(AccessTools.Method(t, nameof(FlashlightItem.ShowEquipItemPopup)), prefix: new HarmonyMethod(typeof(Crank), nameof(Prompt)));
            var wheel = typeof(Panel_ActionsRadial);
            foreach (var m in new[] { nameof(Panel_ActionsRadial.ShowPrimaryRadial), nameof(Panel_ActionsRadial.ShowLightSourceRadial) })
                h.Patch(AccessTools.Method(wheel, m), prefix: new HarmonyMethod(typeof(Crank), nameof(Wheel)));
            var am = typeof(AuroraManager);
            h.Patch(AccessTools.Method(am, nameof(AuroraManager.AuroraIsActive)), postfix: new HarmonyMethod(typeof(Crank), nameof(AuroraTrue)));
            h.Patch(AccessTools.Method(am, nameof(AuroraManager.IsFullyActive)), postfix: new HarmonyMethod(typeof(Crank), nameof(AuroraTrue)));
            h.Patch(AccessTools.Method(am, nameof(AuroraManager.GetNormalizedAlpha)), postfix: new HarmonyMethod(typeof(Crank), nameof(AuroraOne)));
            h.Patch(AccessTools.Method(t, nameof(FlashlightItem.GetNormalizedCharge)), postfix: new HarmonyMethod(typeof(Crank), nameof(ChargeResult)));
        }

        static bool NoAuroraRecharge(FlashlightItem __instance) => !IsOurs(__instance);

        static void NoHighBeam(FlashlightItem __instance, ref FlashlightItem.State state)
        {
            if (state == FlashlightItem.State.High && IsOurs(__instance)) state = FlashlightItem.State.Low;
        }

        // 11-11 (0.3.4 log: the lamp went off by itself about 2 s after every switch-on, and once came on by itself): the
        // game's flashlight code keeps deciding on its own (it is written for an aurora flashlight). For our lamp the mod
        // now owns the switch: the game's Update is not run, on/off happens only here, the charge is spent here.
        static bool _own;
        static void SetOn(FlashlightItem f, bool on, string why)
        {
            bool was = f.m_State != FlashlightItem.State.Off;
            f.m_State = on ? FlashlightItem.State.Low : FlashlightItem.State.Off;
            f.m_PreviousState = f.m_State;
            _own = true; _auroraScope++;
            try { f.EnableLights(f.m_State); } catch (Exception e) { LampMod.Log("EnableLights: " + e.Message); }
            finally { _auroraScope--; _own = false; }
            if (was != on)
            {
                LampMod.Log($"{(on ? "включён" : "выключен")} ({why}), заряд {Normalized(f) * 100f:0}% / switched");
                if (f.InPlayer()) Sound.Play(on ? Sound.TurnOn : Sound.TurnOff);
                // 11-11 (0.3.11 test: "a click on switch-off only"): the off click is the game's own (its Toggle plays
                // it); the on click was played by the game's Update, which the mod skips for this lamp.
                if (!on && f.InPlayer()) OffClick(f);
                if (on && f.InPlayer())
                {
                    _auroraScope++;
                    // 11-11 (0.3.12 test: "now the click is on switch-on only"): with the id of the on-sound remembered the
                    // game's switch-off stops playing its own click; before 0.3.12 that id was always 0 and the off
                    // click was there. So the id is cleared again right after the on-sound starts.
                    try
                    {
                        _capture = true;
                        try { f.PlayTurnOnAudio(); } finally { _capture = false; }
                        LampMod.Log($"звук включения игры: id {f.m_TurnOnAudioId}, поймано: {(_capName ?? (_capId != 0 ? _capId.ToString() : "ничего"))} / game's on sound, captured");
                        f.m_TurnOnAudioId = 0;
                    }
                    catch (Exception e) { LampMod.Log("PlayTurnOnAudio: " + e.Message); }
                    finally { _auroraScope--; }
                }
            }
        }

        // 11-11 (0.3.7/0.3.8 tests: "the left thumb no longer presses the switch, it did in the first version"): the
        // game's own Toggle plays the press. Asking PlayerAnimation for it ourselves threw (0.3.8), so the game's Toggle
        // runs again and the mod sets the lamp's state right after it; what the game's animation callbacks ask for in
        // the next 1.5 s is replaced by the state the player chose.
        static float _toggledAt = -10f; static bool _want;
        static bool _pendingOff; static float _pressedAt;
        static void OwnToggle(FlashlightItem __instance, out bool __state)
        {
            __state = __instance.m_State != FlashlightItem.State.Off;
            _inToggle = true;
        }
        // 11-11 (0.3.10 log): a switch-on less than 1.5 s after a switch-off was taken for a late callback of the game and
        // lost its press. Calls made while the game's Toggle itself runs are the player's press, never a late callback.
        static bool _inToggle;

        // 11-11 (user: "the lamp should wear out in use so that repairing it makes sense"): turning the crank and
        // burning both take condition off the item (GearItem.Degrade, hit points = percent for this item). A worn-out
        // lamp does not switch on and cannot be cranked until it is repaired.
        static float _wearAcc; static bool _wearLogged; static float _brokenMsgAt = -10f;
        static GearItem Gear(FlashlightItem f) { try { return f != null ? f.GetComponent<GearItem>() : null; } catch { return null; } }
        internal static bool Broken(FlashlightItem f)
        {
            try { var gi = Gear(f); return gi != null && (gi.IsWornOut() || gi.m_CurrentHP <= 0.01f); } catch { return false; }
        }
        static void BrokenMessage()
        {
            if (Time.unscaledTime - _brokenMsgAt < 2f) return;
            _brokenMsgAt = Time.unscaledTime;
            try { HUDMessage.AddMessage(Texts.Get("TLDCRANK_Broken"), false, false); } catch { }
        }
        static void Wear(FlashlightItem f, float percent)
        {
            if (percent <= 0f) return;
            _wearAcc += percent;
            if (_wearAcc < 0.1f) return;
            float hp = _wearAcc; _wearAcc = 0f;
            try
            {
                var gi = Gear(f);
                if (gi == null) return;
                float before = gi.m_CurrentHP;
                gi.Degrade(hp);
                if (!_wearLogged)
                {
                    _wearLogged = true;
                    LampMod.Log($"износ: {before:0.0} -> {gi.m_CurrentHP:0.0} HP, починка {(gi.m_Repairable != null ? "есть" : "НЕТ")}, разбор {(gi.m_Harvest != null ? "есть" : "нет")} / wear, repairable, harvest");
                }
                if (Broken(f) && f.m_State != FlashlightItem.State.Off) { SetOn(f, false, "сломан / worn out"); BrokenMessage(); }
            }
            catch (Exception e) { if (!_wearLogged) { _wearLogged = true; LampMod.Log("износ: " + e.Message + " / wear"); } }
        }
        // 11-11 (0.3.12/0.3.13 tests: the off click vanished once the mod started the on click): the mod plays the game's
        // off sound itself, once per switch-off.
        static float _offClickAt = -10f;
        static bool _capture; static uint _capId; static string _capName;
        static void CapU(uint __0) { if (_capture) { _capId = __0; _capName = null; LampMod.Log("поймано событие id " + __0 + " / captured"); } }
        static void CapS(string __0) { if (_capture) { _capName = __0; LampMod.Log("поймано событие " + __0 + " / captured"); } }
        static void CapE(Il2CppAK.Wwise.Event __0) { if (_capture && __0 != null) { try { _capId = __0.Id; _capName = null; LampMod.Log("поймано событие Event " + __0.Name + " " + __0.Id + " / captured"); } catch { } } }
        static void OffClick(FlashlightItem f)
        {
            if (Time.time - _offClickAt < 0.3f) return;
            _offClickAt = Time.time;
            _auroraScope++;
            // 0.3.14/0.3.15 tests: the lamp's own on/off audio events are empty (id 0) and PlayTurnOffAudio is silent.
            // The survival flashlight's switch-off sound is posted by its id instead.
            try
            {
                // 0.3.16 test: the flashlight's off sound is heard, but the user wants the same click as on switch-on.
                // That click comes from the game's PlayTurnOnAudio, so the same call is made on switch-off.
                // 0.3.17/0.3.18 tests: PlayTurnOnAudio is silent on switch-off whatever the state. So the event the game
                // posted during the on click is remembered (prefixes on GameAudioManager.PlaySound) and posted again here;
                // if nothing was caught, the survival flashlight's on sound is posted by its id.
                var p = GameManager.GetPlayerObject();
                uint id; string what;
                if (_capName != null) { id = GameAudioManager.PlaySound(_capName, p); what = _capName; }
                else if (_capId != 0) { id = GameAudioManager.PlaySound(_capId, p); what = _capId.ToString(); }
                else { id = GameAudioManager.PlaySound(Il2CppAK.EVENTS.PLAY_FLASHLIGHTON, p); what = "PLAY_FLASHLIGHTON"; }
                LampMod.Log($"щелчок выключения: {what}, id {id} / off click");
            }
            catch (Exception e) { LampMod.Log("off click: " + e.Message); }
            finally { _auroraScope--; }
        }
        static Exception AfterToggle(FlashlightItem __instance, bool __state, Exception __exception)
        {
            _inToggle = false;
            if (!IsOurs(__instance)) return __exception;
            if (__exception != null) LampMod.Log("Toggle игры: " + __exception.Message + " / game's Toggle threw");
            bool on = __state;
            // 11-11 (0.3.9 test: "the thumb is very late and presses only on switch-off"): the game switches a flashlight
            // off at the end of its press animation, the mod did it at once. The light now waits for the game's own
            // TurnOff (at most 1.2 s). On switch-on the game plays no press at all.
            if (on) { _want = false; _pendingOn = false; _pendingOff = true; _pressedAt = Time.time; }
            else if (_pendingOn) { }   // the game's TurnOn ran inside Toggle: the light waits for the press
            else if (Broken(__instance)) { _want = false; _pendingOff = false; SetOn(__instance, false, "сломан / worn out"); BrokenMessage(); }
            else if (Normalized(__instance) >= MinChargeToTurnOn) { _want = true; _pendingOff = false; SetOn(__instance, true, "кнопка / button"); }
            else { _want = false; _pendingOff = false; SetOn(__instance, false, "нет заряда / no charge"); HUDMessage.AddMessage(Texts.Get("TLDCRANK_Empty"), false, false); }
            _toggledAt = Time.time;
            return null;
        }
        static bool JustToggled(FlashlightItem f, string who)
        {
            if (Time.time - _toggledAt > 1.5f) return false;
            if (_pendingOff && !_want) { _pendingOff = false; who = $"кнопка, игра выключила через {Time.time - _pressedAt:0.00} с / button, game"; }
            SetOn(f, _want, who);
            return true;
        }

        // 11-11 (user: "earlier the thumb pressed on switch-on too"): the game's Toggle calls TurnOn, and TurnOn is what
        // plays the press on switch-on; the mod used to skip it. It runs again; the light comes on when the game's
        // OnTurnOnFXCallback fires (or 1.2 s after the press at the latest).
        static bool _pendingOn;
        static bool OwnTurnOn(FlashlightItem __instance)
        {
            if (!IsOurs(__instance)) return true;
            if (!_inToggle && JustToggled(__instance, "игра: TurnOn после кнопки / game after button")) return false;
            if (Normalized(__instance) < MinChargeToTurnOn) return false;
            if (Broken(__instance)) { BrokenMessage(); return false; }
            _want = true; _pendingOn = true; _pendingOff = false; _pressedAt = Time.time; _toggledAt = Time.time;
            return true;
        }
        static Exception AfterTurnOn(FlashlightItem __instance, Exception __exception)
        {
            if (!IsOurs(__instance) || !_pendingOn) return __exception;
            if (__exception != null) LampMod.Log("TurnOn игры: " + __exception.Message + " / game's TurnOn threw");
            LampMod.Log($"TurnOn игры отработал, состояние {__instance.m_State} / game's TurnOn done");
            return null;
        }
        static void TurnOnFx(FlashlightItem __instance)
        {
            if (!IsOurs(__instance)) return;
            LampMod.Log($"игра: OnTurnOnFXCallback через {Time.time - _pressedAt:0.00} с после нажатия / game fx callback");
            if (_pendingOn) { _pendingOn = false; SetOn(__instance, true, "кнопка, по анимации / button, with the press"); }
        }
        static void TurnOnDone(FlashlightItem __instance)
        {
            if (!IsOurs(__instance)) return;
            LampMod.Log($"игра: OnTurnOnCompleteCallback через {Time.time - _pressedAt:0.00} с, состояние {__instance.m_State} / game complete callback");
            if (_pendingOn) { _pendingOn = false; SetOn(__instance, true, "кнопка, конец анимации / button, press done"); }
            else if ((__instance.m_State != FlashlightItem.State.Off) != _want && Time.time - _toggledAt <= 2.5f) SetOn(__instance, _want, "состояние возвращено / state restored");
        }

        static bool OwnTurnOff(FlashlightItem __instance)
        {
            if (!IsOurs(__instance)) return true;
            if (_pendingOff) { _pendingOff = false; SetOn(__instance, false, $"кнопка, игра выключила через {Time.time - _pressedAt:0.00} с / button, game's TurnOff"); return false; }
            if (_emptyOff) SetOn(__instance, false, "разряжен / empty");
            else LampMod.Log("игра просит выключить (TurnOff): не выполняю / game asked to switch off: ignored");
            return false;
        }

        static bool OwnTurnOffNow(FlashlightItem __instance)
        {
            if (!IsOurs(__instance)) return true;
            if (JustToggled(__instance, "игра: TurnOffImmediate после кнопки / game after button")) return false;
            SetOn(__instance, false, "игра: TurnOffImmediate / game");
            return false;
        }

        static bool _emptyOff;
        static bool BeforeUpdate(FlashlightItem __instance)
        {
            if (!IsOurs(__instance)) return true;
            try
            {
                __instance.m_LowBeamDuration = Math.Max(0.05f, LampMod.FullChargeGameHours.Value);
                if (__instance.m_State == FlashlightItem.State.High) __instance.m_State = FlashlightItem.State.Low;
                if (__instance.m_State != FlashlightItem.State.Off)
                {
                    float hours = 0f;
                    try { hours = GameManager.GetTimeOfDayComponent().GetTODHours(Time.deltaTime); } catch { }
                    float raw = Mathf.Clamp01(Mathf.Clamp01(__instance.m_CurrentBatteryCharge) - hours / Math.Max(0.05f, LampMod.FullChargeGameHours.Value));
                    __instance.m_CurrentBatteryCharge = raw;
                    Wear(__instance, hours * Math.Max(0f, LampMod.WearPerLightHour.Value));
                    if (raw <= 0.0005f) { _emptyOff = true; try { SetOn(__instance, false, "разряжен / empty"); } finally { _emptyOff = false; } }
                }
            }
            catch { }
            try
            {
                // the game's Toggle and its animation callbacks may write the state directly: the player's choice wins
                if (_pendingOn)
                {
                    if (Time.time - _pressedAt > 1.2f) { _pendingOn = false; SetOn(__instance, true, "кнопка, игра не включила за 1.2 с / button, timeout"); }
                }
                else if (_pendingOff && !_want)
                {
                    if (__instance.m_State == FlashlightItem.State.Off)
                    {
                        // the game's Toggle wrote the state itself
                        _pendingOff = false;
                        LampMod.Log($"выключен (кнопка, игра выключила через {Time.time - _pressedAt:0.00} с), заряд {Normalized(__instance) * 100f:0}% / switched");
                        SetOn(__instance, false, "");
                        OffClick(__instance);
                    }
                    else if (Time.time - _pressedAt > 1.2f) { _pendingOff = false; SetOn(__instance, false, "кнопка, игра не выключила за 1.2 с / button, timeout"); }
                }
                else if (Time.time - _toggledAt <= 1.5f && (__instance.m_State != FlashlightItem.State.Off) != _want)
                    SetOn(__instance, _want, "состояние возвращено после кнопки / state restored after button");
            }
            catch { }
            return false;
        }

        // the game's prompt for a flashlight says "high beam" on RMB; ours says what RMB really does
        static bool Prompt(FlashlightItem __instance)
        {
            if (!IsOurs(__instance)) return true;
            try
            {
                var hud = InterfaceManager.GetPanel<Panel_HUD>();
                if (hud == null || hud.m_EquipItemPopup == null) return true;
                hud.m_EquipItemPopup.ShowGenericPopupWithDefaultActions(Texts.Get("TLDCRANK_PromptToggle"), Texts.Get("TLDCRANK_PromptCrank"));
                return false;
            }
            catch { return true; }
        }

        // the light-source wheel shows the items named in its list; ours is added next to the flashlight
        static void Wheel(Panel_ActionsRadial __instance)
        {
            try
            {
                var a = __instance.m_LightSourceRadialOrder;
                if (a == null) return;
                foreach (var n in a) if (n == Assets.Gear) return;
                var list = new System.Collections.Generic.List<string>();
                foreach (var n in a) { list.Add(n); if (n == "GEAR_Flashlight") list.Add(Assets.Gear); }
                if (!list.Contains(Assets.Gear)) list.Add(Assets.Gear);
                var b = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray(list.Count);
                for (int i = 0; i < list.Count; i++) b[i] = list[i];
                __instance.m_LightSourceRadialOrder = b;
                LampMod.Log("круг выбора: источники света = " + string.Join(", ", list) + " / light wheel");
            }
            catch (Exception e) { LampMod.Warn("wheel: " + e.Message); }
        }

        static bool InPlayer(this FlashlightItem f)
        {
            try { var gi = f.GetComponent<GearItem>(); return gi != null && gi.m_InPlayerInventory; } catch { return false; }
        }

        // 11-11 (log of 0.2.0: "raw 1, normalized 0"): the game's GetNormalizedCharge gives 0 when there is no aurora, the
        // real charge is m_CurrentBatteryCharge (0..1). We read and write that one.
        internal static float Normalized(FlashlightItem f)
        {
            try { return Mathf.Clamp01(f.m_CurrentBatteryCharge); } catch { return 0f; }
        }

        static void AddCharge(FlashlightItem f, float addNormalized)
        {
            f.m_CurrentBatteryCharge = Mathf.Clamp01(Mathf.Clamp01(f.m_CurrentBatteryCharge) + addNormalized);
        }

        // The survival flashlight works only under an aurora. For our lamp the aurora "is there" while its own code runs.
        static int _auroraScope;
        static void ScopeIn(FlashlightItem __instance, ref bool __state) { __state = IsOurs(__instance); if (__state) _auroraScope++; }
        static void ScopeOutFinal(bool __state) { if (__state && _auroraScope > 0) _auroraScope--; }
        static void AuroraTrue(ref bool __result) { if (_auroraScope > 0) __result = true; }
        static void AuroraOne(ref float __result) { if (_auroraScope > 0) __result = 1f; }
        static void ChargeResult(FlashlightItem __instance, ref float __result) { if (IsOurs(__instance)) __result = Normalized(__instance); }

        static FlashlightItem HeldLamp()
        {
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                var gi = pm != null ? pm.m_ItemInHands : null;
                if (gi == null || !IsOurs(gi)) return null;
                return gi.m_FlashlightItem ?? gi.GetComponent<FlashlightItem>();
            }
            catch { return null; }
        }

        internal static void Tick()
        {
            var f = HeldLamp();
            if (f != _held)
            {
                _held = f;
                if (_flickering) { try { SetLights(1f); } catch { } _flickering = false; }
                if (f == null) CameraLight(false);
                _lights.Clear();
                if (f == null && _hadLamp) Sound.Play(Sound.Unequip);
                _hadLamp = f != null;
                _crankBone = null; _propAnim = null; _crankAngle = 0f; _cranking = false;
                if (f != null)
                {
                    Sound.Load();
                    Sound.Play(Sound.Equip);
                    HUDMessage.AddMessage(Texts.Get("TLDCRANK_Hint"), false, false);
                    LampMod.Log($"фонарь в руках, заряд {Normalized(f) * 100f:0}% / lamp in hands");
                }
            }
            Arms.Tick(f != null);
            if (f == null) return;

            bool busy;
            try { busy = InterfaceManager.IsOverlayActiveImmediate(); } catch { busy = false; }
            bool hold = !busy && Input.GetMouseButton(1);
            if (hold && Broken(f)) { hold = false; if (Input.GetMouseButtonDown(1)) BrokenMessage(); }
            if (hold != _cranking)
            {
                _cranking = hold;
                Arms.SetCranking(hold);
                if (hold) { _crankedFor = 0f; _nextTick = 0.7f; if (!Arms.Active) _nextTick = Sound.CrankStartSound(); }
                if (!hold) { bool on = false; try { on = f.IsOn(); } catch { } LampMod.Log($"крутил {_crankedFor:0.0} с, заряд {Normalized(f) * 100f:0}%, горит {on}, состояние {f.m_State} / cranked"); }
            }
            if (hold)
            {
                float dt = Time.deltaTime;
                _crankedFor += dt;
                if (!Arms.Active && _crankedFor >= _nextTick) { _nextTick = _crankedFor + Sound.CrankTick(); }
                AddCharge(f, dt / Math.Max(1f, LampMod.CrankSecondsToFull.Value));
                Wear(f, dt / 60f * Math.Max(0f, LampMod.WearPerCrankMinute.Value));
                _crankAngle = (_crankAngle + CrankDegreesPerSecond * dt) % 360f;
            }

            // the light runs out: switch off like an empty flashlight
            try
            {
                if (f.m_State != FlashlightItem.State.Off && Normalized(f) <= 0.001f) SetOn(f, false, "разряжен / empty");
            }
            catch { }
        }

        // 11-11 (0.3.1: state Low, IsOn true, no light): the lamp has no high beam, and the bundle pointed the game's "high"
        // slots at the low-beam objects (item) or left them empty (hands). The game switches low on and high off, so the same
        // object went off again, or the hands' update stopped on the empty slot. Each "high" slot now gets its own idle object.
        static IntPtr _fixedItem, _fixedHands;
        static FirstPersonFlashlight _fp;
        static bool _lastLit; static float _nextLightLog, _nextParentLog;

        static Light Dummy(Transform parent, string name, out GameObject go)
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<Light>();
            l.intensity = 0f; l.range = 0.01f; l.enabled = false;
            return l;
        }

        static void FixHighSlots(FlashlightItem f)
        {
            try
            {
                if (f.Pointer != _fixedItem)
                {
                    _fixedItem = f.Pointer;
                    var a = Dummy(f.transform, "TLDCrankLamp_NoHighBeam", out var go);
                    go.SetActive(false);
                    f.m_FxObjectHigh = go; f.m_LightIndoorHigh = a; f.m_LightOutdoorHigh = a;
                    LampMod.Log($"свет: предмет, low {(f.m_FxObjectLow != null ? f.m_FxObjectLow.name : "нет")}, indoor {(f.m_LightIndoor != null)}, outdoor {(f.m_LightOutdoor != null)} / item light slots fixed");
                }
                var pa = GameManager.GetPlayerAnimationComponent();
                var w = pa != null ? pa.m_EquippedFirstPersonWeaponLeftHand : null;
                var fp = w != null ? w.GetComponentInChildren<FirstPersonFlashlight>(true) : null;
                if (fp == null && _crankBone != null) fp = _crankBone.root.GetComponentInChildren<FirstPersonFlashlight>(true);
                _fp = fp;
                if (fp != null && fp.Pointer != _fixedHands)
                {
                    _fixedHands = fp.Pointer;
                    var a = Dummy(fp.transform, "TLDCrankLamp_NoHighBeam", out var go);
                    go.SetActive(false);
                    fp.m_HighFxGameObject = go; fp.m_LightIndoorsHigh = a; fp.m_LightOutdoorsHigh = a;
                    LampMod.Log($"свет: руки, low {(fp.m_LowFxGameObject != null ? fp.m_LowFxGameObject.name : "нет")}, indoor {(fp.m_LightIndoorsLow != null)}, outdoor {(fp.m_LightOutdoorsLow != null)} / hands light slots fixed");
                }
            }
            catch (Exception e) { LampMod.Warn("light slots: " + e.Message); }
        }

        // 11-11 (0.3.3/0.3.4: "луч есть, освещения нет"): the light that falls on the world is our own spot light on the
        // player's camera, a little down and to the left where the lamp is held; colour as in WINTERMUTE.
        static Light _camLight;
        static void CameraLight(bool on)
        {
            try
            {
                if (!on) { if (_camLight != null && _camLight.enabled) _camLight.enabled = false; return; }
                var cam = GameManager.GetMainCamera();
                if (cam == null) return;
                if (_camLight == null)
                {
                    var go = new GameObject("TLDCrankLamp_Light");
                    _camLight = go.AddComponent<Light>();
                    _camLight.type = LightType.Spot;
                    _camLight.color = new Color(0.72f, 0.81f, 0.92f, 1f);
                    _camLight.spotAngle = 75f; _camLight.range = 40f;
                    _camLight.shadows = LightShadows.Hard;
                    LampMod.Log("свет: свой прожектор создан / own spot light made");
                }
                var t = _camLight.transform;
                if (t.parent != cam.transform) { t.SetParent(cam.transform, false); t.localPosition = new Vector3(-0.12f, -0.15f, 0.25f); t.localRotation = Quaternion.identity; }
                _camLight.intensity = Mathf.Clamp(LampMod.LightIntensity.Value, 0.2f, 20f) * (_flickering ? _flickerK : 1f);
                if (!_camLight.enabled) _camLight.enabled = true;
                if (!_camLight.gameObject.activeSelf) _camLight.gameObject.SetActive(true);
            }
            catch (Exception e) { if (Time.unscaledTime >= _nextLightLog) { _nextLightLog = Time.unscaledTime + 5f; LampMod.Warn("own light: " + e.Message); } }
        }

        // the hands' light follows the lamp: if the game's own update leaves it dark while the lamp is on, we switch it
        static void KeepHandsLight(FlashlightItem f)
        {
            try
            {
                bool on = f.m_State != FlashlightItem.State.Off;
                CameraLight(on);
                var fp = _fp;
                if (fp == null) return;
                var low = fp.m_LowFxGameObject;
                bool was = low != null && low.activeInHierarchy;
                if (low != null && low.activeSelf != on) low.SetActive(on);
                // 11-11 (0.3.2 log: "объект света в руках был выключен" every time): something above LowFX is switched off by
                // the hands' other light script. While the lamp is on, every object from LowFX up to the hands' root stays on.
                if (on && low != null)
                {
                    string off = null;
                    for (var t = low.transform.parent; t != null && t != fp.transform; t = t.parent)
                        if (!t.gameObject.activeSelf) { off = t.name; t.gameObject.SetActive(true); }
                    if (off != null && Time.unscaledTime >= _nextParentLog) { _nextParentLog = Time.unscaledTime + 5f; LampMod.Log("свет: был выключен родитель " + off + " / parent was off"); }
                }
                // the hands' own two lamps gave a visible beam and no light on the world; the light comes from CameraLight
                if (fp.m_LightIndoorsLow != null && fp.m_LightIndoorsLow.enabled) fp.m_LightIndoorsLow.enabled = false;
                if (fp.m_LightOutdoorsLow != null && fp.m_LightOutdoorsLow.enabled) fp.m_LightOutdoorsLow.enabled = false;
                if ((on != _lastLit || (on && !was)) && Time.unscaledTime >= _nextLightLog)
                {
                    _nextLightLog = Time.unscaledTime + 2f; _lastLit = on;
                    LampMod.Log($"свет: горит {on}, объект света в руках был {(was ? "включён" : "выключен")}, indoor {(fp.m_LightIndoorsLow != null && fp.m_LightIndoorsLow.enabled)}, outdoor {(fp.m_LightOutdoorsLow != null && fp.m_LightOutdoorsLow.enabled)} / light state");
                }
            }
            catch (Exception e) { if (Time.unscaledTime >= _nextLightLog) { _nextLightLog = Time.unscaledTime + 5f; LampMod.Warn("hands light: " + e.Message); } }
        }

        internal static void LateTick()
        {
            if (_held == null) return;
            FixHighSlots(_held);
            KeepHandsLight(_held);
            if (_crankBone == null && Time.unscaledTime >= _nextBoneSearch)
            {
                _nextBoneSearch = Time.unscaledTime + 1f;
                foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>())
                {
                    if (t == null || t.name != "HandCrankLight:Crank_jnt" || !t.gameObject.activeInHierarchy) continue;
                    _crankBone = t; _crankBase = t.localRotation;
                    LampMod.Log("ручка найдена / crank bone found");
                    break;
                }
            }
            if (_crankBone != null)
            {
                if (_propAnim == null)
                {
                    // the lamp model's own Animator (Cine_HandCrankLight_Ep5): it is switched off, but its object is the
                    // root the lamp's clips are written for
                    try
                    {
                        for (var t = _crankBone.parent; t != null && _propAnim == null; t = t.parent)
                        {
                            var a = t.GetComponent<Animator>();
                            if (a != null && a.runtimeAnimatorController != null) { _propAnim = a; LampMod.Log("ручка: аниматор " + t.name + " / handle animator"); }
                        }
                    }
                    catch { }
                }
                // the lamp's own clip follows the arm clip; if it does not move the handle, the handle is turned by code:
                // one turn per arm loop, only while the hand is on it
                if (!Arms.SyncProp(_propAnim, _crankBone))
                {
                    float turn = Arms.Active ? (Arms.Phase == 2 ? Arms.LoopTurn * 360f : 0f) : _crankAngle;
                    _crankBone.localRotation = _crankBase * Quaternion.AngleAxis(turn + LampMod.HandleOffsetDegrees.Value, Vector3.right);
                }
            }

            // flicker below 20 %
            try
            {
                bool flicker = _held.m_State != FlashlightItem.State.Off && Normalized(_held) < FlickerBelow;
                if (flicker)
                {
                    if (Time.unscaledTime >= _nextFlicker)
                    {
                        _nextFlicker = Time.unscaledTime + UnityEngine.Random.Range(0.04f, 0.15f);
                        _flickerK = UnityEngine.Random.value < 0.2f ? UnityEngine.Random.Range(0.1f, 0.4f) : UnityEngine.Random.Range(0.7f, 1.05f);
                    }
                    SetLights(_flickerK);
                }
                else if (_flickering) SetLights(1f);
                _flickering = flicker;
            }
            catch { }
        }
    }
}

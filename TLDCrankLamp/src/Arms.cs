using System;
using Il2Cpp;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace TLDCrankLamp
{
    /// <summary>
    /// Arms turning the crank, with WINTERMUTE's own first-person clips (HandCrankLight_Crank_Start/Loop/Exit_FP,
    /// carried in our bundle). Survival's arm controller has no such states, so the clips play through a Playables
    /// output on the arms Animator that is faded in over the game's own animation while RMB is held, and out after
    /// the exit clip. The lamp's own animator gets WINTERMUTE's triggers (Trigger_CrankLampTurn_Start/End), so the
    /// handle turns in step with the hand. Sounds follow the clips' own event times (Start at 0.7 s, Loop each cycle).
    /// </summary>
    internal static class Arms
    {
        const string ClipStart = "HandCrankLight_Crank_Start_FP", ClipLoop = "HandCrankLight_Crank_Loop_FP", ClipExit = "HandCrankLight_Crank_Exit_FP";
        const float Fade = 0.15f;

        static AnimationClip _start, _loop, _exit;
        static bool _clipsTried;
        static PlayableGraph _graph;
        static bool _hasGraph;
        static Animator _arms;
        static PlayableOutputHandle _out;
        static PlayableHandle _hStart, _hLoop, _hExit;
        static int _phase;           // 0 off, 1 start, 2 loop, 3 exit
        static float _phaseTime, _weight, _nextLoopSound;
        static bool _want;
        internal static bool Active => _phase != 0;

        static unsafe class Native
        {
            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
            struct Span { public IntPtr begin; public int length; }
            [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
            delegate void CreateFn(Span* name, PlayableGraph* ret);
            [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
            delegate void SelfFn(PlayableGraph* self);
            static CreateFn _create; static SelfFn _play, _destroy;

            internal static PlayableGraph Create()
            {
                _create ??= Il2CppInterop.Runtime.IL2CPP.ResolveICall<CreateFn>("UnityEngine.Playables.PlayableGraph::Create_Injected");
                Span name = default; PlayableGraph g = default;
                _create(&name, &g);
                return g;
            }
            internal static void Play(PlayableGraph g)
            {
                _play ??= Il2CppInterop.Runtime.IL2CPP.ResolveICall<SelfFn>("UnityEngine.Playables.PlayableGraph::Play");
                _play(&g);
            }
            internal static void Destroy(PlayableGraph g)
            {
                _destroy ??= Il2CppInterop.Runtime.IL2CPP.ResolveICall<SelfFn>("UnityEngine.Playables.PlayableGraph::Destroy");
                _destroy(&g);
            }
        }

        static AnimationClip LoadClip(string name)
        {
            try { return Addressables.LoadAssetAsync<AnimationClip>(name).WaitForCompletion(); }
            catch (Exception e) { LampMod.Warn("clip " + name + ": " + e.Message); return null; }
        }

        static bool Clips()
        {
            if (!_clipsTried)
            {
                _clipsTried = true;
                _start = LoadClip(ClipStart); _loop = LoadClip(ClipLoop); _exit = LoadClip(ClipExit);
                LampMod.Log($"анимации рук / arm clips: {(_start != null)} {(_loop != null)} {(_exit != null)}");
            }
            return _start != null && _loop != null && _exit != null;
        }

        static bool Ensure()
        {
            PlayerAnimation pa = null;
            try { pa = GameManager.GetPlayerAnimationComponent(); } catch { }
            var arms = pa != null ? pa.m_Animator : null;
            if (arms == null || !Clips()) return false;
            if (_hasGraph && _arms == arms && _graph.IsValid()) return true;
            Destroy();
            string step = "";
            try
            {
                // 11-11: this game's wrappers for PlayableGraph.Create/Play/Destroy cannot run ("GetPinnableReference not found",
                // then "invalid program"): they are engine calls on a struct that the wrapper generator got wrong. We call the
                // engine functions themselves.
                step = "create";
                _graph = Native.Create();
                step = "output";
                var output = AnimationPlayableOutput.Create(_graph, "crank", arms);
                _out = output.GetHandle();
                step = "clips";
                var s = AnimationClipPlayable.Create(_graph, _start);
                var l = AnimationClipPlayable.Create(_graph, _loop);
                var x = AnimationClipPlayable.Create(_graph, _exit);
                l.SetOverrideLoopTime(true); l.SetLoopTime(true);
                s.SetApplyFootIK(false); l.SetApplyFootIK(false); x.SetApplyFootIK(false);
                _hStart = s.GetHandle(); _hLoop = l.GetHandle(); _hExit = x.GetHandle();
                step = "source";
                _out.SetSourcePlayable(_hStart, 0);
                _out.SetWeight(0f);
                step = "play";
                Native.Play(_graph);
                _arms = arms; _hasGraph = true;
                LampMod.Log("руки: граф создан / arm graph ready");
                return true;
            }
            catch (Exception e) { LampMod.Warn("arm graph (" + step + "): " + e.Message); Destroy(); return false; }
        }

        static void Destroy()
        {
            if (_hasGraph) { try { if (_graph.IsValid()) Native.Destroy(_graph); } catch { } }
            _hasGraph = false; _arms = null; _phase = 0; _weight = 0f;
        }

        static void Go(int phase, PlayableHandle h)
        {
            _phase = phase; _phaseTime = 0f;
            try { h.SetTime(0); h.Play(); _out.SetSourcePlayable(h, 0); } catch { }
        }

        static void PropTrigger(string trig)
        {
            try
            {
                var pa = GameManager.GetPlayerAnimationComponent();
                var w = pa != null ? pa.m_EquippedFirstPersonWeaponLeftHand : null;
                if (w == null) return;
                foreach (var a in w.GetComponentsInChildren<Animator>(true))
                {
                    if (a == null || a.runtimeAnimatorController == null) continue;
                    a.SetFloat("CrankLampTurnSpeed", 1f);
                    a.SetTrigger(trig);
                }
            }
            catch { }
        }

        internal static void SetCranking(bool on)
        {
            _want = on;
            if (on)
            {
                if (!Ensure()) return;
                if (_phase == 0 || _phase == 3) { Go(1, _hStart); }
            }
            else if (_phase == 1 || _phase == 2)
            {
                Go(3, _hExit);
            }
        }

        /// <summary>Called each frame while the lamp is in hands (and once more when it leaves them).</summary>
        internal static void Tick(bool lampInHands)
        {
            if (!lampInHands) { if (_hasGraph) { try { _out.SetWeight(0f); } catch { } } _phase = 0; _weight = 0f; return; }
            if (!_hasGraph) return;
            float dt = Time.deltaTime;
            _phaseTime += dt;
            switch (_phase)
            {
                case 1:
                    if (_phaseTime >= _start.length)
                    {
                        float wait = Sound.CrankStartSound();
                        Go(2, _hLoop); _nextLoopSound = wait;
                    }
                    break;
                case 2:
                    if (_phaseTime >= _nextLoopSound) { _nextLoopSound = _phaseTime + Sound.CrankTick(); }
                    break;
                case 3:
                    if (_phaseTime >= _exit.length - Fade) _phase = 0;
                    break;
            }
            float target = _phase == 0 ? 0f : 1f;
            _weight = Mathf.MoveTowards(_weight, target, dt / Fade);
            try { _out.SetWeight(_weight); } catch { }
        }

        /// <summary>The lamp's handle follows the arm clip exactly. 0.3.4 played the matching state on the lamp's animator
        /// and the handle stood still, so the lamp's own clips (HandCrankLight_Crank_Start_Item / _Loop_Item, from its
        /// controller) are now put on the lamp directly at the arm clip's moment, after all animators have run.
        /// Returns false when that cannot be done; then the handle is turned by code.</summary>
        static AnimationClip _itemStart, _itemLoop; static IntPtr _itemFrom; static bool _sampleBroken, _sampleLogged;
        internal static int Phase => _phase;
        internal static float LoopTurn => _phase == 2 ? Mathf.Repeat(_phaseTime / Mathf.Max(0.01f, _loop.length), 1f) : 0f;
        static float _checkFrom = -1f; static Quaternion _checkRot; static float _checkMax; static bool _checkDone;
        internal static bool SyncProp(Animator prop, Transform bone)
        {
            if (prop == null || !_hasGraph || _sampleBroken) return false;
            try
            {
                if (prop.Pointer != _itemFrom)
                {
                    _itemFrom = prop.Pointer; _itemStart = _itemLoop = null; _checkDone = false; _checkFrom = -1f;
                    var c = prop.runtimeAnimatorController;
                    if (c != null)
                        foreach (var clip in c.animationClips)
                        {
                            if (clip == null) continue;
                            if (clip.name == "HandCrankLight_Crank_Start_Item") _itemStart = clip;
                            else if (clip.name == "HandCrankLight_Crank_Loop_Item") _itemLoop = clip;
                        }
                    LampMod.Log($"ручка: клипы фонаря start {(_itemStart != null)}, loop {(_itemLoop != null)}, объект {prop.gameObject.name} / lamp clips");
                }
                if (_itemLoop == null) return false;
                if (_phase == 1 && _itemStart != null) _itemStart.SampleAnimation(prop.gameObject, Mathf.Min(_phaseTime, _itemStart.length));
                else if (_phase == 2) _itemLoop.SampleAnimation(prop.gameObject, Mathf.Repeat(_phaseTime, Mathf.Max(0.01f, _itemLoop.length)));
                else { _checkFrom = -1f; return true; }
                // does the handle really move? (0.3.5: the clip was put on the hands' root and nothing moved)
                if (_phase == 2 && !_checkDone && bone != null)
                {
                    if (_checkFrom < 0f) { _checkFrom = Time.unscaledTime; _checkRot = bone.localRotation; _checkMax = 0f; }
                    _checkMax = Mathf.Max(_checkMax, Quaternion.Angle(_checkRot, bone.localRotation));
                    if (Time.unscaledTime - _checkFrom > 0.4f)
                    {
                        _checkDone = true;
                        LampMod.Log($"ручка: клип на {prop.gameObject.name} повернул её на {_checkMax:0} градусов / handle moved by the clip");
                        if (_checkMax < 2f) { _sampleBroken = true; return false; }
                    }
                }
                return true;
            }
            catch (Exception e) { _sampleBroken = true; LampMod.Warn("handle sync: " + e.Message); return false; }
        }

        internal static void Reset() => Destroy();
    }
}

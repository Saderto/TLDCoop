using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Стрела в звере — одна и та же у всех.
    //
    // Игра втыкает стрелу так (ArrowItem.HandleCollisionWithObject → ParentToObject): стрела становится дочерней
    // у капсулы зверя (капсулы повторяют кости через MatchTransform), глубина — m_EmbeddedDepth, зверь — m_EmbeddedInAi.
    // В сохранение идёт то же: путь от зверя до кости, позиция и угол на ней (ArrowSaveData). Раньше стрела была только
    // у стрелявшего (в его копии зверя), у остальных зверь был без стрелы.
    //
    // Теперь стрелявший рассылает (AnimalArrow), в какую кость (путь от модели зверя до кости, капсулу переводим в её
    // кость), где и под каким углом стрела, её глубину, вид стрелы и износ. У остальных в той же кости появляется такая же
    // настоящая стрела (её можно взять, она уходит в сохранение), только «спящая»: зверь у неё не записан, поэтому
    // сама она из зверя не выпадает. Сменилась модель зверя (копия стала настоящим зверем, тушей) — стрелу переносим
    // в ту же кость новой модели. Пришедший позже получает стрелы от хозяина зверей локации.
    //
    // Стрела достаётся одному. У кого стрела вышла из зверя — взял её с туши, четвертовал тушу, поднял тушку (кролик),
    // у стрелявшего она выпала сама, как в одиночной, — тот сразу говорит всем «стрелы нет» (у них она исчезает),
    // а если она легла на землю — рассылает её как выброшенную вещь. Тушу убрали не у нас (четвертовал другой, туша
    // истлела у не-хозяина зверей) — свою стрелу молча убираем, на землю её не роняем.
    public static partial class AnimalSync
    {
        private sealed class ArrowRec
        {
            public int Id, From;
            public long Gid;
            public string Path = "", Gear = "";
            public Vector3 Pos, RootPos, World;
            public Quaternion Rot = Quaternion.identity, RootRot = Quaternion.identity;
            public float Depth, Hp;
            public ArrowItem Item;
            public GearItem Gi;
            public IntPtr HostGo;
            public Transform HostRoot;
            public bool Copy, GoneSent, Detaching;
            public float LostAt = -1f, Since;
            public int Fails;
        }

        private const byte ArrowAdd = 1, ArrowGone = 2;

        private static readonly Dictionary<int, ArrowRec> Arrows = new();
        private static readonly Dictionary<IntPtr, int> ArrowByPtr = new();
        private static readonly HashSet<int> ArrowsGone = new();
        private static readonly Queue<int> ArrowsGoneOrder = new();
        private static readonly List<(ArrowItem ar, float due, float until)> ArrowsToTrack = new();
        private static readonly HashSet<int> ArrowAudience = new();
        private static int _arrowSeq;
        private static float _nextArrowPoll, _arrowResendAt = -1f;
        private static bool _makingArrow, _quarterLocal;
        private static int _arrowsOut, _arrowsIn, _arrowCopies, _arrowMoves, _arrowsLeft, _arrowsGoneIn, _arrowsHidden, _arrowsResent, _arrowFails, _arrowLogs, _flightModels;

        private static string ArrowStats => $"стрел в звере: разослал {_arrowsOut}, принял {_arrowsIn}, воткнул копий {_arrowCopies}, перенёс в новую модель {_arrowMoves}, вышли у меня {_arrowsLeft}, убрал по чужому {_arrowsGoneIn}, убрал с чужой туши {_arrowsHidden}, новичкам {_arrowsResent}, не вышло {_arrowFails}, убрал моделей полёта {_flightModels}, сейчас {Arrows.Count}";

        // ---------- Для других частей мода ----------

        // Стрела в звере (своя или копия): её ведёт AnimalSync, раскладка локации и сверка её не трогают.
        public static bool InAnimal(GearItem gi)
        {
            try
            {
                var ar = gi != null ? gi.m_ArrowItem : null;
                if (ar == null) return false;
                if (ArrowByPtr.ContainsKey(ar.Pointer)) return true;
                var p = gi.transform.parent;
                return p != null && p.GetComponentInParent<BaseAi>() != null;
            }
            catch { return false; }
        }

        // ---------- Своя стрела воткнулась ----------

        internal static void OnArrowHit(ArrowItem ar)
        {
            if (_makingArrow || ar == null || !Active) return;
            try
            {
                if (ArrowByPtr.ContainsKey(ar.Pointer)) return;
                float now = Time.unscaledTime;
                foreach (var t in ArrowsToTrack) if (t.ar != null && t.ar.Pointer == ar.Pointer) return;
                // Не сразу: игра могла тут же сломать стрелу (Break) или она отскочит.
                ArrowsToTrack.Add((ar, now + 0.15f, now + 10f));
            }
            catch { }
        }

        // Стрела загрузилась из сохранения внутри зверя (у хозяина зверей): тоже рассылаем.
        internal static void OnArrowLoaded(ArrowItem ar)
        {
            if (ar == null || !Active || !IsAuthority) return;
            OnArrowHit(ar);
        }

        private static void ScanArrows(Animal a)
        {
            if (a?.Go == null) return;
            try
            {
                foreach (var ar in Root(a).GetComponentsInChildren<ArrowItem>(true))
                    if (ar != null && !ArrowByPtr.ContainsKey(ar.Pointer)) OnArrowHit(ar);
            }
            catch { }
        }

        private static void TrackPending(float now)
        {
            for (int i = ArrowsToTrack.Count - 1; i >= 0; i--)
            {
                var (ar, due, until) = ArrowsToTrack[i];
                if (now < due) continue;
                bool done = true;
                try
                {
                    if (ar == null) { ArrowsToTrack.RemoveAt(i); continue; }
                    var r = TryTrack(ar);
                    if (r == 0 && now < until) { done = false; ArrowsToTrack[i] = (ar, now + 0.5f, until); }
                }
                catch (Exception e) { if (_arrowLogs++ < 20) MelonLogger.Warning("[звери] стрела: " + e.Message); }
                if (done) ArrowsToTrack.RemoveAt(i);
            }
        }

        // 1 — разослали, -1 — не наша / не в звере, 0 — зверя пока не знаем (попробуем ещё).
        private static int TryTrack(ArrowItem ar)
        {
            if (ArrowByPtr.ContainsKey(ar.Pointer)) return -1;
            var gi = ar.m_GearItem ?? ar.GetComponent<GearItem>();
            if (gi == null || !gi.gameObject.activeInHierarchy || gi.m_InPlayerInventory || gi.m_InsideContainer) return -1;
            var parent = ar.transform.parent;
            if (parent == null) return -1;   // не воткнулась (отскочила, упала)
            BaseAi ai = null;
            try { ai = ar.m_EmbeddedInAi; } catch { }
            if (ai == null) ai = parent.GetComponentInParent<BaseAi>();
            if (ai == null) return -1;
            if (!ByAi.TryGetValue(ai.Pointer, out long gid) || !Animals.TryGetValue(gid, out var a) || a.Go == null) return 0;
            var root = Root(a);
            if (!ar.transform.IsChildOf(root)) return -1;
            var rec = new ArrowRec
            {
                Id = (Session.LocalId << 24) | (++_arrowSeq & 0xFFFFFF), From = Session.LocalId, Gid = gid,
                Gear = ItemSync.PrefabName(gi), Item = ar, Gi = gi, HostGo = a.Go.Pointer, HostRoot = root, Since = Time.unscaledTime,
            };
            try { rec.Depth = ar.m_EmbeddedDepth; } catch { }
            try { rec.Hp = gi.CurrentHP; } catch { }
            PoseOf(rec, a);
            Arrows[rec.Id] = rec;
            ArrowByPtr[ar.Pointer] = rec.Id;
            SendArrowAdd(rec);
            _arrowsOut++;
            if (_arrowLogs++ < 40) MelonLogger.Msg($"[звери] стрела {rec.Gear} воткнулась в {a.Prefab}{(a.Real ? "" : " (копия, ведёт " + NameOf(a.Controller) + ")")}: кость «{(rec.Path.Length > 0 ? rec.Path.Substring(rec.Path.LastIndexOf('/') + 1) : "модель")}», глубина {rec.Depth:0.00} — разослал");
            return 1;
        }

        // Где стрела: в кости (капсулу — в её кость) и запасное — на самой модели зверя.
        private static void PoseOf(ArrowRec rec, Animal a)
        {
            var t = rec.Item.transform;
            var top = a.Go.transform;
            Transform bone = t.parent;
            try
            {
                var mt = bone != null ? bone.GetComponent<MatchTransform>() : null;
                if (mt != null)
                {
                    var target = mt.m_CachedTargetTransform;
                    if (target == null && mt.m_Target != null) target = mt.m_Target.transform;
                    if (target != null) bone = target;
                }
            }
            catch { }
            string path = PathUnder(top, bone);
            if (path == null)
            {
                // Кость не под моделью, а рядом (под родителем с навигацией): путь от верха зверя, с пометкой «^».
                var up = PathUnder(Root(a), bone);
                if (up != null) path = "^" + up;
                else { bone = top; path = ""; }
            }
            rec.Path = path;
            rec.Pos = bone.InverseTransformPoint(t.position);
            rec.Rot = Quaternion.Inverse(bone.rotation) * t.rotation;
            rec.RootPos = top.InverseTransformPoint(t.position);
            rec.RootRot = Quaternion.Inverse(top.rotation) * t.rotation;
            rec.World = t.position;
        }

        private static string PathUnder(Transform top, Transform t)
        {
            if (top == null || t == null) return null;
            var parts = new List<string>();
            for (var c = t; c != null; c = c.parent)
            {
                if (c.Pointer == top.Pointer) { parts.Reverse(); return string.Join("/", parts); }
                parts.Add(c.name);
            }
            return null;
        }

        private static Transform BoneOf(Animal a, string path, out bool exact)
        {
            var top = a.Go.transform;
            exact = true;
            if (string.IsNullOrEmpty(path)) return top;
            try
            {
                var t = path[0] == '^' ? Root(a).Find(path.Substring(1)) : top.Find(path);
                if (t != null) return t;
                string leaf = path.Substring(path.LastIndexOf('/') + 1);
                foreach (var c in Root(a).GetComponentsInChildren<Transform>(true))
                    if (c != null && c.name == leaf) return c;
            }
            catch { }
            exact = false;
            return top;
        }

        // ---------- Сообщения ----------

        private static void SendArrowAdd(ArrowRec rec)
        {
            var w = Msg.New(MsgType.AnimalArrow);
            w.Write(ArrowAdd);
            w.Write(_scene);
            w.Write((byte)Session.LocalId);
            w.Write(rec.Id);
            w.Write(rec.Gid);
            w.Write((byte)rec.From);
            w.Write(rec.Path ?? "");
            W3(w, rec.Pos); W4(w, rec.Rot);
            W3(w, rec.RootPos); W4(w, rec.RootRot);
            w.Write(rec.Depth);
            w.Write(rec.Gear ?? "");
            w.Write(rec.Hp);
            W3(w, rec.World);
            Session.SendGameEvent(Msg.Done(w));
        }

        private static void SendArrowGone(ArrowRec rec, string why)
        {
            rec.GoneSent = true;
            var w = Msg.New(MsgType.AnimalArrow);
            w.Write(ArrowGone);
            w.Write(_scene);
            w.Write((byte)Session.LocalId);
            w.Write(rec.Id);
            w.Write(rec.Gid);
            w.Write(why ?? "");
            Session.SendGameEvent(Msg.Done(w));
        }

        private static void W3(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        private static void W4(BinaryWriter w, Quaternion q) { w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w); }
        private static Vector3 R3(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        private static Quaternion R4(BinaryReader r) => new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        private static void ReceiveArrow(BinaryReader r)
        {
            byte kind = r.ReadByte();
            string scene = r.ReadString();
            int from = r.ReadByte();
            int id = r.ReadInt32();
            long gid = r.ReadInt64();
            if (from == Session.LocalId || OtherWorld(from) || scene != _scene) return;
            if (kind == ArrowAdd)
            {
                var rec = new ArrowRec { Id = id, Gid = gid, From = r.ReadByte(), Path = r.ReadString(), Copy = true, Since = Time.unscaledTime };
                rec.Pos = R3(r); rec.Rot = R4(r);
                rec.RootPos = R3(r); rec.RootRot = R4(r);
                rec.Depth = r.ReadSingle();
                rec.Gear = r.ReadString();
                rec.Hp = r.ReadSingle();
                rec.World = R3(r);
                if (ArrowsGone.Contains(id) || Arrows.ContainsKey(id)) return;
                Arrows[id] = rec;
                _arrowsIn++;
                if (Animals.TryGetValue(gid, out var a) && a.Go != null) AttachCopy(rec, a);
                return;
            }
            if (kind == ArrowGone)
            {
                string why = r.ReadString();
                NoteArrowGone(id);
                if (!Arrows.TryGetValue(id, out var rec)) return;
                Arrows.Remove(id);
                if (rec.Item != null) ArrowByPtr.Remove(rec.Item.Pointer);
                bool inside = false;
                try { inside = rec.Item != null && rec.HostRoot != null && rec.Item.transform.IsChildOf(rec.HostRoot); } catch { }
                if (inside) DestroyArrow(rec);
                _arrowsGoneIn++;
                if (_arrowLogs++ < 40) MelonLogger.Msg($"[звери] {NameOf(from)}: стрела {rec.Gear} вышла из зверя у него ({why}) — {(inside ? "у себя убрал" : "у меня её уже не было в звере")}");
            }
        }

        // Модель чужой стрелы в полёте (ArrowFlight) воткнулась в нашу копию зверя и торчит там ещё пару секунд —
        // рядом с настоящей копией было бы две стрелы. Убираем её, ArrowFlight это переносит (у него f.Go == null).
        private static void RemoveFlightModel(Animal a, Vector3 world)
        {
            try
            {
                foreach (var t in Root(a).GetComponentsInChildren<Transform>(true))
                    if (t != null && t.name == "TLDCoop_Arrow" && (t.position - world).sqrMagnitude < 2.5f * 2.5f)
                    {
                        UnityEngine.Object.Destroy(t.gameObject);
                        _flightModels++;
                    }
            }
            catch { }
        }

        private static void NoteArrowGone(int id)
        {
            if (!ArrowsGone.Add(id)) return;
            ArrowsGoneOrder.Enqueue(id);
            while (ArrowsGoneOrder.Count > 512) ArrowsGone.Remove(ArrowsGoneOrder.Dequeue());
        }

        private static void DestroyArrow(ArrowRec rec)
        {
            try
            {
                if (rec.Item != null) ArrowByPtr.Remove(rec.Item.Pointer);
                var go = rec.Gi != null ? rec.Gi.gameObject : rec.Item != null ? rec.Item.gameObject : null;
                if (go != null) { go.SetActive(false); UnityEngine.Object.Destroy(go); }
            }
            catch { }
            rec.Item = null;
            rec.Gi = null;
        }

        // ---------- Копия стрелы в звере ----------

        private static bool AttachCopy(ArrowRec rec, Animal a)
        {
            if (a.Go == null || string.IsNullOrEmpty(rec.Gear)) return false;
            if (!ItemSync.Creatable(rec.Gear)) { rec.Fails = 99; return false; }
            GearItem gi = null;
            bool q = ItemSync.Quiet;
            ItemSync.Quiet = true;
            _makingArrow = true;
            try { gi = GearItem.InstantiateGearItem(rec.Gear); }
            catch (Exception e) { if (_arrowLogs++ < 20) MelonLogger.Warning($"[звери] стрела {rec.Gear}: {e.Message}"); }
            finally { ItemSync.Quiet = q; _makingArrow = false; }
            if (gi == null) { rec.Fails++; _arrowFails++; return false; }
            var ar = gi.m_ArrowItem ?? gi.GetComponent<ArrowItem>();
            if (ar == null) { UnityEngine.Object.Destroy(gi.gameObject); rec.Fails = 99; _arrowFails++; return false; }
            try { gi.m_RolledSpawnChance = true; } catch { }
            try
            {
                var rb = gi.GetComponent<Rigidbody>();
                if (rb != null) Utils.SetIsKinematic(rb, true);
            }
            catch { }
            ar.m_EmbeddedInAi = null;   // «спящая»: сама из зверя не выпадает, выходит только по событию
            try { ar.m_FallOutTimer = 0f; } catch { }
            try { ar.m_EmbeddedDepth = rec.Depth; } catch { }
            try { if (rec.Hp > 0f) gi.CurrentHP = rec.Hp; } catch { }
            rec.Item = ar;
            rec.Gi = gi;
            ArrowByPtr[ar.Pointer] = rec.Id;
            Place(rec, a);
            rec.LostAt = -1f;
            _arrowCopies++;
            RemoveFlightModel(a, rec.World);
            if (_arrowLogs++ < 40) MelonLogger.Msg($"[звери] стрела {NameOf(rec.From)} в {a.Prefab}: воткнул такую же в «{(rec.Path.Length > 0 ? rec.Path.Substring(rec.Path.LastIndexOf('/') + 1) : "модель")}»");
            return true;
        }

        // Поставить стрелу (свою или копию) в ту же кость модели a.
        private static void Place(ArrowRec rec, Animal a)
        {
            var bone = BoneOf(a, rec.Path, out bool exact);
            Vector3 wp;
            Quaternion wr;
            if (exact) { wp = bone.TransformPoint(rec.Pos); wr = bone.rotation * rec.Rot; }
            else { var top = a.Go.transform; bone = top; wp = top.TransformPoint(rec.RootPos); wr = top.rotation * rec.RootRot; }
            var t = rec.Item.transform;
            t.SetPositionAndRotation(wp, wr);
            try { rec.Item.ParentToObject(bone); }
            catch { t.SetParent(bone, true); }
            t.SetPositionAndRotation(wp, wr);
            rec.HostGo = a.Go.Pointer;
            rec.HostRoot = Root(a);
        }

        // Adopt: у зверя новая модель (копия из модели стала зверем места появления, туша и т. п.). Старую уничтожат
        // в конце кадра вместе со стрелами — переносим их в ту же кость новой.
        private static void ArrowsToNewModel(Animal a, GameObject next)
        {
            if (a == null || next == null || a.Go == null || a.Go.Pointer == next.Pointer || Arrows.Count == 0) return;
            List<ArrowRec> moving = null;
            foreach (var rec in Arrows.Values)
                if (rec.Gid == a.Gid && rec.Item != null) (moving ??= new List<ArrowRec>()).Add(rec);
            if (moving == null) return;
            var old = a.Go;
            a.Go = next;   // Place смотрит в a.Go
            try
            {
                foreach (var rec in moving)
                {
                    try { if (rec.Item.transform.IsChildOf(rec.HostRoot)) { Place(rec, a); _arrowMoves++; } }
                    catch { }
                }
            }
            finally { a.Go = old; }
        }

        // ---------- Раз в полсекунды ----------

        private static void TickArrows(float now, string scene)
        {
            if (ArrowsToTrack.Count > 0) TrackPending(now);
            if (now < _nextArrowPoll) return;
            _nextArrowPoll = now + 0.5f;
            ResendArrowsForNewcomers(now, scene);
            if (Arrows.Count == 0) return;
            foreach (var rec in new List<ArrowRec>(Arrows.Values))
            {
                try { PollArrow(rec, now); }
                catch (Exception e) { if (_arrowLogs++ < 20) MelonLogger.Warning("[звери] стрела: " + e.Message); }
            }
        }

        private static void PollArrow(ArrowRec rec, float now)
        {
            Animals.TryGetValue(rec.Gid, out var a);
            bool animal = a != null && a.Go != null;
            if (rec.Item == null)
            {
                // Стрелы у нас нет: ещё не ставили, или её уничтожили вместе со старой моделью зверя — ставим в нынешнюю.
                bool replaced = animal && a.Go.Pointer != rec.HostGo;
                if (animal && (rec.HostGo == IntPtr.Zero || replaced) && rec.Fails < 3 && !rec.GoneSent)
                {
                    if (AttachCopy(rec, a)) return;
                }
                // Убрали у нас по чужому подбору (ItemSync) — ждём «стрелы нет»; зверя нет — ждём дольше и забываем.
                if (rec.LostAt < 0f) rec.LostAt = now;
                else if (now - rec.LostAt > (animal ? 5f : 60f)) ForgetArrow(rec, animal);
                return;
            }
            var gi = rec.Gi;
            if (rec.HostRoot == null && animal) rec.HostRoot = Root(a);
            bool inside = false;
            try { inside = rec.HostRoot != null && rec.Item.transform.IsChildOf(rec.HostRoot); } catch { }
            bool taken = gi != null && (gi.m_InPlayerInventory || gi.m_InsideContainer);
            if (!inside || taken)
            {
                Left(rec, taken ? "взял" : "выпала", !taken);
                return;
            }
            if (!animal)
            {
                // Зверя забыли (убран у всех, тушку подняли): стрела в выключенной модели — ждём «стрелы нет», потом убираем.
                if (rec.LostAt < 0f) rec.LostAt = now;
                else if (now - rec.LostAt > 30f) { DestroyArrow(rec); ForgetArrow(rec); }
                return;
            }
            rec.LostAt = -1f;
            if (a.Go.Pointer != rec.HostGo)
            {
                // Модель сменилась мимо Adopt: переставляем.
                Place(rec, a);
                _arrowMoves++;
            }
        }

        // Стрела вышла из зверя у нас: всем «стрелы нет», легла на землю — разослать как вещь.
        private static void Left(ArrowRec rec, string why, bool publish)
        {
            if (!rec.GoneSent) SendArrowGone(rec, why);
            ForgetArrow(rec);
            _arrowsLeft++;
            var gi = rec.Gi;
            bool world = false;
            try { world = publish && gi != null && WorldExtra.InWorld(gi); } catch { }
            if (world) WorldExtra.WatchDrop(gi);
            if (_arrowLogs++ < 40) MelonLogger.Msg($"[звери] стрела {rec.Gear} вышла из зверя у меня ({why}){(world ? ", лежит на земле — разошлю" : "")}");
        }

        // tomb — запомнить, что стрелы больше нет (повтор «воткнулась» от хозяина зверей не вернёт её).
        private static void ForgetArrow(ArrowRec rec, bool tomb = true)
        {
            Arrows.Remove(rec.Id);
            try { if (rec.Item != null) ArrowByPtr.Remove(rec.Item.Pointer); } catch { }
            if (tomb) NoteArrowGone(rec.Id);
        }

        internal static long CarcassGid(BodyHarvest bh) => bh != null ? GidOf(bh) : 0;
        internal static void QuarterLocal(bool on) => _quarterLocal = on;

        // Хозяин зверей: в локацию пришёл игрок — через 4 с (когда у него появятся звери) шлём ему все стрелы.
        private static void ResendArrowsForNewcomers(float now, string scene)
        {
            var here = HereNow();
            bool fresh = false;
            foreach (var p in here)
                if (p.id != Session.LocalId && ArrowAudience.Add(p.id)) fresh = true;
            ArrowAudience.RemoveWhere(id => !here.Exists(p => p.id == id));
            if (fresh && IsAuthority && Arrows.Count > 0) _arrowResendAt = now + 4f;
            if (_arrowResendAt < 0f || now < _arrowResendAt) return;
            _arrowResendAt = -1f;
            int n = 0;
            foreach (var rec in Arrows.Values)
            {
                if (rec.GoneSent) continue;
                SendArrowAdd(rec);
                n++;
            }
            _arrowsResent += n;
            if (n > 0) MelonLogger.Msg($"[звери] в локацию пришёл игрок, разослал стрелы в зверях: {n}");
        }

        private static void ClearArrows()
        {
            Arrows.Clear();
            ArrowByPtr.Clear();
            ArrowsGone.Clear();
            ArrowsGoneOrder.Clear();
            ArrowsToTrack.Clear();
            ArrowAudience.Clear();
            _arrowResendAt = -1f;
        }

        // ---------- Туша кончилась ----------

        private static List<ArrowRec> ArrowsOf(long gid)
        {
            List<ArrowRec> list = null;
            foreach (var rec in Arrows.Values)
                if (rec.Gid == gid && rec.Item != null && !rec.GoneSent) (list ??= new List<ArrowRec>()).Add(rec);
            return list;
        }

        private static long GidOf(Component c)
        {
            try
            {
                var ai = c.GetComponent<BaseAi>() ?? c.GetComponentInParent<BaseAi>() ?? c.GetComponentInChildren<BaseAi>(true);
                return ai != null && ByAi.TryGetValue(ai.Pointer, out long g) ? g : 0;
            }
            catch { return 0; }
        }

        // Prefix BodyHarvest.BodyHarvestDestroy: игра сейчас уронит стрелы туши на землю (DetachIntoWorld).
        // Четвертовали мы (или туша истлела у хозяина зверей) — роняем и сразу говорим всем «стрелы нет»: сообщение
        // уйдёт раньше «туша убрана» (QuarterDone). Иначе тушу убрали по чужому — свои стрелы убираем молча.
        internal static void OnCarcassDestroying(BodyHarvest bh)
        {
            if (!Active || bh == null || Arrows.Count == 0) return;
            long gid = GidOf(bh);
            if (gid == 0) return;
            var list = ArrowsOf(gid);
            if (list == null) return;
            bool mine = _quarterLocal || IsAuthority;
            foreach (var rec in list)
            {
                if (mine)
                {
                    SendArrowGone(rec, _quarterLocal ? "четвертовал тушу" : "туша истлела");
                    rec.Detaching = true;
                }
                else
                {
                    DestroyArrow(rec);
                    ForgetArrow(rec);
                    _arrowsHidden++;
                }
            }
            if (!mine && _arrowLogs++ < 40) MelonLogger.Msg($"[звери] тушу убрали не у меня: стрел из неё убрал {list.Count}");
        }

        // Postfix: стрелы, которые игра не уронила (висели не под тушей), роняем сами.
        internal static void AfterCarcassDestroy(long gid)
        {
            if (gid == 0) return;
            foreach (var rec in new List<ArrowRec>(Arrows.Values))
            {
                if (rec.Gid != gid || !rec.Detaching || rec.Item == null) continue;
                try { if (rec.HostRoot != null && rec.Item.transform.IsChildOf(rec.HostRoot)) rec.Item.DetachIntoWorld(); }
                catch { }
            }
        }

        // Prefix WildlifeItem.SpawnCarcass при подборе тушки у нас: стрелы выйдут из зверя — сразу «стрелы нет».
        internal static long OnCarcassPickingUp(WildlifeItem wi)
        {
            if (!Active || wi == null || !_inPickup || Arrows.Count == 0) return 0;
            long gid = GidOf(wi);
            var list = gid != 0 ? ArrowsOf(gid) : null;
            if (list == null) return 0;
            foreach (var rec in list) { SendArrowGone(rec, "поднял тушку"); rec.Detaching = true; }
            return gid;
        }

        // Prefix Inventory.AddGear: взял стрелу прямо из зверя.
        internal static void OnArrowToInventory(GearItem gi)
        {
            if (gi == null || Arrows.Count == 0) return;
            try
            {
                var ar = gi.m_ArrowItem;
                if (ar == null || !ArrowByPtr.TryGetValue(ar.Pointer, out int id) || !Arrows.TryGetValue(id, out var rec)) return;
                Left(rec, "взял", false);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(ArrowItem), nameof(ArrowItem.HandleCollisionWithObject))]
    internal static class Patch_ArrowStuck
    {
        private static void Postfix(ArrowItem __instance) => AnimalSync.OnArrowHit(__instance);
    }

    [HarmonyPatch(typeof(ArrowItem), nameof(ArrowItem.Deserialize))]
    internal static class Patch_ArrowLoaded
    {
        private static void Postfix(ArrowItem __instance) => AnimalSync.OnArrowLoaded(__instance);
    }

    [HarmonyPatch(typeof(BodyHarvest), nameof(BodyHarvest.BodyHarvestDestroy))]
    internal static class Patch_ArrowsCarcassGone
    {
        private static void Prefix(BodyHarvest __instance, out long __state)
        {
            __state = 0;
            try { AnimalSync.OnCarcassDestroying(__instance); __state = AnimalSync.CarcassGid(__instance); } catch { }
        }

        private static void Postfix(long __state) => AnimalSync.AfterCarcassDestroy(__state);
    }

    [HarmonyPatch(typeof(Panel_BodyHarvest), nameof(Panel_BodyHarvest.QuarterSuccessful))]
    internal static class Patch_ArrowsQuarter
    {
        private static void Prefix() => AnimalSync.QuarterLocal(true);
        private static void Postfix() => AnimalSync.QuarterLocal(false);
    }

    [HarmonyPatch(typeof(WildlifeItem), nameof(WildlifeItem.SpawnCarcass))]
    internal static class Patch_ArrowsRabbit
    {
        private static void Prefix(WildlifeItem __instance, out long __state) => __state = AnimalSync.OnCarcassPickingUp(__instance);
        private static void Postfix(long __state) => AnimalSync.AfterCarcassDestroy(__state);
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddGear))]
    internal static class Patch_ArrowTaken
    {
        private static void Prefix(GearItem gi) => AnimalSync.OnArrowToInventory(gi);
    }
}

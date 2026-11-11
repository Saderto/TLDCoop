using System;
using System.Collections.Generic;
using System.IO;
using Il2Cpp;
using Il2CppTLD.SteamTrain;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Сценарии проверки для бота (команда хоста «бот проверка» или кнопка в окне).
    // Бот по очереди делает то, что обычно делает игрок: кладёт вещь в контейнер, открывает дверь,
    // разжигает очаг, отдаёт хосту вещь, ставит метку, машет рукой, говорит фразу, вешает и снимает трос,
    // строит и разбирает снежное укрытие, чистит лунку, ставит силок, сверяет поезд и стрелки.
    // После каждого шага, который меняет мир, бот присылает хосту, что у него получилось (число вещей,
    // открыто ли, горит ли), и хост сам сравнивает со своим миром и пишет итог в чат.
    public static class BotTests
    {
        private static int _step = -1;
        private static float _nextStep;

        public static bool Running => _step >= 0;

        // ---------- Бот ----------

        public static void Start()
        {
            if (Session.Mode != Mode.Client || !GameApi.InGame()) return;
            if (!Bot.ContentLikeHost()) return;
            _step = 0;
            _nextStep = Time.unscaledTime + 1f;
            Say("начинаю проверки: контейнер, дверь, очаг, отдать вещь, метка, жест, фраза, трос, укрытие, лунка, силок, поезд");
        }

        public static void Tick(float now)
        {
            if (_step < 0 || now < _nextStep) return;
            _nextStep = now + 5f;
            try
            {
                switch (_step++)
                {
                    case 0: TestContainer(); break;
                    case 1: TestDoor(); break;
                    case 2: TestFire(); break;
                    case 3: TestGive(); break;
                    case 4: TestMarker(); break;
                    case 5: Together.StartGesture(Together.GestureWave); Say("машу тебе рукой — видно?"); break;
                    case 6: TestVoice(); break;
                    case 7: TestRope(false); break;
                    case 8: TestRope(true); break;
                    case 9: TestShelterBuild(); break;
                    case 10: TestShelterRemove(); break;
                    case 11: TestIceHole(); break;
                    case 12: TestSnarePlace(); break;
                    case 13: TestSnareChange(); break;
                    case 14: TestSnareCheck(); break;
                    case 15: TestSnareRemove(); break;
                    case 16: TestTrain(); break;
                    default:
                        _step = -1;
                        Say("проверки закончены. Итоги хост написал в чат");
                        break;
                }
            }
            catch (Exception e) { Say("ошибка в проверке: " + e.Message); }
        }

        private static Vector3 Me => GameManager.GetPlayerTransform().position;

        private static T Nearest<T>(float range, Func<T, bool> ok = null) where T : Component
        {
            T best = null;
            float bestD = range * range;
            var me = Me;
            foreach (var c in UnityEngine.Object.FindObjectsOfType<T>())
            {
                if (c == null || !c.gameObject.activeInHierarchy || (ok != null && !ok(c))) continue;
                float d = (c.transform.position - me).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        private static void GoNear(Transform t)
        {
            var me = Me;
            var dir = me - t.position;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
            var p = t.position + dir * 1.2f;
            float y = p.y;
            if (Physics.Raycast(new Vector3(p.x, t.position.y + 1.5f, p.z), Vector3.down, out var hit, 4f)) y = hit.point.y + 0.05f;
            GameApi.Teleport(p.x, y, p.z);
        }

        private static GearItem SomethingToGive()
        {
            var inv = GameManager.GetInventoryComponent();
            var pm = GameManager.GetPlayerManagerComponent();
            if (inv == null) return null;
            var list = new List<GearItem>();
            foreach (var o in inv.m_Items)
            {
                var gi = o?.m_GearItem;
                if (gi == null || gi.m_CantDropItem || gi.m_ClothingItem != null) continue;
                if (gi.GetComponent<WaterSupply>() != null || gi.GetComponent<LiquidItem>() != null) continue;
                if (pm != null && pm.m_ItemInHands != null && pm.m_ItemInHands.Pointer == gi.Pointer) continue;
                list.Add(gi);
            }
            return list.Count == 0 ? null : list[UnityEngine.Random.Range(0, list.Count)];
        }

        private static void TestContainer()
        {
            var c = Nearest<Container>(40f, x => !x.IsLocked());
            if (c == null) { Say("контейнер: рядом нет контейнера, пропускаю"); return; }
            var gi = SomethingToGive();
            if (gi == null) { Say("контейнер: нечего положить, пропускаю"); return; }
            GoNear(c.transform);
            string what = Pretty(gi);
            GameManager.GetInventoryComponent().RemoveGear(gi.gameObject, true);
            c.AddGear(gi);
            WorldObjects.OnContainerClosed(c);
            Say($"контейнер: положил «{what}» в {c.gameObject.name}");
            SendCheck(WorldObjects.Kind.Container, c, "контейнер");
        }

        private static void TestDoor()
        {
            var oc = Nearest<OpenClose>(25f);
            if (oc == null) { Say("дверь: рядом нет дверей и шкафов, пропускаю"); return; }
            GoNear(oc.transform);
            bool open = oc.IsOpen();
            if (open) oc.Close(false, false); else oc.Open(false, false);
            Say($"дверь: {(open ? "закрыл" : "открыл")} {oc.gameObject.name}");
            SendCheck(WorldObjects.Kind.Door, oc, "дверь");
        }

        private static void TestFire()
        {
            var f = Nearest<Fire>(40f);
            if (f == null) { Say("очаг: рядом нет костра или печи, пропускаю"); return; }
            if (f.IsBurning()) { Say("очаг: уже горит, не трогаю"); SendCheck(WorldObjects.Kind.Fire, f, "очаг"); return; }
            GoNear(f.transform);
            f.ForceBurnTimeInMinutes(20f);
            f.StartFireLit();
            Say(f.IsBurning() ? $"очаг: разжёг {f.gameObject.name} на 20 минут" : $"очаг: не получилось разжечь {f.gameObject.name}");
            SendCheck(WorldObjects.Kind.Fire, f, "очаг");
        }

        private static void TestGive()
        {
            if (!Session.Players.TryGetValue(0, out var host) || host.Scene != GameApi.Scene()) { Say("отдать вещь: хост в другой локации, пропускаю"); return; }
            var gi = SomethingToGive();
            if (gi == null) { Say("отдать вещь: нечего отдать, пропускаю"); return; }
            Say($"отдаю тебе «{Pretty(gi)}» — проверь рюкзак");
            Together.Give(null, gi, host);
        }

        private static void TestMarker()
        {
            if (!Session.Players.TryGetValue(0, out var host) || host.Scene != GameApi.Scene()) { Say("метка: хост в другой локации, пропускаю"); return; }
            Together.PlaceMarker(new Vector3(host.X, host.Y, host.Z));
            Say("поставил метку у тебя под ногами — видно?");
        }

        private static void TestVoice()
        {
            var pv = GameManager.GetPlayerVoiceComponent();
            if (pv == null) { Say("фраза: нет голоса игрока, пропускаю"); return; }
            pv.Play("Play_VOInspectObject", Il2CppVoice.Priority.Critical,
                PlayerVoice.Options.IgnoreNonCriticalDelay | PlayerVoice.Options.AllowWhenAllSpeechDisable);
            Say("сказал фразу — если стоишь ближе 20 м, должно быть слышно");
        }


        // ---------- Трос, укрытие, лунка, силок, поезд ----------

        private static RopeAnchorPoint _rope;

        // Трос: повесить (или снять), а следующим шагом вернуть как было. К точке не прыгаем: она бывает на обрыве.
        private static void TestRope(bool back)
        {
            if (back && _rope == null) return;
            var rap = back ? _rope : Nearest<RopeAnchorPoint>(300f);
            if (rap == null) { Say("трос: рядом нет точки крепления, пропускаю"); return; }
            _rope = back ? null : rap;
            bool on = !rap.m_RopeDeployed;
            rap.SetRopeActiveState(on, false);
            WorldExtra.SendRope(rap);
            Say($"трос: {(on ? "повесил" : "снял")}" + (back ? " (вернул как было)" : ""));
            SendCheck(WorldObjects.Kind.Rope, rap, "трос");
        }

        private static SnowShelter _shelter;
        private static Vector3 _shelterPos;

        private static bool Outdoors()
        {
            try { var w = GameManager.GetWeatherComponent(); return w == null || !w.IsIndoorEnvironment(); } catch { return true; }
        }

        private static Vector3 GroundAhead(float dist)
        {
            var t = GameManager.GetPlayerTransform();
            var f = t.forward;
            f.y = 0f;
            f = f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward;
            var p = t.position + f * dist;
            if (Physics.Raycast(new Vector3(p.x, p.y + 2f, p.z), Vector3.down, out var hit, 6f)) p.y = hit.point.y;
            return p;
        }

        private static void TestShelterBuild()
        {
            if (!Outdoors()) { Say("укрытие: я в помещении, пропускаю"); return; }
            var mgr = GameManager.GetSnowShelterManager();
            var ss = mgr != null ? mgr.InstantiateSnowShelter() : null;
            if (ss == null) { Say("укрытие: не смог создать, пропускаю"); return; }
            var p = GroundAhead(4f);
            ss.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, GameManager.GetPlayerTransform().eulerAngles.y + 180f, 0f));
            ss.m_CurrentHP = ss.m_MaxHP;
            SnowShelterManager.Add(ss);
            _shelter = ss;
            _shelterPos = p;
            WorldExtra.SendShelter(ss, false);
            Say("укрытие: построил снежное укрытие в 4 м перед собой");
            SendPosCheck(KShelter, p, 1, "укрытие построено");
        }

        private static void TestShelterRemove()
        {
            var ss = _shelter;
            _shelter = null;
            if (ss == null) return;
            WorldExtra.SendShelter(ss, true);
            try { SnowShelterManager.Remove(ss); } catch { }
            UnityEngine.Object.Destroy(ss.gameObject);
            WorldObjects.ForgetCache(typeof(SnowShelter));
            Say("укрытие: разобрал");
            SendPosCheck(KShelter, _shelterPos, 0, "укрытие разобрано");
        }

        // Лунку прорубить может только игрок на льду, поэтому бот чистит уже прорубленную.
        private static void TestIceHole()
        {
            var h = Nearest<IceFishingHole>(150f);
            if (h == null) { Say("лунка: рядом нет лунок, пропускаю"); return; }
            h.ClearHole();
            Say("лунка: очистил лунку");
            SendRawCheck(KHoles, "all", CountHoles(), "лунки");
        }

        private static int CountHoles()
        {
            int n = 0;
            foreach (var h in UnityEngine.Object.FindObjectsOfType<IceFishingHole>()) if (h != null && h.gameObject.activeInHierarchy) n++;
            return n;
        }

        private static GearItem _snare;
        private static Vector3 _snarePos;

        private static void TestSnarePlace()
        {
            if (!Outdoors()) { Say("силок: я в помещении, пропускаю"); return; }
            var gi = GearItem.InstantiateGearItem("GEAR_Snare");
            var sn = gi != null ? gi.GetComponent<SnareItem>() : null;
            if (sn == null) { Say("силок: не смог создать, пропускаю"); return; }
            var p = GroundAhead(2.5f);
            gi.transform.SetPositionAndRotation(p, Quaternion.identity);
            gi.gameObject.SetActive(true);
            sn.SetState(SnareState.Set);
            _snare = gi;
            _snarePos = p;
            ItemSync.OnLocalDrop(gi);
            Say("силок: поставил силок в 2,5 м перед собой");
            SendPosCheck(KSnare, p, (int)SnareState.Set, "силок поставлен");
        }

        // Как будто силок сработал: меняем состояние, мод сам разошлёт его в течение секунды.
        private static void TestSnareChange()
        {
            if (_snare == null) return;
            _snare.GetComponent<SnareItem>().SetState(SnareState.Broken);
            Say("силок: сломал его (как будто заяц порвал)");
        }

        private static void TestSnareCheck()
        {
            if (_snare == null) return;
            SendPosCheck(KSnare, _snarePos, (int)_snare.GetComponent<SnareItem>().m_State, "силок после срабатывания");
        }

        private static void TestSnareRemove()
        {
            var gi = _snare;
            _snare = null;
            if (gi == null) return;
            ItemSync.OnLocalPickup(gi, gi.transform.position);
            UnityEngine.Object.Destroy(gi.gameObject);
            Say("силок: убрал");
            SendPosCheck(KSnare, _snarePos, -2, "силок убран");
        }

        private static void TestTrain()
        {
            var cars = new List<TrainCar>();
            foreach (var c in UnityEngine.Object.FindObjectsOfType<TrainCar>())
                if (c != null && !string.IsNullOrEmpty(c.GUID)) cars.Add(c);
            string track = TrackSignature();
            if (cars.Count == 0 && track.Length == 0) { Say("поезд: в этой локации нет поезда и стрелок, пропускаю"); return; }
            foreach (var c in cars.GetRange(0, Math.Min(3, cars.Count)))
                SendRawCheck(KTrainCar, c.GUID, PackPos(c.transform.position), "вагон " + c.gameObject.name);
            if (track.Length > 0) SendRawCheck(KTrack, "track", Fnv(track), "стрелки");
            Say($"поезд: сверяю вагонов {Math.Min(3, cars.Count)}" + (track.Length > 0 ? " и стрелки" : ""));
        }

        private static string TrackSignature()
        {
            try
            {
                var all = TrackElementManager.SerializeAll();
                if (all == null || all.Count == 0) return "";
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < all.Count; i++) { var t = all[i]; if (t != null) sb.Append(t.m_Guid).Append('=').Append(t.m_ElementDataSerialized).Append('\n'); }
                return sb.ToString();
            }
            catch { return ""; }
        }

        // Хэш, одинаковый у всех игроков (string.GetHashCode в .NET каждый запуск свой).
        private static int Fnv(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char ch in s) { h ^= ch; h *= 16777619; }
                return (int)h;
            }
        }

        // Место с точностью до 2 м: x и z по 16 бит.
        private static int PackPos(Vector3 p) => ((Mathf.RoundToInt(p.x / 2f) & 0xFFFF) << 16) | (Mathf.RoundToInt(p.z / 2f) & 0xFFFF);

        private static Vector2 UnpackPos(int v) => new Vector2((short)(v >> 16) * 2f, (short)(v & 0xFFFF) * 2f);

        private static string PosId(Vector3 p)
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            return p.x.ToString("F2", ic) + "|" + p.y.ToString("F2", ic) + "|" + p.z.ToString("F2", ic);
        }

        private static Vector3 ParsePos(string id)
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var a = id.Split('|');
            return new Vector3(float.Parse(a[0], ic), float.Parse(a[1], ic), float.Parse(a[2], ic));
        }

        private static void SendPosCheck(byte kind, Vector3 pos, int probe, string label) => SendRawCheck(kind, PosId(pos), probe, label);

        private static void SendCheck(WorldObjects.Kind kind, Component c, string label) =>
            SendRawCheck((byte)kind, WorldObjects.IdOf(c), WorldObjects.ProbeOf(kind, c), label);

        private static void SendRawCheck(byte kind, string id, int probe, string label)
        {
            var w = Msg.New(MsgType.BotCheck);
            w.Write(ItemSync.SceneKey());
            w.Write(kind);
            w.Write(id);
            w.Write(probe);
            w.Write(label);
            Session.SendGameEvent(Msg.Done(w));
        }

        private static string Pretty(GearItem gi)
        {
            try { return gi.DisplayName ?? ItemSync.PrefabName(gi); } catch { return ItemSync.PrefabName(gi); }
        }

        private static void Say(string text)
        {
            MelonLogger.Msg("[бот] " + text);
            Session.SendChat(text);
        }

        // ---------- Хост: сравнить с собой ----------

        private class Check { public string Scene, Id, Label; public WorldObjects.Kind Kind; public int Probe; public float At; }
        private static readonly List<Check> Checks = new();
        private static int _ok, _bad;

        public static void ReceiveCheck(BinaryReader r)
        {
            if (Session.Mode != Mode.Host) return;
            // Даём изменению дойти и примениться.
            Checks.Add(new Check
            {
                Scene = r.ReadString(), Kind = (WorldObjects.Kind)r.ReadByte(), Id = r.ReadString(),
                Probe = r.ReadInt32(), Label = r.ReadString(), At = Time.unscaledTime + 2.5f,
            });
        }

        public static void HostTick(float now)
        {
            for (int i = Checks.Count - 1; i >= 0; i--)
            {
                var c = Checks[i];
                if (now < c.At) continue;
                Checks.RemoveAt(i);
                string text;
                if (!GameApi.InGame() || c.Scene != ItemSync.SceneKey()) text = "я в другой локации, сравню, когда приду";
                else
                {
                    int mine = HostProbe(c.Kind, c.Id, out bool found);
                    if (!found) { text = "у меня такого объекта нет — РАССИНХРОН"; _bad++; }
                    else if (Same(c.Kind, mine, c.Probe)) { text = "совпадает (" + Describe(c.Kind, mine) + ")"; _ok++; }
                    else { text = $"РАССИНХРОН: у бота {Describe(c.Kind, c.Probe)}, у меня {Describe(c.Kind, mine)}"; _bad++; }
                }
                MelonLogger.Msg($"[проверка бота] {c.Label} {c.Id}: {text}");
                Session.SendChat($"Проверка бота «{c.Label}»: {text}. Всего: совпало {_ok}, рассинхрон {_bad}");
            }
        }

        // Проверки, которых нет среди объектов мира: ищем по месту или по GUID вагона.
        private const byte KShelter = 101, KSnare = 102, KHoles = 103, KTrainCar = 104, KTrack = 105;

        private static int HostProbe(WorldObjects.Kind kind, string id, out bool found)
        {
            found = true;
            switch ((byte)kind)
            {
                case KShelter:
                {
                    var p = ParsePos(id);
                    foreach (var s in UnityEngine.Object.FindObjectsOfType<SnowShelter>())
                        if (s != null && (s.transform.position - p).sqrMagnitude < 2.25f) return 1;
                    return 0;
                }
                case KSnare:
                {
                    var p = ParsePos(id);
                    foreach (var sn in UnityEngine.Object.FindObjectsOfType<SnareItem>())
                    {
                        if (sn == null) continue;
                        var gi = sn.GetComponent<GearItem>();
                        if (gi == null || gi.m_InPlayerInventory || gi.m_InsideContainer) continue;
                        if ((sn.transform.position - p).sqrMagnitude < 0.36f) return (int)sn.m_State;
                    }
                    return -2;
                }
                case KHoles: return CountHoles();
                case KTrainCar:
                    foreach (var c in UnityEngine.Object.FindObjectsOfType<TrainCar>())
                        if (c != null && c.GUID == id) return PackPos(c.transform.position);
                    found = false;
                    return -1;
                case KTrack: return Fnv(TrackSignature());
            }
            return WorldObjects.ProbeById(kind, id, out found);
        }

        private static bool Same(WorldObjects.Kind kind, int a, int b) =>
            (byte)kind == KTrainCar ? (UnpackPos(a) - UnpackPos(b)).magnitude <= 4.5f : a == b;

        private static string Describe(WorldObjects.Kind kind, int v)
        {
            switch ((byte)kind)
            {
                case KShelter: return v == 1 ? "укрытие стоит" : "укрытия нет";
                case KSnare: return v == -2 ? "силка нет" : v switch { 0 => "силок не взведён", 1 => "силок взведён", 2 => "силок сломан", 3 => "в силке заяц", _ => "силок " + v };
                case KHoles: return $"лунок {v}";
                case KTrainCar: { var p = UnpackPos(v); return $"вагон у x={p.x:0}, z={p.y:0}"; }
                case KTrack: return "стрелки " + (v & 0xFFFF).ToString("X4");
            }
            return WorldObjects.DescribeProbe(kind, v);
        }

        public static void Reset()
        {
            _step = -1;
            _rope = null;
            _shelter = null;
            _snare = null;
            Checks.Clear();
        }
    }
}

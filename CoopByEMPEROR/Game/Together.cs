using System;
using System.Collections.Generic;
using System.IO;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Мелочи для игры вдвоём, которые видно на экране:
    //  — сообщение из чата пару секунд висит над головой написавшего;
    //  — стрелка у края экрана в сторону друга, если он за спиной или за краем, с расстоянием;
    //  — друзья на карте игры (значок поверх карты своей локации);
    //  — «Отдать» вещь другу рядом прямо из рюкзака (из стопки — сколько выберешь);
    //  — поднять раненого друга: держать ту же клавишу рядом с ним (Game/Rescue.cs).
    public static class Together
    {
        private const float BubbleTime = 6f, MarkerTime = 30f;

        private class Bubble { public string Text; public float Until; }
        private class Marker { public int From; public string Name; public string Scene; public Vector3 Pos; public float Until; }

        private static readonly Dictionary<int, Bubble> Bubbles = new();
        private static readonly List<Marker> Markers = new();
        private static float _nextPing;

        // ---------- Сообщения над головой ----------

        public static void OnChat(int id, string text)
        {
            if (id == Session.LocalId || string.IsNullOrEmpty(text)) return;
            Bubbles[id] = new Bubble { Text = text, Until = Time.unscaledTime + BubbleTime };
        }

        public static string BubbleOf(int id)
        {
            if (!Session.Sees(Shown.Bubbles) || !Bubbles.TryGetValue(id, out var b)) return null;
            if (Time.unscaledTime > b.Until) { Bubbles.Remove(id); return null; }
            return b.Text;
        }

        // ---------- Метка «сюда» ----------

        // Клавиша: рядом с раненым — лечение. Меток («сюда», на карте) и круга жестов на ней больше нет.
        private const float HoldForMenu = 0.3f;
        private static float _pressAt = -1f;

        public static void Tick()
        {
            TickGifts();
            if (Session.Mode == Mode.Off || !Session.InSharedWorld || !GameApi.InGame()) { CloseMenu(false); Rescue.Tick(null, false, false, 0f); return; }
            try { PushApart(); } catch { }
            if (UI.CoopWindow.Open || UI.Widgets.Typing) { CloseMenu(false); _pressAt = -1f; return; }
            TickGesture();
            float now = Time.unscaledTime;
            if (MapOpenNow())
            {
                CloseMenu(false);
                _pressAt = -1f;
                return;   // метки на карте средней кнопкой убраны (0.17.10, просьба игрока)
            }
            bool down = Input.GetKeyDown(Config.PingKey), held = Input.GetKey(Config.PingKey), up = Input.GetKeyUp(Config.PingKey);
            var downed = _menu ? null : DownedNear();
            Drag.Tick(downed);
            Rescue.Tick(downed, down, held, Time.unscaledDeltaTime);
            // Метка «сюда» и круг жестов на этой клавише убраны (0.17.9, просьба игрока): осталось только лечение рядом с раненым.
            _pressAt = -1f;
            if (_menu) CloseMenu(false);
        }

        private static void Ping()
        {
            try
            {
                var cam = GameManager.GetMainCamera();
                if (cam == null) return;
                var ray = new Ray(cam.transform.position, cam.transform.forward);
                Vector3 pos = Physics.Raycast(ray, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore)
                    ? hit.point : ray.origin + ray.direction * 50f;
                PlaceMarker(pos);
            }
            catch (Exception e) { MelonLogger.Warning("[метка] " + e.Message); }
        }

        // Вид метки: обычная «сюда» на 30 секунд, метка на карте «встречаемся здесь» на 30 минут, убрать метку на карте.
        private const byte KindPing = 0, KindMap = 1, KindMapRemove = 2;

        public static void PlaceMarker(Vector3 pos) => SendMarker(KindPing, pos);

        private static void SendMarker(byte kind, Vector3 pos)
        {
            string scene = GameApi.Scene();
            if (kind == KindPing) AddMarker(Session.LocalId, Session.LocalName, scene, pos);
            else AddMapMarker(Session.LocalId, Session.LocalName, scene, pos, kind == KindMapRemove);
            var w = Msg.New(MsgType.Marker);
            w.Write((byte)Session.LocalId);
            w.Write(scene);
            w.Write(pos.x); w.Write(pos.y); w.Write(pos.z);
            w.Write(kind);
            Session.SendGameEvent(Msg.Done(w));
        }

        public static void ReceiveMarker(BinaryReader r)
        {
            int from = r.ReadByte();
            string scene = r.ReadString();
            var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            byte kind = r.BaseStream.Position < r.BaseStream.Length ? r.ReadByte() : KindPing;
            if (from == Session.LocalId) return;
            string name = Session.Players.TryGetValue(from, out var pl) ? pl.Name : "?";
            if (kind != KindPing)
            {
                AddMapMarker(from, name, scene, pos, kind == KindMapRemove);
                if (kind == KindMap)
                {
                    GameApi.HudMessage(name + " отметил на карте: встречаемся здесь");
                    try { GameAudioManager.PlayGuiConfirm(); } catch { }
                }
                return;
            }
            AddMarker(from, name, scene, pos);
            if (scene == GameApi.Scene())
            {
                GameApi.HudMessage(name + " поставил метку");
                try { GameAudioManager.PlayGuiConfirm(); } catch { }
            }
        }

        // ---------- Метка на карте игры ----------
        // Открыл карту своей локации, навёл мышь, нажал клавишу метки — у всех на карте звёздочка с твоим ником,
        // а в мире над этим местом подпись с расстоянием. Нажал по своей звёздочке ещё раз — метка убрана.

        private const float MapMarkerTime = 15f * 60f;
        private static readonly List<Marker> MapMarkers = new();

        // Своя последняя метка (на карте или «сюда») — для испытания «встретиться у метки» (Game/Challenges.cs).
        public static bool OwnMarker(out Vector3 pos, out string scene)
        {
            var m = MapMarkers.FindLast(x => x.From == Session.LocalId) ?? Markers.FindLast(x => x.From == Session.LocalId);
            pos = m?.Pos ?? Vector3.zero;
            scene = m?.Scene ?? "";
            return m != null;
        }
        private static Panel_Map _mapPanel;
        private static Transform _mapRoot;
        private static Camera _mapCam;
        private static string _mapScene = "";
        private static int _mapFrame = -10;

        private static void AddMapMarker(int from, string name, string scene, Vector3 pos, bool remove)
        {
            MapMarkers.RemoveAll(m => m.From == from);
            if (!remove) MapMarkers.Add(new Marker { From = from, Name = name, Scene = scene, Pos = pos, Until = Time.unscaledTime + MapMarkerTime });
        }

        // Карта нашей локации на экране (её последний раз рисовали в OnGUI только что).
        private static bool MapOpenNow()
        {
            if (Time.frameCount - _mapFrame > 2 || _mapPanel == null) return false;
            try { return InterfaceManager.IsPanelEnabled<Panel_Map>(); } catch { return false; }
        }

        private static Vector2 MapPoint(Vector3 world)
        {
            var mp = _mapPanel.WorldPositionToMapPosition(_mapScene, world);
            var sp = _mapCam.WorldToScreenPoint(_mapRoot.TransformPoint(mp));
            return new Vector2(sp.x, sp.y);   // экранные координаты Unity (y снизу)
        }

        private static void MapClick(Vector3 mouse)
        {
            try
            {
                if (_mapPanel == null || _mapRoot == null || _mapCam == null) return;
                // Своя звёздочка под мышью — убрать.
                var mine = MapMarkers.Find(m => m.From == Session.LocalId && m.Scene == _mapScene);
                if (mine != null && (MapPoint(mine.Pos) - new Vector2(mouse.x, mouse.y)).magnitude < 40f)
                {
                    SendMarker(KindMapRemove, mine.Pos);
                    GameApi.HudMessage("Метка на карте убрана");
                    return;
                }
                // Экран → мир: карта линейна по x и z мира, находим её по трём точкам.
                GameApi.TryGetPlayer(out float px, out float py, out float pz, out _, out _);
                var o = new Vector3(px, py, pz);
                Vector2 m0 = MapPoint(o), mx = MapPoint(o + new Vector3(100f, 0f, 0f)) - m0, mz = MapPoint(o + new Vector3(0f, 0f, 100f)) - m0;
                Vector2 d = new Vector2(mouse.x, mouse.y) - m0;
                float det = mx.x * mz.y - mx.y * mz.x;
                if (Mathf.Abs(det) < 1e-4f) return;
                float a = (d.x * mz.y - d.y * mz.x) / det, b = (mx.x * d.y - mx.y * d.x) / det;
                var pos = o + new Vector3(a * 100f, 0f, b * 100f);
                pos.y = py;
                if (Physics.Raycast(new Vector3(pos.x, py + 1500f, pos.z), Vector3.down, out var hit, 3000f, ~0, QueryTriggerInteraction.Ignore)) pos.y = hit.point.y;
                SendMarker(KindMap, pos);
                try { GameAudioManager.PlayGuiConfirm(); } catch { }
                GameApi.HudMessage("Метка на карте: встречаемся здесь. Видят все");
            }
            catch (Exception e) { MelonLogger.Warning("[метка на карте] " + e.Message); }
        }

        private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        // Окно мода: убрать свою метку на карте, не открывая карту.
        public static bool HasOwnMapMarker => MapMarkers.Exists(m => m.From == Session.LocalId);
        public static void RemoveOwnMapMarker()
        {
            var own = MapMarkers.Find(m => m.From == Session.LocalId);
            if (own == null) return;
            SendMarker(KindMapRemove, own.Pos);
            GameApi.HudMessage("Метка на карте убрана");
        }

        private static void AddMarker(int from, string name, string scene, Vector3 pos)
        {
            Markers.RemoveAll(m => m.From == from);   // у каждого одна метка, новая заменяет старую
            Markers.Add(new Marker { From = from, Name = name, Scene = scene, Pos = pos, Until = Time.unscaledTime + MarkerTime });
        }

        // ---------- Поднять раненого ----------

        private static Player DownedNear()
        {
            if (!GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _) || Death.IsDowned) return null;
            var me = new Vector3(x, y, z);
            string scene = GameApi.Scene();
            foreach (var p in Session.Players.Values)
                if (Session.With(p, scene) && p.Action == Activity.Downed && (new Vector3(p.X, p.Y, p.Z) - me).sqrMagnitude < 2.5f * 2.5f) return p;
            return null;
        }

        public static void ReceiveRevive(BinaryReader r)
        {
            int from = r.ReadByte();
            int target = r.ReadByte();
            if (target != Session.LocalId) return;
            string name = Session.Players.TryGetValue(from, out var pl) ? pl.Name : "друг";
            Death.OnRevivedBy(name);
        }

        // ---------- Отдать вещь ----------

        private static Panel_Inventory InventoryOpen()
        {
            try { return InterfaceManager.TryGetPanel<Panel_Inventory>(out var p) && p != null && p.IsEnabled() ? p : null; }
            catch { return null; }
        }

        private static void DrawGive(Vector3 me, string scene)
        {
            var panel = InventoryOpen();
            if (panel == null) return;
            if (Trade.DrawInInventory(panel)) return;
            var near = new List<Player>();
            foreach (var pl in Session.Players.Values)
                if (Session.With(pl, scene) && (new Vector3(pl.X, pl.Y, pl.Z) - me).sqrMagnitude <= 4f * 4f && near.Count < 3) near.Add(pl);
            if (near.Count == 0) return;
            // Обмен: справа внизу, по кнопке на каждого друга рядом.
            float ty = Screen.height - 46f;
            foreach (var pl in near)
            {
                if (UI.Widgets.Button(new Rect(Screen.width - 330f, ty, 310f, 30f), UI.Widgets.Fit("Обмен с " + pl.Name, 310f))) Trade.Invite(pl);
                ty -= 36f;
            }
            GearItem gi;
            try { gi = panel.GetCurrentlySelectedItem()?.m_GearItem; } catch { return; }
            if (gi == null) return;

            // Стопка: сколько отдать (−/+, 1, половина, все). Выбор держится, пока выбрана та же вещь.
            int units = gi.m_StackableItem != null ? Math.Max(1, gi.m_StackableItem.m_Units) : 1;
            if (gi.Pointer != _giveItem) { _giveItem = gi.Pointer; _giveCount = units; }
            _giveCount = Mathf.Clamp(_giveCount, 1, units);
            // Внизу под сеткой вещей: сверху эта полоса закрывала вкладки разделов рюкзака.
            float total = (units > 1 ? 36f : 0f) + near.Count * 36f;
            float x0 = Mathf.Max(10f, Screen.width * 0.345f - 230f), y = Screen.height - 14f - total;
            if (units > 1)
            {
                UI.Theme.Fill(new Rect(x0, y, 460f, 30f), UI.Theme.PanelBg);
                GUI.Label(new Rect(x0 + 8f, y + 4f, 90f, 22f), "Сколько:");
                if (UI.Widgets.Button(new Rect(x0 + 90f, y + 3f, 30f, 24f), "−")) _giveCount = Math.Max(1, _giveCount - 1);
                UI.Theme.Centered(new Rect(x0 + 122f, y + 3f, 80f, 24f), $"<b>{_giveCount}</b> из {units}");
                if (UI.Widgets.Button(new Rect(x0 + 204f, y + 3f, 30f, 24f), "+")) _giveCount = Math.Min(units, _giveCount + 1);
                if (UI.Widgets.Button(new Rect(x0 + 246f, y + 3f, 66f, 24f), "1")) _giveCount = 1;
                if (UI.Widgets.Button(new Rect(x0 + 316f, y + 3f, 66f, 24f), "½")) _giveCount = Math.Max(1, units / 2);
                if (UI.Widgets.Button(new Rect(x0 + 386f, y + 3f, 66f, 24f), "Все")) _giveCount = units;
                y += 36f;
            }
            string what = gi.DisplayName ?? ItemSync.PrefabName(gi);
            string count = units > 1 ? $" ×{_giveCount}" : "";
            foreach (var pl in near)
            {
                if (UI.Widgets.Button(new Rect(x0, y, 460f, 30f), UI.Widgets.Fit($"Отдать «{what}»{count} → {pl.Name}", 460f)))
                {
                    Give(panel, gi, pl, units > 1 ? _giveCount : 0);
                    break;
                }
                y += 36f;
            }
        }

        private static IntPtr _giveItem;
        private static int _giveCount;

        // count: сколько штук из стопки (0 — всю вещь целиком).
        internal static void Give(Panel_Inventory panel, GearItem gi, Player to, int count = 0)
        {
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                if (gi.m_ClothingItem != null && pm != null && pm.IsWearingClothingItem(gi)) { GameApi.HudMessage("Сначала сними эту одежду"); return; }
                if (pm != null && pm.m_ItemInHands != null && pm.m_ItemInHands.Pointer == gi.Pointer) pm.UnequipItemInHandsSkipAnimation();
                string prefab = ItemSync.PrefabName(gi), what = gi.DisplayName ?? prefab;
                var stack = gi.m_StackableItem;
                int units = stack != null ? Math.Max(1, stack.m_Units) : 1;
                bool part = stack != null && count > 0 && count < units;
                string json;
                if (part)
                {
                    // Часть стопки: записываем копию с нужным числом штук, у себя оставляем остальное.
                    stack.m_Units = count;
                    try { json = gi.SerializeToString() ?? ""; }
                    finally { stack.m_Units = units; }
                    what += " ×" + count;
                }
                else json = gi.SerializeToString() ?? "";
                int giftId = UnityEngine.Random.Range(1, int.MaxValue);
                var w = Msg.New(MsgType.GiveItem);
                w.Write((byte)Session.LocalId);
                w.Write((byte)to.Id);
                w.Write(prefab);
                w.Write(json);
                w.Write(giftId);
                Session.SendGameEvent(Msg.Done(w));
                Sent[giftId] = new Gift { Prefab = prefab, Json = json, To = to.Name, ToId = to.Id, At = Time.unscaledTime };
                if (part) stack.m_Units = units - count;
                else GameManager.GetInventoryComponent().DestroyGear(gi);
                if (panel != null) { try { panel.RefreshTable(); } catch { } }
                GameApi.HudMessage($"Отдал {to.Name}: {what}");
                MelonLogger.Msg($"[вещи] отдал {to.Name}: {prefab}");
            }
            catch (Exception e) { MelonLogger.Warning("[вещи] отдать: " + e.Message); }
        }

        public static void ReceiveGive(BinaryReader r)
        {
            int from = r.ReadByte();
            int to = r.ReadByte();
            string prefab = r.ReadString();
            string json = r.ReadString();
            int giftId = r.BaseStream.Position < r.BaseStream.Length ? r.ReadInt32() : 0;
            bool returned = r.BaseStream.Position < r.BaseStream.Length && r.ReadByte() == 1;
            string owner = returned && r.BaseStream.Position < r.BaseStream.Length ? r.ReadString() : null;
            if (to != Session.LocalId) return;
            // Возврат не нам: наш номер мог достаться другому. Хост хранит ник обрезанным (Session.Clean).
            if (owner != null && owner != Session.LocalName && owner != Session.Clean(Session.LocalName)) return;
            string name = Session.Players.TryGetValue(from, out var pl) ? pl.Name : "Друг";
            if (giftId == 0) { Take(name, prefab, json, from, returned); return; }
            if (!Got.Add(giftId)) return;   // уже получали (дослали повторно)
            if (returned) { Take(name, prefab, json, from, true); return; }   // возврат: решение хоста не нужно, повтор отсекает Got
            // Вещь берём только с разрешения хоста: если даривший уже забрал её назад (подарок шёл дольше 15 с),
            // хост скажет «отменён», и вещь не окажется у обоих.
            Waiting[giftId] = (name, prefab, json, from, Time.unscaledTime);
            Ask(giftId, 1, from, to);
        }

        private static void Take(string name, string prefab, string json, int giver = -1, bool returned = false)
        {
            // Мы на экране загрузки: вещь ждёт, пока загрузимся (раньше она пропадала насовсем).
            if (!GameApi.InGame()) { Incoming.Add((name, prefab, json, giver, returned)); MelonLogger.Msg($"[вещи] {name} отдал {prefab}, я загружаюсь — выдам после загрузки"); return; }
            Accept(name, prefab, json, giver, returned);
        }

        // Подарок у нас не создался (нет мода или DLC, на котором сделана вещь): отправляем его обратно дарившему,
        // иначе он пропал бы у обоих — даривший удалил его у себя сразу при отправке.
        // Адресуем по нику: даривший мог выйти, а его номер — достаться другому. Нет его в общем мире — ждём, пока зайдёт.
        private static void SendBack(int giver, string giverName, string prefab, string json)
        {
            if (giver < 0 || giver == Session.LocalId) return;
            var ret = new Return { To = giverName, GiverId = giver, Prefab = prefab, Json = json, Id = UnityEngine.Random.Range(1, int.MaxValue) };
            if (!TrySendBack(ret)) { Returns.Add(ret); MelonLogger.Msg($"[вещи] {giverName} сейчас не в мире, верну ему {prefab}, когда зайдёт"); }
        }

        private static bool TrySendBack(Return ret)
        {
            if (Session.Mode == Mode.Off) return false;
            static bool Ready(Player p) => p.InWorld && p.Scene.Length > 0 && Time.unscaledTime - p.LastState < 5f;
            int id = -1, same = 0;
            // Сначала номер дарившего, если ник тот же: ники не уникальны (два «Саши» в Steam-лобби).
            if (ret.GiverId != Session.LocalId && Session.Players.TryGetValue(ret.GiverId, out var g) && g.Name == ret.To)
            {
                if (!Ready(g)) return false;
                id = ret.GiverId;
            }
            else
            {
                // Номер ушёл: ищем по нику, но только если такой ник один, иначе вещь может уйти не тому.
                foreach (var kv in Session.Players)
                    if (kv.Key != Session.LocalId && kv.Value.Name == ret.To) { same++; if (Ready(kv.Value)) id = kv.Key; }
                if (same != 1 || id < 0) return false;
            }
            var w = Msg.New(MsgType.GiveItem);
            w.Write((byte)Session.LocalId);
            w.Write((byte)id);
            w.Write(ret.Prefab);
            w.Write(ret.Json);
            w.Write(ret.Id);     // свой номер: повтор из журнала отсекает Got у получателя
            w.Write((byte)1);    // возврат
            w.Write(ret.To);     // кому: номер мог смениться
            Session.SendGameEvent(Msg.Done(w));
            MelonLogger.Msg($"[вещи] вернул {ret.To} {ret.Prefab}");
            return true;
        }

        private class Return { public string To, Prefab, Json; public int Id, GiverId; }
        private static readonly List<Return> Returns = new();   // возвраты, ждущие дарившего (до конца сессии)
        private static float _returnsTry;

        // ---------- Подарок решает хост ----------
        // Получатель: «беру» (1). Даривший, не дождавшись 15 с: «отменяю» (2). Хост помнит, что было первым,
        // и отвечает: получателю «твой» (11) или «отменён» (13), дарившему «доставлен» (12) или «отменён» (13).

        private static readonly Dictionary<int, (string from, string prefab, string json, int giver, float asked)> Waiting = new();
        private static readonly Dictionary<int, byte> HostGifts = new();   // 1 доставлен, 2 отменён

        private static void Ask(int giftId, byte op, int giver, int recipient)
        {
            var w = Msg.New(MsgType.GiftState);
            w.Write(giftId); w.Write(op); w.Write((byte)giver); w.Write((byte)recipient);
            var data = Msg.Done(w);
            if (Session.Mode == Mode.Host) { using var r = Msg.Read(data, out _); HostGiftState(Session.LocalId, r); }
            else Session.SendToHost(data);
        }

        public static void HostGiftState(int sender, BinaryReader r)
        {
            int giftId = r.ReadInt32();
            byte op = r.ReadByte();
            int giver = r.ReadByte(), recipient = r.ReadByte();
            HostGifts.TryGetValue(giftId, out byte st);
            if (op == 1)
            {
                if (st == 2) { Reply(sender, giftId, 13); return; }
                HostGifts[giftId] = 1;
                Reply(sender, giftId, 11);
                if (st == 0) Reply(giver, giftId, 12);
            }
            else if (op == 2)
            {
                if (st == 1) { Reply(sender, giftId, 12); return; }
                HostGifts[giftId] = 2;
                Reply(sender, giftId, 13);
                MelonLogger.Msg($"[вещи] подарок {giftId}: получатель не взял за 15 с, отменил — вещь вернётся дарившему");
            }
            if (HostGifts.Count > 2000) HostGifts.Clear();
        }

        private static void Reply(int to, int giftId, byte code)
        {
            var w = Msg.New(MsgType.GiftState);
            w.Write(giftId); w.Write(code); w.Write((byte)0); w.Write((byte)0);
            var data = Msg.Done(w);
            if (to == Session.LocalId) { using var r = Msg.Read(data, out _); ClientGiftState(r); }
            else Session.SendTo(to, data);
        }

        public static void ClientGiftState(BinaryReader r)
        {
            int giftId = r.ReadInt32();
            byte code = r.ReadByte();
            if (code == 11 && Waiting.Remove(giftId, out var g)) Take(g.from, g.prefab, g.json, g.giver);
            else if (code == 13 && Waiting.Remove(giftId, out var lost)) MelonLogger.Msg($"[вещи] подарок {lost.prefab} от {lost.from} пришёл поздно, он уже вернулся к дарившему");
            else if (code == 12 && Sent.Remove(giftId, out var done)) MelonLogger.Msg($"[вещи] {done.To} получил {done.Prefab}");
            else if (code == 13 && Sent.Remove(giftId, out var back)) GiveBack(back);
        }

        private class Gift { public string Prefab, Json, To; public int ToId; public float At, Asked; }
        private static readonly Dictionary<int, Gift> Sent = new();
        private static readonly HashSet<int> Got = new();
        private static readonly List<(string from, string prefab, string json, int giver, bool returned)> Incoming = new();

        public static void ReceiveGiveAck(BinaryReader r)
        {
            int giver = r.ReadByte();
            int id = r.ReadInt32();
            if (giver != Session.LocalId || !Sent.Remove(id, out var g)) return;
            MelonLogger.Msg($"[вещи] {g.To} получил {g.Prefab}");
        }

        private static void TickGifts()
        {
            if (Incoming.Count > 0 && GameApi.InGame() && !GameApi.IsLoading())
            {
                foreach (var (from, prefab, json, giver, returned) in Incoming.ToArray()) Accept(from, prefab, json, giver, returned);
                Incoming.Clear();
            }
            float now = Time.unscaledTime;
            if (Returns.Count > 0 && now - _returnsTry > 2f)
            {
                _returnsTry = now;
                Returns.RemoveAll(TrySendBack);
            }
            // «Беру» могло потеряться (обрыв): спрашиваем хоста снова каждые 5 с.
            foreach (var kv in new List<KeyValuePair<int, (string from, string prefab, string json, int giver, float asked)>>(Waiting))
                if (now - kv.Value.asked > 5f)
                {
                    Waiting[kv.Key] = (kv.Value.from, kv.Value.prefab, kv.Value.json, kv.Value.giver, now);
                    Ask(kv.Key, 1, kv.Value.giver, Session.LocalId);
                }
            if (Sent.Count == 0) return;
            foreach (var kv in new List<KeyValuePair<int, Gift>>(Sent))
            {
                if (now - kv.Value.At < 15f || now - kv.Value.Asked < 5f) continue;
                if (!GameApi.InGame() || GameApi.IsLoading()) return;
                // Не дождались: просим хоста отменить. Вернём вещь, только когда он ответит «отменён».
                kv.Value.Asked = now;
                Ask(kv.Key, 2, Session.LocalId, kv.Value.ToId);
            }
        }

        private static void GiveBack(Gift g)
        {
            {
                ItemSync.Quiet = true;
                try
                {
                    var gi = ItemSync.Spawn(g.Prefab, g.Json, null, false, false);
                    if (gi != null) GameManager.GetInventoryComponent().AddGear(gi, true);
                    GameApi.HudMessage($"{g.To} не получил подарок: {gi?.DisplayName ?? g.Prefab} вернулся к тебе");
                    MelonLogger.Msg($"[вещи] {g.To} не подтвердил подарок {g.Prefab} за 15 с, вернул себе");
                }
                catch (Exception e) { MelonLogger.Warning("[вещи] вернуть подарок: " + e.Message); }
                finally { ItemSync.Quiet = false; }
            }
        }

        private static void Accept(string name, string prefab, string json, int giver = -1, bool returned = false)
        {
            ItemSync.Quiet = true;
            try
            {
                GearItem gi = null;
                try { gi = ItemSync.Spawn(prefab, json, null, false, false); }
                catch (Exception e) { MelonLogger.Warning("[вещи] подарок " + prefab + ": " + e.Message); }
                if (gi == null)
                {
                    MelonLogger.Warning("[вещи] не создался подарок " + prefab + (returned ? " (возврат)" : ""));
                    if (!returned && giver >= 0)
                    {
                        SendBack(giver, name, prefab, json);
                        GameApi.HudMessage($"Подарок от {name} не взялся: такой вещи нет в твоей игре (нужен мод или DLC). Он вернулся к {name}");
                    }
                    return;
                }
                GameManager.GetInventoryComponent().AddGear(gi, true);
                GameApi.HudMessage(returned
                    ? $"{name} не смог взять «{gi.DisplayName ?? prefab}»: у него нет нужного мода или DLC. Вещь вернулась к тебе"
                    : $"{name} отдал тебе: {gi.DisplayName ?? prefab}");
                if (Bot.Active == false && Session.Mode == Mode.Host && name == "Бот") Session.SendChat($"Проверка бота «отдать вещь»: получил {gi.DisplayName ?? prefab}");
                try { GameAudioManager.PlayGuiConfirm(); } catch { }
                MelonLogger.Msg($"[вещи] {name} отдал мне {prefab}");
            }
            catch (Exception e) { MelonLogger.Warning("[вещи] подарок: " + e.Message); }
            finally { ItemSync.Quiet = false; }
        }

        // ---------- Жесты ----------
        // 1 помахать, 2 показать рукой (куда смотришь), 3 сесть (пока не сдвинешься с места), 4 позвать, 5 «стоп».

        public const byte GestureWave = 1, GesturePoint = 2, GestureSit = 3, GestureBeckon = 4, GestureStop = 5;
        public static byte Gesture { get; private set; }
        private static float _gestureUntil;
        private static Vector3 _gesturePos;

        public static void StartGesture(byte g)
        {
            Gesture = g;
            _gestureUntil = g == GestureSit ? float.MaxValue : Time.unscaledTime + 3f;
            if (GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _)) _gesturePos = new Vector3(x, y, z);
        }

        private static void TickGesture()
        {
            if (Gesture == 0) return;
            if (Time.unscaledTime > _gestureUntil) { Gesture = 0; return; }
            if (GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _) && (new Vector3(x, y, z) - _gesturePos).sqrMagnitude > 0.16f)
                Gesture = 0;
        }

        // ---------- Твёрдые фигуры ----------
        // Коллайдеров у фигур нет (иначе в них упирались бы пули и звери), поэтому сквозь друга не пускаем сами:
        // если подошёл ближе 0,55 м, мягко отодвигаем своего игрока через его CharacterController (стены он учитывает).
        // Лежащих (сон, ранен, схватка) можно переступить.

        private const float BodyGap = 0.55f;
        private static CharacterController _cc;

        private static void PushApart()
        {
            if (!GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _)) return;
            var me = new Vector3(x, y, z);
            string scene = GameApi.Scene();
            var push = Vector3.zero;
            foreach (var p in Session.Players.Values)
            {
                if (!Session.With(p, scene) || !p.Puppet.Alive || p.Action == 1 || p.Action == Activity.Downed || p.Action == Activity.Dead || p.Action == 33 || p.Action == 34) continue;
                var feet = p.Puppet.LabelPoint - new Vector3(0f, 1.95f, 0f);
                if (Math.Abs(feet.y - me.y) > 1.6f) continue;
                var d = new Vector3(me.x - feet.x, 0f, me.z - feet.z);
                float dist = d.magnitude;
                if (dist >= BodyGap) continue;
                var dir = dist > 0.01f ? d / dist : -GameManager.GetPlayerTransform().forward;
                push += dir * (BodyGap - dist);
            }
            if (push.sqrMagnitude < 1e-6f) return;
            push = Vector3.ClampMagnitude(push, 0.08f);   // за кадр — немного, чтобы не дёргало
            var t = GameManager.GetPlayerTransform();
            if (_cc == null || _cc.transform != t) _cc = t.GetComponent<CharacterController>() ?? t.GetComponentInParent<CharacterController>();
            if (_cc != null && _cc.enabled) _cc.Move(push);
            else t.position += push;
        }

        // ---------- Круг жестов (зажатая клавиша метки) ----------
        // Курсор освобождаем, камеру держим на месте (чувствительность мыши 0, пока круг открыт).
        // Выбор — по направлению курсора от центра экрана; в центре — ничего.

        private static readonly (byte g, string name)[] Menu =
        {
            (GestureWave, "Помахать"), (GestureBeckon, "Позвать"), (GesturePoint, "Показать"), (GestureStop, "Стоп"), (GestureSit, "Сесть"),
        };

        private static bool _menu;
        private static float _sens = -1f, _sensZoom, _sensUnzoom;

        public static bool MenuOpen => _menu;

        private static void OpenMenu()
        {
            _menu = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            KeepMenu();
        }

        private static void KeepMenu()
        {
            try
            {
                var cam = GameManager.GetVpFPSCamera();
                if (cam == null) return;
                if (_sens < 0f) { _sens = cam.m_MouseSensitivity; _sensZoom = cam.m_ZoomedMouseSensitivity; _sensUnzoom = cam.m_UnzoomedMouseSensitivity; }
                cam.m_MouseSensitivity = 0f; cam.m_ZoomedMouseSensitivity = 0f; cam.m_UnzoomedMouseSensitivity = 0f;
            }
            catch { }
        }

        private static void CloseMenu(bool pick)
        {
            if (!_menu) return;
            _menu = false;
            if (pick)
            {
                int i = MenuChoice();
                if (i >= 0) StartGesture(Menu[i].g);
            }
            try
            {
                var cam = GameManager.GetVpFPSCamera();
                if (cam != null && _sens >= 0f) { cam.m_MouseSensitivity = _sens; cam.m_ZoomedMouseSensitivity = _sensZoom; cam.m_UnzoomedMouseSensitivity = _sensUnzoom; }
            }
            catch { }
            _sens = -1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Номер пункта под курсором: по кругу от верха по часовой стрелке, по 72°.
        private static int MenuChoice()
        {
            var m = Input.mousePosition;
            float dx = m.x - Screen.width / 2f, dy = m.y - Screen.height / 2f;
            if (dx * dx + dy * dy < 45f * 45f) return -1;
            float a = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
            if (a < 0f) a += 360f;
            return Mathf.RoundToInt(a / 72f) % Menu.Length;
        }

        private static void DrawMenu()
        {
            UI.Theme.Apply();
            float cx = Screen.width / 2f, cy = Screen.height / 2f, rad = 130f;
            int sel = MenuChoice();
            UI.Theme.Fill(new Rect(cx - 4f, cy - 4f, 8f, 8f), UI.Theme.Accent);
            for (int i = 0; i < Menu.Length; i++)
            {
                float a = i * 72f * Mathf.Deg2Rad;
                float x = cx + Mathf.Sin(a) * rad, y = cy - Mathf.Cos(a) * rad;
                var r = new Rect(x - 70f, y - 18f, 140f, 36f);
                UI.Theme.Fill(r, i == sel ? UI.Theme.Selected : UI.Theme.PanelBg);
                UI.Theme.Frame(r, i == sel ? UI.Theme.Accent : UI.Theme.Border);
                UI.Theme.Centered(r, i == sel ? "<b>" + Menu[i].name + "</b>" : Menu[i].name);
            }
            UI.Theme.Centered(new Rect(cx - 200f, cy + rad + 30f, 400f, 24f), "Отпусти на жесте · в центре — отмена");
        }

        // ---------- Рисование (из OnGUI) ----------

        public static void Draw()
        {
            if (Session.Mode == Mode.Off || !GameApi.InGame()) return;
            if (_menu) DrawMenu();
            var cam = GameManager.GetMainCamera();
            if (cam == null) return;
            string scene = GameApi.Scene();
            GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _);
            var me = new Vector3(x, y, z);
            bool mapOpen = DrawMap(scene);
            if (mapOpen) return;
            if (Session.InSharedWorld) DrawGive(me, scene);
            if (Rescue.Hint.Length > 0)
            {
                Shadowed(new Rect(Screen.width / 2f - 300f, Screen.height * 0.62f, 640f, 26f), Rescue.Hint + Drag.Hint(DownedNear()));
                if (Rescue.Treating)
                {
                    var bar = new Rect(Screen.width / 2f - 120f, Screen.height * 0.62f + 30f, 240f, 10f);
                    UI.Theme.Fill(bar, new Color(0f, 0f, 0f, 0.6f));
                    bar.width *= Mathf.Clamp01(Rescue.Progress);
                    UI.Theme.Fill(bar, UI.Theme.Accent);
                }
            }

            float now = Time.unscaledTime;
            Markers.RemoveAll(m => now > m.Until);
            foreach (var m in Markers)
            {
                if (m.Scene != scene) continue;
                int d = (int)Vector3.Distance(me, m.Pos);
                string who = m.From == Session.LocalId ? "Моя метка" : m.Name.Replace("<", "‹");
                Point(cam, m.Pos + Vector3.up * 0.3f, $"<b><color=#{Session.ColorOf(m.From, "ffcc55")}>▼</color></b>", $"<b>{who}</b>  {d} м", true);
            }
            MapMarkers.RemoveAll(m => now > m.Until);
            // Дошёл до своей метки — она больше не нужна: убираем у всех.
            var own = MapMarkers.Find(m => m.From == Session.LocalId && m.Scene == scene);
            if (own != null && now - (own.Until - MapMarkerTime) > 5f && FlatDistance(me, own.Pos) < 8f)
            {
                SendMarker(KindMapRemove, own.Pos);
                GameApi.HudMessage("Ты на месте метки, убрал её");
            }
            foreach (var m in MapMarkers)
            {
                if (m.Scene != scene) continue;
                int d = (int)Vector3.Distance(me, m.Pos);
                string who = m.From == Session.LocalId ? "Моя метка на карте" : m.Name.Replace("<", "‹") + ": встречаемся здесь";
                Point(cam, m.Pos + Vector3.up * 2f, $"<b><color=#{Session.ColorOf(m.From, "7fe0ff")}>★</color></b>", $"<b>{who}</b>  {d} м", true);
            }

            if (!Session.Sees(Shown.Arrows)) return;
            foreach (var pl in Session.Players.Values)
            {
                if (!Session.With(pl, scene) || !pl.Puppet.Alive) continue;
                var p = pl.Puppet.LabelPoint;
                var sp = cam.WorldToScreenPoint(p);
                if (sp.z > 0f && sp.x > 0f && sp.x < Screen.width && sp.y > 0f && sp.y < Screen.height) continue;   // и так видно
                int d = (int)Vector3.Distance(me, p);
                Point(cam, p, "", $"<b>{pl.Name.Replace("<", "‹")}</b>  {d} м", false, Session.ColorOf(pl.Id, "ffcc55"));
            }
        }

        // Точка в мире: на экране — значок и подпись над ней; за краем или за спиной — стрелка у края экрана.
        private static void Point(Camera cam, Vector3 world, string icon, string label, bool showIcon, string arrowColor = "ffcc55")
        {
            var sp = cam.WorldToScreenPoint(world);
            float sx = sp.x, sy = Screen.height - sp.y;
            bool onScreen = sp.z > 0f && sx >= 0f && sx <= Screen.width && sy >= 0f && sy <= Screen.height;
            if (onScreen)
            {
                if (showIcon) Shadowed(new Rect(sx - 10f, sy - 26f, 40f, 30f), $"<size=22>{icon}</size>");
                Shadowed(new Rect(sx - 60f, sy - (showIcon ? 48f : 26f), 240f, 24f), label);
                return;
            }
            // За спиной WorldToScreenPoint зеркалит точку — разворачиваем.
            var dir = new Vector2(sp.x - Screen.width / 2f, sp.y - Screen.height / 2f);
            if (sp.z < 0f) dir = -dir;
            if (dir.sqrMagnitude < 1f) dir = Vector2.down;
            float hw = Screen.width / 2f - 70f, hh = Screen.height / 2f - 50f;
            float k = Mathf.Min(hw / Mathf.Max(0.001f, Mathf.Abs(dir.x)), hh / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
            var e = dir * k;
            float ex = Screen.width / 2f + e.x, ey = Screen.height / 2f - e.y;
            string arrow = Mathf.Abs(dir.x) > Mathf.Abs(dir.y) ? (dir.x > 0 ? "►" : "◄") : (dir.y > 0 ? "▲" : "▼");
            Shadowed(new Rect(ex - 60f, ey - 12f, 240f, 24f), $"<b><color=#{arrowColor}>{arrow}</color></b> " + label);
        }

        private static void Shadowed(Rect r, string text)
        {
            string plain = System.Text.RegularExpressions.Regex.Replace(text, "<color=[^>]+>|</color>", "");
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), "<color=#000000cc>" + plain + "</color>");
            GUI.Label(r, text);
        }

        // ---------- Друзья на карте игры ----------

        private static bool _mapLogged;

        private static bool DrawMap(string scene)
        {
            Panel_Map panel;
            try
            {
                if (!InterfaceManager.IsPanelEnabled<Panel_Map>()) return false;
                if (!InterfaceManager.TryGetPanel<Panel_Map>(out panel) || panel == null || panel.IsWorldMapActive()) return true;
            }
            catch { return false; }
            if (Session.Players.Count == 0) return true;
            try
            {
                // Показана ли сейчас карта нашей локации.
                bool shown = false;
                var maps = panel.m_MapObjects;
                if (maps != null)
                    for (int i = 0; i < maps.Count; i++)
                    {
                        var rm = maps[i];
                        if (rm == null || !string.Equals(rm.m_RegionName, scene, StringComparison.OrdinalIgnoreCase)) continue;
                        shown = rm.m_RegionMapObject != null && rm.m_RegionMapObject.activeInHierarchy;
                        break;
                    }
                if (!shown) return true;
                var root = panel.m_MapElementsTransform;
                var uiCam = UiCamera(root.gameObject.layer);
                if (uiCam == null) return true;
                _mapPanel = panel; _mapRoot = root; _mapCam = uiCam; _mapScene = scene; _mapFrame = Time.frameCount;
                foreach (var m in MapMarkers)
                {
                    if (m.Scene != scene) continue;
                    var mp = MapPoint(m.Pos);
                    float sx = mp.x, sy = Screen.height - mp.y;
                    string who = m.From == Session.LocalId ? "Моя метка" : m.Name.Replace("<", "‹");
                    Shadowed(new Rect(sx - 9f, sy - 15f, 30f, 30f), $"<size=22><b><color=#{Session.ColorOf(m.From, "7fe0ff")}>★</color></b></size>");
                    Shadowed(new Rect(sx + 12f, sy - 12f, 260f, 24f), "<b>" + who + "</b>: встречаемся здесь");
                }
                if (!Session.Sees(Shown.Map)) return true;
                foreach (var pl in Session.Players.Values)
                {
                    if (!Session.With(pl, scene)) continue;
                    var mp = panel.WorldPositionToMapPosition(scene, new Vector3(pl.X, pl.Y, pl.Z));
                    var sp = uiCam.WorldToScreenPoint(root.TransformPoint(mp));
                    float sx = sp.x, sy = Screen.height - sp.y;
                    Shadowed(new Rect(sx - 8f, sy - 14f, 30f, 30f), $"<size=20><b><color=#{Session.ColorOf(pl.Id, "ffcc55")}>●</color></b></size>");
                    Shadowed(new Rect(sx + 12f, sy - 12f, 200f, 24f), "<b>" + pl.Name.Replace("<", "‹") + "</b>");
                }
            }
            catch (Exception e)
            {
                if (!_mapLogged) { _mapLogged = true; MelonLogger.Warning("[карта] " + e.Message); }
            }
            return true;
        }

        private static Camera _uiCam;
        private static int _uiLayer = -1;

        private static Camera UiCamera(int layer)
        {
            if (_uiCam != null && _uiLayer == layer && _uiCam.isActiveAndEnabled) return _uiCam;
            _uiCam = null;
            foreach (var c in Camera.allCameras)
                if (c != null && (c.cullingMask & (1 << layer)) != 0 && c.orthographic) { _uiCam = c; break; }
            _uiLayer = layer;
            return _uiCam;
        }

        public static void Reset()
        {
            Drag.Reset();
            Incoming.Clear();
            Waiting.Clear();
            HostGifts.Clear();
            Returns.Clear();
            Bubbles.Clear();
            Markers.Clear();
            MapMarkers.Clear();
            CloseMenu(false);
            _pressAt = -1f;
        }
    }
}

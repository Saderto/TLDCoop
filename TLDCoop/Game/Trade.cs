using System;
using System.Collections.Generic;
using System.IO;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Обмен вещами. В рюкзаке рядом с другом (до 4 м) кнопка «Обмен с …». Друг соглашается, и пока у обоих
    // открыт рюкзак, сверху окно обмена: выбираешь вещь в рюкзаке игры и кладёшь в обмен (из стопки — сколько
    // нужно), видно, что даёшь ты и что даёт друг. Когда оба нажали «Согласен», каждый отдаёт свои вещи
    // (как «Отдать»). Любое изменение в обмене снимает оба «Согласен».
    public static class Trade
    {
        private const byte KInvite = 1, KAccept = 2, KDecline = 3, KCancel = 4, KOffer = 5, KAgree = 6, KReady = 7;
        private const float Range = 6f, InviteTime = 20f;

        private class Item { public GearItem Gi; public int Count; public string Label; }

        private static int _partner = -1;              // с кем идёт обмен
        private static int _inviteFrom = -1;           // кто зовёт нас
        private static float _inviteUntil, _invitedAt = -100f;
        private static int _invitedTo = -1;
        private static readonly List<Item> Mine = new();
        private static readonly List<string> Theirs = new();
        // Вещи друга, которые у нас не создадутся (нет DLC или мода): такой обмен отменяем до «готово», иначе
        // эта вещь вернулась бы другу, а наши вещи ушли бы — обмен в одну сторону.
        private static readonly List<bool> TheirsBad = new();
        private static int _myVer, _theirVer;
        private static bool _agreed, _theyAgreed;
        private static int _theyAgreedMine = -1, _theyAgreedTheirs = -1;
        // Второй шаг: оба согласны — каждый проверяет свои вещи и отвечает «готово». Отдаём, только когда «готово»
        // пришло от обоих на тех же предложениях. Раньше каждый решал сам: у одного всё есть, и он отдавал, а у другого
        // вещь уже съедена, обмен у него отменялся — первый отдал и ничего не получил.
        private static bool _ready, _theyReady;
        private static int _theyReadyMine = -1, _theyReadyTheirs = -1;
        // После своего «готово» обмен заперт: снять согласие, поменять вещи, отменить или уйти нельзя, пока не придёт
        // «готово» друга или его отмена. Раньше «готово» успевало уйти, друг отдавал, а у нас обмен снимался — вещи в одну сторону.
        // Номер согласия: «готово» друга засчитывается, только если он ответил на это же наше согласие (не на снятое раньше).
        private static int _agreeSeq, _theirAgreeSeq = -1, _theyReadySeq = -1;
        private static float _readyAt;
        private const float ReadyTimeout = 30f;
        private static bool Locked => Active && _ready;
        private static IntPtr _selItem;
        private static int _selCount;

        public static bool Active => _partner >= 0;
        public static bool PromptOpen => _inviteFrom >= 0 && Time.unscaledTime < _inviteUntil;

        private static string NameOf(int id) => Session.Players.TryGetValue(id, out var p) ? p.Name.Replace("<", "‹") : "друг";

        private static void Send(byte kind, int to, Action<BinaryWriter> body = null)
        {
            var w = Msg.New(MsgType.Trade);
            w.Write(kind);
            w.Write((byte)Session.LocalId);
            w.Write((byte)to);
            body?.Invoke(w);
            Session.SendGameEvent(Msg.Done(w));
        }

        // ---------- Начало и конец ----------

        public static void Invite(Player pl)
        {
            if (Active || pl == null) return;
            _invitedTo = pl.Id;
            _invitedAt = Time.unscaledTime;
            Send(KInvite, pl.Id);
            GameApi.HudMessage($"Предложил обмен: {pl.Name}. Ждём ответа");
        }

        private static void Begin(int partner)
        {
            _partner = partner;
            _inviteFrom = -1;
            _invitedTo = -1;
            Mine.Clear();
            Theirs.Clear();
            TheirsBad.Clear();
            _myVer = _theirVer = 0;
            _agreed = _theyAgreed = false;
            _theyAgreedMine = _theyAgreedTheirs = -1;
            _ready = _theyReady = false;
            _theirAgreeSeq = _theyReadySeq = -1;
            GameApi.HudMessage($"Обмен с {NameOf(partner)}: открой рюкзак");
            try { GameAudioManager.PlayGuiConfirm(); } catch { }
        }

        private static void End(string why)
        {
            if (!Active) return;
            _partner = -1;
            Mine.Clear();
            Theirs.Clear();
            TheirsBad.Clear();
            if (why != null) GameApi.HudMessage(why);
        }

        public static void Cancel(string why = "Обмен отменён", string toThem = null)
        {
            if (!Active) return;
            Send(KCancel, _partner, toThem == null ? null : w => w.Write(toThem));
            End(why);
        }

        // ---------- Своё предложение ----------

        private static void Changed()
        {
            _myVer++;
            _agreed = false;
            _ready = false;
            Send(KOffer, _partner, w =>
            {
                w.Write(_myVer);
                w.Write(Mine.Count);
                foreach (var it in Mine) w.Write(it.Label);
                foreach (var it in Mine) w.Write(it.Gi != null ? ItemSync.PrefabName(it.Gi) : "");   // в конце: проверить, создастся ли у друга
            });
            SendAgree();
        }

        private static void SendAgree() => Send(KAgree, _partner, w => { w.Write(_agreed); w.Write(_myVer); w.Write(_theirVer); w.Write(_agreeSeq); });

        private static bool StillHave(Item it)
        {
            try
            {
                if (it.Gi == null || !it.Gi.m_InPlayerInventory) return false;
                var pm = GameManager.GetPlayerManagerComponent();
                if (it.Gi.m_ClothingItem != null && pm != null && pm.IsWearingClothingItem(it.Gi)) return false;   // надел после того, как положил
                var st = it.Gi.m_StackableItem;
                return it.Count <= 0 || (st != null && st.m_Units >= it.Count);
            }
            catch { return false; }
        }

        private static void TryFinish()
        {
            if (!_agreed || !_theyAgreed || _theyAgreedMine != _myVer || _theyAgreedTheirs != _theirVer) { _ready = false; return; }
            if (!Session.Players.TryGetValue(_partner, out var pl)) { End("Обмен отменён: друг вышел"); return; }
            if (!_ready)
            {
                foreach (var it in Mine)
                    if (!StillHave(it)) { Cancel("Обмен отменён: вещи из обмена уже нет в рюкзаке (или она надета)"); return; }
                var bad = new List<string>();
                for (int i = 0; i < Theirs.Count && i < TheirsBad.Count; i++) if (TheirsBad[i]) bad.Add(Theirs[i]);
                if (bad.Count > 0)
                {
                    string list = string.Join(", ", bad);
                    Cancel("Обмен отменён: у тебя нет DLC или мода для: " + list, $"{Session.LocalName} не может взять: {list} (нет DLC или мода)");
                    return;
                }
                _ready = true;
                _readyAt = Time.unscaledTime;
                Send(KReady, _partner, w => { w.Write(_myVer); w.Write(_theirVer); w.Write(_theirAgreeSeq); });
            }
            if (!_theyReady || _theyReadyMine != _myVer || _theyReadyTheirs != _theirVer || _theyReadySeq != _agreeSeq) return;
            if (!GameApi.InGame()) return;   // на экране загрузки: отдадим, когда загрузимся (Tick)
            // Друг уже отдаёт своё, поэтому и мы отдаём, что осталось (отмена здесь оставила бы его без вещей).
            var give = Mine.FindAll(StillHave);
            int n = give.Count, got = Theirs.Count, lost = Mine.Count - give.Count;
            End(null);
            foreach (var it in give) Together.Give(null, it.Gi, pl, it.Count);
            GameApi.HudMessage($"Обмен с {pl.Name} состоялся: отдал {n}, получаешь {got}" + (lost > 0 ? $" (вещей уже нет: {lost})" : ""));
            MelonLogger.Msg($"[обмен] с {pl.Name}: отдал {n}, получаю {got}" + (lost > 0 ? $", не нашёл {lost}" : ""));
        }

        // ---------- Сеть ----------

        public static void Receive(BinaryReader r)
        {
            byte kind = r.ReadByte();
            int from = r.ReadByte(), to = r.ReadByte();
            if (to != Session.LocalId) return;
            if (!GameApi.InGame() && !(Locked && from == _partner)) return;   // запертый обмен ждёт ответа и на загрузке
            switch (kind)
            {
                case KInvite:
                    if (Active) { Send(KDecline, from); return; }
                    _inviteFrom = from;
                    _inviteUntil = Time.unscaledTime + InviteTime;
                    try { GameAudioManager.PlayGuiConfirm(); } catch { }
                    break;
                case KAccept:
                    if (_invitedTo == from && Time.unscaledTime - _invitedAt < InviteTime + 5f && !Active) Begin(from);
                    else Send(KCancel, from);
                    break;
                case KDecline:
                    if (_invitedTo == from) { _invitedTo = -1; GameApi.HudMessage($"{NameOf(from)} отказался от обмена"); }
                    break;
                case KCancel:
                {
                    string why = r.BaseStream.Position < r.BaseStream.Length ? r.ReadString() : null;
                    if (_partner == from) End(why != null ? "Обмен отменён: " + why : $"{NameOf(from)} отменил обмен");
                    if (_inviteFrom == from) _inviteFrom = -1;
                    break;
                }
                case KOffer:
                {
                    if (_partner != from) return;
                    _theirVer = r.ReadInt32();
                    int n = r.ReadInt32();
                    Theirs.Clear();
                    for (int i = 0; i < n; i++) Theirs.Add(r.ReadString());
                    TheirsBad.Clear();
                    for (int i = 0; i < n && r.BaseStream.Position < r.BaseStream.Length; i++) TheirsBad.Add(!ItemSync.Creatable(r.ReadString()));
                    if (_agreed) { _agreed = false; SendAgree(); }
                    _theyAgreed = false;
                    _ready = _theyReady = false;
                    break;
                }
                case KReady:
                    if (_partner != from) return;
                    _theyReady = true;
                    _theyReadyTheirs = r.ReadInt32();
                    _theyReadyMine = r.ReadInt32();
                    _theyReadySeq = r.ReadInt32();
                    TryFinish();
                    break;
                case KAgree:
                    if (_partner != from) return;
                    _theyAgreed = r.ReadBoolean();
                    _theyAgreedTheirs = r.ReadInt32();
                    _theyAgreedMine = r.ReadInt32();
                    _theirAgreeSeq = r.ReadInt32();
                    if (!_theyAgreed) _theyReady = false;
                    TryFinish();
                    break;
            }
        }

        // ---------- Каждый кадр ----------

        public static void Tick()
        {
            if (!Active) return;
            if (!Session.Players.TryGetValue(_partner, out var pl)) { End("Обмен отменён"); return; }
            if (Locked)
            {
                // Ждём «готово» или отмену друга; дальше расстояния и загрузки не отменяем.
                // «Готово» друга уже пришло (например, на загрузке) — он отдаёт, отменять нельзя.
                bool match = _theyReady && _theyReadyMine == _myVer && _theyReadyTheirs == _theirVer && _theyReadySeq == _agreeSeq;
                if (!match && Time.unscaledTime - _readyAt > ReadyTimeout) { Cancel("Обмен отменён: друг не ответил"); return; }
                if (GameApi.InGame() && _theyReady) TryFinish();
                return;
            }
            if (!GameApi.InGame()) { End("Обмен отменён"); return; }
            if (!GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _)) return;
            if (!Session.With(pl, GameApi.Scene()) || (new Vector3(pl.X, pl.Y, pl.Z) - new Vector3(x, y, z)).sqrMagnitude > Range * Range)
                Cancel("Обмен отменён: вы слишком далеко друг от друга");
        }

        // ---------- Рисование ----------

        // Приглашение — в любом месте экрана.
        public static void DrawPrompt()
        {
            if (!PromptOpen) { if (_inviteFrom >= 0 && Time.unscaledTime >= _inviteUntil) { Send(KDecline, _inviteFrom); _inviteFrom = -1; } return; }
            UI.Theme.Apply();
            float w = 420f, h = 110f, x = (Screen.width - w) / 2f, y = Screen.height * 0.25f;
            UI.Widgets.Panel(new Rect(x, y, w, h), "Обмен");
            UI.Theme.Centered(new Rect(x, y + 30f, w, 24f), $"{NameOf(_inviteFrom)} предлагает обмен вещами");
            if (UI.Widgets.Button(new Rect(x + 20f, y + 64f, w / 2f - 30f, 30f), "Да"))
            {
                int from = _inviteFrom;
                Send(KAccept, from);
                Begin(from);
            }
            if (UI.Widgets.Button(new Rect(x + w / 2f + 10f, y + 64f, w / 2f - 30f, 30f), "Нет"))
            {
                Send(KDecline, _inviteFrom);
                _inviteFrom = -1;
            }
        }

        // Окно обмена внизу экрана, пока открыт рюкзак. Возвращает true, если нарисовано (тогда «Отдать» не нужен).
        public static bool DrawInInventory(Panel_Inventory panel)
        {
            if (!Active) return false;
            // Под сеткой вещей, у нижнего края: сверху окно закрывало вкладки разделов рюкзака.
            int rows = Math.Max(Math.Max(Mine.Count, Theirs.Count), 1);
            float h = 132f + rows * 22f;
            float W = 560f, x0 = Mathf.Max(16f, Screen.width * 0.345f - W / 2f), y = Mathf.Max(10f, Screen.height - 10f - h);
            UI.Theme.Fill(new Rect(x0 - 6f, y - 4f, W + 12f, h), UI.Theme.PanelBg);
            UI.Theme.Frame(new Rect(x0 - 6f, y - 4f, W + 12f, h), UI.Theme.Border);
            GUI.Label(new Rect(x0, y, W, 22f), $"<b>Обмен с {NameOf(_partner)}</b>");
            y += 24f;

            // Вещь, выбранная в рюкзаке игры.
            GearItem gi = null;
            try { gi = panel.GetCurrentlySelectedItem()?.m_GearItem; } catch { }
            if (gi != null)
            {
                int units = gi.m_StackableItem != null ? Math.Max(1, gi.m_StackableItem.m_Units) : 1;
                if (gi.Pointer != _selItem) { _selItem = gi.Pointer; _selCount = units; }
                _selCount = Mathf.Clamp(_selCount, 1, units);
                bool already = Mine.Exists(m => m.Gi != null && m.Gi.Pointer == gi.Pointer);
                string what = (gi.DisplayName ?? ItemSync.PrefabName(gi)) + (units > 1 ? $" ×{_selCount}" : "");
                float bx = x0;
                if (units > 1)
                {
                    if (UI.Widgets.Button(new Rect(bx, y, 28f, 24f), "−")) _selCount = Math.Max(1, _selCount - 1);
                    UI.Theme.Centered(new Rect(bx + 30f, y, 70f, 24f), $"{_selCount}/{units}");
                    if (UI.Widgets.Button(new Rect(bx + 102f, y, 28f, 24f), "+")) _selCount = Math.Min(units, _selCount + 1);
                    bx += 136f;
                }
                if (Locked) GUI.Label(new Rect(bx, y, x0 + W - bx, 24f), "<i>ждём «готово» друга…</i>");
                else if (!already && UI.Widgets.Button(new Rect(bx, y, x0 + W - bx, 24f), UI.Widgets.Fit($"Положить в обмен «{what}»", x0 + W - bx)))
                {
                    var pm = GameManager.GetPlayerManagerComponent();
                    if (gi.m_ClothingItem != null && pm != null && pm.IsWearingClothingItem(gi)) GameApi.HudMessage("Сначала сними эту одежду");
                    else
                    {
                        Mine.Add(new Item { Gi = gi, Count = units > 1 && _selCount < units ? _selCount : 0, Label = what });
                        Changed();
                    }
                }
                else if (already) GUI.Label(new Rect(bx, y, x0 + W - bx, 24f), "<i>эта вещь уже в обмене</i>");
            }
            else GUI.Label(new Rect(x0, y, W, 24f), "<i>Выбери вещь в рюкзаке, чтобы положить её в обмен</i>");
            y += 30f;

            float half = W / 2f - 6f;
            GUI.Label(new Rect(x0, y, half, 22f), "<b>Ты даёшь</b>" + (_agreed ? "  <color=#8fe08f>✓ согласен</color>" : ""));
            GUI.Label(new Rect(x0 + half + 12f, y, half, 22f), $"<b>{NameOf(_partner)} даёт</b>" + (_theyAgreed ? "  <color=#8fe08f>✓ согласен</color>" : ""));
            y += 22f;
            int remove = -1;
            for (int i = 0; i < rows; i++)
            {
                if (i < Mine.Count)
                {
                    GUI.Label(new Rect(x0 + 4f, y, half - 34f, 22f), UI.Widgets.Fit(Mine[i].Label, half - 34f));
                    if (!Locked && UI.Widgets.Button(new Rect(x0 + half - 28f, y, 26f, 20f), "×")) remove = i;
                }
                else if (i == 0) GUI.Label(new Rect(x0 + 4f, y, half, 22f), "<i>ничего</i>");
                if (i < Theirs.Count) GUI.Label(new Rect(x0 + half + 16f, y, half - 4f, 22f), UI.Widgets.Fit(Theirs[i], half - 4f) + (i < TheirsBad.Count && TheirsBad[i] ? " <color=#ff8080>(у тебя не создастся)</color>" : ""));
                else if (i == 0) GUI.Label(new Rect(x0 + half + 16f, y, half, 22f), "<i>ничего</i>");
                y += 22f;
            }
            if (remove >= 0) { Mine.RemoveAt(remove); Changed(); }
            y += 6f;
            if (Locked) { UI.Theme.Centered(new Rect(x0, y, W, 30f), "Отдаём… ждём ответа друга"); return true; }
            if (UI.Widgets.Button(new Rect(x0, y, half, 30f), _agreed ? "Не согласен" : "Согласен"))
            {
                if (!_agreed && Mine.Count == 0 && Theirs.Count == 0) GameApi.HudMessage("Обмен пустой");
                else
                {
                    _agreed = !_agreed;
                    if (_agreed) _agreeSeq++;
                    SendAgree();
                    TryFinish();
                }
            }
            if (UI.Widgets.Button(new Rect(x0 + half + 12f, y, half, 30f), "Отменить обмен")) Cancel();
            return true;
        }

        public static void Reset()
        {
            _partner = -1;
            _inviteFrom = -1;
            _invitedTo = -1;
            Mine.Clear();
            Theirs.Clear();
            TheirsBad.Clear();
        }
    }
}

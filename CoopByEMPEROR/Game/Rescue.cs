using System;
using System.Collections.Generic;
using System.IO;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Поднять раненого друга (Game/Death.cs, шаг «ранен»). Раненый сообщает всем, от чего упал, а тому,
    // кто поднимает, по этой причине нужен лечебный лут в рюкзаке: после падения — два бинта, после зверя —
    // бинт и антисептик, от холода — горячий напиток и т. д. (если хост не выключил это правило).
    // Лечение — держать клавишу метки (по умолчанию колёсико) рядом с раненым 4 секунды. Пока его лечат,
    // таймер раненого стоит. Лут тратится, когда лечение закончено.
    public static class Rescue
    {
        public const float TreatSeconds = 4f, Range = 2.5f;

        public enum Need : byte { None, Fall, Wound, Burn, Infection, Stomach, Cold, Hunger, Thirst, Other }

        public static Need FromCause(DamageSource s) => s switch
        {
            DamageSource.Falling or DamageSource.WeakJoints => Need.Fall,
            DamageSource.Wolf or DamageSource.Bear or DamageSource.Cougar or DamageSource.BloodLoss
                or DamageSource.BulletWound or DamageSource.Player => Need.Wound,
            DamageSource.Burns or DamageSource.Electrical => Need.Burn,
            DamageSource.Infection => Need.Infection,
            DamageSource.FoodPoisoning or DamageSource.Dysentery or DamageSource.IntestinalParasites => Need.Stomach,
            DamageSource.Hypothermia or DamageSource.Freezing or DamageSource.FrostBite or DamageSource.IceCracking => Need.Cold,
            DamageSource.Starving or DamageSource.Scurvy => Need.Hunger,
            DamageSource.Dehydrated => Need.Thirst,
            DamageSource.ToxicFog or DamageSource.Suffocating => Need.None,
            _ => Need.Other,
        };

        // Одна строка требования: сколько штук и какие вещи подходят.
        private class Req
        {
            public string Label;
            public int Count;
            public Func<GearItem, bool> Fits;
        }

        private static bool Named(GearItem gi, params string[] names)
        {
            string n = ItemSync.PrefabName(gi);
            foreach (var x in names) if (string.Equals(n, x, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static readonly Func<GearItem, bool> Bandage = gi => Named(gi, "GEAR_HeavyBandage", "GEAR_OldMansBeardDressing");
        private static readonly Func<GearItem, bool> Antiseptic = gi => Named(gi, "GEAR_BottleHydrogenPeroxide", "GEAR_OldMansBeardDressing");
        private static readonly Func<GearItem, bool> Antibiotic = gi => Named(gi, "GEAR_BottleAntibiotics", "GEAR_ReishiTea");
        private static readonly Func<GearItem, bool> Painkiller = gi => Named(gi, "GEAR_BottlePainKillers", "GEAR_BirchbarkTea");
        private static readonly Func<GearItem, bool> HotDrink = gi =>
        {
            if (gi.m_FoodItem == null || !gi.m_FoodItem.m_IsDrink) return false;
            string n = ItemSync.PrefabName(gi);
            return n.IndexOf("Tea", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Coffee", StringComparison.OrdinalIgnoreCase) >= 0;
        };
        private static readonly Func<GearItem, bool> Food = gi => gi.m_FoodItem != null && !gi.m_FoodItem.m_IsDrink;
        private static readonly Func<GearItem, bool> Drink = gi => gi.m_FoodItem != null && gi.m_FoodItem.m_IsDrink;
        private static readonly Func<GearItem, bool> Infect = gi => Antiseptic(gi) || Antibiotic(gi);

        private static List<Req> Reqs(Need n) => n switch
        {
            Need.Fall => new() { new Req { Label = UI.L.T("бинт (медицинский или из лишайника)", "bandage (medical or old man's beard)"), Count = 2, Fits = Bandage } },
            Need.Wound => new()
            {
                new Req { Label = UI.L.T("бинт", "bandage"), Count = 1, Fits = Bandage },
                new Req { Label = UI.L.T("антисептик", "antiseptic"), Count = 1, Fits = gi => Named(gi, "GEAR_BottleHydrogenPeroxide") },
            },
            Need.Burn => new()
            {
                new Req { Label = UI.L.T("бинт", "bandage"), Count = 1, Fits = Bandage },
                new Req { Label = UI.L.T("обезболивающее (или чай из бересты)", "painkillers (or birch bark tea)"), Count = 1, Fits = Painkiller },
            },
            Need.Infection => new() { new Req { Label = UI.L.T("антисептик или антибиотики", "antiseptic or antibiotics"), Count = 1, Fits = Infect } },
            Need.Stomach => new() { new Req { Label = UI.L.T("антибиотики (или чай из трутовика)", "antibiotics (or reishi tea)"), Count = 1, Fits = Antibiotic } },
            Need.Cold => new() { new Req { Label = UI.L.T("горячий чай или кофе", "hot tea or coffee"), Count = 1, Fits = HotDrink } },
            Need.Hunger => new() { new Req { Label = UI.L.T("еда", "food"), Count = 1, Fits = Food } },
            Need.Thirst => new() { new Req { Label = UI.L.T("напиток или 0,5 л питьевой воды", "a drink or 0.5 L of potable water"), Count = 1, Fits = Drink } },
            Need.Other => new() { new Req { Label = UI.L.T("бинт", "bandage"), Count = 1, Fits = Bandage } },
            _ => new(),
        };

        public static string NeedText(Need n)
        {
            var parts = new List<string>();
            foreach (var req in Reqs(n)) parts.Add(req.Count > 1 ? $"{req.Label} ×{req.Count}" : req.Label);
            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        public static string Why(Need n) => n switch
        {
            Need.Fall => "разбился при падении",
            Need.Wound => "истекает кровью",
            Need.Burn => "обгорел",
            Need.Infection => "заражение",
            Need.Stomach => "отравление",
            Need.Cold => "замерзает",
            Need.Hunger => "от голода",
            Need.Thirst => "от жажды",
            Need.None => "задохнулся",
            _ => "без сил",
        };

        private static int Units(GearItem gi) => gi.m_StackableItem != null ? Math.Max(1, gi.m_StackableItem.m_Units) : 1;

        private static List<GearItem> Inventory()
        {
            var list = new List<GearItem>();
            var inv = GameManager.GetInventoryComponent();
            if (inv == null) return list;
            foreach (var o in inv.m_Items)
            {
                var gi = o?.m_GearItem;
                if (gi != null) list.Add(gi);
            }
            return list;
        }

        private static float PotableLiters()
        {
            try
            {
                var ws = GameManager.GetInventoryComponent()?.m_WaterSupplyPotable?.m_WaterSupply;
                return ws == null ? 0f : ws.m_VolumeInLiters.m_Units / (float)Il2CppTLD.IntBackedUnit.ItemLiquidVolume.Liter.m_Units;
            }
            catch { return 0f; }
        }

        // Хватает ли лута. text — что нужно и сколько есть, для подсказки.
        public static bool Have(Need n, out string text)
        {
            if (!Session.CurrentRules.ReviveItems || n == Need.None) { text = ""; return true; }
            var items = Inventory();
            var used = new HashSet<IntPtr>();
            bool ok = true;
            var parts = new List<string>();
            foreach (var req in Reqs(n))
            {
                int have = 0;
                foreach (var gi in items)
                {
                    if (have >= req.Count || used.Contains(gi.Pointer) || !req.Fits(gi)) continue;
                    have += Units(gi);
                    used.Add(gi.Pointer);
                }
                if (n == Need.Thirst && have < req.Count && PotableLiters() >= 0.5f) have = req.Count;
                have = Math.Min(have, req.Count);
                if (have < req.Count) ok = false;
                parts.Add($"{req.Label} {have}/{req.Count}");
            }
            text = string.Join(", ", parts);
            return ok;
        }

        // Тратим лут после лечения.
        private static void Consume(Need n)
        {
            if (!Session.CurrentRules.ReviveItems || n == Need.None) return;
            var inv = GameManager.GetInventoryComponent();
            if (inv == null) return;
            foreach (var req in Reqs(n))
            {
                int left = req.Count;
                foreach (var gi in Inventory())
                {
                    if (left <= 0) break;
                    if (gi == null || !req.Fits(gi)) continue;
                    int u = Units(gi);
                    if (u > left && gi.m_StackableItem != null)
                    {
                        gi.m_StackableItem.m_Units = u - left;
                        left = 0;
                    }
                    else
                    {
                        left -= u;
                        inv.DestroyGear(gi);
                    }
                }
                if (left > 0 && n == Need.Thirst)
                {
                    try
                    {
                        var ws = inv.m_WaterSupplyPotable?.m_WaterSupply;
                        if (ws != null) ws.m_VolumeInLiters = ws.m_VolumeInLiters - Il2CppTLD.IntBackedUnit.ItemLiquidVolume.HalfLiter;
                    }
                    catch { }
                }
            }
        }

        // ---------- Раненый ----------

        private static readonly Dictionary<int, Need> DownedNeeds = new();
        private static readonly Dictionary<int, float> TreatedBy = new();   // кто сейчас лечит меня → до какого времени
        private static float _nextInfo;

        public static Need NeedOf(int id) => DownedNeeds.TryGetValue(id, out var n) ? n : Need.Other;

        // Раненый раз в 2 секунды напоминает всем, от чего упал (новичок в локации тоже узнает).
        public static void DownedTick(Need need)
        {
            float now = Time.unscaledTime;
            if (now < _nextInfo) return;
            _nextInfo = now + 2f;
            var w = Msg.New(MsgType.DownedInfo);
            w.Write((byte)Session.LocalId);
            w.Write((byte)need);
            Session.SendGameEvent(Msg.Done(w));
        }

        public static void ReceiveInfo(BinaryReader r)
        {
            int from = r.ReadByte();
            DownedNeeds[from] = (Need)r.ReadByte();
        }

        // Меня сейчас лечат — таймер раненого стоит.
        public static string TreatedByName()
        {
            float now = Time.unscaledTime;
            foreach (var kv in TreatedBy)
                if (now < kv.Value) return Session.Players.TryGetValue(kv.Key, out var p) ? p.Name : "друг";
            return null;
        }

        public static void ReceiveHold(BinaryReader r)
        {
            int from = r.ReadByte();
            int target = r.ReadByte();
            bool on = r.ReadBoolean();
            if (target != Session.LocalId) return;
            if (on) TreatedBy[from] = Time.unscaledTime + 1.2f; else TreatedBy.Remove(from);
        }

        // ---------- Тот, кто лечит ----------

        private static int _target = -1;
        private static float _progress, _nextHold;
        private static string _hint = "";

        public static bool Treating => _target >= 0;
        public static float Progress => _progress / TreatSeconds;
        public static string Hint => _hint;

        // held — клавиша зажата рядом с раненым. Возвращает true, пока занимаемся лечением.
        public static void Tick(Player downed, bool pressed, bool held, float dt)
        {
            if (downed == null) { Stop(); _hint = ""; return; }
            var need = NeedOf(downed.Id);
            bool ok = Have(need, out string what);
            string name = downed.Name.Replace("<", "‹");
            if (!ok)
            {
                Stop();
                _hint = $"<b>{name} ранен ({Why(need)}).</b> Нужно: {what}";
                if (pressed) GameApi.HudMessage("Не хватает: " + what);
                return;
            }
            if (!held)
            {
                Stop();
                _hint = $"<b>{name} ранен ({Why(need)}).</b> Держи {Config.PingKeyName}, чтобы лечить" + (what.Length > 0 ? $" ({what})" : "");
                return;
            }
            if (_target != downed.Id) { Stop(); _target = downed.Id; _progress = 0f; }
            Activity.Override = Activity.Treating;
            _progress += dt;
            _hint = $"<b>Лечишь {name}…</b>";
            float now = Time.unscaledTime;
            if (now >= _nextHold)
            {
                _nextHold = now + 0.4f;
                SendHold(downed.Id, true);
            }
            if (_progress < TreatSeconds) return;
            Consume(need);
            var w = Msg.New(MsgType.Revive);
            w.Write((byte)Session.LocalId);
            w.Write((byte)downed.Id);
            Session.SendGameEvent(Msg.Done(w));
            GameApi.HudMessage("Поднял " + downed.Name);
            MelonLogger.Msg($"[смерть] поднял {downed.Name} ({need})");
            DownedNeeds.Remove(downed.Id);
            Stop();
        }

        private static void SendHold(int target, bool on)
        {
            var w = Msg.New(MsgType.ReviveHold);
            w.Write((byte)Session.LocalId);
            w.Write((byte)target);
            w.Write(on);
            Session.SendGameEvent(Msg.Done(w));
        }

        private static void Stop()
        {
            if (_target >= 0)
            {
                SendHold(_target, false);
                if (Activity.Override == Activity.Treating) Activity.Override = 0;
            }
            _target = -1;
            _progress = 0f;
        }

        public static void Reset()
        {
            Stop();
            DownedNeeds.Clear();
            TreatedBy.Clear();
            _hint = "";
        }
    }
}

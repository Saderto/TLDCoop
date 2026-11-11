using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2CppTLD.OptionalContent;
using MelonLoader;
using TLDCoop.Net;

namespace TLDCoop.Game
{
    // При подключении хост и друг обмениваются списком DLC и модов. Подключению это никогда
    // не мешает, только подсказывает. Моды «для себя» (трейнер и т. п.) не учитываются вовсе:
    // сравниваются лишь моды, которые сами кладут в мир вещи или лут, — из-за них мир у игроков
    // может отличаться, а их вещи у друга без мода могут не появиться.
    public static class ContentCheck
    {
        // Части имён модов, которые добавляют в мир вещи или меняют лут.
        private static readonly string[] WorldModHints = { "GearSpawner", "ModComponent", "Loot", "Spawn", "Gear", "Item", "Food", "Seagull" };

        // Друг: DLC и моды мира, которые есть у хоста, а у меня нет (последний список хоста). Бот с такими пробелами
        // проверки и походы не делает (Game/Bot.cs).
        public static List<string> MissingHere { get; private set; } = new();

        // Хост: друзья, у которых нет DLC или модов мира хоста (бот: TLD-bot.bat копирует только TLDCoop). В их сохранении
        // локации и первой раскладке нет вещей этих модов — хост их не берёт (Game/SceneHandoff.cs, Game/SceneSnapshot.cs),
        // иначе они стёрли бы вещи у него.
        private static readonly HashSet<int> LacksHost = new();
        public static bool LacksHostContent(int peerId) => LacksHost.Contains(peerId);
        public static void Forget(int peerId) => LacksHost.Remove(peerId);

        public static void Reset()
        {
            LacksHost.Clear();
            MissingHere = new();
        }

        public static List<string> MyDlc()
        {
            var list = new List<string>();
            try
            {
                var installed = OptionalContentManager.Instance?.InstalledContent;
                if (installed != null)
                    foreach (var kv in installed) list.Add(kv.Key);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[содержимое] DLC: " + e.Message);
            }
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        // Имя и версия отдельно: из одной строки «Имя версия» версию не всегда отделить (v1.2, «Survival 2»).
        public static List<(string Name, string Ver)> MyWorldMods()
        {
            var list = new List<(string Name, string Ver)>();
            foreach (var m in MelonMod.RegisteredMelons)
            {
                string name = m.Info?.Name ?? "";
                if (name.Length == 0 || m.Info.SystemType == typeof(CoopMod)) continue;
                if (WorldModHints.Any(h => name.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0))
                    list.Add((name, m.Info.Version ?? ""));
            }
            list.Sort((a, b) => { int c = string.CompareOrdinal(a.Name, b.Name); return c != 0 ? c : string.CompareOrdinal(a.Ver, b.Ver); });
            return list;
        }

        private static string Show((string Name, string Ver) m) => m.Ver.Length > 0 ? m.Name + " " + m.Ver : m.Name;
        private static string Ver(string v) => v.Length > 0 ? v : "без версии";

        private static Dictionary<string, string> ByName(List<(string Name, string Ver)> mods)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var m in mods) d.TryAdd(m.Name, m.Ver);   // имя дважды — берём первое (списки отсортированы одинаково)
            return d;
        }

        public static byte[] Build()
        {
            var w = Msg.New(MsgType.Content);
            var dlc = MyDlc();
            var mods = MyWorldMods();
            w.Write(dlc.Count); foreach (var d in dlc) w.Write(d);
            w.Write(mods.Count); foreach (var m in mods) { w.Write(m.Name); w.Write(m.Ver); }
            return Msg.Done(w);
        }

        // Пришёл список другого игрока. iAmHost: сравниваем с точки зрения хоста.
        public static void Receive(BinaryReader r, string otherName, bool iAmHost, int peerId = -1)
        {
            var theirDlc = new List<string>();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++) theirDlc.Add(r.ReadString());
            var theirMods = new List<(string Name, string Ver)>();
            n = r.ReadInt32();
            for (int i = 0; i < n; i++) { string name = r.ReadString(); theirMods.Add((name, r.ReadString())); }
            theirMods.Sort((a, b) => { int c = string.CompareOrdinal(a.Name, b.Name); return c != 0 ? c : string.CompareOrdinal(a.Ver, b.Ver); });

            var myDlc = MyDlc();
            var myMods = MyWorldMods();
            MelonLogger.Msg($"[содержимое] {otherName}: DLC [{string.Join(", ", theirDlc)}], моды мира [{string.Join(", ", theirMods.Select(Show))}]; у меня DLC [{string.Join(", ", myDlc)}], моды мира [{string.Join(", ", myMods.Select(Show))}]");

            var hostDlc = iAmHost ? myDlc : theirDlc;
            var friendDlc = iAmHost ? theirDlc : myDlc;
            var hostMods = iAmHost ? myMods : theirMods;
            var friendMods = iAmHost ? theirMods : myMods;
            string friend = iAmHost ? otherName : "тебя";
            string him = iAmHost ? "него" : "тебя";
            bool any = false;

            // У хоста есть, у друга нет: вещи и регионы мира хоста у друга не появятся.
            var missingDlc = hostDlc.Except(friendDlc).ToList();
            if (!iAmHost) MissingHere = missingDlc.ToList();
            if (missingDlc.Count > 0)
            {
                Session.Log($"Подсказка: у {friend} нет DLC {string.Join(", ", missingDlc)}. Играть можно, но регионы и вещи этого DLC у {him} работать не будут.");
                GameApi.HudMessage("Кооп: разный набор DLC (подробности в окне мода)");
            }
            // У друга есть, у хоста нет: вещи этого DLC, которые друг отдаст, положит в ящик или в локацию, где он первый,
            // хост создать не сможет, и они пропадут.
            var extraDlc = friendDlc.Except(hostDlc).ToList();
            if (extraDlc.Count > 0)
            {
                Session.Log($"Подсказка: у {friend} есть DLC {string.Join(", ", extraDlc)}, которого нет у хоста. Вещи этого DLC (и вещи модов, сделанные на его основе), которые {(iAmHost ? "он принесёт" : "ты принесёшь")}, у хоста не появятся и пропадут.");
                if (missingDlc.Count == 0) GameApi.HudMessage("Кооп: разный набор DLC (подробности в окне мода)");
            }

            // Моды сравниваем по точному имени, версии — отдельно (любое различие версий — подсказка).
            var hostByName = ByName(hostMods);
            var friendByName = ByName(friendMods);
            var missingMods = hostByName.Keys.Where(k => !friendByName.ContainsKey(k)).ToList();
            if (!iAmHost) MissingHere.AddRange(missingMods);
            var extraMods = friendByName.Keys.Where(k => !hostByName.ContainsKey(k)).ToList();
            var otherVersion = hostByName.Keys.Where(k => friendByName.TryGetValue(k, out var f) && !string.Equals(f, hostByName[k], StringComparison.Ordinal))
                .Select(k => $"{k} (у хоста {Ver(hostByName[k])}, у {(iAmHost ? otherName : "тебя")} {Ver(friendByName[k])})").ToList();
            if (missingMods.Count > 0)
            {
                Session.Log($"Подсказка: у {friend} нет модов, которые добавляют вещи в мир: {string.Join(", ", missingMods)}. Играть можно, но эти вещи у {him} могут не появиться.");
                any = true;
            }
            if (extraMods.Count > 0)
            {
                Session.Log($"Подсказка: у {friend} есть моды с вещами, которых нет у хоста: {string.Join(", ", extraMods)}. Их вещи, которые {(iAmHost ? "он отдаст, положит в ящик или в новую локацию" : "ты отдашь, положишь в ящик или в новую локацию")}, у хоста не появятся и пропадут. Поставьте такие моды обоим или уберите у друга.");
                any = true;
            }
            if (otherVersion.Count > 0)
            {
                Session.Log($"Подсказка: разные версии модов: {string.Join("; ", otherVersion)}. Поставьте обоим одну версию.");
                any = true;
            }
            if (any) GameApi.HudMessage("Кооп: разные моды или их версии (подробности в окне мода)");

            if (iAmHost && peerId >= 0)
            {
                if (missingDlc.Count + missingMods.Count == 0) LacksHost.Remove(peerId);
                else if (LacksHost.Add(peerId))
                {
                    Session.Log($"У {otherName} нет DLC или модов хоста: его сохранения локаций и раскладки новых локаций не беру — в них нет вещей этих модов.");
                    GameApi.HudMessage($"Кооп: у {otherName} нет модов хоста — его локации не беру");
                }
            }
        }
    }
}

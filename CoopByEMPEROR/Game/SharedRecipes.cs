using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Il2CppTLD.Cooking;
using MelonLoader;
using CoopByEMPEROR.Net;

namespace CoopByEMPEROR.Game
{
    // Рецепты блюд общие: кто бы ни выучил рецепт (карточка, книга), он появляется в книге рецептов у всех.
    //  - выучил рецепт (RecipeBook.UnlockRecipe вернул true) — имя рецепта уходит хосту, хост рассылает всем;
    //  - друг, войдя в мир хоста, присылает все свои рецепты, хост отвечает всеми общими;
    //  - хост хранит общие рецепты мира в progress/<мир>/recipes.txt, их получит и тот, кто зайдёт позже;
    //  - открытые трейнером хоста (Game/TrainerLink.cs) не общие: они в progress/<мир>/recipes_cheat.txt.
    // Книга рецептов у каждого в своём прогрессе (m_RecipeBookState), так что выученное сохраняется как обычно.
    public static class SharedRecipes
    {
        private static readonly HashSet<string> Known = new();     // хост: общие рецепты мира
        private static readonly HashSet<string> ToApply = new();   // ждут, пока книга рецептов загрузится
        private static readonly HashSet<string> Cheat = new();     // хост: открытые трейнером, друзьям не отдаём
        private static string _cheatFor;
        private static bool _applying, _sentOwn, _hostMerged;
        private static string _loadedFor;
        private static float _inWorldSince = -1f, _nextTry;

        private static RecipeBook Book()
        {
            try { var b = RecipeBook.Instance; return b != null && b.AllRecipes != null && b.AllRecipes.Count > 0 ? b : null; }
            catch { return null; }
        }

        private static List<string> OwnUnlocked()
        {
            var res = new List<string>();
            var b = Book();
            if (b == null) return res;
            foreach (var r in b.AllRecipes)
                if (r != null && b.IsRecipeUnlocked(r)) res.Add(r.name);
            // И выученные по имени, чьих рецептов в книге ещё нет (моды, например FoodPack, добавляют свои позже).
            try
            {
                var names = b.m_GameState?.m_UnlockedRecipes;
                if (names != null)
                    for (int i = 0; i < names.Count; i++)
                        if (!string.IsNullOrEmpty(names[i]) && !res.Contains(names[i])) res.Add(names[i]);
            }
            catch { }
            return res;
        }

        // Игра: рецепт выучен.
        internal static void OnUnlocked(RecipeData recipe, bool result)
        {
            if (!result || _applying || recipe == null) return;
            if (TrainerLink.Unlocking)
            {
                // Трейнер (и в одиночной игре этого мира): запоминаем, чтобы не отдать при следующем входе.
                if (Session.Mode != Mode.Client) { LoadCheat(); if (Cheat.Add(recipe.name)) SaveCheat(); }
                MelonLogger.Msg($"[рецепты] {recipe.name} открыт трейнером, друзьям не отправляю");
                return;
            }
            if (!Session.InSharedWorld) return;
            string name = recipe.name;
            MelonLogger.Msg($"[рецепты] выучил {name}, отправляю всем");
            if (Session.Mode == Mode.Host) { Load(); if (Known.Add(name)) Save(); }   // Load: мир мог смениться
            Send(new[] { name });
        }

        private static void Send(IEnumerable<string> names, int to = -1)
        {
            var list = names.ToList();
            if (list.Count == 0) return;
            var w = Msg.New(MsgType.Recipes);
            w.Write(list.Count);
            foreach (var n in list) w.Write(n);
            var data = Msg.Done(w);
            if (to >= 0) Session.SendTo(to, data); else Session.SendGameEvent(data);
        }

        // Хост: рецепты от друга. Новые — себе и всем, а самому другу — все общие (он мог прийти без них).
        public static void HostReceive(Peer peer, BinaryReader r)
        {
            var names = Read(r);
            bool wantAll = names.RemoveAll(n => n.Length == 0) > 0;
            Load();
            // Друг выучил сам — уже не только трейнерный.
            LoadCheat();
            if (names.Count(n => Cheat.Remove(n)) > 0) SaveCheat();
            var fresh = names.Where(n => Known.Add(n)).ToList();
            if (fresh.Count > 0)
            {
                Save();
                Apply(fresh, "игрок " + peer.Id);
                Send(fresh);
            }
            if (wantAll && Known.Count > 0) Send(Known, peer.Id);
        }

        // Друг: рецепты от хоста.
        public static void ClientReceive(BinaryReader r) => Apply(Read(r), "хост");

        private static List<string> Read(BinaryReader r)
        {
            int n = r.ReadInt32();
            var list = new List<string>(n);
            for (int i = 0; i < n; i++) list.Add(r.ReadString());
            return list;
        }

        private static void Apply(List<string> names, string from)
        {
            foreach (var n in names) ToApply.Add(n);
            TryApply(from);
        }

        private static void TryApply(string from = "")
        {
            if (ToApply.Count == 0 || !GameApi.InGame() || !Session.InSharedWorld) return;
            var b = Book();
            if (b == null) return;
            var added = new List<string>();
            _applying = true;
            try
            {
                foreach (var r in b.AllRecipes)
                {
                    if (r == null || !ToApply.Contains(r.name)) continue;
                    ToApply.Remove(r.name);
                    if (b.IsRecipeUnlocked(r)) continue;
                    try { if (b.UnlockRecipe(r)) added.Add(r.name); }
                    catch (Exception e) { MelonLogger.Warning($"[рецепты] {r.name}: {e.Message}"); }
                }
            }
            finally { _applying = false; }
            if (ToApply.Count > 0)
            {
                // Рецепта ещё нет в книге (мод добавит его позже) или нет в этой игре: записываем имя в выученные,
                // как это делает сама UnlockRecipe. Игра проверяет выученность по имени, и имя сохранится в прогрессе.
                var missing = new List<string>();
                try
                {
                    var list = b.m_GameState?.m_UnlockedRecipes;
                    foreach (var n in ToApply)
                    {
                        if (n.Length == 0 || list == null) continue;
                        if (!list.Contains(n)) { list.Add(n); added.Add(n); missing.Add(n); }
                    }
                }
                catch (Exception e) { MelonLogger.Warning("[рецепты] по имени: " + e.Message); }
                if (missing.Count > 0) MelonLogger.Msg("[рецепты] пока нет в книге, записал по имени: " + string.Join(", ", missing));
                ToApply.Clear();
            }
            if (added.Count > 0)
            {
                MelonLogger.Msg($"[рецепты] от {(from.Length > 0 ? from : "других")}: {string.Join(", ", added)}");
                GameApi.HudMessage(added.Count == 1 ? "Кооп: новый рецепт от друга" : $"Кооп: новых рецептов от друзей: {added.Count}");
            }
        }

        public static void Tick(float now)
        {
            if (!GameApi.InGame() || !Session.InSharedWorld) { _inWorldSince = -1f; _sentOwn = false; return; }
            if (_inWorldSince < 0f) _inWorldSince = now;
            if (now < _nextTry) return;
            _nextTry = now + 2f;
            if (now - _inWorldSince < 5f || Book() == null) return;
            try
            {
                if (Session.Mode == Mode.Host && !_hostMerged)
                {
                    // Хост: к общим рецептам мира добавляем свои и выучиваем общие, которых у него нет.
                    Load();
                    _hostMerged = true;
                    bool changed = false;
                    LoadCheat();
                    foreach (var n in OwnUnlocked()) if (!Cheat.Contains(n)) changed |= Known.Add(n);
                    if (changed) Save();
                    Apply(Known.ToList(), "мира");
                    if (Known.Count > 0) Send(Known);
                }
                if (Session.Mode == Mode.Client && !_sentOwn)
                {
                    _sentOwn = true;
                    var own = OwnUnlocked();
                    own.Add("");   // пустое имя: «пришли мне все общие», даже если своих нет
                    Send(own);
                }
                TryApply();
            }
            catch (Exception e) { MelonLogger.Warning("[рецепты] " + e.Message); }
        }

        // ---------- Хранение у хоста ----------

        private static string FileName()
        {
            string dir = PlayerProgress.WorldFolder();
            return dir == null ? null : Path.Combine(dir, "recipes.txt");
        }

        private static void Load()
        {
            if (Session.Mode != Mode.Host) return;
            string f = FileName();
            if (f == null || _loadedFor == f) return;
            _loadedFor = f;
            _hostMerged = false;
            Known.Clear();
            try { if (File.Exists(f)) foreach (var l in File.ReadAllLines(f)) if (l.Length > 0) Known.Add(l); } catch { }
        }

        private static void Save()
        {
            string f = FileName();
            if (f == null) return;
            try { File.WriteAllLines(f, Known.Where(n => n.Length > 0)); } catch (Exception e) { MelonLogger.Warning("[рецепты] запись: " + e.Message); }
        }

        private static string CheatFile()
        {
            string dir = PlayerProgress.WorldFolder();
            return dir == null ? null : Path.Combine(dir, "recipes_cheat.txt");
        }

        private static void LoadCheat()
        {
            string f = CheatFile();
            if (f == null || _cheatFor == f) return;
            _cheatFor = f;
            Cheat.Clear();
            try { if (File.Exists(f)) foreach (var l in File.ReadAllLines(f)) if (l.Length > 0) Cheat.Add(l); } catch { }
        }

        private static void SaveCheat()
        {
            string f = CheatFile();
            if (f == null) return;
            try { File.WriteAllLines(f, Cheat); } catch (Exception e) { MelonLogger.Warning("[рецепты] запись: " + e.Message); }
        }

        public static void Reset()
        {
            Known.Clear();
            Cheat.Clear();
            _cheatFor = null;
            ToApply.Clear();
            _sentOwn = _hostMerged = false;
            _loadedFor = null;
            _inWorldSince = -1f;
        }
    }

    [HarmonyPatch(typeof(RecipeBook), nameof(RecipeBook.UnlockRecipe))]
    internal static class Patch_RecipeUnlock
    {
        private static void Postfix(RecipeData recipe, bool __result)
        {
            try { SharedRecipes.OnUnlocked(recipe, __result); } catch (Exception e) { MelonLogger.Warning("[рецепты] " + e.Message); }
        }
    }
}

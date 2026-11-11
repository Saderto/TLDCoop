using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TLDMilitaryStim
{
    /// <summary>
    /// Where the military stim turns up: only where the game itself puts an emergency stim.
    /// Containers: when a loot table rolls GEAR_EmergencyStim, with ContainerChancePercent it gives the military stim
    /// (UnityEngine.Random right after the game's own roll, so a seeded coop roll is the same for everyone).
    /// World: an emergency stim lying in a scene that survives its spawn roll is, with WorldChancePercent, swapped for
    /// a military stim at the same spot. The chance comes from its position, so both coop players get the same item.
    /// </summary>
    internal static class Loot
    {
        const string Vanilla = "GEAR_EmergencyStim";
        static bool _failed;

        internal static void Patch(HarmonyLib.Harmony h)
        {
            // 0.1.2: тайники — по списку, который игра уже вытянула для ящика (Container.m_GearToInstantiate), а вещь
            // создаём сами (как в SmokesBySeagull 0.1.91: игра создаёт содержимое ящика по имени и могла молча
            // пропустить наше). Раньше подменялся результат LootTable.GetRandomGearPrefab — дошёл ли он до ящика, неизвестно.
            h.Patch(AccessTools.Method(typeof(Container), nameof(Container.PopulateWithRandomGear)),
                postfix: new HarmonyMethod(typeof(Loot), nameof(ContainerPostfix)));
            h.Patch(AccessTools.Method(typeof(Container), nameof(Container.InstantiateContents)),
                postfix: new HarmonyMethod(typeof(Loot), nameof(OpenedPostfix)));
            h.Patch(AccessTools.Method(typeof(Container), nameof(Container.InstantiateGearPrefabInContainer)),
                prefix: new HarmonyMethod(typeof(Loot), nameof(SpawnInContainerPrefix)));
            h.Patch(AccessTools.Method(typeof(GearItem), nameof(GearItem.RollSpawnChance)),
                postfix: new HarmonyMethod(typeof(Loot), nameof(WorldRollPostfix)));
        }

        static string Clean(string n) => n == null ? "" : n.Replace("(Clone)", "").Trim();

        static int Seed { get { try { return GameManager.m_SceneTransitionData != null ? GameManager.m_SceneTransitionData.m_GameRandomSeed : 0; } catch { return 0; } } }

        // Шанс — от сида мира, места ящика и номера вещи в нём: у обоих игроков в коопе выходит одно и то же.
        static void ContainerPostfix(Container __instance)
        {
            if (__instance == null || !Assets.Registered) return;
            try
            {
                var drawn = __instance.m_GearToInstantiate;
                if (drawn == null) return;
                var p = __instance.transform.position;
                Medical(__instance, drawn, p);
                for (int i = 0; i < drawn.Count; i++)
                {
                    if (Clean(drawn[i]) != Vanilla) continue;
                    uint h = Hash(p, Seed * 31 + i + 1);
                    float roll = (h % 10000) / 100f;
                    StimMod.Log($"тайник: шприц первой помощи в {Clean(__instance.gameObject.name)} {p}, бросок {roll:0.00} из {StimMod.LootChance.Value} / container stim roll");
                    if (roll >= StimMod.LootChance.Value) continue;
                    drawn[i] = Assets.Gear; _marked.Add(__instance.GetInstanceID());
                    StimMod.Log("тайник: будет военный стимулятор (создастся при первом открытии) / container swap");
                }
            }
            catch (Exception e) { StimMod.Warn("container: " + e.Message); }
        }

        // 0.1.3 (просьба игрока): свой небольшой шанс у любой аптечки и аптечного шкафчика (в ванной тоже), сверх того,
        // что положила игра. Шкаф из нескольких ящиков делит один шанс на все ящики.
        static readonly string[] MedWords = { "FirstAid", "Medic", "Medkit", "Bathroom", "Vanity", "MirrorCabinet", "Pharmac" };
        static bool IsMedical(Container c, out string what)
        {
            what = Clean(c.gameObject.name);
            string loc = "", filter = "";
            try { loc = c.m_LocalizedDisplayName != null ? c.m_LocalizedDisplayName.m_LocalizationID ?? "" : ""; } catch { }
            try { filter = c.m_ContainerFilterName ?? ""; } catch { }
            string par = "";
            try { par = c.transform.parent != null ? c.transform.parent.name : ""; } catch { }
            what = $"{what} ({loc}{(filter.Length > 0 ? ", " + filter : "")})";
            foreach (var w in MedWords)
                foreach (var s in new[] { Clean(c.gameObject.name), loc, filter, par })
                    if (s.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static void Medical(Container c, Il2CppSystem.Collections.Generic.List<string> drawn, Vector3 p)
        {
            float chance = StimMod.MedicalChance.Value;
            if (chance <= 0f) return;
            bool med = IsMedical(c, out string what);
            StimMod.Log($"ящик: {what}{(med ? " — аптечный" : "")} {p}");
            if (!med) return;
            // 0.1.4: шанс больше не делится на «ящики шкафа»: родитель у аптечки — вся группа предметов сцены,
            // и шанс падал до 0,75 % (лог 11-09). Аптечки и шкафчики с лекарствами — один ящик каждый.
            float roll = (Hash(p, Seed * 31 + 0x3ED) % 10000) / 100f;
            StimMod.Log($"аптечный ящик: бросок {roll:0.00} из {chance:0.00} / medical roll");
            if (roll >= chance) return;
            drawn.Add(Assets.Gear); _marked.Add(c.GetInstanceID());
            StimMod.Log("аптечный ящик: добавлен военный стимулятор (создастся при первом открытии) / added to medical container");
        }

        // Игра создаёт содержимое ящика только при первом открытии, по именам из m_GearToInstantiate. Наше имя есть в
        // каталоге ресурсов (наш локатор), так что игра должна создать его сама; на случай, если она делает это через
        // InstantiateGearPrefabInContainer, вещь создаём здесь и пишем об этом в лог.
        // Проверка (лог): ящик, куда мы вписали стимулятор, открыт — создала ли игра вещь.
        static readonly System.Collections.Generic.HashSet<int> _marked = new();
        static readonly Il2CppSystem.Collections.Generic.List<GearItem> _tmp = new();
        static void OpenedPostfix(Container __instance)
        {
            try
            {
                if (__instance == null || !_marked.Remove(__instance.GetInstanceID())) return;
                _tmp.Clear();
                __instance.GetItems(Assets.Gear, _tmp);
                if (_tmp.Count > 0) StimMod.Log($"ящик открыт: военный стимулятор на месте ({_tmp.Count}) / created OK");
                else StimMod.Warn($"ящик открыт, а военного стимулятора нет: игра его не создала / NOT created in {Clean(__instance.gameObject.name)}");
            }
            catch (Exception e) { StimMod.Warn("opened check: " + e.Message); }
        }

        static bool SpawnInContainerPrefix(Container __instance, string prefabName)
        {
            if (Clean(prefabName) != Assets.Gear) return true;
            try
            {
                var gi = GearItem.InstantiateGearItem(Assets.Gear);
                if (gi == null) { StimMod.Warn("не удалось создать стимулятор для ящика / could not make stim for container"); return false; }
                gi.m_RolledSpawnChance = true;
                __instance.AddGear(gi);
                StimMod.Log($"военный стимулятор положен в {Clean(__instance.gameObject.name)} / put into container");
            }
            catch (Exception e) { StimMod.Warn("into container: " + e.Message); }
            return false;
        }

        static void WorldRollPostfix(GearItem __instance)
        {
            if (__instance == null || _failed || !Assets.Registered) return;
            try
            {
                var go = __instance.gameObject;
                if (Clean(go.name) != Vanilla || !go.activeSelf) return;
                if (__instance.m_InPlayerInventory || __instance.m_InsideContainer || go.GetComponentInParent<Container>() != null) return;
                var t = go.transform;
                float roll = (Hash(t.position, 0x57A1) % 10000) / 100f;
                StimMod.Log($"на локации: шприц первой помощи {t.position}, бросок {roll:0.00} из {StimMod.WorldChance.Value} / world stim roll");
                if (roll >= StimMod.WorldChance.Value) return;
                MelonLoader.MelonCoroutines.Start(Swap(go, t.position, t.rotation));
            }
            catch (Exception e) { StimMod.Warn("world stim: " + e.Message); }
        }

        static System.Collections.IEnumerator Swap(GameObject vanilla, Vector3 pos, Quaternion rot)
        {
            yield return null; // after the game's own start of the scene item (a failed roll has removed it by now)
            try
            {
                if (vanilla == null || !vanilla.activeInHierarchy) yield break;
                var old = vanilla.GetComponent<GearItem>();
                if (old == null || old.m_InPlayerInventory) yield break;
                var gi = GearItem.InstantiateGearItem(Assets.Gear);
                if (gi == null) yield break;
                gi.m_RolledSpawnChance = true;
                var root = GearManager.m_GearCategory;
                if (root != null) gi.transform.SetParent(root.transform, true); // saved with the region, like GearItem.Drop
                gi.transform.position = pos;
                gi.transform.rotation = rot;
                UnityEngine.Object.Destroy(vanilla);
                StimMod.Log($"на локации: военный стимулятор вместо шприца первой помощи {pos} / world swap");
            }
            catch (Exception e) { StimMod.Warn("world swap: " + e.Message); }
        }

        static uint Hash(Vector3 p, int salt)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (int v in new[] { Mathf.RoundToInt(p.x * 10), Mathf.RoundToInt(p.y * 10), Mathf.RoundToInt(p.z * 10), salt })
                { h ^= (uint)v; h *= 16777619; h ^= h >> 13; }
                return h;
            }
        }
    }
}

using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TLDShotgun
{
    /// <summary>
    /// Shotgun shells in containers (0.1.18): wherever a loot table rolls rifle ammo (box or loose rounds), with
    /// ShellLootChancePercent it gives shotgun shells instead: a box (40 %) or loose shells (60 %). So shells turn up
    /// in the same lockers, cabinets, crates and bodies as rifle ammo, and only where the game itself puts ammo.
    /// Uses UnityEngine.Random right after the game's own roll, so a seeded roll (coop) gives the same result for everyone.
    /// </summary>
    internal static class Loot
    {
        static GameObject _box, _single;
        // 0.1.32: always in the log (user 11-10: shells found nowhere) — how often the game rolled ammo and how often we swapped
        internal static int AmmoRolls, Swaps, LooseAmmo, LooseShells;
        internal static void Report(string scene)
        {
            ShotgunMod.Instance.LoggerInstance.Msg($"патроны в {scene}: тайники — патронов к винтовке/револьверу {AmmoRolls}, из них стали дробью {Swaps}; на земле — патронов {LooseAmmo}, рядом дробь {LooseShells} / loot stats");
            AmmoRolls = Swaps = LooseAmmo = LooseShells = 0;
        }
        static bool _failed;

        internal static bool IsAmmo(string n) =>
            n != null && (n.StartsWith("GEAR_RifleAmmo") || n.StartsWith("GEAR_RevolverAmmo")) && !n.Contains("Casing");

        internal static void Patch(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(GearItem), nameof(GearItem.RollSpawnChance)),
                postfix: new HarmonyMethod(typeof(Loot), nameof(WorldRollPostfix)));
            h.Patch(AccessTools.Method(typeof(LootTable), nameof(LootTable.GetRandomGearPrefab)),
                postfix: new HarmonyMethod(typeof(Loot), nameof(RollPostfix)));
        }

        static GameObject Load(string name)
        {
            try { return Addressables.LoadAssetAsync<GameObject>(name).WaitForCompletion(); }
            catch (Exception e) { ShotgunMod.Warn($"{name}: {e.Message}"); return null; }
        }

        static void RollPostfix(ref GameObject __result)
        {
            if (__result == null || _failed || !Assets.Registered) return;
            try
            {
                string n = __result.name;
                if (!IsAmmo(n)) return;
                AmmoRolls++;
                float roll = UnityEngine.Random.value * 100f;
                if (roll >= ShotgunMod.ShellLootChance.Value) return;
                if (_box == null) _box = Load("GEAR_ShotgunAmmoBox");
                if (_single == null) _single = Load("GEAR_ShotgunAmmoSingle");
                if (_box == null || _single == null) { _failed = true; return; }
                var pick = UnityEngine.Random.value < 0.4f ? _box : _single;
                Swaps++;
                ShotgunMod.Instance.LoggerInstance.Msg($"тайник: вместо {n} → {pick.name} / loot swap");
                __result = pick;
            }
            catch (Exception e) { _failed = true; ShotgunMod.Warn("loot: " + e.Message); }
        }

        // Loose ammo lying in the world: GearItem.RollSpawnChance runs once, the first time a scene item starts (after
        // that the save keeps it). If rifle/revolver ammo stayed after its roll, with ShellLootChancePercent shotgun
        // shells are put next to it: a box (30 %) or 1–3 loose shells. The chance comes from the ammo's position,
        // not from Random, so in coop both players get the same shells at the same place.
        static void WorldRollPostfix(GearItem __instance)
        {
            if (__instance == null || _failed || !Assets.Registered) return;
            try
            {
                var go = __instance.gameObject;
                string n = go.name.Replace("(Clone)", "").Trim();
                if (!IsAmmo(n) || !go.activeSelf) return;
                LooseAmmo++;
                if (__instance.m_InPlayerInventory || __instance.m_InsideContainer || go.GetComponentInParent<Container>() != null) return;
                var p = go.transform.position;
                uint hsh = Hash(p);
                if ((hsh % 10000) / 100f >= ShotgunMod.ShellLootChance.Value) return;
                bool box = (hsh / 10000 % 100) < 30;
                int units = box ? 0 : 1 + (int)(hsh / 1000000 % 3);
                LooseShells++;
                MelonLoader.MelonCoroutines.Start(SpawnNear(p, go.transform.rotation, box, units));
            }
            catch (Exception e) { ShotgunMod.Warn("loose shells: " + e.Message); }
        }

        // An item made by GearItem.InstantiateGearItem is not in the region's save until it hangs under the scene's gear
        // root (GearManager.m_GearCategory) — that is what GearItem.Drop does. Without it the item was gone after reload
        // (0.1.20–0.1.22: the shotgun on Timberwolf Mountain disappeared).
        internal static void ToScene(GearItem gi)
        {
            try
            {
                var root = GearManager.m_GearCategory;
                if (root != null) gi.transform.SetParent(root.transform, true);
                else ShotgunMod.Warn("GearManager.m_GearCategory is null");
            }
            catch (Exception e) { ShotgunMod.Warn("to scene: " + e.Message); }
        }

        static uint Hash(Vector3 p)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (int v in new[] { Mathf.RoundToInt(p.x * 10), Mathf.RoundToInt(p.y * 10), Mathf.RoundToInt(p.z * 10), 0x5407 })
                { h ^= (uint)v; h *= 16777619; h ^= h >> 13; }
                return h;
            }
        }

        static System.Collections.IEnumerator SpawnNear(Vector3 p, Quaternion rot, bool box, int units)
        {
            yield return null; // not inside the game's own start of the scene item
            try
            {
                string prefab = box ? "GEAR_ShotgunAmmoBox" : "GEAR_ShotgunAmmoSingle";
                var gi = GearItem.InstantiateGearItem(prefab);
                if (gi == null) yield break;
                gi.m_RolledSpawnChance = true;
                Loot.ToScene(gi);
                var side = rot * Vector3.right;
                var at = p + new Vector3(side.x, 0f, side.z).normalized * 0.12f;
                if (Physics.Raycast(at + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 0.8f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;
                gi.transform.position = at;
                gi.transform.rotation = Quaternion.Euler(0f, rot.eulerAngles.y + 25f, 0f);
                if (!box && gi.m_StackableItem != null) gi.m_StackableItem.m_Units = units;
                ShotgunMod.Log($"патроны для дробовика рядом с патронами: {prefab}{(box ? "" : " ×" + units)} {at} / loose shells");
            }
            catch (Exception e) { ShotgunMod.Warn("loose shells spawn: " + e.Message); }
        }
    }
}

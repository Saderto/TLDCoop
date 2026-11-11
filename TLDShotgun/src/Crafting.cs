using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gear;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TLDShotgun
{
    /// <summary>
    /// Shotgun shells at the ammunition workbench. Made from the game's own rifle-cartridge recipe (same bench, time,
    /// tools, skill and lock state), with the shotgun casing instead of the rifle one, and twice the scrap lead for the shot.
    /// Added to BlueprintManager.m_AllBlueprints (GetAllBlueprints returns that list) the first time the game asks for it.
    /// </summary>
    internal static class Crafting
    {
        internal const string Name = "BLUEPRINT_GEAR_ShotgunAmmoSingle_A";
        static BlueprintData _bp;
        static bool _failed;
        static GearItem _shell;

        internal static void Patch(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(BlueprintManager), nameof(BlueprintManager.GetAllBlueprints)),
                postfix: new HarmonyMethod(typeof(Crafting), nameof(AllPostfix)));
            h.Patch(AccessTools.Method(typeof(BlueprintData), nameof(BlueprintData.GetCraftingIcon)),
                postfix: new HarmonyMethod(typeof(Crafting), nameof(IconPostfix)));
        }

        static GearItem Gear(string name)
        {
            try
            {
                var go = Addressables.LoadAssetAsync<GameObject>(name).WaitForCompletion();
                return go != null ? go.GetComponent<GearItem>() : null;
            }
            catch (Exception e) { ShotgunMod.Warn($"{name}: {e.Message}"); return null; }
        }

        static void AllPostfix(Il2CppSystem.Collections.Generic.List<BlueprintData> __result)
        {
            if (__result == null || _failed || !Assets.Registered) return;
            try
            {
                if (_bp != null && __result.Contains(_bp)) return;
                BlueprintData rifle = null;
                for (int i = 0; i < __result.Count; i++)
                {
                    var b = __result[i];
                    if (b == null) continue;
                    var g = b.m_CraftedResultGear;
                    if (g != null && g.name == "GEAR_RifleAmmoSingle") { rifle = b; break; }
                }
                if (rifle == null) return; // list not loaded yet

                if (_bp == null)
                {
                    _shell = Gear("GEAR_ShotgunAmmoSingle");
                    var casing = Gear("GEAR_ShotgunAmmoCasing");
                    if (_shell == null || casing == null) { _failed = true; ShotgunMod.Warn("рецепт патронов: нет патрона или гильзы / no shell or casing"); return; }

                    var bp = UnityEngine.Object.Instantiate(rifle);
                    bp.name = Name;
                    bp.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    var parts = new System.Text.StringBuilder();
                    var req = bp.m_RequiredGear;
                    for (int i = 0; req != null && i < req.Length; i++)
                    {
                        var r = req[i];
                        if (r == null || r.m_Item == null) continue;
                        // never touch the rifle recipe: if the copy shares the entry, give it its own
                        var orig = rifle.m_RequiredGear;
                        if (orig != null && orig.Pointer == req.Pointer) { req = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<BlueprintData.RequiredGearItem>(orig.Length); for (int k = 0; k < orig.Length; k++) req[k] = orig[k]; bp.m_RequiredGear = req; }
                        if (orig != null && i < orig.Length && orig[i] != null && orig[i].Pointer == r.Pointer)
                        {
                            var c = new BlueprintData.RequiredGearItem();
                            c.m_Item = r.m_Item; c.m_Count = r.m_Count; c.m_Weight = r.m_Weight; c.m_Units = r.m_Units;
                            req[i] = c; r = c;
                        }
                        string n = r.m_Item.name;
                        if (n.Contains("Casing")) r.m_Item = casing;
                        else if (n.Contains("Lead")) r.m_Count = Math.Max(1, r.m_Count) * 2;
                        parts.Append($" {r.m_Item.name}×{r.m_Count}");
                    }
                    var pw = bp.m_RequiredPowder;
                    for (int i = 0; pw != null && i < pw.Length; i++)
                        if (pw[i] != null) parts.Append($" порох/powder {pw[i].m_Quantity.ToStringMetric(2)}");
                    bp.m_CraftedResultGear = _shell;
                    try { bp.SetCraftedResult(_shell); } catch { }
                    _bp = bp;
                    ShotgunMod.Log($"рецепт патронов для дробовика:{parts} → {_shell.name}×{bp.m_CraftedResultCount}, {bp.m_DurationMinutes} мин / shell recipe");
                }
                int at = __result.IndexOf(rifle);
                __result.Insert(at + 1, _bp);
            }
            catch (Exception e) { _failed = true; ShotgunMod.Warn("рецепт патронов / shell recipe: " + e); }
        }

        static void IconPostfix(BlueprintData __instance, ref Texture2D __result)
        {
            if (_bp == null || __instance == null || __instance.Pointer != _bp.Pointer || _shell == null) return;
            try { var t = Utils.GetInventoryIconTexture(_shell); if (t != null) __result = t; } catch { }
        }
    }
}

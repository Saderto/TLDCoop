using System;
using System.Collections.Generic;
using System.IO;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace ShotgunByEMPEROR
{
    /// <summary>
    /// Adds an Addressables locator so the game finds the shotgun assets by name and by GUID,
    /// exactly as in episode 5: GEAR_Shotgun, ammo, FPS weapon, state transitions.
    /// Survival data that refers to them (GearItemData_Shotgun*, GunItem ammo refs) then just works.
    /// </summary>
    internal static class Assets
    {
        internal const string BundleFile = "shotgunbyemperor.bundle";
        const string BundleProvider = "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider";
        const string AssetProvider = "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider";

        // asset path in our bundle -> keys (name, GUID from the episode 5 catalog), resource type
        static readonly (string path, string[] keys, Func<Il2CppSystem.Type> type)[] Entries =
        {
            ("Assets/Prefabs/Gear/Weapons/GEAR_Shotgun.prefab", new[]{"GEAR_Shotgun", "dc194e41931194542947eaa30d29e2cf"}, Il2CppType.Of<GameObject>),
            ("Assets/Prefabs/Gear/Ammo/GEAR_ShotgunAmmoBox.prefab", new[]{"GEAR_ShotgunAmmoBox", "c60f540c296d80948845110c79d1206c"}, Il2CppType.Of<GameObject>),
            ("Assets/Prefabs/Gear/Ammo/GEAR_ShotgunAmmoSingle.prefab", new[]{"GEAR_ShotgunAmmoSingle", "8ffcb140418f55142a9c41228c000b06"}, Il2CppType.Of<GameObject>),
            ("Assets/Prefabs/Gear/Ammo/GEAR_ShotgunAmmoCasing.prefab", new[]{"GEAR_ShotgunAmmoCasing", "fa46c196acc99314fbac36d0629c1ae4"}, Il2CppType.Of<GameObject>),
            ("Assets/Prefabs/Characters/FPSItems/FPSWeapon_Shotgun.prefab", new[]{"FPSWeapon_Shotgun", "c01fac93fc52fb748b59ce548596ae27"}, Il2CppType.Of<GameObject>),
            ("Assets/Prefabs/Gear/Weapons/FPH_Shotgun.prefab", new[]{"FPH_Shotgun", "91fcd506738120c41bdb2f1f862db520"}, Il2CppType.Of<GameObject>),
            ("Assets/ScriptableObjects/Gameplay/FPSItems/FPSItem_Shotgun.asset", new[]{"FPSItem_Shotgun", "95b6bebe4ac066f418d8a3b5e77d4f26", "FPSWeapon"}, Il2CppType.Of<Il2CppTLD.Gameplay.FPSItem>),
            ("Assets/ArtAssets/Characters/FP_Rig/ShotgunStateTransitions.asset", new[]{"ShotgunStateTransitions", "ebb7703b4004b3e44b53213a1b142034"}, Il2CppType.Of<Il2Cpp.PlayerStateTransitions>),
        };

        internal static bool Registered;

        // Addressables unloads a bundle (with all its meshes/textures) when nothing holds it anymore.
        // Survival bundles stay loaded because the game holds them; ours did not, so a spawned shotgun
        // lost its mesh and texture half a second later. Keep one load of each prefab for the whole session.
        static UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<IAssetBundleResource> _pin;
        static IResourceLocation _bundleLoc;

        // Only our own bundle is held (not the survival bundles it depends on): cheap, ~1 MB.
        static void Pin()
        {
            try
            {
                _pin = Addressables.ResourceManager.ProvideResource<IAssetBundleResource>(_bundleLoc);
                ShotgunMod.Log("файл ружья держится в памяти / bundle pinned");
            }
            catch (Exception e) { ShotgunMod.Warn("pin: " + e.Message); }
        }

        static int Count(Il2CppSystem.Collections.Generic.IList<IResourceLocation> l)
            => l.Cast<Il2CppSystem.Collections.Generic.ICollection<IResourceLocation>>().Count;

        internal static void Register()
        {
            string path = Path.Combine(ShotgunMod.Folder, BundleFile);
            if (!File.Exists(path)) { ShotgunMod.Error("нет файла / missing " + path + " — запустите / run Mods\\TLDAssets.exe"); AssetsHint.Missing(); return; }

            // survival bundles our objects point to: take them from GEAR_Rifle's location (same set, checked offline)
            var deps = new List<IResourceLocation>();
            var h = Addressables.LoadResourceLocationsAsync("GEAR_Rifle", Il2CppType.Of<GameObject>());
            var found = h.WaitForCompletion();
            if (found == null || Count(found) == 0) { ShotgunMod.Error("GEAR_Rifle location not found"); return; }
            var rifle = found[0];
            if (rifle.HasDependencies)
            {
                var d = rifle.Dependencies;
                int n = Count(d);
                for (int i = 0; i < n; i++) deps.Add(d[i]);
            }
            ShotgunMod.Log($"зависимости из GEAR_Rifle / deps: {deps.Count}");

            var opts = new AssetBundleRequestOptions();
            opts.BundleName = "tldshotgun";
            opts.BundleSize = new FileInfo(path).Length;
            var bundleLoc = new ResourceLocationBase("shotgunbyemperor.bundle", path, BundleProvider,
                Il2CppType.Of<IAssetBundleResource>(), new Il2CppReferenceArray<IResourceLocation>(0));
            bundleLoc.Data = opts;
            _bundleLoc = bundleLoc.Cast<IResourceLocation>();

            var all = new Il2CppReferenceArray<IResourceLocation>(deps.Count + 1);
            all[0] = bundleLoc.Cast<IResourceLocation>();
            for (int i = 0; i < deps.Count; i++) all[i + 1] = deps[i];

            var map = new ResourceLocationMap("ShotgunByEMPEROR", Entries.Length * 3);
            foreach (var (assetPath, keys, type) in Entries)
            {
                var loc = new ResourceLocationBase(keys[0], assetPath, AssetProvider, type(), all);
                var iloc = loc.Cast<IResourceLocation>();
                foreach (var k in keys) map.Add(k, iloc);
                map.Add(assetPath, iloc);
            }
            Addressables.AddResourceLocator(map.Cast<IResourceLocator>(), null, null);
            Registered = true;
            ShotgunMod.Log("локатор добавлен / locator added");
            Pin();
        }
    }

    internal static class Sound
    {
        internal static void LoadBank()
        {
            try
            {
                uint id;
                var r = Il2Cpp.AkSoundEngine.LoadBank("weaponShotgun", out id);
                ShotgunMod.Log($"weaponShotgun bank: {r} id {id}");
            }
            catch (Exception e) { ShotgunMod.Warn("bank: " + e.Message); }
        }
    }
}

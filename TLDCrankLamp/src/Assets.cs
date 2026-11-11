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

namespace TLDCrankLamp
{
    /// <summary>
    /// Adds an Addressables locator so the game finds the crank lamp by name and by GUID, as in episode 5:
    /// GEAR_Flashlight_CrankLamp, its item data, icon, first-person hands and FPS item (label "FPSWeapon", so the camera
    /// registers it like every other first-person item).
    /// </summary>
    internal static class Assets
    {
        internal const string BundleFile = "tldcranklamp.bundle";
        internal const string Gear = "GEAR_Flashlight_CrankLamp";
        const string BundleProvider = "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider";
        const string AssetProvider = "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider";

        static readonly (string path, string[] keys, Func<Il2CppSystem.Type> type)[] Entries =
        {
            ("Assets/Prefabs/Gear/LightSources/GEAR_Flashlight_CrankLamp.prefab", new[]{Gear, "b2468aa455c16ab44940405a81b30da3"}, Il2CppType.Of<GameObject>),
            ("Assets/Prefabs/Gear/LightSources/GearItemData_Flashlight_CrankLamp.asset", new[]{"GearItemData_Flashlight_CrankLamp", "3a451cbf5a798c84d99d451427268d18", "GearItemData"}, Il2CppType.Of<Il2CppTLD.Gear.GearItemData>),
            ("Assets/ScriptableObjects/Gear/IconData/GearIcon_Flashlight_CrankLamp.asset", new[]{"GearIcon_Flashlight_CrankLamp", "c0ba7cc6517bb8044a186f4efa904c76"}, Il2CppType.Of<Il2CppTLD.Gear.GearItemInventoryIconSimpleData>),
            ("Assets/ArtAssets/UI/InventoryGridIcons/ico_GearItem__Flashlight_CrankLamp.png", new[]{"ico_GearItem__Flashlight_CrankLamp", "18acf0506faa49a4c9bda8265b42e80c"}, Il2CppType.Of<Texture2D>),
            ("Assets/Prefabs/Characters/FPSItems/FPSWeapon_Flashlight_CrankLamp.prefab", new[]{"FPSWeapon_Flashlight_CrankLamp", "8d8f99b0338bbdb4182811bd66a1aa3d"}, Il2CppType.Of<GameObject>),
            ("Assets/TLDCrankLamp/HandCrankLight_Crank_Start_FP.anim", new[]{"HandCrankLight_Crank_Start_FP"}, Il2CppType.Of<AnimationClip>),
            ("Assets/TLDCrankLamp/HandCrankLight_Crank_Loop_FP.anim", new[]{"HandCrankLight_Crank_Loop_FP"}, Il2CppType.Of<AnimationClip>),
            ("Assets/TLDCrankLamp/HandCrankLight_Crank_Exit_FP.anim", new[]{"HandCrankLight_Crank_Exit_FP"}, Il2CppType.Of<AnimationClip>),
            ("Assets/ScriptableObjects/Gameplay/FPSItems/FPSItem_Flashlight_CrankLamp.asset", new[]{"FPSItem_Flashlight_CrankLamp", "079f90591665eb44bb5083a9dc6b75ab", "FPSWeapon"}, Il2CppType.Of<Il2CppTLD.Gameplay.FPSItem>),
        };

        internal static bool Registered;

        // Addressables unloads a bundle (meshes/textures included) once nothing holds it; keep ours loaded (0.6 MB).
        static UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<IAssetBundleResource> _pin;
        static IResourceLocation _bundleLoc;

        static int Count(Il2CppSystem.Collections.Generic.IList<IResourceLocation> l)
            => l.Cast<Il2CppSystem.Collections.Generic.ICollection<IResourceLocation>>().Count;

        // survival bundles our objects point to: the aurora flashlight's and the rifle's dependencies (same shared set)
        static void AddDeps(string key, List<IResourceLocation> deps, HashSet<string> seen)
        {
            var found = Addressables.LoadResourceLocationsAsync(key, Il2CppType.Of<GameObject>()).WaitForCompletion();
            if (found == null || Count(found) == 0) { LampMod.Warn(key + " location not found"); return; }
            var loc = found[0];
            if (!loc.HasDependencies) return;
            var d = loc.Dependencies;
            int n = Count(d);
            for (int i = 0; i < n; i++)
                if (seen.Add(d[i].InternalId)) deps.Add(d[i]);
        }

        internal static void Register()
        {
            string path = Path.Combine(LampMod.Folder, BundleFile);
            if (!File.Exists(path)) { LampMod.Error("нет файла / missing " + path + " — запустите / run Mods\\TLDAssets.exe"); AssetsHint.Missing(); return; }

            var deps = new List<IResourceLocation>();
            var seen = new HashSet<string>();
            AddDeps("GEAR_Flashlight", deps, seen);
            AddDeps("GEAR_Rifle", deps, seen);
            if (deps.Count == 0) { LampMod.Error("no survival dependencies found"); return; }
            LampMod.Log($"зависимости / deps: {deps.Count}");

            var opts = new AssetBundleRequestOptions();
            opts.BundleName = "tldcranklamp";
            opts.BundleSize = new FileInfo(path).Length;
            var bundleLoc = new ResourceLocationBase(BundleFile, path, BundleProvider,
                Il2CppType.Of<IAssetBundleResource>(), new Il2CppReferenceArray<IResourceLocation>(0));
            bundleLoc.Data = opts;
            _bundleLoc = bundleLoc.Cast<IResourceLocation>();

            var all = new Il2CppReferenceArray<IResourceLocation>(deps.Count + 1);
            all[0] = _bundleLoc;
            for (int i = 0; i < deps.Count; i++) all[i + 1] = deps[i];

            var map = new ResourceLocationMap("TLDCrankLamp", Entries.Length * 4);
            foreach (var (assetPath, keys, type) in Entries)
            {
                var loc = new ResourceLocationBase(keys[0], assetPath, AssetProvider, type(), all);
                var iloc = loc.Cast<IResourceLocation>();
                foreach (var k in keys) map.Add(k, iloc);
                map.Add(assetPath, iloc);
            }
            Addressables.AddResourceLocator(map.Cast<IResourceLocator>(), null, null);
            Registered = true;
            LampMod.Log("локатор добавлен / locator added");
            try
            {
                _pin = Addressables.ResourceManager.ProvideResource<IAssetBundleResource>(_bundleLoc);
                LampMod.Log("файл держится в памяти / bundle pinned");
            }
            catch (Exception e) { LampMod.Warn("pin: " + e.Message); }
        }
    }
}

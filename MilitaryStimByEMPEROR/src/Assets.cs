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

namespace MilitaryStimByEMPEROR
{
    /// <summary>
    /// Adds an Addressables locator so the game finds the military stim by name and by GUID, as in episode 5:
    /// GEAR_MilitaryStim, its item data, icon, first-person hands and FPS item (label "FPSWeapon", so the camera
    /// registers it like every other first-person item).
    /// </summary>
    internal static class Assets
    {
        internal const string BundleFile = "militarystimbyemperor.bundle";
        internal const string Gear = "GEAR_MilitaryStim";
        const string BundleProvider = "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider";
        const string AssetProvider = "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider";

        static readonly (string path, string[] keys, Func<Il2CppSystem.Type> type)[] Entries =
        {
            ("Assets/Prefabs/Gear/FirstAid/GEAR_MilitaryStim.prefab", new[]{Gear, "bbf3712ac27276241ad4f9d486c96368"}, Il2CppType.Of<GameObject>),
            ("Assets/Prefabs/Gear/FirstAid/GearItemData_MilitaryStim.asset", new[]{"GearItemData_MilitaryStim", "0ad4e4530e4c83a4da5fcd71138585d6", "GearItemData"}, Il2CppType.Of<Il2CppTLD.Gear.GearItemData>),
            ("Assets/ArtAssets/UI/InventoryGridIcons/ico_GearItem__MilitaryStim.png", new[]{"ico_GearItem__MilitaryStim", "a96724ba523047c4fabc115530cdf832"}, Il2CppType.Of<Texture2D>),
            ("Assets/Prefabs/Characters/FPSItems/FPSWeapon_EmergencyStim_Military.prefab", new[]{"FPSWeapon_EmergencyStim_Military", "3370e1daf21a8d2459680ecb122d9398"}, Il2CppType.Of<GameObject>),
            ("Assets/Prefabs/Gear/FirstAid/FPH_EmergencyStim_Military.prefab", new[]{"FPH_EmergencyStim_Military", "79be4e1c6b0e04e4fa10f2d768ff18d5"}, Il2CppType.Of<GameObject>),
            ("Assets/ScriptableObjects/Gameplay/FPSItems/FPSItem_EmergencyStim_Military.asset", new[]{"FPSItem_EmergencyStim_Military", "8306a16318cd91a4aa8451ecf75c218c", "FPSWeapon"}, Il2CppType.Of<Il2CppTLD.Gameplay.FPSItem>),
        };

        internal static bool Registered;

        // Addressables unloads a bundle (meshes/textures included) once nothing holds it; keep ours loaded (0.6 MB).
        static UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<IAssetBundleResource> _pin;
        static IResourceLocation _bundleLoc;

        static int Count(Il2CppSystem.Collections.Generic.IList<IResourceLocation> l)
            => l.Cast<Il2CppSystem.Collections.Generic.ICollection<IResourceLocation>>().Count;

        // survival bundles our objects point to: the vanilla emergency stim's and the rifle's dependencies (same shared set)
        static void AddDeps(string key, List<IResourceLocation> deps, HashSet<string> seen)
        {
            var found = Addressables.LoadResourceLocationsAsync(key, Il2CppType.Of<GameObject>()).WaitForCompletion();
            if (found == null || Count(found) == 0) { StimMod.Warn(key + " location not found"); return; }
            var loc = found[0];
            if (!loc.HasDependencies) return;
            var d = loc.Dependencies;
            int n = Count(d);
            for (int i = 0; i < n; i++)
                if (seen.Add(d[i].InternalId)) deps.Add(d[i]);
        }

        internal static void Register()
        {
            string path = Path.Combine(StimMod.Folder, BundleFile);
            if (!File.Exists(path)) { StimMod.Error("нет файла / missing " + path + " — запустите / run Mods\\TLDAssets.exe"); AssetsHint.Missing(); return; }

            var deps = new List<IResourceLocation>();
            var seen = new HashSet<string>();
            AddDeps("GEAR_EmergencyStim", deps, seen);
            AddDeps("GEAR_Rifle", deps, seen);
            if (deps.Count == 0) { StimMod.Error("no survival dependencies found"); return; }
            StimMod.Log($"зависимости / deps: {deps.Count}");

            var opts = new AssetBundleRequestOptions();
            opts.BundleName = "tldmilitarystim";
            opts.BundleSize = new FileInfo(path).Length;
            var bundleLoc = new ResourceLocationBase(BundleFile, path, BundleProvider,
                Il2CppType.Of<IAssetBundleResource>(), new Il2CppReferenceArray<IResourceLocation>(0));
            bundleLoc.Data = opts;
            _bundleLoc = bundleLoc.Cast<IResourceLocation>();

            var all = new Il2CppReferenceArray<IResourceLocation>(deps.Count + 1);
            all[0] = _bundleLoc;
            for (int i = 0; i < deps.Count; i++) all[i + 1] = deps[i];

            var map = new ResourceLocationMap("MilitaryStimByEMPEROR", Entries.Length * 4);
            foreach (var (assetPath, keys, type) in Entries)
            {
                var loc = new ResourceLocationBase(keys[0], assetPath, AssetProvider, type(), all);
                var iloc = loc.Cast<IResourceLocation>();
                foreach (var k in keys) map.Add(k, iloc);
                map.Add(assetPath, iloc);
            }
            Addressables.AddResourceLocator(map.Cast<IResourceLocator>(), null, null);
            Registered = true;
            StimMod.Log("локатор добавлен / locator added");
            try
            {
                _pin = Addressables.ResourceManager.ProvideResource<IAssetBundleResource>(_bundleLoc);
                StimMod.Log("файл держится в памяти / bundle pinned");
            }
            catch (Exception e) { StimMod.Warn("pin: " + e.Message); }
        }
    }
}

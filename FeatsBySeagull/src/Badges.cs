using System;
using System.IO;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace FeatsBySeagull
{
    /// <summary>
    /// Badge pictures: 12 textures in our bundle (Mods/FeatsBySeagull/featsbyseagull.bundle), registered in Addressables by GUID,
    /// so the game's own feat screens load them the same way as the vanilla badges.
    /// </summary>
    internal static class Badges
    {
        internal const string BundleFile = "featsbyseagull.bundle";
        const string BundleProvider = "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider";
        const string AssetProvider = "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider";

        static UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<IAssetBundleResource> _pin;

        internal static void Register()
        {
            string path = Path.Combine(FeatsMod.Folder, BundleFile);
            if (!File.Exists(path)) { FeatsMod.Error("нет файла / missing " + path); return; }

            var opts = new AssetBundleRequestOptions();
            opts.BundleName = "featsbyseagull";
            opts.BundleSize = new FileInfo(path).Length;
            var bundleLoc = new ResourceLocationBase(BundleFile, path, BundleProvider,
                Il2CppType.Of<IAssetBundleResource>(), new Il2CppReferenceArray<IResourceLocation>(0));
            bundleLoc.Data = opts;
            var ib = bundleLoc.Cast<IResourceLocation>();
            var deps = new Il2CppReferenceArray<IResourceLocation>(1);
            deps[0] = ib;

            var map = new ResourceLocationMap("FeatsBySeagull", Defs.All.Count * 3);
            foreach (var d in Defs.All)
            {
                string assetPath = "Assets/ArtAssets/UI/BadgeIcons/" + d.Texture + ".png";
                var loc = new ResourceLocationBase(d.Texture, assetPath, AssetProvider, Il2CppType.Of<Texture2D>(), deps);
                var il = loc.Cast<IResourceLocation>();
                map.Add(d.Guid, il);
                map.Add(d.Texture, il);
                map.Add(assetPath, il);
            }
            Addressables.AddResourceLocator(map.Cast<IResourceLocator>(), null, null);
            FeatsMod.Log("значки подвигов зарегистрированы / badges registered");
            try { _pin = Addressables.ResourceManager.ProvideResource<IAssetBundleResource>(ib); }
            catch (Exception e) { FeatsMod.Warn("pin: " + e.Message); }
        }
    }
}

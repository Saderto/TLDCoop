using Il2Cpp;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TLDCompat
{
    // Замены для функций, которые убрали из игры в версии 2.58.
    // Старые моды переписаны так, чтобы вызывать эти методы вместо исчезнувших.
    public static class Gear
    {
        // Было GearItem.LoadGearItemPrefab(string): префаб предмета по имени, без создания копии в мире.
        public static GearItem LoadGearItemPrefab(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            try
            {
                var go = Addressables.LoadAssetAsync<GameObject>(name).WaitForCompletion();
                return go == null ? null : go.GetComponent<GearItem>();
            }
            catch
            {
                return null;
            }
        }
    }
}

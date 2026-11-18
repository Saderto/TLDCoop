#nullable disable
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Trader;

namespace FoodPackByEMPEROR
{
    [HarmonyPatch]
    internal static class TraderPreviewPatches
    {
        // the middle of the trader's window for our offers: the game's method stops with an error on items that are not addressables
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Panel_TradeSelection), "UpdateSelectedExchange")]
        private static bool Preview(Panel_TradeSelection __instance)
        {
            TradeExchangeData ex = __instance.SelectedTradeExchange;
            if (!TraderGoods.IsOurs(ex)) return true;
            string id = TraderGoods.IdOf(ex);
            return !TraderPreview.Show(__instance, ex, id != null ? Items.Template(id) : null);
        }
    }
}

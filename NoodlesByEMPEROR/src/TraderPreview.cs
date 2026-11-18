#nullable disable
using System;
using Il2Cpp;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;
using Il2CppTLD.Trader;
using MelonLoader;
using UnityEngine;

namespace NoodlesByEMPEROR
{
    /// <summary>The middle of the trader's window (picture, calories, weight, name, description, price) for an offer of ours.
    /// THE SAME FILE in FoodPack, Snacks, Noodles, Smokehouse, TeaCoffee and Smokes (only the namespace differs).
    /// The game's Panel_TradeSelection.UpdateSelectedExchange loads the item's prefab by its addressable key and stops with
    /// a NullReferenceException when that fails (TLD 2.60, seen in a player's Player.log 11-18). Our items are not addressables,
    /// so the window kept the previous offer's picture and numbers, and the price came only after adding the offer.
    /// Each mod's prefix on UpdateSelectedExchange calls Show for its own offers and skips the game's method; the steps are the
    /// game's own, in its order, with our template item instead of the loaded prefab.</summary>
    internal static class TraderPreview
    {
        public static bool Show(Panel_TradeSelection p, TradeExchangeData ex, GearItem template)
        {
            try
            {
                GearItemData data = ex.GetGainedItemData();
                if (data == null && template != null) data = template.m_GearItemData;
                p.m_GainedItemData = data;
                if (p.m_SpecialRequestRoot != null) p.m_SpecialRequestRoot.SetActive(false);
                if (p.m_RegularRequestRoot != null) p.m_RegularRequestRoot.SetActive(true);

                Texture2D icon = ex.GetGainedItemIcon();
                if (icon == null && data != null) icon = Utils.GetInventoryIconTexture(data, 100f);
                if (p.m_GainedItemTexture != null && icon != null) p.m_GainedItemTexture.mainTexture = icon;

                FoodItem food = template != null ? template.m_FoodItem : null;
                if (p.m_GainedItemCaloriesParent != null) p.m_GainedItemCaloriesParent.SetActive(food != null);
                if (p.m_GainedItemCaloriesLabel != null) p.m_GainedItemCaloriesLabel.text = food != null ? food.m_CaloriesTotal.ToString("F0") : "";
                if (p.m_GainedItemWeightLabel != null)
                {
                    if (template != null) p.m_GainedItemWeightLabel.text = template.GetItemWeightKG(true).ToFormattedStringWithUnits(2);
                    else if (data != null) p.m_GainedItemWeightLabel.text = data.m_BaseWeight.ToFormattedStringWithUnits(2);
                }
                if (p.m_GainedItemStatsGrid != null) p.m_GainedItemStatsGrid.Reposition();

                int mult = Multiplier(p);
                if (p.m_GainedItemNameLabel != null) p.m_GainedItemNameLabel.text = ex.GetTradeExchangeHeader(mult);
                if (p.m_TradeExchangeDescriptionLabel != null) p.m_TradeExchangeDescriptionLabel.text = Description(ex, data);
                p.UpdateSelectedExchangeRequirements(mult);
                if (p.m_ExchangeMultiplierLabel != null) p.m_ExchangeMultiplierLabel.text = "x" + mult;
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Trader window: " + e.Message);
                return false;
            }
        }

        // how many times this offer is already in the trade (the game keeps it per row of the list)
        static int Multiplier(Panel_TradeSelection p)
        {
            var arr = p.m_TradeExchangesMultiplier;
            int i = p.m_ScrollBehaviour != null ? p.m_ScrollBehaviour.SelectedIndex : -1;
            return arr != null && i >= 0 && i < arr.Length ? arr[i] : 0;
        }

        // the offer's own line, as the game shows it; the item's description when the line has no text
        static string Description(TradeExchangeData ex, GearItemData data)
        {
            string key = ex.m_Description != null ? ex.m_Description.m_LocalizationID : null;
            if (!string.IsNullOrEmpty(key))
            {
                string s = Localization.Get(key);
                if (!string.IsNullOrEmpty(s) && s != key) return s;
            }
            if (data != null && data.m_LocalizedDescription != null)
            {
                string s = data.m_LocalizedDescription.Text();
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return "";
        }
    }
}

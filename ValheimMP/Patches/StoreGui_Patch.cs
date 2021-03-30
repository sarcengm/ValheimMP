using HarmonyLib;
using UnityEngine;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class StoreGui_Patch
    {
        [HarmonyPatch(typeof(StoreGui), "BuySelectedItem")]
        [HarmonyPrefix]
        private static bool BuySelectedItem(StoreGui __instance)
        {
            if (__instance.m_selectedItem != null && __instance.CanAfford(__instance.m_selectedItem))
            {
                __instance.m_trader.BuyItem(__instance.m_selectedItem.m_prefab.m_itemData.m_id, 1);
            }

            return false;
        }

        [HarmonyPatch(typeof(StoreGui), "SellItem")]
        [HarmonyPrefix]
        private static bool SellItem(StoreGui __instance)
        {
            ItemDrop.ItemData sellableItem = __instance.GetSellableItem();
            if (sellableItem != null)
            {
                __instance.m_trader.SellItem(sellableItem.m_id, 1);
            }

            return false;
        }

        static Trader listedTrader;

        [HarmonyPatch(typeof(StoreGui), "FillList")]
        [HarmonyPrefix]
        private static void FillList(StoreGui __instance)
        {
            if(listedTrader != __instance.m_trader)
            {
                __instance.m_trader.m_items.Clear();
                listedTrader = __instance.m_trader;
                listedTrader.RequestTradeList();
            }
        }

        [HarmonyPatch(typeof(StoreGui), "SelectItem")]
        [HarmonyPrefix]
        private static bool SelectItem(StoreGui __instance)
        {
            return __instance.m_trader != null && __instance.m_trader.m_items.Count > 0;
        }

        internal static void BoughtItem(StoreGui __instance, ItemDrop item, int count)
        {
            __instance.m_buyEffects.Create(__instance.transform.position, Quaternion.identity);
            Player.m_localPlayer.ShowPickupMessage(item.m_itemData, count);
            __instance.FillList();
            Gogan.LogEvent("Game", "BoughtItem", item.name, 0L);
        }

        internal static void SoldItem(StoreGui __instance, ItemDrop item, int count)
        {
            string text = (count <= 1) ? item.m_itemData.m_shared.m_name : (count + "x" + item.m_itemData.m_shared.m_name);
            __instance.m_sellEffects.Create(__instance.transform.position, Quaternion.identity);
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$msg_sold", text, count.ToString()), 0, item.m_itemData.m_shared.m_icons[0]);
            __instance.FillList();
            Gogan.LogEvent("Game", "SoldItem", text, 0L);
        }
    }
}

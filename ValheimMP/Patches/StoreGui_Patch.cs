using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class StoreGui_Patch
    {

        [HarmonyPatch(typeof(StoreGui), "OnBuyItem")]
        [HarmonyPrefix]
        private static void OnBuyItem(StoreGui __instance)
        {
            __instance.BuySelectedItem();
        }

        [HarmonyPatch(typeof(StoreGui), "BuySelectedItem")]
        [HarmonyPrefix]
        private static void BuySelectedItem(StoreGui __instance)
        {
            if (__instance.m_selectedItem != null && __instance.CanAfford(__instance.m_selectedItem))
            {
                int stack = Mathf.Min(__instance.m_selectedItem.m_stack, __instance.m_selectedItem.m_prefab.m_itemData.m_shared.m_maxStackSize);
                int quality = __instance.m_selectedItem.m_prefab.m_itemData.m_quality;
                int variant = __instance.m_selectedItem.m_prefab.m_itemData.m_variant;
                if (Player.m_localPlayer.GetInventory().AddItem(__instance.m_selectedItem.m_prefab.name, stack, quality, variant, 0L, "") != null)
                {
                    Player.m_localPlayer.GetInventory().RemoveItem(__instance.m_coinPrefab.m_itemData.m_shared.m_name, __instance.m_selectedItem.m_price);
                    __instance.m_trader.OnBought(__instance.m_selectedItem);
                    __instance.m_buyEffects.Create(__instance.transform.position, Quaternion.identity);
                    Player.m_localPlayer.ShowPickupMessage(__instance.m_selectedItem.m_prefab.m_itemData, __instance.m_selectedItem.m_prefab.m_itemData.m_stack);
                    __instance.FillList();
                    Gogan.LogEvent("Game", "BoughtItem", __instance.m_selectedItem.m_prefab.name, 0L);
                }
            }
        }


        [HarmonyPatch(typeof(StoreGui), "OnSellItem")]
        [HarmonyPrefix]
        private static void OnSellItem(StoreGui __instance)
        {
            __instance.SellItem();
        }

        [HarmonyPatch(typeof(StoreGui), "SellItem")]
        [HarmonyPrefix]
        private static void SellItem(StoreGui __instance)
        {
            ItemDrop.ItemData sellableItem = __instance.GetSellableItem();
            if (sellableItem != null)
            {
                int stack = sellableItem.m_shared.m_value * sellableItem.m_stack;
                Player.m_localPlayer.GetInventory().RemoveItem(sellableItem);
                Player.m_localPlayer.GetInventory().AddItem(__instance.m_coinPrefab.gameObject.name, stack, __instance.m_coinPrefab.m_itemData.m_quality, __instance.m_coinPrefab.m_itemData.m_variant, 0L, "");
                string text = "";
                text = ((sellableItem.m_stack <= 1) ? sellableItem.m_shared.m_name : (sellableItem.m_stack + "x" + sellableItem.m_shared.m_name));
                __instance.m_sellEffects.Create(__instance.transform.position, Quaternion.identity);
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$msg_sold", text, stack.ToString()), 0, sellableItem.m_shared.m_icons[0]);
                __instance.m_trader.OnSold();
                __instance.FillList();
                Gogan.LogEvent("Game", "SoldItem", text, 0L);
            }
        }
    }
}

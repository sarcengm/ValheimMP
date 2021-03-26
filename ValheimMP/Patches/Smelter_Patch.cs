using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Smelter_Patch
    {

        [HarmonyPatch(typeof(Smelter), "Awake")]
        [HarmonyPostfix]
        private static void Awake(Smelter __instance)
        {
            if (__instance.m_nview != null)
            {
                __instance.m_nview.Unregister("AddOre");
                __instance.m_nview.Unregister("AddFuel");

                __instance.m_nview.Register("AddOre", (long sender, int itemId) => { RPC_AddOre(__instance, sender, itemId); });
                __instance.m_nview.Register("AddFuel", (long sender, int itemId) => { RPC_AddFuel(__instance, sender, itemId); });
            }
        }

        [HarmonyPatch(typeof(Smelter), "OnAddFuel")]
        [HarmonyPrefix]
        private static bool OnAddFuel(Smelter __instance, Switch sw, Humanoid user, ItemDrop.ItemData item)
        {
            if (item != null && item.m_shared.m_name != __instance.m_fuelItem.m_itemData.m_shared.m_name)
            {
                user.Message(MessageHud.MessageType.Center, "$msg_wrongitem");
                return false;
            }
            if (__instance.GetFuel() > (float)(__instance.m_maxFuel - 1))
            {
                user.Message(MessageHud.MessageType.Center, "$msg_itsfull");
                return false;
            }
            if (!user.GetInventory().HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name))
            {
                user.Message(MessageHud.MessageType.Center, "$msg_donthaveany " + __instance.m_fuelItem.m_itemData.m_shared.m_name);
                return false;
            }
            __instance.m_nview.InvokeRPC("AddFuel", item.m_id);
            return true;
        }

        private static void RPC_AddFuel(Smelter __instance, long sender, int itemId)
        {
            if (!__instance.m_nview.IsOwner())
            {
                return;
            }
            var peer = ZNet.instance.GetPeer(sender);
            if (peer == null || peer.m_player == null)
                return;
            var item = peer.m_player.m_inventory.m_inventory.SingleOrDefault(k => k.m_id == itemId);
            var user = peer.m_player;
            if (item != null && item.m_shared.m_name != __instance.m_fuelItem.m_itemData.m_shared.m_name)
            {
                user.Message(MessageHud.MessageType.Center, "$msg_wrongitem");
                return;
            }
            if (__instance.GetFuel() > (float)(__instance.m_maxFuel - 1))
            {
                user.Message(MessageHud.MessageType.Center, "$msg_itsfull");
                return;
            }
            if (!user.GetInventory().HaveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name))
            {
                user.Message(MessageHud.MessageType.Center, "$msg_donthaveany " + __instance.m_fuelItem.m_itemData.m_shared.m_name);
                return;
            }
            user.Message(MessageHud.MessageType.Center, "$msg_added " + __instance.m_fuelItem.m_itemData.m_shared.m_name);
            user.GetInventory().RemoveItem(__instance.m_fuelItem.m_itemData.m_shared.m_name, 1);


            float fuel = __instance.GetFuel();
            __instance.SetFuel(fuel + 1f);
            __instance.m_fuelAddedEffects.Create(__instance.transform.position, __instance.transform.rotation, __instance.transform);
        }

        [HarmonyPatch(typeof(Smelter), "OnAddOre")]
        [HarmonyPrefix]
        private static bool OnAddOre(Smelter __instance, ref bool __result, Switch sw, Humanoid user, ItemDrop.ItemData item)
        {
            __result = false;
            if (item == null)
            {
                item = __instance.FindCookableItem(user.GetInventory());
                if (item == null)
                {
                    user.Message(MessageHud.MessageType.Center, "$msg_noprocessableitems");
                    return false;
                }
            }
            if (!__instance.IsItemAllowed(item.m_dropPrefab.name))
            {
                user.Message(MessageHud.MessageType.Center, "$msg_wontwork");
                return false;
            }
            if (__instance.GetQueueSize() >= __instance.m_maxOre)
            {
                user.Message(MessageHud.MessageType.Center, "$msg_itsfull");
                return false;
            }
            __instance.m_nview.InvokeRPC("AddOre", item.m_id);
            return true;
        }

        private static void RPC_AddOre(Smelter __instance, long sender, int itemId)
        {
            if (__instance.m_nview.IsOwner())
            {
                var peer = ZNet.instance.GetPeer(sender);
                if (peer == null || peer.m_player == null)
                    return;
                var item = peer.m_player.m_inventory.m_inventory.SingleOrDefault(k => k.m_id == itemId);
                var user = peer.m_player;

                if (item == null)
                {
                    item = __instance.FindCookableItem(user.GetInventory());
                    if (item == null)
                    {
                        user.Message(MessageHud.MessageType.Center, "$msg_noprocessableitems");
                        return;
                    }
                }
                if (!__instance.IsItemAllowed(item.m_dropPrefab.name))
                {
                    user.Message(MessageHud.MessageType.Center, "$msg_wontwork");
                    return;
                }
                if (__instance.GetQueueSize() >= __instance.m_maxOre)
                {
                    user.Message(MessageHud.MessageType.Center, "$msg_itsfull");
                    return;
                }
                user.Message(MessageHud.MessageType.Center, "$msg_added " + item.m_shared.m_name);
                user.GetInventory().RemoveItem(item, 1);
                __instance.QueueOre(item.m_shared.m_name);
                __instance.m_oreAddedEffects.Create(__instance.transform.position, __instance.transform.rotation);
                return;
            }
        }
    }
}

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
    internal class OfferingBowl_Patch
    {
        [HarmonyPatch(typeof(OfferingBowl), "Start")]
        [HarmonyPrefix]
        private static bool Start(OfferingBowl __instance)
        {
            var m_nview = __instance.GetComponentInParent<ZNetView>();

            if (m_nview != null && ZNet.instance.IsServer())
            {
                // What does this even do?!
                m_nview.Register("Interact", (long sender) =>
                {
                    RPC_Interact(__instance, sender);
                });

                m_nview.Register("UseItem", (long sender, int itemId) =>
                {
                    RPC_UseItem(__instance, sender, itemId);
                });
            }

            return false;
        }

        [HarmonyPatch(typeof(OfferingBowl), "Interact")]
        [HarmonyPrefix]
        private static bool Interact(OfferingBowl __instance, ref bool __result, Humanoid user, bool hold)
        {
            __result = false;
            if (hold)
            {
                return false;
            }

            __instance.GetComponent<ZNetView>().InvokeRPC("Interact");
            return false;
        }

        private static void RPC_Interact(OfferingBowl __instance, long sender)
        {
            var peer = ZNet.instance.GetPeer(sender);
            if (peer == null)
                return;

            var player = peer.m_player;
            if (player == null)
                return;

            if ((peer.m_player.transform.position - __instance.transform.position).sqrMagnitude > peer.m_player.m_maxInteractDistance * peer.m_player.m_maxInteractDistance)
                return;

            if (!PrivateArea_Patch.CheckAccess(sender, __instance.transform.position))
                return;

            if (__instance.IsBossSpawnQueued())
            {
                return;
            }
            if (__instance.m_useItemStands)
            {
                List<ItemStand> list = __instance.FindItemStands();
                foreach (ItemStand item in list)
                {
                    if (!item.HaveAttachment())
                    {
                        player.Message(MessageHud.MessageType.Center, "$msg_incompleteoffering");
                        return;
                    }
                }
                if (__instance.SpawnBoss(__instance.transform.position))
                {
                    player.Message(MessageHud.MessageType.Center, "$msg_offerdone");
                    foreach (ItemStand item2 in list)
                    {
                        item2.DestroyAttachment();
                    }
                    if ((bool)__instance.m_itemSpawnPoint)
                    {
                        __instance.m_fuelAddedEffects.Create(__instance.m_itemSpawnPoint.position, __instance.transform.rotation);
                    }
                }
                return;
            }
        }


        [HarmonyPatch(typeof(OfferingBowl), "UseItem")]
        [HarmonyPrefix]
        private static bool UseItem(OfferingBowl __instance, ref bool __result, Humanoid user, ItemDrop.ItemData item)
        {
            var nview = __instance.GetComponentInParent<ZNetView>();
            if (nview != null && item != null)
            {
                nview.InvokeRPC("UseItem", item.m_id);
            }
            __result = true;
            return false;
        }

        private static void RPC_UseItem(OfferingBowl __instance, long sender, int itemId)
        {
            var peer = ZNet.instance.GetPeer(sender);
            if (peer == null)
                return;

            var player = peer.m_player;
            if (player == null)
                return;

            if ((peer.m_player.transform.position - __instance.transform.position).sqrMagnitude > peer.m_player.m_maxInteractDistance * peer.m_player.m_maxInteractDistance)
                return;

            if (!PrivateArea_Patch.CheckAccess(sender, __instance.transform.position))
                return;

            var item = player.m_inventory.m_inventory.SingleOrDefault(k => k.m_id == itemId);
            if (item == null)
                return;


            // From here on its pretty much copy pasta with replaced variables
            if (__instance.m_useItemStands)
            {
                return;
            }
            if (__instance.IsBossSpawnQueued())
            {
                return;
            }
            if (__instance.m_bossItem != null)
            {
                if (item.m_shared.m_name == __instance.m_bossItem.m_itemData.m_shared.m_name)
                {
                    int num = player.GetInventory().CountItems(__instance.m_bossItem.m_itemData.m_shared.m_name);
                    if (num < __instance.m_bossItems)
                    {
                        player.Message(MessageHud.MessageType.Center, "$msg_incompleteoffering: " + __instance.m_bossItem.m_itemData.m_shared.m_name + " " + num + " / " + __instance.m_bossItems);
                        return;
                    }
                    if (__instance.m_bossPrefab != null)
                    {
                        if (__instance.SpawnBoss(__instance.transform.position))
                        {
                            player.GetInventory().RemoveItem(item.m_shared.m_name, __instance.m_bossItems);
                            player.ShowRemovedMessage(__instance.m_bossItem.m_itemData, __instance.m_bossItems);
                            player.Message(MessageHud.MessageType.Center, "$msg_offerdone");
                            if ((bool)__instance.m_itemSpawnPoint)
                            {
                                __instance.m_fuelAddedEffects.Create(__instance.m_itemSpawnPoint.position, __instance.transform.rotation);
                            }
                        }
                    }
                    else if (__instance.m_itemPrefab != null && __instance.SpawnItem(__instance.m_itemPrefab, player))
                    {
                        player.GetInventory().RemoveItem(item.m_shared.m_name, __instance.m_bossItems);
                        player.ShowRemovedMessage(__instance.m_bossItem.m_itemData, __instance.m_bossItems);
                        player.Message(MessageHud.MessageType.Center, "$msg_offerdone");
                        __instance.m_fuelAddedEffects.Create(__instance.m_itemSpawnPoint.position, __instance.transform.rotation);
                    }
                    if (!string.IsNullOrEmpty(__instance.m_setGlobalKey))
                    {
                        ZoneSystem.instance.SetGlobalKey(__instance.m_setGlobalKey);
                    }
                    return;
                }
                player.Message(MessageHud.MessageType.Center, "$msg_offerwrong");
            }
        }
    }
}

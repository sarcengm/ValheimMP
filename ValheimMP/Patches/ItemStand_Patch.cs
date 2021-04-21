using HarmonyLib;
using System.Linq;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class ItemStand_Patch
    {
        [HarmonyPatch(typeof(ItemStand), "Awake")]
        [HarmonyPostfix]
        private static void Awake(ItemStand __instance)
        {
            if (__instance.m_nview && ZNet.instance.IsServer())
            {
                __instance.m_nview.Register("DelayedPowerActivation", (long sender) =>
                {
                    RPC_DelayedPowerActivation(__instance, sender);
                });
                __instance.m_nview.Register("AttachItem", (long sender, int itemId) =>
                {
                    RPC_AttachItem(__instance, sender, itemId);
                });
            }
        }

        [HarmonyPatch(typeof(ItemStand), "RPC_DropItem")]
        [HarmonyPrefix]
        private static bool RPC_DropItem(ItemStand __instance, long sender)
        {
            var peer = ZNet.instance.GetPeer(sender);

            if (peer != null && peer.m_player != null)
            {
                if ((peer.m_player.transform.position - __instance.transform.position).sqrMagnitude > peer.m_player.m_maxInteractDistance * peer.m_player.m_maxInteractDistance)
                {
                    return false;
                }

                return PrivateArea_Patch.CheckAccess(sender, __instance.transform.position);
            }

            return false;
        }

        [HarmonyPatch(typeof(ItemStand), "RPC_DestroyAttachment")]
        [HarmonyPrefix]
        private static bool RPC_DestroyAttachment(ItemStand __instance, long sender)
        {
            var peer = ZNet.instance.GetPeer(sender);

            if (peer != null && peer.m_player != null)
            {
                if ((peer.m_player.transform.position - __instance.transform.position).sqrMagnitude > peer.m_player.m_maxInteractDistance * peer.m_player.m_maxInteractDistance)
                {
                    return false;
                }

                return PrivateArea_Patch.CheckAccess(sender, __instance.transform.position);
            }

            return false;
        }

        [HarmonyPatch(typeof(ItemStand), "DelayedPowerActivation")]
        [HarmonyPrefix]
        private static bool DelayedPowerActivation(ItemStand __instance)
        {
            __instance.m_nview.InvokeRPC("DelayedPowerActivation");
            return false;
        }

        [HarmonyPatch(typeof(ItemStand), "RPC_SetVisualItem")]
        [HarmonyPrefix]
        private static bool RPC_SetVisualItem(ItemStand __instance, long sender)
        {
            // Clients shouldn't be able to call this on the server
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }

        private static void RPC_DelayedPowerActivation(ItemStand __instance, long sender)
        {
            var peer = ZNet.instance.GetPeer(sender);

            if (peer != null && peer.m_player != null)
            {
                if ((peer.m_player.transform.position - __instance.transform.position).sqrMagnitude <= peer.m_player.m_maxInteractDistance * peer.m_player.m_maxInteractDistance)
                {
                    peer.m_player.SetGuardianPower(__instance.m_guardianPower.name);
                }
            }
        }


        [HarmonyPatch(typeof(ItemStand), "UseItem")]
        [HarmonyPrefix]
        private static bool UseItem(ItemStand __instance, ref bool __result, Humanoid user, ItemDrop.ItemData item)
        {
            __result = true;
            if (__instance.HaveAttachment())
            {
                return false;
            }
            if (!__instance.CanAttach(item))
            {
                user.Message(MessageHud.MessageType.Center, "$piece_itemstand_cantattach");
                return false;
            }
            if (!__instance.m_nview.IsOwner())
            {
                __instance.m_nview.InvokeRPC("AttachItem", item.m_id);
            }
            return false;
        }

        private static void RPC_AttachItem(ItemStand __instance, long sender, int itemId)
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
            if (item != null && player.GetInventory().ContainsItem(item) && !__instance.HaveAttachment())
            {
                ItemDrop.ItemData itemData = item.Clone();
                itemData.m_stack = 1;
                __instance.m_nview.GetZDO().Set("item", item.m_dropPrefab.name);
                ItemDrop.SaveToZDO(itemData, __instance.m_nview.GetZDO());
                player.UnequipItem(item);
                player.GetInventory().RemoveOneItem(item);
                __instance.m_nview.InvokeRPC(ZNetView.Everybody, "SetVisualItem", itemData.m_dropPrefab.name, itemData.m_variant);
                Transform attach = __instance.GetAttach(item);
                __instance.m_effects.Create(attach.transform.position, Quaternion.identity);
            }
        }
    }
}

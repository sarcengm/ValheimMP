using HarmonyLib;
using System.Linq;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Inventory_Patch
    {
        /// <summary>
        /// Unregister items attached to netviews when reset, ideally objects would unregister on destruction
        /// but looking at the code it seems they lose their ZDO before destruction, since we need that ID we
        /// hook into ResetZDO. And I don't want to copy paste this same patch for every object type I will 
        /// unregister all of them here.
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPatch(typeof(ZNetView), "ResetZDO")]
        [HarmonyPatch(typeof(ZNetView), "Destroy")]
        [HarmonyPrefix]
        private static void UnregisterNetViewPatch(ZNetView __instance)
        {
            ValheimMP.Instance.InventoryManager.UnregisterAll(__instance);
        }

        /// <summary>
        /// Yes, I actually patch Inventory in Inventory_Patch.cs
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="fromInventory"></param>
        /// <param name="item"></param>
        /// <returns></returns>

        [HarmonyPatch(typeof(Inventory), "MoveItemToThis", new[] { typeof(Inventory), typeof(ItemDrop.ItemData) })]
        [HarmonyPrefix]
        private static bool MoveItemToThis(Inventory __instance, Inventory fromInventory, ItemDrop.ItemData item)
        {
            if (ZNet.instance.IsServer())
                return true;

            var toId = __instance.m_nview.m_zdo.m_uid;
            var fromId = fromInventory.m_nview.m_zdo.m_uid;
            var itemId = item.m_id;
            ZNet.instance.GetServerRPC().Invoke("MoveItemToThis", toId, fromId, itemId);
            return false;
        }

        public static void RPC_MoveItemToThis(ZRpc rpc, ZDOID toId, ZDOID fromId, int itemId)
        {
            var toInv = ValheimMP.Instance.InventoryManager.GetInventory(toId);
            if (toInv == null)
            {
                ValheimMP.Log($"Missing to inventory RPC_MoveItemToThis toId:{toId} fromId:{fromId} itemId:{itemId} ");
                return;
            }
            var fromInv = ValheimMP.Instance.InventoryManager.GetInventory(fromId);
            if (fromInv == null)
            {
                ValheimMP.Log($"Missing from inventory RPC_MoveItemToThis toId:{toId} fromId:{fromId} itemId:{itemId} ");
                return;
            }
            var item = fromInv.m_inventory.SingleOrDefault(k => k.m_id == itemId);
            if (item == null)
            {
                ValheimMP.Log($"Missing item RPC_MoveItemToThis toId:{toId} fromId:{fromId} itemId:{itemId} ");
                return;
            }

            var peer = ZNet.instance.GetPeer(rpc);
            if (peer == null)
                return;
            if (!ValheimMP.Instance.InventoryManager.IsListener(peer.m_uid, toInv))
            {
                ValheimMP.Log($"RPC_MoveItemToThis without being listener on the source container");
                return;
            }
            if (!ValheimMP.Instance.InventoryManager.IsListener(peer.m_uid, fromInv))
            {
                ValheimMP.Log($"RPC_MoveItemToThis without being listener on the target container");
                return;
            }

            toInv.MoveItemToThis(fromInv, item);
        }

        [HarmonyPatch(typeof(Inventory), "Changed")]
        [HarmonyPostfix]
        private static void Changed(Inventory __instance)
        {
            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                if (__instance != null && __instance.m_nview && __instance.m_nview.m_zdo != null)
                {
                    // The client does not know what inventories contain unless they are listeners on it
                    __instance.m_nview.m_zdo.Set("Inventory_NrOfItems" + __instance.m_inventoryIndex, __instance.m_inventory.Count);
                }
            }
        }

        [HarmonyPatch(typeof(Inventory), "NrOfItems")]
        [HarmonyPrefix]
        private static bool NrOfItems(Inventory __instance, ref int __result)
        {
            if (ZNet.instance.IsServer() || __instance.m_inventory.Count > 0)
            {
                __result = __instance.m_inventory.Count;
            }
            else
            {
                if (__instance.m_nview != null && __instance.m_nview.m_zdo != null)
                {
                    __result = __instance.m_nview.m_zdo.GetInt("Inventory_NrOfItems" + __instance.m_inventoryIndex, 0);
                }
            }
            return false;
        }

        [HarmonyPatch(typeof(Inventory), "SlotsUsedPercentage")]
        [HarmonyPrefix]
        private static bool SlotsUsedPercentage(Inventory __instance, ref float __result)
        {
            __result = (float)__instance.NrOfItems() / (float)(__instance.m_width * __instance.m_height) * 100f;
            return false;
        }
    }

}

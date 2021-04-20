using HarmonyLib;
using System.Linq;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class InventoryGrid_Patch
    {
        public static bool LocalMove { get; private set; }

        [HarmonyPatch(typeof(InventoryGrid), "DropItem")]
        [HarmonyPrefix]
        private static bool DropItem(ref InventoryGrid __instance, ref bool __result, Inventory fromInventory, ItemDrop.ItemData item, int amount, Vector2i pos)
        {
            if (LocalMove || ZNet.instance == null || ZNet.instance.IsServer())
                return true;

            var toId = __instance.m_inventory.m_nview.m_zdo.m_uid;
            var fromId = fromInventory.m_nview.m_zdo.m_uid;

            ZPackage pkg = new ZPackage();
            pkg.Write(toId);
            pkg.Write(fromId);
            pkg.Write(item.m_id);
            pkg.Write(amount);
            pkg.Write(pos);

            //ValheimMP.Log($"Invoke InventoryGrid_DropItem ZPKG: fromId:{fromId} from item.m_id:{item.m_id} toId:{toId} to amount:{amount} to pos:{pos}");

            ZNet.instance.GetServerRPC().Invoke("InventoryGrid_DropItem", pkg);

            __result = true;
            return false;
        }

        /// <summary>
        /// I've put this here because it originated here, but the RPC itself kinda has nothing to do with this grid.
        /// TODO: Add movement restriction where it applies.
        /// </summary>
        /// <param name="rpc"></param>
        /// <param name="pkg"></param>
        public static void RPC_DropItem(ZRpc rpc, ZPackage pkg)
        {
            var toId = pkg.ReadZDOID();
            var fromId = pkg.ReadZDOID();
            var itemId = pkg.ReadInt();
            var amount = pkg.ReadInt();
            var pos = pkg.ReadVector2i();

            var m_inventory = ValheimMP.Instance.InventoryManager.GetInventory(toId);
            if(m_inventory == null)
            {
                ValheimMP.Log($"Missing to inventory RPC_DropItem toId:{toId} fromId:{fromId} itemId:{itemId} amount:{amount} pos:{pos}");
                return;
            }
            var fromInventory = ValheimMP.Instance.InventoryManager.GetInventory(fromId);
            if (fromInventory == null)
            {
                ValheimMP.Log($"Missing from inventory RPC_DropItem toId:{toId} fromId:{fromId} itemId:{itemId} amount:{amount} pos:{pos}");
                return;
            }
            var item = fromInventory.m_inventory.SingleOrDefault(k => k.m_id == itemId);
            if (item == null)
            {
                ValheimMP.Log($"Missing item RPC_DropItem toId:{toId} fromId:{fromId} itemId:{itemId} amount:{amount} pos:{pos}");
                return;
            }

            var peer = ZNet.instance.GetPeer(rpc);
            if (peer == null)
                return;
            if (!ValheimMP.Instance.InventoryManager.IsListener(peer.m_uid, m_inventory))
            {
                ValheimMP.Log($"RPC_DropItem without being listener on the source container");
                return;
            }
            if (!ValheimMP.Instance.InventoryManager.IsListener(peer.m_uid, fromInventory))
            {
                ValheimMP.Log($"RPC_DropItem without being listener on the target container");
                return;
            }

            ItemDrop.ItemData itemAt = m_inventory.GetItemAt(pos.x, pos.y);
            if (itemAt == item)
            {
                return;
            }
            if (itemAt != null && (itemAt.m_shared.m_name != item.m_shared.m_name || (item.m_shared.m_maxQuality > 1 && itemAt.m_quality != item.m_quality) || itemAt.m_shared.m_maxStackSize == 1) && item.m_stack == amount)
            {
                fromInventory.m_inventory.Remove(item);
                fromInventory.MoveItemToThis(m_inventory, itemAt, itemAt.m_stack, item.m_gridPos.x, item.m_gridPos.y);
                m_inventory.AddItem(item, amount, pos.x, pos.y);
                if (item.m_stack > 0)
                    fromInventory.m_inventory.Add(item);
                fromInventory.Changed();
                return;
            }
            m_inventory.MoveItemToThis(fromInventory, item, amount, pos.x, pos.y);
        }
    }
}

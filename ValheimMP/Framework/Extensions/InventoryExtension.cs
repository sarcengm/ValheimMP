using System.Linq;

namespace ValheimMP.Framework.Extensions
{
    public static class InventoryExtension
    {
        public static ZDOID GetZDOID(this Inventory inventory)
        {
            return inventory.m_nview.m_zdo.m_uid;
        }

        public static ItemDrop.ItemData GetItemByID(this Inventory inventory, int itemId)
        {
            return inventory.m_inventory.SingleOrDefault(k => k.m_id == itemId);
        }

        /// <summary>
        /// Get the index for this inventory inside their owning netview
        /// </summary>
        /// <param name="inventory"></param>
        /// <returns></returns>
        public static int GetIndex(this Inventory inventory)
        {
            return inventory.m_inventoryIndex;
        }

        /// <summary>
        /// Get the owning netview for this inventory
        /// </summary>
        /// <param name="inventory"></param>
        /// <returns></returns>
        public static ZNetView GetNetView(this Inventory inventory)
        {
            return inventory.m_nview;
        }
    }
}

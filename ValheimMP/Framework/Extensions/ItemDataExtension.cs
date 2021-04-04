using ValheimMP.Patches;

namespace ValheimMP.Framework.Extensions
{

    public static class ItemDataExtension
    {
        /// <summary>
        /// Get unique runtime ID
        /// 
        /// This ID identifies an itemData over the network, this is a non persistant ID, it will not be saved and changes every restart.
        /// It can also change by small actions in the inventory that clone the object instead of moving it.
        /// </summary>
        /// <param name="itemData"></param>
        /// <returns></returns>
        public static int GetNetworkID(this ItemDrop.ItemData itemData)
        {
            return itemData.m_id;
        }

        /// <summary>
        /// Assigns a new id to this item data. Why? because some mods *looks ExtendedItemData's way* don't like playing nice when you just modify certain members
        /// Like this it will destroy the old item and recreate the item in a way that the mod accepts.
        /// </summary>
        /// <param name="itemData"></param>
        /// <returns></returns>
        public static int AssignNewId(this ItemDrop.ItemData itemData)
        {
            itemData.m_id = ++ItemDrop_Patch.itemDataId;
            return itemData.m_id;
        }

        /// <summary>
        /// Set the CraftTrigger
        /// 
        /// if the craft trigger > 0 then the item will generate an InventoryManager.OnCraftedItem event after it replicates to the client
        /// </summary>
        /// <param name="itemData"></param>
        /// <param name="trigger"></param>
        /// <param name="triggerData">Additional data included.</param>
        public static void SetCraftTrigger(this ItemDrop.ItemData itemData, int trigger, byte[] triggerData = null)
        {
            itemData.m_crafted = trigger;
            itemData.m_craftedData = triggerData;
        }

        /// <summary>
        /// Add custom networked variables
        /// </summary>
        /// <param name="itemData"></param>
        /// <param name="name"></param>
        /// <param name="data"></param>
        public static void SetCustomData(this ItemDrop.ItemData itemData, string name, byte[] data)
        {
            itemData.m_customData.SetCustomData(name.GetStableHashCode(), data);
        }

        public static void SetCustomData(this ItemDrop.ItemData itemData, int hash, byte[] data)
        {
            itemData.m_customData.SetCustomData(hash, data);
        }

        public static byte[] GetCustomData(this ItemDrop.ItemData itemData, string name)
        {
            return itemData.m_customData.GetCustomData(name.GetStableHashCode());
        }

        public static byte[] GetCustomData(this ItemDrop.ItemData itemData, int hash)
        {
            return itemData.m_customData.GetCustomData(hash);
        }

        public static void SetCustomData<T>(this ItemDrop.ItemData itemData, string name, T value) where T : unmanaged
        {
            itemData.m_customData.SetCustomData(name, value);
        }

        public static T GetCustomData<T>(this ItemDrop.ItemData itemData, string name) where T : unmanaged
        {
            return itemData.m_customData.GetCustomData<T>(name);
        }

        public static string GetCustomData<T>(this ItemDrop.ItemData itemData, string name, string def = default)
        {
            return itemData.m_customData.GetCustomData<T>(name);
        }

        public static void SetCustomData(this ItemDrop.ItemData itemData, string name, string value)
        {
            itemData.m_customData.SetCustomData(name, value);
        }
    }
}

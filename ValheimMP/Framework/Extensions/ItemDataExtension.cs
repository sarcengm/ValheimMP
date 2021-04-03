using System;
using System.Runtime.InteropServices;
using System.Text;

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
        public static int GetID(this ItemDrop.ItemData itemData)
        {
            return itemData.m_id;
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

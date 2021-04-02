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
            itemData.SetCustomData(name.GetStableHashCode(), data);
        }

        public static void SetCustomData(this ItemDrop.ItemData itemData, int hash, byte[] data)
        {
            itemData.m_customData[hash] = data;
        }

        public static byte[] GetCustomData(this ItemDrop.ItemData itemData, string name)
        {
            return itemData.GetCustomData(name.GetStableHashCode());
        }

        public static byte[] GetCustomData(this ItemDrop.ItemData itemData, int hash)
        {
            if (itemData.m_customData.TryGetValue(hash, out var val))
            {
                return val;
            }

            return null;
        }

        public static void SetCustomData<T>(this ItemDrop.ItemData itemData, string name, T value) where T : unmanaged
        {
            unsafe
            {
                var bytes = new byte[sizeof(T)];
                Marshal.Copy(new IntPtr(&value), bytes, 0, bytes.Length);
                SetCustomData(itemData, name, bytes);
            }
        }

        public static T GetCustomData<T>(this ItemDrop.ItemData itemData, string name) where T : unmanaged
        {
            T value;
            unsafe
            {
                var bytes = GetCustomData(itemData, name);
                if (bytes == null || bytes.Length != sizeof(T))
                    return default;
                Marshal.Copy(bytes, 0, new IntPtr(&value), bytes.Length);
            }
            return value;
        }

        public static string GetCustomData<T>(this ItemDrop.ItemData itemData, string name, string def = default)
        {
            var bytes = GetCustomData(itemData, name);
            if (bytes == null)
                return def;
            return Encoding.UTF8.GetString(bytes);
        }

        public static void SetCustomData(this ItemDrop.ItemData itemData, string name, string value)
        {
            SetCustomData(itemData, name, Encoding.UTF8.GetBytes(value));
        }
    }
}

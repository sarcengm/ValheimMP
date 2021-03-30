using System;

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

        public static void SetCustomData(this ItemDrop.ItemData itemData, string name, bool value)
        {
            itemData.SetCustomData(name, BitConverter.GetBytes(value));
        }

        public static bool GetCustomDataBool(this ItemDrop.ItemData itemData, string name)
        {
            var bytes = itemData.GetCustomData(name);
            if (bytes == null || bytes.Length != sizeof(bool))
                return default;
            return BitConverter.ToBoolean(itemData.GetCustomData(name), 0);
        }

        public static void SetCustomData(this ItemDrop.ItemData itemData, string name, int value)
        {
            itemData.SetCustomData(name, BitConverter.GetBytes(value));
        }

        public static int GetCustomDataInt(this ItemDrop.ItemData itemData, string name)
        {
            var bytes = itemData.GetCustomData(name);
            if (bytes == null || bytes.Length != sizeof(int))
                return default;
            return BitConverter.ToInt32(itemData.GetCustomData(name), 0);
        }

        public static void SetCustomData(this ItemDrop.ItemData itemData, string name, long value)
        {
            itemData.SetCustomData(name, BitConverter.GetBytes(value));
        }

        public static long GetCustomDataLong(this ItemDrop.ItemData itemData, string name)
        {
            var bytes = itemData.GetCustomData(name);
            if (bytes == null || bytes.Length != sizeof(long))
                return default;
            return BitConverter.ToInt64(itemData.GetCustomData(name), 0);
        }

        public static void SetCustomData(this ItemDrop.ItemData itemData, string name, float value)
        {
            itemData.SetCustomData(name, BitConverter.GetBytes(value));
        }

        public static float GetCustomDataFloat(this ItemDrop.ItemData itemData, string name)
        {
            var bytes = itemData.GetCustomData(name);
            if (bytes == null || bytes.Length != sizeof(float))
                return default;
            return BitConverter.ToSingle(itemData.GetCustomData(name), 0);
        }
    }
}

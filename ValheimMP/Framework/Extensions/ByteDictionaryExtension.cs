using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace ValheimMP.Framework.Extensions
{
    public static class ByteDictionaryExtension
    {
        public static bool CustomDataExists(this Dictionary<int, byte[]> dic, string name)
        {
            return dic.ContainsKey(name.GetStableHashCode());
        }

        public static void SetCustomData(this Dictionary<int, byte[]> dic, string name, byte[] data)
        {
            dic.SetCustomData(name.GetStableHashCode(), data);
        }

        public static void SetCustomData(this Dictionary<int, byte[]> dic, int hash, byte[] data)
        {
            dic[hash] = data;
        }

        public static byte[] GetCustomData(this Dictionary<int, byte[]> dic, string name)
        {
            return dic.GetCustomData(name.GetStableHashCode());
        }

        public static byte[] GetCustomData(this Dictionary<int, byte[]> dic, int hash)
        {
            if (dic.TryGetValue(hash, out var val))
            {
                return val;
            }

            return null;
        }

        public static void SetCustomData<T>(this Dictionary<int, byte[]> dic, string name, T value) where T : unmanaged
        {
            unsafe
            {
                var bytes = new byte[sizeof(T)];
                Marshal.Copy(new IntPtr(&value), bytes, 0, bytes.Length);
                dic.SetCustomData(name, bytes);
            }
        }

        public static T GetCustomData<T>(this Dictionary<int, byte[]> dic, string name) where T : unmanaged
        {
            T value;
            unsafe
            {
                var bytes = dic.GetCustomData(name);
                if (bytes == null || bytes.Length != sizeof(T))
                    return default;
                Marshal.Copy(bytes, 0, new IntPtr(&value), bytes.Length);
            }
            return value;
        }

        public static string GetCustomData<T>(this Dictionary<int, byte[]> dic, string name, string def = default)
        {
            var bytes = dic.GetCustomData(name);
            if (bytes == null)
                return def;
            return Encoding.UTF8.GetString(bytes);
        }

        public static void SetCustomData(this Dictionary<int, byte[]> dic, string name, string value)
        {
            dic.SetCustomData(name, Encoding.UTF8.GetBytes(value));
        }
    }
}

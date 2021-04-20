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

        // UnsafeCompare
        // Copyright (c) 2008-2013 Hafthor Stefansson
        // Distributed under the MIT/X11 software license
        // Ref: http://www.opensource.org/licenses/mit-license.php.
        public static unsafe bool UnsafeCompare(byte[] a1, byte[] a2)
        {
            if (a1 == a2) return true;
            if (a1 == null || a2 == null || a1.Length != a2.Length)
                return false;
            fixed (byte* p1 = a1, p2 = a2)
            {
                byte* x1 = p1, x2 = p2;
                int l = a1.Length;
                for (int i = 0; i < l / 8; i++, x1 += 8, x2 += 8)
                    if (*((long*)x1) != *((long*)x2)) return false;
                if ((l & 4) != 0) { if (*((int*)x1) != *((int*)x2)) return false; x1 += 4; x2 += 4; }
                if ((l & 2) != 0) { if (*((short*)x1) != *((short*)x2)) return false; x1 += 2; x2 += 2; }
                if ((l & 1) != 0) if (*((byte*)x1) != *((byte*)x2)) return false;
                return true;
            }
        }
    }
}

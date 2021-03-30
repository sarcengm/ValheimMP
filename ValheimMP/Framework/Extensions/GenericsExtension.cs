using System.Collections.Generic;

namespace ValheimMP.Framework.Extensions
{
    public static class GenericsExtension
    {
        public static bool AddNotNull<T>(this List<T> list, T val)
        {
            if (val != null)
            {
                list.Add(val);
                return true;
            }
            return false;
        }

        public static bool AddUnique<K, V>(this Dictionary<K, V> dic, K key, V val)
        {
            if(!dic.ContainsKey(key))
            {
                dic.Add(key, val);
                return true;
            }
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Util
{
    public static class GenericsExt
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

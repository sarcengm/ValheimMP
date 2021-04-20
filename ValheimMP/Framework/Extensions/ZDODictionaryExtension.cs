using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ValheimMP.Framework.Extensions
{
    public static class ZDODictionaryExtension
    {
        // I wanted to just implement specific types of generics with just a different compare function but it seems that isn't (properly) possible in C# so it ended up being... this.

        /// <summary>
        /// Update dictionary value
        /// </summary>
        /// <returns>true if value was changed or is new</returns>
        public static bool UpdateValue(this Dictionary<int, Quaternion> dic, int valKey, Quaternion val)
        {
            Quaternion currentVal;
            if (val != null && dic.TryGetValue(valKey, out currentVal))
            {
                if (Math.Abs(currentVal.x - val.x) > 0.01f ||
                    Math.Abs(currentVal.y - val.y) > 0.01f ||
                    Math.Abs(currentVal.z - val.z) > 0.01f ||
                    Math.Abs(currentVal.w - val.w) > 0.01f)
                {
                    dic[valKey] = val;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            dic.Add(valKey, val);
            return true;
        }

        /// <summary>
        /// Update dictionary value
        /// </summary>
        /// <returns>true if value was changed or is new</returns>
        public static bool UpdateValue(this Dictionary<int, Vector3> dic, int valKey, Vector3 val)
        {
            if (val == null)
                return false;
            Vector3 currentVal;
            if (dic.TryGetValue(valKey, out currentVal))
            {
                if (Math.Abs(currentVal.x - val.x) > 0.01f ||
                    Math.Abs(currentVal.z - val.z) > 0.01f ||
                    Math.Abs(currentVal.y - val.y) > 0.01f)
                {
                    dic[valKey] = val;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            dic.Add(valKey, val);
            return true;
        }

        /// <summary>
        /// Update dictionary value
        /// </summary>
        /// <returns>true if value was changed or is new</returns>
        public static bool UpdateValue(this Dictionary<int, float> dic, int valKey, float val)
        {
            float currentVal;
            if (dic.TryGetValue(valKey, out currentVal))
            {
                if (Math.Abs(currentVal - val) > 0.01f)
                {
                    dic[valKey] = val;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            dic.Add(valKey, val);
            return true;
        }

        /// <summary>
        /// Update dictionary value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dic"></param>
        /// <param name="valKey">key</param>
        /// <param name="val">value</param>
        /// <returns>true if value was changed or is new</returns>
        public static bool UpdateValue(this Dictionary<int, int> dic, int valKey, int val)
        {
            int currentVal;
            if (dic.TryGetValue(valKey, out currentVal))
            {
                if (currentVal != val)
                {
                    dic[valKey] = val;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            dic.Add(valKey, val);
            return true;
        }

        /// <summary>
        /// Update dictionary value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dic"></param>
        /// <param name="valKey">key</param>
        /// <param name="val">value</param>
        /// <returns>true if value was changed or is new</returns>
        public static bool UpdateValue(this Dictionary<int, string> dic, int valKey, string val)
        {
            string currentVal;
            if (dic.TryGetValue(valKey, out currentVal))
            {
                if (currentVal != val)
                {
                    dic[valKey] = val;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            dic.Add(valKey, val);
            return true;
        }

        public static bool UpdateValue(this Dictionary<int, byte[]> dic, int valKey, byte[] val)
        {
            byte[] currentVal;
            if (dic.TryGetValue(valKey, out currentVal))
            {
                if (!ByteDictionaryExtension.UnsafeCompare(val, currentVal))
                {
                    dic[valKey] = val;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            dic.Add(valKey, val);
            return true;
        }

        /// <summary>
        /// Update dictionary value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dic"></param>
        /// <param name="valKey">key</param>
        /// <param name="val">value</param>
        /// <returns>true if value was changed or is new</returns>
        public static bool UpdateValue(this Dictionary<int, bool> dic, int valKey, bool val)
        {
            bool currentVal;
            if (dic.TryGetValue(valKey, out currentVal))
            {
                if (currentVal != val)
                {
                    dic[valKey] = val;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            dic.Add(valKey, val);
            return true;
        }

        /// <summary>
        /// Update dictionary value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dic"></param>
        /// <param name="valKey">key</param>
        /// <param name="val">value</param>
        /// <returns>true if value was changed or is new</returns>
        public static bool UpdateValue(this Dictionary<int, long> dic, int valKey, long val)
        {
            long currentVal;
            if (dic.TryGetValue(valKey, out currentVal))
            {
                if (currentVal != val)
                {
                    dic[valKey] = val;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            dic.Add(valKey, val);
            return true;
        }

        public static void Increment(this Dictionary<string, int> dic, string key)
        {
            if (dic.TryGetValue(key, out var val)) 
            {
                dic[key] = val + 1;
            }
            else
            {
                dic.Add(key, 1);
            }
        }
    }
}

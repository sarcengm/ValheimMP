using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ValheimMP.Framework.Extensions
{
    public static class GameObjectExtension
    {
        /// <summary>
        /// Get the full name of an object, up to but excluding parent.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static string GetFullName(this GameObject gameObject, GameObject parent = null)
        {
            List<GameObject> list = gameObject.GetHierarchy(parent);
            return list.Join(k => k.name, "/");
        }

        /// <summary>
        /// Get the entire hierarchy of game objects, up to but excluding parent.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static List<GameObject> GetHierarchy(this GameObject gameObject, GameObject parent = null)
        {
            var list = new List<GameObject>();
            var loopObject = gameObject.transform;
            while (loopObject != null)
            {
                if (loopObject.gameObject == parent)
                    break;
                list.Add(loopObject.gameObject);
                loopObject = loopObject.parent;
            }
            list.Reverse();
            return list;
        }
    }
}

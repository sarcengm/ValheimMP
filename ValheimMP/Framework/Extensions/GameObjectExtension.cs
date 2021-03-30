using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ValheimMP.Framework.Extensions
{
    public static class GameObjectExtension
    {
        public static string GetFullName(this GameObject gameObject)
        {
            List<GameObject> list = GetHierarchy(ref gameObject);

            return list.Join(k => k.name, "/");
        }

        public static List<GameObject> GetHierarchy(ref GameObject gameObject)
        {
            var list = new List<GameObject>();
            var loopObject = gameObject.transform;
            while (loopObject != null)
            {
                list.Add(loopObject.gameObject);

                loopObject = loopObject.parent;
            }
            list.Reverse();
            return list;
        }

        public static T GetChildByFullPathAndType<T>(this Component component, string path) where T : Component
        {
            return component.gameObject.GetChildByFullPathAndType<T>(path);
        }

        public static T GetChildByFullPathAndType<T>(this GameObject gameObject, string path) where T : Component
        {
            var targetPath = path.Split('/');
            var myPath = GetHierarchy(ref gameObject);

            if (myPath.Count > targetPath.Length)
            {
                return null;
            }

            for (int i = 0; i < myPath.Count; i++)
            {
                if (myPath[i].name != targetPath[i])
                {
                    return null;
                }
            }

            var children = gameObject.GetComponentsInChildren<T>();
            for (int c = 0; c < children.Length; c++)
            {
                var child = children[c].gameObject;
                var childHierarchy = GetHierarchy(ref child);


                if (childHierarchy.Count != targetPath.Length)
                    continue;

                var foundMatch = true;
                for (int j = myPath.Count; j < targetPath.Length; j++)
                {
                    if (targetPath[j] != childHierarchy[j].name)
                    {
                        foundMatch = false;
                        break;
                    }
                }

                if (foundMatch)
                {
                    return children[c];
                }
            }

            return null;
        }
    }
}

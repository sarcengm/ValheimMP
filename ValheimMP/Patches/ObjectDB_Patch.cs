using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    class ObjectDB_Patch
    {
        [HarmonyPatch(typeof(ObjectDB), "GetAllItems")]
        [HarmonyPrefix]
        private static bool GetAllItems(ObjectDB __instance, ref List<ItemDrop> __result, ItemDrop.ItemData.ItemType type, string startWith)
        {
            __result = new List<ItemDrop>();
            for (int i = 0; i < __instance.m_items.Count; i++)
            {
                var item = __instance.m_items[i];
                if (item == null)
                {
                    __instance.m_items.RemoveAt(i);
                    i--;
                    ValheimMP.Log("Null in ObjectDB items");
                    continue;
                }

                ItemDrop component = item.GetComponent<ItemDrop>();
                if (!component)
                {
                    __instance.m_items.RemoveAt(i);
                    i--;
                    ValheimMP.Log($"{item} without itemdrop in ObjectDB items");
                    continue;
                }

                if (!component.gameObject)
                {
                    __instance.m_items.RemoveAt(i);
                    i--;
                    ValheimMP.Log($"{item} without gameObject in ObjectDB items");
                    continue;
                }

                if (component.gameObject.name == null)
                {
                    ValheimMP.LogWarning($"{item} with null name in ObjectDB items");
                    continue;
                }

                if (component.m_itemData.m_shared.m_itemType == type && component.gameObject.name.StartsWith(startWith))
                {
                    __result.Add(component);
                }
            }

            return false;
        }
    }
}

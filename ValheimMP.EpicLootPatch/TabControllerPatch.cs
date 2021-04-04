using EpicLoot.Crafting;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using static EpicLoot.Crafting.CraftingTabs;

namespace ValheimMP.EpicLootPatch
{
    public static class TabControllerPatchListExt
    {
        public static void AddIfNoneExist<T>(this List<TabController> list) where T : TabController, new()
        {
            if (list.SingleOrDefault(k => k.GetType() == typeof(T)) == null)
            {
                list.Add(new T());
            }
        }

        public static void RemoveIfExist<T>(this List<TabController> list) where T : TabController
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].GetType() == typeof(T))
                {
                    list.RemoveAt(i);
                    break;
                }
            }
        }
    }
    [HarmonyPatch]
    class TabControllerPatch
    {
        [HarmonyPatch(typeof(InventoryGui), "Awake")]
        [HarmonyAfter(EpicLoot.EpicLoot.PluginId)]
        [HarmonyPostfix]
        private static void Awake()
        {
            AddTabControllers();
        }


        public static void AddTabControllers()
        {
            TabControllers.RemoveIfExist<DisenchantTabController>();
            TabControllers.RemoveIfExist<EnchantTabController>();
            TabControllers.RemoveIfExist<AugmentTabController>();

            TabControllers.AddIfNoneExist<VMPDisenchantTabController>();
            TabControllers.AddIfNoneExist<VMPEnchantTabController>();
            TabControllers.AddIfNoneExist<VMPAugmentTabController>();
        }

        public static void RemoveTabControllers()
        {
            TabControllers.RemoveIfExist<VMPDisenchantTabController>();
            TabControllers.RemoveIfExist<VMPEnchantTabController>();
            TabControllers.RemoveIfExist<VMPAugmentTabController>();

            TabControllers.AddIfNoneExist<DisenchantTabController>();
            TabControllers.AddIfNoneExist<EnchantTabController>();
            TabControllers.AddIfNoneExist<AugmentTabController>();
        }
    }
}

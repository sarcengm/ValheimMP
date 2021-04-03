using EpicLoot.Crafting;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static EpicLoot.Crafting.CraftingTabs;

namespace ValheimMP.EpicLootPatch
{
    [HarmonyPatch]
    class TabControllerPatch
    {
        [HarmonyPatch(typeof(InventoryGui), "Awake")]
        [HarmonyAfter(EpicLoot.EpicLoot.PluginId)]
        [HarmonyPostfix]
        private static void Awake()
        {
            foreach (var controller in TabControllers.ToList())
            {
                if (controller is AugmentTabController ||
                    controller is DisenchantTabController ||
                    controller is EnchantTabController)
                {
                    TabControllers.Remove(controller);
                }
            }

            TabControllers.Add(new VMPDisenchantTabController());
            TabControllers.Add(new VMPEnchantTabController());
            TabControllers.Add(new VMPAugmentTabController());
        }
    }
}

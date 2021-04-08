using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.EpicLootPatch
{
    [HarmonyPatch(typeof(StoreGui))]
    public static class StoreGui_Patch
    {
        [HarmonyPatch("Show")]
        [HarmonyPostfix]
        [HarmonyAfter(EpicLoot.EpicLoot.PluginId)]
        public static void Show_Postfix()
        {
            // And the bugs are gone! it's a miracle! (Well so are the features but oh well, maybe one day I can be bothered to fix it)
            EpicLoot.Adventure.StoreGui_Patch.MerchantPanel.gameObject.SetActive(false);
        }
    }
}

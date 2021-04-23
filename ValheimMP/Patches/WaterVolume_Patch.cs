using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    class WaterVolume_Patch
    {
        [HarmonyPatch(typeof(WaterVolume), "UpdateFloaters")]
        [HarmonyPrefix]
        private static bool UpdateFloaters(WaterVolume __instance)
        {
            if (!ValheimMP.IsDedicated || Time.time - __instance.m_updateFloatersTime > 0.1)
            {
                __instance.m_updateFloatersTime = Time.time;
                return true;
            }

            return false;
        }
    }
}

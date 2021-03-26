using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Ship_Patch
    {
        [HarmonyPatch(typeof(Ship), "Start")]
        [HarmonyPostfix]
        private static void Start(ref Ship __instance)
        {
            __instance.StopCoroutine("UpdateOwner");
        }
    }
}

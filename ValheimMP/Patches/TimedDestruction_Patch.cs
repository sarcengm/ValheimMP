using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    public class TimedDestruction_Patch
    {
        [HarmonyPatch(typeof(TimedDestruction), "Awake")]
        [HarmonyPostfix]
        private static void Awake(ref TimedDestruction __instance)
        {
            if (ZNet.instance != null && !ZNet.instance.IsServer())
            {
                //DebugMod.LogComponent(__instance, "TimedDestruction on non-server.");
            }
        }
    }
}

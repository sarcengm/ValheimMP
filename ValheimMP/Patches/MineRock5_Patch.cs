using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class MineRock5_Patch
    {
        [HarmonyPatch(typeof(MineRock5), "Damage")]
        [HarmonyPrefix]
        private static bool Damage()
        {
            return ZNet.instance.IsServer();
        }

        [HarmonyPatch(typeof(MineRock5), "RPC_Damage")]
        [HarmonyPrefix]
        private static bool RPC_Damage(ref MineRock5 __instance, long sender, HitData hit, int hitAreaIndex)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }

        [HarmonyPatch(typeof(MineRock5), "RPC_SetAreaHealth")]
        [HarmonyPrefix]
        private static bool RPC_SetAreaHealth(ref MineRock5 __instance, long sender, int index, float health)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }
    }
}

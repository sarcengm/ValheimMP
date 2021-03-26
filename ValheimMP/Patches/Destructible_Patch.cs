using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Destructible_Patch
    {
        [HarmonyPatch(typeof(Destructible), "Damage")]
        [HarmonyPrefix]
        private static bool Damage()
        {
            return ZNet.instance.IsServer();
        }

        [HarmonyPatch(typeof(Destructible), "RPC_Damage")]
        [HarmonyPrefix]
        private static bool RPC_Damage(ref Destructible __instance, long sender, HitData hit)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }
    }
}

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class TreeLog_Patch
    {
        [HarmonyPatch(typeof(TreeLog), "Damage")]
        [HarmonyPrefix]
        private static bool Damage()
        {
            return ZNet.instance.IsServer();
        }

        [HarmonyPatch(typeof(TreeLog), "RPC_Damage")]
        [HarmonyPrefix]
        private static bool RPC_Damage(TreeLog __instance, long sender)
        {
            // Clients shouldn't be able to call this on the server
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }
    }
}

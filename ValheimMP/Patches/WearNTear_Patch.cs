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
    internal class WearNTear_Patch
    {
        [HarmonyPatch(typeof(WearNTear), "RPC_HealthChanged")]
        [HarmonyPrefix]
        private static bool RPC_HealthChanged(WearNTear __instance, long peer)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, peer);
        }

        [HarmonyPatch(typeof(WearNTear), "RPC_CreateFragments")]
        [HarmonyPrefix]
        private static bool RPC_CreateFragments(WearNTear __instance, long peer)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, peer);
        }

        [HarmonyPatch(typeof(WearNTear), "RPC_Damage")]
        [HarmonyPrefix]
        private static bool RPC_Damage(WearNTear __instance, long sender)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }

        [HarmonyPatch(typeof(WearNTear), "RPC_Remove")]
        [HarmonyPrefix]
        private static bool RPC_Remove(WearNTear __instance, long sender)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }

        [HarmonyPatch(typeof(WearNTear), "RPC_Repair")]
        [HarmonyPrefix]
        private static bool RPC_Repair(WearNTear __instance, long sender)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }


        [HarmonyPatch(typeof(WearNTear), "Repair")]
        [HarmonyPrefix]
        private static bool Repair(WearNTear __instance, ref bool __result)
        {
            __result = false;
            if (!__instance.m_nview.IsValid())
            {
                return false;
            }
            if (__instance.m_nview.GetZDO().GetFloat("health", __instance.m_health) >= __instance.m_health)
            {
                return false;
            }
            if (Time.time - __instance.m_lastRepair < 1f)
            {
                return false;
            }
            __instance.m_lastRepair = Time.time;
            if(ZNet.instance.IsServer())
            {
                __instance.RPC_Repair(0);
            }

            __result = true;
            return false;
        }
    }
}

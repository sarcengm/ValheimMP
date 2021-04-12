using HarmonyLib;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class WearNTear_Patch
    {
        [HarmonyPatch(typeof(WearNTear), "Damage")]
        [HarmonyPrefix]
        private static bool Damage(WearNTear __instance, HitData hit)
        {
            if (ValheimMP.IsDedicated)
            {
                __instance.RPC_Damage(0, hit);
            }
            else
            {
                hit.ApplyResistance(__instance.m_damages, out _);
                if (hit.GetTotalDamage() > 0)
                {
                    __instance.m_hitEffect.Create(hit.m_point, Quaternion.identity, __instance.transform);
                }
            }
            return false;
        }

        [HarmonyPatch(typeof(WearNTear), "UpdateWear")]
        [HarmonyPrefix]
        private static bool UpdateWear(WearNTear __instance)
        {
            return __instance && __instance.isActiveAndEnabled;
        }


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

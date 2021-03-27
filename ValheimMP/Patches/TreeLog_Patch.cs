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
    internal class TreeLog_Patch
    {
        [HarmonyPatch(typeof(TreeLog), "Damage")]
        [HarmonyPrefix]
        private static bool Damage(TreeLog __instance, HitData hit)
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

        [HarmonyPatch(typeof(TreeLog), "RPC_Damage")]
        [HarmonyPrefix]
        private static bool RPC_Damage(TreeLog __instance, long sender)
        {
            // Clients shouldn't be able to call this on the server
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }
    }
}

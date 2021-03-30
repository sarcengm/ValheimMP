using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class TreeBase_Patch
    {
        [HarmonyPatch(typeof(TreeBase), "Damage")]
        [HarmonyPrefix]
        private static bool Damage(TreeBase __instance, HitData hit)
        {
            if (ValheimMPPlugin.IsDedicated)
            {
                __instance.RPC_Damage(0, hit);
            }
            else
            {
                hit.ApplyResistance(__instance.m_damageModifiers, out _);
                if (hit.GetTotalDamage() > 0)
                {
                    __instance.m_hitEffect.Create(hit.m_point, Quaternion.identity, __instance.transform);
                }
            }
            return false;
        }

        [HarmonyPatch(typeof(TreeBase), "RPC_Damage")]
        [HarmonyPrefix]
        private static bool RPC_Damage(TreeBase __instance, long sender)
        {
            // Clients shouldn't be able to call this on the server
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }

        [HarmonyPatch(typeof(TreeBase), "RPC_Grow")]
        [HarmonyPrefix]
        private static bool RPC_Grow(TreeBase __instance, long uid)
        {
            // Clients shouldn't be able to call this on the server
            return ZNet_Patch.IsRPCAllowed(__instance, uid);
        }

        [HarmonyPatch(typeof(TreeBase), "RPC_Shake")]
        [HarmonyPrefix]
        private static bool RPC_Shake(TreeBase __instance, long uid)
        {
            // Clients shouldn't be able to call this on the server
            return ZNet_Patch.IsRPCAllowed(__instance, uid);
        }
    }
}

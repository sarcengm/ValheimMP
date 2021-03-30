using HarmonyLib;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Destructible_Patch
    {
        [HarmonyPatch(typeof(Destructible), "Damage")]
        [HarmonyPrefix]
        private static bool Damage(Destructible __instance, HitData hit)
        {
            if (ValheimMPPlugin.IsDedicated)
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

        [HarmonyPatch(typeof(Destructible), "RPC_Damage")]
        [HarmonyPrefix]
        private static bool RPC_Damage(ref Destructible __instance, long sender, HitData hit)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }
    }
}

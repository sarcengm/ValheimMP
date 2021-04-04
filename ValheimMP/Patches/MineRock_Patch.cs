using HarmonyLib;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class MineRock_Patch
    {
        [HarmonyPatch(typeof(MineRock), "Damage")]
        [HarmonyPrefix]
        private static bool Damage(MineRock __instance, HitData hit)
        {
            if (ValheimMP.IsDedicated)
            {
                if (__instance.m_nview == null || !__instance.m_nview.IsValid() || __instance.m_hitAreas == null)
                {
                    return false;
                }
                if (hit.m_hitCollider == null)
                {
                    ValheimMP.Log("Minerock hit has no collider");
                    return false;
                }
                int areaIndex = __instance.GetAreaIndex(hit.m_hitCollider);
                if (areaIndex == -1)
                {
                    ValheimMP.Log("Invalid hit area on " + __instance.gameObject.name);
                    return false;
                }

                __instance.RPC_Hit(0, hit, areaIndex);
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

        [HarmonyPatch(typeof(MineRock), "RPC_Hit")]
        [HarmonyPrefix]
        private static bool RPC_Hit(ref MineRock __instance, long sender, HitData hit, int hitAreaIndex)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }
    }
}

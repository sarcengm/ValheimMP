using HarmonyLib;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class MineRock5_Patch
    {
        [HarmonyPatch(typeof(MineRock5), "Damage")]
        [HarmonyPrefix]
        private static bool Damage(MineRock5 __instance, HitData hit)
        {
            if (ValheimMP.IsDedicated)
            {
                if (!__instance.m_nview || !__instance.m_nview.IsValid() || __instance.m_hitAreas == null)
                {
                    return false;
                }
                if (!hit.m_hitCollider)
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

                __instance.RPC_Damage(0, hit, areaIndex);
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

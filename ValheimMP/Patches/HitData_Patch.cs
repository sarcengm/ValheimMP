using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Patches
{

    [HarmonyPatch]
    class HitData_Patch
    {
        [HarmonyPatch(typeof(HitData), "SetAttacker")]
        [HarmonyPostfix]
        private static void SetAttacker(HitData __instance, Character attacker)
        {
            __instance.m_attackerCharacter = attacker;
        }
    }

    public static class HitDataExtension
    {
        public static long GetAttackingPlayerID(this HitData hitData)
        {
            if (hitData.m_attackerCharacter is Player player)
                return player.GetPlayerID();
            return 0;
        }
    }
}

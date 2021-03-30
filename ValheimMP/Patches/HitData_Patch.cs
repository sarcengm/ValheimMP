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
}

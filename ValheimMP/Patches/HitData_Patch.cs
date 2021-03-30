using HarmonyLib;

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

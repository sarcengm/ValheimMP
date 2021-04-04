using HarmonyLib;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class ZDOID_Patch
    {
        [HarmonyPatch(typeof(ZDOID), "GetHashCode")]
        [HarmonyPrefix]
        private static bool GetHashCode(ref ZDOID __instance, ref int __result)
        {
            if (__instance.m_hash == 0)
            {
                byte userId = 0;
                for (int i = 0; i < sizeof(long); i += sizeof(byte))
                {
                    userId ^= (byte)(__instance.m_userID >> i);
                }

                __instance.m_hash = (int)((userId << 24) ^ __instance.m_id);
                if (__instance.m_hash == 0)
                    __instance.m_hash = 1;
            }
            __result = __instance.m_hash;
            return false;
        }
    }
}

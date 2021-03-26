using HarmonyLib;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    public class FejdStartup_Patch
    {
        public static bool IsPublicPasswordValid(FejdStartup __instance, ref bool __result, string password, World world)
        {
            __result = true;
            return false;
        }

        public static void Awake(FejdStartup __instance)
        {
            ValheimMP.Instance.SetIsOnValheimMPServer(false);
        }
    }
}

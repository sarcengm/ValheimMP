using HarmonyLib;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class FejdStartup_Patch
    {
        internal static bool IsPublicPasswordValid(FejdStartup __instance, ref bool __result, string password, World world)
        {
            __result = true;
            return false;
        }

        internal static void Awake(FejdStartup __instance)
        {
            ValheimMPPlugin.Instance.SetIsOnValheimMPServer(false);
        }
    }
}

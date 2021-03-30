using HarmonyLib;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class TimedDestruction_Patch
    {
        [HarmonyPatch(typeof(TimedDestruction), "Awake")]
        [HarmonyPostfix]
        private static void Awake(ref TimedDestruction __instance)
        {
            if (ZNet.instance != null && !ZNet.instance.IsServer())
            {
                //DebugMod.LogComponent(__instance, "TimedDestruction on non-server.");
            }
        }
    }
}

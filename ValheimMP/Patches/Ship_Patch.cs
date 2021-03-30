using HarmonyLib;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Ship_Patch
    {
        [HarmonyPatch(typeof(Ship), "Start")]
        [HarmonyPostfix]
        private static void Start(ref Ship __instance)
        {
            __instance.StopCoroutine("UpdateOwner");
        }
    }
}

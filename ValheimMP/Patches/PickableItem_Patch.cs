using HarmonyLib;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class PickableItem_Patch
    {
        [HarmonyPatch(typeof(Pickable), "RPC_Pick")]
        [HarmonyPrefix]
        private static bool RPC_Pick(Pickable __instance, long sender)
        {
            return PrivateArea_Patch.CheckAccess(sender, __instance.transform.position);
        }
    }
}

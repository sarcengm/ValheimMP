using HarmonyLib;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class ZSyncAnimation_Patch
    {
        [HarmonyPatch(typeof(ZSyncAnimation), "SetTrigger")]
        [HarmonyPrefix]
        private static bool SetTrigger(ZSyncAnimation __instance, string name)
        {
            if (ValheimMPPlugin.IsDedicated)
            {
                __instance.m_animator.SetTrigger(name);
                __instance.m_nview.InvokeProximityRPC(100f, ZNetView.Everybody, "SetTrigger", name);
            }
            else if (__instance.IsOwner())
            {
                __instance.m_animator.SetTrigger(name);
            }
            return false;
        }

        [HarmonyPatch(typeof(ZSyncAnimation), "RPC_SetTrigger")]
        [HarmonyPrefix]
        private static bool RPC_SetTrigger(ZSyncAnimation __instance, long sender, string name)
        {
            if(!__instance.IsOwner())
            {
                __instance.m_animator.SetTrigger(name);
            }
            return false;
        }
    }
}

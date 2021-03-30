using HarmonyLib;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class TeleportWorld_Patch
    {
        [HarmonyPatch(typeof(TeleportWorld), "TargetFound")]
        [HarmonyPrefix]
        private static bool TargetFound(ref TeleportWorld __instance, ref bool __result)
        {
            if (ZNet.instance == null || ZNet.instance.IsServer())
                return true;

            __result = __instance.HaveTarget();
            return false;
        }

        [HarmonyPatch(typeof(TeleportWorldTrigger), "OnTriggerEnter")]
        [HarmonyPrefix]
        private static bool OnTriggerEnter(ref TeleportWorldTrigger __instance, Collider collider)
        {
            Player component = collider.GetComponent<Player>();
            if (component != null && (ZNet.instance == null || ZNet.instance.IsServer()))
            {
                __instance.m_tp.Teleport(component);
            }

            return false;
        }


        [HarmonyPatch(typeof(TeleportWorld), "RPC_SetTag")]
        [HarmonyPrefix]
        private static bool RPC_SetTag(TeleportWorld __instance, long sender)
        {
            return PrivateArea_Patch.CheckAccess(sender, __instance.transform.position);
        }
    }
}

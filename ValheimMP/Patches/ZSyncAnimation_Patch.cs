using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class ZSyncAnimation_Patch
    {
        [HarmonyPatch(typeof(ZSyncAnimation), "Awake")]
        [HarmonyPostfix]
        private static void Awake(ZSyncAnimation __instance)
        {
            __instance.m_nview.Unregister("SetTrigger");

            if (__instance.m_nview.m_zdo != null)
            {
                __instance.m_nview.m_zdo.RegisterZDOEvent("SetTrigger", (ZDO zdo) =>
                {
                    ZDOEvent_SetTrigger(__instance, zdo.GetString("SetTrigger"));
                });
            }
        }

        [HarmonyPatch(typeof(ZSyncAnimation), "OnDestroy")]
        [HarmonyPrefix]
        private static void OnDestroy(ZSyncAnimation __instance)
        {
            if (__instance.m_nview != null && __instance.m_nview.m_zdo != null)
            {
                __instance.m_nview.m_zdo.ClearZDOEvent("SetTrigger");
            }
        }

        private static void ZDOEvent_SetTrigger(ZSyncAnimation __instance, string name)
        {
            if (__instance == null)
                return;

            if (!__instance.IsOwner())
            {
                __instance.SetTrigger(name);
            }
        }

        [HarmonyPatch(typeof(ZSyncAnimation), "SetTrigger")]
        [HarmonyPrefix]
        private static bool SetTrigger(ZSyncAnimation __instance, string name)
        {
            if (ZNet.instance == null || ZNet.instance.IsServer())
            {
                if (__instance.m_nview.m_zdo != null)
                {
                    __instance.m_nview.m_zdo.Set("SetTrigger", name);
                }
            }

            __instance.m_animator.SetTrigger(name);
            return false;
        }

        [HarmonyPatch(typeof(ZSyncAnimation), "RPC_SetTrigger")]
        [HarmonyPrefix]
        private static bool RPC_SetTrigger(ref ZSyncAnimation __instance)
        {
            return false;
        }
    }
}

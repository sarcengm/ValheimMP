using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Chair_Patch
    {
        [HarmonyPatch(typeof(Chair), "Awake")]
        [HarmonyPrefix]
        private static bool Awake(Chair __instance)
        {
            var m_nview = __instance.GetComponentInParent<ZNetView>();
            if (m_nview != null && ZNet.instance != null && ZNet.instance.IsServer())
            {
                m_nview.Register("SitChair_" + __instance.name, (long sender) =>
                {
                    RPC_SitChair(__instance, sender);
                });
            }
            return false;
        }

        private static void RPC_SitChair(Chair __instance, long sender)
        {
            var peer = ZNet.instance.GetPeer(sender);

            if (peer != null && peer.m_player != null && __instance.InUseDistance(peer.m_player))
            {
                if (peer.m_player.IsEncumbered())
                {
                    return;
                }
                peer.m_player.AttachStart(__instance.m_attachPoint, hideWeapons: false, isBed: false, __instance.m_attachAnimation, __instance.m_detachOffset);

            }

            return;
        }

        [HarmonyPatch(typeof(Chair), "Interact")]
        [HarmonyPrefix]
        private static bool Interact(ref Chair __instance, ref bool __result, Humanoid human, bool hold)
        {
            if (hold)
            {
                return false;
            }
            Player player = human as Player;
            if (!__instance.InUseDistance(player))
            {
                __result = false;
                return false;
            }
            if (Time.time - Chair.m_lastSitTime < 2f)
            {
                __result = false;
                return false;
            }
            Chair.m_lastSitTime = Time.time;

            var nview = __instance.GetComponentInParent<ZNetView>();
            if (nview)
            {
                nview.InvokeRPC("SitChair_" + __instance.name);
            }
            __result = true;
            return false;
        }
    }
}

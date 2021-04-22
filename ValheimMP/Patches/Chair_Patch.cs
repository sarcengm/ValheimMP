using HarmonyLib;
using System.Linq;
using UnityEngine;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Chair_Patch
    {
        [HarmonyPatch(typeof(ZNetView), "Awake")]
        [HarmonyPostfix]
        private static void Awake(ZNetView __instance)
        {
            if (__instance.m_zdo == null)
                return;

            var chair = __instance.GetComponentInChildren<Chair>();
            if (chair && ValheimMP.IsDedicated)
            {
                __instance.Register("SitChair", (long sender, string name) =>
                {
                    RPC_SitChair(__instance, sender, name);
                });

                chair.m_useDistance *= 1.2f;
            }
        }

        private static void RPC_SitChair(ZNetView netView, long sender, string name)
        {
            var gameObj = netView.transform.Find(name);
            if(gameObj == null)
            {
                ValheimMP.Log($"Missing Chair game object: {name} in {netView}");
                return;
            }

            var chair = gameObj.GetComponent<Chair>();
            if (chair == null)
            {
                ValheimMP.Log($"Missing Chair component: {name} in {netView}->{gameObj}");
                return;
            }

            var peer = ZNet.instance.GetPeer(sender);

            if (peer != null && peer.m_player != null && chair.InUseDistance(peer.m_player))
            {
                if (peer.m_player.IsEncumbered())
                {
                    return;
                }
                peer.m_player.AttachStart(chair.m_attachPoint, hideWeapons: false, isBed: false, chair.m_attachAnimation, chair.m_detachOffset);

            }

            return;
        }

        [HarmonyPatch(typeof(Chair), "Interact")]
        [HarmonyPrefix]
        private static bool Interact(Chair __instance, ref bool __result, Humanoid human, bool hold)
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
                nview.InvokeRPC("SitChair", __instance.gameObject.GetFullName(nview.gameObject));
            }
            __result = true;
            return false;
        }
    }
}

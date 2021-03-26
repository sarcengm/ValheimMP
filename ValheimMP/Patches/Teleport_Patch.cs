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
    internal class Teleport_Patch
    {
        [HarmonyPatch(typeof(Teleport), "Start")]
        [HarmonyPrefix]
        private static bool Start(Teleport __instance)
        {
            var m_nview = __instance.GetComponentInParent<ZNetView>();
            if (m_nview != null && ZNet.instance != null && ZNet.instance.IsServer())
            {
                // Two teleports are contained in this location, the entrance and the exit.
                if (!m_nview.m_functions.ContainsKey("Teleport".GetStableHashCode()))
                {
                    m_nview.Register("Teleport", (long sender) =>
                    {
                        RPC_Teleport(__instance, sender);
                    });
                }
            }
            return false;
        }

        [HarmonyPatch(typeof(Teleport), "Interact")]
        [HarmonyPrefix]
        private static bool Interact(Teleport __instance, ref bool __result, Humanoid character, bool hold)
        {
            __result = false;
            if (hold)
            {
                return false;
            }
            if (__instance.m_targetPoint == null)
            {
                return false;
            }

            __result = true;

            var m_nview = __instance.GetComponentInParent<ZNetView>();
            if (m_nview != null)
            {
                m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "Teleport");
            }

            if (__instance.m_enterText.Length > 0)
            {
                MessageHud.instance.ShowBiomeFoundMsg(__instance.m_enterText, playStinger: false);
            }
            return false;
        }

        private static void RPC_Teleport(Teleport __instance, long sender)
        {
            var peer = ZNet.instance.GetPeer(sender);

            var collider = __instance.GetComponent<Collider>();

            var useDistance = collider == null ? 100f : collider.bounds.extents.sqrMagnitude*2;

            // we don't know which of the two teleporters we are using, the entrance or the exit, so see which is closer to decide.
            var port1 = (__instance.transform.position - peer.m_player.transform.position).sqrMagnitude;
            var port2 = (__instance.m_targetPoint.transform.position - peer.m_player.transform.position).sqrMagnitude;

            var usingTeleporter = port1 < port2 ? __instance : __instance.m_targetPoint;

            if (peer != null && peer.m_player != null && !peer.m_player.IsDead() && (port1 < port2 ? port1 : port2) <= useDistance)
            {
                peer.m_player.TeleportTo(usingTeleporter.m_targetPoint.GetTeleportPoint(), usingTeleporter.m_targetPoint.transform.rotation, distantTeleport: false);
            }
            return;
        }
    }
}

using HarmonyLib;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Teleport_Patch
    {
        [HarmonyPatch(typeof(LocationProxy), "SpawnLocation")]
        [HarmonyPostfix]
        private static void LocationProxy_SpawnLocation(LocationProxy __instance)
        {
            var teleport = __instance.GetComponentInChildren<Teleport>(true);
            if (teleport && __instance.m_nview && ValheimMP.IsDedicated)
            {
                __instance.m_nview.Register("Teleport", (long sender) =>
                {
                    RPC_Teleport(teleport, sender);
                });
            }
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
            if (!__instance.m_targetPoint || !character)
            {
                return false;
            }

            __result = true;

            var m_nview = __instance.GetComponentInParent<ZNetView>();
            if (m_nview != null)
            {
                m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "Teleport");
            }

            if (__instance.m_enterText.Length > 0 && character.transform.position.y < 3000)
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

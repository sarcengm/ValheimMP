using HarmonyLib;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class ShipControls_Patch
    {
        [HarmonyPatch(typeof(ShipControlls), "RPC_RequestControl")]
        [HarmonyPrefix]
        private static bool RPC_RequestControl(ShipControlls __instance, long sender, ZDOID playerID)
        {
            if (!ValheimMPPlugin.IsDedicated)
                return false;

            var peer = ZNet.instance.GetPeer(sender);
            if (peer == null || peer.m_player == null || peer.m_player.IsDead() || !peer.m_player.InInteractRange(__instance.transform.position))
                return false;

            playerID = peer.m_characterID;
            var player = peer.m_player;

            if (player != null && __instance.m_ship.IsPlayerInBoat(playerID))
            {
                if (__instance.GetUser() == playerID || !__instance.HaveValidUser())
                {
                    __instance.m_nview.GetZDO().Set("user", playerID);
                    __instance.m_nview.InvokeRPC(sender, "RequestRespons", true);
                    player.StartShipControl(__instance);
                    if (__instance.m_attachPoint != null)
                    {
                        player.AttachStart(__instance.m_attachPoint, hideWeapons: false, isBed: false, __instance.m_attachAnimation, __instance.m_detachOffset);
                    }
                }
                else
                {
                    player.Message(MessageHud.MessageType.Center, "$msg_inuse");
                }
            }

            return false;
        }

        [HarmonyPatch(typeof(ShipControlls), "RPC_ReleaseControl")]
        [HarmonyPrefix]
        private static bool RPC_ReleaseControl(ShipControlls __instance, long sender, ZDOID playerID)
        {
            if (!ValheimMPPlugin.IsDedicated)
                return false;

            var peer = ZNet.instance.GetPeer(sender);
            if (peer == null || peer.m_player == null || peer.m_player.IsDead())
                return false;

            playerID = peer.m_characterID;

            if (__instance.GetUser() == playerID)
            {
                __instance.m_nview.GetZDO().Set("user", ZDOID.None);
            }

            return false;
        }


        [HarmonyPatch(typeof(ShipControlls), "OnUseStop")]
        [HarmonyPrefix]
        private static void OnUseStop(ShipControlls __instance, Player player)
        {
            if (!ValheimMPPlugin.IsDedicated)
            {
                __instance.m_nview.InvokeRPC("ReleaseControl", player.GetZDOID());
            }
            else
            {
                if (__instance.m_attachPoint != null)
                {
                    player.AttachStop();
                }

                if (__instance.GetUser() == player.GetZDOID())
                {
                    __instance.m_nview.GetZDO().Set("user", ZDOID.None);
                }
            }
        }
    }
}

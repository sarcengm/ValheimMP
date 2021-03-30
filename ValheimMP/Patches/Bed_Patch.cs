using HarmonyLib;
using UnityEngine;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Bed_Patch
    {

        [HarmonyPatch(typeof(Bed), "Awake")]
        [HarmonyPostfix]
        private static void Awake(Bed __instance)
        {
            if (__instance.m_nview.GetZDO() != null && ZNet.instance.IsServer())
            {
                __instance.m_nview.Unregister("SetOwner");
                __instance.m_nview.Register("Sleep", (long sender) => {
                    RPC_Sleep(__instance, sender);
                });
            }
        }


        [HarmonyPatch(typeof(Bed), "Interact")]
        [HarmonyPrefix]
        private static bool Interact(Bed __instance, ref bool __result, Humanoid human, bool repeat)
        {
            __result = false;

            if (repeat)
            {
                return false;
            }

            var player = human as Player;

            if (player == null || !PrivateArea_Patch.CheckAccess(player.GetPlayerID(), __instance.transform.position))
                return false;

            if ((__instance.transform.position - player.transform.position).sqrMagnitude > player.GetMaxSqrInteractRange())
                return false;

            __instance.m_nview.InvokeRPC("Sleep");
            return false;
        }

        private static void RPC_Sleep(Bed __instance, long sender)
        {
            long owner = __instance.GetOwner();
            var peer = ZNet.instance.GetPeer(sender);
            if (peer == null || peer.m_player == null)
                return;

            if ((__instance.transform.position - peer.m_player.transform.position).sqrMagnitude > peer.m_player.GetMaxSqrInteractRange())
                return;

            if (!PrivateArea_Patch.CheckAccess(sender, __instance.transform.position))
                return;


            if (owner == 0L)
            {
                if (!__instance.CheckExposure(peer.m_player))
                {
                    return;
                }
                __instance.RPC_SetOwner(sender, sender, peer.m_player.GetPlayerName());
                peer.m_playerProfile.SetCustomSpawnPoint(__instance.GetSpawnPoint());
                peer.m_player.Message(MessageHud.MessageType.Center, "$msg_spawnpointset");
            }
            else if (__instance.GetOwner() == sender)
            {
                if ((peer.m_playerProfile.GetCustomSpawnPoint() - __instance.GetSpawnPoint()).sqrMagnitude > 0.1f)
                {
                    peer.m_playerProfile.SetCustomSpawnPoint(__instance.GetSpawnPoint());
                    peer.m_player.Message(MessageHud.MessageType.Center, "$msg_spawnpointset");
                    __instance.m_nview.GetZDO().Set("owner", sender);
                    __instance.m_nview.GetZDO().Set("ownerName", peer.m_playerName);
                    return;
                }

                if (!__instance.CheckEnemies(peer.m_player))
                {
                    return;
                }
                if (!__instance.CheckExposure(peer.m_player))
                {
                    return;
                }
                if (!__instance.CheckFire(peer.m_player))
                {
                    return;
                }
                if (!__instance.CheckWet(peer.m_player))
                {
                    return;
                }
                 
                peer.m_player.AttachStart(__instance.m_spawnPoint, hideWeapons: true, isBed: true, "attach_bed", new Vector3(0f, 0.5f, 0f));
            }

        }
    }
}

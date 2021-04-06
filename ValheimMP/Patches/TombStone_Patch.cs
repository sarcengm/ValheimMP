using HarmonyLib;
using System.Collections;
using UnityEngine;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class TombStone_Patch
    {
        [HarmonyPatch(typeof(TombStone), "Awake")]
        [HarmonyPostfix]
        private static void Awake(TombStone __instance)
        {
            // One one didn't support parameters, making it impossible to determine who took the content, disable it and use our own.
            __instance.m_container.m_onTakeAllSuccess = null;
            // Here is our new and improved onTakeAllSuccess2! With 100% more parameters!
            __instance.m_container.m_onTakeAllSuccess2 += OnTakeAllSuccess;
            __instance.m_nview.Register("RequestRevive", (long sender) =>
            {
                RPC_RequestRevive(__instance, sender);
            });
        }

        private static void RPC_RequestRevive(TombStone __instance, long sender)
        {
            var owner = __instance.GetOwner();
            if (owner != 0 && ValheimMP.Instance.PlayerGroupManager.ArePlayersInTheSameGroup(owner, sender))
            {
                var peer = ZNet.instance.GetPeer(sender);
                if (peer == null || !peer.m_player)
                    return;
                if (peer.m_player.IsDead() || (peer.m_player.transform.position - __instance.transform.position).sqrMagnitude > peer.m_player.GetMaxSqrInteractRange())
                    return;

                var routine = ReviveAfterNotMoving(__instance, peer, 10f);
                __instance.StartCoroutine(routine);
            }
        }

        private static IEnumerator ReviveAfterNotMoving(TombStone __instance, ZNetPeer peer, float waitTime)
        {
            var waitStart = Time.time;
            while (Time.time - waitStart < waitTime)
            {
                if (peer == null || !__instance || peer.m_player.IsDead() || (peer.m_player.transform.position - __instance.transform.position).sqrMagnitude > peer.m_player.GetMaxSqrInteractRange())
                {
                    ValheimMP.Log("You died or moved out of range!");
                    yield break;
                }

                ValheimMP.Log("ReviveAfterNotMoving?");
                yield return new WaitForSeconds(0.1f);
            }
            ValheimMP.Log("Reviving!!");
        }

        [HarmonyPatch(typeof(TombStone), "Interact")]
        [HarmonyPrefix]
        private static bool Interact(TombStone __instance, ref bool __result, Humanoid character, bool hold)
        {
            var owner = __instance.GetOwner();
            var shiftDown = ZInput.GetButtonDown("Shift");
            if (!shiftDown && owner != 0 && ValheimMP.Instance.PlayerGroupManager.ArePlayersInTheSameGroup(owner, Player.m_localPlayer.GetPlayerID()))
            {

                return false;
            }

            return true;
        }


        [HarmonyPatch(typeof(TombStone), "GetHoverText")]
        [HarmonyPrefix]
        private static bool GetHoverText(TombStone __instance, ref string __result)
        {
            __result = "";

            if (!__instance.m_nview.IsValid())
            {
                return false;
            }
            string @string = __instance.m_nview.GetZDO().GetString("ownerName");
            string text = __instance.m_text + " " + @string;

            var owner = __instance.GetOwner();

            if (owner != 0 && ValheimMP.Instance.PlayerGroupManager.ArePlayersInTheSameGroup(owner, Player.m_localPlayer.GetPlayerID()))
            {
                __result = Localization.instance.Localize(text + "\n" +
                    "[<color=yellow><b>$KEY_use</b></color>] $revive_player\n" +
                    "[<color=yellow><b>$KEY_use</b></color>] $piece_container_open");
                return false;
            }
            else
            {
                __result = Localization.instance.Localize(text + "\n[<color=yellow><b>$KEY_use</b></color>] $piece_container_open");
            }
            return false;
        }

        private static void OnTakeAllSuccess(Humanoid humanoid)
        {
            Player localPlayer = humanoid as Player;
            if ((bool)localPlayer)
            {
                localPlayer.m_pickupEffects.Create(localPlayer.transform.position, Quaternion.identity);
                localPlayer.Message(MessageHud.MessageType.Center, "$piece_tombstone_recovered");
            }
        }
    }
}

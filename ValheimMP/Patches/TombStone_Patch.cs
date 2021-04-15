using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class TombStone_Patch
    {
        private static Dictionary<long,(ZDOID, IEnumerator)> m_revivalRoutines = new();
        private static HashSet<ZDOID> m_revivalRequests = new();
        private static float m_reviveDelayTimer;
        private static ZDOID m_lastRevive = ZDOID.None;

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
            if (owner != sender && owner != 0 && ValheimMP.Instance.PlayerGroupManager.ArePlayersInTheSameGroup(sender, owner))
            {
                var peer = ZNet.instance.GetPeer(sender);
                if (peer == null || !peer.m_player)
                    return;
                if (peer.m_player.IsDead() || (peer.m_player.transform.position - __instance.transform.position).sqrMagnitude > peer.m_player.GetMaxSqrInteractRange())
                    return;

                if(m_revivalRoutines.TryGetValue(sender, out var oldRoutine))
                {
                    var previousTombstoneObj = ZNetScene.instance.FindInstance(oldRoutine.Item1);
                    if (previousTombstoneObj)
                    {
                        var previousTombstone = previousTombstoneObj.GetComponent<TombStone>();
                        if (previousTombstone)
                        {
                            previousTombstone.StopCoroutine(oldRoutine.Item2);
                        }
                    }

                    m_revivalRoutines.Remove(sender);
                }

                var zdoid = __instance.m_nview.m_zdo.m_uid;
                var routine = ReviveAfterNotMoving(__instance, peer, sender, zdoid, 5);

                m_revivalRoutines[sender] = (zdoid, routine);
                __instance.StartCoroutine(routine);
                // Start some fancy animation or fx here?
            }
        }



        private static IEnumerator ReviveAfterNotMoving(TombStone __instance, ZNetPeer peer, long routineId, ZDOID routineZDOID, int secondsWaitTime)
        {
            while (secondsWaitTime > 0)
            {
                if (peer == null || !peer.m_player || !__instance || peer.m_player.IsDead() || (peer.m_player.transform.position - __instance.transform.position).sqrMagnitude > peer.m_player.GetMaxSqrInteractRange())
                {
                    if (peer != null && peer.m_player)
                    {
                        peer.m_player.Message(MessageHud.MessageType.Center, "$vmp_revival_interupted");
                    }

                    if (m_revivalRoutines.TryGetValue(routineId, out var val2) && val2.Item1 == routineZDOID)
                    {
                        m_revivalRoutines.Remove(routineId);
                    }
                    yield break;
                }

                peer.m_player.Message(MessageHud.MessageType.Center,
                    Localization.instance.Localize("$vmp_reviving_in")
                        .Replace("{secondsWaitTime}", $"{secondsWaitTime}"));
                yield return new WaitForSeconds(1f);
                secondsWaitTime--;
                
            }

            RevivePlayer(__instance, peer);
            if (m_revivalRoutines.TryGetValue(routineId, out var val) && val.Item1 == routineZDOID)
            {
                m_revivalRoutines.Remove(routineId);
            }
            yield break;
        }

        private static void RevivePlayer(TombStone __instance, ZNetPeer peer)
        {
            var zdo = __instance.m_nview.m_zdo;
            var owner = __instance.GetOwner();
            var ownerPeer = ZNet.instance.GetPeer(owner);

            if (ownerPeer == null || ownerPeer.m_player == null)
            {
                return;
            }

            peer.m_player.Message(MessageHud.MessageType.Center,
                Localization.instance.Localize("$vmp_reviving")
                    .Replace("{playerName}", $"{ownerPeer.m_playerName}"));

            m_revivalRequests.Add(zdo.m_uid);
            ownerPeer.m_rpc.Invoke("ReviveRequest", zdo.m_uid, peer.m_playerName, peer.m_uid);
        }

        internal static void RPC_ReviveRequest(ZRpc rpc, ZDOID id, string playerName, long playerId)
        {
            // temp until I can be bothered enough to make a simple UI, which I'm incapable of atm, or at least not bothered enough to figure out.
            Chat.instance.AddInworldText(null, playerId, Player.m_localPlayer.transform.position, Talker.Type.Normal, "", 
                Localization.instance.Localize("$vmp_revival_request")
                .Replace("{playerName}",$"{playerName}"));

            m_lastRevive = id;
        }

        // Called client side
        internal static void AcceptRevivalRequest()
        {
            if (m_lastRevive != ZDOID.None)
            {
                AcceptRevivalRequest(m_lastRevive);
                m_lastRevive = ZDOID.None;
            }
        }

        // Called client side
        internal static void AcceptRevivalRequest(ZDOID id)
        {
            ZNet.instance.GetServerRPC().Invoke("ReviveRequestAccept", id);
        }

        internal static void RPC_ReviveRequestAccept(ZRpc rpc, ZDOID id)
        {
            var peer = ZNet.instance.GetPeer(rpc);
            if (peer == null || !peer.m_player || peer.m_player.IsDead())
                return;
            var tombstoneObj = ZNetScene.instance.FindInstance(id);
            if (!tombstoneObj)
                return;
            var tombstone = tombstoneObj.GetComponent<TombStone>();
            if (!tombstone)
                return;
            if (tombstone.GetOwner() != peer.m_uid)
                return;
            if (!m_revivalRequests.Contains(id))
                return;
            m_revivalRequests.Remove(id);

            peer.m_player.TeleportTo(tombstone.transform.position, tombstone.transform.rotation, true);
            tombstone.m_container.RPC_RequestTakeAll(peer.m_uid, peer.m_uid);
        }


        [HarmonyPatch(typeof(TombStone), "Interact")]
        [HarmonyPrefix]
        private static bool Interact(TombStone __instance, ref bool __result, Humanoid character, bool hold)
        {
            var owner = __instance.GetOwner();
            var shiftDown = Input.GetKey(KeyCode.LeftShift) | Input.GetKey(KeyCode.RightShift); // I bet there is still 1 person on the planet that uses right shift right?
            if (!shiftDown && !hold && owner != character.GetOwner() && owner != 0 && ValheimMP.Instance.PlayerGroupManager.ArePlayersInTheSameGroup(Player.m_localPlayer.GetPlayerID(), owner))
            {
                if (Time.time - m_reviveDelayTimer > 0.5f)
                {
                    m_reviveDelayTimer = Time.time;
                    __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "RequestRevive");
                }

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

            if (owner != 0 && ZNet.instance.GetUID() != owner && ValheimMP.Instance.PlayerGroupManager.ArePlayersInTheSameGroup(owner, Player.m_localPlayer.GetPlayerID()))
            {
                __result = Localization.instance.Localize(text + "\n" +
                    "[<color=yellow><b>$KEY_Use</b></color>] $vmp_revive\n" +
                    "[<color=yellow><b>Shift+$KEY_Use</b></color>] $piece_container_open");
                return false;
            }
            else
            {
                __result = Localization.instance.Localize(text + "\n[<color=yellow><b>$KEY_Use</b></color>] $piece_container_open");
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

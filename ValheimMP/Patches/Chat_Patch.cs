using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Chat_Patch
    {
        [HarmonyPatch(typeof(Chat), "Awake")]
        [HarmonyPrefix]
        private static void Awake(Chat __instance)
        {
            ZRoutedRpc.instance.Register("ClientMessage", (long sender, int type, string text) =>
            {
                RPC_ClientMessage(sender, (Talker.Type)type, text);
            });
        }

		[HarmonyPatch(typeof(Chat), "SendText")]
        [HarmonyPrefix]
        private static bool SendText(Talker.Type type, string text)
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "ClientMessage", (int)type, text);
            return false;
        }

        [HarmonyPatch(typeof(Chat), "OnNewChatMessage")]
        [HarmonyPrefix]
        private static bool OnNewChatMessage(Chat __instance, GameObject go, long senderID, Vector3 pos, Talker.Type type, string user, string text)
        {
            if ((int)type == (-1))
            {
                __instance.AddString(text);
            }
            else 
            {
                __instance.AddString(user, text, type);
                __instance.AddInworldText(go, senderID, pos, type, user, text);
            }
            return false;
        }

        private static void RPC_ClientMessage(long sender, Talker.Type type, string text)
        {
            var peer = ZNet.instance.GetPeer(sender);
            if (peer == null)
                return;

            var player = peer.m_player;
            if (player == null)
                return;

            var playerName = player.GetPlayerName();
            var messageLocation = player.GetHeadPoint();

            var talker = player.GetComponent<Talker>();
            if (talker == null)
                return;

            float messageDistance = 0f;
            switch (type)
            {
                case Talker.Type.Whisper:
                    messageDistance = talker.m_visperDistance;
                    break;
                case Talker.Type.Shout:
                    messageDistance = talker.m_shoutDistance;
                    break;
                case Talker.Type.Normal:
                default:
                    messageDistance = talker.m_normalDistance;
                    type = Talker.Type.Normal;
                    break;
            }

            if (ValheimMPPlugin.Instance.OnChatMessage != null)
            {
                foreach (ValheimMPPlugin.OnChatMessageDel del in ValheimMPPlugin.Instance.OnChatMessage.GetInvocationList())
                {
                    if (!del(peer, player, ref playerName, ref messageLocation, ref messageDistance, ref text, ref type))
                        return;
                }
            }

            ZRoutedRpc.instance.InvokeProximityRoutedRPC(messageLocation, messageDistance,
                ZRoutedRpc.Everybody, ZDOID.None, "ChatMessage", messageLocation, (int)type, playerName, text);
        }
    }
}

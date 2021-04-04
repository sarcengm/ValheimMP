using HarmonyLib;
using System.Text;
using UnityEngine;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Chat_Patch
    {
        private static int m_chatScrollOffset;
        private static int m_chatMaxChatHistory = 30;

        [HarmonyPatch(typeof(Chat), "Awake")]
        [HarmonyPrefix]
        private static bool Awake(Chat __instance)
        {
            Chat.m_instance = __instance;
            __instance.AddString(Localization.instance.Localize("/w [text] - $chat_whisper"));
            __instance.AddString(Localization.instance.Localize("/s [text] - $chat_shout"));
            __instance.AddString(Localization.instance.Localize("/help for a list of commands"));
            __instance.AddString("");
            __instance.m_input.gameObject.SetActive(value: false);
            __instance.m_worldTextBase.SetActive(value: false);

            ZRoutedRpc.instance.Register<Vector3, int, string, string>("ChatMessage", __instance.RPC_ChatMessage);
            ZRoutedRpc.instance.Register("ClientMessage", (long sender, int type, string text) =>
            {
                RPC_ClientMessage(sender, (Talker.Type)type, text);
            });

            m_chatMaxChatHistory = ValheimMP.Instance.ChatMaxHistory.Value;

            return false;
        }

        [HarmonyPatch(typeof(Chat), "InputText")]
        [HarmonyPrefix]
        private static bool InputText(Chat __instance)
        {
            string text = __instance.m_input.text;

            Talker.Type type = Talker.Type.Normal;
            if (text.StartsWith("/s ") || text.StartsWith("/S "))
            {
                type = Talker.Type.Shout;
                text = text.Substring(3);
            }
            if (text.StartsWith("/w ") || text.StartsWith("/W "))
            {
                type = Talker.Type.Whisper;
                text = text.Substring(3);
            }

            SendText(type, text);

            return false;
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

        [HarmonyPatch(typeof(Chat), "AddString", new[] { typeof(string) })]
        [HarmonyPrefix]
        private static bool AddString(Chat __instance, string text)
        {
            __instance.m_chatBuffer.AddRange(text.Split('\n'));

            while (__instance.m_chatBuffer.Count > m_chatMaxChatHistory)
            {
                __instance.m_chatBuffer.RemoveAt(0);
            }
            __instance.UpdateChat();
            return false;
        }

        [HarmonyPatch(typeof(Chat), "UpdateChat")]
        [HarmonyPrefix]
        private static bool UpdateChat(Chat __instance)
        {
            var start = __instance.m_chatBuffer.Count - 1 - m_chatScrollOffset;
            var end = 0;
            var text = "";
            for (int i = start; i >= end; i--)
            {
                if (text.Length + __instance.m_chatBuffer[i].Length > 16380)
                    break;
                text = __instance.m_chatBuffer[i] + "\n" + text;
            }

             __instance.m_output.text = text;
            return false;
        }
        
        private static void Update(Chat __instance)
        {
            if (__instance.m_wasFocused)
            {
                if (Input.GetKeyDown(KeyCode.PageUp))
                {
                    m_chatScrollOffset += 1;
                }

                if(Input.GetKeyDown(KeyCode.PageDown))
                {
                    m_chatScrollOffset -= 1;
                }

                m_chatScrollOffset = Mathf.Clamp(m_chatScrollOffset, 0, m_chatMaxChatHistory - 1);
            }
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

            float messageDistance;
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

            if (ValheimMP.Instance.OnChatMessage != null)
            {
                foreach (ValheimMP.OnChatMessageDelegate del in ValheimMP.Instance.OnChatMessage.GetInvocationList())
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

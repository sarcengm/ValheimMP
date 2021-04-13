using HarmonyLib;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ValheimMP.Framework.Events;
using ValheimMP.Framework.Extensions;
using static Chat;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Chat_Patch
    {
        private static int m_chatScrollOffset;
        private static int m_chatMaxChatHistory = 100;

        [HarmonyPatch(typeof(Chat), "Awake")]
        [HarmonyPrefix]
        private static bool Awake(Chat __instance)
        {
            Chat.m_instance = __instance;
            if (!ValheimMP.IsDedicated)
            {
                __instance.m_chatBuffer.Clear();
                __instance.AddString(Localization.instance.Localize("/w [text] - $chat_whisper"));
                __instance.AddString(Localization.instance.Localize("/s [text] - $chat_shout"));
                __instance.AddString(Localization.instance.Localize("Modes: " + chatModes.Join()));
                __instance.AddString(Localization.instance.Localize("/help for a list of commands"));
                __instance.AddString("");
                ZRoutedRpc.instance.m_functions.Remove("ChatMessage".GetHashCode());
                ZRoutedRpc.instance.Register<Vector3, int, string, string>("ChatMessage", __instance.RPC_ChatMessage);
                m_chatMaxChatHistory = ValheimMP.Instance.ChatMaxHistory.Value;
            }
            else
            {
                ZRoutedRpc.instance.m_functions.Remove("ClientMessage".GetHashCode());
                ZRoutedRpc.instance.Register("ClientMessage", (long sender, int type, string text) =>
                {
                    RPC_ClientMessage(sender, (ChatMessageType)type, text);
                });
            }
            __instance.m_input.gameObject.SetActive(value: false);
            __instance.m_worldTextBase.SetActive(value: false);
            return false;
        }

        private static List<string> chatModes = new() { "/s", "/w", "/g", "/p", "/c" };

        private static string chatMode;

        [HarmonyPatch(typeof(Chat), "InputText")]
        [HarmonyPrefix]
        private static bool InputText(Chat __instance)
        {
            string text = __instance.m_input.text;

            if(!string.IsNullOrEmpty(chatMode) && !text.StartsWith(chatMode))
            {
                chatMode = "";
            }

            if(chatModes.Contains(text.ToLower()))
            {
                chatMode = text;
                return false;
            }

            ChatMessageType type = ChatMessageType.Normal;
            if (text.StartsWith("/s ", System.StringComparison.InvariantCultureIgnoreCase))
            {
                type = ChatMessageType.Shout;
                text = text.Substring(3);
            }
            if (text.StartsWith("/w ", System.StringComparison.InvariantCultureIgnoreCase))
            {
                type = ChatMessageType.Whisper;
                text = text.Substring(3);
            }
            if (text.StartsWith("/g ", System.StringComparison.InvariantCultureIgnoreCase))
            {
                type = ChatMessageType.Global;
                text = text.Substring(3);
            }

            // This exists until I can get an dialog for this.. (so maybe forever)
            if (text.StartsWith("/revive", System.StringComparison.InvariantCultureIgnoreCase))
            {
                TombStone_Patch.AcceptRevivalRequest();
                return false;
            }

            SendText(type, text);
            return false;
        }

        [HarmonyPatch(typeof(Chat), "SendText")]
        [HarmonyPrefix]
        private static bool SendText(Talker.Type type, string text)
        {
            SendText((ChatMessageType)type, text);
            return false;
        }

        private static void SendText(ChatMessageType type, string text)
        {
            var args = new OnChatMessageArgs()
            {
                Text = text,
                MessageType = type,
                Player = Player.m_localPlayer,
                Peer = ZNet.instance.GetServerPeer(),
            };

            ValheimMP.Instance.Internal_OnChatMessage(args);

            if (args.SuppressMessage)
                return;

            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "ClientMessage", (int)args.MessageType, args.Text);
        }

        [HarmonyPatch(typeof(Chat), "OnNewChatMessage")]
        [HarmonyPrefix]
        private static bool OnNewChatMessage(Chat __instance, GameObject go, long senderID, Vector3 pos, Talker.Type type, string user, string text)
        {
            if ((ChatMessageType)type == ChatMessageType.ServerMessage)
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

        [HarmonyPatch(typeof(Chat), "UpdateWorldTextField")]
        [HarmonyPostfix]
        private static void UpdateWorldTextField(WorldTextInstance wt)
        {
            if ((ChatMessageType)wt.m_type == ChatMessageType.Party)
            {
                wt.m_textField.color = ValheimMP.Instance.ChatPartyColor.Value;
            }
            else if ((ChatMessageType)wt.m_type == ChatMessageType.Clan)
            {
                wt.m_textField.color = ValheimMP.Instance.ChatClanColor.Value;
            }
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

        [HarmonyPatch(typeof(Chat), "AddString", new[] { typeof(string), typeof(string), typeof(Talker.Type) })]
        [HarmonyPrefix]
        private static bool AddString(Chat __instance, string user, string text, Talker.Type type)
        {
            ChatMessageType messageType = (ChatMessageType)type;
            Color chatColor;
            switch (messageType)
            {
                case ChatMessageType.Shout:
                    chatColor = ValheimMP.Instance.ChatShoutColor.Value;
                    text = text.ToUpper();
                    break;
                case ChatMessageType.Whisper:
                    chatColor = ValheimMP.Instance.ChatWhisperColor.Value;
                    text = text.ToLowerInvariant();
                    break;
                case ChatMessageType.Clan:
                    chatColor = ValheimMP.Instance.ChatClanColor.Value;
                    break;
                case ChatMessageType.Party:
                    chatColor = ValheimMP.Instance.ChatPartyColor.Value;
                    break;
                case ChatMessageType.Global:
                    chatColor = ValheimMP.Instance.ChatGlobalColor.Value;
                    break;
                default:
                    chatColor = ValheimMP.Instance.ChatDefaultColor.Value;
                    break;
            }

            var groupName = messageType == ChatMessageType.Normal ? "" : Localization.instance.Localize($"[$vmp_{messageType}] ");
            __instance.AddString($"<color=#{ColorUtility.ToHtmlStringRGBA(chatColor * 0.7f)}>{groupName}{user}</color>: <color=#{ColorUtility.ToHtmlStringRGBA(chatColor)}>{text}</color>");
            return false;
        }

        [HarmonyPatch(typeof(Chat), "UpdateChat")]
        [HarmonyPrefix]
        private static bool UpdateChat(Chat __instance)
        {
            var start = Mathf.Clamp(__instance.m_chatBuffer.Count - 1 - m_chatScrollOffset, 0, __instance.m_chatBuffer.Count);
            var end = 0;
            var text = "";
            var visibleLines = new List<string>();

            for (int i = start; i >= end; i--)
            {
                if (text.Length + __instance.m_chatBuffer[i].Length >= 16380)
                    break;

                visibleLines.Insert(0, __instance.m_chatBuffer[i]);
            }

            __instance.m_output.text = visibleLines.Join(delimiter: "\n");
            return false;
        }

        private static float m_lastPageDown;
        private static float m_lastPageUp;

        [HarmonyPatch(typeof(Chat), "Update")]
        [HarmonyPostfix]
        private static void Update(Chat __instance)
        {
            if (__instance.m_wasFocused)
            {
                if (Input.GetKeyUp(KeyCode.Return) || Input.GetKeyDown(KeyCode.Return))
                {
                    __instance.m_input.caretWidth = 2;
                    if (!string.IsNullOrEmpty(chatMode))
                    {
                        __instance.m_input.text = chatMode + " ";
                        __instance.m_input.caretPosition = __instance.m_input.text.Length;
                        __instance.m_input.selectionFocusPosition = __instance.m_input.text.Length;
                        __instance.m_input.selectionAnchorPosition = __instance.m_input.text.Length;
                    }
                }

                if(__instance.m_input.text.StartsWith(chatMode + " /"))
                {
                    __instance.m_input.text = __instance.m_input.text.Substring(3);
                }

                var lastOffset = m_chatScrollOffset;

                if (Input.GetKeyDown(KeyCode.PageUp) || (Input.GetKey(KeyCode.PageUp) && Time.time - m_lastPageUp > 0.05f))
                {
                    m_chatScrollOffset += 1;
                    m_lastPageUp = Time.time;
                    if (Input.GetKeyDown(KeyCode.PageUp))
                        m_lastPageUp += 0.5f;
                }

                if (Input.GetKeyDown(KeyCode.PageDown) || (Input.GetKey(KeyCode.PageDown) && Time.time - m_lastPageDown > 0.05f))
                {
                    m_chatScrollOffset -= 1;
                    m_lastPageDown = Time.time;
                    if (Input.GetKeyDown(KeyCode.PageDown))
                        m_lastPageDown += 0.5f;
                }

                m_chatScrollOffset = Mathf.Clamp(Mathf.Clamp(m_chatScrollOffset, 0, m_chatMaxChatHistory - 1), 0, __instance.m_chatBuffer.Count);
                if (m_chatScrollOffset != lastOffset)
                {
                    __instance.UpdateChat();
                }
            }
            else
            {
                m_chatScrollOffset = 0;
            }
        }

        private static void RPC_ClientMessage(long sender, ChatMessageType type, string text)
        {
            var peer = ZNet.instance.GetPeer(sender);
            if (peer == null)
                return;

            var player = peer.m_player;
            if (player == null)
                return;

            var playerName = player.GetPlayerName();
            var messageLocation = player.GetHeadPoint();

            float messageDistance;
            switch (type)
            {
                case ChatMessageType.Whisper:
                    messageDistance = ValheimMP.Instance.ChatWhisperDistance.Value;
                    break;
                case ChatMessageType.Shout:
                    messageDistance = ValheimMP.Instance.ChatShoutDistance.Value;
                    break;
                case ChatMessageType.Global:
                    messageDistance = 0;
                    break;
                case ChatMessageType.Normal:
                default:
                    messageDistance = ValheimMP.Instance.ChatNormalDistance.Value;
                    type = ChatMessageType.Normal;
                    break;
            }

            var args = new OnChatMessageArgs()
            {
                Peer = peer,
                Player = player,
                PlayerName = playerName,
                MessageLocation = messageLocation,
                MessageDistance = messageDistance,
                Text = text,
                MessageType = type,
                SuppressMessage = false,
            };

            ValheimMP.Instance.Internal_OnChatMessage(args);
            if (args.SuppressMessage)
                return;

            ZRoutedRpc.instance.InvokeProximityRoutedRPC(args.MessageLocation, args.MessageDistance,
                ZRoutedRpc.Everybody, ZDOID.None, "ChatMessage", args.MessageLocation, (int)args.MessageType, args.PlayerName, args.Text);
        }
    }
}

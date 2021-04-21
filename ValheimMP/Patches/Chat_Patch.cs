using HarmonyLib;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
                __instance.AddString(Localization.instance.Localize("/g [text] - $vmp_Global"));
                __instance.AddString(Localization.instance.Localize("/p [text] - $vmp_Party"));
                __instance.AddString(Localization.instance.Localize("/c [text] - $vmp_Clan"));
                __instance.AddString(Localization.instance.Localize("Press <color=green>Page Up</color> and <color=green>Page Down</color> to scroll through the chat."));
                __instance.AddString(Localization.instance.Localize("/help for a list of commands"));
                __instance.AddString("");
                ZRoutedRpc.instance.m_functions.Remove("ChatMessage".GetHashCode());
                ZRoutedRpc.instance.Register("ChatMessage", (long sender, ZPackage pkg) =>
                {
                    RPC_ChatMessage(__instance, sender, pkg);
                });

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

        private static void RPC_ChatMessage(Chat chat, long sender, ZPackage pkg)
        {
            var originator = pkg.ReadLong();
            var pos = pkg.ReadVector3();
            var type = pkg.ReadInt();
            var playerName = pkg.ReadString();
            var text = pkg.ReadString();

            if ((ChatMessageType)type == ChatMessageType.ServerMessage)
            {
                text = Localization.instance.Localize(text);
                var argsCount = pkg.ReadInt();
                if (argsCount > 0)
                {
                    var args = new string[argsCount];

                    for (int i = 0; i < args.Length; i++)
                    {
                        args[i] = Localization.instance.Localize(pkg.ReadString());
                    }

                    text = string.Format(text, args);
                }
            }

            chat.RPC_ChatMessage(originator, pos, type, playerName, text);
        }

        private static List<string> chatModes = new() { "/s ", "/w ", "/g ", "/p ", "/c " };

        private static string chatMode;

        [HarmonyPatch(typeof(Chat), "InputText")]
        [HarmonyPrefix]
        private static bool InputText(Chat __instance)
        {
            string text = __instance.m_input.text;

            var chatModeTarget = text.Length > 2 ? text.Substring(0, 3) : text.Length > 1 ? text.Substring(0, 2) + " " : "";

            if (chatModes.Contains(chatModeTarget))
            {
                chatMode = chatModeTarget;
            }
            else
            {
                chatMode = "";
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
            if (text.StartsWith("/n ", System.StringComparison.InvariantCultureIgnoreCase))
            {
                type = ChatMessageType.Normal;
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

        [HarmonyPatch(typeof(Chat), "SendPing")]
        [HarmonyPrefix]
        private static bool SendPing(Chat __instance, Vector3 position)
        {
            if (Player.m_localPlayer)
            {

                var pingStr = $"PING[{position.x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)},{position.z.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}]";

                if (!__instance.IsChatDialogWindowVisible())
                {
                    __instance.m_input.text = $"{chatMode}{pingStr}";
                    __instance.InputText();
                    __instance.m_input.text = chatMode;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(__instance.m_input.text) && !string.IsNullOrWhiteSpace(chatMode))
                    {
                        __instance.m_input.text = chatMode;
                    }

                    var replaced = false;
                    __instance.m_input.text = pingRegex.Replace(__instance.m_input.text, delegate (Match m)
                    {
                        replaced = true;
                        return pingStr;
                    });

                    if (!replaced)
                        __instance.m_input.text += pingStr;

                    __instance.m_hideTimer = 0f;
                    __instance.m_chatWindow.gameObject.SetActive(value: true);
                    __instance.m_input.gameObject.SetActive(value: true);
                    __instance.m_input.ActivateInputField();

                    __instance.m_input.caretPosition = __instance.m_input.text.Length;
                    __instance.m_input.selectionFocusPosition = __instance.m_input.text.Length;
                    __instance.m_input.selectionAnchorPosition = __instance.m_input.text.Length;

                    __instance.m_input.text = __instance.m_input.text;
                    __instance.m_input.ActivateInputField();
                }
            }

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

        private static readonly Regex pingRegex = new(@"PING\s*\[\s*(?<x>\-?[0-9]+\.?[0-9]*)\s*,\s*(?<z>\-?[0-9]+\.?[0-9]*)\s*(,\s*(?<y>\-?[0-9]+\.?[0-9]*)\s*)?\]", RegexOptions.IgnoreCase);

        [HarmonyPatch(typeof(Chat), "OnNewChatMessage")]
        [HarmonyPrefix]
        private static bool OnNewChatMessage(Chat __instance, GameObject go, long senderID, Vector3 pos, Talker.Type type, string user, string text)
        {
            var ctype = (ChatMessageType)type;
            if (ctype == ChatMessageType.ServerMessage)
            {
                __instance.AddString(text);
            }
            else
            {
                var pingMatch = pingRegex.Match(text);

                if (!pingMatch.Success && go == null && (ctype == ChatMessageType.Normal || ctype == ChatMessageType.Party || ctype == ChatMessageType.Clan))
                {
                    var players = Player.GetAllPlayers();
                    for (int i = 0; i < players.Count; i++)
                    {
                        if (players[i].GetPlayerID() == senderID)
                        {
                            go = players[i].gameObject;
                            break;
                        }
                    }
                }

                if (pingMatch.Success)
                {
                    go = null;
                }

                __instance.AddString(user, text, type);

                if (ctype != ChatMessageType.Global)
                {
                    __instance.AddInworldText(go, senderID, pos, type, user, text);
                }
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

            var pingMatch = pingRegex.Match(wt.m_text);
            if (pingMatch.Success)
            {
                float.TryParse(pingMatch.Groups["x"].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
                float.TryParse(pingMatch.Groups["z"].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
                if (pingMatch.Groups["y"].Success)
                {
                    float.TryParse(pingMatch.Groups["y"].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y);
                    wt.m_position = new Vector3(x, z, y);
                }
                else
                {
                    wt.m_position = new Vector3(x, 0, z);
                    ZoneSystem.instance.GetGroundHeight(wt.m_position, out var y);
                    wt.m_position.y = y;
                }

                wt.m_type = Talker.Type.Ping;
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
            var totalLength = 0;
            var visibleLines = new List<string>();

            for (int i = start; i >= end; i--)
            {
                if (totalLength + __instance.m_chatBuffer[i].Length >= 1000)
                    break;
                totalLength += __instance.m_chatBuffer[i].Length;
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
                        __instance.m_input.text = chatMode;
                        __instance.m_input.caretPosition = __instance.m_input.text.Length;
                        __instance.m_input.selectionFocusPosition = __instance.m_input.text.Length;
                        __instance.m_input.selectionAnchorPosition = __instance.m_input.text.Length;
                    }
                }

                if (!string.IsNullOrEmpty(chatMode) && __instance.m_input.text.StartsWith(chatMode + "/"))
                {
                    __instance.m_input.text = __instance.m_input.text.Substring(chatMode.Length);
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

            var pkg = new ZPackage();
            pkg.Write(sender);
            pkg.Write(args.MessageLocation);
            pkg.Write((int)args.MessageType);
            pkg.Write(args.PlayerName);
            pkg.Write(args.Text);

            ZRoutedRpc.instance.InvokeProximityRoutedRPC(args.MessageLocation, args.MessageDistance,
                ZRoutedRpc.Everybody, ZDOID.None, "ChatMessage", pkg);
        }
    }
}

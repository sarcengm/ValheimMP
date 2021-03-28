using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ValheimMP.ChatCommands
{
    [BepInPlugin(BepInGUID, Name, Version)]
    [BepInDependency(ValheimMP.BepInGUID)]
    public class ChatCommandsPlugin : BaseUnityPlugin
    {
        public const string Author = "Sarcen";
        public const string Name = "ValheimMP.ChatCommands";
        public const string Version = "1.0.0";
        public const string BepInGUID = "BepIn." + Author + "." + Name;

        public void Awake()
        {
            if(!ValheimMP.IsDedicated)
            {
                Logger.LogError($"{Name} is a server side only plugin.");
                return;
            }

            ValheimMP.Instance.OnChatMessage += OnChatMessage;
        }

        private bool OnChatMessage(ZNetPeer peer, Player player, ref string playerName, ref Vector3 messageLocation, ref float messageDistance, ref string text, ref Talker.Type type)
        {
            // This normally happens on the client but we supress it here so we can still send marked up text ourselves!
            text = text.Replace('<', ' ');
            text = text.Replace('>', ' ');

            if (text.StartsWith("/vmp", StringComparison.OrdinalIgnoreCase))
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ChatMessage", messageLocation, -1, "",
                    $"<color=white>Server is running <color=green><b>{ValheimMP.Name}</b></color> version <color=green><b>{ValheimMP.Version}</b></color>.</color>"
                );
                return false;
            }

            if (text.StartsWith("/claim", StringComparison.OrdinalIgnoreCase))
            {
                var list = new List<Piece>();
                var guardStones = 0;
                var claimFor = text.Substring("/claim".Length);
                long.TryParse(claimFor, out var claimForId);
                if (claimForId == 0L)
                    claimForId = peer.m_uid;

                Piece.GetAllPiecesInRadius(player.transform.position, 20f, list);
                foreach (var item in list.Where(item => item.GetComponent<PrivateArea>() != null))
                {
                    item.m_creator = claimForId;
                    item.m_nview.GetZDO().Set(Piece.m_creatorHash, claimForId);
                    guardStones++;
                }

                ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ChatMessage", messageLocation, -1, "",
                    $"<color=white>Claimed <color=green>{guardStones}</color> guardstones for <color=green>{claimForId}</color>.</color>"
                );

                return false;
            }

            return true;
        }
    }
}

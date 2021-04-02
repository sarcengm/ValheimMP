using BepInEx;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ValheimMP.Framework;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.ChatCommands
{
    [BepInPlugin(BepInGUID, Name, Version)]
    [BepInDependency(ValheimMPPlugin.BepInGUID)]
    public class ChatCommandsPlugin : BaseUnityPlugin
    {
        public const string Author = "Sarcen";
        public const string Name = "ValheimMP.ChatCommands";
        public const string Version = "1.0.0";
        public const string BepInGUID = "BepIn." + Author + "." + Name;

        private ChatCommandManager chatCommandManager;

        public static ChatCommandsPlugin Instance { get; private set; }

        public void Awake()
        {
            if (!ValheimMPPlugin.IsDedicated)
            {
                Logger.LogError($"{Name} is a server side only plugin.");
                return;
            }

            Instance = this;

            chatCommandManager = new ChatCommandManager();
            ValheimMPPlugin.Instance.OnChatMessage += chatCommandManager.OnChatMessage;

            chatCommandManager.RegisterAll(this);
        }

        public static void Log(string message)
        {
            Instance.Logger.LogInfo(message);
        }

        [ChatCommand("Claim", "Claim all wards in a nearby area.", requireAdmin: true)]
        private void Command_Claim(ZNetPeer peer, ZNetPeer target = null, long targetId = 0)
        {
            if (target == null)
                target = peer;

            if (targetId == 0)
                targetId = target.m_uid;

            var list = new List<Piece>();
            var guardStones = 0;

            Piece.GetAllPiecesInRadius(target.GetPlayer().transform.position, 20f, list);
            foreach (var item in list.Where(item => item.GetComponent<PrivateArea>() != null))
            {
                item.m_creator = targetId;
                item.m_nview.GetZDO().Set(Piece.m_creatorHash, targetId);
                guardStones++;
            }

            peer.SendServerMessage($"<color=white>Claimed <color=green>{guardStones}</color> guardstones for <color=green>{targetId}</color>.</color>");
        }

        [ChatCommand("Ping", "Get your ping to the server.")]
        private void Command_Ping(ZNetPeer peer, Player player)
        {
            peer.SendServerMessage($"<color=white>Ping <color=green><b>{(int)(peer.GetPing() * 1000)}</b></color>ms.</color>");
        }

        [ChatCommand("Version", "Get the version of ValheimMP", aliases: new[] { "Version", "Ver", "ValheimMP", "Vmp" })]
        private void Command_Version(ZNetPeer peer, Player player)
        {
            peer.SendServerMessage($"<color=white>Server is running <color=green><b>{ValheimMPPlugin.PluginName}</b></color> version <color=green><b>{ValheimMPPlugin.CurrentVersion}</b></color>.</color>");
        }

        [ChatCommand("Teleport", "Teleport to target to destination or coordinates.", requireAdmin: true, aliases: new[] { "Tp" })]
        private void Command_Teleport(ZNetPeer peer, ZNetPeer target = null, ZNetPeer destination = null, float x = 0, float z = 0, float y = 0)
        {
            if (target == null)
                target = peer;

            var player = target.GetPlayer();

            var pos = new Vector3(x, y, z);

            if (pos == Vector3.zero && destination != null)
                pos = destination.GetPlayer().transform.position;

            if (y == 0)
                pos.y = ZoneSystem.instance.GetGroundHeight(pos);

            player.TeleportTo(pos, player.transform.rotation, true);
            peer.SendServerMessage($"<color=white>Teleporting <color=green><b>{player.GetPlayerName()}</b></color> to <color=green><b>{pos}</b></color>.</color>");

            if (peer != target)
            {
                target.SendServerMessage($"<color=white><color=green><b>{peer.m_playerName}</b></color> is teleporting you to <color=green><b>{pos}</b></color>.</color>");
            }
        }

        [ChatCommand("God", "Toggle godmode", requireAdmin: true)]
        private void Command_God(ZNetPeer peer, ZNetPeer target = null)
        {
            if (target == null)
                target = peer;

            var player = target.GetPlayer();

            player.m_godMode = !player.m_godMode;
            peer.SendServerMessage($"<color=white>Godmode for <color=green><b>{player.GetPlayerName()}</b></color> to <color=green><b>{player.m_godMode}</b></color>.</color>");

            if (peer != target)
            {
                target.SendServerMessage($"<color=white><color=green><b>{peer.m_playerName}</b></color> set your Godmode to <color=green><b>{player.m_godMode}</b></color>.</color>");
            }
        }
    }
}

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
        private void Command_Claim(ZNetPeer peer, Player player, long userId)
        {
            var list = new List<Piece>();
            var guardStones = 0;

            Piece.GetAllPiecesInRadius(player.transform.position, 20f, list);
            foreach (var item in list.Where(item => item.GetComponent<PrivateArea>() != null))
            {
                item.m_creator = userId;
                item.m_nview.GetZDO().Set(Piece.m_creatorHash, userId);
                guardStones++;
            }

            peer.SendServerMessage($"<color=white>Claimed <color=green>{guardStones}</color> guardstones for <color=green>{userId}</color>.</color>");
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

        [ChatCommand("Teleport", "Teleport to coordinates given.", requireAdmin: true, aliases: new[] { "Tp" })]
        private void Command_Teleport(ZNetPeer peer, Player player, Player target = null, Player destination = null, float x = 0, float z = 0, float y = 0)
        {
            var pos = new Vector3(x, y, z);

            if (pos == Vector3.zero && destination != null)
                pos = destination.transform.position;

            if (y == 0)
                pos.y = ZoneSystem.instance.GetGroundHeight(pos);

            if (target == null)
                target = player;

            target.TeleportTo(pos, target.transform.rotation, true);
            peer.SendServerMessage($"<color=white>Teleporting <color=green><b>{target.GetPlayerName()}</b></color> to <color=green><b>{pos}</b></color>.</color>");
        }

        [ChatCommand("God", "Toggle godmode", requireAdmin: true)]
        private void Command_God(ZNetPeer peer, Player player, Player target = null)
        {
            if (target == null)
                target = player;

            target.m_godMode = !target.m_godMode;
            peer.SendServerMessage($"<color=white>Godmode for <color=green><b>{target.GetPlayerName()}</b></color> to <color=green><b>{target.m_godMode}</b></color>.</color>");
        }
    }
}

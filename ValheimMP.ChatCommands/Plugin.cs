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
    [BepInDependency(ValheimMP.BepInGUID)]
    public class ChatCommands : BaseUnityPlugin
    {
        public const string Author = "Sarcen";
        public const string Name = "ValheimMP.ChatCommands";
        public const string Version = "1.0.0";
        public const string BepInGUID = "BepIn." + Author + "." + Name;

        public ChatCommandManager ChatCommandManager { get; private set; }

        public static ChatCommands Instance { get; private set; }

        public void Awake()
        {
            if (!ValheimMP.IsDedicated)
            {
                Logger.LogError($"{Name} is a server side only plugin.");
                return;
            }

            Instance = this;

            ChatCommandManager = new ChatCommandManager();
            ValheimMP.Instance.OnChatMessage += ChatCommandManager.OnChatMessage;

            ChatCommandManager.RegisterAll(this);
        }

        /// <summary>
        /// Register all chat commands in this object.
        /// </summary>
        /// <param name="obj"></param>
        public void RegisterAll(object obj)
        {
            ChatCommandManager.RegisterAll(obj);
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
            peer.SendServerMessage($"<color=white>Server is running <color=green><b>{ValheimMP.PluginName}</b></color> version <color=green><b>{ValheimMP.CurrentVersion}</b></color>.</color>");
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

        [ChatCommand("SetPlayerModel", "Sets Character Model", requireAdmin: true, aliases: new[] { "Model", "SetModel" })]
        private void Command_SetModel(ZNetPeer peer, int index, ZNetPeer target = null)
        {
            if (target == null)
                target = peer;

            var player = target.GetPlayer();

            player.SetPlayerModel(index);
            peer.SendServerMessage($"<color=white>Player model for <color=green><b>{player.GetPlayerName()}</b></color> to <color=green><b>{index}</b></color>.</color>");

            if (peer != target)
            {
                target.SendServerMessage($"<color=white><color=green><b>{peer.m_playerName}</b></color> set your Player model to <color=green><b>{index}</b></color>.</color>");
            }
        }

        [ChatCommand("SetHair", "Sets Character Hair", requireAdmin: true, aliases: new[] { "Hair" })]
        private void Command_SetHair(ZNetPeer peer, string hair, ZNetPeer target = null)
        {
            if (target == null)
                target = peer;

            var player = target.GetPlayer();

            player.SetHair(hair);
            peer.SendServerMessage($"<color=white>Hair for <color=green><b>{player.GetPlayerName()}</b></color> to <color=green><b>{hair}</b></color>.</color>");

            if (peer != target)
            {
                target.SendServerMessage($"<color=white><color=green><b>{peer.m_playerName}</b></color> set your hair to <color=green><b>{hair}</b></color>.</color>");
            }
        }

        [ChatCommand("SetBeard", "Sets Character Beard", requireAdmin: true, aliases: new[] { "Beard" })]
        private void Command_SetBeard(ZNetPeer peer, string beard, ZNetPeer target = null)
        {
            if (target == null)
                target = peer;

            var player = target.GetPlayer();

            player.SetBeard(beard);
            peer.SendServerMessage($"<color=white>Beard for <color=green><b>{player.GetPlayerName()}</b></color> to <color=green><b>{beard}</b></color>.</color>");

            if (peer != target)
            {
                target.SendServerMessage($"<color=white><color=green><b>{peer.m_playerName}</b></color> set your beard to <color=green><b>{beard}</b></color>.</color>");
            }
        }
    }
}

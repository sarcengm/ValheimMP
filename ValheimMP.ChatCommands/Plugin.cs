using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using ValheimMP.Framework;
using ValheimMP.Framework.Events;
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

        public static ChatCommands Instance { get; private set; }

        public void Awake()
        {
            Instance = this;

            ValheimMP.Instance.ChatCommandManager.RegisterAll(this);

            var man = ValheimMP.Instance.PlayerGroupManager;
            man.OnPlayerAcceptInvite += PlayerGroupManager_OnPlayerAcceptInvite;
            man.OnPlayerInviteGroup += PlayerGroupManager_OnPlayerInviteGroup;
            man.OnPlayerJoinGroup += PlayerGroupManager_OnPlayerJoinGroup;
            man.OnPlayerKickGroup += PlayerGroupManager_OnPlayerKickGroup;
            man.OnPlayerLeaveGroup += PlayerGroupManager_OnPlayerLeaveGroup;

            var vmp = ValheimMP.Instance;
            vmp.LocalizeWord("vmp_player_left_group", "{0} left your {1}");
            vmp.LocalizeWord("vmp_player_kicked_group", "{0} was kicked from your {1}");
            vmp.LocalizeWord("vmp_player_joined_group", "{0} joined your {1}");
            vmp.LocalizeWord("vmp_player_invited_group", "{0} was invited to your {1}");
        }

        private void PlayerGroupManager_OnPlayerLeaveGroup(PlayerGroup group, PlayerGroupMember member)
        {
            group.SendServerMessage("$vmp_player_left_group", member.Name, $"$vmp_{group.GroupType}");
        }

        private void PlayerGroupManager_OnPlayerKickGroup(PlayerGroup group, PlayerGroupMember member)
        {
            group.SendServerMessage("$vmp_player_kicked_group", member.Name, $"$vmp_{group.GroupType}");
        }

        private void PlayerGroupManager_OnPlayerJoinGroup(PlayerGroup group, PlayerGroupMember member)
        {
            group.SendServerMessage("$vmp_player_joined_group", member.Name, $"$vmp_{group.GroupType}");
        }

        private void PlayerGroupManager_OnPlayerInviteGroup(PlayerGroup group, ZNetPeer peer)
        {
            group.SendServerMessage("$vmp_player_invited_group", peer.m_playerName, $"$vmp_{group.GroupType}");
        }

        private void PlayerGroupManager_OnPlayerAcceptInvite(PlayerGroup group, PlayerGroupMember member)
        {
            //member.Peer.SendServerMessage("vmp_player_accept_invite_group", $"vmp_{group.GroupType}");
        }

        public static void Log(string message)
        {
            Instance.Logger.LogInfo(message);
        }

        [ChatCommand("Help", "List all available commands, or show more info about a certain command.", aliases: new[] { "H", "?" })]
        private void Command_Help(ZNetPeer peer, string command = null)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                var sb = new StringBuilder();
                IEnumerable<ChatCommandManager.CommandInfo> commands = ValheimMP.Instance.ChatCommandManager.GetCommands();

                if (!peer.IsAdmin())
                    commands = commands.Where(k => !k.Command.AdminRequired);

                commands = commands.OrderBy(k => k.Command.AdminRequired).ThenBy(k => k.Command.Name);
                foreach (var cmd in commands)
                {
                    sb.AppendLine(ChatCommandManager.GetCommandSyntax(cmd));
                }

                peer.SendServerMessage(sb.ToString());
            }
            else
            {
                IEnumerable<ChatCommandManager.CommandInfo> commands = ValheimMP.Instance.ChatCommandManager.GetCommands();

                if (!peer.IsAdmin())
                    commands = commands.Where(k => !k.Command.AdminRequired);

                ChatCommandManager.CommandInfo foundCommand;
                if ((foundCommand = commands.SingleOrDefault(k => k.Command.GetAliases().Contains(command, StringComparer.InvariantCultureIgnoreCase))) != null)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(ChatCommandManager.GetCommandSyntax(foundCommand));
                    sb.AppendLine($"\n<i>{foundCommand.Command.Description}</i>\n");
                    var aliases = foundCommand.Command.GetAliases();
                    if (aliases.Length > 1)
                        sb.AppendLine($"Aliases: {aliases.Join()}\n");
                    peer.SendServerMessage(sb.ToString());
                }
                else
                {
                    peer.SendServerMessage($"Chat command \"{command}\" not found.");
                }
            }
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

        [ChatCommand("Ping", "Get the targets ping to the server.")]
        private void Command_Ping(ZNetPeer peer, ZNetPeer target = null)
        {
            if (target == null)
                target = peer;

            peer.SendServerMessage($"<color=white>{target.m_playerName}'s ping is <color=green><b>{(int)(target.GetPing() * 1000)}</b></color>ms.</color>");
        }

        [ChatCommand("Version", "Get the version of ValheimMP", aliases: new[] { "Version", "Ver", "ValheimMP", "Vmp" })]
        private void Command_Version(ZNetPeer peer, Player player)
        {
            peer.SendServerMessage($"<color=white>Server is running <color=green><b>{ValheimMP.PluginName}</b></color> version <color=green><b>{ValheimMP.CurrentVersion}</b></color>.</color>");
        }

        [ChatCommand("Teleport", "Teleport to target to destination or coordinates.", requireAdmin: true, aliases: new[] { "Tp" })]
        private void Command_Teleport(ZNetPeer peer, ZNetPeer target = null, ZNetPeer destination = null, float? x = null, float z = 0, float y = 0)
        {
            if (target == null)
                target = peer;

            var player = target.GetPlayer();

            Vector3 pos;
            if (x != null)
            {
                pos = new Vector3(x.Value, y, z);
            }
            else if (destination != null)
            {
                pos = destination.GetPlayer().transform.position;
            }
            else
            {
                throw new TargetParameterCountException("There needs to be either a destination or target position");
            }

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

        [ChatCommand("NoCost", "Toggle no cost mode", requireAdmin: true)]
        private void Command_NoCost(ZNetPeer peer, ZNetPeer target = null)
        {
            if (target == null)
                target = peer;

            var player = target.GetPlayer();

            player.m_noPlacementCost = !player.m_noPlacementCost;
            player.m_nview.m_zdo.Set("noPlacementCost", player.m_noPlacementCost);
            peer.SendServerMessage($"<color=white>NoCost mode for <color=green><b>{player.GetPlayerName()}</b></color> to <color=green><b>{player.m_noPlacementCost}</b></color>.</color>");

            if (peer != target)
            {
                target.SendServerMessage($"<color=white><color=green><b>{peer.m_playerName}</b></color> set your NoCost mode to <color=green><b>{player.m_noPlacementCost}</b></color>.</color>");
            }
        }


        [ChatCommand("GiveItem", "Client side give item to translate items into actual game names", aliases: new[] { "Give" }, executionLocation: CommandExecutionLocation.Client)]
        private void Command_GiveItemClient(OnChatMessageArgs chatargs, string itemName, int amount = 1, string target = "")
        {
            // passthrough the command to the server
            chatargs.SuppressMessage = false;

            var item = ObjectDB.instance.GetItemByLocalizedName(itemName, true);
            Log($"GetItemByLocalizedName: {itemName} -> {item}");
            if (item != null)
            {
                // we only modify the command if the client typed a localized item name
                chatargs.Text = $"/giveitem \"{item.name}\", {amount}, {target}";
                Log(chatargs.Text);
            }
        }

        [ChatCommand("GiveItem", "Gives an item to the player", requireAdmin: true, aliases: new[] { "Give" })]
        private void Command_GiveItem(ZNetPeer peer, string itemName, int amount = 1, ZNetPeer target = null)
        {
            if (target == null)
                target = peer;

            var player = target.GetPlayer();

            var item = ObjectDB.instance.GetItemPrefab(itemName);
            
            if(item == null)
            {
                peer.SendServerMessage($"No such item found, item names are case sensitive.");
                return;
            }

            // we simply call this to make sure it works, invalid items will throw an exception, perfect handling!
            try { _ = item.GetComponent<ItemDrop>().m_itemData.GetIcon(); }
            catch(Exception)
            {
                peer.SendServerMessage($"The item specified is an invalid inventory item.");
                return;
            }

            var itemAdded = player.GetInventory().AddItem(itemName, amount, 1, 0, 0, "");

            peer.SendServerMessage($"<color=white>Giving <color=green><b>{player.GetPlayerName()}</b></color> to <color=green><b>{amount}x{itemAdded.m_shared.m_name}</b></color>.</color>");

            if (peer != target)
            {
                target.SendServerMessage($"<color=white><color=green><b>{peer.m_playerName}</b></color> gave you <color=green><b>{amount}x{itemAdded.m_shared.m_name}</b></color>.</color>");
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

        [ChatCommand("SetName", "Sets Character Name", requireAdmin: true)]
        private void Command_SetName(ZNetPeer peer, string name, ZNetPeer target = null)
        {
            if (target == null)
                target = peer;

            var player = target.GetPlayer();

            var profile = target.GetPlayerProfile();
            profile.m_playerName = name;
            if (player && player.m_nview && player.m_nview.m_zdo != null)
            {
                player.m_nview.m_zdo.Set("playerName", name);
            }
            peer.SendServerMessage($"<color=white>Name for <color=green><b>{target.m_uid}</b></color> to <color=green><b>{name}</b></color>.</color>");

            if (peer != target)
            {
                target.SendServerMessage($"<color=white><color=green><b>{peer.m_playerName}</b></color> set your beard to <color=green><b>{name}</b></color>.</color>");
            }
        }

        [ChatCommand("ServerKick", "Kicks a player", requireAdmin: true)]
        private void Command_ServerKick(ZNetPeer peer, ZNetPeer target)
        {
            var man = ValheimMP.Instance.AdminManager;
            var admin1 = man.GetAdmin(peer);
            var admin2 = man.GetAdmin(target);

            if (admin1 == null)
                return;

            if (admin1.Rank < admin2.Rank)
            {
                peer.SendServerMessage($"{target.m_playerName} is of a higher rank and can not be kicked.");
                return;
            }

            peer.SendServerMessage($"Kicked {target.m_playerName}");
            ZNet.instance.InternalKick(target);
        }

        [ChatCommand("ServerBan", "Bans a player", requireAdmin: true)]
        private void Command_ServerBan(ZNetPeer peer, ZNetPeer target)
        {
            var man = ValheimMP.Instance.AdminManager;
            var admin1 = man.GetAdmin(peer);
            var admin2 = man.GetAdmin(target);

            if (admin1 == null)
                return;

            if (admin1.Rank < admin2.Rank)
            {
                peer.SendServerMessage($"{target.m_playerName} is of a higher rank and can not be banned.");
                return;
            }

            peer.SendServerMessage($"Banned {target.m_playerName}");
            ZNet.instance.m_bannedList.Add(target.m_uid.ToString());
            ZNet.instance.InternalKick(target);
        }

        [ChatCommand("ServerUnban", "Unbans a player", requireAdmin: true)]
        private void Command_ServerUnban(ZNetPeer peer, string banned)
        {
            ZNet.instance.m_bannedList.Remove(banned);
            peer.SendServerMessage($"Maybe unbanned {banned}, maybe wasn't banned to begin with.");
        }

        [ChatCommand("AdminList", "Get a list of all admins", requireAdmin: true, aliases: new[] { "ListAdmins", "Admins" })]
        private void Command_AdminList(ZNetPeer peer)
        {
            var sb = new StringBuilder();
            foreach (var admin in ValheimMP.Instance.AdminManager.Admins.Values.OrderBy(k => k.Rank).ThenBy(k => k.LastOnline))
            {
                sb.AppendLine($"Rank: {admin.Rank,-3} Name: {admin.Name,10} Id: {admin.Id} Last Online: {admin.LastOnline}");
            }

            peer.SendServerMessage(sb.ToString());
        }

        [ChatCommand("AdminRemove", "Removes an admin", requireAdmin: true, aliases: new[] { "RemoveAdmin" })]
        private void Command_AdminRemove(ZNetPeer peer, ZNetPeer target)
        {
            var man = ValheimMP.Instance.AdminManager;
            var admin1 = man.GetAdmin(peer);
            var admin2 = man.GetAdmin(target);

            if (admin1 == null)
                return;

            if (admin2 == null)
            {
                peer.SendServerMessage($"{target.m_playerName} is not an admin.");
                return;
            }

            if (admin1.Rank < admin2.Rank)
            {
                peer.SendServerMessage($"{target.m_playerName} is of a higher rank and can not be removed.");
                return;
            }

            man.RemoveAdmin(admin2);
        }

        [ChatCommand("AdminAdd", "Adds an admin", requireAdmin: true, aliases: new[] { "AddAdmin" })]
        private void Command_AdminAdd(ZNetPeer peer, ZNetPeer target)
        {
            var man = ValheimMP.Instance.AdminManager;
            var admin1 = man.GetAdmin(peer);
            var admin2 = man.GetAdmin(target);

            if (admin1 == null)
                return;

            if (admin2 != null)
            {
                if (admin1.Rank + 1 < admin2.Rank)
                {
                    admin2.Rank = admin1.Rank + 1;
                    peer.SendServerMessage($"{target.m_playerName} is already an admin. Rank was promoted instead.");
                    return;
                }

                peer.SendServerMessage($"{target.m_playerName} is already an admin.");
                return;
            }

            if (man.AddAdmin(target, peer) != null)
            {
                peer.SendServerMessage($"{target.m_playerName} added as admin.");
            }
        }

        [ChatCommand("PartyInvite", "Invite someone to your party", aliases: new[] { "Invite", "PI" })]
        private void Command_PartyInvite(ZNetPeer peer, ZNetPeer target)
        {
            var man = ValheimMP.Instance.PlayerGroupManager;
            var party = man.GetGroupByType(peer.m_uid, PlayerGroupType.Party);
            if (party == null) party = man.CreateGroup(peer, PlayerGroupType.Party);
            party.Invite(target);
        }

        [ChatCommand("PartyAcceptInvite", "Accept an invite to a party invitation of the target", aliases: new[] { "Accept", "PA", "PartyAccept" })]
        private void Command_PartyAcceptInvite(ZNetPeer peer, ZNetPeer target)
        {
            var man = ValheimMP.Instance.PlayerGroupManager;
            var party = man.GetGroupByType(target.m_uid, PlayerGroupType.Party);
            if (party == null) return;
            party.AcceptInvite(peer);
        }

        [ChatCommand("PartyLeave", "Leave a party", aliases: new[] { "Leave", "PL" })]
        private void Command_PartyLeave(ZNetPeer peer)
        {
            var man = ValheimMP.Instance.PlayerGroupManager;
            var party = man.GetGroupByType(peer.m_uid, PlayerGroupType.Party);
            if (party == null)
                return;
            party.LeaveGroup(peer);
            peer.SendServerMessage($"You have left the party.");
        }

        [ChatCommand("PartyKick", "Kick someone from the party", aliases: new[] { "Kick", "PK" })]
        private void Command_PartyKick(ZNetPeer peer, ZNetPeer target)
        {
            var man = ValheimMP.Instance.PlayerGroupManager;
            var party = man.GetGroupByType(peer.m_uid, PlayerGroupType.Party);
            if (party == null)
            {
                peer.SendServerMessage($"You are not in a party.");
                return;
            }

            if (!party.Members.TryGetValue(peer.m_uid, out PlayerGroupMember member))
            {
                peer.SendServerMessage($"You are not in a party.");
                return;
            }

            if (!party.Members.TryGetValue(target.m_uid, out PlayerGroupMember targetMember))
            {
                peer.SendServerMessage($"{target.m_playerName} is not in a party.");
                return;
            }

            if (member.Rank >= targetMember.Rank)
            {
                peer.SendServerMessage($"Your rank is not high enough to kick {target.m_playerName}.");
                return;
            }

            party.KickGroup(target);
        }

        [ChatCommand("PartyChat", "Send a message to your party", aliases: new[] { "P", "Party" })]
        private void Command_PartyChat(ZNetPeer peer, string message)
        {
            var man = ValheimMP.Instance.PlayerGroupManager;
            var party = man.GetGroupByType(peer.m_uid, PlayerGroupType.Party);
            if (party == null)
                return;

            party.SendGroupMessage(peer, message);
        }

        /// COPY PASTA & RENAME FOR CLAN


        [ChatCommand("ClanInvite", "Invite someone to your clan", aliases: new[] { "CI" })]
        private void Command_ClanInvite(ZNetPeer peer, ZNetPeer target)
        {
            var man = ValheimMP.Instance.PlayerGroupManager;
            var clan = man.GetGroupByType(peer.m_uid, PlayerGroupType.Clan);
            if (clan == null)
                return;
            clan.Invite(target);
        }

        [ChatCommand("ClanCreate", "Create a clan")]
        private void Command_ClanCreate(ZNetPeer peer, string clanName)
        {
            var man = ValheimMP.Instance.PlayerGroupManager;
            var clan = man.GetGroupByType(peer.m_uid, PlayerGroupType.Clan);
            if (clan != null)
                return;
            man.CreateGroup(peer, PlayerGroupType.Clan, clanName);
        }

        

        [ChatCommand("ClanAcceptInvite", "Accept an invite to a clan invitation of the target", aliases: new[] { "CA", "ClanAccept" })]
        private void Command_ClanAcceptInvite(ZNetPeer peer, ZNetPeer target)
        {
            var man = ValheimMP.Instance.PlayerGroupManager;
            var clan = man.GetGroupByType(target.m_uid, PlayerGroupType.Clan);
            if (clan == null) return;
            clan.AcceptInvite(peer);
        }

        [ChatCommand("ClanLeave", "Leave a clan", aliases: new[] { "CL" })]
        private void Command_ClanLeave(ZNetPeer peer)
        {
            var man = ValheimMP.Instance.PlayerGroupManager;
            var clan = man.GetGroupByType(peer.m_uid, PlayerGroupType.Clan);
            if (clan == null)
                return;
            clan.LeaveGroup(peer);
            peer.SendServerMessage($"You have left the clan.");
        }

        [ChatCommand("ClanKick", "Kick someone from the clan", aliases: new[] { "CK" })]
        private void Command_ClanKick(ZNetPeer peer, ZNetPeer target)
        {
            var man = ValheimMP.Instance.PlayerGroupManager;
            var clan = man.GetGroupByType(peer.m_uid, PlayerGroupType.Clan);
            if (clan == null)
            {
                peer.SendServerMessage($"You are not in a clan.");
                return;
            }

            if (!clan.Members.TryGetValue(peer.m_uid, out PlayerGroupMember member))
            {
                peer.SendServerMessage($"You are not in a clan.");
                return;
            }

            if (!clan.Members.TryGetValue(target.m_uid, out PlayerGroupMember targetMember))
            {
                peer.SendServerMessage($"{target.m_playerName} is not in a clan.");
                return;
            }

            if (member.Rank >= targetMember.Rank)
            {
                peer.SendServerMessage($"Your rank is not high enough to kick {target.m_playerName}.");
                return;
            }

            clan.KickGroup(target);
        }

        [ChatCommand("ClanChat", "Send a message to your clan", aliases: new[] { "C", "Clan" })]
        private void Command_ClanChat(ZNetPeer peer, string message)
        {
            var man = ValheimMP.Instance.PlayerGroupManager;
            var clan = man.GetGroupByType(peer.m_uid, PlayerGroupType.Clan);
            if (clan == null)
                return;

            clan.SendGroupMessage(peer, message);
        }


        [ChatCommand("SetActiveArea", "Better not touch this unless you want to destroy the server.", requireAdmin:true)]
        private void DebugCommand_SetActiveArea(ZNetPeer peer, int area = -1)
        {
            if (area >= 0)
            {
                ZoneSystem.instance.m_activeArea = area;
            }

            peer.SendServerMessage($"ZoneSystem.instance.m_activeArea: {ZoneSystem.instance.m_activeArea}");
        }

        [ChatCommand("Stats", "Server FPS, or rather latest delta time", requireAdmin: true)]
        private void DebugCommand_Stats(ZNetPeer peer)
        {           
            peer.SendServerMessage(
                $"Time.deltaTime: {Time.deltaTime} ({1f / Time.deltaTime} fps)\n" +
                $"Instances: {ZNetScene.instance?.m_instances.Count}\n" +
                $"Sectors: {SectorManager.FullyLoadedSectors()}/{ SectorManager.GetSectorCount()}");
        }

        [ChatCommand("SaveWorld", "Saves the world", requireAdmin: true)]
        private void DebugCommand_SaveWorld(ZNetPeer peer)
        {
            ZNet.instance.SaveWorld(false);
        }
    }
}

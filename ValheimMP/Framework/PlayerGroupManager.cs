using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Framework
{
    [Serializable]
    public class PlayerGroupManager
    {
        internal int m_groupCounter;

        [JsonProperty]
        public Dictionary<int, PlayerGroup> Groups { get; private set; } = new();

        [JsonIgnore]
        public Dictionary<long, List<PlayerGroup>> GroupsByPlayerID { get; private set; } = new();

        public event OnPlayerLeaveGroupHandler OnPlayerLeaveGroup;
        public event OnPlayerKickGroupHandler OnPlayerKickGroup;
        public event OnPlayerJoinGroupHandler OnPlayerJoinGroup;
        public event OnPlayerInviteGroupHandler OnPlayerInviteGroup;
        public event OnPlayerAcceptInviteGroupHandler OnPlayerAcceptInvite;

        /// <summary>
        /// Fired for every group the player is in
        /// </summary>
        public event OnGroupMemberOnlineHandler OnGroupMemberOnline;
        /// <summary>
        /// Fired for every group the player is in
        /// </summary>
        public event OnGroupMemberOfflineHandler OnGroupMemberOffline;

        public delegate void OnPlayerLeaveGroupHandler(PlayerGroup group, PlayerGroupMember member);
        public delegate void OnPlayerKickGroupHandler(PlayerGroup group, PlayerGroupMember member, ZNetPeer instigator);
        public delegate void OnPlayerJoinGroupHandler(PlayerGroup group, PlayerGroupMember member);
        public delegate void OnPlayerInviteGroupHandler(PlayerGroup group, ZNetPeer peer, ZNetPeer instigator);
        public delegate void OnPlayerAcceptInviteGroupHandler(PlayerGroup group, PlayerGroupMember member);
        public delegate void OnGroupMemberOnlineHandler(PlayerGroup group, PlayerGroupMember member);
        public delegate void OnGroupMemberOfflineHandler(PlayerGroup group, PlayerGroupMember member);

        public PlayerGroupManager()
        {

        }

        internal void Internal_OnPlayerOnline(ZNetPeer peer)
        {
            if (GroupsByPlayerID.TryGetValue(peer.m_uid, out var groups))
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    var group = groups[i];
                    if (group.Members.TryGetValue(peer.m_uid, out var member))
                    {
                        member.Peer = peer;
                        OnGroupMemberOnline?.Invoke(group, member);
                    }
                }
            }

            SendPlayerGroupUpdate(peer, false);
        }

        internal void Internal_OnPlayerOffline(ZNetPeer peer)
        {
            if (GroupsByPlayerID.TryGetValue(peer.m_uid, out var groups))
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    var group = groups[i];
                    if (group.Members.TryGetValue(peer.m_uid, out var member))
                    {
                        member.LastOnline = DateTime.Now;
                        OnGroupMemberOffline?.Invoke(group, member);
                        member.Peer = null;
                        member.Player = null;
                        sendPlayerOffline(peer.m_uid, group);
                    }
                }
            }
        }

        private void sendPlayerOffline(long m_uid, PlayerGroup group)
        {
            for (int i = 0; i < group.MemberList.Count; i++)
            {
                group.MemberList[i].Peer?.m_rpc.Invoke("PlayerGroupPlayerOffline", group.Id, m_uid);
            }
        }

        private void addToGroupsByPlayer(PlayerGroup group, PlayerGroupMember member)
        {
            List<PlayerGroup> list;
            if (!GroupsByPlayerID.TryGetValue(member.Id, out list))
            {
                list = new List<PlayerGroup>();
                GroupsByPlayerID.Add(member.Id, list);
            }

            list.Add(group);
        }

        private void removeFromGroupsByPlayer(PlayerGroup group, PlayerGroupMember member)
        {
            if (GroupsByPlayerID.TryGetValue(member.Id, out var groups))
            {
                groups.Remove(group);
            }
        }

        internal void OnPeerDisconnect(ZNetPeer peer)
        {
            foreach (var group in Groups.Values)
            {
                group.OnPeerDisconnect(peer);
            }
        }

        internal static PlayerGroupManager Load(string jsonFile)
        {
            PlayerGroupManager manager;
            try
            {
                var jsonStr = System.IO.File.ReadAllText(jsonFile);
                manager = JsonConvert.DeserializeObject<PlayerGroupManager>(jsonStr);

                manager.FixReferences();
            }
            catch (System.IO.FileNotFoundException)
            {
                ValheimMP.LogWarning($"{jsonFile} not found, creating new manager.");
                manager = new PlayerGroupManager();
            }
            catch (Exception ex)
            {
                ValheimMP.LogError($"Error while trying to load PlayerGroupManager: {ex}");
                manager = new PlayerGroupManager();
            }
            return manager;
        }
        internal void Save(string jsonFile)
        {
            try
            {
                var jsonStr = JsonConvert.SerializeObject(this);
                System.IO.File.WriteAllText(jsonFile, jsonStr);
            }
            catch (Exception ex)
            {
                ValheimMP.LogError($"Error while trying to save PlayerGroupManager: {ex}");
            }
        }

        private void FixReferences()
        {
            GroupsByPlayerID.Clear();

            foreach (var group in Groups.Values)
            {
                group.m_manager = this;
                group.MemberList = group.Members.Values.ToList();
                foreach (var member in group.Members.Values)
                {
                    addToGroupsByPlayer(group, member);
                }
            }
        }


        /// <summary>
        /// If the two players belong to the same group, this can be the same clan, or the same party
        /// </summary>
        /// <param name="playerId1"></param>
        /// <param name="playerId2"></param>
        /// <returns></returns>
        public bool ArePlayersInTheSameGroup(long playerId1, long playerId2)
        {
            // Clients only know their own group, so check if characters are in their group rather then the other way around
            // because a GroupByPlayerId will fail on them.
            // Possibly I should just store the group and party id in the zdo? It would probably result in more reliable checking in all cases
            if (!ValheimMP.IsDedicated && ZNet.instance.GetUID() == playerId2)
            {
                var swap = playerId1;
                playerId1 = playerId2;
                playerId2 = swap;
            }

            var clan = GetGroupByType(playerId1, PlayerGroupType.Clan);
            if (clan != null)
            {
                if (clan.Members.ContainsKey(playerId2))
                    return true;
            }

            var party = GetGroupByType(playerId1, PlayerGroupType.Party);
            if (party != null)
            {
                if (party.Members.ContainsKey(playerId2))
                    return true;
            }
            return false;
        }

        public bool ArePlayersInTheSameGroup(long playerId1, long playerId2, PlayerGroupType type)
        {
            // Clients only know their own group, so check if characters are in their group rather then the other way around
            // because a GroupByPlayerId will fail on them.
            // Possibly I should just store the group and party id in the zdo? It would probably result in more reliable checking in all cases
            if (!ValheimMP.IsDedicated && ZNet.instance.GetUID() == playerId2)
            {
                var swap = playerId1;
                playerId1 = playerId2;
                playerId2 = swap;
            }

            var party = GetGroupByType(playerId1, type);
            if (party != null)
            {
                if (party.Members.ContainsKey(playerId2))
                    return true;
            }
            return false;
        }

        public PlayerGroup GetGroupByType(long playerId, PlayerGroupType groupType)
        {
            if (GroupsByPlayerID.TryGetValue(playerId, out var groups))
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    if (groups[i].GroupType == groupType)
                        return groups[i];
                }
            }

            return null;
        }

        internal int GetNewGroupID()
        {
            // I'm sure if we ever reach maxint*2 groups it will endlessly loop and break!
            while (Groups.ContainsKey(m_groupCounter))
            {
                unchecked { m_groupCounter++; }
            }
            return m_groupCounter;
        }

        public PlayerGroup CreateGroup(ZNetPeer peer, PlayerGroupType groupType, string groupName = "")
        {
            var existingGroup = GetGroupByType(peer.m_uid, groupType);
            if (existingGroup != null)
            {
                existingGroup.LeaveGroup(peer);
            }
            var newGroup = new PlayerGroup(this)
            {
                GroupType = groupType,
                Name = groupName,
                Id = GetNewGroupID(),
                CreationDate = DateTime.Now,
            };
            Groups.Add(newGroup.Id, newGroup);

            newGroup.JoinGroup(peer);

            return newGroup;
        }

        internal void playerInvite(PlayerGroup playerGroup, ZNetPeer target, ZNetPeer instigator)
        {
            OnPlayerInviteGroup?.Invoke(playerGroup, target, instigator);
        }

        internal void playerJoin(PlayerGroup playerGroup, PlayerGroupMember member)
        {
            playerAddedToGroup(playerGroup, member);
            OnPlayerJoinGroup?.Invoke(playerGroup, member);
        }

        internal void playerKick(PlayerGroup playerGroup, PlayerGroupMember member, ZNetPeer instigator)
        {
            playerRemovedFromGroup(playerGroup, member);

            OnPlayerKickGroup?.Invoke(playerGroup, member, instigator);
            if (playerGroup.Members.Count == 0)
            {
                Groups.Remove(playerGroup.Id);
            }
        }

        internal void playerAcceptInvite(PlayerGroup playerGroup, PlayerGroupMember member)
        {
            OnPlayerAcceptInvite?.Invoke(playerGroup, member);
        }

        internal void playerLeaveGroup(PlayerGroup playerGroup, PlayerGroupMember member)
        {
            playerRemovedFromGroup(playerGroup, member);

            OnPlayerLeaveGroup?.Invoke(playerGroup, member);
        }

        private void playerRemovedFromGroup(PlayerGroup playerGroup, PlayerGroupMember member)
        {
            removeFromGroupsByPlayer(playerGroup, member);
            if (playerGroup.Members.Count == 0)
            {
                Groups.Remove(playerGroup.Id);
            }

            var members = playerGroup.Members.Values.ToList();

            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].Peer != null)
                {
                    members[i].Peer.m_rpc.Invoke("PlayerGroupRemovePlayer", playerGroup.Id, member.Id);
                }
            }

            if (member.Peer != null)
            {
                member.Peer.m_rpc.Invoke("PlayerGroupRemovePlayer", playerGroup.Id, member.Id);
            }
        }

        private void playerAddedToGroup(PlayerGroup playerGroup, PlayerGroupMember member)
        {
            addToGroupsByPlayer(playerGroup, member);

            if (member.Peer != null)
            {
                SendPlayerGroupUpdate(member.Peer, false);
            }
        }

        public void RPC_PlayerGroupRemovePlayer(ZRpc rpc, int groupId, long playerId)
        {
            if (Groups.TryGetValue(groupId, out var group))
            {
                group.RemoveMember(playerId);

                if (group.Members.Count == 0 || playerId == ZNet.instance.GetUID())
                {
                    Groups.Remove(groupId);
                    if (GroupsByPlayerID.TryGetValue(playerId, out var glist))
                    {
                        glist.Remove(group);
                    }
                }
            }
        }

        static float m_lastUpdateTime;

        internal void Update()
        {
            if (Time.time - m_lastUpdateTime < 1f)
            {
                return;
            }

            m_lastUpdateTime = Time.time;

            if (ZNet.instance == null)
                return;

            var peers = ZNet.instance.GetPeers();

            for (int i = 0; i < peers.Count; i++)
            {
                SendPlayerGroupUpdate(peers[i], true);
            }
        }

        private void SendPlayerGroupUpdate(ZNetPeer peer, bool periodic)
        {
            var pkg = new ZPackage();
            var groupCount = SerializeGroupsFor(peer, pkg, periodic);
            if (groupCount > 0)
            {
                peer.m_rpc.Invoke("PlayerGroupUpdate", pkg);
            }
        }

        public int SerializeGroupsFor(ZNetPeer peer, ZPackage pkg, bool periodic)
        {
            var groupCountPos = pkg.GetPos();
            var groupCount = 0;
            pkg.Write(groupCount);

            if (GroupsByPlayerID.TryGetValue(peer.m_uid, out var groups))
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    var group = groups[i];
                    var flags = group.Serialize(peer, pkg, periodic);

                    if (flags != GroupUpdateFlags.None)
                    {
                        groupCount++;
                    }
                }
            }

            pkg.WriteCounter(groupCountPos, groupCount, true);
            return groupCount;
        }


        public void RPC_PlayerGroupUpdate(ZRpc rpc, ZPackage pkg)
        {
            var count = pkg.ReadInt();
            for (int i = 0; i < count; i++)
            {
                var id = pkg.PeekInt();
                var newGroup = false;
                if (!Groups.TryGetValue(id, out var group))
                {
                    group = new PlayerGroup(this);
                    Groups.Add(id, group);
                    newGroup = true;
                }

                group.Deserialize(pkg);

                if (newGroup)
                {
                    for (int j = 0; j < group.MemberList.Count; j++)
                    {
                        var member = group.MemberList[j];
                        if (!GroupsByPlayerID.TryGetValue(member.Id, out var playerGroups))
                        {
                            playerGroups = new List<PlayerGroup>();
                            GroupsByPlayerID.Add(member.Id, playerGroups);
                        }
                        playerGroups.Add(group);
                    }
                }
            }
        }

        public void RPC_PlayerGroupPlayerOffline(ZRpc rpc, int groupId, long playerId)
        {
            if(Groups.TryGetValue(groupId, out var group))
            {
                if(group.Members.TryGetValue(playerId, out var member))
                {
                    member.PlayerZDOID = ZDOID.None;
                    member.LastOnline = DateTime.Now;
                }
            }
        }
    }



    [Flags]
    public enum MemberUpdateFlags
    {
        None = 0,
        Name = 1 << 2,
        LastOnline = 1 << 3,
        MemberSince = 1 << 4,
        PlayerZDOID = 1 << 5,
        PlayerHealth = 1 << 6,
        PlayerMaxHealth = 1 << 7,
        PlayerPosition = 1 << 8,
        Rank = 1 << 9,

        // Flags send once on connect
        Once = Name | LastOnline | MemberSince | Rank,
        // Flags send for periodic updates when someone is out of range
        PeriodicFar = PlayerZDOID | PlayerHealth | PlayerMaxHealth | PlayerPosition,
        // Flags send for periodic updates when someone is nearby,
        // should be none, since the player will have the ZDO available to query!
        PeriodicNear = PlayerZDOID,
    }

    [Serializable]
    public class PlayerGroupMember
    {
        [JsonProperty]
        public long Id { get; internal set; }

        private string m_name;
        [JsonProperty]
        public string Name
        {
            get
            {
                if (Peer != null)
                    m_name = Peer.m_playerName;
                else if (Player)
                    m_name = Player.GetPlayerName();


                return m_name;
            }

            internal set { m_name = value; }
        }

        [JsonProperty]
        public int Rank { get; internal set; }

        private Player m_player;
        [JsonIgnore]
        public Player Player
        {
            get
            {
                if (Peer != null)
                    m_player = Peer.m_player;

                if (m_player || ValheimMP.IsDedicated)
                    return m_player;

                var gameObj = ZNetScene.instance.FindInstance(m_zdoid);
                if (gameObj)
                    m_player = gameObj.GetComponent<Player>();

                return m_player;
            }

            internal set { m_player = value; }
        }

        [JsonIgnore]
        public ZNetPeer Peer { get; internal set; }

        [JsonProperty]
        public DateTime MemberSince { get; internal set; }

        private DateTime m_lastOnline;
        [JsonProperty]
        public DateTime LastOnline
        {
            get
            {
                if (Peer != null)
                    m_lastOnline = DateTime.Now;

                return m_lastOnline;
            }

            internal set { m_lastOnline = value; }
        }

        private Vector3 m_position;
        [JsonIgnore]
        public Vector3 PlayerPosition
        {
            get
            {
                if (Player)
                    m_position = Player.transform.position;

                return m_position;
            }
            internal set { m_position = value; }
        }

        private float m_health;
        [JsonIgnore]
        public float PlayerHealth
        {
            get
            {
                if (Player)
                    m_health = Player.GetHealth();

                return m_health;
            }
            internal set { m_health = value; }
        }

        private ZDOID m_zdoid;
        [JsonIgnore]
        public ZDOID PlayerZDOID
        {
            get
            {
                if (Player && ValheimMP.IsDedicated)
                    m_zdoid = Player.GetZDOID();
                return m_zdoid;
            }
            internal set { m_zdoid = value; }
        }

        private float m_maxhealth;
        [JsonIgnore]
        public float PlayerMaxHealth
        {
            get
            {
                if (Player)
                    m_maxhealth = Player.GetMaxHealth();

                return m_maxhealth;
            }
            internal set { m_maxhealth = value; }
        }

        public void Serialize(ZPackage pkg, MemberUpdateFlags flags)
        {
            pkg.Write(Id);

            if ((flags & MemberUpdateFlags.Name) == MemberUpdateFlags.Name)
                pkg.Write(Name);

            if ((flags & MemberUpdateFlags.Rank) == MemberUpdateFlags.Rank)
                pkg.Write(Rank);

            if ((flags & MemberUpdateFlags.MemberSince) == MemberUpdateFlags.MemberSince)
                pkg.Write(MemberSince.ToBinary());

            if ((flags & MemberUpdateFlags.LastOnline) == MemberUpdateFlags.LastOnline)
                pkg.Write(LastOnline.ToBinary());

            if ((flags & MemberUpdateFlags.PlayerZDOID) == MemberUpdateFlags.PlayerZDOID)
                pkg.Write(PlayerZDOID);

            if ((flags & MemberUpdateFlags.PlayerHealth) == MemberUpdateFlags.PlayerHealth)
                pkg.Write(PlayerHealth);

            if ((flags & MemberUpdateFlags.PlayerMaxHealth) == MemberUpdateFlags.PlayerMaxHealth)
                pkg.Write(PlayerMaxHealth);

            if ((flags & MemberUpdateFlags.PlayerPosition) == MemberUpdateFlags.PlayerPosition)
                pkg.Write(PlayerPosition);
        }

        public void Deserialize(ZPackage pkg, MemberUpdateFlags flags)
        {
            Id = pkg.ReadLong();

            if ((flags & MemberUpdateFlags.Name) == MemberUpdateFlags.Name)
                Name = pkg.ReadString();

            if ((flags & MemberUpdateFlags.Rank) == MemberUpdateFlags.Rank)
                Rank = pkg.ReadInt();

            if ((flags & MemberUpdateFlags.MemberSince) == MemberUpdateFlags.MemberSince)
                MemberSince = DateTime.FromBinary(pkg.ReadLong());

            if ((flags & MemberUpdateFlags.LastOnline) == MemberUpdateFlags.LastOnline)
                LastOnline = DateTime.FromBinary(pkg.ReadLong());

            if ((flags & MemberUpdateFlags.PlayerZDOID) == MemberUpdateFlags.PlayerZDOID)
                PlayerZDOID = pkg.ReadZDOID();

            if ((flags & MemberUpdateFlags.PlayerHealth) == MemberUpdateFlags.PlayerHealth)
                PlayerHealth = pkg.ReadSingle();

            if ((flags & MemberUpdateFlags.PlayerMaxHealth) == MemberUpdateFlags.PlayerMaxHealth)
                PlayerMaxHealth = pkg.ReadSingle();

            if ((flags & MemberUpdateFlags.PlayerPosition) == MemberUpdateFlags.PlayerPosition)
                PlayerPosition = pkg.ReadVector3();
        }
    }

    public enum PlayerGroupType
    {
        Invalid = 0,
        Party = 1,
        Clan = 2,
    }

    [Flags]
    public enum GroupUpdateFlags
    {
        None = 0,
        Name = 1 << 1,
        GroupType = 1 << 2,
        Members = 1 << 3,
        CreationDate = 1 << 4,

        Periodic = Members,
        Once = Name | GroupType | Members | CreationDate,
    }

    [Serializable]
    public class PlayerGroup
    {
        internal PlayerGroupManager m_manager;
        [JsonProperty]
        public int Id { get; internal set; }
        [JsonProperty]
        public string Name { get; internal set; }
        [JsonProperty]
        public PlayerGroupType GroupType { get; internal set; }
        [JsonIgnore]
        public List<PlayerGroupMember> MemberList { get; set; } = new();
        [JsonProperty]
        public Dictionary<long, PlayerGroupMember> Members { get; private set; } = new();
        [JsonProperty]
        public DateTime CreationDate { get; internal set; }
        [JsonIgnore]
        public HashSet<long> PendingInvites { get; private set; } = new();

        public PlayerGroup()
        {

        }

        public PlayerGroup(PlayerGroupManager manager)
        {
            m_manager = manager;
        }

        public void AcceptInvite(ZNetPeer peer)
        {
            if (PendingInvites.Contains(peer.m_uid))
            {
                PendingInvites.Remove(peer.m_uid);
                if (!Members.ContainsKey(peer.m_uid))
                {
                    var member = JoinGroup(peer);
                    m_manager.playerAcceptInvite(this, member);
                }
            }
        }

        public void LeaveGroup(ZNetPeer peer)
        {
            if (Members.TryGetValue(peer.m_uid, out var member))
            {
                RemoveMember(peer.m_uid);
                m_manager.playerLeaveGroup(this, member);
            }
        }

        public void KickGroup(ZNetPeer peer, ZNetPeer instigator)
        {
            if (Members.TryGetValue(peer.m_uid, out var member))
            {
                RemoveMember(peer.m_uid);
                m_manager.playerKick(this, member, instigator);
            }
        }

        public void OnPeerDisconnect(ZNetPeer peer)
        {
            if (Members.TryGetValue(peer.m_uid, out var member))
            {
                member.LastOnline = DateTime.Now;
            }
        }

        internal PlayerGroupMember JoinGroup(ZNetPeer peer)
        {
            if (Members.TryGetValue(peer.m_uid, out var member))
            {
                return member;
            }
            var newMember = new PlayerGroupMember()
            {
                Id = peer.m_uid,
                Peer = peer,
                Player = peer.m_player,
                Name = peer.m_playerName,
                LastOnline = DateTime.Now,
                MemberSince = DateTime.Now,
                Rank = Members.Count == 0 ? 0 : int.MaxValue,
            };

            AddMember(peer.m_uid, newMember);
            m_manager.playerJoin(this, newMember);
            return newMember;
        }

        public void Invite(ZNetPeer target, ZNetPeer instigator)
        {
            PendingInvites.Add(target.m_uid);
            m_manager.playerInvite(this, target, instigator);
        }
        public void SendServerMessage(string text, params string[] args)
        {
            for (int i = 0; i < MemberList.Count; i++)
            {
                MemberList[i].Peer?.SendServerMessage(text, args);
            }
        }

        public void SendGroupMessage(ZNetPeer peer, string text)
        {
            var messageType = GroupType == PlayerGroupType.Party ? ChatMessageType.Party : (GroupType == PlayerGroupType.Clan ? ChatMessageType.Clan : ChatMessageType.Normal);

            for (int i = 0; i < MemberList.Count; i++)
            {
                MemberList[i].Peer?.SendChatMessage(peer, text, messageType);
            }
        }

        internal GroupUpdateFlags Serialize(ZNetPeer peer, ZPackage pkg, bool periodic)
        {
            var flags = periodic ? GroupUpdateFlags.Periodic : GroupUpdateFlags.Once;

            pkg.Write(Id);
            pkg.Write((int)flags);

            if ((flags & GroupUpdateFlags.Name) == GroupUpdateFlags.Name)
                pkg.Write(Name);

            if ((flags & GroupUpdateFlags.GroupType) == GroupUpdateFlags.GroupType)
                pkg.Write((int)GroupType);

            if ((flags & GroupUpdateFlags.CreationDate) == GroupUpdateFlags.CreationDate)
                pkg.Write(CreationDate.ToBinary());

            if ((flags & GroupUpdateFlags.Members) == GroupUpdateFlags.Members)
            {
                var count = SerializeMembers(peer, pkg, periodic);
                if (count == 0)
                    flags &= ~GroupUpdateFlags.Members;
            }

            return flags;
        }

        internal void Deserialize(ZPackage pkg)
        {
            Id = pkg.ReadInt();
            var flags = (GroupUpdateFlags)pkg.ReadInt();

            if ((flags & GroupUpdateFlags.Name) == GroupUpdateFlags.Name)
                Name = pkg.ReadString();

            if ((flags & GroupUpdateFlags.GroupType) == GroupUpdateFlags.GroupType)
                GroupType = (PlayerGroupType)pkg.ReadInt();

            if ((flags & GroupUpdateFlags.CreationDate) == GroupUpdateFlags.CreationDate)
                CreationDate = DateTime.FromBinary(pkg.ReadLong());

            if ((flags & GroupUpdateFlags.Members) == GroupUpdateFlags.Members)
                DeserializeMembers(pkg);
        }

        private int SerializeMembers(ZNetPeer peer, ZPackage pkg, bool periodic)
        {
            var memberCountPos = pkg.GetPos();
            var memberCount = 0;
            pkg.Write(memberCount);
            var members = Members.Values.ToList();

            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                MemberUpdateFlags flags;

                if (periodic)
                {
                    if (member.Peer == null) // || member.Peer == peer, though for testing purposes leaving this to replicate to myself.
                        continue;

                    var dist = (member.Peer.m_refPos - peer.m_refPos).sqrMagnitude;
                    flags = dist > 128f ? MemberUpdateFlags.PeriodicFar : MemberUpdateFlags.PeriodicNear;
                }
                else
                {
                    flags = MemberUpdateFlags.Once;
                }

                if (flags != MemberUpdateFlags.None)
                {
                    pkg.Write((int)flags);
                    member.Serialize(pkg, flags);
                    memberCount++;
                }
            }

            pkg.WriteCounter(memberCountPos, memberCount, true, true);
            return memberCount;
        }


        private void DeserializeMembers(ZPackage pkg)
        {
            var count = pkg.ReadInt();
            for (int i = 0; i < count; i++)
            {
                var flags = (MemberUpdateFlags)pkg.ReadInt();
                var id = pkg.PeekLong();

                if (!Members.TryGetValue(id, out var member))
                {
                    member = new PlayerGroupMember();
                    AddMember(id, member);
                }

                member.Deserialize(pkg, flags);
            }
        }

        internal void RemoveMember(long playerId)
        {
            if (Members.Remove(playerId))
            {
                MemberList.RemoveAt(MemberList.FindIndex(k => k.Id == playerId));
            }
        }

        internal void AddMember(long m_uid, PlayerGroupMember newMember)
        {
            Members.Add(m_uid, newMember);
            MemberList.Add(newMember);
        }
    }
}

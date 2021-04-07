using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ValheimMP.Framework.Events;

namespace ValheimMP.Framework
{
    public class Admin
    {
        private long m_id;
        [JsonProperty]
        public long Id { 
            get { return Peer == null ? m_id : Peer.m_uid; }
            internal set { m_id = value; }
        }

        private string m_name;
        [JsonProperty]
        public string Name {
            get { return Peer == null ? m_name : Peer.m_playerName; }
            internal set { m_name = value; }
        }

        [JsonProperty]
        public int Rank { get; set; }

        private DateTime m_lastOnline;
        [JsonProperty]
        public DateTime LastOnline
        {
            get { return Peer == null ? m_lastOnline : DateTime.Now; }
            internal set { m_lastOnline = value; }
        }

        [JsonIgnore]
        public ZNetPeer Peer { get; internal set; }
    }

    public class AdminManager
    {
        [JsonProperty]
        public Dictionary<long, Admin> Admins { get; private set; } = new();

        public event OnAdminOnlineHandler OnAdminOnline; 
        public delegate void OnAdminOnlineHandler(Admin admin);

        public AdminManager()
        {

        }

        internal void OnServerConnect(OnServerConnectArgs args)
        {
            if(Admins.TryGetValue(args.Peer.m_uid, out var admin))
            {
                admin.Peer = args.Peer;
                OnAdminOnline?.Invoke(admin);
            }
            else if(ZNet.instance.m_adminList.Contains(args.Peer.m_socket.GetHostName()))
            {
                var importedAdmin = AddAdmin(args.Peer);
                OnAdminOnline?.Invoke(importedAdmin);
            }
        }

        public Admin AddAdmin(ZNetPeer peer, ZNetPeer addedBy = null)
        {
            if(Admins.TryGetValue(peer.m_uid, out var existingAdmin))
            {
                return existingAdmin;
            }

            var rank = 0;

            if(addedBy != null && !Admins.TryGetValue(addedBy.m_uid, out var addedByAdmin))
            {
                rank = addedByAdmin.Rank + 1;
            }

            var newAdmin = new Admin
            {
                Id = peer.m_uid,
                LastOnline = DateTime.Now,
                Name = peer.m_playerName,
                Rank = rank,
            };

            Admins.Add(peer.m_uid, newAdmin);

            return newAdmin;
        }

        public bool IsAdmin(long playerId)
        {
            return Admins.ContainsKey(playerId);
        }

        internal static AdminManager Load(string jsonFile)
        {
            AdminManager manager = null;
            try
            {
                var jsonStr = System.IO.File.ReadAllText(jsonFile);
                manager = JsonConvert.DeserializeObject<AdminManager>(jsonStr);
            }
            catch (System.IO.FileNotFoundException)
            {
                ValheimMP.LogWarning($"{jsonFile} not found, creating new manager.");
                manager = new AdminManager();
            }
            catch (Exception ex)
            {
                ValheimMP.LogError($"Error while trying to load AdminManager: {ex}");
                manager = new AdminManager();
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
                ValheimMP.LogError($"Error while trying to save AdminManager: {ex}");
            }
        }

        public Admin GetAdmin(ZNetPeer peer)
        {
            if (peer == null)
                return null;
            if (Admins.TryGetValue(peer.m_uid, out var admin))
                return admin;
            return null;
        }

        public void RemoveAdmin(Admin admin)
        {
            Admins.Remove(admin.Id);
        }
    }
}

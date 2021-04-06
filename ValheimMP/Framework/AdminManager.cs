using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ValheimMP.Framework
{
    public class Admin
    {
        public long Id { get; internal set; }
        public string Name { get; internal set; }
        public int Rank { get; internal set; }

        private DateTime m_lastOnline;
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
        public Dictionary<long, Admin> Admins { get; private set; } = new();

        public event OnAdminOnlineDelegate OnAdminOnline; 
        public delegate void OnAdminOnlineDelegate(Admin admin);

        public AdminManager()
        {

        }

        internal bool OnServerConnect(ZRpc rpc, ZNetPeer peer, Dictionary<int, byte[]> customData)
        {
            if(Admins.TryGetValue(peer.m_uid, out var admin))
            {
                admin.Peer = peer;
                OnAdminOnline?.Invoke(admin);
            }
            else if(ZNet.instance.m_adminList.Contains(peer.m_socket.GetHostName()))
            {
                var importedAdmin = AddAdmin(peer);
                OnAdminOnline?.Invoke(importedAdmin);
            }

            return true;
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

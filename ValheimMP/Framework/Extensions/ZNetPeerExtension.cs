using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Framework.Extensions
{
    public static class ZNetPeerExtension
    {
        public static PlayerProfile GetPlayerProfile(this ZNetPeer peer)
        {
            return peer.m_playerProfile;
        }

        public static Player GetPlayer(this ZNetPeer peer)
        {
            return peer.m_player;
        }
        public static float GetPing(this ZNetPeer peer)
        {
            return peer.m_rpc.m_averagePing;
        }
    }
}

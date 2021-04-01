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

        public static void SendServerMessage(this ZNetPeer peer, string message)
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ChatMessage", peer.m_refPos, -1, "", message);
        }

        public static bool IsAdmin(this ZNetPeer peer)
        {
            // TODO: This is not exactly an optimal way, it should be stored somewhere.
            return ZNet.instance.m_adminList.Contains(peer.m_uid.ToString());
        }
    }
}

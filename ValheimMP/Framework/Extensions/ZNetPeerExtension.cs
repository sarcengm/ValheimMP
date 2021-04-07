using ValheimMP.Patches;

namespace ValheimMP.Framework.Extensions
{
    public static class ZNetPeerExtension
    {
        public static long GetSteamID(this ZNetPeer peer)
        {
            return (long)(peer.m_socket as ZSteamSocket).m_peerID.GetSteamID().m_SteamID;
        }

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
            return ValheimMP.Instance.AdminManager.IsAdmin(peer.m_uid);
        }
    }
}

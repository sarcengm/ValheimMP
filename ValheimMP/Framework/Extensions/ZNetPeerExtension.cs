using ValheimMP.Patches;

namespace ValheimMP.Framework.Extensions
{
    public enum ChatMessageType
    {
        
        Whisper = Talker.Type.Whisper,
        Normal = Talker.Type.Normal,
        Shout = Talker.Type.Shout,
        Ping = Talker.Type.Ping,

        // start at 100 just in case they ever add more chat message types so it wont instantly break when using Global as their new type
        Global = 100,
        Party,
        Clan,
        ServerMessage,
    }

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
            ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ChatMessage", peer.m_refPos, (int)ChatMessageType.ServerMessage, "", message);
        }

        public static void SendChatMessage(this ZNetPeer peer, string message, ChatMessageType type, string from)
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ChatMessage", peer.m_refPos, (int)type, from, message);
        }

        public static bool IsAdmin(this ZNetPeer peer)
        {
            return ValheimMP.Instance.AdminManager.IsAdmin(peer.m_uid);
        }
    }
}

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="message">message</param>
        /// <param name="args">arguments replaced into message after localization. {0} will be replaced with args[0]. </param>
        public static void SendServerMessage(this ZNetPeer peer, string message, params string[] args)
        {
            if (!ValheimMP.IsDedicated)
            {
                var pos = Player.m_localPlayer ? Player.m_localPlayer.transform.position : Utils.GetMainCamera().transform.position;
                Chat.instance.RPC_ChatMessage(ZNet.instance.GetUID(), pos, (int)ChatMessageType.ServerMessage, "", message);
                return;
            }

            var pkg = new ZPackage();
            pkg.Write(peer.m_uid);
            pkg.Write(peer.m_refPos);
            pkg.Write((int)ChatMessageType.ServerMessage);
            pkg.Write("");
            pkg.Write(message);

            pkg.Write(args != null ? args.Length : 0);
            for (int i = 0; i < args.Length; i++)
            {
                pkg.Write(args[i] == null ? "" : args[i]);
            }
            // server messages can not be send from the client, if a client sends one they will send it to themselves.
            ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ChatMessage", pkg);
        }

        public static void SendChatMessage(this ZNetPeer peer, ZNetPeer sender, string message, ChatMessageType type)
        {
            var pkg = new ZPackage();
            pkg.Write(sender.m_uid);
            pkg.Write(sender.m_refPos);
            pkg.Write((int)type);
            pkg.Write(sender.m_playerName);
            pkg.Write(message);

            ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ChatMessage", pkg);
        }

        public static bool IsAdmin(this ZNetPeer peer)
        {
            // If we are not on a dedicated server we can be counted as admin (well locally anyway)
            // The client doesn't actually have information about wether or not they are a real admin on the server.
            return !ValheimMP.IsDedicated || ValheimMP.Instance.AdminManager.IsAdmin(peer.m_uid);
        }
    }
}

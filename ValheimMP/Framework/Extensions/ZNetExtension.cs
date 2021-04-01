namespace ValheimMP.Framework.Extensions
{
    public static class ZNetExtension
    {
        public static ZNetPeer GetPeerByPlayerName(this ZNet znet, string playerName, bool ignoreCase)
        {
            foreach (var peer in znet.m_peers)
            {
                if (string.Compare(peer.m_playerName, playerName, ignoreCase) == 0)
                    return peer;
            }
            return null;
        }
    }
}

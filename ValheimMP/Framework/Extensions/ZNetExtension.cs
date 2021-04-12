namespace ValheimMP.Framework.Extensions
{
    public static class ZNetExtension
    {
        public static ZNetPeer GetPeerByPlayerName(this ZNet znet, string playerName, bool ignoreCase)
        {
            var peers = ZNet.instance.m_peers;
            for (int i = 0; i < peers.Count; i++)
            {
                var peer = peers[i];
                if (string.Compare(peer.m_playerName, playerName, ignoreCase) == 0)
                    return peer;
            }
            return null;
        }
    }
}

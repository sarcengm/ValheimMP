using System.Collections.Generic;

namespace ValheimMP.Framework.Events
{
    public class OnServerConnectArgs
    {
        public ZRpc Rpc { get; internal set; }
        public ZNetPeer Peer { get; internal set; }
        public Dictionary<int, byte[]> CustomData { get; internal set; }

        /// <summary>
        /// Set to true if you want to abort connecting this client
        /// Should still be manually disconnect for example via peer.SendErrorMessage(...);
        /// </summary>
        public bool AbortConnect { get; set; }
    }
}

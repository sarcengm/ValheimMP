using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Framework.Events
{
    public class OnClientConnectArgs
    {
        public ZRpc Rpc { get; internal set; }
        public ZNetPeer Peer { get; internal set; }
        public Dictionary<int, byte[]> CustomData { get; internal set; }

        /// <summary>
        /// Set to true if you want to abort connecting this client
        /// </summary>
        public bool AbortConnect { get; set; }
    }
}

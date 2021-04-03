using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Framework.Extensions
{
    public static class ZRpcExtension
    {
        public static void SendErrorMessage(this ZRpc rpc, string message)
        {
            rpc.Invoke("Error", 3);
        }
    }
}

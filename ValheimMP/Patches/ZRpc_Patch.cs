using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Patches
{
#if DEBUG
    [HarmonyPatch(typeof(ZRpc))]
    public class ZRpc_Patch
    {
        [HarmonyPatch("Invoke")]
        [HarmonyPrefix]
        private static void Invoke(ref ZRpc __instance, string method, params object[] parameters)
        {
            if(ValheimMP.Instance.DebugRPC)
                ZLog.Log("RPC Invoking " + method + " " + method.GetStableHashCode());
        }
    }
#endif
}

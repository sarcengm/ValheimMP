using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ValheimMP.Util;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    public class ZRoutedRpc_Patch
    {
#if DEBUG
        [HarmonyPatch(typeof(ZRoutedRpc), "InvokeRoutedRPC", new Type[] { typeof(long), typeof(ZDOID), typeof(string), typeof(object[]) })]
        [HarmonyPrefix]
        private static void InvokeRoutedRPC(ref ZRoutedRpc __instance, long targetPeerID, ZDOID targetZDO, string methodName, params object[] parameters)
        {
            if (ValheimMP.Instance.DebugRPC)
                ZLog.Log("RoutedRPC Invoking " + methodName + " " + methodName.GetStableHashCode());
        }
#endif

        [HarmonyPatch(typeof(ZRoutedRpc), "RPC_RoutedRPC", new[] { typeof(ZRpc), typeof(ZPackage) })]
        [HarmonyPrefix]
        private static bool RPC_RoutedRPC(ref ZRoutedRpc __instance, ZRpc rpc, ZPackage pkg)
        {
            ZRoutedRpc.RoutedRPCData routedRPCData = new ZRoutedRpc.RoutedRPCData();
            routedRPCData.Deserialize(pkg);

            var senderId = (long)(rpc.GetSocket() as ZSteamSocket).GetPeerID().m_SteamID;

            if (__instance.m_server && routedRPCData.m_senderPeerID != senderId)
            {
                var me = ZDOMan.instance.GetMyID();
                ZLog.LogWarning($"RPC_RoutedRPC from: {senderId} does not match routedRPCData.m_senderPeerID: {routedRPCData.m_senderPeerID}. server: {me}");
                return false;
            }

            if (routedRPCData.m_targetPeerID == __instance.m_id || routedRPCData.m_targetPeerID == 0L)
            {
                __instance.HandleRoutedRPC(routedRPCData);
            }
            if (__instance.m_server && routedRPCData.m_targetPeerID != __instance.m_id)
            {
                // The only routing we do is routes that are started on the server side.
                if (routedRPCData.m_senderPeerID == ZNet_Patch.GetServerID())
                {
                    __instance.RouteRPC(routedRPCData);
                }
                else
                {
#if DEBUG
                    var hashName = StringExtensionMethods_Patch.GetStableHashName(routedRPCData.m_methodHash);
#else
                    var hashName = routedRPCData.m_methodHash;
#endif
                    ZLog.LogWarning($"Discarding routed RPC from {routedRPCData.m_senderPeerID} to {routedRPCData.m_targetPeerID}: Method: {hashName}");
                }
            }

            return false;
        }
    }
}

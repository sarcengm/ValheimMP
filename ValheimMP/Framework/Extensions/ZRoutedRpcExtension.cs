using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static ZRoutedRpc;

namespace ValheimMP.Framework.Extensions
{
    public static class ZRoutedRpcExtension
    {
        public static bool IsReadyAndInRange(this ZNetPeer peer, float range, Vector3 pos)
        {
            if (!peer.IsReady())
                return false;
            if (range == 0)
                return true;
            return (peer.m_refPos - pos).sqrMagnitude <= range * range;
        }

        public static void InvokeProximityRoutedRPC(this ZRoutedRpc rpc, Vector3 position, float range, long targetPeerID, ZDOID targetZDO, string method, params object[] parameters)
        {
            rpc.InvokeProximityRoutedRPC(position, range, targetPeerID, targetZDO, method.GetStableHashCode(), parameters);
        }

        public static void InvokeProximityRoutedRPC(this ZRoutedRpc rpc, Vector3 position, float range, long targetPeerID, ZDOID targetZDO, int methodHash, params object[] parameters)
        {
            RoutedRPCData routedRPCData = new RoutedRPCData();
            routedRPCData.m_msgID = rpc.m_id + rpc.m_rpcMsgID++;
            routedRPCData.m_senderPeerID = rpc.m_id;
            routedRPCData.m_targetPeerID = targetPeerID;
            routedRPCData.m_targetZDO = targetZDO;
            routedRPCData.m_methodHash = methodHash;
            routedRPCData.m_range = range;
            routedRPCData.m_position = position;
            ZRpc.Serialize(parameters, ref routedRPCData.m_parameters);
            routedRPCData.m_parameters.SetPos(0);
            if (targetPeerID == rpc.m_id || targetPeerID == 0L)
            {
                rpc.HandleRoutedRPC(routedRPCData);
            }
            if (targetPeerID != rpc.m_id)
            {
                rpc.RouteRPC(routedRPCData);
            }
        }

        public static void InvokeRoutedRPC(this ZRoutedRpc rpc, long targetPeerID, ZDOID targetZDO, int methodHash, params object[] parameters)
        {
            RoutedRPCData routedRPCData = new RoutedRPCData();
            routedRPCData.m_msgID = rpc.m_id + rpc.m_rpcMsgID++;
            routedRPCData.m_senderPeerID = rpc.m_id;
            routedRPCData.m_targetPeerID = targetPeerID;
            routedRPCData.m_targetZDO = targetZDO;
            routedRPCData.m_methodHash = methodHash;
            ZRpc.Serialize(parameters, ref routedRPCData.m_parameters);
            routedRPCData.m_parameters.SetPos(0);
            if (targetPeerID == rpc.m_id || targetPeerID == 0L)
            {
                rpc.HandleRoutedRPC(routedRPCData);
            }
            if (targetPeerID != rpc.m_id)
            {
                rpc.RouteRPC(routedRPCData);
            }
        }

        public static void InvokeRoutedRPC(this ZRoutedRpc rpc, long targetPeerID, int methodHash, params object[] parameters)
        {
            rpc.InvokeRoutedRPC(targetPeerID, ZDOID.None, methodHash, parameters);
        }

        public static void InvokeRoutedRPC(this ZRoutedRpc rpc, int methodHash, params object[] parameters)
        {
            rpc.InvokeRoutedRPC(rpc.GetServerPeerID(), methodHash, parameters);
        }
    }
}

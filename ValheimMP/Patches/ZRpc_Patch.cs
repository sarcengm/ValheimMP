using HarmonyLib;
using UnityEngine;
using static ZRpc;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class ZRpc_Patch
    {
        [HarmonyPatch(typeof(ZRpc), "Invoke")]
        [HarmonyPrefix]
        private static void Invoke(ZRpc __instance, string method, params object[] parameters)
        {
            if(ValheimMP.Instance.DebugRPC.Value)
                ValheimMP.Log("RPC Invoking " + method + " " + method.GetStableHashCode());
        }

        [HarmonyPatch(typeof(ZRpc), "UpdatePing")]
        [HarmonyPrefix]
        private static bool UpdatePing(ZRpc __instance, float dt)
        {
            __instance.m_pingTimer += dt;
            if (__instance.m_pingTimer > ZRpc.m_pingInterval)
            {
                __instance.m_pingTime = Time.realtimeSinceStartup;
                __instance.m_pingTimer = 0f;
                __instance.m_pkg.Clear();
                __instance.m_pkg.Write(1);
                __instance.SendPackage(__instance.m_pkg);
            }
            __instance.m_timeSinceLastPing += dt;
            if (__instance.m_timeSinceLastPing > ZRpc.m_timeout)
            {
                ValheimMP.LogWarning("ZRpc timeout detected");
                __instance.m_socket.Close();
            }

            return false;
        }

        private static bool ReceivePing(ZRpc __instance, ZPackage package)
        {
            __instance.m_pkg.Clear();
            __instance.m_pkg.Write(2);
            __instance.SendPackage(__instance.m_pkg);
            return false;
        }

        private static void ReceivePong(ZRpc __instance, ZPackage package)
        {
            var currentPing = Time.realtimeSinceStartup - __instance.m_pingTime;
            // the laziest possible average ping calculation!
            __instance.m_averagePing = (__instance.m_averagePing*2 + __instance.m_ping + currentPing) / 4f;
            __instance.m_ping = currentPing;
            __instance.m_timeSinceLastPing = 0f;
        }

        [HarmonyPatch(typeof(ZRpc), "HandlePackage")]
        [HarmonyPrefix]
        private static bool HandlePackage(ZRpc __instance, ZPackage package)
        {
            int num = package.ReadInt();
            RpcMethodBase value2;
            if (num == 0)
            {
                __instance.ReceivePing(package);
            }
            else if (num == 1)
            {
                ReceivePing(__instance, package);
            }
            else if(num == 2)
            {
                ReceivePong(__instance, package);
            }
            else if (m_DEBUG)
            {
                package.ReadString();
                if (__instance.m_functions.TryGetValue(num, out var value))
                {
                    value.Invoke(__instance, package);
                }
            }
            else if (__instance.m_functions.TryGetValue(num, out value2))
            {
                value2.Invoke(__instance, package);
            }

            return false;
        }

    }
}

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class PrivateArea_Patch
    {

        public static bool CheckAccess(long playerID, Vector3 point, float radius = 0f, bool flash = true)
        {
            bool flag = false;
            List<PrivateArea> list = new List<PrivateArea>();
            foreach (PrivateArea allArea in PrivateArea.m_allAreas)
            {
                if (allArea.IsEnabled() && allArea.IsInside(point, radius))
                {
                    var piece = allArea.GetComponent<Piece>();
                    if ((piece != null && piece.GetCreator() == playerID) || allArea.IsPermitted(playerID))
                    {
                        flag = true;
                    }
                    else
                    {
                        list.Add(allArea);
                        
                    }

                    break;
                }
            }
            if (!flag && list.Count > 0)
            {
                if (flash)
                {
                    foreach (PrivateArea item in list)
                    {
                        item.FlashShield(flashConnected: false);
                    }
                }
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(Beehive), "RPC_Extract")]
        [HarmonyPrefix]
        private static bool RPC_Extract(Beehive __instance, long caller)
        {
            return CheckAccess(caller, __instance.transform.position);
        }

        [HarmonyPatch(typeof(Door), "RPC_UseDoor")]
        [HarmonyPrefix]
        private static bool RPC_UseDoor(Door __instance, long uid)
        {
            return CheckAccess(uid, __instance.transform.position);
        }

        [HarmonyPatch(typeof(PrivateArea), "FlashShield")]
        [HarmonyPrefix]
        private static bool FlashShield(PrivateArea __instance, bool flashConnected)
        {
            if (!__instance.m_flashAvailable)
            {
                return false;
            }

            __instance.m_flashAvailable = false;
            if (ZNet.instance.IsServer())
            {
                __instance.m_nview.InvokeRPC(ZNetView.Everybody, "FlashShield");

                if (!flashConnected)
                {
                    return false;
                }
                foreach (PrivateArea connectedArea in __instance.GetConnectedAreas())
                {
                    if (connectedArea.m_nview.IsValid())
                    {
                        connectedArea.m_nview.InvokeRPC(ZNetView.Everybody, "FlashShield");
                    }
                }
            }
            else
            {
                // basically a client side only flash, others wont see or hear it if it fails before reaching the server.
                __instance.RPC_FlashShield(0);
            }
            return false;
        }
    }
}

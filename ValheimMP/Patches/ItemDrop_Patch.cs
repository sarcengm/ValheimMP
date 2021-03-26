using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class ItemDrop_Patch
    {
        [HarmonyPatch(typeof(ItemDrop), "RequestOwn")]
        [HarmonyPrefix]
        private static bool RequestOwn()
        {

            // Request own? never going to happen.
            return false;
        }

        [HarmonyPatch(typeof(ItemDrop), "Pickup")]
        [HarmonyPrefix]
        private static bool Pickup(ItemDrop __instance, Humanoid character)
        {
            if (__instance.m_nview.IsValid())
            {
                if (__instance.CanPickup())
                {
                    __instance.Load();
                    character.Pickup(__instance.gameObject);
                    __instance.Save();
                }
                else if(!ZNet.instance.IsServer())
                {
                    __instance.m_nview.InvokeRPC("RequestOwn");
                }
            }

            return false;
        }

        [HarmonyPatch(typeof(ItemDrop), "RPC_RequestOwn")]
        [HarmonyPrefix]
        private static bool RPC_RequestOwn(ItemDrop __instance, long uid)
        {
            if (!PrivateArea_Patch.CheckAccess(uid, __instance.transform.position))
                return false;

            var peer = ZNet.instance.GetPeer(uid);
            if (peer == null || peer.m_player == null)
                return false;

            if(__instance.CanPickup() && (__instance.transform.position - peer.m_player.transform.position).magnitude <= peer.m_player.m_autoPickupRange)
            {
                __instance.Pickup(peer.m_player);
            }
            return false;
        }
    }
}

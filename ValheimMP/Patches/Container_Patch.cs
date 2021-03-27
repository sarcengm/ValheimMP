using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Container_Patch
    {
        [HarmonyPatch(typeof(Container), "Awake")]
        [HarmonyPostfix]
        private static void Awake(Container __instance)
        {
            Inventory_Patch.Register(__instance.m_inventory, __instance.m_nview);
            
            if (__instance.m_nview != null)
            {
                if (__instance.m_nview.m_zdo != null)
                {
                    // Don't ever replicate this garbage
                    __instance.m_nview.m_zdo.SetFieldType("items", ZDOFieldType.Ignored);
                }

                __instance.m_nview.Register("CloseChest", (long sender) => {
                    RPC_CloseChest(__instance, sender);
                });
            }
        }

        private static void RPC_CloseChest(Container __instance, long sender)
        {
            Inventory_Patch.RemoveListener(sender, __instance.m_inventory);
            var l = Inventory_Patch.GetListeners(__instance.m_inventory);
            if(l == null || l.Count == 0)
            {
                m_LastChestUser = sender;
                SetInUse(__instance, false);
            }
        }

        static long m_LastChestUser;

        [HarmonyPatch(typeof(Container), "RPC_RequestOpen")]
        [HarmonyPrefix]
        private static bool RPC_RequestOpen(Container __instance, long uid, long playerID)
        {
            if (!PrivateArea_Patch.CheckAccess(uid, __instance.transform.position))
                return false;

            Inventory_Patch.AddListener(uid, __instance.m_inventory);
            m_LastChestUser = uid;
            SetInUse(__instance, true);
            __instance.m_nview.InvokeRPC(uid, "OpenRespons", true);
            return false;
        }

        [HarmonyPatch(typeof(Container), "SetInUse")]
        [HarmonyPrefix]
        private static bool SetInUse(Container __instance, bool inUse)
        {
            if (__instance.m_inUse != inUse)
            {
                if (!inUse && !ZNet.instance.IsServer())
                {
                    __instance.m_inventory.m_inventory.Clear();
                    __instance.m_nview.InvokeRPC("CloseChest");
                }

                __instance.m_inUse = inUse;
                __instance.UpdateUseVisual();

                if (inUse)
                {
                    __instance.m_openEffects.CreateNonOriginator(__instance.transform.position, __instance.transform.rotation, originator: m_LastChestUser);
                }
                else
                {
                    __instance.m_closeEffects.CreateNonOriginator(__instance.transform.position, __instance.transform.rotation, originator: m_LastChestUser);
                }
            }
            return false;
        }


        [HarmonyPatch(typeof(Container), "TakeAll")]
        [HarmonyPrefix]
        private static bool TakeAll(Container __instance, ref bool __result, Humanoid character)
        {
            __result = false;
            if (__instance.m_checkGuardStone && !PrivateArea.CheckAccess(__instance.transform.position))
            {
                return false;
            }
            long playerID = Game.instance.GetPlayerProfile().GetPlayerID();
            if (!__instance.CheckAccess(playerID))
            {
                character.Message(MessageHud.MessageType.Center, "$msg_cantopen");
                return false;
            }

            __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "RequestTakeAll", playerID);
            __result = true;
            return false;
        }

        [HarmonyPatch(typeof(Container), "RPC_RequestTakeAll")]
        [HarmonyPrefix]
        private static bool RPC_RequestTakeAll(Container __instance, long uid, long playerID)
        {
            if (__instance.m_checkGuardStone && !PrivateArea_Patch.CheckAccess(uid, __instance.transform.position))
                return false;

            if (!__instance.CheckAccess(uid))
                return false;

            var peer = ZNet.instance.GetPeer(uid);
            if (peer == null || peer.m_player == null)
                return false;
            peer.m_player.m_inventory.MoveAll(__instance.m_inventory);
            __instance.m_onTakeAllSuccess?.Invoke();
            __instance.m_onTakeAllSuccess2?.Invoke(peer.m_player);
            return false;
        }


        [HarmonyPatch(typeof(Container), "RPC_TakeAllRespons")]
        [HarmonyPrefix]
        private static bool RPC_TakeAllRespons(Container __instance, long uid, bool granted)
        {
            return false;
        }
    }
}

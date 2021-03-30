using HarmonyLib;
using System.Linq;
using static Fermenter;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Fermenter_Patch
    {

        [HarmonyPatch(typeof(Fermenter), "RPC_AddItem")]
        [HarmonyPrefix]
        private static bool RPC_AddItem(Fermenter __instance, long sender, string name)
        {
            if (!PrivateArea_Patch.CheckAccess(sender, __instance.transform.position))
                return false;

            var peer = ZNet.instance.GetPeer(sender);
            if (peer == null)
                return false;           

            if (__instance.m_nview.IsOwner() && __instance.GetStatus() == Status.Empty)
            {
                if (!__instance.IsItemAllowed(name))
                {
                    return false;
                }
                var item = peer.m_player.m_inventory.m_inventory.SingleOrDefault(k => k.m_dropPrefab.name == name);
                if (item == null || !peer.m_player.m_inventory.RemoveOneItem(item))
                {
                    return false;
                }
                __instance.m_addedEffects.Create(__instance.transform.position, __instance.transform.rotation);
                __instance.m_nview.GetZDO().Set("Content", name);
                __instance.m_nview.GetZDO().Set("StartTime", ZNet.instance.GetTime().Ticks);
            }

            return false;
        }

        [HarmonyPatch(typeof(Fermenter),"RPC_Tap")]
        [HarmonyPrefix]
        private static bool RPC_Tap(Fermenter __instance, long sender)
        {
            if (!PrivateArea_Patch.CheckAccess(sender, __instance.transform.position))
                return false;

            if (__instance.m_nview.IsOwner() && __instance.GetStatus() == Status.Ready)
            {
                __instance.m_delayedTapItem = __instance.GetContent();
                __instance.Invoke("DelayedTap", __instance.m_tapDelay);
                __instance.m_tapEffects.Create(__instance.transform.position, __instance.transform.rotation);
                __instance.m_nview.GetZDO().Set("Content", "");
                __instance.m_nview.GetZDO().Set("StartTime", 0);
            }

            return false;
        }

        [HarmonyPatch(typeof(Fermenter), "AddItem")]
        [HarmonyPrefix]
        private static bool AddItem(Fermenter __instance, ref bool __result, Humanoid user, ItemDrop.ItemData item)
        {
            __result = false;
            if (__instance.GetStatus() != 0)
            {
                return false;
            }
            if (!__instance.IsItemAllowed(item))
            {
                return false;
            }
            var player = user as Player;
            if (user == null || !PrivateArea_Patch.CheckAccess(player.GetPlayerID(), __instance.transform.position))
                return false;

            __instance.m_nview.InvokeRPC("AddItem", item.m_dropPrefab.name);
            __result = true;
            return true;
        }

    }
}

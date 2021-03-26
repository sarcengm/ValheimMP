using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Ladder_Patch
    {
        [HarmonyPatch(typeof(Ladder), "Awake")]
        [HarmonyPrefix]
        private static bool Awake(Ladder __instance)
        {
            var m_nview = __instance.GetComponentInParent<ZNetView>();
            if (m_nview && ZNet.instance != null && ZNet.instance.IsServer())
            {
                m_nview.Register("ClimbLadder_" + __instance.name, (long sender) =>
                {
                    RPC_ClimbLadder(__instance, sender);
                });
            }
            return false;
        }

        private static void RPC_ClimbLadder(Ladder __instance, long sender)
        {
            var peer = ZNet.instance.GetPeer(sender);

            if (peer != null && peer.m_player != null && __instance.InUseDistance(peer.m_player))
            {
                peer.m_player.transform.position = __instance.m_targetPos.position;
                peer.m_player.transform.rotation = __instance.m_targetPos.rotation;
                peer.m_player.SetLookDir(__instance.m_targetPos.forward);
            }

            return;
        }

        [HarmonyPatch(typeof(Ladder), "Interact")]
        [HarmonyPrefix]
        private static bool Interact(ref Ladder __instance, ref bool __result, Humanoid character, bool hold)
        {
            if (hold)
            {
                __result = false;
                return false;
            }
            if (!__instance.InUseDistance(character))
            {
                __result = false;
                return false;
            }

            var nview = __instance.GetComponentInParent<ZNetView>();
            if (nview)
            {
                nview.InvokeRPC("ClimbLadder_" + __instance.name);
            }
            __result = true;
            return false;
        }
    }
}

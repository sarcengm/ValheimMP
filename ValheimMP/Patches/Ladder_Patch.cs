using HarmonyLib;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Ladder_Patch
    {
        [HarmonyPatch(typeof(ZNetView), "Awake")]
        [HarmonyPostfix]
        private static void Awake(ZNetView __instance)
        {
            if (__instance.m_zdo == null)
                return;

            var ladder = __instance.GetComponentInChildren<Ladder>();
            if (ladder && ValheimMP.IsDedicated)
            {
                __instance.Register("ClimbLadder", (long sender, string name) =>
                {
                    RPC_ClimbLadder(__instance, sender, name);
                });

                ladder.m_useDistance *= 1.2f;
            }
        }

        private static void RPC_ClimbLadder(ZNetView netView, long sender, string name)
        {
            var gameObj = netView.transform.Find(name);
            if (gameObj == null)
            {
                ValheimMP.Log($"Missing Ladder game object: {name} in {netView}");
                return;
            }

            var ladder = gameObj.GetComponent<Ladder>();
            if (ladder == null)
            {
                ValheimMP.Log($"Missing Ladder component: {name} in {netView}->{gameObj}");
                return;
            }

            var peer = ZNet.instance.GetPeer(sender);

            if (peer != null && peer.m_player != null && ladder.InUseDistance(peer.m_player))
            {
                peer.m_player.transform.position = ladder.m_targetPos.position;
                peer.m_player.transform.rotation = ladder.m_targetPos.rotation;
                peer.m_player.SetLookDir(ladder.m_targetPos.forward);
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
                nview.InvokeRPC("ClimbLadder", __instance.gameObject.GetFullName(nview.gameObject));
            }

            character.transform.position = __instance.m_targetPos.position;
            character.transform.rotation = __instance.m_targetPos.rotation;
            character.SetLookDir(__instance.m_targetPos.forward);
            __result = true;
            return false;
        }
    }
}

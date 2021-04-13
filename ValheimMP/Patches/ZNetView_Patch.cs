using HarmonyLib;
using ValheimMP.Framework;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class ZNetView_Patch
    {

        [HarmonyPatch(typeof(ZNetView), "Awake")]
        [HarmonyPrefix]
        private static void AwakePrefix(ref ZNetView __instance)
        {
            if (ZNetView.m_initZDO != null && ZNet.instance.IsServer())
            {
                ZNetView.m_initZDO.SetOwner(ZDOMan.instance.GetMyID());
            }
        }

        [HarmonyPatch(typeof(ZNetView), "Awake")]
        [HarmonyPostfix]
        private static void Awake(ref ZNetView __instance)
        {
            if (__instance.IsValid() && ZNet.instance.IsServer() != __instance.IsOwner())
            {
                // for now skip sfx and vfx.
                if (!__instance.name.StartsWith("sfx") &&
                    !__instance.name.StartsWith("vfx") &&
                    !__instance.name.StartsWith("fx") &&
                    __instance.GetComponent<Player>() == null &&
                    __instance.GetComponent<PlayerController>() == null)
                {
                    DebugMod.LogComponent(__instance, "Non server ownership " + __instance.name + " " + ZDOMan.instance.GetMyID());
                }
            }

            if (__instance.m_zdo == null)
                return;

            __instance.m_zdo.m_nview = __instance;

            //if (ZNet.instance.IsServer())
            {
                if (__instance.m_type != (ZDO.ObjectType)(-1))
                {
                    if (__instance.GetComponent<StaticPhysics>() != null ||
                        __instance.GetComponent<Piece>() != null ||
                        __instance.GetComponent<ItemDrop>() != null || // Adding item drop to Solid may be questionable since those things can move and roll around but when its not its basically a solid
                        __instance.GetComponent<Pickable>() != null)
                    {
                        __instance.m_type = ZDO.ObjectType.Solid;
                        if (__instance.m_zdo != null)
                        {
                            __instance.m_zdo.SetType(ZDO.ObjectType.Solid);

                        }
                    }
                }

                SectorManager.AddObject(__instance.m_zdo);

                if (ValheimMP.IsDedicated && __instance.m_zdo.m_type == ZDO.ObjectType.Solid)
                {
                    // Solid object spawned into loaded player sector, this wont get send unless its changed, so add it to be send.
                    var peers = ZNet.instance.m_peers;
                    for (int i = 0; i < peers.Count; i++)
                    {
                        var peer = peers[i];
                        if (peer.m_loadedSectors.ContainsKey(__instance.m_zdo.m_sector))
                        {
                            peer.m_solidObjectQueue[__instance.m_zdo.m_uid] = __instance.m_zdo;
                        }
                    }
                }
            }

        }

        [HarmonyPatch(typeof(ZNetView), "Destroy")]
        [HarmonyPrefix]
        private static void Destroy(ref ZNetView __instance)
        {
            if (__instance.m_zdo != null)
            {
                SectorManager.RemoveObject(__instance.m_zdo);
                __instance.m_zdo.m_nview = null;
            }
        }

        [HarmonyPatch(typeof(ZNetView), "ResetZDO")]
        [HarmonyPrefix]
        private static void ResetZDO(ref ZNetView __instance)
        {
            if (__instance.m_zdo != null)
            {
                SectorManager.RemoveObject(__instance.m_zdo);
                __instance.m_zdo.m_nview = null;
            }
        }

#if DEBUG
        [HarmonyPatch(typeof(ZNetView), "HandleRoutedRPC")]
        [HarmonyPrefix]
        private static bool HandleRoutedRPC(ref ZNetView __instance, ZRoutedRpc.RoutedRPCData rpcData)
        {
            if (__instance.m_functions.TryGetValue(rpcData.m_methodHash, out var value))
            {
                value.Invoke(rpcData.m_senderPeerID, rpcData.m_parameters);
            }
            else
            {
                ValheimMP.LogWarning("Failed to find rpc method " + StringExtensionMethods_Patch.GetStableHashName(rpcData.m_methodHash));
            }
            return false;
        }
#endif
    }


}

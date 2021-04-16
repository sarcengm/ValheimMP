using HarmonyLib;
using System.Collections.Generic;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal static class ZNetPeer_Patch
    {
        [HarmonyPatch(typeof(ZNetPeer), MethodType.Constructor, new[] { typeof(ISocket), typeof(bool) })]
        [HarmonyPostfix]
        private static void Constructor(ref ZNetPeer __instance, ISocket socket, bool server)
        {
            __instance.m_lastSector = new Vector2i(9999999, 9999999);
            __instance.m_loadedSectors = new Dictionary<Vector2i, KeyValuePair<int, bool>>();
            __instance.m_solidObjectQueue = new Dictionary<ZDOID, ZDO>();
            __instance.m_rpc.m_peer = __instance;
        }

        [HarmonyPatch(typeof(ZNetPeer), "Dispose")]
        [HarmonyPostfix]
        private static void Dispose(ZNetPeer __instance)
        {
            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                SavePeer(__instance);
                ValheimMP.Instance.InventoryManager.RemoveListenerFromAll(__instance.m_uid);

                if(__instance.m_player && __instance.m_player.m_nview && __instance.m_player.m_nview.m_zdo != null)
                {
                    var zdo = __instance.m_player.m_nview.m_zdo;
                    ZDOMan.instance.DestroyZDO(zdo);
                }
            }
        }

        public static void SavePeer(ZNetPeer __instance, bool setLogoutPoint=true)
        {
            ValheimMP.Log($"Saving {__instance.m_playerName} {__instance.m_player}");
            if (__instance.m_player != null)
            {
                __instance.m_playerProfile.SavePlayerData(__instance.m_player);
                //Minimap.instance.SaveMapData();
                if (setLogoutPoint && !__instance.m_player.IsDead() && !__instance.m_player.InIntro())
                {
                    __instance.m_playerProfile.SetLogoutPoint(__instance.m_player.transform.position);
                }
                
                __instance.m_playerProfile.Save();
            }

            Game_Patch.SaveWorld();
        }
    }


}

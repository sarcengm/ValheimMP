using HarmonyLib;
using System.Collections.Generic;
using ValheimMP.Framework;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal static class ZNetPeer_Patch
    {
        [HarmonyPatch(typeof(ZNetPeer), MethodType.Constructor, new[] { typeof(ISocket), typeof(bool) })]
        [HarmonyPostfix]
        private static void Constructor(ref ZNetPeer __instance, ISocket socket, bool server)
        {
            __instance.m_loadedSectors = new Dictionary<Vector2i, KeyValuePair<int, bool>>();
            __instance.m_solidObjectQueue = new Dictionary<ZDOID, ZDO>();
        }

        [HarmonyPatch(typeof(ZNetPeer), "Dispose")]
        [HarmonyPostfix]
        private static void Dispose(ZNetPeer __instance)
        {
            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                SavePeer(__instance);
                ValheimMPPlugin.Instance.InventoryManager.RemoveListenerFromAll(__instance.m_uid);
            }
        }

        public static void SavePeer(ZNetPeer __instance, bool setLogoutPoint=true)
        {
            ZLog.Log($"Saving {__instance.m_playerName} {__instance.m_player}");
            if (__instance.m_player != null)
            {
                __instance.m_playerProfile.SavePlayerData(__instance.m_player);
                //Minimap.instance.SaveMapData();
                if (setLogoutPoint && !__instance.m_player.IsDead() && !__instance.m_player.InIntro())
                {
                    __instance.m_playerProfile.SetLogoutPoint(__instance.m_player.transform.position);
                }
            }

            __instance.m_playerProfile.Save();
        }
    }


}

using HarmonyLib;
using System;
using System.Collections.Generic;

namespace ValheimMP.Patches
{

    [HarmonyPatch]
    internal class SpawnSystem_Patch
    {
        [HarmonyPatch(typeof(SpawnSystem), "Awake")]
        [HarmonyPrefix]
        private static bool Awake(ref SpawnSystem __instance)
        {
            __instance.GetComponent<ZNetView>().m_type = (ZDO.ObjectType)(-1);

            if (!ZNet.instance.IsServer())
            {
                UnityEngine.Object.Destroy(__instance);
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(SpawnSystem), "UpdateSpawning")]
        [HarmonyPrefix]
        private static bool UpdateSpawning(SpawnSystem __instance)
        {
            if (!__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner())
                return false;

            __instance.m_nearPlayers.Clear();
            __instance.GetPlayersInZone(__instance.m_nearPlayers);
            if (__instance.m_nearPlayers.Count != 0)
            {
                DateTime time = ZNet.instance.GetTime();
                __instance.UpdateSpawnList(__instance.m_spawners, time, eventSpawners: false);
                List<SpawnSystem.SpawnData> currentSpawners = RandEventSystem.instance.GetCurrentSpawners();
                if (currentSpawners != null)
                {
                    __instance.UpdateSpawnList(currentSpawners, time, eventSpawners: true);
                }
            }

            return false;
        }
        [HarmonyPatch(typeof(SpawnSystem), "GetPlayersInZone")]
        [HarmonyPrefix]
        private static bool GetPlayersInZone(SpawnSystem __instance, List<Player> players)
        {
            var peers = ZNet.instance.GetPeers();
            for (int i = 0; i < peers.Count; i++)
            {
                var peer = peers[i];
                if (peer.m_player && !peer.m_player.IsDead() && __instance.InsideZone(peer.m_refPos))
                {
                    players.Add(peer.m_player);
                }
            }
            return false;
        }
    }
}

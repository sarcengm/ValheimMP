using HarmonyLib;
using System;
using System.Collections.Generic;
using static SpawnSystem;

namespace ValheimMP.Patches
{

    [HarmonyPatch]
    internal class SpawnSystem_Patch
    {
        [HarmonyPatch(typeof(SpawnSystem), "Awake")]
        [HarmonyPrefix]
        private static bool Awake(ref SpawnSystem __instance)
        {
            var nview = __instance.GetComponent<ZNetView>();
            if (nview)
            {
                nview.m_type = (ZDO.ObjectType)(-1);
            }

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
            if (!__instance.m_nview || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner())
                return false;

            __instance.m_nearPlayers.Clear();
            __instance.GetPlayersInZone(__instance.m_nearPlayers);
            if (__instance.m_nearPlayers.Count != 0)
            {
                DateTime time = ZNet.instance.GetTime();
                if (ValidateSpawners(__instance.m_spawners))
                {
                    __instance.UpdateSpawnList(__instance.m_spawners, time, eventSpawners: false);
                }
                List<SpawnSystem.SpawnData> currentSpawners = RandEventSystem.instance.GetCurrentSpawners();
                if (currentSpawners != null)
                {
                    if (ValidateSpawners(currentSpawners))
                    {
                        __instance.UpdateSpawnList(currentSpawners, time, eventSpawners: true);
                    }
                }
            }

            return false;
        }

        private static bool ValidateSpawners(List<SpawnData> m_spawners)
        {
            if (m_spawners == null)
                return false;

            for (int i = 0; i < m_spawners.Count; i++)
            {
                var spawn = m_spawners[i];
                if (spawn == null)
                {
                    ValheimMP.LogWarning("Null entry in spawner, removing.");
                    m_spawners.RemoveAt(i);
                    i--;
                    continue;
                }

                if(!spawn.m_prefab)
                {
                    ValheimMP.LogWarning("Null m_prefab in spawner, removing.");
                    m_spawners.RemoveAt(i);
                    i--;
                    continue;
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

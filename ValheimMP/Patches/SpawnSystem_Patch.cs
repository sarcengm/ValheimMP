using HarmonyLib;
using System;
using System.Collections.Generic;

namespace ValheimMP.Patches
{

    [HarmonyPatch]
    public class SpawnSystem_Patch
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
        private static bool UpdateSpawning(ref SpawnSystem __instance)
        {
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
    }
}

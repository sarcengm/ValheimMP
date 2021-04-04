using HarmonyLib;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Patches
{


    [HarmonyPatch]
    internal class PlayerProfile_Patch
    {
        [HarmonyPatch(typeof(PlayerProfile), "LoadPlayerDataFromDisk")]
        [HarmonyPrefix]
        private static void LoadPlayerDataFromDisk(ref PlayerProfile __instance, ref ZPackage __result)
        {

        }

        [HarmonyPatch(typeof(PlayerProfile), "SavePlayerToDisk")]
        [HarmonyPrefix]
        private static void SavePlayerToDisk(ref PlayerProfile __instance, ref bool __result)
        {

        }

        public static ZPackage Serialize(PlayerProfile __instance)
        {
            ZPackage zPackage = new ZPackage();
            zPackage.Write(Version.m_playerVersion);
            zPackage.Write(__instance.m_playerStats.m_kills);
            zPackage.Write(__instance.m_playerStats.m_deaths);
            zPackage.Write(__instance.m_playerStats.m_crafts);
            zPackage.Write(__instance.m_playerStats.m_builds);

            if (__instance.m_worldData.ContainsKey(ZNet.instance.GetWorldUID()))
            {
                var item = __instance.m_worldData[ZNet.instance.GetWorldUID()];
                zPackage.Write((int)1);
                zPackage.Write(ZNet.instance.GetWorldUID());
                zPackage.Write(item.m_haveCustomSpawnPoint);
                zPackage.Write(item.m_spawnPoint);
                zPackage.Write(item.m_haveLogoutPoint);
                zPackage.Write(item.m_logoutPoint);
                zPackage.Write(item.m_haveDeathPoint);
                zPackage.Write(item.m_deathPoint);
                zPackage.Write(item.m_homePoint);
                zPackage.Write(item.m_mapData != null);
                if (item.m_mapData != null)
                {
                    zPackage.Write(item.m_mapData);
                }
            }
            else
            {
                zPackage.Write((int)0);
            }

            zPackage.Write(__instance.m_playerName);
            zPackage.Write(__instance.m_playerID);
            zPackage.Write(__instance.m_startSeed);
            if (__instance.m_playerData != null)
            {
                zPackage.Write(data: true);
                zPackage.Write(__instance.m_playerData);
            }
            else
            {
                zPackage.Write(data: false);
            }

            ZPackage pkg = zPackage.Compress();

            ValheimMP.Log("Serialized package for " + __instance.m_playerName);
            return pkg;
        }

        public static void Deserialize(PlayerProfile __instance, ZPackage pkg)
        {
            ZPackage zPackage = pkg.Decompress();

            int num = zPackage.ReadInt();
            if (!Version.IsPlayerVersionCompatible(num))
            {
                ValheimMP.Log("Player data is not compatible, ignoring");
                return;
            }

            if (num >= 28)
            {
                __instance.m_playerStats.m_kills = zPackage.ReadInt();
                __instance.m_playerStats.m_deaths = zPackage.ReadInt();
                __instance.m_playerStats.m_crafts = zPackage.ReadInt();
                __instance.m_playerStats.m_builds = zPackage.ReadInt();
            }
            __instance.m_worldData.Clear();
            int num2 = zPackage.ReadInt();
            for (int i = 0; i < num2; i++)
            {
                long key = zPackage.ReadLong();
                var worldPlayerData = new PlayerProfile.WorldPlayerData();
                worldPlayerData.m_haveCustomSpawnPoint = zPackage.ReadBool();
                worldPlayerData.m_spawnPoint = zPackage.ReadVector3();
                worldPlayerData.m_haveLogoutPoint = zPackage.ReadBool();
                worldPlayerData.m_logoutPoint = zPackage.ReadVector3();
                if (num >= 30)
                {
                    worldPlayerData.m_haveDeathPoint = zPackage.ReadBool();
                    worldPlayerData.m_deathPoint = zPackage.ReadVector3();
                }
                worldPlayerData.m_homePoint = zPackage.ReadVector3();
                if (num >= 29 && zPackage.ReadBool())
                {
                    worldPlayerData.m_mapData = zPackage.ReadByteArray();
                }
                __instance.m_worldData.Add(key, worldPlayerData);
            }
            __instance.m_playerName = zPackage.ReadString();
            __instance.m_playerID = zPackage.ReadLong();
            __instance.m_startSeed = zPackage.ReadString();
            if (zPackage.ReadBool())
            {
                __instance.m_playerData = zPackage.ReadByteArray();
            }
            else
            {
                __instance.m_playerData = null;
            }

            return;
        }


    }

}

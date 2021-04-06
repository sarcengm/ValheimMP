using HarmonyLib;
using UnityEngine;

namespace ValheimMP.Patches
{


    [HarmonyPatch]
    internal class Game_Patch
    {
        [HarmonyPatch(typeof(Game), "Start")]
        [HarmonyPostfix]
        private static void Start()
        {
            ZRoutedRpc.instance.Register("RequestRespawn", RPC_RequestRespawn);
        }

        [HarmonyPatch(typeof(Game), "FixedUpdate")]
        [HarmonyPrefix]
        private static bool FixedUpdate(ref Game __instance)
        {
            if (!ZNet.instance.IsServer())
            {
                return true;
            }
            __instance.UpdateRespawn(Time.fixedDeltaTime);
            return false;
        }

        [HarmonyPatch(typeof(Game), "RequestRespawn")]
        [HarmonyPrefix]
        private static bool RequestRespawn(Game __instance, float delay)
        {
            if(ValheimMP.IsDedicated)
                return false;
            return true;
        }

        [HarmonyPatch(typeof(Game), "_RequestRespawn")]
        [HarmonyPrefix]
        private static bool _RequestRespawn(Game __instance)
        {
            if (ValheimMP.IsDedicated)
                return false;

            if (Player.m_localPlayer != null)
                ZNetScene.instance.Destroy(Player.m_localPlayer.gameObject);
            Player.m_localPlayer = null;
            ZNet.instance.SetCharacterID(ZDOID.None);
            

            ZRoutedRpc.instance.InvokeRoutedRPC(ZNet.instance.GetServerPeer().m_uid, "RequestRespawn");
            MusicMan.instance.TriggerMusic("respawn");
            return false;
        }

        private static void RPC_RequestRespawn(long sender)
        {
            var peer = ZNet.instance.GetPeer(sender);
            if (peer == null)
                return;

            var player = peer.m_player;
            if (player == null)
                return;
            
            RequestRespawn(player, peer);
        }

        public static void SaveWorld(bool force = false)
        {
            if (Game.instance.m_saveTimer > 120f || force)
            {
                Game.instance.m_saveTimer = 0f;
                ZNet.instance.Save(sync: false);
                
            }
        }

        [HarmonyPatch(typeof(Game), "UpdateRespawn")]
        [HarmonyPrefix]
        private static bool UpdateRespawn(ref Game __instance, float dt)
        {
            foreach (var peer in ZNet.instance.GetPeers())
            {
                peer.m_respawnWait -= dt;

                if (peer.m_requestRespawn)
                {
                    if (peer.m_respawnWait > 0.0)
                    {
                        continue;
                    }

                    if (FindSpawnPoint(ref __instance, peer, out var point, out var usedLogoutPoint, dt))
                    {
                        if (!usedLogoutPoint)
                        {
                            peer.m_playerProfile.SetHomePoint(point);
                        }

                        if (peer.m_player != null)
                        {
                            ZNetScene.instance.Destroy(peer.m_player.gameObject);
                        }

                        SpawnPlayer(ref __instance, peer, point);
                        peer.m_requestRespawn = false;

                        peer.m_rpc.Invoke("RefPos", peer.m_player.transform.position, false);

                        if (peer.m_firstSpawn)
                        {
                            peer.m_firstSpawn = false;
                            //peer.m_rpc.Invoke("PlayerProfile", PlayerProfile_Patch.Serialize(peer.m_playerProfile));
                            peer.m_rpc.Invoke("CharacterID", peer.m_player.GetZDOID());
                        }
                        else
                        {
                            peer.m_rpc.Invoke("CharacterID", peer.m_player.GetZDOID());
                        }
                    }
                }
                else if (peer.m_player != null && !peer.m_player.IsDead() && !peer.m_player.IsTeleporting())
                {
                    peer.m_refPos = peer.m_player.transform.position;
                }
            }

            return false;
        }

        private static bool FindSpawnPoint(ref Game __instance, ZNetPeer peer, out Vector3 point, out bool usedLogoutPoint, float dt)
        {
            usedLogoutPoint = false;
            if (peer.m_player == null && peer.m_playerProfile.HaveLogoutPoint())
            {
                Vector3 logoutPoint = peer.m_playerProfile.GetLogoutPoint();
                peer.m_refPos = logoutPoint;
                if (ZNetScene.instance.IsAreaReady(logoutPoint))
                {
                    if (!ZoneSystem.instance.GetGroundHeight(logoutPoint, out var height))
                    {
                        ValheimMP.Log("Invalid spawn point, no ground " + logoutPoint);
                        peer.m_respawnWait = 0f;
                        peer.m_playerProfile.ClearLoguoutPoint();
                        point = Vector3.zero;
                        return false;
                    }

                    point = logoutPoint;
                    if (point.y < height)
                    {
                        point.y = height;
                    }
                    point.y += 0.25f;
                    usedLogoutPoint = true;
                    ValheimMP.Log("Spawned after " + peer.m_respawnWait);
                    return true;
                }
                point = Vector3.zero;
                return false;
            }
            if (peer.m_playerProfile.HaveCustomSpawnPoint())
            {
                Vector3 customSpawnPoint = peer.m_playerProfile.GetCustomSpawnPoint();
                peer.m_refPos = customSpawnPoint;
                if (ZNetScene.instance.IsAreaReady(customSpawnPoint))
                {
                    point = customSpawnPoint;
                    return true;
                }
                point = Vector3.zero;
                return false;
            }
            if (ZoneSystem.instance.GetLocationIcon(__instance.m_StartLocation, out var pos))
            {
                point = pos + Vector3.up * 2f;
                peer.m_refPos = point;
                return ZNetScene.instance.IsAreaReady(point);
            }
            peer.m_refPos = Vector3.zero;
            point = Vector3.zero;
            return false;
        }

        private static void SpawnPlayer(ref Game __instance, ZNetPeer peer, Vector3 spawnPoint)
        {
            ValheimMP.Log($"Spawning player for peer: {peer.m_uid}");
            Player player = UnityEngine.Object.Instantiate(__instance.m_playerPrefab, spawnPoint, Quaternion.identity).GetComponent<Player>();

            Player.m_localPlayer = null;
            peer.m_player = player;
            peer.m_characterID = player.GetZDOID();
            player.SetPlayerID(peer.m_uid, peer.m_playerName);
            peer.m_playerProfile.LoadPlayerData(player);
            peer.m_refPos = player.transform.position;
            player.m_nview.GetZDO().SetOwner(peer.m_uid);
            //player.m_nview.GetZDO().SetOwner(1234);
            ZDOMan.instance.ForceSendZDO(player.GetZDOID());
            // TODO: OnSpawned basically only does the first spawn fly in, no priority, fix later, if ever.
            //player.OnSpawned();
        }

        internal static void RequestRespawn(Player __instance, ZNetPeer peer)
        {
            if(__instance.IsDead() && peer.m_requestRespawn == false)
            {
                peer.m_requestRespawn = true;
            }
        }

        [HarmonyPatch(typeof(Game), "SpawnPlayer")]
        [HarmonyPrefix]
        private static bool SpawnPlayer(ref Game __instance, ref Player __result, Vector3 spawnPoint)
        {
            return false;
        }

        [HarmonyPatch(typeof(Game), "SavePlayerProfile")]
        [HarmonyPrefix]
        private static bool SavePlayerProfile(ref Game __instance, bool setLogoutPoint)
        {
            if (!ValheimMP.IsDedicated)
            {
                

                if ((bool)Player.m_localPlayer)
                {
                    __instance.m_playerProfile.SavePlayerData(Player.m_localPlayer);
                    Minimap.instance.SaveMapData();
                    if (setLogoutPoint)
                    {
                        __instance.m_playerProfile.SaveLogoutPoint();
                    }
                }

                string serverIdentifier;

                if (ZNet.m_serverSteamID == 0) 
                {
                    ZNet.m_serverIPAddr.ToString(out serverIdentifier, bWithPort: true);
                }
                else 
                {
                    serverIdentifier = ZNet.m_serverSteamID.ToString();
                }

                //var serverName = ZNet.m_ServerName;
                __instance.m_playerProfile.m_playerName = serverIdentifier;
                __instance.m_playerProfile.m_filename = serverIdentifier.Replace(":","_");
                __instance.m_playerProfile.Save();

                return false;
            }

            foreach (var peer in ZNet.instance.GetPeers())
            {
                ZNetPeer_Patch.SavePeer(peer, false);
            }

            return false;
        }

        [HarmonyPatch(typeof(Game), "OnApplicationQuit")]
        [HarmonyPrefix]
        private static void OnApplicationQuit()
        {
            ValheimMP.Instance?.WriteDebugData();
        }
    }
}

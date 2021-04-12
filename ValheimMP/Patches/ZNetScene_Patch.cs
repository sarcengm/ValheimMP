using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using ValheimMP.Framework;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class ZNetScene_Patch
    {
        internal static bool m_isStillLoading = false;
        [HarmonyPatch(typeof(ZNetScene), "CreateObjects")]
        [HarmonyPrefix]
        private static bool CreateObjects(ZNetScene __instance, List<ZDO> currentNearObjects, List<ZDO> currentDistantObjects)
        {
            int maxCreatedPerFrame = 10;

            if (Player.m_localPlayer == null ||
                (Player.m_localPlayer != null && (Player.m_localPlayer.IsTeleporting() || Hud.instance.m_loadingScreen.isActiveAndEnabled)) ||
                m_isStillLoading)
            {
                maxCreatedPerFrame = 1000;
            }

            int frameCount = Time.frameCount;
            foreach (ZDO key in __instance.m_instances.Keys)
            {
                key.m_tempCreateEarmark = frameCount;
            }
            int created = 0;
            __instance.CreateObjectsSorted(currentNearObjects, maxCreatedPerFrame, ref created);
            __instance.CreateDistantObjects(currentDistantObjects, maxCreatedPerFrame, ref created);
            m_isStillLoading = created > 500;
            return false;
        }

        [HarmonyPatch(typeof(ZNetScene), "RemoveObjects")]
        [HarmonyPrefix]
        private static void RemoveObjects(ZNetScene __instance, List<ZDO> currentNearObjects, List<ZDO> currentDistantObjects)
        {
            if (ZNet.instance != null && !ZNet.instance.IsServer() && Player.m_localPlayer != null)
            {
                // tag the local player so we do not get removed by remove objects.
                // normally a player can not go out of the area of a owning player
                // but since the server can control it and teleport it elsewhere it may be out of the
                // owners zone.
                Player.m_localPlayer.m_nview.m_zdo.m_tempRemoveEarmark = Time.frameCount;
            }
        }

        [HarmonyPatch(typeof(ZNetScene), "CreateDestroyObjects")]
        [HarmonyPrefix]
        private static bool CreateDestroyObjects(ZNetScene __instance)
        {
            if (!ZNet.instance.IsServer())
            {
                return true;
            }

            sw.Restart();
            LoadSectors(__instance);
            if (Time.time - m_lastSectorCheck < m_sectorCheckFrequency)
            {
                return false;
            }

            m_lastSectorCheck = Time.time;
            sw.Restart();
            AddPeersToSectors(ZoneSystem.instance.m_activeArea, ZoneSystem.instance.m_activeDistantArea);
            sw.Restart();
            CheckActiveSectors();
            sw.Restart();
            UnloadSectors(__instance);
            return false;
        }

        private static List<LivingSectorObjects> m_pendingLoadSectors = new List<LivingSectorObjects>();
        private static List<LivingSectorObjects> m_pendingUnloadSectors = new List<LivingSectorObjects>();

        private static int m_maxCreatedPerFrame = 100;
        private static int m_createdThisFrame = 0;

        // keeping things loaded could be nice... especially if you run in and out of sectors constantly, but..
        // things start falling out of the world, it seems that the map generation that is still seperate will need
        // to be attached to this loading\unloading of sectors.
        private static float m_sectorStayLoadedTime = 60f;

        private static float m_lastSectorCheck;
        private static float m_sectorCheckFrequency = 1f;

        internal static HashSet<Vector2i> m_fullyLoadedSectors = new HashSet<Vector2i>();

        static Stopwatch sw = new Stopwatch();

        private static void CheckActiveSectors()
        {
            var sectors = LivingSectorObjects.GetSectors();

            for (int i = 0; i < sectors.Count; i++)
            {
                var sector = sectors[i];

                var peerList = sector.ActivePeers.ToList();
                for (int j = 0; j < peerList.Count; j++)
                {
                    var peerId = peerList[j];
                    var peer = ZNet.instance.GetPeer(peerList[j]);
                    if (peer == null || peer.m_socket == null || !peer.m_socket.IsConnected())
                    {
                        sector.ActivePeers.Remove(peerId);
                    }
                }

                if (sector.ActivePeers.Count == 0 && !sector.IsFrozen)
                {
                    sector.FreezeSector();
                    m_pendingUnloadSectors.Add(sector);
                }
            }
        }

        private static void UnloadSectors(ZNetScene ns)
        {
            var zdoman = ZDOMan.instance;
            for (int s = 0; s < m_pendingUnloadSectors.Count; s++)
            {
                var obj = m_pendingUnloadSectors[s];

                if (obj == null || obj.ActivePeers.Count > 0)
                {
                    // Sector still (again?) has peers, dont remove!
                    m_pendingUnloadSectors.RemoveAt(s);
                    s--;
                    continue;
                }

                if (Time.realtimeSinceStartup - obj.LastActive < m_sectorStayLoadedTime)
                    continue;

                //ValheimMP.Log($"Start unloaded sector {obj.x}, {obj.y}");

                var list = new List<ZDO>();
                list.AddRange(obj.SolidObjectsList);
                list.AddRange(obj.PriorityObjectsList);
                list.AddRange(obj.DefaultObjectsList);
                list.AddRange(obj.NonNetworkedObjectsList);
                obj.SolidObjectsList.Clear();
                obj.PriorityObjectsList.Clear();
                obj.DefaultObjectsList.Clear();
                obj.NonNetworkedObjectsList.Clear();

                for (int i = 0; i < list.Count; i++)
                {
                    var zdo = list[i];
                    DestroyZDO(ns, zdoman, zdo);
                }
                //ValheimMP.Log($"Unloaded sector {obj.x}, {obj.y} destroyed {list.Count} objects.");

                LivingSectorObjects.RemoveSector(obj);
                m_pendingUnloadSectors.RemoveAt(s);
                m_fullyLoadedSectors.Remove(new Vector2i(obj.x, obj.y));

                //ValheimMP.Log($"Unloaded sector {obj.x}, {obj.y} in {sw.GetElapsedMilliseconds()}ms", sw.GetElapsedMilliseconds() > 10f ? BepInEx.Logging.LogLevel.Warning : BepInEx.Logging.LogLevel.Info);
                s--;
                break;
            }
        }

        private static void DestroyZDO(ZNetScene netscene, ZDOMan zdoman, ZDO zdo)
        {
            if (zdo == null || netscene == null || zdoman == null || !zdo.m_nview)
            {
                ValheimMP.Log($"DestroyZDO NullSomething zdo: {zdo} netscene: {netscene} zdoman:{zdo} zdoman:{zdo.m_nview}");
                return;
            }
            // Since we clear the lists manually, we should also reset this manually.
            zdo.m_listIndex = -1;

            if (zdo.m_nview)
            {
                if (zdo.m_nview.gameObject)
                {
                    UnityEngine.Object.Destroy(zdo.m_nview.gameObject);
                }
                else
                {
                    ValheimMP.Log($"ZDO Missing gameobj {zdo.m_nview}");
                }
                zdo.m_nview.ResetZDO();
                zdo.m_nview = null;
            }
            else
            {
                var objb = ZNetScene.instance.GetPrefab(zdo.m_prefab);
                ValheimMP.Log($"ZDO Missing nview {objb}");
            }

            netscene.m_instances.Remove(zdo);
            if (!zdo.m_persistent)
            {
                zdoman.DestroyZDO(zdo);
            }
        }

        private static void LoadSectors(ZNetScene ns)
        {
            m_createdThisFrame = 0;

            while (m_pendingLoadSectors.Count > 0)
            {
                var obj = m_pendingLoadSectors[0];

                if (obj.ActivePeers.Count == 0)
                {
                    m_pendingLoadSectors.RemoveAt(0);
                    obj.PendingLoad = false;
                    continue;
                }


                if (!obj.PendingObjectsLoaded)
                {
                    // this is more than heavy enough to only do this during a frame, so fill the list and do the rest next frame!
                    ZDOMan.instance.FindObjects(new Vector2i(obj.x, obj.y), obj.PendingObjects);
                    obj.PendingObjectsLoaded = true;
                    //ValheimMP.Log($"LoadSectors (FindObjects): {sw.GetElapsedMilliseconds()}", sw.GetElapsedMilliseconds() > 10f ? BepInEx.Logging.LogLevel.Warning : BepInEx.Logging.LogLevel.Info);
                    return;
                }

                var list = obj.PendingObjects;

                while (list.Count > 0)
                {
                    var item = list[0];
                    list.RemoveAt(0);

                    // this one has already been instantiated!
                    // Likely because it was already in this sector before the player arrived
                    // Either because it moved there, or... well idk?
                    if (item.m_nview)
                        continue;

                    if (ns.CreateObject(item) != null)
                    {
                        m_createdThisFrame++;
                        if (m_createdThisFrame >= m_maxCreatedPerFrame || sw.GetElapsedMilliseconds() > 5f)
                        {
                            //ValheimMP.Log($"LoadSectors (Create Limit): {m_createdThisFrame} {sw.GetElapsedMilliseconds()}", sw.GetElapsedMilliseconds() > 10f ? BepInEx.Logging.LogLevel.Warning : BepInEx.Logging.LogLevel.Info);
                            return;
                        }
                    }
                    else
                    {
                        item.SetOwner(ZDOMan.instance.GetMyID());
                        ZLog.Log("Destroyed invalid predab ZDO:" + item.m_uid);
                        ZDOMan.instance.DestroyZDO(item);
                    }
                }

                if (list.Count == 0)
                {
                    //ValheimMP.Log($"Loaded sector {obj.x}, {obj.y}");
                    m_pendingLoadSectors.RemoveAt(0);
                    m_fullyLoadedSectors.Add(new Vector2i(obj.x, obj.y));
                }

                if (m_createdThisFrame >= m_maxCreatedPerFrame)
                {
                    break;
                }
            }
        }


        private static void AddPeersToSectors(int area, int distantArea)
        {
            var peers = ZNet.instance.m_peers;
            for (int p = 0; p < peers.Count; p++)
            {
                var peer = peers[p];
                if (!ValheimMP.IsDedicated)
                    peer.m_refPos = ZNet.instance.GetReferencePosition();
                if (peer.m_player && !peer.m_player.IsDead())
                    peer.m_refPos = peer.m_player.transform.position;

                if (peer.m_refPos == Vector3.zero)
                    continue;

                Vector2i sector = ZoneSystem.instance.GetZone(peer.m_refPos);
                if (sector == peer.m_lastSector)
                    continue;

                var oldsector = peer.m_lastSector;
                RemovePeerFromSector(peer, oldsector.x, oldsector.y);

                for (int i = 1; i <= area; i++)
                {
                    for (int x = oldsector.x - i; x <= oldsector.x + i; x++)
                    {
                        RemovePeerFromSector(peer, x, oldsector.y - i);
                        RemovePeerFromSector(peer, x, oldsector.y + i);
                    }
                    for (int y = oldsector.y - i + 1; y <= oldsector.y + i - 1; y++)
                    {
                        RemovePeerFromSector(peer, oldsector.x - i, y);
                        RemovePeerFromSector(peer, oldsector.x + i, y);
                    }
                }

                peer.m_lastSector = sector;
                AddPeerToSector(peer, sector.x, sector.y);
                for (int i = 1; i <= area; i++)
                {
                    for (int x = sector.x - i; x <= sector.x + i; x++)
                    {
                        AddPeerToSector(peer, x, sector.y - i);
                        AddPeerToSector(peer, x, sector.y + i);
                    }
                    for (int y = sector.y - i + 1; y <= sector.y + i - 1; y++)
                    {
                        AddPeerToSector(peer, sector.x - i, y);
                        AddPeerToSector(peer, sector.x + i, y);
                    }
                }
            }
        }

        private static void AddPeerToSector(ZNetPeer peer, int x, int y)
        {
            var sectorObj = LivingSectorObjects.GetObjectOrCreate(x, y);
            sectorObj.ActivePeers.Add(peer.m_uid);

            if (!sectorObj.PendingLoad)
            {
                sectorObj.PendingLoad = true;
                if (peer.m_lastSector.x == x && peer.m_lastSector.y == y)
                {
                    // sector that hasnt being loaded is being stepped on by this peer
                    // add it to the start, so it gets loaded first
                    m_pendingLoadSectors.Insert(0, sectorObj);
                }
                else
                {
                    m_pendingLoadSectors.Add(sectorObj);
                }
            }

            if (sectorObj.IsFrozen)
            {
                sectorObj.UnfreezeSector();
            }
        }

        private static void RemovePeerFromSector(ZNetPeer peer, int x, int y)
        {
            LivingSectorObjects sectorObj = LivingSectorObjects.GetObject(x, y);
            if (sectorObj != null)
            {
                sectorObj.ActivePeers.Remove(peer.m_uid);
            }
        }

        [HarmonyPatch(typeof(ZNetScene), "OutsideActiveArea", new Type[] { typeof(Vector3) })]
        [HarmonyPrefix]
        private static bool OutsideActiveArea(ref ZNetScene __instance, ref bool __result, Vector3 point)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer())
                return true;

            var players = Player.GetAllPlayers();
            foreach (var player in players)
            {
                if (!__instance.OutsideActiveArea(point, player.transform.position))
                {
                    __result = false;
                    return false;
                }
            }

            __result = true;
            return false;
        }


        [HarmonyPatch(typeof(ZNetScene), "RPC_SpawnObject")]
        [HarmonyPrefix]
        private static bool RPC_SpawnObject(ref ZNetScene __instance, long spawner, Vector3 pos, Quaternion rot, int prefabHash)
        {
            // TODO: This function seems only used by FX that are created when stuff is put in the fireplace, fix the way it spawns there.
            return ZNet_Patch.IsRPCAllowed(__instance, spawner);
        }

    }
}

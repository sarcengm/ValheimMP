using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ValheimMP.Framework.Extensions;
using ValheimMP.Framework;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class ZDOMan_Patch
    {
        [HarmonyPatch(typeof(ZDOMan), MethodType.Constructor, new[] { typeof(int) })]
        [HarmonyPostfix]
        private static void Constructor(ref ZDOMan __instance)
        {
            if (ZNet.instance.IsDedicated())
            {
                __instance.m_myid = ValheimMP.ServerUID;
            }
        }

        [HarmonyPatch(typeof(ZDOMan), "RPC_DestroyZDO")]
        [HarmonyPrefix]
        private static bool RPC_DestroyZDO(ref ZDOMan __instance, long sender, ZPackage pkg)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }

        [HarmonyPatch(typeof(ZDOMan), "DestroyZDO", new Type[] { typeof(ZDO) })]
        [HarmonyPrefix]
        private static bool DestroyZDO(ref ZDOMan __instance, ZDO zdo)
        {
            if (zdo == null)
                return false;

            if (ValheimMP.IsDedicated)
            {
                __instance.m_destroySendList.Add(zdo.m_uid);
            }
            // if we are owner of an object just destroy it right away, this is only a local action.
            if (zdo.IsOwnerOrServer())
            {
                __instance.HandleDestroyedZDO(zdo.m_uid);
            }
            return false;
        }

        [HarmonyPatch(typeof(ZDOMan), "SendDestroyed")]
        [HarmonyPrefix]
        private static bool SendDestroyed()
        {
            return false;
        }


        [HarmonyPatch(typeof(ZDOMan), "HandleDestroyedZDO")]
        [HarmonyPrefix]
        private static bool HandleDestroyedZDO(ZDOMan __instance, ZDOID uid)
        {
            if (uid.userID == __instance.m_myid && uid.id >= __instance.m_nextUid)
            {
                __instance.m_nextUid = uid.id + 1;
            }
            ZDO zdo = __instance.GetZDO(uid);
            if (zdo == null)
            {
                return false;
            }

            if (__instance.m_onZDODestroyed != null)
            {
                __instance.m_onZDODestroyed(zdo);
            }

            SectorManager.RemoveObject(zdo);
            __instance.RemoveFromSector(zdo, zdo.GetSector());
            __instance.m_objectsByID.Remove(zdo.m_uid);
            ZDOPool.Release(zdo);

            for (int i = 0; i < __instance.m_peers.Count; i++)
            {
                var peer = __instance.m_peers[i];
                peer.m_zdos.Remove(uid);
            }
            return false;
        }

        [HarmonyPatch(typeof(ZDOMan), "RPC_RequestZDO")]
        [HarmonyPrefix]
        private static bool RPC_RequestZDO(ref ZDOMan __instance, long sender, ZDOID id)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }

        [HarmonyPatch(typeof(ZDOMan), "Update")]
        [HarmonyPrefix]
        private static bool Update(ref ZDOMan __instance, float dt)
        {
            //if (!ZNet.instance.IsServer())
            //    return false;

            return true;
        }

        [HarmonyPatch(typeof(ZDOMan), "ReleaseZDOS")]
        [HarmonyPrefix]
        private static bool ReleaseZDOS(ref ZDOMan __instance, float dt)
        {
            return false;
        }

        [HarmonyPatch(typeof(ZDOMan), "CreateSyncList")]
        [HarmonyPrefix]
        private static bool CreateSyncList(ref ZDOMan __instance, ZDOMan.ZDOPeer peer, List<ZDO> toSync)
        {
            // return false when not server, because a client doesn't need to ever send ZDO's to the server!
            if (!ZNet.instance.IsServer())
                return false;

            if (peer.m_peer.m_refPos == Vector3.zero)
                return false;

            // use refPos not player position because preloading terrain needs to happen before players are spawned!
            Vector2i zone = ZoneSystem.instance.GetZone(peer.m_peer.m_refPos);

            var distantObjects = new List<ZDO>();

            peer.m_peer.m_loadedSectorsTouch++;

            if (peer.m_peer.m_player != null)
            {
                toSync.Add(peer.m_peer.m_player.m_nview.m_zdo);
            }

            FindLiveSectorObjects(peer.m_peer, zone, ZoneSystem.instance.m_activeArea, ZoneSystem.instance.m_activeDistantArea, toSync, distantObjects, peer.m_peer.m_solidObjectQueue);


            // Loaded sectors are the ones that are actively loaded, so any sector that hasn't been touched should be removed.
            foreach (var s in peer.m_peer.m_loadedSectors.Keys.ToList())
            {
                if (peer.m_peer.m_loadedSectors[s].Key != peer.m_peer.m_loadedSectorsTouch)
                {
                    peer.m_peer.m_loadedSectors.Remove(s);
                }
            }


            __instance.AddForceSendZdos(peer, toSync);

            // solid objects should probably be added before distant ones
            // NOTE: altough alternatively I thought it would be good to alternate sending important ZDOs and solid objects,
            // because if you have too many important ZDOs solid ones might never get their turn
            // Immediate playing area > forced zdo > solid > distant
            toSync.AddRange(peer.m_peer.m_solidObjectQueue.Values.OrderBy(k =>
                    (peer.m_peer.m_refPos.x - k.m_position.x) * (peer.m_peer.m_refPos.x - k.m_position.x) +
                    (peer.m_peer.m_refPos.y - k.m_position.y) * (peer.m_peer.m_refPos.y - k.m_position.y)));

            //toSync.OrderByDescending(k => peer.m_zdos.ContainsKey(k.m_uid) ? peer.m_zdos[k.m_uid].m_syncTime : 0);

            toSync.AddRange(distantObjects);
            return false;
        }

        private static void FindLiveSectorObjects(ZNetPeer peer, Vector2i sector, int area, int distantArea, List<ZDO> objects, List<ZDO> distantObjects, Dictionary<ZDOID, ZDO> solidObjects)
        {
            FindLiveObjects(peer, new Vector2i(sector.x, sector.y), objects, solidObjects, false, true);

            var targetSectors = new List<Vector2i>();

            for (int i = 1; i <= area; i++)
            {
                for (int j = sector.x - i; j <= sector.x + i; j++)
                {
                    targetSectors.Add(new Vector2i(j, sector.y - i));
                    targetSectors.Add(new Vector2i(j, sector.y + i));
                }
                for (int k = sector.y - i + 1; k <= sector.y + i - 1; k++)
                {
                    targetSectors.Add(new Vector2i(sector.x - i, k));
                    targetSectors.Add(new Vector2i(sector.x + i, k));
                }
            }

            var zoneSize = ZoneSystem.instance.m_zoneSize;
            var halfSize = zoneSize * 0.5f;

            var sortedSectors = targetSectors.OrderBy(k =>
            {
                var x = zoneSize * k.x + halfSize;
                var y = zoneSize * k.y + halfSize;
                return (peer.m_refPos.x - x) * (peer.m_refPos.x - x) + (peer.m_refPos.y - y) * (peer.m_refPos.y - y);
            });

            // we sort the 3 nearest sectors (well four if we include the one we are standing on!)
            // the rest are distant, so we shouldnt waste any time sorting them, it's not like anyone will be there to notice!
            var sortedCount = 0;
            foreach (var s in sortedSectors)
            {
                FindLiveObjects(peer, s, objects, solidObjects, false, sortedCount++ < 3);
            }


            for (int l = area + 1; l <= area + distantArea; l++)
            {
                for (int m = sector.x - l; m <= sector.x + l; m++)
                {
                    FindLiveObjects(peer, new Vector2i(m, sector.y - l), distantObjects, solidObjects, true);
                    FindLiveObjects(peer, new Vector2i(m, sector.y + l), distantObjects, solidObjects, true);
                }
                for (int n = sector.y - l + 1; n <= sector.y + l - 1; n++)
                {
                    FindLiveObjects(peer, new Vector2i(sector.x - l, n), distantObjects, solidObjects, true);
                    FindLiveObjects(peer, new Vector2i(sector.x + l, n), distantObjects, solidObjects, true);
                }
            }
        }

        private static void FindLiveObjects(ZNetPeer peer, Vector2i sector, List<ZDO> objects, Dictionary<ZDOID, ZDO> solidObjects, bool distant = false, bool sortArea = false)
        {
            var obj = SectorManager.GetObject(sector.x, sector.y);
            if (obj != null)
            {
                if (!distant)
                {
                    if (sortArea)
                    {
                        objects.AddRange(obj.PriorityObjectsList
                                            .OrderBy(k =>
                                                (peer.m_refPos.x - k.m_position.x) * (peer.m_refPos.x - k.m_position.x) +
                                                (peer.m_refPos.y - k.m_position.y) * (peer.m_refPos.y - k.m_position.y)));

                        objects.AddRange(obj.DefaultObjectsList
                                            .OrderBy(k =>
                                                (peer.m_refPos.x - k.m_position.x) * (peer.m_refPos.x - k.m_position.x) +
                                                (peer.m_refPos.y - k.m_position.y) * (peer.m_refPos.y - k.m_position.y)));
                    }
                    else
                    {
                        objects.AddRange(obj.PriorityObjectsList);
                        objects.AddRange(obj.DefaultObjectsList);
                    }

                    var addSolidObjects = true;
                    if (peer.m_loadedSectors.TryGetValue(sector, out var val))
                    {
                        addSolidObjects = val.Value || peer.m_loadedSectorsTouch - 1 != val.Key;
                    }
                    if (addSolidObjects)
                    {
                        obj.SolidObjectsList.Do(k => solidObjects[k.m_uid] = k);
                    }
                    peer.m_loadedSectors[sector] = new KeyValuePair<int, bool>(peer.m_loadedSectorsTouch, false);
                }
                else
                {
                    objects.AddRange(obj.PriorityObjectsList.Where(k => k.m_distant));
                    objects.AddRange(obj.DefaultObjectsList.Where(k => k.m_distant));

                    var addSolidObjects = true;
                    if (peer.m_loadedSectors.TryGetValue(sector, out var val))
                    {
                        addSolidObjects = peer.m_loadedSectorsTouch - 1 != val.Key;
                    }
                    if (addSolidObjects)
                    {
                        obj.SolidObjectsList.DoIf(k => k.m_distant, k => solidObjects[k.m_uid] = k);
                    }
                    peer.m_loadedSectors[sector] = new KeyValuePair<int, bool>(peer.m_loadedSectorsTouch, true);
                }
            }
        }

        [HarmonyPatch(typeof(ZDOMan), "SendZDOToPeers")]
        [HarmonyPrefix]
        private static bool SendZDOToPeers(ZDOMan __instance, float dt)
        {
            if (!ValheimMP.IsDedicated)
                return false;

            __instance.m_sendTimer += dt;
            if (__instance.m_sendTimer < 0.05f)
            {
                return false;
            }

            __instance.m_sendTimer = 0f;
            foreach (ZDOMan.ZDOPeer peer in __instance.m_peers)
            {
                SendUpdateZDO(__instance, peer);
                SendDestroyZDO(__instance, peer);
            }
            __instance.m_destroySendList.Clear();
            return false;
        }

        [HarmonyPatch(typeof(ZDOMan), "RemovePeer")]
        [HarmonyPrefix]
        private static void RemovePeer(ZDOMan __instance, ZNetPeer netPeer)
        {
            ZDOMan.ZDOPeer zDOPeer = __instance.FindPeer(netPeer);
            if (zDOPeer != null)
            {
                ValheimMP.Instance.Internal_OnPlayerOffline(netPeer);
                __instance.m_peers.Remove(zDOPeer);
                if (ZNet.instance.IsServer() && netPeer.m_player && netPeer.m_player.m_nview && netPeer.m_player.m_nview.m_zdo != null)
                {
                    __instance.DestroyZDO(netPeer.m_player.m_nview.m_zdo);
                }
            }
        }

        private static void SendDestroyZDO(ZDOMan __instance, ZDOMan.ZDOPeer peer)
        {
            if (__instance.m_destroySendList.Count > 0)
            {
                var pkg = new ZPackage();
                for (int i = 0; i < __instance.m_destroySendList.Count; i++)
                {
                    var zdoid = __instance.m_destroySendList[i];
                    if (peer.m_zdoImage.Remove(zdoid))
                    {
                        pkg.Write(zdoid);
                    }
                }

                if (pkg.Size() > 0)
                {
                    peer.m_peer.m_rpc.Invoke("ZDODestroy", pkg);
                }
            }
        }
        internal static void RPC_ZDODestroy(ZPackage pkg)
        {
            ZDOMan man = ZDOMan.instance;
            while (pkg.GetPos() < pkg.Size())
            {
                man.HandleDestroyedZDO(pkg.ReadZDOID());
            }
        }


        [Flags]
        public enum ZDOFlags : int
        {
            none = 0,
            m_uid = 1,
            m_owner = 1 << 1,
            m_position = 1 << 2,
            m_persistent = 1 << 3,
            m_distant = 1 << 4,
            m_timeCreated = 1 << 5,
            m_pgwVersion = 1 << 6,
            m_prefab = 1 << 7,
            m_rotation = 1 << 8,
            m_type = 1 << 9,
            m_floats = 1 << 10,
            m_vec3 = 1 << 11,
            m_quats = 1 << 12,
            m_ints = 1 << 13,
            m_strings = 1 << 14,
            m_longs = 1 << 15,
            m_byteArrays = 1 << 16,


            all = ZDOFlags.m_uid | ZDOFlags.m_owner | ZDOFlags.m_position | ZDOFlags.m_persistent | ZDOFlags.m_distant |
            ZDOFlags.m_timeCreated | ZDOFlags.m_pgwVersion | ZDOFlags.m_prefab | ZDOFlags.m_rotation | ZDOFlags.m_type |
            ZDOFlags.m_floats | ZDOFlags.m_vec3 | ZDOFlags.m_quats | ZDOFlags.m_ints | ZDOFlags.m_strings | ZDOFlags.m_longs | m_byteArrays,

            invalid = ~all
        }

        //static Stopwatch sw = new Stopwatch();

        //[HarmonyPatch(typeof(ZDOMan), "SendZDOs")]
        //[HarmonyPrefix]
        //static void SendZDOsPre()
        //{
        //    sw.Reset();
        //    sw.Start();
        //}

        //[HarmonyPatch(typeof(ZDOMan), "SendZDOs")]
        //[HarmonyPostfix]
        //static void SendZDOsPost()
        //{
        //    sw.Stop();
        //    long microseconds = sw.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L));
        //    ValheimMP.Log("SendZDOs in " + (float)microseconds / 1000f + "ms");
        //}

        private static void SendUpdateZDO(ZDOMan __instance, ZDOMan.ZDOPeer peer)
        {
            __instance.m_tempToSync.Clear();
            __instance.CreateSyncList(peer, __instance.m_tempToSync);

            //long microseconds = sw.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L));
            //ValheimMP.Log("CreateSyncList " + __instance.m_tempToSync.Count + " candidates (" + peer.m_peer?.m_solidObjectQueue?.Count + " solid) in " + (float)microseconds / 1000f + "ms");
            //sw.Restart();

            if (__instance.m_tempToSync.Count <= 0)
            {
                return;
            }

            if (peer.m_zdoImage == null)
                peer.m_zdoImage = new Dictionary<ZDOID, ZDO>();

            // I'm not exactly sure what the MTU size is
            // Also I'm not exactly sure how big the overhead is from all ZPackage and ZRPC stuff
            // so lets just lower it a little from the common default MTU size
            const int maxPacketSize = 1400;

            // The game source code divided it by 20 assuming you have 20 fps
            // This seems a little iffy it would be better to multiply it by the actual frequency this function is being called
            // After all this 0.05 or 20 fps, is under the assumption that we are actually reaching 20fps :P
            // int maxDataPerTick = (int)((float)__instance.m_dataPerSec * 0.05f);

            // Packet size is probably much more effective
            int maxDataPerTick = (maxPacketSize - 150) * 20;

            var zdoCollectionPkg = new ZPackage();
            var zdoPkg = new ZPackage();

            int zdoSendCount = 0;
            int totalBytes = 0;
            int totalCollections = 0;

            int zdoCollectionCount = 0;
            var zdoCollectionCountPos = zdoCollectionPkg.GetPos();
            zdoCollectionPkg.Write(zdoCollectionCount); // Placeholder count;

            var valheimMP = ValheimMP.Instance;

            for (int i = 0; i < __instance.m_tempToSync.Count; i++)
            {
                ZDO zDO = __instance.m_tempToSync[i];
                var forced = peer.m_forceSend.Remove(zDO.m_uid);
                var solidUpdate = peer.m_peer.m_solidObjectQueue.Remove(zDO.m_uid);

                if (zDO.m_zdoType == (int)ZDOType.Ignored)
                    continue;

                if (zDO.m_zdoType == (int)ZDOType.AllExceptOriginator && peer.m_peer.m_uid == zDO.m_originator)
                    continue;

                if (zDO.m_zdoType == (int)ZDOType.Private && peer.m_peer.m_uid != zDO.m_owner)
                    continue;

                // this object is no longer relevant, just skip it
                if (!peer.m_peer.m_loadedSectors.ContainsKey(zDO.m_sector))
                    continue;

                if (!forced && !solidUpdate && !peer.ShouldSend(zDO))
                    continue;

                peer.m_zdos[zDO.m_uid] = new ZDOMan.ZDOPeer.PeerZDOInfo(zDO.m_dataRevision, zDO.m_ownerRevision, Time.time);

                ZDO clientZDO;

                if (!peer.m_zdoImage.TryGetValue(zDO.m_uid, out clientZDO))
                {
                    clientZDO = new ZDO();
                    peer.m_zdoImage.Add(zDO.m_uid, clientZDO);
                }


                if (!SerializeZDOFor(peer, ref zdoPkg, ref zDO, ref clientZDO))
                    continue;

                zdoSendCount++;

                totalBytes += zdoPkg.Size();

                // a single zdo wont fit in a package? send it alone! ignore the current collection
                if (zdoPkg.Size() >= maxPacketSize)
                {
                    ZPackage singleItemCollection = new ZPackage();
                    singleItemCollection.Write((int)1);
                    singleItemCollection.Write(zdoPkg);

                    peer.m_peer.m_rpc.Invoke("ZDOData", valheimMP.UseZDOCompression.Value ? singleItemCollection.Compress() : singleItemCollection);
                    totalCollections++;
                    // we've send the single item in a packge and now we continue
                    // any smaller zdos will stil continue being added to the current collection
                    continue;
                }


                if (zdoCollectionPkg.Size() + zdoPkg.Size() >= maxPacketSize)
                {
                    var collectionEndPos = zdoCollectionPkg.GetPos();
                    zdoCollectionPkg.SetPos(zdoCollectionCountPos);
                    zdoCollectionPkg.Write(zdoCollectionCount);
                    zdoCollectionPkg.SetPos(collectionEndPos);

                    peer.m_peer.m_rpc.Invoke("ZDOData", valheimMP.UseZDOCompression.Value ? zdoCollectionPkg.Compress() : zdoCollectionPkg);
                    totalCollections++;


                    zdoCollectionPkg = new ZPackage();
                    zdoCollectionPkg.Write(zdoCollectionCount); // Placeholder count;
                    // Count to one because we are writing the current page to the new collection!
                    zdoCollectionCount = 0;
                }

                zdoCollectionCount++;
                zdoCollectionPkg.Write(zdoPkg);

                if (totalBytes > maxDataPerTick)
                {
                    break;
                }
            }

            if (zdoCollectionCount > 0)
            {
                var collectionEndPos = zdoCollectionPkg.GetPos();
                zdoCollectionPkg.SetPos(zdoCollectionCountPos);
                zdoCollectionPkg.Write(zdoCollectionCount);
                zdoCollectionPkg.SetPos(collectionEndPos);
                peer.m_peer.m_rpc.Invoke("ZDOData", valheimMP.UseZDOCompression.Value ? zdoCollectionPkg.Compress() : zdoCollectionPkg);
                totalCollections++;
            }
        }

        internal static bool SerializeZDOFor(ZDOMan.ZDOPeer peer, ref ZPackage zdoPkg, ref ZDO zdo, ref ZDO clientZDO)
        {
            var valheimMP = ValheimMP.Instance;

#if DEBUG
            if (valheimMP && valheimMP.DebugOutputZDO.Value)
            {
                if (!valheimMP.ZDODebug.ContainsKey(zdo.m_prefab))
                    valheimMP.ZDODebug.Add(zdo.m_prefab, new Dictionary<string, int>());
            }
#endif

            zdoPkg.Clear();

            ZDOFlags flags = ZDOFlags.none;
            // All fixed members of ZDO's
            // Normally all this junk including owner and data revision are send
            // even if they dont change, so lets just not.
            zdoPkg.Write(zdo.m_uid);
            var flagsPos = zdoPkg.GetPos();
            zdoPkg.Write((int)flags); // this is a placeholder!


            if (Math.Abs(zdo.m_position.x - clientZDO.m_position.x) > 0.01f ||
                Math.Abs(zdo.m_position.z - clientZDO.m_position.z) > 0.01f ||
                Math.Abs(zdo.m_position.y - clientZDO.m_position.y) > 0.01f)
            {
                clientZDO.m_position = zdo.m_position;
                flags |= ZDOFlags.m_position;
                zdoPkg.Write(zdo.m_position);
            }

            if (zdo.m_owner != clientZDO.m_owner)
            {
                clientZDO.m_owner = zdo.m_owner;
                flags |= ZDOFlags.m_owner;
                zdoPkg.Write(zdo.m_owner);
            }

            if (zdo.m_persistent != clientZDO.m_persistent)
            {
                clientZDO.m_persistent = zdo.m_persistent;
                flags |= ZDOFlags.m_persistent;
                zdoPkg.Write(zdo.m_persistent);
            }

            if (zdo.m_distant != clientZDO.m_distant)
            {
                clientZDO.m_distant = zdo.m_distant;
                flags |= ZDOFlags.m_distant;
                zdoPkg.Write(zdo.m_distant);
            }

            if (zdo.m_timeCreated != clientZDO.m_timeCreated)
            {
                clientZDO.m_timeCreated = zdo.m_timeCreated;
                flags |= ZDOFlags.m_timeCreated;
                zdoPkg.Write(zdo.m_timeCreated);
            }

            if (zdo.m_pgwVersion != clientZDO.m_pgwVersion)
            {
                clientZDO.m_pgwVersion = zdo.m_pgwVersion;
                flags |= ZDOFlags.m_pgwVersion;
                zdoPkg.Write(zdo.m_pgwVersion);
            }

            if (zdo.m_type != clientZDO.m_type)
            {
                clientZDO.m_type = zdo.m_type;
                flags |= ZDOFlags.m_type;
                zdoPkg.Write((int)zdo.m_type);
            }

            if (zdo.m_prefab != clientZDO.m_prefab)
            {
                clientZDO.m_prefab = zdo.m_prefab;
                flags |= ZDOFlags.m_prefab;
                zdoPkg.Write(zdo.m_prefab);
            }

            if (Math.Abs(zdo.m_rotation.x - clientZDO.m_rotation.x) > 0.01f ||
                Math.Abs(zdo.m_rotation.y - clientZDO.m_rotation.y) > 0.01f ||
                Math.Abs(zdo.m_rotation.z - clientZDO.m_rotation.z) > 0.01f ||
                Math.Abs(zdo.m_rotation.w - clientZDO.m_rotation.w) > 0.01f)
            {
                clientZDO.m_rotation = zdo.m_rotation;
                flags |= ZDOFlags.m_rotation;
                zdoPkg.Write(zdo.m_rotation);
            }

            if (zdo.m_floats != null && zdo.m_floats.Count > 0)
            {
                byte writtenCount = 0;
                int countAt = zdoPkg.GetPos();
                // write the placeholder
                zdoPkg.Write(writtenCount);
                foreach (KeyValuePair<int, float> item in zdo.m_floats)
                {
                    if (zdo.m_fieldTypes.TryGetValue(item.Key, out var zdoFieldType))
                    {
                        if (zdoFieldType == (int)ZDOFieldType.Ignored)
                            continue;
                        if (peer.m_peer.m_uid != zdo.m_owner && zdoFieldType == (int)ZDOFieldType.Private)
                            continue;
                        if (peer.m_peer.m_uid == zdo.m_owner && zdoFieldType == (int)ZDOFieldType.AllExceptOwner)
                            continue;
                    }

                    clientZDO.InitFloats();
                    if (clientZDO.m_floats.UpdateValue(item.Key, item.Value))
                    {
                        writtenCount++;
                        zdoPkg.Write(item.Key);
                        zdoPkg.Write(item.Value);
#if DEBUG
                        if (valheimMP && valheimMP.DebugOutputZDO.Value)
                            valheimMP.ZDODebug[zdo.m_prefab].Increment(StringExtensionMethods_Patch.GetStableHashName(item.Key));
#endif
                    }
                }

                int listEndPos = zdoPkg.GetPos();
                zdoPkg.SetPos(countAt);
                if (writtenCount > 0)
                {
                    zdoPkg.Write(writtenCount);
                    flags |= ZDOFlags.m_floats;
                    zdoPkg.SetPos(listEndPos);
                }
            }

            if (zdo.m_vec3 != null && zdo.m_vec3.Count > 0)
            {
                byte writtenCount = 0;
                int countAt = zdoPkg.GetPos();
                // write the placeholder
                zdoPkg.Write(writtenCount);
                foreach (KeyValuePair<int, Vector3> item in zdo.m_vec3)
                {
                    if (zdo.m_fieldTypes.TryGetValue(item.Key, out var zdoFieldType))
                    {
                        if (zdoFieldType == (int)ZDOFieldType.Ignored)
                            continue;
                        if (peer.m_peer.m_uid != zdo.m_owner && zdoFieldType == (int)ZDOFieldType.Private)
                            continue;
                        if (peer.m_peer.m_uid == zdo.m_owner && zdoFieldType == (int)ZDOFieldType.AllExceptOwner)
                            continue;
                    }

                    clientZDO.InitVec3();

                    if (clientZDO.m_vec3.UpdateValue(item.Key, item.Value))
                    {
                        writtenCount++;
                        zdoPkg.Write(item.Key);
                        zdoPkg.Write(item.Value);
#if DEBUG
                        if (valheimMP && valheimMP.DebugOutputZDO.Value)
                            valheimMP.ZDODebug[zdo.m_prefab].Increment(StringExtensionMethods_Patch.GetStableHashName(item.Key));
#endif
                    }
                }

                int listEndPos = zdoPkg.GetPos();
                zdoPkg.SetPos(countAt);
                if (writtenCount > 0)
                {
                    zdoPkg.Write(writtenCount);
                    flags |= ZDOFlags.m_vec3;
                    zdoPkg.SetPos(listEndPos);
                }
            }

            if (zdo.m_quats != null && zdo.m_quats.Count > 0)
            {
                byte writtenCount = 0;
                int countAt = zdoPkg.GetPos();
                // write the placeholder
                zdoPkg.Write(writtenCount);
                foreach (KeyValuePair<int, Quaternion> item in zdo.m_quats)
                {
                    if (zdo.m_fieldTypes.TryGetValue(item.Key, out var zdoFieldType))
                    {
                        if (zdoFieldType == (int)ZDOFieldType.Ignored)
                            continue;
                        if (peer.m_peer.m_uid != zdo.m_owner && zdoFieldType == (int)ZDOFieldType.Private)
                            continue;
                        if (peer.m_peer.m_uid == zdo.m_owner && zdoFieldType == (int)ZDOFieldType.AllExceptOwner)
                            continue;
                    }

                    clientZDO.InitQuats();
                    if (clientZDO.m_quats.UpdateValue(item.Key, item.Value))
                    {
                        writtenCount++;
                        zdoPkg.Write(item.Key);
                        zdoPkg.Write(item.Value);
#if DEBUG && !TEST
                        if (valheimMP && valheimMP.DebugOutputZDO.Value)
                            valheimMP.ZDODebug[zdo.m_prefab].Increment(StringExtensionMethods_Patch.GetStableHashName(item.Key));
#endif
                    }
                }

                int listEndPos = zdoPkg.GetPos();
                zdoPkg.SetPos(countAt);
                if (writtenCount > 0)
                {
                    zdoPkg.Write(writtenCount);
                    flags |= ZDOFlags.m_quats;
                    zdoPkg.SetPos(listEndPos);
                }
            }

            if (zdo.m_ints != null && zdo.m_ints.Count > 0)
            {
                byte writtenCount = 0;
                int countAt = zdoPkg.GetPos();
                // write the placeholder
                zdoPkg.Write(writtenCount);
                foreach (KeyValuePair<int, int> item in zdo.m_ints)
                {
                    if (zdo.m_fieldTypes.TryGetValue(item.Key, out var zdoFieldType))
                    {
                        if (zdoFieldType == (int)ZDOFieldType.Ignored)
                            continue;
                        if (peer.m_peer.m_uid != zdo.m_owner && zdoFieldType == (int)ZDOFieldType.Private)
                            continue;
                        if (peer.m_peer.m_uid == zdo.m_owner && zdoFieldType == (int)ZDOFieldType.AllExceptOwner)
                            continue;
                    }

                    clientZDO.InitInts();
                    if (clientZDO.m_ints.UpdateValue(item.Key, item.Value))
                    {
                        writtenCount++;
                        zdoPkg.Write(item.Key);
                        zdoPkg.Write(item.Value);
#if DEBUG && !TEST
                        if (valheimMP && valheimMP.DebugOutputZDO.Value)
                            valheimMP.ZDODebug[zdo.m_prefab].Increment(StringExtensionMethods_Patch.GetStableHashName(item.Key));
#endif
                    }
                }

                int listEndPos = zdoPkg.GetPos();
                zdoPkg.SetPos(countAt);
                if (writtenCount > 0)
                {
                    zdoPkg.Write(writtenCount);
                    flags |= ZDOFlags.m_ints;
                    zdoPkg.SetPos(listEndPos);
                }
            }

            if (zdo.m_strings != null && zdo.m_strings.Count > 0)
            {
                byte writtenCount = 0;
                int countAt = zdoPkg.GetPos();
                // write the placeholder
                zdoPkg.Write(writtenCount);
                foreach (KeyValuePair<int, string> item in zdo.m_strings)
                {
                    if (zdo.m_fieldTypes.TryGetValue(item.Key, out var zdoFieldType))
                    {
                        if (zdoFieldType == (int)ZDOFieldType.Ignored)
                            continue;
                        if (peer.m_peer.m_uid != zdo.m_owner && zdoFieldType == (int)ZDOFieldType.Private)
                            continue;
                        if (peer.m_peer.m_uid == zdo.m_owner && zdoFieldType == (int)ZDOFieldType.AllExceptOwner)
                            continue;
                    }

                    clientZDO.InitStrings();
                    if (clientZDO.m_strings.UpdateValue(item.Key, item.Value))
                    {
                        writtenCount++;
                        zdoPkg.Write(item.Key);
                        zdoPkg.Write(item.Value);
#if DEBUG
                        if (valheimMP && valheimMP.DebugOutputZDO.Value)
                            valheimMP.ZDODebug[zdo.m_prefab].Increment(StringExtensionMethods_Patch.GetStableHashName(item.Key));
#endif
                    }
                }

                int listEndPos = zdoPkg.GetPos();
                zdoPkg.SetPos(countAt);
                if (writtenCount > 0)
                {
                    zdoPkg.Write(writtenCount);
                    flags |= ZDOFlags.m_strings;
                    zdoPkg.SetPos(listEndPos);
                }
            }

            if (zdo.m_byteArrays != null && zdo.m_byteArrays.Count > 0)
            {
                byte writtenCount = 0;
                int countAt = zdoPkg.GetPos();
                // write the placeholder
                zdoPkg.Write(writtenCount);
                foreach (KeyValuePair<int, byte[]> item in zdo.m_byteArrays)
                {
                    if (zdo.m_fieldTypes.TryGetValue(item.Key, out var zdoFieldType))
                    {
                        if (zdoFieldType == (int)ZDOFieldType.Ignored)
                            continue;
                        if (peer.m_peer.m_uid != zdo.m_owner && zdoFieldType == (int)ZDOFieldType.Private)
                            continue;
                        if (peer.m_peer.m_uid == zdo.m_owner && zdoFieldType == (int)ZDOFieldType.AllExceptOwner)
                            continue;
                    }

                    clientZDO.InitByteArrays();
                    if (clientZDO.m_byteArrays.UpdateValue(item.Key, item.Value))
                    {
                        writtenCount++;
                        zdoPkg.Write(item.Key);
                        zdoPkg.Write(item.Value);
#if DEBUG
                        if (valheimMP && valheimMP.DebugOutputZDO.Value)
                            valheimMP.ZDODebug[zdo.m_prefab].Increment(StringExtensionMethods_Patch.GetStableHashName(item.Key));
#endif
                    }
                }

                int listEndPos = zdoPkg.GetPos();
                zdoPkg.SetPos(countAt);
                if (writtenCount > 0)
                {
                    zdoPkg.Write(writtenCount);
                    flags |= ZDOFlags.m_byteArrays;
                    zdoPkg.SetPos(listEndPos);
                }
            }

            if (zdo.m_longs != null && zdo.m_longs.Count > 0)
            {
                byte writtenCount = 0;
                int countAt = zdoPkg.GetPos();
                // write the placeholder
                zdoPkg.Write(writtenCount);
                foreach (KeyValuePair<int, long> item in zdo.m_longs)
                {
                    if (zdo.m_fieldTypes.TryGetValue(item.Key, out var zdoFieldType))
                    {
                        if (zdoFieldType == (int)ZDOFieldType.Ignored)
                            continue;
                        if (peer.m_peer.m_uid != zdo.m_owner && zdoFieldType == (int)ZDOFieldType.Private)
                            continue;
                        if (peer.m_peer.m_uid == zdo.m_owner && zdoFieldType == (int)ZDOFieldType.AllExceptOwner)
                            continue;
                    }

                    clientZDO.InitLongs();
                    if (clientZDO.m_longs.UpdateValue(item.Key, item.Value))
                    {
                        writtenCount++;
                        zdoPkg.Write(item.Key);
                        zdoPkg.Write(item.Value);
#if DEBUG
                        if (valheimMP && valheimMP.DebugOutputZDO.Value)
                            valheimMP.ZDODebug[zdo.m_prefab].Increment(StringExtensionMethods_Patch.GetStableHashName(item.Key));
#endif
                    }
                }

                int listEndPos = zdoPkg.GetPos();
                zdoPkg.SetPos(countAt);
                if (writtenCount > 0)
                {
                    zdoPkg.Write(writtenCount);
                    flags |= ZDOFlags.m_longs;
                    zdoPkg.SetPos(listEndPos);
                }
            }


            if (flags == ZDOFlags.none)
            {
                // we have reached the end, and nothing has been changed!
                return false;
            }

            var endPos = zdoPkg.GetPos();
            // Seek back to the flags pos and write the actual result!
            zdoPkg.SetPos(flagsPos);
            zdoPkg.Write((int)flags);
            zdoPkg.SetPos(endPos);

#if DEBUG
            if (valheimMP && valheimMP.DebugOutputZDO.Value)
            {
                for (int i = 0; i < 20; i++)
                {
                    var key = (1 << i);
                    var strKey = ((ZDOFlags)key).ToString();
                    if (((int)flags & key) == key)
                    {
                        valheimMP.ZDODebug[zdo.m_prefab].Increment(strKey);
                    }
                }
            }
#endif
            return true;
        }


        [HarmonyPatch(typeof(ZDOMan), "RPC_ZDOData")]
        [HarmonyPrefix]
        private static bool RPC_ZDOData(ref ZDOMan __instance, ZRpc rpc, ZPackage pkg)
        {
            if (ZNet.instance.IsServer())
            {
                // Received a ZDO on the server... we don't send any on clients, so we should never receive any!
                // For now anyway, if I were to delegate AI to another server I would probably re-enable this for only that client
                var peer = ZNet.instance.GetPeer(rpc);
                if (peer != null)
                {
                    ValheimMP.LogWarning($"Disconnecting client {peer.m_playerName} ({peer.m_uid}).");
                    ZNet.instance.Disconnect(peer);
                }
                return false;
            }

            ZDOMan.ZDOPeer zDOPeer = __instance.FindPeer(rpc);
            if (zDOPeer == null)
            {
                ValheimMP.Log("ZDO data from unkown host, ignoring");
                return false;
            }

            if (ValheimMP.Instance.UseZDOCompression.Value)
                pkg = pkg.Decompress();

            var count = pkg.ReadInt();

            //ValheimMP.Log("Receiving collection with " + count + " items (" + pkg.Size() + " bytes)");
            ZPackage zdoPkg = new ZPackage();

            for (int i = 0; i < count; i++)
            {
                pkg.ReadPackage(ref zdoPkg);

                // We want to know the ZDOID so we can determine if we can use an existing one!
                // So read it and reset the position, that way DeserializeZDO will still remain a fully self contained function
                var zdoid = zdoPkg.ReadZDOID();
                zdoPkg.SetPos(0);

                var isNewZDO = false;
                var zDO = __instance.GetZDO(zdoid);
                if (zDO == null)
                {
                    zDO = ZDOPool.Create(__instance);
                    isNewZDO = true;
                }

                var updateActions = new List<Action<ZDO>>();

                DeserializeZDO(ref zdoPkg, ref zDO, ref updateActions);

                if (isNewZDO)
                {
                    // Initialize ZDO, this is normally done bt ZDOMan.CreateNewZDO() 
                    // but since we dont use that some things need to be done manually
                    zDO.m_sector = ZoneSystem.instance.GetZone(zDO.m_position);
                    __instance.AddToSector(zDO, zDO.m_sector);
                    __instance.m_objectsByID.Add(zDO.m_uid, zDO);
                }
                else
                {
                    zDO.SetSector(ZoneSystem.instance.GetZone(zDO.m_position));
                }


                zDOPeer.m_zdos[zDO.m_uid] = new ZDOMan.ZDOPeer.PeerZDOInfo(zDO.m_dataRevision++, zDO.m_ownerRevision, Time.time);

                updateActions.Do(k => k(zDO));
            }

            __instance.m_zdosRecv += count;


            return false;
        }

        internal static void DeserializeZDO(ref ZPackage zdoPkg, ref ZDO zdo, ref List<Action<ZDO>> updateEvents)
        {
            zdo.m_uid = zdoPkg.ReadZDOID();

            var intFlags = zdoPkg.ReadInt();

            var flags = (ZDOFlags)intFlags;

            //ValheimMP.Log("Recv ZDO " + zdoid + " (" + pkg.Size() + " bytes) flags: "+ flags);

            if (((int)ZDOFlags.invalid & intFlags) != 0)
            {
                ValheimMP.LogError("Invalid flags in package!");
                return;
            }

            if ((flags & ZDOFlags.m_position) == ZDOFlags.m_position) zdo.m_position = zdoPkg.ReadVector3();
            if ((flags & ZDOFlags.m_owner) == ZDOFlags.m_owner) zdo.m_owner = zdoPkg.ReadLong();
            if ((flags & ZDOFlags.m_persistent) == ZDOFlags.m_persistent) zdo.m_persistent = zdoPkg.ReadBool();
            if ((flags & ZDOFlags.m_distant) == ZDOFlags.m_distant) zdo.m_distant = zdoPkg.ReadBool();
            if ((flags & ZDOFlags.m_timeCreated) == ZDOFlags.m_timeCreated) zdo.m_timeCreated = zdoPkg.ReadLong();
            if ((flags & ZDOFlags.m_pgwVersion) == ZDOFlags.m_pgwVersion) zdo.m_pgwVersion = zdoPkg.ReadInt();
            if ((flags & ZDOFlags.m_type) == ZDOFlags.m_type) zdo.m_type = (ZDO.ObjectType)zdoPkg.ReadInt();
            if ((flags & ZDOFlags.m_prefab) == ZDOFlags.m_prefab) zdo.m_prefab = zdoPkg.ReadInt();
            if ((flags & ZDOFlags.m_rotation) == ZDOFlags.m_rotation) zdo.m_rotation = zdoPkg.ReadQuaternion();

            if ((flags & ZDOFlags.m_floats) == ZDOFlags.m_floats)
            {
                zdo.InitFloats();
                var listCount = zdoPkg.ReadByte();
                for (byte j = 0; j < listCount; j++)
                {
                    var key = zdoPkg.ReadInt();
                    var value = zdoPkg.ReadSingle();
                    zdo.m_floats[key] = value;
                    if (zdo.m_zdoEvents.TryGetValue(key, out var action))
                        updateEvents.Add(action);
                }
            }

            if ((flags & ZDOFlags.m_vec3) == ZDOFlags.m_vec3)
            {
                zdo.InitVec3();
                var listCount = zdoPkg.ReadByte();
                for (byte j = 0; j < listCount; j++)
                {
                    var key = zdoPkg.ReadInt();
                    var value = zdoPkg.ReadVector3();
                    zdo.m_vec3[key] = value;
                    if (zdo.m_zdoEvents.TryGetValue(key, out var action))
                        updateEvents.Add(action);
                }
            }

            if ((flags & ZDOFlags.m_quats) == ZDOFlags.m_quats)
            {
                zdo.InitQuats();
                var listCount = zdoPkg.ReadByte();
                for (byte j = 0; j < listCount; j++)
                {
                    var key = zdoPkg.ReadInt();
                    var value = zdoPkg.ReadQuaternion();
                    zdo.m_quats[key] = value;
                    if (zdo.m_zdoEvents.TryGetValue(key, out var action))
                        updateEvents.Add(action);
                }
            }

            if ((flags & ZDOFlags.m_ints) == ZDOFlags.m_ints)
            {
                zdo.InitInts();
                var listCount = zdoPkg.ReadByte();
                for (byte j = 0; j < listCount; j++)
                {
                    var key = zdoPkg.ReadInt();
                    var value = zdoPkg.ReadInt();
                    zdo.m_ints[key] = value;
                    if (zdo.m_zdoEvents.TryGetValue(key, out var action))
                        updateEvents.Add(action);

                }
            }

            if ((flags & ZDOFlags.m_strings) == ZDOFlags.m_strings)
            {
                zdo.InitStrings();
                var listCount = zdoPkg.ReadByte();
                for (byte j = 0; j < listCount; j++)
                {
                    var key = zdoPkg.ReadInt();
                    var value = zdoPkg.ReadString();
                    zdo.m_strings[key] = value;
                    if (zdo.m_zdoEvents.TryGetValue(key, out var action))
                        updateEvents.Add(action);
                }
            }

            if ((flags & ZDOFlags.m_byteArrays) == ZDOFlags.m_byteArrays)
            {
                zdo.InitByteArrays();
                var listCount = zdoPkg.ReadByte();
                for (byte j = 0; j < listCount; j++)
                {
                    var key = zdoPkg.ReadInt();
                    var value = zdoPkg.ReadByteArray();
                    zdo.m_byteArrays[key] = value;
                    if (zdo.m_zdoEvents.TryGetValue(key, out var action))
                        updateEvents.Add(action);
                }
            }

            if ((flags & ZDOFlags.m_longs) == ZDOFlags.m_longs)
            {
                zdo.InitLongs();
                var listCount = zdoPkg.ReadByte();
                for (byte j = 0; j < listCount; j++)
                {
                    var key = zdoPkg.ReadInt();
                    var value = zdoPkg.ReadLong();
                    zdo.m_longs[key] = value;
                    if (zdo.m_zdoEvents.TryGetValue(key, out var action))
                        updateEvents.Add(action);
                }
            }
        }

        [HarmonyPatch(typeof(ZDOMan), "ClientChanged")]
        [HarmonyPrefix]
        private static bool ClientChanged(ZDOID id)
        {
            return false;
        }
    }
}

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Security;
using System.Security.Permissions;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class ZNetScene_Patch
    {

        [HarmonyPatch(typeof(ZNetScene), "CreateObjects")]
        [HarmonyPrefix]
        private static bool CreateObjects(ZNetScene __instance, List<ZDO> currentNearObjects, List<ZDO> currentDistantObjects)
        {
            int maxCreatedPerFrame = 10;

            if (ZNet.instance.IsServer())
            {
                maxCreatedPerFrame = 100;
            }

            else if(Player.m_localPlayer == null || (Player.m_localPlayer != null && 
                (Player.m_localPlayer.IsTeleporting() || 
                Player.m_localPlayer.m_teleportCooldown < 5f || 
                Hud.instance.m_loadingScreen.isActiveAndEnabled)))
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
            return false;
        }

        [HarmonyPatch(typeof(ZNetScene), "RemoveObjects")]
        [HarmonyPrefix]
        private static void RemoveObjects(ZNetScene __instance, List<ZDO> currentNearObjects, List<ZDO> currentDistantObjects)
        {
            if(ZNet.instance != null && !ZNet.instance.IsServer() && Player.m_localPlayer != null)
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
        private static bool CreateDestroyObjects(ref ZNetScene __instance, ref List<ZDO> ___m_tempCurrentObjects, ref List<ZDO> ___m_tempCurrentDistantObjects)
        {
            if (!ZNet.instance.IsServer())
            {
                return true;
            }

            ___m_tempCurrentObjects.Clear();
            ___m_tempCurrentDistantObjects.Clear();
            FindSectorObjectsForAllCharacters(ZoneSystem.instance.m_activeArea, ZoneSystem.instance.m_activeDistantArea, ___m_tempCurrentObjects, ___m_tempCurrentDistantObjects);
            __instance.CreateObjects(___m_tempCurrentObjects, ___m_tempCurrentDistantObjects);
            __instance.RemoveObjects(___m_tempCurrentObjects, ___m_tempCurrentDistantObjects);

            return false;
        }

        private static void FindSectorObjectsForAllCharacters(int area, int distantArea, List<ZDO> sectorObjects, List<ZDO> distantSectorObjects = null)
        {
            var sectorVectors = new HashSet<Vector2i>();
            var distantSectorVectors = new HashSet<Vector2i>();
            var peers = ZNet.instance.m_peers;

            // peers may have overlapping sectors so add them all to a hashset before we scan the final ones.
            foreach (var peer in peers)
            {
                // better check all these for nulls wouldnt want to suddenly break and stop sending everyones zdos because of one guy 
                if(peer.m_player != null && peer.m_player.m_nview != null && peer.m_player.m_nview.m_zdo != null)
                {
                    // Keep player characters alive regardless of whether they fall out of the playable area
                    peer.m_player.m_nview.m_zdo.m_tempRemoveEarmark = Time.frameCount;
                }

                if (peer.m_refPos == Vector3.zero)
                    continue;
                // use refPos not player position because preloading terrain needs to happen before players are spawned!
                Vector2i sector = ZoneSystem.instance.GetZone(peer.m_refPos);

                sectorVectors.Add(sector);
                for (int i = 1; i <= area; i++)
                {
                    for (int x = sector.x - i; x <= sector.x + i; x++)
                    {
                        sectorVectors.Add(new Vector2i(x, sector.y - i));
                        sectorVectors.Add(new Vector2i(x, sector.y + i));
                    }
                    for (int y = sector.y - i + 1; y <= sector.y + i - 1; y++)
                    {
                        sectorVectors.Add(new Vector2i(sector.x - i, y));
                        sectorVectors.Add(new Vector2i(sector.x + i, y));
                    }
                }

                for (int l = area + 1; l <= area + distantArea; l++)
                {
                    for (int m = sector.x - l; m <= sector.x + l; m++)
                    {
                        distantSectorVectors.Add(new Vector2i(m, sector.y - l));
                        distantSectorVectors.Add(new Vector2i(m, sector.y + l));
                    }

                    for (int n = sector.y - l + 1; n <= sector.y + l - 1; n++)
                    {
                        distantSectorVectors.Add(new Vector2i(sector.x - l, n));
                        distantSectorVectors.Add(new Vector2i(sector.x + l, n));
                    }
                }
            }

            foreach (var v in sectorVectors)
            {
                ZDOMan.instance.FindObjects(v, sectorObjects);
            }

            var objects = distantSectorObjects ?? sectorObjects;
            foreach (var v in distantSectorVectors)
            {
                ZDOMan.instance.FindDistantObjects(v, objects);
            }


            //ZLog.Log("Managing " + sectorObjects.Count.ToString() + ":" + distantSectorObjects.Count.ToString() + " objects for " + peers.Count.ToString() + " peer(s) ");
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

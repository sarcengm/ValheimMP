using HarmonyLib;
using System;
using System.Collections.Generic;
using ValheimMP.Framework;

namespace ValheimMP.Patches
{

    [HarmonyPatch]
    internal class ZDO_Patch
    {
        [HarmonyPatch(typeof(ZDO), MethodType.Constructor)]
        [HarmonyPostfix]
        private static void Constructor(ref ZDO __instance)
        {
            // Similar to field type except for the entire zdo
            __instance.m_zdoType = 0; 
            // Private variables are those that are not replicated to non owning clients
            __instance.m_fieldTypes = new Dictionary<int, int>();
            // Event that gets fired when a specific ZDO element is updated.
            __instance.m_zdoEvents = new Dictionary<int, Action<ZDO>>();
        }

        [HarmonyPatch(typeof(ZDO), "Reset")]
        [HarmonyPostfix]
        private static void Reset(ref ZDO __instance)
        {
            __instance.m_zdoType = 0;
            __instance.m_fieldTypes.Clear();
            __instance.m_zdoEvents.Clear();
            __instance.ReleaseByteArrays();
        }

        [HarmonyPatch(typeof(ZDO), "SetOwner")]
        [HarmonyPrefix]
        private static bool SetOwner(ref ZDO __instance, long uid)
        {
            if (!ZNet.instance.IsServer())
            {
                ValheimMP.LogWarning("ZDO.SetOwner on client");
                ValheimMP.Log(new System.Diagnostics.StackTrace().ToString());
                return false;
            }

            if (uid != ZDOMan.instance.GetMyID() && ZNet.instance.IsServer())
            {
                if (uid != 0)
                {
                    if (__instance.m_nview == null || __instance.m_nview.GetComponent<Player>() == null)
                    {
                        ValheimMP.LogWarning("SetOwner to non server! " + uid + " mine: " + ZDOMan.instance.GetMyID());
                        ValheimMP.Log(new System.Diagnostics.StackTrace().ToString());
                    }
                }
                //uid = ZDOMan.instance.GetMyID();
            }

            if (__instance.m_owner != uid)
            {
                __instance.m_owner = uid;
                __instance.IncreseOwnerRevision();
            }
            return false;
        }

        [HarmonyPatch(typeof(ZDO), "IncreseDataRevision")]
        [HarmonyPrefix]
        private static void IncreseDataRevision(ref ZDO __instance)
        {
            if (ZNet.instance.IsServer() && (__instance.m_type == ZDO.ObjectType.Solid || __instance.m_type == ZDO.ObjectType.Terrain) && __instance.m_nview != null)
            {
                var peers = ZNet.instance.m_peers;
                for (int i=0; i< peers.Count; i++)
                {
                    var peer = peers[i];
                    if (peer.m_loadedSectors.ContainsKey(__instance.m_sector))
                    {
                        peer.m_solidObjectQueue[__instance.m_uid] = __instance;
                    }
                }
            }
        }


        [HarmonyPatch(typeof(ZDO), "SetSector")]
        [HarmonyPrefix]
        private static bool SetSector(ref ZDO __instance, Vector2i sector)
        {
            if (!(__instance.m_sector == sector))
            {
                SectorManager.RemoveObject(__instance);
                __instance.m_zdoMan.RemoveFromSector(__instance, __instance.m_sector);
                __instance.m_sector = sector;

                SectorManager.AddObject(__instance);
                __instance.m_zdoMan.AddToSector(__instance, __instance.m_sector);
                __instance.m_zdoMan.ZDOSectorInvalidated(__instance);
            }

            return false;
        }

#if DEBUG
        [HarmonyPatch(typeof(ZDO), "Print")]
        [HarmonyPrefix]
        private static bool Print(ref ZDO __instance)
        {
            foreach (var item in __instance.m_floats)
            {
                ValheimMP.Log(StringExtensionMethods_Patch.GetStableHashName(item.Key) + ": " + item.Value);
            }
            foreach (var item in __instance.m_ints)
            {
                ValheimMP.Log(StringExtensionMethods_Patch.GetStableHashName(item.Key) + ": " + item.Value);
            }
            foreach (var item in __instance.m_longs)
            {
                ValheimMP.Log(StringExtensionMethods_Patch.GetStableHashName(item.Key) + ": " + item.Value);
            }
            foreach (var item in __instance.m_quats)
            {
                ValheimMP.Log(StringExtensionMethods_Patch.GetStableHashName(item.Key) + ": " + item.Value);
            }
            foreach (var item in __instance.m_vec3)
            {
                ValheimMP.Log(StringExtensionMethods_Patch.GetStableHashName(item.Key) + ": " + item.Value);
            }
            foreach (var item in __instance.m_strings)
            {
                ValheimMP.Log(StringExtensionMethods_Patch.GetStableHashName(item.Key) + ": " + item.Value);
            }
            return false;
        }
#endif

    }




}

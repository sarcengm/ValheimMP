﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ValheimMP.Patches
{

    [HarmonyPatch]
    public class ZoneSystem_Patch
    {
        [HarmonyPatch(typeof(ZoneSystem), "Update")]
        [HarmonyPrefix]
        private static bool Update(ref ZoneSystem __instance)
        {
            if (!ZNet.instance.IsServer())
                return true;

            __instance.m_updateTimer += Time.deltaTime;
            if (__instance.m_updateTimer < 0.1f)
            {
                return false;
            }
            __instance.m_updateTimer = 0f;

            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
            {
                __instance.CreateLocalZones(peer.m_refPos);
            }
            return false;
        }
    }
}

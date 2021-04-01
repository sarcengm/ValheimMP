using HarmonyLib;
using System;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class ZSyncTransform_Patch
    {
        [HarmonyPatch(typeof(ZSyncTransform), "ClientSync")]
        [HarmonyPrefix]
        private static bool ClientSync(ZSyncTransform __instance, float dt)
        {
            if (!__instance.m_nview.IsOwner() || ZNet.instance.IsServer())
            {
                return true;
            }

            var zdo = __instance.m_nview.GetZDO();
            if (zdo == null)
                return false;

            if (__instance.m_syncScale)
            {
                Vector3 vec3 = zdo.GetVec3(ZSyncTransform.m_scaleHash, __instance.transform.localScale);
                __instance.transform.localScale = vec3;
            }

            if (__instance.m_body == null)
                return false;

            var player = __instance.GetComponent<Player>();
            if (player == null)
                return false;

            // OwnerSync is reserved for the server, while Client sync normally syncs clients.
            // But since we simulate our own position we only need to resync if we go out of sync too much
            Vector3 vector = zdo.GetPosition();

            if (!ZoneSystem.instance.IsZoneLoaded(zdo.m_sector) ||
                // shortly after we teleport make sure that our Y position remains approximately the same as our zdo
                // to prevent falling through unloaded objects
                (player.m_teleportCooldown < 4f && Math.Abs(vector.y - __instance.m_body.position.y) > 0.1f))
            {
                __instance.m_body.position = vector;
            }
            else
            {
                var distance = (vector - __instance.transform.position).magnitude;
                var speed = __instance.GetVelocity().magnitude;

                var ping = ZNet.instance.GetServerRPC().m_averagePing;

                if (distance > Mathf.Clamp(ping * 16f + 4f, 4f, 10f))
                {
                    __instance.m_body.position = vector;
                }
            }

            return false;
        }
    }
}

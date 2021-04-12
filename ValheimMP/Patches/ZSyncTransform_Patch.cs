using HarmonyLib;
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
            if (ValheimMP.IsDedicated)
                return true;


            if (!__instance.m_nview.IsOwner())
            {
                // we do the rotation syncing for ships ourselves, we send quaternions with only 0.01 change so the original code would cause issues
                if (__instance.GetComponent<Ship>() != null)
                {
                    __instance.m_syncRotation = false;
                    Quaternion rotation2 = __instance.m_nview.m_zdo.GetRotation();
                    if (Quaternion.Angle(__instance.transform.rotation, rotation2) > 0.02f)
                    {
                        __instance.transform.rotation = Quaternion.Slerp(__instance.transform.rotation, rotation2, 0.01f);
                    }
                }
                    

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

            // if debug flying dont snap back
            if (player.m_debugFly)
                return false;

            // OwnerSync is reserved for the server, while Client sync normally syncs clients.
            // But since we simulate our own position we only need to resync if we go out of sync too much
            Vector3 vector = zdo.GetPosition();

            if (!player.IsTeleporting())
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

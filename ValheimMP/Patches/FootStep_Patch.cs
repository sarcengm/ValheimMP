using HarmonyLib;
using System;
using UnityEngine;

namespace ValheimMP.Patches
{
    // TODO: I removed replication from footsteps since these should be client side only effects, but that means I will need to make other clients simulate this as well, probably..
    [HarmonyPatch]
    internal class FootStep_Patch
    {
        [HarmonyPatch(typeof(FootStep), "OnLand")]
        [HarmonyPrefix]
        private static bool OnLand(ref FootStep __instance, Vector3 point)
        {
            if (__instance.m_nview.IsValid())
            {
                FootStep.GroundMaterial groundMaterial = __instance.GetGroundMaterial(__instance.m_character, point);
                int num = __instance.FindBestStepEffect(groundMaterial, FootStep.MotionType.Land);
                if (num != -1)
                {
                    __instance.RPC_Step(0, num, point);
                }
            }
            return false;
        }

        [HarmonyPatch(typeof(FootStep), "OnFoot", new Type[] { typeof(Transform) })]
        [HarmonyPrefix]
        private static bool OnFoot(ref FootStep __instance, Transform foot)
        {
            if (__instance.m_nview.IsValid())
            {
                Vector3 vector = ((foot != null) ? foot.position : __instance.transform.position);
                FootStep.MotionType motionType = __instance.GetMotionType(__instance.m_character);
                FootStep.GroundMaterial groundMaterial = __instance.GetGroundMaterial(__instance.m_character, vector);
                int num = __instance.FindBestStepEffect(groundMaterial, motionType);
                if (num != -1)
                {
                    __instance.RPC_Step(0, num, vector);
                }

            }

            return false;
        }
    }
}

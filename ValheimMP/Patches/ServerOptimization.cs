using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class ServerOptimization
    {
        [HarmonyPatch(typeof(EnemyHud), "LateUpdate")]
        [HarmonyPatch(typeof(Hud), "FixedUpdate")]
        [HarmonyPatch(typeof(ClutterSystem), "LateUpdate")]
        [HarmonyPatch(typeof(Character), "SetupContinousEffect")]
        [HarmonyPatch(typeof(CookingStation), "UpdateVisual")]
        [HarmonyPatch(typeof(DistantFogEmitter), "Update")]
        [HarmonyPatch(typeof(EnvMan), "SetParticleArrayEnabled")]
        [HarmonyPatch(typeof(Windmill), "Update")]
        [HarmonyPatch(typeof(MessageHud), "ShowMessage")]
        [HarmonyPatch(typeof(MessageHud), "Update")]
        [HarmonyPatch(typeof(MessageHud), "ShowBiomeFoundMsg")]
        [HarmonyPatch(typeof(MessageHud), "QueueUnlockMsg")]
        [HarmonyPatch(typeof(CookingStation), "UpdateVisual")]
        [HarmonyPatch(typeof(CookingStation), "SetSlotVisual")]
        [HarmonyPrefix]
        private static bool DoNothing()
        {
            return !ValheimMP.IsDedicated;
        }

        [HarmonyPatch(typeof(Game), "Start")]
        [HarmonyPostfix]
        private static void Game_Start(Game __instance)
        {
            //if (ValheimMP.IsDedicated)
            {
                // this thing takes an absolute insane amount of processing, better just redo it altogether.
                // Why would this have to be so hard anyway? :thinking:
                // Seems portals still work with this disabled, was it even needed?
                __instance.StopCoroutine("ConnectPortals");
            }
        }

        [HarmonyPatch(typeof(ItemStand), "Awake")]
        [HarmonyPostfix]
        private static void ItemStand_Awake(ItemStand __instance)
        {
            if (ValheimMP.IsDedicated)
            {
                // this thing takes an absolute insane amount of processing, better just redo it altogether.
                // Why would this have to be so hard anyway? :thinking:
                __instance.CancelInvoke("UpdateVisual");
            }
        }

        [HarmonyPatch(typeof(GlobalWind), "Start")]
        [HarmonyPostfix]
        private static void GlobalWind_Start(GlobalWind __instance)
        {
            if (ValheimMP.IsDedicated)
            {
                if (__instance.m_ps)
                    UnityEngine.Object.Destroy(__instance.m_ps);
                if (__instance.m_cloth)
                    UnityEngine.Object.Destroy(__instance.m_cloth);
                __instance.m_ps = null;
                __instance.m_cloth = null;
            }
        }

        [HarmonyPatch(typeof(EffectFade), "Awake")]
        [HarmonyPostfix]
        private static void EffectFade_Awake(EffectFade __instance)
        {
            if (ValheimMP.IsDedicated)
            {
                __instance.m_particles.Do(k => UnityEngine.Object.Destroy(k));
                __instance.m_particles = new ParticleSystem[] { };

                if (__instance.m_light)
                    UnityEngine.Object.Destroy(__instance.m_light);
                __instance.m_light = null;

                if (__instance.m_audioSource)
                    UnityEngine.Object.Destroy(__instance.m_audioSource);
                __instance.m_audioSource = null;
            }
        }

        [HarmonyPatch(typeof(ParticleDecal), "Awake")]
        [HarmonyPostfix]
        private static void ParticleDecal_Awake(ParticleDecal __instance)
        {
            if (ValheimMP.IsDedicated)
            {
                UnityEngine.Object.Destroy(__instance.part);
                __instance.part = null;
            }
        }
    }
}

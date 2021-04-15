using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using ValheimMP.Framework;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Character_Patch
    {

        //[HarmonyPatch(typeof(Character), "UpdateWalking")]
        //[HarmonyPrefix]
        //static void UpdateWalking(ref Character __instance, float dt)
        //{
        //    if (ZNet.instance.IsServer() && __instance.IsPlayer())
        //    {
        //        ValheimMP.Log("Post UpdateWalking Sleeping?: " + __instance.m_body.IsSleeping() + " velocity: " + __instance.m_body.velocity + " position: " + __instance.transform.position + " m_moveDir: " + __instance + ":" + __instance.m_moveDir);
        //    }
        //}

        [HarmonyPatch(typeof(Character), "Damage")]
        [HarmonyPrefix]
        private static bool Damage(Character __instance, HitData hit)
        {
            if (!ValheimMP.IsDedicated)
                return false;

            if (__instance is Player player)
            {
                if (!player.m_pvp && hit.m_attackerCharacter && hit.m_attackerCharacter is Player player2)
                {
                    if (ValheimMP.Instance.PlayerGroupManager.ArePlayersInTheSameGroup(player2.GetPlayerID(), player.GetPlayerID()))
                        return false;
                }

                player.m_delayedDamage.Enqueue(new KeyValuePair<float, HitData>(Time.time, hit));
            }
            else
            {
                __instance.RPC_Damage(0, hit);
            }
            return false;
        }

        private static bool Heal(Character __instance, float hp, bool showText = true)
        {
            if (!ValheimMP.IsDedicated)
                return false;

            __instance.RPC_Heal(0, hp, showText);
            return false;
        }

        [HarmonyPatch(typeof(Character), "ResetCloth")]
        [HarmonyPrefix]
        private static bool ResetCloth()
        {
            return ZNet.instance.IsServer();
        }

        [HarmonyPatch(typeof(Character), "RPC_Damage")]
        [HarmonyPrefix]
        private static bool RPC_Damage(ref Character __instance, long sender, HitData hit)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }

        [HarmonyPatch(typeof(Character), "RPC_Stagger")]
        [HarmonyPrefix]
        private static bool RPC_Stagger(ref Character __instance, long sender, Vector3 forceDirection)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }

        [HarmonyPatch(typeof(Character), "RPC_Heal")]
        [HarmonyPrefix]
        private static bool RPC_Heal(ref Character __instance, long sender, float hp, bool showText)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }

        [HarmonyPatch(typeof(Character), "RPC_AddNoise")]
        [HarmonyPrefix]
        private static bool RPC_AddNoise(ref Character __instance, long sender, float range)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }

        [HarmonyPatch(typeof(Character), "AddNoise")]
        [HarmonyPrefix]
        private static bool AddNoise(Character __instance)
        {
            return __instance.IsOwnerOrServer();
        }


        [HarmonyPatch(typeof(Character), "RPC_ResetCloth")]
        [HarmonyPrefix]
        private static bool RPC_ResetCloth(ref Character __instance, long sender)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }

        [HarmonyPatch(typeof(Character), "RPC_SetTamed")]
        [HarmonyPrefix]
        private static bool RPC_SetTamed(ref Character __instance, long sender, bool tamed)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }

        [HarmonyPatch(typeof(Character), "SyncVelocity")]
        [HarmonyPrefix]
        private static bool SyncVelocity()
        {
            // Sync velocity is nonsensical it adds a BodyVelocity vector to the zdo and constantly updates it, it is only used once by AI (but our AI only runs on the server)
            // and besides that the ZSyncTransform already has body_vel, so even if we were to delegate AI we should use that variable and not have two the same.
            return false;
        }
    }
}

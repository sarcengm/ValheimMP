using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class PrivateArea_Patch
    {
        [HarmonyPatch(typeof(PrivateArea), "FlashShield")]
        [HarmonyPrefix]
        private static bool FlashShield(PrivateArea __instance, bool flashConnected)
        {
            if (!__instance.m_flashAvailable)
            {
                return false;
            }

            __instance.m_flashAvailable = false;
            if (ZNet.instance.IsServer())
            {
                __instance.m_nview.InvokeRPC(ZNetView.Everybody, "FlashShield");

                if (!flashConnected)
                {
                    return false;
                }
                foreach (PrivateArea connectedArea in __instance.GetConnectedAreas())
                {
                    if (connectedArea.m_nview.IsValid())
                    {
                        connectedArea.m_nview.InvokeRPC(ZNetView.Everybody, "FlashShield");
                    }
                }
            }
            else
            {
                // basically a client side only flash, others wont see or hear it if it fails before reaching the server.
                __instance.RPC_FlashShield(0);
            }
            return false;
        }

        public static bool CheckAccess(long playerID, Vector3 point, float radius = 0f, bool flash = true)
        {
            bool flag = false;
            List<PrivateArea> list = new List<PrivateArea>();
            foreach (PrivateArea allArea in PrivateArea.m_allAreas)
            {
                if (allArea.IsEnabled() && allArea.IsInside(point, radius))
                {
                    var piece = allArea.GetComponent<Piece>();
                    if ((piece != null && piece.GetCreator() == playerID) || allArea.IsPermitted(playerID))
                    {
                        flag = true;
                    }
                    else
                    {
                        list.Add(allArea);

                    }

                    break;
                }
            }
            if (!flag && list.Count > 0)
            {
                if (flash)
                {
                    foreach (PrivateArea item in list)
                    {
                        item.FlashShield(flashConnected: false);
                    }
                }
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(Beehive), "RPC_Extract")]
        [HarmonyPrefix]
        private static bool RPC_Extract(Beehive __instance, long caller)
        {
            return CheckAccess(caller, __instance.transform.position);
        }

        [HarmonyPatch(typeof(Door), "RPC_UseDoor")]
        [HarmonyPrefix]
        private static bool RPC_UseDoor(Door __instance, long uid)
        {
            return CheckAccess(uid, __instance.transform.position);
        }


        [HarmonyPatch(typeof(WearNTear), "RPC_Damage", new Type[] { typeof(long), typeof(HitData) })]
        [HarmonyPatch(typeof(TreeBase), "RPC_Damage", new Type[] { typeof(long), typeof(HitData) })]
        [HarmonyPatch(typeof(TreeLog), "RPC_Damage", new Type[] { typeof(long), typeof(HitData) })]
        [HarmonyPatch(typeof(Destructible), "RPC_Damage", new Type[] { typeof(long), typeof(HitData) })]
        [HarmonyPatch(typeof(MineRock5), "RPC_Damage", new Type[] { typeof(long), typeof(HitData), typeof(int) })]
        [HarmonyPatch(typeof(MineRock), "RPC_Hit", new Type[] { typeof(long), typeof(HitData), typeof(int) })]
        [HarmonyPrefix]
        private static void HitProtectedAreas(long sender, HitData hit)
        {
            CheckHitProtection(hit);
        }

        [HarmonyPatch(typeof(Character), "RPC_Damage", new Type[] { typeof(long), typeof(HitData) })]
        [HarmonyPrefix]
        private static void HitProtectedAreas(Character __instance, long sender, HitData hit)
        {
            var player = __instance as Player;
            CheckHitProtection(hit, player == null ? 0L : player.GetPlayerID(), player == null);
        }

        private static void CheckHitProtection(HitData hit, long hitPlayerId = 0, bool victimMonster = false)
        {
            // don't protect players for environmental damage\fall damage and whatnot.
            if (!hit.m_attackerCharacter)
                return;

            var attackerMonster = hit.m_attackerCharacter is not Player;
            if (victimMonster && attackerMonster)
                return;

            var attacker = hit.GetAttackingPlayerID();
            if (CheckAccess(attacker, hit.m_point))
                return;

            if (hitPlayerId != 0 && !CheckAccess(hitPlayerId, hit.m_point, flash: false))
                return;

            var vmp = ValheimMP.Instance;
            float damageMul;
            float reflectMul;

            if (attacker == 0)
            {
                reflectMul = hitPlayerId == 0 ? vmp.WardMonsterReflectDamage.Value : vmp.WardMonsterVPlayerReflectDamage.Value;
                damageMul = hitPlayerId == 0 ? vmp.WardMonsterDamageMultiplier.Value : vmp.WardMonsterVPlayerDamageMultiplier.Value;
            }
            else
            {
                reflectMul = hitPlayerId == 0 ? vmp.WardPlayerReflectDamage.Value : vmp.WardPlayerVPlayerReflectDamage.Value;
                damageMul = hitPlayerId == 0 ? vmp.WardPlayerDamageMultiplier.Value : vmp.WardPlayerVPlayerDamageMultiplier.Value;
            }

            if (hit.m_attackerCharacter != null && reflectMul > 0f)
            {
                var reflectHit = new HitData();
                reflectHit.m_damage = hit.m_damage;
                reflectHit.m_point = hit.m_attackerCharacter.transform.position;
                reflectHit.m_staggerMultiplier = 100000f;
                reflectHit.m_damage.Modify(reflectMul);
                hit.m_attackerCharacter.Damage(reflectHit);
            }

            hit.m_damage.Modify(damageMul);
        }
    }
}

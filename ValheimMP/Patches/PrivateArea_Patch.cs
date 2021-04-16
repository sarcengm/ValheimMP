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
        [HarmonyPatch(typeof(PrivateArea), "Awake")]
        [HarmonyPostfix]
        private static void Awake(PrivateArea __instance)
        {
            if (ValheimMP.IsDedicated)
            {
                __instance.m_nview.Register("CycleAllowMode", (long sender) =>
                {
                    RPC_CycleAllowMode(__instance, sender);
                });
            }
        }

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

        private static void RPC_CycleAllowMode(PrivateArea __instance, long sender)
        {
            if (__instance.m_piece.GetCreator() == sender)
            {
                var allowMode = __instance.GetAllowMode();
                var vals = (PrivateAreaAllowMode[])Enum.GetValues(typeof(PrivateAreaAllowMode));

                for (int i = 0; i < vals.Length; i++)
                {
                    if (vals[i] == allowMode)
                    {
                        i++;
                        if (i >= vals.Length)
                            i = 0;
                        __instance.SetAllowMode(vals[i]);
                        return;
                    }
                }

                __instance.SetAllowMode(PrivateAreaAllowMode.Private);
            }
        }

        [HarmonyPatch(typeof(PrivateArea), "GetHoverText")]
        [HarmonyPostfix]
        private static void GetHoverText(PrivateArea __instance, ref string __result)
        {
            var allowMode = __instance.GetAllowMode();

            if (__instance.m_piece.IsCreator())
            {
                __result += Localization.instance.Localize($"\n[Shift+$KEY_Use] $vmp_allowMode $vmp_allowMode_{allowMode}");
            }
            else
            {
                __result += Localization.instance.Localize($"\n$vmp_allowMode $vmp_{allowMode}");
            }
        }

        [HarmonyPatch(typeof(PrivateArea), "Interact")]
        [HarmonyPrefix]
        private static bool Interact(PrivateArea __instance, ref bool __result)
        {
            if(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "CycleAllowMode");
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(PrivateArea), "IsPermitted")]
        [HarmonyPrefix]
        private static bool IsPermitted(PrivateArea __instance, ref bool __result, long playerID)
        {
            var playerId1 = playerID;
            var playerId2 = __instance.GetComponent<Piece>().GetCreator();

            var allowMode = __instance.GetAllowMode();
            if (allowMode == PrivateAreaAllowMode.Both)
            {
                if (ValheimMP.Instance.PlayerGroupManager.ArePlayersInTheSameGroup(playerId1, playerId2))
                {
                    __result = true;
                    return false;
                }
            }
            else if (allowMode == PrivateAreaAllowMode.Clan)
            {
                if (ValheimMP.Instance.PlayerGroupManager.ArePlayersInTheSameGroup(playerId1, playerId2, Framework.PlayerGroupType.Clan))
                {
                    __result = true;
                    return false;
                }
            }
            else if (allowMode == PrivateAreaAllowMode.Party)
            {
                if (ValheimMP.Instance.PlayerGroupManager.ArePlayersInTheSameGroup(playerId1, playerId2, Framework.PlayerGroupType.Party))
                {
                    __result = true;
                    return false;
                }
            }

            return true;
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
            // bosses are excempted from this, else wouldn't people just put bosses in indestructable cages?
            if (hit.m_attackerCharacter.m_faction == Character.Faction.Boss)
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

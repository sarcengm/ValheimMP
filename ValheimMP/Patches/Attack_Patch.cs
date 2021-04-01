using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Attack_Patch
    {

        [HarmonyPatch(typeof(Attack), "Update", new[] { typeof(float) })]
        [HarmonyPatch(typeof(Attack), "DoNonAttack", new Type[] { })]
        [HarmonyPatch(typeof(Attack), "DoAreaAttack", new Type[] { })]
        [HarmonyPatch(typeof(Attack), "DoMeleeAttack", new Type[] { })]
        [HarmonyPatch(typeof(Attack), "OnTrailStart", new Type[] { })]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> CreateNonOriginatorEffects(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(AccessTools.Method(typeof(EffectList), "Create", new[] { typeof(Vector3), typeof(Quaternion), typeof(Transform), typeof(float) })))
                {
                    list[i] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(EffectListExtension), "CreateNonOriginator", new[] { typeof(EffectList), typeof(Vector3), typeof(Quaternion), typeof(Transform), typeof(float), typeof(long) }));
                    list.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Attack), "m_character")),
                        new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Character), "GetOwner"))
                    });
                    i += 4;
                }
            }
            return list;
        }

        [HarmonyPatch(typeof(Attack), "DoMeleeAttack", new Type[] { })]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> AttackDamage(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(AccessTools.Method(typeof(IDestructible), "Damage", new[] { typeof(HitData) })))
                {
                    list[i] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Attack_Patch), "AttackDamage", new[] { typeof(IDestructible), typeof(HitData), typeof(Attack) }));
                    list.Insert(i, new CodeInstruction(OpCodes.Ldarg_0));
                    break;
                }
            }
            return list;
        }


        private static void AttackDamage(IDestructible destructible, HitData hitData, Attack attack)
        {
            if (attack.m_lastMeleeHits != null)
            {
                attack.m_lastMeleeHits.Add(hitData);
            }

            destructible.Damage(hitData);
        }

        [HarmonyPatch(typeof(Attack), "DoMeleeAttack")]
        [HarmonyPrefix]
        private static void DoMeleeAttack(Attack __instance)
        {
            if (__instance.m_character is Player player)
            {
                __instance.m_lastMeleeHitTime = Time.time;
                if (__instance.m_lastMeleeHits == null)
                    __instance.m_lastMeleeHits = new List<HitData>();
                __instance.m_lastMeleeHits.Clear();
            }
        }


        [HarmonyPatch(typeof(Attack), "DoMeleeAttack")]
        [HarmonyPostfix]
        private static void DoMeleeAttackPost(Attack __instance)
        {
            if (__instance.m_character is Player player)
            {
                if (!ValheimMPPlugin.IsDedicated)
                {
                    if (__instance.m_lastMeleeHits != null &&
                        __instance.m_lastMeleeHits.Count > 0)
                    {
                        var pkg = new ZPackage();
                        var hitCount = 0;
                        foreach (var item in __instance.m_lastMeleeHits)
                        {
                            if (item == null || item.m_hitCollider == null)
                                continue;
                            var nv = item.m_hitCollider.GetComponentInParent<ZNetView>();

                            if (nv != null && nv.m_zdo != null)
                            {
                                pkg.Write(nv.m_zdo.m_uid);
                                hitCount++;
                            }
                        }

                        if (hitCount > 0)
                        {
                            player.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "ClientMeleeHit", pkg);
                        }
                    }
                }
                else
                {
                    CompensateMeleeHits(__instance);
                }
            }
        }

        internal static void RPC_ClientMeleeHit(Player player, Attack attack, ZPackage pkg)
        {
            if (attack == null)
                return;
            if (player == null || player.IsDead())
                return;

            attack.m_lastClientMeleeHitTime = Time.time;
            if (attack.m_lastClientMeleeHits == null)
                attack.m_lastClientMeleeHits = new HashSet<ZDOID>();
            attack.m_lastClientMeleeHits.Clear();

            while (pkg.GetPos() < pkg.m_stream.Length)
            {
                attack.m_lastClientMeleeHits.Add(pkg.ReadZDOID());
            }

            if (attack.m_lastClientMeleeHits.Count > 10)
            {
                ZLog.LogWarning($"Discarding client hits with more then 10 hits: {attack.m_lastClientMeleeHits.Count} {player.GetPlayerName()} {player.GetPlayerID()}");

                // This is going to be one of those, I don't believe you! moments.
                attack.m_lastClientMeleeHitTime = 0f;
                attack.m_lastClientMeleeHits.Clear();
            }

            CompensateMeleeHits(attack);
        }

        private static void CompensateMeleeHits(Attack attack)
        {
            if (Mathf.Abs(attack.m_lastMeleeHitTime - attack.m_lastClientMeleeHitTime) < ValheimMPPlugin.Instance.ClientAttackCompensationWindow.Value)
            {
                attack.m_lastClientMeleeHitTime = 0f;
                var player = attack.m_character as Player;
                var peer = ZNet.instance.GetPeer(player.GetPlayerID());
                var vmp = ValheimMPPlugin.Instance;
                var clientCompDistance = Mathf.Clamp(vmp.ClientAttackCompensationDistance.Value * peer.GetPing(), 
                    vmp.ClientAttackCompensationDistanceMin.Value, 
                    vmp.ClientAttackCompensationDistanceMax.Value);

                clientCompDistance *= clientCompDistance;

                var maxHitDistance = clientCompDistance + (attack.m_attackRange * attack.m_attackRange);

                foreach (var item in attack.m_lastClientMeleeHits)
                {
                    var obj = ZNetScene.instance.FindInstance(item);
                    if (obj == null) continue;
                    var nv = obj.GetComponent<ZNetView>();
                    if (nv == null) continue;
                    var destr = obj.GetComponent<IDestructible>();
                    if (destr == null) continue;
                    var collider = obj.GetComponentInChildren<Collider>();
                    if (collider == null) continue;

                    if (attack.m_lastMeleeHits.SingleOrDefault(k => k.m_hitCollider?.GetComponentInParent<ZNetView>() == nv) == null)
                    {
                        if((collider.transform.position - attack.m_character.transform.position).sqrMagnitude < maxHitDistance)
                        {
                            AddWeaponDamage(attack, destr, collider, collider.transform.position);
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Attack), "ConsumeItem")]
        [HarmonyPrefix]
        private static bool ConsumeItem()
        {
            if (ZNet.instance.IsServer())
                return true;
            return false;
        }

        [HarmonyPatch(typeof(Attack), "UseAmmo")]
        [HarmonyPrefix]
        private static bool UseAmmo(Attack __instance, ref bool __result)
        {
            if (ZNet.instance.IsServer())
                return true;


            __instance.m_ammoItem = null;
            ItemDrop.ItemData itemData = null;
            if (!string.IsNullOrEmpty(__instance.m_weapon.m_shared.m_ammoType))
            {
                itemData = __instance.m_character.GetAmmoItem();
                if (itemData != null && (!__instance.m_character.GetInventory().ContainsItem(itemData) || itemData.m_shared.m_ammoType != __instance.m_weapon.m_shared.m_ammoType))
                {
                    itemData = null;
                }
                if (itemData == null)
                {
                    itemData = __instance.m_character.GetInventory().GetAmmoItem(__instance.m_weapon.m_shared.m_ammoType);
                }
                if (itemData == null)
                {
                    __instance.m_character.Message(MessageHud.MessageType.Center, "$msg_outof " + __instance.m_weapon.m_shared.m_ammoType);
                    __result = false;
                    return false;
                }

                __result = true;
                __instance.m_ammoItem = itemData;
                return true;
            }
            __result = true;
            return true;
        }

        [HarmonyPatch(typeof(Attack), "ProjectileAttackTriggered")]
        [HarmonyPrefix]
        private static bool ProjectileAttackTriggered()
        {
            if (ZNet.instance.IsServer())
                return true;
            return false;
        }

        [HarmonyPatch(typeof(Attack), "SpawnOnHitTerrain")]
        [HarmonyPrefix]
        private static bool SpawnOnHitTerrain()
        {
            if (ZNet.instance.IsServer())
                return true;
            return false;
        }

        [HarmonyPatch(typeof(Attack), "Start")]
        [HarmonyPostfix]
        private static void Start(Attack __instance)
        {
            if (__instance.m_character.IsPlayer() && ZNet.instance.IsServer())
            {
                __instance.m_character.transform.rotation = __instance.m_character.GetLookYaw();
                __instance.m_body.rotation = __instance.m_character.transform.rotation;
            }
        }


        private static void AddWeaponDamage(Attack __instance, IDestructible destructible, Collider hitCollider, Vector3 point)
        {
            DestructibleType destructibleType = destructible.GetDestructibleType();
            Skills.SkillType skillType = __instance.m_weapon.m_shared.m_skillType;
            if (__instance.m_specialHitSkill != 0 && (destructibleType & __instance.m_specialHitType) != 0)
            {
                skillType = __instance.m_specialHitSkill;
            }
            float num5 = __instance.m_character.GetRandomSkillFactor(skillType);
            if (__instance.m_lowerDamagePerHit && __instance.m_lastMeleeHits.Count > 1)
            {
                num5 /= (float)__instance.m_lastMeleeHits.Count * 0.75f;
            }
            HitData hitData = new HitData();
            hitData.m_toolTier = __instance.m_weapon.m_shared.m_toolTier;
            hitData.m_statusEffect = (__instance.m_weapon.m_shared.m_attackStatusEffect ? __instance.m_weapon.m_shared.m_attackStatusEffect.name : "");
            hitData.m_pushForce = __instance.m_weapon.m_shared.m_attackForce * num5 * __instance.m_forceMultiplier;
            hitData.m_backstabBonus = __instance.m_weapon.m_shared.m_backstabBonus;
            hitData.m_staggerMultiplier = __instance.m_staggerMultiplier;
            hitData.m_dodgeable = __instance.m_weapon.m_shared.m_dodgeable;
            hitData.m_blockable = __instance.m_weapon.m_shared.m_blockable;
            hitData.m_skill = skillType;
            hitData.m_damage = __instance.m_weapon.GetDamage();
            hitData.m_point = point;
            hitData.m_dir = (point - __instance.m_character.transform.position).normalized;
            hitData.m_hitCollider = hitCollider;
            hitData.SetAttacker(__instance.m_character);
            hitData.m_damage.Modify(__instance.m_damageMultiplier);
            hitData.m_damage.Modify(num5);
            hitData.m_damage.Modify(__instance.GetLevelDamageFactor());
            if (__instance.m_attackChainLevels > 1 && __instance.m_currentAttackCainLevel == __instance.m_attackChainLevels - 1)
            {
                hitData.m_damage.Modify(2f);
                hitData.m_pushForce *= 1.2f;
            }
            __instance.m_character.GetSEMan().ModifyAttack(skillType, ref hitData);
            destructible.Damage(hitData);
            __instance.m_lastMeleeHits.Add(hitData);
        }
    }
}

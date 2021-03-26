using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ValheimMP.Util;

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
                if (list[i].Calls(AccessTools.Method(typeof(EffectList), "Create", new [] { typeof(Vector3), typeof(Quaternion), typeof(Transform), typeof(float) })))
                {
                    list[i] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(EffectListExtension), "CreateNonOriginator", new[] { typeof(EffectList), typeof(Vector3), typeof(Quaternion), typeof(Transform), typeof(float), typeof(long) }));
                    list.InsertRange(i, new []
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
    }
}

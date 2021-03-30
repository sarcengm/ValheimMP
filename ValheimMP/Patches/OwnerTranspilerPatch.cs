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
    /// <summary>
    /// Collection of all patches that simply change Owner and Server checks.
    /// </summary>
    [HarmonyPatch]
    internal class OwnerTranspilerPatch
    {
        [HarmonyPatch(typeof(Character), "Awake", new Type[] { })]
        [HarmonyPatch(typeof(Character), "Heal", new Type[] { typeof(float), typeof(bool) })]
        [HarmonyPatch(typeof(Character), "Stagger", new Type[] { typeof(Vector3) })]
        [HarmonyPatch(typeof(Character), "SetHealth", new Type[] { typeof(float) })]
        [HarmonyPatch(typeof(Character), "GetNoiseRange", new Type[] { })]
        [HarmonyPatch(typeof(Character), "IsTamed", new Type[] { })]
        [HarmonyPatch(typeof(Humanoid), "SetupEquipment", new Type[] { })]
        [HarmonyPatch(typeof(Player), "UpdateAwake", new Type[] { typeof(float) })]
        [HarmonyPatch(typeof(Player), "IsPVPEnabled", new Type[] { })]
        [HarmonyPatch(typeof(Player), "UpdateEmote", new Type[] { })]
        [HarmonyPatch(typeof(Player), "GetBaseValue", new Type[] { })]
        [HarmonyPatch(typeof(ZSyncAnimation), "SetBool", new Type[] { typeof(int), typeof(bool) })]
        [HarmonyPatch(typeof(ZSyncAnimation), "SetFloat", new Type[] { typeof(int), typeof(float) })]
        [HarmonyPatch(typeof(ZSyncAnimation), "SetInt", new Type[] { typeof(int), typeof(int) })]
        [HarmonyPatch(typeof(Container), "UpdateUseVisual", new Type[] { })]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Replace_IsOwner_With_IsServer(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();

            // Replacing m_nview.IsOwner() with ZNet.instance.IsServer()
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(AccessTools.Method(typeof(ZNetView), "IsOwner")))
                {
                    // if m_nview were to be stored in a local or arg it would be there.
                    if (!list[i - 1].IsLdloc() && !list[i - 1].IsLdarg())
                        list[i - 2].opcode = OpCodes.Nop; // if not then here is the this pointer, nop it!
                    list[i - 1] = new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(ZNet), "instance"));
                    list[i - 0] = new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ZNet), "IsServer"));
                }
            }
            return list;
        }

        [HarmonyPatch(typeof(Character), "AddNoise", new Type[] { typeof(float) })]
        [HarmonyPatch(typeof(Character), "FixedUpdate", new Type[] { })]
        [HarmonyPatch(typeof(Character), "UpdateLayer", new Type[] { })]
        [HarmonyPatch(typeof(Character), "UpdateGroundTilt", new Type[] { typeof(float) })]
        [HarmonyPatch(typeof(Character), "RPC_Damage", new Type[] { typeof(long), typeof(HitData) })]
        [HarmonyPatch(typeof(Character), "RPC_Heal", new Type[] { typeof(long), typeof(float), typeof(bool) })]
        [HarmonyPatch(typeof(Character), "OnCollisionStay", new Type[] { typeof(Collision) })]
        [HarmonyPatch(typeof(Character), "OnAutoJump", new Type[] { typeof(Vector3), typeof(float), typeof(float) })]
        [HarmonyPatch(typeof(Character), "UpdateWater", new Type[] { typeof(float) })]
        [HarmonyPatch(typeof(Character), "RPC_AddNoise", new Type[] { typeof(long), typeof(float) })]
        [HarmonyPatch(typeof(Character), "GetVelocity", new Type[] { })]
        [HarmonyPatch(typeof(Character), "RPC_SetTamed", new Type[] { typeof(long), typeof(bool) })]
        [HarmonyPatch(typeof(Humanoid), "FixedUpdate", new Type[] { })]
        [HarmonyPatch(typeof(Humanoid), "OnAttackTrigger", new Type[] { })]
        [HarmonyPatch(typeof(Humanoid), "IsBlocking", new Type[] { })]
        [HarmonyPatch(typeof(Player), "UseStamina", new Type[] { typeof(float) })]
        [HarmonyPatch(typeof(Player), "HaveStamina", new Type[] { typeof(float) })]
        [HarmonyPatch(typeof(Player), "InDodge", new Type[] { })]
        [HarmonyPatch(typeof(Player), "GetStealthFactor", new Type[] { })]
        [HarmonyPatch(typeof(CharacterAnimEvent), "OnAnimatorMove", new Type[] { })]
        [HarmonyPatch(typeof(CharacterAnimEvent), "UpdateHeadRotation", new Type[] { typeof(float) })]
        [HarmonyPatch(typeof(SEMan), "HaveStatusAttribute", new Type[] { typeof(StatusEffect.StatusAttribute) })]
        [HarmonyPatch(typeof(Floating), "TerrainCheck", new Type[] { })]
        [HarmonyPatch(typeof(Floating), "FixedUpdate", new Type[] { })]
        [HarmonyPatch(typeof(Ship), "UpdateControlls", new Type[] { typeof(float) })]
        [HarmonyPatch(typeof(ZSyncAnimation), "SyncParameters", new Type[] { })]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Replace_IsOwner_With_IsOwnerOrServer(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();

            // Replacing m_nview.IsOwner() with m_nview.IsOwnerOrServer() (TranspilerUtil.IsOwnerOrServer extension)
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(AccessTools.Method(typeof(ZNetView), "IsOwner")))
                {
                    list[i].operand = AccessTools.Method(typeof(TranspilerUtilExtension), "IsOwnerOrServer", new Type[] { typeof(ZNetView) });
                }
            }
            return list;
        }

        [HarmonyPatch(typeof(WaterVolume), "UpdateFloaters", new Type[] { })]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Replace_IWaterInteractable_IsOwner_With_IsOwnerOrServer(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(AccessTools.Method(typeof(IWaterInteractable), "IsOwner")))
                {
                    list[i].operand = AccessTools.Method(typeof(TranspilerUtilExtension), "IsOwnerOrServer", new Type[] { typeof(IWaterInteractable) });
                }
            }
            return list;
        }

        
        [HarmonyPatch(typeof(EffectArea), "OnTriggerStay", new Type[] { typeof(Collider) })]
        [HarmonyPatch(typeof(Aoe), "OnHit", new Type[] { typeof(Collider), typeof(Vector3) })]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Replace_Character_IsOwner_With_IsOwnerOrServer(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(AccessTools.Method(typeof(Character), "IsOwner")))
                {
                    list[i].operand = AccessTools.Method(typeof(TranspilerUtilExtension), "IsOwnerOrServer", new Type[] { typeof(Character) });
                }
            }
            return list;
        }

        // ZSyncTransform as only object in the world directly checks the ZDO instead of the netview.
        [HarmonyPatch(typeof(ZSyncTransform), "OwnerSync")]
        [HarmonyPatch(typeof(ZSyncTransform), "ClientSync")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Replace_ZDOIsOwner_With_IsServer(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(AccessTools.Method(typeof(ZDO), "IsOwner")))
                {
                    //list[i].operand = AccessTools.Method(typeof(TranspilerUtil), "IsOwnerOrServer", new Type[] { typeof(ZDO) });
                    //list[i - 2].opcode = OpCodes.Nop;
                    list[i - 1] = new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(ZNet), "instance"));
                    list[i - 0] = new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ZNet), "IsServer"));
                }
            }
            return list;
        }
    }
}

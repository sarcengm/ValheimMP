using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    class EffectList_Patch
    {

        [HarmonyPatch(typeof(WearNTear), "RPC_Damage", new Type[] { typeof(long), typeof(HitData) })]
        [HarmonyPatch(typeof(TreeBase), "RPC_Damage", new Type[] { typeof(long), typeof(HitData) })]
        [HarmonyPatch(typeof(TreeLog), "RPC_Damage", new Type[] { typeof(long), typeof(HitData) })]
        [HarmonyPatch(typeof(Destructible), "RPC_Damage", new Type[] { typeof(long), typeof(HitData) })]
        [HarmonyPatch(typeof(MineRock5), "DamageArea", new Type[] { typeof(int), typeof(HitData) })]
        [HarmonyPatch(typeof(MineRock), "RPC_Hit", new Type[] { typeof(long), typeof(HitData), typeof(int) })]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> CreateNonOriginatorHitEffects(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(AccessTools.Method(typeof(EffectList), "Create", new[] { typeof(Vector3), typeof(Quaternion), typeof(Transform), typeof(float) })))
                {
                    list[i] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(EffectListExtension), "CreateNonOriginator", new[] { typeof(EffectList), typeof(Vector3), typeof(Quaternion), typeof(Transform), typeof(float), typeof(long) }));
                    list.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_2),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HitDataExtension),"GetAttackingPlayerID")),
                    });
                    break;
                }
            }
            return list;
        }
    }
}

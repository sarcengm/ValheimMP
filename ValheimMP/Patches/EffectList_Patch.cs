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

    public static class EffectListExtension
    {
        public static GameObject[] CreateNonOriginator(this EffectList effectList, Vector3 pos, Quaternion rot, Transform parent = null, float scale = 1f, long originator = 0)
        {
            var gameObjects = effectList.Create(pos, rot, parent, scale);
            for (int i = 0; i < gameObjects.Length; i++)
            {
                var netView = gameObjects[i].GetComponent<ZNetView>();
                if (netView != null && netView.m_zdo != null && !netView.m_persistent)
                {
                    netView.m_zdo.SetZDOType(ZDOType.AllExceptOriginator);
                    netView.m_zdo.m_originator = originator;
                }
            }

            return gameObjects;
        }
    }
}

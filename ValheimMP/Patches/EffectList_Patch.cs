using System;
using System.Collections.Generic;
using System.Linq;
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

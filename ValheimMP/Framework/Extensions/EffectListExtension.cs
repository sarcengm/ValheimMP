using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ValheimMP.Framework.Extensions
{
    public static class EffectListExtension
    {

        /// <summary>
        /// Create an effect that is not send to the originator. 
        /// This is useful for all effects that are simulated on the client side by the instigator. 
        /// They have no use for a delayed repeated effected.
        /// </summary>
        /// <param name="effectList"></param>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        /// <param name="parent"></param>
        /// <param name="scale"></param>
        /// <param name="originator"></param>
        /// <returns></returns>
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

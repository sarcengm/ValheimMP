using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class CreatureSpawner_Patch
    {
        [HarmonyPatch(typeof(CreatureSpawner), "Awake")]
        [HarmonyPrefix]
        private static bool Awake(ref CreatureSpawner __instance)
        {
            __instance.GetComponent<ZNetView>().m_type = (ZDO.ObjectType)(-1);

            if (!ZNet.instance.IsServer())
            {
                //DebugMod.LogComponent(__instance, "CreatureSpawner on non-server.");
                UnityEngine.Object.Destroy(__instance);

                return false;
            }

            return true;
        }
    }
}

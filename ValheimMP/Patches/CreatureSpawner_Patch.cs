using HarmonyLib;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class CreatureSpawner_Patch
    {
        [HarmonyPatch(typeof(CreatureSpawner), "Awake")]
        [HarmonyPrefix]
        private static bool Awake(ref CreatureSpawner __instance)
        {
            var nview = __instance.GetComponent<ZNetView>();
            if(nview) nview.m_type = (ZDO.ObjectType)(-1);

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

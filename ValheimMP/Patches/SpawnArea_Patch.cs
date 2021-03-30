using HarmonyLib;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class SpawnArea_Patch
    {
        [HarmonyPatch(typeof(SpawnArea), "Awake")]
        [HarmonyPrefix]
        private static bool Awake(ref SpawnArea __instance)
        {
            __instance.GetComponent<ZNetView>().m_type = (ZDO.ObjectType)(-1);

            if (!ZNet.instance.IsServer())
            {
                //DebugMod.LogComponent(__instance, "SpawnArea on non-server.");
                UnityEngine.Object.Destroy(__instance);

                return false;
            }

            return true;
        }
    }

}

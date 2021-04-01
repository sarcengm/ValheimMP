using HarmonyLib;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Vector2i_Patch
    {

        /// <summary>
        /// The original method generated a million of collisions because most numbers are fairly low, and 1,0 would be equal to 0,1 and so on.
        /// </summary>
        /// <param name="v"></param>
        /// <param name="__result"></param>
        /// <returns></returns>

        [HarmonyPatch(typeof(Vector2i), "GetHashCode")]
        [HarmonyPrefix]
        private static bool GetHashCode(ref Vector2i __instance, ref int __result)
        {
            __result = (__instance.x << 16) ^ __instance.y;
            return false;
        }
    }
}

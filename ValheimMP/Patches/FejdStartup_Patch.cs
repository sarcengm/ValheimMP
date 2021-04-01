using HarmonyLib;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class FejdStartup_Patch
    {
        internal static string m_beardItem;
        internal static string m_hairItem;
        internal static Vector3 m_hairColor;
        internal static Vector3 m_skinColor;
        internal static int m_modelIndex;

        internal static bool IsPublicPasswordValid(FejdStartup __instance, ref bool __result, string password, World world)
        {
            __result = true;
            return false;
        }

        internal static void Awake(FejdStartup __instance)
        {
            ValheimMPPlugin.Instance.SetIsOnValheimMPServer(false);
        }

        [HarmonyPatch(typeof(FejdStartup), "SetupCharacterPreview")]
        [HarmonyPostfix]
        private static void SetupCharacterPreview(FejdStartup __instance)
        {
            var player = __instance.m_playerInstance?.GetComponent<Player>();
            if(player != null)
            {
                m_beardItem = player.m_beardItem;
                m_hairItem = player.m_hairItem;
                m_hairColor = player.m_hairColor;
                m_skinColor = player.m_skinColor;
                m_modelIndex = player.m_modelIndex;
            }
        }
    }
}

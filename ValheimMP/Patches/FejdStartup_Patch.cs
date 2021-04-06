using HarmonyLib;
using System.Linq;
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

        internal static void Awake()
        {
            ValheimMP.Instance.SetIsOnValheimMPServer(false);
        }

        internal static void AwakePost()
        {
            if (ValheimMP.IsDedicated)
            {

            }
        }

        internal static void SetupCharacterPreview(FejdStartup __instance)
        {
            var player = __instance.m_playerInstance.GetComponent<VisEquipment>();
            if (player != null)
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

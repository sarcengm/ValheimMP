using HarmonyLib;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class TombStone_Patch
    {
        [HarmonyPatch(typeof(TombStone), "Awake")]
        [HarmonyPostfix]
        private static void Awake(TombStone __instance)
        {
            // One one didn't support parameters, making it impossible to determine who took the content, disable it and use our own.
            __instance.m_container.m_onTakeAllSuccess = null;
            // Here is our new and improved onTakeAllSuccess2! With 100% more parameters!
            __instance.m_container.m_onTakeAllSuccess2 += OnTakeAllSuccess;
        }

        private static void OnTakeAllSuccess(Humanoid humanoid)
        {
            Player localPlayer = humanoid as Player;
            if ((bool)localPlayer)
            {
                localPlayer.m_pickupEffects.Create(localPlayer.transform.position, Quaternion.identity);
                localPlayer.Message(MessageHud.MessageType.Center, "$piece_tombstone_recovered");
            }
        }
    }
}

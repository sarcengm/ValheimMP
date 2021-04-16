using HarmonyLib;
using Steamworks;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class ZSteamMatchmaking_Patch
    {
        internal static bool HasConnected { get; set; }

        [HarmonyPatch(typeof(ZSteamMatchmaking), "OnSteamServersConnected")]
        [HarmonyPrefix]
        private static void OnSteamServersConnected(SteamServersConnected_t data)
        {
            ZLog.Log("OnSteamServersConnected " + SteamGameServer.GetSteamID());
            HasConnected = true;
        }
    }
}

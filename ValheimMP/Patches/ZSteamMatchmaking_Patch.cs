using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    public class ZSteamMatchmaking_Patch
    {
        public static bool HasConnected { get; internal set; }

        [HarmonyPatch(typeof(ZSteamMatchmaking), "OnSteamServersConnected")]
        [HarmonyPrefix]
        private static void OnSteamServersConnected(SteamServersConnected_t data)
        {
            ZLog.Log("OnSteamServersConnected " + SteamGameServer.GetSteamID());
            HasConnected = true;
        }
    }
}

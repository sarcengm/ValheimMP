using HarmonyLib;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Sign_Patch
    {

        [HarmonyPatch(typeof(Sign), "Awake")]
        [HarmonyPostfix]
        private static void Awake(Sign __instance)
        {
            if (ZNet.instance.IsServer())
            {
                __instance.m_nview.Register("SetText", (long sender, string text) =>
                {
                    RPC_SetText(__instance, sender, text);
                });

                instance.CancelInvoke("UpdateText");
            }
        }

        private static void RPC_SetText(Sign __instance, long sender, string text)
        {
            if (!PrivateArea_Patch.CheckAccess(sender, __instance.transform.position))
                return;

            __instance.m_nview.GetZDO().Set("text", text);
        }

        [HarmonyPatch(typeof(Sign), "SetText")]
        [HarmonyPrefix]
        private static bool SetText(Sign __instance, string text)
        {
            if (PrivateArea.CheckAccess(__instance.transform.position))
            {
                __instance.m_nview.InvokeRPC("SetText", text);
            }

            return false;
        }
    }
}

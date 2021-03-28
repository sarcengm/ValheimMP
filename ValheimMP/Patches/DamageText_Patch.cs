using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static DamageText;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class DamageText_Patch
    {
        [HarmonyPatch(typeof(DamageText), "RPC_DamageText")]
        [HarmonyPrefix]
        private static bool RPC_DamageText(ref DamageText __instance, long sender, ZPackage pkg)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }

        [HarmonyPatch(typeof(DamageText), "ShowText", new[] { typeof(TextType), typeof(Vector3), typeof(float), typeof(bool) })]
        [HarmonyPrefix]
        private static bool ShowText(DamageText __instance, TextType type, Vector3 pos, float dmg, bool player = false)
        {
            ZPackage zPackage = new ZPackage();
            zPackage.Write((int)type);
            zPackage.Write(pos);
            zPackage.Write(dmg);
            zPackage.Write(player);
            if (ZNet.instance.IsServer())
            {
                ZRoutedRpc.instance.InvokeProximityRoutedRPC(pos, 100f, ZRoutedRpc.Everybody, ZDOID.None, "DamageText", zPackage);
            }
            else
            {
                zPackage.SetPos(0);
                __instance.RPC_DamageText(0, zPackage);
            }
            return false;
        }
    }
}

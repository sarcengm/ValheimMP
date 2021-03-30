using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class PlayerController_Patch
    {
        [HarmonyPatch(typeof(PlayerController), "FixedUpdate")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> FixedUpdate(IEnumerable<CodeInstruction> instructions)
        {

            // removes the isowner check and replaces it with true
            var list = instructions.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(AccessTools.Method(typeof(ZNetView), "IsOwner")))
                {
                    list[i - 2].opcode = System.Reflection.Emit.OpCodes.Nop;
                    list[i - 1].opcode = System.Reflection.Emit.OpCodes.Nop;
                    list[i].opcode = System.Reflection.Emit.OpCodes.Ldc_I4_1;
                }
            }
            return list.AsEnumerable();
        }

        [HarmonyPatch(typeof(PlayerController), "FixedUpdate")]
        [HarmonyPrefix]
        private static bool FixedUpdate()
        {
            if (ZNet.instance != null && ZNet.instance.IsServer())
                return false;
            return true;
        }

        [HarmonyPatch(typeof(PlayerController), "LateUpdate")]
        [HarmonyPrefix]
        private static bool LateUpdate()
        {
            if (ZNet.instance != null && ZNet.instance.IsServer())
                return false;
            return true;
        }
    }
}

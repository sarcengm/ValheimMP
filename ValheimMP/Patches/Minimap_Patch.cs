using HarmonyLib;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Minimap_Patch
    {
        // If I ever decided to network the mapdata again this should be re-enabled so it doesn't load local map data before synced with server
        //[HarmonyPatch(typeof(Minimap), "Update")]
        //[HarmonyTranspiler]
        //private static IEnumerable<CodeInstruction> Update(IEnumerable<CodeInstruction> instructions)
        //{
        //    var list = instructions.ToList();
        //    for (var i = 0; i < list.Count; i++)
        //    {
        //        if (list[i].Calls(AccessTools.Method(typeof(Minimap), "LoadMapData")))
        //        {
        //            list[i - 1].opcode = System.Reflection.Emit.OpCodes.Nop;
        //            list[i].opcode = System.Reflection.Emit.OpCodes.Nop;
        //        }
        //    }
        //    return list.AsEnumerable();
        //}

    }
}

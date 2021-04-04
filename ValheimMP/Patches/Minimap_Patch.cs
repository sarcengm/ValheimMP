using HarmonyLib;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Minimap_Patch
    {
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

        [HarmonyPatch(typeof(Minimap), "GenerateWorldMap")]
        [HarmonyPrefix]
        private static bool GenerateWorldMap(Minimap __instance)
        {
            int num = __instance.m_textureSize / 2;
            float num2 = __instance.m_pixelSize / 2f;
            Color32[] array = new Color32[__instance.m_textureSize * __instance.m_textureSize];
            Color32[] array2 = new Color32[__instance.m_textureSize * __instance.m_textureSize];
            Color[] array3 = new Color[__instance.m_textureSize * __instance.m_textureSize];
            for (int i = 0; i < __instance.m_textureSize; i++)
            {
                for (int j = 0; j < __instance.m_textureSize; j++)
                {
                    float wx = (float)(j - num) * __instance.m_pixelSize + num2;
                    float wy = (float)(i - num) * __instance.m_pixelSize + num2;
                    Heightmap.Biome biome = WorldGenerator.instance.GetBiome(wx, wy);
                    float biomeHeight = WorldGenerator.instance.GetBiomeHeight(biome, wx, wy);
                    array[i * __instance.m_textureSize + j] = __instance.GetPixelColor(biome);
                    array2[i * __instance.m_textureSize + j] = __instance.GetMaskColor(wx, wy, biomeHeight, biome);
                    array3[i * __instance.m_textureSize + j] = GetPixelColor(__instance, wx, wy, biomeHeight, biome);
                }
            }
            __instance.m_forestMaskTexture.SetPixels32(array2);
            __instance.m_forestMaskTexture.Apply();
            __instance.m_mapTexture.SetPixels32(array);
            __instance.m_mapTexture.Apply();
            __instance.m_heightTexture.SetPixels(array3);
            __instance.m_heightTexture.Apply();
            return false;
        }

        private static Color GetPixelColor(Minimap minimap, float wx, float wy, float biomeHeight, Heightmap.Biome biome)
        {
            var distSqr = ValheimMP.Instance.ForcedPVPDistanceFromCenter.Value;
            distSqr *= distSqr;
            var biomeDistSqr = ValheimMP.Instance.ForcedPVPDistanceForBiomesOnly.Value;
            biomeDistSqr *= biomeDistSqr;
            var distCenterSqr = wx * wx + wy * wy;

            var color = new Color(biomeHeight, 0f, 0f);

            if (distCenterSqr > distSqr || (distCenterSqr > biomeDistSqr &&
                ValheimMP.Instance.ForcedPVPBiomes.TryGetValue(biome, out var val) && val.Value))
            {
                color += new Color(0f, 1f, 1f);
            }

            return color;
        }
    }
}

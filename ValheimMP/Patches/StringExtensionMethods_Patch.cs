﻿using HarmonyLib;
using System.Collections.Generic;

#if DEBUG

namespace ValheimMP.Patches
{

    [HarmonyPatch]
    internal class StringExtensionMethods_Patch
    {
        private static Dictionary<int, string> stablehashNames = new Dictionary<int, string>();
        private static Dictionary<int, string> stablehashNamesAnim = new Dictionary<int, string>();
        private static Dictionary<string, int> stablehashLookup = new Dictionary<string, int>();
        private static Dictionary<string, int> stablehashLookupAnim = new Dictionary<string, int>();

        [HarmonyPatch(typeof(StringExtensionMethods), "GetStableHashCode")]
        [HarmonyPrefix]
        public static bool GetStableHashCode(string str, ref int __result)
        {
            if (stablehashLookup.TryGetValue(str, out __result))
            {
                return false;
            }

            /////////////////////////////////////////////////////////////////
            /// COPY PASTA THE ORIGINAL, ReversePatch wasn't working, 
            /// cant be bothered to figure out why
            int num = 5381;
            int num2 = num;
            for (int i = 0; i < str.Length && str[i] != 0; i += 2)
            {
                num = ((num << 5) + num) ^ str[i];
                if (i == str.Length - 1 || str[i + 1] == '\0')
                {
                    break;
                }
                num2 = ((num2 << 5) + num2) ^ str[i + 1];
            }
            __result = num + num2 * 1566083941;
            /////////////////////////////////////////////////////////////////

            stablehashNames[__result] = str;
            stablehashLookup[str] = __result;
            //ValheimMP.Log("StablehashCode: " + str + ": " + __result);
            return false;
        }

        [HarmonyPatch(typeof(ZSyncAnimation), "GetHash")]
        [HarmonyPostfix]
        public static void GetAnimHash(string name, ref int __result)
        {
            stablehashNamesAnim[__result] = name;
            stablehashLookupAnim[name] = __result;
        }

        public static string GetStableHashName(int code)
        {
            string str;
            if (stablehashNames.TryGetValue(code, out str))
            {
                return str;
            }

            if(stablehashNamesAnim.TryGetValue(code - 438569, out str))
            {
                return str + $" (A)";
            }

            return code.ToString();
        }
    }
}

#endif

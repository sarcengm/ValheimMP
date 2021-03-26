using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Patches
{
    internal class ZPackage_Patch
    {
        [HarmonyPatch(typeof(ZPackage), "ReadZDOID")]
        [HarmonyPrefix]
        private static bool ReadZDOID(ref ZPackage __instance, ref ZDOID __result)
        {
            __result = new ZDOID(ZNet_Patch.GetServerID(), __instance.m_reader.ReadUInt32());
            return false;
        }

        [HarmonyPatch(typeof(ZPackage), "Write", new Type[] { typeof(ZDOID) })]
        [HarmonyPrefix]
        private static bool Write(ref ZPackage __instance, ZDOID id)
        {
            if (id.userID != ZNet_Patch.GetServerID())
                ZLog.Log($"Write ZDOID with id.userId: {id.userID}");
            __instance.m_writer.Write(id.id);
            return false;
        }


        // 1 MB, should be more then enough for anything?
        private static uint max_array_size = 1024 * 1024;
        [HarmonyPatch(typeof(ZPackage), "ReadPackage")]
        [HarmonyPrefix]
        private static bool ReadPackage(ref ZPackage __instance, ref ZPackage __result)
        {
            int count = __instance.m_reader.ReadInt32();

            if ((uint)count > max_array_size)
            {
                ZLog.LogError("ZPackage::ReadPackage() count too large: " + count);
                return false;
            }

            __result = new ZPackage(__instance.m_reader.ReadBytes(count));
            return false;
        }

        [HarmonyPatch(typeof(ZPackage), "ReadPackage")]
        [HarmonyPrefix]
        private static bool ReadPackage2(ref ZPackage __instance, ref ZPackage pkg)
        {
            int count = __instance.m_reader.ReadInt32();

            if ((uint)count > max_array_size)
            {
                ZLog.LogError("ZPackage::ReadPackage() count too large: " + count);
                return false;
            }

            byte[] array = __instance.m_reader.ReadBytes(count);
            pkg.Clear();
            pkg.m_stream.Write(array, 0, array.Length);
            pkg.m_stream.Position = 0L;
            return false;
        }

        [HarmonyPatch(typeof(ZPackage), "ReadByteArray")]
        [HarmonyPrefix]
        private static bool ReadByteArray(ref ZPackage __instance, ref byte[] __result)
        {
            int count = __instance.m_reader.ReadInt32();

            if ((uint)count > max_array_size)
            {
                ZLog.LogError("ZPackage::ReadByteArray() count too large: " + count);
                return false;
            }

            __result = __instance.m_reader.ReadBytes(count);
            return false;
        }
    }
}

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ValheimMP.Util;

namespace ValheimMP.Patches
{

    public enum ZDOType
    {
        /// <summary>
        /// Normal will always be replicated to everyone (if changed)
        /// </summary>
        Normal = 0,
        /// <summary>
        /// Private will only be replicated to the client that owns the object (usually only player characters are owned)
        /// </summary>
        Private = 1,
        /// <summary>
        /// Ignored will never be replicated.
        /// </summary>
        Ignored = 2,
        /// <summary>
        /// AllExceptOriginator, will be replicated to all clients except the client that instigated the creation of this object (used for effects, the owning client does not need another copy that is already dated!)
        /// </summary>
        AllExceptOriginator = 3
    }

    public enum ZDOFieldType
    {
        /// <summary>
        /// Normal fields, these are all fields that are unspecified, will always be replicated to everyone (if changed)
        /// </summary>
        Normal = 0,
        /// <summary>
        /// Private fields, will only be replicated to the client that owns the object (usually only player characters are owned)
        /// </summary>
        Private = 1,
        /// <summary>
        /// Ignored fields, will never be replicated.
        /// </summary>
        Ignored = 2,
        /// <summary>
        /// AllExceptOwner, will be replicated to all clients except the client who owns this object.
        /// </summary>
        AllExceptOwner = 3
    }

    [HarmonyPatch]
    public class ZDO_Patch
    {
        [HarmonyPatch(typeof(ZDO), MethodType.Constructor)]
        [HarmonyPostfix]
        private static void Constructor(ref ZDO __instance)
        {
            // Similar to field type except for the entire zdo
            __instance.m_zdoType = 0; 
            // Private variables are those that are not replicated to non owning clients
            __instance.m_fieldTypes = new Dictionary<int, int>();
            // Event that gets fired when a specific ZDO element is updated.
            __instance.m_zdoEvents = new Dictionary<int, Action<ZDO>>();
        }

        [HarmonyPatch(typeof(ZDO), "Reset")]
        [HarmonyPostfix]
        private static void Reset(ref ZDO __instance)
        {
            __instance.m_zdoType = 0;
            __instance.m_fieldTypes.Clear();
            __instance.m_zdoEvents.Clear();
        }

        [HarmonyPatch(typeof(ZDO), "SetOwner")]
        [HarmonyPrefix]
        private static bool SetOwner(ref ZDO __instance, long uid)
        {
            if (!ZNet.instance.IsServer())
            {
                ZLog.LogWarning("ZDO.SetOwner on client");
                ZLog.Log(new System.Diagnostics.StackTrace().ToString());
                return false;
            }

            if (uid != ZDOMan.instance.GetMyID() && ZNet.instance.IsServer())
            {
                if (uid != 0)
                {
                    if (__instance.m_nview == null || __instance.m_nview.GetComponent<Player>() == null)
                    {
                        ZLog.LogWarning("SetOwner to non server! " + uid + " mine: " + ZDOMan.instance.GetMyID());
                        ZLog.Log(new System.Diagnostics.StackTrace().ToString());
                    }
                }
                //uid = ZDOMan.instance.GetMyID();
            }

            if (__instance.m_owner != uid)
            {
                __instance.m_owner = uid;
                __instance.IncreseOwnerRevision();
            }
            return false;
        }

        [HarmonyPatch(typeof(ZDO), "IncreseDataRevision")]
        [HarmonyPrefix]
        private static void IncreseDataRevision(ref ZDO __instance)
        {
            if (ZNet.instance.IsServer() && __instance.m_type == ZDO.ObjectType.Solid && __instance.m_nview != null)
            {
                foreach (var peer in ZNet.instance.m_peers)
                {
                    if (peer.m_loadedSectors.ContainsKey(__instance.m_sector))
                    {
                        peer.m_solidObjectQueue[__instance.m_uid] = __instance;
                    }
                }
            }
        }


        [HarmonyPatch(typeof(ZDO), "SetSector")]
        [HarmonyPrefix]
        private static bool SetSector(ref ZDO __instance, Vector2i sector)
        {
            if (!(__instance.m_sector == sector))
            {
                LivingSectorObjects.RemoveObject(__instance);
                __instance.m_zdoMan.RemoveFromSector(__instance, __instance.m_sector);
                __instance.m_sector = sector;

                LivingSectorObjects.AddObject(__instance);
                __instance.m_zdoMan.AddToSector(__instance, __instance.m_sector);
                __instance.m_zdoMan.ZDOSectorInvalidated(__instance);
            }

            return false;
        }

#if DEBUG
        [HarmonyPatch(typeof(ZDO), "Print")]
        [HarmonyPrefix]
        private static bool Print(ref ZDO __instance)
        {
            foreach (var item in __instance.m_floats)
            {
                ZLog.Log(StringExtensionMethods_Patch.GetStableHashName(item.Key) + ": " + item.Value);
            }
            foreach (var item in __instance.m_ints)
            {
                ZLog.Log(StringExtensionMethods_Patch.GetStableHashName(item.Key) + ": " + item.Value);
            }
            foreach (var item in __instance.m_longs)
            {
                ZLog.Log(StringExtensionMethods_Patch.GetStableHashName(item.Key) + ": " + item.Value);
            }
            foreach (var item in __instance.m_quats)
            {
                ZLog.Log(StringExtensionMethods_Patch.GetStableHashName(item.Key) + ": " + item.Value);
            }
            foreach (var item in __instance.m_vec3)
            {
                ZLog.Log(StringExtensionMethods_Patch.GetStableHashName(item.Key) + ": " + item.Value);
            }
            foreach (var item in __instance.m_strings)
            {
                ZLog.Log(StringExtensionMethods_Patch.GetStableHashName(item.Key) + ": " + item.Value);
            }
            return false;
        }
#endif

    }


    public static class ZDOExtension
    {

        public static void RegisterZDOEvent(this ZDO zdo, string variableName, Action<ZDO> action)
        {
            zdo.RegisterZDOEvent(variableName.GetStableHashCode(), action);
        }

        public static void RegisterZDOEvent(this ZDO zdo, int variableHash, Action<ZDO> action)
        {
            if (zdo.m_zdoEvents.ContainsKey(variableHash))
            {
                zdo.m_zdoEvents[variableHash] += action;
            }
            else
            {
                zdo.m_zdoEvents.Add(variableHash, action);
            }
        }

        public static void UnregisterZDOEvent(this ZDO zdo, string variableName, Action<ZDO> action)
        {
            zdo.UnregisterZDOEvent(variableName.GetStableHashCode(), action);
        }

        public static void UnregisterZDOEvent(this ZDO zdo, int variableHash, Action<ZDO> action)
        {
            if (zdo.m_zdoEvents.ContainsKey(variableHash))
            {
                zdo.m_zdoEvents[variableHash] -= action;
            }
        }

        public static void ClearZDOEvent(this ZDO zdo, string variableName)
        {
            zdo.ClearZDOEvent(variableName);
        }

        public static void ClearZDOEvent(this ZDO zdo, int variableHash)
        {
            zdo.m_zdoEvents.Remove(variableHash);
        }

        public static void SetFieldType(this ZDO zdo, string variableName, ZDOFieldType fieldType)
        {
            zdo.SetFieldType(variableName.GetStableHashCode(), fieldType);
        }

        public static void SetFieldType(this ZDO zdo, int variableHash, ZDOFieldType fieldType)
        {
            zdo.m_fieldTypes[variableHash] = (int)fieldType;
        }

        public static void SetZDOType(this ZDO zdo, ZDOType fieldType)
        {
            zdo.m_zdoType = (int)fieldType;
        }

        public static ZDOType GetFieldType(this ZDO zdo)
        {
            return (ZDOType) zdo.m_zdoType;
        }
    }

}

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class ItemDrop_Patch
    {
        private static int itemDataId = 0;

        [HarmonyPatch(typeof(ItemDrop.ItemData), MethodType.Constructor)]
        [HarmonyPostfix]
        private static void ItemDataConstructor(ItemDrop.ItemData __instance)
        {
            // unique id during runtime used for replication
            // non persistant after saving.
            __instance.m_id = ++itemDataId;
            __instance.m_customData = new Dictionary<int, byte[]>();
        }

        [HarmonyPatch(typeof(ItemDrop.ItemData), "Clone")]
        [HarmonyPostfix]
        private static void Clone(ItemDrop.ItemData __instance, ItemDrop.ItemData __result)
        {
            __result.m_id = ++itemDataId;
            __result.m_customData = new Dictionary<int, byte[]>();
            __instance.m_customData.Do(k => __result.m_customData[k.Key] = (byte[])k.Value.Clone());
        }

        [HarmonyPatch(typeof(ItemDrop), "RequestOwn")]
        [HarmonyPrefix]
        private static bool RequestOwn()
        {

            // Request own? never going to happen.
            return false;
        }

        [HarmonyPatch(typeof(ItemDrop), "Pickup")]
        [HarmonyPrefix]
        private static bool Pickup(ItemDrop __instance, Humanoid character)
        {
            if (__instance.m_nview.IsValid())
            {
                if (__instance.CanPickup())
                {
                    __instance.Load();
                    character.Pickup(__instance.gameObject);
                    __instance.Save();
                }
                else if (!ZNet.instance.IsServer())
                {
                    __instance.m_nview.InvokeRPC("RequestOwn");
                }
            }

            return false;
        }

        [HarmonyPatch(typeof(ItemDrop), "RPC_RequestOwn")]
        [HarmonyPrefix]
        private static bool RPC_RequestOwn(ItemDrop __instance, long uid)
        {
            if (!PrivateArea_Patch.CheckAccess(uid, __instance.transform.position))
                return false;

            var peer = ZNet.instance.GetPeer(uid);
            if (peer == null || peer.m_player == null)
                return false;

            if (__instance.CanPickup() && (__instance.transform.position - peer.m_player.transform.position).magnitude <= peer.m_player.m_autoPickupRange)
            {
                __instance.Pickup(peer.m_player);
            }
            return false;
        }
    }

    public static class ItemDropExtension
    {

        public static int GetID(this ItemDrop.ItemData itemData)
        {
            return itemData.m_id;
        }

        public static void SetCustomData(this ItemDrop.ItemData itemData, string name, byte[] data)
        {
            itemData.SetCustomData(name.GetStableHashCode(), data);
        }

        public static void SetCustomData(this ItemDrop.ItemData itemData, int hash, byte[] data)
        {
            itemData.m_customData[hash] = data;
        }

        public static byte[] GetCustomData(this ItemDrop.ItemData itemData, string name)
        {
            return itemData.GetCustomData(name.GetStableHashCode());
        }

        public static byte[] GetCustomData(this ItemDrop.ItemData itemData, int hash)
        {
            if (itemData.m_customData.TryGetValue(hash, out var val))
            {
                return val;
            }

            return null;
        }

        public static void SetCustomData(this ItemDrop.ItemData itemData, string name, bool value)
        {
            itemData.SetCustomData(name, BitConverter.GetBytes(value));
        }

        public static bool GetCustomDataBool(this ItemDrop.ItemData itemData, string name)
        {
            var bytes = itemData.GetCustomData(name);
            if (bytes == null || bytes.Length != sizeof(bool))
                return default;
            return BitConverter.ToBoolean(itemData.GetCustomData(name), 0);
        }

        public static void SetCustomData(this ItemDrop.ItemData itemData, string name, int value)
        {
            itemData.SetCustomData(name, BitConverter.GetBytes(value));
        }

        public static int GetCustomDataInt(this ItemDrop.ItemData itemData, string name)
        {
            var bytes = itemData.GetCustomData(name);
            if (bytes == null || bytes.Length != sizeof(int))
                return default;
            return BitConverter.ToInt32(itemData.GetCustomData(name), 0);
        }

        public static void SetCustomData(this ItemDrop.ItemData itemData, string name, long value)
        {
            itemData.SetCustomData(name, BitConverter.GetBytes(value));
        }

        public static long GetCustomDataLong(this ItemDrop.ItemData itemData, string name)
        {
            var bytes = itemData.GetCustomData(name);
            if (bytes == null || bytes.Length != sizeof(long))
                return default;
            return BitConverter.ToInt64(itemData.GetCustomData(name), 0);
        }

        public static void SetCustomData(this ItemDrop.ItemData itemData, string name, float value)
        {
            itemData.SetCustomData(name, BitConverter.GetBytes(value));
        }

        public static float GetCustomDataFloat(this ItemDrop.ItemData itemData, string name)
        {
            var bytes = itemData.GetCustomData(name);
            if (bytes == null || bytes.Length != sizeof(float))
                return default;
            return BitConverter.ToSingle(itemData.GetCustomData(name), 0);
        }
    }
}

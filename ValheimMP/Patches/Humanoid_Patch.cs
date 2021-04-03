using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using ValheimMP.Framework;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Humanoid_Patch
    {

        /// <summary>
        /// Turn on to perform actions in this class locally (the default way without rpcs)
        /// </summary>
        public static bool LocalActionOnly { get; set; }

        [HarmonyPatch(typeof(Humanoid), "Awake")]
        [HarmonyPostfix]
        private static void Awake(Humanoid __instance)
        {
            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                __instance.m_nview.Register("EquipItem", (long sender, int itemId, bool triggerEquipEffects) =>
                {
                    RPC_EquipItem(__instance, sender, itemId, triggerEquipEffects);
                });
                __instance.m_nview.Register("UnequipItem", (long sender, int itemId, bool triggerEquipEffects) =>
                {
                    RPC_UnequipItem(__instance, sender, itemId, triggerEquipEffects);
                });
                __instance.m_nview.Register("DropItem", (long sender, ZDOID invId, int itemId, int amount) =>
                {
                    RPC_DropItem(__instance, sender, invId, itemId, amount);
                });
            }

            ValheimMP.Instance.InventoryManager.Register(__instance.m_inventory, __instance.m_nview);
        }

        private static void RPC_EquipItem(Humanoid __instance, long sender, int itemId, bool triggerEquipEffects)
        {
            var item = __instance.m_inventory.m_inventory.SingleOrDefault(k => k.m_id == itemId);
            if (item == null)
                return;

            if (ZNet.instance.IsServer())
            {
                Player_Patch.SuppressMessages = true;
                if (__instance.EquipItem(item, triggerEquipEffects))
                {
                    //__instance.m_nview.InvokeRPC("EquipItem", itemPos, itemHash, triggerEquipEffects);
                    // inventory item equipped flag changed, let the client know!
                    __instance.m_inventory.Changed();
                }
                Player_Patch.SuppressMessages = false;
            }
            else
            {
                LocalActionOnly = true;
                __instance.EquipItem(item, triggerEquipEffects);
                LocalActionOnly = false;
            }

            return;
        }

        private static void RPC_UnequipItem(Humanoid __instance, long sender, int itemId, bool triggerEquipEffects)
        {
            var item = __instance.m_inventory.m_inventory.SingleOrDefault(k => k.m_id == itemId);
            if (item == null)
                return;


            if (ZNet.instance.IsServer())
            {
                Player_Patch.SuppressMessages = true;
                __instance.UnequipItem(item, triggerEquipEffects);
                Player_Patch.SuppressMessages = false;
                //__instance.m_nview.InvokeRPC("UnequipItem", itemPos, itemHash, triggerEquipEffects);

                // inventory item equipped flag changed, let the client know!
                __instance.m_inventory.Changed();
            }
            else
            {
                LocalActionOnly = true;
                __instance.UnequipItem(item, triggerEquipEffects);
                LocalActionOnly = false;
            }
        }


        // This function is basically EquipItem with all checks ripped out of them, it can be done better.
        // Possibly with transpiler?
        public static bool ForceEquipItem(Humanoid __instance, ItemDrop.ItemData item, bool triggerEquipEffects = true)
        {
            if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Tool)
            {
                __instance.UnequipItem(__instance.m_rightItem, triggerEquipEffects);
                __instance.UnequipItem(__instance.m_leftItem, triggerEquipEffects);
                __instance.m_rightItem = item;
                __instance.m_hiddenRightItem = null;
                __instance.m_hiddenLeftItem = null;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch)
            {
                if (__instance.m_rightItem != null && __instance.m_leftItem == null && __instance.m_rightItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon)
                {
                    __instance.m_leftItem = item;
                }
                else
                {
                    __instance.UnequipItem(__instance.m_rightItem, triggerEquipEffects);
                    if (__instance.m_leftItem != null && __instance.m_leftItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Shield)
                    {
                        __instance.UnequipItem(__instance.m_leftItem, triggerEquipEffects);
                    }
                    __instance.m_rightItem = item;
                }
                __instance.m_hiddenRightItem = null;
                __instance.m_hiddenLeftItem = null;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon)
            {
                if (__instance.m_rightItem != null && __instance.m_rightItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch && __instance.m_leftItem == null)
                {
                    ItemDrop.ItemData rightItem = __instance.m_rightItem;
                    __instance.UnequipItem(__instance.m_rightItem, triggerEquipEffects);
                    __instance.m_leftItem = rightItem;
                    __instance.m_leftItem.m_equiped = true;
                }
                __instance.UnequipItem(__instance.m_rightItem, triggerEquipEffects);
                if (__instance.m_leftItem != null && __instance.m_leftItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Shield && __instance.m_leftItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Torch)
                {
                    __instance.UnequipItem(__instance.m_leftItem, triggerEquipEffects);
                }
                __instance.m_rightItem = item;
                __instance.m_hiddenRightItem = null;
                __instance.m_hiddenLeftItem = null;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
            {
                __instance.UnequipItem(__instance.m_leftItem, triggerEquipEffects);
                if (__instance.m_rightItem != null && __instance.m_rightItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.OneHandedWeapon && __instance.m_rightItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Torch)
                {
                    __instance.UnequipItem(__instance.m_rightItem, triggerEquipEffects);
                }
                __instance.m_leftItem = item;
                __instance.m_hiddenRightItem = null;
                __instance.m_hiddenLeftItem = null;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow)
            {
                __instance.UnequipItem(__instance.m_leftItem, triggerEquipEffects);
                __instance.UnequipItem(__instance.m_rightItem, triggerEquipEffects);
                __instance.m_leftItem = item;
                __instance.m_hiddenRightItem = null;
                __instance.m_hiddenLeftItem = null;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon)
            {
                __instance.UnequipItem(__instance.m_leftItem, triggerEquipEffects);
                __instance.UnequipItem(__instance.m_rightItem, triggerEquipEffects);
                __instance.m_rightItem = item;
                __instance.m_hiddenRightItem = null;
                __instance.m_hiddenLeftItem = null;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Chest)
            {
                __instance.UnequipItem(__instance.m_chestItem, triggerEquipEffects);
                __instance.m_chestItem = item;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Legs)
            {
                __instance.UnequipItem(__instance.m_legItem, triggerEquipEffects);
                __instance.m_legItem = item;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo)
            {
                __instance.UnequipItem(__instance.m_ammoItem, triggerEquipEffects);
                __instance.m_ammoItem = item;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Helmet)
            {
                __instance.UnequipItem(__instance.m_helmetItem, triggerEquipEffects);
                __instance.m_helmetItem = item;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shoulder)
            {
                __instance.UnequipItem(__instance.m_shoulderItem, triggerEquipEffects);
                __instance.m_shoulderItem = item;
            }
            else if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Utility)
            {
                __instance.UnequipItem(__instance.m_utilityItem, triggerEquipEffects);
                __instance.m_utilityItem = item;
            }
            if (__instance.IsItemEquiped(item))
            {
                item.m_equiped = true;
            }
            __instance.SetupEquipment();
            if (triggerEquipEffects)
            {
                __instance.TriggerEquipEffect(item);
            }
            return true;
        }

        [HarmonyPatch(typeof(Humanoid), "EquipItem")]
        [HarmonyPrefix]
        private static bool EquipItem(ref Humanoid __instance, ref bool __result, ItemDrop.ItemData item, bool triggerEquipEffects)
        {
            // Let the server side perform the default one
            if (LocalActionOnly || ZNet.instance == null || ZNet.instance.IsServer())
                return true;

            __result = false;

            if (__instance.IsItemEquiped(item))
            {
                __result = true;
                return false;
            }
            if (!__instance.m_inventory.ContainsItem(item))
            {
                return false;
            }
            if (__instance.InAttack() || __instance.InDodge())
            {
                return false;
            }
            if (__instance.IsPlayer() && !__instance.IsDead() && __instance.IsSwiming() && !__instance.IsOnGround())
            {
                return false;
            }
            if (item.m_shared.m_useDurability && item.m_durability <= 0f)
            {
                return false;
            }
            if (item.m_shared.m_dlc.Length > 0 && !DLCMan.instance.IsDLCInstalled(item.m_shared.m_dlc))
            {
                __instance.Message(MessageHud.MessageType.Center, "$msg_dlcrequired");
                return false;
            }

            __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "EquipItem", item.m_id, triggerEquipEffects);
            __result = true;
            return false;
        }

        [HarmonyPatch(typeof(Humanoid), "UnequipItem")]
        [HarmonyPrefix]
        private static bool UnequipItem(ref Humanoid __instance, ItemDrop.ItemData item, bool triggerEquipEffects)
        {
            if (item == null) return false;

            if (LocalActionOnly || ZNet.instance == null || ZNet.instance.IsServer())
                return true;

            __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "UnequipItem", item.m_id, triggerEquipEffects);
            return false;
        }

        [HarmonyPatch(typeof(Humanoid), "DropItem")]
        [HarmonyPrefix]
        private static bool DropItem(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item, int amount)
        {
            if (LocalActionOnly || ZNet.instance == null || ZNet.instance.IsServer())
                return true;

            if (amount == 0)
            {
                return false;
            }
            if (item.m_shared.m_questItem)
            {
                __instance.Message(MessageHud.MessageType.Center, "$msg_cantdrop");
                return false;
            }

            var player = __instance as Player;

            if (player == null)
                return false;

            if (!PrivateArea_Patch.CheckAccess(player.GetPlayerID(), __instance.transform.position))
            {
                return false;
            }

            if (amount > item.m_stack)
            {
                amount = item.m_stack;
            }
            LocalActionOnly = true;
            __instance.RemoveFromEquipQueue(item);
            LocalActionOnly = false;

            var invId = inventory.m_nview.m_zdo.m_uid;
            var itemId = item.m_id;

            

            __instance.m_nview.InvokeRPC(ZNet.instance.GetServerPeer().m_uid, "DropItem", invId, itemId, amount);
            return false;
        }

        private static void RPC_DropItem(Humanoid __instance, long sender, ZDOID invId, int itemId, int amount)
        {
            var inventory = ValheimMP.Instance.InventoryManager.GetInventory(invId);
            if (inventory == null)
                return;

            var player = __instance as Player;
            if (player == null || !PrivateArea_Patch.CheckAccess(player.GetPlayerID(), __instance.transform.position))
                return;

            var item = inventory.m_inventory.SingleOrDefault(k => k.m_id == itemId);
            if (item == null)
                return;
            Player_Patch.SuppressMessages = true;
            LocalActionOnly = true;
            __instance.DropItem(inventory, item, amount);
            LocalActionOnly = false;
            Player_Patch.SuppressMessages = false;
        }


        [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> BlockAttack(IEnumerable<CodeInstruction> instructions)
        {
            if (ValheimMP.IsDedicated)
                return instructions;

            var list = instructions.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(AccessTools.PropertyGetter(typeof(DamageText), "instance")))
                {
                    list.RemoveRange(i, 11);
                    break;
                }
            }
            return list;
        }
    }
}

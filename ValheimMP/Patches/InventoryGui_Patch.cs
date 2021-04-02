using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class InventoryGui_Patch
    {
        public static bool LocalMove { get; private set; }


        [HarmonyPatch(typeof(InventoryGui), "Awake")]
        [HarmonyPostfix]
        private static void Awake()
        {
            ValheimMPPlugin.Instance.InventoryManager.OnItemCrafted += OnItemCrafted;
        }

        [HarmonyPatch(typeof(InventoryGui), "OnDestroy")]
        [HarmonyPostfix]
        private static void OnDestroy()
        {
            ValheimMPPlugin.Instance.InventoryManager.OnItemCrafted -= OnItemCrafted;
        }

        private static void OnItemCrafted(Inventory inventory, ItemDrop.ItemData itemData)
        {
            if (InventoryGui.instance.isActiveAndEnabled)
            {
                InventoryGui.instance.UpdateCraftingPanel();
            }
        }

        [HarmonyPatch(typeof(InventoryGui), "UpdateContainer")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Replace_IsOwner_With_True(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(AccessTools.Method(typeof(Container), "IsOwner")))
                {
                    list[i - 2].opcode = OpCodes.Nop;
                    list[i - 1].opcode = OpCodes.Nop;
                    list[i - 0] = new CodeInstruction(OpCodes.Ldc_I4_1);
                }
            }
            return list;
        }



        [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
        [HarmonyPrefix]
        private static bool DoCrafting(ref InventoryGui __instance, Player player)
        {
            if (__instance.m_craftRecipe == null)
            {
                return false;
            }
            int num = ((__instance.m_craftUpgradeItem == null) ? 1 : (__instance.m_craftUpgradeItem.m_quality + 1));
            if (num > __instance.m_craftRecipe.m_item.m_itemData.m_shared.m_maxQuality ||
                (!player.HaveRequirements(__instance.m_craftRecipe, discover: false, num) && !player.NoCostCheat()) ||
                (__instance.m_craftUpgradeItem != null && !player.GetInventory().ContainsItem(__instance.m_craftUpgradeItem)) ||
                (__instance.m_craftUpgradeItem == null && !player.GetInventory().HaveEmptySlot()))
            {
                return false;
            }
            if (__instance.m_craftRecipe.m_item.m_itemData.m_shared.m_dlc.Length > 0 && !DLCMan.instance.IsDLCInstalled(__instance.m_craftRecipe.m_item.m_itemData.m_shared.m_dlc))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_dlcrequired");
                return false;
            }

            var recipeName = __instance.m_craftRecipe.m_item.m_itemData.m_shared.m_name;
            var upgradeItemId = __instance.m_craftUpgradeItem != null ? __instance.m_craftUpgradeItem.m_id : -1;
            ZNet.instance.GetServerRPC().Invoke("InventoryGui_DoCrafting", recipeName, upgradeItemId);

            CraftingStation currentCraftingStation = Player.m_localPlayer.GetCurrentCraftingStation();
            if ((bool)currentCraftingStation)
            {
                currentCraftingStation.m_craftItemDoneEffects.Create(player.transform.position, Quaternion.identity);
            }
            else
            {
                __instance.m_craftItemDoneEffects.Create(player.transform.position, Quaternion.identity);
            }
            Game.instance.GetPlayerProfile().m_playerStats.m_crafts++;
            Gogan.LogEvent("Game", "Crafted", __instance.m_craftRecipe.m_item.m_itemData.m_shared.m_name, num);
            return false;
        }



        /// <summary>
        /// We pretend the Gui window is open all the time on the server to make sure some checks go right.
        /// </summary>
        /// <param name="__result"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(InventoryGui), "IsVisible")]
        [HarmonyPrefix]
        public static bool IsVisible(ref bool __result)
        {
            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                __result = true;
                return false;
            }
            return true;
        }

        public static void RPC_DoCrafting(ZRpc rpc, string recipeName, int upgradeItemId)
        {
            var __instance = InventoryGui.instance;

            var peer = ZNet.instance.GetPeer(rpc);
            if (peer == null)
                return;

            var player = peer.m_player;
            if (player == null)
                return;

            __instance.m_craftRecipe = ObjectDB.instance.m_recipes.SingleOrDefault(k => k.m_item?.m_itemData?.m_shared?.m_name == recipeName);

            if (__instance.m_craftRecipe == null)
                return;

            __instance.m_craftUpgradeItem = upgradeItemId != -1 ? player.m_inventory.m_inventory.SingleOrDefault(k => k.m_id == upgradeItemId) : null;

            int num = ((__instance.m_craftUpgradeItem == null) ? 1 : (__instance.m_craftUpgradeItem.m_quality + 1));

            if (num > __instance.m_craftRecipe.m_item.m_itemData.m_shared.m_maxQuality ||
                (!player.HaveRequirements(__instance.m_craftRecipe, discover: false, num) && !player.NoCostCheat()) ||
                (__instance.m_craftUpgradeItem != null && !player.GetInventory().ContainsItem(__instance.m_craftUpgradeItem)) ||
                (__instance.m_craftUpgradeItem == null && !player.GetInventory().HaveEmptySlot()))
            {
                return;
            }
            if (__instance.m_craftRecipe.m_item.m_itemData.m_shared.m_dlc.Length > 0 && !DLCMan.instance.IsDLCInstalled(__instance.m_craftRecipe.m_item.m_itemData.m_shared.m_dlc))
            {
                return;
            }
            int variant = __instance.m_craftVariant;
            if (__instance.m_craftUpgradeItem != null)
            {
                variant = __instance.m_craftUpgradeItem.m_variant;
                player.UnequipItem(__instance.m_craftUpgradeItem);
                player.GetInventory().RemoveItem(__instance.m_craftUpgradeItem);
            }
            long playerID = player.GetPlayerID();
            string playerName = player.GetPlayerName();
            var craftedItem = player.GetInventory().AddItem(__instance.m_craftRecipe.m_item.gameObject.name, __instance.m_craftRecipe.m_amount, num, variant, playerID, playerName);
            if (craftedItem != null)
            {
                if (!player.NoCostCheat())
                {
                    player.ConsumeResources(__instance.m_craftRecipe.m_resources, num);
                    // this is used as a trigger on the client side when the item is synchonized
                    // doing an RPC with the item id right now would arrive before the item exists
                    craftedItem.m_crafted = true;
                }
            }
        }

        private static void RepairOneItem(ItemDrop.ItemData itemData)
        {
            ZNet.instance.GetServerRPC().Invoke("InventoryGui_RepairOneItem", itemData.m_id);
        }

        [HarmonyPatch(typeof(InventoryGui), "RepairOneItem")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> RepairOneItem(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();

            //stfld float32 ItemDrop / ItemData::m_durability
            var op = AccessTools.Field(typeof(ItemDrop.ItemData), "m_durability");
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].opcode == OpCodes.Stfld && op.Equals(list[i].operand))
                {

                    var plist = new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_2),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(InventoryGui_Patch), "RepairOneItem", new Type[] { typeof(ItemDrop.ItemData) })),
                    };

                    list.InsertRange(i + 1, plist);
                    break;
                }
            }

            return list;
        }

        public static void RPC_RepairOneItem(ZRpc rpc, int itemId)
        {
            var peer = ZNet.instance.GetPeer(rpc);
            if (peer == null)
                return;

            var player = peer.m_player;
            if (player == null)
                return;

            var item = player.m_inventory.m_inventory.SingleOrDefault(k => k.m_id == itemId);
            if (item != null && CanRepair(player, item))
            {
                item.m_durability = item.GetMaxDurability();
            }
        }

        public static bool CanRepair(Player player, ItemDrop.ItemData item)
        {
            if (player == null)
            {
                return false;
            }
            if (!item.m_shared.m_canBeReparied)
            {
                return false;
            }
            if (player.NoCostCheat())
            {
                return true;
            }
            CraftingStation currentCraftingStation = player.GetCurrentCraftingStation();
            if (currentCraftingStation == null)
            {
                return false;
            }
            Recipe recipe = ObjectDB.instance.GetRecipe(item);
            if (recipe == null)
            {
                return false;
            }
            if (recipe.m_craftingStation == null && recipe.m_repairStation == null)
            {
                return false;
            }
            if ((recipe.m_repairStation != null && recipe.m_repairStation.m_name == currentCraftingStation.m_name) || (recipe.m_craftingStation != null && recipe.m_craftingStation.m_name == currentCraftingStation.m_name))
            {
                if (currentCraftingStation.GetLevel() < recipe.m_minStationLevel)
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        [HarmonyPatch(typeof(InventoryGui), "OnTakeAll")]
        [HarmonyPrefix]
        private static bool OnTakeAll(InventoryGui __instance)
        {
            if (!Player.m_localPlayer.IsTeleporting() && __instance.m_currentContainer != null)
            {
                __instance.SetupDragItem(null, null, 1);
                // Container TakeAll handles this properly!
                __instance.m_currentContainer.TakeAll(Player.m_localPlayer);
            }

            return false;
        }
    }
}

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class InventoryGui_Patch
    {
        public static bool LocalMove { get; private set; }

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
            if (player.GetInventory().AddItem(__instance.m_craftRecipe.m_item.gameObject.name, __instance.m_craftRecipe.m_amount, num, variant, playerID, playerName) != null)
            {
                if (!player.NoCostCheat())
                {
                    player.ConsumeResources(__instance.m_craftRecipe.m_resources, num);
                }
            }
        }

        private static void InvokeRepair_Content()
        {
            var tempWornItem = new ItemDrop.ItemData();
            // Actual content.
            ZNet.instance.GetServerRPC().Invoke("InventoryGui_DoCrafting", tempWornItem.m_id);

            //IL_0007: call class [assembly_valheim]ZNet [assembly_valheim]ZNet::get_instance()
            //IL_000c: callvirt instance class [assembly_valheim]ZRpc [assembly_valheim]ZNet::GetServerRPC()
            //IL_0011: ldstr "InventoryGui_DoCrafting"
            //IL_0016: ldc.i4.1
            //IL_0017: newarr [mscorlib]System.Object
            //IL_001c: dup
            //IL_001d: ldc.i4.0

            //IL_001e: ldloc.0   <- tempWornItem, different on the actual code.

            //IL_001f: ldfld int32 [assembly_valheim]ItemDrop/ItemData::m_id
            //IL_0024: box [mscorlib]System.Int32
            //IL_0029: stelem.ref
            //// (no C# code)
            //IL_002a: callvirt instance void [assembly_valheim]ZRpc::Invoke(string, object[])
        }


        [HarmonyPatch(typeof(InventoryGui), "RepairOneItem")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> InvokeRepair(IEnumerable<CodeInstruction> instructions)
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
                        new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(ZNet), "instance")),
                        new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ZNet), "GetServerRPC")),
                        new CodeInstruction(OpCodes.Ldstr, "InventoryGui_RepairOneItem"),
                        new CodeInstruction(OpCodes.Ldc_I4_1),
                        new CodeInstruction(OpCodes.Newarr, typeof(object)),
                        new CodeInstruction(OpCodes.Dup),
                        new CodeInstruction(OpCodes.Ldc_I4_0),
                        new CodeInstruction(list[i-2].opcode),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ItemDrop.ItemData), "m_id")),
                        new CodeInstruction(OpCodes.Box, typeof(Int32)),
                        new CodeInstruction(OpCodes.Stelem_Ref),
                        new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ZRpc), "Invoke")),
                    };

                    list.InsertRange(i + 1, plist);
                    break;
                }
            }

            return list;
        }

        public static void RPC_RepairOneItem(ZRpc rpc, int itemId)
        {
            ZLog.Log($"RPC_RepairOneItem {itemId}");
            var peer = ZNet.instance.GetPeer(rpc);
            if (peer == null)
                return;

            var player = peer.m_player;
            if (player == null)
                return;

            var item = player.m_inventory.m_inventory.SingleOrDefault(k => k.m_id == itemId);
            if (item != null)
            {
                // TODO: There needs to be checked if we can actually repair this item.
                // but... there is no clear way, other then checking the radius around the player
                // and finding all crafting stations that are capable of repairing this item
                // and then seeing if the player can use said station
                // *zzz*
                item.m_durability = item.GetMaxDurability();
            }
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

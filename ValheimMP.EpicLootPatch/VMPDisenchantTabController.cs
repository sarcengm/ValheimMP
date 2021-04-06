using EpicLoot;
using EpicLoot.Crafting;
using System.Collections.Generic;
using UnityEngine;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.EpicLootPatch
{
    public class VMPDisenchantTabController : DisenchantTabController
    {
        public static readonly int DisenchantTrigger = "EpicLoot_Disenchant".GetStableHashCode();
        private static float m_craftResponse;
        public override void DoCrafting(InventoryGui __instance, Player player)
        {
            if (SelectedRecipe >= 0 && SelectedRecipe < Recipes.Count && Time.time - m_craftResponse > 1f)
            {
                var recipe = Recipes[SelectedRecipe];
                m_craftResponse = Time.time;

                ZNet.instance.GetServerRPC().Invoke("EpicLoot_Disenchant", recipe.FromItem.GetNetworkID());

                if (player.GetCurrentCraftingStation() != null)
                {
                    player.GetCurrentCraftingStation().m_craftItemDoneEffects.Create(player.transform.position, Quaternion.identity);
                }

                Game.instance.GetPlayerProfile().m_playerStats.m_crafts++;
                Gogan.LogEvent("Game", "Disenchanted", recipe.FromItem.m_shared.m_name, 1);
            }
        }

        public void OnDisenchant(Inventory inventory, ItemDrop.ItemData itemData, byte[] triggerData)
        {
            m_craftResponse = 0;
        }

        public static void RPC_Disenchant(ZRpc rpc, int itemId)
        {
            var peer = ZNet.instance.GetPeer(rpc);
            if (peer == null)
                return;

            var player = peer.GetPlayer();
            if (player == null || player.IsDead())
                return;
            var inventory = player.GetInventory();
            var item = inventory.GetItemByID(itemId);
            if (item == null)
                return;

            var products = GetDisenchantProducts(item);
            if (products == null)
                return;

            inventory.RemoveOneItem(item);

            var didntAdd = new List<KeyValuePair<ItemDrop.ItemData, int>>();
            foreach (var product in products)
            {
                var addSuccess = false;
                var canAdd = player.GetInventory().CanAddItem(product.Key.m_itemData, product.Value);
                if (canAdd)
                {
                    var itemData = player.GetInventory().AddItem(product.Key.name, product.Value, 1, 0, 0, "");
                    addSuccess = itemData != null;
                    if (itemData != null && itemData.IsMagicCraftingMaterial())
                    {
                        itemData.m_variant = EpicLoot.EpicLoot.GetRarityIconIndex(itemData.GetRarity());
                    }
                }

                if (!addSuccess)
                {
                    var newItem = product.Key.m_itemData.Clone();
                    newItem.m_dropPrefab = ObjectDB.instance.GetItemPrefab(product.Key.GetPrefabName(product.Key.gameObject.name));
                    didntAdd.Add(new KeyValuePair<ItemDrop.ItemData, int>(newItem, product.Value));
                }
            }

            foreach (var itemNotAdded in didntAdd)
            {
                var itemDrop = ItemDrop.DropItem(itemNotAdded.Key, itemNotAdded.Value, player.transform.position + player.transform.forward + player.transform.up, player.transform.rotation);
                itemDrop.GetComponent<Rigidbody>().velocity = (player.transform.forward + Vector3.up) * 5f;
                player.Message(MessageHud.MessageType.TopLeft, $"$msg_dropped {itemDrop.m_itemData.m_shared.m_name} (Inventory was full when crafting)", itemDrop.m_itemData.m_stack, itemDrop.m_itemData.GetIcon());
            }
        }
    }
}

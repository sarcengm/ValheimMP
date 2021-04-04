using EpicLoot;
using EpicLoot.Crafting;
using ExtendedItemDataFramework;
using UnityEngine;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.EpicLootPatch
{
    public class VMPEnchantTabController : EnchantTabController
    {
        private static float m_craftResponse;
        public static readonly int EnchantTrigger = "EpicLoot_Enchant".GetStableHashCode();

        public override void DoCrafting(InventoryGui __instance, Player player)
        {
            if (SelectedRecipe >= 0 && SelectedRecipe < Recipes.Count && Time.time - m_craftResponse > 1f)
            {
                var recipe = Recipes[SelectedRecipe];
                m_craftResponse = Time.time;

                ZNet.instance.GetServerRPC().Invoke("EpicLoot_Enchant", recipe.FromItem.GetNetworkID(), (int)SelectedRarity);

                if (player.GetCurrentCraftingStation() != null)
                {
                    player.GetCurrentCraftingStation().m_craftItemDoneEffects.Create(player.transform.position, Quaternion.identity);
                }

                Game.instance.GetPlayerProfile().m_playerStats.m_crafts++;
                Gogan.LogEvent("Game", "Enchanted", recipe.FromItem.m_shared.m_name, 1);
            }
        }

        public void OnEnchant(Inventory inventory, ItemDrop.ItemData itemData, byte[] triggerData)
        {
            m_craftResponse = 0;
            SuccessDialog.Show(itemData.Extended());
        }

        public static void RPC_Enchant(ZRpc rpc, int itemId, int rarity)
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
            var recipe = new EnchantRecipe() { FromItem = item };

            var requirements = GetRecipeRequirementArray(recipe, (ItemRarity)rarity);

            if (!player.HaveRequirements(requirements, false, 1))
                return;

            if (!recipe.FromItem.IsExtended())
            {
                inventory.RemoveItem(recipe.FromItem);
                var extendedItemData = new ExtendedItemData(recipe.FromItem);
                inventory.m_inventory.Add(extendedItemData);
                inventory.Changed();
                recipe.FromItem = extendedItemData;
            }

            float previousDurabilityPercent = 0;
            if (recipe.FromItem.m_shared.m_useDurability)
            {
                previousDurabilityPercent = recipe.FromItem.m_durability / recipe.FromItem.GetMaxDurability();
            }

            var magicItemComponent = recipe.FromItem.Extended().AddComponent<MagicItemComponent>();
            var magicItem = LootRoller.RollMagicItem((ItemRarity)rarity, recipe.FromItem.Extended());
            magicItemComponent.SetMagicItem(magicItem);

            // Spend Resources
            if (!player.NoCostCheat())
            {
                player.ConsumeResources(requirements, 1);
            }

            // Maintain durability
            if (recipe.FromItem.m_shared.m_useDurability)
            {
                recipe.FromItem.m_durability = previousDurabilityPercent * recipe.FromItem.GetMaxDurability();
            }

            recipe.FromItem.SetCraftTrigger(EnchantTrigger);
            recipe.FromItem.AssignNewId();

            player.m_inventory.Changed();
        }
    }
}

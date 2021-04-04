using Common;
using EpicLoot;
using EpicLoot.Crafting;
using ExtendedItemDataFramework;
using fastJSON;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.EpicLootPatch
{
    public class VMPAugmentTabController : AugmentTabController
    {
        internal static readonly int AugmentTrigger = "EpicLoot_Augment".GetStableHashCode();
        internal static readonly int AugmentCompleteTrigger = "EpicLoot_AugmentComplete".GetStableHashCode();
        private static float m_craftResponse;

        private static Dictionary<int, List<MagicItemEffect>> m_pendingAugments = new Dictionary<int, List<MagicItemEffect>>();

        public override void TryInitialize(InventoryGui inventoryGui, int tabIndex, System.Action<TabController> onTabPressed)
        {
            if (ChoiceDialog == null || ChoiceDialog is not VMPAugmentChoiceDialog)
            {
                if (ChoiceDialog != null)
                {
                    UnityEngine.Object.DestroyImmediate(ChoiceDialog);
                }

                ChoiceDialog = CreateDialog<VMPAugmentChoiceDialog>(inventoryGui, "AugmentChoiceDialog");

                var background = ChoiceDialog.gameObject.transform.Find("Frame").gameObject.RectTransform();
                ChoiceDialog.MagicBG = Object.Instantiate(inventoryGui.m_recipeIcon, background);
                ChoiceDialog.MagicBG.name = "MagicItemBG";
                ChoiceDialog.MagicBG.sprite = EpicLoot.EpicLoot.GetMagicItemBgSprite();
                ChoiceDialog.MagicBG.color = Color.white;

                ChoiceDialog.NameText = Object.Instantiate(inventoryGui.m_recipeName, background);
                ChoiceDialog.Description = Object.Instantiate(inventoryGui.m_recipeDecription, background);
                ChoiceDialog.Description.rectTransform.anchoredPosition += new Vector2(0, -55);
                ChoiceDialog.Description.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 350);
                ChoiceDialog.Icon = Object.Instantiate(inventoryGui.m_recipeIcon, background);

                var closeButton = ChoiceDialog.gameObject.GetComponentInChildren<Button>();
                Object.Destroy(closeButton.gameObject);

                for (int i = 0; i < 3; i++)
                {
                    var button = Object.Instantiate(inventoryGui.m_craftButton, background);
                    var rt = button.gameObject.RectTransform();
                    rt.anchoredPosition = new Vector2(0, -155 - (i * 45));
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 40);
                    ChoiceDialog.EffectChoiceButtons.Add(button);
                }
            }

            base.TryInitialize(inventoryGui, tabIndex, onTabPressed);
        }

        public override void DoCrafting(InventoryGui __instance, Player player)
        {
            if (SelectedRecipe >= 0 && SelectedRecipe < Recipes.Count && Time.time - m_craftResponse > 1f)
            {
                var previouslySelectedRecipe = SelectedRecipe;
                var recipe = Recipes[SelectedRecipe];
                m_craftResponse = Time.time;

                ZNet.instance.GetServerRPC().Invoke("EpicLoot_Augment", recipe.FromItem.GetNetworkID(), recipe.EffectIndex);

                UpdateRecipeList(__instance);
                OnSelectedRecipe(__instance, previouslySelectedRecipe);
            }
        }

        public static void RPC_Augment(ZRpc rpc, int itemId, int effectIndex)
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

            var recipe = new AugmentRecipe() { FromItem = item, EffectIndex = effectIndex };

            var requirements = GetRecipeRequirementArray(recipe);

            if (!player.HaveRequirements(requirements, false, 1))
                return;



            // Set as augmented
            var magicItem = recipe.FromItem.GetMagicItem();
            magicItem.AugmentedEffectIndex = recipe.EffectIndex;
            // Note: I do not know why I have to do this, but this is the only thing that causes this item to save correctly
            recipe.FromItem.Extended().RemoveComponent<MagicItemComponent>();
            recipe.FromItem.Extended().AddComponent<MagicItemComponent>().SetMagicItem(magicItem);

            EpicLootPatch.Log($"{item}, {item.Extended()}, {magicItem}, {effectIndex}");

            List<MagicItemEffect> effects;
            if (!m_pendingAugments.TryGetValue(item.GetNetworkID(), out effects))
            {
                effects = LootRoller.RollAugmentEffects(item.Extended(), magicItem, effectIndex);
                m_pendingAugments[item.GetNetworkID()] = effects;
            }

            if (!player.NoCostCheat())
            {
                player.ConsumeResources(GetRecipeRequirementArray(recipe), 1);
            }

            var pkg = new ZPackage();
            pkg.Write(effectIndex);
            pkg.Write(JSON.ToJSON(effects));
            item.SetCraftTrigger(AugmentTrigger, pkg.GetArray());
            player.m_inventory.Changed();
        }

        internal void OnAugment(Inventory inventory, ItemDrop.ItemData itemData, byte[] triggerData)
        {
            m_craftResponse = 0;

            var pkg = new ZPackage(triggerData);
            int effectIndex = pkg.ReadInt();
            var recipe = new AugmentRecipe() { FromItem = itemData, EffectIndex = effectIndex };
            var json = pkg.ReadString();
            var effects = JSON.ToObject<List<MagicItemEffect>>(json);

            ((VMPAugmentChoiceDialog)ChoiceDialog).Show(recipe, OnAugmentChoiceComplete, effects);
        }

        private void OnAugmentChoiceComplete(AugmentRecipe arg1, MagicItemEffect arg2)
        {
            ZNet.instance.GetServerRPC().Invoke("EpicLoot_AugmentComplete", arg1.FromItem.GetNetworkID(), arg1.EffectIndex, JSON.ToJSON(arg2));
        }

        public static void RPC_AugmentComplete(ZRpc rpc, int itemId, int effectIndex, string effectJson)
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

            var recipe = new AugmentRecipe() { FromItem = item, EffectIndex = effectIndex };
            var newEffect = JSON.ToObject<MagicItemEffect>(effectJson);

            if (!m_pendingAugments.TryGetValue(item.GetNetworkID(), out var effectList))
                return;

            newEffect = effectList.SingleOrDefault(k => k.EffectType == newEffect.EffectType && k.EffectValue == newEffect.EffectValue && k.Version == newEffect.Version);
            if (newEffect == null)
                return;

            m_pendingAugments.Remove(item.GetNetworkID());

            var magicItem = recipe.FromItem.GetMagicItem();

            if (magicItem.HasBeenAugmented())
            {
                magicItem.ReplaceEffect(magicItem.AugmentedEffectIndex, newEffect);
            }
            else
            {
                magicItem.ReplaceEffect(recipe.EffectIndex, newEffect);
            }

            if (magicItem.Rarity == ItemRarity.Rare)
            {
                magicItem.DisplayName = MagicItemNames.GetNameForItem(recipe.FromItem, magicItem);
            }

            // Note: I do not know why I have to do this, but this is the only thing that causes this item to save correctly
            recipe.FromItem.Extended().RemoveComponent<MagicItemComponent>();
            recipe.FromItem.Extended().AddComponent<MagicItemComponent>().SetMagicItem(magicItem);
            recipe.FromItem.AssignNewId();
            recipe.FromItem.SetCraftTrigger(AugmentCompleteTrigger);
            player.m_inventory.Changed();
        }

        internal void OnAugmentComplete(Inventory inventory, ItemDrop.ItemData itemData, byte[] triggerData)
        {
            var player = Player.m_localPlayer;
            if (player.GetCurrentCraftingStation() != null)
            {
                player.GetCurrentCraftingStation().m_craftItemDoneEffects.Create(player.transform.position, Quaternion.identity);
            }

            InventoryGui.instance?.UpdateCraftingPanel();

            var index = Recipes.FindIndex(k => k.FromItem.GetNetworkID() == itemData.GetNetworkID());

            OnSelectorValueChanged(index, true);

            Game.instance.GetPlayerProfile().m_playerStats.m_crafts++;
            Gogan.LogEvent("Game", "Augmented", itemData.m_shared.m_name, 1);
        }
    }
}

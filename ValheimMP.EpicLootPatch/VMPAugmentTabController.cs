using EpicLoot;
using EpicLoot.Crafting;
using ExtendedItemDataFramework;
using HarmonyLib;
using System.Linq;

namespace ValheimMP.EpicLootPatch
{
    public class VMPAugmentTabController : AugmentTabController
    {
        public override void DoCrafting(InventoryGui __instance, Player player)
        {
            if (SelectedRecipe >= 0 && SelectedRecipe < Recipes.Count)
            {
                var previouslySelectedRecipe = SelectedRecipe;
                var recipe = Recipes[SelectedRecipe];

                UpdateRecipeList(__instance);
                OnSelectedRecipe(__instance, previouslySelectedRecipe);

                // Set as augmented
                var magicItem = recipe.FromItem.GetMagicItem();
                magicItem.AugmentedEffectIndex = recipe.EffectIndex;
                // Note: I do not know why I have to do this, but this is the only thing that causes this item to save correctly
                recipe.FromItem.Extended().RemoveComponent<MagicItemComponent>();
                recipe.FromItem.Extended().AddComponent<MagicItemComponent>().SetMagicItem(magicItem);

                ChoiceDialog.Show(recipe, OnAugmentComplete);
            }
        }

        private void OnAugmentComplete(AugmentRecipe arg1, MagicItemEffect arg2)
        {
            throw new System.NotImplementedException();
        }
    }
}

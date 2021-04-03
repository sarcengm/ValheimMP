using EpicLoot;
using EpicLoot.Crafting;
using System.Collections.Generic;
using UnityEngine;

namespace ValheimMP.EpicLootPatch
{
    public class VMPDisenchantTabController : DisenchantTabController
    {
        public override void DoCrafting(InventoryGui __instance, Player player)
        {
            if (SelectedRecipe >= 0 && SelectedRecipe < Recipes.Count)
            {
                var recipe = Recipes[SelectedRecipe];

            }
        }
    }
}

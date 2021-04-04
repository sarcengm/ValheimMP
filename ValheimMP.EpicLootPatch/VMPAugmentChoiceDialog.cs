using EpicLoot;
using EpicLoot.Crafting;
using System;
using ExtendedItemDataFramework;
using System.Collections.Generic;
using UnityEngine.UI;

namespace ValheimMP.EpicLootPatch
{
    public class VMPAugmentChoiceDialog : AugmentChoiceDialog
    {
        public void Show(AugmentTabController.AugmentRecipe recipe, Action<AugmentTabController.AugmentRecipe, MagicItemEffect> onCompleteCallback, List<MagicItemEffect> newEffectOptions)
        {
            gameObject.SetActive(true);

            _audioSource.loop = true;
            _audioSource.clip = EpicLoot.EpicLoot.Assets.ItemLoopSFX;
            _audioSource.volume = 0.5f;
            _audioSource.Play();

            var item = recipe.FromItem.Extended();
            var rarity = item.GetRarity();
            var magicItem = item.GetMagicItem();
            var rarityColor = item.GetRarityColor();

            MagicBG.enabled = item.IsMagic();
            MagicBG.color = rarityColor;

            NameText.text = Localization.instance.Localize(item.GetDecoratedName());
            Description.text = Localization.instance.Localize(item.GetTooltip());
            Icon.sprite = item.GetIcon();

            for (var index = 0; index < newEffectOptions.Count; index++)
            {
                var effect = newEffectOptions[index];
                var button = EffectChoiceButtons[index];
                var text = button.GetComponentInChildren<Text>();
                text.text = (index == 0 ? "(keep) " : "") + MagicItem.GetEffectText(effect, rarity, true);
                text.color = rarityColor;
                var buttonColor = button.GetComponent<ButtonTextColor>();
                buttonColor.m_defaultColor = rarityColor;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => {
                    onCompleteCallback(recipe, effect);
                    OnClose();
                });
            }
        }
    }
}

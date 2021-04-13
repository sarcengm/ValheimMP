using UnityEngine;

namespace ValheimMP.Framework.Extensions
{
    public static class ObjectDBExtension
    {
        public static StatusEffect GetStatusEffect(this ObjectDB db, int hash)
        {
            foreach (StatusEffect statusEffect in db.m_StatusEffects)
            {
                if (statusEffect.name.GetStableHashCode() == hash)
                {
                    return statusEffect;
                }
            }
            return null;
        }

        public static GameObject GetItemByLocalizedName(this ObjectDB db, string name, bool includeSimilar)
        {
            var bestDistance = 0;
            GameObject bestMatch = null;

            for (int i = 0; i < db.m_items.Count; i++)
            {
                var item = db.m_items[i];
                if (!item) continue;
                var itemDrop = item.GetComponent<ItemDrop>();
                if (!itemDrop) continue;
                var localizedName = Localization.instance.Localize(itemDrop.m_itemData.m_shared.m_name);
                if (string.Compare(name, localizedName, true) == 0)
                    return item;

                if (includeSimilar)
                {
                    if (LevenshteinDistance.SimilarWithinMargin(name, localizedName, 0.3f, out var currentDistance))
                    {
                        if (bestMatch == null || currentDistance < bestDistance)
                        {
                            bestDistance = currentDistance;
                            bestMatch = item;
                        }
                    }
                }
            }

            return bestMatch;
        }
    }
}

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
    }
}

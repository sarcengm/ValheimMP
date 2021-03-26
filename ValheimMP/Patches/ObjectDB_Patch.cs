using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Patches
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ValheimMP.Patches;

namespace ValheimMP.Framework.Extensions
{
    public static class EnemyHudExtension
    {
        public static void ClearPartyFrames()
        {
            EnemyHud_Patch.ClearPartyFrames();
        }
    }
}

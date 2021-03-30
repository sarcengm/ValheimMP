using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Util
{
    public static class TranspilerUtil
    {
        public static bool IsOwnerOrServer(this ZNetView netView)
        {
            return netView.IsOwner() || ValheimMPPlugin.IsDedicated;
        }

        public static bool IsOwnerOrServer(this Character chr)
        {
            return chr.IsOwner() || ValheimMPPlugin.IsDedicated;
        }

        public static bool IsOwnerOrServer(this IWaterInteractable chr)
        {
            return chr.IsOwner() || ValheimMPPlugin.IsDedicated;
        }
        
        public static bool IsOwnerOrServer(this ZDO zDO)
        {
            return zDO.IsOwner() || ValheimMPPlugin.IsDedicated;
        }
    }
}

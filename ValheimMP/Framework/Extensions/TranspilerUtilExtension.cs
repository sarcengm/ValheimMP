namespace ValheimMP.Framework.Extensions
{
    public static class TranspilerUtilExtension
    {
        public static bool IsOwnerOrServer(this ZNetView netView)
        {
            return netView.IsOwner() || ValheimMP.IsDedicated;
        }

        public static bool IsOwnerOrServer(this Character chr)
        {
            return chr.IsOwner() || ValheimMP.IsDedicated;
        }

        public static bool IsOwnerOrServer(this IWaterInteractable chr)
        {
            return chr.IsOwner() || ValheimMP.IsDedicated;
        }
        
        public static bool IsOwnerOrServer(this ZDO zDO)
        {
            return zDO.IsOwner() || ValheimMP.IsDedicated;
        }
    }
}

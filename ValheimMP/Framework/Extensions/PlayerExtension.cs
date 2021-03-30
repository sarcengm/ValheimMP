namespace ValheimMP.Framework.Extensions
{
    public static class PlayerExtension
    {
        public static float GetMaxSqrInteractRange(this Player player)
        {
            var range = player.m_maxInteractDistance;
            if (ValheimMPPlugin.IsDedicated)
                range *= 1.1f;
            range *= range;
            return range;
        }
    }
}

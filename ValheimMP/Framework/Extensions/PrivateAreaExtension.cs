namespace ValheimMP.Framework.Extensions
{
    public enum PrivateAreaAllowMode
    {
        Private = 0,
        Clan = 1 << 0,
        Party = 1 << 1,

        Both = Clan | Party,
    }

    public static class PrivateAreaExtension
    {

        public static PrivateAreaAllowMode GetAllowMode(this PrivateArea privateArea)
        {
            if (privateArea.m_nview && privateArea.m_nview.m_zdo != null)
            {
                return (PrivateAreaAllowMode)privateArea.m_nview.m_zdo.GetInt("allowMode");
            }

            return PrivateAreaAllowMode.Private;
        }

        public static void SetAllowMode(this PrivateArea privateArea, PrivateAreaAllowMode allowMode)
        {
            if (privateArea.m_nview && privateArea.m_nview.m_zdo != null)
            {
                privateArea.m_nview.m_zdo.Set("allowMode", (int)allowMode);
            }
        }
    }
}

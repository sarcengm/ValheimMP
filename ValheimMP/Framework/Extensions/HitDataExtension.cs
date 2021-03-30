namespace ValheimMP.Framework.Extensions
{
    public static class HitDataExtension
    {
        /// <summary>
        /// If the attacker is a player, get their ID, if not return 0
        /// 
        /// Server side only
        /// </summary>
        /// <param name="hitData"></param>
        /// <returns></returns>
        public static long GetAttackingPlayerID(this HitData hitData)
        {
            if (hitData.m_attackerCharacter is Player player)
                return player.GetPlayerID();
            return 0;
        }
    }
}

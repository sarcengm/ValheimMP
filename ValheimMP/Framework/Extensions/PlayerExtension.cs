using UnityEngine;

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

        public static bool InInteractRange(this Player player, Vector3 point)
        {
            return (player.transform.position - point).sqrMagnitude < player.GetMaxSqrInteractRange();
        }
    }
}

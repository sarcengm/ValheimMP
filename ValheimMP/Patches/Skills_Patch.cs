using HarmonyLib;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Skills_Patch
    {
        [HarmonyPatch(typeof(Skills), "Awake")]
        [HarmonyPostfix]
        private static void Awake(Skills __instance)
        {
            var m_nview = __instance.GetComponentInParent<ZNetView>();
            if (m_nview != null && m_nview.IsValid() && m_nview.IsOwner())
            {
                m_nview.Register("SkillLevelUp", (long sender, int skillType, float level) =>
                {
                    RPC_SkillLevelUp(__instance, sender, skillType, level);
                });
            }
        }

        private static bool RPC_SkillLevelUp(Skills __instance, long sender, int skillType, float level)
        {
            if (!ZNet_Patch.IsRPCAllowed(__instance, sender))
                return false;

            var m_nview = __instance.GetComponentInParent<ZNetView>();
            if (m_nview == null || !m_nview.IsOwner())
                return false;

            var eSkillType = (Skills.SkillType)skillType;
            var skill = __instance.GetSkill(eSkillType);

            skill.m_level = level;
            skill.m_accumulator = 0f;

            __instance.m_player.OnSkillLevelup(eSkillType, skill.m_level);
            MessageHud.MessageType type = (((int)level != 0) ? MessageHud.MessageType.TopLeft : MessageHud.MessageType.Center);
            __instance.m_player.Message(type, "$msg_skillup $skill_" + skill.m_info.m_skill.ToString().ToLower() + ": " + (int)skill.m_level, 0, skill.m_info.m_icon);
            Gogan.LogEvent("Game", "Levelup", eSkillType.ToString(), (int)skill.m_level);

            return false;
        }

        [HarmonyPatch(typeof(Skills), "RaiseSkill")]
        [HarmonyPrefix]
        private static bool RaiseSkill(ref Skills __instance, Skills.SkillType skillType, float factor)
        {
            if (skillType == Skills.SkillType.None)
            {
                return false;
            }
            var skill = __instance.GetSkill(skillType);
            float level = skill.m_level;
            if (skill.Raise(factor))
            {
                if (__instance.m_useSkillCap)
                {
                    __instance.RebalanceSkills(skillType);
                }

                var m_nview = __instance.GetComponentInParent<ZNetView>();
                if (m_nview != null && ZNet.instance.IsServer() && level > 0)
                {
                    m_nview.InvokeRPC("SkillLevelUp", (int)skillType, level);
                }
            }
            return false;
        }
    }
}

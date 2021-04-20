using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class EnemyHud_Patch
    {
        private static List<EnemyHud.HudData> m_partyFrames = new();

        internal static void ClearPartyFrames()
        {
            while (m_partyFrames.Count > 0)
            {
                UnityEngine.Object.Destroy(m_partyFrames[m_partyFrames.Count - 1].m_gui);
                m_partyFrames.RemoveAt(m_partyFrames.Count - 1);
            }
        }

        [HarmonyPatch(typeof(EnemyHud), "UpdateHuds")]
        [HarmonyPostfix]
        private static void UpdateHuds(EnemyHud __instance)
        {
            UpdatePartyFrames();
        }

        private static void UpdatePartyFrames()
        {
            var party = ValheimMP.Instance.PlayerGroupManager.GetGroupByType(ZNet.instance.GetUID(), Framework.PlayerGroupType.Party);
            if (party != null && ValheimMP.Instance.PartyFramesEnabled.Value)
            {
                while (m_partyFrames.Count > party.MemberList.Count)
                {
                    UnityEngine.Object.Destroy(m_partyFrames[m_partyFrames.Count - 1].m_gui);
                    m_partyFrames.RemoveAt(m_partyFrames.Count - 1);
                }

                for (int i = 0; i < party.MemberList.Count; i++)
                {
                    if (i >= m_partyFrames.Count) 
                    {
                        m_partyFrames.Add(AddPartyHud(i));
                    }

                    var member = party.MemberList[i];
                    var hud = m_partyFrames[i];
                    var healthPercentage = Mathf.Clamp(member.PlayerHealth / member.PlayerMaxHealth, 0, 1);
                    hud.m_name.text = member.Name;
                    hud.m_healthSlow.SetValue(healthPercentage);
                    hud.m_healthFast.SetValue(healthPercentage);
                }
            }
            else
            {
                while (m_partyFrames.Count > 0)
                {
                    UnityEngine.Object.Destroy(m_partyFrames[m_partyFrames.Count - 1].m_gui);
                    m_partyFrames.RemoveAt(m_partyFrames.Count - 1);
                }
            }
        }

        private static EnemyHud.HudData AddPartyHud(int index)
        {
            var color = ValheimMP.Instance.ChatPartyColor.Value;

            var partyhud = new EnemyHud.HudData();
            // Maybe get some custom gui some day =p
            partyhud.m_gui = UnityEngine.Object.Instantiate(EnemyHud.instance.m_baseHudPlayer, EnemyHud.instance.m_hudRoot.transform);
            partyhud.m_gui.SetActive(value: true);
            partyhud.m_healthRoot = partyhud.m_gui.transform.Find("Health").gameObject;
            partyhud.m_healthFast = partyhud.m_healthRoot.transform.Find("health_fast").GetComponent<GuiBar>();
            partyhud.m_healthFast.m_bar.GetComponent<Image>().color = color * 0.8f;
            partyhud.m_healthSlow = partyhud.m_healthRoot.transform.Find("health_slow").GetComponent<GuiBar>();
            partyhud.m_name = partyhud.m_gui.transform.Find("Name").GetComponent<Text>();
            partyhud.m_name.color = color;
            partyhud.m_gui.transform.position = ValheimMP.Instance.PartyFramesPosition.Value + ValheimMP.Instance.PartyFramesOffset.Value * index;
            partyhud.m_gui.transform.localScale = ValheimMP.Instance.PartyFramesScale.Value;
            return partyhud;
        }

        [HarmonyPatch(typeof(EnemyHud), "ShowHud")]
        [HarmonyPostfix]
        private static void ShowHud(EnemyHud __instance, Character c)
        {
            if (c is Player player && __instance.m_huds.TryGetValue(c, out var value))
            {
                if (ValheimMP.Instance.PlayerGroupManager.ArePlayersInTheSameGroup(ZNet.instance.GetUID(), player.GetPlayerID(), Framework.PlayerGroupType.Party))
                {
                    var color = ValheimMP.Instance.ChatPartyColor.Value;
                    value.m_name.color = color;
                    value.m_healthFast.m_bar.GetComponent<Image>().color = color * 0.8f;
                }
                else if (ValheimMP.Instance.PlayerGroupManager.ArePlayersInTheSameGroup(ZNet.instance.GetUID(), player.GetPlayerID(), Framework.PlayerGroupType.Clan))
                {
                    var color = ValheimMP.Instance.ChatPartyColor.Value;
                    value.m_name.color = color;
                    value.m_healthFast.m_bar.GetComponent<Image>().color = color * 0.8f;
                }
                else
                {
                    value.m_name.color = Color.white;
                    value.m_healthFast.m_bar.GetComponent<Image>().color = new Color(1, 0.12f, 0.12f);
                }
            }
        }
    }
}

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
        private class PartyHudData
        {
            public GameObject m_gui;

            public GameObject m_healthRoot;

            public GuiBar m_healthFast;

            public GuiBar m_healthSlow;

            public Text m_name;
        }

        private static List<PartyHudData> m_partyFrames = new();

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
                var color = ValheimMP.Instance.ChatPartyColor.Value;

                var usedframes = 0;
                for (int i = 0; i < party.MemberList.Count; i++)
                {
                    var member = party.MemberList[i];

                    var isOffline = member.PlayerZDOID == ZDOID.None;

                    if (!ValheimMP.Instance.PartyFramesShowOffline.Value && isOffline)
                        continue;
                    if (!ValheimMP.Instance.PartyFramesShowSelf.Value && member.Id == ZNet.instance.GetUID())
                        continue;

                    
                    if (usedframes >= m_partyFrames.Count) 
                    {
                        m_partyFrames.Add(AddPartyHud(usedframes));
                    }

                    
                    var hud = m_partyFrames[usedframes];
                    var healthPercentage = Mathf.Clamp(member.PlayerHealth / member.PlayerMaxHealth, 0, 1);
                    hud.m_name.text = member.Name;
                    hud.m_name.color = color * (isOffline? 0.5f : 1.0f);
                    hud.m_healthSlow.SetValue(isOffline ? 0f : healthPercentage);
                    hud.m_healthFast.SetValue(healthPercentage);
                    hud.m_healthFast.m_barImage.color = color * (isOffline ? 0.3f : 1.0f) * 0.8f;
                    usedframes++;
                }

                while (m_partyFrames.Count > usedframes)
                {
                    UnityEngine.Object.Destroy(m_partyFrames[m_partyFrames.Count - 1].m_gui);
                    m_partyFrames.RemoveAt(m_partyFrames.Count - 1);
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

        private static PartyHudData AddPartyHud(int index)
        {
            var hud = new PartyHudData();
            // Maybe get some custom gui some day =p
            hud.m_gui = UnityEngine.Object.Instantiate(EnemyHud.instance.m_baseHudPlayer, EnemyHud.instance.m_hudRoot.transform);
            hud.m_gui.SetActive(value: true);
            hud.m_healthRoot = hud.m_gui.transform.Find("Health").gameObject;
            hud.m_healthFast = hud.m_healthRoot.transform.Find("health_fast").GetComponent<GuiBar>();
            hud.m_healthSlow = hud.m_healthRoot.transform.Find("health_slow").GetComponent<GuiBar>();
            hud.m_name = hud.m_gui.transform.Find("Name").GetComponent<Text>();
            var pos = ValheimMP.Instance.PartyFramesPosition.Value;
            if (pos.x < 0) pos.x = Screen.width + pos.x;
            if (pos.y < 0) pos.y = Screen.height + pos.y;

            hud.m_gui.transform.position = pos + ValheimMP.Instance.PartyFramesOffset.Value * index;
            hud.m_gui.transform.localScale = ValheimMP.Instance.PartyFramesScale.Value;
            return hud;
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
                    value.m_healthFast.m_barImage.color = color * 0.8f;
                }
                else if (ValheimMP.Instance.PlayerGroupManager.ArePlayersInTheSameGroup(ZNet.instance.GetUID(), player.GetPlayerID(), Framework.PlayerGroupType.Clan))
                {
                    var color = ValheimMP.Instance.ChatPartyColor.Value;
                    value.m_name.color = color;
                    value.m_healthFast.m_barImage.color = color * 0.8f;
                }
                else
                {
                    value.m_name.color = Color.white;
                    value.m_healthFast.m_barImage.color = new Color(1, 0.12f, 0.12f);
                }
            }
        }

    }
}

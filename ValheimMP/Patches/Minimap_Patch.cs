using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using static Minimap;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Minimap_Patch
    {
        // If I ever decided to network the mapdata again this should be re-enabled so it doesn't load local map data before synced with server
        //[HarmonyPatch(typeof(Minimap), "Update")]
        //[HarmonyTranspiler]
        //private static IEnumerable<CodeInstruction> Update(IEnumerable<CodeInstruction> instructions)
        //{
        //    var list = instructions.ToList();
        //    for (var i = 0; i < list.Count; i++)
        //    {
        //        if (list[i].Calls(AccessTools.Method(typeof(Minimap), "LoadMapData")))
        //        {
        //            list[i - 1].opcode = System.Reflection.Emit.OpCodes.Nop;
        //            list[i].opcode = System.Reflection.Emit.OpCodes.Nop;
        //        }
        //    }
        //    return list.AsEnumerable();
        //}

        private static List<PartyPinData> m_partyPins = new List<PartyPinData>();
        private static Dictionary<long, PartyPinData> m_partyPinsById = new Dictionary<long, PartyPinData>();

        [HarmonyPatch(typeof(Minimap), "UpdatePlayerPins")]
        [HarmonyPrefix]
        private static bool UpdatePlayerPins(Minimap __instance, float dt)
        {
            var man = ValheimMP.Instance.PlayerGroupManager;
            var party = man.GetGroupByType(ZNet.instance.GetUID(), Framework.PlayerGroupType.Party);

            if (party == null)
                return false;

            for (int i = 0; i < m_partyPins.Count; i++)
            {
                var pin = m_partyPins[i];
                if (!party.Members.ContainsKey(pin.m_playerId))
                {
                    __instance.RemovePin(m_partyPins[i]);
                    m_partyPins.RemoveAt(i);
                    m_partyPinsById.Remove(pin.m_playerId);
                    i--;
                }
            }

            var members = party.Members.Values.ToList();
            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];

                if (member.Id == ZNet.instance.GetUID())
                    continue;

                if (member.PlayerZDOID == ZDOID.None)
                    continue;

                if (!m_partyPinsById.TryGetValue(member.Id, out var pin))
                {
                    pin = AddPartyPin(__instance);
                    pin.m_playerId = member.Id;
                    pin.m_name = member.Name;

                    m_partyPins.Add(pin);
                    m_partyPinsById.Add(member.Id, pin);
                }

                pin.m_pos = member.PlayerPosition;
            }

            return false;
        }

        internal class PartyPinData : PinData
        {
            internal long m_playerId;
        }

        internal static PartyPinData AddPartyPin(Minimap minimap)
        {
            PartyPinData pinData = new PartyPinData();
            pinData.m_type = PinType.Player;
            pinData.m_icon = minimap.GetSprite(PinType.Player);
            pinData.m_save = false;
            pinData.m_checked = false;
            minimap.m_pins.Add(pinData);
            return pinData;
        }

    }
}

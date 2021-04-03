using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class SEMan_Patch
    {
        internal class NetworkedStatusEffect
        {
            int m_nameHash;
            float m_time;
            float m_syncTime;
            internal int m_tagTime;

            internal bool Serialize(StatusEffect se, ZPackage pkg)
            {
                var updated = false;
                var syncDiff = Time.time - m_syncTime;
                var timeDiff = Mathf.Abs(m_time + syncDiff - se.m_time);

                if (timeDiff > 1.0f)
                {
                    m_nameHash = se.name.GetStableHashCode();
                    m_time = se.m_time;
                    m_syncTime = Time.time;

                    pkg.Write(m_nameHash);
                    pkg.Write(m_time);

                    updated = true;
                }

                m_tagTime = Time.frameCount;
                return updated;
            }

            internal bool SerializeDestroy(ZPackage pkg)
            {
                var updated = false;

                if (m_tagTime != Time.frameCount)
                {
                    pkg.Write(m_nameHash);
                    pkg.Write(0f);

                    updated = true;
                }

                return updated;
            }

            internal static void Deserialize(SEMan seman, ZPackage pkg)
            {
                var hash = pkg.ReadInt();
                var time = pkg.ReadSingle();

                var se = seman.m_statusEffects.SingleOrDefault(k => k.name.GetStableHashCode() == hash);

                if (se != null)
                {
                    if (time == 0)
                    {
                        seman.RemoveStatusEffect(se);
                    }
                    else
                    {
                        se.m_time = time;
                    }
                }
                else if(time > 0)
                {
                    se = seman.AddStatusEffect(ObjectDB.instance.GetStatusEffect(hash));
                    se.m_time = time;
                }
            }
        }

        [HarmonyPatch(typeof(SEMan), MethodType.Constructor, new[] { typeof(Character), typeof(ZNetView) })]
        [HarmonyPostfix]
        private static void Constructor(SEMan __instance)
        {
            __instance.m_clientStatus = new Dictionary<int, NetworkedStatusEffect>();

            if (!ValheimMP.IsDedicated && __instance.m_nview != null)
            {
                __instance.m_nview.Register("StatusEffectData", (long sender, ZPackage pkg) =>
                {
                    RPC_StatusEffectData(__instance, sender, pkg);
                });
            }
        }

        private static void Sync(SEMan __instance)
        {
            var clientStatus = (Dictionary<int, NetworkedStatusEffect>)__instance.m_clientStatus;

            var hasUpdates = false;
            var pkg = new ZPackage();

            for (int i = 0; i < __instance.m_statusEffects.Count; i++)
            {
                var se = __instance.m_statusEffects[i];
                var sehash = se.name.GetStableHashCode();
                if (!clientStatus.TryGetValue(sehash, out var networkedStatusEffect))
                {
                    networkedStatusEffect = new NetworkedStatusEffect();
                    clientStatus.Add(sehash, networkedStatusEffect);
                }

                hasUpdates |= networkedStatusEffect.Serialize(se, pkg);
            }

            foreach(var nse in clientStatus.ToList())
            {
                if (nse.Value.SerializeDestroy(pkg))
                {
                    clientStatus.Remove(nse.Key);
                    hasUpdates = true;
                }
            }

            if (hasUpdates)
            {
                __instance.m_nview.InvokeRPC("StatusEffectData", pkg);
            }
        }

        private static void RPC_StatusEffectData(SEMan __instance, long sender, ZPackage pkg)
        {
            while (pkg.GetPos() < pkg.Size())
            {
                NetworkedStatusEffect.Deserialize(__instance, pkg);
            }
        }

        [HarmonyPatch(typeof(SEMan), "Update")]
        [HarmonyPostfix]
        private static void Update(SEMan __instance, float dt)
        {
            if (!ValheimMP.IsDedicated)
                return;

            // do we need to sync all characters? if so we need to not only send it to the owner but to every peer..
            // requires slight different code. but I believe all FX already synced and the clients themselves dont really need
            // to know about the effects on other characters or people.
            if (__instance.m_character.IsOwner())
                return;

            if (Time.time - __instance.m_clientStatusSyncTime > 0.1f)
            {
                __instance.m_clientStatusSyncTime = Time.time;
                Sync(__instance);
            }
        }

        [HarmonyPatch(typeof(SEMan), "RPC_AddStatusEffect")]
        [HarmonyPrefix]
        private static bool RPC_AddStatusEffect(SEMan __instance, long sender, string name, bool resetTime)
        {
            return ZNet_Patch.IsRPCAllowed(__instance, sender);
        }

        // these things are continues and should just be similated on the client.

        [HarmonyPatch(typeof(SEMan), "AddStatusEffect", new[] { typeof(string), typeof(bool) })]
        [HarmonyPrefix]
        private static bool AddStatusEffect(SEMan __instance, ref StatusEffect __result, string name, bool resetTime)
        {
            if (__instance.m_nview.IsOwner())
            {
                // simulate on owning client
                __result = __instance.Internal_AddStatusEffect(name, resetTime);
            }
            else if (ZNet.instance != null && ZNet.instance.IsServer())
            {

                __result = __instance.Internal_AddStatusEffect(name, resetTime);
            }
            else
            {
                __result = null;
            }
            return false;
        }
    }
}

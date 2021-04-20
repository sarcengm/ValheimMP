using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using ValheimMP.Framework.Events;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Patches
{


    [HarmonyPatch]
    internal class ZNet_Patch
    {
        /// <summary>
        /// Check if the current RPC is allowed from the sender.
        /// 
        /// Server -> Client = Valid.
        /// Server -> Server = Valid.
        /// Client -> Client (self) = Valid.
        /// Client -> Client = Invalid.
        /// Client -> Server = Invalid.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="sender"></param>
        /// <returns></returns>
        public static bool IsRPCAllowed(object __instance, long sender)
        {
            // RPC ourselves? eh sure why not.
            if (sender == ZNet.instance.GetUID() || sender == 0L)
                return true;

            if (ZNet.instance.IsServer() && sender != ZNet.instance.GetUID())
            {
                DebugMod.LogComponent(__instance, "Received RPC from client " + sender);
                return false;
            }

            if (!ZNet.instance.IsServer() && ZNet.GetConnectionStatus() == ZNet.ConnectionStatus.Connected && sender != ZNet.instance.GetServerPeer().m_uid)
            {
                DebugMod.LogComponent(__instance, "Received RPC from non server " + sender);
                return false;
            }
            return true;
        }


        public static long GetServerID()
        {
            if (ZNet.instance.IsServer())
                return ZDOMan.instance.GetMyID();
            return ZNet.instance.GetServerPeer().m_uid;
        }

        private static IEnumerable<CodeInstruction> SendPeerInfo(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].opcode == OpCodes.Ldstr && "PeerInfo".Equals(list[i].operand))
                {
                    var labels = list[i - 1].ExtractLabels();
                    list.InsertRange(i - 1, new CodeInstruction[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0).WithLabels(labels), // znet
                        new CodeInstruction(OpCodes.Ldarg_1), // rpc
                        new CodeInstruction(OpCodes.Ldloc_0), // pkg
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ZNet_Patch), "SendValheimMPPeerInfo"))
                    });
                    break;
                }
            }

            return list;
        }

        private static IEnumerable<CodeInstruction> RPC_PeerInfo(ILGenerator generator, IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            int i = 0;

            if (ValheimMP.IsDedicated)
            {
                for (; i < list.Count; i++)
                {
                    if (list[i].Calls(AccessTools.Method(typeof(ZPackage), "ReadLong")))
                    {
                        i++;
                        list.InsertRange(i, new CodeInstruction[]
                        {
                        new CodeInstruction(OpCodes.Pop),
                        new CodeInstruction(OpCodes.Ldloc_0), //peer
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ZNetPeerExtension), "GetSteamID")),
                        });
                        break;
                    }
                }
            }

            for (; i < list.Count; i++)
            {
                if (list[i].Calls(AccessTools.Method(typeof(WorldGenerator), "Initialize")))
                {
                    //ldsfld class World ZNet::m_world
                    //call void WorldGenerator::Initialize(class World)
                    list.RemoveRange(i - 1, 2);
                    break;
                }
            }

            for (; i < list.Count; i++)
            {
                if (list[i].StoresField(AccessTools.Field(typeof(ZNetPeer), "m_refPos")))
                {
                    // peer.m_refPos = refPos;
                    //ldloc.0
                    //ldloc.s 5
                    //stfld valuetype[UnityEngine.CoreModule]UnityEngine.Vector3 ZNetPeer::m_refPos
                    list[i - 2].opcode = OpCodes.Nop; // this one has a label, just nop it and keep the label.
                    list.RemoveRange(i - 1, 2);
                    break;
                }
            }

            for (; i < list.Count; i++)
            {
                if (list[i].StoresField(AccessTools.Field(typeof(ZNetPeer), "m_playerName")))
                {
                    var jumpLabel = generator.DefineLabel();
                    var generatorJump = generator.DefineLabel();

                    list.InsertRange(i + 1, new CodeInstruction[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1), //rpc
                        new CodeInstruction(OpCodes.Ldarg_2), //pkg
                        new CodeInstruction(OpCodes.Ldloc_0), //peer
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ZNet_Patch), "ValheimMPPeerInfo")),
                        new CodeInstruction(OpCodes.Brtrue_S, jumpLabel),
                        new CodeInstruction(OpCodes.Ret),
                        new CodeInstruction(OpCodes.Nop).WithLabels(jumpLabel),

                        // We reinsert the WorldGenerator.Initialize() that we removed earlier
                        new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(ValheimMP), "IsDedicated")),
                        new CodeInstruction(OpCodes.Brtrue_S, generatorJump),
                        new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(ZNet), "m_world")),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WorldGenerator), "Initialize")),
                        new CodeInstruction(OpCodes.Nop).WithLabels(generatorJump),
                    });

                    break;
                }
            }

            return list;
        }
 
        /// <summary>
        /// Send valheim MP information, identifier, version, and possible settings.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="pkg"></param>
        private static void SendValheimMPPeerInfo(ZNet __instance, ZRpc rpc, ZPackage pkg)
        {
            ValheimMP.Log("SendValheimMPPeerInfo");
            pkg.Write(ValheimMP.ProtocolIdentifier);
            pkg.Write(ValheimMP.Version);

            var dic = new Dictionary<int, byte[]>();

            if (ValheimMP.IsDedicated)
            {
                dic.SetCustomData("SteamID", (long)(rpc.GetSocket() as ZSteamSocket).GetPeerID().m_SteamID);
                dic.SetCustomData("UseZDOCompression", ValheimMP.Instance.UseZDOCompression.Value);
                dic.SetCustomData("RespawnDelay", ValheimMP.Instance.RespawnDelay.Value);
                dic.SetCustomData("ForcedPVPDistanceForBiomesOnly", ValheimMP.Instance.ForcedPVPDistanceForBiomesOnly.Value);
                dic.SetCustomData("ForcedPVPDistanceFromCenter", ValheimMP.Instance.ForcedPVPDistanceFromCenter.Value);
                Heightmap.Biome pvpbiomes = Heightmap.Biome.None;
                foreach (var item in ValheimMP.Instance.ForcedPVPBiomes)
                {
                    if (item.Value.Value)
                        pvpbiomes |= item.Key;
                }
                dic.SetCustomData("ForcedPVPBiomes", pvpbiomes);

                ValheimMP.Instance.Internal_OnServerSendPeerInfo(rpc, dic);
            }
            else
            {
                ValheimMP.Instance.Internal_OnClientSendPeerInfo(rpc, dic);
            }

            pkg.Write(dic);
        }

        /// <summary>
        /// Reading Valheim MP peer info
        /// </summary>
        /// <param name="rpc"></param>
        /// <param name="pkg"></param>
        /// <param name="peer"></param>
        /// <returns>False if the connection is aborted.</returns>
        private static bool ValheimMPPeerInfo(ZRpc rpc, ZPackage pkg, ZNetPeer peer)
        {
            ValheimMP valheimMP = ValheimMP.Instance;
            if (pkg.GetPos() >= pkg.Size())
            {
                if (ZNet.m_isServer)
                {
                    ValheimMP.Log("Peer is Non-Valheim MP Client.");
                    rpc.Invoke("Error", 3);
                    return false;
                }
                else
                {
                    valheimMP.SetIsOnValheimMPServer(false);
                    ValheimMP.Log("Connected to Non-Valheim MP Server");
                    return true;
                }
            }

            string valheimMPIdentifier = String.Empty;
            try
            {
                valheimMPIdentifier = pkg.ReadString();
            }
            catch (Exception ex)
            {
                ValheimMP.Log($"Exception trying to read ValheimMP Identifier: {ex}");
            }

            var validIdentifier = string.Compare(valheimMPIdentifier, ValheimMP.ProtocolIdentifier) == 0;
            if (!validIdentifier)
            {
                ValheimMP.LogError($"Valheim MP Identifier mismatch: {valheimMPIdentifier} vs. mine {ValheimMP.ProtocolIdentifier}");
                if (ZNet.m_isServer)
                {
                    rpc.Invoke("Error", 3);
                }
                else
                {
                    ZNet.m_connectionStatus = ZNet.ConnectionStatus.ErrorVersion;
                }
                return false;
            }

            var valheimMPVersion = pkg.ReadString();
            var validVersion = string.Compare(valheimMPVersion, ValheimMP.Version) == 0;
            if (!validVersion)
            {
                ValheimMP.LogError($"Valheim MP Version mismatch: {valheimMPVersion} vs. mine {ValheimMP.Version}");
                if (ZNet.m_isServer)
                {
                    rpc.Invoke("Error", 3);
                }
                else
                {
                    ZNet.m_connectionStatus = ZNet.ConnectionStatus.ErrorVersion;
                }
                return false;
            }

            valheimMP.SetIsOnValheimMPServer(true);

            var customData = new Dictionary<int, byte[]>();
            pkg.Read(ref customData);

            if (!ValheimMP.IsDedicated)
            {
                var userId = customData.GetCustomData<long>("SteamID");
                ZDOMan.instance.m_myid = userId;
                ZNet.instance.m_routedRpc.SetUID(userId);
                ValheimMP.Log($"SteamID: {userId}");
                valheimMP.UseZDOCompression.Value = customData.GetCustomData<bool>("UseZDOCompression");
                ValheimMP.Log($"UseZDOCompression: {valheimMP.UseZDOCompression.Value}");
                valheimMP.RespawnDelay.Value = customData.GetCustomData<float>("RespawnDelay");
                ValheimMP.Log($"RespawnDelay: {valheimMP.RespawnDelay.Value}");
                valheimMP.ForcedPVPDistanceForBiomesOnly.Value = customData.GetCustomData<float>("ForcedPVPDistanceForBiomesOnly");
                ValheimMP.Log($"ForcedPVPDistanceForBiomesOnly: {valheimMP.ForcedPVPDistanceForBiomesOnly.Value}");
                valheimMP.ForcedPVPDistanceFromCenter.Value = customData.GetCustomData<float>("ForcedPVPDistanceFromCenter");
                ValheimMP.Log($"ForcedPVPDistanceFromCenter: {valheimMP.ForcedPVPDistanceFromCenter.Value}");
                var biomes = customData.GetCustomData<Heightmap.Biome>("ForcedPVPBiomes");
                ValheimMP.Log($"ForcedPVPBiomes:");
                foreach (var key in valheimMP.ForcedPVPBiomes.Keys.ToList()) 
                {
                    valheimMP.ForcedPVPBiomes[key].Value = (biomes & key) == key;
                    ValheimMP.Log($" {key} = {(biomes & key) == key}");
                }

                Minimap.instance.m_hasGenerated = false;

                string serverIdentifier;

                if (ZNet.m_serverSteamID == 0)
                {
                    ZNet.m_serverIPAddr.ToString(out serverIdentifier, bWithPort: true);
                }
                else
                {
                    serverIdentifier = ZNet.m_serverSteamID.ToString();
                }

                Game.instance.m_playerProfile = new PlayerProfile(serverIdentifier.Replace(":", "_"));
                Game.instance.m_playerProfile.Load();

                Game.instance.m_firstSpawn = true;
                Game.instance.GetPlayerProfile().m_playerID = userId;
                ValheimMP.Log("Connected to Valheim MP Server: " + valheimMPIdentifier + valheimMPVersion);

                var args = new OnClientConnectArgs()
                {
                    Rpc = rpc,
                    Peer = peer,
                    CustomData = customData,
                    AbortConnect = false,
                };

                valheimMP.Internal_OnClientConnect(args);

                if (args.AbortConnect)
                {
                    ValheimMP.Log($"OnClientConnect AbortConnect");
                    return false;
                }
            }
            else
            {
                var args = new OnServerConnectArgs()
                {
                    Rpc = rpc,
                    Peer = peer,
                    CustomData = customData,
                    AbortConnect = false,
                };

                valheimMP.Internal_OnServerConnectBeforeProfileLoad(args);

                if (args.AbortConnect)
                {
                    ValheimMP.Log($"OnServerConnectBeforeProfileLoad AbortConnect");
                    return false;
                }

                var steamId = (peer.m_socket as ZSteamSocket).GetPeerID();
                peer.m_playerProfile = new PlayerProfile(steamId.ToString());
                // Loading can and should possibly be done async?
                var loaded = peer.m_playerProfile.Load();
                if (!loaded)
                {
                    peer.m_playerProfile.SetName(peer.m_playerName);
                    peer.m_playerProfile.m_playerID = (long)steamId.m_SteamID;
                }
                else
                {
                    peer.m_playerName = peer.m_playerProfile.m_playerName;
                }

                peer.m_firstSpawn = true;
                peer.m_requestRespawn = true;

                ValheimMP.Log($"Client connected to Valheim MP Server: {valheimMPIdentifier}  {valheimMPVersion}");

                valheimMP.Internal_OnServerConnect(args);

                if (args.AbortConnect)
                {
                    ValheimMP.Log($"OnServerConnect AbortConnect");
                    return false;
                }
            }

            if (ValheimMP.IsDedicated)
            {
                rpc.Register("InventoryGrid_DropItem", (ZRpc rpc, ZPackage pkg) =>
                {
                    InventoryGrid_Patch.RPC_DropItem(rpc, pkg);
                });

                rpc.Register("MoveItemToThis", (ZRpc rpc, ZDOID toId, ZDOID fromId, int itemId) =>
                {
                    Inventory_Patch.RPC_MoveItemToThis(rpc, toId, fromId, itemId);
                });

                rpc.Register("InventoryGui_DoCrafting", (ZRpc rpc, ZPackage pkg) =>
                {
                    InventoryGui_Patch.RPC_DoCrafting(rpc, pkg);
                });

                rpc.Register("InventoryGui_RepairOneItem", (ZRpc rpc, int itemId) =>
                {
                    InventoryGui_Patch.RPC_RepairOneItem(rpc, itemId);
                });

                rpc.Register("SyncPlayerMovement", (ZRpc rpc, ZPackage pkg) =>
                {
                    Player_Patch.RPC_SyncPlayerMovement(rpc, pkg);
                });
                rpc.Register("ReviveRequestAccept", (ZRpc rpc, ZDOID id) =>
                {
                    TombStone_Patch.RPC_ReviveRequestAccept(rpc, id);
                });

            }
            else if (ValheimMP.Instance.IsOnValheimMPServer)
            {
                rpc.Register("InventoryData", (ZRpc rpc, ZPackage pkg) =>
                {
                    RPC_InventoryData(rpc, pkg);
                });
                rpc.Register("ZDODestroy", (ZRpc rpc, ZPackage pkg) =>
                {
                    ZDOMan_Patch.RPC_ZDODestroy(pkg);
                });
                rpc.Register("PlayerGroupUpdate", (ZRpc rpc, ZPackage pkg) =>
                {
                    ValheimMP.Instance.PlayerGroupManager.RPC_PlayerGroupUpdate(rpc, pkg);
                });
                rpc.Register("PlayerGroupPlayerOffline", (ZRpc rpc, int groupId, long playerId) =>
                {
                    ValheimMP.Instance.PlayerGroupManager.RPC_PlayerGroupPlayerOffline(rpc, groupId, playerId);
                });
                rpc.Register("PlayerGroupRemovePlayer", (ZRpc rpc, int groupId, long playerId) =>
                {
                    ValheimMP.Instance.PlayerGroupManager.RPC_PlayerGroupRemovePlayer(rpc, groupId, playerId);
                });
                rpc.Register("ReviveRequest", (ZRpc rpc, ZDOID id, string playerName, long playerId) =>
                {
                    TombStone_Patch.RPC_ReviveRequest(rpc, id, playerName, playerId);
                });
            }

            return true;
        }

        [HarmonyPatch(typeof(ZNet), "SendPeriodicData")]
        [HarmonyPrefix]
        private static bool SendPeriodicData(float dt)
        {
            return ZNet.instance == null || ZNet.instance.IsServer();
        }

        [HarmonyPatch(typeof(ZNet), "SetCharacterID")]
        [HarmonyPrefix]
        private static bool SetCharacterID(ref ZNet __instance, ZDOID id)
        {
            return false;
        }

        public static bool ProfileLoaded { get; private set; }

        private static void RPC_PlayerProfile(ZNet __instance, ZRpc rpc, ZPackage pkg)
        {
            UnityEngine.Debug.Log("Received player profile for player character.");
            PlayerProfile_Patch.Deserialize(Game.instance.m_playerProfile, pkg);
            Game.instance.m_firstSpawn = true;
            ProfileLoaded = true;
            Player_Patch.CheckPlayerReady();
        }

        [HarmonyPatch(typeof(ZNet), "RPC_CharacterID")]
        [HarmonyPrefix]
        private static bool RPC_CharacterID(ref ZNet __instance, ZRpc rpc, ZDOID characterID)
        {
            UnityEngine.Debug.Log("Received ZDOID (" + characterID + ") for player character.");
            ZNet.instance.m_characterID = characterID;
            return false;
        }

        [HarmonyPatch(typeof(ZNet), "RPC_RefPos")]
        [HarmonyPrefix]
        private static bool RPC_RefPos(ZRpc rpc, Vector3 pos, bool publicRefPos)
        {
            if (!ZNet.instance.IsServer() && rpc == ZNet.instance.GetServerRPC())
            {
                ZNet.instance.SetReferencePosition(pos);
            }

            return false;
        }

        private static void RPC_InventoryData(ZRpc rpc, ZPackage pkg)
        {
            if (!ZNet.instance.IsServer() && rpc == ZNet.instance.GetServerRPC())
            {
                ValheimMP.Instance.InventoryManager.RPC_InventoryData(pkg);
            }
        }

        [HarmonyPatch(typeof(ZNet), "SaveWorld")]
        [HarmonyPrefix]
        private static void SaveWorld()
        {
            ValheimMP.Instance.Internal_OnWorldSave();
        }

        [HarmonyPatch(typeof(ZNet), "GetPeer", new[] { typeof(ZRpc) })]
        [HarmonyPrefix]
        private static bool GetPeer(ZRpc rpc, ref ZNetPeer __result)
        {
            __result = rpc.m_peer;
            return false;
        }

        [HarmonyPatch(typeof(ZNet), "GetPeer", new[] { typeof(long) })]
        [HarmonyPrefix]
        private static bool GetPeer(long uid, ref ZNetPeer __result)
        {
            m_peers.TryGetValue(uid, out __result);
            return false;
        }

        private static Dictionary<long, ZNetPeer> m_peers = new Dictionary<long, ZNetPeer>();

        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        [HarmonyPrefix]
        private static void OnNewConnection(ZNet __instance, ZNetPeer peer)
        {
            var uid = (long)(peer.m_socket as ZSteamSocket).m_peerID.GetSteamID().m_SteamID;

            if(m_peers.TryGetValue(uid, out var existingPeer))
            {
                // someone with the same steam ID connecting, possibly duplicate account?
                // also possibly the same person that has already timed out and is reconnecting
                if (existingPeer != null)
                {
                    __instance.Disconnect(existingPeer);
                }
                m_peers.Remove(uid);
            }

            m_peers.Add(uid, peer);
        }

        [HarmonyPatch(typeof(ZNet), "Disconnect")]
        [HarmonyPrefix]
        private static void Disconnect(ZNetPeer peer)
        {
            m_peers.Remove(peer.m_uid);
        }

        [HarmonyPatch(typeof(ZNet), "StopAll")]
        [HarmonyPostfix]
        private static void StopAll()
        {
            m_peers.Clear();
        }
    }
}

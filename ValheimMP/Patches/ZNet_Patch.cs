using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using ValheimMP.Framework;
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
            ZLog.Log("SendValheimMPPeerInfo");
            pkg.Write(ValheimMP.ProtocolIdentifier);
            pkg.Write(ValheimMP.Version);

            var dic = new Dictionary<int, byte[]>();

            if (ValheimMP.IsDedicated)
            {
                dic.SetCustomData("SteamID", (long)(rpc.GetSocket() as ZSteamSocket).GetPeerID().m_SteamID);
                dic.SetCustomData("UseZDOCompression", ValheimMP.Instance.UseZDOCompression.Value);
                dic.SetCustomData("RespawnDelay", ValheimMP.Instance.RespawnDelay.Value);

                ValheimMP.Instance.OnServerSendPeerInfo?.Invoke(rpc, dic);
            }
            else
            {
                ValheimMP.Instance.OnClientSendPeerInfo?.Invoke(rpc, dic);
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
                    ZLog.Log("Peer is Non-Valheim MP Client.");
                    rpc.Invoke("Error", 3);
                    return false;
                }
                else
                {
                    valheimMP.SetIsOnValheimMPServer(false);
                    ZLog.Log("Connected to Non-Valheim MP Server");
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
                ZLog.Log($"Exception trying to read ValheimMP Identifier: {ex}");
            }

            var validIdentifier = string.Compare(valheimMPIdentifier, ValheimMP.ProtocolIdentifier) == 0;
            if (!validIdentifier)
            {
                ZLog.LogError($"Valheim MP Identifier mismatch: {valheimMPIdentifier} vs. mine {ValheimMP.ProtocolIdentifier}");
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
                ZLog.LogError($"Valheim MP Version mismatch: {valheimMPVersion} vs. mine {ValheimMP.Version}");
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
                ZLog.Log("UID=" + userId);


                var useZDOCompression = customData.GetCustomData<bool>("UseZDOCompression");
                ZLog.Log("UseZDOCompression=" + useZDOCompression);
                valheimMP.UseZDOCompression.Value = useZDOCompression;

                var respawnDelay = customData.GetCustomData<float>("RespawnDelay");
                valheimMP.RespawnDelay.Value = respawnDelay;

                Game.instance.m_firstSpawn = true;
                Game.instance.GetPlayerProfile().m_playerID = userId;
                ZLog.Log("Connected to Valheim MP Server: " + valheimMPIdentifier + valheimMPVersion);

                if (valheimMP.OnClientConnect != null)
                {
                    foreach (ValheimMP.OnClientConnectDelegate del in valheimMP.OnClientConnect.GetInvocationList())
                    {
                        if (!del(rpc, peer, customData))
                            return false;
                    }
                }
            }
            else
            {
                if (valheimMP.OnServerConnectBeforeProfileLoad != null)
                {
                    foreach (ValheimMP.OnServerConnectBeforeProfileLoadDelegate del in valheimMP.OnServerConnectBeforeProfileLoad.GetInvocationList())
                    {
                        if (!del(rpc, peer, customData))
                            return false;
                    }
                }

                var steamId = (peer.m_socket as ZSteamSocket).GetPeerID();
                peer.m_playerProfile = new PlayerProfile(System.IO.Path.Combine(valheimMP.CharacterPath.Value, steamId.ToString()));
                // Loading can and should possibly be done async?
                var loaded = peer.m_playerProfile.Load();
                if (!loaded)
                {
                    peer.m_playerProfile.SetName(peer.m_playerName);
                    peer.m_playerProfile.m_playerID = (long)steamId.m_SteamID;
                }
                peer.m_firstSpawn = true;
                peer.m_requestRespawn = true;

                ZLog.Log($"Client connected to Valheim MP Server: {valheimMPIdentifier}  {valheimMPVersion}");

                if (valheimMP.OnServerConnect != null)
                {
                    foreach (ValheimMP.OnServerConnectDelegate del in valheimMP.OnServerConnect.GetInvocationList())
                    {
                        if (!del(rpc, peer, customData))
                            return false;
                    }
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

                rpc.Register("InventoryGui_DoCrafting", (ZRpc rpc, string recipeName, int upgradeItemId) =>
                {
                    InventoryGui_Patch.RPC_DoCrafting(rpc, recipeName, upgradeItemId);
                });

                rpc.Register("InventoryGui_RepairOneItem", (ZRpc rpc, int itemId) =>
                {
                    InventoryGui_Patch.RPC_RepairOneItem(rpc, itemId);
                });

                rpc.Register("SyncPlayerMovement", (ZRpc rpc, ZPackage pkg) =>
                {
                    Player_Patch.RPC_SyncPlayerMovement(rpc, pkg);
                });
            }
            else if (ValheimMP.Instance.IsOnValheimMPServer)
            {
                rpc.Register("InventoryData", (ZRpc rpc, ZPackage pkg) =>
                {
                    RPC_InventoryData(rpc, pkg);
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
                ValheimMP.Instance.InventoryManager.DeserializeRPC(pkg);
            }
        }
    }
}

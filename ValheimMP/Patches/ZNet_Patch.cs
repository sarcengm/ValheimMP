using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ValheimMP.Patches
{


    [HarmonyPatch]
    public class ZNet_Patch
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

        //[HarmonyPatch(typeof(ZNet), "SendPeerInfo")]
        //[HarmonyPrefix]
        private static bool SendPeerInfo(ref ZNet __instance, ZRpc rpc, string password = "")
        {
            ZPackage zPackage = new ZPackage();

            zPackage.Write(__instance.GetUID()); // TODO: This should be the steam ID, letting a client generate their own UID is madness!
            zPackage.Write(Version.GetVersionString());
            zPackage.Write(__instance.m_referencePosition);
            zPackage.Write(Game.instance.GetPlayerProfile().GetName());
            if (__instance.IsServer())
            {
                zPackage.Write(ZNet.m_world.m_name);
                zPackage.Write(ZNet.m_world.m_seed);
                zPackage.Write(ZNet.m_world.m_seedName);
                zPackage.Write(ZNet.m_world.m_uid);
                zPackage.Write(ZNet.m_world.m_worldGenVersion);
                zPackage.Write(__instance.m_netTime);
            }
            else
            {
                string data = (string.IsNullOrEmpty(password) ? "" : ZNet.HashPassword(password));
                zPackage.Write(data);
                byte[] array = ZSteamMatchmaking.instance.RequestSessionTicket();
                if (array == null)
                {
                    ZNet.m_connectionStatus = ZNet.ConnectionStatus.ErrorConnectFailed;
                    return false;
                }
                zPackage.Write(array);
            }

            // Add valheim identifier
            SendValheimMPPeerInfo(__instance, rpc, zPackage);

            rpc.Invoke("PeerInfo", zPackage);
            return false;
        }

        // TODO This function is an abomination :(, transpiler needed.
        //[HarmonyPatch(typeof(ZNet), "RPC_PeerInfo")]
        //[HarmonyPrefix]
        private static bool RPC_PeerInfo(ZNet __instance, ZRpc rpc, ZPackage pkg)
        {
            ZNetPeer peer = __instance.GetPeer(rpc);
            if (peer == null)
            {
                return false;
            }
            long num = pkg.ReadLong();
            string text = pkg.ReadString();
            string endPointString = peer.m_socket.GetEndPointString();
            string hostName = peer.m_socket.GetHostName();
            ZLog.Log("VERSION check their:" + text + "  mine:" + Version.GetVersionString());
            if (text != Version.GetVersionString())
            {
                if (ZNet.m_isServer)
                {
                    rpc.Invoke("Error", 3);
                }
                else
                {
                    ZNet.m_connectionStatus = ZNet.ConnectionStatus.ErrorVersion;
                }
                ZLog.Log("Peer " + endPointString + " has incompatible version, mine:" + Version.GetVersionString() + " remote " + text);
                return false;
            }
            Vector3 refPos = pkg.ReadVector3();
            string playerName = pkg.ReadString();
            if (ZNet.m_isServer)
            {
                if (!__instance.IsAllowed(hostName, playerName))
                {
                    rpc.Invoke("Error", 8);
                    ZLog.Log("Player " + playerName + " : " + hostName + " is blacklisted or not in whitelist.");
                    return false;
                }
                string text3 = pkg.ReadString();
                ZSteamSocket zSteamSocket = peer.m_socket as ZSteamSocket;
                byte[] ticket = pkg.ReadByteArray();
                if (!ZSteamMatchmaking.instance.VerifySessionTicket(ticket, zSteamSocket.GetPeerID()))
                {
                    ZLog.Log("Peer " + endPointString + " has invalid session ticket");
                    rpc.Invoke("Error", 8);
                    return false;
                }
                if (__instance.GetNrOfPlayers() >= __instance.m_serverPlayerLimit)
                {
                    rpc.Invoke("Error", 9);
                    ZLog.Log("Peer " + endPointString + " disconnected due to server is full");
                    return false;
                }
                if (ZNet.m_serverPassword != text3)
                {
                    rpc.Invoke("Error", 6);
                    ZLog.Log("Peer " + endPointString + " has wrong password");
                    return false;
                }

                num = (long)zSteamSocket.GetPeerID().m_SteamID;
                if (__instance.IsConnected(num))
                {
                    rpc.Invoke("Error", 7);
                    ZLog.Log("Already connected to peer with UID:" + num + "  " + endPointString);
                    return false;
                }
            }
            else
            {
                ZNet.m_world = new World();
                ZNet.m_world.m_name = pkg.ReadString();
                ZNet.m_world.m_seed = pkg.ReadInt();
                ZNet.m_world.m_seedName = pkg.ReadString();
                ZNet.m_world.m_uid = pkg.ReadLong();
                ZNet.m_world.m_worldGenVersion = pkg.ReadInt();
                __instance.m_netTime = pkg.ReadDouble();
            }

            peer.m_uid = num;
            peer.m_playerName = playerName;

            /// Valheim handshake
            if (!ValheimMPPeerInfo(rpc, pkg, peer))
                return false;


            if (!ZNet.m_isServer)
            {
                WorldGenerator.Initialize(ZNet.m_world);
            }

            rpc.Register<Vector3, bool>("RefPos", __instance.RPC_RefPos);
            rpc.Register<ZPackage>("PlayerList", __instance.RPC_PlayerList);
            rpc.Register<string>("RemotePrint", __instance.RPC_RemotePrint);
            if (ZNet.m_isServer)
            {
                rpc.Register<ZDOID>("CharacterID", __instance.RPC_CharacterID);
                rpc.Register<string>("Kick", __instance.RPC_Kick);
                rpc.Register<string>("Ban", __instance.RPC_Ban);
                rpc.Register<string>("Unban", __instance.RPC_Unban);
                rpc.Register("Save", __instance.RPC_Save);
                rpc.Register("PrintBanned", __instance.RPC_PrintBanned);

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
            else
            {
                rpc.Register<double>("NetTime", __instance.RPC_NetTime);

                if (ValheimMP.Instance.IsOnValheimMPServer)
                {
                    rpc.Register("InventoryData", (ZRpc rpc, ZPackage pkg) =>
                    {
                        RPC_InventoryData(__instance, rpc, pkg);
                    });
                }
            }
            if (ZNet.m_isServer)
            {
                __instance.SendPeerInfo(rpc);
                __instance.SendPlayerList();
            }
            else
            {
                ZNet.m_connectionStatus = ZNet.ConnectionStatus.Connected;
                Game.instance.m_firstSpawn = false;
            }
            __instance.m_zdoMan.AddPeer(peer);
            __instance.m_routedRpc.AddPeer(peer);

            return false;
        }

        /// <summary>
        /// Send valheim MP information, identifier, version, and possible settings.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="pkg"></param>
        private static void SendValheimMPPeerInfo(ZNet __instance, ZRpc rpc, ZPackage pkg)
        {
            pkg.Write(ValheimMP.ProtocolIdentifier);
            pkg.Write(ValheimMP.Version);

            if (__instance.IsServer())
            {
                pkg.Write((long)(rpc.GetSocket() as ZSteamSocket).GetPeerID().m_SteamID);
                pkg.Write(ValheimMP.Instance.UseZDOCompression);
            }
            else
            {

            }
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

            var valheimMPIdentifier = pkg.ReadString();
            var validIdentifier = string.Compare(valheimMPIdentifier, ValheimMP.ProtocolIdentifier) == 0;
            if (!validIdentifier)
            {
                ZLog.LogError("Valheim MP Identifier mismatch: " + valheimMPIdentifier + " vs. mine " + ValheimMP.ProtocolIdentifier);
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
                ZLog.LogError("Valheim MP Version mismatch: " + valheimMPVersion + " vs. mine " + ValheimMP.Version);
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

            if (!ZNet.m_isServer)
            {
                var userId = pkg.ReadLong();
                // TODO: this whole userid setting wouldnt be nessasary if it was set sooner like on the dedicated server side. But I'm unsure how to retrieve the SteamID at that point,
                // Not even sure it's possible at construction time.
                ZDOMan.instance.m_myid = userId;
                ZNet.instance.m_routedRpc.SetUID(userId);

                ZLog.Log("UID=" + userId);
                var useZDOCompression = pkg.ReadBool();
                ZLog.Log("UseZDOCompression=" + useZDOCompression);
                valheimMP.UseZDOCompression = useZDOCompression;
                Game.instance.m_firstSpawn = true;
                Game.instance.GetPlayerProfile().m_playerID = userId;
                ZLog.Log("Connected to Valheim MP Server: " + valheimMPIdentifier + valheimMPVersion);

                if (valheimMP.OnClientConnect != null)
                {
                    foreach (ValheimMP.OnClientConnectDel del in valheimMP.OnClientConnect.GetInvocationList())
                    {
                        if (!del(rpc))
                            return false;
                    }
                }
            }
            else
            {
                //var beard = 

                var steamId = (peer.m_socket as ZSteamSocket).GetPeerID();
                peer.m_playerProfile = new PlayerProfile(System.IO.Path.Combine(valheimMP.CharacterPath, steamId.ToString()));
                // Loading can and should possibly be done async?
                var loaded = peer.m_playerProfile.Load();
                if (!loaded)
                {
                    peer.m_playerProfile.SetName(peer.m_playerName);
                    peer.m_playerProfile.m_playerID = (long)steamId.m_SteamID;
                }
                peer.m_firstSpawn = true;
                peer.m_requestRespawn = true;

                ZLog.Log("Client connected to Valheim MP Server: " + valheimMPIdentifier + valheimMPVersion);

                if (valheimMP.OnServerConnect != null)
                {
                    foreach (ValheimMP.OnServerConnectDel del in valheimMP.OnServerConnect.GetInvocationList())
                    {
                        if (!del(peer))
                            return false;
                    }
                }
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

        private static void RPC_InventoryData(ZNet __instance, ZRpc rpc, ZPackage pkg)
        {
            if (!__instance.IsServer() && rpc == __instance.GetServerRPC())
            {
                Inventory_Patch.DeserializeRPC(pkg);
            }
        }
    }
}

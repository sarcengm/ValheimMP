using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Linq;
using ValheimMP.Patches;


namespace ValheimMP
{
    [BepInPlugin(BepInGUID, Name, Version)]
    public class ValheimMP : BaseUnityPlugin
    {
        public const string Author = "Sarcen";
        public const string Name = "ValheimMP";
        public const string Version = "0.1.0";
        public const string BepInGUID = "BepIn." + Author + "." + Name;
        public const string HarmonyGUID = "Harmony." + Author + "." + Name;
        public const string ProtocolIdentifier = "VMP";

        public static ValheimMP Instance { get; private set; }

        private Harmony m_harmonyHandshake;
        private Harmony m_harmony;

        /// <summary>
        /// If the current session is on a Valheim MP server (or if we are a Valheim MP server)
        /// </summary>
        public bool IsOnValheimMPServer { get; private set; } = true;

        /// <summary>
        /// Path where characters are stored
        /// </summary>
        public string CharacterPath { get; private set; } = "vmmp/";

        /// <summary>
        /// Does the current server use compression?
        /// </summary>
        public bool UseZDOCompression { get; internal set; }

        /// <summary>
        /// Whether to store and output the ZDO traffic usage
        /// </summary>
        public bool DebugOutputZDO { get; internal set; }

        public bool DebugRPC { get; internal set; }

        /// <summary>
        /// DebugOutputZDO information
        /// </summary>
        public Dictionary<int, Dictionary<string, int>> ZDODebug { get; internal set; } = new Dictionary<int, Dictionary<string, int>>();

        /// <summary>
        /// Turned in a variable just because I overwrote that whole function.
        /// </summary>
        public bool DoNotHideCharacterWhenCameraClose { get; internal set; }

        /// <summary>
        /// Fired when you connect to a Valheim MP server as Client
        /// 
        /// Only fired on the client that connects
        /// </summary>
        /// <param name="serverRpc">RPC of the server</param>
        /// <returns>False if we should abort. Only useful if you want to disconnect the user. Manual disconnect should still be called.</returns>
        public OnClientConnectDel OnClientConnect { get; set; }
        public delegate bool OnClientConnectDel(ZRpc serverRpc);

        /// <summary>
        /// Fired when someone joins the server.
        /// 
        /// Only fired on the server.
        /// </summary>
        /// <param name="peer"></param>
        /// <returns>False if we should abort. Only useful if you want to disconnect the user. Manual disconnect should still be called.</returns>
        public OnServerConnectDel OnServerConnect { get; set; }
        public delegate bool OnServerConnectDel(ZNetPeer peer);

        /// <summary>
        /// Fired when a chat message is received on the server.
        /// </summary>
        /// <returns>False if we should supress the message from being send.</returns>
        public OnChatMessageDel OnChatMessage { get; set; }
        public delegate bool OnChatMessageDel(ZNetPeer peer, Player player, ref string playerName, ref Vector3 messageLocation, ref float messageDistance, ref string text, ref Talker.Type type);

        public bool DebugShowZDOPlayerLocation { get; internal set; }

        /// <summary>
        /// Additional delay on both sending and receiving of packets.
        /// </summary>
        public float ArtificialPing { get; internal set; }

        public static bool IsDedicated { get; private set; }
        public float RespawnDelay { get; internal set; }

        // Awake is called once when both the game and the plug-in are loaded
        private void Awake()
        {
            Instance = this;

            m_harmonyHandshake = new Harmony(HarmonyGUID + ".hs");
            m_harmonyHandshake.Patch(AccessTools.Method(typeof(ZNet), "SendPeerInfo"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ZNet_Patch), "SendPeerInfo")));

            m_harmonyHandshake.Patch(AccessTools.Method(typeof(ZNet), "RPC_PeerInfo"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ZNet_Patch), "RPC_PeerInfo")));

            m_harmonyHandshake.Patch(AccessTools.Method(typeof(FejdStartup), "IsPublicPasswordValid"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(FejdStartup_Patch), "IsPublicPasswordValid")));

            m_harmonyHandshake.Patch(AccessTools.Method(typeof(FejdStartup), "Awake"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(FejdStartup_Patch), "Awake")));

            m_harmonyHandshake.Patch(AccessTools.Method(typeof(ZSteamSocket), "RegisterGlobalCallbacks"),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ZSteamSocket_Patch), "RegisterGlobalCallbacks")));

            m_harmony = new Harmony(HarmonyGUID);
            if (isDedicated())
            {
                m_harmony.PatchAll();
            }

            UseZDOCompression = true;
            DebugOutputZDO = false;
            DebugRPC = false;
            DebugShowZDOPlayerLocation = true;
            ArtificialPing = 125f;
            RespawnDelay = 10.0f;

            AddChatFuntions();

            //Directory.CreateDirectory(System.IO.Path.Combine(Utils.GetSaveDataPath(), CharacterPath));
        }

        private static bool isDedicated()
        {
            var dummy = new ZNet();
            if (dummy.IsDedicated())
            {
                IsDedicated = true;
            }
            dummy = null;
            GC.Collect(); // Lets pretend this does something, I just want this thing out of my sight!

            return IsDedicated;
        }

        public void SetIsOnValheimMPServer(bool b)
        {
            if (IsDedicated)
                return;

            if (IsOnValheimMPServer != b)
            {
                IsOnValheimMPServer = b;

                if (b)
                {
                    Logger.LogInfo($"Patching {HarmonyGUID}");
                    m_harmony.PatchAll();
                }
                else
                {
                    Logger.LogInfo($"Unpatching {HarmonyGUID}");
                    m_harmony.UnpatchAll(HarmonyGUID);
                }
            }
        }

        private void AddChatFuntions()
        {
            OnChatMessage += (ZNetPeer peer, Player player, ref string playerName, ref Vector3 messageLocation, ref float messageDistance, ref string text, ref Talker.Type type) =>
            {
                // This normally happens on the client but we supress it here so we can still send marked up text ourselves!
                text = text.Replace('<', ' ');
                text = text.Replace('>', ' ');

                if (text.StartsWith("/vmp", StringComparison.OrdinalIgnoreCase))
                {
                    ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ChatMessage", messageLocation, -1, "",
                        $"<color=white>Server is running <color=green><b>{Name}</b></color> version <color=green><b>{Version}</b></color>.</color>"
                    );
                    return false;
                }

                if (text.StartsWith("/claim", StringComparison.OrdinalIgnoreCase))
                {
                    var list = new List<Piece>();
                    var guardStones = 0;
                    var claimFor = text.Substring("/claim".Length);
                    long.TryParse(claimFor, out var claimForId);
                    if (claimForId == 0L)
                        claimForId = peer.m_uid;

                    Piece.GetAllPiecesInRadius(player.transform.position, 20f, list);
                    foreach (var item in list.Where(item => item.GetComponent<PrivateArea>() != null))
                    {
                        item.m_creator = claimForId;
                        item.m_nview.GetZDO().Set(Piece.m_creatorHash, claimForId);
                        guardStones++;
                    }

                    ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ChatMessage", messageLocation, -1, "",
                        $"<color=white>Claimed <color=green>{guardStones}</color> guardstones for <color=green>{claimForId}</color>.</color>"
                    );

                    return false;
                }

                return true;
            };
        }

        /// <summary>
        /// Dummy function to easily generate all the fields I created inside various classes.
        ///
        /// These fields will be inserted by the patcher
        /// </summary>
        private static void DummyMemberCreation()
        {
            var player = new Player();
            player.m_delayedDamage = new Queue<KeyValuePair<float, HitData>>();
            player.m_lastMoveFlags = 0;
            player.m_lastMoveDir = new Vector3();
            player.m_clientPosition = new Vector3();

            var netpeer = new ZNetPeer(null, false);
            netpeer.m_player = new Player();
            netpeer.m_playerProfile = new PlayerProfile();
            netpeer.m_requestRespawn = false;
            netpeer.m_respawnWait = 0f;
            netpeer.m_haveSpawned = false;
            netpeer.m_firstSpawn = false;
            netpeer.m_lastMoveFlags = 0;
            netpeer.m_lastMoveDir = new Vector3();
            netpeer.m_loadedSectorsTouch = 0;
            netpeer.m_loadedSectors = new Dictionary<Vector2i, KeyValuePair<int, bool>>();
            netpeer.m_solidObjectQueue = new Dictionary<ZDOID, ZDO>();

            var zdopeer = new ZDOMan.ZDOPeer();
            zdopeer.m_zdoImage = new Dictionary<ZDOID, ZDO>();

            var zdo = new ZDO();
            zdo.m_nview = new ZNetView();
            zdo.m_originator = 0L;
            zdo.m_zdoType = 0;
            zdo.m_fieldTypes = new Dictionary<int, int>();
            zdo.m_zdoEvents = new Dictionary<int, Action<ZDO>>();

            var container = new Container();
            container.m_onTakeAllSuccess2 = new Action<Humanoid>(delegate(Humanoid humanoid) {  });

            var inventory = new Inventory(null, null, 0, 0);
            inventory.m_nview = new ZNetView();
            inventory.m_inventoryIndex = 0;

            var itemdata = new ItemDrop.ItemData();
            itemdata.m_id = 0;

            var seman = new SEMan(null, null);
            seman.m_clientStatus = (object)null; ///new Dictionary<int, NetworkedStatusEffect>();
            seman.m_clientStatusSyncTime = 0f;

            var hitdata = new HitData();
            hitdata.m_attackerCharacter = new Character();

            var routedRpcData = new ZRoutedRpc.RoutedRPCData();
            routedRpcData.m_range = 0f;
            routedRpcData.m_position = Vector3.zero;

            var zrpc = new ZRpc(null);
            zrpc.m_ping = 0f;
            zrpc.m_averagePing = 0f;
            zrpc.m_pingTime = 0f;
        }

        internal void WriteDebugData()
        {
#if DEBUG
            if (!DebugOutputZDO) return;

            var prefabList = new Dictionary<int, List<KeyValuePair<string, int>>>();
            foreach (var i in ZDODebug)
            {
                var elemntCountList = i.Value.OrderByDescending(k => k.Value).ToList();
                prefabList.Add(i.Key, elemntCountList);
            }

            var ordererdList = prefabList.OrderByDescending(k => k.Value.Count > 0 ? k.Value[0].Value : 0).ToList();

            for (int i = 0; i < ordererdList.Count; i++)
            {
                var current = ordererdList[i];
                var obj = ZNetScene.instance.GetPrefab(current.Key);
                Logger.LogInfo($"{obj?.name} ({current.Key})");
                for (int j = 0; j < current.Value.Count; j++)
                {
                    var currentsub = current.Value[j];
                    Logger.LogInfo($"  {currentsub.Key}: {currentsub.Value}");
                }
            }
#endif
        }
    }
}
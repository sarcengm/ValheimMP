using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Linq;
using ValheimMP.Patches;
using BepInEx.Configuration;

namespace ValheimMP
{
    [BepInPlugin(BepInGUID, PluginName, Version)]
    public class ValheimMPPlugin : BaseUnityPlugin
    {
        public const string Author = "Sarcen";
        public const string PluginName = "ValheimMP";
        public const string Version = "0.1.0";
        public const string BepInGUID = "BepIn." + Author + "." + PluginName;
        public const string HarmonyGUID = "Harmony." + Author + "." + PluginName;
        public const string ProtocolIdentifier = "VMP";

        public static string CurrentVersion { get { return Version; } }

        public static ValheimMPPlugin Instance { get; private set; }

        private Harmony m_harmonyHandshake;
        private Harmony m_harmony;

        /// <summary>
        /// If the current session is on a Valheim MP server (or if we are a Valheim MP server)
        /// </summary>
        public bool IsOnValheimMPServer { get; private set; } = true;

        /// <summary>
        /// Whether or not this is a dedicated server!
        /// </summary>
        public static bool IsDedicated { get; private set; }

        /// <summary>
        /// DebugOutputZDO information
        /// </summary>
        public Dictionary<int, Dictionary<string, int>> ZDODebug { get; internal set; } = new Dictionary<int, Dictionary<string, int>>();

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

        /// <summary>
        /// Fired when the plugin is patched into the game
        /// </summary>
        public OnPluginActivateDel OnPluginActivate { get; set; }
        public delegate void OnPluginActivateDel();

        /// <summary>
        /// Fired when the plugin is unpatched from the game
        /// </summary>
        public OnPluginDeactivateDel OnPluginDeactivate { get; set; }
        public delegate void OnPluginDeactivateDel();

        /// <summary>
        /// Path where characters are stored
        /// </summary>
        public ConfigEntry<string> CharacterPath { get; internal set; }

        /// <summary>
        /// Does the current server use compression?
        /// </summary>
        public ConfigEntry<bool> UseZDOCompression { get; internal set; }

        /// <summary>
        /// Whether to store and output the ZDO traffic usage
        /// </summary>
        public ConfigEntry<bool> DebugOutputZDO { get; internal set; }

        /// <summary>
        /// Outputs all RPC invokes to log
        /// </summary>
        public ConfigEntry<bool> DebugRPC { get; internal set; }

        /// <summary>
        /// 
        /// </summary>
        public ConfigEntry<bool> DoNotHideCharacterWhenCameraClose { get; internal set; }

        /// <summary>
        /// Show an ingame marker where the server thinks your character is
        /// </summary>
        public ConfigEntry<bool> DebugShowZDOPlayerLocation { get; internal set; }

        /// <summary>
        /// Additional delay on both sending and receiving of packets.
        /// </summary>
        public ConfigEntry<float> ArtificialPing { get; internal set; }

        /// <summary>
        /// Minimal time it takes to respawn, it may be slightly longer if it still needs to load
        /// </summary>
        public ConfigEntry<float> RespawnDelay { get; internal set; }

        /// <summary>
        /// Player Attacks something (other then a player) in a ward where they have no access
        /// </summary>
        public ConfigEntry<float> WardPlayerDamageMultiplier { get; internal set; }

        /// <summary>
        /// Monster attacks something (other then a player) in a ward where they have no access
        /// </summary>
        public ConfigEntry<float> WardMonsterDamageMultiplier { get; internal set; }

        /// <summary>
        /// Monster attacks something (other then a player) in a ward where they have no access
        /// 
        /// And receives this multiplier worth of his own damage in return
        /// </summary>
        public ConfigEntry<float> WardMonsterReflectDamage { get; internal set; }

        /// <summary>
        /// Player attacks something (other then a player) in a ward where they have no access
        /// 
        /// And receives this multiplier worth of his own damage in return
        /// </summary>
        public ConfigEntry<float> WardPlayerReflectDamage { get; internal set; }

        /// <summary>
        /// Monster attacks player in a ward where they have no access
        /// </summary>
        public ConfigEntry<float> WardMonsterVPlayerDamageMultiplier { get; internal set; }

        /// <summary>
        /// Player attacks player in a ward where they have no access
        /// </summary>
        public ConfigEntry<float> WardPlayerVPlayerDamageMultiplier { get; internal set; }

        /// <summary>
        /// Player attacks player in a ward where they have no access
        /// 
        /// And receives this multiplier worth of his own damage in return
        /// </summary>
        public ConfigEntry<float> WardPlayerVPlayerReflectDamage { get; internal set; }

        /// <summary>
        /// Monster attacks player in a ward where they have no access
        /// 
        /// And receives this multiplier worth of his own damage in return
        /// </summary>
        public ConfigEntry<float> WardMonsterVPlayerReflectDamage { get; internal set; }

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

            CharacterPath = Config.Bind("Server", "CharacterPath", "vmp/", "Where it stores the server side characters.");
            UseZDOCompression = Config.Bind("Server", "UseZDOCompression", true, "Whether or not to compress ZDO data.");
            DebugOutputZDO = Config.Bind("Debug", "DebugOutputZDO", false, "ZDO send number debug output.");
            DebugRPC = Config.Bind("Debug", "DebugRPC", false, "Log all RPC invokes.");
            DebugShowZDOPlayerLocation = Config.Bind("Debug", "DebugShowZDOPlayerLocation", false, "Show a marker of where the server thinks the player is");
            ArtificialPing = Config.Bind("Debug", "ArtificialPing", 0f, "Add this delay in ms to your incoming and outgoing packets.");
            RespawnDelay = Config.Bind("Server", "RespawnDelay", 10f, "Minimum time it takes to respawn.");
            DoNotHideCharacterWhenCameraClose = Config.Bind("Client", "DoNotHideCharacterWhenCameraClose", false, "");

            DebugRPC.Value = false;

            WardPlayerDamageMultiplier = Config.Bind("Server",
                "WardPlayerDamageMultiplier", 0f,
                "Player (without access) attacks something (other then a player) in a ward.\n" +
                "Their damage is multiplied by this amount, 0 means no damage, 1 means normal damage.");
            WardPlayerReflectDamage = Config.Bind("Server",
                "WardPlayerReflectDamage", 1.0f,
                "Player (without access) attacks something (other then a player) in a ward.\n" +
                "And receives this multiplier worth of his own damage in return.");
            WardPlayerVPlayerDamageMultiplier = Config.Bind("Server",
                "WardPlayerVPlayerDamageMultiplier", 0f,
                "Player (without access) attacks player (with access) in a ward.\n" +
                "Their damage is multiplied by this amount, 0 means no damage, 1 means normal damage.");
            WardPlayerVPlayerReflectDamage = Config.Bind("Server",
                "WardPlayerVPlayerReflectDamage", 0f,
                "Player (without access) attacks player (with access) in a ward.\n" +
                "And receives this multiplier worth of his own damage in return.");
            WardMonsterDamageMultiplier = Config.Bind("Server",
                "WardMonsterDamageMultiplier", 0f,
                "Monster attacks something (other then a player) in a ward.\n" +
                "Their damage is multiplied by this amount, 0 means no damage, 1 means normal damage.");
            WardMonsterReflectDamage = Config.Bind("Server",
                "WardMonsterReflectDamage", 1f,
                "Monster attacks something (other then a player) in a ward.\n" +
                "And receives this multiplier worth of his own damage in return.");
            WardMonsterVPlayerDamageMultiplier = Config.Bind("Server",
                "WardMonsterVPlayerDamageMultiplier", 1f,
                "Monster attacks player (with access) in a ward.\n" +
                "Their damage is multiplied by this amount, 0 means no damage, 1 means normal damage.");
            WardMonsterVPlayerReflectDamage = Config.Bind("Server",
                "WardMonsterVPlayerReflectDamage", 0f,
                "Monster attacks player (with access) in a ward.\n" +
                "And receives this multiplier worth of his own damage in return.");
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
                    OnPluginActivate?.Invoke();
                }
                else
                {
                    Logger.LogInfo($"Unpatching {HarmonyGUID}");
                    m_harmony.UnpatchAll(HarmonyGUID);
                    OnPluginDeactivate?.Invoke();
                }
            }
        }

        /// <summary>
        /// Dummy function to easily generate all the fields I created inside various classes.
        /// 
        /// When editing valheim MP.
        /// 
        /// If using valheim MP as a library extension functions should be used to retrieve them.
        /// 
        /// E.g. ZNetPeerExtension.GetPlayerProfile();
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
            itemdata.m_customData = new Dictionary<int, byte[]>();

            var seman = new SEMan(null, null);
            seman.m_clientStatus = (object)null; 
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
            if (!DebugOutputZDO.Value) return;

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
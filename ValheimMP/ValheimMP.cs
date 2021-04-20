using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Linq;
using ValheimMP.Patches;
using BepInEx.Configuration;
using ValheimMP.Framework;
using BepInEx.Logging;
using System.IO;
using ValheimMP.Framework.Events;
using ValheimMP.Framework.Extensions;

namespace ValheimMP
{
    [BepInPlugin(BepInGUID, PluginName, Version)]
    public class ValheimMP : BaseUnityPlugin
    {
        #region Private
        internal Harmony m_harmonyHandshake;
        private Harmony m_harmony;
        #endregion

        #region Constants
        public const string Author = "Sarcen";
        public const string PluginName = "ValheimMP";
        public const string Version = "0.1.0";
        public const string BepInGUID = "BepIn." + Author + "." + PluginName;
        public const string HarmonyGUID = "Harmony." + Author + "." + PluginName;
        public const string ProtocolIdentifier = "VMP";
        public static readonly long ServerUID = 1;
        #endregion

        #region Properties
        public static string CurrentVersion { get { return Version; } }

        public static ValheimMP Instance { get; private set; }

        public static bool IsDedicated { get; private set; }

        public InventoryManager InventoryManager { get; private set; }

        public ChatCommandManager ChatCommandManager { get; private set; }

        public PlayerGroupManager PlayerGroupManager { get; private set; }

        public AdminManager AdminManager { get; private set; }

        /// <summary>
        /// If the current session is on a Valheim MP server (or if we are a Valheim MP server)
        /// </summary>
        public bool IsOnValheimMPServer { get; private set; } = true;

        public Dictionary<int, Dictionary<string, int>> ZDODebug { get; private set; } = new Dictionary<int, Dictionary<string, int>>();
        #endregion

        #region Events
        /// <summary>
        /// Fired when you connect to a Valheim MP server as Client
        /// 
        /// Only fired on the client that connects
        /// </summary>
        public event OnClientConnectHandler OnClientConnect;

        /// <summary>
        /// Fired when someone joins the server. (After their profile is loaded)
        /// 
        /// Only fired on the server.
        /// </summary>
        public event OnServerConnectHandler OnServerConnect;

        /// <summary>
        /// Fired when someone joins the server, but it is fired before their profile is loaded.
        /// </summary>
        public event OnServerConnectBeforeProfileLoadHandler OnServerConnectBeforeProfileLoad;

        /// <summary>
        /// Fired when the server sends their peer info
        /// </summary>
        public event OnServerSendPeerInfoHandler OnServerSendPeerInfo;

        /// <summary>
        /// Fired when the client sends their peer info. 
        /// </summary>
        public event OnClientSendPeerInfoHandler OnClientSendPeerInfo;

        /// <summary>
        /// Fired when a chat message is received on the server.
        /// 
        /// Also fired on clients when they send a chat message.
        /// </summary>
        public event OnChatMessageHandler OnChatMessage;

        /// <summary>
        /// Fired on clients after they successfully sold an item
        /// </summary>
        public event OnTraderClientSoldItemHandler OnTraderClientSoldItem;

        /// <summary>
        /// Fired on clients after they successfully bought an item
        /// </summary>
        public event OnTraderClientBoughtItemHandler OnTraderClientBoughtItem;


        /// <summary>
        /// Fired when the plugin is patched into the game
        /// </summary>
        public event OnPluginActivateHandler OnPluginActivate;

        /// <summary>
        /// Fired when the plugin is unpatched from the game
        /// </summary>
        public event OnPluginDeactivateHandler OnPluginDeactivate;

        /// <summary>
        /// Fired when the world is being saved
        /// </summary>
        public event OnWorldSaveDelegate OnWorldSave;

        /// <summary>
        /// Fired when a player is online (and their player character has spawned for the first time)
        /// </summary>
        public event OnPlayerOnlineHandler OnPlayerOnline;

        /// <summary>
        /// Fired when a player has disconnected from the server
        /// </summary>
        public event OnPlayerOfflineHandler OnPlayerOffline;

        public delegate void OnWorldSaveDelegate();
        #endregion

        #region Configuration
        /// <summary>
        /// Does the current server use compression?
        /// </summary>
        public ConfigEntry<bool> UseZDOCompression { get; private set; }

        /// <summary>
        /// Whether to store and output the ZDO traffic usage
        /// </summary>
        public ConfigEntry<bool> DebugOutputZDO { get; private set; }

        /// <summary>
        /// Outputs all RPC invokes to log
        /// </summary>
        public ConfigEntry<bool> DebugRPC { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public ConfigEntry<bool> DoNotHideCharacterWhenCameraClose { get; private set; }

        /// <summary>
        /// Show an ingame marker where the server thinks your character is
        /// </summary>
        public ConfigEntry<bool> DebugShowZDOPlayerLocation { get; private set; }

        /// <summary>
        /// Additional delay on both sending and receiving of packets.
        /// </summary>
        public ConfigEntry<float> ArtificialPing { get; private set; }

        /// <summary>
        /// Minimal time it takes to respawn, it may be slightly longer if it still needs to load
        /// </summary>
        public ConfigEntry<float> RespawnDelay { get; private set; }

        /// <summary>
        /// Player Attacks something (other then a player) in a ward where they have no access
        /// </summary>
        public ConfigEntry<float> WardPlayerDamageMultiplier { get; private set; }

        /// <summary>
        /// Monster attacks something (other then a player) in a ward where they have no access
        /// </summary>
        public ConfigEntry<float> WardMonsterDamageMultiplier { get; private set; }

        /// <summary>
        /// Monster attacks something (other then a player) in a ward where they have no access
        /// 
        /// And receives this multiplier worth of his own damage in return
        /// </summary>
        public ConfigEntry<float> WardMonsterReflectDamage { get; private set; }

        /// <summary>
        /// Player attacks something (other then a player) in a ward where they have no access
        /// 
        /// And receives this multiplier worth of his own damage in return
        /// </summary>
        public ConfigEntry<float> WardPlayerReflectDamage { get; private set; }

        /// <summary>
        /// Monster attacks player in a ward where they have no access
        /// </summary>
        public ConfigEntry<float> WardMonsterVPlayerDamageMultiplier { get; private set; }

        /// <summary>
        /// Player attacks player in a ward where they have no access
        /// </summary>
        public ConfigEntry<float> WardPlayerVPlayerDamageMultiplier { get; private set; }

        /// <summary>
        /// Player attacks player in a ward where they have no access
        /// 
        /// And receives this multiplier worth of his own damage in return
        /// </summary>
        public ConfigEntry<float> WardPlayerVPlayerReflectDamage { get; private set; }

        /// <summary>
        /// Monster attacks player in a ward where they have no access
        /// 
        /// And receives this multiplier worth of his own damage in return
        /// </summary>
        public ConfigEntry<float> WardMonsterVPlayerReflectDamage { get; private set; }
        public ConfigEntry<float> ClientAttackCompensationWindow { get; private set; }
        public ConfigEntry<float> ClientAttackCompensationDistance { get; private set; }
        public ConfigEntry<float> ClientAttackCompensationDistanceMin { get; private set; }
        public ConfigEntry<float> ClientAttackCompensationDistanceMax { get; private set; }
        public ConfigEntry<float> ForcedPVPDistanceFromCenter { get; private set; }
        public ConfigEntry<float> ForcedPVPDistanceForBiomesOnly { get; private set; }
        public Dictionary<Heightmap.Biome, ConfigEntry<bool>> ForcedPVPBiomes { get; private set; }
        public ConfigEntry<int> ServerObjectsCreatedPerFrame { get; private set; }
        public ConfigEntry<int> ChatMaxHistory { get; private set; }
        public ConfigEntry<Color> ChatPartyColor { get; private set; }
        public ConfigEntry<Color> ChatClanColor { get; private set; }
        public ConfigEntry<Color> ChatWhisperColor { get; private set; }
        public ConfigEntry<Color> ChatShoutColor { get; private set; }
        public ConfigEntry<Color> ChatGlobalColor { get; private set; }
        public ConfigEntry<Color> ChatDefaultColor { get; private set; }
        public ConfigEntry<float> ChatShoutDistance { get; private set; }
        public ConfigEntry<float> ChatWhisperDistance { get; private set; }
        public ConfigEntry<float> ChatNormalDistance { get; private set; }
        public ConfigEntry<float> PerfectBlockWindow { get; internal set; }
        public ConfigEntry<float> PlayerDamageDelay { get; internal set; }
        public ConfigEntry<Vector3> PartyFramesPosition { get; internal set; }
        public ConfigEntry<Vector3> PartyFramesOffset { get; internal set; }
        public ConfigEntry<Vector3> PartyFramesScale { get; internal set; }
        public ConfigEntry<bool> PartyFramesEnabled { get; internal set; }

        private ConfigFile m_localizationFile;
        private Dictionary<string, ConfigEntry<string>> m_localizedStrings = new Dictionary<string, ConfigEntry<string>>();
        #endregion

        #region delegates
        public delegate void OnClientConnectHandler(OnClientConnectArgs args);
        public delegate void OnServerConnectHandler(OnServerConnectArgs args);
        public delegate void OnServerConnectBeforeProfileLoadHandler(OnServerConnectArgs args);
        public delegate void OnServerSendPeerInfoHandler(ZRpc rpc, Dictionary<int, byte[]> customData);
        public delegate void OnClientSendPeerInfoHandler(ZRpc rpc, Dictionary<int, byte[]> customData);
        public delegate void OnChatMessageHandler(OnChatMessageArgs args);
        public delegate void OnTraderClientSoldItemHandler(OnTraderClientSoldItemArgs args);
        public delegate void OnTraderClientBoughtItemHandler(OnTraderClientBoughtItemArgs args);
        public delegate void OnPluginActivateHandler();
        public delegate void OnPluginDeactivateHandler();
        public delegate void OnPlayerOnlineHandler(ZNetPeer peer);
        public delegate void OnPlayerOfflineHandler(ZNetPeer peer);
        #endregion

        // Awake is called once when both the game and the plug-in are loaded
        private void Awake()
        {
            m_localizationFile = new ConfigFile(Path.Combine(Path.GetDirectoryName(Config.ConfigFilePath), BepInGUID + ".Localization.cfg"), false);

            Instance = this;

            m_harmony = new Harmony(HarmonyGUID);
            if (isDedicated())
            {
                m_harmony.PatchAll();
            }

            m_harmonyHandshake = new Harmony(HarmonyGUID + ".hs");
            m_harmonyHandshake.Patch(AccessTools.Method(typeof(ZNet), "SendPeerInfo"),
                transpiler: new HarmonyMethod(AccessTools.Method(typeof(ZNet_Patch), "SendPeerInfo")));

            m_harmonyHandshake.Patch(AccessTools.Method(typeof(ZNet), "RPC_PeerInfo"),
                transpiler: new HarmonyMethod(AccessTools.Method(typeof(ZNet_Patch), "RPC_PeerInfo")));

            m_harmonyHandshake.Patch(AccessTools.Method(typeof(FejdStartup), "IsPublicPasswordValid"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(FejdStartup_Patch), "IsPublicPasswordValid")));

            m_harmonyHandshake.Patch(AccessTools.Method(typeof(FejdStartup), "Awake"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(FejdStartup_Patch), "Awake")),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(FejdStartup_Patch), "AwakePost")));


            m_harmonyHandshake.Patch(AccessTools.Method(typeof(FejdStartup), "ShowCharacterSelection"),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(FejdStartup_Patch), "SetupCharacterPreview")));

            m_harmonyHandshake.Patch(AccessTools.Method(typeof(FejdStartup), "SetupCharacterPreview"),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(FejdStartup_Patch), "SetupCharacterPreview")));

            m_harmonyHandshake.Patch(AccessTools.Method(typeof(ZSteamSocket), "RegisterGlobalCallbacks"),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ZSteamSocket_Patch), "RegisterGlobalCallbacks")));



            if (IsDedicated)
            {
                AdminManager = AdminManager.Load(Path.Combine(Paths.PluginPath, PluginName, "admins.json"));
                OnServerConnect += AdminManager.OnServerConnect;
                PlayerGroupManager = PlayerGroupManager.Load(Path.Combine(Paths.PluginPath, PluginName, "groups.json"));

                OnWorldSave += () =>
                {
                    PlayerGroupManager.Save(Path.Combine(Paths.PluginPath, PluginName, "groups.json"));
                    AdminManager.Save(Path.Combine(Paths.PluginPath, PluginName, "admins.json"));
                };

                PlayerGroupManager.Save(Path.Combine(Paths.PluginPath, PluginName, "groups.json"));
                OnPlayerOnline += PlayerGroupManager.Internal_OnPlayerOnline;
                OnPlayerOffline += PlayerGroupManager.Internal_OnPlayerOffline;
                AdminManager.Save(Path.Combine(Paths.PluginPath, PluginName, "admins.json"));
            }
            else
            {
                PlayerGroupManager = new PlayerGroupManager();
            }

            ChatCommandManager = new ChatCommandManager(this);
            InventoryManager = new InventoryManager(this);

            UseZDOCompression = Config.Bind("Server", "UseZDOCompression", true, "Whether or not to compress ZDO data.");
            DebugOutputZDO = Config.Bind("Debug", "DebugOutputZDO", false, "ZDO send number debug output.");
            DebugRPC = Config.Bind("Debug", "DebugRPC", false, "Log all RPC invokes.");
            DebugShowZDOPlayerLocation = Config.Bind("Debug", "DebugShowZDOPlayerLocation", false, "Show a marker of where the server thinks the player is");
            ArtificialPing = Config.Bind("Debug", "ArtificialPing", 0f, "Add this delay in ms to your incoming and outgoing packets.");
            RespawnDelay = Config.Bind("Server", "RespawnDelay", 10f, "Minimum time it takes to respawn.");
            DoNotHideCharacterWhenCameraClose = Config.Bind("Client", "DoNotHideCharacterWhenCameraClose", false, "");

            ServerObjectsCreatedPerFrame = Config.Bind("Server", "ServerObjectsCreatedPerFrame", 100, "Number of objects per frame the server instantiates while loading sectors, too many will cause stutters, too few will make it so you see the world loading in slowly."); ;

            ClientAttackCompensationWindow = Config.Bind("Server", "ClientAttackCompensationWindow", 0.15f,
                "Max amount of time window for hits to be compensated (towards client hit detection).");
            ClientAttackCompensationDistance = Config.Bind("Server", "ClientAttackCompensationDistance", 10.0f,
                "Max amount of distance for hits to be compensated (towards client hit detection). \n" +
                " This amount is multiplied by the average ping in seconds. \n" +
                " E.g. 150 ms ping will be multiplied by 0.15");
            ClientAttackCompensationDistanceMin = Config.Bind("Server", "ClientAttackCompensationDistanceMin", 2.0f, "Minimum value for compensation distance.");
            ClientAttackCompensationDistanceMax = Config.Bind("Server", "ClientAttackCompensationDistanceMax", 5.0f, "Maximum value for compensation distance.");

            ForcedPVPDistanceFromCenter = Config.Bind("ForcedPVP", "ForcedPVPDistanceFromCenter", 10000f, "Force PVP on at this distance from the center of the world.");
            ForcedPVPDistanceForBiomesOnly = Config.Bind("ForcedPVP", "ForcedPVPDistanceForBiomesOnly", 10000f, "Force PVP on at this distance from the center of the world. But only for the selected biomes.");

            ForcedPVPBiomes = new Dictionary<Heightmap.Biome, ConfigEntry<bool>>();

            ChatMaxHistory = Config.Bind("Client", "ChatMaxHistory", 100, "Amount of lines you can scroll back in the history of chat (with page up\\down)");
            ChatPartyColor = Config.Bind<Color>("Client", "ChatPartyColor", new Color32(170, 170, 255, 255), "Sets the chat color for party messages.");
            ChatClanColor = Config.Bind<Color>("Client", "ChatClanColor", new Color32(64, 255, 64, 255), "Sets the chat color for clan messages.");
            ChatWhisperColor = Config.Bind("Client", "ChatWhisperColor", new Color(1f, 1f, 1f, 0.75f), "Sets the chat color for whisper messages.");
            ChatShoutColor = Config.Bind("Client", "ChatShoutColor", Color.yellow, "Sets the chat color for shout messages.");
            ChatGlobalColor = Config.Bind("Client", "ChatGlobalColor", Color.white, "Sets the chat color for global messages.");
            ChatDefaultColor = Config.Bind("Client", "ChatDefaultColor", Color.white, "Sets the default chat color for messages.");

            ChatShoutDistance = Config.Bind("Server", "ChatShoutDistance", 128f, "Sets the distance for shouting. For distance a 2x2 floor tile is 2f by 2f.");
            ChatWhisperDistance = Config.Bind("Server", "ChatWhisperDistance", 10f, "Sets the distance for whispers. For distance a 2x2 floor tile is 2f by 2f.");
            ChatNormalDistance = Config.Bind("Server", "ChatNormalDistance", 64f, "Sets the distance for normal chat. For distance a 2x2 floor tile is 2f by 2f.");

            PerfectBlockWindow = Config.Bind("Server", "PerfectBlockWindow", 0.35f, "Window for perfect blocks, 0.25f in Vanilla, 0.35f by default in VMP (slight compensation)");
            PlayerDamageDelay = Config.Bind("Server", "PlayerDamageDelay", 0.1f, "Delay on damage against players (on top of the already established latency delay) to allow for easier blocking.");

            PartyFramesOffset = Config.Bind("Client", "PartyFramesOffset", new Vector3(100, 200), "Offset of each subsequent party member");
            PartyFramesPosition = Config.Bind("Client", "PartyFramesPosition", new Vector3(0, 50), "Position of the party frames");
            PartyFramesScale = Config.Bind("Client", "PartyFramesScales", new Vector3(1, 1, 1), "Scale of the party frames");
            PartyFramesEnabled = Config.Bind("Client", "PartyFramesEnabled", true, "Party frames enabled");

            foreach (Heightmap.Biome val in typeof(Heightmap.Biome).GetEnumValues())
            {
                if (val == Heightmap.Biome.None || val == Heightmap.Biome.BiomesMax)
                    continue;
                ForcedPVPBiomes.Add(val, Config.Bind("ForcedPVP", val.ToString(), false, "Force PVP on in this biome."));
            }

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

            LocalizeDefaults();
        }

        public void OnDestroy()
        {
            m_localizationFile.Save();
            Config.Save();
        }

        private void LocalizeDefaults()
        {
            foreach (ChatMessageType val in typeof(ChatMessageType).GetEnumValues())
            {
                LocalizeWord($"vmp_{val}", val.ToString());
            }

            LocalizeWord("vmp_revive", "Revive");
            LocalizeWord("vmp_reviving_in", "Reviving in {secondsWaitTime}");
            LocalizeWord("vmp_reviving", "Reviving {playerName}");
            LocalizeWord("vmp_revival_interupted", "Reviving Interrupted");
            LocalizeWord("vmp_revival_request", "<color=green>{playerName}</color> wishes to revive you type <color=green>/revive</color> to accept.");
            LocalizeWord("vmp_forcedpvp_enter", "Entering forced pvp area");
            LocalizeWord("vmp_forcedpvp_exit", "Leaving forced pvp area");

            LocalizeWord("vmp_allowMode", "Allow: ");
            LocalizeWord("vmp_allowMode_Private", "Private");
            LocalizeWord("vmp_allowMode_Clan", "Clan");
            LocalizeWord("vmp_allowMode_Party", "Party");
            LocalizeWord("vmp_allowMode_Both", "Clan and Party");
        }

        public string LocalizeWord(string key, string val)
        {
            if (!m_localizedStrings.ContainsKey(key))
            {
                var loc = Localization.instance;
                var langSection = loc.GetSelectedLanguage();
                var configEntry = m_localizationFile.Bind(langSection, key, val);
                Localization.instance.AddWord(key, configEntry.Value);
                m_localizedStrings.Add(key, configEntry);
            }

            return $"${key}";
        }

        internal static void Log(object o, LogLevel level = LogLevel.Info)
        {
            Instance.Logger.Log(level, o);
        }

        internal static void LogWarning(object o)
        {
            Instance.Logger.LogWarning(o);
        }

        internal static void LogError(object o)
        {
            Instance.Logger.LogError(o);
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

                    var chat = Chat.instance;
                    chat.Awake();
                }
                else
                {
                    Logger.LogInfo($"Unpatching {HarmonyGUID}");
                    m_harmony.UnpatchAll(HarmonyGUID);
                    OnPluginDeactivate?.Invoke();
                }
            }
        }

        private void Update()
        {
            if (IsDedicated)
            {
                InventoryManager.Update();
                PlayerGroupManager.Update();
            }
        }

        internal void Internal_OnPlayerOffline(ZNetPeer peer)
        {
            OnPlayerOffline?.Invoke(peer);
        }

        internal void Internal_OnPlayerOnline(ZNetPeer peer)
        {
            OnPlayerOnline?.Invoke(peer);
        }

        internal void Internal_OnServerSendPeerInfo(ZRpc rpc, Dictionary<int, byte[]> dic)
        {
            OnServerSendPeerInfo?.Invoke(rpc, dic);
        }

        internal void Internal_OnClientSendPeerInfo(ZRpc rpc, Dictionary<int, byte[]> dic)
        {
            OnClientSendPeerInfo?.Invoke(rpc, dic);
        }

        internal void Internal_OnWorldSave()
        {
            OnWorldSave?.Invoke();
        }

        internal void Internal_OnChatMessage(OnChatMessageArgs args)
        {
            OnChatMessage?.Invoke(args);
        }

        internal void Internal_OnClientConnect(OnClientConnectArgs args)
        {
            OnClientConnect?.Invoke(args);
        }


        internal void Internal_OnServerConnect(OnServerConnectArgs args)
        {
            OnServerConnect?.Invoke(args);
        }

        internal void Internal_OnServerConnectBeforeProfileLoad(OnServerConnectArgs args)
        {
            OnServerConnectBeforeProfileLoad?.Invoke(args);
        }

        internal void Internal_OnTraderClientBoughtItem(OnTraderClientBoughtItemArgs args)
        {
            OnTraderClientBoughtItem?.Invoke(args);
        }

        internal void Internal_OnTraderClientSoldItem(OnTraderClientSoldItemArgs args)
        {
            OnTraderClientSoldItem?.Invoke(args);
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
            zdo.m_listIndex = 0;

            var container = new Container();
            container.m_onTakeAllSuccess2 = new Action<Humanoid>(delegate (Humanoid humanoid) { });

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
            zrpc.m_peer = new ZNetPeer(null, false);

            var attack = new Attack();
            attack.m_lastMeleeHitTime = 0f;
            attack.m_lastMeleeHits = new List<HitData>();
            attack.m_lastClientMeleeHitTime = 0f;
            attack.m_lastClientMeleeHits = new HashSet<ZDOID>();
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
                Log($"{obj?.name} ({current.Key})");
                for (int j = 0; j < current.Value.Count; j++)
                {
                    var currentsub = current.Value[j];
                    Log($"  {currentsub.Key}: {currentsub.Value}");
                }
            }
#endif
        }
    }
}
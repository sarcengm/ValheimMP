using BepInEx;
using Common;
using EpicLoot;
using EpicLoot.Adventure;
using EpicLoot.Crafting;
using EpicLoot.GatedItemType;
using System.Collections.Generic;
using ValheimMP.ChatCommands;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.EpicLootPatch
{
    [BepInPlugin(BepInGUID, Name, Version)]
    [BepInDependency(ValheimMP.BepInGUID)]
    [BepInDependency(ChatCommands.ChatCommands.BepInGUID)]
    [BepInDependency(EpicLoot.EpicLoot.PluginId)]
    public class EpicLootPatch : BaseUnityPlugin
    {
        public const string Author = "Sarcen";
        public const string Name = "ValheimMP.EpicLootPatch";
        public const string Version = "1.0.0";
        public const string BepInGUID = "BepIn." + Author + "." + Name;
        private static byte[] m_epicLootConfig;

        public static EpicLootPatch Instance { get; private set; }

        public static string EpicLootVersion { get; private set; }

        public void Awake()
        {
            Instance = this;

            EpicLootVersion = typeof(EpicLoot.EpicLoot).GetConstantValue<string>("Version");

            ChatCommands.ChatCommands.Instance.RegisterAll(this);

            ValheimMP.Instance.OnClientSendPeerInfo += OnClientSendPeerInfo;
            ValheimMP.Instance.OnClientConnect += OnClientConnect;
            ValheimMP.Instance.OnServerSendPeerInfo += OnServerSendPeerInfo;
            ValheimMP.Instance.OnServerConnectBeforeProfileLoad += OnServerConnect;
        }

        public static void OnClientSendPeerInfo(ZRpc rpc, Dictionary<int, byte[]> customData)
        {
            customData.SetCustomData("EpicLootVersion", typeof(EpicLoot.EpicLoot).GetConstantValue<string>("Version"));
        }

        public static void OnServerSendPeerInfo(ZRpc rpc, Dictionary<int, byte[]> customData)
        {
            if (m_epicLootConfig == null)
            {
                var pkg = new ZPackage();
                pkg.Write(EpicLoot.EpicLoot.LoadJsonText("loottables.json"));
                pkg.Write(EpicLoot.EpicLoot.LoadJsonText("magiceffects.json"));
                pkg.Write(EpicLoot.EpicLoot.LoadJsonText("iteminfo.json"));
                pkg.Write(EpicLoot.EpicLoot.LoadJsonText("recipes.json"));
                pkg.Write(EpicLoot.EpicLoot.LoadJsonText("enchantcosts.json"));
                pkg.Write(EpicLoot.EpicLoot.LoadJsonText("adventuredata.json"));
                pkg = pkg.Compress();
                m_epicLootConfig = pkg.GetArray();
            }

            customData.SetCustomData("EpicLootConfig", m_epicLootConfig);
        }

        public static bool OnClientConnect(ZRpc rpc, ZNetPeer peer, Dictionary<int, byte[]> customData)
        {
            var config = customData.GetCustomData("EpicLootConfig");
            if (config != null)
            {
                var pkg = new ZPackage(config);
                pkg = pkg.Decompress();
                LootRoller.Initialize(EpicLoot.EpicLoot.JsonToObject<LootConfig>(pkg.ReadString()));
                MagicItemEffectDefinitions.Initialize(EpicLoot.EpicLoot.JsonToObject<MagicItemEffectsList>(pkg.ReadString()));
                GatedItemTypeHelper.Initialize(EpicLoot.EpicLoot.JsonToObject<ItemInfoConfig>(pkg.ReadString()));
                RecipesHelper.Initialize(EpicLoot.EpicLoot.JsonToObject<RecipesConfig>(pkg.ReadString()));
                EnchantCostsHelper.Initialize(EpicLoot.EpicLoot.JsonToObject<EnchantingCostsConfig>(pkg.ReadString()));
                MagicItemNames.Initialize(EpicLoot.EpicLoot.JsonToObject<ItemNameConfig>(pkg.ReadString()));
                AdventureDataManager.Initialize(EpicLoot.EpicLoot.JsonToObject<AdventureDataConfig>(pkg.ReadString()));
            }
            return true;
        }

        public static bool OnServerConnect(ZRpc rpc, ZNetPeer peer, Dictionary<int, byte[]> customData)
        {
            var clientVersion = customData.GetCustomData<string>("EpicLootVersion");

            if (EpicLootVersion != clientVersion)
            {
                rpc.SendErrorMessage($"Version mismatch for EpicLoot you have {clientVersion} while the server has {EpicLootVersion}");
                return false;
            }

            rpc.Register("EpicLoot_DoCraft", (ZRpc rpc, int itemHash, int recipe) =>
            {

            });

            return true;
        }

        public static void Log(string message)
        {
            Instance.Logger.LogInfo(message);
        }

        [ChatCommand("EpicLoot", "Shows the current EpicLoot version and EpicLootPatch version.")]
        private void Command_EpicLoot(ZNetPeer peer)
        {
            var message = $"Server running <color=green>EpicLoot</color> ({EpicLoot.EpicLoot.PluginId}) version <color=green>{EpicLootVersion}</color> <color=green>EpicLootPatch</color> version <color=green>{Version}</color>.";
            if (EpicLootVersion != EpicLoot.EpicLoot.Version)
            {
                message += $"\n <color=yellow>(Warning: <color=green>EpicLootPatch</color> was compiled against version {EpicLoot.EpicLoot.Version}, and may be incompatible with {EpicLootVersion})</color>";
            }
            peer.SendServerMessage(message);
        }
    }
}

using BepInEx;
using Common;
using EpicLoot;
using EpicLoot.Adventure;
using EpicLoot.Crafting;
using EpicLoot.GatedItemType;
using HarmonyLib;
using System.Collections.Generic;
using ValheimMP.Framework;
using ValheimMP.Framework.Events;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.EpicLootPatch
{
    [BepInPlugin(BepInGUID, Name, Version)]
    [BepInDependency(ValheimMP.BepInGUID)]
    [BepInDependency(EpicLoot.EpicLoot.PluginId)]
    public class EpicLootPatch : BaseUnityPlugin
    {
        public const string Author = "Sarcen";
        public const string Name = "ValheimMP.EpicLootPatch";
        public const string Version = "1.0.0";
        public const string BepInGUID = "BepIn." + Author + "." + Name;
        public const string HarmonyInGUID = "Harmony." + Author + "." + Name;
        private static byte[] m_epicLootConfig;

        public static EpicLootPatch Instance { get; private set; }

        public static string EpicLootVersion { get; private set; }

        private Harmony m_harmony;

        public void Awake()
        {
            Instance = this;

            m_harmony = new Harmony(HarmonyInGUID);

            if (ValheimMP.IsDedicated)
            {
                m_harmony.PatchAll();
            }

            EpicLootVersion = typeof(EpicLoot.EpicLoot).GetConstantValue<string>("Version");

            if (EpicLootVersion != EpicLoot.EpicLoot.Version)
            {
                Logger.LogWarning($"EpicLoot version {EpicLootVersion} while {Name} was built on {EpicLoot.EpicLoot.Version}");
            }

            if (ValheimMP.Instance.ChatCommandManager != null)
                ValheimMP.Instance.ChatCommandManager.RegisterAll(this);

            ValheimMP.Instance.OnClientSendPeerInfo += OnClientSendPeerInfo;
            ValheimMP.Instance.OnClientConnect += OnClientConnect;
            ValheimMP.Instance.OnServerSendPeerInfo += OnServerSendPeerInfo;
            ValheimMP.Instance.OnServerConnectBeforeProfileLoad += OnServerConnect;
            ValheimMP.Instance.InventoryManager.OnItemCrafted += OnItemCrafted;

            if (!ValheimMP.IsDedicated)
            {
                ValheimMP.Instance.OnPluginActivate += () => {
                    Log($"Patching {HarmonyInGUID}");
                    m_harmony.PatchAll();
                    TabControllerPatch.AddTabControllers();
                };
                ValheimMP.Instance.OnPluginDeactivate += () => {
                    Log($"Unpatching {HarmonyInGUID}");
                    m_harmony.UnpatchAll(HarmonyInGUID);
                    TabControllerPatch.RemoveTabControllers();
                };
            }
        }

        public static void OnItemCrafted(int craftTrigger, Inventory inventory, ItemDrop.ItemData itemData, byte[] triggerData)
        {
            CraftingTabs.TabControllers.Do(k =>
            {
                if (craftTrigger == VMPDisenchantTabController.DisenchantTrigger &&
                    k is VMPDisenchantTabController disenchant) disenchant.OnDisenchant(inventory, itemData, triggerData);

                else if (craftTrigger == VMPEnchantTabController.EnchantTrigger &&
                    k is VMPEnchantTabController enchant) enchant.OnEnchant(inventory, itemData, triggerData);

                else if (craftTrigger == VMPAugmentTabController.AugmentTrigger &&
                    k is VMPAugmentTabController augment) augment.OnAugment(inventory, itemData, triggerData);

                else if (craftTrigger == VMPAugmentTabController.AugmentCompleteTrigger &&
                    k is VMPAugmentTabController augment2) augment2.OnAugmentComplete(inventory, itemData, triggerData);
            });
        }

        public static void OnClientSendPeerInfo(ZRpc rpc, Dictionary<int, byte[]> customData)
        {
            customData.SetCustomData("EpicLootVersion", EpicLootVersion);
        }

        public static void OnServerSendPeerInfo(ZRpc rpc, Dictionary<int, byte[]> customData)
        {
            if (m_epicLootConfig == null)
            {
                var pkg = new ZPackage();
                pkg.Write(EpicLoot.EpicLoot.LoadJsonText("loottables.json") ?? "");
                pkg.Write(EpicLoot.EpicLoot.LoadJsonText("magiceffects.json") ?? "");
                pkg.Write(EpicLoot.EpicLoot.LoadJsonText("iteminfo.json") ?? "");
                pkg.Write(EpicLoot.EpicLoot.LoadJsonText("recipes.json") ?? "");
                pkg.Write(EpicLoot.EpicLoot.LoadJsonText("enchantcosts.json") ?? "");
                pkg.Write(EpicLoot.EpicLoot.LoadJsonText("itemnames.json") ?? "");
                pkg.Write(EpicLoot.EpicLoot.LoadJsonText("adventuredata.json") ?? "");
                pkg = pkg.Compress();
                m_epicLootConfig = pkg.GetArray();
            }

            customData.SetCustomData("EpicLootConfig", m_epicLootConfig);
        }

        public static void OnClientConnect(OnClientConnectArgs args)
        {
            var config = args.CustomData.GetCustomData("EpicLootConfig");
            if (config != null)
            {
                var pkg = new ZPackage(config);
                pkg = pkg.Decompress();
                var lootRoller = pkg.ReadString();
                var magicItemEffectDefinitions = pkg.ReadString();
                var gatedItemTypeHelper = pkg.ReadString();
                var recipesHelper = pkg.ReadString();
                var enchantCostsHelper = pkg.ReadString();
                var magicItemNames = pkg.ReadString();
                var adventureDataManager = pkg.ReadString();

                if (!string.IsNullOrWhiteSpace(lootRoller)) LootRoller.Initialize(EpicLoot.EpicLoot.JsonToObject<LootConfig>(lootRoller));
                if (!string.IsNullOrWhiteSpace(magicItemEffectDefinitions)) MagicItemEffectDefinitions.Initialize(EpicLoot.EpicLoot.JsonToObject<MagicItemEffectsList>(magicItemEffectDefinitions));
                if (!string.IsNullOrWhiteSpace(gatedItemTypeHelper)) GatedItemTypeHelper.Initialize(EpicLoot.EpicLoot.JsonToObject<ItemInfoConfig>(gatedItemTypeHelper));
                if (!string.IsNullOrWhiteSpace(recipesHelper)) RecipesHelper.Initialize(EpicLoot.EpicLoot.JsonToObject<RecipesConfig>(recipesHelper));
                if (!string.IsNullOrWhiteSpace(enchantCostsHelper)) EnchantCostsHelper.Initialize(EpicLoot.EpicLoot.JsonToObject<EnchantingCostsConfig>(enchantCostsHelper));
                if (!string.IsNullOrWhiteSpace(magicItemNames)) MagicItemNames.Initialize(EpicLoot.EpicLoot.JsonToObject<ItemNameConfig>(magicItemNames));
                if (!string.IsNullOrWhiteSpace(adventureDataManager)) AdventureDataManager.Initialize(EpicLoot.EpicLoot.JsonToObject<AdventureDataConfig>(adventureDataManager));
            }
        }

        public static void OnServerConnect(OnServerConnectArgs args)
        {
            var clientVersion = args.CustomData.GetCustomData<string>("EpicLootVersion");

            Log($"Client is running {clientVersion}, server is {EpicLootVersion}");

            if (EpicLootVersion != clientVersion)
            {
                args.Rpc.SendErrorMessage($"Version mismatch for EpicLoot you have {clientVersion} while the server has {EpicLootVersion}");
                args.AbortConnect = true;
                return;
            }

            args.Rpc.Register("EpicLoot_Disenchant", (ZRpc rpc, int itemId) =>
            {
                VMPDisenchantTabController.RPC_Disenchant(rpc, itemId);
            });

            args.Rpc.Register("EpicLoot_Enchant", (ZRpc rpc, int itemId, int rarity) =>
            {
                VMPEnchantTabController.RPC_Enchant(rpc, itemId, rarity);
            });

            args.Rpc.Register("EpicLoot_Augment", (ZRpc rpc, int itemId, int effectIndex) =>
            {
                VMPAugmentTabController.RPC_Augment(rpc, itemId, effectIndex);
            });

            args.Rpc.Register("EpicLoot_AugmentComplete", (ZRpc rpc, int itemId, int effectIndex, string effectJson) =>
            {
                VMPAugmentTabController.RPC_AugmentComplete(rpc, itemId, effectIndex, effectJson);
            });
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

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ValheimMP.Util;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Trader_Patch
    {
        internal static readonly int rpcBuyItem = "BuyItem".GetStableHashCode();
        internal static readonly int rpcSellItem = "SellItem".GetStableHashCode();
        internal static readonly int rpcRequestTradeList = "RequestTradeList".GetStableHashCode();

        internal static readonly int rpcBoughtItem = "BoughtItem".GetStableHashCode();
        internal static readonly int rpcSoldItem = "SoldItem".GetStableHashCode();
        internal static readonly int rpcTradeList = "TradeList".GetStableHashCode();

        [HarmonyPatch(typeof(Trader), "Start")]
        [HarmonyPrefix]
        private static void Start(Trader __instance)
        {
            var netview = __instance.GetNetView();
            if (netview != null)
            {
                if (ValheimMP.IsDedicated)
                {
                    netview.Register(rpcBuyItem, (long sender, int itemId, int count) =>
                    {
                        RPC_BuyItem(__instance, sender, itemId, count);
                    });
                    netview.Register(rpcSellItem, (long sender, int itemId, int count) =>
                    {
                        RPC_SellItem(__instance, sender, itemId, count);
                    });
                    netview.Register(rpcRequestTradeList, (long sender) =>
                    {
                        RPC_RequestTradeList(__instance, sender);
                    });
                }
                else
                {
                    netview.Register(rpcBoughtItem, (long sender, int itemHash, int count) =>
                    {
                        RPC_BoughtItem(__instance, sender, itemHash, count);
                    });
                    netview.Register(rpcSoldItem, (long sender, int itemHash, int count) =>
                    {
                        RPC_SoldItem(__instance, sender, itemHash, count);
                    });
                    netview.Register(rpcTradeList, (long sender, ZPackage pkg) =>
                    {
                        RPC_TradeList(__instance, sender, pkg);
                    });
                    __instance.m_items.Clear();
                }
            }
        }

        private static void RPC_TradeList(Trader trader, long sender, ZPackage pkg)
        {
            trader.m_items.Clear();

            var dummyInventory = new Inventory(null, null, 9999, 1);
            pkg = pkg.Decompress();

            var itemCount = pkg.ReadInt();

            for(int i=0; i<itemCount; i++)
            {
                var remoteItemData = new Inventory_Patch.NetworkedItemData();
                var itemData = remoteItemData.Deserialize(dummyInventory, pkg);

                // m_dropPrefab is the template, I don't think it's a good idea to be modifying it, even if its just the item data, so lets make a copy.
                ZNetView.m_forceDisableInit = true;
                GameObject gameObject = UnityEngine.Object.Instantiate(itemData.m_dropPrefab);
                ZNetView.m_forceDisableInit = false;

                var tradeItem = new Trader.TradeItem()
                {
                    m_prefab = gameObject.GetComponent<ItemDrop>(),
                    m_price = itemData.GetCustomDataInt("m_price"),
                    m_stack = itemData.GetCustomDataInt("m_stack"),
                };
                tradeItem.m_prefab.m_itemData = itemData;

                trader.m_items.Add(tradeItem);

            }

            StoreGui.instance.FillList();
        }

        private static void RPC_RequestTradeList(Trader trader, long sender)
        {
            var pkg = new ZPackage();
            pkg.Write(trader.m_items.Count);

            foreach(var item in trader.m_items)
            {
                var localItemData = item.m_prefab.m_itemData;
                localItemData.m_dropPrefab = item.m_prefab.gameObject;
                localItemData.SetCustomData("m_price", item.m_price);
                localItemData.SetCustomData("m_stack", item.m_stack);

                var remoteItemData = new Inventory_Patch.NetworkedItemData();
                remoteItemData.Serialize(localItemData, pkg);
            }

            trader.GetNetView().InvokeRPC(sender, rpcTradeList, pkg.Compress());
        }

        private static void RPC_SoldItem(Trader trader, long sender, int itemHash, int count)
        {
            var itemObj = ObjectDB.instance.GetItemPrefab(itemHash);
            if (itemObj == null)
                return;
            var item = itemObj.GetComponent<ItemDrop>();
            if (item == null)
                return;

            StoreGui_Patch.SoldItem(StoreGui.instance, item, count);
        }

        private static void RPC_BoughtItem(Trader trader, long sender, int itemHash, int count)
        {
            var itemObj = ObjectDB.instance.GetItemPrefab(itemHash);
            if (itemObj == null)
                return;
            var item = itemObj.GetComponent<ItemDrop>();
            if (item == null)
                return;

            StoreGui_Patch.BoughtItem(StoreGui.instance, item, count);
        }

        private static void RPC_SellItem(Trader trader, long sender, int itemId, int count)
        {
            if (count <= 0)
                return;

            var peer = ZNet.instance.GetPeer(sender);
            if (peer == null || peer.m_player == null || peer.m_player.IsDead())
                return;

            var player = peer.m_player;
            var inventory = player.GetInventory();

            if ((trader.transform.position - player.transform.position).sqrMagnitude > player.GetMaxSqrInteractRange())
                return;

            var item = inventory.m_inventory.SingleOrDefault(k => k.m_id == itemId);
            if (item == null)
                return;

            int stack = item.m_shared.m_value * count;

            inventory.RemoveItem(item, count);
            var currency = trader.GetCurrencyPrefab();
            inventory.AddItem(currency.m_itemData.m_shared.m_name, stack, currency.m_itemData.m_quality, currency.m_itemData.m_variant, currency.m_itemData.m_crafterID, currency.m_itemData.m_crafterName);
            trader.OnSold();

            trader.GetNetView().InvokeRPC(sender, rpcSoldItem, item.m_dropPrefab.name.GetStableHashCode(), count);
        }

        private static void RPC_BuyItem(Trader trader, long sender, int itemId, int count)
        {
            if (count <= 0)
                return;

            var peer = ZNet.instance.GetPeer(sender);
            if (peer == null || peer.m_player == null || peer.m_player.IsDead())
                return;

            var player = peer.m_player;
            if ((trader.transform.position - player.transform.position).sqrMagnitude > player.GetMaxSqrInteractRange())
                return;

            var item = trader.m_items.SingleOrDefault(k => k.m_prefab.m_itemData.m_id == itemId);
            if (item == null)
                return;

            if (trader.GetPlayerCurrency(player) >= item.m_price * count)
            {
                var itemData = item.m_prefab.m_itemData;
                int stack = Mathf.Min(item.m_stack, itemData.m_shared.m_maxStackSize);
                var boughtCount = 0;

                for (int i = 0; i < count; i++)
                {
                    if (player.GetInventory().AddItem(item.m_prefab.name, stack, itemData.m_quality, itemData.m_variant, itemData.m_crafterID, itemData.m_crafterName) == null)
                        break;
                    player.GetInventory().RemoveItem(trader.GetCurrencyName(), item.m_price);
                    boughtCount++;
                }

                if (boughtCount > 0)
                {
                    trader.OnBought(item);
                    trader.GetNetView().InvokeRPC(sender, rpcBoughtItem, item.m_prefab.name.GetStableHashCode(), boughtCount * stack);
                }
            }
        }
    }

    public static class TraderExtension
    {
        public static int GetPlayerCurrency(this Trader trader, Player player)
        {
            return player.m_inventory.CountItems(GetCurrencyName(trader));
        }

        public static string GetCurrencyName(this Trader trader)
        {
            return trader.GetCurrencyPrefab().m_itemData.m_shared.m_name;
        }

        public static ItemDrop GetCurrencyPrefab(this Trader trader)
        {
            return StoreGui.instance.m_coinPrefab;
        }

        public static ZNetView GetNetView(this Trader trader)
        {
            return trader.GetComponentInParent<ZNetView>();
        }

        public static void SellItem(this Trader trader, int itemId, int itemCount)
        {
            trader.GetNetView().InvokeRPC(Trader_Patch.rpcSellItem, parameters: new object[] { itemId, itemCount });
        }

        public static void BuyItem(this Trader trader, int itemId, int itemCount)
        {
            trader.GetNetView().InvokeRPC(Trader_Patch.rpcBuyItem, parameters: new object[] { itemId, itemCount });
        }

        public static void RequestTradeList(this Trader trader)
        {
            trader.GetNetView().InvokeRPC(Trader_Patch.rpcRequestTradeList);
        }
    }
}

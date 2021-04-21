using ValheimMP.Patches;

namespace ValheimMP.Framework.Extensions
{
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
            var nview = trader.GetComponentInParent<ZNetView>();
            if (nview)
                return nview;
            return null;
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

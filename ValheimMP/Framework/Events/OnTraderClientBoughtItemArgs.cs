namespace ValheimMP.Framework.Events
{
    public class OnTraderClientBoughtItemArgs
    {
        public Trader Trader { get; internal set; }
        public int ItemHash { get; internal set; }
        public int Count { get; internal set; }

        /// <summary>
        /// By default buying an item shows the pickup text and refreshes the list
        /// </summary>
        public bool SuppressDefaultEvent { get; set; }
    }
}

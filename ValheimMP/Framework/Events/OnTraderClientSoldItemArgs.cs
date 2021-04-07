using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Framework.Events
{
    public class OnTraderClientSoldItemArgs
    {
        public Trader Trader { get; internal set; }
        public int ItemHash { get; internal set; }
        public int Count { get; internal set; }
        public bool SuppressDefaultEvent { get; set; }
    }
}

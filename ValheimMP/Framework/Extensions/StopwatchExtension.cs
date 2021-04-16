using System.Diagnostics;

namespace ValheimMP.Framework.Extensions
{
    public static class StopwatchExtension
    {
        public static float GetElapsedMilliseconds(this Stopwatch sw) 
        {
            return sw.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L)) / 1000f;
        }
    }
}

namespace ValheimMP
{
    public static class DebugMod
    {
        public static void LogComponent(object __instance, string message)
        {
            ZLog.LogWarning(message);
            var comp = __instance as UnityEngine.Component;
            if (comp)
            {
                object[] obj = comp.GetComponents<object>();
                foreach (var o in obj) { ZLog.Log(o.GetType().ToString()); }
            }
            ZLog.Log(new System.Diagnostics.StackTrace(1).ToString());
        }
    }
}

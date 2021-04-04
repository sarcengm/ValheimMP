namespace ValheimMP
{
    public static class DebugMod
    {
        public static void LogComponent(object __instance, string message)
        {
            ValheimMP.LogWarning(message);
            var comp = __instance as UnityEngine.Component;
            if (comp)
            {
                object[] obj = comp.GetComponents<object>();
                foreach (var o in obj) { ValheimMP.Log(o.GetType().ToString()); }
            }
            ValheimMP.Log(new System.Diagnostics.StackTrace(1).ToString());
        }
    }
}

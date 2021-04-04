namespace ValheimMP.Framework.Extensions
{
    public static class ZRpcExtension
    {
        public static void SendErrorMessage(this ZRpc rpc, string message)
        {
            rpc.Invoke("Error", 3);
        }
    }
}

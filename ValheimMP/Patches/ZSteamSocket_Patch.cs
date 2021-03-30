using Steamworks;
using System;
using System.Runtime.InteropServices;

namespace ValheimMP.Patches
{
    internal class ZSteamSocket_Patch
    {
        private class DisposeableHandle : IDisposable
        {
            private GCHandle m_handle;

            public IntPtr IntPtr { get { return m_handle.AddrOfPinnedObject(); } }

            public DisposeableHandle(object obj)
            {
                m_handle = GCHandle.Alloc(obj, GCHandleType.Pinned);
            }

            public void Dispose()
            {
                m_handle.Free();
            }
        }

        private static void RegisterGlobalCallbacks()
        {
            if (ValheimMPPlugin.Instance.ArtificialPing.Value > 0)
            {
                using var fakePacketLag_Send = new DisposeableHandle(ValheimMPPlugin.Instance.ArtificialPing.Value);
                using var fakePacketLag_Recv = new DisposeableHandle(ValheimMPPlugin.Instance.ArtificialPing.Value);
                try
                {
                    SteamNetworkingUtils.SetConfigValue(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_FakePacketLag_Send,
                        ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global,
                        IntPtr.Zero,
                        ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Float,
                        fakePacketLag_Send.IntPtr);
                    SteamNetworkingUtils.SetConfigValue(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_FakePacketLag_Recv,
                        ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global,
                        IntPtr.Zero,
                        ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Float,
                        fakePacketLag_Recv.IntPtr);
                }
                catch (Exception)
                {

                }
            }
        }
    }
}

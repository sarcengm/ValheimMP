using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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
            if (ValheimMP.Instance.ArtificialPing > 0)
            {
                using var fakePacketLag_Send = new DisposeableHandle(ValheimMP.Instance.ArtificialPing);
                using var fakePacketLag_Recv = new DisposeableHandle(ValheimMP.Instance.ArtificialPing);
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

    public static class ZSteamSocketExt
    {
        public static void SendUnreliable(this ZSteamSocket sock, ZPackage pkg)
        {
            if (!sock.IsConnected())
            {
                return;
            }
            var array = pkg.GetArray();
            IntPtr intPtr = Marshal.AllocHGlobal(array.Length);
            Marshal.Copy(array, 0, intPtr, array.Length);
            long pOutMessageNumber;
            EResult eResult = SteamNetworkingSockets.SendMessageToConnection(sock.m_con, intPtr, (uint)array.Length, 1|4, out pOutMessageNumber);
            Marshal.FreeHGlobal(intPtr);
            if (eResult == EResult.k_EResultOK)
            {
                sock.m_totalSent += array.Length;
            }
        }
    }
}

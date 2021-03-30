using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Framework.Extensions
{
    public static class ZNetViewExtension
    {
        public static void InvokeProximityRPC(this ZNetView netView, float range, long targetID, string method, params object[] parameters)
        {
            ZRoutedRpc.instance.InvokeProximityRoutedRPC(netView.transform.position, range, targetID, netView.m_zdo.m_uid, method, parameters);
        }

        public static void Register(this ZNetView netView, int hash, Action<long> f)
        {
            netView.m_functions.Add(hash, new RoutedMethod(f));
        }

        public static void Register<T>(this ZNetView netView, int hash, Action<long, T> f)
        {
            netView.m_functions.Add(hash, new RoutedMethod<T>(f));
        }

        public static void Register<T, U>(this ZNetView netView, int hash, Action<long, T, U> f)
        {
            netView.m_functions.Add(hash, new RoutedMethod<T, U>(f));
        }

        public static void Register<T, U, V>(this ZNetView netView, int hash, Action<long, T, U, V> f)
        {
            netView.m_functions.Add(hash, new RoutedMethod<T, U, V>(f));
        }

        public static void InvokeRPC(this ZNetView netView, long targetID, int methodHash, params object[] parameters)
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(targetID, netView.m_zdo.m_uid, methodHash, parameters);
        }

        public static void InvokeRPC(this ZNetView netView, int methodHash, params object[] parameters)
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(netView.m_zdo.m_owner, netView.m_zdo.m_uid, methodHash, parameters);
        }
    }
}

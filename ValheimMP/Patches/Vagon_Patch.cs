using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    internal class Vagon_Patch
    {
        public static KeyValuePair<int, int> m_attachToHash = ZDO.GetHashZDOID("AttachTo");
        public static int m_LastUserHash = "LastUser".GetStableHashCode();

        [HarmonyPatch(typeof(Vagon), "Awake")]
        [HarmonyPostfix]
        private static void Awake(Vagon __instance)
        {
            // this does not need to be known by the client
            __instance.m_nview.m_zdo.SetFieldType(m_LastUserHash, ZDOFieldType.Ignored);
            __instance.m_nview.m_zdo.RegisterZDOEvent(m_attachToHash.Value, (ZDO zdo) =>
            {
                ZDOEvent_AttachTo(__instance);
            });

            if(__instance.m_container != null && __instance.m_container.m_inventory != null)
            {
                __instance.m_container.m_inventory.m_onChanged += () => {
                    __instance.CancelInvoke("UpdateMass");
                    __instance.CancelInvoke("UpdateLoadVisualization");
                    __instance.Invoke("UpdateMass", 0.1f);
                    __instance.Invoke("UpdateLoadVisualization", 0.1f);
                };
            }
        }

        [HarmonyPatch(typeof(Vagon), "OnDestroy")]
        [HarmonyPostfix]
        private static void OnDestroy(Vagon __instance)
        {
            if(__instance.m_nview.m_zdo != null)
            {
                __instance.m_nview.m_zdo.ClearZDOEvent(m_attachToHash.Value);
            }
        }

        [HarmonyPatch(typeof(Vagon), "DetachAll")]
        [HarmonyPrefix]
        private static bool DetachAll()
        {
            if (DetachAll_m_useRequester == null)
                return false;

            foreach (Vagon instance in Vagon.m_instances)
            {
                if (DetachAll_m_useRequester == instance.m_useRequester)
                {
                    instance.Detach();
                }
            }
            return false;
        }
        private static Humanoid DetachAll_m_useRequester;

        [HarmonyPatch(typeof(Vagon), "AttachTo")]
        [HarmonyPrefix]
        private static void AttachTo(Vagon __instance, GameObject go)
        {
            // TODO: Instead of this shitty solution i should probably use the transpiler to add a parameter and call a custom DetachAll
            DetachAll_m_useRequester = __instance.m_useRequester;
        }

        
        [HarmonyPatch(typeof(Vagon), "AttachTo")]
        [HarmonyPostfix]
        private static void AttachTo_Post(Vagon __instance, GameObject go)
        {
            var nv = go.GetComponent<ZNetView>();
            ValheimMP.Log($"AttachTo_Post {nv} {nv?.m_zdo.m_uid}");
            if (nv?.m_zdo != null)
            {
                __instance.m_nview.m_zdo.Set(m_attachToHash, nv.m_zdo.m_uid);
            }
        }

        [HarmonyPatch(typeof(Vagon), "Detach")]
        [HarmonyPostfix]
        private static void Detach(Vagon __instance)
        {
            __instance.m_nview.m_zdo.Set(m_attachToHash, ZDOID.None);
        }

        [HarmonyPatch(typeof(Vagon), "Interact")]
        [HarmonyPrefix]
        private static bool Interact(Vagon __instance, ref bool __result, Humanoid character, bool hold)
        {
            __result = false;
            if (hold)
            {
                return false;
            }
            if (!__instance.m_nview.IsOwner())
            {
                __instance.m_nview.InvokeRPC("RequestOwn");
            }
            return false;
        }

        [HarmonyPatch(typeof(Vagon), "RPC_RequestOwn")]
        [HarmonyPrefix]
        private static bool RPC_RequestOwn(Vagon __instance, long sender)
        {
            var peer = ZNet.instance.GetPeer(sender);
            if (peer == null)
                return false;

            var player = peer.m_player;
            if (player == null)
                return false;

            if (__instance.IsAttached() && __instance.m_nview.m_zdo.GetLong(m_LastUserHash) == sender)
            {
                __instance.Detach();
                return false;
            }

            if ((peer.m_player.transform.position - __instance.transform.position).sqrMagnitude > peer.m_player.m_maxInteractDistance * peer.m_player.m_maxInteractDistance)
                return false;

            var piece = __instance.GetComponent<Piece>();
            // Can't steal carts from private areas, unless you are the creator or last user of the cart.
            // (This should only happen if you drag your cart into a protected area and drop it)
            // Outside private areas anything is fair game
            if ((piece != null && piece.GetCreator() != sender) && 
                (__instance.m_nview.m_zdo.GetLong(m_LastUserHash, 0) != sender) && 
                !PrivateArea_Patch.CheckAccess(sender, __instance.transform.position))
                return false;

            if (__instance.InUse())
            {
                player.Message(MessageHud.MessageType.Center, __instance.m_name + " is in use by someone else");
            }
            else
            {
                __instance.m_useRequester = player;
                __instance.m_nview.m_zdo.Set(m_LastUserHash, sender);
            }

            return false;
        }

        [HarmonyPatch(typeof(Vagon), "FixedUpdate")]
        [HarmonyPrefix]
        private static bool FixedUpdate(Vagon __instance)
        {
            if (!__instance.m_nview.IsValid())
            {
                return false;
            }
            __instance.UpdateAudio(Time.fixedDeltaTime);

            if (__instance.m_nview.IsOwner())
            {
                if (__instance.m_useRequester != null)
                {
                    if (__instance.IsAttached())
                    {
                        __instance.Detach();
                    }
                    else if (__instance.CanAttach(__instance.m_useRequester.gameObject))
                    {
                        __instance.AttachTo(__instance.m_useRequester.gameObject);
                    }
                    else
                    {
                        __instance.m_useRequester.Message(MessageHud.MessageType.Center, "Not in the right position");
                    }
                    __instance.m_useRequester = null;
                }
                if (__instance.IsAttached() && (__instance.m_attachJoin.connectedBody == null || !__instance.CanAttach(__instance.m_attachJoin.connectedBody.gameObject)))
                {
                    __instance.Detach();
                }
            }
            else
            {
                if (__instance.m_useRequester != null)
                {
                    if (!__instance.IsAttached())
                    {
                        __instance.AttachTo(__instance.m_useRequester.gameObject);
                        // Clients need room to move since they are not in control of the cart, this may be an issue in an of itself.
                        __instance.m_attachJoin.linearLimit = new SoftJointLimit() { limit = 1.5f };
                        __instance.m_attachJoin.zMotion = ConfigurableJointMotion.Limited;
                    }
                }
                else
                {
                    __instance.Detach();
                }
            }
            return false;
        }

        private static void ZDOEvent_AttachTo(Vagon __instance)
        {
            if (__instance == null)
                return;

            var attachTargetID = __instance.m_nview.m_zdo.GetZDOID(m_attachToHash);
            var attachTarget = ZNetScene.instance.FindInstance(attachTargetID);

            Humanoid user = null;
            if(attachTarget != null)
            {
                user = attachTarget.GetComponent<Humanoid>();
            }

            if (user != null)
            {
                __instance.m_useRequester = user;
            }
            else
            {
                __instance.m_useRequester = null;
            }
        }
    }
}

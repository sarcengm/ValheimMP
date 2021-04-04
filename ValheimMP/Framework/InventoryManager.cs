using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Framework
{
    public class InventoryManager
    {
        private float m_inventorySyncTimer = 0;
        private Dictionary<ZDOID, Dictionary<int, InventoryWrapper>> m_inventoryWrappers = new Dictionary<ZDOID, Dictionary<int, InventoryWrapper>>();
        private HashSet<InventoryWrapper> m_changedInventories = new HashSet<InventoryWrapper>();
        private ValheimMP m_valheimMP;

        public InventoryManager(ValheimMP valheimMP)
        {
            m_valheimMP = valheimMP;
        }

        public delegate void OnItemCraftedDelegate(int craftTrigger, Inventory inventory, ItemDrop.ItemData itemData, byte[] triggerData);
        public OnItemCraftedDelegate OnItemCrafted { get; set; }

        private class InventoryWrapper
        {
            private Dictionary<long, Dictionary<int, NetworkedItemData>> listenerItemData = new();
            public Inventory Inventory { get; private set; }

            private static readonly float maxListenerRange = 20f;

            private static readonly float maxListenerRangeSqr = maxListenerRange * maxListenerRange;

            private InventoryManager m_inventoryManager;

            public InventoryWrapper(InventoryManager inventoryManager, Inventory inventory, int index)
            {
                Inventory = inventory;
                m_inventoryManager = inventoryManager;

                Inventory.m_onChanged += OnChanged;
                Inventory.m_inventoryIndex = index;
            }

            ~InventoryWrapper()
            {
                if (Inventory != null)
                    Inventory.m_onChanged -= OnChanged;
            }

            public void OnChanged()
            {
                if (Inventory.m_nview != null && Inventory.m_nview.m_zdo != null)
                {
                    // may be called multiple times so dont use add.
                    m_inventoryManager.m_changedInventories.Add(this);
                }
            }

            public List<long> GetListeners()
            {
                return listenerItemData.Keys.ToList();
            }

            public void AddListener(long user)
            {
                if (!listenerItemData.ContainsKey(user))
                {
#if DEBUG_INVENTORY
                    ValheimMP.Log($"AddListener {user} {Inventory} {Inventory?.m_nview} {Inventory?.m_nview?.m_zdo} ");
#endif
                    listenerItemData.Add(user, new Dictionary<int, NetworkedItemData>());

                    OnChanged();
                }
            }

            public void RemoveListener(long user)
            {
                listenerItemData.Remove(user);
            }

            public void SyncListeners()
            {
                if (Inventory == null || Inventory.m_nview == null || Inventory.m_nview.m_zdo == null)
                    return;

                var crafted = new List<ItemDrop.ItemData>();
                var znet = ZNet.instance;
                var listeners = listenerItemData.Keys.ToList();
                foreach (var user in listeners)
                {
                    var peer = znet.GetPeer(user);
                    if (peer == null)
                    {
                        // this listener is no longer valid
                        listenerItemData.Remove(user);
                        continue;
                    }

                    // Far away from the inventory, lets not sync this person.
                    if ((peer.m_refPos - Inventory.m_nview.m_zdo.GetPosition()).sqrMagnitude > maxListenerRangeSqr)
                    {
                        continue;
                    }

                    var pkg = new ZPackage();
                    //Write the inventory owner uid!
                    pkg.Write(Inventory.m_nview.m_zdo.m_uid);
                    //Write the inventory index!
                    pkg.Write(Inventory.m_inventoryIndex);

                    var countPos = pkg.GetPos();
                    pkg.Write((int)0); // Placeholder item count

                    var syncedItems = new HashSet<int>();
                    var newItems = 0; // new items that didnt exist in the previous
                    var updatedItems = 0; // update check of change
                    var modifiedItems = 0; // actually modified items
                    var userItemData = listenerItemData[user];
                    foreach (var item in Inventory.m_inventory)
                    {
                        NetworkedItemData networkedItemData = null;
                        syncedItems.Add(item.m_id);

                        if (!userItemData.TryGetValue(item.m_id, out networkedItemData))
                        {
                            newItems++;
                            networkedItemData = new NetworkedItemData(m_inventoryManager);
                            userItemData.Add(item.m_id, networkedItemData);
                        }
                        else
                        {
                            updatedItems++;
                        }

                        var flags = networkedItemData.Serialize(item, pkg);
                        if (flags != NetworkedItemDataFlags.none)
                        {
                            modifiedItems++;
                        }
                    }

                    foreach (var item in userItemData.ToList())
                    {
                        if (!syncedItems.Contains(item.Value.m_id))
                        {
                            userItemData.Remove(item.Key);
                            // This item no longer exists in the inventory
                            pkg.Write((int)NetworkedItemDataFlags.m_destroy);
                            pkg.Write(item.Value.m_id);
                            modifiedItems++;
                        }
                    }

                    var endPos = pkg.GetPos();
                    pkg.SetPos(countPos);
                    pkg.Write(modifiedItems);
                    pkg.SetPos(endPos);
                    // Shrink the stream if needed, there may be two integers of the last package if it ended up with no flags.
                    pkg.m_stream.SetLength(endPos);

                    peer.m_rpc.Invoke("InventoryData", pkg.Compress());
                }
            }

            public bool IsListener(long user)
            {
                return listenerItemData.ContainsKey(user);
            }
        }

        internal void RPC_InventoryData(ZPackage pkg)
        {
            pkg = pkg.Decompress();
            var uid = pkg.ReadZDOID();
            var index = pkg.ReadInt();
            var count = pkg.ReadInt();

            var inventory = GetInventory(uid, index);

            if (inventory == null)
            {
                ValheimMP.Log($"Missing inventory for inventory deserialization: {uid} index: {index} count: {count}");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var tmp = new NetworkedItemData(this);
                tmp.Deserialize(inventory, pkg);
            }
        }

        /// <summary>
        /// Register an inventory, needed for network syncing.
        /// </summary>
        /// <param name="inventory">inventory</param>
        /// <param name="netview">netview, usually the one from the parent object, e.g. character or chest.</param>
        public void Register(Inventory inventory, ZNetView netview)
        {
            if (netview == null)
                return;

            inventory.m_nview = netview;

            var zdo = netview.GetZDO();
            if (zdo == null)
            {
                ValheimMP.Log("Register Inventory, ZDO missing.");
                return;
            }

            if (!m_inventoryWrappers.ContainsKey(zdo.m_uid))
            {
                m_inventoryWrappers.Add(zdo.m_uid, new Dictionary<int, InventoryWrapper>());
            }

            var inventoriesOnZDOID = m_inventoryWrappers[zdo.m_uid];
            var index = inventoriesOnZDOID.Count;

            inventoriesOnZDOID[index] = new InventoryWrapper(this, inventory, index);
        }

        public void UnregisterAll(ZNetView netview)
        {
            if (netview == null)
                return;

            var zdo = netview.GetZDO();
            if (zdo != null)
            {
                var id = zdo.m_uid;

                m_inventoryWrappers.Remove(id);
                m_changedInventories.RemoveWhere(k => k.Inventory.m_nview == netview);
            }
        }

        public void Unregister(ZNetView netview, int index)
        {
            if (netview == null)
                return;
            var zdo = netview.GetZDO();
            if (zdo != null)
            {
                var id = zdo.m_uid;

                if (m_inventoryWrappers.TryGetValue(id, out var val))
                {
                    val.Remove(index);
                }

                m_changedInventories.RemoveWhere(k => k.Inventory.m_nview == netview && k.Inventory.m_inventoryIndex == index);
            }
        }

        public void Unregister(Inventory inventory)
        {
            var id = inventory.GetZDOID();
            if (m_inventoryWrappers.TryGetValue(id, out var listeners))
            {
                listeners.Remove(inventory.m_inventoryIndex);
            }

            m_changedInventories.RemoveWhere(k => k.Inventory == inventory);
        }

        public void Unregister(ZDOID id)
        {
            m_inventoryWrappers.Remove(id);
            m_changedInventories.RemoveWhere(k => k.Inventory.GetZDOID() == id);
        }

        public void RemoveListenerFromAll(long user)
        {
            m_inventoryWrappers.Values.Do(k => k.Values.Do(j => j.RemoveListener(user)));
        }

        private InventoryWrapper GetInventoryWrapper(Inventory inventory)
        {
            if (m_inventoryWrappers.TryGetValue(inventory.GetZDOID(), out var dic))
            {
                if (dic.TryGetValue(inventory.m_inventoryIndex, out var val))
                {
                    return val;
                }
            }

            return null;
        }

        /// <summary>
        /// Add a listener to this inventory. User will be send updates when the inventory changes.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="inventory"></param>
        public bool AddListener(long user, Inventory inventory)
        {
            InventoryWrapper wrapper;
            if ((wrapper = GetInventoryWrapper(inventory)) != null)
            {
                wrapper.AddListener(user);
                return true;
            }

            ValheimMP.Log($"Inventory AddListener, missing inventory wrapper, inventory not registered.");
            return false;
        }

        /// <summary>
        /// Check if user is a listener on that inventory.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="inventory"></param>
        /// <returns></returns>
        public bool IsListener(long user, Inventory inventory)
        {
            InventoryWrapper wrapper;
            if ((wrapper = GetInventoryWrapper(inventory)) != null)
            {
                return wrapper.IsListener(user);
            }

            ValheimMP.Log($"Inventory IsListener, missing inventory wrapper, inventory not registered.");
            return false;
        }


        /// <summary>
        /// Remove a listener from an inventory
        /// </summary>
        /// <param name="user"></param>
        /// <param name="inventory"></param>
        public bool RemoveListener(long user, Inventory inventory)
        {
            InventoryWrapper wrapper;
            if ((wrapper = GetInventoryWrapper(inventory)) != null)
            {
                wrapper.RemoveListener(user);
                return true;
            }

            ValheimMP.Log($"Inventory RemoveListener, missing inventory wrapper, inventory not registered.");
            return false;
        }

        public List<long> GetListeners(Inventory inventory)
        {
            InventoryWrapper wrapper;
            if ((wrapper = GetInventoryWrapper(inventory)) != null)
            {
                return wrapper.GetListeners();
            }

            ValheimMP.Log($"Inventory GetListeners, missing inventory wrapper, inventory not registered.");
            return null;
        }


        /// <summary>
        /// Get the inventory belonging to the ZDOID
        /// </summary>
        /// <param name="id"></param>
        /// <param name="index">Inventory index, in case an object contains more then one inventory object</param>
        /// <returns></returns>
        public Inventory GetInventory(ZDOID id, int index = 0)
        {
            if (m_inventoryWrappers.TryGetValue(id, out var dic))
            {
                if (dic.TryGetValue(index, out var val))
                {
                    return val.Inventory;
                }
            }
            return null;
        }

        /// <summary>
        /// Get all inventories on a ZDOID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public List<Inventory> GetInventories(ZDOID id)
        {
            if (m_inventoryWrappers.TryGetValue(id, out var val))
            {
                return val.Values.Select(k => k.Inventory).ToList();
            }
            return null;
        }

        public void SyncAll()
        {
            if (Time.time - m_inventorySyncTimer < 0.05f)
            {
                return;
            }

            m_inventorySyncTimer = Time.time;

            var list = m_changedInventories.ToList();
            foreach (var item in list)
            {
                item.SyncListeners();
            }

            m_changedInventories.Clear();
        }
    }
}

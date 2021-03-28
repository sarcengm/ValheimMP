using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ValheimMP.Patches
{
    [HarmonyPatch]
    public class Inventory_Patch
    {
        /// <summary>
        /// Unregister items attached to netviews when reset, ideally objects would unregister on destruction
        /// but looking at the code it seems they lose their ZDO before destruction, since we need that ID we
        /// hook into ResetZDO. And I don't want to copy paste this same patch for every object type I will 
        /// unregister all of them here.
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPatch(typeof(ZNetView), "ResetZDO")]
        [HarmonyPatch(typeof(ZNetView), "Destroy")]
        [HarmonyPrefix]
        private static void UnregisterNetViewPatch(ZNetView __instance)
        {
            if (__instance.m_zdo != null)
            {
                var container = __instance.GetComponentInChildren<Container>();
                if (container != null)
                {
                    Unregister(container.m_inventory);
                    return;
                }

                var humanoid = __instance.GetComponent<Humanoid>();
                if (humanoid != null)
                {
                    Unregister(humanoid.m_inventory);
                    return;
                }
            }
        }

        private static int itemDataId = 0;

        [HarmonyPatch(typeof(ItemDrop.ItemData), MethodType.Constructor)]
        [HarmonyPostfix]
        private static void ItemDataConstructor(ItemDrop.ItemData __instance)
        {
            // unique id during runtime used for replication
            // non persistant after saving.
            __instance.m_id = ++itemDataId;
        }

        [HarmonyPatch(typeof(ItemDrop.ItemData), "Clone")]
        [HarmonyPostfix]
        private static void Clone(ItemDrop.ItemData __instance, ItemDrop.ItemData __result)
        {
            __result.m_id = ++itemDataId;
        }

        public static void RemoveListenerFromAll(long user)
        {
            m_inventoryListeners.Values.Do(k => k.RemoveListener(user));
        }

        /// <summary>
        /// Add a listener to this inventory. User will be send updates when the inventory changes.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="m_inventory"></param>
        /// <param name="index"></param>
        public static bool AddListener(long user, Inventory inventory)
        {
            var key = new InventoryKey(inventory);

            if (m_inventoryListeners.TryGetValue(key, out var inventoryWrapper))
            {
                inventoryWrapper.AddListener(user);
                return true;
            }

            ZLog.Log($"Inventory AddListener, missing inventory wrapper {key.m_owner}:{key.m_index}, inventory not registered.");
            return false;
        }

        internal static bool IsListener(long user, Inventory inventory)
        {
            var key = new InventoryKey(inventory);

            if (m_inventoryListeners.TryGetValue(key, out var inventoryWrapper))
            {
                return inventoryWrapper.IsListener(user);
            }

            ZLog.Log($"Inventory IsListener, missing inventory wrapper {key.m_owner}:{key.m_index}, inventory not registered.");
            return false;
        }


        /// <summary>
        /// Remove a listener from an inventory
        /// </summary>
        /// <param name="user"></param>
        /// <param name="inventory"></param>
        /// <param name="index"></param>
        public static bool RemoveListener(long user, Inventory inventory)
        {
            var key = new InventoryKey(inventory);

            if (m_inventoryListeners.TryGetValue(key, out var inventoryWrapper))
            {
                inventoryWrapper.RemoveListener(user);
                return true;
            }

            ZLog.Log($"Inventory RemoveListener, missing inventory wrapper {key.m_owner}:{key.m_index}, inventory not registered.");
            return false;
        }

        public static List<long> GetListeners(Inventory inventory)
        {
            var key = new InventoryKey(inventory);

            if (m_inventoryListeners.TryGetValue(key, out var inventoryWrapper))
            {
                return inventoryWrapper.GetListeners();
            }

            ZLog.Log($"Inventory GetListeners, missing inventory wrapper {key.m_owner}:{key.m_index}, inventory not registered.");
            return null;
        }

        [Flags]
        public enum NetworkedItemDataFlags
        {
            none = 0,
            m_dropPrefab = 1,
            m_stack = 1 << 1,
            m_durability = 1 << 2,
            m_gridPos = 1 << 3,
            m_quality = 1 << 4,
            m_variant = 1 << 5,
            m_crafterID = 1 << 6,
            m_crafterName = 1 << 7,

            // special flag when the inventory item is destroyed
            m_destroy = 1 << 8,
            // when this item is equiped
            m_equiped = 1 << 9,
        }

        public class NetworkedItemData
        {
            public int m_id;
            private string m_dropPrefab;
            private int m_stack;
            private float m_durability;
            private Vector2i m_gridPos;
            private int m_quality;
            private int m_variant;
            private long m_crafterID;
            private string m_crafterName = "";

            private bool m_equiped;

            /// <summary>
            /// Serializes itemData based on the differences with this NetworkedItemData.
            /// </summary>
            /// <param name="itemData"></param>
            /// <param name="pkg"></param>
            /// <returns></returns>
            public NetworkedItemDataFlags Serialize(ItemDrop.ItemData itemData, ZPackage pkg)
            {
                NetworkedItemDataFlags flags = NetworkedItemDataFlags.none;
                var flagsPos = pkg.GetPos();
                pkg.Write((int)flags);

                m_id = itemData.m_id;
                pkg.Write(itemData.m_id);

                if (itemData.m_dropPrefab.name != m_dropPrefab)
                {
                    m_dropPrefab = itemData.m_dropPrefab.name;
                    flags |= NetworkedItemDataFlags.m_dropPrefab;
                    pkg.Write(m_dropPrefab);
                }
                if (itemData.m_stack != m_stack)
                {
                    m_stack = itemData.m_stack;
                    flags |= NetworkedItemDataFlags.m_stack;
                    pkg.Write(m_stack);
                }
                if (Mathf.Abs(itemData.m_durability - m_durability) > 0.01f)
                {
                    m_durability = itemData.m_durability;
                    flags |= NetworkedItemDataFlags.m_durability;
                    pkg.Write(m_durability);
                }
                if (itemData.m_gridPos != m_gridPos)
                {
                    m_gridPos = itemData.m_gridPos;
                    flags |= NetworkedItemDataFlags.m_gridPos;
                    pkg.Write(m_gridPos);
                }
                if (itemData.m_quality != m_quality)
                {
                    m_quality = itemData.m_quality;
                    flags |= NetworkedItemDataFlags.m_quality;
                    pkg.Write(m_quality);
                }
                if (itemData.m_variant != m_variant)
                {
                    m_variant = itemData.m_variant;
                    flags |= NetworkedItemDataFlags.m_variant;
                    pkg.Write(m_variant);
                }
                if (itemData.m_crafterID != m_crafterID)
                {
                    m_crafterID = itemData.m_crafterID;
                    flags |= NetworkedItemDataFlags.m_crafterID;
                    pkg.Write(m_crafterID);
                }
                if (itemData.m_crafterName != m_crafterName)
                {
                    m_crafterName = itemData.m_crafterName;
                    flags |= NetworkedItemDataFlags.m_crafterName;
                    pkg.Write(m_crafterName);
                }
                if (itemData.m_equiped != m_equiped)
                {
                    m_equiped = itemData.m_equiped;
                    flags |= NetworkedItemDataFlags.m_equiped;
                    pkg.Write(m_equiped);
                }

                var endPos = pkg.GetPos();
                pkg.SetPos(flagsPos);
                if (flags != NetworkedItemDataFlags.none)
                {
                    pkg.Write((int)flags);
                    pkg.SetPos(endPos);
                }
                return flags;
            }

            /// <summary>
            /// Deserializes the object into the target inventory.
            /// 
            /// This one is a little strange because the object itself is basically used as local variable. 
            /// </summary>
            /// <param name="targetInventory"></param>
            /// <param name="pkg"></param>
            /// <returns></returns>
            public ItemDrop.ItemData Deserialize(Inventory targetInventory, ZPackage pkg)
            {
                var equipedChanged = false;
                var intFlags = pkg.ReadInt();
                var flags = (NetworkedItemDataFlags)intFlags;

                var m_id = pkg.ReadInt();

                var itemData = targetInventory.m_inventory.SingleOrDefault(k => k.m_id == m_id);

                if ((flags & NetworkedItemDataFlags.m_dropPrefab) == NetworkedItemDataFlags.m_dropPrefab)
                {
                    m_dropPrefab = pkg.ReadString();

                    if (itemData != null)
                    {
                        ZLog.Log("m_dropPrefab changed?");
                        // setting a name? :thinking:
                        // Like this maybe? I don't think item name changes should ever happen though
                        targetInventory.RemoveItem(itemData);
                        itemData = null;
                    }

                }
                if ((flags & NetworkedItemDataFlags.m_stack) == NetworkedItemDataFlags.m_stack)
                {
                    m_stack = pkg.ReadInt();

                    if (itemData != null)
                    {
                        itemData.m_stack = m_stack;
                    }
                }
                if ((flags & NetworkedItemDataFlags.m_durability) == NetworkedItemDataFlags.m_durability)
                {
                    m_durability = pkg.ReadSingle();

                    if (itemData != null)
                    {
                        itemData.m_durability = m_durability;
                    }
                }
                if ((flags & NetworkedItemDataFlags.m_gridPos) == NetworkedItemDataFlags.m_gridPos)
                {
                    m_gridPos = pkg.ReadVector2i();

                    if (itemData != null)
                    {
                        itemData.m_gridPos = m_gridPos;
                    }
                }
                if ((flags & NetworkedItemDataFlags.m_quality) == NetworkedItemDataFlags.m_quality)
                {
                    m_quality = pkg.ReadInt();

                    if (itemData != null)
                    {
                        itemData.m_quality = m_quality;
                    }
                }
                if ((flags & NetworkedItemDataFlags.m_variant) == NetworkedItemDataFlags.m_variant)
                {
                    m_variant = pkg.ReadInt();

                    if (itemData != null)
                    {
                        itemData.m_variant = m_variant;
                    }
                }
                if ((flags & NetworkedItemDataFlags.m_crafterID) == NetworkedItemDataFlags.m_crafterID)
                {
                    m_crafterID = pkg.ReadLong();

                    if (itemData != null)
                    {
                        itemData.m_crafterID = m_crafterID;
                    }
                }
                if ((flags & NetworkedItemDataFlags.m_crafterName) == NetworkedItemDataFlags.m_crafterName)
                {
                    m_crafterName = pkg.ReadString();

                    if (itemData != null)
                    {
                        itemData.m_crafterName = m_crafterName;
                    }
                }
                if ((flags & NetworkedItemDataFlags.m_equiped) == NetworkedItemDataFlags.m_equiped)
                {
                    m_equiped = pkg.ReadBool();
                    equipedChanged = true;

                    if (itemData != null)
                    {
                        itemData.m_equiped = m_equiped;
                    }
                }

#if DEBUG_INVENTORY
                if (flags != NetworkedItemDataFlags.none)
                {
                    var sb = new List<string>();
                    sb.Add($"m_id:{m_id}");
                    if ((flags & NetworkedItemDataFlags.m_destroy) == NetworkedItemDataFlags.m_destroy) sb.Add($"m_destroy");
                    if ((flags & NetworkedItemDataFlags.m_dropPrefab) == NetworkedItemDataFlags.m_dropPrefab) sb.Add($"m_dropPrefab:{m_dropPrefab}");
                    if ((flags & NetworkedItemDataFlags.m_stack) == NetworkedItemDataFlags.m_stack) sb.Add($"m_stack:{m_stack}");
                    if ((flags & NetworkedItemDataFlags.m_quality) == NetworkedItemDataFlags.m_quality) sb.Add($"m_quality:{m_quality}");
                    if ((flags & NetworkedItemDataFlags.m_variant) == NetworkedItemDataFlags.m_variant) sb.Add($"m_variant:{m_variant}");
                    if ((flags & NetworkedItemDataFlags.m_crafterID) == NetworkedItemDataFlags.m_crafterID) sb.Add($"m_crafterID:{m_crafterID}");
                    if ((flags & NetworkedItemDataFlags.m_crafterName) == NetworkedItemDataFlags.m_crafterName) sb.Add($"m_crafterName:{m_crafterName}");
                    if ((flags & NetworkedItemDataFlags.m_durability) == NetworkedItemDataFlags.m_durability) sb.Add($"m_durability:{m_durability}");
                    if ((flags & NetworkedItemDataFlags.m_gridPos) == NetworkedItemDataFlags.m_gridPos) sb.Add($"m_gridPos:{m_gridPos}");
                    if ((flags & NetworkedItemDataFlags.m_equiped) == NetworkedItemDataFlags.m_equiped) sb.Add($"m_equiped:{m_equiped}");
                    ZLog.Log($"Deserialize item {sb.Join()} ");
                }
#endif

                if (itemData == null &&
                    // No point in creating an item just to destroy it right away!
                    (flags & NetworkedItemDataFlags.m_destroy) != NetworkedItemDataFlags.m_destroy)
                {
                    // we abuse an empty inventory so we can still use the additem command to create it
                    // if we dont it will act all smart and stack our items!
                    var tempInventory = new Inventory(null, null, 4, 4);
                    itemData = tempInventory.AddItem(m_dropPrefab, m_stack, m_quality, m_variant, m_crafterID, m_crafterName);
                    if (itemData != null)
                    {
                        targetInventory.m_inventory.Add(itemData);
                        itemData.m_id = m_id;
                        itemData.m_gridPos = m_gridPos;
                        itemData.m_equiped = m_equiped;
                        itemData.m_durability = m_durability;
                        itemData.m_stack = m_stack;
                    }
                }

                if (equipedChanged && itemData != null)
                {
                    var humanoid = targetInventory.m_nview.GetComponent<Humanoid>();
                    if (humanoid != null)
                    {
                        Player_Patch.SuppressMessages = true;
                        Humanoid_Patch.LocalActionOnly = true;
                        if (m_equiped) Humanoid_Patch.ForceEquipItem(humanoid, itemData);
                        else humanoid.UnequipItem(itemData);
                        Humanoid_Patch.LocalActionOnly = false;
                        Player_Patch.SuppressMessages = false;
                    }
                }

                if ((flags & NetworkedItemDataFlags.m_destroy) == NetworkedItemDataFlags.m_destroy)
                {
                    if (itemData != null)
                    {
                        targetInventory.m_inventory.Remove(itemData);
                    }
                }

                targetInventory.Changed();

                return itemData;
            }
        }

        public class InventoryListener
        {
            private Dictionary<long, Dictionary<int, NetworkedItemData>> listenerItemData = new Dictionary<long, Dictionary<int, NetworkedItemData>>();
            public Inventory Inventory { get; private set; }
            public ZNetView NetView { get; private set; }
            public int Index { get; private set; }

            public InventoryListener(Inventory inventory, ZNetView netview, int index)
            {
                this.Inventory = inventory;
                this.NetView = netview;
                this.Index = index;

                Inventory.m_onChanged += OnChanged;
                Inventory.m_inventoryIndex = index;
            }

            ~InventoryListener()
            {
                if (Inventory != null)
                    Inventory.m_onChanged -= OnChanged;
            }

            public void OnChanged()
            {
                if (NetView != null && NetView.m_zdo != null)
                {
                    // may be called multiple times so dont use add.
                    m_changedInventories[new InventoryKey(NetView.m_zdo.m_uid, Index)] = this;
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
                    ZLog.Log($"AddListener {user} {NetView}: {Inventory}:{Index} ");
#endif
                    listenerItemData.Add(user, new Dictionary<int, NetworkedItemData>());

                    OnChanged();
                }
            }

            public void RemoveListener(long user)
            {
                listenerItemData.Remove(user);
            }

            private static float maxListenerRange = 10f;
            private static float maxListenerRangeSqr = maxListenerRange * maxListenerRange;

            public void SyncListeners()
            {
                if (NetView == null || NetView.m_zdo == null || Inventory == null)
                    return;

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
                    if ((peer.m_refPos - NetView.m_zdo.GetPosition()).sqrMagnitude > maxListenerRangeSqr)
                    {
                        continue;
                    }

                    var pkg = new ZPackage();
                    //Write the inventory owner uid!
                    pkg.Write(NetView.m_zdo.m_uid);
                    //Write the inventory index!
                    pkg.Write(Index);

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
                            networkedItemData = new NetworkedItemData();
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

                    peer.m_rpc.Invoke("InventoryData", pkg);
                }
            }

            public bool IsListener(long user)
            {
                return listenerItemData.ContainsKey(user);
            }
        }
        internal static void DeserializeRPC(ZPackage pkg)
        {
            var uid = pkg.ReadZDOID();
            var index = pkg.ReadInt();
            var count = pkg.ReadInt();

            var inventory = GetInventory(uid, index);

            if (inventory == null)
            {
                ZLog.Log($"Missing inventory for inventory deserialization: {uid} index: {index} count: {count}");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var tmp = new NetworkedItemData();
                tmp.Deserialize(inventory, pkg);
            }
        }

        private struct InventoryKey : IEquatable<InventoryKey>
        {
            public ZDOID m_owner;
            public int m_index;
            private int m_hash;

            public InventoryKey(ZDOID owner, int index)
            {
                this.m_owner = owner;
                this.m_index = index;
                this.m_hash = 0;
            }

            public InventoryKey(Inventory inventory)
            {
                this.m_owner = inventory.m_nview.m_zdo.m_uid;
                this.m_index = inventory.m_inventoryIndex;
                this.m_hash = 0;
            }

            public override bool Equals(object obj)
            {
                if (obj is InventoryKey)
                    return Equals((InventoryKey)obj);
                return false;
            }

            /// <summary>
            /// Not sure if this is a good way to generate a hash but seems ZDOID uses it lets lets just go with it
            /// After looking how dictionaries work it seems Equals is used after GetHashCode collides so it should be fine.
            /// </summary>
            /// <returns></returns>
            public override int GetHashCode()
            {
                if (m_hash == 0)
                {
                    m_hash = m_owner.GetHashCode() ^ m_index.GetHashCode();
                }
                return m_hash;
            }

            public bool Equals(InventoryKey other)
            {
                return (other.m_index == m_index && other.m_owner.Equals(m_owner));
            }
        }

        private static Dictionary<InventoryKey, InventoryListener> m_inventoryListeners = new Dictionary<InventoryKey, InventoryListener>();
        private static Dictionary<InventoryKey, InventoryListener> m_changedInventories = new Dictionary<InventoryKey, InventoryListener>();

        /// <summary>
        /// Register an inventory, needed for network syncing.
        /// </summary>
        /// <param name="inventory">inventory</param>
        /// <param name="netview">netview, usually the one from the parent object, e.g. character or chest.</param>
        /// <param name="index">index if there is more then one inventory on that netview</param>
        public static void Register(Inventory inventory, ZNetView netview, int index = 0)
        {
            if (netview == null)
                return;

            inventory.m_nview = netview;

            var zdo = netview.GetZDO();
            if (zdo == null)
            {
                ZLog.Log("Register Inventory, ZDO missing.");
                return;
            }

            var key = new InventoryKey(zdo.m_uid, index);

            if (m_inventoryListeners.Remove(key))
            {
                ZLog.Log("Register Inventory, key already exists, unregistering and registering new.");
            }

            m_inventoryListeners.Add(key, new InventoryListener(inventory, netview, index));
        }

        public static void UnregisterAll(ZNetView netview)
        {
            if (netview == null)
                return;
            var zdo = netview.GetZDO();
            if (zdo != null)
            {
                var id = zdo.m_uid;
                var inventories = GetInventories(id);
                foreach (var inv in inventories)
                {
                    var key = new InventoryKey(id, inv.Index);
                    m_inventoryListeners.Remove(key);
                    m_changedInventories.Remove(key);
                }
            }
        }

        public static void Unregister(ZNetView netview, int index = 0)
        {
            if (netview == null)
                return;
            var zdo = netview.GetZDO();
            if (zdo != null)
            {
                var id = zdo.m_uid;
                var key = new InventoryKey(id, index);
                m_inventoryListeners.Remove(key);
                m_changedInventories.Remove(key);
            }
        }

        public static void Unregister(Inventory inventory)
        {
            var key = new InventoryKey(inventory);
            m_inventoryListeners.Remove(key);
            m_changedInventories.Remove(key);
        }

        public static void Unregister(ZDOID id, int index = 0)
        {
            var key = new InventoryKey(id, index);
            m_inventoryListeners.Remove(key);
            m_changedInventories.Remove(key);
        }

        /// <summary>
        /// Get the inventory belonging to the ZDOID
        /// </summary>
        /// <param name="id"></param>
        /// <param name="index">Inventory index, in case an object contains more then one inventory object</param>
        /// <returns></returns>
        public static Inventory GetInventory(ZDOID id, int index = 0)
        {
            if (m_inventoryListeners.TryGetValue(new InventoryKey(id, index), out var inventoryWrapper))
            {
                return inventoryWrapper.Inventory;
            }
            return null;
        }

        /// <summary>
        /// Get all inventories on a ZDOID.
        /// 
        /// Loops through all inventories in order to find them, should be avoided if you know the index
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static List<InventoryListener> GetInventories(ZDOID id)
        {
            return m_inventoryListeners.Where(k => k.Key.m_owner == id).Select(k => k.Value).ToList();
        }


        private static float inventorySyncTimer = 0;

        [HarmonyPatch(typeof(Game), "Update")]
        [HarmonyPrefix]
        private static void Update(ref Game __instance)
        {
            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                // Done relatively frequently but if you pick up 100 items every frame one
                // it shouldnt be updating it every frame!
                if (Time.time - inventorySyncTimer > 0.05f)
                {
                    inventorySyncTimer = Time.time;
                    SyncAll();
                }
            }
        }

        public static void SyncAll()
        {
            var list = m_changedInventories.Values.ToList();
            foreach (var item in list)
            {
                item.SyncListeners();
            }

            m_changedInventories.Clear();
        }


        /// <summary>
        /// Yes, I actually patch Inventory in Inventory_Patch.cs
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="fromInventory"></param>
        /// <param name="item"></param>
        /// <returns></returns>

        [HarmonyPatch(typeof(Inventory), "MoveItemToThis", new[] { typeof(Inventory), typeof(ItemDrop.ItemData) })]
        [HarmonyPrefix]
        private static bool MoveItemToThis(Inventory __instance, Inventory fromInventory, ItemDrop.ItemData item)
        {
            if (ZNet.instance.IsServer())
                return true;

            var toId = __instance.m_nview.m_zdo.m_uid;
            var fromId = fromInventory.m_nview.m_zdo.m_uid;
            var itemId = item.m_id;
            ZNet.instance.GetServerRPC().Invoke("MoveItemToThis", toId, fromId, itemId);
            return false;
        }

        public static void RPC_MoveItemToThis(ZRpc rpc, ZDOID toId, ZDOID fromId, int itemId)
        {
            var toInv = GetInventory(toId);
            if (toInv == null)
            {
                ZLog.Log($"Missing to inventory RPC_MoveItemToThis toId:{toId} fromId:{fromId} itemId:{itemId} ");
                return;
            }
            var fromInv = GetInventory(fromId);
            if (fromInv == null)
            {
                ZLog.Log($"Missing from inventory RPC_MoveItemToThis toId:{toId} fromId:{fromId} itemId:{itemId} ");
                return;
            }
            var item = fromInv.m_inventory.SingleOrDefault(k => k.m_id == itemId);
            if (item == null)
            {
                ZLog.Log($"Missing item RPC_MoveItemToThis toId:{toId} fromId:{fromId} itemId:{itemId} ");
                return;
            }

            var peer = ZNet.instance.GetPeer(rpc);
            if (peer == null)
                return;
            if (!Inventory_Patch.IsListener(peer.m_uid, toInv))
            {
                ZLog.Log($"RPC_MoveItemToThis without being listener on the source container");
                return;
            }
            if (!Inventory_Patch.IsListener(peer.m_uid, fromInv))
            {
                ZLog.Log($"RPC_MoveItemToThis without being listener on the target container");
                return;
            }

            toInv.MoveItemToThis(fromInv, item);
        }

        [HarmonyPatch(typeof(Inventory), "Changed")]
        [HarmonyPostfix]
        private static void Changed(Inventory __instance)
        {
            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                // The client does not know what inventories contain unless they are listeners on it
                __instance.m_nview.m_zdo.Set("Inventory_NrOfItems" + __instance.m_inventoryIndex, __instance.m_inventory.Count);
            }
        }

        [HarmonyPatch(typeof(Inventory), "NrOfItems")]
        [HarmonyPrefix]
        private static bool NrOfItems(Inventory __instance, ref int __result)
        {
            if (ZNet.instance.IsServer() || __instance.m_inventory.Count > 0)
            {
                __result = __instance.m_inventory.Count;
            }
            else
            {
                if (__instance.m_nview != null && __instance.m_nview.m_zdo != null)
                {
                    __result = __instance.m_nview.m_zdo.GetInt("Inventory_NrOfItems" + __instance.m_inventoryIndex, 0);
                }
            }
            return false;
        }

        [HarmonyPatch(typeof(Inventory), "SlotsUsedPercentage")]
        [HarmonyPrefix]
        private static bool SlotsUsedPercentage(Inventory __instance, ref float __result)
        {
            __result = (float)__instance.NrOfItems() / (float)(__instance.m_width * __instance.m_height) * 100f;
            return false;
        }
    }
}

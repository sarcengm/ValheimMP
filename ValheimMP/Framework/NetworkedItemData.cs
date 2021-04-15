using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ValheimMP.Patches;

namespace ValheimMP.Framework
{
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

        m_customData = 1 << 10,

        // special flag set only when the item has been crafted for the first time
        // for triggers related to crafted item completion
        m_crafted = 1 << 11,
        m_craftedData = 1 << 12,
    }

    public class NetworkedItemData
    {
        internal int m_id;
        private string m_dropPrefab;
        private int m_stack;
        private float m_durability;
        private Vector2i m_gridPos;
        private int m_quality;
        private int m_variant;
        private long m_crafterID;
        private string m_crafterName = string.Empty;

        private Dictionary<int, byte[]> m_customData = new();

        private bool m_equiped;

        private InventoryManager m_inventoryManager;

        internal static Inventory m_tempInventory = new Inventory(null, null, 9999, 9999);

        public NetworkedItemData(InventoryManager inventoryManager)
        {
            m_inventoryManager = inventoryManager;
        }

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

            if (itemData.m_crafted != 0)
            {
                // this flag is useful only once, only the person who created it will first serialize it.
                pkg.Write(itemData.m_crafted);
                itemData.m_crafted = 0;
                flags |= NetworkedItemDataFlags.m_crafted;

                if (itemData.m_craftedData != null)
                {
                    pkg.Write(itemData.m_craftedData);
                    itemData.m_craftedData = null;
                    flags |= NetworkedItemDataFlags.m_craftedData;
                }
            }
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

            var customDataPos = pkg.GetPos();
            var customDataFields = 0;
            pkg.Write(customDataFields);

            foreach (var customData in itemData.m_customData)
            {
                if (!m_customData.TryGetValue(customData.Key, out var data) || !data.SequenceEqual(customData.Value))
                {
                    m_customData[customData.Key] = customData.Value;
                    pkg.Write(customData.Key);
                    pkg.Write(customData.Value);
                    customDataFields++;
                }
            }

            var customDataEndPos = pkg.GetPos();
            pkg.SetPos(customDataPos);

            if (customDataFields > 0)
            {
                flags |= NetworkedItemDataFlags.m_customData;
                pkg.Write(customDataFields);
                pkg.SetPos(customDataEndPos);
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
                if ((flags & NetworkedItemDataFlags.m_customData) == NetworkedItemDataFlags.m_customData) sb.Add($"m_customData");
                if ((flags & NetworkedItemDataFlags.m_crafted) == NetworkedItemDataFlags.m_crafted) sb.Add($"m_crafted");
                if ((flags & NetworkedItemDataFlags.m_craftedData) == NetworkedItemDataFlags.m_craftedData) sb.Add($"m_craftedData");
                ValheimMP.Log($"Serialize item {sb.Join()} ");
            }
#endif

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
            var craftTrigger = 0;
            byte[] triggerData = null;

            var itemData = targetInventory.m_inventory.SingleOrDefault(k => k.m_id == m_id);

            if ((flags & NetworkedItemDataFlags.m_crafted) == NetworkedItemDataFlags.m_crafted)
            {
                // this flag is useful only once, only the person who created it will first serialize it.
                craftTrigger = pkg.ReadInt();
                if ((flags & NetworkedItemDataFlags.m_craftedData) == NetworkedItemDataFlags.m_craftedData)
                {
                    triggerData = pkg.ReadByteArray();
                }
            }

            if ((flags & NetworkedItemDataFlags.m_dropPrefab) == NetworkedItemDataFlags.m_dropPrefab)
            {
                m_dropPrefab = pkg.ReadString();

                if (itemData != null)
                {
                    ValheimMP.Log("m_dropPrefab changed?");
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
                if ((flags & NetworkedItemDataFlags.m_customData) == NetworkedItemDataFlags.m_customData) sb.Add($"m_customData");
                if ((flags & NetworkedItemDataFlags.m_crafted) == NetworkedItemDataFlags.m_crafted) sb.Add($"m_crafted");
                if ((flags & NetworkedItemDataFlags.m_craftedData) == NetworkedItemDataFlags.m_craftedData) sb.Add($"m_craftedData");
                ValheimMP.Log($"Deserialize item {sb.Join()} ");
            }
#endif

            if (itemData == null &&
                // No point in creating an item just to destroy it right away!
                (flags & NetworkedItemDataFlags.m_destroy) != NetworkedItemDataFlags.m_destroy)
            {
                // we abuse an empty inventory so we can still use the additem command to create it
                // if we dont it will act all smart and stack our items!
                m_tempInventory.AddItem(m_dropPrefab, m_stack, m_durability, m_gridPos, m_equiped, m_quality, m_variant, m_crafterID, m_crafterName);
                if (m_tempInventory.m_inventory.Count > 0)
                {
                    itemData = m_tempInventory.m_inventory[0];
                    targetInventory.m_inventory.Add(itemData);
                    m_tempInventory.m_inventory.Clear();
                    itemData.m_id = m_id;
                    itemData.m_gridPos = m_gridPos;
                    itemData.m_equiped = m_equiped;
                    itemData.m_durability = m_durability;
                    itemData.m_stack = m_stack;
                }

            }

            // Little out of order here, but there is no need for these fields in the item creation so lets just serialize them straight into the object.
            // Should possibly re-order some others as well?
            if ((flags & NetworkedItemDataFlags.m_customData) == NetworkedItemDataFlags.m_customData)
            {
                var itemDataCount = pkg.ReadInt();

                for (int i = 0; i < itemDataCount; i++)
                {
                    var key = pkg.ReadInt();
                    var value = pkg.ReadByteArray();

                    if (itemData != null)
                    {
                        itemData.m_customData[key] = value;
                    }
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

            if (itemData != null && (flags & NetworkedItemDataFlags.m_crafted) == NetworkedItemDataFlags.m_crafted)
            {
                m_inventoryManager.Internal_OnItemCrafted(craftTrigger, targetInventory, itemData, triggerData);
            }

            if ((flags & NetworkedItemDataFlags.m_destroy) == NetworkedItemDataFlags.m_destroy)
            {
                if (itemData != null)
                {
                    if (itemData.m_equiped)
                    {
                        var humanoid = targetInventory.m_nview.GetComponent<Humanoid>();
                        if (humanoid != null)
                        {
                            Player_Patch.SuppressMessages = true;
                            Humanoid_Patch.LocalActionOnly = true;
                            humanoid.UnequipItem(itemData);
                            Humanoid_Patch.LocalActionOnly = false;
                            Player_Patch.SuppressMessages = false;
                        }
                    }
                    targetInventory.m_inventory.Remove(itemData);
                }
            }

            targetInventory.Changed();

            return itemData;
        }
    }
}

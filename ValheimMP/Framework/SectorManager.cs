using System;
using System.Collections.Generic;
using UnityEngine;
using ValheimMP.Patches;

namespace ValheimMP.Framework
{
    public class SectorManager
    {
        private static Dictionary<int, Dictionary<int, Sector>> m_objects = new Dictionary<int, Dictionary<int, Sector>>();
        private static List<Sector> m_sectors = new List<Sector>();

        public static List<Sector> GetSectors()
        {
            return m_sectors;
        }

        /// <summary>
        /// Add ZDO to sector
        /// </summary>
        /// <param name="zdo"></param>
        public static void AddObject(ZDO zdo)
        {
            if (!ValheimMP.IsDedicated)
                return;
            // Only add living objects, one that does not have a netview is not instantiated!
            if (zdo.m_nview == null)
                return;

            var obj = GetObjectOrCreate(zdo.m_sector.x, zdo.m_sector.y);
            obj.AddIndexed(zdo);
            if (obj.IsFrozen)
            {
                obj.FreezeObject(zdo);
            }
            //ValheimMP.Log($"Added object zdo.m_type: {zdo.m_type} name: {zdo.m_nview} sector: {zdo.m_sector}");
        }


        /// <summary>
        /// Get the full lists of objects for a sector
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static Sector GetObject(int x, int y)
        {
            if (m_objects.TryGetValue(x, out var ydic))
            {
                if (ydic.TryGetValue(y, out var obj))
                {
                    return obj;
                }
            }

            return null;
        }

        internal static Sector GetObjectOrCreate(int x, int y)
        {
            Sector obj;
            if (!m_objects.TryGetValue(x, out var ydic))
            {
                ydic = new Dictionary<int, Sector>();
                m_objects.Add(x, ydic);
            }

            if (!ydic.TryGetValue(y, out obj))
            {
                obj = new Sector(x, y);
                ydic.Add(y, obj);
            }

            return obj;
        }


        /// <summary>
        /// Remove ZDO from sector
        /// </summary>
        /// <param name="zdo"></param>
        /// <returns>True if the ZDO was removed</returns>
        public static void RemoveObject(ZDO zdo)
        {
            if (!ValheimMP.IsDedicated)
                return;
            // index gets set to -1 when removed so calling remove twice on the same object will result in this.
            if (zdo.m_listIndex < 0)
                return;

            if (m_objects.TryGetValue(zdo.m_sector.x, out var ydic))
            {
                if (ydic.TryGetValue(zdo.m_sector.y, out var obj))
                {
                    obj.RemoveIndexed(zdo);

                    // Clean up of empty sectors
                    if (obj.IsEmpty())
                    {
                        ydic.Remove(zdo.m_sector.y);

                        if (ydic.Count == 0)
                        {
                            m_objects.Remove(zdo.m_sector.x);
                        }

                        obj.Dispose();
                    }
                }
            }
        }

        internal static void RemoveSector(Sector obj)
        {
            if (m_objects.TryGetValue(obj.x, out var ydic))
            {
                ydic.Remove(obj.y);

                if (ydic.Count == 0)
                {
                    m_objects.Remove(obj.x);
                }

                obj.Dispose();
            }
        }

        public static int GetSectorCount()
        {
            return m_sectors.Count;
        }

        public static int FullyLoadedSectors()
        {
            return ZNetScene_Patch.m_fullyLoadedSectors.Count;
        }

        public class Sector : IDisposable
        {
            public List<ZDO> PendingObjects { get; private set; } = new List<ZDO>();
            public HashSet<long> ActivePeers { get; private set; } = new HashSet<long>();
            public bool PendingLoad { get; internal set; }
            public float LastActive { get; internal set; }

            public int x;
            public int y;

            private int m_listIndex;


            public Sector(int x, int y)
            {
                m_listIndex = m_sectors.Count;
                m_sectors.Add(this);
                this.x = x;
                this.y = y;
            }

            public void Dispose()
            {
                if (m_listIndex == -1)
                    return;
                m_sectors[m_listIndex] = m_sectors[m_sectors.Count - 1];
                m_sectors[m_listIndex].m_listIndex = m_listIndex;
                m_sectors.RemoveAt(m_sectors.Count - 1);
                m_listIndex = -1;
            }

            /// <summary>
            /// Solid objects, these objects only need to be send and checked once when a player enters a new sector
            /// For example trees, since they do not move, or basically do not do anything there is no reason to continues loop over all of them
            /// If something changes they are temporarily added to a list
            /// </summary>
            public List<ZDO> SolidObjectsList { get; private set; } = new List<ZDO>();

            /// <summary>
            /// Priority objects, these are basically all moving characters
            /// </summary>
            public List<ZDO> PriorityObjectsList { get; private set; } = new List<ZDO>();

            /// <summary>
            /// The rest ?
            /// </summary>
            public List<ZDO> DefaultObjectsList { get; private set; } = new List<ZDO>();

            public List<ZDO> NonNetworkedObjectsList { get; private set; } = new List<ZDO>();

            public bool PendingObjectsLoaded { get; internal set; }
            public bool IsFrozen { get; internal set; }
            public List<(ZDO, List<Behaviour>)> FrozenObjects { get; internal set; } = new();

            private List<ZDO> GetList(ZDO.ObjectType type)
            {
                switch (type)
                {
                    case (ZDO.ObjectType)(-1):
                        return NonNetworkedObjectsList;
                    case ZDO.ObjectType.Solid:
                    case ZDO.ObjectType.Terrain:
                        return SolidObjectsList;
                    case ZDO.ObjectType.Prioritized:
                        return PriorityObjectsList;
                    case ZDO.ObjectType.Default:
                    default:
                        return DefaultObjectsList;
                }
            }

            public bool Contains(ZDO zdo)
            {
                var list = GetList(zdo.m_type);
                return (zdo.m_listIndex > 0 && list.Count > zdo.m_listIndex && list[zdo.m_listIndex] == zdo);
            }

            internal void RemoveIndexed(ZDO zdo)
            {
                var list = GetList(zdo.m_type);
                if (list.Count > zdo.m_listIndex && list[zdo.m_listIndex] == zdo)
                {
                    list[zdo.m_listIndex] = list[list.Count - 1];
                    list[zdo.m_listIndex].m_listIndex = zdo.m_listIndex;
                    list.RemoveAt(list.Count - 1);
                    zdo.m_listIndex = -1;
                }
                else
                {
                    var foundIndex = list.FindIndex(k => k == zdo);
                    var foundIndex1 = NonNetworkedObjectsList.FindIndex(k => k == zdo);
                    var foundIndex2 = PriorityObjectsList.FindIndex(k => k == zdo);
                    var foundIndex3 = DefaultObjectsList.FindIndex(k => k == zdo);
                    var foundIndex4 = SolidObjectsList.FindIndex(k => k == zdo);
                    ValheimMP.Log($"RemoveIndexed item did not match index prefab: {zdo.m_prefab} index: {zdo.m_listIndex} foundIndex:{foundIndex} foundIndex1:{foundIndex1} foundIndex2:{foundIndex2} foundIndex3:{foundIndex3} foundIndex4:{foundIndex4} listprefab: {(list.Count > zdo.m_listIndex ? list[zdo.m_listIndex].m_prefab : -1)}");
                }
            }

            internal void AddIndexed(ZDO zdo)
            {
                var list = GetList(zdo.m_type);
                list.Add(zdo);
                zdo.m_listIndex = list.Count - 1;
            }

            internal bool IsEmpty()
            {
                return SolidObjectsList.Count == 0 && PriorityObjectsList.Count == 0 && DefaultObjectsList.Count == 0 && NonNetworkedObjectsList.Count == 0;
            }

            internal void FreezeObject(ZDO item)
            {
                if (item.m_nview && item.m_nview.gameObject && item.m_nview.gameObject.activeSelf)
                {
                    //ValheimMP.Log($"Freezing {item.m_nview}");
                    item.m_nview.gameObject.SetActive(false);

                    var behaviour = new List<Behaviour>();

                    var components = item.m_nview.GetComponents<Behaviour>();
                    for (int j = 0; j < components.Length; j++)
                    {
                        if (components[j] && components[j].enabled)
                        {
                            // Does this crash it? or doesn't it? 
                            components[j].enabled = false;
                            behaviour.Add(components[j]);
                        }
                    }

                    FrozenObjects.Add((item, behaviour));
                }
            }

            internal void UnfreezeObject((ZDO, List<Behaviour>) item)
            {
                if (item.Item1 != null && item.Item1.m_nview && item.Item1.m_nview.gameObject)
                {
                    item.Item1.m_nview.gameObject.SetActive(true);
                    for (int j = 0; j < item.Item2.Count; j++)
                    {
                        if (item.Item2[j])
                            item.Item2[j].enabled = true;
                    }
                }
            }

            internal void FreezeSector()
            {
                //ValheimMP.Log($"FreezeSector {x},{y}", IsFrozen? BepInEx.Logging.LogLevel.Error : BepInEx.Logging.LogLevel.Info);
                IsFrozen = true;
                for (int i = 0; i < NonNetworkedObjectsList.Count; i++)
                {
                    var item = NonNetworkedObjectsList[i];
                    FreezeObject(item);
                }
                for (int i = 0; i < PriorityObjectsList.Count; i++)
                {
                    var item = PriorityObjectsList[i];
                    FreezeObject(item);
                }
                for (int i = 0; i < SolidObjectsList.Count; i++)
                {
                    var item = SolidObjectsList[i];
                    FreezeObject(item);
                }
                for (int i = 0; i < DefaultObjectsList.Count; i++)
                {
                    var item = DefaultObjectsList[i];
                    FreezeObject(item);
                }
            }

            internal void UnfreezeSector()
            {
                //ValheimMP.Log($"UnfreezeSector {x},{y}", !IsFrozen ? BepInEx.Logging.LogLevel.Error : BepInEx.Logging.LogLevel.Info);
                IsFrozen = false;
                var list = FrozenObjects;
                for (int i = 0; i < list.Count; i++)
                {
                    var item = list[i];
                    UnfreezeObject(item);
                }

                list.Clear();
            }
        }
    }
}
